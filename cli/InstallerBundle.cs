using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VideoEnhancer;

/// <summary>
/// 安装器容器：前缀是控制台运行 EXE，尾部承载安装时释放的独立组件。
/// 安装时校验前缀，释放不带安装载荷的运行 EXE。
/// </summary>
internal sealed partial class InstallerBundle : IDisposable
{
    private const int SchemaVersion = 1;
    private const int FooterLength = sizeof(long) + 16;
    private const int MaximumManifestLength = 1024 * 1024;
    private const ushort ConsoleSubsystem = 3;
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("VIDEOENH-BUNDLE1");

    private readonly FileStream stream;
    private readonly BundleManifest manifest;
    private readonly long manifestOffset;

    private InstallerBundle(FileStream stream, BundleManifest manifest, long manifestOffset)
    {
        this.stream = stream;
        this.manifest = manifest;
        this.manifestOffset = manifestOffset;
    }

    internal IReadOnlyList<string> PayloadPaths => manifest.Entries
        .Select(entry => entry.RelativePath)
        .ToArray();

    internal static bool HasFooter(string path)
    {
        using var source = File.OpenRead(path);
        if (source.Length < FooterLength) return false;
        source.Position = source.Length - Magic.Length;
        var marker = new byte[Magic.Length];
        source.ReadExactly(marker);
        return marker.AsSpan().SequenceEqual(Magic);
    }

    internal static void Create(string runtimePath, string outputPath, string payloadRoot)
    {
        var runtime = Path.GetFullPath(runtimePath);
        var output = Path.GetFullPath(outputPath);
        var payload = Path.GetFullPath(payloadRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!File.Exists(runtime)) throw new FileNotFoundException("安装器运行文件不存在", runtime);
        if (!Directory.Exists(payload)) throw new DirectoryNotFoundException("安装器载荷目录不存在：" + payload);
        if (runtime.Equals(output, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("安装器输出不能覆盖正在使用的运行文件");

        var payloadFiles = Directory.EnumerateFiles(payload, "*", SearchOption.AllDirectories)
            .Select(path => new
            {
                Source = path,
                RelativePath = NormalizeRelativePath(Path.GetRelativePath(payload, path))
            })
            .OrderBy(item => item.RelativePath, StringComparer.Ordinal)
            .ToArray();
        if (payloadFiles.Length == 0) throw new InvalidDataException("安装器载荷为空");

        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        var temporary = output + ".new-" + Guid.NewGuid().ToString("N");
        try
        {
            var bundleManifest = new BundleManifest();
            using (var destination = new FileStream(temporary, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            {
                using (var source = File.OpenRead(runtime))
                {
                    CopyAndHash(source, destination, source.Length);
                    bundleManifest.RuntimeLength = destination.Position;
                }
                destination.Position = 0;
                bundleManifest.RuntimeSha256 = Convert.ToHexString(SHA256.HashData(destination));
                destination.Position = bundleManifest.RuntimeLength;

                foreach (var item in payloadFiles)
                {
                    using var source = File.OpenRead(item.Source);
                    var entry = new BundleEntry
                    {
                        RelativePath = item.RelativePath,
                        Offset = destination.Position,
                        Length = source.Length
                    };
                    entry.Sha256 = CopyAndHash(source, destination, entry.Length);
                    bundleManifest.Entries.Add(entry);
                }

                var manifestBytes = JsonSerializer.SerializeToUtf8Bytes(
                    bundleManifest, BundleJsonContext.Default.BundleManifest);
                if (manifestBytes.Length > MaximumManifestLength)
                    throw new InvalidDataException("安装器清单过大");
                destination.Write(manifestBytes);
                Span<byte> manifestLength = stackalloc byte[sizeof(long)];
                BinaryPrimitives.WriteInt64LittleEndian(manifestLength, manifestBytes.Length);
                destination.Write(manifestLength);
                destination.Write(Magic);
                destination.Flush(true);
            }
            File.Move(temporary, output, true);
        }
        finally
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); } catch { }
        }
    }

    internal static InstallerBundle Open(string packagePath)
    {
        var fullPath = Path.GetFullPath(packagePath);
        var source = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
        try
        {
            if (source.Length < FooterLength)
                throw new InvalidDataException("该文件不是完整的 VideoEnhancer 安装器");

            source.Position = source.Length - FooterLength;
            Span<byte> footer = stackalloc byte[FooterLength];
            source.ReadExactly(footer);
            var manifestLength = BinaryPrimitives.ReadInt64LittleEndian(footer[..sizeof(long)]);
            if (!footer[sizeof(long)..].SequenceEqual(Magic))
                throw new InvalidDataException("安装器缺少有效的载荷尾标");
            if (manifestLength <= 0 || manifestLength > MaximumManifestLength)
                throw new InvalidDataException("安装器清单长度无效");

            var manifestOffset = source.Length - FooterLength - manifestLength;
            if (manifestOffset <= 0) throw new InvalidDataException("安装器清单位置无效");
            source.Position = manifestOffset;
            var json = new byte[checked((int)manifestLength)];
            source.ReadExactly(json);
            var manifest = JsonSerializer.Deserialize(json, BundleJsonContext.Default.BundleManifest)
                ?? throw new InvalidDataException("安装器清单为空");
            ValidateManifest(manifest, manifestOffset);
            return new InstallerBundle(source, manifest, manifestOffset);
        }
        catch
        {
            source.Dispose();
            throw;
        }
    }

    internal void ExtractRuntime(string destinationPath)
    {
        ExtractRange(destinationPath, 0, manifest.RuntimeLength, manifest.RuntimeSha256);
        using var runtime = new FileStream(destinationPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        ValidateConsoleSubsystem(runtime);
    }

    private static void ValidateConsoleSubsystem(FileStream image)
    {
        if (image.Length < 0x100) throw new InvalidDataException("安装器 PE 文件过短");
        image.Position = 0x3C;
        Span<byte> offsetBytes = stackalloc byte[4];
        image.ReadExactly(offsetBytes);
        var peOffset = BinaryPrimitives.ReadInt32LittleEndian(offsetBytes);
        if (peOffset < 0x40 || peOffset > image.Length - 96)
            throw new InvalidDataException("安装器 PE 头位置无效");
        image.Position = peOffset;
        Span<byte> signature = stackalloc byte[4];
        image.ReadExactly(signature);
        if (!signature.SequenceEqual("PE\0\0"u8))
            throw new InvalidDataException("安装器缺少 PE 签名");
        var optionalHeader = peOffset + 24;
        image.Position = optionalHeader;
        Span<byte> magicBytes = stackalloc byte[2];
        image.ReadExactly(magicBytes);
        var magic = BinaryPrimitives.ReadUInt16LittleEndian(magicBytes);
        if (magic is not (0x10B or 0x20B))
            throw new InvalidDataException("安装器 PE 可选头格式无效");
        image.Position = optionalHeader + 68;
        Span<byte> subsystemBytes = stackalloc byte[2];
        image.ReadExactly(subsystemBytes);
        if (BinaryPrimitives.ReadUInt16LittleEndian(subsystemBytes) != ConsoleSubsystem)
            throw new InvalidDataException("安装器 PE 子系统与预期不符");
    }

    internal IReadOnlyList<StagedApplicationFile> ExtractPayload(string destinationRoot)
    {
        var root = Path.GetFullPath(destinationRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        Directory.CreateDirectory(root);
        var staged = new List<StagedApplicationFile>(manifest.Entries.Count);
        foreach (var entry in manifest.Entries)
        {
            var destination = ResolveInsideRoot(root, entry.RelativePath);
            ExtractRange(destination, entry.Offset, entry.Length, entry.Sha256);
            staged.Add(new StagedApplicationFile(entry.RelativePath, destination));
        }
        return staged;
    }

    public void Dispose() => stream.Dispose();

    private void ExtractRange(string destinationPath, long offset, long length, string expectedHash)
    {
        if (offset < 0 || length < 0 || offset > manifestOffset - length)
            throw new InvalidDataException("安装器载荷范围无效");

        var destination = Path.GetFullPath(destinationPath);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var temporary = destination + ".new-" + Guid.NewGuid().ToString("N");
        try
        {
            stream.Position = offset;
            string actualHash;
            using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                actualHash = CopyAndHash(stream, output, length);
                output.Flush(true);
            }
            if (!actualHash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("安装器载荷校验失败：" + destination);
            File.Move(temporary, destination, true);
        }
        finally
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); } catch { }
        }
    }

    private static string CopyAndHash(Stream source, Stream destination, long count)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[128 * 1024];
        var remaining = count;
        while (remaining > 0)
        {
            var read = source.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining));
            if (read == 0) throw new EndOfStreamException("安装器载荷被意外截断");
            destination.Write(buffer, 0, read);
            hash.AppendData(buffer, 0, read);
            remaining -= read;
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static void ValidateManifest(BundleManifest value, long manifestOffset)
    {
        if (value.SchemaVersion != SchemaVersion)
            throw new InvalidDataException("不支持的安装器清单版本：" + value.SchemaVersion);
        if (value.RuntimeLength <= 0 || value.RuntimeLength > manifestOffset)
            throw new InvalidDataException("安装器运行文件长度无效");
        ValidateHash(value.RuntimeSha256, "运行文件");
        if (value.Entries.Count == 0) throw new InvalidDataException("安装器没有载荷");

        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var previousEnd = value.RuntimeLength;
        foreach (var entry in value.Entries.OrderBy(item => item.Offset))
        {
            entry.RelativePath = NormalizeRelativePath(entry.RelativePath);
            if (!paths.Add(entry.RelativePath))
                throw new InvalidDataException("安装器载荷路径重复：" + entry.RelativePath);
            ValidateHash(entry.Sha256, entry.RelativePath);
            if (entry.Offset < previousEnd || entry.Length < 0 || entry.Offset > manifestOffset - entry.Length)
                throw new InvalidDataException("安装器载荷范围重叠或越界：" + entry.RelativePath);
            previousEnd = entry.Offset + entry.Length;
        }
    }

    private static void ValidateHash(string value, string name)
    {
        if (value.Length != 64 || value.Any(character => !Uri.IsHexDigit(character)))
            throw new InvalidDataException("安装器 SHA-256 无效：" + name);
    }

    internal static string NormalizeRelativePath(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new InvalidDataException("安装器载荷路径为空");
        var normalized = value.Replace('\\', '/');
        if (normalized.StartsWith('/') || Path.IsPathRooted(normalized) || normalized.Contains(':'))
            throw new InvalidDataException("安装器载荷不是相对路径：" + value);
        var parts = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || parts.Any(part => part is "." or ".."))
            throw new InvalidDataException("安装器载荷路径不安全：" + value);
        return string.Join('/', parts);
    }

    private static string ResolveInsideRoot(string root, string relativePath)
    {
        var destination = Path.GetFullPath(Path.Combine(root,
            NormalizeRelativePath(relativePath).Replace('/', Path.DirectorySeparatorChar)));
        var prefix = root + Path.DirectorySeparatorChar;
        if (!destination.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("安装器载荷越出目标目录：" + relativePath);
        return destination;
    }

    private sealed class BundleManifest
    {
        public int SchemaVersion { get; set; } = InstallerBundle.SchemaVersion;
        public long RuntimeLength { get; set; }
        public string RuntimeSha256 { get; set; } = "";
        public List<BundleEntry> Entries { get; set; } = new();
    }

    private sealed class BundleEntry
    {
        public string RelativePath { get; set; } = "";
        public long Offset { get; set; }
        public long Length { get; set; }
        public string Sha256 { get; set; } = "";
    }

    [JsonSourceGenerationOptions(WriteIndented = false)]
    [JsonSerializable(typeof(BundleManifest))]
    private sealed partial class BundleJsonContext : JsonSerializerContext
    {
    }
}

internal readonly record struct StagedApplicationFile(string RelativePath, string SourcePath);
