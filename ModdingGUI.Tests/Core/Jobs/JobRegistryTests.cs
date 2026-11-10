using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ModdingGUI.Core.Jobs;
using ModdingGUI.Core.Logging;
using ModdingGUI.Core.Tools;
using Xunit;

namespace ModdingGUI.Tests.Core.Jobs;

/// <summary>
/// The point of a job is that "is it finished, and did it work?" is data. Polling the log for a phrase and sleeping
/// for six minutes are both guesses, and both were what the UI driver did before this existed.
/// </summary>
/// <remarks>
/// These tests await instead of blocking: a synchronous wait holds a thread-pool thread, and the work under test needs
/// one too, so a suite running classes in parallel could starve itself and fail on a timeout that never expired.
/// </remarks>
public sealed class JobRegistryTests
{
    private static readonly TimeSpan Ceiling = TimeSpan.FromSeconds(30);

    private static BuildResult Ok() => new(0, new[] { new BuildStepResult("step", 0, Array.Empty<string>()) });

    private static BuildResult Bad(int code) =>
        new(code, new[] { new BuildStepResult("step", code, Array.Empty<string>()) });

    [Fact]
    public async Task AJobCompletesWithItsResult()
    {
        var registry = new JobRegistry();

        BuildJob job = registry.Start("unpack", "proj", _ => Ok());
        await job.Completion.WaitAsync(Ceiling);

        Assert.Equal(JobState.Succeeded, job.State);
        Assert.True(job.Result!.Succeeded);
        Assert.NotNull(job.FinishedAt);
        Assert.True(job.IsFinished);
    }

    [Fact]
    public async Task AFailedStepMarksTheJobFailedButStillCompletes()
    {
        var registry = new JobRegistry();

        BuildJob job = registry.Start("pack", "proj", _ => Bad(3));
        await job.Completion.WaitAsync(Ceiling);

        Assert.Equal(JobState.Failed, job.State);
        Assert.Equal(3, job.Result!.ExitCode);
    }

    [Fact]
    public async Task AThrownExceptionBecomesAFailedResultRatherThanAFaultedTask()
    {
        var log = AppLog.CreateForRun();
        var registry = new JobRegistry();

        BuildJob job = registry.Start("pack", "proj", _ => throw new InvalidOperationException("disk on fire"), log);
        await job.Completion.WaitAsync(Ceiling);

        Assert.Equal(JobState.Failed, job.State);
        Assert.Contains(job.Result!.Steps, step => step.Output.Any(line => line.Contains("disk on fire")));
        Assert.Contains(log.Tail(200), entry => entry.Message.Contains("disk on fire"));
    }

    [Fact]
    public async Task CancellingTheJobIsReportedAsCancelled()
    {
        var registry = new JobRegistry();
        using var started = new ManualResetEventSlim();

        BuildJob job = registry.Start("pack", "proj", token =>
        {
            started.Set();
            while (!token.IsCancellationRequested) Thread.Sleep(10);
            token.ThrowIfCancellationRequested();
            return Ok();
        });

        Assert.True(started.Wait(Ceiling));
        job.Cancel();
        await job.Completion.WaitAsync(Ceiling);

        Assert.Equal(JobState.Cancelled, job.State);
        Assert.Equal(ToolRunner.CancelledExitCode, job.Result!.ExitCode);
    }

    [Fact]
    public async Task WaitReturnsFinishedJobsAndTimesOutOnRunningOnes()
    {
        var registry = new JobRegistry();
        using var release = new ManualResetEventSlim();

        BuildJob job = registry.Start("unpack", "proj", _ =>
        {
            release.Wait(Ceiling);
            return Ok();
        });

        // A short deadline against a running job is a timeout, not a hang.
        Assert.Null(registry.Wait(job.Id, timeout: TimeSpan.FromMilliseconds(100)));

        release.Set();
        BuildJob? finished = await Task.Run(() => registry.Wait(job.Id, timeout: Ceiling));

        Assert.NotNull(finished);
        Assert.Equal(JobState.Succeeded, finished!.State);
    }

    [Fact]
    public async Task WaitCanFindTheLatestJobOfAKind()
    {
        var registry = new JobRegistry();
        await registry.Start("compile", "proj", _ => Ok()).Completion.WaitAsync(Ceiling);

        BuildJob? latest = await Task.Run(() => registry.Wait(kind: "compile", timeout: Ceiling));

        Assert.NotNull(latest);
        Assert.Equal("compile", latest!.Kind);
        Assert.Null(registry.Wait(kind: "unpack", timeout: TimeSpan.FromMilliseconds(50)));
    }

    [Fact]
    public async Task ActiveAndRecentTrackState()
    {
        var registry = new JobRegistry();
        using var release = new ManualResetEventSlim();

        BuildJob running = registry.Start("unpack", "proj", _ =>
        {
            release.Wait(Ceiling);
            return Ok();
        });

        Assert.Contains(registry.Active, job => job.Id == running.Id);
        Assert.Empty(registry.Recent());

        release.Set();
        await running.Completion.WaitAsync(Ceiling);

        Assert.Empty(registry.Active);
        Assert.Contains(registry.Recent(), job => job.Id == running.Id);
        Assert.Equal(running.Id, registry.Find(running.Id)!.Id);
    }

    [Fact]
    public async Task TheEventsCarryTheJobSoConsumersDoNotPollTheLog()
    {
        var registry = new JobRegistry();
        var finished = new TaskCompletionSource<BuildJob>(TaskCreationOptions.RunContinuationsAsynchronously);

        registry.Finished += (_, job) => finished.TrySetResult(job);

        registry.Start("unpack", "proj", _ => Ok());

        BuildJob seen = await finished.Task.WaitAsync(Ceiling);

        Assert.Equal(JobState.Succeeded, seen.State);
        Assert.True(seen.IsFinished);
    }

    [Fact]
    public async Task AThrowingSubscriberDoesNotBreakTheJob()
    {
        var registry = new JobRegistry();
        int calls = 0;

        registry.Finished += (_, _) => throw new InvalidOperationException("consumer blew up");
        registry.Finished += (_, _) => Interlocked.Increment(ref calls);

        BuildJob job = registry.Start("unpack", "proj", _ => Ok());
        await job.Completion.WaitAsync(Ceiling);

        Assert.Equal(JobState.Succeeded, job.State);

        // Handlers run before the waiters are released, so this is already visible.
        Assert.Equal(1, Volatile.Read(ref calls));
    }

    [Fact]
    public async Task TheJobIsAlsoWrittenToTheLogWithItsOutcome()
    {
        var log = AppLog.CreateForRun();
        var registry = new JobRegistry();

        BuildJob job = registry.Start("unpack", "proj", _ => Ok(), log);
        await job.Completion.WaitAsync(Ceiling);

        Assert.Contains(log.Tail(200), entry => entry.Message.Contains("unpack started"));
        Assert.Contains(log.Tail(200), entry => entry.Message.Contains($"finished ({job.Id})"));
    }

    [Fact]
    public async Task StartReturnsImmediatelyInsteadOfBlockingOnTheWork()
    {
        // The caller must get a handle at once — that handle is what the GUI, the driver and MCP wait on.
        var registry = new JobRegistry();
        using var release = new ManualResetEventSlim();

        BuildJob job = registry.Start("unpack", "proj", _ =>
        {
            release.Wait(Ceiling);
            return Ok();
        });

        Assert.Equal(JobState.Running, job.State);
        Assert.False(job.Completion.IsCompleted);
        Assert.False(job.IsFinished);

        release.Set();
        await job.Completion.WaitAsync(Ceiling);

        Assert.Equal(JobState.Succeeded, job.State);
    }

    [Fact]
    public async Task HistoryIsBounded()
    {
        var registry = new JobRegistry();

        for (int i = 0; i < 60; i++)
        {
            await registry.Start("compile", "proj", _ => Ok()).Completion.WaitAsync(Ceiling);
        }

        Assert.True(registry.Recent(100).Count <= 50);
    }
}
