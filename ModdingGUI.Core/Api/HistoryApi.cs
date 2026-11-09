namespace ModdingGUI.Core.Api;

using System;
using System.Collections.Generic;
using ModdingGUI.Core.History;

/// <summary>
/// <see cref="IHistoryApi"/> over <see cref="ChangeHistoryService"/>, under the project lock like every other write.
/// </summary>
public sealed class HistoryApi : IHistoryApi
{
    /// <inheritdoc />
    public IReadOnlyList<BackupEntry> ListAll(string projectRoot)
    {
        string bec = ProjectRoots.ResolveBecFolder(projectRoot);
        return ChangeHistoryService.ListBackups(bec);
    }

    /// <inheritdoc />
    public IReadOnlyList<(string RelativePath, int Count, DateTime Newest)> ListFiles(string projectRoot)
    {
        string bec = ProjectRoots.ResolveBecFolder(projectRoot);
        return ChangeHistoryService.ListBackedUpFiles(bec);
    }

    /// <inheritdoc />
    public IReadOnlyList<BackupEntry> ListFor(string projectRoot, string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        string bec = ProjectRoots.ResolveBecFolder(projectRoot);
        return ChangeHistoryService.ListBackups(bec, relativePath);
    }

    /// <inheritdoc />
    public BackupEntry Restore(string projectRoot, BackupEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return Restore(projectRoot, entry.BackupPath);
    }

    /// <inheritdoc />
    public BackupEntry Restore(string projectRoot, string backupPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(backupPath);

        string bec = ProjectRoots.ResolveBecFolder(projectRoot);

        using (ProjectLock.Acquire(bec))
        {
            return ChangeHistoryService.RestoreBackup(bec, backupPath);
        }
    }
}
