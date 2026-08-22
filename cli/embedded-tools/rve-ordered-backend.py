"""在同一 rve-backend 进程内执行先超分、再补帧的帧级管线。"""

import os
import runpy
import sys


def _patch_render(render_module):
    Render = render_module.Render
    original_setup_interpolate = Render.setupInterpolate

    def setup_interpolate_at_upscaled_size(self):
        # RIFE 等补帧模型按初始化时的宽高分配缓存；先超分时必须使用超分后的尺寸。
        original_width, original_height = self.width, self.height
        self.width = original_width * self.upscaleTimes
        self.height = original_height * self.upscaleTimes
        try:
            original_setup_interpolate(self)
        finally:
            self.width, self.height = original_width, original_height

    def render_upscale_first(self):
        frames_rendered = 0
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

            if self.upscaleModel:
                frame = self.upscaleOption(frame)
                if self.override_upscale_scale:
                    frame.resize_frame(
                        self.width * self.override_upscale_scale,
                        self.height * self.override_upscale_scale,
                    )

            if self.interpolateModel:
                scene_detect = self.sceneDetect.detect(frame)
                interpolated_frames = self.interpolateOption(
                    img1=frame,
                    transition=scene_detect,
                )
                if not interpolated_frames:
                    return
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

        self.writeBuffer.writeQueue.put(None)

    Render.setupInterpolate = setup_interpolate_at_upscaled_size
    Render.render = render_upscale_first


def main():
    backend_dir = os.environ.get("VIDEOENHANCER_BACKEND_DIR", "")
    if not backend_dir:
        raise RuntimeError("VIDEOENHANCER_BACKEND_DIR 未设置")
    backend_dir = os.path.abspath(backend_dir)
    if backend_dir not in sys.path:
        sys.path.insert(0, backend_dir)

    from src import RenderVideo

    _patch_render(RenderVideo)
    backend_script = os.path.join(backend_dir, "rve-backend.py")
    runpy.run_path(backend_script, run_name="__main__")


if __name__ == "__main__":
    main()
