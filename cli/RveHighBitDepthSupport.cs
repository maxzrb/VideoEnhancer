using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace VideoEnhancer;

/// <summary>在启动推理前同步与入口脚本配套的 RGB48 核心，避免混用帧协议。</summary>
internal static class RveHighBitDepthSupport
{
    private const string ResourcePrefix = "VideoEnhancer.Embedded.RveHighBitDepth/";
    private const string ManagedMarker = ".videoenhancer-rgb48-owner";

    internal static bool IsManaged(string backendDirectory)
    {
        var marker = Path.Combine(backendDirectory, ManagedMarker);
        return File.Exists(marker) && File.ReadAllText(marker, Encoding.UTF8).Trim() == "cli";
    }

    internal static void MarkManaged(string backendDirectory)
    {
        var marker = Path.Combine(backendDirectory, ManagedMarker);
        if (!File.Exists(marker)) File.WriteAllText(marker, "cli\n", new UTF8Encoding(false));
    }

    internal static void ValidateExternal(string backendDirectory)
    {
        var protocol = File.ReadAllText(Path.Combine(backendDirectory, "src", "utils", "PixelFormat.py"), Encoding.UTF8);
        if (!Regex.IsMatch(protocol, "FRAME_PROTOCOL\\s*=\\s*['\"]rgb48-v1['\"]"))
            throw new InvalidOperationException("独立高位深后端缺少配套的 rgb48-v1 修订标记，请完整安装修订后端；现有脚本未改动");
        foreach (var file in new[] { "rve-backend.py", "rve-ordered-backend.py", "rve-segmented-backend.py",
                                    "rve-image-backend.py", "rve_output_scale.py", "convert_tensorrt.py" })
            if (!File.Exists(Path.Combine(backendDirectory, file)))
                throw new FileNotFoundException("独立高位深后端不完整，现有脚本未改动：" + file);
    }

    internal static void Install(string backendDirectory)
    {
        if (!File.Exists(Path.Combine(backendDirectory, "rve-backend.py"))) return;
        var versionFile = Path.Combine(backendDirectory, "src", "version.py");
        if (!File.Exists(versionFile) || !File.ReadAllText(versionFile, Encoding.UTF8)
                .Contains("2.4.1-dev17", StringComparison.Ordinal))
            throw new InvalidOperationException("16bit核心补丁需要配套的 RVE 2.4.1-dev17 后端，请先更新后端组件");

        // 先记录所有权；若中途写入失败，下次启动仍会重试完整同步。
        MarkManaged(backendDirectory);
        var assembly = Assembly.GetExecutingAssembly();
        foreach (var resourceName in assembly.GetManifestResourceNames()
                     .Where(name => name.StartsWith(ResourcePrefix, StringComparison.Ordinal))
                     .Order(StringComparer.Ordinal))
        {
            var relative = resourceName[ResourcePrefix.Length..].Replace('\\', '/');
            var destination = Path.Combine(backendDirectory, relative.Replace('/', Path.DirectorySeparatorChar));
            using var resource = assembly.GetManifestResourceStream(resourceName)!;
            if (File.Exists(destination))
            {
                using var existing = File.OpenRead(destination);
                var matches = SHA256.HashData(resource).AsSpan().SequenceEqual(SHA256.HashData(existing));
                resource.Position = 0;
                if (matches) continue;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            // 完整写入临时文件后再替换，写入失败时不截断原脚本。
            var staged = destination + ".rgb48-new";
            using (var output = File.Create(staged)) resource.CopyTo(output);
            File.Move(staged, destination, true);
        }
    }
}
