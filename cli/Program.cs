using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace VideoEnhancer;

/// <summary>
/// videoenhancer.exe — rve-backend 的命令行中转器。
/// 简化参数：-i / -modelpath / -ffmpeg-settings；输出路径位于 ffmpeg-settings 末尾（无 -o）。
/// </summary>
internal static class Program
{
    private const string ToolVersion = "1.4.0";

    // exe 所在目录：videoenhancer.ini 的查找位置，也是未配置 core-path 时的回退根目录（1.0 布局）
    private static readonly string AppRoot = AppContext.BaseDirectory.TrimEnd(
        Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    // 核心程序根目录：默认 exe 同目录；若 videoenhancer.ini 配置了 core-path="<核心程序路径>"，
    // 则指向后端分离后的根目录（bin\ffmpeg / python / models 所在处）。
    private static string CoreRoot = AppRoot;

    private static string PythonExe => Path.Combine(CoreRoot, "python", "python", "python.exe");
    private static string BackendScript => Path.Combine(CoreRoot, "python", "backend", "rve-backend.py");
    private static string FfmpegExe => Path.Combine(CoreRoot, "bin", "ffmpeg", "ffmpeg.exe");
    private static string ModelsDir => Path.Combine(CoreRoot, "models");
    private static string SceneDetectModel => Path.Combine(ModelsDir, "EfficientNet-SceneDetect");
    private static string DefaultModel => Path.Combine(ModelsDir, "RealESRGAN-AnimeVideoV3-2x");
    private static string PythonSitePackages => Path.Combine(CoreRoot, "python", "python", "Lib", "site-packages");

    // ── Windows Job Object：CLI 进程被 3fui 停止/退出时，整棵后端进程树（python + ffmpeg）一并终止 ──

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, string? lpName);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetInformationJobObject(IntPtr hJob, int JobObjectInfoClass, IntPtr lpJobObjectInfo, uint cbJobObjectInfoLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr hObject);

    private const int JobObjectExtendedLimitInformation = 9;
    private const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x00002000;

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public UIntPtr MinimumWorkingSetSize;
        public UIntPtr MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public UIntPtr Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IO_COUNTERS
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
    {
        public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
        public IO_COUNTERS IoInfo;
        public UIntPtr ProcessMemoryLimit;
        public UIntPtr JobMemoryLimit;
        public UIntPtr PeakProcessMemoryUsed;
        public UIntPtr PeakJobMemoryUsed;
    }

    /// <summary>创建"最后一个句柄关闭即终止作业内进程"的作业对象；失败返回 IntPtr.Zero。</summary>
    private static IntPtr CreateKillOnCloseJob()
    {
        try
        {
            var job = CreateJobObject(IntPtr.Zero, null);
            if (job == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            var info = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION();
            info.BasicLimitInformation.LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE;
            var size = Marshal.SizeOf<JOBOBJECT_EXTENDED_LIMIT_INFORMATION>();
            var ptr = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(info, ptr, false);
                if (!SetInformationJobObject(job, JobObjectExtendedLimitInformation, ptr, (uint)size))
                {
                    CloseHandle(job);
                    return IntPtr.Zero;
                }
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
            return job;
        }
        catch
        {
            return IntPtr.Zero;
        }
    }

    /// <summary>进度行节流：rve-backend 每秒输出大量 "FPS:…" 行，逐行转发会让 3fui 队列整行重绘闪烁。</summary>
    private sealed class ProgressThrottle
    {
        private readonly object _sync = new();
        private DateTime _lastForward = DateTime.MinValue;

        public bool ShouldForward(string line)
        {
            if (line.IndexOf("Current Frame:", StringComparison.OrdinalIgnoreCase) < 0
                || line.IndexOf("FPS:", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return true;
            }

            lock (_sync)
            {
                var now = DateTime.UtcNow;
                if ((now - _lastForward).TotalSeconds < 1.0)
                {
                    return false;
                }
                _lastForward = now;
                return true;
            }
        }
    }

    private sealed class Options
    {
        public bool ShowHelp;
        public bool ListModels;
        public bool CheckOnly;
        public bool DebugSplit;
        public bool Json;
        public string Input = "";
        public bool HasInput;
        public string Model = "";
        public bool HasModel;
        public string FfmpegSettings = "";
        public bool HasFfmpegSettings;
        public string ScaleOverride = "";
        public bool HasScaleOverride;
        public string PauseShm = "";
        public bool HasPauseShm;
        public string StopShm = "";
        public bool HasStopShm;
        public string InterpModel = "";
        public bool HasInterpModel;
        public string InterpFactor = "";
        public bool HasInterpFactor;
        public bool NoUpscale;
        public bool ListInterpModels;
    }

    private static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;
        try
        {
            return Run(args);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine("[错误] " + ex.Message);
            return 1;
        }
    }

    /// <summary>
    /// 读取 exe 同目录的 videoenhancer.ini（第一行 core-path="&lt;核心程序路径&gt;"）。
    /// 返回 null 表示成功；返回字符串为错误信息（找不到对应的库），调用方直接报错退出。
    /// 未找到配置文件时回退到 exe 同目录布局（1.0 兼容）。
    /// </summary>
    private static string? LoadCorePathConfig()
    {
        var iniPath = Path.Combine(AppRoot, "videoenhancer.ini");
        if (!File.Exists(iniPath))
        {
            return null;
        }

        string? corePath = null;
        try
        {
            foreach (var rawLine in File.ReadAllLines(iniPath))
            {
                var line = rawLine.Trim();
                if (line.Length == 0 || line[0] == ';' || line[0] == '#')
                {
                    continue;
                }
                var eq = line.IndexOf('=');
                if (eq <= 0)
                {
                    continue;
                }
                if (!line[..eq].Trim().Equals("core-path", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                var value = line[(eq + 1)..].Trim();
                if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
                {
                    value = value[1..^1].Trim();
                }
                corePath = value;
                break;
            }
        }
        catch (Exception ex)
        {
            return "找不到对应的库：无法读取配置文件 " + iniPath + "（" + ex.Message + "）";
        }

        if (string.IsNullOrWhiteSpace(corePath))
        {
            return "找不到对应的库：videoenhancer.ini 未配置 core-path"
                + "（第 1 行应为 core-path=\"<核心程序路径>\"）";
        }

        var resolved = Path.IsPathRooted(corePath) ? corePath : Path.Combine(AppRoot, corePath);
        resolved = Path.GetFullPath(resolved);
        if (!Directory.Exists(resolved))
        {
            return "找不到对应的库：core-path 指向的目录不存在：" + resolved;
        }

        CoreRoot = resolved.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return null;
    }

    private static int Run(string[] args)
    {
        if (args.Length == 0)
        {
            PrintHelp(Console.Out);
            return 2;
        }

        var o = ParseArgs(args);

        if (o.ShowHelp)
        {
            PrintHelp(Console.Out);
            return 0;
        }

        // 读取 videoenhancer.ini（第一行 core-path="<核心程序路径>"）确定核心程序根目录
        var configError = LoadCorePathConfig();
        if (configError is not null)
        {
            return Fail(configError, 1);
        }

        if (o.ListModels)
        {
            return ListModels(o.Json);
        }

        if (o.ListInterpModels)
        {
            return ListInterpModels(o.Json);
        }

        if (o.CheckOnly)
        {
            return RunCheck(verbose: true) ? 0 : 1;
        }

        if (o.DebugSplit)
        {
            if (!o.HasFfmpegSettings)
            {
                return Fail("--debug-split 需要 -ffmpeg-settings 参数");
            }
            var (customDebug, outputDebug, overwriteDebug) = SplitFfmpegSettings(o.FfmpegSettings);
            Console.WriteLine("custom_encoder: " + customDebug);
            Console.WriteLine("output: " + outputDebug);
            Console.WriteLine("overwrite: " + overwriteDebug);
            return 0;
        }

        if (!o.HasInput)
        {
            return Fail("缺少必需参数：-i <输入视频路径>");
        }

        if (!o.HasFfmpegSettings)
        {
            return Fail("缺少必需参数：-ffmpeg-settings \"<FFmpeg 编码参数 + 输出路径>\"");
        }
        // 停止共享内存在此处就创建并持有（进程结束自动释放），插件点击“停止”时按名打开写入 1 即可触发
        var stopWatcher = o.HasStopShm ? new StopWatcher(o.StopShm) : null;

        // 1. 环境检测（ffmpeg / python 库 / 模型库）
        if (!RunCheck(verbose: false))
        {
            return 1;
        }

        // 2. 输入视频
        var input = Path.GetFullPath(o.Input);
        if (!File.Exists(input))
        {
            return Fail("输入视频不存在：" + input);
        }

        // 3. 放大模型（-no-upscale 时跳过，用于"仅补帧"模式）
        var useUpscale = !o.NoUpscale;
        var model = "";
        if (useUpscale)
        {
            model = ResolveModel(o.Model);
            if (model.Length == 0)
            {
                return 1;
            }
        }

        // 3.5 补帧模型（RIFE）：与放大可同时使用（先补帧后放大）
        string? interpModel = null;
        if (o.HasInterpModel)
        {
            interpModel = ResolveInterpModel(o.InterpModel);
            if (interpModel.Length == 0)
            {
                return 1;
            }
        }
        if (!useUpscale && interpModel is null)
        {
            return Fail("-no-upscale 已指定但未提供 -interp-model（仅补帧模式需要补帧模型）");
        }
        if (interpModel is null && o.NoUpscale)
        {
            return Fail("需要至少一个模型：-no-upscale 时请提供 -interp-model");
        }

        // 4. 倍率：优先用户指定，其次从模型名自动识别（与 GUI 一致）
        string? scale = null;
        if (useUpscale)
        {
            if (o.HasScaleOverride)
            {
                if (!int.TryParse(o.ScaleOverride, out var s) || s < 1)
                {
                    return Fail("-scale 必须是大于 0 的整数，当前值：" + o.ScaleOverride);
                }
                scale = s.ToString();
            }
            else
            {
                scale = DetectScale(model);
            }
        }

        // 4.5 补帧倍率（默认 2；rve-backend 要求大于 1）
        string? interpFactor = null;
        if (interpModel is not null)
        {
            interpFactor = o.HasInterpFactor ? o.InterpFactor : "2";
            if (!double.TryParse(interpFactor, out var f) || f <= 1.0)
            {
                return Fail("-interp-factor 必须是大于 1 的数字，当前值：" + interpFactor);
            }
        }

        // 5. 拆分 ffmpeg-settings：最后一项为输出路径，其余为编码参数
        string customEncoder;
        string outputFile;
        bool overwrite;
        try
        {
            (customEncoder, outputFile, overwrite) = SplitFfmpegSettings(o.FfmpegSettings);
        }
        catch (ArgumentException ex)
        {
            return Fail(ex.Message);
        }

        outputFile = Path.GetFullPath(outputFile);

        // 5.5 超大输出分辨率预警（ncnn 帧队列在高分辨率下容易内存不足）
        if (scale != null && int.TryParse(scale, out var scaleNum) && scaleNum >= 2)
        {
            var (srcW, srcH) = GetInputResolution(input);
            if (srcW > 0 && srcH > 0)
            {
                var outW = (long)srcW * scaleNum;
                var outH = (long)srcH * scaleNum;
                if (outW * outH >= 7680L * 4320L)
                {
                    Console.Error.WriteLine("[警告] 输出分辨率约 " + outW + "x" + outH +
                        "（8K 级）。rve-backend 的帧队列可能内存不足，若失败请改用较低倍率模型或对视频分段处理。");
                }
            }
        }

        // 6. 构建并启动 rve-backend
        var backendArgs = BuildBackendArgs(input, outputFile, model, customEncoder, overwrite, scale, o.PauseShm, interpModel, interpFactor);
        return LaunchBackend(backendArgs, input, model, outputFile, customEncoder, stopWatcher, interpModel, interpFactor);
    }

    private static Options ParseArgs(string[] args)
    {
        var o = new Options();
        for (var i = 0; i < args.Length; i++)
        {
            var (name, inlineValue) = SplitOption(args[i]);
            switch (name)
            {
                case "-h":
                case "--help":
                    o.ShowHelp = true;
                    break;
                case "--list-models":
                case "--search-models":
                    o.ListModels = true;
                    break;
                case "--json":
                    o.Json = true;
                    break;
                case "--check":
                    o.CheckOnly = true;
                    break;
                case "--debug-split":
                    o.DebugSplit = true;
                    break;
                case "-i":
                case "--input":
                    o.Input = TakeValue(args, ref i, name, inlineValue);
                    o.HasInput = true;
                    break;
                case "-modelpath":
                case "--modelpath":
                case "--model":
                    o.Model = TakeValue(args, ref i, name, inlineValue);
                    o.HasModel = true;
                    break;
                case "-ffmpeg-settings":
                case "--ffmpeg-settings":
                    o.FfmpegSettings = TakeValue(args, ref i, name, inlineValue);
                    o.HasFfmpegSettings = true;
                    break;
                case "-scale":
                case "--scale":
                    o.ScaleOverride = TakeValue(args, ref i, name, inlineValue);
                    o.HasScaleOverride = true;
                    break;
                case "-pause-shm":
                case "--pause-shm":
                    o.PauseShm = TakeValue(args, ref i, name, inlineValue);
                    o.HasPauseShm = true;
                    break;
                case "-stop-shm":
                case "--stop-shm":
                    o.StopShm = TakeValue(args, ref i, name, inlineValue);
                    o.HasStopShm = true;
                    break;
                case "-interp-model":
                case "--interp-model":
                case "--interp-modelpath":
                    o.InterpModel = TakeValue(args, ref i, name, inlineValue);
                    o.HasInterpModel = true;
                    break;
                case "-interp-factor":
                case "--interp-factor":
                    o.InterpFactor = TakeValue(args, ref i, name, inlineValue);
                    o.HasInterpFactor = true;
                    break;
                case "-no-upscale":
                case "--no-upscale":
                    o.NoUpscale = true;
                    break;
                case "--list-interp-models":
                case "--search-interp-models":
                    o.ListInterpModels = true;
                    break;
                default:
                    throw new ArgumentException("未知参数：" + args[i] + "（使用 -h 查看帮助）");
            }
        }
        return o;
    }

    private static string TakeValue(string[] args, ref int i, string name, string? inlineValue)
    {
        if (inlineValue is not null)
        {
            return inlineValue;
        }
        if (i + 1 >= args.Length)
        {
            throw new ArgumentException("参数 " + name + " 缺少值");
        }
        return args[++i];
    }

    private static (string Name, string? Value) SplitOption(string arg)
    {
        var eq = arg.IndexOf('=');
        if (eq > 1 && arg.StartsWith('-'))
        {
            return (arg[..eq], arg[(eq + 1)..]);
        }
        return (arg, null);
    }

    /// <summary>解析模型路径：完整路径 / models 下相对路径 / 模型文件夹名；省略时用默认模型。</summary>
    private static string ResolveModel(string requested)
    {
        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(requested))
        {
            var raw = requested.Trim().Trim('"');
            candidates.Add(Path.GetFullPath(raw));
            if (!Path.IsPathRooted(raw))
            {
                candidates.Add(Path.Combine(ModelsDir, raw));
                candidates.Add(Path.Combine(ModelsDir, Path.GetFileName(raw)));
            }
        }
        else
        {
            candidates.Add(DefaultModel);
        }

        foreach (var c in candidates)
        {
            if (Directory.Exists(c) && IsNcnnModelFolder(c))
            {
                return c;
            }
        }

        Console.Error.WriteLine("[错误] 未找到可用模型：" + (string.IsNullOrWhiteSpace(requested) ? DefaultModel : requested));
        Console.Error.WriteLine("[提示] 可用模型（models 目录）：");
        foreach (var m in DiscoverModelFolders())
        {
            Console.Error.WriteLine("       " + Path.GetFileName(m));
        }
        Console.Error.WriteLine("[提示] 用法：-modelpath <模型名或路径>，例如 -modelpath RealESRGAN-AnimeVideoV3-2x");
        return "";
    }

    /// <summary>从模型文件夹名解析放大倍率（RealESRGAN-AnimeVideoV3-2x → 2）。</summary>
    private static string? DetectScale(string modelFolder)
    {
        var name = Path.GetFileName(modelFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var match = Regex.Match(name, @"-(\d)x", RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            match = Regex.Match(name, @"x(\d)", RegexOptions.IgnoreCase);
        }
        return match.Success ? match.Groups[1].Value : null;
    }

    /// <summary>
    /// 把 -ffmpeg-settings 拆分为“编码参数”与“输出文件”。
    /// 约定：输出文件路径是最后一个非选项参数；末尾的 -y 表示允许覆盖。
    /// </summary>
    private static (string CustomEncoder, string OutputFile, bool Overwrite) SplitFfmpegSettings(string settings)
    {
        var tokens = Tokenize(settings);
        if (tokens.Count == 0)
        {
            throw new ArgumentException("-ffmpeg-settings 为空，请提供编码参数与输出路径");
        }

        var overwrite = false;
        while (tokens.Count > 0 && tokens[^1].Equals("-y", StringComparison.OrdinalIgnoreCase))
        {
            overwrite = true;
            tokens.RemoveAt(tokens.Count - 1);
        }

        if (tokens.Count == 0)
        {
            throw new ArgumentException("-ffmpeg-settings 中缺少输出文件路径");
        }

        var output = tokens[^1];
        if (output.StartsWith('-'))
        {
            throw new ArgumentException(
                "输出文件路径必须是 -ffmpeg-settings 的最后一个参数，当前末项以 \"-\" 开头：" + output);
        }

        if (!output.Contains('\\') && !output.Contains('/') && Path.GetExtension(output).Length == 0)
        {
            throw new ArgumentException(
                "输出文件路径应为带扩展名或包含目录的路径（如 \"out.mp4\"），当前末项不像文件路径：" + output);
        }

        tokens.RemoveAt(tokens.Count - 1);

        // rve-backend 写进程自带输入映射（0=原始帧管道，1=源文件）：
        //   -map 0:v -map 1:a? -map 1:s?
        // 3fui 模板里的 -map 流映射会与自带映射冲突（例如双份视频流导致输出失败），
        // 这里统一剥除；-map_metadata / -map_chapters 的输入索引 0（3fui 中的源文件）
        // 改写为 1（rve-backend 写进程中的源文件）。
        var cleaned = new List<string>();
        for (var k = 0; k < tokens.Count; k++)
        {
            var tok = tokens[k];
            if (tok.Equals("-map", StringComparison.OrdinalIgnoreCase))
            {
                k++; // 跳过映射目标（含 -map 0:t? 附件映射）
                continue;
            }
            if (k + 1 < tokens.Count &&
                (tok.Equals("-map_metadata", StringComparison.OrdinalIgnoreCase) ||
                 tok.Equals("-map_chapters", StringComparison.OrdinalIgnoreCase)) &&
                tokens[k + 1].StartsWith('0'))
            {
                var target = tokens[k + 1];
                cleaned.Add(tok);
                cleaned.Add(target.Length > 1 ? "1" + target.Substring(1) : "1");
                k++;
                continue;
            }
            cleaned.Add(tok);
        }
        var custom = string.Join(" ", cleaned);

        foreach (var t in cleaned)
        {
            if (t.Contains(' '))
            {
                Console.Error.WriteLine(
                    "[警告] 参数 \"" + t + "\" 含空格；rve-backend 按空白拆分编码参数，可能导致该参数失效");
            }
        }

        if (custom.Length == 0)
        {
            throw new ArgumentException(
                "-ffmpeg-settings 除输出路径外还需包含编码参数，例如：-c:v libx264 -crf 18 \"输出.mkv\"");
        }

        return (custom, output, overwrite);
    }

    /// <summary>Windows 风格按空白拆分，双引号包裹的空格保留在令牌内（支持 "" 转义）。</summary>
    private static List<string> Tokenize(string line)
    {
        var tokens = new List<string>();
        var sb = new StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    sb.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (char.IsWhiteSpace(c) && !inQuotes)
            {
                if (sb.Length > 0)
                {
                    tokens.Add(sb.ToString());
                    sb.Clear();
                }
            }
            else
            {
                sb.Append(c);
            }
        }
        if (sb.Length > 0)
        {
            tokens.Add(sb.ToString());
        }
        return tokens;
    }

    /// <summary>构建 rve-backend.py 的命令行参数，逻辑与 GUI 的 RvePaths.BuildBackendArgs 一致。</summary>
    private static List<string> BuildBackendArgs(
        string input, string outputFile, string modelFolder, string customEncoder, bool overwrite, string? scale, string pauseShm,
        string? interpModel, string? interpFactor)
    {
        var args = new List<string>
        {
            BackendScript,
            "-i", input,
            "-o", outputFile,
            "-b", "ncnn",
            "--precision", "auto",
            "--custom_encoder", " " + customEncoder + " ",
            "--tensorrt_opt_profile", "3",
            "--ncnn_gpu_id", "0",
            "--pytorch_gpu_id", "0",
            "--cwd", CoreRoot,
            "--ffmpeg_path", FfmpegExe,
        };

        if (!string.IsNullOrEmpty(modelFolder))
        {
            args.Add("--upscale_model");
            args.Add(modelFolder);
        }

        if (interpModel is not null)
        {
            args.Add("--interpolate_model");
            args.Add(interpModel);
            args.Add("--interpolate_factor");
            args.Add(interpFactor ?? "2");
        }

        if (!string.IsNullOrEmpty(scale))
        {
            args.Add("--override_upscale_scale");
            args.Add(scale);
        }

        args.Add("--scene_detect_model");
        args.Add(SceneDetectModel);
        args.Add("--scene_detect_method");
        args.Add("sudo_scene_detect");
        args.Add("--scene_detect_threshold");
        args.Add("3.5");

        if (overwrite)
        {
            args.Add("--overwrite");
        }

        if (!string.IsNullOrWhiteSpace(pauseShm))
        {
            args.Add("--pause_shared_memory_id");
            args.Add(pauseShm);
        }
        return args;
    }

    /// <summary>解析补帧模型路径：完整路径 / models\RIFE 下相对路径 / RIFE 子文件夹名；返回空串表示失败。</summary>
    private static string ResolveInterpModel(string requested)
    {
        var rifeDir = Path.Combine(ModelsDir, "RIFE");
        var raw = requested.Trim().Trim('"');
        var candidates = new List<string>();
        candidates.Add(Path.GetFullPath(raw));
        if (!Path.IsPathRooted(raw))
        {
            candidates.Add(Path.Combine(rifeDir, raw));
            candidates.Add(Path.Combine(rifeDir, Path.GetFileName(raw)));
        }

        foreach (var candidate in candidates)
        {
            if (Directory.Exists(candidate) && IsNcnnModelFolder(candidate))
            {
                return candidate;
            }
        }

        Console.Error.WriteLine("[错误] 未找到可用补帧模型：" + raw);
        Console.Error.WriteLine(@"[提示] 可用补帧模型（models\RIFE 目录）：");
        foreach (var m in DiscoverInterpModelFolders())
        {
            Console.Error.WriteLine("       " + Path.GetFileName(m));
        }
        Console.Error.WriteLine("[提示] 用法：-interp-model <模型名或路径>，例如 -interp-model rife-v4.25");
        return "";
    }

    /// <summary>发现 models\RIFE 下的补帧模型子文件夹（含 .param/.bin）。</summary>
    private static List<string> DiscoverInterpModelFolders()
    {
        var rifeDir = Path.Combine(ModelsDir, "RIFE");
        if (!Directory.Exists(rifeDir))
        {
            return new List<string>();
        }
        return Directory.GetDirectories(rifeDir)
            .Where(IsNcnnModelFolder)
            .OrderBy(p => p, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static int ListInterpModels(bool json)
    {
        var models = DiscoverInterpModelFolders();
        if (json)
        {
            var names = models.Select(m => Path.GetFileName(m)).ToList();
            Console.WriteLine("[" + string.Join(",", names.Select(n => "\"" + n + "\"")) + "]");
            return 0;
        }
        Console.WriteLine(@"可用补帧模型（models\RIFE 目录）：");
        if (models.Count == 0)
        {
            Console.WriteLine("  (未找到任何含 .param/.bin 的补帧模型文件夹)");
            return 0;
        }
        foreach (var m in models)
        {
            Console.WriteLine("  " + Path.GetFileName(m));
        }
        return 0;
    }

    private static readonly Regex OomHintRegex = new(
        @"MemoryError|Could not allocate bytes object|Out of memory|Cannot allocate|Unable to allocate",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>清洗后端行：丢弃空行/纯空白行（rve-backend 用 \r 清屏产生的伪行），去除行尾空白。</summary>
    private static string? SanitizeLine(string? line)
    {
        if (line is null)
        {
            return null;
        }
        var trimmed = line.TrimEnd();
        return trimmed.Length == 0 ? null : trimmed;
    }

    /// <summary>读取共享内存暂停/停止字节；返回 null 表示共享内存尚未创建。</summary>
    private static byte? ReadShmByte(string shmBase)
    {
        if (string.IsNullOrWhiteSpace(shmBase))
        {
            return null;
        }
        foreach (var name in new[] { "/" + shmBase, shmBase })
        {
            try
            {
                using var mmf = MemoryMappedFile.OpenExisting(name, MemoryMappedFileRights.ReadWrite);
                using var acc = mmf.CreateViewAccessor(0, 1);
                acc.Read(0, out byte b);
                return b;
            }
            catch
            {
                // 尝试下一个候选名
            }
        }
        return null;
    }

    /// <summary>
    /// 等待 -stop-shm 字节变为 1（插件点击“停止”时写入）。
    /// 启动时创建并持有共享内存（初始化为 0），插件只需按名打开写入 1。
    /// </summary>
    private sealed class StopWatcher : IDisposable
    {
        private readonly string _shmBase;
        private readonly MemoryMappedFile? _owned;
        private bool _stopRequested;

        public StopWatcher(string shmBase)
        {
            _shmBase = shmBase;
            _owned = CreateMapping(shmBase);
        }

        /// <summary>创建（若已存在则打开）停止共享内存并清零，句柄保持到进程结束。</summary>
        private static MemoryMappedFile? CreateMapping(string shmBase)
        {
            foreach (var name in new[] { shmBase, "/" + shmBase })
            {
                try
                {
                    var mmf = MemoryMappedFile.CreateOrOpen(name, 1, MemoryMappedFileAccess.ReadWrite);
                    using (var acc = mmf.CreateViewAccessor(0, 1))
                    {
                        acc.Read(0, out byte current);
                        if (current != 0)
                        {
                            acc.Write(0, (byte)0);
                        }
                    }
                    return mmf;
                }
                catch
                {
                    // 尝试下一个候选名
                }
            }
            return null;
        }

        public bool IsStopRequested()
        {
            if (_stopRequested)
            {
                return true;
            }
            var b = ReadShmByte(_shmBase);
            if (b == 1)
            {
                _stopRequested = true;
            }
            return _stopRequested;
        }

        public void Dispose() => _owned?.Dispose();
    }

    [DllImport("kernel32.dll")]
    private static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);
    [DllImport("kernel32.dll")]
    private static extern bool Process32First(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);
    [DllImport("kernel32.dll")]
    private static extern bool Process32Next(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

    private const uint TH32CS_SNAPPROCESS = 0x00000002;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct PROCESSENTRY32
    {
        public uint dwSize;
        public uint cntUsage;
        public uint th32ProcessID;
        public IntPtr th32DefaultHeapID;
        public uint th32ModuleID;
        public uint cntThreads;
        public uint th32ParentProcessID;
        public int pcPriClassBase;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szExeFile;
    }

    /// <summary>枚举指定进程的 ffmpeg.exe 子进程（后端渲染管道）。</summary>
    private static List<int> GetFfmpegChildPids(int parentPid)
    {
        var result = new List<int>();
        var snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
        if (snapshot == IntPtr.Zero || snapshot == new IntPtr(-1))
        {
            return result;
        }
        try
        {
            var entry = new PROCESSENTRY32 { dwSize = (uint)Marshal.SizeOf<PROCESSENTRY32>() };
            if (Process32First(snapshot, ref entry))
            {
                do
                {
                    if (entry.th32ParentProcessID == parentPid &&
                        string.Equals(entry.szExeFile, "ffmpeg.exe", StringComparison.OrdinalIgnoreCase))
                    {
                        result.Add((int)entry.th32ProcessID);
                    }
                } while (Process32Next(snapshot, ref entry));
            }
        }
        finally
        {
            CloseHandle(snapshot);
        }
        return result;
    }

    private static bool IsProcessAlive(int pid)
    {
        try
        {
            using var p = Process.GetProcessById(pid);
            return !p.HasExited;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 优雅停止：只终止后端 python 进程，让 ffmpeg 写进程在 stdin 收到 EOF 后
    /// 自行刷新编码器并完成封装（已处理部分正常写入磁盘），再清理残留的 ffmpeg 子进程。
    /// </summary>
    private static int GracefulStop(Process process, List<int> ffmpegPids, string outputFile)
    {
        Console.WriteLine();
        Console.WriteLine("[信息] 正在停止：保留已处理的部分视频…");
        try
        {
            if (!process.HasExited)
            {
                process.Kill(); // 只杀 python；ffmpeg 写进程 stdin EOF 后自动收尾写盘
            }
        }
        catch
        {
            // 进程可能已退出
        }

        // 等待 ffmpeg 子进程收尾（写进程封装输出，读进程因管道断开自行退出）
        var deadline = DateTime.UtcNow.AddSeconds(25);
        var remaining = new List<int>(ffmpegPids);
        while (DateTime.UtcNow < deadline && remaining.Count > 0)
        {
            remaining.RemoveAll(pid => !IsProcessAlive(pid));
            if (remaining.Count == 0)
            {
                break;
            }
            Thread.Sleep(250);
        }

        foreach (var pid in remaining)
        {
            try
            {
                using var p = Process.GetProcessById(pid);
                p.Kill();
            }
            catch
            {
                // 进程已退出
            }
        }

        try
        {
            if (!process.HasExited)
            {
                process.WaitForExit(5000);
            }
        }
        catch
        {
            // 忽略
        }

        Console.WriteLine("[信息] 已停止；已处理部分已写入输出文件：" + outputFile);
        Console.WriteLine("[信息] 提示：输出视频的时长可能短于原视频（停止点之后没有画面）。");
        return 130;
    }

    /// <summary>用 ffmpeg -i 探测输入视频分辨率（失败返回 0x0）。</summary>
    private static (int W, int H) GetInputResolution(string input)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = FfmpegExe,
                Arguments = "-hide_banner -i \"" + input + "\"",
                UseShellExecute = false,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            using var p = Process.Start(psi);
            if (p is null)
            {
                return (0, 0);
            }
            var err = p.StandardError.ReadToEnd();
            if (!p.WaitForExit(15000))
            {
                try { p.Kill(); } catch { }
                return (0, 0);
            }
            var m = Regex.Match(err, @"Video:.*?\b(\d{3,5})x(\d{3,5})\b", RegexOptions.IgnoreCase);
            if (m.Success)
            {
                return (int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value));
            }
        }
        catch
        {
            // 探测失败不影响处理
        }
        return (0, 0);
    }

    private static int LaunchBackend(
        List<string> backendArgs, string input, string model, string outputFile, string customEncoder, StopWatcher? stopWatcher,
        string? interpModel, string? interpFactor)
    {
        Console.WriteLine();
        Console.WriteLine("[信息] 输入视频 : " + input);
        if (string.IsNullOrEmpty(model))
        {
            Console.WriteLine("[信息] 放大模型 : （未使用，仅补帧）");
        }
        else
        {
            Console.WriteLine("[信息] 放大模型 : " + model);
            var scale = DetectScale(model);
            if (!string.IsNullOrEmpty(scale))
            {
                Console.WriteLine("[信息] 放大倍率 : " + scale + "x");
            }
        }
        if (interpModel is not null)
        {
            Console.WriteLine("[信息] 补帧模型 : " + interpModel);
            Console.WriteLine("[信息] 补帧倍率 : " + (interpFactor ?? "2") + "x");
        }
        Console.WriteLine("[信息] 输出文件 : " + outputFile);
        Console.WriteLine("[信息] FFmpeg 参数 : " + customEncoder);
        Console.WriteLine("[信息] 正在启动 rve-backend，输出实时转发，Ctrl+C 可中止…");
        Console.WriteLine();

        var psi = new ProcessStartInfo
        {
            FileName = PythonExe,
            WorkingDirectory = CoreRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var a in backendArgs)
        {
            psi.ArgumentList.Add(a);
        }

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        var job = CreateKillOnCloseJob();
        var throttle = new ProgressThrottle();
        var oomHintPrinted = false;

        // 启动前检查停止请求（用户可能在环境检测阶段就点了停止）
        if (stopWatcher is not null && stopWatcher.IsStopRequested())
        {
            Console.WriteLine("[信息] 已收到停止请求，未启动处理。");
            return 130;
        }

        var cancelRequested = false;
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cancelRequested = true;
        };

        void Forward(string? data, bool isError)
        {
            var line = SanitizeLine(data);
            if (line is null)
            {
                return;
            }
            if (!throttle.ShouldForward(line))
            {
                return;
            }
            if (!oomHintPrinted && OomHintRegex.IsMatch(line))
            {
                oomHintPrinted = true;
                Console.Error.WriteLine();
                Console.Error.WriteLine("[提示] 检测到内存不足（MemoryError）。建议：改用较低倍率模型（如 2x）、关闭占用内存的程序，或对视频分段处理。");
                Console.Error.WriteLine();
            }
            if (isError)
            {
                Console.Error.WriteLine(line);
            }
            else
            {
                Console.WriteLine(line);
            }
        }

        process.OutputDataReceived += (_, e) => Forward(e.Data, isError: false);
        process.ErrorDataReceived += (_, e) => Forward(e.Data, isError: true);

        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            return Fail("无法启动 Python（" + PythonExe + "）：" + ex.Message);
        }

        if (job != IntPtr.Zero)
        {
            try
            {
                AssignProcessToJobObject(job, process.Handle);
            }
            catch
            {
                // 进程可能已加入其他作业，停止时降级为仅杀 CLI
            }
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        // 周期性记录后端启动的 ffmpeg 子进程（停止时用于等待收尾）
        var ffmpegPids = new List<int>();
        var ffmpegPidsLock = new object();
        using var snapshotTimer = new System.Threading.Timer(_ =>
        {
            lock (ffmpegPidsLock)
            {
                ffmpegPids = GetFfmpegChildPids(process.Id);
            }
        }, null, 1000, 1000);

        var stopped = false;
        while (!process.HasExited && !cancelRequested)
        {
            if (stopWatcher is not null && stopWatcher.IsStopRequested())
            {
                stopped = true;
                break;
            }
            Thread.Sleep(200);
        }

        snapshotTimer.Dispose();
        List<int> childSnapshot;
        lock (ffmpegPidsLock)
        {
            childSnapshot = new List<int>(ffmpegPids);
        }

        if (stopped || cancelRequested)
        {
            // 优雅停止期间保持作业对象存活，让 ffmpeg 写进程能自行收尾并完成封装；
            // 停止完成后关闭作业句柄，清掉作业内残留进程（python 已终止）。
            var result = GracefulStop(process, childSnapshot, outputFile);
            if (job != IntPtr.Zero)
            {
                try
                {
                    CloseHandle(job);
                }
                catch
                {
                    // 忽略
                }
            }
            return result;
        }

        if (job != IntPtr.Zero)
        {
            try
            {
                CloseHandle(job);
            }
            catch
            {
                // 忽略
            }
        }

        Console.WriteLine();
        var exitCode = process.ExitCode;
        var outputOk = !string.IsNullOrEmpty(outputFile)
            && File.Exists(outputFile)
            && new FileInfo(outputFile).Length > 0;
        var sizeText = outputOk ? "（" + FormatSize(new FileInfo(outputFile).Length) + "）" : "";

        if (exitCode == 0 || outputOk)
        {
            Console.WriteLine("[完成] 视频超分辨率处理成功结束。");
            if (outputOk)
            {
                Console.WriteLine("[信息] 输出文件 : " + outputFile + " " + sizeText);
            }
            if (exitCode != 0)
            {
                Console.Error.WriteLine(
                    "[警告] 后端退出码 " + exitCode + "（非零，通常为退出时驱动/库清理问题），但输出文件已生成，可正常使用。");
            }
            return 0;
        }

        Console.WriteLine("[失败] rve-backend 退出码 " + exitCode + "，请查看上方错误信息。");
        return exitCode;
    }

    private static bool RunCheck(bool verbose)
    {
        var ok = true;

        Console.WriteLine("[环境检查] videoenhancer v" + ToolVersion);
        Console.WriteLine("[环境检查] 根目录   : " + CoreRoot);

        var ffmpegOk = File.Exists(FfmpegExe);
        Report(ffmpegOk, "bin\\ffmpeg", FfmpegExe);
        ok &= ffmpegOk;

        var pythonOk = File.Exists(PythonExe);
        Report(pythonOk, "python", PythonExe);
        ok &= pythonOk;

        var backendOk = File.Exists(BackendScript);
        Report(backendOk, "后端脚本", BackendScript);
        ok &= backendOk;

        var sitePkgOk = Directory.Exists(PythonSitePackages);
        Report(sitePkgOk, "python 库", PythonSitePackages);
        ok &= sitePkgOk;

        var models = DiscoverModelFolders();
        Report(models.Count > 0, "模型库", ModelsDir,
            models.Count > 0 ? models.Count + " 个可用模型" : "未找到含 .param/.bin 的模型");
        ok &= models.Count > 0;

        var interpModels = DiscoverInterpModelFolders();
        Report(true, "补帧模型库", Path.Combine(ModelsDir, "RIFE"),
            interpModels.Count > 0 ? interpModels.Count + " 个可用补帧模型" : "未找到含 .param/.bin 的补帧模型（可忽略，仅超分可用）");

        if (verbose)
        {
            var ffmpegVersion = RunProcessCapture(FfmpegExe, new[] { "-version" }, 30);
            var ffmpegFirst = ffmpegVersion.Output.Split('\n').FirstOrDefault(l => l.Contains("ffmpeg version"));
            Report(ffmpegVersion.Ok, "ffmpeg 可执行", FfmpegExe, ffmpegFirst?.Trim() ?? ffmpegVersion.Error.Trim());
            ok &= ffmpegVersion.Ok;

            var pyImport = RunProcessCapture(
                PythonExe, new[] { "-c", "import numpy, cv2; print('numpy', numpy.__version__); print('cv2', cv2.__version__)" }, 60);
            var pyDetail = pyImport.Ok ? pyImport.Output.Trim().Replace('\n', ' ') : pyImport.Error.Trim();
            Report(pyImport.Ok, "python 库导入", "numpy / opencv", pyDetail);
            ok &= pyImport.Ok;

            var backendVersion = RunProcessCapture(PythonExe, new[] { BackendScript, "--version" }, 60);
            Report(backendVersion.Ok, "后端脚本运行", BackendScript,
                backendVersion.Ok ? "rve-backend v" + backendVersion.Output.Trim() : backendVersion.Error.Trim());
            ok &= backendVersion.Ok;
        }

        Console.WriteLine("[环境检查] " + (ok ? "全部通过。" : "存在缺失项，请检查上方 [缺失] 标记。"));
        return ok;
    }

    private static void Report(bool ok, string label, string detail, string? extra = null)
    {
        var mark = ok ? "[通过]" : "[缺失]";
        var line = "  " + mark + " " + label + " : " + detail;
        if (!string.IsNullOrWhiteSpace(extra))
        {
            line += "  (" + extra + ")";
        }
        (ok ? Console.Out : Console.Error).WriteLine(line);
    }

    private static (bool Ok, string Output, string Error) RunProcessCapture(string fileName, string[] args, int timeoutSeconds)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };
            foreach (var a in args)
            {
                psi.ArgumentList.Add(a);
            }
            using var p = Process.Start(psi);
            if (p is null)
            {
                return (false, "", "无法启动进程");
            }
            var stdout = p.StandardOutput.ReadToEnd();
            var stderr = p.StandardError.ReadToEnd();
            if (!p.WaitForExit(timeoutSeconds * 1000))
            {
                try
                {
                    p.Kill(entireProcessTree: true);
                }
                catch
                {
                    // 忽略
                }
                return (false, stdout, "超时（" + timeoutSeconds + " 秒）");
            }
            return (p.ExitCode == 0, stdout, stderr);
        }
        catch (Exception ex)
        {
            return (false, "", ex.Message);
        }
    }

    private static int ListModels(bool json)
    {
        var models = DiscoverModelFolders();
        if (json)
        {
            // 机器可读：一行 JSON 数组（插件下拉框等调用方直接解析）
            var names = models.Select(m => Path.GetFileName(m)).ToList();
            Console.WriteLine("[" + string.Join(",", names.Select(n => "\"" + n + "\"")) + "]");
            return 0;
        }
        Console.WriteLine("可用放大模型（models 目录）：");
        if (models.Count == 0)
        {
            Console.WriteLine("  (未找到任何含 .param/.bin 的模型文件夹)");
            return 0;
        }
        foreach (var m in models)
        {
            var scale = DetectScale(m);
            Console.WriteLine("  " + Path.GetFileName(m) + (scale is null ? "" : "  (" + scale + "x)"));
        }
        return 0;
    }

    private static List<string> DiscoverModelFolders()
    {
        if (!Directory.Exists(ModelsDir))
        {
            return new List<string>();
        }
        return Directory.GetDirectories(ModelsDir)
            .Where(IsNcnnModelFolder)
            .OrderBy(p => p, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static string FormatSize(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        var value = (double)bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }
        return value.ToString(unit == 0 ? "0" : "0.##") + " " + units[unit];
    }

    private static bool IsNcnnModelFolder(string dir)
    {
        return Directory.EnumerateFiles(dir, "*.param", SearchOption.TopDirectoryOnly).Any()
            && Directory.EnumerateFiles(dir, "*.bin", SearchOption.TopDirectoryOnly).Any();
    }

    private static int Fail(string message, int exitCode = 2)
    {
        Console.Error.WriteLine();
        Console.Error.WriteLine("[错误] " + message);
        Console.Error.WriteLine("[提示] 使用 videoenhancer.exe -h 查看详细帮助。");
        return exitCode;
    }

    private static void PrintHelp(TextWriter writer)
    {
        writer.WriteLine("videoenhancer.exe — 视频超分辨率命令行工具  v" + ToolVersion);
        writer.WriteLine("============================================================");
        writer.WriteLine("用法");
        writer.WriteLine("  videoenhancer.exe -i <输入视频> -modelpath <模型目录> -ffmpeg-settings \"<FFmpeg 参数 + 输出路径>\"");
        writer.WriteLine("  videoenhancer.exe -i <输入视频> -interp-model <补帧模型> [-no-upscale] -ffmpeg-settings \"<FFmpeg 参数 + 输出路径>\"");
        writer.WriteLine();
        writer.WriteLine("必需参数");
        writer.WriteLine("  -i, --input <路径>");
        writer.WriteLine("        输入视频路径，含空格时用双引号包裹，例如 -i \"D:\\videos\\input.mp4\"");
        writer.WriteLine("  -modelpath, --modelpath, --model <路径>");
        writer.WriteLine("        放大模型：可给完整路径、models 下的相对路径或模型名");
        writer.WriteLine("        （如 RealESRGAN-AnimeVideoV3-2x）；省略时使用默认模型；");
        writer.WriteLine("        配合 -no-upscale 时可不提供（仅补帧模式）");
        writer.WriteLine("  -ffmpeg-settings, --ffmpeg-settings <字符串>");
        writer.WriteLine("        FFmpeg 输出编码参数，最后一个参数必须是输出文件路径");
        writer.WriteLine("        （因此不需要 -o，输出路径内置于该参数中）");
        writer.WriteLine();
        writer.WriteLine("可选参数");
        writer.WriteLine("  -h, --help          显示本帮助并退出");
        writer.WriteLine("  -scale <N>          强制放大倍率（如 2/3/4），默认从模型名自动识别");
        writer.WriteLine("  -interp-model <路径>  补帧模型（RIFE）：完整路径、models\\RIFE 下的相对路径或子文件夹名");
        writer.WriteLine("        （如 rife-v4.25）；可与 -modelpath 同时使用（先补帧后放大）");
        writer.WriteLine("  -interp-factor <N>  补帧倍率（帧率倍数，默认 2，需大于 1）");
        writer.WriteLine("  -no-upscale         不放大（仅补帧模式，需配合 -interp-model）");
        writer.WriteLine("  -pause-shm <ID>     暂停共享内存名（透传给 rve-backend --pause_shared_memory_id）");
        writer.WriteLine("  -stop-shm <ID>      停止共享内存名：字节变 1 时优雅停止，已处理部分写入输出文件");
        writer.WriteLine("  --list-models, --search-models  列出 models 目录下可用的放大模型并退出");
        writer.WriteLine("        （配合 --json 输出一行 JSON 数组，供界面程序解析）");
        writer.WriteLine("  --list-interp-models  列出 models\\RIFE 目录下可用的补帧模型并退出");
        writer.WriteLine("        （配合 --json 输出一行 JSON 数组，供界面程序解析）");
        writer.WriteLine("  --check             仅检测运行环境（ffmpeg / python 库 / 模型库）并退出");
        writer.WriteLine();
        writer.WriteLine("说明");
        writer.WriteLine("  · 配置：exe 同目录的 videoenhancer.ini 第一行写入 core-path=\"<核心程序路径>\"，");
        writer.WriteLine("    指向 bin\\ffmpeg、python、models 所在的根目录（后端分离部署时使用）；");
        writer.WriteLine("    未配置时回退到 exe 同目录布局，任一路径缺失会报错并标出缺失项。");
        writer.WriteLine("  · 程序自动检测 core-path 下的 bin\\ffmpeg\\ffmpeg.exe、python\\python\\python.exe、");
        writer.WriteLine("    python\\backend\\rve-backend.py、python 库与 models\\ 模型库；");
        writer.WriteLine("    任一缺失会报错并标出缺失项。");
        writer.WriteLine("  · ffmpeg-settings 是“编码参数 + 输出文件”的完整片段，程序会中转给");
        writer.WriteLine("    rve-backend（--custom_encoder 与 -o）。输出路径必须是最后一个参数；");
        writer.WriteLine("    末尾可加 -y 表示覆盖已存在文件。");
        writer.WriteLine("    -map 流映射会被自动移除（后端写进程自带映射），-map_metadata / -map_chapters");
        writer.WriteLine("    的源输入索引自动从 0 改写为 1（后端写进程中源文件为输入 1）。");
        writer.WriteLine("  · 带空格的参数值请用双引号包裹；编码参数需要完整（如像素格式），");
        writer.WriteLine("    与 GUI“参数总览”生成的片段一致。");
        writer.WriteLine();
        writer.WriteLine("示例（PowerShell）");
        writer.WriteLine("  .\\videoenhancer.exe -i \"D:\\videos\\input.mp4\" -modelpath RealESRGAN-AnimeVideoV3-2x `");
        writer.WriteLine("      -ffmpeg-settings '-c:v av1_nvenc -preset:v p4 -cq:v 38 -pix_fmt:v p010le `");
        writer.WriteLine("                       -c:a libopus -b:a 192k \"D:\\videos\\input_upscaled.mkv\"'");
        writer.WriteLine();
        writer.WriteLine("示例（cmd）");
        writer.WriteLine("  videoenhancer.exe -i \"D:\\videos\\input.mp4\" -modelpath RealESRGAN-AnimeVideoV3-2x `");
        writer.WriteLine("      -ffmpeg-settings \"-c:v libx264 -crf 18 -c:a aac \\\"D:\\videos\\out.mp4\\\"\"");
        writer.WriteLine("示例（cmd，仅补帧 2x）");
        writer.WriteLine("  videoenhancer.exe -i \"D:\\videos\\input.mp4\" -no-upscale -interp-model rife-v4.25 `");
        writer.WriteLine("      -ffmpeg-settings \"-c:v libx264 -crf 18 -r 60 \\\"D:\\videos\\out_60fps.mp4\\\"\"");
        writer.WriteLine();
        writer.WriteLine("退出码");
        writer.WriteLine("  0 成功；1 处理失败或环境错误；2 参数错误；130 用户中止");
        writer.WriteLine();
        writer.WriteLine("说明：本工具是 Video Enhancer GUI 的 rve-backend 命令行中转器，后端逻辑");
        writer.WriteLine("与 GUI 完全一致（ncnn 后端、场景检测、倍率自动识别等）。");
    }
}












