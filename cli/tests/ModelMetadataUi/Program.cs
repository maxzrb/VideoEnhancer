using System.Collections;
using System.Reflection;
using VideoEnhancer.Testing;
using System.Windows.Forms;

partial class Program
{
    static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static object Field(object instance, string name) => instance.GetType().GetField(name, Flags)!.GetValue(instance)!;
    static void Set(object instance, string name, object value) => instance.GetType().GetProperty(name, Flags)!.SetValue(instance, value);
    static object Get(object instance, string name) => instance.GetType().GetProperty(name, Flags)!.GetValue(instance)!;
    static object Call(object instance, string name, params object[] values) => instance.GetType().GetMethod(name, Flags)!.Invoke(instance, values)!;
    static void Check(bool value, string name)
    {
        if (!value) throw new Exception(name);
        Console.WriteLine("PASS " + name);
    }

    [STAThread]
    static void Main(string[] args)
    {
        var mode = args.FirstOrDefault(arg => arg.StartsWith("--", StringComparison.Ordinal));
        if (mode == "--dpi" || mode == "--scroll" || mode == "--appearance")
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        if (mode == "--tooltips" && Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
        {
            // STA 初始化占用窗口资源，先在新线程关联测试桌面再创建界面。
            Exception failure = null;
            var thread = new Thread(() => { try { Main(args); } catch (Exception ex) { failure = ex; } });
            thread.Start();
            thread.Join();
            if (failure != null) throw failure;
            return;
        }
        if (mode == "--tooltips") UseTestDesktop();
        var root = RepositoryPaths.ResolveRoot(args.FirstOrDefault(arg => !arg.StartsWith("--", StringComparison.Ordinal)));
        var directory = Path.Combine(root, "Artifacts/model-audit/ui/Plugin");
        Directory.CreateDirectory(directory);
        var pluginPath = Path.Combine(directory, "videoenhancer.3fui.dll");
        File.Copy(Path.Combine(root, "VideoEnhancerPlugin/obj/plugin-artifact/videoenhancer.3fui.dll"), pluginPath, true);
        var assembly = Assembly.LoadFrom(pluginPath);
        if (mode == "--host-runtime")
        {
            RunHostRuntimeChecks(assembly);
            return;
        }
        if (mode == "--appearance")
        {
            RunAppearanceChecks(assembly);
            return;
        }
        if (mode == "--scroll")
        {
            RunScrollChecks(assembly);
            return;
        }
        if (mode == "--dpi")
        {
            RunDpiLayoutChecks(assembly);
            return;
        }
        if (mode == "--tooltips")
        {
            RunTooltipChecks(assembly);
            return;
        }
        var config = Activator.CreateInstance(assembly.GetType("videoenhancer.PluginConfig")!)!;
        Set(config, "Enabled", true);
        Set(config, "Backend", "cuda");
        Set(config, "Model", "PTH/realesr-animevideov3");
        var legacyConfig = System.Text.Json.JsonSerializer.Deserialize("{\"OutputScale\":16}", config.GetType())!;
        Set(config, "OutputScale", (int)Get(legacyConfig, "OutputScale"));
        Check((int)Get(config, "OutputScale") == 8, "旧配置16x归一为8x");
        Set(config, "OutputScale", 0);
        using var panel = (Control)Activator.CreateInstance(assembly.GetType("videoenhancer.PluginPanel")!, config, true)!;
        panel.Size = new System.Drawing.Size(1200, 1000);
        var handle = panel.Handle;
        var modelType = assembly.GetType("videoenhancer.ModelCatalogItem")!;
        var models = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(modelType))!;
        var model = Activator.CreateInstance(modelType)!;
        Set(model, "Id", "PTH/realesr-animevideov3");
        Set(model, "DisplayName", "realesr-animevideov3");
        Set(model, "Architecture", "RealESRGAN Compact");
        Set(model, "ArchitectureGroup", "Compact");
        Set(model, "Scale", 4);
        models.Add(model);
        var model2x = Activator.CreateInstance(modelType)!;
        Set(model2x, "Id", "PTH/test-native2x");
        Set(model2x, "DisplayName", "test-native2x");
        Set(model2x, "Scale", 2);
        models.Add(model2x);
        Call(panel, "ApplyModelCatalog", models, false);
        var videoCombo = Field(panel, "_cmbOutputScale");
        var imageCombo = Field(panel, "_cmbImageOutputScale");
        Check((int)Get(videoCombo, "SelectedIndex") == 0, "默认原生倍率");
        Check(((IList)Get(videoCombo, "Items")).Count == 9 && ((IList)Get(imageCombo, "Items")).Count == 9,
              "两页仅提供原生和1至8x");
        Set(videoCombo, "SelectedIndex", 8);
        Check((int)Get(config, "OutputScale") == 8 && (int)Get(imageCombo, "SelectedIndex") == 8,
              "8x边界同步配置与图片页");
        Set(videoCombo, "SelectedIndex", 3);
        Check((int)Get(config, "OutputScale") == 3 && (int)Get(imageCombo, "SelectedIndex") == 3, "视频与图片倍率同步保存");
        Check(((string)Get(Field(panel, "_outputScaleHint"), "Text")).Contains("原生推理后缩放至 3x"), "缩放提示准确");
        Set(videoCombo, "SelectedIndex", 2);
        Call(panel, "SetCatalogSelection", model2x, false, true);
        Check((string)Get(Field(panel, "_outputScaleHint"), "Text") == "原生 2x", "切换2x模型立即清除额外缩放提示");
        Check((int)Get(config, "OutputScale") == 0 && (int)Get(videoCombo, "SelectedIndex") == 0 &&
              (int)Get(imageCombo, "SelectedIndex") == 0, "模型切换恢复原生倍率并同步两页");
        Set(videoCombo, "SelectedIndex", 3);
        Call(panel, "ApplyModelCatalog", models, false);
        Check((int)Get(config, "OutputScale") == 3, "刷新模型清单保留用户输出倍率");
        Call(panel, "SetCatalogSelection", model, false, true);
        Check((int)Get(config, "OutputScale") == 0 &&
              (string)Get(Field(panel, "_outputScaleHint"), "Text") == "原生 4x", "切回4x模型恢复原生输出");
        Set(videoCombo, "SelectedIndex", 2);
        var expectedHint = "原生 4x；原生推理后缩放至 2x";
        Check((string)Get(Field(panel, "_outputScaleHint"), "Text") == expectedHint &&
              (string)Get(Field(panel, "_imageOutputScaleHint"), "Text") == expectedHint,
              "重设输出倍率立即同步工作台和图片提示");
        Call(panel, "SetCatalogSelection", model, true, true);
        Check((int)Get(config, "OutputScale") == 2, "选择补帧模型保留超分输出倍率");
        Set(config, "Backend", "rtxvsr");
        Call(panel, "UpdateAdvancedControlState");
        Check(!(bool)Get(videoCombo, "Enabled") && !(bool)Get(imageCombo, "Enabled"), "RTX使用专用输出规格");
        Set(config, "Backend", "cuda");
        Set(imageCombo, "SelectedIndex", 4);
        Call(panel, "UpdateAdvancedControlState");
        Check((int)Get(videoCombo, "SelectedIndex") == 4, "图片设置同步视频");
        Check(!((string)Get(Field(panel, "_outputScaleHint"), "Text")).Contains("缩放至"), "原生输出不提示额外缩放");
        Console.WriteLine("界面倍率状态验证通过；未启动宿主或显示窗口");
    }
}
