using System.Collections;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

partial class Program
{
    static IEnumerable<Control> Descendants(Control parent)
    {
        yield return parent;
        foreach (Control child in parent.Controls)
            foreach (var descendant in Descendants(child)) yield return descendant;
    }

    static void ArrangeTree(Control parent)
    {
        parent.PerformLayout();
        foreach (Control child in parent.Controls) ArrangeTree(child);
    }

    static SizeF LayoutScale(object control) => (SizeF)Get(control, "LayoutScale");
    static int Pixels(int value, float scale) => (int)Math.Round(value * (double)scale);

    static void RunDpiLayoutChecks(Assembly plugin)
    {
        // 在实际 WinForms 控件树上执行框架缩放；不改系统 DPI，也不启动宿主或后台任务。
        var config = Activator.CreateInstance(plugin.GetType("videoenhancer.PluginConfig")!)!;
        var videoConfig = Activator.CreateInstance(plugin.GetType("videoenhancer.SegmentedVideoConfig")!)!;
        Set(videoConfig, "Path", "dpi-layout-test.mp4");
        Set(videoConfig, "BoundaryMode", "frames");
        Set(videoConfig, "FrameCount", 200L);
        var segment = Activator.CreateInstance(plugin.GetType("videoenhancer.SegmentedUpscaleRange")!)!;
        Set(segment, "Start", 1L);
        Set(segment, "End", 100L);
        ((IList)Get(videoConfig, "Segments")).Add(segment);
        var secondSegment = Activator.CreateInstance(plugin.GetType("videoenhancer.SegmentedUpscaleRange")!)!;
        Set(secondSegment, "Start", 101L);
        Set(secondSegment, "End", 200L);
        ((IList)Get(videoConfig, "Segments")).Add(secondSegment);
        ((IList)Get(config, "SegmentedVideos")).Add(videoConfig);
        using var panel = (UserControl)Activator.CreateInstance(plugin.GetType("videoenhancer.PluginPanel")!, config, true)!;
        panel.GetType().GetField("_segmentSync", Flags)!.SetValue(panel, true);
        ((IList)Field(panel, "_segmentVideoPaths")).Add("dpi-layout-test.mp4");
        var videoCombo = Field(panel, "_cmbSegmentVideo");
        ((IList)Get(videoCombo, "Items")).Add("DPI 测试视频");
        Set(videoCombo, "SelectedIndex", 0);
        panel.GetType().GetField("_segmentSync", Flags)!.SetValue(panel, false);
        Check(panel.AutoScaleMode == AutoScaleMode.Dpi, "插件启用 DPI 自动缩放");
        using var host = new Form { AutoScaleMode = AutoScaleMode.None, ClientSize = new Size(1280, 1000) };
        host.Controls.Add(panel);
        ArrangeTree(host);
        var root = (Control)Field(panel, "_upscaleRoot");
        var originalScale = LayoutScale(root).Width;
        Check(Math.Abs(originalScale - panel.DeviceDpi / 96f) < 0.01f,
            $"启动时从 96 DPI 基准缩放一次: root={originalScale}, device={panel.DeviceDpi}, baseline={panel.AutoScaleDimensions}");
        foreach (var dpi in new[] { 96, 120, 144, 192, 144, 120, 96 })
        {
            var targetScale = dpi / 96f;
            var ratio = targetScale / LayoutScale(root).Width;
            panel.Scale(new SizeF(ratio, ratio));
            host.ClientSize = new Size(Pixels(1280, targetScale), Pixels(1000, targetScale));
            ArrangeTree(host);
            Call(panel, "SyncUpscaleRootBounds");
            Call(panel, "SyncImageRootBounds");
            Call(panel, "SyncSegmentedRootBounds");
            ArrangeTree(panel);
            Check(Math.Abs(LayoutScale(root).Width - targetScale) < 0.01f && root.Height == Pixels(744, targetScale),
                $"{dpi} DPI 工作台内容高度一致");
            var editor = (Control)Field(panel, "_cmbBackend");
            Check(editor.Top == Pixels(23, targetScale) && editor.Height == Pixels(28, targetScale),
                $"{dpi} DPI 字段标题和编辑器对齐");
            var imageRoot = (Control)Field(panel, "_imageRoot");
            var segmentRoot = (Control)Field(panel, "_segmentRoot");
            Check(imageRoot.Height == Pixels(272, targetScale) && segmentRoot.Height == Pixels(656, targetScale),
                $"{dpi} DPI 图片和分段页内容高度一致");
            var tabs = Field(panel, "_tabs");
            var tabCount = ((IEnumerable)Get(tabs, "Items")).Cast<object>().Count();
            for (var index = 0; index < tabCount; index++)
            {
                Set(tabs, "SelectedIndex", index);
                ArrangeTree(panel);
            }
            Set(tabs, "SelectedIndex", 0);
            ArrangeTree(panel);
            Check(new[] { root, imageRoot, segmentRoot }
                    .All(pageRoot => Math.Abs(LayoutScale(pageRoot).Width - targetScale) < 0.01f),
                $"{dpi} DPI 切换全部页签不会重复缩放");
            var convertButton = (Control)Field(panel, "_btnPickPth");
            Check(convertButton.Width == Pixels(140, targetScale) && convertButton.Height == Pixels(28, targetScale),
                $"{dpi} DPI 网格固定行列按相同基准缩放: {convertButton.Bounds}, margin={convertButton.Margin}, parent={convertButton.Parent!.Bounds}, rowScale={LayoutScale(convertButton.Parent!)}, rootScale={LayoutScale(convertButton.Parent!.Parent!)}");
            var layouts = Descendants(panel).Where(c => c.GetType().FullName == "videoenhancer.ModernHorizontalPanel").ToArray();
            Check(layouts.Length > 0 && layouts.All(layout => Math.Abs(LayoutScale(layout).Width - targetScale) < 0.01f),
                $"{dpi} DPI 横向布局缩放一致");
            var field = editor.Parent!;
            Check(editor.Bottom <= field.ClientSize.Height && editor.Top > 0,
                $"{dpi} DPI 编辑器完整处于字段内部");
            var page = (Control)Field(panel, "_pageUpscale");
            Check(root.Width <= page.ClientSize.Width, $"{dpi} DPI 内容不会越过页面右边缘");
            var compactNames = new[] { "_cmbBackend", "_cmbModel", "_cmbOutputScale", "_cmbInterpBackend",
                "_cmbInterp", "_cmbFactor", "_cmbProcessOrder", "_numRtxHdrContrast", "_numRtxHdrMaxLuminance",
                "_cmbSegmentVideo", "_cmbSegmentMode", "_btnSegmentRefresh", "_btnSegmentAdd",
                "_btnCheckUpdates", "_btnImageFiles", "_btnImageOutput", "_btnImageStart", "_cmbImageOutputScale",
                "_btnRefreshDownloads", "_btnDownloadPluginUpdate", "_btnPickImportFile", "_btnPickImportFolder",
                "_btnImportModel", "_btnPickPth", "_btnConvert", "_btnShellAdd", "_btnShellApply", "_cmbTask", "_cmbRate" };
            var heightMismatches = compactNames.Where(name => ((Control)Field(panel, name)).Height != Pixels(28, targetScale)).ToArray();
            Check(heightMismatches.Length == 0,
                $"{dpi} DPI 页面常规控件统一紧凑高度: " + string.Join(", ", heightMismatches.Select(name => $"{name}={((Control)Field(panel, name)).Height}")));
            Check(compactNames.All(name => {
                    var control = (Control)Field(panel, name);
                    return control.Height >= Math.Ceiling(control.Font.GetHeight(dpi)) + Pixels(4, targetScale);
                }), $"{dpi} DPI 紧凑控件仍为原字号保留字高和留白");
            var exeValue = ((Control)Field(panel, "_lblExe")).Parent!;
            Check(exeValue.Height == Pixels(28, targetScale), $"{dpi} DPI 固定程序路径栏不会被过大行高拉伸");
            var videoEditor = (Control)Field(panel, "_cmbSegmentVideo");
            var refresh = (Control)Field(panel, "_btnSegmentRefresh");
            Check(videoEditor.Parent!.Top + videoEditor.Top == refresh.Parent!.Top + refresh.Top &&
                  refresh.Parent.Left + refresh.Left - videoEditor.Parent.Left - videoEditor.Right == Pixels(8, targetScale),
                $"{dpi} DPI 视频框与刷新按钮对齐且只有单一列间距");
            var primarySwitchRow = ((Control)Field(panel, "_switchSegmented")).Parent!;
            var mixedSwitchRow = ((Control)Field(panel, "_switchMixedSegmentBackends")).Parent!;
            Check(primarySwitchRow.Height == Pixels(32, targetScale) &&
                  mixedSwitchRow.Top - primarySwitchRow.Bottom == Pixels(4, targetScale),
                $"{dpi} DPI 分段开关行紧凑且无大块空白");
            Call(panel, "RenderSegmentRows");
            var rows = (Control)Field(panel, "_segmentRowsPanel");
            ArrangeTree(rows);
            var row = rows.Controls[0];
            Check(Math.Abs(LayoutScale(row).Width - targetScale) < 0.01f &&
                  row.Top == Pixels(8, targetScale) && row.Height == Pixels(36, targetScale),
                $"{dpi} DPI 动态分段行使用当前缩放比例");
            Check(rows.Controls[1].Top == Pixels(48, targetScale) && row.Controls.Cast<Control>()
                    .All(control => control.Height == Pixels(28, targetScale)),
                $"{dpi} DPI 分段数据行高度和行距一致");

            foreach (var clientSize in new[] { new Size(800, 520), new Size(960, 640), new Size(1280, 1000) })
            {
                host.ClientSize = new Size(Pixels(clientSize.Width, targetScale), Pixels(clientSize.Height, targetScale));
                for (var index = 0; index < tabCount; index++)
                {
                    Set(tabs, "SelectedIndex", index);
                    ArrangeTree(panel);
                }
                var clipped = compactNames.Where(name => {
                    var control = (Control)Field(panel, name);
                    return !control.Parent!.ClientRectangle.Contains(control.Bounds);
                }).ToArray();
                Check(clipped.Length == 0,
                    $"{dpi} DPI {clientSize.Width}×{clientSize.Height} 窗口常规控件不越过容器: " +
                    string.Join(", ", clipped.Select(name => $"{name}={((Control)Field(panel, name)).Bounds}/{((Control)Field(panel, name)).Parent!.ClientRectangle}")));
            }
            Set(tabs, "SelectedIndex", 0);
            ArrangeTree(panel);
        }

        foreach (var (name, fixedWidths) in new[] {
                     ("_shellModelList", new[] { 130, 100 }),
                     ("_importModelList", new[] { 150, 110, 80, 210, 100 }) })
        {
            var list = (Control)Field(panel, name);
            _ = list.Handle;
            var columns = ((IEnumerable)Get(list, "Columns")).Cast<object>().ToArray();
            var scale = list.DeviceDpi / 96f;
            Check(columns.Skip(1).Select(column => (int)Get(column, "Width"))
                    .SequenceEqual(fixedWidths.Select(width => Pixels(width, scale))),
                $"本机 {list.DeviceDpi} DPI 模型列表固定列只缩放一次: {name}");
            var widths = columns.Skip(1).Select(column => (int)Get(column, "Width")).ToArray();
            list.Width += 120;
            list.Width -= 120;
            Check(columns.Skip(1).Select(column => (int)Get(column, "Width")).SequenceEqual(widths),
                $"模型列表窗口拉伸不会重复缩放固定列: {name}");
        }

        using var quad = (Form)Activator.CreateInstance(plugin.GetType("videoenhancer.QuadGridForm")!, config)!;
        Check(quad.AutoScaleMode == AutoScaleMode.Dpi, "四宫格窗口启用 DPI 自动缩放");
        _ = quad.Handle;
        var nativeScale = quad.DeviceDpi / 96f;
        var nativeSlot = (Control)((Array)Field(quad, "_slotLabels")).GetValue(0)!;
        Check(Math.Abs(LayoutScale(nativeSlot).Width - nativeScale) < 0.01f &&
              ((Control)Field(quad, "_btnClose")).Width == Pixels(45, nativeScale),
            $"本机 {quad.DeviceDpi} DPI 四宫格窗口和标题栏按相同基准初始化");
        Check(!plugin.GetManifestResourceNames().Contains("videoenhancer-layout.json"), "插件不再依赖布局 JSON 资源");
        var bounds = (IDictionary)Field(quad, "_layoutBounds");
        var startingDpi = quad.DeviceDpi;
        foreach (var dpi in new[] { 96, 120, 144, 192, 120, 96 })
        {
            // 客户区使用对应 DPI 的尺寸，验证窗口拉伸后的坐标重算和反复缩放。
            var targetScale = dpi / 96f;
            var slot = (Control)((Array)Field(quad, "_slotLabels")).GetValue(0)!;
            var ratio = targetScale / LayoutScale(slot).Width;
            quad.Scale(new SizeF(ratio, ratio));
            quad.ClientSize = new Size(Pixels(1200, targetScale), Pixels(720, targetScale));
            ArrangeTree(quad);
            var consistentBounds = true;
            foreach (DictionaryEntry entry in bounds)
            {
                var control = (Control)entry.Key;
                var logical = (Rectangle)entry.Value!;
                var expected = new Rectangle(Pixels(logical.X, targetScale), Pixels(logical.Y, targetScale),
                    Pixels(logical.Width, targetScale), Pixels(logical.Height, targetScale));
                consistentBounds &= control.Bounds == expected;
            }
            Check(consistentBounds, $"{dpi} DPI 四宫格所有控件边界一致");
            foreach (var pair in new[] { ("_encoderHost", "_cmbEncoder"), ("_scaleHost", "_cmbScale"),
                         ("_sizeHost", "_cmbSize"), ("_layoutHost", "_cmbLayout"),
                         ("_qualityHost", "_numQuality"), ("_lineHost", "_numLine") })
            {
                var container = (Control)Field(quad, pair.Item1);
                var child = (Control)Field(quad, pair.Item2);
                Check(container.ClientRectangle.Contains(child.Bounds) && child.Height >= Pixels(28, targetScale),
                    $"{dpi} DPI 四宫格编辑器高度完整 {pair.Item2}: {child.Bounds} / {container.ClientRectangle}");
            }
            var badge = (Control)Field(slot, "_badge");
            Check(badge.Height >= Pixels(24, targetScale), $"{dpi} DPI 视频编号不会缩回固定像素尺寸");
            quad.ClientSize = new Size(Pixels(1300, targetScale), Pixels(780, targetScale));
            ArrangeTree(quad);
            quad.ClientSize = new Size(Pixels(1200, targetScale), Pixels(720, targetScale));
            ArrangeTree(quad);
            Check(slot.Bounds == new Rectangle(Pixels(20, targetScale), Pixels(58, targetScale),
                Pixels(205, targetScale), Pixels(95, targetScale)), $"{dpi} DPI 窗口拉伸往返无累积偏移");
        }
        quad.Close();
        Console.WriteLine($"DPI_LAYOUT_TESTS_PASS|native-startup-{startingDpi}|96|120|144|192|round-trip");
    }
}
