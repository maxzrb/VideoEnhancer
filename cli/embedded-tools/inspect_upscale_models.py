import argparse
import json
import os
import re
import sys


BACKEND_DIRECTORY = os.environ.get("VIDEOENHANCER_BACKEND_DIR") or os.path.dirname(os.path.abspath(__file__))
if BACKEND_DIRECTORY not in sys.path:
    sys.path.insert(0, BACKEND_DIRECTORY)


TORCH_EXTENSIONS = {".pth", ".pt", ".ckpt", ".safetensors"}


def _base_result(model_path: str) -> dict:
    return {
        "path": os.path.abspath(model_path),
        "format": os.path.splitext(model_path)[1].lower().lstrip("."),
        "architecture": "",
        "purpose": "",
        "scale": 0,
        "input_channels": 0,
        "output_channels": 0,
        "supports_half": False,
        "supports_bfloat16": False,
        "input_multiple": 1,
        "minimum_size": 0,
        "square": False,
        "tiling": "",
        "backends": [],
        "error": "",
    }


def _inspect_torch(model_path: str) -> dict:
    result = _base_result(model_path)
    try:
        from src.pytorch.spandrel import ImageModelDescriptor, ModelLoader

        descriptor = ModelLoader(device="cpu").load_from_file(model_path)
        if not isinstance(descriptor, ImageModelDescriptor):
            raise ValueError("当前 RVE 单图管线不支持该模型描述类型")
        requirements = descriptor.size_requirements
        architecture = descriptor.architecture
        architecture_name = str(getattr(architecture, "name", None) or architecture.id)
        tensor_rt_blocked = architecture_name.lower() in {"animesr", "swinir", "craft"}
        result.update(
            architecture=architecture_name,
            purpose=str(descriptor.purpose),
            scale=int(descriptor.scale),
            input_channels=int(descriptor.input_channels),
            output_channels=int(descriptor.output_channels),
            supports_half=bool(descriptor.supports_half),
            supports_bfloat16=bool(descriptor.supports_bfloat16),
            input_multiple=max(1, int(requirements.multiple_of)),
            minimum_size=max(0, int(requirements.minimum)),
            square=bool(requirements.square),
            tiling=str(getattr(descriptor.tiling, "name", descriptor.tiling)),
            backends=["cuda"] if descriptor.purpose == "Restoration" or tensor_rt_blocked else ["cuda", "tensorrt"],
        )
    except Exception as exc:
        result["error"] = str(exc)
    return result


def _shape_dimension(value) -> int | None:
    return value if isinstance(value, int) and value > 0 else None


def _filename_scale(model_path: str) -> int:
    name = os.path.splitext(os.path.basename(model_path))[0]
    matches = re.findall(r"(?i)(?:^|[-_.])(?:x(\d+)|(\d+)x)(?=$|[-_.])", name)
    values = [int(left or right) for left, right in matches if int(left or right) in {1, 2, 3, 4, 8}]
    return values[-1] if values else 0


def _filename_architecture(model_path: str) -> str:
    name = os.path.basename(model_path)
    aliases = (
        ("RealHatGAN", "RealHatGAN"),
        ("RealESRGAN", "RealESRGAN"),
        ("RealPLKSR", "RealPLKSR"),
        ("SPANPlus", "SPANPlus"),
        ("SPANF", "SPAN"),
        ("SPAN", "SPAN"),
        ("SwinIR", "SwinIR"),
        ("CRAFT", "CRAFT"),
        ("Compact", "Compact"),
        ("DITN", "DITN"),
        ("DAT", "DAT"),
        ("GRL", "GRL"),
        ("Omni", "OmniSR"),
        ("RRDB", "RRDBNet"),
        ("ESRGAN", "ESRGAN"),
    )
    lowered = name.lower()
    for needle, architecture in aliases:
        if needle.lower() in lowered:
            return architecture
    return "ONNX"


def _inspect_onnx(model_path: str) -> dict:
    result = _base_result(model_path)
    try:
        import onnxruntime as ort

        session = ort.InferenceSession(model_path, providers=["CPUExecutionProvider"])
        inputs = session.get_inputs()
        outputs = session.get_outputs()
        if len(inputs) != 1 or len(outputs) < 1:
            raise ValueError("当前 ONNX 超分管线要求单输入且至少一个输出")
        input_meta = inputs[0]
        output_meta = outputs[0]
        if len(input_meta.shape) != 4 or len(output_meta.shape) != 4:
            raise ValueError("当前 ONNX 超分管线要求 NCHW 四维输入输出")
        input_channels = _shape_dimension(input_meta.shape[1]) or 3
        output_channels = _shape_dimension(output_meta.shape[1]) or 3
        input_height = _shape_dimension(input_meta.shape[2])
        input_width = _shape_dimension(input_meta.shape[3])
        output_height = _shape_dimension(output_meta.shape[2])
        output_width = _shape_dimension(output_meta.shape[3])
        scale = 0
        if input_height and input_width and output_height and output_width:
            if output_height % input_height == 0 and output_width % input_width == 0:
                scale_h = output_height // input_height
                scale_w = output_width // input_width
                if scale_h == scale_w:
                    scale = scale_h
        if scale == 0:
            scale = _filename_scale(model_path)
        if scale <= 0:
            raise ValueError("无法确定 ONNX 模型倍率，请在文件名中包含 1x/2x/3x/4x/8x")
        metadata = session.get_modelmeta()
        custom = metadata.custom_metadata_map or {}
        architecture = custom.get("architecture") or custom.get("model_architecture") or _filename_architecture(model_path)
        result.update(
            architecture=str(architecture),
            purpose="Restoration" if scale == 1 else "SR",
            scale=int(scale),
            input_channels=int(input_channels),
            output_channels=int(output_channels),
            supports_half="float16" in str(input_meta.type).lower(),
            supports_bfloat16="bfloat16" in str(input_meta.type).lower(),
            input_multiple=max(1, int(custom.get("input_multiple", "1"))),
            minimum_size=max(0, int(custom.get("minimum_size", "0"))),
            square=str(custom.get("square", "false")).lower() == "true",
            tiling="SUPPORTED",
            backends=["onnx"],
        )
    except Exception as exc:
        result["error"] = str(exc)
    return result


def inspect_model(model_path: str) -> dict:
    full_path = os.path.abspath(model_path)
    extension = os.path.splitext(full_path)[1].lower()
    if not os.path.isfile(full_path):
        result = _base_result(full_path)
        result["error"] = "模型文件不存在"
        return result
    if extension in TORCH_EXTENSIONS:
        return _inspect_torch(full_path)
    if extension == ".onnx":
        return _inspect_onnx(full_path)
    result = _base_result(full_path)
    result["error"] = "仅支持 .pth、.pt、.ckpt、.safetensors 和 .onnx"
    return result


def main() -> int:
    parser = argparse.ArgumentParser(description="安全检查图像超分/修复模型能力")
    parser.add_argument("models", nargs="+", help="待检查的模型路径")
    args = parser.parse_args()
    results = [inspect_model(path) for path in args.models]
    print(json.dumps(results, ensure_ascii=False))
    return 0 if all(not item["error"] for item in results) else 2


if __name__ == "__main__":
    raise SystemExit(main())
