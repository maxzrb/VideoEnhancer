using System.Diagnostics;
using System.Reflection;
using System.Text.Json;

partial class Program
{
    static bool RunConverterCatalogFixture(string[] args)
    {
        if (!args.Contains("--list-model-catalog")) return false;
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.Write("[{\"id\":\"ONNX/约束 模型\",\"inputMultiple\":16}," +
                      "{\"id\":\"User/upscale/HAT/用户模型\",\"inputMultiple\":32}," +
                      "{\"id\":\"ONNX/旧目录协议\"}]");
        return true;
    }

    static void RunConverterChecks(Assembly assembly)
    {
        var panel = assembly.GetType("videoenhancer.PluginPanel")!;
        var modelsRoot = Path.Combine(AppContext.BaseDirectory, "models");
        var resolve = panel.GetMethod("ReadConverterInputMultiple", Flags)!;
        foreach (var pair in new[] { ("ONNX/约束 模型.onnx", 16), ("User/upscale/HAT/用户模型.onnx", 32),
                                     ("ONNX/旧目录协议.onnx", 1), ("ONNX/未登记模型.onnx", 1) })
        {
            var value = (int)resolve.Invoke(null, new object[] { Environment.ProcessPath!, Path.Combine(modelsRoot, pair.Item1) })!;
            Check(value == pair.Item2, "手动转换读取目录尺寸倍数：" + pair.Item1);
        }
        var create = panel.GetMethod("CreateTensorRtConversionStartInfo", Flags)!;
        var input = Path.Combine(modelsRoot, "ONNX/约束 模型.onnx");
        var output = Path.Combine(AppContext.BaseDirectory, "Engine 输出");
        var start = (ProcessStartInfo)create.Invoke(null, new object[] { Environment.ProcessPath!, "--echo-args", input, output, 16 })!;
        Check(start.ArgumentList.Contains("--input-multiple") && start.ArgumentList.Last() == "16",
              "ONNX 约束进入实际转换进程参数");
        // 用真实子进程回读，保证中文和含空格路径不会与尺寸约束合并。
        start.WorkingDirectory = AppContext.BaseDirectory;
        using var child = Process.Start(start)!;
        var stdout = child.StandardOutput.ReadToEnd();
        child.WaitForExit();
        var actual = JsonSerializer.Deserialize<string[]>(stdout)!;
        Check(child.ExitCode == 0 && actual.SequenceEqual(new[] { input, "--output-dir", output, "--input-multiple", "16" }),
              "转换参数经过进程边界保持原样");
        var pth = (ProcessStartInfo)create.Invoke(null, new object[] { Environment.ProcessPath!, "--echo-args", "model.pth", output, 16 })!;
        Check(!pth.ArgumentList.Contains("--input-multiple"), "PTH 不附加 ONNX 专用约束");
        Console.WriteLine("CONVERTER_INPUT_MULTIPLE_TESTS_PASS");
    }
}
