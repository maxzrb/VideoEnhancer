"""Build a portable Torch-TensorRT engine from a PyTorch SR checkpoint.

NCNN .param/.bin folders are accepted as input only when a matching .pth is
present beside the folder; NCNN weights themselves cannot be converted to
Torch-TensorRT without reconstructing the original network.
"""
import argparse
import os
import re
import sys

import torch
import torch.nn.functional as F
import torch_tensorrt

from src.pytorch.UpscaleModelWrapper import UpscaleModelWrapper
from src.pytorch.TensorRTHandler import TorchTensorRTHandler


class FinalOutputScale(torch.nn.Module):
    """Apply Real-ESRGAN-style final outscale inside the exported graph."""

    def __init__(self, model, output_height, output_width):
        super().__init__()
        self.model = model
        self.output_size = (output_height, output_width)

    def forward(self, x):
        output = self.model(x)
        # TensorRT cannot lower PyTorch's adaptive-area path for a non-integer
        # ratio such as native x4 -> x3. Bicubic maps to TensorRT Resize and
        # remains close to Real-ESRGAN's final high-quality resize step.
        return F.interpolate(
            output, size=self.output_size, mode="bicubic", align_corners=False
        )


def resolve_checkpoint(path):
    path = os.path.abspath(path)
    if os.path.isfile(path):
        if os.path.splitext(path)[1].lower() == ".pth":
            return path
        raise ValueError("TensorRT conversion needs a .pth checkpoint")
    if os.path.isdir(path):
        base = os.path.basename(os.path.normpath(path))
        for candidate in (os.path.join(os.path.dirname(path), base + ".pth"),
                          os.path.join(path, base + ".pth")):
            if os.path.isfile(candidate):
                return candidate
        raise FileNotFoundError("NCNN .param/.bin folder has no matching .pth checkpoint: " + path)
    raise FileNotFoundError(path)


def main():
    p = argparse.ArgumentParser(description="Convert a PyTorch/Real-ESRGAN checkpoint to a TensorRT engine")
    p.add_argument("input", help=".pth file or an NCNN folder with a matching .pth")
    p.add_argument("-o", "--output", help="output .engine path")
    p.add_argument("--output-dir", help="directory for the generated .engine file")
    p.add_argument("--width", type=int, default=1920)
    p.add_argument("--height", type=int, default=1080)
    p.add_argument(
        "--output-scale",
        type=int,
        help="final integer output scale; embeds the final resize when it differs from the native model scale",
    )
    p.add_argument("--tile-size", type=int, default=0,
                   help="content tile size; builds the engine for tile plus padding when nonzero")
    p.add_argument("--tile-pad", type=int, default=10,
                   help="padding around each content tile")
    p.add_argument("--precision", choices=("fp16", "fp32"), default="fp16",
                   help="TensorRT input precision")
    p.add_argument("--fp32", action="store_true", help="build FP32 instead of FP16")
    p.add_argument("--optimization-level", type=int, default=3)
    args = p.parse_args()

    if args.output and args.output_dir:
        p.error("--output and --output-dir cannot be used together")

    checkpoint = resolve_checkpoint(args.input)
    if not torch.cuda.is_available():
        raise RuntimeError("CUDA is required to build a TensorRT engine")
    device = torch.device("cuda:0")
    if args.tile_size < 0 or args.tile_pad < 0:
        raise ValueError("--tile-size and --tile-pad must be non-negative")
    if args.precision == "fp32":
        args.fp32 = True
    if args.tile_size > 0:
        args.width = min(args.width, args.tile_size + 2 * args.tile_pad)
        args.height = min(args.height, args.tile_size + 2 * args.tile_pad)
    dtype = torch.float32 if args.fp32 else torch.float16
    print("VIDEOENHANCER_TRT_PROGRESS|TensorRT Engine|5|加载模型", flush=True)
    wrapper = UpscaleModelWrapper(checkpoint, device=device, precision=dtype)
    example = wrapper.get_dummy_input(args.width, args.height)
    if example.ndim != 4 or tuple(example.shape[:2]) != (1, 3):
        raise ValueError(
            "Direct .engine loading currently supports single-image NCHW upscale models only; "
            f"'{os.path.basename(checkpoint)}' requires input shape {tuple(example.shape)}"
        )
    handler = TorchTensorRTHandler(os.path.dirname(checkpoint), trt_optimization_level=args.optimization_level)
    model = wrapper.get_model().eval()
    native_scale = wrapper.get_scale()
    scale = args.output_scale or native_scale
    if scale < 1 or scale > native_scale:
        raise ValueError(
            f"--output-scale must be between 1 and the model's native {native_scale}x scale"
        )
    if scale != native_scale:
        model = FinalOutputScale(
            model,
            output_height=args.height * scale,
            output_width=args.width * scale,
        ).eval()
    stem = os.path.splitext(os.path.basename(checkpoint))[0]
    output_dir = os.path.abspath(args.output_dir) if args.output_dir else os.path.dirname(checkpoint)
    output = args.output or os.path.join(output_dir, f"{stem}-x{scale}-tensorrt.engine")
    output = os.path.abspath(output)
    os.makedirs(os.path.dirname(output), exist_ok=True)
    exported = handler.build_engine(model, dtype, device, [example],
                                    trt_engine_name=os.path.splitext(output)[0],
                                    trt_multi_precision_engine=False, dynamic_shapes=None)
    print("VIDEOENHANCER_TRT_PROGRESS|TensorRT Engine|90|序列化 Engine", flush=True)
    # Handler's versioned naming is useful for its cache, while the explicit
    # output requested by users is the model name exposed to the UI/CLI.
    torch_tensorrt.save(exported, output, output_format="torchscript", inputs=(example,))
    print("VIDEOENHANCER_TRT_PROGRESS|TensorRT Engine|100|构建完成", flush=True)
    print(output)


if __name__ == "__main__":
    main()
