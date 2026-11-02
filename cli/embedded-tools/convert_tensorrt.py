"""用 PTH/ONNX 构建兼容 RVE 的 Torch-TensorRT 引擎。"""
import argparse
import os

import torch
import torch.nn.functional as F
import torch_tensorrt

from src.pytorch.UpscaleModelWrapper import UpscaleModelWrapper
from src.pytorch.TensorRTHandler import TorchTensorRTHandler


class FinalOutputScale(torch.nn.Module):
    """在导出的 GPU 图内缩放模型结果，不改变学习网络或权重。"""

    def __init__(self, model, output_height, output_width):
        super().__init__()
        self.model = model
        self.output_size = (output_height, output_width)

    def forward(self, x):
        output = self.model(x)
        # 非整数比例的 adaptive-area 不能转换到 TensorRT；双三次支持 4x 到 3x。
        return F.interpolate(
            output, size=self.output_size, mode="bicubic", align_corners=False
        )


def resolve_checkpoint(path):
    path = os.path.abspath(path)
    if os.path.isfile(path):
        if os.path.splitext(path)[1].lower() in (".pth", ".onnx"):
            return path
        raise ValueError("TensorRT conversion needs a .pth or .onnx source model")
    if os.path.isdir(path):
        base = os.path.basename(os.path.normpath(path))
        for candidate in (os.path.join(os.path.dirname(path), base + ".pth"),
                          os.path.join(path, base + ".pth")):
            if os.path.isfile(candidate):
                return candidate
        raise FileNotFoundError("NCNN .param/.bin folder has no matching .pth checkpoint: " + path)
    raise FileNotFoundError(path)


def convert_onnx(args, source, device):
    import onnx
    import tensorrt as trt
    from torch_tensorrt.dynamo.runtime import TorchTensorRTModule

    # 缓存指纹由源文件内容生成，外置权重需先合并，避免复用过期 Engine。
    model = onnx.load(source, load_external_data=False)
    if any(value.data_location == onnx.TensorProto.EXTERNAL for value in model.graph.initializer):
        raise ValueError("TensorRT conversion requires a self-contained ONNX file; merge external weights first")
    metadata = {value.key: value.value for value in model.metadata_props}
    multiple = max(1, args.input_multiple, int(metadata.get("input_multiple", "1")))
    del model
    logger = trt.Logger(trt.Logger.WARNING)
    builder = trt.Builder(logger)
    network = builder.create_network(1 << int(trt.NetworkDefinitionCreationFlag.EXPLICIT_BATCH))
    parser = trt.OnnxParser(network, logger)
    print("VIDEOENHANCER_TRT_PROGRESS|TensorRT Engine|5|解析 ONNX", flush=True)
    if not parser.parse_from_file(source):
        raise ValueError("TensorRT ONNX parse failed: " + "\n".join(
            str(parser.get_error(index)) for index in range(parser.num_errors)
        ))
    if network.num_inputs != 1 or network.num_outputs != 1:
        raise ValueError("ONNX TensorRT upscale requires exactly one input and one output")
    input_tensor = network.get_input(0)
    output_tensor = network.get_output(0)
    shape = tuple(input_tensor.shape)
    if len(shape) != 4 or shape[0] not in (1, -1) or shape[1] != 3:
        raise ValueError(f"ONNX TensorRT upscale requires single-image RGB NCHW input, got {shape}")
    dtype_map = {trt.float32: torch.float32, trt.float16: torch.float16}
    if input_tensor.dtype not in dtype_map:
        raise ValueError(f"Unsupported ONNX TensorRT input precision: {input_tensor.dtype}")
    # 固定尺寸图保留原 profile；动态维度按当前任务编译，RVE 负责补边/分块。
    input_shape = (1, 3, shape[2] if shape[2] > 0 else (args.height + multiple - 1) // multiple * multiple,
                   shape[3] if shape[3] > 0 else (args.width + multiple - 1) // multiple * multiple)
    config = builder.create_builder_config()
    config.builder_optimization_level = args.optimization_level
    config.set_memory_pool_limit(trt.MemoryPoolType.WORKSPACE, 1 << 30)
    if not args.fp32:
        config.set_flag(trt.BuilderFlag.FP16)
    profile = builder.create_optimization_profile()
    profile.set_shape(input_tensor.name, input_shape, input_shape, input_shape)
    config.add_optimization_profile(profile)

    def build():
        plan = builder.build_serialized_network(network, config)
        if plan is None:
            raise RuntimeError("TensorRT failed to build the ONNX engine; see the builder errors above")
        runtime = trt.Runtime(logger)
        engine = runtime.deserialize_cuda_engine(plan)
        if engine is None:
            raise RuntimeError("TensorRT could not deserialize the newly built ONNX engine")
        context = engine.create_execution_context()
        if context is None or not context.set_input_shape(input_tensor.name, input_shape):
            raise RuntimeError("TensorRT could not select the ONNX input profile")
        return bytearray(plan), tuple(context.get_tensor_shape(network.get_output(0).name))

    print(f"VIDEOENHANCER_TRT_PROGRESS|TensorRT Engine|20|构建 ONNX profile {input_shape}", flush=True)
    plan, output_shape = build()
    if (len(output_shape) != 4 or output_shape[:2] != (1, 3)
            or output_shape[2] <= 0 or output_shape[3] <= 0
            or output_shape[2] % input_shape[2] or output_shape[3] % input_shape[3]
            or output_shape[2] // input_shape[2] != output_shape[3] // input_shape[3]):
        raise ValueError(f"ONNX TensorRT upscale requires equal integer RGB output scale: {input_shape} -> {output_shape}")
    native_scale = output_shape[2] // input_shape[2]
    scale = args.output_scale or native_scale
    if scale < 1 or scale > native_scale:
        raise ValueError(f"--output-scale must be between 1 and the model's native {native_scale}x scale")
    if scale != native_scale:
        # 缩小留在 TensorRT 图内，Engine 元数据与最终输出尺寸一致。
        resize = network.add_resize(output_tensor)
        resize.resize_mode = trt.InterpolationMode.CUBIC
        resize.coordinate_transformation = trt.ResizeCoordinateTransformation.HALF_PIXEL
        resize.cubic_coeff = -0.75
        resize.shape = (1, 3, input_shape[2] * scale, input_shape[3] * scale)
        network.unmark_output(output_tensor)
        output_tensor = resize.get_output(0)
        output_tensor.name = "videoenhancer_scaled_output"
        network.mark_output(output_tensor)
        plan, _ = build()
    # 与 PTH 转换器使用同一运行时封装，现有 RVE 无须新增原生 plan 加载器。
    module = TorchTensorRTModule(plan, input_binding_names=[input_tensor.name],
                                output_binding_names=[output_tensor.name], name="onnx_upscale")
    example = torch.zeros(input_shape, dtype=dtype_map[input_tensor.dtype], device=device)
    exported = torch.jit.trace(module, example)
    stem = os.path.splitext(os.path.basename(source))[0]
    output_dir = os.path.abspath(args.output_dir) if args.output_dir else os.path.dirname(source)
    output = os.path.abspath(args.output or os.path.join(output_dir, f"{stem}-x{scale}-tensorrt.engine"))
    os.makedirs(os.path.dirname(output), exist_ok=True)
    print("VIDEOENHANCER_TRT_PROGRESS|TensorRT Engine|90|序列化 Engine", flush=True)
    torch.jit.save(exported, output)
    print("VIDEOENHANCER_TRT_PROGRESS|TensorRT Engine|100|构建完成", flush=True)
    print(output)


def main():
    p = argparse.ArgumentParser(description="Convert a PTH/ONNX upscale model to a TensorRT engine")
    p.add_argument("input", help=".pth/.onnx file or an NCNN folder with a matching .pth")
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
    p.add_argument("--input-multiple", type=int, default=1,
                   help="spatial input multiple for dynamic ONNX models")
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
    if args.width <= 0 or args.height <= 0:
        raise ValueError("--width and --height must be positive")
    if os.path.splitext(checkpoint)[1].lower() == ".onnx":
        convert_onnx(args, checkpoint, device)
        return
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
    # 内部处理器使用版本化缓存名称，这里保存为调用方指定的引擎路径。
    torch_tensorrt.save(exported, output, output_format="torchscript", inputs=(example,))
    print("VIDEOENHANCER_TRT_PROGRESS|TensorRT Engine|100|构建完成", flush=True)
    print(output)


if __name__ == "__main__":
    main()
