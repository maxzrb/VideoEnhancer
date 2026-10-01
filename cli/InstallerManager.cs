using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;

namespace VideoEnhancer;

/// <summary>便携自解包安装器：命令行确认后选择 3FUI 主程序并复制插件文件。</summary>
internal static class InstallerManager
{
    private const string PluginResource = "VideoEnhancer.Embedded.videoenhancer.3fui.dll";
    private const uint FileDialogFlags = 0x00080000 | 0x00000800 | 0x00001000;
    private const uint HideWindow = 0;

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetConsoleWindow();

    [DllImport("kernel32.dll")]
    private static extern uint GetConsoleProcessList([Out] uint[] processList, uint processCount);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr window, uint command);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBox(IntPtr owner, string text, string caption, uint flags);

    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetOpenFileName(ref OpenFileName dialog);

    [DllImport("comdlg32.dll")]
    private static extern uint CommDlgExtendedError();

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct OpenFileName
    {
        public int Size;
        public IntPtr Owner;
        public IntPtr Instance;
        public IntPtr Filter;
        public IntPtr CustomFilter;
        public int MaxCustomFilter;
        public int FilterIndex;
        public IntPtr File;
        public int MaxFile;
        public IntPtr FileTitle;
        public int MaxFileTitle;
        public IntPtr InitialDirectory;
        public IntPtr Title;
        public uint Flags;
        public short FileOffset;
        public short FileExtension;
        public IntPtr DefaultExtension;
        public IntPtr CustomData;
        public IntPtr Hook;
        public IntPtr TemplateName;
        public IntPtr Reserved;
        public int ReservedDword;
        public uint FlagsEx;
    }

    internal static bool IsInstaller() =>
        Environment.ProcessPath is { } path && InstallerBundle.HasFooter(path);

    internal static bool TryShowStandaloneLaunchGuidance()
    {
        var console = GetConsoleWindow();
        if (console == IntPtr.Zero || GetConsoleProcessList(new uint[2], 2) != 1) return false;

        // 资源管理器为控制台程序临时创建的窗口只属于当前进程；隐藏后显示持久的安装指引。
        ShowWindow(console, HideWindow);
        MessageBox(IntPtr.Zero,
            "这是 VideoEnhancer 的运行程序，不含插件安装文件。\n\n" +
            "安装或覆盖安装 3FUI 插件，请运行 VideoEnhancerInstaller.exe。\n" +
            "命令行用法请在终端执行 videoenhancer.exe --help。",
            "VideoEnhancer 运行程序", 0x40);
        return true;
    }

    internal static int Create(string[] args)
    {
        if (args.Length != 4) return Fail("--create-installer-bundle 需要运行 EXE、输出 EXE 和载荷目录");
        InstallerBundle.Create(args[1], args[2], args[3]);
        using var bundle = InstallerBundle.Open(args[2]);
        ValidatePayload(bundle.PayloadPaths);
        Console.WriteLine("INSTALLER_BUNDLE_COMPLETE|" + Path.GetFullPath(args[2]));
        return 0;
    }

    internal static int InstallCommand(string[] args)
    {
        if (args.Length < 2 || args.Length > 4 ||
            (args.Length > 2 && !args.Skip(2).All(arg => arg is "--quiet" or "--skip-legacy-cleanup")))
            return Fail("请先点击“选择目录”，选择包含 FFmpegFreeUI.exe 的 3FUI 根目录。");
        try
        {
            Install(args[1], args.Contains("--skip-legacy-cleanup"));
            return 0;
        }
        catch (UnauthorizedAccessException)
        {
            return Fail("无法写入所选 3FUI 目录。请确认目录可写，或以管理员身份运行安装程序。");
        }
        catch (Exception ex)
        {
            return Fail(ex.Message);
        }
    }

    internal static int RunInteractive()
    {
        var installationStarted = false;
        try
        {
            Console.WriteLine("VideoEnhancer 插件安装程序");
            Console.WriteLine("按下y并enter执行安装，按其他任意键并enter退出安装。");
            Console.Write("> ");
            if (!ReadYes()) return 0;
            installationStarted = true;
            Console.WriteLine("请选择正确的ffmpegfreeui.exe路径（可执行文件名称不限）。");
            var executable = ChooseHostExecutable();
            if (string.IsNullOrWhiteSpace(executable)) return 0;
            var hostRoot = Path.GetDirectoryName(executable)!;
            Console.WriteLine(Install(hostRoot, skipLegacyCleanup: false, selectedHost: executable));
            var applicationRoot = Path.Combine(hostRoot, "Plugin", "videoenhancer");
            Console.Write($"程序即将在\"{applicationRoot}\"中自动创建核心目录（models、python 和 bin），是否继续？选择\"是(Y)\"：");
            if (ReadYes())
                foreach (var directory in new[] { "models", "python", "bin" })
                    Directory.CreateDirectory(Path.Combine(applicationRoot, directory));
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("安装失败：" + ex.Message);
            return 1;
        }
        finally
        {
            if (installationStarted)
            {
                Console.WriteLine("按 Enter 键关闭此窗口。");
                Console.ReadLine();
            }
        }
    }

    private static bool ReadYes() => Console.ReadLine()?.Trim() is { } answer
        && (answer.Equals("Y", StringComparison.OrdinalIgnoreCase) || answer == "是");

    private static string? ChooseHostExecutable()
    {
        var configuredHost = Environment.GetEnvironmentVariable("VIDEOENHANCER_INSTALL_HOST")?.Trim().Trim('"');
        if (!string.IsNullOrWhiteSpace(configuredHost))
        {
            var fullPath = Path.GetFullPath(configuredHost);
            if (!File.Exists(fullPath)) throw new FileNotFoundException("所选主程序不存在", fullPath);
            return fullPath;
        }
        const int capacity = 32768;
        var file = Marshal.AllocHGlobal(capacity * sizeof(char));
        var filter = Marshal.StringToHGlobalUni("可执行程序 (*.exe)\0*.exe\0所有文件 (*.*)\0*.*\0\0");
        var title = Marshal.StringToHGlobalUni("请选择正确的ffmpegfreeui.exe路径（文件名不限）");
        try
        {
            Marshal.WriteInt16(file, 0);
            var dialog = new OpenFileName
            {
                Size = Marshal.SizeOf<OpenFileName>(),
                Filter = filter,
                File = file,
                MaxFile = capacity,
                Title = title,
                Flags = FileDialogFlags
            };
            if (GetOpenFileName(ref dialog)) return Marshal.PtrToStringUni(file);
            var error = CommDlgExtendedError();
            if (error != 0) throw new InvalidOperationException($"无法打开文件选择窗口（0x{error:X8}）");
            return null;
        }
        finally
        {
            Marshal.FreeHGlobal(file);
            Marshal.FreeHGlobal(filter);
            Marshal.FreeHGlobal(title);
        }
    }

    private static string ValidateRoot(string selectedRoot)
    {
        if (string.IsNullOrWhiteSpace(selectedRoot))
            throw new InvalidDataException("请先点击“选择目录”，选择包含 FFmpegFreeUI.exe 的 3FUI 根目录。");
        var root = Path.GetFullPath(selectedRoot.Trim().Trim('"'));
        if (!File.Exists(Path.Combine(root, "FFmpegFreeUI.exe")))
            throw new InvalidDataException("所选目录不是 3FUI 根目录：" + root + "\n请重新选择包含 FFmpegFreeUI.exe 的目录，不要选择 Plugin 子目录。");
        return root;
    }

    private static string Install(string selectedRoot, bool skipLegacyCleanup, string? selectedHost = null)
    {
        // 交互入口使用用户实际选择的 EXE，允许重命名；目录命令入口仍检查标准主程序。
        var root = selectedHost is null ? ValidateRoot(selectedRoot) : Path.GetFullPath(selectedRoot);
        var pluginRoot = Path.Combine(root, "Plugin");
        Directory.CreateDirectory(pluginRoot);
        var transactionRoot = Path.Combine(pluginRoot, ".videoenhancer-install-" + Guid.NewGuid().ToString("N"));
        var stage = Path.Combine(transactionRoot, "stage");
        var backup = Path.Combine(transactionRoot, "backup");
        var installed = new List<(string Target, string? Backup)>();
        try
        {
            Directory.CreateDirectory(stage);
            using (var bundle = InstallerBundle.Open(Environment.ProcessPath!))
            {
                bundle.ExtractRuntime(Path.Combine(stage, "plugin", "videoenhancer", "videoenhancer.exe"));
                bundle.ExtractPayload(stage);
            }
            var stagedDll = Path.Combine(stage, "plugin", "videoenhancer.3fui.dll");
            Directory.CreateDirectory(Path.GetDirectoryName(stagedDll)!);
            using (var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream(PluginResource)
                ?? throw new InvalidDataException("安装器缺少内嵌插件 DLL"))
            using (var output = File.Create(stagedDll)) resource.CopyTo(output);

            foreach (var source in Directory.EnumerateFiles(Path.Combine(stage, "plugin"), "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(stage, source);
                var target = Path.GetFullPath(Path.Combine(root, relative));
                if (!target.StartsWith(pluginRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("安装包路径超出 Plugin 目录");
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                string? old = null;
                if (File.Exists(target))
                {
                    old = Path.Combine(backup, relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(old)!);
                    File.Copy(target, old);
                }
                File.Move(source, target, true);
                installed.Add((target, old));
                if (int.TryParse(Environment.GetEnvironmentVariable("VIDEOENHANCER_INSTALL_FAIL_AFTER"), out var failAfter) &&
                    installed.Count == failAfter) throw new IOException("测试注入：安装中断");
            }
        }
        catch
        {
            // 回滚本次写入的文件，保留用户自己的模型与配置。
            foreach (var file in installed.AsEnumerable().Reverse())
            {
                if (file.Backup is null) File.Delete(file.Target);
                else File.Move(file.Backup, file.Target, true);
            }
            throw;
        }
        finally
        {
            for (var attempt = 0; Directory.Exists(transactionRoot); attempt++)
            {
                try
                {
                    Directory.Delete(transactionRoot, true);
                }
                catch (IOException) when (attempt < 9)
                {
                    // Windows 可能短暂占用刚释放的 PE 文件或目录句柄；重试清理事务目录。
                    Thread.Sleep(100);
                }
            }
        }

        if (skipLegacyCleanup) return "插件已安装到：" + pluginRoot;
        var runtime = Path.Combine(pluginRoot, "videoenhancer", "videoenhancer.exe");
        var start = new ProcessStartInfo(runtime)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        start.ArgumentList.Add("--cleanup-legacy-residue");
        start.ArgumentList.Add("--plugin-root");
        start.ArgumentList.Add(pluginRoot);
        using var process = Process.Start(start) ?? throw new IOException("无法检查旧版插件文件");
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return process.ExitCode == 0
            ? "插件已安装到：" + pluginRoot
            : "插件文件已安装到：" + pluginRoot + "\n旧版文件检查未完成：" + stdout + stderr;
    }

    private static void ValidatePayload(IEnumerable<string> paths)
    {
        var entries = paths.ToArray();
        if (entries.Length == 0 || entries.Any(path =>
            !path.StartsWith("plugin/videoenhancer/", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException("安装器载荷必须位于 plugin/videoenhancer 下");
    }

    private static int Fail(string message)
    {
        Console.Error.WriteLine("[错误] " + message);
        return 1;
    }

}
