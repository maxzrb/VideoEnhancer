using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.IO.MemoryMappedFiles;
using System.IO.Pipes;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32;

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
    private const string EmbeddedThirdPartyNoticesResource = "VideoEnhancer.Embedded.THIRD-PARTY-NOTICES.txt";
    private const string EmbeddedProjectLicenseResource = "VideoEnhancer.Embedded.LICENSE.txt";
    private const string EmbeddedSharpCompressLicenseResource = "VideoEnhancer.Embedded.SharpCompress.LICENSE.txt";
    private const string EmbeddedOrderedBackendResource = "VideoEnhancer.Embedded.rve-ordered-backend.py";
    private const string EmbeddedInterpolationInspectorResource = "VideoEnhancer.Embedded.inspect_interpolation_models.py";
    private const string EmbeddedUpscaleInspectorResource = "VideoEnhancer.Embedded.inspect_upscale_models.py";
    private const string EmbeddedRifeTensorRTPrepareResource = "VideoEnhancer.Embedded.prepare_rife_tensorrt.py";
    private const string EmbeddedTensorRTConverterResource = "VideoEnhancer.Embedded.convert_tensorrt.py";
    private const string EmbeddedImageBackendResource = "VideoEnhancer.Embedded.rve-image-backend.py";
    private const string EmbeddedSegmentedBackendResource = "VideoEnhancer.Embedded.rve-segmented-backend.py";
    private const int InterpolationCapabilityCacheVersion = 1;
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

    // CoreRoot 永远是 videoenhancer.exe 所在目录，不读取 INI 或用户目录配置。
    private static readonly string CoreRoot = PortablePaths.CoreRoot;

    private static string PythonExe => Path.Combine(CoreRoot, "python", "python", "python.exe");
    private static string BackendScript => Path.Combine(CoreRoot, "python", "backend", "rve-backend.py");
    private static string ImageBackendScript => Path.Combine(CoreRoot, "python", "backend", "rve-image-backend.py");
    private static string SegmentedBackendScript => Path.Combine(CoreRoot, "python", "backend", "rve-segmented-backend.py");
    private static string TensorRTValidatorScript => Path.Combine(CoreRoot, "python", "backend", "validate_tensorrt_engines.py");
    private static string TensorRTConverterScript => Path.Combine(CoreRoot, "python", "backend", "convert_tensorrt.py");
    private static string InterpolationInspectorScript => Path.Combine(CoreRoot, "python", "backend", "inspect_interpolation_models.py");
    private static string UpscaleInspectorScript => Path.Combine(CoreRoot, "python", "backend", "inspect_upscale_models.py");
    private static string RifeTensorRTPrepareScript => Path.Combine(CoreRoot, "python", "backend", "prepare_rife_tensorrt.py");
    private static string Aria2NextExe => Path.Combine(CoreRoot, "bin", "aria2-next", "aria2-next.exe");
    // 最终编码复用 3FUI 的 FFmpeg：显式覆盖除外，先查 3FUI 工作目录，再查其他路径。
    private static string FfmpegExe => Resolve3FuiFfmpegTool("ffmpeg.exe");
    private static string FfprobeExe => Resolve3FuiFfmpegTool("ffprobe.exe");
    private static string FfmpegPathOverride = "";
    private static string FfprobePathOverride = "";
    private static string RtxVideoBackendExe => RtxVideoBackendClient.FindBackend(CoreRoot);
    private static string ModelsDir => Path.Combine(CoreRoot, "models");
    private static string FrameInterpolationDir => Path.Combine(ModelsDir, "Frame-Interpolation");
    private static string UserInterpolationDir => Path.Combine(ModelsDir, "User", "Interpolation");
    private static string UserRestorationDir => Path.Combine(ModelsDir, "User", "Restoration");
    private static string LegacyRifeDir => Path.Combine(ModelsDir, "RIFE");
    private static string TensorRTCacheDir => Path.Combine(ModelsDir, "TensorRT-Cache");
    private static string SceneDetectModel => FindNcnnModelFolder("EfficientNet-SceneDetect")
        ?? Path.Combine(ModelsDir, "EfficientNet-SceneDetect");
    private static string DefaultModel => Path.Combine(ModelsDir, "RealESRGAN-AnimeVideoV3-2x");
    private static string PythonSitePackages => Path.Combine(CoreRoot, "python", "python", "Lib", "site-packages");

    private static string Resolve3FuiFfmpegTool(string fileName)
    {
        var candidates = new List<string>();
        var suppliedPath = fileName.Equals("ffmpeg.exe", StringComparison.OrdinalIgnoreCase)
            ? FfmpegPathOverride : FfprobePathOverride;
        if (!string.IsNullOrWhiteSpace(suppliedPath)) candidates.Add(suppliedPath);
        var explicitFfmpeg = Environment.GetEnvironmentVariable("VIDEOENHANCER_FFMPEG")?.Trim().Trim('"');
        if (!string.IsNullOrWhiteSpace(explicitFfmpeg))
        {
            candidates.Add(fileName.Equals("ffmpeg.exe", StringComparison.OrdinalIgnoreCase)
                ? explicitFfmpeg
                : Path.Combine(Path.GetDirectoryName(explicitFfmpeg) ?? "", fileName));
        }
        try
        {
            // 标准安装：<3FUI>\Plugin\videoenhancer，因此向上两级就是宿主 EXE 目录。
            var hostRoot = Path.GetFullPath(Path.Combine(CoreRoot, "..", ".."));
            var settingsPath = Path.Combine(hostRoot, "Settings.json");
            if (File.Exists(settingsPath))
            {
                using var settings = JsonDocument.Parse(File.ReadAllText(settingsPath));
                if (settings.RootElement.TryGetProperty("工作目录", out var configured) &&
                    configured.ValueKind == JsonValueKind.String)
                {
                    var workingDirectory = configured.GetString()?.Trim();
                    if (!string.IsNullOrWhiteSpace(workingDirectory) && Directory.Exists(workingDirectory))
                        candidates.Add(Path.Combine(workingDirectory, fileName));
                }
            }
            candidates.Add(Path.Combine(hostRoot, fileName));
        }
        catch
        {
            // 设置文件无效或目录异常时继续按宿主目录、PATH 与旧版目录解析。
            try { candidates.Add(Path.Combine(Path.GetFullPath(Path.Combine(CoreRoot, "..", "..")), fileName)); }
            catch { }
        }
        candidates.Add(Path.Combine(Environment.CurrentDirectory, fileName));
        var pathValue = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var rawDirectory in pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var directory = rawDirectory.Trim().Trim('"');
            if (directory.Length > 0) candidates.Add(Path.Combine(directory, fileName));
        }
        var legacyFallback = Path.Combine(CoreRoot, "bin", "ffmpeg", fileName);
        candidates.Add(legacyFallback);
        foreach (var candidate in candidates)
        {
            try
            {
                var fullPath = Path.GetFullPath(candidate);
                if (File.Exists(fullPath)) return fullPath;
            }
            catch
            {
                // PATH 可能包含无效目录；跳过该项并继续查找其余 3FUI 环境。
            }
        }
        return legacyFallback;
    }
    private static string InterpolationCapabilityCachePath =>
        Path.Combine(PortablePaths.CacheRoot,
            $"interpolation-capabilities-v{InterpolationCapabilityCacheVersion}.json");

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

    [DllImport("ntdll.dll")]
    private static extern uint NtSuspendProcess(IntPtr processHandle);

    [DllImport("ntdll.dll")]
    private static extern uint NtResumeProcess(IntPtr processHandle);

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

    private static string NormaliseRtxTarget(string value)
    {
        var target = value.Trim().ToLowerInvariant();
        if (Regex.IsMatch(target, @"^(1(?:\.5)?|2|3|4)x$", RegexOptions.CultureInvariant)
            || target is "1080p" or "1440p" or "2160p" or "4320p")
            return target;
        Fail("-rtx-target 仅支持 1x、1.5x、2x、3x、4x、1080p、1440p、2160p 或 4320p，当前值：" + value);
        return "";
    }

    private static double ResolveRtxScale(string input, string target)
    {
        if (target.EndsWith('x')
            && double.TryParse(target[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var direct))
            return Math.Clamp(direct, 1.0, 4.0);

        var (width, height) = GetInputResolution(input);
        if (width <= 0 || height <= 0) throw new InvalidOperationException("RTX VSR 无法探测输入分辨率：" + input);
        var reference = width >= height ? height : width;
        var requested = target switch
        {
            "1080p" => 1080,
            "1440p" => 1440,
            "2160p" => 2160,
            "4320p" => 4320,
            _ => throw new InvalidOperationException("未知 RTX VSR 输出规格：" + target),
        };
        return Math.Clamp((double)requested / reference, 1.0, 4.0);
    }

    private static string SelectRtxCodec(string customEncoder, bool hdr)
    {
        var lower = customEncoder.ToLowerInvariant();
        // sidecar 仅输出 RTX 处理后的原始帧，最终编码器由 3FUI FFmpeg 打开；
        // 因此这里识别编码族只用于 10-bit/HDR 参数校验，不能再依赖 sidecar 的 NVENC 能力。
        if (lower.Contains("av1", StringComparison.Ordinal)) return "av1";
        if (lower.Contains("hevc", StringComparison.Ordinal)
            || lower.Contains("h265", StringComparison.Ordinal)
            || lower.Contains("x265", StringComparison.Ordinal)) return "hevc";
        if (lower.Contains("h264", StringComparison.Ordinal)
            || lower.Contains("avc", StringComparison.Ordinal)
            || lower.Contains("x264", StringComparison.Ordinal))
        {
            if (hdr) throw new InvalidOperationException("RTX HDR 需要 10-bit HEVC 或 AV1 编码器，不能使用 H.264");
            return "h264";
        }
        return hdr ? "hevc" : "h264";
    }

    private static int RunVideoWithRtx(
        string input, string outputFile, string model, string customEncoder, string originalFfmpegSettings,
        bool overwrite, string? scale,
        string pauseShm, StopWatcher? stopWatcher, string? interpModel, string? interpFactor,
        string upscaleBackend, string interpBackend, string processOrder, bool hdrMode, bool dynamicOpticalFlow,
        double sceneThreshold, int tileSize, string requestedUpscalePrecision, string requestedInterpPrecision,
        bool rtxVsr, bool rtxHdr, string rtxTarget, int rtxQuality,
        int rtxHdrContrast, int rtxHdrSaturation, int rtxHdrMiddleGray, int rtxHdrMaxLuminance)
    {
        var outputDir = Path.GetDirectoryName(outputFile);
        if (string.IsNullOrWhiteSpace(outputDir)) outputDir = Environment.CurrentDirectory;
        Directory.CreateDirectory(outputDir);
        if (File.Exists(outputFile) && !overwrite) return Fail("输出文件已存在；请在 FFmpeg 参数中加入 -y 允许覆盖：" + outputFile, 1);
        var gracefulStopMarker = outputFile + ".videoenhancer-stop-ok";
        try { if (File.Exists(gracefulStopMarker)) File.Delete(gracefulStopMarker); } catch { }

        var rveInput = input;
        string? intermediate = null;
        var outputTouched = false;
        var finalOutputCompleted = false;
        var useRegularUpscale = !rtxVsr && !string.IsNullOrEmpty(model);
        var useRve = useRegularUpscale || interpModel is not null;
        if (useRve)
        {
            intermediate = PortablePaths.CreateWorkFilePath("rtx-input", ".mkv");
            // sidecar 只支持 D3D11VA 硬解，FFV1 没有硬件解码器会在解码首包直接失败；
            // 中间文件改用数学无损 HEVC Main10：RVE 输出的 RGB 中间帧落为 10-bit 4:2:0，
            // 相对最终 NVENC Main10 编码没有额外精度损失，且任意 RTX 机器都能硬解。
            // HDR 输入时通过 x265 VUI 写入 BT.2020/PQ 标记，sidecar 才能按 HDR 咬合色彩空间。
            var x265Params = hdrMode
                ? "lossless=1:colorprim=bt2020:transfer=smpte2084:colormatrix=bt2020nc"
                : "lossless=1";
            var losslessEncoder = "-c:v libx265 -preset ultrafast -x265-params " + x265Params
                + " -pix_fmt yuv420p10le -c:a copy -c:s copy";
            var rveModel = useRegularUpscale ? model : "";
            var forcedOrder = rtxVsr ? "interp-first" : processOrder;
            if (rtxVsr && interpModel is not null)
                Console.WriteLine("[处理顺序] RTX VSR 与补帧组合固定为：先补帧，再 RTX 超分。");
            var exit = RunVideoPipeline(input, intermediate, rveModel, losslessEncoder, true,
                useRegularUpscale ? scale : null, pauseShm, stopWatcher, interpModel, interpFactor,
                useRegularUpscale ? upscaleBackend : interpBackend, interpBackend, forcedOrder, hdrMode,
                dynamicOpticalFlow, sceneThreshold, tileSize, requestedUpscalePrecision, requestedInterpPrecision);
            if (exit != 0) return exit;
            if (!File.Exists(intermediate) || new FileInfo(intermediate).Length == 0)
                return Fail("RTX 前置阶段未生成有效的无损中间视频：" + intermediate, 1);
            rveInput = intermediate;
        }

        try
        {
            using var cancellation = new CancellationTokenSource();
            using var client = RtxVideoBackendClient.StartAsync(RtxVideoBackendExe, cancellation.Token).GetAwaiter().GetResult();
            var capabilities = client.GetCapabilitiesAsync(cancellation.Token).GetAwaiter().GetResult();
            if (!capabilities.D3d11Available || !capabilities.RtxSdkFound)
                return Fail("RTX Video 运行环境不可用：" + string.Join("；", capabilities.Messages), 1);
            if (rtxVsr && !capabilities.VsrAvailable) return Fail("当前 GPU/驱动不支持 RTX VSR", 1);
            if (rtxHdr && !capabilities.TruehdrAvailable) return Fail("当前 GPU/驱动不支持 RTX Video HDR", 1);

            var resolvedScale = rtxVsr ? ResolveRtxScale(rveInput, rtxTarget) : 1.0;
            var (sourceWidth, sourceHeight) = GetInputResolution(rveInput);
            var outputWidth = Math.Max(2, (int)Math.Round(sourceWidth * resolvedScale));
            var outputHeight = Math.Max(2, (int)Math.Round(sourceHeight * resolvedScale));
            if ((outputWidth & 1) != 0) outputWidth++;
            if ((outputHeight & 1) != 0) outputHeight++;
            if (rtxVsr)
            {
                Console.WriteLine($"[RTX VSR] 输出映射：{sourceWidth}x{sourceHeight} × {resolvedScale.ToString("0.###", CultureInfo.InvariantCulture)} → {outputWidth}x{outputHeight}；质量 {rtxQuality}");
            }
            var codec = SelectRtxCodec(customEncoder, rtxHdr);
            var encoderOptions = ParseRtxEncoderOptions(customEncoder);
            var pixelFormat = ParseRtxPixelFormat(customEncoder, rtxHdr);
            if (pixelFormat == "p010le"
                && customEncoder.Contains("h264_nvenc", StringComparison.OrdinalIgnoreCase))
                return Fail("H.264 NVENC does not support the requested 10-bit output", 1);
            // SplitFfmpegSettings 会剥掉 -map；RTX 的宿主 FFmpeg 映射必须从原始设置解析。
            var (audioStreamIndices, subtitleStreamIndices) = ParseRtxStreamSelection(originalFfmpegSettings);
            if (encoderOptions.Count > 0)
            {
                Console.WriteLine("[RTX Video] 3FUI 编码参数：" + string.Join("，", encoderOptions.Select(o => o.Key + "=" + o.Value)));
            }
            // RTX SDK 只产出处理后的原始帧；最终编码、映射和封装统一交回 3FUI FFmpeg。
            outputTouched = true;
            if (overwrite && File.Exists(outputFile)) File.Delete(outputFile);
            var pipeName = "videoenhancer-rtx-" + Guid.NewGuid().ToString("N");
            var pipePath = @"\\.\pipe\" + pipeName;
            var rawPixelFormat = rtxHdr ? "x2bgr10le" : pixelFormat == "p010le" ? "p010le" : "nv12";
            var frameRate = GetInputFrameRate(rveInput);
            using var framePipe = new NamedPipeServerStream(
                pipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous, 4 * 1024 * 1024, 4 * 1024 * 1024);
            using var ffmpeg = StartRtxHostEncoder(
                input, outputFile, customEncoder, overwrite, rawPixelFormat,
                outputWidth, outputHeight, frameRate, rtxHdr,
                audioStreamIndices, subtitleStreamIndices);
            Console.WriteLine("[RTX Video] 最终编码：3FUI FFmpeg（" + ffmpeg.StartInfo.FileName + "）");
            var ffmpegErrorTask = ffmpeg.StandardError.ReadToEndAsync();
            using var relayCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellation.Token);
            var relayTask = RelayRtxFramesAsync(framePipe, ffmpeg.StandardInput.BaseStream, relayCancellation.Token);
            RtxVideoBackendClient.JobResult result;
            try
            {
                result = client.RunAsync(rveInput, outputFile, rtxVsr, rtxQuality, resolvedScale,
                    rtxHdr, rtxHdrContrast, rtxHdrSaturation, rtxHdrMiddleGray, rtxHdrMaxLuminance,
                    codec, "rawvideo", "none", pixelFormat,
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                    audioStreamIndices, subtitleStreamIndices, pipePath,
                    () => stopWatcher?.IsStopRequested() == true,
                    () => ReadShmByte(pauseShm) == 1,
                    cancellation.Token).GetAwaiter().GetResult();
                if (!result.Succeeded)
                {
                    relayCancellation.Cancel();
                    try { ffmpeg.StandardInput.Close(); } catch { }
                    if (result.Canceled)
                    {
                        // sidecar 取消后先给 FFmpeg 最多 8 秒读取 EOF 并完成尾部封装；
                        // 只有仍未退出时才强制终止，避免把可读取的部分输出误删。
                        try
                        {
                            if (!ffmpeg.HasExited && !ffmpeg.WaitForExit(8000))
                            {
                                ffmpeg.Kill(entireProcessTree: true);
                            }
                        }
                        catch
                        {
                            try { if (!ffmpeg.HasExited) ffmpeg.Kill(entireProcessTree: true); } catch { }
                        }
                        try { relayTask.Wait(TimeSpan.FromSeconds(1)); } catch { }
                        if (ffmpeg.HasExited && ffmpeg.ExitCode == 0 && File.Exists(outputFile))
                        {
                            try
                            {
                                if (new FileInfo(outputFile).Length > 0)
                                {
                                    finalOutputCompleted = true;
                                    WriteGracefulStopMarker(gracefulStopMarker);
                                    Console.WriteLine("[停止] RTX Video 已完成尾部封装，保留非空部分输出：" + outputFile);
                                }
                            }
                            catch { }
                        }
                    }
                    else
                    {
                        try { if (!ffmpeg.HasExited) ffmpeg.Kill(entireProcessTree: true); } catch { }
                    }
                }
                else
                {
                    relayTask.GetAwaiter().GetResult();
                    ffmpeg.WaitForExit();
                }
            }
            finally
            {
                relayCancellation.Cancel();
                try { if (!ffmpeg.HasExited) ffmpeg.Kill(entireProcessTree: true); } catch { }
            }
            foreach (var warning in result.Warnings)
            {
                Console.WriteLine("[RTX Video] 警告：" + warning);
            }
            if (!result.Succeeded)
                return result.Canceled ? 130 : Fail("RTX Video 处理失败：" + result.Error, 1);
            var ffmpegError = ffmpegErrorTask.GetAwaiter().GetResult();
            if (ffmpeg.ExitCode != 0)
                return Fail("3FUI FFmpeg 编码失败（退出码 " + ffmpeg.ExitCode + "）：" + ffmpegError.Trim(), 1);
            finalOutputCompleted = true;
            Console.WriteLine("[完成] RTX Video 输出：" + outputFile);
            return 0;
        }
        finally
        {
            if (outputTouched && !finalOutputCompleted)
            {
                try { if (File.Exists(outputFile)) File.Delete(outputFile); }
                catch (Exception ex) { Console.Error.WriteLine("[警告] 无法清理失败的 RTX 输出文件：" + ex.Message); }
            }
            if (!finalOutputCompleted)
            {
                try { if (File.Exists(gracefulStopMarker)) File.Delete(gracefulStopMarker); } catch { }
            }
            if (!string.IsNullOrWhiteSpace(intermediate))
            {
                try { if (File.Exists(intermediate)) File.Delete(intermediate); }
                catch (Exception ex) { Console.Error.WriteLine("[警告] 无法清理 RTX 临时文件：" + ex.Message); }
            }
        }
    }

    /// <summary>
    /// 从 ffmpeg-settings 提取常见编码选项用于控制台摘要。RTX 帧管道不会把这些
    /// 选项交给 sidecar；完整参数由宿主 FFmpeg 原样执行。忽略流指示后缀
    /// （如 -preset:v:0）以及 -c:v、-pix_fmt、-map、-y 等非摘要项。
    /// </summary>
    private static readonly string[] RtxEncoderOptionNames =
    {
        "preset", "tune", "rc", "cq", "qp", "b:v", "maxrate", "bufsize",
        "spatial-aq", "temporal-aq", "aq-strength", "rc-lookahead", "multipass", "g",
    };

    private static Dictionary<string, string> ParseRtxEncoderOptions(string customEncoder)
    {
        var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(customEncoder)) return options;
        var tokens = Tokenize(customEncoder);
        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            if (!token.StartsWith('-') || token.Length < 2) continue;
            var name = token.TrimStart('-');
            string? value = null;
            var equals = name.IndexOf('=');
            if (equals >= 0)
            {
                value = name[(equals + 1)..];
                name = name[..equals];
            }
            var colon = name.IndexOf(':');
            if (colon > 0) name = name[..colon];
            if (Array.IndexOf(RtxEncoderOptionNames, name) < 0) continue;
            if (value is null)
            {
                if (i + 1 >= tokens.Count) break;
                value = tokens[++i];
            }
            if (value.Length > 0 && value.Length <= 64)
            {
                options[name] = value;
            }
        }
        return options;
    }

    private static Process StartRtxHostEncoder(
        string sourceInput, string outputFile, string customEncoder, bool overwrite,
        string rawPixelFormat, int width, int height, string frameRate, bool hdr,
        IReadOnlyList<int>? audioStreamIndices, IReadOnlyList<int>? subtitleStreamIndices)
    {
        var start = new ProcessStartInfo
        {
            FileName = FfmpegExe,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardError = true,
        };
        PortablePaths.ConfigureChildProcess(start);
        foreach (var value in new[]
        {
            "-hide_banner", "-loglevel", "warning", "-nostats", "-nostdin",
            "-f", "rawvideo", "-pixel_format", rawPixelFormat,
            "-video_size", width.ToString(CultureInfo.InvariantCulture) + "x" + height.ToString(CultureInfo.InvariantCulture),
            "-framerate", frameRate, "-i", "pipe:0", "-i", sourceInput,
            "-map", "0:v:0",
        }) start.ArgumentList.Add(value);
        AddRtxHostMaps(start.ArgumentList, "a", audioStreamIndices);
        AddRtxHostMaps(start.ArgumentList, "s", subtitleStreamIndices);
        if (hdr)
        {
            foreach (var value in new[]
            {
                "-color_primaries", "bt2020", "-color_trc", "smpte2084",
                "-colorspace", "bt2020nc", "-color_range", "tv",
            }) start.ArgumentList.Add(value);
        }
        foreach (var value in Tokenize(customEncoder)) start.ArgumentList.Add(value);
        start.ArgumentList.Add(outputFile);
        start.ArgumentList.Add(overwrite ? "-y" : "-n");
        var process = new Process { StartInfo = start };
        if (!process.Start()) throw new InvalidOperationException("无法启动 3FUI FFmpeg：" + FfmpegExe);
        return process;
    }

    private static void AddRtxHostMaps(ICollection<string> arguments, string type, IReadOnlyList<int>? indices)
    {
        if (indices is null)
        {
            arguments.Add("-map");
            arguments.Add("1:" + type + "?");
            return;
        }
        foreach (var index in indices)
        {
            arguments.Add("-map");
            arguments.Add("1:" + type + ":" + index.ToString(CultureInfo.InvariantCulture) + "?");
        }
    }

    /// <summary>通知插件：RTX 任务被优雅停止且最终 FFmpeg 已完成非空封装。</summary>
    private static void WriteGracefulStopMarker(string markerPath)
    {
        try
        {
            File.WriteAllText(markerPath, DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("[警告] 无法写入优雅停止标记：" + ex.Message);
        }
    }

    private static async Task RelayRtxFramesAsync(
        NamedPipeServerStream source, Stream destination, CancellationToken token)
    {
        try
        {
            await source.WaitForConnectionAsync(token);
            await source.CopyToAsync(destination, 4 * 1024 * 1024, token);
            await destination.FlushAsync(token);
        }
        finally
        {
            try { destination.Close(); } catch { }
        }
    }

    /// <summary>提取用户预设的视频像素格式；RTX HDR 始终使用 10-bit 输出。</summary>
    private static string ParseRtxPixelFormat(string customEncoder, bool hdr)
    {
        if (hdr) return "p010le";
        var tokens = Tokenize(customEncoder);
        for (var i = 0; i + 1 < tokens.Count; i++)
        {
            var name = tokens[i].TrimStart('-');
            var colon = name.IndexOf(':');
            if (colon > 0) name = name[..colon];
            if (!name.Equals("pix_fmt", StringComparison.OrdinalIgnoreCase)) continue;
            return tokens[i + 1].ToLowerInvariant() switch
            {
                "p010" or "p010le" or "yuv420p10le" => "p010le",
                "nv12" or "yuv420p" => "nv12",
                _ => "auto",
            };
        }
        return "auto";
    }

    /// <summary>
    /// 解析 3FUI 生成的正向 -map 选流。null 表示沿用 sidecar 的“复制全部”行为，
    /// 空数组表示用户显式映射但没有选择该类型；索引按音频/字幕类型分别计数。
    /// </summary>
    private static (IReadOnlyList<int>? Audio, IReadOnlyList<int>? Subtitles) ParseRtxStreamSelection(
        string customEncoder)
    {
        var tokens = Tokenize(customEncoder);
        var sawMap = false;
        var allAudio = false;
        var allSubtitles = false;
        var audio = new SortedSet<int>();
        var subtitles = new SortedSet<int>();
        for (var i = 0; i < tokens.Count; i++)
        {
            if (!tokens[i].Equals("-map", StringComparison.OrdinalIgnoreCase)) continue;
            if (++i >= tokens.Count) throw new ArgumentException("-map 缺少流选择表达式");
            sawMap = true;
            var map = tokens[i];
            if (map.StartsWith('-'))
                throw new ArgumentException("RTX Video 暂不支持排除式 -map，请改用明确的正向流映射：" + map);
            map = map.TrimEnd('?');
            if (map == "0")
            {
                allAudio = true;
                allSubtitles = true;
                continue;
            }
            var match = Regex.Match(map, @"^0:(a|s)(?::(\d+))?$", RegexOptions.IgnoreCase);
            if (!match.Success) continue; // 视频映射由 RTX 主视频流固定处理。
            var target = match.Groups[1].Value.ToLowerInvariant();
            if (!match.Groups[2].Success)
            {
                if (target == "a") allAudio = true;
                else allSubtitles = true;
                continue;
            }
            var index = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
            if (target == "a") audio.Add(index);
            else subtitles.Add(index);
        }
        if (!sawMap) return (null, null);
        return (allAudio ? null : audio.ToArray(), allSubtitles ? null : subtitles.ToArray());
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

    /// <summary>参与 TensorRT Engine 缓存隔离的本机运行时信息。</summary>
    private sealed record TensorRtRuntime(string GpuName, string TensorRtVersion, string TorchTensorRtVersion);

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

    private static int Run(string[] args)
    {
        if (args.Length > 0 && args[0].Equals("--create-installer-bundle", StringComparison.Ordinal))
            return InstallerManager.Create(args);
        if (args.Length > 0 && args[0].Equals("--install-folder", StringComparison.Ordinal))
            return InstallerManager.InstallCommand(args);
        if (args.Length > 0 && args[0].Equals("--apply-update", StringComparison.Ordinal))
            return SelfUpdateManager.Apply(args);
        if (args.Length > 0 && args[0].Equals("--create-7z", StringComparison.Ordinal))
            return CreateSevenZip(args);
        if (args.Length == 1 && args[0].Equals("--third-party-notices", StringComparison.Ordinal))
        {
            PrintThirdPartyNotices(Console.Out);
            return 0;
        }
        if (args.Length == 1 && args[0].Equals("--license", StringComparison.Ordinal))
        {
            PrintProjectLicense(Console.Out);
            return 0;
        }
        if (args.Length == 0)
        {
            if (InstallerManager.IsInstaller()) return InstallerManager.RunInteractive();
            if (InstallerManager.TryShowStandaloneLaunchGuidance()) return 0;
            CliHelp.Print(Console.Out, ToolVersion, DefaultModelScopeDataset);
            return 0;
        }

        var o = CliArgumentParser.Parse(args);
        FfmpegPathOverride = o.FfmpegPath;
        FfprobePathOverride = o.FfprobePath;

        if (o.ShowVersion)
        {
            // 发布脚本用它在构建后做端到端版本一致性校验。
            Console.WriteLine(ToolVersion);
            return 0;
        }

        if (o.ShowHelp)
        {
            CliHelp.Print(Console.Out, ToolVersion, DefaultModelScopeDataset);
            return 0;
        }

        // 便携安装器在首次复制后调用，仅处理旧版已知残留。
        if (o.CleanupLegacyResidue)
        {
            return CleanupLegacyResidue(o);
        }
        if (o.CleanupRegistryResidue)
        {
            return CleanupRegistryResidue();
        }

        // 推理后端：ncnn（默认，Vulkan）或 cuda（PyTorch，需 .pth 模型）
        if (o.HasBackend)
        {
            var b = o.Backend.Trim().ToLowerInvariant();
            if (b is not ("ncnn" or "cuda" or "tensorrt" or "onnx" or "flashvsr" or "basicvsrpp" or "rtxvsr"))
            {
                return Fail("-backend 仅支持 ncnn、cuda、tensorrt、onnx、flashvsr、basicvsrpp 或 rtxvsr，当前值：" + o.Backend);
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
        o.UpscalePrecision = NormalisePrecisionOption(o.UpscalePrecision, "-upscale-precision");
        if (o.UpscalePrecision.Length == 0) return 2;
        o.InterpPrecision = NormalisePrecisionOption(o.InterpPrecision, "-interp-precision");
        if (o.InterpPrecision.Length == 0) return 2;
        o.RtxTarget = NormaliseRtxTarget(o.RtxTarget);
        if (o.RtxTarget.Length == 0) return 2;
        if (!int.TryParse(o.RtxQuality, NumberStyles.Integer, CultureInfo.InvariantCulture, out var rtxQuality)
            || rtxQuality is < 1 or > 4)
            return Fail("-rtx-quality 必须是 1-4 的整数，当前值：" + o.RtxQuality);
        if (!int.TryParse(o.RtxHdrContrast, NumberStyles.Integer, CultureInfo.InvariantCulture, out var rtxHdrContrast)
            || rtxHdrContrast is < 0 or > 200)
            return Fail("-rtx-hdr-contrast 必须是 0-200 的整数，当前值：" + o.RtxHdrContrast);
        if (!int.TryParse(o.RtxHdrSaturation, NumberStyles.Integer, CultureInfo.InvariantCulture, out var rtxHdrSaturation)
            || rtxHdrSaturation is < 0 or > 200)
            return Fail("-rtx-hdr-saturation 必须是 0-200 的整数，当前值：" + o.RtxHdrSaturation);
        if (!int.TryParse(o.RtxHdrMiddleGray, NumberStyles.Integer, CultureInfo.InvariantCulture, out var rtxHdrMiddleGray)
            || rtxHdrMiddleGray is < 10 or > 100)
            return Fail("-rtx-hdr-middle-gray 必须是 10-100 的整数，当前值：" + o.RtxHdrMiddleGray);
        if (!int.TryParse(o.RtxHdrMaxLuminance, NumberStyles.Integer, CultureInfo.InvariantCulture, out var rtxHdrMaxLuminance)
            || rtxHdrMaxLuminance is < 400 or > 2000)
            return Fail("-rtx-hdr-max-luminance 必须是 400-2000 的整数，当前值：" + o.RtxHdrMaxLuminance);
        if (!double.TryParse(o.SceneThreshold, NumberStyles.Float, CultureInfo.InvariantCulture, out var sceneThreshold) || sceneThreshold <= 0 || sceneThreshold > 10.0)
            return Fail("-scene-threshold 必须是官方 0-10 标尺中的大于 0 数字，当前值：" + o.SceneThreshold);
        if (!int.TryParse(o.TileSize, NumberStyles.Integer, CultureInfo.InvariantCulture, out var tileSize) || tileSize < 0 || (tileSize > 0 && tileSize < 32))
            return Fail("-tile-size 必须是 0（RVE 默认）或不小于 32 的整数，当前值：" + o.TileSize);
        if (tileSize > 0 && o.Backend is not ("ncnn" or "cuda" or "tensorrt" or "onnx"))
            return Fail("-tile-size 仅支持 NCNN、CUDA/PyTorch、TensorRT 和 ONNX；当前后端 " + o.Backend + " 不使用该参数");

        // 在线列表只读取远端元数据，不依赖本地核心目录。
        if (o.ListDownloadModels)
        {
            return CreateModelDownloadManager().ListRemoteModels(o.Json);
        }

        BackendUpdateManager.RecoverPending(CoreRoot);

        if (o.BackendStatus)
        {
            return RunBackendStatus(o.BackendChannel, o.Json);
        }

        if (o.UpdateBackend)
        {
            return RunBackendUpdate(o.BackendChannel, o.ForceBackendFull);
        }

        if (!string.IsNullOrWhiteSpace(o.ApplyBackendPatch))
        {
            return ApplyBackendPatchArchive(Path.GetFullPath(o.ApplyBackendPatch));
        }

        MigrateLegacyFfmpegLayout();
        EnsureInterpolationSupportScripts();

        if (o.CleanDownloadArchives)
        {
            return CreateModelDownloadManager().CleanDownloadArchives();
        }

        if (!string.IsNullOrWhiteSpace(o.DownloadModel))
        {
            return CreateModelDownloadManager().DownloadRepositoryModel(o.DownloadModel);
        }

        if (!string.IsNullOrWhiteSpace(o.DeleteDownloadModel))
        {
            return CreateModelDownloadManager().DeleteDownloadedModel(o.DeleteDownloadModel);
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
            return ExtractArchive(archive, output);
        }

        if (!string.IsNullOrWhiteSpace(o.InspectUpscaleModel))
        {
            return InspectUpscaleModel(o.InspectUpscaleModel);
        }

        if (!string.IsNullOrWhiteSpace(o.ImportModel))
        {
            return ImportModels(o.ImportModel, o.Json);
        }

        if (!string.IsNullOrWhiteSpace(o.UpdateUserModel))
        {
            return UpdateUserModel(o);
        }

        if (!string.IsNullOrWhiteSpace(o.DeleteUserModel))
        {
            return DeleteUserModel(o);
        }

        if (!string.IsNullOrWhiteSpace(o.InspectInterpModel))
        {
            return InspectInterpolationModel(o.InspectInterpModel);
        }

        if (!string.IsNullOrWhiteSpace(o.PrepareInterpEngine))
        {
            if (!int.TryParse(o.PrepareWidth, NumberStyles.Integer, CultureInfo.InvariantCulture, out var prepareWidth)
                || !int.TryParse(o.PrepareHeight, NumberStyles.Integer, CultureInfo.InvariantCulture, out var prepareHeight)
                || prepareWidth <= 0 || prepareHeight <= 0)
            {
                return Fail("--prepare-width 和 --prepare-height 必须是大于 0 的整数");
            }
            return PrepareRifeTensorRTEngine(o.PrepareInterpEngine, prepareWidth, prepareHeight, o.PrepareStaticShape);
        }

        if (o.ListModels)
        {
            return ListModels(o.Json, o.Backend);
        }

        if (o.ListModelCatalog)
        {
            return ListModelCatalog(o.Json, o.Backend, interpolation: false);
        }

        if (o.ListInterpModelCatalog)
        {
            return ListModelCatalog(o.Json, o.InterpBackend, interpolation: true);
        }

        if (o.ListUserModels)
        {
            return ListUserModels(o.Json);
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
            if (o.Backend == "rtxvsr") return Fail("RTX VSR 仅支持视频；图片请改用 NCNN、CUDA、TensorRT、ONNX、FlashVSR 或 BasicVSR++");
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
        var segmentedUpscale = !string.IsNullOrWhiteSpace(o.SegmentsBase64);

        // 1. 环境检测（ffmpeg / python 库 / 模型库）
        if (!segmentedUpscale && !RunCheck(verbose: false, backend: o.Backend))
        {
            return 1;
        }
        if (o.Backend == "rtxvsr" && o.HasInterpModel && !RunCheck(verbose: false, backend: o.InterpBackend))
        {
            return 1;
        }
        if (o.RtxHdr && o.Backend != "rtxvsr" && !RunRtxCheck(verbose: false, requireVsr: false, requireHdr: true))
        {
            return 1;
        }

        // 2. 输入视频
        var input = Path.GetFullPath(o.Input);
        if (!File.Exists(input))
        {
            return Fail("输入视频不存在：" + input);
        }

        if (segmentedUpscale && o.NoUpscale)
        {
            return Fail("分段超分不能与 -no-upscale 同时使用");
        }
        if (segmentedUpscale && (o.HasInterpModel || o.RtxHdr))
        {
            return Fail("分段超分当前不能同时启用运动补帧或 RTX HDR");
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
        string? requestedScale = null;
        if (useUpscale && !segmentedUpscale)
        {
            if (o.Backend == "rtxvsr")
            {
                model = "__rtx_vsr__";
            }
            else
            {
                model = ResolveModelWithTensorRtOutputScalePreset(o.Model, o.Backend, out var tensorRtPresetScale);
                if (model.Length == 0)
                {
                    return 1;
                }
                if (o.HasScaleOverride)
                {
                    if (!int.TryParse(o.ScaleOverride, out var requestedScaleValue) || requestedScaleValue < 1)
                    {
                        return Fail("-scale 必须是大于 0 的整数，当前值：" + o.ScaleOverride);
                    }
                    requestedScale = requestedScaleValue.ToString(CultureInfo.InvariantCulture);
                }
                else if (tensorRtPresetScale > 0)
                {
                    requestedScale = tensorRtPresetScale.ToString(CultureInfo.InvariantCulture);
                }
                else
                {
                    requestedScale = o.Backend == "basicvsrpp" ? BasicVsrPlusPlusScale(model) : DetectScale(model);
                }
                if (o.Backend == "tensorrt")
                {
                    var engineScale = int.TryParse(requestedScale, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedScale)
                        ? parsedScale
                        : 0;
                    var engineWidth = inputResolution.Item1;
                    var engineHeight = inputResolution.Item2;
                    if (ModelCapabilityCatalog.TryGet(model, ModelsDir, out var engineCapability)
                        && engineCapability.InputMultiple > 1)
                    {
                        engineWidth = (engineWidth + engineCapability.InputMultiple - 1)
                            / engineCapability.InputMultiple * engineCapability.InputMultiple;
                        engineHeight = (engineHeight + engineCapability.InputMultiple - 1)
                            / engineCapability.InputMultiple * engineCapability.InputMultiple;
                    }
                    model = EnsureTensorRtEngine(model, engineWidth, engineHeight, stopWatcher, tileSize, engineScale,
                        o.UpscalePrecision);
                    if (model.Length == 0) return stopWatcher?.IsStopRequested() == true ? 130 : 1;
                }
            }
        }

        // 3.5 补帧模型（RIFE）：补帧使用独立的有效后端，避免 TensorRT/ONNX 与 NCNN 模型格式错配。
        string? interpModel = null;
        if (o.HasInterpModel)
        {
            interpModel = ResolveInterpModel(o.InterpModel, o.InterpBackend);
            if (interpModel.Length == 0)
            {
                return 1;
            }
        }
        if (!useUpscale && interpModel is null && !o.RtxHdr)
        {
            return Fail("-no-upscale 已指定，但未启用补帧或 RTX HDR");
        }

        // 4. 倍率：优先用户指定，其次从模型名自动识别（与 GUI 一致）
        string? scale = null;
        if (useUpscale && o.Backend != "rtxvsr")
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
                // TensorRT 会把源模型替换为带输入尺寸的缓存 Engine 路径；继续解析新路径
                // 可能把 96x64 中的 x6 误认为倍率，因此沿用转换前已经确定的值。
                scale = requestedScale;
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
        if (o.RtxHdr && hdrMode)
        {
            return Fail("输入已经是 PQ/HLG HDR，不能再次应用 RTX HDR 映射；请关闭 RTX HDR");
        }
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
        if (segmentedUpscale)
        {
            if (hdrMode)
            {
                return Fail("分段超分使用 RGB24 逐帧管线，当前不支持 PQ/HLG HDR 输入");
            }
            return RunSegmentedVideo(o, input, outputFile, customEncoder, overwrite, o.PauseShm,
                stopWatcher, tileSize);
        }
        if (o.Backend == "rtxvsr" || o.RtxHdr)
        {
            return RunVideoWithRtx(input, outputFile, model, customEncoder, o.FfmpegSettings, overwrite, scale,
                o.PauseShm, stopWatcher, interpModel, interpFactor, o.Backend, o.InterpBackend,
                o.ProcessOrder, hdrMode, o.DynamicOpticalFlow, sceneThreshold, tileSize, o.UpscalePrecision,
                o.InterpPrecision, o.Backend == "rtxvsr" && useUpscale, o.RtxHdr, o.RtxTarget, rtxQuality,
                rtxHdrContrast, rtxHdrSaturation, rtxHdrMiddleGray, rtxHdrMaxLuminance);
        }
        return RunVideoPipeline(input, outputFile, model, customEncoder, overwrite, scale,
            o.PauseShm, stopWatcher, interpModel, interpFactor, o.Backend, o.InterpBackend, o.ProcessOrder, hdrMode,
            o.DynamicOpticalFlow, sceneThreshold, tileSize, o.UpscalePrecision, o.InterpPrecision);
    }

    private static int CreateSevenZip(string[] args)
    {
        if (args.Length != 3) return Fail("--create-7z 需要 <源目录> <输出.7z>");
        ManagedArchiveExtractor.CreateSevenZip(args[1], args[2]);
        Console.WriteLine("ARCHIVE_CREATE_COMPLETE|" + Path.GetFullPath(args[2]));
        return 0;
    }

    private static void PrintThirdPartyNotices(TextWriter writer)
    {
        foreach (var resourceName in new[]
        {
            EmbeddedThirdPartyNoticesResource,
            EmbeddedSharpCompressLicenseResource
        })
        {
            using var source = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException("缺少第三方声明资源：" + resourceName);
            using var reader = new StreamReader(source, Encoding.UTF8, true, leaveOpen: false);
            writer.WriteLine(reader.ReadToEnd().TrimEnd());
            writer.WriteLine();
        }
    }

    private static void PrintProjectLicense(TextWriter writer)
    {
        using var source = Assembly.GetExecutingAssembly().GetManifestResourceStream(EmbeddedProjectLicenseResource)
            ?? throw new InvalidOperationException("缺少项目许可证资源：" + EmbeddedProjectLicenseResource);
        using var reader = new StreamReader(source, Encoding.UTF8, true, leaveOpen: false);
        writer.WriteLine(reader.ReadToEnd().TrimEnd());
    }

    /// <summary>由便携安装器调用：迁移旧布局并清除可明确识别的旧版残留。</summary>
    private static int CleanupLegacyResidue(CliOptions o)
    {
        if (string.IsNullOrWhiteSpace(o.PluginRoot))
            return Fail("--cleanup-legacy-residue 需要 --plugin-root", 1);

        var pluginRoot = Path.GetFullPath(o.PluginRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!Directory.Exists(pluginRoot))
            return Fail("Plugin 目录不存在：" + pluginRoot, 1);

        var applicationRoot = Path.Combine(pluginRoot, "videoenhancer");
        Directory.CreateDirectory(applicationRoot);
        var legacyLocalAppData = string.IsNullOrWhiteSpace(o.LegacyLocalAppData)
            ? null
            : Path.GetFullPath(o.LegacyLocalAppData);
        var legacyTempRoot = string.IsNullOrWhiteSpace(o.LegacyTempRoot)
            ? null
            : Path.GetFullPath(o.LegacyTempRoot);
        LegacyResidueCleaner.MigrateLegacyPluginLayout(pluginRoot);
        var protectedLegacyConfig = LegacyResidueCleaner.MigratePluginConfiguration(
            applicationRoot,
            legacyLocalAppData);
        LegacyResidueCleaner.Clean(
            pluginRoot,
            protectedLegacyConfig,
            legacyLocalAppData,
            legacyTempRoot);
        LegacyResidueCleaner.CleanObsoleteEmbeddedTools(applicationRoot);
        Console.WriteLine("LEGACY_CLEANUP_COMPLETE|" + pluginRoot);
        return 0;
    }

    /// <summary>旧版兼容命令：移除当前用户由插件创建的图片右键菜单。</summary>
    private static int CleanupRegistryResidue()
    {
        const string shellRoot = @"Software\Classes\SystemFileAssociations\image\shell";
        using var shell = Registry.CurrentUser.OpenSubKey(shellRoot, writable: true);
        shell?.DeleteSubKeyTree("VideoEnhancer.Upscale", throwOnMissingSubKey: false);
        Console.WriteLine("REGISTRY_CLEANUP_COMPLETE|HKCU\\" + shellRoot + "\\VideoEnhancer.Upscale");
        return 0;
    }

    private static string DefaultBackendChannel => ModelScopeResolveRoot + "Backend/channel.json";
    private static ModelRepositoryClient CreateModelRepository() => new(ModelScopeDataset, ModelScopeToken, ToolVersion);

    private static BackendUpdateChannel LoadBackendChannel(string configuredSource, out string source)
    {
        source = string.IsNullOrWhiteSpace(configuredSource)
            ? Environment.GetEnvironmentVariable("VIDEOENHANCER_BACKEND_CHANNEL")?.Trim() ?? DefaultBackendChannel
            : configuredSource.Trim();
        string json;
        if (Uri.TryCreate(source, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(25) };
            CreateModelRepository().ApplyModelScopeAuthentication(client);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("VideoEnhancer/" + ToolVersion);
            json = client.GetStringAsync(uri).GetAwaiter().GetResult();
        }
        else
        {
            source = Path.GetFullPath(source);
            json = File.ReadAllText(source, Encoding.UTF8);
        }
        return BackendUpdateManager.ReadChannel(json);
    }

    private static int RunBackendStatus(string configuredSource, bool json)
    {
        try
        {
            var channel = LoadBackendChannel(configuredSource, out _);
            var status = BackendUpdateManager.GetStatus(CoreRoot, channel);
            if (json)
            {
                using var buffer = new MemoryStream();
                using (var writer = new Utf8JsonWriter(buffer))
                {
                    writer.WriteStartObject();
                    writer.WriteString("state", status.State);
                    writer.WriteString("installedVersion", status.InstalledVersion);
                    writer.WriteString("latestVersion", status.LatestVersion);
                    writer.WriteString("mode", status.Mode);
                    writer.WriteNumber("downloadSize", status.DownloadSize);
                    writer.WriteNumber("fullSize", status.Full.Size);
                    writer.WriteNumber("patchCount", status.PatchRoute.Count);
                    writer.WriteEndObject();
                }
                Console.WriteLine(Encoding.UTF8.GetString(buffer.ToArray()));
            }
            else
            {
                Console.WriteLine($"BACKEND_STATUS|{status.State}|{status.InstalledVersion}|{status.LatestVersion}|{status.Mode}|{status.DownloadSize}");
            }
            return 0;
        }
        catch (Exception ex)
        {
            ModelRepositoryClient.WriteRemoteFailure("无法读取后端更新状态", ex);
            return 3;
        }
    }

    private static int RunBackendUpdate(string configuredSource, bool forceFull)
    {
        try
        {
            var channel = LoadBackendChannel(configuredSource, out var source);
            var status = BackendUpdateManager.GetStatus(CoreRoot, channel);
            if (status.State == "current" && !forceFull)
            {
                Console.WriteLine("BACKEND_CURRENT|" + status.LatestVersion);
                return 0;
            }
            if (forceFull || status.Mode == "full")
            {
                Console.WriteLine($"BACKEND_FULL_START|{status.LatestVersion}|{status.Full.Size}");
                var archive = AcquireBackendArtifact(source, status.Full.Path, status.Full.Size, status.Full.Sha256);
                return ApplyBackendFullArchive(archive, status.LatestVersion);
            }

            foreach (var patch in status.PatchRoute)
            {
                Console.WriteLine($"BACKEND_PATCH_START|{patch.BaseVersion}|{patch.TargetVersion}|{patch.Size}");
                var archive = AcquireBackendArtifact(source, patch.Path, patch.Size, patch.Sha256);
                var code = ApplyBackendPatchArchive(archive);
                if (code != 0) return code;
            }
            Console.WriteLine("BACKEND_UPDATE_COMPLETE|" + status.LatestVersion);
            return 0;
        }
        catch (Exception ex)
        {
            if (ModelRepositoryClient.IsNetworkFailure(ex) || ModelRepositoryClient.IsAuthenticationFailure(ex))
                ModelRepositoryClient.WriteRemoteFailure("后端更新失败", ex);
            else
                Console.Error.WriteLine("[错误] 后端更新失败：" + ex.Message);
            return 1;
        }
    }

    private static string AcquireBackendArtifact(
        string channelSource,
        string artifactPath,
        long expectedSize,
        string expectedSha256)
    {
        var downloads = Path.Combine(BackendUpdateManager.StateRoot(CoreRoot), "downloads");
        Directory.CreateDirectory(downloads);
        var safeName = Path.GetFileName(artifactPath.Replace('\\', '/'));
        if (string.IsNullOrWhiteSpace(safeName))
            throw new InvalidOperationException("后端更新包路径无效：" + artifactPath);
        var destination = Path.Combine(downloads, safeName);
        if (File.Exists(destination))
        {
            try
            {
                BackendUpdateManager.VerifyArtifact(destination, expectedSize, expectedSha256);
                return destination;
            }
            catch
            {
                File.Delete(destination);
            }
        }

        if (Uri.TryCreate(channelSource, UriKind.Absolute, out var channelUri)
            && (channelUri.Scheme == Uri.UriSchemeHttp || channelUri.Scheme == Uri.UriSchemeHttps))
        {
            string url;
            if (Uri.TryCreate(artifactPath, UriKind.Absolute, out var artifactUri))
                url = artifactUri.ToString();
            else if (channelSource.Equals(DefaultBackendChannel, StringComparison.OrdinalIgnoreCase))
                url = ModelScopeResolveRoot + string.Join("/", artifactPath.Split('/').Select(Uri.EscapeDataString));
            else
                url = new Uri(channelUri, artifactPath).ToString();
            var code = ModelScopeToken is null
                ? DownloadWithAria(url, destination, printComplete: false)
                : CreateModelDownloadManager().DownloadModelScopeFile(url, destination);
            if (code != 0) throw new InvalidOperationException("无法下载后端更新包：" + artifactPath);
        }
        else
        {
            var channelFile = Path.GetFullPath(channelSource);
            var channelDirectory = Path.GetDirectoryName(channelFile)!;
            var source = Path.IsPathRooted(artifactPath)
                ? Path.GetFullPath(artifactPath)
                : Path.GetFullPath(Path.Combine(channelDirectory, artifactPath.Replace('/', Path.DirectorySeparatorChar)));
            if (!File.Exists(source)
                && Path.GetFileName(channelDirectory).Equals("Backend", StringComparison.OrdinalIgnoreCase)
                && artifactPath.Replace('\\', '/').StartsWith("Backend/", StringComparison.OrdinalIgnoreCase))
            {
                source = Path.GetFullPath(Path.Combine(
                    Directory.GetParent(channelDirectory)!.FullName,
                    artifactPath.Replace('/', Path.DirectorySeparatorChar)));
            }
            if (!File.Exists(source)) throw new FileNotFoundException("找不到本地后端更新包", source);
            File.Copy(source, destination, true);
        }
        BackendUpdateManager.VerifyArtifact(destination, expectedSize, expectedSha256);
        return destination;
    }

    private static int ApplyBackendPatchArchive(string archive)
    {
        if (!File.Exists(archive)) return Fail("后端补丁不存在：" + archive, 1);
        var extractRoot = Path.Combine(
            BackendUpdateManager.StateRoot(CoreRoot),
            "extract-" + Guid.NewGuid().ToString("N"));
        try
        {
            var code = ExtractArchive(archive, extractRoot);
            if (code != 0) return code;
            var version = BackendUpdateManager.ApplyExtractedPatch(CoreRoot, extractRoot);
            Console.WriteLine("BACKEND_PATCH_COMPLETE|" + version);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("BACKEND_FULL_REQUIRED|增量补丁不适用于当前本地文件，可改用完整修复包");
            Console.Error.WriteLine("[错误] 应用后端补丁失败：" + ex.Message);
            return 1;
        }
        finally
        {
            if (Directory.Exists(extractRoot)) Directory.Delete(extractRoot, true);
        }
    }

    private static int ApplyBackendFullArchive(string archive, string targetVersion)
    {
        var extractRoot = Path.Combine(
            BackendUpdateManager.StateRoot(CoreRoot),
            "full-extract-" + Guid.NewGuid().ToString("N"));
        try
        {
            var code = ExtractArchive(archive, extractRoot);
            if (code != 0) return code;
            var stagedPython = File.Exists(Path.Combine(extractRoot, "python", "python", "python.exe"))
                ? Path.Combine(extractRoot, "python")
                : extractRoot;
            BackendUpdateManager.ApplyStagedFullBackend(CoreRoot, stagedPython, targetVersion);
            Console.WriteLine("BACKEND_UPDATE_COMPLETE|" + targetVersion);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("[错误] 完整后端安装失败：" + ex.Message);
            return 1;
        }
        finally
        {
            if (Directory.Exists(extractRoot)) Directory.Delete(extractRoot, true);
        }
    }

    private static ModelDownloadManager CreateModelDownloadManager() => new(
        CoreRoot, ModelScopeToken, ToolVersion, CreateModelRepository(),
        DownloadWithAria, ExtractArchive, Fail);

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

    private static string EnsureEmbeddedFile(string resourceName, string fileName)
    {
        var directory = PortablePaths.EmbeddedToolsRoot(ToolVersion);
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

    /// <summary>把与插件版本配套的补帧检查/预构建脚本同步到当前 RVE 后端。</summary>
    private static void EnsureInterpolationSupportScripts()
    {
        var backendDirectory = Path.Combine(CoreRoot, "python", "backend");
        if (!Directory.Exists(backendDirectory)) return;
        try
        {
            InstallEmbeddedBackendScript(EmbeddedInterpolationInspectorResource, InterpolationInspectorScript);
            InstallEmbeddedBackendScript(EmbeddedUpscaleInspectorResource, UpscaleInspectorScript);
            InstallEmbeddedBackendScript(EmbeddedRifeTensorRTPrepareResource, RifeTensorRTPrepareScript);
            InstallEmbeddedBackendScript(EmbeddedTensorRTConverterResource, TensorRTConverterScript);
            InstallEmbeddedBackendScript(EmbeddedImageBackendResource, ImageBackendScript);
            InstallEmbeddedBackendScript(EmbeddedSegmentedBackendResource, SegmentedBackendScript);
            EnsureGmfssModelTypeCompatibility();
            EnsureGimmModelCompatibility();
            EnsurePytorchUpscaleCompatibility();
            EnsureOnnxModelCompatibility();
            EnsureImagePrecisionCompatibility();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("[警告] 无法同步补帧辅助脚本：" + ex.Message);
        }
    }

    private static void InstallEmbeddedBackendScript(string resourceName, string destinationPath)
    {
        using var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException("内置补帧脚本资源不存在：" + resourceName);
        var needsUpdate = !File.Exists(destinationPath);
        if (!needsUpdate)
        {
            using var existing = new FileStream(destinationPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var resourceHash = SHA256.HashData(resource);
            var existingHash = SHA256.HashData(existing);
            needsUpdate = !resourceHash.AsSpan().SequenceEqual(existingHash);
            resource.Position = 0;
        }
        if (!needsUpdate) return;
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        using var output = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
        resource.CopyTo(output);
    }

    /// <summary>修补当前 RVE 2.4 GMFSS 加载器，使其按权重元数据区分 Base 与 Union。</summary>
    private static void EnsureGmfssModelTypeCompatibility()
    {
        var modelLoader = Path.Combine(CoreRoot, "python", "backend", "src", "pytorch",
            "InterpolateArchs", "GMFSS", "GMFSS.py");
        if (!File.Exists(modelLoader)) return;

        var bytes = File.ReadAllBytes(modelLoader);
        var hasUtf8Bom = bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble);
        var text = Encoding.UTF8.GetString(bytes, hasUtf8Bom ? Encoding.UTF8.Preamble.Length : 0,
            bytes.Length - (hasUtf8Bom ? Encoding.UTF8.Preamble.Length : 0));
        if (text.Contains("detected_model_type", StringComparison.Ordinal)) return;

        var newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var normalized = text.Replace("\r\n", "\n");
        const string oldDetect =
            "        combined_state_dict = torch.load(model_path, map_location=\"cpu\")\n\n" +
            "        archDetect = ArchDetect(combined_state_dict[\"rife\"])\n" +
            "        rife_version = archDetect.getArchName()\n" +
            "        # print(rife_version)\n" +
            "        if rife_version.lower() == \"rife46\":\n" +
            "            from .IFNet_HDv3 import IFNet\n" +
            "        else:\n" +
            "            # this is dumb, it detects rife4.7 with a stupid hack, so we need to just force load 422\n" +
            "            from .IFNet_HDv3_422 import IFNet";
        const string newDetect =
            "        combined_state_dict = torch.load(\n" +
            "            model_path, map_location=\"cpu\", weights_only=True, mmap=True\n" +
            "        )\n" +
            "        metadata = combined_state_dict.get(\"metadata\", {})\n" +
            "        detected_model_type = str(metadata.get(\"model_type\", \"\")).lower()\n" +
            "        if detected_model_type in {\"base\", \"union\"}:\n" +
            "            self.model_type = detected_model_type\n" +
            "        elif \"rife\" not in combined_state_dict:\n" +
            "            # 旧版 Base 权重没有 metadata，也没有 RIFE 分支。\n" +
            "            self.model_type = \"base\"\n\n" +
            "        IFNet = None\n" +
            "        if self.model_type != \"base\":\n" +
            "            if \"rife\" not in combined_state_dict:\n" +
            "                raise ValueError(\"GMFSS Union 权重缺少 rife 组件\")\n" +
            "            archDetect = ArchDetect(combined_state_dict[\"rife\"])\n" +
            "            rife_version = archDetect.getArchName()\n" +
            "            if rife_version.lower() == \"rife46\":\n" +
            "                from .IFNet_HDv3 import IFNet\n" +
            "            else:\n" +
            "                # RIFE 4.7 等分支继续使用现有 4.22 兼容实现。\n" +
            "                from .IFNet_HDv3_422 import IFNet";
        if (!normalized.Contains("from .FusionNet_u import GridNet", StringComparison.Ordinal)
            || !normalized.Contains(oldDetect, StringComparison.Ordinal)
            || !normalized.Contains("        self.ifnet = IFNet(ensemble=ensemble).to(dtype=dtype, device=device)", StringComparison.Ordinal)
            || !normalized.Contains("        self.fusionnet = GridNet().to(dtype=dtype, device=device)", StringComparison.Ordinal)
            || !normalized.Contains("        if model_type != \"base\":", StringComparison.Ordinal))
        {
            // 后端结构不是已知的 2.4 版本时不做文本改写，避免覆盖未来实现。
            return;
        }

        normalized = normalized
            .Replace("from .FusionNet_u import GridNet",
                "from .FusionNet_b import GridNet as BaseGridNet\nfrom .FusionNet_u import GridNet as UnionGridNet",
                StringComparison.Ordinal)
            .Replace(oldDetect, newDetect, StringComparison.Ordinal)
            .Replace("        self.ifnet = IFNet(ensemble=ensemble).to(dtype=dtype, device=device)",
                "        self.ifnet = (\n" +
                "            IFNet(ensemble=ensemble).to(dtype=dtype, device=device)\n" +
                "            if IFNet is not None\n" +
                "            else None\n" +
                "        )", StringComparison.Ordinal)
            .Replace("        self.fusionnet = GridNet().to(dtype=dtype, device=device)",
                "        fusionnet_class = BaseGridNet if self.model_type == \"base\" else UnionGridNet\n" +
                "        self.fusionnet = fusionnet_class().to(dtype=dtype, device=device)",
                StringComparison.Ordinal)
            .Replace("        if model_type != \"base\":",
                "        if self.model_type != \"base\":", StringComparison.Ordinal);

        File.WriteAllText(modelLoader, normalized.Replace("\n", newline), new UTF8Encoding(hasUtf8Bom));
    }

    /// <summary>修补当前 RVE 2.4 GIMM 加载器，使其兼容带元数据的新权重字段名。</summary>
    private static void EnsureGimmModelCompatibility()
    {
        var backendRoot = Path.Combine(CoreRoot, "python", "backend", "src", "pytorch");
        var files = new[]
        {
            Path.Combine(backendRoot, "InterpolateArchs", "DetectInterpolateArch.py"),
            Path.Combine(backendRoot, "InterpolateArchs", "GIMM", "gimmvfi_r.py"),
            Path.Combine(backendRoot, "InterpolateGIMM.py"),
        };
        if (files.Any(path => !File.Exists(path))) return;

        static void PatchUtf8File(string path, string oldValue, string newValue, string marker)
        {
            var bytes = File.ReadAllBytes(path);
            var hasUtf8Bom = bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble);
            var text = Encoding.UTF8.GetString(bytes, hasUtf8Bom ? Encoding.UTF8.Preamble.Length : 0,
                bytes.Length - (hasUtf8Bom ? Encoding.UTF8.Preamble.Length : 0));
            if (text.Contains(marker, StringComparison.Ordinal)) return;
            var newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
            var normalized = text.Replace("\r\n", "\n");
            if (!normalized.Contains(oldValue, StringComparison.Ordinal)) return;
            normalized = normalized.Replace(oldValue, newValue, StringComparison.Ordinal);
            File.WriteAllText(path, normalized.Replace("\n", newline), new UTF8Encoding(hasUtf8Bom));
        }

        PatchUtf8File(
            files[0],
            "        if \"raft\" in self.state_dict:  # load in GIMM RAFT\n" +
            "            self.state_dict = self.state_dict[\"raft\"]",
            "        if \"raft\" in self.state_dict:  # load in legacy GIMM RAFT\n" +
            "            self.state_dict = self.state_dict[\"raft\"]\n" +
            "        elif \"flow_estimator\" in self.state_dict:  # 新模型包字段名\n" +
            "            self.state_dict = self.state_dict[\"flow_estimator\"]",
            "新模型包字段名");
        PatchUtf8File(
            files[1],
            "        ckpt = torch.load(model_path)\n" +
            "        model.load_state_dict(ckpt[\"raft\"], strict=True)",
            "        ckpt = torch.load(model_path, weights_only=True, map_location=\"cpu\")\n" +
            "        flow_state = ckpt.get(\"raft\", ckpt.get(\"flow_estimator\"))\n" +
            "        if flow_state is None:\n" +
            "            raise ValueError(\"GIMM 权重缺少 raft/flow_estimator 组件\")\n" +
            "        model.load_state_dict(flow_state, strict=True)",
            "GIMM 权重缺少 raft/flow_estimator 组件");
        PatchUtf8File(
            files[2],
            "            state_dict = torch.load(self.interpolateModel, map_location=self.device)[\n" +
            "                \"gimmvfi_r\"\n" +
            "            ]\n" +
            "            self.flownet.load_state_dict(state_dict)",
            "            combined_state_dict = torch.load(\n" +
            "                self.interpolateModel, map_location=self.device, weights_only=True\n" +
            "            )\n" +
            "            state_dict = combined_state_dict.get(\n" +
            "                \"gimmvfi_r\", combined_state_dict.get(\"gimmvfi\")\n" +
            "            )\n" +
            "            if state_dict is None:\n" +
            "                raise ValueError(\"GIMM 权重缺少 gimmvfi_r/gimmvfi 组件\")\n" +
            "            self.flownet.load_state_dict(state_dict)",
            "GIMM 权重缺少 gimmvfi_r/gimmvfi 组件");
        PatchUtf8File(
            files[2],
            "                    with torch.autocast(enabled=True, device_type=self.device.type):",
            "                    # FP32 兼容模式必须关闭 autocast，否则仍会回到半精度并产生 NaN。\n" +
            "                    with torch.autocast(\n" +
            "                        enabled=self.dtype != torch.float32, device_type=self.device.type\n" +
            "                    ):",
            "FP32 兼容模式必须关闭 autocast");
    }

    /// <summary>让独立图片后端沿用超分半精度开关，而不是固定使用 auto。</summary>
    private static void EnsureImagePrecisionCompatibility()
    {
        if (!File.Exists(ImageBackendScript)) return;
        var bytes = File.ReadAllBytes(ImageBackendScript);
        var hasUtf8Bom = bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble);
        var text = Encoding.UTF8.GetString(bytes, hasUtf8Bom ? Encoding.UTF8.Preamble.Length : 0,
            bytes.Length - (hasUtf8Bom ? Encoding.UTF8.Preamble.Length : 0));
        const string marker = "VIDEOENHANCER_UPSCALE_PRECISION";
        if (text.Contains(marker, StringComparison.Ordinal)) return;
        const string oldValue = "precision=\"auto\",";
        if (!text.Contains(oldValue, StringComparison.Ordinal)) return;
        var replacement = "precision=os.environ.get(\"VIDEOENHANCER_UPSCALE_PRECISION\", \"auto\"),";
        File.WriteAllText(ImageBackendScript, text.Replace(oldValue, replacement, StringComparison.Ordinal),
            new UTF8Encoding(hasUtf8Bom));
    }

    /// <summary>修补当前 RVE 2.4 ONNX 加载器的倍率解析与动态输入尺寸兼容性。</summary>
    private static void EnsureOnnxModelCompatibility()
    {
        var loader = Path.Combine(CoreRoot, "python", "backend", "src", "onnx", "UpscaleONNX.py");
        if (!File.Exists(loader)) return;
        var bytes = File.ReadAllBytes(loader);
        var hasUtf8Bom = bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble);
        var text = Encoding.UTF8.GetString(bytes, hasUtf8Bom ? Encoding.UTF8.Preamble.Length : 0,
            bytes.Length - (hasUtf8Bom ? Encoding.UTF8.Preamble.Length : 0));
        const string legacyAutoTile =
            "        if self.tilesize == 0:\r\n" +
            "            self.tilesize = max(0, int(os.environ.get(\"VIDEOENHANCER_ONNX_TILE_SIZE\", \"0\")))\r\n";
        const string legacyAutoTileLf =
            "        if self.tilesize == 0:\n" +
            "            self.tilesize = max(0, int(os.environ.get(\"VIDEOENHANCER_ONNX_TILE_SIZE\", \"0\")))\n";
        if (text.Contains(legacyAutoTile, StringComparison.Ordinal)
            || text.Contains(legacyAutoTileLf, StringComparison.Ordinal))
        {
            text = text.Replace(legacyAutoTile, string.Empty, StringComparison.Ordinal)
                .Replace(legacyAutoTileLf, string.Empty, StringComparison.Ordinal);
            File.WriteAllText(loader, text, new UTF8Encoding(hasUtf8Bom));
            return;
        }
        const string oldValue = "    name = os.path.basename(modelPath).lower()";
        const string newValue = "    name = os.path.splitext(os.path.basename(modelPath))[0].lower()";
        if (!text.Contains(newValue, StringComparison.Ordinal)
            && text.Contains(oldValue, StringComparison.Ordinal))
        {
            text = text.Replace(oldValue, newValue, StringComparison.Ordinal);
        }

        const string marker = "VIDEOENHANCER_ONNX_INPUT_MULTIPLE";
        if (!text.Contains(marker, StringComparison.Ordinal))
        {
            const string oldInit =
                "        self.tilesize = max(0, int(tilesize or 0))\n" +
                "        self.gpu_id = gpu_id";
            const string newInit =
                "        self.tilesize = max(0, int(tilesize or 0))\n" +
                "        # 已知窗口注意力模型通过环境变量声明固有输入尺寸约束。\n" +
                "        self.input_multiple = max(1, int(os.environ.get(\"VIDEOENHANCER_ONNX_INPUT_MULTIPLE\", \"1\")))\n" +
                "        self.gpu_id = gpu_id";
            const string oldRun =
                "    def _run(self, image: np.ndarray) -> np.ndarray:\n" +
                "        output = self.inference_session.run(None, {self.input_name: self._prepare_input(image)})[0]\n" +
                "        return self._normalise_output(output)\n\n" +
                "    def _run_static_tiled";
            const string newRun =
                "    def _run(self, image: np.ndarray) -> np.ndarray:\n" +
                "        output = self.inference_session.run(None, {self.input_name: self._prepare_input(image)})[0]\n" +
                "        return self._normalise_output(output)\n\n" +
                "    def _run_padded(self, image: np.ndarray) -> np.ndarray:\n" +
                "        height, width = image.shape[:2]\n" +
                "        multiple = self.input_multiple\n" +
                "        pad_h = (-height) % multiple\n" +
                "        pad_w = (-width) % multiple\n" +
                "        if pad_h or pad_w:\n" +
                "            mode = \"reflect\" if height > 1 and width > 1 else \"edge\"\n" +
                "            image = np.pad(image, ((0, pad_h), (0, pad_w), (0, 0)), mode=mode)\n" +
                "        output = self._run(image)\n" +
                "        return output[: height * self.scale, : width * self.scale]\n\n" +
                "    def _run_dynamic_tiled(self, image: np.ndarray) -> np.ndarray:\n" +
                "        height, width = image.shape[:2]\n" +
                "        multiple = self.input_multiple\n" +
                "        core = max(multiple, self.tilesize - self.tilesize % multiple)\n" +
                "        overlap = max(self.tile_pad, multiple)\n" +
                "        result = np.empty((height * self.scale, width * self.scale, 3), dtype=np.uint8)\n" +
                "        for y in range(0, height, core):\n" +
                "            for x in range(0, width, core):\n" +
                "                actual_h = min(core, height - y)\n" +
                "                actual_w = min(core, width - x)\n" +
                "                y0, x0 = max(0, y - overlap), max(0, x - overlap)\n" +
                "                y1 = min(height, y + actual_h + overlap)\n" +
                "                x1 = min(width, x + actual_w + overlap)\n" +
                "                upscaled = self._run_padded(image[y0:y1, x0:x1])\n" +
                "                sy, sx = (y - y0) * self.scale, (x - x0) * self.scale\n" +
                "                cropped = upscaled[sy:sy + actual_h * self.scale, sx:sx + actual_w * self.scale]\n" +
                "                result[y * self.scale:(y + actual_h) * self.scale, x * self.scale:(x + actual_w) * self.scale] = cropped\n" +
                "        return result\n\n" +
                "    def _run_static_tiled";
            const string oldCall =
                "        if self.model_width is None or (\n" +
                "            image.shape[1] == self.model_width and image.shape[0] == self.model_height\n" +
                "        ):\n" +
                "            output = self._run(image)\n" +
                "        else:\n" +
                "            output = self._run_static_tiled(image)";
            const string newCall =
                "        if self.model_width is None:\n" +
                "            output = self._run_dynamic_tiled(image) if self.tilesize > 0 else self._run_padded(image)\n" +
                "        elif image.shape[1] == self.model_width and image.shape[0] == self.model_height:\n" +
                "            output = self._run(image)\n" +
                "        else:\n" +
                "            output = self._run_static_tiled(image)";

            var normalized = text.Replace("\r\n", "\n");
            if (normalized.Contains(oldInit, StringComparison.Ordinal)
                && normalized.Contains(oldRun, StringComparison.Ordinal)
                && normalized.Contains(oldCall, StringComparison.Ordinal))
            {
                normalized = normalized.Replace(oldInit, newInit, StringComparison.Ordinal)
                    .Replace(oldRun, newRun, StringComparison.Ordinal)
                    .Replace(oldCall, newCall, StringComparison.Ordinal);
                var newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
                text = normalized.Replace("\n", newline);
            }
        }

        var original = Encoding.UTF8.GetString(bytes, hasUtf8Bom ? Encoding.UTF8.Preamble.Length : 0,
            bytes.Length - (hasUtf8Bom ? Encoding.UTF8.Preamble.Length : 0));
        if (!text.Equals(original, StringComparison.Ordinal))
        {
            File.WriteAllText(loader, text, new UTF8Encoding(hasUtf8Bom));
        }
    }

    /// <summary>让 PyTorch/TensorRT 超分对模型声明的输入倍数统一补边并裁回原尺寸。</summary>
    private static void EnsurePytorchUpscaleCompatibility()
    {
        var loader = Path.Combine(CoreRoot, "python", "backend", "src", "pytorch", "UpscaleTorch.py");
        if (!File.Exists(loader)) return;
        var bytes = File.ReadAllBytes(loader);
        var hasUtf8Bom = bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble);
        var text = Encoding.UTF8.GetString(bytes, hasUtf8Bom ? Encoding.UTF8.Preamble.Length : 0,
            bytes.Length - (hasUtf8Bom ? Encoding.UTF8.Preamble.Length : 0));
        if (text.Contains("VIDEOENHANCER_UPSCALE_INPUT_MULTIPLE", StringComparison.Ordinal)) return;

        var newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var normalized = text.Replace("\r\n", "\n");
        const string oldInit =
            "        self.tilesize = tilesize\n" +
            "        self.tile = [self.tilesize, self.tilesize]";
        const string newInit =
            "        self.tilesize = tilesize\n" +
            "        # CLI 的本地能力清单为已知模型声明真实的输入尺寸倍数。\n" +
            "        self.input_multiple = max(1, int(os.environ.get(\"VIDEOENHANCER_UPSCALE_INPUT_MULTIPLE\", \"1\")))\n" +
            "        self.tile = [self.tilesize, self.tilesize]";
        const string oldModulo =
            "            match self.scale:\n" +
            "                case 1:\n" +
            "                    modulo = 4\n" +
            "                case 2:\n" +
            "                    modulo = 2\n" +
            "                case _:\n" +
            "                    modulo = 1";
        const string newModulo =
            "            match self.scale:\n" +
            "                case 1:\n" +
            "                    modulo = 4\n" +
            "                case 2:\n" +
            "                    modulo = 2\n" +
            "                case _:\n" +
            "                    modulo = 1\n" +
            "            modulo = math.lcm(modulo, self.input_multiple)";
        const string oldInference =
            "            if not self.use_tiling:\n" +
            "                output = self.upscale_model_wrapper(image_tensor)\n" +
            "            else:\n" +
            "                output = self.renderTiledImage(image_tensor)";
        const string newInference =
            "            if not self.use_tiling:\n" +
            "                original_h, original_w = image_tensor.shape[-2:]\n" +
            "                pad_h = (-original_h) % self.input_multiple\n" +
            "                pad_w = (-original_w) % self.input_multiple\n" +
            "                if pad_h or pad_w:\n" +
            "                    image_tensor = F.pad(image_tensor, (0, pad_w, 0, pad_h), \"replicate\")\n" +
            "                output = self.upscale_model_wrapper(image_tensor)\n" +
            "                output = output[:, :, : original_h * self.scale, : original_w * self.scale]\n" +
            "            else:\n" +
            "                output = self.renderTiledImage(image_tensor)";

        if (!normalized.Contains(oldInit, StringComparison.Ordinal)
            || !normalized.Contains(oldModulo, StringComparison.Ordinal)
            || !normalized.Contains(oldInference, StringComparison.Ordinal))
        {
            return;
        }
        normalized = normalized.Replace(oldInit, newInit, StringComparison.Ordinal)
            .Replace(oldModulo, newModulo, StringComparison.Ordinal)
            .Replace(oldInference, newInference, StringComparison.Ordinal);
        File.WriteAllText(loader, normalized.Replace("\n", newline), new UTF8Encoding(hasUtf8Bom));
    }

    private static int DownloadWithAria(string url, string destination, bool printComplete = true)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            var aria = Aria2NextExe;
            if (!File.Exists(aria))
                return Fail("缺少独立下载组件 aria2-next：" + aria + "。请重新安装完整发行包。", 1);
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
            PortablePaths.ConfigureChildProcess(start);
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
            if (!process.Start()) return Fail("无法启动 aria2-next", 1);
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

    private static int ExtractArchive(string archive, string outputDirectory, bool printComplete = true)
    {
        try
        {
            if (!File.Exists(archive)) return Fail("压缩文件不存在：" + archive, 1);
            if (printComplete) Console.WriteLine("EXTRACT_START|" + outputDirectory);
            ManagedArchiveExtractor.Extract(archive, outputDirectory,
                printComplete ? percent => Console.WriteLine("EXTRACT_PROGRESS|" + percent) : null);
            if (printComplete) Console.WriteLine("EXTRACT_COMPLETE|" + outputDirectory);
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
        ".png", ".jpg", ".jpeg", ".bmp", ".webp", ".tif", ".tiff", ".avif"
    };

    /// <summary>找到图片任务中的第一张有效图片，用于 TensorRT 输入尺寸探测。</summary>
    private static string? FindFirstImageInput(CliOptions o)
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

    private static int RunImageJob(CliOptions o)
    {
        if (!File.Exists(PythonExe)) return Fail("图片后端找不到便携 Python：" + PythonExe, 1);
        if (!File.Exists(ImageBackendScript)) return Fail("图片后端脚本不存在：" + ImageBackendScript, 1);
        if (!o.ImageOutputOriginal && string.IsNullOrWhiteSpace(o.ImageOutput))
        {
            return Fail("图片处理需要 --image-output <文件夹>，或使用 --image-output-original");
        }

        var model = ResolveModelWithTensorRtOutputScalePreset(o.Model, o.Backend, out var tensorRtImagePresetScale);
        if (model.Length == 0) return 1;
        if (o.Backend == "tensorrt")
        {
            var firstInput = FindFirstImageInput(o);
            if (firstInput is null)
                return Fail("TensorRT 图片任务没有找到可用于探测尺寸的输入图片", 1);
            var (width, height) = GetInputResolution(firstInput);
            if (width <= 0 || height <= 0)
                return Fail("TensorRT 无法探测输入图片尺寸：" + firstInput, 1);
            var imageScale = tensorRtImagePresetScale > 0
                ? tensorRtImagePresetScale.ToString(CultureInfo.InvariantCulture)
                : DetectScale(model);
            var imageScaleValue = int.TryParse(imageScale, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedImageScale)
                ? parsedImageScale
                : 0;
            model = EnsureTensorRtEngine(model, width, height, stopWatcher: null, outputScale: imageScaleValue,
                requestedPrecision: o.UpscalePrecision);
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
        PortablePaths.ConfigureChildProcess(start);
        start.Environment["PYTHONUTF8"] = "1";
        start.Environment["PYTHONIOENCODING"] = "utf-8";
        start.Environment["VIDEOENHANCER_UPSCALE_PRECISION"] =
            ResolveUpscalePrecision(model, o.Backend, o.UpscalePrecision);
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
        start.ArgumentList.Add("--ffmpeg-path");
        start.ArgumentList.Add(FfmpegExe);

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
    /// <remarks>cuda 接受 PTH/PT/PKL/CKPT/safetensors；tensorrt 接受可转换源权重；ncnn 接受含 .param/.bin 的模型文件夹。</remarks>
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
                     && (IsTensorRTEngineFile(c) || IsTensorRtConvertibleUpscaleSource(c))
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
                if (relativeName.Equals(requestedName, StringComparison.OrdinalIgnoreCase)
                    || relativeName.Equals(requestedWithoutExtension, StringComparison.OrdinalIgnoreCase)
                    || ModelBaseName(f).Equals(baseName, StringComparison.OrdinalIgnoreCase))
                {
                    return f;
                }
            }
        }

        Console.Error.WriteLine("[错误] 未找到可用模型：" + (string.IsNullOrWhiteSpace(requested) ? DefaultModel : requested));
        if (backend == "cuda" || backend == "tensorrt" || backend == "onnx" || backend == "flashvsr" || backend == "basicvsrpp")
        {
            Console.Error.WriteLine(backend == "basicvsrpp" ? "[提示] BasicVSR++ 后端需要 models\\BasicVSR++ 下的官方 .pth，或包含 config.py/chkpts.pth 的优化目录。" : backend == "tensorrt" ? "[提示] TensorRT 后端需要可转换的源权重，并按当前设备和输入尺寸自动编译。" : backend == "onnx" ? "[提示] ONNX 后端需要 models 或其子目录下的 .onnx 放大模型。" : "[提示] CUDA 后端需要 models 或其子目录下的 .pth/.pt/.pkl/.ckpt/.safetensors 放大模型。");
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
                Console.Error.WriteLine(backend == "tensorrt" ? "[提示] models 及其子目录下未找到可转换的源权重。" : backend == "onnx" ? "[提示] models 及其子目录下未找到 .onnx 放大模型文件。" : "[提示] models 及其子目录下未找到可用的 PyTorch/safetensors 放大模型文件。");
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

    /// <summary>是否为当前图像超分加载器可读取的权重文件。</summary>
    private static bool IsPthModelFile(string path)
    {
        var ext = Path.GetExtension(path);
        return ext.Equals(".pth", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".pt", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".pkl", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".ckpt", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".safetensors", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>当前 RIFE/GMFSS/GIMM 检测器明确支持的补帧权重格式。</summary>
    private static bool IsInterpolationWeightFile(string path)
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
        && File.Exists(Path.Combine(path, "chkpts.pth"));

    private static bool IsBasicVsrPlusPlusModel(string path) =>
        IsBasicVsrPlusPlusModelDirectory(path)
        || (File.Exists(path) && Path.GetExtension(path).Equals(".pth", StringComparison.OrdinalIgnoreCase)
            && IsInBasicVsrPlusPlusDirectory(path));

    private static string BasicVsrPlusPlusScale(string path) =>
        IsBasicVsrPlusPlusModelDirectory(path) ? "1" : "4";

    private static string ResolveModelWithTensorRtOutputScalePreset(string requested, string backend, out int presetScale)
    {
        presetScale = 0;
        if (backend == "tensorrt"
            && TryResolveTensorRtOutputScalePreset(requested, out var sourceRequest, out var scale))
        {
            requested = sourceRequest;
            presetScale = scale;
        }
        return ResolveModel(requested, backend);
    }

    private static bool TryResolveTensorRtOutputScalePreset(string requested, out string sourceRequest, out int scale)
    {
        sourceRequest = requested;
        scale = 0;
        if (string.IsNullOrWhiteSpace(requested)) return false;
        var match = Regex.Match(requested.Replace('\\', '/').Trim(),
            @"^(?<source>.*realesr-animevideov3)-(?<scale>[234])x$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!match.Success) return false;
        sourceRequest = match.Groups["source"].Value;
        return int.TryParse(match.Groups["scale"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out scale);
    }

    /// <summary>优先查询内置能力清单；未知模型才从文件名保守解析倍率。</summary>
    private static string? DetectScale(string modelFolder)
    {
        if (ModelCapabilityCatalog.TryGet(modelFolder, ModelsDir, out var capability))
        {
            return capability.Scale.ToString(CultureInfo.InvariantCulture);
        }
        if (IsBasicVsrPlusPlusModel(modelFolder))
        {
            return BasicVsrPlusPlusScale(modelFolder);
        }
        if (IsFlashVsrModelDirectory(modelFolder))
        {
            return "4";
        }
        var name = Path.GetFileName(modelFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var stem = Path.GetFileNameWithoutExtension(name);
        // RealESRGAN AnimeVideo v3 的官方文件名没有倍率后缀，但模型原生输出为 4 倍。
        if (stem.Equals("realesr-animevideov3", StringComparison.OrdinalIgnoreCase))
        {
            return "4";
        }
        // 优先取最后一个独立倍率标记。ONNX 文件名可能先包含 1x3xHxW 张量形状，
        // 直接取第一个 x 数字会把通道数误当成放大倍率。
        var matches = Regex.Matches(name, @"(?:^|[-_])(\d+)x(?=[-_.]|$)", RegexOptions.IgnoreCase);
        if (matches.Count > 0)
        {
            return matches[^1].Groups[1].Value;
        }
        // x4 形式也必须位于独立字段边界，避免把 fix1、fix2 等普通单词尾部误判为倍率。
        matches = Regex.Matches(name, @"(?:^|[-_])x(\d+)(?=[-_.v]|$)", RegexOptions.IgnoreCase);
        return matches.Count > 0 ? matches[^1].Groups[1].Value : null;
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
        bool dynamicOpticalFlow = false, double sceneThreshold = 4.0, int tileSize = 0, string precision = "auto")
    {
        var args = new List<string>
        {
            string.IsNullOrWhiteSpace(backendScript) ? BackendScript : backendScript,
            "-i", input,
            "-o", outputFile,
            "-b", backend is "cuda" or "tensorrt" ? (backend == "tensorrt" ? "tensorrt" : "pytorch") : backend,
            "--precision", precision,
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
            if (tileSize > 0 && backend is ("ncnn" or "cuda" or "tensorrt" or "onnx"))
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

        args.Add("--scene_detect_method");
        if (backend is "cuda" or "tensorrt")
        {
            // 当前模型包只提供 NCNN 格式的 EfficientNet 转场模型。CUDA/TensorRT
            // 主后端会把模型路径交给 torch.jit.load，因此改用 RVE 内置且无需模型的检测器。
            args.Add("pyscenedetect");
        }
        else
        {
            args.Add("sudo_scene_detect");
            args.Add("--scene_detect_model");
            args.Add(SceneDetectModel);
        }
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

    /// <summary>规范化用户请求的推理精度；auto 表示优先半精度并保留后端回退。</summary>
    private static string NormalisePrecisionOption(string value, string optionName)
    {
        var precision = (value ?? "auto").Trim().ToLowerInvariant();
        if (precision is "auto" or "float16" or "float32") return precision;
        Fail(optionName + " 仅支持 auto、float16 或 float32，当前值：" + value);
        return "";
    }

    private static string PrecisionDisplayName(string precision) => precision switch
    {
        "float32" => "FP32（强制）",
        "float16" => "FP16（强制）",
        _ => "FP16 优先（不兼容时回退 FP32）",
    };

    /// <summary>超分精度独立决策：用户强制 FP32 优先，已知 FP16 数值不稳定的模型保持 FP32。</summary>
    private static string ResolveUpscalePrecision(string modelPath, string backend, string requested)
    {
        if (requested == "float32") return "float32";
        // DAT2 与 AniToon-RPLKSRL 在当前 PyTorch/CUDA FP16 路径会直接输出 NaN；
        // 后续转字节会把 NaN 隐式变为黑值，因此不能依赖编码阶段发现问题。
        if (backend == "cuda" && Regex.IsMatch(
                ModelBaseName(modelPath), @"SwinIR|GRL|DAT2|AniToon-RPLKSRL", RegexOptions.IgnoreCase))
            return "float32";
        if (backend == "tensorrt" && Regex.IsMatch(ModelBaseName(modelPath), @"GRL", RegexOptions.IgnoreCase))
            return "float32";
        return requested;
    }

    /// <summary>补帧精度独立决策：GIMM 的 CUDA 路径必须使用 FP32，其他模型优先半精度。</summary>
    private static string ResolveInterpPrecision(string? modelPath, string backend, string requested)
    {
        if (requested == "float32") return "float32";
        if (backend == "cuda" && Regex.IsMatch(ModelBaseName(modelPath ?? ""), @"GIMM", RegexOptions.IgnoreCase))
            return "float32";
        return requested;
    }

    /// <summary>解析补帧模型路径：完整路径 / Frame-Interpolation 下相对路径 / 模型名；返回空串表示失败。</summary>
    /// <remarks>新目录按后端区分 NCNN 文件夹和 PyTorch 权重；TensorRT 由 RVE 从 RIFE 权重自动构建 Engine。</remarks>
    private static string ResolveInterpModel(string requested, string backend)
    {
        var raw = requested.Trim().Trim('"');
        var candidates = new List<string> { Path.GetFullPath(raw) };
        if (!Path.IsPathRooted(raw))
        {
            candidates.Add(Path.Combine(FrameInterpolationDir, raw));
            candidates.Add(Path.Combine(ModelsDir, raw));
            candidates.Add(Path.Combine(UserInterpolationDir, raw));
            candidates.Add(Path.Combine(LegacyRifeDir, raw));
        }

        foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (backend == "cuda")
            {
                if (File.Exists(candidate) && IsInterpolationWeightFile(candidate)
                    && InspectInterpolationCapabilities(new[] { candidate })
                        .TryGetValue(Path.GetFullPath(candidate), out var capability)
                    && string.IsNullOrWhiteSpace(capability.Error) && capability.Cuda)
                {
                    return Path.GetFullPath(candidate);
                }
            }
            else if (backend == "tensorrt")
            {
                if (File.Exists(candidate) && IsRifeInterpolationSource(candidate)) return Path.GetFullPath(candidate);
            }
            else if (Directory.Exists(candidate) && IsNcnnModelFolder(candidate))
            {
                return Path.GetFullPath(candidate);
            }
        }

        var normalizedRaw = raw.Replace('\\', '/').TrimEnd('/');
        var discovered = DiscoverInterpModels(backend);

        // UI 保存的是相对架构路径。先处理这个无歧义的精确值，避免把
        // rife4.26.heavy 中的 .heavy 误当作扩展名后又匹配到 rife4.26。
        var exactMatched = discovered
            .Where(path => InterpModelDisplayName(path).Equals(normalizedRaw, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (exactMatched.Count == 1)
        {
            return exactMatched[0];
        }
        if (exactMatched.Count > 1)
        {
            Console.Error.WriteLine("[错误] 补帧模型相对路径不唯一：" + raw);
            return "";
        }

        var requestedName = InterpModelLookupName(normalizedRaw);
        var matched = discovered
            .Where(path => InterpModelLookupName(InterpModelDisplayName(path))
                .Equals(requestedName, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (matched.Count == 1)
        {
            return matched[0];
        }
        if (matched.Count > 1)
        {
            Console.Error.WriteLine("[错误] 补帧模型名不唯一，请使用包含架构目录的相对路径：" + raw);
            foreach (var path in matched)
            {
                Console.Error.WriteLine("       " + InterpModelDisplayName(path));
            }
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

    /// <summary>生成补帧模型的短名称，只剥离真实权重扩展名，保留 .heavy 等模型名后缀。</summary>
    private static string InterpModelLookupName(string value)
    {
        var fileName = Path.GetFileName(value.Replace('\\', '/').TrimEnd('/'));
        var extension = Path.GetExtension(fileName);
        return new[] { ".pth", ".pt", ".pkl", ".engine" }
            .Contains(extension, StringComparer.OrdinalIgnoreCase)
            ? Path.GetFileNameWithoutExtension(fileName)
            : fileName;
    }

    /// <summary>发现补帧模型，并兼容读取旧 models\RIFE 目录。</summary>
    private static List<string> DiscoverInterpModels(string backend)
    {
        var roots = new[] { FrameInterpolationDir, UserInterpolationDir, LegacyRifeDir }
            .Where(Directory.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (roots.Count == 0)
        {
            return new List<string>();
        }
        if (backend is "cuda" or "tensorrt")
        {
            var weights = roots.SelectMany(root => new[] { "*.pth", "*.pt", "*.pkl" }
                    .SelectMany(pattern => Directory.GetFiles(root, pattern, SearchOption.AllDirectories)))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(p => p, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
            var capabilities = InspectInterpolationCapabilities(weights);
            return weights.Where(path => capabilities.TryGetValue(Path.GetFullPath(path), out var capability)
                    && string.IsNullOrWhiteSpace(capability.Error)
                    && (backend == "cuda" ? capability.Cuda : capability.TensorRT))
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
        else if (IsPathUnder(path, UserInterpolationDir))
        {
            relative = Path.GetRelativePath(ModelsDir, path);
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
        if (!File.Exists(path) || !IsInterpolationWeightFile(path)) return false;
        var capabilities = InspectInterpolationCapabilities(new[] { path });
        return capabilities.TryGetValue(Path.GetFullPath(path), out var capability)
            && string.IsNullOrWhiteSpace(capability.Error)
            && capability.TensorRT;
    }

    private sealed record InterpolationCapability(
        string Path, string Architecture, string BaseArchitecture, bool Cuda, bool TensorRT, string Error);

    private sealed record CachedInterpolationCapability(
        long Length, long LastWriteTimeUtcTicks, InterpolationCapability Capability);

    private static Dictionary<string, CachedInterpolationCapability> LoadInterpolationCapabilityCache()
    {
        var cache = new Dictionary<string, CachedInterpolationCapability>(StringComparer.OrdinalIgnoreCase);
        try
        {
            if (!File.Exists(InterpolationCapabilityCachePath)) return cache;
            using var document = JsonDocument.Parse(File.ReadAllBytes(InterpolationCapabilityCachePath));
            var root = document.RootElement;
            if (root.GetProperty("version").GetInt32() != InterpolationCapabilityCacheVersion) return cache;
            foreach (var item in root.GetProperty("entries").EnumerateArray())
            {
                var path = Path.GetFullPath(item.GetProperty("path").GetString() ?? "");
                if (string.IsNullOrWhiteSpace(path)) continue;
                var capability = new InterpolationCapability(
                    path,
                    item.GetProperty("architecture").GetString() ?? "",
                    item.GetProperty("base_architecture").GetString() ?? "",
                    item.GetProperty("cuda").GetBoolean(),
                    item.GetProperty("tensorrt").GetBoolean(),
                    item.GetProperty("error").GetString() ?? "");
                cache[path] = new CachedInterpolationCapability(
                    item.GetProperty("length").GetInt64(),
                    item.GetProperty("last_write_utc_ticks").GetInt64(),
                    capability);
            }
        }
        catch
        {
            // 缓存损坏或被旧进程同时替换时直接重新检查，不影响模型列表。
        }
        return cache;
    }

    private static void SaveInterpolationCapabilityCache(
        Dictionary<string, CachedInterpolationCapability> cache)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(InterpolationCapabilityCachePath)!);
            using var buffer = new MemoryStream();
            using (var writer = new Utf8JsonWriter(buffer))
            {
                writer.WriteStartObject();
                writer.WriteNumber("version", InterpolationCapabilityCacheVersion);
                writer.WriteStartArray("entries");
                foreach (var entry in cache.Values
                             .Where(entry => File.Exists(entry.Capability.Path))
                             .Take(512))
                {
                    writer.WriteStartObject();
                    writer.WriteString("path", entry.Capability.Path);
                    writer.WriteNumber("length", entry.Length);
                    writer.WriteNumber("last_write_utc_ticks", entry.LastWriteTimeUtcTicks);
                    writer.WriteString("architecture", entry.Capability.Architecture);
                    writer.WriteString("base_architecture", entry.Capability.BaseArchitecture);
                    writer.WriteBoolean("cuda", entry.Capability.Cuda);
                    writer.WriteBoolean("tensorrt", entry.Capability.TensorRT);
                    writer.WriteString("error", entry.Capability.Error);
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();
                writer.WriteEndObject();
            }
            File.WriteAllBytes(InterpolationCapabilityCachePath, buffer.ToArray());
        }
        catch
        {
            // 缓存只是性能优化；只读目录或并发写入失败时仍使用本次检查结果。
        }
    }

    /// <summary>读取权重内部结构，不再用扩展名或目录名猜测补帧架构。</summary>
    private static Dictionary<string, InterpolationCapability> InspectInterpolationCapabilities(IEnumerable<string> modelPaths)
    {
        var paths = modelPaths.Select(Path.GetFullPath)
            .Where(File.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var result = new Dictionary<string, InterpolationCapability>(StringComparer.OrdinalIgnoreCase);
        if (paths.Count == 0)
        {
            return result;
        }

        var cache = LoadInterpolationCapabilityCache();
        var pendingPaths = new List<string>();
        foreach (var path in paths)
        {
            var info = new FileInfo(path);
            if (cache.TryGetValue(path, out var cached)
                && cached.Length == info.Length
                && cached.LastWriteTimeUtcTicks == info.LastWriteTimeUtc.Ticks)
            {
                result[path] = cached.Capability;
            }
            else
            {
                pendingPaths.Add(path);
            }
        }
        if (pendingPaths.Count == 0
            || !File.Exists(PythonExe)
            || !File.Exists(InterpolationInspectorScript))
        {
            return result;
        }

        var args = new List<string> { InterpolationInspectorScript };
        args.AddRange(pendingPaths);
        var inspected = RunProcessCapture(PythonExe, args.ToArray(), 180);
        var jsonLine = inspected.Output.Replace("\r", "")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .LastOrDefault(line => line.StartsWith("[", StringComparison.Ordinal));
        if (string.IsNullOrWhiteSpace(jsonLine)) return result;

        try
        {
            using var document = JsonDocument.Parse(jsonLine);
            foreach (var item in document.RootElement.EnumerateArray())
            {
                var fullPath = Path.GetFullPath(item.GetProperty("path").GetString() ?? "");
                result[fullPath] = new InterpolationCapability(
                    fullPath,
                    item.GetProperty("architecture").GetString() ?? "",
                    item.GetProperty("base_architecture").GetString() ?? "",
                    item.GetProperty("cuda").GetBoolean(),
                    item.GetProperty("tensorrt").GetBoolean(),
                    item.GetProperty("error").GetString() ?? "");
                var info = new FileInfo(fullPath);
                cache[fullPath] = new CachedInterpolationCapability(
                    info.Length, info.LastWriteTimeUtc.Ticks, result[fullPath]);
            }
            SaveInterpolationCapabilityCache(cache);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("[警告] 补帧模型架构检查输出无效：" + ex.Message);
        }
        return result;
    }

    private static int InspectInterpolationModel(string requested)
    {
        var path = File.Exists(requested) ? Path.GetFullPath(requested) : ResolveInterpModel(requested, "cuda");
        if (string.IsNullOrWhiteSpace(path)) return 2;
        var capabilities = InspectInterpolationCapabilities(new[] { path });
        if (!capabilities.TryGetValue(path, out var capability))
            return Fail("补帧模型检查器不可用，请确认 inspect_interpolation_models.py 已安装");
        static string JsonString(string value) => "\"" + JsonEncodedText.Encode(value).ToString() + "\"";
        Console.WriteLine("{" +
            "\"path\":" + JsonString(capability.Path) + "," +
            "\"architecture\":" + JsonString(capability.Architecture) + "," +
            "\"base_architecture\":" + JsonString(capability.BaseArchitecture) + "," +
            "\"cuda\":" + capability.Cuda.ToString().ToLowerInvariant() + "," +
            "\"tensorrt\":" + capability.TensorRT.ToString().ToLowerInvariant() + "," +
            "\"error\":" + JsonString(capability.Error) + "}");
        return string.IsNullOrWhiteSpace(capability.Error) ? 0 : 2;
    }

    private static int InspectUpscaleModel(string requested)
    {
        var path = Path.GetFullPath(requested.Trim().Trim('"'));
        var manager = new ModelImportManager(ModelsDir, PythonExe, UpscaleInspectorScript, InterpolationInspectorScript);
        var inspection = manager.Inspect(path);
        WriteInspectionJson(inspection);
        return string.IsNullOrWhiteSpace(inspection.Error) ? 0 : 2;
    }

    private static int ImportModels(string requested, bool json)
    {
        var source = Path.GetFullPath(requested.Trim().Trim('"'));
        string? extractionRoot = null;
        try
        {
            if (File.Exists(source) && IsModelArchive(source))
            {
                extractionRoot = PortablePaths.CreateWorkDirectory("model-import");
                if (ExtractArchive(source, extractionRoot, printComplete: false) != 0)
                    return 1;
                source = extractionRoot;
            }
            var manager = new ModelImportManager(ModelsDir, PythonExe, UpscaleInspectorScript, InterpolationInspectorScript);
            var results = manager.Import(source);
            if (json)
                WriteImportResultsJson(results);
            else
                foreach (var result in results)
                    if (result.Success)
                        Console.WriteLine("[已导入] " + result.Id + "  " + string.Join('/', result.Backends));
                    else
                        Console.Error.WriteLine("[失败] " + result.Source + "：" + result.Error);
            return results.Count > 0 && results.All(result => result.Success) ? 0 : 2;
        }
        finally
        {
            if (extractionRoot is not null && Directory.Exists(extractionRoot))
            {
                try { Directory.Delete(extractionRoot, recursive: true); }
                catch { }
            }
        }
    }

    private static bool IsModelArchive(string path) =>
        new[] { ".zip", ".7z", ".rar", ".tar", ".gz", ".xz", ".zst" }
            .Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    private static void WriteInspectionJson(ModelImportInspection item)
    {
        using var writer = new Utf8JsonWriter(Console.OpenStandardOutput());
        writer.WriteStartObject();
        writer.WriteString("path", item.Path);
        writer.WriteString("format", item.Format);
        writer.WriteString("task", item.Task);
        writer.WriteString("architecture", item.Architecture);
        writer.WriteString("purpose", item.Purpose);
        writer.WriteNumber("scale", item.Scale);
        writer.WriteNumber("inputChannels", item.InputChannels);
        writer.WriteNumber("outputChannels", item.OutputChannels);
        writer.WriteNumber("inputMultiple", item.InputMultiple);
        writer.WriteNumber("minimumSize", item.MinimumSize);
        writer.WriteBoolean("square", item.Square);
        writer.WriteBoolean("supportsHalf", item.SupportsHalf);
        writer.WriteBoolean("supportsBfloat16", item.SupportsBFloat16);
        writer.WriteString("tiling", item.Tiling);
        writer.WriteStartArray("backends");
        foreach (var backend in item.Backends) writer.WriteStringValue(backend);
        writer.WriteEndArray();
        writer.WriteString("error", item.Error);
        writer.WriteEndObject();
        writer.Flush();
        Console.WriteLine();
    }

    private static void WriteImportResultsJson(IReadOnlyList<ModelImportResult> results)
    {
        using var writer = new Utf8JsonWriter(Console.OpenStandardOutput());
        writer.WriteStartArray();
        foreach (var item in results)
        {
            writer.WriteStartObject();
            writer.WriteBoolean("success", item.Success);
            writer.WriteString("source", item.Source);
            writer.WriteString("id", item.Id);
            writer.WriteString("installedPath", item.InstalledPath);
            writer.WriteString("task", item.Task);
            writer.WriteString("architecture", item.Architecture);
            writer.WriteString("purpose", item.Purpose);
            writer.WriteNumber("scale", item.Scale);
            writer.WriteStartArray("backends");
            foreach (var backend in item.Backends) writer.WriteStringValue(backend);
            writer.WriteEndArray();
            writer.WriteString("error", item.Error);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        writer.Flush();
        Console.WriteLine();
    }

    private static int PrepareRifeTensorRTEngine(string requested, int width, int height, bool staticShape)
    {
        if (!File.Exists(PythonExe)) return Fail("找不到便携 Python：" + PythonExe);
        if (!File.Exists(RifeTensorRTPrepareScript))
            return Fail("缺少 RIFE TensorRT 预构建脚本：" + RifeTensorRTPrepareScript);
        var model = File.Exists(requested) ? Path.GetFullPath(requested) : ResolveInterpModel(requested, "tensorrt");
        if (string.IsNullOrWhiteSpace(model)) return 2;

        var start = new ProcessStartInfo
        {
            FileName = PythonExe,
            WorkingDirectory = Path.GetDirectoryName(RifeTensorRTPrepareScript)!,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        PortablePaths.ConfigureChildProcess(start);
        start.Environment["PYTHONUTF8"] = "1";
        start.Environment["PYTHONIOENCODING"] = "utf-8";
        foreach (var arg in new[]
        {
            RifeTensorRTPrepareScript, model,
            "--width", width.ToString(CultureInfo.InvariantCulture),
            "--height", height.ToString(CultureInfo.InvariantCulture)
        }) start.ArgumentList.Add(arg);
        if (staticShape) start.ArgumentList.Add("--static-shape");

        using var process = Process.Start(start);
        if (process is null) return Fail("无法启动 RIFE TensorRT 预构建进程");
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) Console.WriteLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) Console.Error.WriteLine(e.Data); };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();
        process.WaitForExit();
        return process.ExitCode;
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

    private sealed class SegmentRequest
    {
        public long Start { get; set; }
        public long End { get; set; }
        public double StartSeconds { get; set; }
        public double EndSeconds { get; set; }
        public string Backend { get; set; } = "";
        public string Model { get; set; } = "";
        public int TargetWidth { get; set; }
        public int TargetHeight { get; set; }
    }

    private sealed class PreparedSegment
    {
        public long Start { get; set; }
        public long End { get; set; }
        public double StartSeconds { get; set; }
        public double EndSeconds { get; set; }
        public string Backend { get; set; } = "";
        public string Model { get; set; } = "";
        public int Scale { get; set; }
        public int InputMultiple { get; set; } = 1;
        public int OutputWidth { get; set; }
        public int OutputHeight { get; set; }
    }

    private sealed class HybridSegmentPart
    {
        public PreparedSegment? CustomSegment { get; set; }
        public string ModelChunkPath { get; set; } = "";
        public long Start { get; set; }
        public long End { get; set; }
    }

    private static string EncodePreparedSegments(IEnumerable<PreparedSegment> segments)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartArray();
            foreach (var segment in segments)
            {
                writer.WriteStartObject();
                writer.WriteNumber("start", segment.Start);
                writer.WriteNumber("end", segment.End);
                writer.WriteString("backend", segment.Backend);
                writer.WriteString("model", segment.Model);
                writer.WriteNumber("scale", segment.Scale);
                writer.WriteNumber("inputMultiple", segment.InputMultiple);
                writer.WriteNumber("outputWidth", segment.OutputWidth);
                writer.WriteNumber("outputHeight", segment.OutputHeight);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
        }
        return Convert.ToBase64String(stream.ToArray());
    }

    private static string EncodeStringList(IEnumerable<string> values)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartArray();
            foreach (var value in values) writer.WriteStringValue(value);
            writer.WriteEndArray();
        }
        return Convert.ToBase64String(stream.ToArray());
    }

    private static List<string> GetFfmpegEncoderArguments(string settings)
    {
        var tokens = Tokenize(settings);
        while (tokens.Count > 0 && tokens[^1].Equals("-y", StringComparison.OrdinalIgnoreCase))
            tokens.RemoveAt(tokens.Count - 1);
        if (tokens.Count == 0) throw new ArgumentException("FFmpeg 参数中缺少输出文件");
        tokens.RemoveAt(tokens.Count - 1);
        var cleaned = new List<string>();
        for (var index = 0; index < tokens.Count; index++)
        {
            var token = tokens[index];
            if (token.Equals("-map", StringComparison.OrdinalIgnoreCase))
            {
                index++;
                continue;
            }
            if (index + 1 < tokens.Count &&
                (token.Equals("-map_metadata", StringComparison.OrdinalIgnoreCase) ||
                 token.Equals("-map_chapters", StringComparison.OrdinalIgnoreCase)) &&
                tokens[index + 1].StartsWith('0'))
            {
                cleaned.Add(token);
                var target = tokens[index + 1];
                cleaned.Add(target.Length > 1 ? "1" + target[1..] : "1");
                index++;
                continue;
            }
            cleaned.Add(token);
        }
        return cleaned;
    }

    private static bool IsSegmentModelBackend(string backend) =>
        backend is "ncnn" or "cuda" or "tensorrt" or "onnx";

    private static bool ScriptSupportsArgument(string script, string argument)
    {
        try
        {
            return File.ReadAllText(script).Contains(argument, StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    private static int RunDirectFfmpeg(
        string title, IReadOnlyList<string> arguments, StopWatcher? stopWatcher,
        out TimeSpan elapsed, string pauseShm = "")
    {
        Console.WriteLine("[分段直连] " + title);
        var start = new ProcessStartInfo
        {
            FileName = FfmpegExe,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);

        using var process = new Process { StartInfo = start };
        var stderr = new StringBuilder();
        process.ErrorDataReceived += (_, e) =>
        {
            if (string.IsNullOrWhiteSpace(e.Data)) return;
            lock (stderr) stderr.AppendLine(e.Data);
        };
        var timer = Stopwatch.StartNew();
        try
        {
            if (!process.Start())
            {
                elapsed = timer.Elapsed;
                return Fail("无法启动 FFmpeg：" + title, 1);
            }
        }
        catch (Exception ex)
        {
            elapsed = timer.Elapsed;
            return Fail("无法启动 FFmpeg（" + title + "）：" + ex.Message, 1);
        }
        process.BeginErrorReadLine();
        var suspended = false;
        while (!process.HasExited)
        {
            var shouldPause = !string.IsNullOrWhiteSpace(pauseShm) && ReadShmByte(pauseShm) == 1;
            if (shouldPause && !suspended)
            {
                try
                {
                    if (NtSuspendProcess(process.Handle) == 0)
                        suspended = true;
                }
                catch
                {
                    // 暂停能力失败时继续执行，避免影响正常处理。
                }
            }
            else if (!shouldPause && suspended)
            {
                try { NtResumeProcess(process.Handle); } catch { }
                suspended = false;
            }
            if (stopWatcher?.IsStopRequested() == true)
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                elapsed = timer.Elapsed;
                return 130;
            }
            Thread.Sleep(100);
        }
        process.WaitForExit();
        timer.Stop();
        elapsed = timer.Elapsed;
        if (process.ExitCode == 0) return 0;

        string detail;
        lock (stderr) detail = stderr.ToString().Trim();
        Console.Error.WriteLine($"[分段直连失败] {title}；FFmpeg 退出码 {process.ExitCode}");
        if (!string.IsNullOrWhiteSpace(detail)) Console.Error.WriteLine(detail);
        return process.ExitCode;
    }

    private static string ResolveAnime4kShaderForFfmpeg(string requested)
    {
        if (Path.IsPathRooted(requested) && File.Exists(requested))
            return Path.GetFullPath(requested);
        var name = Path.GetFileName(requested);
        var roots = new List<string>();
        var configured = Environment.GetEnvironmentVariable("VIDEOENHANCER_ANIME4K_DIR");
        if (!string.IsNullOrWhiteSpace(configured)) roots.Add(configured);
        var ffmpegDirectory = Path.GetDirectoryName(FfmpegExe) ?? "";
        roots.Add(Path.Combine(ffmpegDirectory, "libplacebo"));
        var parent = Directory.GetParent(ffmpegDirectory)?.FullName;
        if (!string.IsNullOrWhiteSpace(parent)) roots.Add(Path.Combine(parent, "libplacebo"));
        foreach (var root in roots)
        {
            var candidate = Path.Combine(root, name);
            if (File.Exists(candidate)) return Path.GetFullPath(candidate);
        }
        return "";
    }

    private static string EscapeFfmpegFilterPath(string path) =>
        Path.GetFullPath(path).Replace("\\", "/", StringComparison.Ordinal)
            .Replace(":", "\\:", StringComparison.Ordinal)
            .Replace("'", "\\'", StringComparison.Ordinal);

    private static string BuildDirectCustomFilter(PreparedSegment segment)
    {
        if (segment.Backend == "ffmpeg")
            return $"scale={segment.OutputWidth}:{segment.OutputHeight}:flags={segment.Model}";
        var shader = ResolveAnime4kShaderForFfmpeg(segment.Model);
        if (shader.Length == 0)
            throw new FileNotFoundException("找不到 Anime4K 着色器：" + segment.Model);
        return $"libplacebo=w={segment.OutputWidth}:h={segment.OutputHeight}:"
               + $"custom_shader_path='{EscapeFfmpegFilterPath(shader)}'";
    }

    private static List<string> RebaseMetadataArguments(
        IReadOnlyList<string> arguments, int sourceInputIndex)
    {
        var result = arguments.ToList();
        for (var index = 0; index + 1 < result.Count; index++)
        {
            if (result[index] is not ("-map_metadata" or "-map_chapters")) continue;
            var value = result[index + 1];
            if (!value.StartsWith('1')) continue;
            result[index + 1] = sourceInputIndex.ToString(CultureInfo.InvariantCulture) + value[1..];
            index++;
        }
        return result;
    }

    private static int RunDirectCustomSegment(
        string input, string output, PreparedSegment segment, VideoProbeInfo video,
        bool secondsMode, bool finalOutput, IReadOnlyList<string> finalEncoderArguments,
        bool overwrite, StopWatcher? stopWatcher, string pauseShm)
    {
        var expectedFrames = segment.End - segment.Start + 1;
        string filter;
        try
        {
            filter = BuildDirectCustomFilter(segment);
        }
        catch (Exception ex)
        {
            return Fail(ex.Message, 1);
        }

        var args = new List<string> { "-hide_banner", "-loglevel", "error", overwrite ? "-y" : "-n" };
        if (secondsMode)
        {
            if (segment.StartSeconds > 0.000001)
            {
                args.Add("-ss");
                args.Add(segment.StartSeconds.ToString("0.########", CultureInfo.InvariantCulture));
            }
            args.Add("-i");
            args.Add(input);
            filter = "setpts=PTS-STARTPTS," + filter;
        }
        else
        {
            args.Add("-i");
            args.Add(input);
            var startFrame = segment.Start - 1;
            var endFrame = segment.End - 1;
            var rate = video.FrameRate.ToString("0.########", CultureInfo.InvariantCulture);
            filter =
                $"select=between(n\\,{startFrame.ToString(CultureInfo.InvariantCulture)}\\,{endFrame.ToString(CultureInfo.InvariantCulture)}),"
                + $"setpts=N/{rate}/TB,{filter}";
        }

        args.Add("-map");
        args.Add("0:v:0");
        if (finalOutput)
        {
            args.Add("-map");
            args.Add("0:a?");
            args.Add("-map");
            args.Add("0:s?");
        }
        args.Add("-vf");
        args.Add(filter);
        args.Add("-frames:v");
        args.Add(expectedFrames.ToString(CultureInfo.InvariantCulture));
        args.Add("-fps_mode");
        args.Add("passthrough");
        if (finalOutput)
        {
            args.AddRange(RebaseMetadataArguments(finalEncoderArguments, 0));
        }
        else
        {
            args.AddRange(new[] { "-an", "-sn", "-c:v", "ffv1", "-level", "3", "-pix_fmt", "rgb24" });
        }
        args.Add(output);

        var exit = RunDirectFfmpeg(
            $"{segment.Backend} {segment.Start}-{segment.End} 帧直接处理",
            args, stopWatcher, out var elapsed, pauseShm);
        if (exit != 0) return exit;
        var result = ProbeGeneratedVideoFast(output);
        if (result is null
            || result.Width != segment.OutputWidth
            || result.Height != segment.OutputHeight
            || result.Frames != expectedFrames)
        {
            return Fail(
                $"直接 {segment.Backend} 分段输出校验失败："
                + $"{result?.Width ?? 0}x{result?.Height ?? 0}, {result?.Frames ?? 0} 帧；"
                + $"预期 {segment.OutputWidth}x{segment.OutputHeight}, {expectedFrames} 帧",
                1);
        }
        var fps = expectedFrames / Math.Max(0.000001, elapsed.TotalSeconds);
        Console.WriteLine(
            $"SEGMENTED_DIRECT_DONE|{segment.Backend}|{segment.Start}|{segment.End}|"
            + $"{expectedFrames}|{elapsed.TotalSeconds:0.000}|{fps:0.00}");
        return 0;
    }

    private static int ExtractModelSegmentGroup(
        string input, string output, PreparedSegment first, PreparedSegment last,
        VideoProbeInfo video, bool secondsMode, StopWatcher? stopWatcher, string pauseShm)
    {
        var expectedFrames = last.End - first.Start + 1;
        if (secondsMode)
        {
            var copyArgs = new List<string> { "-hide_banner", "-loglevel", "error", "-y" };
            if (first.StartSeconds > 0.000001)
            {
                copyArgs.Add("-ss");
                copyArgs.Add(first.StartSeconds.ToString("0.########", CultureInfo.InvariantCulture));
            }
            copyArgs.AddRange(new[]
            {
                "-i", input,
                "-t", Math.Max(0.000001, last.EndSeconds - first.StartSeconds)
                    .ToString("0.########", CultureInfo.InvariantCulture),
                "-map", "0:v:0", "-an", "-sn", "-c:v", "copy",
                "-avoid_negative_ts", "make_zero", output,
            });
            var copyExit = RunDirectFfmpeg(
                $"模型段关键帧快速切片 {first.StartSeconds:0.###}-{last.EndSeconds:0.###}s",
                copyArgs, stopWatcher, out _, pauseShm);
            if (copyExit == 130) return 130;
            var copied = copyExit == 0 ? ProbeGeneratedVideoFast(output) : null;
            if (copied is not null && copied.Frames == expectedFrames)
            {
                Console.WriteLine($"[分段直连] 模型段 stream-copy 校验通过：{expectedFrames} 帧。");
                return 0;
            }
            try { if (File.Exists(output)) File.Delete(output); } catch { }
            Console.WriteLine(
                $"[分段直连] 模型段 stream-copy 帧数不匹配（{copied?.Frames ?? 0}/{expectedFrames}），"
                + "回退到精确重编码切片。");
        }

        var args = new List<string> { "-hide_banner", "-loglevel", "error", "-y" };
        string? selectFilter = null;
        if (secondsMode)
        {
            if (first.StartSeconds > 0.000001)
            {
                args.Add("-ss");
                args.Add(first.StartSeconds.ToString("0.########", CultureInfo.InvariantCulture));
            }
            args.Add("-i");
            args.Add(input);
        }
        else
        {
            args.Add("-i");
            args.Add(input);
            var rate = video.FrameRate.ToString("0.########", CultureInfo.InvariantCulture);
            selectFilter =
                $"select=between(n\\,{(first.Start - 1).ToString(CultureInfo.InvariantCulture)}\\,{(last.End - 1).ToString(CultureInfo.InvariantCulture)}),"
                + $"setpts=N/{rate}/TB";
        }
        args.AddRange(new[] { "-map", "0:v:0" });
        if (selectFilter is not null)
        {
            args.Add("-vf");
            args.Add(selectFilter);
        }
        args.Add("-frames:v");
        args.Add(expectedFrames.ToString(CultureInfo.InvariantCulture));
        args.AddRange(new[]
        {
            "-an", "-sn", "-c:v", "ffv1", "-level", "3", "-pix_fmt", "rgb24",
            "-r", video.FrameRate.ToString("0.########", CultureInfo.InvariantCulture),
            output,
        });
        var exit = RunDirectFfmpeg("模型段精确源切片", args, stopWatcher, out _, pauseShm);
        if (exit != 0) return exit;
        var exact = ProbeGeneratedVideoFast(output);
        return exact is not null && exact.Frames == expectedFrames
            ? 0
            : Fail($"模型段源切片帧数异常：{exact?.Frames ?? 0}/{expectedFrames}", 1);
    }

    private static int RunModelSegmentGroup(
        string sourceClip, string outputClip, IReadOnlyList<PreparedSegment> group,
        long absoluteStart, string pauseShm, StopWatcher? stopWatcher, int tileSize,
        string segmentedPrecision)
    {
        var rebased = group.Select(segment => new PreparedSegment
        {
            Start = segment.Start - absoluteStart + 1,
            End = segment.End - absoluteStart + 1,
            Backend = segment.Backend,
            Model = segment.Model,
            Scale = segment.Scale,
            InputMultiple = segment.InputMultiple,
            OutputWidth = segment.OutputWidth,
            OutputHeight = segment.OutputHeight,
        }).ToList();
        var segmentPayload = EncodePreparedSegments(rebased);
        var encoderPayload = EncodeStringList(new[]
        {
            "-c:v", "ffv1", "-level", "3", "-pix_fmt", "rgb24", "-an", "-sn",
        });
        var script = EnsureEmbeddedFile(EmbeddedSegmentedBackendResource, "rve-segmented-backend.py");
        var arguments = new List<string>
        {
            script,
            "--input", sourceClip,
            "--output", outputClip,
            "--segments-base64", segmentPayload,
            "--encoder-args-base64", encoderPayload,
            "--ffmpeg-path", FfmpegExe,
            "--overwrite",
        };
        if (ScriptSupportsArgument(script, "--tile-size"))
        {
            arguments.Add("--tile-size");
            arguments.Add(tileSize.ToString(CultureInfo.InvariantCulture));
        }
        if (!string.IsNullOrWhiteSpace(pauseShm))
        {
            arguments.Add("--pause-shm");
            arguments.Add(pauseShm);
        }
        var distinctBackends = group.Select(segment => segment.Backend).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var backend = distinctBackends.Count == 1 ? distinctBackends[0] : "mixed";
        var model = distinctBackends.Count == 1 ? group[0].Model : "";
        var exit = LaunchBackend(
            arguments, sourceClip, model, outputClip,
            "-c:v ffv1 -level 3 -pix_fmt rgb24 -an -sn",
            stopWatcher, null, null, backend, pauseShm,
            "分段模型组", isFinalStage: false, upscalePrecision: segmentedPrecision);
        if (exit != 0) return exit;
        var expectedFrames = group[^1].End - group[0].Start + 1;
        var result = ProbeGeneratedVideoFast(outputClip);
        return result is not null
               && result.Width == group[0].OutputWidth
               && result.Height == group[0].OutputHeight
               && result.Frames == expectedFrames
            ? 0
            : Fail(
                $"模型分段组输出校验失败：{result?.Width ?? 0}x{result?.Height ?? 0}, "
                + $"{result?.Frames ?? 0} 帧；预期 {group[0].OutputWidth}x{group[0].OutputHeight}, {expectedFrames} 帧",
                1);
    }

    private static int FinalizeHybridSegments(
        IReadOnlyList<HybridSegmentPart> parts, string originalInput, string outputFile,
        IReadOnlyList<string> encoderArguments, bool overwrite, StopWatcher? stopWatcher,
        VideoProbeInfo sourceVideo, bool secondsMode, int outputWidth, int outputHeight,
        string pauseShm)
    {
        var args = new List<string>
        {
            "-hide_banner", "-loglevel", "error", overwrite ? "-y" : "-n",
            "-i", originalInput,
        };
        var modelInputIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in parts.Where(part => part.CustomSegment is null))
        {
            if (modelInputIndex.ContainsKey(part.ModelChunkPath)) continue;
            var inputIndex = modelInputIndex.Count + 1;
            modelInputIndex[part.ModelChunkPath] = inputIndex;
            args.Add("-i");
            args.Add(part.ModelChunkPath);
        }

        var graph = new StringBuilder();
        var customParts = parts.Where(part => part.CustomSegment is not null).ToList();
        var customLabels = new Dictionary<HybridSegmentPart, string>();
        if (customParts.Count > 1)
        {
            graph.Append("[0:v]split=").Append(customParts.Count);
            for (var index = 0; index < customParts.Count; index++)
            {
                var label = $"customsrc{index}";
                customLabels[customParts[index]] = label;
                graph.Append('[').Append(label).Append(']');
            }
            graph.Append(';');
        }
        else if (customParts.Count == 1)
        {
            customLabels[customParts[0]] = "0:v";
        }

        var customFrames = 0L;
        for (var index = 0; index < parts.Count; index++)
        {
            var part = parts[index];
            var outputLabel = $"segment{index}";
            if (part.CustomSegment is not null)
            {
                var segment = part.CustomSegment;
                var sourceLabel = customLabels[part];
                graph.Append('[').Append(sourceLabel).Append(']');
                if (secondsMode)
                {
                    graph.Append("trim=start=")
                        .Append(segment.StartSeconds.ToString("0.########", CultureInfo.InvariantCulture))
                        .Append(":end=")
                        .Append(segment.EndSeconds.ToString("0.########", CultureInfo.InvariantCulture));
                }
                else
                {
                    graph.Append("trim=start_frame=")
                        .Append(segment.Start - 1)
                        .Append(":end_frame=")
                        .Append(segment.End);
                }
                graph.Append(",setpts=PTS-STARTPTS,")
                    .Append(BuildDirectCustomFilter(segment))
                    .Append(",setsar=1,format=rgb24[")
                    .Append(outputLabel)
                    .Append("];");
                customFrames += segment.End - segment.Start + 1;
            }
            else
            {
                var inputIndex = modelInputIndex[part.ModelChunkPath];
                graph.Append('[').Append(inputIndex).Append(":v]")
                    .Append("setpts=PTS-STARTPTS,setsar=1,format=rgb24[")
                    .Append(outputLabel)
                    .Append("];");
            }
        }
        for (var index = 0; index < parts.Count; index++)
            graph.Append("[segment").Append(index).Append(']');
        graph.Append("concat=n=").Append(parts.Count).Append(":v=1:a=0[vout]");

        args.Add("-filter_complex");
        args.Add(graph.ToString());
        args.AddRange(new[] { "-map", "[vout]", "-map", "0:a?", "-map", "0:s?" });
        args.AddRange(RebaseMetadataArguments(encoderArguments, 0));
        args.Add(outputFile);
        var exit = RunDirectFfmpeg(
            "FFmpeg 直连自定义段 + 模型段拼接 + 最终编码",
            args, stopWatcher, out var elapsed, pauseShm);
        if (exit != 0) return exit;
        if (customFrames > 0)
        {
            var fps = customFrames / Math.Max(0.000001, elapsed.TotalSeconds);
            Console.WriteLine(
                $"SEGMENTED_DIRECT_GRAPH|{customParts.Count}|{customFrames}|"
                + $"{elapsed.TotalSeconds:0.000}|{fps:0.00}");
        }
        var result = ProbeGeneratedVideoFast(outputFile);
        return result is not null
               && result.Width == outputWidth
               && result.Height == outputHeight
               && result.Frames == sourceVideo.Frames
            ? 0
            : Fail(
                $"分段最终输出校验失败：{result?.Width ?? 0}x{result?.Height ?? 0}, "
                + $"{result?.Frames ?? 0} 帧；预期 {outputWidth}x{outputHeight}, {sourceVideo.Frames} 帧",
                1);
    }

    private static int RunHybridSegmentedVideo(
        string input, string outputFile, IReadOnlyList<PreparedSegment> prepared,
        VideoProbeInfo video, bool secondsMode, IReadOnlyList<string> encoderArguments,
        bool overwrite, string pauseShm, StopWatcher? stopWatcher, int tileSize,
        string segmentedPrecision)
    {
        if (prepared.Count == 1 && !IsSegmentModelBackend(prepared[0].Backend))
        {
            Console.WriteLine("[分段超分] 单一 FFmpeg/Anime4K 段直接进入最终 FFmpeg 编码，不经过 Python。");
            return RunDirectCustomSegment(
                input, outputFile, prepared[0], video, secondsMode, true,
                encoderArguments, overwrite, stopWatcher, pauseShm);
        }

        var outputDirectory = Path.GetDirectoryName(outputFile);
        if (string.IsNullOrWhiteSpace(outputDirectory)) outputDirectory = Environment.CurrentDirectory;
        Directory.CreateDirectory(outputDirectory);
        var workDirectory = Path.Combine(
            outputDirectory,
            "." + Path.GetFileNameWithoutExtension(outputFile)
            + ".videoenhancer-direct-segments-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDirectory);
        var parts = new List<HybridSegmentPart>();
        try
        {
            var index = 0;
            while (index < prepared.Count)
            {
                if (stopWatcher?.IsStopRequested() == true) return 130;
                var current = prepared[index];
                if (!IsSegmentModelBackend(current.Backend))
                {
                    parts.Add(new HybridSegmentPart
                    {
                        CustomSegment = current,
                        Start = current.Start,
                        End = current.End,
                    });
                    index++;
                    continue;
                }

                var end = index;
                while (end + 1 < prepared.Count && IsSegmentModelBackend(prepared[end + 1].Backend))
                    end++;
                var group = prepared.Skip(index).Take(end - index + 1).ToList();
                var sourceClip = Path.Combine(workDirectory, $"model-{parts.Count + 1:D3}-source.mkv");
                var processedClip = Path.Combine(workDirectory, $"chunk-{parts.Count + 1:D3}-model.mkv");
                var extractExit = ExtractModelSegmentGroup(
                    input, sourceClip, group[0], group[^1], video, secondsMode, stopWatcher, pauseShm);
                if (extractExit != 0) return extractExit;
                var processExit = RunModelSegmentGroup(
                    sourceClip, processedClip, group, group[0].Start,
                    pauseShm, stopWatcher, tileSize, segmentedPrecision);
                if (processExit != 0) return processExit;
                parts.Add(new HybridSegmentPart
                {
                    ModelChunkPath = processedClip,
                    Start = group[0].Start,
                    End = group[^1].End,
                });
                index = end + 1;
            }
            return FinalizeHybridSegments(
                parts, input, outputFile, encoderArguments, overwrite, stopWatcher,
                video, secondsMode, prepared[0].OutputWidth, prepared[0].OutputHeight, pauseShm);
        }
        finally
        {
            try
            {
                if (Directory.Exists(workDirectory)) Directory.Delete(workDirectory, recursive: true);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[警告] 分段直连临时目录清理失败：" + ex.Message);
            }
        }
    }

    private static int RunSegmentedVideo(
        CliOptions options, string input, string outputFile, string customEncoder, bool overwrite,
        string pauseShm, StopWatcher? stopWatcher, int tileSize)
    {
        var requested = new List<SegmentRequest>();
        try
        {
            var bytes = Convert.FromBase64String(options.SegmentsBase64);
            using var document = JsonDocument.Parse(bytes);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                return Fail("分段配置根节点必须是数组");
            foreach (var item in document.RootElement.EnumerateArray())
            {
                requested.Add(new SegmentRequest
                {
                    Start = item.TryGetProperty("Start", out var start) && start.TryGetInt64(out var startValue) ? startValue : 0,
                    End = item.TryGetProperty("End", out var end) && end.TryGetInt64(out var endValue) ? endValue : 0,
                    StartSeconds = item.TryGetProperty("StartSeconds", out var startSeconds) && startSeconds.TryGetDouble(out var startSecondsValue) ? startSecondsValue : 0,
                    EndSeconds = item.TryGetProperty("EndSeconds", out var endSeconds) && endSeconds.TryGetDouble(out var endSecondsValue) ? endSecondsValue : 0,
                    Backend = item.TryGetProperty("Backend", out var backendValue) ? backendValue.GetString() ?? "" : "",
                    Model = item.TryGetProperty("Model", out var modelValue) ? modelValue.GetString() ?? "" : "",
                    TargetWidth = item.TryGetProperty("TargetWidth", out var targetWidth) && targetWidth.TryGetInt32(out var targetWidthValue) ? targetWidthValue : 0,
                    TargetHeight = item.TryGetProperty("TargetHeight", out var targetHeight) && targetHeight.TryGetInt32(out var targetHeightValue) ? targetHeightValue : 0,
                });
            }
        }
        catch (Exception ex)
        {
            return Fail("分段配置无法解析：" + ex.Message);
        }
        if (requested.Count == 0)
            return Fail("分段配置为空");

        var video = ProbeVideoOutput(input);
        if (video is null)
            return Fail("无法检测视频尺寸、帧数、帧率或时长，不能执行分段超分");

        var secondsMode = requested.Any(segment => segment.EndSeconds > 0.000001);
        var modelBackends = new HashSet<string>(new[] { "ncnn", "cuda", "tensorrt", "onnx" }, StringComparer.OrdinalIgnoreCase);
        var requestedModelBackends = requested
            .Select(segment => segment.Backend.Trim().ToLowerInvariant())
            .Where(modelBackends.Contains)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (requestedModelBackends.Count > 1 && !options.AllowMixedSegmentBackends)
        {
            return Fail(
                "跨 NCNN / CUDA / TensorRT / ONNX 分段混用是测试功能；"
                + "请在插件中手动开启“跨模型后端混用”，或显式传入 --allow-mixed-segment-backends");
        }
        var ffmpegScalers = new HashSet<string>(new[]
        {
            "fast_bilinear", "bilinear", "bicubic", "neighbor",
            "area", "bicublin", "lanczos", "spline",
        }, StringComparer.OrdinalIgnoreCase);
        var checkedBackends = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        string NormaliseFfmpegScaler(string value)
        {
            var name = value.Trim().ToLowerInvariant().Replace(' ', '_');
            if (name is "nearest" or "nearest_neighbour" or "nearest_neighbor" or "nearest-neighbour" or "nearest-neighbor")
                name = "neighbor";
            return ffmpegScalers.Contains(name) ? name : "";
        }

        var expectedStart = 1L;
        var expectedStartSeconds = 0.0;
        var prepared = new List<PreparedSegment>();
        var fixedScale = 0;
        var segmentedPrecision = options.UpscalePrecision;

        for (var index = 0; index < requested.Count; index++)
        {
            var segment = requested[index];
            long rangeStart;
            long rangeEnd;
            if (secondsMode)
            {
                if (Math.Abs(segment.StartSeconds - expectedStartSeconds) > 0.002
                    || segment.EndSeconds <= segment.StartSeconds)
                {
                    return Fail($"第 {index + 1} 段秒级边界不连续：{segment.StartSeconds:0.###}-{segment.EndSeconds:0.###} 秒");
                }
                rangeStart = expectedStart;
                if (index == requested.Count - 1)
                {
                    rangeEnd = video.Frames;
                }
                else
                {
                    rangeEnd = (long)Math.Round(
                        segment.EndSeconds * video.FrameRate,
                        MidpointRounding.AwayFromZero);
                    rangeEnd = Math.Min(rangeEnd, video.Frames - 1);
                    if (rangeEnd < rangeStart)
                        return Fail($"第 {index + 1} 段太短，按当前帧率无法形成独立帧范围");
                }
                expectedStartSeconds = segment.EndSeconds;
            }
            else
            {
                rangeStart = segment.Start;
                rangeEnd = segment.End;
                if (rangeStart != expectedStart || rangeEnd < rangeStart)
                    return Fail($"第 {index + 1} 段必须从第 {expectedStart} 帧开始，当前为 {rangeStart}-{rangeEnd}");
            }

            var backend = segment.Backend.Trim().ToLowerInvariant();
            var model = segment.Model.Trim();
            var modelScale = 0;
            var inputMultiple = 1;

            if (modelBackends.Contains(backend))
            {
                if (checkedBackends.Add(backend) && !RunCheck(verbose: false, backend: backend))
                    return 1;
                model = ResolveModelWithTensorRtOutputScalePreset(model, backend, out var tensorRtSegmentPresetScale);
                if (model.Length == 0) return 1;
                var scaleText = tensorRtSegmentPresetScale > 0
                    ? tensorRtSegmentPresetScale.ToString(CultureInfo.InvariantCulture)
                    : DetectScale(model);
                if (!int.TryParse(scaleText, NumberStyles.Integer, CultureInfo.InvariantCulture, out modelScale) || modelScale < 1)
                    return Fail($"第 {index + 1} 段无法识别模型倍率：{segment.Model}");
                if (fixedScale == 0)
                    fixedScale = modelScale;
                else if (fixedScale != modelScale)
                    return Fail($"第 {index + 1} 段模型倍率为 {modelScale}x；分段中已有固定倍率 {fixedScale}x。固定倍率模型必须一致");

                if (ModelCapabilityCatalog.TryGet(model, ModelsDir, out var capability))
                    inputMultiple = Math.Max(1, capability.InputMultiple);
                if (backend == "tensorrt")
                {
                    var engineWidth = (video.Width + inputMultiple - 1) / inputMultiple * inputMultiple;
                    var engineHeight = (video.Height + inputMultiple - 1) / inputMultiple * inputMultiple;
                    model = EnsureTensorRtEngine(model, engineWidth, engineHeight, stopWatcher, tileSize, modelScale,
                        options.UpscalePrecision);
                    if (model.Length == 0) return stopWatcher?.IsStopRequested() == true ? 130 : 1;
                }
                if (ResolveUpscalePrecision(model, backend, options.UpscalePrecision) == "float32")
                    segmentedPrecision = "float32";
            }
            else if (backend == "ffmpeg")
            {
                model = NormaliseFfmpegScaler(model);
                if (model.Length == 0)
                    return Fail($"第 {index + 1} 段使用了不支持的 FFmpeg 缩放算法：{segment.Model}");
            }
            else if (backend == "anime4k")
            {
                if (model.Length == 0)
                    return Fail($"第 {index + 1} 段尚未选择 Anime4K 着色器");
            }
            else
            {
                return Fail($"第 {index + 1} 段使用了不支持的处理后端：{segment.Backend}");
            }

            prepared.Add(new PreparedSegment
            {
                Start = rangeStart,
                End = rangeEnd,
                StartSeconds = secondsMode
                    ? segment.StartSeconds
                    : (rangeStart - 1) / video.FrameRate,
                EndSeconds = secondsMode
                    ? segment.EndSeconds
                    : (index == requested.Count - 1 ? video.DurationSeconds : rangeEnd / video.FrameRate),
                Backend = backend,
                Model = model,
                Scale = modelScale,
                InputMultiple = inputMultiple,
                OutputWidth = segment.TargetWidth,
                OutputHeight = segment.TargetHeight,
            });
            expectedStart = rangeEnd + 1;
        }

        if (secondsMode)
        {
            var durationTolerance = Math.Max(0.1, 2.0 / video.FrameRate);
            if (Math.Abs(expectedStartSeconds - video.DurationSeconds) > durationTolerance)
            {
                return Fail(
                    $"秒级分段必须覆盖完整时长 {video.DurationSeconds:0.###} 秒；"
                    + $"当前结束于 {expectedStartSeconds:0.###} 秒");
            }
        }
        if (prepared[0].Start != 1 || prepared[^1].End != video.Frames || expectedStart != video.Frames + 1)
            return Fail($"分段必须连续覆盖到第 {video.Frames} 帧；当前最后一帧为 {prepared[^1].End}");

        int outputWidth;
        int outputHeight;
        if (fixedScale > 0)
        {
            outputWidth = checked(video.Width * fixedScale);
            outputHeight = checked(video.Height * fixedScale);
        }
        else
        {
            var firstWidth = prepared[0].OutputWidth;
            var firstHeight = prepared[0].OutputHeight;
            if (firstWidth <= 0 || firstHeight <= 0)
                return Fail("仅使用 FFmpeg / Anime4K 时必须设置自定义目标宽度和高度");
            if (prepared.Any(segment => segment.OutputWidth != firstWidth || segment.OutputHeight != firstHeight))
                return Fail("仅使用 FFmpeg / Anime4K 时，所有分段必须使用相同的目标分辨率");
            outputWidth = firstWidth;
            outputHeight = firstHeight;
        }
        foreach (var segment in prepared)
        {
            segment.OutputWidth = outputWidth;
            segment.OutputHeight = outputHeight;
        }

        List<string> encoderArguments;
        try
        {
            encoderArguments = GetFfmpegEncoderArguments(options.FfmpegSettings);
        }
        catch (ArgumentException ex)
        {
            return Fail(ex.Message);
        }
        var modeText = secondsMode ? "按秒（关键帧断点）" : "精确帧";
        var sizeRule = fixedScale > 0
            ? $"固定倍率模型优先：{fixedScale}x -> {outputWidth}x{outputHeight}"
            : $"自定义统一输出：{outputWidth}x{outputHeight}";
        Console.WriteLine($"[分段超分] {modeText}；{prepared.Count} 段连续覆盖全片；{sizeRule}。");

        if (prepared.Any(segment => !IsSegmentModelBackend(segment.Backend)))
        {
            Console.WriteLine(
                "[分段超分] FFmpeg / Anime4K 段正在直接处理源视频，模型段使用对应的推理后端。");
            return RunHybridSegmentedVideo(
                input, outputFile, prepared, video, secondsMode, encoderArguments,
                overwrite, pauseShm, stopWatcher, tileSize, segmentedPrecision);
        }

        var segmentPayload = EncodePreparedSegments(prepared);
        var encoderPayload = EncodeStringList(encoderArguments);
        var script = EnsureEmbeddedFile(EmbeddedSegmentedBackendResource, "rve-segmented-backend.py");
        var arguments = new List<string>
        {
            script,
            "--input", input,
            "--output", outputFile,
            "--segments-base64", segmentPayload,
            "--encoder-args-base64", encoderPayload,
            "--ffmpeg-path", FfmpegExe,
        };
        if (ScriptSupportsArgument(script, "--tile-size"))
        {
            arguments.Add("--tile-size");
            arguments.Add(tileSize.ToString(CultureInfo.InvariantCulture));
        }
        if (!string.IsNullOrWhiteSpace(pauseShm))
        {
            arguments.Add("--pause-shm");
            arguments.Add(pauseShm);
        }
        if (overwrite) arguments.Add("--overwrite");

        return LaunchBackend(arguments, input, "", outputFile, customEncoder, stopWatcher,
            null, null, "mixed", pauseShm, "分段超分", isFinalStage: true,
            upscalePrecision: segmentedPrecision);
    }

    /// <summary>
    /// 运行视频增强管线。同后端组合在单进程内按帧处理；后端格式不兼容时才使用 FFV1 无损中间视频。
    /// 这样既不把 NCNN RIFE 模型错误地交给 TensorRT/ONNX，也不让同后端任务产生整段临时视频。
    /// </summary>
    private static int RunVideoPipeline(
        string input, string outputFile, string model, string customEncoder, bool overwrite, string? scale,
        string pauseShm, StopWatcher? stopWatcher, string? interpModel, string? interpFactor,
        string upscaleBackend, string interpBackend, string processOrder, bool hdrMode,
        bool dynamicOpticalFlow, double sceneThreshold, int tileSize,
        string requestedUpscalePrecision, string requestedInterpPrecision)
    {
        var useUpscale = !string.IsNullOrEmpty(model);
        var useInterp = interpModel is not null;
        var upscalePrecision = ResolveUpscalePrecision(model, upscaleBackend, requestedUpscalePrecision);
        var interpPrecision = ResolveInterpPrecision(interpModel, interpBackend, requestedInterpPrecision);
        if (!useUpscale || !useInterp)
        {
            var activeBackend = useUpscale ? upscaleBackend : interpBackend;
            var activePrecision = useUpscale ? upscalePrecision : interpPrecision;
            var args = BuildBackendArgs(input, outputFile, model, customEncoder, overwrite, scale,
                pauseShm, interpModel, interpFactor, activeBackend, hdrMode: hdrMode,
                dynamicOpticalFlow: dynamicOpticalFlow, sceneThreshold: sceneThreshold, tileSize: tileSize,
                precision: activePrecision);
            return LaunchBackend(args, input, model, outputFile, customEncoder, stopWatcher,
                interpModel, interpFactor, activeBackend, pauseShm, "单阶段处理", isFinalStage: true,
                upscalePrecision: useUpscale ? upscalePrecision : null,
                interpPrecision: useInterp ? interpPrecision : null);
        }

        var upscaleFirst = processOrder == "upscale-first";
        Console.WriteLine(upscaleFirst
            ? "[处理顺序] 画质优先：先超分，再补帧。"
            : "[处理顺序] 速度/算力优先：先补帧，再超分。");

        // 同后端组合统一使用内置包装器：保持单进程帧级传递，同时允许两个模型使用不同精度。
        if (upscaleBackend == interpBackend)
        {
            var orderedBackend = EnsureEmbeddedFile(
                EmbeddedOrderedBackendResource, "rve-ordered-backend.py");
            var args = BuildBackendArgs(input, outputFile, model, customEncoder, overwrite, scale,
                pauseShm, interpModel, interpFactor, upscaleBackend, orderedBackend, hdrMode,
                dynamicOpticalFlow, sceneThreshold, tileSize,
                upscaleFirst ? upscalePrecision : interpPrecision);
            return LaunchBackend(args, input, model, outputFile, customEncoder, stopWatcher,
                interpModel, interpFactor, upscaleBackend, pauseShm,
                upscaleFirst ? "先超分，再补帧" : "先补帧，再超分", isFinalStage: true,
                upscalePrecision: upscalePrecision, interpPrecision: interpPrecision,
                processOrder: processOrder);
        }

        var intermediate = PortablePaths.CreateWorkFilePath("pipeline", ".mkv");
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
                    dynamicOpticalFlow: dynamicOpticalFlow, sceneThreshold: sceneThreshold, tileSize: tileSize,
                    precision: upscalePrecision);
            }
            else
            {
                firstModel = "";
                firstInterp = interpModel;
                firstBackend = interpBackend;
                firstTitle = "阶段 1/2：补帧";
                firstArgs = BuildBackendArgs(input, intermediate, "", losslessEncoder, true,
                    null, pauseShm, interpModel, interpFactor, interpBackend, hdrMode: hdrMode,
                    dynamicOpticalFlow: dynamicOpticalFlow, sceneThreshold: sceneThreshold,
                    precision: interpPrecision);
            }
            var firstExit = LaunchBackend(firstArgs, input, firstModel, intermediate, losslessEncoder,
                stopWatcher, firstInterp, firstInterp is null ? null : interpFactor, firstBackend,
                pauseShm, firstTitle, isFinalStage: false,
                upscalePrecision: firstInterp is null ? upscalePrecision : null,
                interpPrecision: firstInterp is not null ? interpPrecision : null);
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
                    dynamicOpticalFlow: dynamicOpticalFlow, sceneThreshold: sceneThreshold,
                    precision: interpPrecision);
            }
            else
            {
                secondModel = model;
                secondInterp = null;
                secondBackend = upscaleBackend;
                secondTitle = "阶段 2/2：超分";
                secondArgs = BuildBackendArgs(intermediate, outputFile, model, customEncoder, overwrite,
                    scale, pauseShm, null, null, upscaleBackend, hdrMode: hdrMode,
                    dynamicOpticalFlow: dynamicOpticalFlow, sceneThreshold: sceneThreshold, tileSize: tileSize,
                    precision: upscalePrecision);
            }
            return LaunchBackend(secondArgs, intermediate, secondModel, outputFile, customEncoder,
                stopWatcher, secondInterp, secondInterp is null ? null : interpFactor, secondBackend,
                pauseShm, secondTitle, isFinalStage: true,
                upscalePrecision: secondInterp is null ? upscalePrecision : null,
                interpPrecision: secondInterp is not null ? interpPrecision : null);
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
            PortablePaths.ConfigureChildProcess(psi);
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
            var m = Regex.Match(err, @"Video:.*?\b(\d{1,5})x(\d{1,5})\b", RegexOptions.IgnoreCase);
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

    /// <summary>读取主视频流的精确平均帧率，供 RTX 原始帧管道建立时间基。</summary>
    private static string GetInputFrameRate(string input)
    {
        try
        {
            var result = RunProcessCapture(FfprobeExe, new[]
            {
                "-v", "error", "-select_streams", "v:0", "-show_entries",
                "stream=avg_frame_rate", "-of", "default=noprint_wrappers=1:nokey=1", input,
            }, 30);
            var value = result.Output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim()).FirstOrDefault() ?? "";
            var match = Regex.Match(value, @"^(\d+)/(\d+)$");
            if (match.Success && match.Groups[1].Value != "0" && match.Groups[2].Value != "0")
                return value;
        }
        catch
        {
        }
        Console.Error.WriteLine("[警告] 无法读取源帧率，RTX 帧管道回退为 30/1");
        return "30/1";
    }

    private sealed record VideoProbeInfo(
        int Width, int Height, long Frames, double FrameRate, double DurationSeconds);

    private static double ParseFfprobeRate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return 0;
        var parts = value.Split('/', 2);
        if (parts.Length == 2
            && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var numerator)
            && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var denominator)
            && denominator != 0)
            return numerator / denominator;
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var direct)
            ? direct
            : 0;
    }

    /// <summary>用 ffprobe 严格读取视频尺寸和可解码帧数；任何字段缺失都视为探测失败。</summary>
    private static VideoProbeInfo? ProbeVideoOutput(string path)
    {
        if (!File.Exists(FfprobeExe) || !File.Exists(path)) return null;
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = FfprobeExe,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };
            PortablePaths.ConfigureChildProcess(psi);
            foreach (var argument in new[]
                     {
                         "-v", "error", "-select_streams", "v:0", "-count_frames",
                         "-show_entries", "stream=width,height,nb_read_frames,nb_frames,avg_frame_rate,r_frame_rate,duration:format=duration",
                         "-of", "json", path,
                     })
            {
                psi.ArgumentList.Add(argument);
            }
            using var process = Process.Start(psi);
            if (process is null) return null;
            var output = process.StandardOutput.ReadToEnd();
            _ = process.StandardError.ReadToEnd();
            if (!process.WaitForExit(120000) || process.ExitCode != 0) return null;

            using var document = JsonDocument.Parse(output);
            var stream = document.RootElement.GetProperty("streams").EnumerateArray().FirstOrDefault();
            if (stream.ValueKind != JsonValueKind.Object) return null;
            var width = stream.GetProperty("width").GetInt32();
            var height = stream.GetProperty("height").GetInt32();
            long frames = 0;
            foreach (var propertyName in new[] { "nb_read_frames", "nb_frames" })
            {
                if (!stream.TryGetProperty(propertyName, out var value)) continue;
                if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out frames)) break;
                if (value.ValueKind == JsonValueKind.String
                    && long.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out frames)) break;
            }
            var rateText = stream.TryGetProperty("avg_frame_rate", out var avgRate)
                ? avgRate.GetString()
                : null;
            if (ParseFfprobeRate(rateText) <= 0 && stream.TryGetProperty("r_frame_rate", out var realRate))
                rateText = realRate.GetString();
            var frameRate = ParseFfprobeRate(rateText);
            double duration = 0;
            if (stream.TryGetProperty("duration", out var streamDuration))
            {
                var durationText = streamDuration.ValueKind == JsonValueKind.String
                    ? streamDuration.GetString()
                    : streamDuration.GetRawText();
                double.TryParse(durationText, NumberStyles.Float, CultureInfo.InvariantCulture, out duration);
            }
            if (duration <= 0 && document.RootElement.TryGetProperty("format", out var format)
                && format.TryGetProperty("duration", out var formatDuration))
            {
                var durationText = formatDuration.ValueKind == JsonValueKind.String
                    ? formatDuration.GetString()
                    : formatDuration.GetRawText();
                double.TryParse(durationText, NumberStyles.Float, CultureInfo.InvariantCulture, out duration);
            }
            if (duration <= 0 && frameRate > 0 && frames > 0)
                duration = frames / frameRate;
            return width > 0 && height > 0 && frames > 0 && frameRate > 0 && duration > 0
                ? new VideoProbeInfo(width, height, frames, frameRate, duration)
                : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 对程序自己生成的中间/最终视频做轻量校验。使用 packet 数避免 -count_frames
    /// 把高分辨率成品完整解码第二遍；packet 计数不可用时才回退到严格帧探测。
    /// </summary>
    private static VideoProbeInfo? ProbeGeneratedVideoFast(string path)
    {
        if (!File.Exists(FfprobeExe) || !File.Exists(path)) return null;
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = FfprobeExe,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };
            foreach (var argument in new[]
                     {
                         "-v", "error", "-select_streams", "v:0", "-count_packets",
                         "-show_entries", "stream=width,height,nb_read_packets,nb_frames,avg_frame_rate,r_frame_rate,duration:format=duration",
                         "-of", "json", path,
                     })
            {
                psi.ArgumentList.Add(argument);
            }
            using var process = Process.Start(psi);
            if (process is null) return ProbeVideoOutput(path);
            var output = process.StandardOutput.ReadToEnd();
            _ = process.StandardError.ReadToEnd();
            if (!process.WaitForExit(30000) || process.ExitCode != 0)
                return ProbeVideoOutput(path);

            using var document = JsonDocument.Parse(output);
            var stream = document.RootElement.GetProperty("streams").EnumerateArray().FirstOrDefault();
            if (stream.ValueKind != JsonValueKind.Object) return ProbeVideoOutput(path);
            var width = stream.GetProperty("width").GetInt32();
            var height = stream.GetProperty("height").GetInt32();
            long frames = 0;
            foreach (var propertyName in new[] { "nb_read_packets", "nb_frames" })
            {
                if (!stream.TryGetProperty(propertyName, out var value)) continue;
                if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out frames)) break;
                if (value.ValueKind == JsonValueKind.String
                    && long.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out frames)) break;
            }
            if (frames <= 0) return ProbeVideoOutput(path);

            var rateText = stream.TryGetProperty("avg_frame_rate", out var avgRate)
                ? avgRate.GetString()
                : null;
            if (ParseFfprobeRate(rateText) <= 0 && stream.TryGetProperty("r_frame_rate", out var realRate))
                rateText = realRate.GetString();
            var frameRate = ParseFfprobeRate(rateText);
            double duration = 0;
            if (stream.TryGetProperty("duration", out var streamDuration))
            {
                var durationText = streamDuration.ValueKind == JsonValueKind.String
                    ? streamDuration.GetString()
                    : streamDuration.GetRawText();
                double.TryParse(durationText, NumberStyles.Float, CultureInfo.InvariantCulture, out duration);
            }
            if (duration <= 0 && document.RootElement.TryGetProperty("format", out var format)
                && format.TryGetProperty("duration", out var formatDuration))
            {
                var durationText = formatDuration.ValueKind == JsonValueKind.String
                    ? formatDuration.GetString()
                    : formatDuration.GetRawText();
                double.TryParse(durationText, NumberStyles.Float, CultureInfo.InvariantCulture, out duration);
            }
            if (duration <= 0 && frameRate > 0)
                duration = frames / frameRate;
            return width > 0 && height > 0 && frameRate > 0 && duration > 0
                ? new VideoProbeInfo(width, height, frames, frameRate, duration)
                : ProbeVideoOutput(path);
        }
        catch
        {
            return ProbeVideoOutput(path);
        }
    }

    /// <summary>
    /// NCNN 在少数 Vulkan 驱动上会于成品封装完成后的进程清理阶段触发访问冲突。
    /// 仅当输入、输出的尺寸与帧数完全符合本阶段预期时，才把该特定退出码归一为成功。
    /// </summary>
    private static bool ValidateCompletedNcnnOutput(
        string input, string output, string model, string? interpFactor, out string detail)
    {
        detail = "";
        var source = ProbeVideoOutput(input);
        var result = ProbeVideoOutput(output);
        if (source is null || result is null) return false;

        var scale = 1;
        if (!string.IsNullOrWhiteSpace(model))
        {
            var detectedScale = DetectScale(model);
            if (!int.TryParse(detectedScale, NumberStyles.Integer, CultureInfo.InvariantCulture, out scale)
                || scale <= 0)
            {
                return false;
            }
        }

        var factor = 1;
        if (!string.IsNullOrWhiteSpace(interpFactor)
            && (!int.TryParse(interpFactor, NumberStyles.Integer, CultureInfo.InvariantCulture, out factor)
                || factor <= 1))
        {
            return false;
        }

        var expectedWidth = (long)source.Width * scale;
        var expectedHeight = (long)source.Height * scale;
        var expectedFrames = factor == 1
            ? source.Frames
            : source.Frames * factor - (factor - 1);
        detail = $"{result.Width}x{result.Height}, {result.Frames} 帧（预期 {expectedWidth}x{expectedHeight}, {expectedFrames} 帧）";
        return result.Width == expectedWidth
               && result.Height == expectedHeight
               && result.Frames == expectedFrames;
    }

    private static int LaunchBackend(
        List<string> backendArgs, string input, string model, string outputFile, string customEncoder, StopWatcher? stopWatcher,
        string? interpModel, string? interpFactor, string backend, string pauseShm, string stageTitle, bool isFinalStage,
        string? upscalePrecision = null, string? interpPrecision = null, string? processOrder = null)
    {
        Console.WriteLine();
        Console.WriteLine("[阶段] " + stageTitle);
        Console.WriteLine("[信息] 输入视频 : " + input);
        Console.WriteLine("[信息] 推理后端 : " + (backend == "mixed" ? "分段混合处理" : backend == "basicvsrpp" ? "BasicVSR++（时序视频）" : backend == "flashvsr" ? "FlashVSR（时序视频）" : backend == "cuda" ? "CUDA（PyTorch）" : backend == "tensorrt" ? "TensorRT（NVIDIA）" : backend == "onnx" ? "ONNX Runtime" : "NCNN（Vulkan）"));
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
        if (upscalePrecision is not null)
            Console.WriteLine("[信息] 超分精度 : " + PrecisionDisplayName(upscalePrecision));
        if (interpPrecision is not null)
            Console.WriteLine("[信息] 补帧精度 : " + PrecisionDisplayName(interpPrecision));
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
        PortablePaths.ConfigureChildProcess(psi);
        psi.Environment["PYTHONUTF8"] = "1";
        psi.Environment["PYTHONIOENCODING"] = "utf-8";
        // 先超后补的同后端包装器需要导入核心后端目录中的 src 包。
        psi.Environment["VIDEOENHANCER_BACKEND_DIR"] = Path.GetDirectoryName(BackendScript)!;
        if (upscalePrecision is not null)
            psi.Environment["VIDEOENHANCER_UPSCALE_PRECISION"] = upscalePrecision;
        if (interpPrecision is not null)
            psi.Environment["VIDEOENHANCER_INTERP_PRECISION"] = interpPrecision;
        if (processOrder is not null)
            psi.Environment["VIDEOENHANCER_PROCESS_ORDER"] = processOrder;
        if (ModelCapabilityCatalog.TryGet(model, ModelsDir, out var capability))
        {
            psi.Environment["VIDEOENHANCER_UPSCALE_INPUT_MULTIPLE"] =
                capability.InputMultiple.ToString(CultureInfo.InvariantCulture);
            if (backend == "onnx")
            {
                psi.Environment["VIDEOENHANCER_ONNX_INPUT_MULTIPLE"] =
                    capability.InputMultiple.ToString(CultureInfo.InvariantCulture);
            }
        }
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
        long fatalBackendDetectedAt = 0;

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
                Interlocked.CompareExchange(ref fatalBackendDetectedAt, Stopwatch.GetTimestamp(), 0);
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
            var fatalDetectedAt = Interlocked.Read(ref fatalBackendDetectedAt);
            if (fatalDetectedAt > 0
                && Stopwatch.GetTimestamp() - fatalDetectedAt >= Stopwatch.Frequency * 2)
            {
                Console.Error.WriteLine("[失败] 检测到后端渲染线程异常，正在终止残留进程…");
                try { process.Kill(entireProcessTree: true); } catch { }
                break;
            }
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

        const int windowsAccessViolation = unchecked((int)0xC0000005);
        if (backend == "ncnn"
            && exitCode == windowsAccessViolation
            && !hasFatalBackendError
            && ValidateCompletedNcnnOutput(input, outputFile, model, interpFactor, out var validationDetail))
        {
            Console.Error.WriteLine("[警告] NCNN 在 Vulkan 清理阶段异常退出，但后端未报告渲染错误。");
            Console.WriteLine("[校验] 输出视频完整：" + validationDetail);
            Console.WriteLine(isFinalStage
                ? "[完成] 视频增强处理成功结束。"
                : "[阶段完成] " + stageTitle + " 已完成。");
            Console.WriteLine("[信息] 输出文件 : " + outputFile + " " + sizeText);
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
    private static string EnsureTensorRtEngine(
        string modelPath, int inputWidth, int inputHeight, StopWatcher? stopWatcher, int tileSize = 0, int outputScale = 0,
        string requestedPrecision = "auto")
    {
        var sourcePath = modelPath;
        if (IsTensorRTEngineFile(modelPath))
        {
            // 预置或用户选择的 Engine 不再作为任务入口：始终回到同名 PTH，
            // 按当前设备/尺寸/配置生成 models\TensorRT-Cache 下的专用 Engine。
            var baseName = Path.GetFileNameWithoutExtension(modelPath);
            var cacheMarker = baseName.IndexOf("__gpu-", StringComparison.OrdinalIgnoreCase);
            if (cacheMarker > 0) baseName = baseName[..cacheMarker];
            baseName = Regex.Replace(baseName, @"-x[1-8](-tensorrt)?$", "", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            sourcePath = DiscoverUpscalePthModels().FirstOrDefault(path =>
                ModelBaseName(path).Equals(baseName, StringComparison.OrdinalIgnoreCase)) ?? "";
            if (sourcePath.Length == 0)
            {
                Console.Error.WriteLine("[错误] TensorRT 任务需要 PTH 源模型；预置 Engine 不再直接使用，且未找到对应 PTH：" + baseName);
                Console.Error.WriteLine("[处理建议] 下载对应 PTH 模型后重试，程序会按当前设备自动编译并缓存。");
                return "";
            }
            Console.WriteLine("[TensorRT] 已忽略预置 Engine，将使用对应 PTH 构建本机 Engine：" + sourcePath);
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
        var enginePrecision = TensorRtPrecisionForModel(sourcePath, requestedPrecision);
        var cachePath = BuildTensorRtCachePath(sourcePath, runtime!, inputWidth, inputHeight, tileSize, outputScale,
            enginePrecision);
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
                if (ValidateTensorRTEngine(cachePath, printSuccess: true, inputWidth: inputWidth, inputHeight: inputHeight, tileSize: tileSize))
                {
                    EmitTensorRtProgress("超分 Engine", 100, "复用已验证缓存");
                    return cachePath;
                }
                Console.Error.WriteLine("[TensorRT] 缓存已失效，将自动重新构建。");
                try { File.Delete(cachePath); } catch { }
            }

            Console.WriteLine("[TensorRT] 未命中可用缓存，开始自动构建 Engine；首次使用可能需要数分钟。");
            Console.WriteLine("[TensorRT] GPU=" + runtime!.GpuName + "，TensorRT=" + runtime.TensorRtVersion +
                "，Torch-TensorRT=" + runtime.TorchTensorRtVersion +
                "，输入=" + inputWidth + "x" + inputHeight + "，输出倍率=" +
                (outputScale > 0 ? outputScale.ToString(CultureInfo.InvariantCulture) : "native") +
                "，分块=" + tileSize);
            EmitTensorRtProgress("超分 Engine", 0, "准备构建");
            var builtPath = RunTensorRtConverter(
                sourcePath, inputWidth, inputHeight, outputScale, tileSize, stopWatcher, enginePrecision);
            if (builtPath.Length == 0) return "";

            var partialPath = Path.Combine(TensorRTCacheDir,
                Path.GetFileNameWithoutExtension(cachePath) + ".building-" + Guid.NewGuid().ToString("N") + ".engine");
            try
            {
                File.Copy(builtPath, partialPath, overwrite: true);
            if (!ValidateTensorRTEngine(partialPath, printSuccess: false, inputWidth: inputWidth, inputHeight: inputHeight, tileSize: tileSize))
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
            EmitTensorRtProgress("超分 Engine", 100, "构建完成");
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
            "import torch_tensorrt; " +
            "print('TRT_ENV|' + torch.cuda.get_device_name(0).replace('|','/') + '|' + str(trt.__version__) + '|' + str(torch_tensorrt.__version__))";
        var result = RunProcessCapture(PythonExe, new[] { "-c", script }, 60);
        var line = result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .LastOrDefault(value => value.StartsWith("TRT_ENV|", StringComparison.Ordinal));
        if (!result.Ok || line is null)
        {
            error = string.IsNullOrWhiteSpace(result.Error) ? "便携 Python 未返回 GPU/TensorRT 信息" : result.Error.Trim();
            return false;
        }
        var parts = line.Split('|');
        if (parts.Length < 4 || string.IsNullOrWhiteSpace(parts[1]) || string.IsNullOrWhiteSpace(parts[2]) || string.IsNullOrWhiteSpace(parts[3]))
        {
            error = "GPU/TensorRT 信息格式无效：" + line;
            return false;
        }
        runtime = new TensorRtRuntime(parts[1].Trim(), parts[2].Trim(), parts[3].Trim());
        error = "";
        return true;
    }

    private const string TensorRtCacheSchema = "3";
    private const int TensorRtOptimizationLevel = 3;
    private const int TensorRtTilePad = 10;

    /// <summary>GRL 的相对位置编码在 FP16 转换时混用 Float/Half，必须构建 FP32 Engine。</summary>
    private static string TensorRtPrecisionForModel(string sourcePath, string requestedPrecision = "auto") =>
        requestedPrecision == "float32" || Regex.IsMatch(ModelBaseName(sourcePath), @"GRL", RegexOptions.IgnoreCase)
            ? "fp32"
            : "fp16";

    private static string BuildTensorRtCachePath(
        string sourcePath, TensorRtRuntime runtime, int width, int height, int tileSize, int outputScale,
        string precision)
    {
        using var stream = File.OpenRead(sourcePath);
        var sourceHash = Convert.ToHexString(SHA256.HashData(stream)).Substring(0, 12).ToLowerInvariant();
        var configuration = string.Join("|", new[]
        {
            runtime.GpuName, runtime.TensorRtVersion, runtime.TorchTensorRtVersion,
            precision, TensorRtOptimizationLevel.ToString(CultureInfo.InvariantCulture),
            TensorRtCacheSchema, width.ToString(CultureInfo.InvariantCulture),
            height.ToString(CultureInfo.InvariantCulture), tileSize.ToString(CultureInfo.InvariantCulture),
            TensorRtTilePad.ToString(CultureInfo.InvariantCulture), outputScale.ToString(CultureInfo.InvariantCulture),
            sourceHash,
        });
        var configurationHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(configuration))).Substring(0, 20).ToLowerInvariant();
        // 完整配置进入指纹，不再把 GPU/运行时版本全部展开到文件名，避免 Windows MAX_PATH。
        var fileName = SafeCacheComponent(ModelBaseName(sourcePath), 48) +
            "__input-" + width + "x" + height +
            "__scale-" + outputScale +
            "__tile-" + tileSize +
            "__cfg-" + configurationHash +
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

    /// <summary>输出给 3FUI 队列解析的 TensorRT 构建进度事件。</summary>
    private static void EmitTensorRtProgress(string phase, int percent, string detail)
    {
        var safeDetail = (detail ?? "").Replace('|', '/').Replace('\r', ' ').Replace('\n', ' ');
        Console.WriteLine("VIDEOENHANCER_TRT_PROGRESS|" + phase + "|" +
            Math.Clamp(percent, 0, 100).ToString(CultureInfo.InvariantCulture) + "|" + safeDetail);
    }

    /// <summary>调用开发包自带转换器，并实时转发构建日志与停止请求。</summary>
    private static string RunTensorRtConverter(
        string sourcePath, int inputWidth, int inputHeight, int outputScale, int tileSize, StopWatcher? stopWatcher,
        string precision)
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
        PortablePaths.ConfigureChildProcess(start);
        start.Environment["PYTHONUTF8"] = "1";
        start.Environment["PYTHONIOENCODING"] = "utf-8";
        start.ArgumentList.Add(TensorRTConverterScript);
        start.ArgumentList.Add(sourcePath);
        start.ArgumentList.Add("--output-dir");
        start.ArgumentList.Add(buildDir);
        start.ArgumentList.Add("--width");
        start.ArgumentList.Add(inputWidth.ToString(CultureInfo.InvariantCulture));
        start.ArgumentList.Add("--height");
        start.ArgumentList.Add(inputHeight.ToString(CultureInfo.InvariantCulture));
        if (outputScale > 0)
        {
            start.ArgumentList.Add("--output-scale");
            start.ArgumentList.Add(outputScale.ToString(CultureInfo.InvariantCulture));
        }
        start.ArgumentList.Add("--tile-size");
        start.ArgumentList.Add(tileSize.ToString(CultureInfo.InvariantCulture));
        start.ArgumentList.Add("--tile-pad");
        start.ArgumentList.Add(TensorRtTilePad.ToString(CultureInfo.InvariantCulture));
        start.ArgumentList.Add("--precision");
        start.ArgumentList.Add(precision);
        start.ArgumentList.Add("--optimization-level");
        start.ArgumentList.Add(TensorRtOptimizationLevel.ToString(CultureInfo.InvariantCulture));

        using var process = new Process { StartInfo = start };
        var job = CreateKillOnCloseJob();
        var cancelled = false;
        ConsoleCancelEventHandler cancelHandler = (_, e) => { e.Cancel = true; cancelled = true; };
        process.OutputDataReceived += (_, e) =>
        {
            if (string.IsNullOrWhiteSpace(e.Data)) return;
            // 保留机器可读进度协议的行首，3FUI 才能把转换阶段显示到任务进度。
            if (e.Data.StartsWith("VIDEOENHANCER_TRT_PROGRESS|", StringComparison.Ordinal))
                Console.WriteLine(e.Data);
            else
                Console.WriteLine("[TensorRT 构建] " + e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data)) Console.Error.WriteLine("[TensorRT 构建] " + e.Data);
        };
        Console.CancelKeyPress += cancelHandler;
        try
        {
            if (!process.Start())
            {
                Console.Error.WriteLine("[错误] 无法启动 TensorRT 自动构建进程。");
                EmitTensorRtProgress("超分 Engine", 0, "启动转换器失败");
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
                EmitTensorRtProgress("超分 Engine", 0, "构建已取消");
                return "";
            }
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                Console.Error.WriteLine("[错误] TensorRT 自动构建失败，退出码：" + process.ExitCode);
                EmitTensorRtProgress("超分 Engine", 0, "构建失败");
                return "";
            }
            var engine = Directory.EnumerateFiles(buildDir, "*.engine", SearchOption.AllDirectories)
                .OrderByDescending(path => File.GetLastWriteTimeUtc(path)).FirstOrDefault();
            if (engine is null)
            {
                Console.Error.WriteLine("[错误] 转换器已退出，但没有生成 .engine 文件：" + buildDir);
                EmitTensorRtProgress("超分 Engine", 0, "未生成 Engine");
                return "";
            }
            return engine;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("[错误] TensorRT 自动构建异常：" + ex.Message);
            EmitTensorRtProgress("超分 Engine", 0, "构建异常");
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

    private static bool ValidateTensorRTEngine(string enginePath, bool printSuccess, int inputWidth = 0, int inputHeight = 0, int tileSize = 0)
    {
        if (!File.Exists(TensorRTValidatorScript))
        {
            Console.Error.WriteLine("[错误] 缺少 TensorRT Engine 验证脚本：" + TensorRTValidatorScript);
            return false;
        }
        var validatorArgs = new List<string> { TensorRTValidatorScript, "--engine", enginePath };
        if (inputWidth > 0 && inputHeight > 0)
        {
            validatorArgs.Add("--width");
            validatorArgs.Add((tileSize > 0 ? Math.Min(inputWidth, tileSize + TensorRtTilePad * 2) : inputWidth).ToString(CultureInfo.InvariantCulture));
            validatorArgs.Add("--height");
            validatorArgs.Add((tileSize > 0 ? Math.Min(inputHeight, tileSize + TensorRtTilePad * 2) : inputHeight).ToString(CultureInfo.InvariantCulture));
        }
        var result = RunProcessCapture(PythonExe, validatorArgs.ToArray(), 120);
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
        if (string.Equals(backend, "rtxvsr", StringComparison.OrdinalIgnoreCase))
            return RunRtxCheck(verbose, requireVsr: true, requireHdr: false);

        var ok = true;

        Console.WriteLine("[环境检查] videoenhancer v" + ToolVersion);
        Console.WriteLine("[环境检查] 根目录   : " + CoreRoot);

        var ffmpegOk = File.Exists(FfmpegExe);
        Report(ffmpegOk, "3FUI FFmpeg", FfmpegExe);
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
            var osOk = OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763)
                       && RuntimeInformation.OSArchitecture == Architecture.X64;
            Report(osOk, "Windows 运行环境",
                RuntimeInformation.OSDescription + " / " + RuntimeInformation.OSArchitecture,
                osOk ? "支持 Windows 10 1809 或更高版本（x64）" : "需要 Windows 10 1809 或更高版本（x64）");
            ok &= osOk;

            var vcRuntimeOk = NativeLibrary.TryLoad("msvcp140.dll", out var vcRuntimeHandle);
            if (vcRuntimeOk) NativeLibrary.Free(vcRuntimeHandle);
            Report(vcRuntimeOk, "VC++ 运行库", "MSVCP140.dll",
                vcRuntimeOk
                    ? "已可加载"
                    : "请安装 Microsoft Visual C++ 2015-2022 x64 运行库：https://aka.ms/vc14/vc_redist.x64.exe");
            ok &= vcRuntimeOk;

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

            var backendProbe = RunBackendDependencyProbe(backend);
            if (backendProbe is not null)
            {
                Report(backendProbe.Value.Ok, "推理后端依赖", backend ?? "自动",
                    backendProbe.Value.Ok ? backendProbe.Value.Output.Trim().Replace('\n', ' ') : backendProbe.Value.Error.Trim());
                ok &= backendProbe.Value.Ok;
            }

            // 启动环境检查只验证基础组件，避免逐个加载大尺寸 TensorRT Engine。
            // Engine 兼容性仍由 --validate-engines、后端列表和实际推理路径按需检查。
        }

        Console.WriteLine("[环境检查] " + (ok ? "全部通过。" : "存在缺失项，请检查上方 [缺失] 标记。"));
        return ok;
    }

    private static bool RunRtxCheck(bool verbose, bool requireVsr, bool requireHdr)
    {
        Console.WriteLine("[环境检查] videoenhancer v" + ToolVersion + " / RTX Video");
        var exists = File.Exists(RtxVideoBackendExe);
        Report(exists, "RTX Video sidecar", RtxVideoBackendExe,
            exists ? "已安装" : "缺少 vsr_backend.exe 及其运行库");
        if (!exists) return false;
        try
        {
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            using var client = RtxVideoBackendClient.StartAsync(RtxVideoBackendExe, cancellation.Token).GetAwaiter().GetResult();
            var capabilities = client.GetCapabilitiesAsync(cancellation.Token).GetAwaiter().GetResult();
            var ok = capabilities.D3d11Available && capabilities.RtxSdkFound
                && (!requireVsr || capabilities.VsrAvailable)
                && (!requireHdr || capabilities.TruehdrAvailable);
            Report(capabilities.D3d11Available, "Direct3D 11", "Windows D3D11");
            Report(capabilities.RtxSdkFound, "NVIDIA RTX Video SDK", Path.GetDirectoryName(RtxVideoBackendExe)!);
            if (requireVsr || verbose) Report(capabilities.VsrAvailable, "RTX VSR", "NVIDIA RTX Video Super Resolution");
            if (requireHdr || verbose) Report(capabilities.TruehdrAvailable, "RTX Video HDR", "NVIDIA TrueHDR");
            if (verbose && capabilities.Messages.Length > 0)
                Console.WriteLine("[RTX Video] " + string.Join("；", capabilities.Messages));
            Console.WriteLine("[环境检查] " + (ok ? "全部通过。" : "RTX Video 能力不满足当前任务。"));
            return ok;
        }
        catch (Exception ex)
        {
            Report(false, "RTX Video sidecar 启动", RtxVideoBackendExe, ex.Message);
            return false;
        }
    }

    /// <summary>只导入所选后端的关键模块并检查设备，不加载模型或 TensorRT Engine。</summary>
    private static (bool Ok, string Output, string Error)? RunBackendDependencyProbe(string? backend)
    {
        if (string.IsNullOrWhiteSpace(backend)) return null;
        var script = backend.ToLowerInvariant() switch
        {
            "ncnn" =>
                "import ncnn, rife_ncnn_vulkan_python; print('ncnn', getattr(ncnn, '__version__', 'ok'), 'rife-ncnn ok')",
            "cuda" or "flashvsr" or "basicvsrpp" =>
                "import torch, torchvision; assert torch.cuda.is_available(), 'PyTorch 未检测到可用的 NVIDIA CUDA 设备，请更新显卡驱动'; print('torch', torch.__version__, 'cuda', torch.version.cuda, 'gpu', torch.cuda.get_device_name(0))",
            "tensorrt" =>
                "import torch, tensorrt, torch_tensorrt; assert torch.cuda.is_available(), 'TensorRT 未检测到可用的 NVIDIA CUDA 设备，请更新显卡驱动'; print('torch', torch.__version__, 'cuda', torch.version.cuda, 'tensorrt', tensorrt.__version__, 'gpu', torch.cuda.get_device_name(0))",
            "onnx" =>
                "import onnxruntime as ort; providers=ort.get_available_providers(); assert providers, 'ONNX Runtime 没有可用执行提供程序'; print('onnxruntime', ort.__version__, 'providers', ','.join(providers))",
            _ => null,
        };
        return script is null ? null : RunProcessCapture(PythonExe, new[] { "-c", script }, 90);
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
            PortablePaths.ConfigureChildProcess(psi);
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
            : isCuda ? "可用放大模型（CUDA，递归扫描 models 的 .pth/.pt/.pkl/.ckpt/.safetensors 文件，不含补帧目录）："
            : "可用放大模型（NCNN，递归扫描 models 中含 .param/.bin 的文件夹，不含补帧目录）：");
        if (models.Count == 0)
        {
            Console.WriteLine(isTensorRT
                ? "  (未找到任何 PTH 源模型或预制 .engine 文件)"
                : isOnnx ? "  (未找到任何 .onnx 模型文件)"
                : isCuda ? "  (未找到任何 PyTorch/safetensors 模型文件)"
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

    private static int ListUserModels(bool json)
    {
        var models = UserModelCatalog.Load(ModelsDir)
            .OrderBy(item => item.Task, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Architecture, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        if (!json)
        {
            foreach (var item in models)
                Console.WriteLine($"{item.Id}  {item.Architecture}  {item.Scale}x  [{string.Join(",", item.Backends)}]");
            return 0;
        }
        WriteUserModelsJson(models);
        return 0;
    }

    private static int UpdateUserModel(CliOptions options)
    {
        if (!int.TryParse(options.UserScale, NumberStyles.Integer, CultureInfo.InvariantCulture, out var scale))
            return Fail("--update-user-model 需要 --user-scale <整数>");
        if (!int.TryParse(options.UserInputMultiple, NumberStyles.Integer, CultureInfo.InvariantCulture, out var inputMultiple))
            return Fail("--update-user-model 需要 --user-input-multiple <整数>");
        if (string.IsNullOrWhiteSpace(options.UserArchitecture))
            return Fail("--update-user-model 需要 --user-architecture <架构>");
        if (string.IsNullOrWhiteSpace(options.UserBackends))
            return Fail("--update-user-model 需要 --user-backends <逗号分隔列表>");
        try
        {
            var updated = UserModelCatalog.UpdateCapabilities(
                ModelsDir, options.UpdateUserModel, options.UserArchitecture, options.UserPurpose,
                scale, inputMultiple, options.UserBackends.Split(',', StringSplitOptions.RemoveEmptyEntries));
            if (options.Json) WriteUserModelsJson([updated]);
            else Console.WriteLine("已更新用户模型能力：" + updated.Id);
            return 0;
        }
        catch (Exception ex)
        {
            return Fail("更新用户模型能力失败：" + ex.Message);
        }
    }

    private static int DeleteUserModel(CliOptions options)
    {
        try
        {
            var deleted = UserModelCatalog.Delete(ModelsDir, options.DeleteUserModel);
            if (options.Json)
            {
                using var writer = new Utf8JsonWriter(Console.OpenStandardOutput());
                writer.WriteStartObject();
                writer.WriteBoolean("deleted", true);
                writer.WriteString("id", deleted.Id);
                writer.WriteEndObject();
                writer.Flush();
                Console.WriteLine();
            }
            else
                Console.WriteLine("已删除用户模型：" + deleted.Id);
            return 0;
        }
        catch (Exception ex)
        {
            return Fail("删除用户模型失败：" + ex.Message);
        }
    }

    private static void WriteUserModelsJson(IEnumerable<UserModelRecord> models)
    {
        using var writer = new Utf8JsonWriter(Console.OpenStandardOutput());
        writer.WriteStartArray();
        foreach (var item in models)
        {
            writer.WriteStartObject();
            writer.WriteString("id", item.Id);
            writer.WriteString("displayName", item.DisplayName);
            writer.WriteString("relativePath", item.RelativePath);
            writer.WriteString("task", item.Task);
            writer.WriteString("architecture", item.Architecture);
            writer.WriteString("purpose", item.Purpose);
            writer.WriteString("format", item.Format);
            writer.WriteNumber("scale", item.Scale);
            writer.WriteNumber("inputMultiple", item.InputMultiple);
            writer.WriteNumber("minimumSize", item.MinimumSize);
            writer.WriteBoolean("square", item.Square);
            writer.WriteString("tiling", item.Tiling);
            writer.WriteString("sha256", item.Sha256);
            writer.WriteNumber("size", item.Size);
            writer.WriteString("importedAtUtc", item.ImportedAtUtc);
            writer.WriteStartArray("backends");
            foreach (var backend in item.Backends) writer.WriteStringValue(backend);
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        writer.Flush();
        Console.WriteLine();
    }

    private sealed class ModelListCatalogEntry
    {
        public string Id { get; init; } = "";
        public string DisplayName { get; init; } = "";
        public string Architecture { get; init; } = "";
        public string Purpose { get; init; } = "";
        public int Scale { get; init; }
        public string Source { get; init; } = "";
        public string[] Backends { get; init; } = [];
    }

    private static int ListModelCatalog(bool json, string backend, bool interpolation)
    {
        var paths = interpolation
            ? DiscoverInterpModels(backend)
            : backend == "basicvsrpp" ? DiscoverBasicVsrPlusPlusModels()
            : backend == "flashvsr" ? DiscoverFlashVsrModels()
            : backend == "cuda" ? DiscoverUpscalePthModels()
            : backend == "tensorrt" ? DiscoverTensorRTSelectableModels()
            : backend == "onnx" ? DiscoverOnnxModels()
            : DiscoverModelFolders();
        var userModels = UserModelCatalog.Load(ModelsDir);
        var entries = new List<ModelListCatalogEntry>();
        foreach (var path in paths)
        {
            var id = interpolation ? InterpModelDisplayName(path) : UpscaleModelDisplayName(path, backend);
            var user = userModels.FirstOrDefault(item =>
                UserModelCatalog.NormalizeRelativePath(item.RelativePath, ModelsDir)
                    .Equals(UserModelCatalog.NormalizeRelativePath(path, ModelsDir), StringComparison.OrdinalIgnoreCase));
            var normalizedPath = UserModelCatalog.NormalizeRelativePath(path, ModelsDir);
            if (user is null && normalizedPath.StartsWith("User/", StringComparison.OrdinalIgnoreCase))
                continue;
            if (user is not null && !string.IsNullOrWhiteSpace(backend)
                && !user.Backends.Contains(backend, StringComparer.OrdinalIgnoreCase))
                continue;
            ModelCapability? builtIn = null;
            if (user is null && ModelCapabilityCatalog.TryGet(path, ModelsDir, out var capability)) builtIn = capability;
            var architecture = user?.Architecture ?? builtIn?.Architecture ?? InferArchitecture(id, interpolation);
            var purpose = user?.Purpose ?? (interpolation ? "Interpolation" : "SR");
            var scale = user?.Scale ?? builtIn?.Scale ?? (int.TryParse(DetectScale(path), out var detected) ? detected : 0);
            var backends = user?.Backends ?? builtIn?.Backends ?? [backend];
            if (!interpolation && backend == "tensorrt" && user is null
                && Path.GetFileNameWithoutExtension(path).Equals("realesr-animevideov3", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var outputScale in new[] { 2, 3, 4 })
                {
                    entries.Add(new ModelListCatalogEntry
                    {
                        Id = id + "-" + outputScale.ToString(CultureInfo.InvariantCulture) + "x",
                        DisplayName = ModelBaseName(path) + "-" + outputScale.ToString(CultureInfo.InvariantCulture) + "x",
                        Architecture = architecture,
                        Purpose = purpose,
                        Scale = outputScale,
                        Source = builtIn is not null ? "builtin" : "discovered",
                        Backends = backends,
                    });
                }
                continue;
            }
            entries.Add(new ModelListCatalogEntry
            {
                Id = id,
                DisplayName = ModelBaseName(path),
                Architecture = architecture,
                Purpose = purpose,
                Scale = scale,
                Source = user is not null ? "user" : builtIn is not null ? "builtin" : "discovered",
                Backends = backends,
            });
        }
        entries = entries.OrderBy(item => item.Architecture, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToList();
        if (!json)
        {
            foreach (var group in entries.GroupBy(item => item.Architecture))
            {
                Console.WriteLine(group.Key + "：");
                foreach (var item in group) Console.WriteLine("  " + item.Id);
            }
            return 0;
        }
        using var writer = new Utf8JsonWriter(Console.OpenStandardOutput());
        writer.WriteStartArray();
        foreach (var item in entries)
        {
            writer.WriteStartObject();
            writer.WriteString("id", item.Id);
            writer.WriteString("displayName", item.DisplayName);
            writer.WriteString("architecture", item.Architecture);
            writer.WriteString("purpose", item.Purpose);
            writer.WriteNumber("scale", item.Scale);
            writer.WriteString("source", item.Source);
            writer.WriteStartArray("backends");
            foreach (var value in item.Backends) writer.WriteStringValue(value);
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        writer.Flush();
        Console.WriteLine();
        return 0;
    }

    private static string InferArchitecture(string id, bool interpolation)
    {
        var normalized = id.Replace('\\', '/');
        if (normalized.StartsWith("User/", StringComparison.OrdinalIgnoreCase))
        {
            var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length >= 3) return segments[2];
        }
        foreach (var architecture in new[]
        {
            "RealESRGAN", "RealHatGAN", "ESRGAN", "SPANPlus", "SPAN", "SwinIR", "RealCUGAN",
            "AnimeSR", "CRAFT", "DITN", "MoSR", "RIFE", "GMFSS", "GIMM"
        })
            if (normalized.Contains(architecture, StringComparison.OrdinalIgnoreCase)) return architecture;
        if (interpolation)
        {
            var first = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(first)) return first;
        }
        return "其他模型";
    }

    private static List<string> DiscoverModelFolders()
    {
        if (!Directory.Exists(ModelsDir))
        {
            return new List<string>();
        }
        return Directory.GetDirectories(ModelsDir, "*", SearchOption.AllDirectories)
            .Where(p => !IsInInterpolationDirectory(p))
            .Where(p => !IsInRestorationDirectory(p))
            .Where(IsNcnnModelFolder)
            .Where(p => !ModelBaseName(p).Equals("EfficientNet-SceneDetect", StringComparison.OrdinalIgnoreCase))
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
        foreach (var pattern in new[] { "*.pth", "*.pt", "*.pkl", "*.ckpt", "*.safetensors" })
        {
            foreach (var f in Directory.GetFiles(ModelsDir, pattern, SearchOption.AllDirectories)
                         .Where(p => !IsInInterpolationDirectory(p) && !IsInRestorationDirectory(p)
                             && !IsInFlashVsrDirectory(p) && !IsInBasicVsrPlusPlusDirectory(p)))
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
    /// TensorRT 任务只展示 PTH 源模型；Engine 始终按当前任务配置自动构建到本机缓存。
    /// </summary>
    private static List<string> DiscoverTensorRTSelectableModels()
    {
        return DiscoverUpscalePthModels()
            .Where(IsTensorRtConvertibleUpscaleSource)
            .OrderBy(path => UpscaleModelDisplayName(path, "tensorrt"), StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <summary>排除已实机确认不能进入当前单图直接 Engine 路径的架构。</summary>
    private static bool IsTensorRtConvertibleUpscaleSource(string path)
    {
        var name = ModelBaseName(path);
        return !Regex.IsMatch(name, @"AnimeSR|SwinIR|CRAFT", RegexOptions.IgnoreCase);
    }

    private static List<string> DiscoverOnnxModels()
    {
        if (!Directory.Exists(ModelsDir)) return new List<string>();
        return Directory.GetFiles(ModelsDir, "*.onnx", SearchOption.AllDirectories)
            .Where(p => !IsInInterpolationDirectory(p))
            .Where(p => !IsInRestorationDirectory(p))
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
        IsPathUnder(path, FrameInterpolationDir) || IsPathUnder(path, UserInterpolationDir) || IsInLegacyRifeDirectory(path);

    private static bool IsInRestorationDirectory(string path) => IsPathUnder(path, UserRestorationDir);

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
        return Path.GetFullPath(path).StartsWith(root, StringComparison.OrdinalIgnoreCase)
            || HasModelPackageAncestor(path, IsBasicVsrPlusPlusModelDirectory);
    }

    private static bool IsInFlashVsrDirectory(string path)
    {
        var root = Path.GetFullPath(Path.Combine(ModelsDir, "FlashVSR"))
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        return Path.GetFullPath(path).StartsWith(root, StringComparison.OrdinalIgnoreCase)
            || HasModelPackageAncestor(path, IsFlashVsrModelDirectory);
    }

    private static bool HasModelPackageAncestor(string path, Func<string, bool> predicate)
    {
        var directory = File.Exists(path) ? Path.GetDirectoryName(Path.GetFullPath(path)) : Path.GetFullPath(path);
        var modelsRoot = Path.GetFullPath(ModelsDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        while (!string.IsNullOrWhiteSpace(directory)
               && directory.StartsWith(modelsRoot, StringComparison.OrdinalIgnoreCase))
        {
            if (predicate(directory)) return true;
            if (directory.Equals(modelsRoot, StringComparison.OrdinalIgnoreCase)) break;
            directory = Path.GetDirectoryName(directory);
        }
        return false;
    }

    private static string? FindNcnnModelFolder(string modelName)
    {
        if (!Directory.Exists(ModelsDir)) return null;
        // 场景检测模型不应出现在超分下拉框，但内部仍需要能够定位它。
        return Directory.GetDirectories(ModelsDir, "*", SearchOption.AllDirectories)
            .Where(IsNcnnModelFolder)
            .FirstOrDefault(p =>
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


}
