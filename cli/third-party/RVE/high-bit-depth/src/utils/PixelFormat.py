"""内部统一使用 RGB48；HDR 仅控制色彩语义，不决定传输位深。"""

import re
import subprocess
from functools import lru_cache

import numpy as np

FRAME_PROTOCOL = "rgb48-v1"


def to_uint16(image):
    image = np.asarray(image)
    if image.dtype == np.uint16:
        return np.ascontiguousarray(image)
    if image.dtype == np.uint8:
        return image.astype(np.uint16) * 257
    return np.ascontiguousarray(np.rint(np.clip(image.astype(np.float32), 0, 1) * 65535).astype(np.uint16))


def to_uint8_compat(image):
    """仅供旧 NCNN RIFE 字节接口使用；其他处理链不得调用。"""
    image = np.asarray(image)
    if image.dtype == np.uint8:
        return np.ascontiguousarray(image)
    if image.dtype == np.uint16:
        return np.ascontiguousarray(np.rint(image.astype(np.float32) / 257.0).astype(np.uint8))
    return np.ascontiguousarray(
        np.rint(np.clip(image.astype(np.float32), 0, 1) * 255).astype(np.uint8)
    )


def _ffmpeg_help(ffmpeg_path, *args):
    result = subprocess.run([str(ffmpeg_path), "-hide_banner", *args],
                            capture_output=True, text=True, encoding="utf-8", errors="replace",
                            creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0))
    return result.stdout + result.stderr


@lru_cache(maxsize=32)
def _encoder_formats(ffmpeg_path, encoder):
    match = re.search(r"Supported pixel formats: ([^\r\n]+)",
                      _ffmpeg_help(ffmpeg_path, "-h", f"encoder={encoder}"))
    return set(match.group(1).split()) if match else set()


@lru_cache(maxsize=8)
def _pixel_depths(ffmpeg_path):
    # 从 FFmpeg 描述符读取通道位深，兼容 RGB48、XYZ12 和浮点等命名。
    formats = {}
    for line in _ffmpeg_help(ffmpeg_path, "-pix_fmts").splitlines():
        match = re.match(r"^[IO.HPB.]{5}\s+(\S+)\s+\d+\s+\d+\s+([\d-]+)\s*$", line)
        if match:
            formats[match[1]] = min(int(part) for part in match[2].split("-"))
    return formats


def _video_option(token, names):
    # 所有入口均将视频映射到输出第 0 路，音频和其他视频流不参与本次决策。
    name, _, stream = token.partition(":")
    return name in names and stream in ("", "0", "v", "v:0")


def high_depth_encoder_args(args, ffmpeg_path, default_format="yuv420p10le"):
    args = list(args)
    start = max((i + 2 for i, token in enumerate(args[:-1]) if token == "-i"), default=0)
    formats = [i + 1 for i, token in enumerate(args[:-1])
               if i >= start and _video_option(token, ("-pix_fmt",))]
    requested = args[formats[-1]] if formats else default_format
    strict = requested.startswith("+")
    value = requested.lstrip("+")
    promoted = {"yuv420p": "yuv420p10le", "yuvj420p": "yuv420p10le",
                "yuv422p": "yuv422p10le", "yuv422": "yuv422p10le", "yuvj422p": "yuv422p10le",
                "yuv444p": "yuv444p10le", "yuv444": "yuv444p10le", "yuvj444p": "yuv444p10le",
                "rgb24": "gbrp16le", "bgr24": "gbrp16le", "gbrp": "gbrp10le",
                "gray": "gray16le", "nv12": "p010le"}
    value = promoted.get(value, value)
    if _pixel_depths(str(ffmpeg_path)).get(value, 0) < 10:
        raise ValueError(f"输出像素格式必须至少为 10bit：{value}")
    if formats:
        args[formats[-1]] = ("+" if strict else "") + value
    else:
        args.extend(["-pix_fmt", value])
    encoders = [args[i + 1] for i, token in enumerate(args[:-1])
                if i >= start and _video_option(token, ("-c", "-codec", "-vcodec"))]
    encoder = encoders[-1] if encoders else "libx264"
    if not encoders:
        args.extend(["-c:v", encoder])
    supported = _encoder_formats(str(ffmpeg_path), encoder)
    if value not in supported:
        raise ValueError(f"编码器 {encoder} 不支持 {value}；请选择支持对应位深的编码器和像素格式")
    return args
