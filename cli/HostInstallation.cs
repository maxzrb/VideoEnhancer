namespace VideoEnhancer;

/// <summary>兼容 3FUI 首次下载与本体更新后的主程序名称。</summary>
internal static class HostInstallation
{
    internal const string ExecutableNames = "FFmpegFreeUI.exe 或 FFmpegFreeUI.x64.exe";
    internal const string ExecutableFilter = "FFmpegFreeUI.exe;FFmpegFreeUI.x64.exe";

    internal static bool IsHostRoot(string root) =>
        File.Exists(Path.Combine(root, "FFmpegFreeUI.exe")) ||
        File.Exists(Path.Combine(root, "FFmpegFreeUI.x64.exe"));
}
