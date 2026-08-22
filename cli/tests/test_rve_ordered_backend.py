import importlib.util
import queue
import types
import unittest
from pathlib import Path


SCRIPT = Path(__file__).parents[1] / "embedded-tools" / "rve-ordered-backend.py"
SPEC = importlib.util.spec_from_file_location("rve_ordered_backend", SCRIPT)
ORDERED = importlib.util.module_from_spec(SPEC)
assert SPEC.loader is not None
SPEC.loader.exec_module(ORDERED)


class DummyFrame:
    def __init__(self, width, height, payload, hdr_mode=False):
        self.width = width
        self.height = height
        self.hdr_mode = hdr_mode
        self.backend = "ncnn"
        self.device = "cpu"
        self.gpu_id = 0
        self.dtype = "auto"
        self._bytes = payload

    def set_frame_bytes(self, payload):
        self._bytes = payload
        return self

    def get_frame_bytes(self):
        return self._bytes

    def get_dummy_frame(self):
        return DummyFrame(self.width, self.height, b"", self.hdr_mode)

    def resize_frame(self, width, height):
        bytes_per_pixel = 6 if self.hdr_mode else 3
        self.width = width
        self.height = height
        self._bytes = bytes(width * height * bytes_per_pixel)
        return self


class DummyInfo:
    def __init__(self):
        self.stopped = False
        self.preview = []
        self.rendered = []

    def get_is_paused(self):
        return False

    def stopWriting(self):
        self.stopped = True

    def setPreviewFrame(self, payload):
        self.preview.append(payload)

    def setFramesRendered(self, value):
        self.rendered.append(value)


class DummyReadBuffer:
    def __init__(self, frames):
        self.frames = list(frames)
        self.readQueue = queue.Queue()
        self.closed = False

    def get(self):
        return self.frames.pop(0)

    def close(self):
        self.closed = True


class DummyWriteBuffer:
    def __init__(self):
        self.writeQueue = queue.Queue()


def patched_render_class():
    module = types.SimpleNamespace()

    class SetupSceneDetect:
        def __init__(self, width, height):
            self.size = (width, height)

    class Render:
        def __init__(self):
            pass

        def setupInterpolate(self):
            self.setup_size = (self.width, self.height)
            self.sceneDetect = module.SceneDetect(width=self.width, height=self.height)

    module.Render = Render
    module.SceneDetect = SetupSceneDetect
    ORDERED._patch_render(module)
    return Render


def make_render(interpolate_result="frame", upscale_error=None):
    render_type = patched_render_class()
    render = render_type()
    source = DummyFrame(2, 1, bytes(6))
    render.width = 2
    render.height = 1
    render.modelScale = 2
    render.upscaleTimes = 2
    render.hdr_mode = False
    render.interpolateModel = "rife"
    render.upscaleModel = "2x"
    render.override_upscale_scale = None
    render.ceilInterpolateFactor = 2
    render.extraRestorationModels = []
    render.informationHandler = DummyInfo()
    render.readBuffer = DummyReadBuffer([source, None])
    render.writeBuffer = DummyWriteBuffer()
    calls = {}

    class SceneDetect:
        def detect(self, frame):
            calls["scene_size"] = (frame.width, frame.height)
            return True

    render.sceneDetect = SceneDetect()

    def upscale(frame):
        if upscale_error is not None:
            raise upscale_error
        # 模拟 NCNN：字节已放大，但 Frame 元数据仍是源尺寸。
        return DummyFrame(frame.width, frame.height, bytes(24))

    def interpolate(img1, transition):
        calls["interp_size"] = (img1.width, img1.height)
        calls["transition"] = transition
        if interpolate_result is None:
            return None
        return [DummyFrame(4, 2, b"i" * 24)]

    render.upscaleOption = upscale
    render.interpolateOption = interpolate
    return render, calls, source


class OrderedBackendTests(unittest.TestCase):
    def test_ncnn_metadata_is_fixed_before_interpolation(self):
        render, calls, _ = make_render()
        render.render()

        self.assertEqual((2, 1), calls["scene_size"])
        self.assertEqual((4, 2), calls["interp_size"])
        self.assertTrue(calls["transition"])
        self.assertTrue(render.informationHandler.stopped)
        self.assertIsNone(render._videoenhancer_render_error)
        self.assertEqual([b"i" * 24, bytes(24), None], self._drain(render.writeBuffer.writeQueue))

    def test_raw_bytes_are_wrapped_in_frame(self):
        render, _, source = make_render()
        frame = ORDERED._normalise_upscaled_frame(render, source, bytes(24))

        self.assertIsInstance(frame, DummyFrame)
        self.assertEqual((4, 2), (frame.width, frame.height))
        self.assertEqual(bytes(24), frame.get_frame_bytes())

    def test_missing_interpolated_first_frame_does_not_end_render(self):
        render, _, _ = make_render(interpolate_result=None)
        render.render()

        self.assertIsNone(render._videoenhancer_render_error)
        self.assertEqual([bytes(24), None], self._drain(render.writeBuffer.writeQueue))

    def test_render_error_is_recorded_and_unblocks_pipeline(self):
        render, _, _ = make_render(upscale_error=RuntimeError("upscale failed"))
        render.render()

        self.assertIsInstance(render._videoenhancer_render_error, RuntimeError)
        self.assertTrue(render.informationHandler.stopped)
        self.assertTrue(render.readBuffer.closed)
        self.assertEqual([None], self._drain(render.writeBuffer.writeQueue))

    def test_setup_splits_interpolator_and_scene_detector_sizes(self):
        render_type = patched_render_class()
        render = render_type()
        render.width = 1920
        render.height = 1080
        render.upscaleTimes = 2

        render.setupInterpolate()

        self.assertEqual((3840, 2160), render.setup_size)
        self.assertEqual((1920, 1080), render.sceneDetect.size)
        self.assertEqual((1920, 1080), (render.width, render.height))

    def test_hdr_payload_uses_six_bytes_per_pixel(self):
        render, _, source = make_render()
        render.hdr_mode = True
        source.hdr_mode = True
        frame = ORDERED._normalise_upscaled_frame(render, source, bytes(48))
        self.assertEqual((4, 2), (frame.width, frame.height))

        with self.assertRaisesRegex(ValueError, "expected 48"):
            ORDERED._normalise_upscaled_frame(render, source, bytes(24))

    @staticmethod
    def _drain(target):
        result = []
        while not target.empty():
            result.append(target.get_nowait())
        return result


if __name__ == "__main__":
    unittest.main()

