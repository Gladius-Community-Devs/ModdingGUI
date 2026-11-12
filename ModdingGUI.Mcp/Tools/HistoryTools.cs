namespace ModdingGUI.Mcp.Tools;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using ModelContextProtocol.Server;
using ModdingGUI.Core.History;
using ModdingGUI.Mcp.Server;

/// <summary>
/// <c>&lt;project&gt;/.mod-history</c>: every backup every API has written, and the undo for all of them.
/// </summary>
/// <remarks>
/// Backups are per file, mirroring the project-relative path, one per write, so restoring is exact. A restore
/// backs the live file up first, which makes restoring itself undoable — the reason this is safe to expose to an
/// agent.
/// </remarks>
[McpServerToolType]
public sealed class HistoryTools(McpProject project)
{
    /// <summary>Every backup.</summary>
    [McpServerTool(Name = "history_list_all", ReadOnly = true, Idempotent = true)]
    [Description(
        "List every backup in the project, newest first: timestamp, project-relative path, absolute backup path " +
        "and size. This is the undo stack for every API, binary grids included.")]
    public object ListAll() =>
        ToolResult.Call(project, "history_list_all", () =>
            project.History.ListAll(project.BecPath).Select(HistoryEntry.From).ToArray());

    /// <summary>Which files have backups.</summary>
    [McpServerTool(Name = "history_list_files", ReadOnly = true, Idempotent = true)]
    [Description("List the files that have at least one backup, with how many and how recently, newest activity first.")]
    public object ListFiles() =>
        ToolResult.Call(project, "history_list_files", () =>
            project.History.ListFiles(project.BecPath)
                .Select(item => new HistoryFile(item.RelativePath, item.Count, item.Newest))
                .ToArray());

    /// <summary>One file's backups.</summary>
    [McpServerTool(Name = "history_list_for", ReadOnly = true, Idempotent = true)]
    [Description("List one file's backups, newest first. relativePath is project-relative, e.g. data/units/gladiators.txt.")]
    public object ListFor(
        [Description("Project-relative path of the file whose history to read.")] string relativePath) =>
        ToolResult.Call(project, $"history_list_for '{relativePath}'", () =>
            project.History.ListFor(project.BecPath, relativePath).Select(HistoryEntry.From).ToArray());

    /// <summary>Restore a backup over the live file.</summary>
    [McpServerTool(Name = "history_restore")]
    [Description(
        "Restore a backup over the live file and return the restored entry. Pass the backupPath that " +
        "history_list_all / history_list_for returned. The live file is backed up first, so the restore is itself " +
        "undoable; a wrong restore is one history_restore away from being reversed.")]
    public object Restore(
        [Description("Absolute path of the .bak file to restore.")] string backupPath) =>
        ToolResult.Call(project, $"history_restore '{backupPath}'", () =>
            HistoryEntry.From(project.History.Restore(project.BecPath, backupPath)));

    /// <summary>Restore a backup by entry.</summary>
    [McpServerTool(Name = "history_restore_entry")]
    [Description(
        "Restore a backup given its full entry (timestamp, relativePath, backupPath, size) rather than just the " +
        "path — the form history_list_all returns. Same undoable semantics as history_restore.")]
    public object RestoreEntry(
        [Description("The backup entry to restore.")] BackupEntry entry) =>
        ToolResult.Call(project, "history_restore_entry", () =>
            HistoryEntry.From(project.History.Restore(project.BecPath, entry)));
}

/// <summary>One backup, as the history tools answer.</summary>
/// <param name="Timestamp">When the backup was taken.</param>
/// <param name="RelativePath">Project-relative path of the file that was backed up.</param>
/// <param name="BackupPath">Absolute path of the .bak file, the value <c>history_restore</c> takes.</param>
/// <param name="Size">Size in bytes.</param>
public sealed record HistoryEntry(string Timestamp, string RelativePath, string BackupPath, long Size)
{
    /// <summary>Project a Core entry onto the wire shape.</summary>
    public static HistoryEntry From(BackupEntry entry) => new(
        entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
        entry.RelativePath,
        entry.BackupPath,
        entry.Size);
}

/// <summary>One file's backup summary.</summary>
/// <param name="RelativePath">Project-relative path.</param>
/// <param name="Count">How many backups it has.</param>
/// <param name="Newest">When the most recent one was taken.</param>
public sealed record HistoryFile(string RelativePath, int Count, DateTime Newest);
