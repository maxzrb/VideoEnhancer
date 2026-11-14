using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

partial class Program
{
    static void RunScrollChecks(Assembly plugin)
    {
        // 直接驱动真实控件的滚轮和动画步进，不显示窗口、不改变桌面 DPI、不启动业务任务。
        foreach (var dpi in new[] { 96, 120, 144, 192 })
        {
            var config = Activator.CreateInstance(plugin.GetType("videoenhancer.PluginConfig")!)!;
            using var panel = (UserControl)Activator.CreateInstance(plugin.GetType("videoenhancer.PluginPanel")!, config, true)!;
            using var page = (Control)Field(panel, "_pageUpscale");
            var root = (Control)Field(panel, "_upscaleRoot");
            var targetScale = dpi / 96f;
            var ratio = targetScale / LayoutScale(page).Width;
            panel.Scale(new SizeF(ratio, ratio));
            page.Parent = null;
            page.Dock = DockStyle.None;
            page.Size = new Size(Pixels(1280, targetScale), Pixels(420, targetScale));
            page.Visible = true;
            ArrangeTree(page);
            var maximum = (int)Call(page, "MaximumVerticalOffset");
            var step = Pixels(48, targetScale);
            Check(maximum > step * 3, $"{dpi} DPI 工作台存在可滚动内容");
            Check(root.GetType().FullName == "videoenhancer.GpuScrollContentPanel", $"{dpi} DPI 内容根使用防旧像素复制窗口");

            var wheel = new HandledMouseEventArgs(MouseButtons.None, 0, 0, 0, -120);
            Call(page, "OnMouseWheel", wheel);
            Check(wheel.Handled && (int)Get(page, "VerticalScrollOffset") == 0 && (bool)Field(page, "_scrollAnimating"),
                $"{dpi} DPI 滚轮已消费但不立即跳动");
            Call(page, "AdvanceScroll", 16.0);
            var partial = (int)Get(page, "VerticalScrollOffset");
            Check(partial > 0 && partial < step && root.Top == -partial, $"{dpi} DPI 中间帧向目标渐进且内容坐标一致");
            Call(page, "AdvanceScroll", 120.0);
            Check((int)Get(page, "VerticalScrollOffset") == step && !(bool)Field(page, "_scrollAnimating"),
                $"{dpi} DPI 动画结束精确落在缩放后的步长并停止计时器");

            Call(page, "BeginWheelScroll", -120);
            Call(page, "BeginWheelScroll", -120);
            Check((int)Field(page, "_scrollTarget") == step * 3, $"{dpi} DPI 连续输入合并到同一动画目标");
            Call(page, "AdvanceScroll", 20.0);
            var beforeReverse = (int)Get(page, "VerticalScrollOffset");
            Call(page, "BeginWheelScroll", 120);
            var reverseTarget = Math.Max(0, beforeReverse - step);
            Check((int)Field(page, "_scrollTarget") == reverseTarget, $"{dpi} DPI 反向输入立即转向当前画面");
            Call(page, "AdvanceScroll", 120.0);
            Check((int)Get(page, "VerticalScrollOffset") == reverseTarget, $"{dpi} DPI 反向动画无迟到的旧方向移动");

            Call(page, "ScrollTo", 0, 0);
            for (var count = 0; count < 3; count++)
            {
                Call(page, "BeginWheelScroll", -1);
                Call(page, "AdvanceScroll", 120.0);
            }
            Check((int)Get(page, "VerticalScrollOffset") == (int)Math.Truncate(3.0 * step / 120.0),
                $"{dpi} DPI 触摸板小 delta 的不足一像素部分累计保留");

            Call(page, "ScrollTo", 0, 0);
            var combo = (Control)Field(panel, "_cmbBackend");
            var originalIndex = Get(combo, "SelectedIndex");
            var originalBackend = Get(config, "Backend");
            var message = Message.Create(IntPtr.Zero, 0x20A, new IntPtr(unchecked((int)((ushort)-120 << 16))), IntPtr.Zero);
            combo.GetType().GetMethod("WndProc", Flags)!.Invoke(combo, new object[] { message });
            Check((int)Field(page, "_scrollTarget") == step && (bool)Field(page, "_scrollAnimating"),
                $"{dpi} DPI 下拉框滚轮转发至工作台视口");
            Check(Equals(originalIndex, Get(combo, "SelectedIndex")) && Equals(originalBackend, Get(config, "Backend")),
                $"{dpi} DPI 滚轮经过下拉框不更改选项或配置");
            Call(page, "AdvanceScroll", 120.0);
            var numeric = (Control)Field(panel, "_numRtxHdrContrast");
            var originalValue = Get(numeric, "Value");
            Call(numeric, "OnMouseWheel", new MouseEventArgs(MouseButtons.None, 0, 0, 0, -120));
            Call(page, "AdvanceScroll", 120.0);
            Check((int)Get(page, "VerticalScrollOffset") == step * 2 && Equals(originalValue, Get(numeric, "Value")),
                $"{dpi} DPI 数字框滚轮继续滚动页面但不修改 HDR 参数");

            Call(page, "BeginWheelScroll", -120);
            Call(page, "OnMouseDown", new MouseEventArgs(MouseButtons.Left, 1, 0, 0, 0));
            var stoppedOffset = (int)Get(page, "VerticalScrollOffset");
            Call(page, "AdvanceScroll", 120.0);
            Check(!(bool)Field(page, "_scrollAnimating") && (int)Get(page, "VerticalScrollOffset") == stoppedOffset,
                $"{dpi} DPI 操作滚动条前取消旧动画");

            Call(page, "BeginWheelScroll", -120);
            page.Height -= Pixels(20, targetScale);
            Check(!(bool)Field(page, "_scrollAnimating"), $"{dpi} DPI 改变视口尺寸停止旧动画");
            Call(page, "BeginWheelScroll", -120);
            page.Visible = false;
            Check(!(bool)Field(page, "_scrollAnimating"), $"{dpi} DPI 离开页签停止计时器");
            page.Visible = true;
            ArrangeTree(page);

            Call(page, "ScrollTo", 0, int.MaxValue);
            maximum = (int)Call(page, "MaximumVerticalOffset");
            Check((int)Get(page, "VerticalScrollOffset") == maximum, $"{dpi} DPI 滚动位置钳制在内容底部");
            Call(page, "BeginWheelScroll", -120);
            Check(!(bool)Field(page, "_scrollAnimating"), $"{dpi} DPI 底部不会积累无效动画");
            for (var count = 0; count < 30; count++)
            {
                Call(page, "ScrollTo", 0, maximum);
                Call(panel, "SyncUpscaleRootBounds");
                Call(page, "ScrollTo", 0, 0);
                Call(panel, "SyncUpscaleRootBounds");
            }
            Check(root.Top == 0 && root.Height == Pixels(744, targetScale), $"{dpi} DPI 滚动往返没有布局漂移或顶部空白");
            var timer = (System.Windows.Forms.Timer)Field(page, "_scrollTimer");
            page.Dispose();
            Check(!timer.Enabled, $"{dpi} DPI 释放视口停止并释放滚动计时器");
        }

        var viewportType = plugin.GetType("videoenhancer.SmoothScrollPanel")!;
        foreach (var delta in new[] { -120, -30, -1, 1, 30, 120 })
        {
            var wParam = new IntPtr(unchecked((int)((ushort)delta << 16)));
            Check((int)viewportType.GetMethod("WheelDelta", Flags)!.Invoke(null, new object[] { wParam })! == delta,
                $"WM_MOUSEWHEEL 正确解码有符号 delta {delta}");
        }
        var contentType = plugin.GetType("videoenhancer.GpuScrollContentPanel")!;
        var flagsMethod = contentType.GetMethod("ScrollMoveFlags", Flags)!;
        Check((uint)flagsMethod.Invoke(null, new object[] { 0x15u })! == 0x115u,
            "移动滚动内容设置 SWP_NOCOPYBITS 且保留其他窗口标志");
        Check((uint)flagsMethod.Invoke(null, new object[] { 0x17u })! == 0x17u,
            "没有移动时不改变窗口重绘策略");
        Console.WriteLine("SCROLL_LAYOUT_TESTS_PASS|96|120|144|192|wheel-routing|animation|round-trip|no-copy-bits");
    }
}
