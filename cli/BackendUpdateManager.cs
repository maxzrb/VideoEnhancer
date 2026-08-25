using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace VideoEnhancer;

internal sealed record BackendPatchOperation(
    string Action,
    string Path,
    string OldSha256,
    string NewSha256,
    long Size);

internal sealed record BackendPatchManifest(
    string BaseVersion,
    string TargetVersion,
    bool PythonProbe,
    IReadOnlyList<string> HealthCheckFiles,
    IReadOnlyList<BackendPatchOperation> Operations);

internal sealed record BackendArtifact(string Path, long Size, string Sha256);
internal sealed record BackendPatchEdge(
    string BaseVersion,
    string TargetVersion,
    string Path,
    long Size,
    string Sha256);
internal sealed record BackendSentinel(string Path, string Sha256);
internal sealed record BackendLegacyBaseline(string Version, IReadOnlyList<BackendSentinel> Sentinels);
internal sealed record BackendUpdateChannel(
    string LatestVersion,
    BackendArtifact Full,
    IReadOnlyList<BackendPatchEdge> Patches,
    IReadOnlyList<BackendLegacyBaseline> LegacyBaselines);
internal sealed record BackendUpdateStatus(
    string State,
    string InstalledVersion,
    string LatestVersion,
    string Mode,
    long DownloadSize,
    IReadOnlyList<BackendPatchEdge> PatchRoute,
    BackendArtifact Full);

internal static class BackendUpdateManager
{
    internal const string MarkerFileName = ".videoenhancer-backend.json";
    private const string StateDirectoryName = ".videoenhancer-backend-update";
    private const string PendingFileName = "pending.json";

    internal static string PythonRoot(string coreRoot) => Path.Combine(coreRoot, "python");
    internal static string MarkerPath(string coreRoot) => Path.Combine(PythonRoot(coreRoot), MarkerFileName);
    internal static string StateRoot(string coreRoot) => Path.Combine(coreRoot, StateDirectoryName);

    internal static BackendUpdateChannel ReadChannel(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (!root.TryGetProperty("schemaVersion", out var schema) || schema.GetInt32() != 1)
            throw new InvalidOperationException("不支持的后端更新通道协议版本");
        var latestVersion = RequiredString(root, "latestVersion");
        var fullElement = root.GetProperty("full");
        var full = new BackendArtifact(
            RequiredString(fullElement, "path"),
            fullElement.TryGetProperty("size", out var fullSize) ? fullSize.GetInt64() : 0,
            OptionalString(fullElement, "sha256"));

        var patches = new List<BackendPatchEdge>();
        if (root.TryGetProperty("patches", out var patchElements))
        {
            foreach (var item in patchElements.EnumerateArray())
            {
                patches.Add(new BackendPatchEdge(
                    RequiredString(item, "baseVersion"),
                    RequiredString(item, "targetVersion"),
                    RequiredString(item, "path"),
                    item.TryGetProperty("size", out var size) ? size.GetInt64() : 0,
                    OptionalString(item, "sha256")));
            }
        }

        var baselines = new List<BackendLegacyBaseline>();
        if (root.TryGetProperty("legacyBaselines", out var baselineElements))
        {
            foreach (var baseline in baselineElements.EnumerateArray())
            {
                var sentinels = new List<BackendSentinel>();
                foreach (var sentinel in baseline.GetProperty("sentinels").EnumerateArray())
                {
                    sentinels.Add(new BackendSentinel(
                        NormalizeRelativePath(RequiredString(sentinel, "path")),
                        RequiredString(sentinel, "sha256")));
                }
                if (sentinels.Count == 0)
                    throw new InvalidOperationException("旧版后端基线必须至少包含一个哨兵文件");
                baselines.Add(new BackendLegacyBaseline(RequiredString(baseline, "version"), sentinels));
            }
        }
        return new BackendUpdateChannel(latestVersion, full, patches, baselines);
    }

    internal static BackendUpdateStatus GetStatus(string coreRoot, BackendUpdateChannel channel)
    {
        var pythonRoot = PythonRoot(coreRoot);
        var pythonExe = Path.Combine(pythonRoot, "python", "python.exe");
        var backendScript = Path.Combine(pythonRoot, "backend", "rve-backend.py");
        var hasBackend = File.Exists(pythonExe) && File.Exists(backendScript);
        if (!hasBackend)
            return new BackendUpdateStatus("not-installed", "", channel.LatestVersion, "full",
                channel.Full.Size, Array.Empty<BackendPatchEdge>(), channel.Full);

        var installedVersion = ReadInstalledVersion(coreRoot) ?? DetectLegacyVersion(pythonRoot, channel);
        if (string.IsNullOrWhiteSpace(installedVersion))
            return new BackendUpdateStatus("full-required", "unknown", channel.LatestVersion, "full",
                channel.Full.Size, Array.Empty<BackendPatchEdge>(), channel.Full);
        if (installedVersion.Equals(channel.LatestVersion, StringComparison.OrdinalIgnoreCase))
            return new BackendUpdateStatus("current", installedVersion, channel.LatestVersion, "none",
                0, Array.Empty<BackendPatchEdge>(), channel.Full);

        var route = FindSmallestPatchRoute(installedVersion, channel.LatestVersion, channel.Patches);
        if (route.Count == 0)
            return new BackendUpdateStatus("full-required", installedVersion, channel.LatestVersion, "full",
                channel.Full.Size, route, channel.Full);
        return new BackendUpdateStatus(
            ReadInstalledVersion(coreRoot) is null ? "legacy-update-available" : "update-available",
            installedVersion,
            channel.LatestVersion,
            "patch",
            route.Sum(item => Math.Max(0, item.Size)),
            route,
            channel.Full);
    }

    private static string? DetectLegacyVersion(string pythonRoot, BackendUpdateChannel channel)
    {
        foreach (var baseline in channel.LegacyBaselines)
        {
            var matched = true;
            foreach (var sentinel in baseline.Sentinels)
            {
                var path = SafeCombine(pythonRoot, sentinel.Path);
                if (!File.Exists(path) || !HashMatches(path, sentinel.Sha256))
                {
                    matched = false;
                    break;
                }
            }
            if (matched) return baseline.Version;
        }
        return null;
    }

    private static IReadOnlyList<BackendPatchEdge> FindSmallestPatchRoute(
        string installedVersion,
        string latestVersion,
        IReadOnlyList<BackendPatchEdge> patches)
    {
        var distances = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase)
            { [installedVersion] = 0 };
        var previous = new Dictionary<string, BackendPatchEdge>(StringComparer.OrdinalIgnoreCase);
        var pending = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { installedVersion };
        while (pending.Count > 0)
        {
            var current = pending.OrderBy(version => distances[version]).First();
            pending.Remove(current);
            if (current.Equals(latestVersion, StringComparison.OrdinalIgnoreCase)) break;
            foreach (var edge in patches.Where(item => item.BaseVersion.Equals(current, StringComparison.OrdinalIgnoreCase)))
            {
                var candidate = distances[current] + Math.Max(0, edge.Size);
                if (distances.TryGetValue(edge.TargetVersion, out var known) && known <= candidate) continue;
                distances[edge.TargetVersion] = candidate;
                previous[edge.TargetVersion] = edge;
                pending.Add(edge.TargetVersion);
            }
        }
        if (!previous.ContainsKey(latestVersion)) return Array.Empty<BackendPatchEdge>();
        var route = new List<BackendPatchEdge>();
        var cursor = latestVersion;
        while (!cursor.Equals(installedVersion, StringComparison.OrdinalIgnoreCase))
        {
            if (!previous.TryGetValue(cursor, out var edge)) return Array.Empty<BackendPatchEdge>();
            route.Add(edge);
            cursor = edge.BaseVersion;
        }
        route.Reverse();
        return route;
    }

    internal static string? ReadInstalledVersion(string coreRoot)
    {
        var marker = MarkerPath(coreRoot);
        if (!File.Exists(marker)) return null;
        using var document = JsonDocument.Parse(File.ReadAllText(marker, Encoding.UTF8));
        return document.RootElement.TryGetProperty("version", out var version)
            ? version.GetString()
            : null;
    }

    internal static BackendPatchManifest ReadPatchManifest(string extractedRoot)
    {
        var manifestPath = Path.Combine(extractedRoot, "backend-patch.json");
        if (!File.Exists(manifestPath))
            throw new InvalidOperationException("补丁缺少 backend-patch.json");

        using var document = JsonDocument.Parse(File.ReadAllText(manifestPath, Encoding.UTF8));
        var root = document.RootElement;
        if (!root.TryGetProperty("schemaVersion", out var schema) || schema.GetInt32() != 1)
            throw new InvalidOperationException("不支持的后端补丁协议版本");
        var baseVersion = RequiredString(root, "baseVersion");
        var targetVersion = RequiredString(root, "targetVersion");
        if (baseVersion.Equals(targetVersion, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("补丁的基础版本与目标版本相同");

        var healthChecks = new List<string>();
        if (root.TryGetProperty("healthCheckFiles", out var healthElement))
        {
            foreach (var item in healthElement.EnumerateArray())
                healthChecks.Add(item.GetString() ?? "");
        }

        var operations = new List<BackendPatchOperation>();
        var uniquePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in root.GetProperty("operations").EnumerateArray())
        {
            var operation = new BackendPatchOperation(
                RequiredString(item, "action").ToLowerInvariant(),
                NormalizeRelativePath(RequiredString(item, "path")),
                OptionalString(item, "oldSha256"),
                OptionalString(item, "newSha256"),
                item.TryGetProperty("size", out var size) ? size.GetInt64() : 0);
            if (operation.Action is not ("add" or "replace" or "delete"))
                throw new InvalidOperationException("补丁包含未知操作：" + operation.Action);
            if (!uniquePaths.Add(operation.Path))
                throw new InvalidOperationException("补丁重复操作同一路径：" + operation.Path);
            operations.Add(operation);
        }
        if (operations.Count == 0)
            throw new InvalidOperationException("后端补丁不包含任何文件操作");

        return new BackendPatchManifest(
            baseVersion,
            targetVersion,
            root.TryGetProperty("pythonProbe", out var probe) && probe.GetBoolean(),
            healthChecks,
            operations);
    }

    internal static void RecoverPending(string coreRoot)
    {
        var pendingPath = Path.Combine(StateRoot(coreRoot), PendingFileName);
        if (!File.Exists(pendingPath)) return;
        Console.WriteLine("BACKEND_RECOVERY_START|检测到未完成的后端更新，正在回滚");
        RollbackFromJournal(coreRoot, pendingPath);
        Console.WriteLine("BACKEND_RECOVERY_COMPLETE|后端已恢复到更新前状态");
    }

    internal static string ApplyExtractedPatch(string coreRoot, string extractedRoot)
    {
        RecoverPending(coreRoot);
        var pythonRoot = PythonRoot(coreRoot);
        if (!Directory.Exists(pythonRoot))
            throw new InvalidOperationException("后端目录不存在：" + pythonRoot);
        EnsureBackendNotRunning(pythonRoot);

        var manifest = ReadPatchManifest(extractedRoot);
        var installedVersion = ReadInstalledVersion(coreRoot);
        if (!string.IsNullOrWhiteSpace(installedVersion)
            && !installedVersion.Equals(manifest.BaseVersion, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"补丁基础版本不匹配：当前 {installedVersion}，补丁要求 {manifest.BaseVersion}");
        }

        var operationsToApply = ValidatePatchFiles(pythonRoot, extractedRoot, manifest);
        var stateRoot = StateRoot(coreRoot);
        Directory.CreateDirectory(stateRoot);
        var transactionDirectory = Path.Combine(stateRoot, "transaction-" + Guid.NewGuid().ToString("N"));
        var backupRoot = Path.Combine(transactionDirectory, "backup");
        Directory.CreateDirectory(backupRoot);
        var markerPath = MarkerPath(coreRoot);
        var oldMarker = File.Exists(markerPath) ? File.ReadAllBytes(markerPath) : null;
        var backedUp = new List<string>();
        var added = new List<string>();

        foreach (var operation in operationsToApply)
        {
            var target = SafeCombine(pythonRoot, operation.Path);
            if (File.Exists(target))
            {
                var backup = SafeCombine(backupRoot, operation.Path);
                Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
                File.Copy(target, backup, true);
                backedUp.Add(operation.Path);
            }
            else
            {
                added.Add(operation.Path);
            }
        }

        var pendingPath = Path.Combine(stateRoot, PendingFileName);
        WriteJournal(pendingPath, transactionDirectory, backedUp, added, oldMarker);
        try
        {
            foreach (var operation in operationsToApply)
            {
                var target = SafeCombine(pythonRoot, operation.Path);
                if (operation.Action == "delete")
                {
                    File.Delete(target);
                    continue;
                }

                var payload = SafeCombine(Path.Combine(extractedRoot, "payload"), operation.Path);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                var temporary = target + ".videoenhancer-new-" + Guid.NewGuid().ToString("N");
                try
                {
                    using (var input = File.OpenRead(payload))
                    using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        input.CopyTo(output);
                        output.Flush(true);
                    }
                    File.Move(temporary, target, true);
                }
                finally
                {
                    if (File.Exists(temporary)) File.Delete(temporary);
                }
            }

            ValidateHealth(pythonRoot, manifest);
            WriteMarker(markerPath, manifest.TargetVersion, manifest.BaseVersion);
            File.Delete(pendingPath);
            Directory.Delete(transactionDirectory, true);
            return manifest.TargetVersion;
        }
        catch
        {
            RollbackFromJournal(coreRoot, pendingPath);
            throw;
        }
    }

    internal static void ApplyStagedFullBackend(string coreRoot, string stagedPythonRoot, string targetVersion)
    {
        RecoverPending(coreRoot);
        var pythonRoot = PythonRoot(coreRoot);
        EnsureBackendNotRunning(pythonRoot);
        var stagedRoot = Path.GetFullPath(stagedPythonRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!File.Exists(Path.Combine(stagedRoot, "python", "python.exe"))
            || !File.Exists(Path.Combine(stagedRoot, "backend", "rve-backend.py")))
            throw new InvalidOperationException("完整后端包目录结构无效");
        ProbePython(Path.Combine(stagedRoot, "python", "python.exe"));

        var stateRoot = StateRoot(coreRoot);
        Directory.CreateDirectory(stateRoot);
        var backupDirectory = Path.Combine(stateRoot, "full-backup-" + Guid.NewGuid().ToString("N"));
        var failedDirectory = Path.Combine(stateRoot, "failed-full-" + Guid.NewGuid().ToString("N"));
        var hadOriginal = Directory.Exists(pythonRoot);
        var pendingPath = Path.Combine(stateRoot, PendingFileName);
        WriteJsonAtomically(pendingPath, writer =>
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", 1);
            writer.WriteString("mode", "full");
            writer.WriteBoolean("hadOriginal", hadOriginal);
            writer.WriteString("backupDirectory", backupDirectory);
            writer.WriteString("failedDirectory", failedDirectory);
            writer.WriteEndObject();
        });
        try
        {
            WriteMarker(Path.Combine(stagedRoot, MarkerFileName), targetVersion, ReadInstalledVersion(coreRoot) ?? "unknown");
            if (hadOriginal) Directory.Move(pythonRoot, backupDirectory);
            Directory.Move(stagedRoot, pythonRoot);
            File.Delete(pendingPath);
            if (Directory.Exists(backupDirectory)) Directory.Delete(backupDirectory, true);
        }
        catch
        {
            RollbackFromJournal(coreRoot, pendingPath);
            throw;
        }
    }

    private static IReadOnlyList<BackendPatchOperation> ValidatePatchFiles(
        string pythonRoot,
        string extractedRoot,
        BackendPatchManifest manifest)
    {
        var operationsToApply = new List<BackendPatchOperation>();
        foreach (var operation in manifest.Operations)
        {
            var target = SafeCombine(pythonRoot, operation.Path);
            var exists = File.Exists(target);
            if (operation.Action == "add")
            {
                if (exists)
                {
                    if (HashMatches(target, operation.NewSha256)) continue;
                    throw new InvalidOperationException("新增文件已存在，拒绝覆盖未知内容：" + operation.Path);
                }
            }
            else
            {
                if (!exists)
                {
                    if (operation.Action == "delete") continue;
                    throw new InvalidOperationException("待更新文件不存在：" + operation.Path);
                }
                if (operation.Action == "replace" && HashMatches(target, operation.NewSha256)) continue;
                VerifyHash(target, operation.OldSha256, "旧文件");
            }

            if (operation.Action != "delete")
            {
                var payload = SafeCombine(Path.Combine(extractedRoot, "payload"), operation.Path);
                if (!File.Exists(payload))
                    throw new InvalidOperationException("补丁 payload 缺少文件：" + operation.Path);
                if (operation.Size >= 0 && new FileInfo(payload).Length != operation.Size)
                    throw new InvalidOperationException("补丁文件大小不匹配：" + operation.Path);
                VerifyHash(payload, operation.NewSha256, "补丁文件");
            }
            operationsToApply.Add(operation);
        }
        return operationsToApply;
    }

    private static void ValidateHealth(string pythonRoot, BackendPatchManifest manifest)
    {
        foreach (var relative in manifest.HealthCheckFiles)
        {
            var normalized = NormalizeRelativePath(relative);
            if (!File.Exists(SafeCombine(pythonRoot, normalized)))
                throw new InvalidOperationException("更新后健康检查缺少文件：" + normalized);
        }
        if (manifest.PythonProbe)
            ProbePython(Path.Combine(pythonRoot, "python", "python.exe"));
    }

    private static void ProbePython(string python)
    {
        if (!File.Exists(python)) throw new InvalidOperationException("更新后找不到 Python 解释器");
        var start = new ProcessStartInfo
        {
            FileName = python,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        start.ArgumentList.Add("--version");
        using var process = Process.Start(start) ?? throw new InvalidOperationException("无法启动后端 Python 健康检查");
        if (!process.WaitForExit(15000))
        {
            process.Kill(true);
            throw new InvalidOperationException("后端 Python 健康检查超时");
        }
        if (process.ExitCode != 0)
            throw new InvalidOperationException("后端 Python 健康检查失败，退出码：" + process.ExitCode);
    }

    private static void EnsureBackendNotRunning(string pythonRoot)
    {
        var normalizedRoot = Path.GetFullPath(pythonRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        foreach (var name in new[] { "python", "pythonw" })
        {
            foreach (var process in Process.GetProcessesByName(name))
            {
                using (process)
                {
                    try
                    {
                        var executable = process.MainModule?.FileName;
                        if (executable is not null && Path.GetFullPath(executable).StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
                            throw new InvalidOperationException("后端仍在运行，请先停止全部视频任务后再更新");
                    }
                    catch (InvalidOperationException) { throw; }
                    catch { /* 无权读取其他进程路径时忽略，不能据此误判正在使用本后端。 */ }
                }
            }
        }
    }

    private static void RollbackFromJournal(string coreRoot, string pendingPath)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(pendingPath, Encoding.UTF8));
        var root = document.RootElement;
        if (OptionalString(root, "mode").Equals("full", StringComparison.OrdinalIgnoreCase))
        {
            RollbackFull(coreRoot, pendingPath, root);
            return;
        }
        var transactionDirectory = RequiredString(root, "transactionDirectory");
        var stateRoot = Path.GetFullPath(StateRoot(coreRoot)).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var normalizedTransaction = Path.GetFullPath(transactionDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!normalizedTransaction.StartsWith(stateRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("更新恢复记录包含非法事务目录");

        var pythonRoot = PythonRoot(coreRoot);
        foreach (var item in root.GetProperty("addedPaths").EnumerateArray())
        {
            var target = SafeCombine(pythonRoot, NormalizeRelativePath(item.GetString() ?? ""));
            if (File.Exists(target)) File.Delete(target);
        }
        foreach (var item in root.GetProperty("backedUpPaths").EnumerateArray())
        {
            var relative = NormalizeRelativePath(item.GetString() ?? "");
            var backup = SafeCombine(Path.Combine(transactionDirectory, "backup"), relative);
            var target = SafeCombine(pythonRoot, relative);
            if (!File.Exists(backup))
                throw new InvalidOperationException("更新备份不完整，无法自动回滚：" + relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(backup, target, true);
        }

        var marker = MarkerPath(coreRoot);
        var oldMarker = OptionalString(root, "oldMarkerBase64");
        if (oldMarker.Length == 0)
        {
            if (File.Exists(marker)) File.Delete(marker);
        }
        else
        {
            Directory.CreateDirectory(Path.GetDirectoryName(marker)!);
            File.WriteAllBytes(marker, Convert.FromBase64String(oldMarker));
        }
        File.Delete(pendingPath);
        if (Directory.Exists(transactionDirectory)) Directory.Delete(transactionDirectory, true);
    }

    private static void RollbackFull(string coreRoot, string pendingPath, JsonElement root)
    {
        var stateRoot = Path.GetFullPath(StateRoot(coreRoot)).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var backupDirectory = Path.GetFullPath(RequiredString(root, "backupDirectory"));
        var failedDirectory = Path.GetFullPath(RequiredString(root, "failedDirectory"));
        if (!backupDirectory.StartsWith(stateRoot, StringComparison.OrdinalIgnoreCase)
            || !failedDirectory.StartsWith(stateRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("完整后端恢复记录包含非法目录");
        var pythonRoot = PythonRoot(coreRoot);
        var hadOriginal = root.TryGetProperty("hadOriginal", out var original) && original.GetBoolean();
        if (hadOriginal)
        {
            // 备份尚未出现表示中断发生在第一次目录改名前，当前 python 仍是原目录。
            if (!Directory.Exists(backupDirectory))
            {
                File.Delete(pendingPath);
                return;
            }
            if (Directory.Exists(pythonRoot))
            {
                if (Directory.Exists(failedDirectory))
                    throw new InvalidOperationException("无法保存失败的完整后端目录：" + failedDirectory);
                Directory.Move(pythonRoot, failedDirectory);
            }
            Directory.Move(backupDirectory, pythonRoot);
        }
        else if (Directory.Exists(pythonRoot))
        {
            if (Directory.Exists(failedDirectory))
                throw new InvalidOperationException("无法保存失败的完整后端目录：" + failedDirectory);
            Directory.Move(pythonRoot, failedDirectory);
        }
        File.Delete(pendingPath);
    }

    private static void WriteJournal(
        string path,
        string transactionDirectory,
        IReadOnlyList<string> backedUp,
        IReadOnlyList<string> added,
        byte[]? oldMarker)
    {
        WriteJsonAtomically(path, writer =>
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", 1);
            writer.WriteString("transactionDirectory", transactionDirectory);
            writer.WriteStartArray("backedUpPaths");
            foreach (var item in backedUp) writer.WriteStringValue(item);
            writer.WriteEndArray();
            writer.WriteStartArray("addedPaths");
            foreach (var item in added) writer.WriteStringValue(item);
            writer.WriteEndArray();
            writer.WriteString("oldMarkerBase64", oldMarker is null ? "" : Convert.ToBase64String(oldMarker));
            writer.WriteEndObject();
        });
    }

    private static void WriteMarker(string path, string version, string previousVersion)
    {
        WriteJsonAtomically(path, writer =>
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", 1);
            writer.WriteString("version", version);
            writer.WriteString("previousVersion", previousVersion);
            writer.WriteString("installedAt", DateTimeOffset.Now.ToString("o"));
            writer.WriteEndObject();
        });
    }

    private static void WriteJsonAtomically(string path, Action<Utf8JsonWriter> write)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".new-" + Guid.NewGuid().ToString("N");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
            {
                write(writer);
                writer.Flush();
                stream.Flush(true);
            }
            File.Move(temporary, path, true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private static void VerifyHash(string path, string expected, string label)
    {
        if (string.IsNullOrWhiteSpace(expected))
            throw new InvalidOperationException(label + "缺少 SHA256：" + path);
        using var stream = File.OpenRead(path);
        var actual = Convert.ToHexString(SHA256.HashData(stream));
        if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(label + " SHA256 不匹配：" + path);
    }

    internal static void VerifyArtifact(string path, long expectedSize, string expectedSha256)
    {
        if (expectedSize > 0 && new FileInfo(path).Length != expectedSize)
            throw new InvalidOperationException("更新包大小校验失败：" + path);
        if (!string.IsNullOrWhiteSpace(expectedSha256) && !HashMatches(path, expectedSha256))
            throw new InvalidOperationException("更新包 SHA256 校验失败：" + path);
    }

    private static bool HashMatches(string path, string expected)
    {
        if (string.IsNullOrWhiteSpace(expected)) return false;
        using var stream = File.OpenRead(path);
        var actual = Convert.ToHexString(SHA256.HashData(stream));
        return actual.Equals(expected, StringComparison.OrdinalIgnoreCase);
    }

    internal static string NormalizeRelativePath(string value)
    {
        var normalized = value.Replace('\\', '/').Trim('/');
        if (normalized.Length == 0 || Path.IsPathRooted(value))
            throw new InvalidOperationException("补丁包含非法空路径或绝对路径");
        var segments = normalized.Split('/');
        if (segments.Any(segment => segment.Length == 0 || segment is "." or ".." || segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0))
            throw new InvalidOperationException("补丁包含非法相对路径：" + value);
        return string.Join('/', segments);
    }

    internal static string SafeCombine(string root, string relative)
    {
        var normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var combined = Path.GetFullPath(Path.Combine(normalizedRoot, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!combined.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("路径超出允许目录：" + relative);
        return combined;
    }

    private static string RequiredString(JsonElement element, string name)
    {
        var value = OptionalString(element, name);
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException("JSON 字段不能为空：" + name);
        return value;
    }

    private static string OptionalString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) ? value.GetString() ?? "" : "";
}
