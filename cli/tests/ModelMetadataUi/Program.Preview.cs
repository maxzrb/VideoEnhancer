using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.Text.Json;
using System.Windows.Forms;

partial class Program
{
    // 用同一个测试程序模拟慢探测和抽帧，不启动宿主或触碰用户任务。
    static bool RunPreviewToolFixture(string[] args)
    {
        var root = Environment.GetEnvironmentVariable("VIDEOENHANCER_PREVIEW_FIXTURE");
        if (string.IsNullOrEmpty(root) || !args.Contains("-v")) return false;
        var capture = args.Contains("-frames:v");
        File.AppendAllText(Path.Combine(root, "calls.txt"), JsonSerializer.Serialize(args) + "\n");
        File.WriteAllText(Path.Combine(root, capture ? "capture.pid" : "probe.pid"), Environment.ProcessId.ToString());
        var mode = File.ReadAllText(Path.Combine(root, "mode.txt"));
        if (mode == "stall" || (capture && mode == "slow-capture")) Thread.Sleep(5000);
        if (capture)
        {
            if (mode == "no-frame") return true;
            using var image = new Bitmap(32, 24);
            using var graphics = Graphics.FromImage(image);
            graphics.Clear(Color.Coral);
            image.Save(Console.OpenStandardOutput(), ImageFormat.Jpeg);
        }
        else if (args.Contains("stream=avg_frame_rate")) Console.WriteLine("24/1");
        else Console.Write(File.ReadAllText(Path.Combine(root, "packets.txt")));
        return true;
    }

    static void PumpUntil(Func<bool> done, int milliseconds = 2000)
    {
        var timer = Stopwatch.StartNew();
        while (!done() && timer.ElapsedMilliseconds < milliseconds)
        {
            Application.DoEvents();
            Thread.Sleep(10);
        }
        Application.DoEvents();
        if (!done()) throw new Exception("预览测试等待超时");
    }

    static void PutField(object target, string name, object value) => target.GetType().GetField(name, Flags)!.SetValue(target, value);

    static void RunPreviewEngineChecks(Assembly plugin, bool probeOnly)
    {
        var directory = Path.Combine(Path.GetTempPath(), "VideoEnhancer-preview-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "mode.txt"), "");
        File.WriteAllText(Path.Combine(directory, "packets.txt"), "0,K_\n1,__\n2,K_\n");
        var output = Path.Combine(directory, "growing.mkv");
        File.WriteAllBytes(output, new byte[8]);
        var previous = Environment.GetEnvironmentVariable("VIDEOENHANCER_PREVIEW_FIXTURE");
        Environment.SetEnvironmentVariable("VIDEOENHANCER_PREVIEW_FIXTURE", directory);
        var runtime = plugin.GetType("videoenhancer.HostRuntime")!;
        var host = runtime.GetProperty("TestAssembly", Flags)!;
        host.SetValue(null, typeof(FFmpegFreeUI.编码队列_v6).Assembly);
        using var owner = new Control();
        _ = owner.Handle;
        var native = FFmpegFreeUI.编码队列_v6.Task;
        native.输入文件 = output;
        var config = Activator.CreateInstance(plugin.GetType("videoenhancer.PluginConfig")!)!;
        var engineType = plugin.GetType("videoenhancer.PreviewEngine")!;
        var frames = new List<DateTime>();
        var hashes = new HashSet<string>();
        object CreateEngine()
        {
            var engine = Activator.CreateInstance(engineType, config, owner)!;
            Call(engine, "Start");
            ((System.Windows.Forms.Timer)Field(engine, "_timer")).Stop();
            var executable = Path.ChangeExtension(Assembly.GetExecutingAssembly().Location, ".exe");
            PutField(engine, "_ffmpeg", executable);
            PutField(engine, "_ffprobe", executable);
            PutField(engine, "_lastTaskId", native.ID);
            var cache = (System.Collections.IDictionary)Field(engine, "_inputFpsCache");
            cache[native.输入文件] = 24d;
            Action<object, Image> callback = (_, image) =>
            {
                using (image)
                using (var buffer = new MemoryStream())
                {
                    image.Save(buffer, ImageFormat.Png);
                    hashes.Add(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(buffer.ToArray())));
                }
                frames.Add(DateTime.UtcNow);
            };
            var ready = engineType.GetEvent("FrameReady")!;
            ready.AddEventHandler(engine, Delegate.CreateDelegate(ready.EventHandlerType!, callback.Target, callback.Method));
            return engine;
        }
        void Tick(object engine) => Call(engine, "OnTick", null!, EventArgs.Empty);
        try
        {
            using (var engine = (IDisposable)CreateEngine())
            {
                native.输出文件 = Path.Combine(directory, "not-muxed-yet.mkv");
                native.当前步骤 = new() { 显示名称 = "音频编码", 实际执行参数 = $"-i \"{output}\" -vn \"{directory}\\audio.flac\"" };
                native.进度.当前时间 = TimeSpan.FromSeconds(80);
                var before = frames.Count;
                Tick(engine);
                Check(!(bool)Field(engine, "_busy") && frames.Count == before &&
                      (double)Field(engine, "_lastProgressSeconds") == -1,
                      "音频阶段不抽视频帧、不消费视频进度");
                native.当前步骤 = new() { 实际执行参数 = $"-i \"{output}\" -an \"{output}\" -y" };
                native.当前进程ID++;
                native.进度.当前时间 = TimeSpan.FromSeconds(0.2);
                // 模拟上一个步骤遗留的高进度，视频开始必须恢复。
                PutField(engine, "_lastProgressSeconds", 80d);
                Tick(engine);
                PumpUntil(() => frames.Count > before);
                Check(File.ReadAllLines(Path.Combine(directory, "calls.txt")).Any(line =>
                    line.Contains("-i\",\"" + output.Replace("\\", "\\\\") + "\"")),
                    "视频阶段从实际临时输出读取，不等待最终封装文件");
                PumpUntil(() => !(bool)Field(engine, "_busy"));
                native.进度.当前时间 = TimeSpan.FromSeconds(70);
                Tick(engine);
                PumpUntil(() => !(bool)Field(engine, "_busy"));
                var count = frames.Count;
                native.进度.当前时间 = TimeSpan.FromSeconds(0.3);
                Tick(engine);
                PumpUntil(() => frames.Count > count);
                Check((double)Field(engine, "_lastProgressSeconds") == 0.3,
                      "同任务进度回退清空抽帧游标，低时间新阶段仍可更新");
                native.当前步骤 = null;
            }
            using (var engine = (IDisposable)CreateEngine())
            {
                native.输出文件 = output;
                native.进度.当前时间 = TimeSpan.FromSeconds(1);
                File.WriteAllText(Path.Combine(directory, "packets.txt"), "0,K_\n");
                File.WriteAllText(Path.Combine(directory, "mode.txt"), "no-frame");
                Call(engine, "SetKeyframeMode", true);
                var before = frames.Count;
                Tick(engine);
                PumpUntil(() => !(bool)Field(engine, "_tickBusy") && !(bool)Field(engine, "_busy"));
                Check(frames.Count == before && (double)Field(engine, "_lastKeyframePts") == -1,
                      "关键帧未完整写入时不提前记作已显示，也不冒充输入原帧");
                File.WriteAllText(Path.Combine(directory, "mode.txt"), "");
                PutField(engine, "_lastExtractStartAt", DateTime.MinValue);
                Tick(engine);
                PumpUntil(() => frames.Count > before);
                Check((double)Field(engine, "_lastKeyframePts") == 0,
                      "相同文件大小可重试刚才失败的关键帧，无需等待下个长GOP");
            }
            using (var engine = (IDisposable)CreateEngine())
            {
                var before = frames.Count;
                File.WriteAllText(Path.Combine(directory, "mode.txt"), "slow-capture");
                Tick(engine);
                Check((bool)Field(engine, "_busy"), "阶段取消用例已启动旧抽帧");
                native.当前步骤 = new() { 显示名称 = "音频编码", 实际执行参数 = "-vn" };
                native.当前进程ID++;
                native.进度.当前时间 = TimeSpan.FromSeconds(80);
                Tick(engine);
                PumpUntil(() => !(bool)Field(engine, "_busy"));
                Check(frames.Count == before, "进入音频步骤会取消并拒绝旧视频画面");
                File.WriteAllText(Path.Combine(directory, "mode.txt"), "");
                native.当前步骤 = null;
                native.进度.当前时间 = TimeSpan.FromSeconds(0.2);
                Tick(engine);
                PumpUntil(() => frames.Count > before);
                Check((double)Field(engine, "_lastProgressSeconds") == 0.2, "取消旧工作后新视频阶段可恢复预览");
            }
            File.WriteAllText(Path.Combine(directory, "calls.txt"), "");
            if (!probeOnly)
            {
                foreach (var interval in new[] { 0.5, 1d, 2d, 3d })
                {
                    using var engine = (IDisposable)CreateEngine();
                    Set(engine, "IntervalSeconds", interval);
                    PutField(engine, "_lastProgressSeconds", 0d);
                    native.输出文件 = "";
                    // 每次进度只前进0.1秒，仍应相对上次抽帧累计到0.3秒。
                    PutField(engine, "_lastExtractStartAt", DateTime.UtcNow);
                    native.进度.当前时间 = TimeSpan.FromSeconds(0.1);
                    Tick(engine);
                    native.进度.当前时间 = TimeSpan.FromSeconds(0.2);
                    Tick(engine);
                    PutField(engine, "_lastExtractStartAt", DateTime.UtcNow - TimeSpan.FromSeconds(interval + 0.1));
                    native.进度.当前时间 = TimeSpan.FromSeconds(0.3);
                    var before = frames.Count;
                    Tick(engine);
                    Check((DateTime)Field(engine, "_lastExtractStartAt") > DateTime.UtcNow.AddSeconds(-0.5),
                          $"{interval}秒模式按累计进度启动新抽帧");
                    PumpUntil(() => !(bool)Field(engine, "_busy"));
                    PumpUntil(() => frames.Count > before);
                }
            }

            using (var engine = (IDisposable)CreateEngine())
            {
                native.输出文件 = output;
                native.进度.当前时间 = TimeSpan.FromSeconds(8);
                File.WriteAllText(Path.Combine(directory, "mode.txt"), "stall");
                Call(engine, "SetKeyframeMode", true);
                var stopwatch = Stopwatch.StartNew();
                Tick(engine);
                Check(stopwatch.ElapsedMilliseconds < 200, "关键帧慢探测不阻塞UI线程");
                var heartbeat = 0;
                using var pulse = new System.Windows.Forms.Timer { Interval = 20 };
                pulse.Tick += (_, _) => heartbeat++;
                pulse.Start();
                PumpUntil(() => heartbeat >= 5);
                Tick(engine);
                var calls = File.ReadAllLines(Path.Combine(directory, "calls.txt"));
                Check(calls.Count(line => line.Contains("packet=pts_time,flags")) == 1, "探测单航班防止Tick重入叠加");
                PumpUntil(() => !(bool)Field(engine, "_tickBusy"), 4000);
                var probePid = int.Parse(File.ReadAllText(Path.Combine(directory, "probe.pid")));
                Check(!Process.GetProcesses().Any(process => process.Id == probePid), "探测超时真实结束子进程");
                Tick(engine);
                Check(File.ReadAllLines(Path.Combine(directory, "calls.txt")).Length == calls.Length,
                      "未命中新关键帧且文件未增长时不重复探测");
            }

            File.WriteAllText(Path.Combine(directory, "mode.txt"), "");
            using (var engine = (IDisposable)CreateEngine())
            {
                native.输出文件 = output;
                Call(engine, "SetKeyframeMode", true);
                File.WriteAllText(Path.Combine(directory, "packets.txt"), "0,K_\n1,__\n");
                var before = frames.Count;
                Tick(engine);
                PumpUntil(() => frames.Count > before);
                Check((double)Field(engine, "_lastKeyframePts") == 0, "PTS为0的首个关键帧可正常显示");
                Check(File.ReadAllLines(Path.Combine(directory, "calls.txt")).Any(line => line.Contains("-ss\",\"0\"")),
                      "首个关键帧按0秒定位而非回退到最新进度");
                File.AppendAllText(output, "growth");
                PutField(engine, "_lastProbeAt", DateTime.MinValue);
                PutField(engine, "_lastExtractStartAt", DateTime.MinValue);
                File.WriteAllText(Path.Combine(directory, "packets.txt"), "1,__\n2,K_\n3,__\n");
                Tick(engine);
                PumpUntil(() => frames.Count > before + 1);
                Check((double)Field(engine, "_lastKeyframePts") == 2 &&
                      File.ReadAllLines(Path.Combine(directory, "calls.txt")).Any(line => line.Contains("-read_intervals")),
                      "增量包标记只显示新关键帧，不使用全文件解码");
            }

            foreach (var action in new[] { "interval", "task", "hide", "stop" })
            {
                using var engine = (IDisposable)CreateEngine();
                File.WriteAllText(Path.Combine(directory, "mode.txt"), "stall");
                native.输出文件 = output;
                Call(engine, "SetKeyframeMode", true);
                Tick(engine);
                PumpUntil(() => File.Exists(Path.Combine(directory, "probe.pid")));
                if (action == "interval") Set(engine, "IntervalSeconds", 0.5d);
                if (action == "task") Set(engine, "SelectedTaskId", "next-task");
                if (action == "hide") Set(engine, "PreviewVisible", false);
                if (action == "stop") Call(engine, "Stop_");
                PumpUntil(() => !(bool)Field(engine, "_tickBusy"));
                Check((double)Field(engine, "_lastKeyframePts") == -1, $"{action}取消旧探测并丢弃过期结果");
            }

            File.WriteAllText(Path.Combine(directory, "mode.txt"), "slow-capture");
            using (var engine = (IDisposable)CreateEngine())
            {
                Set(engine, "IntervalSeconds", 0.5d);
                var before = frames.Count;
                Tick(engine);
                Set(engine, "SelectedTaskId", "next-task");
                PumpUntil(() => !(bool)Field(engine, "_busy"));
                Check(frames.Count == before, "任务切换取消慢抽帧且不回灌旧画面");
            }

            File.WriteAllText(Path.Combine(directory, "mode.txt"), "stall");
            using (var engine = (IDisposable)CreateEngine())
            {
                ((System.Collections.IDictionary)Field(engine, "_inputFpsCache")).Clear();
                var watch = Stopwatch.StartNew();
                Tick(engine);
                Check(watch.ElapsedMilliseconds < 200, "输入帧率慢探测也不阻塞UI线程");
                Set(engine, "PreviewVisible", false);
                PumpUntil(() => !(bool)Field(engine, "_tickBusy"));
            }

            using (var engine = (IDisposable)CreateEngine())
            {
                ((System.Collections.IDictionary)Field(engine, "_inputFpsCache")).Clear();
                File.WriteAllText(Path.Combine(directory, "mode.txt"), "stall");
                var failed = (Task<double>)Call(engine, "ProbeInputFpsAsync", native.输入文件, CancellationToken.None);
                PumpUntil(() => failed.IsCompleted, 4000);
                Check(failed.GetAwaiter().GetResult() == 0 &&
                      !((System.Collections.IDictionary)Field(engine, "_inputFpsCache")).Contains(native.输入文件),
                      "帧率超时不缓存零值");
                File.WriteAllText(Path.Combine(directory, "mode.txt"), "");
                var recovered = (Task<double>)Call(engine, "ProbeInputFpsAsync", native.输入文件, CancellationToken.None);
                PumpUntil(() => recovered.IsCompleted);
                Check(recovered.GetAwaiter().GetResult() == 24 &&
                      (double)((System.Collections.IDictionary)Field(engine, "_inputFpsCache"))[native.输入文件]! == 24,
                      "同一输入在超时后恢复探测并缓存有效帧率");
            }

            foreach (var option in new[] { "-ffmpeg-settings ", "--ffmpeg-settings=" })
            {
                native.输出文件 = Path.Combine(directory, "enhanced.mkv");
                native.当前步骤 = new()
                {
                    显示名称 = "命令行",
                    实际执行参数 = $"-i \"{output}\" {option}\"-c:v libx264 out.mkv -y\" --ffprobe-path \"{directory}\\ffprobe.exe\""
                };
                var wrapped = SharedCall(plugin.GetType("videoenhancer.HostTask")!, "Wrap", native);
                var values = new object[] { wrapped, "", false };
                var resolved = (string)engineType.GetMethod("ResolvePreviewOutput", Flags)!.Invoke(null, values)!;
                Check(resolved == native.输出文件 && !(bool)values[2],
                      "有当前步骤的CLI中转保留增强输出，不误取末尾工具路径：" + option.Trim());
            }
            native.当前步骤 = null;

            var media = Environment.GetEnvironmentVariable("VIDEOENHANCER_PREVIEW_MEDIA");
            if (!string.IsNullOrEmpty(media))
            {
                native.输入文件 = media;
                native.输出文件 = media;
                foreach (var interval in new[] { 0.5, 1d, 2d, 3d })
                {
                    using var engine = (IDisposable)CreateEngine();
                    using var panel = (Control)Activator.CreateInstance(plugin.GetType("videoenhancer.PluginPanel")!, config, true)!;
                    PutField(panel, "_engine", engine);
                    var combo = Field(panel, "_cmbRate");
                    Set(combo, "SelectedIndex", 4);
                    Check((bool)Get(engine, "IsKeyframeMode"), "下拉框可选关键帧模式");
                    Set(combo, "SelectedIndex", Array.IndexOf(new[] { 0.5, 1d, 2d, 3d }, interval));
                    Check((double)Get(engine, "IntervalSeconds") == interval && !(bool)Get(engine, "IsKeyframeMode"),
                          $"下拉框{interval}秒实际更新引擎并退出关键帧模式");
                    PutField(panel, "_engine", null!);
                    PutField(engine, "_ffmpeg", Environment.GetEnvironmentVariable("VIDEOENHANCER_PREVIEW_FFMPEG")!);
                    PutField(engine, "_ffprobe", Environment.GetEnvironmentVariable("VIDEOENHANCER_PREVIEW_FFPROBE")!);
                    frames.Clear();
                    hashes.Clear();
                    var clock = Stopwatch.StartNew();
                    using var progress = new System.Windows.Forms.Timer { Interval = 20 };
                    progress.Tick += (_, _) => native.进度.当前时间 = TimeSpan.FromSeconds(1 + clock.Elapsed.TotalSeconds);
                    native.进度.当前时间 = TimeSpan.FromSeconds(1);
                    progress.Start();
                    ((System.Windows.Forms.Timer)Field(engine, "_timer")).Start();
                    Tick(engine);
                    PumpUntil(() => frames.Count >= 2, (int)((interval * 2 + 3) * 1000));
                    var gap = (frames[1] - frames[0]).TotalSeconds;
                    // 4K/12-bit解码本身可能超过刷新周期；仍必须在抽帧超时预算内持续换图。
                    Check(gap >= interval - 0.15 && gap <= Math.Max(interval + 1, 6.5) && hashes.Count >= 2,
                          $"真实媒体{interval}秒模式连续换帧，间隔{gap:F2}秒且画面不同");
                }
            }

            var encodeSource = Environment.GetEnvironmentVariable("VIDEOENHANCER_PREVIEW_ENCODE_SOURCE");
            if (!string.IsNullOrEmpty(encodeSource))
            {
                native.输入文件 = encodeSource;
                native.输出文件 = Path.Combine(directory, "final-not-muxed.mkv");
                native.当前步骤 = new() { 显示名称 = "音频编码", 实际执行参数 = "-vn" };
                native.进度.当前时间 = TimeSpan.FromSeconds(80);
                using var engine = (IDisposable)CreateEngine();
                PutField(engine, "_ffmpeg", Environment.GetEnvironmentVariable("VIDEOENHANCER_PREVIEW_FFMPEG")!);
                PutField(engine, "_ffprobe", Environment.GetEnvironmentVariable("VIDEOENHANCER_PREVIEW_FFPROBE")!);
                Set(engine, "IntervalSeconds", 0.5d);
                frames.Clear();
                hashes.Clear();
                Tick(engine);
                Check(frames.Count == 0 && !(bool)Field(engine, "_busy"), "真实原片测试先等待音频阶段");
                var video = Path.Combine(directory, "native-p6-video.mkv");
                var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("VIDEOENHANCER_PREVIEW_FFMPEG")!)
                {
                    UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardError = true, RedirectStandardOutput = true
                };
                foreach (var argument in new[] { "-v", "warning", "-nostdin", "-ss", "5", "-i", encodeSource,
                    "-t", "3", "-map", "0:v:0", "-an", "-c:v", "libsvtav1", "-preset", "6", "-crf", "12",
                    "-pix_fmt", "yuv444p12le", "-svtav1-params", "enable-tf=0:enable-cdef=-1:lp=4", "-g", "48",
                    "-flush_packets", "1", "-cluster_time_limit", "250", "-progress", "pipe:1", video })
                    start.ArgumentList.Add(argument);
                using var writer = Process.Start(start)!;
                var errors = writer.StandardError.ReadToEndAsync();
                var positions = new System.Collections.Concurrent.ConcurrentQueue<double>();
                var reader = Task.Run(async () =>
                {
                    while (await writer.StandardOutput.ReadLineAsync() is { } line)
                        if (line.StartsWith("out_time_us=") && double.TryParse(line[12..],
                            System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var time))
                            positions.Enqueue(time / 1000000);
                });
                native.当前步骤 = new() { 实际执行参数 = string.Join(" ", start.ArgumentList.Select(arg => "\"" + arg + "\"")) };
                native.当前进程ID = writer.Id;
                native.进度.当前时间 = TimeSpan.Zero;
                using var progress = new System.Windows.Forms.Timer { Interval = 50 };
                progress.Tick += (_, _) => { while (positions.TryDequeue(out var time)) native.进度.当前时间 = TimeSpan.FromSeconds(time); };
                progress.Start();
                ((System.Windows.Forms.Timer)Field(engine, "_timer")).Start();
                try
                {
                    PumpUntil(() => writer.HasExited && (writer.ExitCode != 0 || (frames.Count >= 3 && hashes.Count >= 3)), 60000);
                    Check(writer.ExitCode == 0, "2160p/约48fps原片p6/CRF12/444p12纯压制正常完成");
                    Check(frames.Count >= 3 && hashes.Count >= 3, "真实编码进度驱动预览连续更新不同画面，未等最终封装路径");
                    Console.WriteLine($"NATIVE_ENCODE_PREVIEW|frames={frames.Count}|different={hashes.Count}|video={video}");
                }
                finally
                {
                    Call(engine, "Stop_");
                    if (!writer.HasExited) writer.Kill(entireProcessTree: true);
                    writer.WaitForExit();
                    File.WriteAllText(Path.Combine(directory, "native-encode.log"), errors.GetAwaiter().GetResult());
                    reader.GetAwaiter().GetResult();
                    PumpUntil(() => !(bool)Field(engine, "_tickBusy") && !(bool)Field(engine, "_busy"));
                    native.当前步骤 = null;
                }
            }

            var growingSource = Environment.GetEnvironmentVariable("VIDEOENHANCER_PREVIEW_GROWING_SOURCE");
            if (!string.IsNullOrEmpty(growingSource))
            {
                var growing = Path.Combine(directory, "live-av1.mkv");
                var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("VIDEOENHANCER_PREVIEW_FFMPEG")!)
                {
                    UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardError = true
                };
                foreach (var argument in new[] { "-v", "error", "-re", "-i", growingSource, "-c:v", "copy", "-an",
                                               "-flush_packets", "1", "-cluster_time_limit", "250", growing })
                    start.ArgumentList.Add(argument);
                using var writer = Process.Start(start)!;
                var errors = writer.StandardError.ReadToEndAsync();
                try
                {
                    native.输入文件 = growingSource;
                    native.输出文件 = growing;
                    using var engine = (IDisposable)CreateEngine();
                    PutField(engine, "_ffmpeg", Environment.GetEnvironmentVariable("VIDEOENHANCER_PREVIEW_FFMPEG")!);
                    PutField(engine, "_ffprobe", Environment.GetEnvironmentVariable("VIDEOENHANCER_PREVIEW_FFPROBE")!);
                    Call(engine, "SetKeyframeMode", true);
                    frames.Clear();
                    hashes.Clear();
                    var clock = Stopwatch.StartNew();
                    using var progress = new System.Windows.Forms.Timer { Interval = 20 };
                    progress.Tick += (_, _) => native.进度.当前时间 = TimeSpan.FromSeconds(clock.Elapsed.TotalSeconds);
                    progress.Start();
                    ((System.Windows.Forms.Timer)Field(engine, "_timer")).Start();
                    PumpUntil(() => hashes.Count >= 3 && (double)Field(engine, "_lastKeyframePts") >= 2, 7000);
                    Check(!writer.HasExited, "未封口AV1/4:4:4 12-bit文件写入期间持续显示三个新关键帧");
                    Call(engine, "Stop_");
                    PumpUntil(() => !(bool)Field(engine, "_tickBusy") && !(bool)Field(engine, "_busy"));
                }
                finally
                {
                    if (!writer.WaitForExit(10000)) writer.Kill(entireProcessTree: true);
                }
                Check(writer.ExitCode == 0, "AV1写入进程正常完成，预览未干扰编码");
            }
        }
        finally
        {
            host.SetValue(null, null);
            native.当前步骤 = null;
            Environment.SetEnvironmentVariable("VIDEOENHANCER_PREVIEW_FIXTURE", previous);
        }
        Console.WriteLine("PREVIEW_ENGINE_PASS|cadence|ui-heartbeat|single-flight|timeout|unchanged-output");
        Console.WriteLine("预览测试证据：" + directory);
    }
}
