import json
import pathlib
import unittest


ROOT = pathlib.Path(__file__).resolve().parents[2]


class UserModelImportContractTests(unittest.TestCase):
    def test_upscale_inspector_supports_safe_and_legacy_formats(self):
        source = (ROOT / "cli" / "embedded-tools" / "inspect_upscale_models.py").read_text(encoding="utf-8")
        compile(source, "inspect_upscale_models.py", "exec")
        for extension in (".pth", ".pt", ".ckpt", ".safetensors", ".onnx"):
            self.assertIn(extension, source)
        self.assertIn("ModelLoader(device=\"cpu\")", source)

    def test_user_catalog_is_transactional_and_hardware_neutral(self):
        source = (ROOT / "cli" / "UserModelCatalog.cs").read_text(encoding="utf-8")
        self.assertIn('"model-catalog.json"', source)
        self.assertIn('".staging-"', source)
        self.assertIn("Directory.Move(stagedModelDirectory, destinationDirectory)", source)
        self.assertIn("File.Move(temporary, path, overwrite: true)", source)
        self.assertNotIn("preferredTileSize", source)
        self.assertNotIn("6GB", source)

    def test_plugin_uses_lakeui_submenus_and_expected_tab_order(self):
        source = (ROOT / "VideoEnhancerPlugin" / "PluginPanel.vb").read_text(encoding="utf-8")
        self.assertIn("ModernContextMenu.ModernMenuItem", source)
        self.assertIn(".SubMenu = submenu", source)
        expected = ["超分工作台", "实时预览", "模型下载", "模型转换", "模型导入", "使用教程"]
        positions = [source.index(f'ModernTab("{name}")') for name in expected]
        self.assertEqual(positions, sorted(positions))
        self.assertNotIn('ModernTab("对比工具")', source)
        self.assertNotIn('ModernTab("模型指南")', source)

    def test_import_page_lists_models_and_exposes_capability_editor(self):
        source = (ROOT / "VideoEnhancerPlugin" / "PluginPanel.vb").read_text(encoding="utf-8")
        self.assertIn("Private ReadOnly _importModelList As New UltraDetailListView()", source)
        self.assertIn("_importModelList.ItemDoubleClick", source)
        self.assertIn("用户模型（双击修正能力）", source)
        self.assertIn("ShowUserModelCapabilityEditor", source)
        self.assertIn('"--update-user-model"', source)
        self.assertIn('_btnImportModel.Text = "预检并导入模型"', source)
        self.assertIn("_btnImportModel.Dock = DockStyle.Right", source)
        self.assertIn("ConfigureOfficialImportButton(_btnImportModel, UiSuccess)", source)
        self.assertIn("button.AnimationDuration = 0", source)
        self.assertIn("button.BackColor1 = Color.FromArgb(40, 220, 220, 220)", source)
        self.assertIn("button.TextAlign = ModernButton.TextAlignEnum.Center", source)
        self.assertNotIn("ConfigureImportButtonCaption", source)
        self.assertNotIn("_btnImportModel.Enabled = False", source)
        self.assertIn('_btnImportModel.Text = "正在预检并导入…"', source)

    def test_capability_updates_are_validated_and_affect_backend_lists(self):
        catalog = (ROOT / "cli" / "UserModelCatalog.cs").read_text(encoding="utf-8")
        program = (ROOT / "cli" / "Program.cs").read_text(encoding="utf-8")
        self.assertIn("UpdateCapabilities", catalog)
        self.assertIn("AllowedBackends", catalog)
        self.assertIn('format == "onnx"', catalog)
        self.assertIn('format == "ncnn"', catalog)
        self.assertIn("!user.Backends.Contains(backend", program)
        self.assertIn('case "--list-user-models"', program)

    def test_builtin_catalog_remains_valid_json(self):
        document = json.loads((ROOT / "cli" / "model-capabilities.json").read_text(encoding="utf-8"))
        self.assertEqual(document["schemaVersion"], 1)
        self.assertGreater(len(document["models"]), 0)


if __name__ == "__main__":
    unittest.main()
