# SPDX-License-Identifier: AGPL-3.0-only
# Copyright (c) 2026 VideoEnhancer contributors
# 本脚本与 RVE 结合运行；完整许可见 ../third-party/RVE/AGPL-3.0.txt。
"""Independent still-image super-resolution entry point for RVE Backend."""

from __future__ import annotations

import argparse
import os
import re
import subprocess
import sys
import tempfile
import time
from pathlib import Path

import numpy as np
import cv2
from PIL import Image

BACKEND_ROOT = Path(__file__).resolve().parent
sys.path.insert(0, str(BACKEND_ROOT))
from src.utils.PixelFormat import to_uint16


def read_image(path: Path) -> np.ndarray:
    # 由 Python 处理 Unicode 文件名，OpenCV 只解码内存中的图像。
    return cv2.imdecode(np.frombuffer(path.read_bytes(), dtype=np.uint8), cv2.IMREAD_UNCHANGED)


SUPPORTED_IMAGES = {".png", ".jpg", ".jpeg", ".webp", ".bmp", ".tif", ".tiff", ".avif"}


def model_scale(path: Path) -> int:
    if path.is_dir() and all((path / name).is_file() for name in (
        "diffusion_pytorch_model_streaming_dmd.safetensors", "LQ_proj_in.ckpt", "TCDecoder.ckpt", "Wan2.1_VAE.pth")):
        return 4
    if path.is_dir() and (path / "config.py").is_file() and (path / "chkpts.pth").is_file():
        return 1
    if path.is_file() and "basicvsr" in path.name.lower() and path.suffix.lower() == ".pth":
        return 4
    name = path.stem.lower()
    match = re.search(r"(?:^|[_-])(\d+)x(?:[_-]|$)|(?:^|[_-])x(\d+)(?:[_-]|$)", name)
    if match:
        return int(match.group(1) or match.group(2))
    if path.is_dir():
        param = next(path.glob("*.param"), None)
        if param:
            text = param.read_text(encoding="utf-8", errors="ignore")
            match = re.search(r"\bPixelShuffle\b[^\r\n]*\b0=(\d+)", text)
            if match:
                return int(match.group(1))
    raise ValueError(f"无法从模型名称识别倍率，请在名称中包含 x2/x3/x4：{path.name}")


def collect_inputs(files: list[str], folders: list[str]) -> list[Path]:
    found: dict[str, Path] = {}
    for raw in files:
        path = Path(raw).resolve()
        if path.is_file() and path.suffix.lower() in SUPPORTED_IMAGES:
            found[str(path).lower()] = path
    for raw in folders:
        root = Path(raw).resolve()
        if root.is_dir():
            for path in root.rglob("*"):
                if path.is_file() and path.suffix.lower() in SUPPORTED_IMAGES:
                    found[str(path.resolve()).lower()] = path.resolve()
    return sorted(found.values(), key=lambda path: str(path).lower())


from src.ncnn.UpscaleFloat import NCNNImageUpscaler, tiled_rgb


def onnx_tiled(upscaler, image: np.ndarray, tile: int = 256, pad: int = 8) -> np.ndarray:
    return tiled_rgb(upscaler._run, image, int(upscaler.scale), tile, pad)




from rve_output_scale import resize_frame


class ImageUpscaler:
    def __init__(
        self,
        backend: str,
        model: Path,
        width: int,
        height: int,
        tile_size: int = 0,
        use_rve_ncnn: bool = False,
        native_scale: int = 0,
        output_scale: int = 0,
    ):
        self.backend, self.model_path = backend, model
        self.width, self.height = width, height
        self.output_scale = output_scale
        self.tile_size = max(0, int(tile_size))
        self.use_rve_ncnn = bool(use_rve_ncnn)
        try:
            self.scale = native_scale or model_scale(model)
        except ValueError:
            if backend != "cuda":
                raise
            self.scale = 0
        self.model = self._create()
        if backend in ("cuda", "tensorrt"):
            self.scale = int(self.model.getScale())

    def _create(self):
        if self.backend == "onnx":
            from src.onnx.UpscaleONNX import UpscaleONNX
            return UpscaleONNX(str(self.model_path), device="default", width=self.width,
                               height=self.height, scale=self.scale, tilesize=256, tile_pad=8)
        if self.backend in ("cuda", "tensorrt"):
            import torch
            from src.pytorch.UpscaleTorch import UpscalePytorch
            model = UpscalePytorch(str(self.model_path), device="cuda",
                                   precision=os.environ.get("VIDEOENHANCER_UPSCALE_PRECISION", "auto"),
                                   width=self.width, height=self.height, tilesize=256, tile_pad=10,
                                   backend="pytorch" if self.backend == "cuda" else "tensorrt")
            actual_dtype = model.dtype
            if self.backend == "cuda":
                try:
                    actual_dtype = next(model.upscale_model_wrapper.get_model().parameters()).dtype
                    model.dtype = actual_dtype
                except (AttributeError, StopIteration):
                    pass
            self.frame_precision = "float32" if actual_dtype == torch.float32 else "float16"
            return model
        if self.backend == "ncnn":
            model = next(self.model_path.glob("*.param")).with_suffix("") if self.model_path.is_dir() else self.model_path.with_suffix("")
            if not self.use_rve_ncnn:
                return NCNNImageUpscaler(model, self.scale)
            from src.ncnn.UpscaleNCNN import UpscaleNCNN
            return UpscaleNCNN(
                modelPath=str(model),
                num_threads=1,
                scale=self.scale,
                gpuid=0,
                width=self.width,
                height=self.height,
                tilesize=self.tile_size,
            )
        raise ValueError(f"不支持的图片推理后端：{self.backend}")

    def __call__(self, rgb: np.ndarray) -> np.ndarray:
        rgb = to_uint16(rgb)
        if self.backend == "onnx":
            return onnx_tiled(self.model, rgb)
        if self.backend == "ncnn":
            if not self.use_rve_ncnn:
                return self.model(rgb)
            return np.frombuffer(self.process_bytes(rgb.tobytes()), dtype=np.uint16).reshape(
                self.height * self.scale, self.width * self.scale, 3
            )
        from src.utils.Frame import Frame
        internal = "pytorch" if self.backend == "cuda" else self.backend
        frame = Frame(internal, self.width, self.height, "cuda", 0, False, self.frame_precision).set_frame_bytes(to_uint16(rgb).tobytes())
        result = self.model(frame)
        if self.output_scale:
            result = resize_frame(result, self.width * self.output_scale, self.height * self.output_scale)
        return np.frombuffer(result.get_frame_bytes(), dtype=np.uint16).reshape(
            result.height, result.width, 3)

    def process_bytes(self, payload: bytes) -> bytes:
        if self.backend != "ncnn" or not self.use_rve_ncnn:
            raise ValueError("process_bytes 仅用于分段视频的 RVE NCNN 优化路径")
        from src.utils.Frame import Frame
        frame = Frame(
            "ncnn", self.width, self.height, "cuda", 0, False, "float16"
        ).set_frame_bytes(payload)
        return self.model(frame).get_frame_bytes()


def run_checked(command: list[str], stage: str) -> None:
    process = subprocess.run(command, cwd=BACKEND_ROOT, text=True, encoding="utf-8", errors="replace",
                             stdout=subprocess.PIPE, stderr=subprocess.STDOUT, check=False)
    if process.returncode != 0:
        detail = "\n".join(line for line in process.stdout.splitlines()[-30:] if line.strip())
        raise RuntimeError(f"{stage}失败（退出码 {process.returncode}）：{detail}")


def temporal_upscale(source: Path, backend: str, model: Path, ffmpeg: Path, work: Path, native_scale: int = 0) -> np.ndarray:
    if not ffmpeg.is_file():
        raise FileNotFoundError(f"未找到 FFmpeg：{ffmpeg}")
    input_video, output_video, output_png = work / "input.mkv", work / "enhanced.mkv", work / "first.png"
    frames = 21 if backend == "flashvsr" else 4
    with Image.open(source) as opened:
        source_width, source_height = opened.size
    scale = native_scale or model_scale(model)
    target_scale = int(os.environ.get("VIDEOENHANCER_OUTPUT_SCALE", "0")) or scale
    print(f"IMAGE_STAGE|{source}|构造 {frames} 帧无损输入", flush=True)
    run_checked([str(ffmpeg), "-y", "-hide_banner", "-loglevel", "error", "-loop", "1", "-framerate", "1",
                 "-i", str(source), "-vf", "pad=max(iw\\,64):max(ih\\,64):0:0", "-frames:v", str(frames), "-c:v", "ffv1", "-level", "3",
                 "-pix_fmt", "gbrp16le", str(input_video)], "图片转单帧视频")
    encoder = "-c:v ffv1 -level 3 -pix_fmt gbrp16le"
    if backend == "flashvsr":
        command = [sys.executable, str(BACKEND_ROOT / "rve-flashvsr-backend.py"),
                   "--input", str(input_video), "--output", str(output_video), "--model-dir", str(model),
                   "--ffmpeg-path", str(ffmpeg), "--custom-encoder", encoder, "--scale", str(scale),
                   "--window-length", "21", "--window-context", "0", "--overwrite"]
    else:
        command = [sys.executable, str(BACKEND_ROOT / "rve-basicvsrpp-backend.py"),
                   "--input", str(input_video), "--output", str(output_video), "--model", str(model),
                   "--ffmpeg-path", str(ffmpeg), "--custom-encoder", encoder,
                   "--clip-length", "4", "--clip-context", "0", "--overwrite"]
    print(f"IMAGE_STAGE|{source}|执行 {backend} 时序超分", flush=True)
    run_checked(command, f"{backend} 图片超分")
    print(f"IMAGE_STAGE|{source}|提取目标第一帧", flush=True)
    run_checked([str(ffmpeg), "-y", "-hide_banner", "-loglevel", "error", "-i", str(output_video),
                 "-vf", f"crop={source_width * target_scale}:{source_height * target_scale}:0:0",
                 "-frames:v", "1", "-pix_fmt", "rgb48be", str(output_png)], "提取超分结果")
    value = read_image(output_png)
    return to_uint16(cv2.cvtColor(value, cv2.COLOR_BGR2RGB))


def unique_path(path: Path) -> Path:
    if not path.exists():
        return path
    index = 2
    while True:
        candidate = path.with_name(f"{path.stem}-{index}{path.suffix}")
        if not candidate.exists():
            return candidate
        index += 1


def output_path(source: Path, args, suffix_text: str) -> Path:
    directory = source.parent if args.output_original else Path(args.output).resolve()
    directory.mkdir(parents=True, exist_ok=True)
    extension = source.suffix.lower() if not args.png and source.suffix.lower() in (".png", ".tif", ".tiff") else ".png"
    return unique_path(directory / f"{source.stem}-{suffix_text}{extension}")


def save_image(value: np.ndarray, alpha: np.ndarray | None, target: Path) -> None:
    if target.suffix.lower() not in (".png", ".tif", ".tiff"):
        raise ValueError("High precision image output requires PNG or TIFF")
    value = to_uint16(value)
    image = cv2.cvtColor(value, cv2.COLOR_RGB2BGR)
    if alpha is not None:
        alpha = cv2.resize(to_uint16(alpha), (value.shape[1], value.shape[0]), interpolation=cv2.INTER_LINEAR)
        image = np.dstack((image, alpha))
    ok, encoded = cv2.imencode(target.suffix.lower(), image)
    if not ok:
        raise RuntimeError(f"高位深图片编码失败：{target}")
    target.write_bytes(encoded.tobytes())


def main() -> int:
    parser = argparse.ArgumentParser(description="RVE independent image super-resolution backend")
    parser.add_argument("--input", action="append", default=[])
    parser.add_argument("--folder", action="append", default=[])
    parser.add_argument("--output", default="")
    parser.add_argument("--output-original", action="store_true")
    parser.add_argument("--suffix", choices=("timestamp", "model"), default="timestamp")
    parser.add_argument("--png", action=argparse.BooleanOptionalAction, default=True)
    parser.add_argument("--backend", choices=("ncnn", "cuda", "tensorrt", "onnx", "flashvsr", "basicvsrpp"), required=True)
    parser.add_argument("--model", required=True)
    parser.add_argument("--native-scale", type=int, default=0)
    parser.add_argument("--output-scale", type=int, choices=range(1, 9), default=0)
    parser.add_argument("--ffmpeg-path", default="")
    args = parser.parse_args()

    sources = collect_inputs(args.input, args.folder)
    if not sources:
        raise ValueError("没有找到受支持的图片文件")
    if not args.output_original and not args.output:
        raise ValueError("未输出到原目录时必须指定输出文件夹")

    model_path = Path(args.model).resolve()
    ffmpeg = Path(args.ffmpeg_path).resolve() if args.ffmpeg_path else Path("ffmpeg.exe")
    started = time.monotonic()
    timestamp = time.strftime("%Y%m%d-%H%M%S")
    model_suffix = re.sub(r"[^0-9A-Za-z_.\u4e00-\u9fff-]+", "_", model_path.stem)
    cache: dict[tuple[int, int], ImageUpscaler] = {}
    total = len(sources)
    for index, source in enumerate(sources, 1):
        try:
            decoded = read_image(source)
            if decoded is None:
                raise RuntimeError(f"Failed to decode image without reducing precision: {source}")
            alpha = decoded[:, :, 3] if decoded.ndim == 3 and decoded.shape[2] == 4 else None
            if decoded.ndim == 2:
                rgb = cv2.cvtColor(decoded, cv2.COLOR_GRAY2RGB)
            else:
                rgb = cv2.cvtColor(decoded[:, :, :3], cv2.COLOR_BGR2RGB)
            rgb = to_uint16(rgb)
            height, width = rgb.shape[:2]
            if args.backend in ("flashvsr", "basicvsrpp"):
                work_root = os.environ.get("VIDEOENHANCER_WORK_DIR") or None
                with tempfile.TemporaryDirectory(
                    prefix="videoenhancer-image-", dir=work_root
                ) as temporary:
                    value = temporal_upscale(source, args.backend, model_path, ffmpeg, Path(temporary), args.native_scale)
            else:
                key = (width, height)
                if key not in cache:
                    cache[key] = ImageUpscaler(args.backend, model_path, width, height, native_scale=args.native_scale, output_scale=args.output_scale)
                upscaler = cache[key]
                value = upscaler(rgb)
                if args.backend in ("cuda", "tensorrt") and float(rgb.std()) > 5.0 * 257:
                    output_mean = float(value.mean())
                    if float(value.std()) < 257.0 or output_mean < 128.5 or output_mean > 65406.5:
                        raise RuntimeError("该模型对图片不兼容，请选择 NCNN/ONNX 模型")
            if args.output_scale and value.shape[:2] != (height * args.output_scale, width * args.output_scale):
                print(f"IMAGE_STAGE|{source}|原生推理后缩放至 {args.output_scale}x", flush=True)
                value = cv2.resize(value, (width * args.output_scale, height * args.output_scale), interpolation=cv2.INTER_LANCZOS4)
            target = output_path(source, args, timestamp if args.suffix == "timestamp" else model_suffix)
            save_image(value, alpha, target)
        except Exception as exc:
            raise RuntimeError(f"处理图片 {source} 失败：{type(exc).__name__}: {exc}") from exc
        elapsed = time.monotonic() - started
        eta = (elapsed / index) * (total - index)
        print(f"IMAGE_PROGRESS|{index}|{total}|{elapsed:.2f}|{eta:.2f}|{target}", flush=True)
    print(f"IMAGE_COMPLETE|{total}|{time.monotonic()-started:.2f}", flush=True)
    os._exit(0)


if __name__ == "__main__":
    try:
        exit_code = main()
        sys.stdout.flush()
        sys.stderr.flush()
        os._exit(exit_code)
    except Exception as exc:
        print(f"IMAGE_ERROR|{type(exc).__name__}: {exc}", file=sys.stderr, flush=True)
        os._exit(1)
