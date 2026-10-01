import ast
import re
import unittest
from pathlib import Path


# 仅加载倍率解析函数，保持这组回归不依赖 GPU、numpy 或 Pillow。
SCRIPT = Path(__file__).parents[1] / "embedded-tools" / "rve-image-backend.py"
tree = ast.parse(SCRIPT.read_text(encoding="utf-8-sig"))
function = next(node for node in tree.body if isinstance(node, ast.FunctionDef) and node.name == "model_scale")
namespace = {"Path": Path, "re": re}
exec(compile(ast.Module(body=[function], type_ignores=[]), str(SCRIPT), "exec"), namespace)
model_scale = namespace["model_scale"]


class ImageModelScaleTests(unittest.TestCase):
    def test_tensor_rt_cache_scale_is_recognized(self):
        for scale in (2, 3, 4):
            with self.subTest(scale=scale):
                self.assertEqual(scale, model_scale(Path(
                    f"realesr-animevideov3__input-64x48__scale-{scale}__tile-0__cfg-a.engine")))

    def test_cache_scale_overrides_native_scale_in_model_name(self):
        self.assertEqual(2, model_scale(Path("model-4x__input-64x48__scale-2__tile-0.engine")))

    def test_legacy_engine_and_ncnn_names_still_work(self):
        self.assertEqual(3, model_scale(Path("realesr-animevideov3-x3-tensorrt.engine")))
        self.assertEqual(2, model_scale(Path("RealESRGAN-AnimeVideoV3-2x")))

    def test_unrecognized_name_is_rejected(self):
        with self.assertRaises(ValueError):
            model_scale(Path("unknown-model.engine"))


if __name__ == "__main__":
    unittest.main()
