# SPDX-License-Identifier: AGPL-3.0-only
# Copyright (c) 2026 VideoEnhancer contributors
# 本脚本与 RVE 结合运行；完整许可见 ../third-party/RVE/AGPL-3.0.txt。
"""Dedicated temporal FlashVSR runner used by rve-backend.

FlashVSR consumes a sequence of frames (at least 21 after padding), so it cannot
be represented by RVE's ordinary one-frame PyTorch upscaler wrapper.
"""

from __future__ import annotations

import argparse
import os
import sys
from pathlib import Path

import numpy as np
import torch

from src.temporal_video import RawVideoWriter, iter_video_windows
from rve_output_scale import resize_tensor, temporal_target

# Triton defaults to the user profile.  Portable/sandboxed deployments may not
# have write access there, so keep all generated kernels beside the backend.
_triton_cache = Path(__file__).resolve().parent / "cache" / "triton"
_triton_cache.mkdir(parents=True, exist_ok=True)
os.environ.setdefault("TRITON_CACHE_DIR", str(_triton_cache))


REQUIRED_WEIGHTS = (
    "diffusion_pytorch_model_streaming_dmd.safetensors",
    "LQ_proj_in.ckpt",
    "TCDecoder.ckpt",
    "Wan2.1_VAE.pth",
)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="FlashVSR temporal backend for RVE")
    parser.add_argument("--input", required=True)
    parser.add_argument("--output", required=True)
    parser.add_argument("--model-dir", required=True)
    parser.add_argument("--ffmpeg-path", required=True)
    parser.add_argument("--custom-encoder", default="-c:v libx264 -crf 18")
    parser.add_argument("--scale", type=int, choices=(2, 4), default=4)
    parser.add_argument("--mode", choices=("tiny", "tiny-long", "full"), default="tiny-long")
    parser.add_argument("--precision", choices=("fp16", "fp32"), default="fp32")
    parser.add_argument("--gpu", type=int, default=0)
    parser.add_argument("--window-length", type=int, default=29)
    parser.add_argument("--window-context", type=int, default=4)
    parser.add_argument("--overwrite", action="store_true")
    parser.add_argument("--probe", action="store_true")
    return parser.parse_args()


def validate_model_dir(model_dir: Path) -> None:
    if not model_dir.is_dir():
        raise FileNotFoundError(f"FlashVSR 模型目录不存在：{model_dir}")
    missing = [name for name in REQUIRED_WEIGHTS if not (model_dir / name).is_file()]
    if missing:
        raise FileNotFoundError("FlashVSR 模型不完整，缺少：" + "、".join(missing))


def main() -> int:
    args = parse_args()
    model_dir = Path(args.model_dir).resolve()
    validate_model_dir(model_dir)
    if not torch.cuda.is_available():
        raise RuntimeError("FlashVSR 需要 NVIDIA CUDA GPU")
    torch.cuda.set_device(args.gpu)
    os.environ["RVE_FLASHVSR_MODEL_DIR"] = str(model_dir)

    from src.flashvsr.nodes import FlashVSRNode, init_pipeline

    print(f"FLASHVSR_READY|{torch.cuda.get_device_name(args.gpu)}|{model_dir}", flush=True)
    if args.probe:
        dtype = torch.float32 if args.precision == "fp32" else torch.float16
        pipeline = init_pipeline(args.mode, f"cuda:{args.gpu}", dtype)
        del pipeline
        torch.cuda.empty_cache()
        print("FLASHVSR_MODEL_VALID", flush=True)
        return 0

    if args.window_length < 21:
        raise ValueError("FlashVSR 时间窗口不能小于 21 帧")
    fps, windows = iter_video_windows(args.input, args.window_length, args.window_context, args.ffmpeg_path)
    dtype = torch.float32 if args.precision == "fp32" else torch.float16
    pipeline = init_pipeline(args.mode, f"cuda:{args.gpu}", dtype)
    node = FlashVSRNode()
    writer = None
    encoded = 0
    try:
        for window in windows:
            print(f"FLASHVSR_DECODE|{window.decoded}|{window.expected}", flush=True)
            frames = torch.from_numpy(window.frames).float().div_(65535)
            output = node.main(
                frames=frames, mode=args.mode, scale=args.scale, color_fix=True,
                tiled_vae=True, tiled_dit=True, tile_size=256, tile_overlap=24,
                unload_dit=True, sparse_ratio=2.0, kv_ratio=3.0, local_range=11,
                seed=0, device=f"cuda:{args.gpu}", precision=args.precision,
                attention_mode="sparse_sage_attention", pipeline=pipeline,
            )[0][window.emit_start:window.emit_end]
            target = temporal_target(window.frames)
            if target is not None:
                output = resize_tensor(output.permute(0, 3, 1, 2), *target).permute(0, 2, 3, 1)
            output = (output.clamp(0, 1).numpy() * 65535.0 + 0.5).astype(np.uint16)
            if writer is None:
                writer = RawVideoWriter(args.ffmpeg_path, args.input, args.output,
                                        args.custom_encoder, fps, output.shape[2], output.shape[1],
                                        args.overwrite)
            writer.write(output)
            encoded += output.shape[0]
            print(f"FLASHVSR_PROCESS|{encoded}|{window.expected}|{args.scale}|{args.mode}", flush=True)
            del frames, output
            torch.cuda.empty_cache()
        if writer is None:
            raise RuntimeError("输入视频没有可解码的视频帧")
        writer.close()
    except Exception:
        if writer is not None:
            writer.abort()
        raise
    del pipeline
    print(f"FLASHVSR_PEAK_VRAM|{torch.cuda.max_memory_allocated(args.gpu)}", flush=True)
    torch.cuda.empty_cache()
    print("FLASHVSR_COMPLETE|" + args.output, flush=True)
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as exc:
        print("FLASHVSR_ERROR|" + str(exc), file=sys.stderr, flush=True)
        raise
