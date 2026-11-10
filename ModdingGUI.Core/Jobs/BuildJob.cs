namespace ModdingGUI.Core.Jobs;

using System;
using System.Threading;
using System.Threading.Tasks;
using ModdingGUI.Core.Tools;

/// <summary>Where a <see cref="BuildJob"/> is in its life.</summary>
public enum JobState
{
    /// <summary>Started and not finished.</summary>
    Running,

    /// <summary>Finished with <see cref="BuildResult.Succeeded"/>.</summary>
    Succeeded,

    /// <summary>Finished, and at least one step failed.</summary>
    Failed,

    /// <summary>Finished because someone asked it to stop.</summary>
    Cancelled,
}

/// <summary>
/// One long-running operation (unpack, pack, compile, randomize) as an object: its state, its result, and something
/// to wait on.
/// </summary>
/// <remarks>
/// <para>
/// Before this, "is the unpack done?" could only be answered by polling the log for a phrase or by sleeping. Both are
/// guesses: the log is text, and a sleep is a bet on how long the game data takes. A job answers the question with
/// data — <see cref="Completion"/> — and the same object is what the GUI, the CLI, the UI driver and (Phase 8) the MCP
/// server can expose, each in its own idiom.
/// </para>
/// <para>
/// The job never faults. A thrown exception becomes a failed result with the message in it, because every front end
/// has to report failure the same way a non-zero exit code is reported.
/// </para>
/// </remarks>
public sealed class BuildJob
{
    private readonly TaskCompletionSource<BuildResult> _completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly CancellationTokenSource _cancellation = new();

    private JobState _state = JobState.Running;

    internal BuildJob(string kind, string target)
    {
        Id = Guid.NewGuid().ToString("N")[..12];
        Kind = string.IsNullOrWhiteSpace(kind) ? "job" : kind;
        Target = target ?? string.Empty;
        StartedAt = DateTimeOffset.Now;
    }

    /// <summary>Short id, stable for the life of the job (for logs and tool responses).</summary>
    public string Id { get; }

    /// <summary>What ran: <c>unpack</c>, <c>pack</c>, <c>compile</c>, <c>randomize</c>, …</summary>
    public string Kind { get; }

    /// <summary>What it ran against — a project folder or an ISO path.</summary>
    public string Target { get; }

    /// <summary>When it started.</summary>
    public DateTimeOffset StartedAt { get; }

    /// <summary>When it finished, or null while it is still running.</summary>
    public DateTimeOffset? FinishedAt { get; private set; }

    /// <summary>Current state. Read under the job's own lock semantics: it only ever moves forward.</summary>
    public JobState State => _state;

    /// <summary>The result once finished, or null while running.</summary>
    public BuildResult? Result { get; private set; }

    /// <summary>
    /// Completes when the job finishes, with the same result. Await it instead of sleeping or polling the log; use
    /// <c>WaitAsync(timeout)</c> when you need a deadline.
    /// </summary>
    public Task<BuildResult> Completion => _completion.Task;

    /// <summary>True once the job has finished, whatever the outcome.</summary>
    public bool IsFinished => _state != JobState.Running;

    /// <summary>Ask the job to stop. The tool runner kills the child process tree.</summary>
    public void Cancel()
    {
        try
        {
            _cancellation.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Finished and cleaned up between the check and the call.
        }
    }

    /// <summary>The token handed to the work.</summary>
    internal CancellationToken Token => _cancellation.Token;

    /// <summary>Wall-clock duration, or the time so far while running.</summary>
    public TimeSpan Elapsed => (FinishedAt ?? DateTimeOffset.Now) - StartedAt;

    /// <summary>
    /// Record the outcome. Deliberately does not complete <see cref="Completion"/>: the registry raises
    /// <c>Finished</c> first, then completes, so a waiter that returns knows every subscriber has been told.
    /// </summary>
    internal void Finish(JobState state, BuildResult result)
    {
        _state = state;
        Result = result;
        FinishedAt = DateTimeOffset.Now;
    }

    /// <summary>Release the waiters, once the finished event has been delivered.</summary>
    internal void Complete()
    {
        _completion.TrySetResult(Result ?? BuildResult.Empty);
        _cancellation.Dispose();
    }

    /// <inheritdoc />
    public override string ToString() =>
        $"{Kind} {Id} {_state} ({Elapsed.TotalSeconds:0.0}s){(_state == JobState.Running ? string.Empty : $" exit {Result?.ExitCode}")}";
}
