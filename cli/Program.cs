using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.IO.MemoryMappedFiles;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace VideoEnhancer;

/// <summary>
/// videoenhancer.exe — rve-backend 的命令行中转器。
/// 简化参数：-i / -modelpath / -ffmpeg-settings；输出路径位于 ffmpeg-settings 末尾（无 -o）。
/// </summary>
internal static class Program
{
    // 版本唯一来源是 cli\VideoEnhancer.csproj 的 <Version>；运行时从程序集元数据读取，避免多处字面量漂移。
    private static string ToolVersion { get; } = ResolveToolVersion();

    private static string ResolveToolVersion()
    {
        var assembly = Assembly.GetEntryAssembly();
        var informational = assembly?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informational))
        {
            // InformationalVersion 可能带 "+提交哈希" 元数据后缀，缓存目录等用途需要纯版本号。
            var plus = informational.IndexOf('+');
            return plus > 0 ? informational[..plus] : informational;
        }
        return assembly?.GetName().Version?.ToString(3) ?? "0.0.0";
    }
    private const string EmbeddedPluginResource = "VideoEnhancer.Embedded.videoenhancer.3fui.dll";
    private const string EmbeddedAriaResource = "VideoEnhancer.Embedded.aria2-next.exe";
    private const string Embedded7ZipResource = "VideoEnhancer.Embedded.7za.exe";
    private const string EmbeddedOrderedBackendResource = "VideoEnhancer.Embedded.rve-ordered-backend.py";
    private const string DefaultModelScopeDataset = "AerithDream/VideoEnhancer-Models";
    private static string? ModelScopeToken =>
        Environment.GetEnvironmentVariable("VIDEOENHANCER_MODELSCOPE_TOKEN")?.Trim() is { Length: > 0 } localToken
            ? localToken
            : Environment.GetEnvironmentVariable("MODELSCOPE_API_TOKEN")?.Trim() is { Length: > 0 } apiToken
                ? apiToken
                : null;
    private static string ModelScopeDataset =>
        Environment.GetEnvironmentVariable("VIDEOENHANCER_MODELSCOPE_DATASET")?.Trim() is { Length: > 0 } value
            ? value.Trim('/').Replace('\\', '/')
            : DefaultModelScopeDataset;
    private const int ModelScopeTreePageSize = 500;
    private static string ModelScopeTreeApi(int pageNumber) =>
        "https://www.modelscope.cn/api/v1/datasets/" + ModelScopeDataset +
        "/repo/tree?Revision=master&Recursive=true&PageNumber=" + pageNumber +
        "&PageSize=" + ModelScopeTreePageSize;
    private static string ModelScopeResolveRoot =>
        "https://www.modelscope.cn/datasets/" + ModelScopeDataset + "/resolve/master/";

    // exe 所在目录：videoenhancer.ini 的查找位置，也是未配置 core-path 时的回退根目录（1.0 布局）
    private static readonly string AppRoot = AppContext.BaseDirectory.TrimEnd(
        Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    // 核心程序根目录：默认 exe 同目录；若 videoenhancer.ini 配置了 core-path="<核心程序路径>"，
    // 则指向后端分离后的根目录（bin\ffmpeg / python / models 所在处）。
    private static string CoreRoot = AppRoot;

    private static string PythonExe => Path.Combine(CoreRoot, "python", "python", "python.exe");
    private static string BackendScript => Path.Combine(CoreRoot, "python", "backend", "rve-backend.py");
    private static string ImageBackendScript => Path.Combine(CoreRoot, "python", "backend", "rve-image-backend.py");
    private static string TensorRTValidatorScript => Path.Combine(CoreRoot, "python", "backend", "validate_tensorrt_engines.py");
    private static string TensorRTConverterScript => Path.Combine(CoreRoot, "python", "backend", "convert_tensorrt.py");
    private static string FfmpegExe => Path.Combine(CoreRoot, "bin", "ffmpeg", "ffmpeg.exe");
    private static string ModelsDir => Path.Combine(CoreRoot, "models");
    private static string FrameInterpolationDir => Path.Combine(ModelsDir, "Frame-Interpolation");
    private static string LegacyRifeDir => Path.Combine(ModelsDir, "RIFE");
    private static string TensorRTCacheDir => Path.Combine(ModelsDir, "TensorRT-Cache");
    private static string SceneDetectModel => FindNcnnModelFolder("EfficientNet-SceneDetect")
        ?? Path.Combine(ModelsDir, "EfficientNet-SceneDetect");
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

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetConsoleWindow();

    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetOpenFileName(ref OPENFILENAME ofn);

    [DllImport("comdlg32.dll")]
    private static extern uint CommDlgExtendedError();

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct OPENFILENAME
    {
        public int lStructSize;
        public IntPtr hwndOwner;
        public IntPtr hInstance;
        public IntPtr lpstrFilter;
        public IntPtr lpstrCustomFilter;
        public int nMaxCustFilter;
        public int nFilterIndex;
        public IntPtr lpstrFile;
        public int nMaxFile;
        public IntPtr lpstrFileTitle;
        public int nMaxFileTitle;
        public IntPtr lpstrInitialDir;
        public IntPtr lpstrTitle;
        public uint Flags;
        public short nFileOffset;
        public short nFileExtension;
        public IntPtr lpstrDefExt;
        public IntPtr lCustData;
        public IntPtr lpfnHook;
        public IntPtr lpTemplateName;
        public IntPtr pvReserved;
        public int dwReserved;
        public uint FlagsEx;
    }

    private const uint OFN_PATHMUSTEXIST = 0x00000800;
    private const uint OFN_FILEMUSTEXIST = 0x00001000;
    private const uint OFN_EXPLORER = 0x00080000;

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

    /// <summary>
    /// FPS 精确重算：rve-backend 自报的 FPS 是整数值且把暂停时间计入（暂停后恢复平均值偏低）。
    /// 这里用"已渲染帧数 / 有效耗时（总耗时 − 暂停耗时）"重算，输出保留两位小数；
    /// 同时按相同速率重算 ETA。暂停状态通过 -pause-shm 共享内存字节（1=暂停）采样。
    /// </summary>
    private sealed class FpsTracker
    {
        private static readonly Regex ProgressLine = new(
            @"FPS:\s*[\d.]+\s*Current Frame:\s*(\d+)\s*ETA:\s*[\d:]+",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex TotalFramesLine = new(
            @"Total Output Frames:\s*(\d+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private readonly string? _pauseShm;
        private readonly object _sync = new();
        private DateTime _firstLine;
        private bool _hasFirstLine;
        private DateTime _lastPauseSample = DateTime.UtcNow;
        private TimeSpan _paused = TimeSpan.Zero;
        private bool _wasPaused;
        private long _totalFrames;
        private bool _hasTotal;

        public FpsTracker(string? pauseShm)
        {
            _pauseShm = pauseShm;
        }

        /// <summary>采样暂停共享内存（每 ~200ms 由主循环调用），累计暂停时长。</summary>
        public void SamplePause()
        {
            if (string.IsNullOrWhiteSpace(_pauseShm))
            {
                return;
            }
            var now = DateTime.UtcNow;
            var delta = now - _lastPauseSample;
            _lastPauseSample = now;
            var paused = ReadShmByte(_pauseShm) == 1;
            if (_wasPaused || paused)
            {
                lock (_sync)
                {
                    _paused += delta;
                }
            }
            _wasPaused = paused;
        }

        /// <summary>重写进度行；非进度行原样返回。</summary>
        public string Rewrite(string? line)
        {
            if (line is null)
            {
                return string.Empty;
            }
            var totalMatch = TotalFramesLine.Match(line);
            if (totalMatch.Success && long.TryParse(totalMatch.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var total))
            {
                _totalFrames = total;
                _hasTotal = true;
            }
            var m = ProgressLine.Match(line);
            if (!m.Success)
            {
                return line;
            }
            if (!long.TryParse(m.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var frame) || frame <= 0)
            {
                return line;
            }

            var now = DateTime.UtcNow;
            if (!_hasFirstLine)
            {
                _firstLine = now;
                _hasFirstLine = true;
            }
            TimeSpan active;
            lock (_sync)
            {
                active = now - _firstLine - _paused;
            }
            if (active <= TimeSpan.Zero)
            {
                return line;
            }

            var fps = frame / active.TotalSeconds;
            var fpsText = fps.ToString("F2", CultureInfo.InvariantCulture);
            var etaText = m.Groups[2].Value;
            if (_hasTotal && _totalFrames > frame)
            {
                var secondsPerFrame = active.TotalSeconds / frame;
                var remainingSeconds = (long)Math.Ceiling((_totalFrames - frame) * secondsPerFrame);
                etaText = FormatEta(remainingSeconds);
            }
            return ProgressLine.Replace(line,
                "FPS: " + fpsText + " Current Frame: " + m.Groups[1].Value + " ETA: " + etaText);
        }

        /// <summary>ETA 格式与 rve-backend 一致：H:MM:SS（小时不补零，分/秒补零）。</summary>
        private static string FormatEta(long seconds)
        {
            if (seconds < 0)
            {
                seconds = 0;
            }
            var h = seconds / 3600;
            var mm = (seconds % 3600) / 60;
            var ss = seconds % 60;
            return h.ToString(CultureInfo.InvariantCulture) + ":" +
                   mm.ToString("00", CultureInfo.InvariantCulture) + ":" +
                   ss.ToString("00", CultureInfo.InvariantCulture);
        }
    }

    private sealed class Options
    {
        public bool ShowHelp;
        public bool ShowVersion;
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
        public string Backend = "ncnn";
        public bool HasBackend;
        public string InterpBackend = "";
        public bool HasInterpBackend;
        public string ProcessOrder = "upscale-first";
        public string SceneThreshold = "4.0";
        public bool HasSceneThreshold;
        public bool DynamicOpticalFlow;
        public string TileSize = "0";
        public bool HasTileSize;
        public bool ListBackends;
        public bool ValidateEngines;
        public bool ListDownloadModels;
        public bool CleanDownloadArchives;
        public string DownloadModel = "";
        public string DownloadUrl = "";
        public string DownloadOutput = "";
        public string ExtractArchive = "";
        public string ExtractOutput = "";
        public bool ApplyUpdate;
        public string UpdatePackage = "";
        public string UpdateTarget = "";
        public string WaitPid = "0";
        public string RestartExe = "";
        public string UpdateResult = "";
        public readonly List<string> ImageInputs = new();
        public readonly List<string> ImageFolders = new();
        public string ImageOutput = "";
        public bool ImageOutputOriginal;
        public string ImageSuffix = "timestamp";
        public bool ImagePng = true;
    }

    /// <summary>参与 TensorRT Engine 缓存隔离的本机运行时信息。</summary>
    private sealed record TensorRtRuntime(string GpuName, string TensorRtVersion);

    [STAThread]
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
            return RunInteractiveInstaller();
        }

        var o = ParseArgs(args);

        if (o.ShowVersion)
        {
            // 发布脚本用它在构建后做端到端版本一致性校验。
            Console.WriteLine(ToolVersion);
            return 0;
        }

        if (o.ShowHelp)
        {
            PrintHelp(Console.Out);
            return 0;
        }

        // 外部更新器运行在临时目录，不依赖 core-path、模型或推理后端。
        if (o.ApplyUpdate)
        {
            return ApplyUpdate(o);
        }

        // 推理后端：ncnn（默认，Vulkan）或 cuda（PyTorch，需 .pth 模型）
        if (o.HasBackend)
        {
            var b = o.Backend.Trim().ToLowerInvariant();
            if (b is not ("ncnn" or "cuda" or "tensorrt" or "onnx" or "flashvsr" or "basicvsrpp"))
            {
                return Fail("-backend 仅支持 ncnn、cuda、tensorrt、onnx、flashvsr 或 basicvsrpp，当前值：" + o.Backend);
            }
            o.Backend = b;
        }
        if (o.HasInterpBackend)
        {
            var b = o.InterpBackend.Trim().ToLowerInvariant();
            if (b is not ("ncnn" or "cuda" or "tensorrt"))
                return Fail("-interp-backend 仅支持 ncnn、cuda 或 tensorrt，当前值：" + o.InterpBackend);
            o.InterpBackend = b;
        }
        else
        {
            o.InterpBackend = DefaultInterpBackend(o.Backend);
        }
        o.ProcessOrder = o.ProcessOrder.Trim().ToLowerInvariant();
        if (o.ProcessOrder is not ("upscale-first" or "interp-first"))
            return Fail("-process-order 仅支持 upscale-first 或 interp-first，当前值：" + o.ProcessOrder);
        if (!double.TryParse(o.SceneThreshold, NumberStyles.Float, CultureInfo.InvariantCulture, out var sceneThreshold) || sceneThreshold <= 0 || sceneThreshold > 10.0)
            return Fail("-scene-threshold 必须是官方 0-10 标尺中的大于 0 数字，当前值：" + o.SceneThreshold);
        if (!int.TryParse(o.TileSize, NumberStyles.Integer, CultureInfo.InvariantCulture, out var tileSize) || tileSize < 0 || (tileSize > 0 && tileSize < 32))
            return Fail("-tile-size 必须是 0（RVE 默认）或不小于 32 的整数，当前值：" + o.TileSize);
        if (tileSize > 0 && o.Backend is not ("ncnn" or "cuda" or "tensorrt"))
            return Fail("-tile-size 仅支持 NCNN、CUDA/PyTorch 和 TensorRT；当前后端 " + o.Backend + " 不使用该参数");

        // 在线列表只读取远端元数据，不依赖本地核心目录。必须在配置校验前处理，
        // 否则无效的 core-path 会被界面误报为“当前无网络”。
        if (o.ListDownloadModels)
        {
            return ListRemoteModels(o.Json);
        }

        // 读取 videoenhancer.ini（第一行 core-path="<核心程序路径>"）确定核心程序根目录
        var configError = LoadCorePathConfig();
        if (configError is not null)
        {
            return Fail(configError, 1);
        }

        MigrateLegacyFfmpegLayout();

        if (o.CleanDownloadArchives)
        {
            return CleanDownloadArchives();
        }

        if (!string.IsNullOrWhiteSpace(o.DownloadModel))
        {
            return DownloadRepositoryModel(o.DownloadModel);
        }

        if (!string.IsNullOrWhiteSpace(o.DownloadUrl))
        {
            if (string.IsNullOrWhiteSpace(o.DownloadOutput))
                return Fail("--download-url 需要同时指定 --download-output <文件路径>");
            return DownloadWithAria(o.DownloadUrl, Path.GetFullPath(o.DownloadOutput));
        }

        if (!string.IsNullOrWhiteSpace(o.ExtractArchive))
        {
            var archive = Path.GetFullPath(o.ExtractArchive);
            var output = string.IsNullOrWhiteSpace(o.ExtractOutput)
                ? Path.GetDirectoryName(archive)!
                : Path.GetFullPath(o.ExtractOutput);
            return ExtractWith7Zip(archive, output);
        }

        if (o.ListModels)
        {
            return ListModels(o.Json, o.Backend);
        }

        if (o.ListInterpModels)
        {
            return ListInterpModels(o.Json, o.InterpBackend);
        }

        if (o.CheckOnly)
        {
            return RunCheck(verbose: true, backend: o.HasBackend ? o.Backend : null) ? 0 : 1;
        }

        if (o.ListBackends)
        {
            return ListBackendsWithEngineValidation();
        }

        if (o.ValidateEngines)
        {
            return ValidateAllTensorRTEngines();
        }

        // 图片超分是独立路径：不依赖 FFmpegFreeUI/FFmpeg 编码参数。
        if (o.ImageInputs.Count > 0 || o.ImageFolders.Count > 0)
        {
            if (o.Backend == "basicvsrpp") return Fail("BasicVSR++ 是连续视频帧模型，不能用于图片超分");
            return RunImageJob(o);
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
        if (!RunCheck(verbose: false, backend: o.Backend))
        {
            return 1;
        }

        // 2. 输入视频
        var input = Path.GetFullPath(o.Input);
        if (!File.Exists(input))
        {
            return Fail("输入视频不存在：" + input);
        }

        var useUpscale = !o.NoUpscale;
        // TensorRT Engine 与输入 profile 绑定，先探测尺寸再解析/构建模型。
        var inputResolution = useUpscale && o.Backend == "tensorrt" ? GetInputResolution(input) : (0, 0);
        if (useUpscale && o.Backend == "tensorrt" && (inputResolution.Item1 <= 0 || inputResolution.Item2 <= 0))
        {
            return Fail("TensorRT 无法探测输入尺寸，不能生成安全的 Engine 缓存键：" + input);
        }

        // 3. 放大模型（-no-upscale 时跳过，用于"仅补帧"模式）
        var model = "";
        if (useUpscale)
        {
            model = ResolveModel(o.Model, o.Backend);
            if (model.Length == 0)
            {
                return 1;
            }
            if (o.Backend == "tensorrt")
            {
                model = EnsureTensorRtEngine(model, inputResolution.Item1, inputResolution.Item2, stopWatcher, tileSize);
                if (model.Length == 0) return stopWatcher?.IsStopRequested() == true ? 130 : 1;
            }
        }

        // 3.5 补帧模型（RIFE）：补帧使用独立的有效后端，避免 TensorRT/ONNX 与 NCNN 模型格式错配。
        string? interpModel = null;
        if (o.HasInterpModel)
        {
            if (o.Backend == "basicvsrpp")
            {
                return Fail("BasicVSR++ 是时序视频超分管线，不能与 RIFE 补帧同时运行");
            }
            interpModel = ResolveInterpModel(o.InterpModel, o.InterpBackend);
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
                scale = o.Backend == "basicvsrpp" ? BasicVsrPlusPlusScale(model) : DetectScale(model);
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
            var (srcW, srcH) = inputResolution.Item1 > 0
                ? inputResolution
                : GetInputResolution(input);
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

        // 6. 自动识别 PQ/HLG；HDR 组合阶段必须保留 16-bit RGB 数据。
        var hdrMode = DetectHdrMode(input);
        if (hdrMode)
        {
            var unsupportedHdrBackends = new[] { "ncnn", "onnx", "flashvsr" };
            if (useUpscale && unsupportedHdrBackends.Contains(o.Backend))
            {
                return Fail("检测到 PQ/HLG HDR 视频，但超分后端 " + o.Backend +
                    " 不支持 RVE 的 16-bit RGB 帧管线；请改用 CUDA/PyTorch 或 TensorRT，不能静默降为 SDR。");
            }
            if (interpModel is not null && unsupportedHdrBackends.Contains(o.InterpBackend))
            {
                return Fail("检测到 PQ/HLG HDR 视频，但补帧后端 " + o.InterpBackend +
                    " 不支持 RVE 的 16-bit RGB 帧管线；请改用 CUDA/PyTorch 或 TensorRT，不能静默降为 SDR。");
            }
            Console.WriteLine("[HDR] 检测到 PQ/HLG 视频；组合中间帧将使用 16-bit RGB FFV1，并向 RVE 后端启用 HDR 模式。");
        }
        return RunVideoPipeline(input, outputFile, model, customEncoder, overwrite, scale,
            o.PauseShm, stopWatcher, interpModel, interpFactor, o.Backend, o.InterpBackend, o.ProcessOrder, hdrMode,
            o.DynamicOpticalFlow, sceneThreshold, tileSize);
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
                case "-v":
                case "--version":
                    o.ShowVersion = true;
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
                case "--list-backends":
                case "--list_backends":
                    o.ListBackends = true;
                    break;
                case "--validate-engines":
                    o.ValidateEngines = true;
                    break;
                case "--list-download-models":
                    o.ListDownloadModels = true;
                    break;
                case "--clean-download-archives":
                    o.CleanDownloadArchives = true;
                    break;
                case "--download-model":
                    o.DownloadModel = TakeValue(args, ref i, name, inlineValue);
                    break;
                case "--download-url":
                    o.DownloadUrl = TakeValue(args, ref i, name, inlineValue);
                    break;
                case "--download-output":
                    o.DownloadOutput = TakeValue(args, ref i, name, inlineValue);
                    break;
                case "--extract-archive":
                    o.ExtractArchive = TakeValue(args, ref i, name, inlineValue);
                    break;
                case "--extract-output":
                    o.ExtractOutput = TakeValue(args, ref i, name, inlineValue);
                    break;
                case "--apply-update":
                    o.ApplyUpdate = true;
                    break;
                case "--update-package":
                    o.UpdatePackage = TakeValue(args, ref i, name, inlineValue);
                    break;
                case "--update-target":
                    o.UpdateTarget = TakeValue(args, ref i, name, inlineValue);
                    break;
                case "--wait-pid":
                    o.WaitPid = TakeValue(args, ref i, name, inlineValue);
                    break;
                case "--restart-exe":
                    o.RestartExe = TakeValue(args, ref i, name, inlineValue);
                    break;
                case "--update-result":
                    o.UpdateResult = TakeValue(args, ref i, name, inlineValue);
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
                case "-backend":
                case "--backend":
                    o.Backend = TakeValue(args, ref i, name, inlineValue);
                    o.HasBackend = true;
                    break;
                case "-interp-backend":
                case "--interp-backend":
                    o.InterpBackend = TakeValue(args, ref i, name, inlineValue);
                    o.HasInterpBackend = true;
                    break;
                case "-process-order":
                case "--process-order":
                    o.ProcessOrder = TakeValue(args, ref i, name, inlineValue);
                    break;
                case "-scene-threshold":
                case "--scene-threshold":
                    o.SceneThreshold = TakeValue(args, ref i, name, inlineValue);
                    o.HasSceneThreshold = true;
                    break;
                case "-dynamic-optical-flow":
                case "--dynamic-optical-flow":
                    o.DynamicOpticalFlow = true;
                    break;
                case "-tile-size":
                case "--tile-size":
                case "--tilesize":
                    o.TileSize = TakeValue(args, ref i, name, inlineValue);
                    o.HasTileSize = true;
                    break;
                case "--image-input":
                    o.ImageInputs.Add(TakeValue(args, ref i, name, inlineValue));
                    break;
                case "--image-folder":
                    o.ImageFolders.Add(TakeValue(args, ref i, name, inlineValue));
                    break;
                case "--image-output":
                    o.ImageOutput = TakeValue(args, ref i, name, inlineValue);
                    break;
                case "--image-output-original":
                    o.ImageOutputOriginal = true;
                    break;
                case "--image-suffix":
                    o.ImageSuffix = TakeValue(args, ref i, name, inlineValue).Trim().ToLowerInvariant();
                    if (o.ImageSuffix is not ("timestamp" or "model"))
                    {
                        throw new ArgumentException("--image-suffix 仅支持 timestamp 或 model");
                    }
                    break;
                case "--image-png":
                    o.ImagePng = true;
                    break;
                case "--image-source-format":
                    o.ImagePng = false;
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

    private sealed record UpdatePackageManifest(string Version, List<UpdatePackageFile> Files);
    private sealed record UpdatePackageFile(string Path, long Size, string Sha256);

    /// <summary>在 3FUI 退出后应用已校验的三文件更新包；任一替换失败都会恢复原文件。</summary>
    private static int ApplyUpdate(Options o)
    {
        if (string.IsNullOrWhiteSpace(o.UpdatePackage) || string.IsNullOrWhiteSpace(o.UpdateTarget))
        {
            return Fail("--apply-update 需要 --update-package 和 --update-target");
        }
        if (!int.TryParse(o.WaitPid, NumberStyles.None, CultureInfo.InvariantCulture, out var waitPid) || waitPid < 0)
        {
            return Fail("--wait-pid 必须是非负整数");
        }
        if (waitPid == Environment.ProcessId)
        {
            return Fail("更新器不能等待自身退出");
        }

        var packagePath = Path.GetFullPath(o.UpdatePackage);
        var targetDirectory = Path.GetFullPath(o.UpdateTarget)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!File.Exists(packagePath)) return Fail("更新包不存在：" + packagePath, 1);
        if (!Directory.Exists(targetDirectory)) return Fail("更新目标目录不存在：" + targetDirectory, 1);

        var updateFiles = new[]
        {
            "videoenhancer.exe",
            "videoenhancer.3fui.dll",
            "videoenhancer-layout.json"
        };
        var allowedFiles = new HashSet<string>(updateFiles, StringComparer.OrdinalIgnoreCase);
        var workRoot = Path.Combine(Path.GetTempPath(), "VideoEnhancerUpdate", Guid.NewGuid().ToString("N"));
        var stagingDirectory = Path.Combine(workRoot, "staging");
        var backupDirectory = Path.Combine(workRoot, "backup");
        Directory.CreateDirectory(stagingDirectory);
        Directory.CreateDirectory(backupDirectory);

        try
        {
            UpdatePackageManifest manifest;
            using (var archive = ZipFile.OpenRead(packagePath))
            {
                var entries = new Dictionary<string, ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);
                foreach (var entry in archive.Entries)
                {
                    var name = entry.FullName.Replace('\\', '/');
                    if (entry.Name.Length == 0 || name.Contains('/') ||
                        (!name.Equals("package.json", StringComparison.OrdinalIgnoreCase) && !allowedFiles.Contains(name)))
                    {
                        throw new InvalidDataException("更新包包含未授权路径：" + entry.FullName);
                    }
                    if (!entries.TryAdd(name, entry))
                    {
                        throw new InvalidDataException("更新包包含重复文件：" + entry.FullName);
                    }
                }

                if (!entries.TryGetValue("package.json", out var manifestEntry) || manifestEntry.Length > 1024 * 1024)
                {
                    throw new InvalidDataException("更新包缺少有效的 package.json");
                }
                using (var reader = new StreamReader(manifestEntry.Open(), Encoding.UTF8, true))
                {
                    manifest = ParseUpdatePackageManifest(reader.ReadToEnd());
                }
                if (manifest.Files.Count != updateFiles.Length)
                {
                    throw new InvalidDataException("更新包必须且只能声明三个运行文件");
                }

                var declared = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var file in manifest.Files)
                {
                    var name = file.Path.Replace('\\', '/');
                    if (!allowedFiles.Contains(name) || name.Contains('/') || !declared.Add(name))
                    {
                        throw new InvalidDataException("package.json 包含未授权或重复路径：" + file.Path);
                    }
                    if (!entries.TryGetValue(name, out var entry))
                    {
                        throw new InvalidDataException("更新包缺少文件：" + name);
                    }
                    if (file.Size < 0 || entry.Length != file.Size)
                    {
                        throw new InvalidDataException("更新文件大小不匹配：" + name);
                    }

                    var stagedPath = Path.Combine(stagingDirectory, name);
                    using (var source = entry.Open())
                    using (var destination = new FileStream(stagedPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        source.CopyTo(destination);
                    }
                    using var staged = File.OpenRead(stagedPath);
                    var actualHash = Convert.ToHexString(SHA256.HashData(staged));
                    if (file.Sha256.Length != 64 || !actualHash.Equals(file.Sha256, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidDataException("更新文件 SHA-256 不匹配：" + name);
                    }
                }
            }

            if (waitPid > 0)
            {
                try
                {
                    using var process = Process.GetProcessById(waitPid);
                    Console.WriteLine("等待 3FUI 退出（PID " + waitPid + "）...");
                    if (!process.WaitForExit(5 * 60 * 1000))
                    {
                        throw new TimeoutException("等待 3FUI 退出超时，未修改任何文件");
                    }
                }
                catch (ArgumentException)
                {
                    // 宿主已退出，可以继续替换。
                }
            }

            var hadOriginal = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            foreach (var name in updateFiles)
            {
                var target = Path.Combine(targetDirectory, name);
                var exists = File.Exists(target);
                hadOriginal[name] = exists;
                if (exists) File.Copy(target, Path.Combine(backupDirectory, name), true);
            }

            try
            {
                foreach (var name in updateFiles)
                {
                    File.Copy(Path.Combine(stagingDirectory, name), Path.Combine(targetDirectory, name), true);
                }
            }
            catch
            {
                foreach (var name in updateFiles)
                {
                    var target = Path.Combine(targetDirectory, name);
                    try
                    {
                        if (hadOriginal[name])
                            File.Copy(Path.Combine(backupDirectory, name), target, true);
                        else if (File.Exists(target))
                            File.Delete(target);
                    }
                    catch (Exception rollbackError)
                    {
                        Console.Error.WriteLine("[回滚警告] " + name + "：" + rollbackError.Message);
                    }
                }
                throw;
            }

            Console.WriteLine("UPDATE_COMPLETE|" + manifest.Version);
            WriteUpdateResult(o.UpdateResult, "OK|" + manifest.Version);
            if (!string.IsNullOrWhiteSpace(o.RestartExe))
            {
                var restartExe = Path.GetFullPath(o.RestartExe);
                if (File.Exists(restartExe))
                {
                    Process.Start(new ProcessStartInfo(restartExe) { UseShellExecute = true });
                }
                else
                {
                    Console.Error.WriteLine("[警告] 更新已完成，但找不到要重启的 3FUI：" + restartExe);
                }
            }
            return 0;
        }
        catch (Exception ex)
        {
            WriteUpdateResult(o.UpdateResult, "ERROR|" + ex.Message.Replace('\r', ' ').Replace('\n', ' '));
            return Fail("应用更新失败：" + ex.Message, 1);
        }
        finally
        {
            try { Directory.Delete(workRoot, true); } catch { }
        }
    }

    private static UpdatePackageManifest ParseUpdatePackageManifest(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.GetProperty("schemaVersion").GetInt32() != 1)
        {
            throw new InvalidDataException("不支持的更新包清单版本");
        }
        var version = root.GetProperty("version").GetString()?.Trim() ?? "";
        if (version.Length == 0) throw new InvalidDataException("package.json 缺少版本号");
        var files = new List<UpdatePackageFile>();
        foreach (var file in root.GetProperty("files").EnumerateArray())
        {
            files.Add(new UpdatePackageFile(
                file.GetProperty("path").GetString()?.Trim() ?? "",
                file.GetProperty("size").GetInt64(),
                file.GetProperty("sha256").GetString()?.Trim() ?? ""));
        }
        return new UpdatePackageManifest(version, files);
    }

    private static void WriteUpdateResult(string resultPath, string value)
    {
        if (string.IsNullOrWhiteSpace(resultPath)) return;
        try
        {
            var fullPath = Path.GetFullPath(resultPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, value, new UTF8Encoding(false));
        }
        catch
        {
            // 结果文件只用于下次启动提示，不影响已经完成的更新或回滚。
        }
    }

    /// <summary>双击无参数启动时安装插件 DLL，并按需初始化便携核心目录。</summary>
    private static int RunInteractiveInstaller()
    {
        var installationStarted = false;
        try
        {
            Console.WriteLine($"VideoEnhancer 插件安装程序 v{ToolVersion}");
            Console.WriteLine("按下y并enter执行安装，按其他任意键并enter退出安装。");
            Console.Write("> ");
            if (!ReadYes())
            {
                Console.WriteLine("已退出安装。");
                return 0;
            }
            installationStarted = true;

            Console.WriteLine("请选择正确的ffmpegfreeui.exe路径（可执行文件名称不限）。");
            var hostExe = PickHostExecutable();
            if (string.IsNullOrWhiteSpace(hostExe))
            {
                Console.WriteLine("未选择程序，已取消安装。");
                return 0;
            }

            var hostDirectory = Path.GetDirectoryName(hostExe)
                ?? throw new InvalidOperationException("无法确定所选程序的目录。");
            var pluginDirectory = Path.Combine(hostDirectory, "plugin");
            var pluginPath = Path.Combine(pluginDirectory, "videoenhancer.3fui.dll");
            Directory.CreateDirectory(pluginDirectory);
            ExtractEmbeddedPlugin(pluginPath);
            Console.WriteLine("插件已安装到：" + pluginPath);

            var currentExe = Path.GetFullPath(Environment.ProcessPath ?? Path.Combine(AppRoot, "videoenhancer.exe"));
            var hasOtherEntries = Directory.EnumerateFileSystemEntries(AppRoot)
                .Any(path => !string.Equals(Path.GetFullPath(path), currentExe, StringComparison.OrdinalIgnoreCase));
            if (hasOtherEntries)
            {
                Console.Write("检测到程序当前目录存在其他文件，");
            }
            Console.Write($"程序即将在\"{AppRoot}\"中自动创建核心目录（models、python 和 bin），是否继续？选择\"是(Y)\"：");
            if (!ReadYes())
            {
                Console.WriteLine("插件安装完成；已跳过核心目录初始化。");
                return 0;
            }

            Directory.CreateDirectory(Path.Combine(AppRoot, "models"));
            Directory.CreateDirectory(Path.Combine(AppRoot, "python"));
            Directory.CreateDirectory(Path.Combine(AppRoot, "bin"));
            Console.WriteLine("安装完成。核心目录已准备好。");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("安装失败：" + ex);
            return 1;
        }
        finally
        {
            IfInstallationWasStartedPause(installationStarted);
        }
    }

    private static void IfInstallationWasStartedPause(bool installationStarted)
    {
        if (!installationStarted) return;
        Console.WriteLine("按 Enter 键关闭此窗口。");
        Console.ReadLine();
    }

    private static bool ReadYes()
    {
        var answer = Console.ReadLine()?.Trim();
        return string.Equals(answer, "Y", StringComparison.OrdinalIgnoreCase)
            || string.Equals(answer, "是", StringComparison.OrdinalIgnoreCase);
    }

    private static string? PickHostExecutable()
    {
        const int fileCapacity = 32768;
        var fileBuffer = Marshal.AllocHGlobal(fileCapacity * sizeof(char));
        var filter = Marshal.StringToHGlobalUni("可执行程序 (*.exe)\0*.exe\0所有文件 (*.*)\0*.*\0\0");
        var initialDirectory = Marshal.StringToHGlobalUni(AppRoot);
        var title = Marshal.StringToHGlobalUni("请选择正确的ffmpegfreeui.exe路径（文件名不限）");
        var defaultExtension = Marshal.StringToHGlobalUni("exe");
        try
        {
            Marshal.WriteInt16(fileBuffer, 0);
            var dialog = new OPENFILENAME
            {
                lStructSize = Marshal.SizeOf<OPENFILENAME>(),
                hwndOwner = GetConsoleWindow(),
                lpstrFilter = filter,
                nFilterIndex = 1,
                lpstrFile = fileBuffer,
                nMaxFile = fileCapacity,
                lpstrInitialDir = initialDirectory,
                lpstrTitle = title,
                Flags = OFN_EXPLORER | OFN_PATHMUSTEXIST | OFN_FILEMUSTEXIST,
                lpstrDefExt = defaultExtension
            };
            if (GetOpenFileName(ref dialog)) return Marshal.PtrToStringUni(fileBuffer);
            var error = CommDlgExtendedError();
            if (error != 0) throw new InvalidOperationException($"无法打开文件选择窗口（错误 0x{error:X8}）。");
            return null;
        }
        finally
        {
            Marshal.FreeHGlobal(fileBuffer);
            Marshal.FreeHGlobal(filter);
            Marshal.FreeHGlobal(initialDirectory);
            Marshal.FreeHGlobal(title);
            Marshal.FreeHGlobal(defaultExtension);
        }
    }

    private static void ExtractEmbeddedPlugin(string destinationPath)
    {
        using var source = Assembly.GetExecutingAssembly().GetManifestResourceStream(EmbeddedPluginResource)
            ?? throw new InvalidOperationException("内置的 videoenhancer 插件 DLL 不存在，无法安装。");
        using var destination = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
        source.CopyTo(destination);
    }

    private sealed record RemoteModel(string Name, string Path, long Size, string Sha256);

    private static List<RemoteModel> FetchRemoteModels()
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(25) };
        ApplyModelScopeAuthentication(client);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("VideoEnhancer/" + ToolVersion);
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        var result = new List<RemoteModel>();
        var allowedRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "Backend", "BasicVSR++", "Bin", "FlashVSR", "Frame-Interpolation", "ONNX", "Param-Bin", "RIFE", "PTH", "TensorRT-Default" };
        var fetchedEntries = 0;
        for (var pageNumber = 1; ; pageNumber++)
        {
            var json = client.GetStringAsync(ModelScopeTreeApi(pageNumber)).GetAwaiter().GetResult();
            using var document = JsonDocument.Parse(json);
            var rootElement = document.RootElement;
            var files = rootElement.GetProperty("Data").GetProperty("Files");
            var returnedEntries = files.GetArrayLength();
            fetchedEntries += returnedEntries;

            foreach (var file in files.EnumerateArray())
            {
                if (!string.Equals(file.GetProperty("Type").GetString(), "blob", StringComparison.OrdinalIgnoreCase)) continue;
                var path = file.GetProperty("Path").GetString()?.Replace('\\', '/').TrimStart('/') ?? "";
                if (path.Length == 0 || path.EndsWith("/.gitkeep", StringComparison.OrdinalIgnoreCase)) continue;
                var slash = path.IndexOf('/');
                var root = slash < 0 ? path : path[..slash];
                if (!allowedRoots.Contains(root)) continue;
                result.Add(new RemoteModel(
                    file.GetProperty("Name").GetString() ?? System.IO.Path.GetFileName(path),
                    path,
                    file.TryGetProperty("Size", out var size) ? size.GetInt64() : 0,
                    file.TryGetProperty("Sha256", out var hash) ? hash.GetString() ?? "" : ""));
            }

            var totalCount = rootElement.TryGetProperty("TotalCount", out var total)
                && total.TryGetInt32(out var parsedTotal)
                    ? parsedTotal
                    : -1;
            // ModelScope 默认只返回 100 条。根据总数翻页；旧接口缺少总数时，
            // 以实际返回数小于请求页大小作为结束条件。
            if (returnedEntries == 0
                || (totalCount >= 0 && fetchedEntries >= totalCount)
                || (totalCount < 0 && returnedEntries < ModelScopeTreePageSize))
            {
                break;
            }
        }
        // Backend 曾同时保留无日期包和日期包；界面只展示最新日期包，避免用户下载两套
        // 内容几乎相同的便携 Python。旧文件继续留在镜像中供历史版本使用。
        var preferredPythonArchive = result
            .Where(model => Regex.IsMatch(
                model.Path, @"^Backend/python_\d{8}\.7z$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            .OrderByDescending(model => model.Path, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
        if (preferredPythonArchive is not null)
        {
            result.RemoveAll(model =>
                model.Path.Equals("Backend/python.7z", StringComparison.OrdinalIgnoreCase)
                || (Regex.IsMatch(
                        model.Path, @"^Backend/python_\d{8}\.7z$",
                        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
                    && !model.Path.Equals(preferredPythonArchive.Path, StringComparison.OrdinalIgnoreCase)));
        }

        // 新版补帧包已迁移到 Frame-Interpolation；旧 RIFE/RIFE.7z 与其内容重复，
        // 但远端文件仍保留给旧客户端使用，因此只从当前下载列表隐藏旧路径。
        result.RemoveAll(model => model.Path.Equals("RIFE/RIFE.7z", StringComparison.OrdinalIgnoreCase));

        return result.OrderBy(m => m.Path, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static bool IsNetworkFailure(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is HttpRequestException or TaskCanceledException or TimeoutException)
                return true;
        }
        return false;
    }

    private static bool IsAuthenticationFailure(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is HttpRequestException request
                && request.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                return true;
        }
        return false;
    }

    private static void ApplyModelScopeAuthentication(HttpClient client)
    {
        var token = ModelScopeToken;
        if (token is null) return;
        client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", "Bearer " + token);
        client.DefaultRequestHeaders.TryAddWithoutValidation(
            "Cookie", "m_session_id=" + token + "; modelscope_session=" + token);
    }

    private static void WriteRemoteFailure(string operation, Exception exception)
    {
        if (IsAuthenticationFailure(exception))
            Console.Error.WriteLine("AUTH_REQUIRED|ModelScope 私有仓库需要有效令牌；请设置 VIDEOENHANCER_MODELSCOPE_TOKEN 或 MODELSCOPE_API_TOKEN");
        else if (IsNetworkFailure(exception))
            Console.Error.WriteLine("NO_NETWORK|无法连接 ModelScope");
        else
            Console.Error.WriteLine("REMOTE_ERROR|ModelScope 返回的数据无法解析");
        Console.Error.WriteLine($"[错误] {operation}：{exception.Message}");
    }

    private static int ListRemoteModels(bool json)
    {
        try
        {
            var models = FetchRemoteModels();
            if (json)
            {
                using var buffer = new MemoryStream();
                using (var writer = new Utf8JsonWriter(buffer))
                {
                    writer.WriteStartArray();
                    foreach (var model in models)
                    {
                        writer.WriteStartObject();
                        writer.WriteString("name", model.Name);
                        writer.WriteString("path", model.Path);
                        writer.WriteNumber("size", model.Size);
                        writer.WriteEndObject();
                    }
                    writer.WriteEndArray();
                }
                Console.WriteLine(Encoding.UTF8.GetString(buffer.ToArray()));
            }
            else
            {
                foreach (var model in models)
                    Console.WriteLine($"{model.Path}\t{model.Size}");
            }
            return 0;
        }
        catch (Exception ex)
        {
            WriteRemoteFailure("无法读取模型列表", ex);
            return 3;
        }
    }

    private static int DownloadRepositoryModel(string requestedPath)
    {
        List<RemoteModel> models;
        try
        {
            models = FetchRemoteModels();
        }
        catch (Exception ex)
        {
            WriteRemoteFailure("无法连接模型镜像", ex);
            return 3;
        }

        var normalized = requestedPath.Replace('\\', '/').TrimStart('/');
        var model = models.FirstOrDefault(m => m.Path.Equals(normalized, StringComparison.OrdinalIgnoreCase));
        if (model is null) return Fail("镜像中不存在该文件：" + normalized, 1);

        var slash = model.Path.IndexOf('/');
        if (slash <= 0) return Fail("模型镜像路径无效：" + model.Path, 1);
        var category = model.Path[..slash];
        var suffix = model.Path[(slash + 1)..].Replace('/', Path.DirectorySeparatorChar);
        var destinationRoot = category.Equals("Backend", StringComparison.OrdinalIgnoreCase)
            ? Path.Combine(CoreRoot, "python")
            : category.Equals("Bin", StringComparison.OrdinalIgnoreCase)
                ? Path.Combine(CoreRoot, "bin")
                : Path.Combine(CoreRoot, "models", category);
        var destination = SafeCombine(destinationRoot, suffix);
        var url = ModelScopeResolveRoot + string.Join("/", model.Path.Split('/').Select(Uri.EscapeDataString));
        Console.WriteLine("DOWNLOAD_START|" + model.Path);
        var code = ModelScopeToken is null
            ? DownloadWithAria(url, destination, printComplete: false)
            : DownloadModelScopeFile(url, destination);
        if (code != 0) return code;

        if (model.Size > 0 && new FileInfo(destination).Length != model.Size)
            return Fail("下载文件大小校验失败：" + destination, 1);
        if (!string.IsNullOrWhiteSpace(model.Sha256))
        {
            using var stream = File.OpenRead(destination);
            var actual = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream));
            if (!actual.Equals(model.Sha256, StringComparison.OrdinalIgnoreCase))
                return Fail("下载文件 SHA256 校验失败：" + destination, 1);
        }

        if (IsArchiveFile(destination))
        {
            // 旧镜像压缩包包含一级分类目录；新版补帧包只包含架构目录，需直接解到 Frame-Interpolation。
            var extractionRoot = category.Equals("Backend", StringComparison.OrdinalIgnoreCase)
                ? CoreRoot
                : category.Equals("Bin", StringComparison.OrdinalIgnoreCase)
                    ? Path.Combine(CoreRoot, "bin")
                    : category.Equals("Frame-Interpolation", StringComparison.OrdinalIgnoreCase)
                        ? FrameInterpolationDir
                        : Path.Combine(CoreRoot, "models");
            code = ExtractWith7Zip(destination, extractionRoot);
            if (code != 0) return code;
            if (category.Equals("Frame-Interpolation", StringComparison.OrdinalIgnoreCase))
            {
                var marker = FrameInterpolationArchiveMarkerPath(model.Path);
                Directory.CreateDirectory(Path.GetDirectoryName(marker)!);
                File.WriteAllText(marker, model.Path, Encoding.UTF8);
            }
        }
        Console.WriteLine("DOWNLOAD_COMPLETE|" + destination);
        return 0;
    }

    private static string FrameInterpolationArchiveMarkerPath(string relativePath)
    {
        var normalized = relativePath.Replace('\\', '/').ToUpperInvariant();
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)));
        return Path.Combine(FrameInterpolationDir, ".downloads", hash + ".installed");
    }

    private static int DownloadModelScopeFile(string url, string destination)
    {
        var partial = destination + ".part";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            using var handler = new HttpClientHandler { AllowAutoRedirect = false };
            using var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
            ApplyModelScopeAuthentication(client);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("VideoEnhancer/" + ToolVersion);
            using var response = client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException(
                    $"ModelScope 下载返回 HTTP {(int)response.StatusCode} ({response.ReasonPhrase})",
                    null,
                    response.StatusCode);

            var total = response.Content.Headers.ContentLength ?? 0;
            using var input = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult();
            using (var output = new FileStream(partial, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                var buffer = new byte[1024 * 1024];
                long completed = 0;
                var lastPercent = -1;
                int read;
                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    output.Write(buffer, 0, read);
                    completed += read;
                    if (total <= 0) continue;
                    var percent = (int)Math.Clamp(completed * 100L / total, 0, 100);
                    if (percent == lastPercent) continue;
                    lastPercent = percent;
                    Console.WriteLine($"DOWNLOAD_PROGRESS|{percent}|{Path.GetFileName(destination)}");
                }
                output.Flush(true);
            }
            File.Move(partial, destination, true);
            return 0;
        }
        catch (Exception ex)
        {
            try { if (File.Exists(partial)) File.Delete(partial); } catch { }
            if (IsAuthenticationFailure(ex))
                Console.Error.WriteLine("AUTH_REQUIRED|ModelScope 私有文件需要有效令牌；请检查 VIDEOENHANCER_MODELSCOPE_TOKEN 或 MODELSCOPE_API_TOKEN");
            else
                Console.Error.WriteLine("[错误] ModelScope 下载失败：" + ex.Message);
            return 1;
        }
    }

    private static string SafeCombine(string root, string relative)
    {
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var full = Path.GetFullPath(Path.Combine(fullRoot, relative));
        if (!full.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("路径越出了目标目录：" + relative);
        return full;
    }

    private static bool IsArchiveFile(string path)
    {
        var extension = Path.GetExtension(path);
        return extension.Equals(".7z", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".zip", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".rar", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".gz", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".xz", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".zst", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".tar", StringComparison.OrdinalIgnoreCase);
    }

    private static int CleanDownloadArchives()
    {
        var deleted = 0;
        long reclaimedBytes = 0;
        var failures = new List<string>();

        var candidates = new List<string>();
        var modelsRoot = Path.Combine(CoreRoot, "models");
        if (Directory.Exists(modelsRoot))
            candidates.AddRange(Directory.EnumerateFiles(modelsRoot, "*", SearchOption.AllDirectories));
        // Backend 下载包落在核心 python 目录的顶层；其子目录是运行时与后端源码，
        // 其中也包含 base_library.zip、测试数据 .gz 等不可删除的正常文件。
        var pythonRoot = Path.Combine(CoreRoot, "python");
        if (Directory.Exists(pythonRoot))
            candidates.AddRange(Directory.EnumerateFiles(pythonRoot, "*", SearchOption.TopDirectoryOnly));

        foreach (var file in candidates.Where(IsArchiveFile).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var full = Path.GetFullPath(file);
                var length = new FileInfo(full).Length;
                File.Delete(full);
                reclaimedBytes += length;
                deleted++;
                Console.WriteLine("CLEAN_DELETED|" + full);
            }
            catch (Exception ex)
            {
                failures.Add(file + "：" + ex.Message);
            }
        }

        Console.WriteLine($"CLEAN_COMPLETE|{deleted}|{reclaimedBytes}");
        if (failures.Count == 0) return 0;
        foreach (var failure in failures) Console.Error.WriteLine("[清理失败] " + failure);
        return 2;
    }

    /// <summary>把 preview.2 旧下载逻辑生成的 models 下 FFmpeg 目录迁移到标准 bin 目录。</summary>
    private static void MigrateLegacyFfmpegLayout()
    {
        if (File.Exists(FfmpegExe))
        {
            return;
        }

        var targetRoot = Path.Combine(CoreRoot, "bin", "ffmpeg");
        var legacyRoots = new[]
        {
            Path.Combine(CoreRoot, "models", "ffmpeg"),
            Path.Combine(CoreRoot, "models", "Bin", "ffmpeg"),
        };
        foreach (var legacyRoot in legacyRoots)
        {
            var legacyFfmpeg = Path.Combine(legacyRoot, "ffmpeg.exe");
            if (!File.Exists(legacyFfmpeg))
            {
                continue;
            }

            try
            {
                foreach (var file in Directory.EnumerateFiles(legacyRoot, "*", SearchOption.AllDirectories))
                {
                    var relative = Path.GetRelativePath(legacyRoot, file);
                    var destination = Path.Combine(targetRoot, relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                    File.Copy(file, destination, overwrite: false);
                }
                Console.WriteLine("[兼容] 已将旧位置的 FFmpeg 迁移到：" + targetRoot);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[警告] FFmpeg 旧目录迁移失败，将继续使用旧路径检查：" + ex.Message);
            }
            return;
        }
    }

    private static string EnsureEmbeddedTool(string resourceName, string fileName)
    {
        var basePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(basePath)) basePath = Path.GetTempPath();
        var directory = Path.Combine(basePath, "VideoEnhancer", "tools", ToolVersion);
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, fileName);
        using var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException("内置工具资源不存在：" + fileName);
        var needsUpdate = !File.Exists(path);
        if (!needsUpdate)
        {
            using var existing = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var resourceHash = SHA256.HashData(resource);
            var existingHash = SHA256.HashData(existing);
            needsUpdate = !resourceHash.AsSpan().SequenceEqual(existingHash);
            resource.Position = 0;
        }
        if (needsUpdate)
        {
            using var output = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
            resource.CopyTo(output);
        }
        return path;
    }

    private static int DownloadWithAria(string url, string destination, bool printComplete = true)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            var aria = EnsureEmbeddedTool(EmbeddedAriaResource, "aria2-next.exe");
            var start = new ProcessStartInfo
            {
                FileName = aria,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            foreach (var argument in new[]
            {
                "--allow-overwrite=true", "--auto-file-renaming=false", "--continue=true",
                "--file-allocation=none", "--max-connection-per-server=8", "--split=8",
                "--min-split-size=1M", "--summary-interval=1", "--enable-color=false",
                "--dir=" + Path.GetDirectoryName(destination), "--out=" + Path.GetFileName(destination), url
            }) start.ArgumentList.Add(argument);

            using var process = new Process { StartInfo = start };
            var percentRegex = new Regex(@"\((\d{1,3})%\)", RegexOptions.Compiled);
            var lastPercent = -1;
            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data is null) return;
                var match = percentRegex.Match(e.Data);
                if (match.Success && int.TryParse(match.Groups[1].Value, out var percent) && percent != lastPercent)
                {
                    lastPercent = percent;
                    Console.WriteLine($"DOWNLOAD_PROGRESS|{percent}|{Path.GetFileName(destination)}");
                }
            };
            process.ErrorDataReceived += (_, e) => { if (e.Data is not null) Console.Error.WriteLine(e.Data); };
            if (!process.Start()) return Fail("无法启动内置 aria2-next", 1);
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();
            if (process.ExitCode != 0) return Fail("aria2-next 下载失败，退出码：" + process.ExitCode, 1);
            if (!File.Exists(destination)) return Fail("下载结束但未找到输出文件：" + destination, 1);
            if (printComplete) Console.WriteLine("DOWNLOAD_COMPLETE|" + destination);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("[错误] 下载失败：" + ex.Message);
            return 1;
        }
    }

    private static int ExtractWith7Zip(string archive, string outputDirectory)
    {
        try
        {
            if (!File.Exists(archive)) return Fail("压缩文件不存在：" + archive, 1);
            Directory.CreateDirectory(outputDirectory);
            var sevenZip = EnsureEmbeddedTool(Embedded7ZipResource, "7za.exe");
            var start = new ProcessStartInfo
            {
                FileName = sevenZip,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            start.ArgumentList.Add("x");
            start.ArgumentList.Add(archive);
            start.ArgumentList.Add("-o" + outputDirectory);
            start.ArgumentList.Add("-y");
            using var process = Process.Start(start) ?? throw new InvalidOperationException("无法启动内置 7-Zip-zstd");
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                Console.Error.WriteLine(stderr.GetAwaiter().GetResult());
                return Fail("7-Zip-zstd 解压失败，退出码：" + process.ExitCode, 1);
            }
            _ = stdout.GetAwaiter().GetResult();
            Console.WriteLine("EXTRACT_COMPLETE|" + outputDirectory);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("[错误] 解压失败：" + ex.Message);
            return 1;
        }
    }

    /// <summary>启动完全独立于 FFmpeg 的静态图片超分后端。</summary>
    private static readonly HashSet<string> SupportedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".bmp", ".webp", ".tif", ".tiff"
    };

    /// <summary>找到图片任务中的第一张有效图片，用于 TensorRT 输入尺寸探测。</summary>
    private static string? FindFirstImageInput(Options o)
    {
        foreach (var input in o.ImageInputs)
        {
            var full = Path.GetFullPath(input);
            if (File.Exists(full) && SupportedImageExtensions.Contains(Path.GetExtension(full))) return full;
        }
        foreach (var folder in o.ImageFolders)
        {
            var full = Path.GetFullPath(folder);
            if (!Directory.Exists(full)) continue;
            try
            {
                var found = Directory.EnumerateFiles(full, "*", SearchOption.AllDirectories)
                    .FirstOrDefault(path => SupportedImageExtensions.Contains(Path.GetExtension(path)));
                if (found is not null) return found;
            }
            catch
            {
                // 让图片后端继续负责报告目录访问错误。
            }
        }
        return null;
    }

    private static int RunImageJob(Options o)
    {
        if (!File.Exists(PythonExe)) return Fail("图片后端找不到便携 Python：" + PythonExe, 1);
        if (!File.Exists(ImageBackendScript)) return Fail("图片后端脚本不存在：" + ImageBackendScript, 1);
        if (!o.ImageOutputOriginal && string.IsNullOrWhiteSpace(o.ImageOutput))
        {
            return Fail("图片处理需要 --image-output <文件夹>，或使用 --image-output-original");
        }

        var model = ResolveModel(o.Model, o.Backend);
        if (model.Length == 0) return 1;
        if (o.Backend == "tensorrt")
        {
            var firstInput = FindFirstImageInput(o);
            if (firstInput is null)
                return Fail("TensorRT 图片任务没有找到可用于探测尺寸的输入图片", 1);
            var (width, height) = GetInputResolution(firstInput);
            if (width <= 0 || height <= 0)
                return Fail("TensorRT 无法探测输入图片尺寸：" + firstInput, 1);
            model = EnsureTensorRtEngine(model, width, height, stopWatcher: null);
            if (model.Length == 0) return 1;
        }

        var start = new ProcessStartInfo
        {
            FileName = PythonExe,
            WorkingDirectory = Path.GetDirectoryName(ImageBackendScript)!,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        start.Environment["PYTHONUTF8"] = "1";
        start.Environment["PYTHONIOENCODING"] = "utf-8";
        start.ArgumentList.Add(ImageBackendScript);
        foreach (var input in o.ImageInputs)
        {
            start.ArgumentList.Add("--input");
            start.ArgumentList.Add(Path.GetFullPath(input));
        }
        foreach (var folder in o.ImageFolders)
        {
            start.ArgumentList.Add("--folder");
            start.ArgumentList.Add(Path.GetFullPath(folder));
        }
        if (o.ImageOutputOriginal)
        {
            start.ArgumentList.Add("--output-original");
        }
        else
        {
            start.ArgumentList.Add("--output");
            start.ArgumentList.Add(Path.GetFullPath(o.ImageOutput));
        }
        start.ArgumentList.Add("--suffix");
        start.ArgumentList.Add(o.ImageSuffix);
        start.ArgumentList.Add(o.ImagePng ? "--png" : "--no-png");
        start.ArgumentList.Add("--backend");
        start.ArgumentList.Add(o.Backend);
        start.ArgumentList.Add("--model");
        start.ArgumentList.Add(model);

        using var process = new Process { StartInfo = start };
        var job = CreateKillOnCloseJob();
        using var imageComplete = new ManualResetEventSlim(false);
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            Console.WriteLine(e.Data);
            if (e.Data.StartsWith("IMAGE_COMPLETE|", StringComparison.Ordinal)) imageComplete.Set();
        };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) Console.Error.WriteLine(e.Data); };
        if (!process.Start()) return Fail("无法启动图片超分后端", 1);
        if (job != IntPtr.Zero) AssignProcessToJobObject(job, process.Handle);
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        while (!process.WaitForExit(250))
        {
            if (!imageComplete.IsSet) continue;
            // NCNN/Vulkan can hang in native object destruction after the file
            // and IMAGE_COMPLETE record are already committed.  End only that
            // owned inference tree and report the completed job as successful.
            try { process.Kill(entireProcessTree: true); } catch { }
            process.WaitForExit();
            break;
        }
        var exitCode = imageComplete.IsSet ? 0 : process.ExitCode;
        if (job != IntPtr.Zero) CloseHandle(job);
        return exitCode;
    }

    /// <summary>解析模型路径：完整路径 / models 下相对路径 / 模型名；省略时用默认模型。</summary>
    /// <remarks>cuda 接受 .pth/.pt/.pkl；tensorrt 接受 PTH 源模型或预制 .engine；ncnn 接受含 .param/.bin 的模型文件夹。</remarks>
    private static string ResolveModel(string requested, string backend)
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
            if (backend == "flashvsr")
            {
                if (Directory.Exists(c) && IsFlashVsrModelDirectory(c))
                {
                    return c;
                }
            }
            else if (backend == "cuda")
            {
                if (File.Exists(c) && IsPthModelFile(c) && !IsInInterpolationDirectory(c))
                {
                    return c;
                }
            }
            else if (backend == "tensorrt" && File.Exists(c)
                     && (IsTensorRTEngineFile(c) || IsPthModelFile(c))
                     && !IsInInterpolationDirectory(c))
            {
                return c;
            }
            else if (backend == "onnx" && File.Exists(c) && IsOnnxModelFile(c) && !IsInInterpolationDirectory(c))
            {
                return c;
            }
            else if (backend == "basicvsrpp" && IsBasicVsrPlusPlusModel(c))
            {
                return c;
            }
            else if (Directory.Exists(c) && IsNcnnModelFolder(c) && !IsInInterpolationDirectory(c))
            {
                return c;
            }
        }

        // 按相对 models 的路径或模型名递归搜索；扩展名可省略。
        // 空请求也会按默认模型名递归定位，兼容 models\Param-Bin 等分类目录。
        var lookup = string.IsNullOrWhiteSpace(requested)
            ? Path.GetFileName(DefaultModel)
            : requested.Trim().Trim('"');
        if (!string.IsNullOrWhiteSpace(lookup))
        {
            var requestedName = lookup
                .Replace('\\', '/')
                .TrimStart('/');
            var requestedWithoutExtension = Path.ChangeExtension(requestedName, null) ?? requestedName;
            var baseName = Path.GetFileNameWithoutExtension(requestedName);
            var discovered = backend == "flashvsr" ? DiscoverFlashVsrModels()
                : backend == "tensorrt"
                ? DiscoverTensorRTSelectableModels()
                : backend == "cuda" ? DiscoverUpscalePthModels()
                : backend == "onnx" ? DiscoverOnnxModels()
                : backend == "basicvsrpp" ? DiscoverBasicVsrPlusPlusModels()
                : DiscoverModelFolders();
            foreach (var f in discovered)
            {
                var relativeName = UpscaleModelDisplayName(f, backend);
                if (relativeName.Equals(requestedWithoutExtension, StringComparison.OrdinalIgnoreCase)
                    || ModelBaseName(f).Equals(baseName, StringComparison.OrdinalIgnoreCase))
                {
                    return f;
                }
            }
        }

        Console.Error.WriteLine("[错误] 未找到可用模型：" + (string.IsNullOrWhiteSpace(requested) ? DefaultModel : requested));
        if (backend == "cuda" || backend == "tensorrt" || backend == "onnx" || backend == "flashvsr" || backend == "basicvsrpp")
        {
            Console.Error.WriteLine(backend == "basicvsrpp" ? "[提示] BasicVSR++ 后端需要 models\\BasicVSR++ 下的官方 .pth，或包含 config.py/chkpts.pth 的优化目录。" : backend == "tensorrt" ? "[提示] TensorRT 后端需要 PTH 源模型或预制 .engine；PTH 会按当前设备和输入尺寸自动编译。" : backend == "onnx" ? "[提示] ONNX 后端需要 models 或其子目录下的 .onnx 放大模型。" : "[提示] CUDA 后端需要 models 或其子目录下的 .pth/.pt/.pkl 放大模型。");
            var pth = backend == "basicvsrpp" ? DiscoverBasicVsrPlusPlusModels() : backend == "tensorrt" ? DiscoverTensorRTSelectableModels() : backend == "onnx" ? DiscoverOnnxModels() : DiscoverUpscalePthModels();
            if (pth.Count > 0)
            {
                Console.Error.WriteLine(backend == "tensorrt" ? "[提示] 可用 TensorRT 放大模型：" : backend == "onnx" ? "[提示] 可用 ONNX 放大模型：" : "[提示] 可用 CUDA 放大模型：");
                foreach (var m in pth)
                {
                    Console.Error.WriteLine("       " + UpscaleModelDisplayName(m, backend));
                }
            }
            else
            {
                Console.Error.WriteLine(backend == "tensorrt" ? "[提示] models 及其子目录下未找到 PTH 源模型或 .engine 文件。" : backend == "onnx" ? "[提示] models 及其子目录下未找到 .onnx 放大模型文件。" : "[提示] models 及其子目录下未找到 .pth 放大模型文件。");
            }
            Console.Error.WriteLine("[提示] 用法：-backend " + backend + " -modelpath <模型名>");
        }
        else
        {
            Console.Error.WriteLine("[提示] 可用模型（models 目录）：");
            foreach (var m in DiscoverModelFolders())
            {
                Console.Error.WriteLine("       " + UpscaleModelDisplayName(m, "ncnn"));
            }
            Console.Error.WriteLine("[提示] 用法：-modelpath <模型名或路径>，例如 -modelpath RealESRGAN-AnimeVideoV3-2x");
        }
        return "";
    }

    /// <summary>是否为 PyTorch 可加载的模型文件（.pth/.pt/.pkl）。</summary>
    private static bool IsPthModelFile(string path)
    {
        var ext = Path.GetExtension(path);
        return ext.Equals(".pth", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".pt", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".pkl", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTensorRTEngineFile(string path) =>
        Path.GetExtension(path).Equals(".engine", StringComparison.OrdinalIgnoreCase);

    private static bool IsOnnxModelFile(string path) =>
        Path.GetExtension(path).Equals(".onnx", StringComparison.OrdinalIgnoreCase);

    private static readonly string[] FlashVsrWeights =
    {
        "diffusion_pytorch_model_streaming_dmd.safetensors",
        "LQ_proj_in.ckpt",
        "TCDecoder.ckpt",
        "Wan2.1_VAE.pth",
    };

    private static bool IsFlashVsrModelDirectory(string path) =>
        Directory.Exists(path) && FlashVsrWeights.All(name => File.Exists(Path.Combine(path, name)));

    private static bool IsBasicVsrPlusPlusModelDirectory(string path) =>
        Directory.Exists(path)
        && File.Exists(Path.Combine(path, "config.py"))
        && File.Exists(Path.Combine(path, "chkpts.pth"))
        && IsInBasicVsrPlusPlusDirectory(path);

    private static bool IsBasicVsrPlusPlusModel(string path) =>
        IsBasicVsrPlusPlusModelDirectory(path)
        || (File.Exists(path) && IsPthModelFile(path) && IsInBasicVsrPlusPlusDirectory(path));

    private static string BasicVsrPlusPlusScale(string path) =>
        IsBasicVsrPlusPlusModelDirectory(path) ? "1" : "4";

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
        string? interpModel, string? interpFactor, string backend, string? backendScript = null, bool hdrMode = false,
        bool dynamicOpticalFlow = false, double sceneThreshold = 4.0, int tileSize = 0)
    {
        var args = new List<string>
        {
            string.IsNullOrWhiteSpace(backendScript) ? BackendScript : backendScript,
            "-i", input,
            "-o", outputFile,
            "-b", backend is "cuda" or "tensorrt" ? (backend == "tensorrt" ? "tensorrt" : "pytorch") : backend,
            "--precision", "auto",
            "--custom_encoder", " " + customEncoder + " ",
            "--tensorrt_opt_profile", "3",
            "--pytorch_gpu_id", "0",
            "--cwd", CoreRoot,
            "--ffmpeg_path", FfmpegExe,
        };
        if (backend is "cuda" or "tensorrt" or "onnx" or "flashvsr" or "basicvsrpp")
        {
            args.Add("--device");
            args.Add("cuda");
        }
        else
        {
            args.Add("--ncnn_gpu_id");
            args.Add("0");
        }

        if (hdrMode)
        {
            args.Add("--hdr_mode");
        }

        if (!string.IsNullOrEmpty(modelFolder))
        {
            args.Add("--upscale_model");
            args.Add(modelFolder);
            if (tileSize > 0 && backend is ("ncnn" or "cuda" or "tensorrt"))
            {
                args.Add("--tilesize");
                args.Add(tileSize.ToString(CultureInfo.InvariantCulture));
            }
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
        // 直接使用 RVE 官方外部阈值标尺；RVE 内部负责换算为模型阈值。
        args.Add(sceneThreshold.ToString("0.###", CultureInfo.InvariantCulture));
        if (dynamicOpticalFlow && backend == "cuda" && interpModel is not null)
        {
            args.Add("--dynamic_scaled_optical_flow");
        }

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

    /// <summary>未显式指定时，为补帧选择与现有模型格式匹配的后端。</summary>
    private static string DefaultInterpBackend(string upscaleBackend) =>
        upscaleBackend is "cuda" or "tensorrt" ? upscaleBackend : "ncnn";

    /// <summary>解析补帧模型路径：完整路径 / Frame-Interpolation 下相对路径 / 模型名；返回空串表示失败。</summary>
    /// <remarks>新目录按后端区分 NCNN 文件夹和 PyTorch 权重；TensorRT 由 RVE 从 RIFE 权重自动构建 Engine。</remarks>
    private static string ResolveInterpModel(string requested, string backend)
    {
        var raw = requested.Trim().Trim('"');
        var candidates = new List<string> { Path.GetFullPath(raw) };
        if (!Path.IsPathRooted(raw))
        {
            candidates.Add(Path.Combine(FrameInterpolationDir, raw));
            candidates.Add(Path.Combine(LegacyRifeDir, raw));
        }

        foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (backend == "cuda")
            {
                if (File.Exists(candidate) && IsPthModelFile(candidate))
                {
                    return candidate;
                }
            }
            else if (backend == "tensorrt")
            {
                if (File.Exists(candidate) && IsRifeInterpolationSource(candidate)) return candidate;
            }
            else if (Directory.Exists(candidate) && IsNcnnModelFolder(candidate))
            {
                return candidate;
            }
        }

        var normalizedRaw = raw.Replace('\\', '/').TrimEnd('/');
        var matched = DiscoverInterpModels(backend)
            .Where(path => InterpModelDisplayName(path).Equals(normalizedRaw, StringComparison.OrdinalIgnoreCase)
                           || Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
                               .Equals(Path.GetFileName(raw), StringComparison.OrdinalIgnoreCase)
                           || Path.GetFileNameWithoutExtension(path)
                               .Equals(Path.GetFileNameWithoutExtension(raw), StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (matched.Count == 1)
        {
            return matched[0];
        }
        if (matched.Count > 1)
        {
            Console.Error.WriteLine("[错误] 补帧模型名不唯一，请使用包含架构目录的相对路径：" + raw);
            return "";
        }

        Console.Error.WriteLine("[错误] 未找到可用补帧模型：" + raw);
        Console.Error.WriteLine(backend is "cuda" or "tensorrt"
            ? "[提示] 可用补帧模型（" + (backend == "tensorrt" ? "RIFE TensorRT 自动构建" : "CUDA/PyTorch") + "）："
            : @"[提示] 可用补帧模型（models\Frame-Interpolation）：");
        foreach (var m in DiscoverInterpModels(backend))
        {
            Console.Error.WriteLine("       " + InterpModelDisplayName(m));
        }
        Console.Error.WriteLine(backend is "cuda" or "tensorrt"
            ? "[提示] 用法：-interp-backend " + backend + " -interp-model <模型名>，例如 -interp-model rife46"
            : "[提示] 用法：-interp-model <模型名或路径>，例如 -interp-model rife-v4.25");
        return "";
    }

    /// <summary>发现补帧模型，并兼容读取旧 models\RIFE 目录。</summary>
    private static List<string> DiscoverInterpModels(string backend)
    {
        var roots = new[] { FrameInterpolationDir, LegacyRifeDir }
            .Where(Directory.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (roots.Count == 0)
        {
            return new List<string>();
        }
        if (backend == "cuda")
        {
            return roots.SelectMany(root => new[] { "*.pth", "*.pt", "*.pkl" }
                    .SelectMany(pattern => Directory.GetFiles(root, pattern, SearchOption.AllDirectories)))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(p => p, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }
        if (backend == "tensorrt")
        {
            // RVE 的补帧 TensorRT 路径以 RIFE PyTorch 权重为入口，并在权重旁按
            // 分辨率/GPU/TensorRT 版本自动生成缓存。生成的 .engine 不能反过来当模型输入。
            return roots.SelectMany(root => new[] { "*.pth", "*.pt", "*.pkl" }
                    .SelectMany(pattern => Directory.GetFiles(root, pattern, SearchOption.AllDirectories)))
                .Where(IsRifeInterpolationSource)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(p => p, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }
        var ncnnFolders = roots.SelectMany(root => Directory.GetDirectories(root, "*", SearchOption.AllDirectories))
            .Where(IsNcnnModelFolder)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var preferredNames = ncnnFolders
            .Where(path => IsPathUnder(path, FrameInterpolationDir))
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        // 新目录和旧 models\RIFE 可能同时存在同一套 NCNN 模型。优先新目录，
        // 只有新目录没有对应模型名时才显示旧兼容目录，避免 UI 出现重复项。
        return ncnnFolders
            .Where(path => IsPathUnder(path, FrameInterpolationDir)
                || !preferredNames.Contains(Path.GetFileName(path)))
            .OrderBy(p => p, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <summary>补帧模型显示为相对架构路径；旧 RIFE 目录仍沿用原来的短名称。</summary>
    private static string InterpModelDisplayName(string path)
    {
        string relative;
        if (IsPathUnder(path, FrameInterpolationDir))
        {
            relative = Path.GetRelativePath(FrameInterpolationDir, path);
        }
        else if (IsPathUnder(path, LegacyRifeDir))
        {
            relative = Path.GetRelativePath(LegacyRifeDir, path);
        }
        else
        {
            relative = Path.GetFileName(path);
        }
        var extension = Path.GetExtension(path);
        if (new[] { ".pth", ".pt", ".pkl", ".engine" }.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            relative = Path.ChangeExtension(relative, null) ?? relative;
        }
        return relative.Replace('\\', '/').TrimEnd('/');
    }

    /// <summary>只有 RIFE 权重具备当前 RVE 后端的 TensorRT 自动构建实现。</summary>
    private static bool IsRifeInterpolationSource(string path)
    {
        if (!File.Exists(path) || !IsPthModelFile(path)) return false;
        if (IsInLegacyRifeDirectory(path)) return true;

        var rifeRoot = Path.Combine(FrameInterpolationDir, "RIFE");
        if (IsPathUnder(path, rifeRoot)) return true;

        // 兼容用户直接放在 Frame-Interpolation 根目录的第三方 RIFE 权重。
        return IsPathUnder(path, FrameInterpolationDir)
            && Path.GetFileNameWithoutExtension(path).Contains("rife", StringComparison.OrdinalIgnoreCase);
    }

    private static int ListInterpModels(bool json, string backend)
    {
        var models = DiscoverInterpModels(backend);
        if (json)
        {
            var names = models.Select(InterpModelDisplayName).ToList();
            Console.WriteLine("[" + string.Join(",", names.Select(n => "\"" + n + "\"")) + "]");
            return 0;
        }
        Console.WriteLine(backend is "cuda" or "tensorrt"
            ? "可用补帧模型（" + (backend == "tensorrt" ? "RIFE TensorRT 自动构建" : "CUDA/PyTorch") + "，models\\Frame-Interpolation）："
            : @"可用补帧模型（models\Frame-Interpolation）：");
        if (models.Count == 0)
        {
            Console.WriteLine(backend is "cuda" or "tensorrt"
                ? "  (未找到兼容的补帧权重；请检查 models\\Frame-Interpolation 目录和所选后端)"
                : "  (未找到任何含 .param/.bin 的补帧模型文件夹)");
            return 0;
        }
        foreach (var m in models)
        {
            Console.WriteLine("  " + InterpModelDisplayName(m));
        }
        return 0;
    }

    private static readonly Regex OomHintRegex = new(
        @"MemoryError|Could not allocate bytes object|Out of memory|Cannot allocate|Unable to allocate",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex BackendFatalRegex = new(
        @"Traceback \(most recent call last\):|Exception in thread|VIDEOENHANCER_FATAL:|"
        + @"(?:ValueError|RuntimeError|AssertionError):|FFmpeg failed to render the video",
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
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Process32First(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
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

    /// <summary>
    /// 运行视频增强管线。同后端组合在单进程内按帧处理；后端格式不兼容时才使用 FFV1 无损中间视频。
    /// 这样既不把 NCNN RIFE 模型错误地交给 TensorRT/ONNX，也不让同后端任务产生整段临时视频。
    /// </summary>
    private static int RunVideoPipeline(
        string input, string outputFile, string model, string customEncoder, bool overwrite, string? scale,
        string pauseShm, StopWatcher? stopWatcher, string? interpModel, string? interpFactor,
        string upscaleBackend, string interpBackend, string processOrder, bool hdrMode,
        bool dynamicOpticalFlow, double sceneThreshold, int tileSize)
    {
        var useUpscale = !string.IsNullOrEmpty(model);
        var useInterp = interpModel is not null;
        if (!useUpscale || !useInterp)
        {
            var activeBackend = useUpscale ? upscaleBackend : interpBackend;
            var args = BuildBackendArgs(input, outputFile, model, customEncoder, overwrite, scale,
                pauseShm, interpModel, interpFactor, activeBackend, hdrMode: hdrMode,
                dynamicOpticalFlow: dynamicOpticalFlow, sceneThreshold: sceneThreshold, tileSize: tileSize);
            return LaunchBackend(args, input, model, outputFile, customEncoder, stopWatcher,
                interpModel, interpFactor, activeBackend, pauseShm, "单阶段处理", isFinalStage: true);
        }

        var upscaleFirst = processOrder == "upscale-first";
        Console.WriteLine(upscaleFirst
            ? "[处理顺序] 画质优先：先超分，再补帧。"
            : "[处理顺序] 速度/算力优先：先补帧，再超分。");

        // 同后端的先补后超正好是 rve-backend 原生顺序，无需中间文件。
        if (!upscaleFirst && upscaleBackend == interpBackend)
        {
            var args = BuildBackendArgs(input, outputFile, model, customEncoder, overwrite, scale,
                pauseShm, interpModel, interpFactor, upscaleBackend, hdrMode: hdrMode,
                dynamicOpticalFlow: dynamicOpticalFlow, sceneThreshold: sceneThreshold, tileSize: tileSize);
            return LaunchBackend(args, input, model, outputFile, customEncoder, stopWatcher,
                interpModel, interpFactor, upscaleBackend, pauseShm, "先补帧，再超分", isFinalStage: true);
        }

        // 同后端的先超后补通过内置包装器在同一进程内交换帧处理顺序，
        // 避免为了改变顺序把整段视频编码成临时文件。
        if (upscaleFirst && upscaleBackend == interpBackend)
        {
            var orderedBackend = EnsureEmbeddedTool(
                EmbeddedOrderedBackendResource, "rve-ordered-backend.py");
            var args = BuildBackendArgs(input, outputFile, model, customEncoder, overwrite, scale,
                pauseShm, interpModel, interpFactor, upscaleBackend, orderedBackend, hdrMode,
                dynamicOpticalFlow, sceneThreshold, tileSize);
            return LaunchBackend(args, input, model, outputFile, customEncoder, stopWatcher,
                interpModel, interpFactor, upscaleBackend, pauseShm, "先超分，再补帧", isFinalStage: true);
        }

        var outputDir = Path.GetDirectoryName(outputFile);
        if (string.IsNullOrWhiteSpace(outputDir)) outputDir = Environment.CurrentDirectory;
        Directory.CreateDirectory(outputDir);
        var intermediate = Path.Combine(outputDir,
            "." + Path.GetFileNameWithoutExtension(outputFile) + ".videoenhancer-" + Guid.NewGuid().ToString("N") + ".mkv");
        var intermediatePixelFormat = hdrMode ? "gbrp16le" : "gbrp10le";
        var losslessEncoder = "-c:v ffv1 -level 3 -coder 1 -context 1 -g 1 -pix_fmt " +
            intermediatePixelFormat + " -c:a copy -c:s copy";
        Console.WriteLine("[管线] 两种后端格式不兼容，必须跨进程传递中间视频；使用 " +
            intermediatePixelFormat + " RGB FFV1 无损编码并在完成后自动清理。");
        Console.WriteLine("[管线] 临时文件：" + intermediate);

        try
        {
            List<string> firstArgs;
            string firstModel;
            string? firstInterp;
            string firstBackend;
            string firstTitle;
            if (upscaleFirst)
            {
                firstModel = model;
                firstInterp = null;
                firstBackend = upscaleBackend;
                firstTitle = "阶段 1/2：超分";
                firstArgs = BuildBackendArgs(input, intermediate, model, losslessEncoder, true,
                    scale, pauseShm, null, null, upscaleBackend, hdrMode: hdrMode,
                    dynamicOpticalFlow: dynamicOpticalFlow, sceneThreshold: sceneThreshold, tileSize: tileSize);
            }
            else
            {
                firstModel = "";
                firstInterp = interpModel;
                firstBackend = interpBackend;
                firstTitle = "阶段 1/2：补帧";
                firstArgs = BuildBackendArgs(input, intermediate, "", losslessEncoder, true,
                    null, pauseShm, interpModel, interpFactor, interpBackend, hdrMode: hdrMode,
                    dynamicOpticalFlow: dynamicOpticalFlow, sceneThreshold: sceneThreshold);
            }
            var firstExit = LaunchBackend(firstArgs, input, firstModel, intermediate, losslessEncoder,
                stopWatcher, firstInterp, firstInterp is null ? null : interpFactor, firstBackend,
                pauseShm, firstTitle, isFinalStage: false);
            if (firstExit != 0) return firstExit;
            if (!File.Exists(intermediate) || new FileInfo(intermediate).Length == 0)
                return Fail("第一阶段未生成有效的无损中间视频：" + intermediate, 1);

            List<string> secondArgs;
            string secondModel;
            string? secondInterp;
            string secondBackend;
            string secondTitle;
            if (upscaleFirst)
            {
                secondModel = "";
                secondInterp = interpModel;
                secondBackend = interpBackend;
                secondTitle = "阶段 2/2：补帧";
                secondArgs = BuildBackendArgs(intermediate, outputFile, "", customEncoder, overwrite,
                    null, pauseShm, interpModel, interpFactor, interpBackend, hdrMode: hdrMode,
                    dynamicOpticalFlow: dynamicOpticalFlow, sceneThreshold: sceneThreshold);
            }
            else
            {
                secondModel = model;
                secondInterp = null;
                secondBackend = upscaleBackend;
                secondTitle = "阶段 2/2：超分";
                secondArgs = BuildBackendArgs(intermediate, outputFile, model, customEncoder, overwrite,
                    scale, pauseShm, null, null, upscaleBackend, hdrMode: hdrMode,
                    dynamicOpticalFlow: dynamicOpticalFlow, sceneThreshold: sceneThreshold, tileSize: tileSize);
            }
            return LaunchBackend(secondArgs, intermediate, secondModel, outputFile, customEncoder,
                stopWatcher, secondInterp, secondInterp is null ? null : interpFactor, secondBackend,
                pauseShm, secondTitle, isFinalStage: true);
        }
        finally
        {
            try
            {
                if (File.Exists(intermediate)) File.Delete(intermediate);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[警告] 无法清理无损中间视频：" + ex.Message);
            }
        }
    }

    /// <summary>根据视频流颜色传递函数识别 PQ/HLG HDR。</summary>
    private static bool DetectHdrMode(string input)
    {
        try
        {
            var result = RunProcessCapture(FfmpegExe, new[] { "-hide_banner", "-i", input }, 30);
            var detail = result.Output + "\n" + result.Error;
            return Regex.IsMatch(detail, @"smpte2084|arib-std-b67|\bhlg\b|\bpq\b",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }
        catch
        {
            // 无法读取元数据时保持 SDR 路径，避免误把普通视频当成 HDR。
            return false;
        }
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
        string? interpModel, string? interpFactor, string backend, string pauseShm, string stageTitle, bool isFinalStage)
    {
        Console.WriteLine();
        Console.WriteLine("[阶段] " + stageTitle);
        Console.WriteLine("[信息] 输入视频 : " + input);
        Console.WriteLine("[信息] 推理后端 : " + (backend == "basicvsrpp" ? "BasicVSR++（时序视频）" : backend == "flashvsr" ? "FlashVSR（时序视频）" : backend == "cuda" ? "CUDA（PyTorch）" : backend == "tensorrt" ? "TensorRT（NVIDIA）" : backend == "onnx" ? "ONNX Runtime" : "NCNN（Vulkan）"));
        if (string.IsNullOrEmpty(model))
        {
            Console.WriteLine("[信息] 放大模型 : （未使用，仅补帧）");
        }
        else
        {
            Console.WriteLine("[信息] 放大模型 : " + model);
            var scale = backend == "basicvsrpp" ? BasicVsrPlusPlusScale(model) : DetectScale(model);
            if (!string.IsNullOrEmpty(scale))
            {
                Console.WriteLine("[信息] 放大倍率 : " + scale + "x");
            }
        }
        if (interpModel is not null)
        {
            Console.WriteLine("[信息] 补帧模型 : " + interpModel);
            Console.WriteLine("[信息] 补帧倍率 : " + (interpFactor ?? "2") + "x");
            if (backend == "tensorrt")
            {
                Console.WriteLine("[TensorRT] RIFE 将按本阶段实际输入尺寸自动构建或复用 Engine 缓存。");
            }
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
        psi.Environment["PYTHONUTF8"] = "1";
        psi.Environment["PYTHONIOENCODING"] = "utf-8";
        // 先超后补的同后端包装器需要导入核心后端目录中的 src 包。
        psi.Environment["VIDEOENHANCER_BACKEND_DIR"] = Path.GetDirectoryName(BackendScript)!;
        foreach (var a in backendArgs)
        {
            psi.ArgumentList.Add(a);
        }

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        var job = CreateKillOnCloseJob();
        var throttle = new ProgressThrottle();
        var fpsTracker = new FpsTracker(pauseShm);
        var oomHintPrinted = false;
        var fatalBackendError = 0;

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
            line = fpsTracker.Rewrite(line);
            if (BackendFatalRegex.IsMatch(line))
            {
                Interlocked.Exchange(ref fatalBackendError, 1);
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
            fpsTracker.SamplePause();
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

        // 确保异步 stdout/stderr 回调全部排空，再判断是否出现后台线程异常。
        process.WaitForExit();

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

        var hasFatalBackendError = Volatile.Read(ref fatalBackendError) != 0;
        if (exitCode == 0 && !hasFatalBackendError)
        {
            Console.WriteLine(isFinalStage
                ? "[完成] 视频增强处理成功结束。"
                : "[阶段完成] " + stageTitle + " 已完成。");
            if (outputOk)
            {
                Console.WriteLine("[信息] 输出文件 : " + outputFile + " " + sizeText);
            }
            return 0;
        }

        if (hasFatalBackendError)
        {
            Console.Error.WriteLine("[失败] 后端报告渲染异常；已有输出可能不完整，不能作为成功结果使用。");
        }
        Console.WriteLine("[失败] rve-backend 退出码 " + exitCode + "，请查看上方错误信息。");
        return exitCode == 0 ? 1 : exitCode;
    }

    /// <summary>
    /// 把 PTH 源模型解析为当前 GPU、TensorRT 版本和输入尺寸对应的 Engine。
    /// 已有 Engine 会先验证；不兼容时若能找到同名 PTH，则自动重建本机缓存。
    /// </summary>
    private static string EnsureTensorRtEngine(string modelPath, int inputWidth, int inputHeight, StopWatcher? stopWatcher, int tileSize = 0)
    {
        var sourcePath = modelPath;
        if (IsTensorRTEngineFile(modelPath))
        {
            if (ValidateTensorRTEngine(modelPath, printSuccess: true)) return modelPath;
            var baseName = Path.GetFileNameWithoutExtension(modelPath);
            var cacheMarker = baseName.IndexOf("__gpu-", StringComparison.OrdinalIgnoreCase);
            if (cacheMarker > 0) baseName = baseName[..cacheMarker];
            sourcePath = DiscoverUpscalePthModels().FirstOrDefault(path =>
                ModelBaseName(path).Equals(baseName, StringComparison.OrdinalIgnoreCase)) ?? "";
            if (sourcePath.Length == 0)
            {
                Console.Error.WriteLine("[错误] Engine 与当前环境不兼容，且未找到同名 PTH 源模型，无法自动重建：" + baseName);
                Console.Error.WriteLine("[处理建议] 下载对应 PTH 模型后重试，程序会自动编译并缓存。");
                return "";
            }
            Console.WriteLine("[TensorRT] 已找到同名 PTH 源模型，将自动重建本机 Engine：" + sourcePath);
        }

        if (!IsPthModelFile(sourcePath) || !File.Exists(sourcePath))
        {
            Console.Error.WriteLine("[错误] TensorRT 自动构建需要有效的 PTH 源模型：" + sourcePath);
            return "";
        }
        if (!File.Exists(TensorRTConverterScript))
        {
            Console.Error.WriteLine("[错误] 缺少 TensorRT 自动构建脚本：" + TensorRTConverterScript);
            return "";
        }
        if (!TryGetTensorRtRuntime(out var runtime, out var runtimeError))
        {
            Console.Error.WriteLine("[错误] 无法读取 TensorRT 运行环境：" + runtimeError);
            return "";
        }

        Directory.CreateDirectory(TensorRTCacheDir);
        var cachePath = BuildTensorRtCachePath(sourcePath, runtime!, inputWidth, inputHeight, tileSize);
        var mutexName = "Local\\VideoEnhancer_TRT_" + Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(cachePath))).Substring(0, 24);
        using var buildMutex = new Mutex(false, mutexName);
        var hasMutex = false;
        try
        {
            Console.WriteLine("[TensorRT] 缓存键：" + Path.GetFileName(cachePath));
            while (!hasMutex)
            {
                if (stopWatcher?.IsStopRequested() == true)
                {
                    Console.WriteLine("[TensorRT] 已取消等待 Engine 构建。");
                    return "";
                }
                try { hasMutex = buildMutex.WaitOne(250); }
                catch (AbandonedMutexException) { hasMutex = true; }
            }

            if (File.Exists(cachePath))
            {
                Console.WriteLine("[TensorRT] 命中本机尺寸缓存，正在验证…");
                if (ValidateTensorRTEngine(cachePath, printSuccess: true)) return cachePath;
                Console.Error.WriteLine("[TensorRT] 缓存已失效，将自动重新构建。");
                try { File.Delete(cachePath); } catch { }
            }

            Console.WriteLine("[TensorRT] 未命中可用缓存，开始自动构建 Engine；首次使用可能需要数分钟。");
            Console.WriteLine("[TensorRT] GPU=" + runtime!.GpuName + "，TensorRT=" + runtime.TensorRtVersion +
                "，输入=" + inputWidth + "x" + inputHeight);
            var builtPath = RunTensorRtConverter(sourcePath, stopWatcher);
            if (builtPath.Length == 0) return "";

            var partialPath = Path.Combine(TensorRTCacheDir,
                Path.GetFileNameWithoutExtension(cachePath) + ".building-" + Guid.NewGuid().ToString("N") + ".engine");
            try
            {
                File.Copy(builtPath, partialPath, overwrite: true);
                if (!ValidateTensorRTEngine(partialPath, printSuccess: false))
                {
                    Console.Error.WriteLine("[错误] 自动构建完成，但 Engine 无法在当前 GPU 上反序列化，未写入缓存。");
                    return "";
                }
                File.Move(partialPath, cachePath, overwrite: true);
            }
            finally
            {
                try { if (File.Exists(partialPath)) File.Delete(partialPath); } catch { }
                var buildDir = Path.GetDirectoryName(builtPath);
                if (buildDir is not null && IsPathUnder(buildDir, TensorRTCacheDir)
                    && Path.GetFileName(buildDir).StartsWith(".build-", StringComparison.Ordinal))
                {
                    try { Directory.Delete(buildDir, recursive: true); } catch { }
                }
            }
            Console.WriteLine("[TensorRT] Engine 已写入本机缓存：" + cachePath);
            return cachePath;
        }
        finally
        {
            if (hasMutex) buildMutex.ReleaseMutex();
        }
    }

    /// <summary>查询便携 Python 中的 GPU 名称和 NVIDIA TensorRT 版本。</summary>
    private static bool TryGetTensorRtRuntime(out TensorRtRuntime? runtime, out string error)
    {
        runtime = null;
        const string script = "import torch, tensorrt as trt; " +
            "assert torch.cuda.is_available(), 'CUDA is unavailable'; " +
            "print('TRT_ENV|' + torch.cuda.get_device_name(0).replace('|','/') + '|' + str(trt.__version__))";
        var result = RunProcessCapture(PythonExe, new[] { "-c", script }, 60);
        var line = result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .LastOrDefault(value => value.StartsWith("TRT_ENV|", StringComparison.Ordinal));
        if (!result.Ok || line is null)
        {
            error = string.IsNullOrWhiteSpace(result.Error) ? "便携 Python 未返回 GPU/TensorRT 信息" : result.Error.Trim();
            return false;
        }
        var parts = line.Split('|');
        if (parts.Length < 3 || string.IsNullOrWhiteSpace(parts[1]) || string.IsNullOrWhiteSpace(parts[2]))
        {
            error = "GPU/TensorRT 信息格式无效：" + line;
            return false;
        }
        runtime = new TensorRtRuntime(parts[1].Trim(), parts[2].Trim());
        error = "";
        return true;
    }

    private static string BuildTensorRtCachePath(string sourcePath, TensorRtRuntime runtime, int width, int height, int tileSize)
    {
        using var stream = File.OpenRead(sourcePath);
        var sourceHash = Convert.ToHexString(SHA256.HashData(stream)).Substring(0, 12).ToLowerInvariant();
        var fileName = SafeCacheComponent(ModelBaseName(sourcePath), 72) +
            "__gpu-" + SafeCacheComponent(runtime.GpuName, 64) +
            "__trt-" + SafeCacheComponent(runtime.TensorRtVersion, 32) +
            "__input-" + width + "x" + height +
            "__tile-" + tileSize +
            "__src-" + sourceHash + ".engine";
        return Path.Combine(TensorRTCacheDir, fileName);
    }

    private static string SafeCacheComponent(string value, int maxLength)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);
        foreach (var ch in value.Trim())
        {
            builder.Append(char.IsWhiteSpace(ch) || invalid.Contains(ch) ? '-' : ch);
        }
        var result = Regex.Replace(builder.ToString(), "-+", "-").Trim('-', '.');
        if (result.Length == 0) result = "unknown";
        return result.Length <= maxLength ? result : result[..maxLength];
    }

    /// <summary>调用开发包自带转换器，并实时转发构建日志与停止请求。</summary>
    private static string RunTensorRtConverter(string sourcePath, StopWatcher? stopWatcher)
    {
        var buildDir = Path.Combine(TensorRTCacheDir, ".build-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(buildDir);
        var start = new ProcessStartInfo
        {
            FileName = PythonExe,
            WorkingDirectory = Path.GetDirectoryName(TensorRTConverterScript)!,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        start.Environment["PYTHONUTF8"] = "1";
        start.Environment["PYTHONIOENCODING"] = "utf-8";
        start.ArgumentList.Add(TensorRTConverterScript);
        start.ArgumentList.Add(sourcePath);
        start.ArgumentList.Add("--output-dir");
        start.ArgumentList.Add(buildDir);

        using var process = new Process { StartInfo = start };
        var job = CreateKillOnCloseJob();
        var cancelled = false;
        ConsoleCancelEventHandler cancelHandler = (_, e) => { e.Cancel = true; cancelled = true; };
        process.OutputDataReceived += (_, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) Console.WriteLine("[TensorRT 构建] " + e.Data); };
        process.ErrorDataReceived += (_, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) Console.Error.WriteLine("[TensorRT 构建] " + e.Data); };
        Console.CancelKeyPress += cancelHandler;
        try
        {
            if (!process.Start())
            {
                Console.Error.WriteLine("[错误] 无法启动 TensorRT 自动构建进程。");
                return "";
            }
            if (job != IntPtr.Zero) AssignProcessToJobObject(job, process.Handle);
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            while (!process.WaitForExit(250))
            {
                if (!cancelled && stopWatcher?.IsStopRequested() != true) continue;
                try { process.Kill(entireProcessTree: true); } catch { }
                process.WaitForExit();
                Console.WriteLine("[TensorRT] 自动构建已取消。");
                return "";
            }
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                Console.Error.WriteLine("[错误] TensorRT 自动构建失败，退出码：" + process.ExitCode);
                return "";
            }
            var engine = Directory.EnumerateFiles(buildDir, "*.engine", SearchOption.AllDirectories)
                .OrderByDescending(path => File.GetLastWriteTimeUtc(path)).FirstOrDefault();
            if (engine is null)
            {
                Console.Error.WriteLine("[错误] 转换器已退出，但没有生成 .engine 文件：" + buildDir);
                return "";
            }
            return engine;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("[错误] TensorRT 自动构建异常：" + ex.Message);
            return "";
        }
        finally
        {
            Console.CancelKeyPress -= cancelHandler;
            if (job != IntPtr.Zero) CloseHandle(job);
            if (!Directory.EnumerateFiles(buildDir, "*.engine", SearchOption.AllDirectories).Any())
            {
                try { Directory.Delete(buildDir, recursive: true); } catch { }
            }
        }
    }

    private static bool IsPathUnder(string path, string root)
    {
        var fullPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static bool ValidateTensorRTEngine(string enginePath, bool printSuccess)
    {
        if (!File.Exists(TensorRTValidatorScript))
        {
            Console.Error.WriteLine("[错误] 缺少 TensorRT Engine 验证脚本：" + TensorRTValidatorScript);
            return false;
        }
        var result = RunProcessCapture(
            PythonExe, new[] { TensorRTValidatorScript, "--engine", enginePath }, 120);
        var lines = (result.Output + "\n" + result.Error)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var line in lines)
        {
            if (line.StartsWith("ENGINE_VALID|", StringComparison.Ordinal))
            {
                if (printSuccess) Console.WriteLine("[TensorRT] 当前 GPU 已成功反序列化：" + Path.GetFileName(enginePath));
            }
            else if (line.StartsWith("ENGINE_INVALID|", StringComparison.Ordinal))
            {
                var parts = line.Split('|');
                var detail = parts.Length > 2 ? parts[2] : line;
                Console.Error.WriteLine("[TensorRT 不兼容] " + enginePath);
                Console.Error.WriteLine("[错误] " + detail);
                Console.Error.WriteLine("[处理建议] 该 engine 需要在当前 GPU 上重新编译。");
            }
        }
        if (!result.Ok && !lines.Any(line => line.StartsWith("ENGINE_INVALID|", StringComparison.Ordinal)))
        {
            Console.Error.WriteLine("[TensorRT 不兼容] " + enginePath);
            Console.Error.WriteLine("[处理建议] 该 engine 需要在当前 GPU 上重新编译。" +
                (string.IsNullOrWhiteSpace(result.Error) ? "" : " 原因：" + result.Error.Trim()));
        }
        return result.Ok;
    }

    private static int ValidateAllTensorRTEngines()
    {
        var engines = DiscoverTensorRTEngineModels();
        if (engines.Count == 0)
        {
            Console.WriteLine("[TensorRT] models 中没有找到 .engine 文件。");
            return 0;
        }
        var failures = 0;
        foreach (var engine in engines)
        {
            if (!ValidateTensorRTEngine(engine, printSuccess: true)) failures++;
        }
        Console.WriteLine($"[TensorRT] 验证完成：{engines.Count - failures} 个可加载，{failures} 个需要重新编译。");
        return failures == 0 ? 0 : 3;
    }

    private static int ListBackendsWithEngineValidation()
    {
        var result = RunProcessCapture(PythonExe,
            new[] { BackendScript, "--list_backends", "--engine_dir", ModelsDir }, 600);
        if (!string.IsNullOrWhiteSpace(result.Output)) Console.Write(result.Output);
        if (!string.IsNullOrWhiteSpace(result.Error)) Console.Error.Write(result.Error);
        return result.Ok ? 0 : 3;
    }

    private static bool RunCheck(bool verbose, string? backend = null)
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

        var models = DiscoverModelsForBackend(backend);
        var modelDescription = string.IsNullOrWhiteSpace(backend)
            ? "未找到支持的模型（NCNN .param/.bin、CUDA/TensorRT .pth/.engine、ONNX 或 FlashVSR）"
            : "未找到 " + backend + " 后端可用模型";
        Report(models.Count > 0, "模型库", ModelsDir,
            models.Count > 0 ? models.Count + " 个可用模型（" + (backend ?? "自动") + "）" : modelDescription);
        ok &= models.Count > 0;

        var interpModels = DiscoverInterpModels("ncnn");
        Report(true, "补帧模型库", FrameInterpolationDir,
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

            var checkTensorRtEngines = string.Equals(backend, "tensorrt", StringComparison.OrdinalIgnoreCase);
            var engineCount = checkTensorRtEngines ? DiscoverTensorRTEngineModels().Count : 0;
            if (engineCount > 0)
            {
                var engineStatus = ValidateAllTensorRTEngines();
                Report(engineStatus == 0, "TensorRT Engine 当前 GPU 反序列化", ModelsDir,
                    engineStatus == 0 ? engineCount + " 个 Engine 均可加载" : "存在不兼容 Engine；需要在当前 GPU 上重新编译");
                ok &= engineStatus == 0;
            }
        }

        Console.WriteLine("[环境检查] " + (ok ? "全部通过。" : "存在缺失项，请检查上方 [缺失] 标记。"));
        return ok;
    }

    /// <summary>按实际推理后端检查模型，避免 TensorRT 机器被 NCNN 文件格式误判。</summary>
    private static List<string> DiscoverModelsForBackend(string? backend)
    {
        if (string.IsNullOrWhiteSpace(backend))
        {
            return DiscoverModelFolders()
                .Concat(DiscoverUpscalePthModels())
                .Concat(DiscoverTensorRTEngineModels())
                .Concat(DiscoverOnnxModels())
                .Concat(DiscoverFlashVsrModels())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        return backend.ToLowerInvariant() switch
        {
            "ncnn" => DiscoverModelFolders(),
            "cuda" => DiscoverUpscalePthModels(),
            "tensorrt" => DiscoverTensorRTSelectableModels(),
            "onnx" => DiscoverOnnxModels(),
            "flashvsr" => DiscoverFlashVsrModels(),
            "basicvsrpp" => DiscoverBasicVsrPlusPlusModels(),
            _ => new List<string>(),
        };
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
            psi.Environment["PYTHONUTF8"] = "1";
            psi.Environment["PYTHONIOENCODING"] = "utf-8";
            foreach (var a in args)
            {
                psi.ArgumentList.Add(a);
            }
            using var p = Process.Start(psi);
            if (p is null)
            {
                return (false, "", "无法启动进程");
            }
            var stdoutTask = p.StandardOutput.ReadToEndAsync();
            var stderrTask = p.StandardError.ReadToEndAsync();
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
                p.WaitForExit();
                return (false, stdoutTask.GetAwaiter().GetResult(), "超时（" + timeoutSeconds + " 秒）");
            }
            var stdout = stdoutTask.GetAwaiter().GetResult();
            var stderr = stderrTask.GetAwaiter().GetResult();
            return (p.ExitCode == 0, stdout, stderr);
        }
        catch (Exception ex)
        {
            return (false, "", ex.Message);
        }
    }

    private static int ListModels(bool json, string backend)
    {
        var isCuda = backend == "cuda";
        var isTensorRT = backend == "tensorrt";
        var isOnnx = backend == "onnx";
        var isFlashVsr = backend == "flashvsr";
        var isBasicVsrPlusPlus = backend == "basicvsrpp";
        var models = isBasicVsrPlusPlus ? DiscoverBasicVsrPlusPlusModels() : isFlashVsr ? DiscoverFlashVsrModels() : isCuda ? DiscoverUpscalePthModels() : isTensorRT ? DiscoverTensorRTSelectableModels() : isOnnx ? DiscoverOnnxModels() : DiscoverModelFolders();
        string DisplayName(string path) => UpscaleModelDisplayName(path, backend);
        if (json)
        {
            // 机器可读：一行 JSON 数组（插件下拉框等调用方直接解析）
            var names = models.Select(DisplayName).ToList();
            Console.WriteLine("[" + string.Join(",", names.Select(n => "\"" + n + "\"")) + "]");
            return 0;
        }
        Console.WriteLine(isBasicVsrPlusPlus ? "可用 BasicVSR++ 时序视频模型："
            : isFlashVsr ? "可用 FlashVSR 时序视频模型："
            : isTensorRT
            ? "可用放大模型（TensorRT，PTH 首次使用自动构建本机 Engine）："
            : isOnnx ? "可用放大模型（ONNX Runtime，递归扫描 models 的 .onnx 文件）："
            : isCuda ? "可用放大模型（CUDA，递归扫描 models 的 .pth/.pt/.pkl 文件，不含补帧目录）："
            : "可用放大模型（NCNN，递归扫描 models 中含 .param/.bin 的文件夹，不含补帧目录）：");
        if (models.Count == 0)
        {
            Console.WriteLine(isTensorRT
                ? "  (未找到任何 PTH 源模型或预制 .engine 文件)"
                : isOnnx ? "  (未找到任何 .onnx 模型文件)"
                : isCuda ? "  (未找到任何 .pth/.pt/.pkl 模型文件；CUDA 放大需要 models 下的 .pth 模型)"
                : "  (未找到任何含 .param/.bin 的模型文件夹)");
            return 0;
        }
        foreach (var m in models)
        {
            var scale = isBasicVsrPlusPlus ? BasicVsrPlusPlusScale(m) : DetectScale(m);
            Console.WriteLine("  " + DisplayName(m) + (scale is null ? "" : "  (" + scale + "x)"));
        }
        return 0;
    }

    private static List<string> DiscoverModelFolders()
    {
        if (!Directory.Exists(ModelsDir))
        {
            return new List<string>();
        }
        return Directory.GetDirectories(ModelsDir, "*", SearchOption.AllDirectories)
            .Where(p => !IsInInterpolationDirectory(p))
            .Where(IsNcnnModelFolder)
            .OrderBy(p => p, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <summary>发现 CUDA 放大模型：递归扫描 models，但排除独立的补帧目录。</summary>
    private static List<string> DiscoverUpscalePthModels()
    {
        if (!Directory.Exists(ModelsDir))
        {
            return new List<string>();
        }
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pattern in new[] { "*.pth", "*.pt", "*.pkl" })
        {
            foreach (var f in Directory.GetFiles(ModelsDir, pattern, SearchOption.AllDirectories)
                         .Where(p => !IsInInterpolationDirectory(p) && !IsInFlashVsrDirectory(p) && !IsInBasicVsrPlusPlusDirectory(p)))
            {
                set.Add(f);
            }
        }
        return set.OrderBy(p => p, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    private static List<string> DiscoverTensorRTEngineModels()
    {
        if (!Directory.Exists(ModelsDir)) return new List<string>();
        return Directory.GetFiles(ModelsDir, "*.engine", SearchOption.AllDirectories)
            .OrderBy(p => p, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    /// <summary>
    /// TensorRT 下拉框展示 PTH 源模型和非缓存预制 Engine；自动缓存目录不重复展示。
    /// 同名时优先展示预制 Engine，运行时若不兼容会自动寻找同名 PTH 重建。
    /// </summary>
    private static List<string> DiscoverTensorRTSelectableModels()
    {
        var engines = DiscoverTensorRTEngineModels()
            .Where(path => !IsPathUnder(path, TensorRTCacheDir))
            .Where(path => !IsInInterpolationDirectory(path));
        return engines.Concat(DiscoverUpscalePthModels())
            .GroupBy(path => UpscaleModelDisplayName(path, "tensorrt"), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(path => UpscaleModelDisplayName(path, "tensorrt"), StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static List<string> DiscoverOnnxModels()
    {
        if (!Directory.Exists(ModelsDir)) return new List<string>();
        return Directory.GetFiles(ModelsDir, "*.onnx", SearchOption.AllDirectories)
            .Where(p => !IsInInterpolationDirectory(p))
            .OrderBy(p => p, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    private static List<string> DiscoverFlashVsrModels()
    {
        if (!Directory.Exists(ModelsDir)) return new List<string>();
        return Directory.GetDirectories(ModelsDir, "*", SearchOption.AllDirectories)
            .Prepend(ModelsDir)
            .Where(IsFlashVsrModelDirectory)
            .OrderBy(p => p, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static List<string> DiscoverBasicVsrPlusPlusModels()
    {
        if (!Directory.Exists(ModelsDir)) return new List<string>();
        var optimizedDirectories = Directory.GetDirectories(ModelsDir, "*", SearchOption.AllDirectories)
            .Where(IsBasicVsrPlusPlusModelDirectory)
            .ToList();
        var officialWeights = Directory.GetFiles(ModelsDir, "*.pth", SearchOption.AllDirectories)
            .Where(IsInBasicVsrPlusPlusDirectory)
            .Where(path => !optimizedDirectories.Any(directory => IsPathUnder(path, directory)));
        return optimizedDirectories.Concat(officialWeights)
            .OrderBy(p => p, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <summary>TensorRT 模型显示为相对 models 的无扩展名路径，避免子目录中同名模型冲突。</summary>
    private static string TensorRTEngineDisplayName(string path)
    {
        return RelativeModelDisplayName(path, removeExtension: true);
    }

    /// <summary>超分模型显示为相对 models 的路径，避免分类目录中的同名模型冲突。</summary>
    private static string UpscaleModelDisplayName(string path, string backend)
    {
        var removeExtension = File.Exists(path) && backend is ("cuda" or "tensorrt" or "onnx" or "basicvsrpp");
        return RelativeModelDisplayName(path, removeExtension);
    }

    private static string RelativeModelDisplayName(string path, bool removeExtension)
    {
        var relative = Path.GetRelativePath(ModelsDir, path);
        if (removeExtension)
        {
            relative = Path.ChangeExtension(relative, null) ?? relative;
        }
        return relative.Replace('\\', '/');
    }

    private static string ModelBaseName(string path)
    {
        return File.Exists(path)
            ? Path.GetFileNameWithoutExtension(path)
            : Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
    }

    private static bool IsInInterpolationDirectory(string path) =>
        IsPathUnder(path, FrameInterpolationDir) || IsInLegacyRifeDirectory(path);

    private static bool IsInLegacyRifeDirectory(string path)
    {
        var rifeRoot = Path.GetFullPath(LegacyRifeDir)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(path);
        return fullPath.StartsWith(rifeRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsInBasicVsrPlusPlusDirectory(string path)
    {
        var root = Path.GetFullPath(Path.Combine(ModelsDir, "BasicVSR++"))
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        return Path.GetFullPath(path).StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsInFlashVsrDirectory(string path)
    {
        var root = Path.GetFullPath(Path.Combine(ModelsDir, "FlashVSR"))
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        return Path.GetFullPath(path).StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }

    private static string? FindNcnnModelFolder(string modelName)
    {
        return DiscoverModelFolders().FirstOrDefault(p =>
            ModelBaseName(p).Equals(modelName, StringComparison.OrdinalIgnoreCase));
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
        writer.WriteLine("videoenhancer.exe — 视频/图片超分辨率命令行工具  v" + ToolVersion);
        writer.WriteLine("============================================================");
        writer.WriteLine("用法");
        writer.WriteLine("  videoenhancer.exe -i <输入视频> -modelpath <模型目录> -ffmpeg-settings \"<FFmpeg 参数 + 输出路径>\"");
        writer.WriteLine("  videoenhancer.exe -i <输入视频> -interp-model <补帧模型> [-no-upscale] -ffmpeg-settings \"<FFmpeg 参数 + 输出路径>\"");
        writer.WriteLine("  videoenhancer.exe -i <输入视频> -no-upscale -backend cuda -interp-model <CUDA 补帧模型> -ffmpeg-settings \"<FFmpeg 参数 + 输出路径>\"");
        writer.WriteLine("  videoenhancer.exe --image-input <图片> --image-output <文件夹> -backend onnx -modelpath <模型>");
        writer.WriteLine("  videoenhancer.exe --image-folder <文件夹> --image-output-original -modelpath <模型>");
        writer.WriteLine("  videoenhancer.exe --list-download-models --json");
        writer.WriteLine("  videoenhancer.exe --clean-download-archives");
        writer.WriteLine("  videoenhancer.exe --download-model <镜像相对路径>");
        writer.WriteLine("  videoenhancer.exe --download-url <链接> --download-output <文件>");
        writer.WriteLine("  videoenhancer.exe --extract-archive <压缩包> [--extract-output <目录>]");
        writer.WriteLine("  videoenhancer.exe --version");
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
        writer.WriteLine("  -interp-model <路径>  补帧模型：完整路径、models\\Frame-Interpolation 下的相对路径或模型名");
        writer.WriteLine("        （如 RIFE/rife-v4.25、GIMM-VFI/gimm-vfi）；旧 models\\RIFE 目录仍可读取；");
        writer.WriteLine("        CUDA 使用 .pth/.pt/.pkl；TensorRT 使用 RIFE 权重自动构建 Engine；NCNN 使用 .param/.bin 文件夹");
        writer.WriteLine("  -interp-factor <N>  补帧倍率（帧率倍数，默认 2，需大于 1）");
        writer.WriteLine("  -process-order <upscale-first|interp-first>  组合处理顺序；默认 upscale-first");
        writer.WriteLine("        画质优先：先超分，再补帧。速度/算力优先：先补帧，再超分。");
        writer.WriteLine("  -interp-backend <ncnn|cuda|tensorrt>  可选的独立补帧后端；RIFE 实际支持 NCNN、CUDA/PyTorch、TensorRT");
        writer.WriteLine("  -scene-threshold <N>  转场检测阈值（RVE 官方外部 0-10 标尺；数值越低越敏感，默认 4）");
        writer.WriteLine("  -dynamic-optical-flow  开启 RIFE 动态光流尺度（仅 CUDA/PyTorch 补帧有效）");
        writer.WriteLine("  -tile-size <N>  超分分块边长（0 为 RVE 默认；至少 32；仅 NCNN/CUDA/TensorRT）");
        writer.WriteLine("  -backend <ncnn|cuda|tensorrt|onnx|flashvsr|basicvsrpp>  超分推理后端；");
        writer.WriteLine("        basicvsrpp 支持官方 x4 PTH，及 config.py/chkpts.pth 的 1x 优化目录；");
        writer.WriteLine("        所有后端均递归扫描 models 子目录；Frame-Interpolation 仅用于补帧；");
        writer.WriteLine("        cuda 使用 .pth/.pt/.pkl；tensorrt 接受 PTH 或 .engine，缺少缓存时会自动构建；");
        writer.WriteLine("        TensorRT 缓存名包含 GPU、TensorRT 版本、输入尺寸和源模型摘要；onnx 使用 .onnx；");
        writer.WriteLine("        超分与补帧可同时指定；同一后端逐帧执行，跨后端才使用 FFV1 无损中间视频；");
        writer.WriteLine("        SDR 内部为 8-bit RGB；PQ/HLG 使用 16-bit RGB，且仅支持 CUDA/PyTorch 或 TensorRT");
        writer.WriteLine("  -no-upscale         不放大（仅补帧模式，需配合 -interp-model）");
        writer.WriteLine("  -pause-shm <ID>     暂停共享内存名（透传给 rve-backend --pause_shared_memory_id）");
        writer.WriteLine("  -stop-shm <ID>      停止共享内存名：字节变 1 时优雅停止，已处理部分写入输出文件");
        writer.WriteLine("  --list-models, --search-models  列出可用的放大模型并退出（默认 ncnn 文件夹）");
        writer.WriteLine("        各后端均递归列出 models 子目录中的对应放大模型（排除补帧目录）；");
        writer.WriteLine("        （配合 --json 输出一行 JSON 数组，供界面程序解析）");
        writer.WriteLine("  --list-interp-models  列出 models\\Frame-Interpolation 下可用的补帧模型并退出");
        writer.WriteLine("        （配合 --json 输出一行 JSON 数组，供界面程序解析）；");
        writer.WriteLine("        加 -interp-backend cuda 列出全部 PyTorch 权重，tensorrt 只列 RIFE 权重；旧 RIFE 兼容读取");
        writer.WriteLine("  --check             仅检测运行环境（ffmpeg / python 库 / 模型库）并退出");
        writer.WriteLine("  --list-backends     列出后端，并逐个在当前 GPU 上反序列化 models 中的 TensorRT Engine");
        writer.WriteLine("  --validate-engines  递归验证全部 .engine；不兼容时提示在当前 GPU 上重新编译");
        writer.WriteLine("  --list-download-models  从 ModelScope 镜像读取可下载文件；配合 --json 输出给界面");
        writer.WriteLine("        默认仓库：" + DefaultModelScopeDataset);
        writer.WriteLine("        VIDEOENHANCER_MODELSCOPE_DATASET 可覆盖仓库 ID；私有仓库需设置");
        writer.WriteLine("        VIDEOENHANCER_MODELSCOPE_TOKEN 或 MODELSCOPE_API_TOKEN（不会写入配置文件）");
        writer.WriteLine("  --clean-download-archives  递归清理 models 与 python 中的下载压缩包");
        writer.WriteLine("  --download-model <路径>  用内置 aria2-next 下载镜像文件；压缩包自动用内置 7-Zip-zstd 解压");
        writer.WriteLine("  --download-url <链接> --download-output <文件>  使用内置 aria2-next 下载任意直链");
        writer.WriteLine("  --extract-archive <文件> [--extract-output <目录>]  使用内置 7-Zip-zstd 解压");
        writer.WriteLine("  --image-input <文件>  添加一个图片输入（可重复）");
        writer.WriteLine("  --image-folder <目录>  递归添加目录及其子目录图片（可重复）");
        writer.WriteLine("  --image-output <目录>  指定图片输出目录；或用 --image-output-original 输出到原目录");
        writer.WriteLine("  --image-suffix <timestamp|model>  文件名附加处理时间戳或模型名称");
        writer.WriteLine("  --image-png / --image-source-format  输出无损 PNG（默认）或保持源扩展格式");
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
        writer.WriteLine("  · 放大模型递归扫描 models：NCNN 取 .param/.bin 文件夹，CUDA 取 .pth/.pt/.pkl，");
        writer.WriteLine("    TensorRT 取 PTH 源模型或预制 .engine，并把自动构建结果写入 models\\TensorRT-Cache；");
        writer.WriteLine("    models\\Frame-Interpolation 独立保留给补帧模型，旧 models\\RIFE 兼容读取；");
        writer.WriteLine("    （rve-backend 的 spandrel/InterpolateRIFE 加载）。");
    }
}












