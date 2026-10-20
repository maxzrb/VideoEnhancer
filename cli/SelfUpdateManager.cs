using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace VideoEnhancer;

internal static class SelfUpdateManager
{
    private const string EmbeddedPluginResource = "VideoEnhancer.Embedded.videoenhancer.3fui.dll";

    internal static int Apply(string[] args)
    {
        var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 1; index < args.Length; index += 2)
        {
            if (index + 1 >= args.Length || !args[index].StartsWith("--", StringComparison.Ordinal))
                return Fail("更新参数格式无效");
            options[args[index]] = args[index + 1];
        }
        if (!options.TryGetValue("--update-package", out var package) ||
            !options.TryGetValue("--update-target", out var target) ||
            !options.TryGetValue("--wait-pid", out var pidText) ||
            !int.TryParse(pidText, NumberStyles.None, CultureInfo.InvariantCulture, out var waitPid) ||
            waitPid < 0 || waitPid == Environment.ProcessId)
            return Fail("更新缺少有效的包、目标目录或等待进程 ID");

        var packagePath = Path.GetFullPath(package);
        var pluginRoot = Path.GetFullPath(target).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!Path.GetFileName(pluginRoot).Equals("Plugin", StringComparison.OrdinalIgnoreCase) ||
            !HostInstallation.IsHostRoot(Path.GetDirectoryName(pluginRoot)!))
            return Fail("更新目标不是有效的 3FUI Plugin 目录");
        if (!File.Exists(packagePath)) return Fail("更新包不存在：" + packagePath);
        var applicationRoot = ApplicationLayoutManager.ApplicationRoot(pluginRoot);
        var resultPath = Path.Combine(applicationRoot, ".update", "update-result.txt");
        var workRoot = Path.Combine(applicationRoot, ".update", "transactions", Guid.NewGuid().ToString("N"));
        var hostExitConfirmed = waitPid == 0;
        try
        {
            if (waitPid > 0)
            {
                try
                {
                    using var host = Process.GetProcessById(waitPid);
                    if (!host.WaitForExit(5 * 60 * 1000))
                        throw new TimeoutException("等待 3FUI 退出超时，未修改文件");
                }
                catch (ArgumentException)
                {
                    // 宿主在查询前已退出。
                }
                hostExitConfirmed = true;
            }

            var updaterPath = Path.GetFullPath(Environment.ProcessPath
                ?? throw new InvalidOperationException("无法确定临时更新器路径"));
            using (var source = File.OpenRead(packagePath))
            using (var updater = File.OpenRead(updaterPath))
            {
                var sourceHash = SHA256.HashData(source);
                var updaterHash = SHA256.HashData(updater);
                if (!CryptographicOperations.FixedTimeEquals(sourceHash, updaterHash))
                    throw new InvalidDataException("更新包和临时更新器哈希不一致");
            }

            var staging = Path.Combine(workRoot, "staging");
            Directory.CreateDirectory(staging);
            var stagedExe = Path.Combine(staging, "videoenhancer.exe");
            var stagedPlugin = Path.Combine(staging, "videoenhancer.3fui.dll");
            File.Copy(packagePath, stagedExe);
            using (var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream(EmbeddedPluginResource)
                ?? throw new InvalidDataException("更新包没有内嵌插件 DLL"))
            using (var output = File.Create(stagedPlugin))
                resource.CopyTo(output);
            if (new FileInfo(stagedPlugin).Length == 0)
                throw new InvalidDataException("更新包内的插件 DLL 为空");

            var existingCoreRoot = Directory.Exists(Path.Combine(applicationRoot, "python"))
                ? applicationRoot : pluginRoot;
            BackendUpdateManager.RecoverPending(existingCoreRoot);
            ApplicationLayoutManager.Install(pluginRoot, stagedExe, stagedPlugin,
                replaceCanonicalExe: true, removeLegacyExe: true);
            var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";
            WriteResult(resultPath, "OK|" + version);
            Console.WriteLine("UPDATE_COMPLETE|" + version);
            return 0;
        }
        catch (Exception ex)
        {
            WriteResult(resultPath, "ERROR|" + ex.Message.Replace('\r', ' ').Replace('\n', ' '));
            return Fail("应用更新失败：" + ex.Message);
        }
        finally
        {
            try { if (Directory.Exists(workRoot)) Directory.Delete(workRoot, true); } catch { }
            if (hostExitConfirmed && options.TryGetValue("--restart-exe", out var restartExe) &&
                !string.IsNullOrWhiteSpace(restartExe))
            {
                try
                {
                    if (File.Exists(restartExe))
                        Process.Start(new ProcessStartInfo(restartExe) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("[警告] 无法重启 3FUI：" + ex.Message);
                }
            }
        }
    }

    private static void WriteResult(string path, string value)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, value, new UTF8Encoding(false));
        }
        catch
        {
            // 结果文件只供下次启动提示，不影响更新或回滚结果。
        }
    }

    private static int Fail(string message)
    {
        Console.Error.WriteLine("[错误] " + message);
        return 1;
    }
}
