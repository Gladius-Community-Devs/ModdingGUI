using System;

namespace ModdingGUI.Core.Logging;

/// <summary>How important a log line is. Mirrors the colours the shell already used.</summary>
public enum LogLevel
{
    /// <summary>Step-by-step detail, hidden unless the user asks for it.</summary>
    Debug = 0,

    /// <summary>Normal progress ("Starting pack operation...").</summary>
    Info = 1,

    /// <summary>Something completed ("Item 'X' saved.").</summary>
    Success = 2,

    /// <summary>Recoverable problem the user should read.</summary>
    Warning = 3,

    /// <summary>Failure.</summary>
    Error = 4,
}

/// <summary>
/// One log line: the unit of output the shell renders and an MCP client reads.
/// </summary>
/// <param name="At">When it happened, local time.</param>
/// <param name="Level">Severity.</param>
/// <param name="Source">Who wrote it: <c>grid-builder</c>, <c>pack</c>, <c>items</c>, ...</param>
/// <param name="Message">The text, as the user would read it.</param>
public readonly record struct LogEntry(DateTimeOffset At, LogLevel Level, string Source, string Message)
{
    /// <summary>True for lines a user (or an agent) must not miss.</summary>
    public bool IsProblem => Level is LogLevel.Warning or LogLevel.Error;

    /// <summary>Single-line form used by the file sink and by MCP.</summary>
    public string ToLine() => $"{At:yyyy-MM-dd HH:mm:ss.fff}\t{Level}\t{Source}\t{Message}";

    public override string ToString() => $"[{At:HH:mm:ss}] {Level}: {Message}";
}
