import os
import re
import sys
import time

import numpy as np

# The portable build already ships CUDA/cuDNN beside PyTorch. Register that
# DLL directory before ONNX Runtime creates its CUDA execution provider.
_DLL_HANDLES = []
_TORCH_LIB = os.path.join(sys.prefix, "Lib", "site-packages", "torch", "lib")
if os.name == "nt" and hasattr(os, "add_dll_directory"):
    if os.path.isdir(_TORCH_LIB):
        _DLL_HANDLES.append(os.add_dll_directory(_TORCH_LIB))

import onnxruntime as ort

if os.name == "nt" and os.path.isdir(_TORCH_LIB) and hasattr(ort, "preload_dlls"):
    ort.preload_dlls(directory=_TORCH_LIB)
ort.set_default_logger_severity(3)

from ..utils.Util import log
from ..utils.Frame import Frame
from ..utils.PixelFormat import to_uint16


def getONNXScale(modelPath: str = "") -> int:
    """Read the native integer scale from a conventional SR model name."""
    name = os.path.splitext(os.path.basename(modelPath))[0].lower()
    match = re.search(r"(?:^|[_-])(\d+)x(?:[_-]|$)|(?:^|[_-])x(\d+)(?:[_-]|$)", name)
    if match:
        return int(match.group(1) or match.group(2))
    raise ValueError(
        "Scale not found in ONNX model name. Include a scale such as '2x_' or '-x2-' in the filename."
    )


def _static_dimension(value):
    return int(value) if isinstance(value, int) and value > 0 else None


class UpscaleONNX:
    """ONNX Runtime upscaler with CUDA/DirectML/CPU provider fallback.

    Dynamic models run on the complete frame. Static-shape models are applied
    tile by tile, so fixed 240x320-style exports can process arbitrary videos.
    """

    @classmethod
    def getModelScale(cls, modelPath: str) -> int:
        return getONNXScale(modelPath)

    def __init__(
        self,
        modelPath: str,
        device="default",
        tile_pad: int = 10,
        precision: str = "auto",
        width: int = 1920,
        height: int = 1080,
        scale: int | None = None,
        tilesize: int = 0,
        gpu_id: int = 0,
        hdr_mode: bool = False,
    ):
        if not os.path.isfile(modelPath):
            raise FileNotFoundError(modelPath)

        self.width = width
        self.height = height
        self.scale = scale or self.getModelScale(modelPath)
        self.modelPath = modelPath
        self.device = device
        self.tile_pad = max(0, int(tile_pad))
        self.tilesize = max(0, int(tilesize or 0))
        # 已知窗口注意力模型通过环境变量声明输入尺寸约束和安全分块大小。
        self.input_multiple = max(1, int(os.environ.get("VIDEOENHANCER_ONNX_INPUT_MULTIPLE", "1")))
        self.gpu_id = gpu_id
        self.hdr_mode = hdr_mode
        self.paused = False

        options = ort.SessionOptions()
        options.graph_optimization_level = ort.GraphOptimizationLevel.ORT_ENABLE_ALL
        options.log_severity_level = 3
        self.inference_session = self._create_session(options)
        self.input_info = self.inference_session.get_inputs()[0]
        self.output_info = self.inference_session.get_outputs()[0]
        self.input_name = self.input_info.name

        shape = list(self.input_info.shape)
        if len(shape) != 4:
            raise ValueError(f"ONNX SR model must have one 4D image input, got {shape}")
        if shape[1] == 3 or shape[1] in (None, "channels", "C"):
            self.layout = "NCHW"
            self.model_height = _static_dimension(shape[2])
            self.model_width = _static_dimension(shape[3])
        elif shape[3] == 3:
            self.layout = "NHWC"
            self.model_height = _static_dimension(shape[1])
            self.model_width = _static_dimension(shape[2])
        else:
            raise ValueError(f"Unable to identify RGB channel dimension in ONNX input {shape}")

        type_map = {
            "tensor(float16)": np.float16,
            "tensor(float)": np.float32,
            "tensor(double)": np.float64,

        }
        if self.input_info.type not in type_map:
            raise ValueError(f"Unsupported ONNX input type: {self.input_info.type}")
        self.input_dtype = type_map[self.input_info.type]
        requested = precision.lower() if isinstance(precision, str) else "auto"
        if requested not in ("auto", "float16", "float32"):
            raise ValueError(f"Unsupported ONNX precision request: {precision}")
        if requested != "auto" and np.dtype(self.input_dtype).name != requested:
            log(
                f"ONNX model input is {np.dtype(self.input_dtype).name}; "
                f"using the model's stored precision instead of requested {requested}."
            )

        provider = self.inference_session.get_providers()[0]
        shape_text = "dynamic" if self.model_width is None else f"{self.model_width}x{self.model_height}"
        log(
            f"Loaded ONNX model with {provider}: input {shape_text} {self.layout} "
            f"{np.dtype(self.input_dtype).name}, scale {self.scale}x"
        )

    def _create_session(self, options):
        available = ort.get_available_providers()
        requested_cpu = str(self.device).lower() == "cpu"
        candidates = []
        if not requested_cpu and "CUDAExecutionProvider" in available:
            candidates.append(
                [("CUDAExecutionProvider", {"device_id": self.gpu_id, "use_tf32": 0}), "CPUExecutionProvider"]
            )
        if not requested_cpu and "DmlExecutionProvider" in available:
            candidates.append(
                [("DmlExecutionProvider", {"device_id": self.gpu_id}), "CPUExecutionProvider"]
            )
        candidates.append(["CPUExecutionProvider"])

        errors = []
        for providers in candidates:
            try:
                session = ort.InferenceSession(self.modelPath, options, providers=providers)
                if session.get_providers():
                    return session
            except Exception as exc:
                errors.append(f"{providers[0]}: {exc}")
        raise RuntimeError("Unable to create ONNX Runtime session: " + " | ".join(errors))

    def _prepare_input(self, image: np.ndarray) -> np.ndarray:
        value = to_uint16(image).astype(np.float32) / 65535.0
        value = value.astype(self.input_dtype)
        if self.layout == "NCHW":
            value = np.transpose(value, (2, 0, 1))
        return np.ascontiguousarray(value[None, ...])

    def _normalise_output(self, output: np.ndarray) -> np.ndarray:
        value = output[0]
        if value.ndim != 3:
            raise ValueError(f"ONNX SR output must be a 4D image tensor, got {output.shape}")
        if value.shape[0] in (1, 3, 4):
            value = np.transpose(value, (1, 2, 0))
        if value.shape[2] > 3:
            value = value[:, :, :3]
        if not np.issubdtype(value.dtype, np.floating):
            raise ValueError("ONNX output must use floating point to retain at least 10 bits")
        return to_uint16(value)

    def _run(self, image: np.ndarray) -> np.ndarray:
        output = self.inference_session.run(None, {self.input_name: self._prepare_input(image)})[0]
        return self._normalise_output(output)

    def _run_padded(self, image: np.ndarray) -> np.ndarray:
        height, width = image.shape[:2]
        multiple = self.input_multiple
        pad_h = (-height) % multiple
        pad_w = (-width) % multiple
        if pad_h or pad_w:
            mode = "reflect" if height > 1 and width > 1 else "edge"
            image = np.pad(image, ((0, pad_h), (0, pad_w), (0, 0)), mode=mode)
        output = self._run(image)
        return output[: height * self.scale, : width * self.scale]

    def _run_dynamic_tiled(self, image: np.ndarray) -> np.ndarray:
        height, width = image.shape[:2]
        multiple = self.input_multiple
        core = max(multiple, self.tilesize - self.tilesize % multiple)
        overlap = max(self.tile_pad, multiple)
        result = np.empty((height * self.scale, width * self.scale, 3), dtype=np.uint16)
        for y in range(0, height, core):
            for x in range(0, width, core):
                actual_h = min(core, height - y)
                actual_w = min(core, width - x)
                y0, x0 = max(0, y - overlap), max(0, x - overlap)
                y1 = min(height, y + actual_h + overlap)
                x1 = min(width, x + actual_w + overlap)
                upscaled = self._run_padded(image[y0:y1, x0:x1])
                sy, sx = (y - y0) * self.scale, (x - x0) * self.scale
                cropped = upscaled[sy:sy + actual_h * self.scale, sx:sx + actual_w * self.scale]
                result[y * self.scale:(y + actual_h) * self.scale, x * self.scale:(x + actual_w) * self.scale] = cropped
        return result

    def _run_static_tiled(self, image: np.ndarray) -> np.ndarray:
        model_h = self.model_height
        model_w = self.model_width
        pad = min(self.tile_pad, (model_h - 1) // 2, (model_w - 1) // 2)
        core_h = model_h - 2 * pad
        core_w = model_w - 2 * pad
        if core_h <= 0 or core_w <= 0:
            raise ValueError(f"ONNX static input {model_w}x{model_h} is too small for tile padding {pad}")

        height, width = image.shape[:2]
        result = np.empty((height * self.scale, width * self.scale, 3), dtype=np.uint16)
        for y in range(0, height, core_h):
            for x in range(0, width, core_w):
                actual_h = min(core_h, height - y)
                actual_w = min(core_w, width - x)
                y0, x0 = max(0, y - pad), max(0, x - pad)
                y1, x1 = min(height, y + core_h + pad), min(width, x + core_w + pad)
                tile = image[y0:y1, x0:x1]
                missing = (
                    (max(0, pad - y), max(0, y + core_h + pad - height)),
                    (max(0, pad - x), max(0, x + core_w + pad - width)),
                    (0, 0),
                )
                mode = "reflect" if tile.shape[0] > 1 and tile.shape[1] > 1 else "edge"
                tile = np.pad(tile, missing, mode=mode)
                tile = tile[:model_h, :model_w]
                upscaled = self._run(tile)
                sy, sx = pad * self.scale, pad * self.scale
                cropped = upscaled[
                    sy : sy + actual_h * self.scale,
                    sx : sx + actual_w * self.scale,
                ]
                result[
                    y * self.scale : (y + actual_h) * self.scale,
                    x * self.scale : (x + actual_w) * self.scale,
                ] = cropped
        return result

    def hotUnload(self):
        self.paused = True

    def hotReload(self):
        self.paused = False

    def __call__(self, frame) -> bytes:
        while self.paused:
            time.sleep(0.1)
        if hasattr(frame, "get_frame_np"):
            image = frame.get_frame_np()
        elif isinstance(frame, (bytes, bytearray, memoryview)):
            image = np.frombuffer(frame, dtype=np.uint16).reshape(self.height, self.width, 3)
        else:
            image = np.asarray(frame)

        if self.model_width is None:
            output = self._run_dynamic_tiled(image) if self.tilesize > 0 else self._run_padded(image)
        elif image.shape[1] == self.model_width and image.shape[0] == self.model_height:
            output = self._run(image)
        else:
            output = self._run_static_tiled(image)
        if hasattr(frame, "get_frame_np"):
            return Frame("onnx", output.shape[1], output.shape[0], frame.device,
                         frame.gpu_id, frame.hdr_mode, frame.dtype).set_frame_np(output)
        return output.tobytes()
