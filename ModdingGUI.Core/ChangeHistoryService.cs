namespace ModdingGUI.Core;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ModdingGUI.Core.History;

/// <summary>
/// Writes pre-mutation backups under <c>&lt;project&gt;/.mod-history/&lt;relative-path&gt;/&lt;stamp&gt;.bak</c>,
/// and browses or restores them.
/// </summary>
/// <remarks>
/// <para>
/// History is scoped per-project and mirrors the relative path of the edited
/// file, so <c>data/config/items.tok</c> edits accumulate under
/// <c>&lt;project&gt;/.mod-history/data/config/items.tok/</c>. The timestamp has
/// millisecond resolution so rapid consecutive writes from the API produce
/// distinct files.
/// </para>
/// <para>
/// Restoring is itself undoable: the live file is backed up before it is replaced, so restoring the wrong backup is
/// recoverable through the same mechanism.
/// </para>
/// </remarks>
public static class ChangeHistoryService
{
    /// <summary>The top-level folder name used for all backups inside a project.</summary>
    public const string RootFolderName = ".mod-history";

    /// <summary>
    /// The randomizer's pinned baseline lives inside <c>.mod-history</c> but is a snapshot, not a history of edits, so
    /// browsing skips it.
    /// </summary>
    private const string BaselineFolderName = "_randomizer-baseline";

    /// <summary>
    /// Copy <paramref name="filePath"/> into the history folder for
    /// <paramref name="projectPath"/>. If the target file does not exist
    /// (e.g. first-time write) no backup is written and the method returns null.
    /// </summary>
    /// <param name="projectPath">Root of the project the file belongs to.</param>
    /// <param name="filePath">Fully qualified path of the file about to be overwritten.</param>
    /// <returns>The absolute path of the backup file, or null if none was created.</returns>
    public static string? Backup(string projectPath, string filePath)
    {
        if (string.IsNullOrWhiteSpace(projectPath)) throw new ArgumentException("Project path is required.", nameof(projectPath));
        if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("File path is required.", nameof(filePath));
        if (!File.Exists(filePath)) return null;

        string projectFull = Path.GetFullPath(projectPath);
        string fileFull = Path.GetFullPath(filePath);

        string relative = Path.GetRelativePath(projectFull, fileFull);
        if (relative.StartsWith("..", StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"File '{fileFull}' is not inside project '{projectFull}'.");

        string historyDir = Path.Combine(projectFull, RootFolderName, relative);
        Directory.CreateDirectory(historyDir);

        string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture);
        string dest = Path.Combine(historyDir, stamp + ".bak");
        // Extremely unlikely collision; add a counter just in case.
        int n = 1;
        while (File.Exists(dest))
        {
            dest = Path.Combine(historyDir, $"{stamp}-{n:D2}.bak");
            n++;
        }

        File.Copy(fileFull, dest, overwrite: false);
        return dest;
    }

    /// <summary>
    /// Every backup in a project, newest first.
    /// </summary>
    /// <param name="projectPath">Root of the project (the folder holding <c>.mod-history</c>).</param>
    /// <param name="relativePathFilter">Optional project-relative path; only that file's backups are returned.</param>
    public static IReadOnlyList<BackupEntry> ListBackups(string projectPath, string? relativePathFilter = null)
    {
        if (string.IsNullOrWhiteSpace(projectPath)) throw new ArgumentException("Project path is required.", nameof(projectPath));

        string projectFull = Path.GetFullPath(projectPath);
        string historyRoot = Path.Combine(projectFull, RootFolderName);
        if (!Directory.Exists(historyRoot)) return Array.Empty<BackupEntry>();

        string? filter = string.IsNullOrWhiteSpace(relativePathFilter) ? null : Normalise(relativePathFilter!);
        var entries = new List<BackupEntry>();

        string[] files;
        try
        {
            files = Directory.EnumerateFiles(historyRoot, "*.bak", SearchOption.AllDirectories).ToArray();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A locked or permission-denied history folder should not break the browser; report what is readable.
            return Array.Empty<BackupEntry>();
        }

        foreach (string file in files)
        {
            string historyRelative = Normalise(Path.GetRelativePath(historyRoot, file));

            // <relative path>/<stamp>.bak - the backup's subject is the folder that holds it.
            int slash = historyRelative.LastIndexOf('/');
            if (slash <= 0) continue;

            string subject = historyRelative[..slash];
            if (subject.Equals(BaselineFolderName, StringComparison.OrdinalIgnoreCase)
                || subject.StartsWith(BaselineFolderName + "/", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (filter is not null && !subject.Equals(filter, StringComparison.OrdinalIgnoreCase)) continue;

            long size;
            try
            {
                size = new FileInfo(file).Length;
            }
            catch (IOException)
            {
                continue;
            }

            entries.Add(new BackupEntry(
                ParseTimestamp(Path.GetFileNameWithoutExtension(file)), subject, file, size));
        }

        return entries
            .OrderByDescending(entry => entry.Timestamp)
            .ThenBy(entry => entry.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>The distinct files that have at least one backup, with counts, newest activity first.</summary>
    public static IReadOnlyList<(string RelativePath, int Count, DateTime Newest)> ListBackedUpFiles(string projectPath)
    {
        return ListBackups(projectPath)
            .GroupBy(entry => entry.RelativePath, StringComparer.OrdinalIgnoreCase)
            .Select(group => (RelativePath: group.Key, Count: group.Count(), Newest: group.Max(entry => entry.Timestamp)))
            .OrderByDescending(item => item.Newest)
            .ToArray();
    }

    /// <summary>
    /// Put a backup back over the live file. The current file is backed up first, so the restore is undoable.
    /// </summary>
    /// <param name="projectPath">Root of the project.</param>
    /// <param name="backupPath">The backup to restore, as returned by <see cref="ListBackups"/>.</param>
    /// <returns>The backup that was restored.</returns>
    /// <exception cref="InvalidOperationException">The backup is missing or outside this project's history.</exception>
    public static BackupEntry RestoreBackup(string projectPath, string backupPath)
    {
        if (string.IsNullOrWhiteSpace(projectPath)) throw new ArgumentException("Project path is required.", nameof(projectPath));
        if (string.IsNullOrWhiteSpace(backupPath)) throw new ArgumentException("Backup path is required.", nameof(backupPath));

        string projectFull = Path.GetFullPath(projectPath);
        string historyRoot = Path.GetFullPath(Path.Combine(projectFull, RootFolderName));
        string backupFull = Path.GetFullPath(backupPath);

        if (!File.Exists(backupFull))
            throw new InvalidOperationException($"Backup '{backupFull}' does not exist.");

        if (!backupFull.StartsWith(historyRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"'{backupFull}' is not inside this project's {RootFolderName}.");

        string historyRelative = Normalise(Path.GetRelativePath(historyRoot, backupFull));
        int slash = historyRelative.LastIndexOf('/');
        if (slash <= 0)
            throw new InvalidOperationException($"'{backupFull}' is not a per-file backup.");

        string subject = historyRelative[..slash];
        if (subject.Equals(BaselineFolderName, StringComparison.OrdinalIgnoreCase)
            || subject.StartsWith(BaselineFolderName + "/", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The randomizer baseline is not a per-file backup; use Reset to Vanilla.");
        }

        string target = Path.Combine(projectFull, subject.Replace('/', Path.DirectorySeparatorChar));

        // Keep the pre-restore state: restoring the wrong backup must be recoverable.
        Backup(projectFull, target);

        string? folder = Path.GetDirectoryName(target);
        if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);

        File.Copy(backupFull, target, overwrite: true);

        return new BackupEntry(ParseTimestamp(Path.GetFileNameWithoutExtension(backupFull)), subject, backupFull,
            new FileInfo(backupFull).Length);
    }

    /// <summary>Parse <c>yyyyMMdd-HHmmss-fff</c>, with an optional <c>-NN</c> collision suffix.</summary>
    private static DateTime ParseTimestamp(string stamp)
    {
        string value = stamp;

        int lastDash = value.LastIndexOf('-');
        if (lastDash > 0 && value.Length - lastDash == 3 && value[(lastDash + 1)..].All(char.IsDigit))
        {
            value = value[..lastDash];
        }

        return DateTime.TryParseExact(value, "yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out DateTime parsed)
            ? parsed
            : DateTime.MinValue;
    }

    private static string Normalise(string path) => path.Replace('\\', '/').Trim('/');
}
