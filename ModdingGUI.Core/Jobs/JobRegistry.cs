namespace ModdingGUI.Core.Jobs;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ModdingGUI.Core.Logging;
using ModdingGUI.Core.Tools;

/// <summary>
/// Tracks the long-running operations of one front end, so "is it done yet?" has an answer that is not a guess.
/// </summary>
/// <remarks>
/// <para>
/// One instance per process (the shell owns one, the CLI owns one, the MCP server will own one). Not a static: a test
/// gets its own, and nothing here touches a UI.
/// </para>
/// <para>
/// <see cref="Started"/> and <see cref="Finished"/> carry the <see cref="BuildJob"/>, so a consumer that wants to react
/// (the GUI status line, an MCP progress notification, a driver waiting over a socket) subscribes once instead of
/// scraping <see cref="IAppLog"/> text. Logging still happens — the lines are the human record — but the completion is
/// data.
/// </para>
/// </remarks>
public sealed class JobRegistry
{
    private const int HistoryLimit = 50;

    private readonly object _gate = new();
    private readonly List<BuildJob> _jobs = new();

    /// <summary>Raised after a job is registered and before its work starts.</summary>
    public event EventHandler<BuildJob>? Started;

    /// <summary>Raised once the job's state and result are set. Handlers must not throw; they are isolated.</summary>
    public event EventHandler<BuildJob>? Finished;

    /// <summary>Jobs that have not finished yet, newest first.</summary>
    public IReadOnlyList<BuildJob> Active
    {
        get
        {
            lock (_gate) return _jobs.Where(job => !job.IsFinished).Reverse().ToArray();
        }
    }

    /// <summary>Finished jobs, newest first, up to <paramref name="count"/>.</summary>
    public IReadOnlyList<BuildJob> Recent(int count = 20)
    {
        int take = Math.Clamp(count, 1, HistoryLimit);
        lock (_gate) return _jobs.Where(job => job.IsFinished).Reverse().Take(take).ToArray();
    }

    /// <summary>The most recent job of a kind, running or not, or null.</summary>
    public BuildJob? Latest(string? kind = null)
    {
        lock (_gate)
        {
            return _jobs
                .Where(job => kind is null || job.Kind.Equals(kind, StringComparison.OrdinalIgnoreCase))
                .LastOrDefault();
        }
    }

    /// <summary>One job by id, or null.</summary>
    public BuildJob? Find(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        lock (_gate) return _jobs.FirstOrDefault(job => job.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Start a job. The work runs on the thread pool; its result, its state and its log lines are all observable
    /// without polling anything.
    /// </summary>
    /// <param name="kind">What it is, e.g. <c>unpack</c>.</param>
    /// <param name="target">What it is against, for logs and tool responses.</param>
    /// <param name="work">The operation. It receives the cancellation token and returns the build result.</param>
    /// <param name="log">Where to record the start, the end and a thrown exception.</param>
    public BuildJob Start(string kind, string target, Func<CancellationToken, BuildResult> work, IAppLog? log = null)
    {
        ArgumentNullException.ThrowIfNull(work);

        var job = new BuildJob(kind, target);

        lock (_gate)
        {
            _jobs.Add(job);
            Trim();
        }

        log?.Write(LogLevel.Info, $"{job.Kind} started ({job.Id}): {job.Target}", "jobs");
        Raise(Started, job);

        _ = Task.Run(() =>
        {
            BuildResult result;
            try
            {
                result = work(job.Token) ?? BuildResult.Empty;
            }
            catch (OperationCanceledException)
            {
                result = new BuildResult(ToolRunner.CancelledExitCode, Array.Empty<BuildStepResult>());
            }
            catch (Exception ex)
            {
                // A front end must be able to report this, so it becomes a failed result rather than a faulted task.
                string message = ex.Message;
                log?.Write(LogLevel.Error, $"{job.Kind} failed: {message}", "jobs");
                result = new BuildResult(1, new[] { new BuildStepResult(job.Kind, 1, new[] { message }) });
            }

            JobState state = job.Token.IsCancellationRequested
                ? JobState.Cancelled
                : result.Succeeded ? JobState.Succeeded : JobState.Failed;

            job.Finish(state, result);

            log?.Write(
                state == JobState.Succeeded ? LogLevel.Success : LogLevel.Error,
                $"{job.Kind} finished ({job.Id}) in {job.Elapsed.TotalSeconds:0.0}s: {state} (exit {result.ExitCode})",
                "jobs");

            // Subscribers first, waiters second: when a Wait returns, the events have been delivered.
            Raise(Finished, job);
            job.Complete();
        });

        return job;
    }

    /// <summary>
    /// Wait for a job to finish, by id, or the latest of <paramref name="kind"/>.
    /// </summary>
    /// <param name="id">Job id, or null to use <paramref name="kind"/>.</param>
    /// <param name="kind">Job kind when no id is given.</param>
    /// <param name="timeout">How long to wait; <see cref="Timeout.InfiniteTimeSpan"/> to wait forever.</param>
    /// <param name="cancellationToken">Stops the wait (not the job).</param>
    /// <returns>The job once finished, or null when it was not found or the timeout elapsed.</returns>
    public BuildJob? Wait(
        string? id = null,
        string? kind = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        BuildJob? job = string.IsNullOrWhiteSpace(id) ? Latest(kind) : Find(id);
        if (job is null) return null;

        TimeSpan limit = timeout ?? TimeSpan.FromMinutes(10);

        try
        {
            bool finished = job.Completion.Wait(limit, cancellationToken);
            return finished ? job : null;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (AggregateException)
        {
            // Completion never faults; this is defensive only.
            return job.IsFinished ? job : null;
        }
    }

    private void Trim()
    {
        if (_jobs.Count <= HistoryLimit) return;

        foreach (BuildJob finished in _jobs.Where(job => job.IsFinished)
                     .Take(_jobs.Count - HistoryLimit)
                     .ToList())
        {
            _jobs.Remove(finished);
        }
    }

    private void Raise(EventHandler<BuildJob>? handlers, BuildJob job)
    {
        if (handlers is null) return;

        foreach (EventHandler<BuildJob> handler in handlers.GetInvocationList())
        {
            try
            {
                handler(this, job);
            }
            catch (Exception)
            {
                // A consumer that throws must not take down the operation (or the other consumers).
            }
        }
    }
}
