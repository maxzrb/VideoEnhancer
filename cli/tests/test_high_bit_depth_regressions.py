"""验证 RGB48 协议、编码选项和中文图片；使用项目内的隔离后端运行。"""

import importlib.util
import os
from pathlib import Path
import sys
import tempfile
import unittest

BACKEND = os.environ.get("VIDEOENHANCER_TEST_RVE_BACKEND")
FFMPEG = os.environ.get("VIDEOENHANCER_TEST_FFMPEG")
if BACKEND:
    sys.path.insert(0, BACKEND)


@unittest.skipUnless(BACKEND and FFMPEG, "需要隔离后端和 FFmpeg 路径")
class HighBitDepthRegressions(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        import numpy as np
        from src.utils import PixelFormat
        cls.np, cls.pixel = np, PixelFormat
        spec = importlib.util.spec_from_file_location("image_backend_rgb48", Path(BACKEND) / "rve-image-backend.py")
        cls.image = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(cls.image)

    def test_video_stream_specifiers_and_last_option(self):
        for codec in ("-c", "-c:0", "-codec:0", "-c:v:0", "-codec:v:0", "-vcodec"):
            for pixel in ("-pix_fmt", "-pix_fmt:0", "-pix_fmt:v", "-pix_fmt:v:0"):
                args = [codec, "ffv1", pixel, "+gbrp16le", "-c:a", "copy"]
                self.assertEqual(self.pixel.high_depth_encoder_args(args, FFMPEG), args)
        args = ["-c:v", "libx264", "-c:0", "ffv1", "-pix_fmt:v", "yuv420p10le", "-pix_fmt:0", "gbrp16le"]
        self.assertEqual(self.pixel.high_depth_encoder_args(args, FFMPEG), args)

    def test_explicit_high_depth_format_is_preserved(self):
        for codec, pixel in (("ffv1", "yuv444p12le"), ("libsvtav1", "yuv420p10le"), ("png", "rgb48be")):
            args = ["-c:v", codec, "-pix_fmt:v", pixel]
            self.assertEqual(self.pixel.high_depth_encoder_args(args, FFMPEG), args)

    def test_video_summary_uses_channel_depth_from_actual_format(self):
        from src.utils.VideoInfo import FFMpegInfoWrapper
        info = FFMpegInfoWrapper.__new__(FFMpegInfoWrapper)
        info.ffmpeg_path = FFMPEG
        info.ffmpeg_output_stripped = ""
        for pixel, depth in (("yuv420p", 8), ("yuv420p10le", 10), ("yuv444p12le", 12),
                             ("gbrp16le", 16), ("rgb48be", 16), ("rgba64be", 16), ("gray16le", 16)):
            info.stream_line = f"Stream #0:0: Video: rawvideo, {pixel}, 64x64"
            self.assertEqual(info.get_bit_depth(), depth, pixel)

    def test_input_format_is_not_promoted_as_output(self):
        args = ["-pix_fmt", "rgb48le", "-i", "pipe:0", "-c:v", "ffv1", "-pix_fmt:v", "gbrp16le"]
        self.assertEqual(self.pixel.high_depth_encoder_args(args, FFMPEG), args)

    def test_ncnn_rife_compatibility_is_explicitly_eight_bit(self):
        np = self.np
        rgb48 = np.array([0, 257, 32768, 32896, 65535], dtype=np.uint16)
        rgb24 = self.pixel.to_uint8_compat(rgb48)
        np.testing.assert_array_equal(rgb24, np.array([0, 1, 128, 128, 255], dtype=np.uint8))
        restored = self.pixel.to_uint16(rgb24)
        np.testing.assert_array_equal(restored, np.array([0, 257, 32896, 32896, 65535], dtype=np.uint16))

    def test_unicode_rgba_16bit_roundtrip(self):
        np = self.np
        with tempfile.TemporaryDirectory(dir=Path(BACKEND).parent, prefix="中文 图片-") as folder:
            target = Path(folder) / "输出 图片.png"
            rgb = np.arange(48, dtype=np.uint16).reshape(4, 4, 3) * 1000
            alpha = np.arange(16, dtype=np.uint16).reshape(4, 4) * 4000
            self.image.save_image(rgb, alpha, target)
            decoded = self.image.read_image(target)
            self.assertTrue(target.exists())
            self.assertEqual(decoded.dtype, np.uint16)
            np.testing.assert_array_equal(decoded[:, :, :3][:, :, ::-1], rgb)
            np.testing.assert_array_equal(decoded[:, :, 3], alpha)

    def test_source_format_falls_back_to_png_for_jpeg(self):
        from types import SimpleNamespace
        with tempfile.TemporaryDirectory(dir=Path(BACKEND).parent) as folder:
            args = SimpleNamespace(output_original=False, output=folder, png=False)
            self.assertEqual(self.image.output_path(Path("中文.jpg"), args, "model").suffix, ".png")
            self.assertEqual(self.image.output_path(Path("中文.tiff"), args, "model").suffix, ".tiff")

    def test_ncnn_borrowed_input_lives_until_extraction(self):
        import weakref
        from types import SimpleNamespace
        from src.ncnn.UpscaleFloat import NCNNImageUpscaler
        np = self.np
        class BorrowedMat:
            def __init__(self, array):
                self.value = weakref.ref(array)
        class Extractor:
            def input(self, name, mat):
                self.mat = mat
                return 0
            def extract(self, name):
                value = self.mat.value()
                if value is None:
                    raise RuntimeError("NCNN 输入缓冲区已提前释放")
                return 0, value.copy()
        model = NCNNImageUpscaler.__new__(NCNNImageUpscaler)
        model.ncnn = SimpleNamespace(Mat=BorrowedMat)
        model.net = SimpleNamespace(create_extractor=Extractor)
        model.input_name, model.output_name = "input", "output"
        source = np.arange(48, dtype=np.uint16).reshape(4, 4, 3) * 1000
        np.testing.assert_array_equal(model._run(source), source)

    def test_scene_thresholds_keep_their_original_scale(self):
        from src.utils.SceneDetect import SceneDetect
        from src.utils.Frame import Frame
        for method in ("mean", "mean_diff", "mean_segmented", "pyscenedetect"):
            detector = SceneDetect(sceneChangeMethod=method, sceneChangeSensitivity=4)
            result = []
            for level in (100, 101, 255):
                frame = Frame("onnx", 32, 32, "cpu", 0, False, "float32").set_frame_np(
                    self.np.full((32, 32, 3), level, self.np.uint8))
                result.append(bool(detector.detect(frame)))
            self.assertFalse(result[1], method)
            self.assertTrue(result[2], method)

    def test_resized_frame_uses_current_tensor_dimensions(self):
        from src.utils.Frame import Frame
        frame = Frame("pytorch", 4, 2, "cpu", 0, False, "float32").set_frame_np(
            self.np.full((2, 4, 3), 65535, self.np.uint16))
        frame.resize_frame(8, 4)
        tensor = frame.get_frame_tensor()
        self.assertEqual(tuple(tensor.shape), (1, 3, 4, 8))
        self.assertEqual(float(tensor.min()), 1.0)

    def test_known_tensorrt_fp16_models_fall_back_to_fp32(self):
        from src.pytorch.UpscaleTorch import _resolve_upscale_precision
        for name in (
            "PTH/AniSD-DC-DAT2-97500-2x.pth",
            "ONNX/AniSD-DC-DAT2-97500-fp32FO-2x.onnx",
            "PTH/AniToon-RPLKSRL-280K-2x.pth",
            "PTH/APISR-GRL-GAN-generator-4x.pth",
        ):
            self.assertEqual(_resolve_upscale_precision(name, "tensorrt", "float16"), "float32")
        self.assertEqual(
            _resolve_upscale_precision("PTH/Ani4K-Compact-35000-2x.pth", "tensorrt", "float16"),
            "float16",
        )

    def test_fp32_frame_roundtrip_preserves_all_65536_codes(self):
        from src.utils.Frame import Frame
        np = self.np
        source = np.arange(65536, dtype=np.uint16).reshape(256, 256, 1).repeat(3, axis=2)
        frame = Frame("pytorch", 256, 256, "cpu", 0, False, "float32").set_frame_bytes(source.tobytes())
        tensor = frame.get_frame_tensor()
        output = frame.get_dummy_frame().set_frame_tensor(tensor).get_frame_bytes()
        self.assertEqual(output, source.tobytes())

    def test_real_onnx_tiling_retains_4096_levels(self):
        import onnx
        from onnx import helper, TensorProto, numpy_helper
        from src.onnx.UpscaleONNX import UpscaleONNX
        np = self.np
        with tempfile.TemporaryDirectory(dir=Path(BACKEND).parent) as folder:
            path = Path(folder) / "2x_identity.onnx"
            graph = helper.make_graph(
                [helper.make_node("Resize", ["input", "", "scales"], ["output"], mode="nearest",
                                  coordinate_transformation_mode="asymmetric", nearest_mode="floor")],
                "rgb48", [helper.make_tensor_value_info("input", TensorProto.FLOAT, [1, 3, "h", "w"])],
                [helper.make_tensor_value_info("output", TensorProto.FLOAT, [1, 3, "oh", "ow"])],
                [numpy_helper.from_array(np.array([1, 1, 2, 2], np.float32), "scales")])
            model = helper.make_model(graph, opset_imports=[helper.make_opsetid("", 18)])
            model.ir_version = 9
            onnx.save(model, path)
            source = np.rint(np.linspace(0, 65535, 4096)).astype(np.uint16).reshape(64, 64, 1).repeat(3, axis=2)
            upscaler = UpscaleONNX(str(path), device="cpu", width=64, height=64, tilesize=16)
            output = np.frombuffer(upscaler(source), dtype=np.uint16).reshape(128, 128, 3)
            np.testing.assert_array_equal(output, source.repeat(2, axis=0).repeat(2, axis=1))
            self.assertEqual(np.unique(output[:, :, 0]).size, 4096)


if __name__ == "__main__":
    unittest.main()
