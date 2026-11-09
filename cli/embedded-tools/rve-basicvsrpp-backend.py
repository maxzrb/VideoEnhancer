# SPDX-License-Identifier: AGPL-3.0-only
# Copyright (c) 2026 VideoEnhancer contributors
# 本脚本与 RVE 结合运行；完整许可见 ../third-party/RVE/AGPL-3.0.txt。
"""Dedicated temporal BasicVSR++ runner for RVE."""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

import numpy as np
import torch

from src.temporal_video import RawVideoWriter, iter_video_windows
from rve_output_scale import resize_tensor, temporal_target


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="BasicVSR++ temporal backend for RVE")
    parser.add_argument("--input", required=True)
    parser.add_argument("--output", required=True)
    parser.add_argument("--model", required=True)
    parser.add_argument("--ffmpeg-path", required=True)
    parser.add_argument("--custom-encoder", default="-c:v libx264 -crf 18")
    parser.add_argument("--gpu", type=int, default=0)
    parser.add_argument("--clip-length", type=int, default=4)
    parser.add_argument("--clip-context", type=int, default=1)
    parser.add_argument("--precision", choices=("float16", "float32"), default="float32")
    parser.add_argument("--overwrite", action="store_true")
    parser.add_argument("--probe", action="store_true")
    return parser.parse_args()


def run_clip(model: torch.nn.Module, frames: np.ndarray, args: argparse.Namespace) -> torch.Tensor:
    device = torch.device(f"cuda:{args.gpu}")
    dtype = torch.float16 if args.precision == "float16" else torch.float32
    clip = torch.from_numpy(frames).permute(0, 3, 1, 2).float().div_(65535).unsqueeze(0).to(device)
    if clip.shape[1] == 1:
        clip = torch.cat((clip, clip), dim=1)
    with torch.inference_mode(), torch.autocast("cuda", dtype=dtype, enabled=dtype == torch.float16):
        enhanced = model(clip)[0]
    return enhanced[:frames.shape[0]].float()


def main() -> int:
    args = parse_args()
    model_path = Path(args.model).resolve()
    if not model_path.exists():
        raise FileNotFoundError("BasicVSR++ 模型不存在：" + str(model_path))
    if not torch.cuda.is_available():
        raise RuntimeError("BasicVSR++ 需要 NVIDIA CUDA GPU")
    torch.cuda.set_device(args.gpu)

    from src.basicvsrpp.model import load_basicvsrpp_model

    model, checkpoint_path = load_basicvsrpp_model(str(model_path), cpu_cache_length=0)
    model = model.eval().to(f"cuda:{args.gpu}")
    scale = 4 if model.is_low_res_input else 1
    print(f"BASICVSRPP_READY|{torch.cuda.get_device_name(args.gpu)}|{checkpoint_path}|{scale}x", flush=True)
    if args.probe:
        input_size = 64 if scale == 4 else 256
        test = torch.zeros(1, 2, 3, input_size, input_size, device=f"cuda:{args.gpu}")
        dtype = torch.float16 if args.precision == "float16" else torch.float32
        with torch.inference_mode(), torch.autocast("cuda", dtype=dtype, enabled=dtype == torch.float16):
            result = model(test)
        if result.shape != (1, 2, 3, 256, 256):
            raise RuntimeError(f"BasicVSR++ 探测输出尺寸异常：{tuple(result.shape)}")
        print(f"BASICVSRPP_PEAK_VRAM|{torch.cuda.max_memory_allocated(args.gpu)}", flush=True)
        print("BASICVSRPP_MODEL_VALID", flush=True)
        return 0

    fps, windows = iter_video_windows(args.input, max(2, args.clip_length), args.clip_context, args.ffmpeg_path)
    writer = None
    encoded = 0
    try:
        for window in windows:
            print(f"BASICVSRPP_DECODE|{window.decoded}|{window.expected}", flush=True)
            output = run_clip(model, window.frames, args)[window.emit_start:window.emit_end]
            target = temporal_target(window.frames)
            if target is not None:
                output = resize_tensor(output, *target)
            output = output.clamp(0, 1).permute(0, 2, 3, 1).cpu().numpy()
            output = (output * 65535.0 + 0.5).astype(np.uint16)
            if writer is None:
                writer = RawVideoWriter(args.ffmpeg_path, args.input, args.output,
                                        args.custom_encoder, fps, output.shape[2], output.shape[1],
                                        args.overwrite)
            writer.write(output)
            encoded += output.shape[0]
            print(f"BASICVSRPP_PROCESS|{encoded}|{window.expected}", flush=True)
            del output
            torch.cuda.empty_cache()
        if writer is None:
            raise RuntimeError("输入视频没有可解码的视频帧")
        writer.close()
    except Exception:
        if writer is not None:
            writer.abort()
        raise
    print(f"BASICVSRPP_PEAK_VRAM|{torch.cuda.max_memory_allocated(args.gpu)}", flush=True)
    print("BASICVSRPP_COMPLETE|" + args.output, flush=True)
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as exc:
        print("BASICVSRPP_ERROR|" + str(exc), file=sys.stderr, flush=True)
        raise
