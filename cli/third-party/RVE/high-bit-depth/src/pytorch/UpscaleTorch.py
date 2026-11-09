import os
import math
import base64
import re

import gc
from .TorchUtils import TorchUtils
from .UpscaleModelWrapper import UpscaleModelWrapper
import torch as torch
import torch.nn.functional as F
import sys
from time import sleep

from ..utils.Util import log, CudaChecker
from ..utils.Frame import Frame
HAS_PYTORCH_CUDA = CudaChecker().HAS_PYTORCH_CUDA
import numpy as np


class _PrebuiltTensorRTEngine:
    """Adapter for a prebuilt Torch-TensorRT TorchScript ``.engine`` file."""

    def __init__(self, path, device, precision):
        if device.type != "cuda":
            raise ValueError("A prebuilt TensorRT engine can only run on a CUDA device.")

        # Importing torch_tensorrt registers the custom TensorRT operators used
        # by the serialized TorchScript module before torch.jit.load sees them.
        try:
            import torch_tensorrt  # noqa: F401
        except ImportError as exc:
            raise RuntimeError(
                "Loading a prebuilt .engine model requires torch_tensorrt."
            ) from exc

        try:
            self.__model = torch.jit.load(path, map_location=device).eval()
        except Exception as exc:
            raise RuntimeError(
                f"Failed to load TensorRT engine '{path}'. The backend expects a "
                "Torch-TensorRT TorchScript .engine file. "
                "该 engine 可能来自不同 GPU/架构，需要在当前 GPU 上重新编译。"
            ) from exc

        self.__device = device
        metadata = self.__inspect_engine()
        self.__input_name = metadata["input_name"]
        self.__input_min_shape = metadata["input_min_shape"]
        self.__input_opt_shape = metadata["input_opt_shape"]
        self.__input_max_shape = metadata["input_max_shape"]
        self.__precision = metadata["input_dtype"]

        if precision in (torch.float16, torch.float32) and precision != self.__precision:
            log(
                f"TensorRT engine input precision is {self.__precision}; "
                f"overriding requested precision {precision}."
            )

        input_shape = self.__input_opt_shape
        output_shape = metadata["output_shape"]
        scale_h = output_shape[-2] / input_shape[-2]
        scale_w = output_shape[-1] / input_shape[-1]
        if scale_h == scale_w and scale_h.is_integer():
            self.__scale = int(scale_h)
        else:
            name = os.path.basename(path).lower()
            match = re.search(r"(?:^|[-_])x([1-8])(?:[-_.]|$)", name)
            if not match:
                raise ValueError(
                    "Could not determine the upscale factor from TensorRT engine I/O shapes "
                    f"{input_shape} -> {output_shape}."
                )
            self.__scale = int(match.group(1))

        if input_shape[0] != 1 or input_shape[1] != 3:
            raise ValueError(
                "Only single-image, three-channel NCHW TensorRT upscale engines are supported; "
                f"engine input is {input_shape}."
            )

        log(
            f"Loaded prebuilt TensorRT engine: input profile "
            f"{self.__input_min_shape} / {self.__input_opt_shape} / "
            f"{self.__input_max_shape}, precision {self.__precision}, scale {self.__scale}x"
        )

    def __inspect_engine(self):
        """Read TensorRT I/O metadata embedded in the TorchScript module."""
        try:
            import tensorrt as trt

            runtime_module = next(
                module
                for _, module in self.__model.named_modules()
                if hasattr(module, "engine")
            )
            engine_state = runtime_module.engine.__getstate__()[0]
            serialized_engine = engine_state[3]
            if isinstance(serialized_engine, str):
                serialized_engine = base64.b64decode(serialized_engine)

            logger = trt.Logger(trt.Logger.ERROR)
            runtime = trt.Runtime(logger)
            engine = runtime.deserialize_cuda_engine(serialized_engine)
            if engine is None:
                raise RuntimeError("TensorRT rejected the serialized engine data.")

            input_names = []
            output_names = []
            for index in range(engine.num_io_tensors):
                name = engine.get_tensor_name(index)
                if engine.get_tensor_mode(name) == trt.TensorIOMode.INPUT:
                    input_names.append(name)
                else:
                    output_names.append(name)
            if len(input_names) != 1 or len(output_names) != 1:
                raise ValueError(
                    "TensorRT upscale engines must have exactly one input and one output; "
                    f"found {len(input_names)} input(s) and {len(output_names)} output(s)."
                )

            input_name = input_names[0]
            output_name = output_names[0]
            input_shape = tuple(engine.get_tensor_shape(input_name))
            if any(dimension < 0 for dimension in input_shape):
                minimum, optimum, maximum = engine.get_tensor_profile_shape(input_name, 0)
                input_min_shape = tuple(minimum)
                input_opt_shape = tuple(optimum)
                input_max_shape = tuple(maximum)
            else:
                input_min_shape = input_opt_shape = input_max_shape = input_shape

            context = engine.create_execution_context()
            if context is None or not context.set_input_shape(input_name, input_opt_shape):
                raise RuntimeError(
                    f"TensorRT could not select the optimum input shape {input_opt_shape}."
                )
            output_shape = tuple(context.get_tensor_shape(output_name))
            if any(dimension < 1 for dimension in output_shape):
                raise RuntimeError(
                    f"TensorRT returned an invalid output shape {output_shape}."
                )

            dtype_map = {
                trt.DataType.HALF: torch.float16,
                trt.DataType.FLOAT: torch.float32,
            }
            input_trt_dtype = engine.get_tensor_dtype(input_name)
            if input_trt_dtype not in dtype_map:
                raise ValueError(
                    f"Unsupported TensorRT engine input precision: {input_trt_dtype}."
                )

            return {
                "input_name": input_name,
                "input_min_shape": input_min_shape,
                "input_opt_shape": input_opt_shape,
                "input_max_shape": input_max_shape,
                "input_dtype": dtype_map[input_trt_dtype],
                "output_shape": output_shape,
            }
        except (ValueError, RuntimeError):
            raise
        except Exception as exc:
            raise RuntimeError(
                "Failed to inspect the TensorRT engine metadata. The .engine file may be "
                "incompatible with the installed TensorRT/Torch-TensorRT runtime. "
                "该 engine 需要在当前 GPU 上重新编译。"
            ) from exc

    def get_scale(self):
        return self.__scale

    def get_input_profile(self):
        return (
            self.__input_min_shape,
            self.__input_opt_shape,
            self.__input_max_shape,
        )

    def get_input_dtype(self):
        return self.__precision

    def supports_shape(self, width, height):
        shape = (1, 3, height, width)
        return all(
            minimum <= actual <= maximum
            for actual, minimum, maximum in zip(
                shape, self.__input_min_shape, self.__input_max_shape
            )
        )

    def select_input_shape(self, width, height):
        """Choose a valid engine shape for padding or tiled inference."""
        selected = []
        requested = (1, 3, height, width)
        for actual, minimum, maximum in zip(
            requested, self.__input_min_shape, self.__input_max_shape
        ):
            selected.append(max(minimum, min(actual, maximum)))
        return tuple(selected)

    def get_dummy_input(self, width, height):
        return torch.zeros(
            (1, 3, height, width),
            dtype=self.__precision,
            device=self.__device,
        )

    def get_model(self):
        return self.__model

    def load_model(self, model):
        self.__model = model.eval()

    def __call__(self, image):
        height, width = image.shape[-2:]
        if not self.supports_shape(width, height):
            raise ValueError(
                f"Input tensor shape {tuple(image.shape)} is outside the TensorRT engine "
                f"profile {self.__input_min_shape} .. {self.__input_max_shape}."
            )
        image = image.to(
            device=self.__device,
            dtype=self.__precision,
            non_blocking=True,
        ).contiguous()
        return self.__model(image)


def process_output(output, hdr_mode):
    # Step 1: Squeeze the first dimension
    output = np.squeeze(output, axis=0)

    # Step 2: Permute axes from (C, H, W) to (H, W, C)
    output = np.transpose(output, (1, 2, 0))

    # Step 3: Clamp values to the range [0, 1]
    output = np.clip(output, 0, 1)

    # Step 4: Multiply by scaling factor
    scale_factor = 65535.0
    output = output * scale_factor

    # Step 5: Convert to appropriate dtype
    dtype = np.uint16
    output = output.astype(dtype)

    return output

class UpscalePytorch:
    """A class for upscaling images using PyTorch.

    Args:
        modelPath (str): The path to the model file.
        device (str, optional): The device to use for inference. Defaults to "default".
        tile_pad (int, optional): The padding size for tiles. Defaults to 10.
        precision (str, optional): The precision mode for the model. Defaults to "auto".
        width (int, optional): The width of the input image. Defaults to 1920.
        height (int, optional): The height of the input image. Defaults to 1080.
        backend (str, optional): The backend for inference. Defaults to "pytorch".
        trt_workspace_size (int, optional): The workspace size for TensorRT. Defaults to 0.
        trt_cache_dir (str, optional): The cache directory for TensorRT. Defaults to modelsDirectory().

    Attributes:
        tile_pad (int): The padding size for tiles.
        dtype (torch.dtype): The data type for the model.
        device (torch.device): The device used for inference.
        model (torch.nn.Module): The loaded model.
        width (int): The width of the input image.
        height (int): The height of the input image.
        scale (float): The scale factor of the model.

    Methods:
        handlePrecision(precision): Handles the precision mode for the model.
        loadModel(modelPath, dtype, device): Loads the model from file.
        bytesToFrame(frame): Converts bytes to a torch tensor.
        tensorToNPArray(image): Converts a torch tensor to a NumPy array.
        renderImage(image): Renders an image using the model.
        renderToNPArray(image): Renders an image and returns it as a NumPy array.
        renderImagesInDirectory(dir): Renders all images in a directory.
        getScale(): Returns the scale factor of the model.
        saveImage(image, fullOutputPathLocation): Saves an image to a file.
        renderTiledImage(image, tile_size): Renders a tiled image."""

    @torch.inference_mode()
    def __init__(
        self,
        modelPath: str,
        device="default",
        tile_pad: int = 10,
        precision: str = "auto",
        width: int = 1920,
        height: int = 1080,
        tilesize: int = 0,
        backend: str = "pytorch",
        gpu_id: int = 0,
        hdr_mode: bool = False,
        override_upscale_scale: int | None = None,
        # trt options
        trt_workspace_size: int = 0,
        trt_optimization_level: int = 3,
        trt_max_aux_streams: int | None = None,
        trt_debug: bool = False,
        trt_static_shape: bool = False,

    ):
        self.torchUtils = TorchUtils(width=width, height=height,hdr_mode=hdr_mode,device_type=device)  
        device = self.torchUtils.handle_device(device, gpu_id=gpu_id)
        self.dtype = self.torchUtils.handle_precision(precision)
        self.tile_pad = tile_pad
        self.device = device
        self.videoWidth = width
        self.videoHeight = height
        self.tilesize = tilesize
        # CLI 的本地能力清单为已知模型声明真实的输入尺寸倍数。
        self.input_multiple = max(1, int(os.environ.get("VIDEOENHANCER_UPSCALE_INPUT_MULTIPLE", "1")))
        self.tile = [self.tilesize, self.tilesize]
        self.use_tiling = self.tilesize > 0
        self.modelPath = modelPath
        self.backend = backend
      
        self.trt_workspace_size = trt_workspace_size
        self.trt_optimization_level = trt_optimization_level
        self.trt_aux_streams = trt_max_aux_streams
        self.trt_debug = trt_debug
        
        self.hdr_mode = hdr_mode         

        self.trt_static_shape = trt_static_shape

        # streams
        self.stream = self.torchUtils.init_stream(gpu_id=gpu_id)
        self.f2tstream = self.torchUtils.init_stream(gpu_id=gpu_id)
        self.prepareStream = self.torchUtils.init_stream(gpu_id=gpu_id)
        self.convertStream = self.torchUtils.init_stream(gpu_id=gpu_id)
        self._load()

    @torch.inference_mode()
    def _load(self):

        self.trt_min_shape = [128, 128]
        self.trt_opt_shape = [1920, 1080]
        self.trt_max_shape = [1920, 1920]
        

        if self.videoWidth > 1920 or self.videoHeight > 1920 and not self.trt_static_shape:
            log("The video resolution is very large for TensorRT dynamic shape and will use a lot of VRAM, falling back to static shape")
            self.trt_static_shape = True

        if self.videoWidth < 128 or self.videoHeight < 128 and not self.trt_static_shape:
            log("The video resolution is too small for TensorRT dynamic shape, falling back to static shape")
            self.trt_static_shape = True

        
        with self.torchUtils.run_stream(self.prepareStream):
            if self.backend == "tensorrt" and os.path.splitext(self.modelPath)[1].lower() == ".engine":
                self.upscale_model_wrapper = _PrebuiltTensorRTEngine(self.modelPath, self.device, self.dtype)
                self.scale = self.upscale_model_wrapper.get_scale()
                self.dtype = self.upscale_model_wrapper.get_input_dtype()

                if self.tilesize == 0 and self.upscale_model_wrapper.supports_shape(
                    self.videoWidth, self.videoHeight
                ):
                    self.pad_w = self.videoWidth
                    self.pad_h = self.videoHeight
                    self.use_tiling = False
                else:
                    selected_shape = self.upscale_model_wrapper.select_input_shape(
                        self.videoWidth if self.tilesize == 0 else self.tilesize + 2 * self.tile_pad,
                        self.videoHeight if self.tilesize == 0 else self.tilesize + 2 * self.tile_pad,
                    )
                    self.pad_h, self.pad_w = selected_shape[-2:]
                    max_tile_w = self.pad_w - 2 * self.tile_pad
                    max_tile_h = self.pad_h - 2 * self.tile_pad
                    if max_tile_w < 1 or max_tile_h < 1:
                        raise ValueError(
                            f"TensorRT engine input {selected_shape} is too small for "
                            f"tile padding {self.tile_pad}."
                        )
                    requested_tile = self.tilesize if self.tilesize > 0 else max(
                        max_tile_w, max_tile_h
                    )
                    self.tile = [
                        min(requested_tile, max_tile_w),
                        min(requested_tile, max_tile_h),
                    ]
                    self.use_tiling = True
                    log(
                        f"Using TensorRT tiled inference with content tile "
                        f"{self.tile[0]}x{self.tile[1]} and engine input "
                        f"{self.pad_w}x{self.pad_h}."
                    )
                self.torchUtils.clear_cache()
                self.torchUtils.sync_all_streams()
                return
            self.upscale_model_wrapper = UpscaleModelWrapper(
                model_path=self.modelPath,
                device=self.device,
                precision=self.dtype
            )
            self.scale = self.upscale_model_wrapper.get_scale()
            
            match self.scale:
                case 1:
                    modulo = 4
                case 2:
                    modulo = 2
                case _:
                    modulo = 1
            modulo = math.lcm(modulo, self.input_multiple)
            if all(t > 0 for t in self.tile):
                self.pad_w = (
                    math.ceil(
                        min(self.tile[0] + 2 * self.tile_pad, self.videoWidth) / modulo
                    )
                    * modulo
                )
                self.pad_h = (
                    math.ceil(
                        min(self.tile[1] + 2 * self.tile_pad, self.videoHeight) / modulo
                    )
                    * modulo
                )
            else:
                modulo = 1 if self.videoWidth < 720 or self.videoHeight < 720 else 1
                self.pad_w = math.ceil(self.videoWidth / modulo) * modulo
                self.pad_h = math.ceil(self.videoHeight / modulo) * modulo

            if self.backend == "tensorrt":
                self.tensorrt_example_inputs = (self.upscale_model_wrapper.get_dummy_input(self.pad_w, self.pad_h),) # gotta make a tuple cause im dumb.
                from .TensorRTHandler import TorchTensorRTHandler

                trtHandler = TorchTensorRTHandler(
                    model_parent_path=os.path.dirname(self.modelPath),
                    trt_optimization_level=self.trt_optimization_level,

                )
                static_dimensions = f"{self.pad_w}x{self.pad_h}"
                
                for i in range(2):
                    self.trt_min_shape[i] = math.ceil(self.trt_min_shape[i] / modulo) * modulo
                    self.trt_opt_shape[i] = math.ceil(self.trt_opt_shape[i] / modulo) * modulo
                    self.trt_max_shape[i] = math.ceil(self.trt_max_shape[i] / modulo) * modulo

                dynamic_dimensions = (
                    f"min-{self.trt_min_shape[0]}x{self.trt_min_shape[1]}"
                    f"_opt-{self.trt_opt_shape[0]}x{self.trt_opt_shape[1]}"
                    f"_max-{self.trt_max_shape[0]}x{self.trt_max_shape[1]}"
                )
                self.trt_engine_name = os.path.join(
                    (
                        f"{os.path.basename(self.modelPath)}"
                        + f"_{'fp16' if self.dtype == torch.float16 else 'fp32'}"
                        + f"_{torch.cuda.get_device_name(self.device)}"
                        + f"_trt-{trtHandler.tensorrt_version}"
                        + f"_torch_tensorrt-{trtHandler.torch_tensorrt_version}"
                        + f"_opt-{self.trt_optimization_level}"
                        + (
                            f"_workspace-{self.trt_workspace_size}"
                            if self.trt_workspace_size > 0
                            else ""
                        )
                    ),
                )
                self.trt_engine_static_name = self.trt_engine_name + f"_{static_dimensions}.engine"
                self.trt_engine_dynamic_name = self.trt_engine_name + f"_{dynamic_dimensions}.engine"
                self.trt_engine_name = self.trt_engine_static_name if self.trt_static_shape else self.trt_engine_dynamic_name

                if not trtHandler.check_engine_exists(self.trt_engine_name):
                    
                    if self.trt_static_shape:
                        dynamic_shapes = None
                        
                    else:
                        self.trt_min_shape.reverse()
                        self.trt_opt_shape.reverse()
                        self.trt_max_shape.reverse()

                        _height = torch.export.Dim("height", min=self.trt_min_shape[0] // modulo, max=self.trt_max_shape[0] // modulo)
                        _width = torch.export.Dim("width", min=self.trt_min_shape[1] // modulo, max=self.trt_max_shape[1] // modulo)
                        dim_height = _height * modulo
                        dim_width = _width * modulo
                        dynamic_shapes = {"x": {2: dim_height, 3: dim_width}}

                    try:
                        trt_engine = trtHandler.build_engine(
                            self.upscale_model_wrapper.get_model(),
                            self.dtype,
                            self.device,
                            example_inputs=self.tensorrt_example_inputs,
                            trt_engine_name=self.trt_engine_name,
                            trt_multi_precision_engine=False,
                            dynamic_shapes=dynamic_shapes,
                        )
                        trtHandler.save_engine(
                            trt_engine,
                            self.trt_engine_name,
                            example_inputs=self.tensorrt_example_inputs,
                        )

                    except Exception as e:
                        if dynamic_shapes is not None:
                            print(f"ERROR: building TensorRT engine with dynamic shapes, trying without.\n", file=sys.stderr)

                            if trtHandler.check_engine_exists(self.trt_engine_static_name):
                                trtHandler.load_engine(
                                    trt_engine_name=self.trt_engine_static_name
                                )

                            else:

                                trt_engine = trtHandler.build_engine(
                                    self.upscale_model_wrapper.get_model(),
                                    self.dtype,
                                    self.device,
                                    example_inputs=self.tensorrt_example_inputs,
                                    trt_engine_name=self.trt_engine_static_name,
                                    trt_multi_precision_engine=False,
                                )
                                trtHandler.save_engine(
                                    trt_engine,
                                    self.trt_engine_static_name,
                                    example_inputs=self.tensorrt_example_inputs,
                                )
                            self.trt_engine_name = self.trt_engine_static_name
                        else:
                            raise RuntimeError(
                                f"Failed to build TensorRT engine: {e}\n"
                            )
                model = trtHandler.load_engine(trt_engine_name=self.trt_engine_name)
                self.upscale_model_wrapper.load_model(model)
                

        self.torchUtils.clear_cache()
        self.torchUtils.sync_all_streams()

    @torch.inference_mode()
    def hotUnload(self):
        self.upscale_model_wrapper = None
        gc.collect()
        self.torchUtils.clear_cache()
        if HAS_PYTORCH_CUDA:
            torch.cuda.reset_max_memory_allocated()
            torch.cuda.reset_max_memory_cached()

    @torch.inference_mode()
    def hotReload(self):
        self._load()
    
    @torch.inference_mode()
    def __call__(self, image: Frame) -> Frame:
        dtype = image.dtype
        gpu_id = image.gpu_id
        device = image.device
        with self.torchUtils.run_stream(self.f2tstream):  # type: ignore
            image_tensor = image.get_frame_tensor()
        del image
        self.torchUtils.sync_stream(self.f2tstream)

        with self.torchUtils.run_stream(self.stream):
            while self.upscale_model_wrapper is None:
                sleep(1)
            if not self.use_tiling:
                original_h, original_w = image_tensor.shape[-2:]
                pad_h = (-original_h) % self.input_multiple
                pad_w = (-original_w) % self.input_multiple
                if pad_h or pad_w:
                    image_tensor = F.pad(image_tensor, (0, pad_w, 0, pad_h), "replicate")
                output = self.upscale_model_wrapper(image_tensor)
                output = output[:, :, : original_h * self.scale, : original_w * self.scale]
            else:
                output = self.renderTiledImage(image_tensor)
            
            retFrame = Frame(self.backend, self.videoWidth * self.scale, self.videoHeight * self.scale, device=device, gpu_id=gpu_id, hdr_mode=self.hdr_mode, dtype=dtype)
            
            retFrame.set_frame_tensor(output)
        
        self.torchUtils.sync_stream(self.stream)
        return retFrame
 
    def getScale(self):
        return self.upscale_model_wrapper.get_scale()

    @torch.inference_mode()
    def renderTiledImage(
        self,
        img: torch.Tensor,
    ) -> torch.Tensor:
        scale = self.scale
        tile = self.tile
        tile_pad = self.tile_pad

        batch, channel, height, width = img.shape
        output_shape = (batch, channel, height * scale, width * scale)

        # start with black image
        output = img.new_zeros(output_shape).to(device=self.device, dtype=self.dtype)

        tiles_x = math.ceil(width / tile[0])
        tiles_y = math.ceil(height / tile[1])

        # loop over all tiles
        for y in range(tiles_y):
            for x in range(tiles_x):
                # extract tile from input image
                ofs_x = x * tile[0]
                ofs_y = y * tile[1]

                # input tile area on total image
                input_start_x = ofs_x
                input_end_x = min(ofs_x + tile[0], width)
                input_start_y = ofs_y
                input_end_y = min(ofs_y + tile[1], height)

                # input tile area on total image with padding
                input_start_x_pad = max(input_start_x - tile_pad, 0)
                input_end_x_pad = min(input_end_x + tile_pad, width)
                input_start_y_pad = max(input_start_y - tile_pad, 0)
                input_end_y_pad = min(input_end_y + tile_pad, height)

                # input tile dimensions
                input_tile_width = input_end_x - input_start_x
                input_tile_height = input_end_y - input_start_y

                input_tile = img[
                    :,
                    :,
                    input_start_y_pad:input_end_y_pad,
                    input_start_x_pad:input_end_x_pad,
                ].to(device=self.device, dtype=self.dtype)

                h, w = input_tile.shape[2:]
                input_tile = F.pad(
                    input_tile, (0, self.pad_w - w, 0, self.pad_h - h), "replicate"
                )

                # process tile
                output_tile = self.upscale_model_wrapper(
                    input_tile
                )

                output_tile = output_tile[:, :, : h * scale, : w * scale]

                # output tile area on total image
                output_start_x = input_start_x * scale
                output_end_x = input_end_x * scale
                output_start_y = input_start_y * scale
                output_end_y = input_end_y * scale

                # output tile area without padding
                output_start_x_tile = (input_start_x - input_start_x_pad) * scale
                output_end_x_tile = output_start_x_tile + input_tile_width * scale
                output_start_y_tile = (input_start_y - input_start_y_pad) * scale
                output_end_y_tile = output_start_y_tile + input_tile_height * scale

                # put tile into output image
                output[
                    :, :, output_start_y:output_end_y, output_start_x:output_end_x
                ] = output_tile[
                    :,
                    :,
                    output_start_y_tile:output_end_y_tile,
                    output_start_x_tile:output_end_x_tile,
                ]

        return output
