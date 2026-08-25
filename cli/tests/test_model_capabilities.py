import json
import unittest
from pathlib import Path


MANIFEST = Path(__file__).resolve().parents[1] / "model-capabilities.json"
PROGRAM_SOURCE = Path(__file__).resolve().parents[1] / "Program.cs"
PLUGIN_SOURCE = Path(__file__).resolve().parents[2] / "VideoEnhancerPlugin" / "PluginPanel.vb"
QUEUE_HOOK_SOURCE = Path(__file__).resolve().parents[2] / "VideoEnhancerPlugin" / "QueueHook.vb"


class ModelCapabilityManifestTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.document = json.loads(MANIFEST.read_text(encoding="utf-8"))
        cls.models = cls.document["models"]

    def test_manifest_is_unique_and_valid(self):
        self.assertEqual(1, self.document["schemaVersion"])
        self.assertEqual(93, len(self.models))
        names = [item["model"].casefold() for item in self.models]
        self.assertEqual(len(names), len(set(names)))
        for item in self.models:
            self.assertGreaterEqual(item["scale"], 1)
            self.assertTrue(item["architecture"])
            self.assertTrue(item["backends"])
            self.assertGreaterEqual(item.get("inputMultiple", 1), 1)

    def test_backend_coverage_matches_release_matrix(self):
        expected = {
            "ncnn": 21,
            "cuda": 42,
            "tensorrt": 36,
            "onnx": 28,
            "flashvsr": 1,
            "basicvsrpp": 1,
        }
        actual = {
            backend: sum(backend in item["backends"] for item in self.models)
            for backend in expected
        }
        self.assertEqual(expected, actual)

    def test_empirical_input_constraints_are_recorded(self):
        constrained = {
            item["model"]: item.get("inputMultiple", 1)
            for item in self.models
            if item.get("inputMultiple", 1) > 1
        }
        expected = {
            "ONNX/RealHatGAN-JP-Illustration-2x-fix1": 16,
            "ONNX/RealHatGAN-JP-Illustration-4x-fix1": 16,
            "ONNX/RealHatGAN-Universal-Illustration-2x-fix1": 16,
            "ONNX/RealHatGAN-x1-jp-Illustration-fix-only": 16,
            "PTH/AnimeSR-V2-4x": 4,
            "PTH/AniScale2-ESRGAN-i16-110K-2x": 2,
            "PTH/AniScale2-ESRGAN-Lite-i16-165K-2x": 2,
            "PTH/APISR-RRDB-GAN-generator-2x": 2,
        }
        self.assertEqual(expected, constrained)

    def test_onnx_manual_tiling_is_available_end_to_end(self):
        program = PROGRAM_SOURCE.read_text(encoding="utf-8-sig")
        plugin = PLUGIN_SOURCE.read_text(encoding="utf-8-sig")
        queue_hook = QUEUE_HOOK_SOURCE.read_text(encoding="utf-8-sig")
        self.assertIn('backend is ("ncnn" or "cuda" or "tensorrt" or "onnx")', program)
        self.assertIn('o.Backend is not ("ncnn" or "cuda" or "tensorrt" or "onnx")', program)
        self.assertIn('String.Equals(_config.Backend, "onnx", StringComparison.OrdinalIgnoreCase)', plugin)
        self.assertIn('String.Equals(backend, "onnx", StringComparison.OrdinalIgnoreCase)', queue_hook)
        self.assertIn('sb.Append(" -tile-size ")', queue_hook)


if __name__ == "__main__":
    unittest.main()
