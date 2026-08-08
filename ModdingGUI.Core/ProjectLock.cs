namespace ModdingGUI.Core;

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;

/// <summary>
/// Per-project mutex used to serialize writes into a given project directory.
/// Every API mutation acquires the lock for the project that owns the target
/// file before opening, mutating, and writing.
/// </summary>
/// <remarks>
/// <para>
/// The "project" is identified by a fully-qualified, normalized directory path
/// (trailing separators removed, case-insensitive on Windows). Two calls with
/// equivalent normalized paths acquire the same lock; calls with distinct
/// project paths acquire independent locks.
/// </para>
/// <para>
/// This is an in-process lock (it does not coordinate with other OS processes).
/// A second instance of the WinForms shell and the MCP server running at the
/// same time are independent writers; external coordination is outside the
/// scope of this class. The xDelta / pack pipelines are likewise unaffected.
/// </para>
/// </remarks>
public static class ProjectLock
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> s_locks =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Acquire the lock for <paramref name="projectPath"/> and return a scope
    /// whose <see cref="IDisposable.Dispose"/> releases it. Blocks the current
    /// thread until the lock is available.
    /// </summary>
    /// <exception cref="ArgumentException">If <paramref name="projectPath"/> is null/empty.</exception>
    public static IDisposable Acquire(string projectPath)
    {
        if (string.IsNullOrWhiteSpace(projectPath))
            throw new ArgumentException("Project path is required.", nameof(projectPath));

        string key = Normalize(projectPath);
        var sem = s_locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        sem.Wait();
        return new Scope(sem);
    }

    /// <summary>Number of distinct projects currently tracked (primarily for diagnostics).</summary>
    public static int TrackedProjectCount => s_locks.Count;

    private static string Normalize(string path)
    {
        string full = Path.GetFullPath(path);
        // Strip trailing directory separators for stable keying.
        return full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private sealed class Scope : IDisposable
    {
        private SemaphoreSlim? _sem;

        public Scope(SemaphoreSlim sem) { _sem = sem; }

        public void Dispose()
        {
            var s = System.Threading.Interlocked.Exchange(ref _sem, null);
            s?.Release();
        }
    }
}
