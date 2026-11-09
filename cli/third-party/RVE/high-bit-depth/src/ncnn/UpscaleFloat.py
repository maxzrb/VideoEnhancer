"""通过浮点 NCNN Mat 处理高位深 RGB，避开原生包装器的 8bit 像素接口。"""
from pathlib import Path
import numpy as np
from ..utils.PixelFormat import to_uint16


def reflect_pad(image: np.ndarray, tile: int, pad: int) -> tuple[np.ndarray, int, int]:
    height, width = image.shape[:2]
    extra_h = (tile - height % tile) % tile
    extra_w = (tile - width % tile) % tile
    mode = "reflect" if height > 1 and width > 1 else "edge"
    return np.pad(image, ((pad, extra_h + pad), (pad, extra_w + pad), (0, 0)), mode=mode), extra_h, extra_w


def tiled_rgb(run_block, image: np.ndarray, scale: int, tile: int, pad: int) -> np.ndarray:
    padded, extra_h, extra_w = reflect_pad(image, tile, pad)
    height, width = padded.shape[:2]
    output = np.zeros((height * scale, width * scale, 3), dtype=np.uint16)
    tiles_x = (width - 2 * pad + tile - 1) // tile
    tiles_y = (height - 2 * pad + tile - 1) // tile
    for y in range(tiles_y):
        for x in range(tiles_x):
            x0, y0 = x * tile + pad, y * tile + pad
            x1, y1 = min(x0 + tile, width - pad), min(y0 + tile, height - pad)
            px0, py0, px1, py1 = x0 - pad, y0 - pad, x1 + pad, y1 + pad
            block = padded[py0:py1, px0:px1]
            block_out = run_block(block)
            loss_x = block.shape[1] * scale - block_out.shape[1]
            loss_y = block.shape[0] * scale - block_out.shape[0]
            crop_x = (x0 - px0) * scale - max(0, loss_x // 2)
            crop_y = (y0 - py0) * scale - max(0, loss_y // 2)
            output[y0*scale:y1*scale, x0*scale:x1*scale] = block_out[
                crop_y:crop_y+(y1-y0)*scale, crop_x:crop_x+(x1-x0)*scale
            ]
    top = left = pad * scale
    return output[top:output.shape[0]-(extra_h+pad)*scale,
                  left:output.shape[1]-(extra_w+pad)*scale]


class NCNNImageUpscaler:
    def __init__(self, model_base: Path, scale: int, gpu_id: int = 0, tile: int = 256, pad: int = 10):
        import ncnn
        self.ncnn = ncnn
        self.scale = scale
        self.tile = tile or 256
        self.pad = pad
        self.input_name, self.output_name = self._blob_names(model_base.with_suffix(".param"))
        self.net = ncnn.Net()
        self.net.opt.use_vulkan_compute = True
        self.net.opt.use_fp16_packed = False
        self.net.opt.use_fp16_storage = False
        self.net.opt.use_fp16_arithmetic = False
        self.net.set_vulkan_device(gpu_id)
        self.net.load_param(str(model_base.with_suffix(".param")))
        self.net.load_model(str(model_base.with_suffix(".bin")))
        print(f"Using GPU: {ncnn.get_gpu_device(gpu_id).info().device_name()}; NCNN blobs {self.input_name} -> {self.output_name}", flush=True)

    @staticmethod
    def _blob_names(param_path: Path) -> tuple[str, str]:
        input_name, output_name = "data", "output"
        for raw in param_path.read_text(encoding="utf-8", errors="ignore").splitlines():
            parts = raw.split()
            if len(parts) < 5 or parts[0].startswith("#"):
                continue
            try:
                input_count, output_count = int(parts[2]), int(parts[3])
            except ValueError:
                continue
            outputs = parts[4 + input_count:4 + input_count + output_count]
            if parts[0] == "Input" and outputs:
                input_name = outputs[0]
            if outputs:
                output_name = outputs[-1]
        return input_name, output_name

    def _run(self, rgb: np.ndarray) -> np.ndarray:
        height, width = rgb.shape[:2]
        value = to_uint16(rgb).astype(np.float32) / 65535.0
        # Mat 借用 NumPy 内存，必须保留数组直到 extractor 完成计算。
        planar = np.ascontiguousarray(value.transpose(2, 0, 1))
        mat = self.ncnn.Mat(planar)
        extractor = self.net.create_extractor()
        input_result = extractor.input(self.input_name, mat)
        if input_result != 0:
            raise RuntimeError(f"NCNN 输入节点 {self.input_name} 返回错误码 {input_result}")
        result, output = extractor.extract(self.output_name)
        if result != 0:
            raise RuntimeError(f"NCNN 输出节点 {self.output_name} 返回错误码 {result}")
        value = np.asarray(output)
        if value.ndim != 3:
            raise RuntimeError(f"NCNN 输出形状异常：{value.shape}")
        return to_uint16(value.astype(np.float32).transpose(1, 2, 0))

    def __call__(self, rgb: np.ndarray) -> np.ndarray:
        return tiled_rgb(self._run, rgb, self.scale, tile=self.tile, pad=self.pad)

