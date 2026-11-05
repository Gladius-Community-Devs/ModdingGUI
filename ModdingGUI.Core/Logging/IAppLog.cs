using System;
using System.Collections.Generic;

namespace ModdingGUI.Core.Logging;

/// <summary>
/// The application's output stream. Everything a user needs to see — progress, validation notes,
/// child-process output, failures — is written here, and the GUI, the tests and (later) the MCP
/// server all read the same entries.
/// </summary>
/// <remarks>
/// The shell used to keep this inside RichTextBox controls, which meant nothing outside the GUI could
/// observe a run: no tests, no headless pack, no AI client. This interface is the fix (PLAN.md Phase
/// 7.8). A control is a <em>consumer</em> of the log, never the store.
/// </remarks>
public interface IAppLog
{
    /// <summary>Write one line. Never throws: a logging failure must not break the operation.</summary>
    void Write(LogLevel level, string message, string? source = null);

    /// <summary>The most recent entries, oldest first.</summary>
    IReadOnlyList<LogEntry> Tail(int count = 200);

    /// <summary>Drop the in-memory buffer (the file sink is append-only and untouched).</summary>
    void Clear();

    /// <summary>
    /// Tag everything written inside the returned scope. Nested scopes win until disposed, so a tool or
    /// a view can own its lines without threading a parameter through every call.
    /// </summary>
    IDisposable Scope(string source);

    /// <summary>Raised after an entry is stored and flushed, so consumers can render or stream it.</summary>
    event EventHandler<LogEntry>? Written;

    /// <summary>Where entries are appended on disk, or null when no file sink is configured.</summary>
    string? SinkPath { get; }
}
