using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ModdingGUI.Core.Logging;

/// <summary>
/// In-memory ring buffer plus an optional append-only file sink. One instance per process; the shell
/// renders <see cref="Written"/>, tests read <see cref="Tail"/>, and the MCP server will stream the
/// same stream (PLAN.md Phase 7.8).
/// </summary>
public sealed class AppLog : IAppLog
{
    /// <summary>Default number of entries kept in memory.</summary>
    public const int DefaultCapacity = 2000;

    private readonly object _gate = new();
    private readonly Queue<LogEntry> _entries;
    private readonly int _capacity;
    private readonly AsyncLocal<string?> _scope = new();
    private bool _sinkUnavailable;

    /// <summary>Create a log.</summary>
    /// <param name="capacity">How many entries stay in memory; older lines fall out.</param>
    /// <param name="sinkPath">
    /// File to append lines to, or null for memory only. Failures to open or write it are swallowed and
    /// the in-memory log keeps working.
    /// </param>
    public AppLog(int capacity = DefaultCapacity, string? sinkPath = null)
    {
        if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
        _capacity = capacity;
        _entries = new Queue<LogEntry>(Math.Min(capacity, 256));
        SinkPath = string.IsNullOrWhiteSpace(sinkPath) ? null : Path.GetFullPath(sinkPath);
    }

    /// <inheritdoc />
    public event EventHandler<LogEntry>? Written;

    /// <inheritdoc />
    public string? SinkPath { get; }

    /// <summary>
    /// The conventional sink path: <c>%LOCALAPPDATA%\GladiusModding\last-run.log</c>. One file per
    /// machine, truncated per run, so a headless run leaves evidence of what happened.
    /// </summary>
    public static string DefaultSinkPath()
    {
        string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(root, "GladiusModding", "last-run.log");
    }

    /// <summary>Log with both a ring buffer and the conventional file sink, truncated for this run.</summary>
    public static AppLog CreateForRun(string? sinkPath = null)
    {
        string path = sinkPath ?? DefaultSinkPath();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, string.Empty);   // one file per run
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Memory-only is fine; the caller can still render.
            return new AppLog(capacity: DefaultCapacity, sinkPath: null);
        }

        return new AppLog(capacity: DefaultCapacity, sinkPath: path);
    }

    /// <inheritdoc />
    public void Write(LogLevel level, string message, string? source = null)
    {
        var entry = new LogEntry(
            DateTimeOffset.Now,
            level,
            source ?? _scope.Value ?? "app",
            message ?? string.Empty);

        lock (_gate)
        {
            _entries.Enqueue(entry);
            while (_entries.Count > _capacity) _entries.Dequeue();
        }

        AppendToSink(entry);

        // Each subscriber is isolated: a renderer that throws must not starve the MCP bridge (or the
        // other way round), and a logging failure must never break the operation that logged.
        var handlers = Written;
        if (handlers is null) return;

        foreach (var handler in handlers.GetInvocationList())
        {
            try
            {
                ((EventHandler<LogEntry>)handler).Invoke(this, entry);
            }
            catch (Exception)
            {
                // Swallowed on purpose; see above.
            }
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<LogEntry> Tail(int count = 200)
    {
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));

        lock (_gate)
        {
            int take = Math.Min(count, _entries.Count);
            var result = new LogEntry[take];
            int skip = _entries.Count - take;
            int index = 0;
            foreach (var entry in _entries)
            {
                if (skip-- > 0) continue;
                result[index++] = entry;
            }
            return result;
        }
    }

    /// <inheritdoc />
    public void Clear()
    {
        lock (_gate) _entries.Clear();
    }

    /// <inheritdoc />
    public IDisposable Scope(string source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        string? previous = _scope.Value;
        _scope.Value = source;
        return new ScopeToken(this, previous);
    }

    private void AppendToSink(LogEntry entry)
    {
        if (SinkPath is null || _sinkUnavailable) return;

        lock (_gate)
        {
            try
            {
                File.AppendAllText(SinkPath, entry.ToLine() + Environment.NewLine, Encoding.UTF8);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _sinkUnavailable = true;   // stop trying; memory log keeps working
            }
        }
    }

    private sealed class ScopeToken : IDisposable
    {
        private readonly AppLog _owner;
        private readonly string? _previous;
        private bool _disposed;

        public ScopeToken(AppLog owner, string? previous)
        {
            _owner = owner;
            _previous = previous;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _owner._scope.Value = _previous;
        }
    }
}
