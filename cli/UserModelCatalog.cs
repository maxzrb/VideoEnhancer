using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace VideoEnhancer;

/// <summary>用户导入模型的持久能力清单；只登记已经通过预检并完成原子安装的模型。</summary>
internal static class UserModelCatalog
{
    internal const int SchemaVersion = 1;
    private static readonly object CacheSync = new();
    private static string _cachedPath = "";
    private static long _cachedLength = -1;
    private static long _cachedWriteTicks = -1;
    private static IReadOnlyList<UserModelRecord> _cachedModels = [];

    internal static string UserRoot(string modelsDirectory) => Path.Combine(modelsDirectory, "User");
    internal static string CatalogPath(string modelsDirectory) => Path.Combine(UserRoot(modelsDirectory), "model-catalog.json");

    internal static IReadOnlyList<UserModelRecord> Load(string modelsDirectory)
    {
        var path = CatalogPath(modelsDirectory);
        if (!File.Exists(path)) return [];
        try
        {
            var info = new FileInfo(path);
            lock (CacheSync)
            {
                if (_cachedPath.Equals(path, StringComparison.OrdinalIgnoreCase)
                    && _cachedLength == info.Length && _cachedWriteTicks == info.LastWriteTimeUtc.Ticks)
                    return _cachedModels;
            }
            using var document = JsonDocument.Parse(File.ReadAllBytes(path));
            var root = document.RootElement;
            if (root.GetProperty("schemaVersion").GetInt32() != SchemaVersion) return [];
            var result = new List<UserModelRecord>();
            foreach (var item in root.GetProperty("models").EnumerateArray())
            {
                var record = new UserModelRecord
                {
                    Id = item.GetProperty("id").GetString() ?? "",
                    DisplayName = item.GetProperty("displayName").GetString() ?? "",
                    RelativePath = item.GetProperty("relativePath").GetString() ?? "",
                    Task = item.GetProperty("task").GetString() ?? "",
                    Architecture = item.GetProperty("architecture").GetString() ?? "",
                    Purpose = item.GetProperty("purpose").GetString() ?? "",
                    Format = item.GetProperty("format").GetString() ?? "",
                    Scale = item.GetProperty("scale").GetInt32(),
                    InputChannels = item.TryGetProperty("inputChannels", out var inputChannels) ? inputChannels.GetInt32() : 3,
                    OutputChannels = item.TryGetProperty("outputChannels", out var outputChannels) ? outputChannels.GetInt32() : 3,
                    InputMultiple = item.TryGetProperty("inputMultiple", out var multiple) ? multiple.GetInt32() : 1,
                    MinimumSize = item.TryGetProperty("minimumSize", out var minimum) ? minimum.GetInt32() : 0,
                    Square = item.TryGetProperty("square", out var square) && square.GetBoolean(),
                    SupportsHalf = item.TryGetProperty("supportsHalf", out var half) && half.GetBoolean(),
                    SupportsBFloat16 = item.TryGetProperty("supportsBfloat16", out var bf16) && bf16.GetBoolean(),
                    Tiling = item.TryGetProperty("tiling", out var tiling) ? tiling.GetString() ?? "" : "",
                    Sha256 = item.GetProperty("sha256").GetString() ?? "",
                    Size = item.GetProperty("size").GetInt64(),
                    ImportedAtUtc = item.GetProperty("importedAtUtc").GetString() ?? "",
                    Backends = item.GetProperty("backends").EnumerateArray()
                        .Select(value => value.GetString() ?? "").Where(value => value.Length > 0).ToArray(),
                };
                if (record.Id.Length > 0 && record.RelativePath.Length > 0 && record.Backends.Length > 0)
                    result.Add(record);
            }
            lock (CacheSync)
            {
                _cachedPath = path;
                _cachedLength = info.Length;
                _cachedWriteTicks = info.LastWriteTimeUtc.Ticks;
                _cachedModels = result;
            }
            return result;
        }
        catch
        {
            // 清单损坏时不把未经确认的 User 文件暴露给工作台，等待重新导入或修复清单。
            return [];
        }
    }

    internal static void Save(string modelsDirectory, IReadOnlyList<UserModelRecord> records)
    {
        var directory = UserRoot(modelsDirectory);
        Directory.CreateDirectory(directory);
        var path = CatalogPath(modelsDirectory);
        var temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
        using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", SchemaVersion);
            writer.WriteStartArray("models");
            foreach (var item in records.OrderBy(value => value.Id, StringComparer.OrdinalIgnoreCase))
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
                writer.WriteNumber("inputChannels", item.InputChannels);
                writer.WriteNumber("outputChannels", item.OutputChannels);
                writer.WriteNumber("inputMultiple", Math.Max(1, item.InputMultiple));
                writer.WriteNumber("minimumSize", Math.Max(0, item.MinimumSize));
                writer.WriteBoolean("square", item.Square);
                writer.WriteBoolean("supportsHalf", item.SupportsHalf);
                writer.WriteBoolean("supportsBfloat16", item.SupportsBFloat16);
                writer.WriteString("tiling", item.Tiling);
                writer.WriteStartArray("backends");
                foreach (var backend in item.Backends) writer.WriteStringValue(backend);
                writer.WriteEndArray();
                writer.WriteString("sha256", item.Sha256);
                writer.WriteNumber("size", item.Size);
                writer.WriteString("importedAtUtc", item.ImportedAtUtc);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        File.Move(temporary, path, overwrite: true);
        lock (CacheSync)
        {
            _cachedPath = "";
            _cachedLength = -1;
            _cachedWriteTicks = -1;
            _cachedModels = [];
        }
    }

    internal static bool TryGet(string modelPath, string modelsDirectory, out UserModelRecord record)
    {
        var key = NormalizeRelativePath(modelPath, modelsDirectory);
        record = Load(modelsDirectory).FirstOrDefault(item =>
            NormalizeRelativePath(item.RelativePath, modelsDirectory).Equals(key, StringComparison.OrdinalIgnoreCase))!;
        return record is not null;
    }

    internal static UserModelRecord UpdateCapabilities(
        string modelsDirectory,
        string id,
        string architecture,
        string purpose,
        int scale,
        int inputMultiple,
        IEnumerable<string> backends)
    {
        var records = Load(modelsDirectory).ToList();
        var record = records.FirstOrDefault(item => item.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("未找到指定的用户模型");
        architecture = (architecture ?? "").Trim();
        purpose = (purpose ?? "").Trim();
        if (architecture.Length is < 1 or > 80)
            throw new InvalidOperationException("架构名称必须为 1–80 个字符");
        if (architecture.Any(char.IsControl))
            throw new InvalidOperationException("架构名称不能包含控制字符");
        if (inputMultiple is < 1 or > 1024)
            throw new InvalidOperationException("输入尺寸倍数必须在 1–1024 之间");

        var task = record.Task.ToLowerInvariant();
        if (task is "interpolation" or "restoration") scale = 1;
        else if (scale is < 1 or > 16)
            throw new InvalidOperationException("模型倍率必须在 1–16 之间");
        if (string.IsNullOrWhiteSpace(purpose))
            purpose = task == "interpolation" ? "Interpolation" : task == "restoration" ? "Restoration" : "SR";

        var requested = backends.Select(value => (value ?? "").Trim().ToLowerInvariant())
            .Where(value => value.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (requested.Length == 0) throw new InvalidOperationException("至少选择一个可用后端");
        var allowed = AllowedBackends(record, architecture);
        var invalid = requested.Where(value => !allowed.Contains(value, StringComparer.OrdinalIgnoreCase)).ToArray();
        if (invalid.Length > 0)
            throw new InvalidOperationException("模型格式或架构不支持后端：" + string.Join(" / ", invalid));

        record.Architecture = architecture;
        record.Purpose = purpose;
        record.Scale = scale;
        record.InputMultiple = inputMultiple;
        record.Backends = requested;
        Save(modelsDirectory, records);
        return record;
    }

    /// <summary>删除用户模型文件及其能力清单记录；文件先移入临时目录，清单写入失败时恢复。</summary>
    internal static UserModelRecord Delete(string modelsDirectory, string id)
    {
        var records = Load(modelsDirectory).ToList();
        var record = records.FirstOrDefault(item => item.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("未找到指定的用户模型");

        var modelsRoot = Path.GetFullPath(modelsDirectory);
        var userRoot = Path.GetFullPath(UserRoot(modelsDirectory))
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var relative = NormalizeRelativePath(record.RelativePath, modelsRoot);
        if (!relative.StartsWith("User/", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("用户模型路径不在 models\\User 目录中，已拒绝删除");

        var installedPath = Path.GetFullPath(Path.Combine(
            modelsRoot, relative.Replace('/', Path.DirectorySeparatorChar)));
        var isDirectoryModel = record.Format.Equals("directory", StringComparison.OrdinalIgnoreCase)
            || record.Format.Equals("ncnn", StringComparison.OrdinalIgnoreCase);
        var modelContainer = isDirectoryModel
            ? installedPath
            : Path.GetDirectoryName(installedPath)!;
        if (!IsPathInside(modelContainer, userRoot))
            throw new InvalidOperationException("用户模型路径超出 models\\User 目录，已拒绝删除");

        var containerRelative = Path.GetRelativePath(userRoot, modelContainer);
        var containerSegments = containerRelative.Split(
            new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
            StringSplitOptions.RemoveEmptyEntries);
        if (containerSegments.Length < 3 || containerSegments.Any(segment => segment == ".."))
            throw new InvalidOperationException("用户模型路径层级异常，已拒绝删除");

        var hasTarget = Directory.Exists(modelContainer) || File.Exists(modelContainer);
        string? temporaryPath = null;
        if (hasTarget)
        {
            var attributes = File.GetAttributes(modelContainer);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("用户模型目录是符号链接或联接点，已拒绝递归删除");

            temporaryPath = Path.Combine(userRoot, ".deleting-" + Guid.NewGuid().ToString("N"));
            MovePath(modelContainer, temporaryPath);
        }

        var remaining = records.Where(item => !ReferenceEquals(item, record)).ToList();
        try
        {
            Save(modelsRoot, remaining);
        }
        catch
        {
            if (temporaryPath is not null && (Directory.Exists(temporaryPath) || File.Exists(temporaryPath)))
                MovePath(temporaryPath, modelContainer);
            throw;
        }

        if (temporaryPath is not null)
        {
            try
            {
                DeletePath(temporaryPath);
            }
            catch (Exception cleanupError)
            {
                try
                {
                    Save(modelsRoot, records);
                    if (Directory.Exists(temporaryPath) || File.Exists(temporaryPath))
                        MovePath(temporaryPath, modelContainer);
                }
                catch (Exception rollbackError)
                {
                    throw new InvalidOperationException(
                        "删除用户模型失败，且回滚也失败：" + rollbackError.Message, rollbackError);
                }
                throw new InvalidOperationException(
                    "删除用户模型失败，模型文件可能正在使用或当前用户没有删除权限：" + cleanupError.Message,
                    cleanupError);
            }
        }
        return record;
    }

    private static bool IsPathInside(string path, string root)
    {
        var normalizedPath = Path.GetFullPath(path)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var normalizedRoot = Path.GetFullPath(root)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        return normalizedPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static void MovePath(string source, string destination)
    {
        if (Directory.Exists(source))
        {
            Directory.Move(source, destination);
            return;
        }
        if (File.Exists(source))
        {
            File.Move(source, destination);
            return;
        }
        throw new FileNotFoundException("待处理的用户模型文件不存在", source);
    }

    private static void DeletePath(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
            return;
        }
        if (File.Exists(path)) File.Delete(path);
    }

    private static string[] AllowedBackends(UserModelRecord record, string architecture)
    {
        var format = record.Format.ToLowerInvariant();
        var task = record.Task.ToLowerInvariant();
        if (format == "onnx") return ["onnx"];
        if (format == "ncnn") return ["ncnn"];
        if (format == "directory")
            return architecture.Equals("FlashVSR", StringComparison.OrdinalIgnoreCase)
                ? ["flashvsr"] : architecture.Equals("BasicVSR++", StringComparison.OrdinalIgnoreCase)
                ? ["basicvsrpp"] : record.Backends;
        if (task == "interpolation")
            return architecture.StartsWith("RIFE", StringComparison.OrdinalIgnoreCase)
                ? ["cuda", "tensorrt"] : ["cuda"];
        if (task == "restoration") return ["cuda"];
        return ["cuda", "tensorrt"];
    }

    internal static string NormalizeRelativePath(string path, string modelsDirectory)
    {
        var value = (path ?? "").Trim().Trim('"');
        if (Path.IsPathRooted(value))
        {
            try { value = Path.GetRelativePath(modelsDirectory, value); }
            catch { }
        }
        return value.Replace('\\', '/').Trim('/');
    }
}

internal sealed class UserModelRecord
{
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string RelativePath { get; set; } = "";
    public string Task { get; set; } = "";
    public string Architecture { get; set; } = "";
    public string Purpose { get; set; } = "";
    public string Format { get; set; } = "";
    public int Scale { get; set; }
    public int InputChannels { get; set; } = 3;
    public int OutputChannels { get; set; } = 3;
    public int InputMultiple { get; set; } = 1;
    public int MinimumSize { get; set; }
    public bool Square { get; set; }
    public bool SupportsHalf { get; set; }
    public bool SupportsBFloat16 { get; set; }
    public string Tiling { get; set; } = "";
    public string[] Backends { get; set; } = [];
    public string Sha256 { get; set; } = "";
    public long Size { get; set; }
    public string ImportedAtUtc { get; set; } = "";
}

internal sealed class ModelImportInspection
{
    public string Path { get; set; } = "";
    public string Format { get; set; } = "";
    public string Task { get; set; } = "";
    public string Architecture { get; set; } = "";
    public string Purpose { get; set; } = "";
    public int Scale { get; set; }
    public int InputChannels { get; set; } = 3;
    public int OutputChannels { get; set; } = 3;
    public bool SupportsHalf { get; set; }
    public bool SupportsBFloat16 { get; set; }
    public int InputMultiple { get; set; } = 1;
    public int MinimumSize { get; set; }
    public bool Square { get; set; }
    public string Tiling { get; set; } = "";
    public string[] Backends { get; set; } = [];
    public string Error { get; set; } = "";
}

internal sealed class ModelImportResult
{
    public bool Success { get; set; }
    public string Source { get; set; } = "";
    public string Id { get; set; } = "";
    public string InstalledPath { get; set; } = "";
    public string Task { get; set; } = "";
    public string Architecture { get; set; } = "";
    public string Purpose { get; set; } = "";
    public int Scale { get; set; }
    public string[] Backends { get; set; } = [];
    public string Error { get; set; } = "";
}

/// <summary>执行用户模型预检与事务式导入，不让失败文件进入正式模型列表。</summary>
internal sealed class ModelImportManager
{
    private readonly string _modelsDirectory;
    private readonly string _pythonExe;
    private readonly string _upscaleInspector;
    private readonly string _interpolationInspector;

    internal ModelImportManager(string modelsDirectory, string pythonExe, string upscaleInspector, string interpolationInspector)
    {
        _modelsDirectory = modelsDirectory;
        _pythonExe = pythonExe;
        _upscaleInspector = upscaleInspector;
        _interpolationInspector = interpolationInspector;
    }

    internal IReadOnlyList<ModelImportResult> Import(string source)
    {
        var fullSource = Path.GetFullPath(source.Trim().Trim('"'));
        if (!File.Exists(fullSource) && !Directory.Exists(fullSource))
            return [new ModelImportResult { Source = fullSource, Error = "导入路径不存在" }];

        var candidates = DiscoverCandidates(fullSource);
        if (candidates.Count == 0)
            return [new ModelImportResult { Source = fullSource, Error = "未找到受支持的模型文件或 NCNN param/bin 目录" }];

        var existing = UserModelCatalog.Load(_modelsDirectory).ToList();
        var results = new List<ModelImportResult>();
        foreach (var candidate in candidates)
        {
            var inspection = Inspect(candidate);
            if (!string.IsNullOrWhiteSpace(inspection.Error))
            {
                results.Add(new ModelImportResult { Source = candidate, Error = inspection.Error });
                continue;
            }
            try
            {
                var hash = ComputeContentHash(candidate);
                var duplicate = existing.FirstOrDefault(item => item.Sha256.Equals(hash, StringComparison.OrdinalIgnoreCase));
                if (duplicate is not null)
                {
                    results.Add(ToResult(duplicate, candidate, true));
                    continue;
                }
                var record = InstallCandidate(candidate, inspection, hash, existing);
                existing.Add(record);
                try
                {
                    UserModelCatalog.Save(_modelsDirectory, existing);
                }
                catch
                {
                    existing.Remove(record);
                    RollbackInstalled(record);
                    throw;
                }
                results.Add(ToResult(record, candidate, true));
            }
            catch (Exception ex)
            {
                results.Add(new ModelImportResult { Source = candidate, Error = ex.Message });
            }
        }
        return results;
    }

    internal ModelImportInspection Inspect(string candidate)
    {
        if (Directory.Exists(candidate))
        {
            if (IsFlashVsrPackage(candidate))
                return new ModelImportInspection
                {
                    Path = candidate, Format = "directory", Task = "upscale", Architecture = "FlashVSR",
                    Purpose = "SR", Scale = 4, Backends = ["flashvsr"]
                };
            if (IsBasicVsrPlusPlusPackage(candidate))
                return new ModelImportInspection
                {
                    Path = candidate, Format = "directory", Task = "restoration", Architecture = "BasicVSR++",
                    Purpose = "Restoration", Scale = 1, Backends = ["basicvsrpp"]
                };
            return InspectNcnn(candidate);
        }
        var extension = Path.GetExtension(candidate).ToLowerInvariant();
        if (extension == ".pkl") return InspectInterpolation(candidate);
        if (extension is ".pth" or ".pt")
        {
            var upscale = InspectUpscale(candidate);
            return string.IsNullOrWhiteSpace(upscale.Error) ? upscale : InspectInterpolation(candidate);
        }
        return InspectUpscale(candidate);
    }

    private ModelImportInspection InspectUpscale(string path)
    {
        if (!File.Exists(_pythonExe)) return new ModelImportInspection { Path = path, Error = "找不到便携 Python" };
        if (!File.Exists(_upscaleInspector)) return new ModelImportInspection { Path = path, Error = "缺少超分模型预检脚本" };
        var process = RunInspectorInstance(_upscaleInspector, path, 300);
        var result = ParseUpscaleInspection(process.Output, path);
        if (string.IsNullOrWhiteSpace(result.Error) && !process.Ok)
            result.Error = string.IsNullOrWhiteSpace(process.Error) ? "超分模型预检失败" : LastLine(process.Error);
        result.Task = result.Purpose.Equals("Restoration", StringComparison.OrdinalIgnoreCase) ? "restoration" : "upscale";
        return result;
    }

    private ModelImportInspection InspectInterpolation(string path)
    {
        if (!File.Exists(_pythonExe)) return new ModelImportInspection { Path = path, Error = "找不到便携 Python" };
        if (!File.Exists(_interpolationInspector)) return new ModelImportInspection { Path = path, Error = "缺少补帧模型预检脚本" };
        var process = RunInspectorInstance(_interpolationInspector, path, 300);
        try
        {
            var line = JsonArrayLine(process.Output);
            if (line is null) throw new InvalidOperationException(LastLine(process.Error));
            using var document = JsonDocument.Parse(line);
            var item = document.RootElement[0];
            var error = item.GetProperty("error").GetString() ?? "";
            var cuda = item.GetProperty("cuda").GetBoolean();
            var tensorRt = item.GetProperty("tensorrt").GetBoolean();
            var baseArchitecture = item.GetProperty("base_architecture").GetString() ?? "";
            return new ModelImportInspection
            {
                Path = path,
                Format = Path.GetExtension(path).TrimStart('.').ToLowerInvariant(),
                Task = "interpolation",
                Architecture = string.IsNullOrWhiteSpace(baseArchitecture)
                    ? item.GetProperty("architecture").GetString() ?? ""
                    : baseArchitecture.ToUpperInvariant(),
                Purpose = "Interpolation",
                Scale = 1,
                Backends = new[] { cuda ? "cuda" : "", tensorRt ? "tensorrt" : "" }
                    .Where(value => value.Length > 0).ToArray(),
                Error = error,
            };
        }
        catch (Exception ex)
        {
            return new ModelImportInspection { Path = path, Error = "无法识别为超分或已支持的补帧模型：" + ex.Message };
        }
    }

    private static ModelImportInspection InspectNcnn(string directory)
    {
        var param = Directory.GetFiles(directory, "*.param", SearchOption.TopDirectoryOnly).FirstOrDefault();
        var bin = Directory.GetFiles(directory, "*.bin", SearchOption.TopDirectoryOnly).FirstOrDefault();
        if (param is null || bin is null)
            return new ModelImportInspection { Path = directory, Error = "NCNN 模型目录必须同时包含 .param 和 .bin" };
        var architecture = DetectArchitectureFromName(Path.GetFileName(directory));
        if (architecture.Equals("RIFE", StringComparison.OrdinalIgnoreCase))
        {
            return new ModelImportInspection
            {
                Path = directory,
                Format = "ncnn",
                Task = "interpolation",
                Architecture = "RIFE",
                Purpose = "Interpolation",
                Scale = 1,
                Backends = ["ncnn"],
            };
        }
        var scale = DetectScaleFromName(Path.GetFileName(directory));
        if (scale <= 0)
            return new ModelImportInspection { Path = directory, Error = "无法确定 NCNN 模型倍率，请在目录名中包含 1x/2x/3x/4x/8x" };
        return new ModelImportInspection
        {
            Path = directory,
            Format = "ncnn",
            Task = scale == 1 ? "restoration" : "upscale",
            Architecture = architecture,
            Purpose = scale == 1 ? "Restoration" : "SR",
            Scale = scale,
            Backends = ["ncnn"],
        };
    }

    private UserModelRecord InstallCandidate(string candidate, ModelImportInspection inspection, string hash, IReadOnlyList<UserModelRecord> existing)
    {
        var taskFolder = inspection.Task switch
        {
            "interpolation" => "Interpolation",
            "restoration" => "Restoration",
            _ => "Upscale",
        };
        var architecture = SafeSegment(inspection.Architecture, "Unknown");
        var modelName = SafeSegment(Directory.Exists(candidate) ? Path.GetFileName(candidate) : Path.GetFileNameWithoutExtension(candidate), "Model");
        var parent = Path.Combine(UserModelCatalog.UserRoot(_modelsDirectory), taskFolder, architecture);
        var destinationName = modelName;
        var candidateRelative = Path.Combine("User", taskFolder, architecture, destinationName,
            Directory.Exists(candidate) ? "" : Path.GetFileName(candidate));
        if (existing.Any(item => item.RelativePath.Equals(candidateRelative.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase))
            || Directory.Exists(Path.Combine(parent, destinationName)))
            destinationName += "-" + hash[..8].ToLowerInvariant();

        var stagingRoot = Path.Combine(UserModelCatalog.UserRoot(_modelsDirectory), ".staging-" + Guid.NewGuid().ToString("N"));
        var stagedModelDirectory = Path.Combine(stagingRoot, destinationName);
        var destinationDirectory = Path.Combine(parent, destinationName);
        try
        {
            Directory.CreateDirectory(stagedModelDirectory);
            string installedModelPath;
            if (Directory.Exists(candidate))
            {
                CopyDirectory(candidate, stagedModelDirectory);
                installedModelPath = destinationDirectory;
            }
            else
            {
                File.Copy(candidate, Path.Combine(stagedModelDirectory, Path.GetFileName(candidate)), overwrite: false);
                installedModelPath = Path.Combine(destinationDirectory, Path.GetFileName(candidate));
            }
            Directory.CreateDirectory(parent);
            Directory.Move(stagedModelDirectory, destinationDirectory);
            var relative = Path.GetRelativePath(_modelsDirectory, installedModelPath).Replace('\\', '/');
            var stableId = File.Exists(installedModelPath) ? Path.ChangeExtension(relative, null) ?? relative : relative;
            return new UserModelRecord
            {
                Id = stableId.Replace('\\', '/'),
                DisplayName = modelName,
                RelativePath = relative,
                Task = inspection.Task,
                Architecture = inspection.Architecture,
                Purpose = inspection.Purpose,
                Format = inspection.Format,
                Scale = inspection.Scale,
                InputChannels = inspection.InputChannels,
                OutputChannels = inspection.OutputChannels,
                InputMultiple = Math.Max(1, inspection.InputMultiple),
                MinimumSize = Math.Max(0, inspection.MinimumSize),
                Square = inspection.Square,
                SupportsHalf = inspection.SupportsHalf,
                SupportsBFloat16 = inspection.SupportsBFloat16,
                Tiling = inspection.Tiling,
                Backends = inspection.Backends,
                Sha256 = hash,
                Size = ContentSize(installedModelPath),
                ImportedAtUtc = DateTime.UtcNow.ToString("O"),
            };
        }
        finally
        {
            if (Directory.Exists(stagingRoot))
            {
                try { Directory.Delete(stagingRoot, recursive: true); }
                catch { }
            }
        }
    }

    private static List<string> DiscoverCandidates(string source)
    {
        if (File.Exists(source))
        {
            if (Path.GetExtension(source).Equals(".param", StringComparison.OrdinalIgnoreCase)
                || Path.GetExtension(source).Equals(".bin", StringComparison.OrdinalIgnoreCase))
            {
                var directory = Path.GetDirectoryName(source)!;
                return Directory.EnumerateFiles(directory, "*.param", SearchOption.TopDirectoryOnly).Any()
                    && Directory.EnumerateFiles(directory, "*.bin", SearchOption.TopDirectoryOnly).Any()
                    ? [directory] : [];
            }
            return IsSupportedFile(source) ? [source] : [];
        }
        var packages = Directory.GetDirectories(source, "*", SearchOption.AllDirectories)
            .Prepend(source)
            .Where(directory => IsFlashVsrPackage(directory) || IsBasicVsrPlusPlusPackage(directory))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var ncnnDirectories = Directory.GetDirectories(source, "*", SearchOption.AllDirectories)
            .Prepend(source)
            .Where(directory => Directory.EnumerateFiles(directory, "*.param", SearchOption.TopDirectoryOnly).Any()
                && Directory.EnumerateFiles(directory, "*.bin", SearchOption.TopDirectoryOnly).Any())
            .Where(directory => !packages.Any(package => IsPathUnder(directory, package)))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var files = Directory.GetFiles(source, "*", SearchOption.AllDirectories)
            .Where(IsSupportedFile)
            .Where(path => !packages.Any(package => IsPathUnder(path, package)))
            .Where(path => !ncnnDirectories.Any(directory => IsPathUnder(path, directory)));
        return packages.Concat(ncnnDirectories).Concat(files).OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static readonly string[] FlashVsrPackageFiles =
    [
        "diffusion_pytorch_model_streaming_dmd.safetensors", "LQ_proj_in.ckpt", "TCDecoder.ckpt", "Wan2.1_VAE.pth"
    ];

    private static bool IsFlashVsrPackage(string directory) => Directory.Exists(directory)
        && FlashVsrPackageFiles.All(file => File.Exists(Path.Combine(directory, file)));

    private static bool IsBasicVsrPlusPlusPackage(string directory) => Directory.Exists(directory)
        && File.Exists(Path.Combine(directory, "config.py"))
        && File.Exists(Path.Combine(directory, "chkpts.pth"));

    private static bool IsSupportedFile(string path) =>
        new[] { ".pth", ".pt", ".pkl", ".ckpt", ".safetensors", ".onnx" }
            .Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    private static bool IsPathUnder(string path, string directory)
    {
        var root = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        return Path.GetFullPath(path).StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }

    private (bool Ok, string Output, string Error) RunInspectorInstance(string script, string model, int timeoutSeconds)
    {
        try
        {
            var start = new ProcessStartInfo
            {
                FileName = _pythonExe,
                WorkingDirectory = Path.GetDirectoryName(script)!,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };
            start.Environment["PYTHONUTF8"] = "1";
            start.Environment["PYTHONIOENCODING"] = "utf-8";
            start.Environment["VIDEOENHANCER_BACKEND_DIR"] = Path.GetDirectoryName(_upscaleInspector)!;
            start.ArgumentList.Add(script);
            start.ArgumentList.Add(model);
            using var process = Process.Start(start) ?? throw new InvalidOperationException("无法启动模型预检进程");
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(timeoutSeconds * 1000))
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                process.WaitForExit();
                return (false, stdout.GetAwaiter().GetResult(), "模型预检超时");
            }
            return (process.ExitCode == 0, stdout.GetAwaiter().GetResult(), stderr.GetAwaiter().GetResult());
        }
        catch (Exception ex)
        {
            return (false, "", ex.Message);
        }
    }

    private static ModelImportInspection ParseUpscaleInspection(string output, string path)
    {
        try
        {
            var line = JsonArrayLine(output) ?? throw new InvalidOperationException("预检器没有返回 JSON");
            using var document = JsonDocument.Parse(line);
            var item = document.RootElement[0];
            return new ModelImportInspection
            {
                Path = path,
                Format = item.GetProperty("format").GetString() ?? "",
                Architecture = item.GetProperty("architecture").GetString() ?? "",
                Purpose = item.GetProperty("purpose").GetString() ?? "",
                Scale = item.GetProperty("scale").GetInt32(),
                InputChannels = item.GetProperty("input_channels").GetInt32(),
                OutputChannels = item.GetProperty("output_channels").GetInt32(),
                SupportsHalf = item.GetProperty("supports_half").GetBoolean(),
                SupportsBFloat16 = item.GetProperty("supports_bfloat16").GetBoolean(),
                InputMultiple = item.GetProperty("input_multiple").GetInt32(),
                MinimumSize = item.GetProperty("minimum_size").GetInt32(),
                Square = item.GetProperty("square").GetBoolean(),
                Tiling = item.GetProperty("tiling").GetString() ?? "",
                Backends = item.GetProperty("backends").EnumerateArray().Select(value => value.GetString() ?? "").ToArray(),
                Error = item.GetProperty("error").GetString() ?? "",
            };
        }
        catch (Exception ex)
        {
            return new ModelImportInspection { Path = path, Error = ex.Message };
        }
    }

    private static string? JsonArrayLine(string output) => output.Replace("\r", "")
        .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .LastOrDefault(line => line.StartsWith("[", StringComparison.Ordinal));

    private static string LastLine(string value) => value.Replace("\r", "")
        .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault() ?? "未知错误";

    private static int DetectScaleFromName(string name)
    {
        var matches = System.Text.RegularExpressions.Regex.Matches(name,
            @"(?i)(?:^|[-_.])(?:x(?<a>\d+)|(?<b>\d+)x)(?=$|[-_.])");
        foreach (System.Text.RegularExpressions.Match match in matches.Cast<System.Text.RegularExpressions.Match>().Reverse())
        {
            if (int.TryParse(match.Groups["a"].Value + match.Groups["b"].Value, out var scale)
                && scale is 1 or 2 or 3 or 4 or 8) return scale;
        }
        return 0;
    }

    private static string DetectArchitectureFromName(string name)
    {
        foreach (var architecture in new[] { "RealESRGAN", "ESRGAN", "SPANPlus", "SPAN", "SwinIR", "RealCUGAN", "RIFE", "GMFSS", "GIMM" })
            if (name.Contains(architecture, StringComparison.OrdinalIgnoreCase)) return architecture;
        return "NCNN";
    }

    private static string SafeSegment(string value, string fallback)
    {
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        var result = new string((value ?? "").Where(character => !invalid.Contains(character)).ToArray()).Trim().Trim('.');
        return string.IsNullOrWhiteSpace(result) ? fallback : result;
    }

    private static string ComputeContentHash(string path)
    {
        if (File.Exists(path))
        {
            using var source = File.OpenRead(path);
            return Convert.ToHexString(SHA256.HashData(source)).ToLowerInvariant();
        }
        using var incremental = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories).OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
        {
            var relative = Path.GetRelativePath(path, file).Replace('\\', '/');
            incremental.AppendData(Encoding.UTF8.GetBytes(relative));
            using var stream = File.OpenRead(file);
            var buffer = new byte[1024 * 1024];
            int read;
            while ((read = stream.Read(buffer, 0, buffer.Length)) > 0) incremental.AppendData(buffer, 0, read);
        }
        return Convert.ToHexString(incremental.GetHashAndReset()).ToLowerInvariant();
    }

    private static long ContentSize(string path) => File.Exists(path)
        ? new FileInfo(path).Length
        : Directory.GetFiles(path, "*", SearchOption.AllDirectories).Sum(file => new FileInfo(file).Length);

    private static void CopyDirectory(string source, string destination)
    {
        foreach (var directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: false);
        }
    }

    private void RollbackInstalled(UserModelRecord record)
    {
        var installed = Path.GetFullPath(Path.Combine(_modelsDirectory, record.RelativePath.Replace('/', Path.DirectorySeparatorChar)));
        var root = File.Exists(installed) ? Path.GetDirectoryName(installed)! : installed;
        var userRoot = Path.GetFullPath(UserModelCatalog.UserRoot(_modelsDirectory))
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (root.StartsWith(userRoot, StringComparison.OrdinalIgnoreCase) && Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }

    private static ModelImportResult ToResult(UserModelRecord record, string source, bool success) => new()
    {
        Success = success,
        Source = source,
        Id = record.Id,
        InstalledPath = record.RelativePath,
        Task = record.Task,
        Architecture = record.Architecture,
        Purpose = record.Purpose,
        Scale = record.Scale,
        Backends = record.Backends,
    };
}
