# SPDX-License-Identifier: AGPL-3.0-only
# Copyright (c) 2026 VideoEnhancer contributors
# 本脚本与 RVE 结合运行；完整许可见 ../third-party/RVE/AGPL-3.0.txt。
import argparse
import json
import os
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
        architecture_name = str(architecture if isinstance(architecture, str) else
                                getattr(architecture, "name", None) or architecture.id)
        if type(descriptor.model).__name__ == "RealPLKSR":
            architecture_name = "RealPLKSR"
        tensor_rt_blocked = architecture_name.lower() in {"animesr", "swinir", "craft"}
        result.update(
            architecture=architecture_name,
            model_class=type(descriptor.model).__name__,
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
            backends=["cuda"] if tensor_rt_blocked or (descriptor.purpose == "Restoration" and type(descriptor.model).__name__ != "SRVGGNetCompact") else ["cuda", "tensorrt"],
        )
    except Exception as exc:
        result["error"] = str(exc) or type(exc).__name__
        # AnimeSR 使用时序包装器，不能由单图 Spandrel 描述；按权重严格加载确认。
        try:
            import torch
            from src.pytorch.VSRArchs.AnimeSR.animesr_arch import AnimeSR
            state = torch.load(model_path, map_location="cpu", weights_only=True)
            model = AnimeSR()
            model.load_state_dict(state, strict=True)
            result.update(architecture="AnimeSR", model_class="AnimeSR", purpose="SR", scale=4,
                          input_channels=3, output_channels=3, input_multiple=4,
                          supports_half=True, tiling="SUPPORTED", backends=["cuda"], error="")
        except Exception:
            pass
    return result


def _shape_dimension(value) -> int | None:
    return value if isinstance(value, int) and value > 0 else None


def _graph_architecture(model_path: str) -> str:
    """仅识别具有明确结构特征的导出图；未知图不按文件名归类。"""
    import onnx
    graph = onnx.load(model_path).graph
    names = "\n".join(node.name for node in graph.node).lower()
    operations = {node.op_type for node in graph.node}
    if "rdb1/conv1" in names:
        return "ESRGAN"
    if "conv_block/cab" in names and "softmax" in names:
        return "HAT"
    if "channel_mixer" in names and "lk" in names:
        return "RealPLKSR"
    if "c1_r" in names and "eval_conv" in names:
        return "SPANF3" if "/conv_1/conv_1.1/" in names else "SPAN"
    if "PRelu" in operations and "DepthToSpace" in operations and not operations & {"Softmax", "MatMul"}:
        return "Compact"
    if "attn/dwconv" in names:
        return "DAT"
    if "residual_group/blocks" in names and "attn/qkv" in names and not "conv_block/cab" in names:
        return "SwinIR"
    return "ONNX"


def _inspect_onnx(model_path: str) -> dict:
    result = _base_result(model_path)
    try:
        import onnxruntime as ort

        options = ort.SessionOptions()
        options.log_severity_level = 3
        session = ort.InferenceSession(model_path, sess_options=options, providers=["CPUExecutionProvider"])
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
            import numpy as np
            height = input_height or 64
            width = input_width or 64
            dtype = np.float16 if "float16" in str(input_meta.type).lower() else np.float32
            value = session.run([output_meta.name], {input_meta.name: np.zeros((1, input_channels, height, width), dtype=dtype)})[0]
            result["shape_probe"] = {"input": [height, width], "output": list(value.shape[-2:])}
            if value.shape[-2] % height == 0 and value.shape[-1] % width == 0:
                scale_h, scale_w = value.shape[-2] // height, value.shape[-1] // width
                if scale_h == scale_w:
                    scale = int(scale_h)
        if scale <= 0:
            raise ValueError("无法从 ONNX 输入输出确定相同的整数倍率")
        metadata = session.get_modelmeta()
        custom = metadata.custom_metadata_map or {}
        architecture = _graph_architecture(model_path)
        if architecture == "ONNX":
            architecture = custom.get("architecture") or custom.get("model_architecture") or "ONNX"
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
            backends=["onnx", "tensorrt"] if (
                len(outputs) == 1 and input_channels == output_channels == 3
                and input_meta.type in ("tensor(float)", "tensor(float16)")
                and output_meta.type in ("tensor(float)", "tensor(float16)")
                and input_meta.shape[1] == output_meta.shape[1] == 3
                and _shape_dimension(input_meta.shape[0]) in (1, None)
            ) else ["onnx"],
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
