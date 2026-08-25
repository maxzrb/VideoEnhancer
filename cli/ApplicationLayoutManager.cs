using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VideoEnhancer;

/// <summary>
/// 管理 3FUI 插件布局：DLL 留在 Plugin 根目录，EXE、后端、模型和工具位于 Plugin\videoenhancer。
/// 旧平铺布局迁移使用持久日志；普通异常立即回滚，进程中断后下次安装或更新先恢复。
/// </summary>
internal static partial class ApplicationLayoutManager
{
    internal const string ApplicationDirectoryName = "videoenhancer";
    internal const string ExecutableName = "videoenhancer.exe";
    internal const string PluginDllName = "videoenhancer.3fui.dll";
    internal const string JournalFileName = ".videoenhancer-layout-pending.json";

    private static readonly string[] ManagedDirectories =
    {
        "bin",
        "models",
        "python",
        ".videoenhancer-backend-update"
    };

    private sealed class LayoutJournal
    {
        public int SchemaVersion { get; set; } = 1;
        public string PluginRoot { get; set; } = "";
        public string ApplicationRoot { get; set; } = "";
        public string WorkRoot { get; set; } = "";
        public bool HadCanonicalExe { get; set; }
        public bool HadLegacyExe { get; set; }
        public bool HadPluginDll { get; set; }
        public bool ReplaceCanonicalExe { get; set; }
        public bool RemoveLegacyExe { get; set; }
        public List<LayoutMove> Moves { get; set; } = new();
    }

    private sealed class LayoutMove
    {
        public string Source { get; set; } = "";
        public string Target { get; set; } = "";
        public bool IsDirectory { get; set; }
        public bool Completed { get; set; }
    }

    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(LayoutJournal))]
    private sealed partial class LayoutJsonContext : JsonSerializerContext
    {
    }

    internal static string ApplicationRoot(string pluginRoot) =>
        Path.Combine(NormalizePluginRoot(pluginRoot), ApplicationDirectoryName);

    internal static string ExecutablePath(string pluginRoot) =>
        Path.Combine(ApplicationRoot(pluginRoot), ExecutableName);

    internal static string PluginDllPath(string pluginRoot) =>
        Path.Combine(NormalizePluginRoot(pluginRoot), PluginDllName);

    internal static void RecoverPending(string pluginRoot)
    {
        var root = NormalizePluginRoot(pluginRoot);
        var journalPath = Path.Combine(root, JournalFileName);
        if (!File.Exists(journalPath)) return;

        LayoutJournal journal;
        try
        {
            journal = JsonSerializer.Deserialize(
                    File.ReadAllText(journalPath, Encoding.UTF8),
                    LayoutJsonContext.Default.LayoutJournal)
                ?? throw new InvalidDataException("布局迁移日志为空");
        }
        catch (Exception ex)
        {
            throw new InvalidDataException("无法读取上次未完成的布局迁移日志：" + journalPath, ex);
        }
        if (journal.SchemaVersion != 1 ||
            !Path.GetFullPath(journal.PluginRoot).Equals(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("布局迁移日志与当前 Plugin 目录不匹配：" + journalPath);
        }

        Console.WriteLine("检测到未完成的目录迁移，正在恢复旧布局...");
        Rollback(journal, journalPath);
        Console.WriteLine("旧布局恢复完成，可以重新安装或更新。");
    }

    internal static string Install(
        string pluginRoot,
        string stagedExe,
        string stagedPluginDll,
        bool replaceCanonicalExe,
        bool removeLegacyExe)
    {
        var root = NormalizePluginRoot(pluginRoot);
        var appRoot = ApplicationRoot(root);
        var canonicalExe = ExecutablePath(root);
        var legacyExe = Path.Combine(root, ExecutableName);
        var pluginDll = PluginDllPath(root);
        var journalPath = Path.Combine(root, JournalFileName);

        Directory.CreateDirectory(root);
        RecoverPending(root);
        if (!File.Exists(stagedExe)) throw new FileNotFoundException("缺少暂存 EXE", stagedExe);
        if (!File.Exists(stagedPluginDll)) throw new FileNotFoundException("缺少暂存插件 DLL", stagedPluginDll);

        var moves = BuildMoves(root, appRoot);
        ValidateMoveConflicts(moves);
        if (replaceCanonicalExe) WaitForExclusiveAccess(canonicalExe, TimeSpan.FromSeconds(10));
        WaitForExclusiveAccess(pluginDll, TimeSpan.FromSeconds(10));
        if (removeLegacyExe) WaitForExclusiveAccess(legacyExe, TimeSpan.FromSeconds(10));

        var workRoot = Path.Combine(root, ".videoenhancer-layout-transaction-" + Guid.NewGuid().ToString("N"));
        var backupRoot = Path.Combine(workRoot, "backup");
        Directory.CreateDirectory(backupRoot);
        var journal = new LayoutJournal
        {
            PluginRoot = root,
            ApplicationRoot = appRoot,
            WorkRoot = workRoot,
            HadCanonicalExe = File.Exists(canonicalExe),
            HadLegacyExe = File.Exists(legacyExe),
            HadPluginDll = File.Exists(pluginDll),
            ReplaceCanonicalExe = replaceCanonicalExe,
            RemoveLegacyExe = removeLegacyExe,
            Moves = moves
        };

        try
        {
            BackupIfPresent(canonicalExe, Path.Combine(backupRoot, "canonical-videoenhancer.exe"));
            BackupIfPresent(legacyExe, Path.Combine(backupRoot, "legacy-videoenhancer.exe"));
            BackupIfPresent(pluginDll, Path.Combine(backupRoot, PluginDllName));
            WriteJournal(journalPath, journal);

            Directory.CreateDirectory(appRoot);
            var completedMoves = 0;
            foreach (var move in journal.Moves)
            {
                // 先持久化迁移动作，再执行同卷重命名；即使在两步之间中断，恢复也可安全判断源/目标状态。
                move.Completed = true;
                WriteJournal(journalPath, journal);
                if (move.IsDirectory)
                    Directory.Move(move.Source, move.Target);
                else
                    File.Move(move.Source, move.Target);
                completedMoves++;
                RunMigrationTestHook(completedMoves);
            }

            if (replaceCanonicalExe)
                CopyWithSharingRetry(stagedExe, canonicalExe, TimeSpan.FromSeconds(10));
            CopyWithSharingRetry(stagedPluginDll, pluginDll, TimeSpan.FromSeconds(10));
            if (removeLegacyExe && File.Exists(legacyExe)) File.Delete(legacyExe);

            VerifySameFile(stagedExe, canonicalExe, "安装后的 videoenhancer.exe 校验失败");
            VerifySameFile(stagedPluginDll, pluginDll, "安装后的 videoenhancer.3fui.dll 校验失败");
            RewriteMigratedIni(appRoot, root);

            File.Delete(journalPath);
            TryDeleteDirectory(workRoot);
            return canonicalExe;
        }
        catch
        {
            Rollback(journal, journalPath);
            throw;
        }
    }

    private static List<LayoutMove> BuildMoves(string pluginRoot, string appRoot)
    {
        var moves = new List<LayoutMove>();
        foreach (var name in ManagedDirectories)
        {
            var source = Path.Combine(pluginRoot, name);
            if (Directory.Exists(source))
            {
                moves.Add(new LayoutMove
                {
                    Source = source,
                    Target = Path.Combine(appRoot, name),
                    IsDirectory = true
                });
            }
        }

        foreach (var name in new[] { "videoenhancer.ini", "videoenhancer-layout.json", "ffmpeg_log.txt" })
        {
            var source = Path.Combine(pluginRoot, name);
            if (File.Exists(source))
            {
                moves.Add(new LayoutMove
                {
                    Source = source,
                    Target = Path.Combine(appRoot, name),
                    IsDirectory = false
                });
            }
        }
        foreach (var source in Directory.EnumerateFiles(pluginRoot, "python_*.7z", SearchOption.TopDirectoryOnly))
        {
            moves.Add(new LayoutMove
            {
                Source = source,
                Target = Path.Combine(appRoot, Path.GetFileName(source)),
                IsDirectory = false
            });
        }
        return moves;
    }

    private static void ValidateMoveConflicts(IEnumerable<LayoutMove> moves)
    {
        var conflicts = moves.Where(move => move.IsDirectory
                ? Directory.Exists(move.Target) || File.Exists(move.Target)
                : File.Exists(move.Target) || Directory.Exists(move.Target))
            .Select(move => move.Target)
            .ToArray();
        if (conflicts.Length > 0)
        {
            throw new IOException("新旧布局同时存在同名内容，未执行迁移：" + string.Join("；", conflicts));
        }
    }

    private static void Rollback(LayoutJournal journal, string journalPath)
    {
        var backupRoot = Path.Combine(journal.WorkRoot, "backup");
        var canonicalExe = Path.Combine(journal.ApplicationRoot, ExecutableName);
        var legacyExe = Path.Combine(journal.PluginRoot, ExecutableName);
        var pluginDll = Path.Combine(journal.PluginRoot, PluginDllName);
        var errors = new List<string>();

        RestoreFile(canonicalExe, Path.Combine(backupRoot, "canonical-videoenhancer.exe"),
            journal.HadCanonicalExe, errors);
        RestoreFile(legacyExe, Path.Combine(backupRoot, "legacy-videoenhancer.exe"),
            journal.HadLegacyExe, errors);
        RestoreFile(pluginDll, Path.Combine(backupRoot, PluginDllName),
            journal.HadPluginDll, errors);

        foreach (var move in journal.Moves.AsEnumerable().Reverse())
        {
            if (!move.Completed) continue;
            try
            {
                if (move.IsDirectory)
                {
                    if (Directory.Exists(move.Target) && !Directory.Exists(move.Source))
                        Directory.Move(move.Target, move.Source);
                }
                else if (File.Exists(move.Target) && !File.Exists(move.Source))
                {
                    File.Move(move.Target, move.Source);
                }
            }
            catch (Exception ex)
            {
                errors.Add(Path.GetFileName(move.Source) + "：" + ex.Message);
            }
        }

        if (errors.Count > 0)
        {
            throw new IOException("布局迁移回滚不完整：" + string.Join("；", errors));
        }
        TryDeleteDirectory(journal.ApplicationRoot);
        TryDeleteDirectory(journal.WorkRoot);
        try { if (File.Exists(journalPath)) File.Delete(journalPath); } catch { }
    }

    private static void RestoreFile(string target, string backup, bool hadOriginal, List<string> errors)
    {
        try
        {
            if (hadOriginal)
            {
                if (!File.Exists(backup)) throw new FileNotFoundException("事务备份不存在", backup);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(backup, target, true);
            }
            else if (File.Exists(target))
            {
                File.Delete(target);
            }
        }
        catch (Exception ex)
        {
            errors.Add(Path.GetFileName(target) + "：" + ex.Message);
        }
    }

    private static void RewriteMigratedIni(string appRoot, string oldRoot)
    {
        var iniPath = Path.Combine(appRoot, "videoenhancer.ini");
        if (!File.Exists(iniPath)) return;
        var lines = File.ReadAllLines(iniPath, Encoding.UTF8);
        var changed = false;
        for (var index = 0; index < lines.Length; index++)
        {
            var trimmed = lines[index].Trim();
            if (!trimmed.StartsWith("core-path", StringComparison.OrdinalIgnoreCase)) continue;
            var equals = trimmed.IndexOf('=');
            if (equals < 0) continue;
            var value = trimmed[(equals + 1)..].Trim().Trim('"');
            if (value.Length == 0 || Path.IsPathRooted(value)) continue;
            lines[index] = "core-path=\"" + Path.GetFullPath(Path.Combine(oldRoot, value)) + "\"";
            changed = true;
            break;
        }
        if (changed) File.WriteAllLines(iniPath, lines, new UTF8Encoding(false));
    }

    private static void RunMigrationTestHook(int completedMoves)
    {
        if (int.TryParse(Environment.GetEnvironmentVariable("VIDEOENHANCER_TEST_LAYOUT_FAIL_AFTER_MOVE"),
                out var failAfter) && failAfter == completedMoves)
        {
            throw new IOException("测试注入：布局迁移在第 " + completedMoves + " 项后失败");
        }
        if (!int.TryParse(Environment.GetEnvironmentVariable("VIDEOENHANCER_TEST_LAYOUT_PAUSE_AFTER_MOVE"),
                out var pauseAfter) || pauseAfter != completedMoves) return;
        var readyPath = Environment.GetEnvironmentVariable("VIDEOENHANCER_TEST_LAYOUT_READY_FILE")?.Trim();
        if (!string.IsNullOrWhiteSpace(readyPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(readyPath))!);
            File.WriteAllText(readyPath, completedMoves.ToString(), new UTF8Encoding(false));
        }
        Thread.Sleep(Timeout.Infinite);
    }

    private static void BackupIfPresent(string source, string backup)
    {
        if (!File.Exists(source)) return;
        Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
        File.Copy(source, backup, true);
    }

    private static void VerifySameFile(string expected, string actual, string message)
    {
        using var expectedStream = File.OpenRead(expected);
        using var actualStream = File.OpenRead(actual);
        var expectedHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(expectedStream));
        var actualHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(actualStream));
        if (!expectedHash.Equals(actualHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException(message);
    }

    private static void WaitForExclusiveAccess(string path, TimeSpan timeout)
    {
        if (!File.Exists(path)) return;
        var deadline = DateTime.UtcNow + timeout;
        while (true)
        {
            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
                return;
            }
            catch (IOException ex) when (IsSharingViolation(ex) && DateTime.UtcNow < deadline)
            {
                Thread.Sleep(250);
            }
            catch (IOException ex) when (IsSharingViolation(ex))
            {
                throw new IOException("文件持续被占用，请关闭 3FUI 和视频任务后重试：" + path, ex);
            }
        }
    }

    private static void CopyWithSharingRetry(string source, string target, TimeSpan timeout)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        var deadline = DateTime.UtcNow + timeout;
        while (true)
        {
            try
            {
                File.Copy(source, target, true);
                return;
            }
            catch (IOException ex) when (IsSharingViolation(ex) && DateTime.UtcNow < deadline)
            {
                Thread.Sleep(250);
            }
            catch (IOException ex) when (IsSharingViolation(ex))
            {
                throw new IOException("文件持续被占用，无法完成复制：" + source + " -> " + target, ex);
            }
        }
    }

    private static bool IsSharingViolation(IOException ex)
    {
        var errorCode = ex.HResult & 0xFFFF;
        return errorCode is 32 or 33;
    }

    private static void WriteJournal(string path, LayoutJournal journal)
    {
        var temporary = path + ".new";
        File.WriteAllText(temporary,
            JsonSerializer.Serialize(journal, LayoutJsonContext.Default.LayoutJournal),
            new UTF8Encoding(false));
        File.Move(temporary, path, true);
    }

    private static string NormalizePluginRoot(string path) =>
        Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path) && !Directory.EnumerateFileSystemEntries(path).Any())
                Directory.Delete(path);
            else if (Directory.Exists(path) && Path.GetFileName(path).StartsWith(".videoenhancer-layout-transaction-", StringComparison.Ordinal))
                Directory.Delete(path, true);
        }
        catch
        {
        }
    }
}
