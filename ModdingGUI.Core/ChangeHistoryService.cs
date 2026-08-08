namespace ModdingGUI.Core;

using System;
using System.Globalization;
using System.IO;

/// <summary>
/// Writes pre-mutation backups under <c>&lt;project&gt;/.mod-history/&lt;relative-path&gt;/&lt;stamp&gt;.bak</c>.
/// Every API write acquires <see cref="ProjectLock"/> and then calls
/// <see cref="Backup"/> exactly once before overwriting the target file.
/// </summary>
/// <remarks>
/// <para>
/// History is scoped per-project and mirrors the relative path of the edited
/// file, so <c>data/config/items.tok</c> edits accumulate under
/// <c>&lt;project&gt;/.mod-history/data/config/items.tok/</c>. The timestamp has
/// millisecond resolution so rapid consecutive writes from the API produce
/// distinct files.
/// </para>
/// </remarks>
public static class ChangeHistoryService
{
    /// <summary>The top-level folder name used for all backups inside a project.</summary>
    public const string RootFolderName = ".mod-history";

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
}
