namespace VideoEnhancer;

/// <summary>区分宿主原生 FFmpeg 调用与带编码参数封装的增强任务。</summary>
internal static class FfmpegPassthrough
{
    internal static bool IsNativeCommand(string[] args)
    {
        if (args.Length == 0 || !args[0].StartsWith('-') || args[0].StartsWith("--")) return false;
        // 保留 CLI 自身的简写帮助和版本命令；-h filter=libvmaf、-v error 属于 FFmpeg。
        if (args.Length == 1 && args[0] is "-h" or "-v") return false;
        return !args.Any(arg => arg.StartsWith('-') && arg.Split('=', 2)[0].TrimStart('-') is
            "ffmpeg-settings" or "modelpath" or "model" or "output-scale" or "scale" or
            "pause-shm" or "stop-shm" or "interp-model" or "interp-modelpath" or "interp-factor" or
            "no-upscale" or "rtx-hdr" or "rtx-target" or "rtx-quality" or "rtx-hdr-contrast" or
            "rtx-hdr-saturation" or "rtx-hdr-middle-gray" or "rtx-hdr-max-luminance" or
            "backend" or "interp-backend" or "process-order" or "upscale-precision" or
            "interp-precision" or "scene-threshold" or "dynamic-optical-flow" or "tile-size");
    }
}
