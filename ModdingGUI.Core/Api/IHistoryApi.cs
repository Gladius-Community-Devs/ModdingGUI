namespace ModdingGUI.Core.Api;

using System;
using System.Collections.Generic;
using ModdingGUI.Core.History;

/// <summary>
/// Browse and restore a project's <c>.mod-history</c>. The undo story for every other Api, including the binary ones
/// (grids), because they all write through <see cref="ChangeHistoryService"/>.
/// </summary>
public interface IHistoryApi
{
    /// <summary>Every backup in the project, newest first.</summary>
    IReadOnlyList<BackupEntry> ListAll(string projectRoot);

    /// <summary>The files that have backups, with counts, newest activity first.</summary>
    IReadOnlyList<(string RelativePath, int Count, DateTime Newest)> ListFiles(string projectRoot);

    /// <summary>The backups of one file, newest first.</summary>
    /// <param name="projectRoot">Project root (the folder holding <c>_BEC</c>).</param>
    /// <param name="relativePath">Project-relative path such as <c>data/units/gladiators.txt</c>.</param>
    IReadOnlyList<BackupEntry> ListFor(string projectRoot, string relativePath);

    /// <summary>
    /// Restore a backup over the live file. The live file is backed up first, so the restore is itself undoable.
    /// </summary>
    BackupEntry Restore(string projectRoot, BackupEntry entry);

    /// <summary>Restore by absolute backup path, for callers that only hold the path.</summary>
    BackupEntry Restore(string projectRoot, string backupPath);
}
