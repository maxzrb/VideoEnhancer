"""提供有界内存的视频时间窗口和 FFmpeg 原始帧写入器。"""

from __future__ import annotations

import shlex
import subprocess
from dataclasses import dataclass
from typing import Iterator

from .utils.PixelFormat import high_depth_encoder_args, to_uint16

import cv2
import numpy as np


@dataclass(frozen=True)
class VideoWindow:
    frames: np.ndarray
    emit_start: int
    emit_end: int
    decoded: int
    expected: int


def split_encoder_args(value: str) -> list[str]:
    result = []
    for token in shlex.split(value.strip(), posix=False):
        if len(token) >= 2 and token[0] == token[-1] and token[0] in ('"', "'"):
            token = token[1:-1]
        result.append(token)
    return result


def iter_video_windows(path: str, window_size: int, context: int, ffmpeg_path: str = "ffmpeg") -> tuple[float, Iterator[VideoWindow]]:
    if window_size < 2 or context < 0 or window_size <= 2 * context:
        raise ValueError("时间窗口必须大于两侧上下文之和")
    capture = cv2.VideoCapture(path)
    if not capture.isOpened():
        raise RuntimeError("无法打开输入视频：" + path)
    fps = float(capture.get(cv2.CAP_PROP_FPS) or 24.0)
    expected = int(capture.get(cv2.CAP_PROP_FRAME_COUNT) or 0)
    width = int(capture.get(cv2.CAP_PROP_FRAME_WIDTH))
    height = int(capture.get(cv2.CAP_PROP_FRAME_HEIGHT))
    capture.release()
    frame_size = width * height * 6

    def generate() -> Iterator[VideoWindow]:
        decoder = subprocess.Popen([ffmpeg_path, "-hide_banner", "-loglevel", "error", "-i", path,
                                    "-map", "0:v:0", "-fps_mode", "passthrough", "-f", "rawvideo",
                                    "-pix_fmt", "rgb48le", "pipe:1"], stdout=subprocess.PIPE)
        buffer: list[np.ndarray] = []
        decoded = 0
        first = True
        try:
            while True:
                eof = False
                while len(buffer) < window_size + 1:
                    payload = decoder.stdout.read(frame_size)
                    if not payload:
                        eof = True
                        break
                    if len(payload) != frame_size:
                        raise RuntimeError("Truncated RGB48 frame from FFmpeg")
                    frame = np.frombuffer(payload, dtype=np.uint16).reshape(height, width, 3)
                    buffer.append(frame)
                    decoded += 1
                if not buffer:
                    break
                active = buffer if eof else buffer[:window_size]
                left = 0 if first else min(context, len(buffer))
                right = len(active) if eof else len(active) - context
                yield VideoWindow(np.stack(active), left, right, decoded, expected)
                first = False
                if eof:
                    break
                buffer = buffer[max(0, right - context):]
            if decoder.wait() != 0:
                raise RuntimeError("FFmpeg video decoding failed")
        finally:
            decoder.stdout.close()
            if decoder.poll() is None:
                decoder.terminate()
            decoder.wait()

    return fps, generate()


class RawVideoWriter:
    def __init__(self, ffmpeg_path: str, input_path: str, output_path: str,
                 custom_encoder: str, fps: float, width: int, height: int,
                 overwrite: bool):
        command = [
            ffmpeg_path, "-hide_banner", "-loglevel", "warning",
            "-f", "rawvideo", "-pix_fmt", "rgb48le", "-s:v", f"{width}x{height}",
            "-r", f"{fps:.8f}", "-i", "pipe:0", "-i", input_path,
            "-map", "0:v:0", "-map", "1:a?", "-map", "1:s?",
        ]
        command.extend(high_depth_encoder_args(split_encoder_args(custom_encoder), ffmpeg_path))
        if overwrite:
            command.append("-y")
        command.append(output_path)
        self.process = subprocess.Popen(command, stdin=subprocess.PIPE)
        if self.process.stdin is None:
            raise RuntimeError("无法打开 FFmpeg 输入管道")

    def write(self, frames: np.ndarray) -> None:
        for frame in frames:
            self.process.stdin.write(to_uint16(frame).tobytes())

    def close(self) -> None:
        if self.process.stdin is not None and not self.process.stdin.closed:
            self.process.stdin.close()
        code = self.process.wait()
        if code != 0:
            raise RuntimeError(f"FFmpeg 输出失败，退出码 {code}")

    def abort(self) -> None:
        try:
            if self.process.stdin is not None and not self.process.stdin.closed:
                self.process.stdin.close()
        except OSError:
            pass
        try:
            self.process.kill()
            self.process.wait(timeout=5)
        except (OSError, subprocess.TimeoutExpired):
            pass
