using System.Collections;
using System.Reflection;
using System.Windows.Forms;

partial class Program
{
    static object SharedCall(Type type, string name, params object[] args) => type.GetMethod(name, Flags)!.Invoke(null, args)!;

    static void RunHostRuntimeChecks(Assembly plugin)
    {
        Check(plugin.GetReferencedAssemblies().All(reference => reference.Name != "FFmpegFreeUI"),
              "插件程序集元数据不引用 FFmpegFreeUI");
        var runtime = plugin.GetType("videoenhancer.HostRuntime")!;
        var queue = plugin.GetType("videoenhancer.HostQueueAccess")!;
        var settings = plugin.GetType("videoenhancer.HostSettings")!;
        var presets = plugin.GetType("videoenhancer.HostPresetAccess")!;
        var hostAccess = plugin.GetType("videoenhancer.HostAccess")!;
        var overrideProperty = runtime.GetProperty("TestAssembly", Flags)!;
        Check(((IList)SharedCall(queue, "GetQueueSnapshot")).Count == 0,
              "无宿主时可加载插件并取得空队列");
        overrideProperty.SetValue(null, typeof(FFmpegFreeUI.编码队列_v6).Assembly);
        try
        {
            var native = FFmpegFreeUI.编码队列_v6.Task;
            var snapshot = (IList)SharedCall(queue, "GetQueueSnapshot");
            var task = SharedCall(queue, "FindTask", native.ID);
            Check(snapshot.Count == 1 && (string)Get(snapshot[0]!, "ID") == native.ID,
                  "IReadOnlyList 快照转换为任务视图");
            Check((bool)Get(task, "IsPaused"), "暂停状态按名称解析而非复制枚举值");
            Check((int)Get(task, "当前进程ID") == 42 && (string)Get(task, "任务名称") == "fixture",
                  "任务字段和数值类型正确读取");
            Set(task, "输出文件", "changed.mkv");
            Set(task, "手动停止", true);
            Check(native.输出文件 == "changed.mkv" && native.手动停止, "任务属性写入宿主原对象");

            var progress = Get(task, "进度");
            Set(progress, "百分比", 0.5d);
            Set(progress, "效率文本", "24 FPS");
            Set(progress, "输出大小KB", 4096L);
            Check(native.进度.百分比 == 0.5f && native.进度.效率文本 == "24 FPS" && native.进度.输出大小KB == 4096,
                  "进度属性和字段写入且数值自动转换");
            Check((TimeSpan)Get(progress, "当前时间") == TimeSpan.FromSeconds(4), "原生时间进度可供预览读取");
            Check((bool)SharedCall(queue, "InvokeTaskCommand", "停止任务", new[] { native.ID, "second" }) &&
                  FFmpegFreeUI.编码队列_v6.LastIds.SequenceEqual(new[] { native.ID, "second" }),
                  "队列控制准确转发 ID 集合");
            Check((bool)SharedCall(queue, "InvokeTaskInstance", task, "暂停") && native.PauseCalled,
                  "任务实例控制作用于原对象");

            settings.GetProperty("AlternativeProcessPath", Flags)!.SetValue(null, "relative/videoenhancer.exe");
            Check(FFmpegFreeUI.设置_v6.实例对象.替代进程文件名 == "relative/videoenhancer.exe" &&
                  (string)SharedCall(settings, "GetWorkingDirectory") == "work",
                  "宿主设置读写正确");
            Check((int)SharedCall(plugin.GetType("videoenhancer.StopControl")!, "ReadHostOutputCleanupMode") == 0,
                  "停止逻辑读取宿主清理模式");
            var panel = SharedCall(hostAccess, "GetDefaultInstance", "Form_v6_参数面板");
            Check(ReferenceEquals(panel, FFmpegFreeUI.My.MyProject.Forms.Form_v6_参数面板),
                  "从宿主 My.Forms 获取默认窗体");
            var preset = SharedCall(presets, "从面板创建预设", panel);
            Check((string)Get(preset, "输出容器") == "mkv", "预设保留宿主原对象");
            SharedCall(presets, "刷新参数总览", panel);
            Check(FFmpegFreeUI.预设管理_v6.LastPanel == panel, "参数总览刷新接收原始窗体类型");
            var reserved = new HashSet<string> { "occupied.mkv" };
            Check((string)SharedCall(queue, "ComputeOutputPath", "input.mp4", preset, true, reserved) == "computed.mkv" &&
                  ReferenceEquals(FFmpegFreeUI.编码队列_v6.LastReserved, reserved), "输出计算保留原始预设与保留路径集合");
            var command = (string)SharedCall(plugin.GetType("videoenhancer.QueueCommandBuilder")!,
                "BuildFfmpegSettings", preset, "input.mp4", "output with spaces.mkv");
            Check(command == "-c:v libx264 \"output with spaces.mkv\" -y", "预设转换和命令占位符替换保持编码参数");
            Check((string)SharedCall(plugin.GetType("videoenhancer.PauseControl")!, "ExtractShmName", task) == "pause-id" &&
                  (string)SharedCall(plugin.GetType("videoenhancer.StopControl")!, "ExtractStopShm", task) == "stop-id",
                  "暂停和停止共享内存名称仍从任务命令行解析");

            FFmpegFreeUI.编码队列_v6.Task.进度 = null!;
            Check(Get(task, "进度") == null, "缺少进度对象保持空值");
            FFmpegFreeUI.编码队列_v6.Task.进度 = new FFmpegFreeUI.NativeProgress();
            var eventJson = "{\"task\":{\"id\":\"fixture-id\"},\"log\":{\"text\":\"Total Output Frames: 100\"}}";
            var backend = plugin.GetType("videoenhancer.BackendProgress")!;
            SharedCall(backend, "OnQueueEvent", "task.log", eventJson);
            SharedCall(backend, "OnQueueEvent", "task.log",
                "{\"task\":{\"id\":\"fixture-id\"},\"log\":{\"text\":\"FPS: 24 Current Frame: 50 ETA: 00:02\"}}");
            Check(native.进度.百分比 == 0.5f && native.进度.效率文本 == "24.00 FPS" && native.进度.当前阶段 == "视频超分",
                  "真实后端事件写入宿主任务进度");
            try
            {
                SharedCall(runtime, "InvokeShared", "预设管理_v6", "NativeFailure", Array.Empty<object>());
                throw new Exception("宿主异常被吞掉");
            }
            catch (TargetInvocationException error)
            {
                Check(error.InnerException is InvalidOperationException && error.InnerException.Message == "native-failure",
                      "宿主异常按原类型传出");
            }
        }
        finally
        {
            overrideProperty.SetValue(null, null);
            FFmpegFreeUI.My.MyProject.Forms.Form_v6_参数面板.Dispose();
        }

        // 可选核对真实宿主的公开契约，仅读取类型/成员元数据，不运行宿主或加载用户设置。
        var hostPath = Environment.GetEnvironmentVariable("VIDEOENHANCER_HOST_ASSEMBLY");
        if (!string.IsNullOrWhiteSpace(hostPath))
        {
            var host = Assembly.LoadFrom(Path.GetFullPath(hostPath));
            foreach (var name in new[] { "编码队列_v6", "编码任务_v6", "预设管理_v6", "预设数据_v6", "设置_v6", "Form_v6_参数面板" })
                Check(host.GetType("FFmpegFreeUI." + name) != null, "真实宿主类型契约 " + name);
            var taskType = host.GetType("FFmpegFreeUI.编码任务_v6")!;
            foreach (var name in new[] { "ID", "输入文件", "输出文件", "命令行", "状态", "当前进程ID", "正在执行", "手动停止", "进度" })
                Check(taskType.GetProperty(name, Flags) != null || taskType.GetField(name, Flags) != null,
                      "真实任务成员契约 " + name);
            foreach (var pair in new[] { ("编码队列_v6", "获取队列快照"), ("编码队列_v6", "根据ID获取任务"),
                ("编码队列_v6", "计算输出位置_v6"), ("预设管理_v6", "从面板创建预设"),
                ("预设管理_v6", "将预设数据转换为命令行"), ("预设管理_v6", "刷新参数总览") })
                Check(host.GetType("FFmpegFreeUI." + pair.Item1)!.GetMethods(Flags).Any(method => method.Name == pair.Item2),
                      "真实宿主方法契约 " + pair.Item2);
        }
        Console.WriteLine("HOST_RUNTIME_TESTS_PASS|no-host-reference|queue|progress|preset|settings|pause-stop");
    }
}

// 测试边界使用不同的任务/进度类型，并混用字段、属性和数值类型，检查适配实际行为。
namespace FFmpegFreeUI
{
    public enum NativeState { 已暂停 = 47 }
    public sealed class NativeTask
    {
        public string ID { get; } = "fixture-id";
        public string 任务名称 = "fixture";
        public string 输入文件 = "input.mp4";
        public string 输出文件 { get; set; } = "output.mkv";
        public string 命令行 = "-pause-shm pause-id -stop-shm stop-id";
        public NativeState 状态 = NativeState.已暂停;
        public bool 正在执行 => true;
        public long 当前进程ID = 42;
        public bool 手动停止 { get; set; }
        public NativeProgress 进度 { get; set; } = new();
        public bool PauseCalled;
        public void 暂停() => PauseCalled = true;
    }
    public sealed class NativeProgress
    {
        public TimeSpan 当前时间 = TimeSpan.FromSeconds(4);
        public TimeSpan 总时长 = TimeSpan.FromSeconds(8);
        public float 百分比 { get; set; }
        public string 进度文本 = "";
        public string 效率文本 { get; set; } = "";
        public string 时间文本 = "";
        public string 当前阶段 = "";
        public long 输出大小KB;
        public string 输出大小文本 = "";
    }
    public static class 编码队列_v6
    {
        public static NativeTask Task = new();
        public static string[] LastIds = Array.Empty<string>();
        public static HashSet<string> LastReserved = null!;
        public static IReadOnlyList<NativeTask> 获取队列快照() => new[] { Task };
        public static NativeTask 根据ID获取任务(string id) => Task.ID == id ? Task : null!;
        public static void 停止任务(IEnumerable<string> ids) => LastIds = ids.ToArray();
        public static string 计算输出位置_v6(string input, NativePreset preset, bool autoRename, HashSet<string> reserved)
        {
            if (input != "input.mp4" || preset.输出容器 != "mkv" || !autoRename) throw new Exception("原始参数丢失");
            LastReserved = reserved;
            return "computed.mkv";
        }
    }
    public sealed class NativePreset { public string 输出容器 = "mkv"; }
    public static class 预设管理_v6
    {
        public const string 输入占位符 = "<input>";
        public const string 输出占位符 = "<output>";
        public const string 媒体总时长占位符 = "<duration>";
        public static Control LastPanel = null!;
        public static NativePreset 从面板创建预设(Control panel) => new();
        public static void 刷新参数总览(Control panel) => LastPanel = panel;
        public static string 将预设数据转换为命令行(NativePreset preset, string input, string output)
            => $"-hide_banner -i \"{input}\" -c:v libx264 \"{output}\"";
        public static void NativeFailure() => throw new InvalidOperationException("native-failure");
    }
    public sealed class 设置_v6
    {
        public static 设置_v6 实例对象 { get; } = new();
        public string 替代进程文件名 = "";
        public int 任务失败自动删除输出文件 { get; set; } = 0;
        public static string 获取有效工作目录() => "work";
    }
}
namespace FFmpegFreeUI.My
{
    public static class MyProject { public static NativeForms Forms { get; } = new(); }
    public sealed class NativeForms { public Control Form_v6_参数面板 { get; } = new(); }
}
