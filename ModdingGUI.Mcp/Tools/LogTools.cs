namespace ModdingGUI.Mcp.Tools;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using ModelContextProtocol.Server;
using ModdingGUI.Core.Logging;
using ModdingGUI.Mcp.Server;

/// <summary>
/// The application log as tools. The same stream the GUI renders: pack progress, validation notes, API
/// warnings and tool refusals all land here, so an agent can read what happened instead of guessing.
/// </summary>
[McpServerToolType]
public sealed class LogTools(McpProject project)
{
    /// <summary>Read the tail of the log.</summary>
    [McpServerTool(Name = "logs_tail", ReadOnly = true, Idempotent = true)]
    [Description(
        "Read the tail of the application log — the same lines the GUI shows: pack/unpack progress, validation " +
        "notes, API warnings and tool refusals. Returns oldest first. Use it after a write or a job to see what " +
        "the Core APIs reported.")]
    public IReadOnlyList<LogLine> Tail(
        [Description("How many of the most recent lines to return. Default 100; clamped to 1..2000.")]
        int count = 100,
        [Description("Minimum level to include: debug, info, success, warning or error. Omit for everything.")]
        string? level = null)
    {
        LogLevel? minimum = ParseLevel(level);
        int take = Math.Clamp(count, 1, 2000);

        // Tail() returns the newest `take` lines; filtering afterwards keeps the semantics "the last N lines
        // at or above this level" honest for a log whose tail is mostly info.
        IReadOnlyList<LogEntry> entries = project.Log.Tail(take);
        return entries
            .Where(entry => LogLine.AtLeast(entry, minimum))
            .Select(LogLine.From)
            .ToArray();
    }

    /// <summary>Drop the in-memory log.</summary>
    [McpServerTool(Name = "logs_clear", Destructive = true)]
    [Description(
        "Drop the in-memory log buffer (the file sink under %LOCALAPPDATA%\\GladiusModding is append-only and " +
        "untouched). Returns how many lines were dropped, so a caller can tell an empty log from a cleared one.")]
    public int Clear()
    {
        int dropped = project.Log.Tail(2000).Count;
        project.Log.Clear();
        project.Log.Write(LogLevel.Info, "Log cleared by MCP client.", "mcp");
        return dropped;
    }

    private static LogLevel? ParseLevel(string? level)
    {
        if (string.IsNullOrWhiteSpace(level)) return null;

        string text = level.Trim();
        if (int.TryParse(text, out int numeric) && numeric is >= 0 and <= 4) return (LogLevel)numeric;

        foreach (LogLevel candidate in Enum.GetValues<LogLevel>())
        {
            if (candidate.ToString().Equals(text, StringComparison.OrdinalIgnoreCase)) return candidate;
        }

        throw new McpToolException(
            $"unknown level '{level}'; use debug, info, success, warning or error");
    }
}
