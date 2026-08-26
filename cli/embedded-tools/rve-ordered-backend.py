"""在同一 rve-backend 进程内执行先超分、再补帧的帧级管线。"""

import os
import queue
import runpy
import sys
import traceback


def _normalise_upscaled_frame(render, source_frame, frame):
    """把各超分后端的返回值统一为尺寸正确的 Frame。"""
    width = render.width * render.modelScale
    height = render.height * render.modelScale

    if isinstance(frame, bytes):
        frame_object = source_frame.get_dummy_frame()
        frame_object.width = width
        frame_object.height = height
        frame_object.set_frame_bytes(frame)
        frame = frame_object
    else:
        frame.width = width
        frame.height = height

    payload = getattr(frame, "_bytes", None)
    if payload is not None:
        bytes_per_pixel = 6 if render.hdr_mode else 3
        expected = width * height * bytes_per_pixel
        if len(payload) != expected:
            raise ValueError(
                f"Upscaler returned {len(payload)} bytes, expected {expected} "
                f"for {width}x{height} ({bytes_per_pixel} bytes per pixel)"
            )
    return frame


def _align_interpolation_dtype(render, frame):
    """跨模型精度不同时，把超分结果转换为插帧器实际使用的 dtype。"""
    target_dtype = getattr(render.interpolateOption, "dtype", None)
    if target_dtype is None or not hasattr(frame, "get_frame_tensor"):
        return frame
    tensor = frame.get_frame_tensor()
    if getattr(tensor, "dtype", None) == target_dtype:
        return frame
    frame.dtype = str(target_dtype).replace("torch.", "")
    return frame.set_frame_tensor(tensor.to(dtype=target_dtype))


class _PrecisionAlignedUpscaler:
    """在进入超分模型前按模型实际 dtype 转换，避免组合阶段共享精度。"""

    def __init__(self, inner):
        self._inner = inner

    def __getattr__(self, name):
        return getattr(self._inner, name)

    def __call__(self, frame):
        target_dtype = getattr(self._inner, "dtype", None)
        if target_dtype is not None and hasattr(frame, "get_frame_tensor"):
            tensor = frame.get_frame_tensor()
            if getattr(tensor, "dtype", None) != target_dtype:
                frame.set_frame_tensor(tensor.to(dtype=target_dtype))
            frame.dtype = str(target_dtype).replace("torch.", "")
        return self._inner(frame)


def _stop_after_render_error(render):
    """解除 RVE 三条工作线程的阻塞，让主线程能够报告失败。"""
    try:
        render.informationHandler.stopWriting()
    except Exception:
        pass
    try:
        render.readBuffer.close()
    except Exception:
        pass
    try:
        while True:
            render.readBuffer.readQueue.get_nowait()
    except (AttributeError, queue.Empty):
        pass
    try:
        while True:
            render.writeBuffer.writeQueue.get_nowait()
    except (AttributeError, queue.Empty):
        pass


def _patch_render(render_module, instance_holder=None):
    Render = render_module.Render
    original_init = Render.__init__
    original_setup_upscale = getattr(Render, "setupUpscale", None)
    original_setup_interpolate = Render.setupInterpolate
    process_order = os.environ.get("VIDEOENHANCER_PROCESS_ORDER", "upscale-first")
    upscale_precision = os.environ.get("VIDEOENHANCER_UPSCALE_PRECISION", "auto")
    interp_precision = os.environ.get("VIDEOENHANCER_INTERP_PRECISION", "auto")

    def capture_instance(self, *args, **kwargs):
        if "precision" in kwargs:
            kwargs["precision"] = upscale_precision if process_order == "upscale-first" else interp_precision
        original_init(self, *args, **kwargs)
        if instance_holder is not None:
            instance_holder["render"] = self

    def setup_upscale_with_precision(self):
        previous_precision = getattr(self, "precision", "auto")
        self.precision = upscale_precision
        try:
            if original_setup_upscale is None:
                return
            original_setup_upscale(self)
            self.upscaleOption = _PrecisionAlignedUpscaler(self.upscaleOption)
        finally:
            self.precision = previous_precision

    def setup_interpolate_at_upscaled_size(self):
        # 先超后补时 RIFE 按超分后的最终尺寸分配缓存；先补后超保持源尺寸。
        original_width, original_height = self.width, self.height
        original_scene_detect = getattr(render_module, "SceneDetect", None)

        if process_order == "upscale-first" and original_scene_detect is not None:
            def source_sized_scene_detect(*args, **kwargs):
                kwargs["width"] = original_width
                kwargs["height"] = original_height
                return original_scene_detect(*args, **kwargs)

            render_module.SceneDetect = source_sized_scene_detect

        if process_order == "upscale-first":
            self.width = original_width * self.upscaleTimes
            self.height = original_height * self.upscaleTimes
        previous_precision = getattr(self, "precision", "auto")
        self.precision = interp_precision
        try:
            original_setup_interpolate(self)
        finally:
            self.precision = previous_precision
            self.width, self.height = original_width, original_height
            if process_order == "upscale-first" and original_scene_detect is not None:
                render_module.SceneDetect = original_scene_detect

    def render_upscale_first(self):
        frames_rendered = 0
        self._videoenhancer_render_error = None
        try:
            while True:
                if self.informationHandler.get_is_paused():
                    from time import sleep
                    sleep(1)
                    continue

                frame = self.readBuffer.get()
                if frame is None:
                    self.informationHandler.stopWriting()
                    break

                for extra_restoration in self.extraRestorationModels:
                    frame = extra_restoration(frame)

                # 与 RVE 原生顺序一致：转场检测使用源尺寸帧，避免超分后尺寸和模型缓存错配。
                scene_detect = False
                if self.interpolateModel:
                    scene_detect = self.sceneDetect.detect(frame)

                if self.upscaleModel:
                    source_frame = frame
                    frame = self.upscaleOption(frame)
                    frame = _normalise_upscaled_frame(self, source_frame, frame)
                    if self.override_upscale_scale:
                        frame.resize_frame(
                            self.width * self.override_upscale_scale,
                            self.height * self.override_upscale_scale,
                        )

                if self.interpolateModel:
                    frame = _align_interpolation_dtype(self, frame)
                    interpolated_frames = self.interpolateOption(
                        img1=frame,
                        transition=scene_detect,
                    )
                    if interpolated_frames is not None:
                        for interpolated_frame in interpolated_frames:
                            frame_bytes = (
                                interpolated_frame.get_frame_bytes()
                                if type(interpolated_frame) != bytes
                                else interpolated_frame
                            )
                            self.informationHandler.setPreviewFrame(frame_bytes)
                            self.informationHandler.setFramesRendered(frames_rendered)
                            self.writeBuffer.writeQueue.put(frame_bytes)

                frame_bytes = frame.get_frame_bytes() if type(frame) != bytes else frame
                self.informationHandler.setFramesRendered(frames_rendered)
                self.informationHandler.setPreviewFrame(frame_bytes)
                self.writeBuffer.writeQueue.put(frame_bytes)
                frames_rendered += int(self.ceilInterpolateFactor)
        except BaseException as exc:
            self._videoenhancer_render_error = exc
            traceback.print_exc()
            _stop_after_render_error(self)
        finally:
            self.writeBuffer.writeQueue.put(None)

    Render.__init__ = capture_instance
    if original_setup_upscale is not None:
        Render.setupUpscale = setup_upscale_with_precision
    Render.setupInterpolate = setup_interpolate_at_upscaled_size
    if process_order == "upscale-first":
        Render.render = render_upscale_first


def main():
    backend_dir = os.environ.get("VIDEOENHANCER_BACKEND_DIR", "")
    if not backend_dir:
        raise RuntimeError("VIDEOENHANCER_BACKEND_DIR 未设置")
    backend_dir = os.path.abspath(backend_dir)
    if backend_dir not in sys.path:
        sys.path.insert(0, backend_dir)

    from src import RenderVideo

    instance_holder = {}
    _patch_render(RenderVideo, instance_holder)
    backend_script = os.path.join(backend_dir, "rve-backend.py")
    runpy.run_path(backend_script, run_name="__main__")

    render = instance_holder.get("render")
    if render is not None:
        render.renderThread.join()
        if getattr(render, "_videoenhancer_render_error", None) is not None:
            try:
                render.ffmpegWriteThread.join(timeout=15)
            except Exception:
                pass
            print("VIDEOENHANCER_FATAL: upscale-first render thread failed", file=sys.stderr)
            sys.stdout.flush()
            sys.stderr.flush()
            os._exit(1)


if __name__ == "__main__":
    main()
