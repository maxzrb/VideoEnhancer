using System.Collections;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

partial class Program
{
    static void RunAppearanceChecks(Assembly plugin)
    {
        // 检查真实控件的 GPU 外观属性；不显示窗口、不联网加载教程、不启动业务任务。
        var config = Activator.CreateInstance(plugin.GetType("videoenhancer.PluginConfig")!)!;
        using var panel = (UserControl)Activator.CreateInstance(plugin.GetType("videoenhancer.PluginPanel")!, config, true)!;
        var source = (Control)Field(panel, "ModernPanel1");
        var workbench = (Control)Field(panel, "_pageUpscale");
        var tutorial = (Control)Field(panel, "_pageTutorial");
        var surface = Color.FromArgb(40, 220, 220, 220);
        var darkSurface = Color.FromArgb(40, 0, 0, 0);
        var separator = Color.FromArgb(80, 220, 220, 220);
        var thumb = Color.FromArgb(80, 220, 220, 220);
        var hoverThumb = Color.FromArgb(120, 220, 220, 220);
        var markdown = "# 透明背景教程\n\n普通正文与 **粗体**、`inline code`。\n\n" +
            "[链接](https://example.invalid/tutorial)\n\n```vb\nDim scale = 2\n```\n\n" +
            "| 模型 | 倍率 |\n| --- | --- |\n| 示例 | 2x |\n\n> 引用\n\n---\n";
        var lists = new[] { ("_downloadList", 4), ("_importModelList", 6), ("_shellModelList", 3) };

        foreach (var dpi in new[] { 96, 120, 144, 192 })
        {
            var scale = dpi / 96f;
            var ratio = scale / LayoutScale(workbench).Width;
            panel.Scale(new SizeF(ratio, ratio));
            panel.Size = new Size(Pixels(1280, scale), Pixels(820, scale));
            ArrangeTree(panel);
            foreach (var (name, columnCount) in lists)
            {
                var list = (Control)Field(panel, name);
                Check(list.BackColor.A == 0 && (Color)Get(list, "BackgroundColor") == surface &&
                    ReferenceEquals(Get(list, "BackgroundSource"), source), $"{dpi} DPI {name} 使用原生半透明列表底色与宿主背景源");
                Check(((Color)Get(list, "HeaderBackColor")).A == 0 && (Color)Get(list, "HeaderBorderColor") == surface &&
                    (int)Get(list, "HeaderBorderWidth") == 2, $"{dpi} DPI {name} 表头和分隔线适配透明背景");
                Check((int)Get(list, "BorderSize") == 0 && (int)Get(list, "BorderRadius") == 10 &&
                    (int)Get(list, "ItemCornerRadius") == 10, $"{dpi} DPI {name} 采用准备文件页的圆角而无不透明外框");
                Check((Color)Get(list, "GroupBackColor") == darkSurface && (Color)Get(list, "GroupBorderColor") == surface &&
                    ((Color)Get(list, "ItemHoverBackColor")).A < 255 && (Color)Get(list, "ItemSelectedBackColor") == surface,
                    $"{dpi} DPI {name} 分组、悬停与选中状态保留背景穿透");
                Check((Color)Get(list, "ScrollBarTrackColor") == surface && (Color)Get(list, "ScrollBarThumbColor") == thumb &&
                    (Color)Get(list, "ScrollBarThumbHoverColor") == hoverThumb, $"{dpi} DPI {name} 滚动条使用半透明颜色");
                Check(((IList)Get(list, "Columns")).Count == columnCount && (int)Get(list, "HeaderHeight") == 30 &&
                    !(bool)Get(list, "MultiSelect") && !(bool)Get(list, "AllowDragReorder"),
                    $"{dpi} DPI {name} 保持列定义、紧凑表头高度和原有选择方式");
            }
            Check((Color)Get(workbench, "ScrollBarTrackColor") == surface &&
                (Color)Get(workbench, "ScrollBarThumbColor") == thumb, $"{dpi} DPI 工作台滚动条不覆盖不透明轨道");

            using var viewer = (Control)Call(panel, "CreateMarkdownViewer", markdown);
            tutorial.Controls.Add(viewer);
            Check(viewer.GetType().BaseType!.FullName == "LakeUI.MarkdownViewerCore" &&
                viewer.BackColor.A == 0 && (Color)Get(viewer, "BackColor1") == darkSurface &&
                ReferenceEquals(Get(viewer, "BackgroundSource"), source),
                $"{dpi} DPI 教程使用 Agent 相同渲染内核与半透明 GPU 底色");
            Check((Color)Get(viewer, "InlineCodeBackColor") == Color.FromArgb(120, 0, 0, 0) &&
                (Color)Get(viewer, "CodeBlockBackColor") == Color.FromArgb(120, 0, 0, 0),
                $"{dpi} DPI 教程行内代码与代码块采用 Agent 的半透明默认样式");
            Check((Color)Get(viewer, "TableHeaderBackColor") == surface && (Color)Get(viewer, "TableBorderColor") == separator &&
                (Color)Get(viewer, "HeadingSeparatorColor") == separator && (Color)Get(viewer, "HorizontalRuleColor") == separator,
                $"{dpi} DPI 教程表格、标题分隔线和水平线适配透明背景");
            Check((Color)Get(viewer, "ScrollBarTrackColor") == surface && (Color)Get(viewer, "ScrollBarColor") == thumb &&
                (Color)Get(viewer, "ScrollBarHoverColor") == hoverThumb && (Color)Get(viewer, "SelectionColor") == surface,
                $"{dpi} DPI 教程滚动条和文本选区保持半透明");
            Check(viewer.Text == markdown, $"{dpi} DPI 教程源文保留正文、链接、代码与表格");
            var reloaded = markdown + "\n**重新加载教程**";
            Call(viewer, "SetMarkdownImmediate", reloaded, false, false, false);
            Check(viewer.Text == reloaded && (Color)Get(viewer, "BackColor1") == darkSurface &&
                (Color)Get(viewer, "TableHeaderBackColor") == surface, $"{dpi} DPI 重载 Markdown 不恢复不透明默认样式");
            Check((int)Get(Field(panel, "_markdownReady"), "Count") == 0,
                $"{dpi} DPI 外观检查不触发在线教程加载或修改惰性页签状态");
        }
        Console.WriteLine("TRANSPARENT_APPEARANCE_TESTS_PASS|96|120|144|192|three-lists|tutorial-markdown|no-network");
    }
}
