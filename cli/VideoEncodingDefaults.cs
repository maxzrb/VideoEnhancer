namespace VideoEnhancer;

/// <summary>缺少视频编码器时使用 SVT y412 超分预设，避免 FFmpeg 静默选择默认编码器。</summary>
internal static class VideoEncodingDefaults
{
    internal const string Arguments = "-c:v libsvtav1 -preset:v 6 -profile:v professional -crf:v 12 -pix_fmt:v yuv444p12le "
        + "-svtav1-params tune=1:scd=1:scd-min-keyint=33:keyint=321:enable-tf=0:enable-cdef=-1:"
        + "enable-dlf=2:scm=3:enable-qm=1:qm-min=2:chroma-qm-min=4:enable-variance-boost=1:"
        + "variance-boost-strength=1:film-grain=0:adaptive-film-grain=0:sharpness=1:ac-bias=1:lp=4";

    internal static bool HasVideoEncoder(IReadOnlyList<string> tokens) => VideoEncoder(tokens) is not null;

    internal static bool RequiresSvtY412(IReadOnlyList<string> tokens)
    {
        if (!string.Equals(VideoEncoder(tokens), "libsvtav1", StringComparison.OrdinalIgnoreCase)) return false;
        string? pixelFormat = null;
        for (var i = 0; i + 1 < tokens.Count; i++)
            if (tokens[i] == "-pix_fmt" || tokens[i].StartsWith("-pix_fmt:v", StringComparison.Ordinal))
                pixelFormat = tokens[++i];
        return pixelFormat?.TrimStart('+') == "yuv444p12le";
    }

    private static string? VideoEncoder(IReadOnlyList<string> tokens)
    {
        string? encoder = null;
        for (var i = 0; i + 1 < tokens.Count; i++)
        {
            var option = tokens[i];
            if (option is "-c" or "-codec" or "-vcodec" || option.StartsWith("-c:v", StringComparison.Ordinal)
                || option.StartsWith("-codec:v", StringComparison.Ordinal))
                encoder = tokens[++i];
        }
        return encoder;
    }

    internal static bool SupportsY412(string encoderHelp)
    {
        foreach (var line in encoderHelp.Split('\n'))
        {
            const string label = "Supported pixel formats:";
            var text = line.Trim();
            if (text.StartsWith(label, StringComparison.Ordinal))
                return text[label.Length..].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Contains("yuv444p12le");
        }
        return false;
    }
}
