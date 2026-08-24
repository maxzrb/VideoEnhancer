import argparse
import json
import os

from src.pytorch.InterpolateArchs.DetectInterpolateArch import ArchDetect


SUPPORTED_EXTENSIONS = {".pth", ".pt", ".pkl"}


def inspect_model(model_path: str) -> dict:
    full_path = os.path.abspath(model_path)
    result = {
        "path": full_path,
        "architecture": "",
        "base_architecture": "",
        "cuda": False,
        "tensorrt": False,
        "error": "",
    }
    try:
        if not os.path.isfile(full_path):
            raise FileNotFoundError("模型文件不存在")
        if os.path.splitext(full_path)[1].lower() not in SUPPORTED_EXTENSIONS:
            raise ValueError("仅支持 .pth、.pt 和 .pkl 权重")
        detector = ArchDetect(full_path)
        architecture = detector.getArchName()
        base_architecture = str(detector.getArchBase() or "").lower()
        if not architecture or not base_architecture:
            raise ValueError("无法从权重内容识别补帧架构")
        result["architecture"] = str(architecture)
        result["base_architecture"] = base_architecture
        result["cuda"] = base_architecture in {"rife", "gimm", "gmfss"}
        # 当前 RVE TensorRT 实现只覆盖 RIFE 的 flow/encode 网络。
        result["tensorrt"] = base_architecture == "rife"
    except Exception as exc:
        result["error"] = str(exc)
    return result


def main() -> int:
    parser = argparse.ArgumentParser(description="检查补帧权重的真实架构与后端能力")
    parser.add_argument("models", nargs="+", help="待检查的权重路径")
    args = parser.parse_args()
    results = [inspect_model(path) for path in args.models]
    print(json.dumps(results, ensure_ascii=False))
    return 0 if all(not item["error"] for item in results) else 2


if __name__ == "__main__":
    raise SystemExit(main())
