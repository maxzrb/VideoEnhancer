import json
import pathlib
import unittest


ROOT = pathlib.Path(__file__).resolve().parents[2]
PLUGIN = ROOT / "VideoEnhancerPlugin"
CLI = ROOT / "cli"


def read_plugin_panel_sources():
    paths = [PLUGIN / "PluginPanel.vb"]
    paths.extend(sorted((PLUGIN / "Pages").glob("PluginPanel.*Page.vb")))
    return "\n".join(path.read_text(encoding="utf-8-sig") for path in paths)


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
        self.assertIn('".deleting-"', source)
        self.assertIn("internal static UserModelRecord Delete", source)
        self.assertIn("Directory.Move(stagedModelDirectory, destinationDirectory)", source)
        self.assertIn("File.Move(temporary, path, overwrite: true)", source)
        self.assertNotIn("preferredTileSize", source)
        self.assertNotIn("6GB", source)

    def test_plugin_uses_lakeui_submenus_and_expected_tab_order(self):
        source = read_plugin_panel_sources()
        self.assertIn("ModernContextMenu.ModernMenuItem", source)
        self.assertIn(".SubMenu = submenu", source)
        expected = ["超分工作台", "实时预览", "模型下载", "模型转换", "模型导入", "分段超分", "右键超分", "使用教程"]
        positions = [source.index(f'ModernTab("{name}")') for name in expected]
        self.assertEqual(positions, sorted(positions))
        self.assertNotIn('ModernTab("对比工具")', source)
        self.assertNotIn('ModernTab("模型指南")', source)

    def test_segmented_page_and_shell_per_item_delete_are_wired(self):
        panel = read_plugin_panel_sources()
        source = panel
        segmented = (PLUGIN / "Pages" / "PluginPanel.SegmentedUpscalePage.vb").read_text(encoding="utf-8") + (
            PLUGIN / "SegmentEditingRules.vb"
        ).read_text(encoding="utf-8")
        config = (ROOT / "VideoEnhancerPlugin" / "PluginConfig.vb").read_text(encoding="utf-8")
        queue = "\n".join((PLUGIN / name).read_text(encoding="utf-8") for name in (
            "QueueHook.vb", "QueueCommandBuilder.vb", "SegmentEditingRules.vb"))
        program = "\n".join((CLI / name).read_text(encoding="utf-8") for name in (
            "Program.cs", "CliArgumentParser.cs"))
        self.assertIn("_cmbRtxHdrMode.Enabled = hdrParametersEnabled", panel)
        self.assertIn("OnShellModelItemClick", panel)
        self.assertIn("分段总开关", segmented)
        self.assertIn("按秒（默认，断点自动吸附关键帧）", segmented)
        self.assertIn("SnapSegmentBoundary", segmented)
        self.assertIn('"ffmpeg"', segmented)
        self.assertIn('"anime4k"', segmented)
        self.assertIn("模型倍率优先", segmented)
        self.assertIn("BoundaryMode As String", config)
        self.assertIn("StartSeconds As Double", config)
        self.assertIn("TargetWidth As Integer", config)
        self.assertIn("AllowMixedModelBackends As Boolean = False", config)
        self.assertIn("If File.Exists(sourcePath) Then", config)
        self.assertIn("loadedLegacyConfig", config)
        self.assertIn("SpecialFolder.LocalApplicationData", config)
        self.assertIn("测试功能：跨模型后端混用", segmented)
        self.assertIn("SegmentBoundaryColumnWidth As Single = 128.0F", segmented)
        self.assertIn("SegmentTargetSizeColumnWidth As Single = 96.0F", segmented)
        self.assertGreaterEqual(segmented.count("CreateSegmentGridPanel()"), 3)
        self.assertIn("Dim videoRow As New ModernHorizontalPanel(-1.0F, CSng(UiColumnGap), 132.0F)", segmented)
        self.assertIn("_btnSegmentRefresh.SetBounds(0, refreshField.ScaleY(UiFieldEditorTop), refreshField.ClientSize.Width", segmented)
        self.assertIn("refreshField.ScaleY(UiControlHeight)", segmented)
        self.assertIn('CreateOfficialField("视频", _cmbSegmentVideo, 0)', segmented)
        self.assertIn("_switchSegmented.Anchor = AnchorStyles.None", segmented)
        self.assertIn("_switchMixedSegmentBackends.Anchor = AnchorStyles.None", segmented)
        self.assertNotIn("_switchSegmented.Margin = New Padding(0, 16, 0, 0)", segmented)
        self.assertNotIn("_switchMixedSegmentBackends.Margin = New Padding(0, 16, 0, 0)", segmented)
        self.assertIn("config.AllowMixedModelBackends", segmented)
        self.assertIn("allowMixedSegmentBackends", queue)
        self.assertIn("--allow-mixed-segment-backends", queue)
        self.assertIn("所有固定倍率模型必须使用相同放大倍率", queue)
        self.assertIn("segmentsBase64", queue)
        self.assertIn('case "--segments-base64"', program)
        self.assertIn("RunSegmentedVideo", program)
        self.assertIn("StartSeconds", program)
        self.assertIn('writer.WriteNumber("outputWidth"', program)
        self.assertIn('"mixed"', program)
        self.assertIn("outputWidth = checked(video.Width * fixedScale)", program)
        self.assertIn("prepared.Any(segment => segment.OutputWidth != firstWidth", program)
        self.assertIn("ApplySegmentResolutionRule(config)", segmented)
        self.assertIn('case "--allow-mixed-segment-backends"', program)
        self.assertIn("!options.AllowMixedSegmentBackends", program)
        self.assertIn('EnsureEmbeddedFile(EmbeddedSegmentedBackendResource, "rve-segmented-backend.py")', program)
        self.assertIn('ScriptSupportsArgument(script, "--tile-size")', program)
        self.assertIn("RunHybridSegmentedVideo(", program)
        self.assertIn("RunDirectCustomSegment(", program)
        self.assertIn("SEGMENTED_DIRECT_DONE", program)
        self.assertIn("SEGMENTED_DIRECT_GRAPH", program)
        self.assertIn("prepared.Any(segment => !IsSegmentModelBackend(segment.Backend))", program)
        self.assertIn("ProbeGeneratedVideoFast(", program)
        portable_paths = (ROOT / "cli" / "PortablePaths.cs").read_text(encoding="utf-8")
        self.assertIn("AppContext.BaseDirectory", portable_paths)
        self.assertIn("internal static string CoreRoot", portable_paths)
        self.assertNotIn('"videoenhancer.ini"', portable_paths)
        self.assertNotIn('"core-path"', portable_paths)
        self.assertNotIn("ResolveCoreRoot", portable_paths)

    def test_import_page_lists_models_and_exposes_capability_editor(self):
        source = read_plugin_panel_sources()
        self.assertIn("Private ReadOnly _importModelList As New UltraDetailListView()", source)
        self.assertIn("_importModelList.ItemDoubleClick", source)
        self.assertIn("用户模型（双击修正 / Delete 删除）", source)
        self.assertIn("_importModelList.KeyDown", source)
        self.assertIn("_importModelList.MouseDown", source)
        self.assertIn("删除用户模型", source)
        self.assertIn("ShowUserModelCapabilityEditor", source)
        self.assertIn('"--update-user-model"', source)
        self.assertIn('"--delete-user-model"', source)
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
        program = (ROOT / "cli" / "Program.cs").read_text(encoding="utf-8") + (
            CLI / "CliArgumentParser.cs"
        ).read_text(encoding="utf-8")
        self.assertIn("UpdateCapabilities", catalog)
        self.assertIn("AllowedBackends", catalog)
        self.assertIn('format == "onnx"', catalog)
        self.assertIn('format == "ncnn"', catalog)
        self.assertIn("!user.Backends.Contains(backend", program)
        self.assertIn('case "--list-user-models"', program)

    def test_download_catalog_keeps_latest_runtime_and_supports_safe_local_delete(self):
        program = "\n".join((CLI / name).read_text(encoding="utf-8") for name in (
            "Program.cs", "CliArgumentParser.cs", "ModelRepositoryClient.cs", "ModelDownloadManager.cs"))
        panel = read_plugin_panel_sources()
        self.assertIn("KeepLatestVersionedArchive", program)
        self.assertIn("RTXVideoRuntime_(?<version>", program)
        self.assertIn('case "--delete-download-model"', program)
        self.assertIn("DeleteDownloadedModel", program)
        self.assertIn("_downloadList.MouseDown", panel)
        self.assertIn('"卸载 RTX 运行组件", "删除本地模型"', panel)
        self.assertIn("CanDeleteDownloadedModel", panel)
        self.assertIn("RunDownloadedModelDelete", panel)
        self.assertIn("DeleteRtxVideoRuntime", program)
        self.assertIn("DeleteRtxVideoRuntimeArchives", program)
        self.assertIn("RTX_RUNTIME_DELETE_COMPLETE", program)
        self.assertIn("IsRtxVideoRuntimeDownload", panel)
        updater = (ROOT / "VideoEnhancerPlugin" / "PluginUpdater.vb").read_text(encoding="utf-8")
        self.assertNotIn("Optional installedExePath As String", updater)
        self.assertIn("Return remoteVersion > installedVersion", updater)
        self.assertIn("info.Length <> manifest.Package.Size", updater)
        self.assertIn("SHA256.HashData(stream)", updater)

    def test_each_plugin_page_has_own_runtime_source(self):
        project = (PLUGIN / "VideoEnhancerPlugin.vbproj").read_text(encoding="utf-8")
        runtime_pages = {
            "PluginPanel.UpscalePage.vb": "BuildOfficialUpscalePage",
            "PluginPanel.PreviewPage.vb": "BuildOfficialPreviewPage",
            "PluginPanel.ImagePage.vb": "BuildOfficialImagePage",
            "PluginPanel.ModelDownloadPage.vb": "BuildOfficialModelDownloadPage",
            "PluginPanel.ModelConverterPage.vb": "BuildOfficialConverterPage",
            "PluginPanel.ModelImportPage.vb": "BuildOfficialImporterPage",
            "PluginPanel.SegmentedUpscalePage.vb": "BuildOfficialSegmentedPage",
            "PluginPanel.ShellPage.vb": "BuildOfficialShellPage",
            "PluginPanel.TutorialPage.vb": "BuildMarkdownPage",
        }
        panel = (PLUGIN / "PluginPanel.vb").read_text(encoding="utf-8-sig")
        for file_name, build_method in runtime_pages.items():
            page_path = PLUGIN / "Pages" / file_name
            self.assertTrue(page_path.is_file(), file_name)
            self.assertIn(build_method, page_path.read_text(encoding="utf-8-sig"))
            self.assertNotIn(f"Private Sub {build_method}", panel)
        self.assertFalse((PLUGIN / "DesignerPages").exists())
        self.assertNotIn("Visual Studio designer pages", project)
        self.assertGreaterEqual(project.count("<Private>false</Private>"), 2)
        self.assertGreaterEqual(project.count("<ExternallyResolved>true</ExternallyResolved>"), 2)
        self.assertNotIn("layoutOnly", panel)
        self.assertNotIn("AttachDesignerPage", panel)
        self.assertNotIn("RebindDesignerBackgroundSources", panel)

    def test_builtin_catalog_remains_valid_json(self):
        document = json.loads((ROOT / "cli" / "model-capabilities.json").read_text(encoding="utf-8"))
        self.assertEqual(document["schemaVersion"], 1)
        self.assertGreater(len(document["models"]), 0)


if __name__ == "__main__":
    unittest.main()
