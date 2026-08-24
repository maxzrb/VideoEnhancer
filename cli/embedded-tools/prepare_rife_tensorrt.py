import argparse
import json
import os


def emit_progress(percent: int, detail: str) -> None:
    safe_detail = str(detail).replace("|", "/").replace("\r", " ").replace("\n", " ")
    print(f"VIDEOENHANCER_TRT_PROGRESS|RIFE Engine|{max(0, min(100, percent))}|{safe_detail}", flush=True)


def main() -> int:
    parser = argparse.ArgumentParser(description="使用 RVE 的真实 RIFE 路径预构建 TensorRT Engine")
    parser.add_argument("model", help="RIFE 权重，可为 .pth、.pt 或 .pkl")
    parser.add_argument("--width", type=int, required=True)
    parser.add_argument("--height", type=int, required=True)
    parser.add_argument("--precision", choices=("float16", "float32"), default="float16")
    parser.add_argument("--gpu-id", type=int, default=0)
    parser.add_argument("--static-shape", action="store_true")
    parser.add_argument("--optimization-level", type=int, choices=range(0, 6), default=5)
    args = parser.parse_args()

    from inspect_interpolation_models import inspect_model
    info = inspect_model(args.model)
    if info["error"]:
        raise RuntimeError(info["error"])
    if not info["tensorrt"]:
        raise RuntimeError(f"{info['architecture']} 当前不支持 TensorRT 补帧；请使用 CUDA/PyTorch")
    if args.width <= 0 or args.height <= 0:
        raise ValueError("宽高必须大于 0")

    import torch
    if not torch.cuda.is_available():
        raise RuntimeError("当前环境未检测到可用的 NVIDIA CUDA GPU，无法构建 TensorRT Engine")

    emit_progress(1, f"已识别 {info['architecture']}，准备加载 RVE TensorRT 构建器")
    from src.pytorch.InterpolateRIFE import InterpolateRifeTorch
    interpolation = InterpolateRifeTorch(
        modelPath=os.path.abspath(args.model), ceilInterpolateFactor=2,
        width=args.width, height=args.height, device="cuda", dtype=args.precision,
        backend="tensorrt", gpu_id=args.gpu_id,
        trt_static_shape=args.static_shape,
        trt_optimization_level=args.optimization_level,
    )
    cache_dir = interpolation.trt_cache_dir
    del interpolation
    emit_progress(100, "RIFE flow/encode Engine 已就绪")
    print("VIDEOENHANCER_TRT_READY|" + json.dumps({
        "model": os.path.abspath(args.model), "architecture": info["architecture"],
        "width": args.width, "height": args.height, "cache_dir": cache_dir,
    }, ensure_ascii=False), flush=True)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
