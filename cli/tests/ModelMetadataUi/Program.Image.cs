using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Windows.Forms;

partial class Program
{
    static void RunImageChecks(Assembly assembly)
    {
        var config = Activator.CreateInstance(assembly.GetType("videoenhancer.PluginConfig")!)!;
        Set(config, "Backend", "onnx");
        Set(config, "Model", "ONNX/RealHatGAN-JP-Illustration-2x-fix1.onnx");
        using var panel = (Control)Activator.CreateInstance(assembly.GetType("videoenhancer.PluginPanel")!, config, true)!;
        IEnumerable<Control> Descendants(Control root)
        {
            foreach (Control child in root.Controls)
            {
                yield return child;
                foreach (var descendant in Descendants(child)) yield return descendant;
            }
        }
        var precisionLabels = Descendants(panel).Where(control => control.Text == "半精度推理").ToArray();
        Check(precisionLabels.Length == 2 && precisionLabels.All(label =>
                  Get(label, "InfoIconPosition").ToString() == "End" &&
                  ((string)Get(label, "ToolTipText")).Contains("组合任务") &&
                  ((string)Get(label, "ToolTipText")).Contains("固定精度 ONNX")),
              "PR17两处半精度标签均带原生图标及精度说明");
        Check(!(bool)Get(config, "UpscaleHalfPrecision") && !(bool)Get(config, "InterpHalfPrecision"),
              "新配置默认使用 FP32");
        var buildPrecision = (ProcessStartInfo)Call(panel, "CreateImageStartInfo", @"Z:\");
        Check(buildPrecision.ArgumentList[buildPrecision.ArgumentList.IndexOf("-upscale-precision") + 1] == "float32",
              "图片默认传递 FP32");
        Set(config, "UpscaleHalfPrecision", true);
        buildPrecision = (ProcessStartInfo)Call(panel, "CreateImageStartInfo", @"Z:\");
        Check(buildPrecision.ArgumentList[buildPrecision.ArgumentList.IndexOf("-upscale-precision") + 1] == "float16",
              "主动开启半精度时明确传递 FP16");
        Set(config, "UpscaleHalfPrecision", false);
        var jobType = assembly.GetType("videoenhancer.QueueJobOptions")!;
        var job = Activator.CreateInstance(jobType)!;
        foreach (var pair in new[] { ("input", "input.mp4"), ("output", "out.mkv"), ("ffmpegSettings", "out.mkv") })
            jobType.GetField(pair.Item1, Flags)!.SetValue(job, pair.Item2);
        jobType.GetField("interpOn", Flags)!.SetValue(job, true);
        var builder = assembly.GetType("videoenhancer.QueueCommandBuilder")!.GetMethod("BuildCliArgs", Flags)!;
        var command = (string)builder.Invoke(null, [job])!;
        Check(command.Contains("-upscale-precision float32") && command.Contains("-interp-precision float32") &&
              command.Contains("-interp-backend \"cuda\""), "队列默认使用 FP32 和支持16bit的补帧后端");
        jobType.GetField("upscaleHalfPrecision", Flags)!.SetValue(job, true);
        jobType.GetField("interpHalfPrecision", Flags)!.SetValue(job, true);
        command = (string)builder.Invoke(null, [job])!;
        Check(command.Contains("-upscale-precision float16") && command.Contains("-interp-precision float16"),
              "队列主动开启半精度时明确传递 FP16");
        var folders = (List<string>)Field(panel, "_imageFolders");
        folders.Add(@"\\server\share\输入 文件夹\");
        ProcessStartInfo lastStart = null!;
        foreach (var output in new[] { @"Z:\", @"\\server\share\输出 文件夹\" })
        {
            var start = (ProcessStartInfo)Call(panel, "CreateImageStartInfo", output);
            var expected = start.ArgumentList.ToArray();
            Check(start.Arguments == "", "图片命令使用独立参数列表");
            start.FileName = Environment.ProcessPath!;
            start.ArgumentList.Clear();
            start.ArgumentList.Add("--echo-args");
            foreach (var argument in expected) start.ArgumentList.Add(argument);
            using var child = Process.Start(start)!;
            var stdout = child.StandardOutput.ReadToEnd();
            var stderr = child.StandardError.ReadToEnd();
            child.WaitForExit();
            Check(child.ExitCode == 0, "参数回读进程成功：" + stderr);
            var actual = JsonSerializer.Deserialize<string[]>(stdout)!;
            Check(expected.SequenceEqual(actual), "根目录/UNC/中文空格/末尾反斜杠经过真实进程边界保持原样");
            Check(actual[Array.IndexOf(actual, "--image-output") + 1] == output &&
                  actual[Array.IndexOf(actual, "-backend") + 1] == "onnx" &&
                  actual[Array.IndexOf(actual, "-modelpath") + 1] == (string)Get(config, "Model"),
                  "输出路径没有吞掉 ONNX 后端与模型");
            start.ArgumentList.Clear();
            foreach (var argument in expected) start.ArgumentList.Add(argument);
            lastStart = start;
        }

        var error = "[错误] 未找到可用模型：ONNX/test.onnx\r\n[提示] 用法：-modelpath <模型名或路径>\r\n";
        Call(panel, "SetImageFailure", error, 1, lastStart!);
        Check(((string)Get(Field(panel, "_lblImageProgress"), "Text")).Contains("未找到可用模型") &&
              !((string)Get(Field(panel, "_lblImageProgress"), "Text")).Contains("用法"),
              "摘要优先展示主要错误");
        Check(((Control)Field(panel, "_btnImageErrorDetails")).Enabled &&
              File.ReadAllText((string)Field(panel, "_imageErrorLogPath")).Contains(error),
              "错误详情保留完整输出并可查看");
        var page = (Control)Field(panel, "_pageImage");
        panel.Controls.Add(page);
        foreach (var width in new[] { 600, 1100 })
        {
            panel.Size = new System.Drawing.Size(width, 600);
            ArrangeTree(panel);
            Call(panel, "SyncImageRootBounds");
            ArrangeTree(panel);
            var progress = (Control)Field(panel, "_imageProgress");
            var label = (Control)Field(panel, "_lblImageProgress");
            var details = (Control)Field(panel, "_btnImageErrorDetails");
            Check(progress.Parent!.Bottom <= label.Parent!.Top && label.Height >= 40 && details.Width > 0,
                  "进度条与错误行分开布局，详情入口可见：" + width);
        }
        Console.WriteLine("IMAGE_PAGE_TESTS_PASS");
    }
}
