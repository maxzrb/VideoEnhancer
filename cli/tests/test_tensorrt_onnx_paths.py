"""验证 ONNX TensorRT 转换可读取中文模型路径；GPU 回归按环境变量启用。"""
import ast
import contextlib
import importlib.util
import io
import os
import sys
import tempfile
import types
import unittest
from pathlib import Path
from unittest.mock import MagicMock, patch

CONVERTER = Path(__file__).resolve().parents[1] / "embedded-tools/convert_tensorrt.py"


class StopAfterParse(Exception):
    pass


class OnnxPathParsingTests(unittest.TestCase):
    def setUp(self):
        # 只执行解析入口，常规回归不依赖 CUDA、PyTorch 或 TensorRT。
        tree = ast.parse(CONVERTER.read_text(encoding="utf-8-sig"))
        function = next(node for node in tree.body if isinstance(node, ast.FunctionDef) and node.name == "convert_onnx")
        namespace = {}
        exec(compile(ast.Module(body=[function], type_ignores=[]), str(CONVERTER), "exec"), namespace)
        self.convert = namespace["convert_onnx"]
        self.model = MagicMock()
        self.model.graph.initializer = []
        self.model.metadata_props = []
        self.payload = b"\x08\x09\x00onnx-payload"
        self.model.SerializeToString.return_value = self.payload
        self.onnx = types.ModuleType("onnx")
        self.onnx.load = MagicMock(return_value=self.model)
        self.onnx.TensorProto = types.SimpleNamespace(EXTERNAL=1)
        self.parser = MagicMock()
        self.parser.parse_from_file.side_effect = AssertionError("不得把中文路径交给原生文件接口")
        self.network = MagicMock()
        self.network.get_input.side_effect = StopAfterParse
        builder = MagicMock()
        builder.create_network.return_value = self.network
        trt = types.ModuleType("tensorrt")
        trt.Logger = MagicMock()
        trt.Logger.WARNING = 0
        trt.Builder = MagicMock(return_value=builder)
        trt.NetworkDefinitionCreationFlag = types.SimpleNamespace(EXPLICIT_BATCH=0)
        trt.OnnxParser = MagicMock(return_value=self.parser)
        runtime = types.ModuleType("torch_tensorrt.dynamo.runtime")
        runtime.TorchTensorRTModule = MagicMock()
        self.modules = patch.dict(sys.modules, {
            "onnx": self.onnx, "tensorrt": trt,
            "torch_tensorrt": types.ModuleType("torch_tensorrt"),
            "torch_tensorrt.dynamo": types.ModuleType("torch_tensorrt.dynamo"),
            "torch_tensorrt.dynamo.runtime": runtime,
        })
        self.modules.start()
        self.addCleanup(self.modules.stop)
        self.args = types.SimpleNamespace(input_multiple=1)

    def test_unicode_path_is_loaded_by_onnx_and_parsed_as_bytes(self):
        source = str(Path("中文 目录") / "超分 模型.onnx")
        self.parser.parse.return_value = True
        self.network.num_inputs = self.network.num_outputs = 1
        with contextlib.redirect_stdout(io.StringIO()), self.assertRaises(StopAfterParse):
            self.convert(self.args, source, None)
        self.onnx.load.assert_called_once_with(source, load_external_data=False)
        self.parser.parse.assert_called_once_with(self.payload)
        self.parser.parse_from_file.assert_not_called()

    def test_parser_errors_are_still_reported(self):
        self.parser.parse.return_value = False
        self.parser.num_errors = 1
        self.parser.get_error.return_value = "unsupported test operator"
        with contextlib.redirect_stdout(io.StringIO()), self.assertRaisesRegex(
                ValueError, "TensorRT ONNX parse failed: unsupported test operator"):
            self.convert(self.args, "模型.onnx", None)

    def test_external_weights_are_rejected_before_serializing_or_parsing(self):
        self.model.graph.initializer = [types.SimpleNamespace(data_location=1)]
        with self.assertRaisesRegex(ValueError, "self-contained ONNX"):
            self.convert(self.args, "外置权重.onnx", None)
        self.model.SerializeToString.assert_not_called()
        self.parser.parse.assert_not_called()


@unittest.skipUnless(os.environ.get("VIDEOENHANCER_TEST_TRT") == "1",
                     "设置 VIDEOENHANCER_TEST_TRT=1 才运行真实 GPU 回归")
class OnnxUnicodePathGpuTests(unittest.TestCase):
    def test_chinese_input_path_builds_and_runs_an_engine(self):
        import numpy as np
        import onnx
        import torch
        from onnx import helper, numpy_helper

        backend = os.environ.get("VIDEOENHANCER_TEST_RVE_BACKEND")
        if backend:
            sys.path.insert(0, backend)
            self.addCleanup(lambda: sys.path.remove(backend))
        if not torch.cuda.is_available():
            self.skipTest("需要 CUDA GPU")
        spec = importlib.util.spec_from_file_location("unicode_path_converter", CONVERTER)
        converter = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(converter)
        with tempfile.TemporaryDirectory() as folder:
            source = Path(folder) / "中文 目录" / "超分 模型.onnx"
            source.parent.mkdir()
            # 输出使用 ASCII 文件名，单独验证 ONNX 输入路径，避免混入 RVE 加载器的路径问题。
            output = Path(folder) / "unicode-input.engine"
            graph = helper.make_graph(
                [helper.make_node("Resize", ["input", "", "scales"], ["output"],
                                  mode="nearest", coordinate_transformation_mode="asymmetric",
                                  nearest_mode="floor")],
                "unicode_path_upscale",
                [helper.make_tensor_value_info("input", onnx.TensorProto.FLOAT, [1, 3, 8, 8])],
                [helper.make_tensor_value_info("output", onnx.TensorProto.FLOAT, [1, 3, 16, 16])],
                [numpy_helper.from_array(np.array([1, 1, 2, 2], dtype=np.float32), "scales")],
            )
            model = helper.make_model(graph, opset_imports=[helper.make_opsetid("", 18)])
            model.ir_version = 9
            onnx.checker.check_model(model)
            onnx.save(model, source)
            arguments = ["convert_tensorrt.py", str(source), "--output", str(output),
                         "--width", "8", "--height", "8", "--precision", "fp32"]
            with patch.object(sys, "argv", arguments), contextlib.redirect_stdout(io.StringIO()):
                converter.main()
            self.assertTrue(output.is_file())
            with output.open("rb") as stream:
                engine = torch.jit.load(stream, map_location="cuda").eval()
            value = torch.rand((1, 3, 8, 8), device="cuda")
            with torch.no_grad():
                actual = engine(value)
            expected = torch.nn.functional.interpolate(value, scale_factor=2, mode="nearest")
            torch.testing.assert_close(actual, expected, atol=1e-5, rtol=1e-5)


if __name__ == "__main__":
    unittest.main()
