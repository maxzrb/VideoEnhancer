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
from PIL import Image

BACKEND_ROOT = Path(__file__).resolve().parent
sys.path.insert(0, str(BACKEND_ROOT))
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
    if path.suffix.lower() == ".engine":
        match = re.search(r"__scale-(\d+)(?:__|$)", name)
        if match:
            return int(match.group(1))
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


def reflect_pad(image: np.ndarray, tile: int, pad: int) -> tuple[np.ndarray, int, int]:
    height, width = image.shape[:2]
    extra_h = (tile - height % tile) % tile
    extra_w = (tile - width % tile) % tile
    mode = "reflect" if height > 1 and width > 1 else "edge"
    return np.pad(image, ((pad, extra_h + pad), (pad, extra_w + pad), (0, 0)), mode=mode), extra_h, extra_w


def tiled_rgb(run_block, image: np.ndarray, scale: int, tile: int, pad: int) -> np.ndarray:
    padded, extra_h, extra_w = reflect_pad(image, tile, pad)
    height, width = padded.shape[:2]
    output = np.zeros((height * scale, width * scale, 3), dtype=np.uint8)
    tiles_x = (width - 2 * pad + tile - 1) // tile
    tiles_y = (height - 2 * pad + tile - 1) // tile
    for y in range(tiles_y):
        for x in range(tiles_x):
            x0, y0 = x * tile + pad, y * tile + pad
            x1, y1 = min(x0 + tile, width - pad), min(y0 + tile, height - pad)
            px0, py0, px1, py1 = x0 - pad, y0 - pad, x1 + pad, y1 + pad
            block = padded[py0:py1, px0:px1]
            block_out = run_block(block)
            loss_x = block.shape[1] * scale - block_out.shape[1]
            loss_y = block.shape[0] * scale - block_out.shape[0]
            crop_x = (x0 - px0) * scale - max(0, loss_x // 2)
            crop_y = (y0 - py0) * scale - max(0, loss_y // 2)
            output[y0*scale:y1*scale, x0*scale:x1*scale] = block_out[
                crop_y:crop_y+(y1-y0)*scale, crop_x:crop_x+(x1-x0)*scale
            ]
    top = left = pad * scale
    return output[top:output.shape[0]-(extra_h+pad)*scale,
                  left:output.shape[1]-(extra_w+pad)*scale]


def onnx_tiled(upscaler, image: np.ndarray, tile: int = 256, pad: int = 8) -> np.ndarray:
    return tiled_rgb(upscaler._run, image, int(upscaler.scale), tile, pad)


class NCNNImageUpscaler:
    def __init__(self, model_base: Path, scale: int):
        import ncnn
        self.ncnn = ncnn
        self.scale = scale
        self.input_name, self.output_name = self._blob_names(model_base.with_suffix(".param"))
        self.net = ncnn.Net()
        self.net.opt.use_vulkan_compute = True
        self.net.opt.use_fp16_packed = True
        self.net.opt.use_fp16_storage = True
        self.net.opt.use_fp16_arithmetic = False
        self.net.set_vulkan_device(0)
        self.net.load_param(str(model_base.with_suffix(".param")))
        self.net.load_model(str(model_base.with_suffix(".bin")))
        print(f"Using GPU: {ncnn.get_gpu_device(0).info().device_name()}; NCNN blobs {self.input_name} -> {self.output_name}", flush=True)

    @staticmethod
    def _blob_names(param_path: Path) -> tuple[str, str]:
        input_name, output_name = "data", "output"
        for raw in param_path.read_text(encoding="utf-8", errors="ignore").splitlines():
            parts = raw.split()
            if len(parts) < 5 or parts[0].startswith("#"):
                continue
            try:
                input_count, output_count = int(parts[2]), int(parts[3])
            except ValueError:
                continue
            outputs = parts[4 + input_count:4 + input_count + output_count]
            if parts[0] == "Input" and outputs:
                input_name = outputs[0]
            if outputs:
                output_name = outputs[-1]
        return input_name, output_name

    def _run(self, rgb: np.ndarray) -> np.ndarray:
        height, width = rgb.shape[:2]
        mat = self.ncnn.Mat.from_pixels(np.ascontiguousarray(rgb), self.ncnn.Mat.PixelType.PIXEL_RGB, width, height)
        mat.substract_mean_normalize([], [1 / 255.0] * 3)
        extractor = self.net.create_extractor()
        input_result = extractor.input(self.input_name, mat)
        if input_result != 0:
            raise RuntimeError(f"NCNN 输入节点 {self.input_name} 返回错误码 {input_result}")
        result, output = extractor.extract(self.output_name)
        if result != 0:
            raise RuntimeError(f"NCNN 输出节点 {self.output_name} 返回错误码 {result}")
        value = np.asarray(output)
        if value.ndim != 3:
            raise RuntimeError(f"NCNN 输出形状异常：{value.shape}")
        return np.ascontiguousarray(np.clip(value.transpose(1, 2, 0) * 255.0, 0, 255).round().astype(np.uint8))

    def __call__(self, rgb: np.ndarray) -> np.ndarray:
        return tiled_rgb(self._run, rgb, self.scale, tile=256, pad=10)


class ImageUpscaler:
    def __init__(
        self,
        backend: str,
        model: Path,
        width: int,
        height: int,
        tile_size: int = 0,
        use_rve_ncnn: bool = False,
    ):
        self.backend, self.model_path = backend, model
        self.width, self.height = width, height
        self.tile_size = max(0, int(tile_size))
        self.use_rve_ncnn = bool(use_rve_ncnn)
        try:
            self.scale = model_scale(model)
        except ValueError:
            if backend != "cuda":
                raise
            self.scale = 0
        self.model = self._create()
        if backend == "cuda":
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
            if not self.use_rve_ncnn:
                model = self.model_path / self.model_path.name if self.model_path.is_dir() else self.model_path.with_suffix("")
                return NCNNImageUpscaler(model, self.scale)
            from src.ncnn.UpscaleNCNN import UpscaleNCNN
            model = self.model_path / self.model_path.name if self.model_path.is_dir() else self.model_path.with_suffix("")
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
        if self.backend == "onnx":
            return onnx_tiled(self.model, rgb)
        if self.backend == "ncnn":
            if not self.use_rve_ncnn:
                return self.model(rgb)
            return np.frombuffer(self.process_bytes(rgb.tobytes()), dtype=np.uint8).reshape(
                self.height * self.scale, self.width * self.scale, 3
            )
        from src.utils.Frame import Frame
        internal = "pytorch" if self.backend == "cuda" else self.backend
        frame = Frame(internal, self.width, self.height, "cuda", 0, False, self.frame_precision).set_frame_bytes(rgb.tobytes())
        result = self.model(frame)
        return np.frombuffer(result.get_frame_bytes(), dtype=np.uint8).reshape(
            self.height * self.scale, self.width * self.scale, 3)

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


def temporal_upscale(source: Path, backend: str, model: Path, ffmpeg: Path, work: Path) -> np.ndarray:
    if not ffmpeg.is_file():
        raise FileNotFoundError(f"未找到 FFmpeg：{ffmpeg}")
    input_video, output_video, output_png = work / "input.mkv", work / "enhanced.mkv", work / "first.png"
    frames = 21 if backend == "flashvsr" else 4
    with Image.open(source) as opened:
        source_width, source_height = opened.size
    scale = model_scale(model)
    print(f"IMAGE_STAGE|{source}|构造 {frames} 帧无损输入", flush=True)
    run_checked([str(ffmpeg), "-y", "-hide_banner", "-loglevel", "error", "-loop", "1", "-framerate", "1",
                 "-i", str(source), "-vf", "pad=max(iw\\,64):max(ih\\,64):0:0", "-frames:v", str(frames), "-c:v", "ffv1", "-level", "3",
                 "-pix_fmt", "rgb24", str(input_video)], "图片转单帧视频")
    encoder = "-c:v ffv1 -level 3 -pix_fmt rgb24"
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
                 "-vf", f"crop={source_width * scale}:{source_height * scale}:0:0",
                 "-frames:v", "1", str(output_png)], "提取超分结果")
    with Image.open(output_png) as opened:
        return np.asarray(opened.convert("RGB"))


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
    extension = ".png" if args.png else source.suffix.lower()
    return unique_path(directory / f"{source.stem}-{suffix_text}{extension}")


def save_image(value: np.ndarray, alpha: np.ndarray | None, target: Path) -> None:
    image = Image.fromarray(value, "RGB")
    if alpha is not None:
        image.putalpha(Image.fromarray(alpha, "L").resize(image.size, Image.Resampling.BILINEAR))
    params = {"compress_level": 4} if target.suffix.lower() == ".png" else {}
    if target.suffix.lower() in (".jpg", ".jpeg"):
        image = image.convert("RGB")
        params = {"quality": 95, "subsampling": 0}
    image.save(target, **params)


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
            with Image.open(source) as opened:
                alpha = np.asarray(opened.getchannel("A")) if "A" in opened.getbands() else None
                rgb = np.asarray(opened.convert("RGB"))
            height, width = rgb.shape[:2]
            if args.backend in ("flashvsr", "basicvsrpp"):
                work_root = os.environ.get("VIDEOENHANCER_WORK_DIR") or None
                with tempfile.TemporaryDirectory(
                    prefix="videoenhancer-image-", dir=work_root
                ) as temporary:
                    value = temporal_upscale(source, args.backend, model_path, ffmpeg, Path(temporary))
            else:
                key = (width, height)
                if key not in cache:
                    cache[key] = ImageUpscaler(args.backend, model_path, width, height)
                upscaler = cache[key]
                value = upscaler(rgb)
                if args.backend in ("cuda", "tensorrt") and float(rgb.std()) > 5.0:
                    output_mean = float(value.mean())
                    if float(value.std()) < 1.0 or output_mean < 0.5 or output_mean > 254.5:
                        raise RuntimeError("该模型对图片不兼容，请选择 NCNN/ONNX 模型")
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
