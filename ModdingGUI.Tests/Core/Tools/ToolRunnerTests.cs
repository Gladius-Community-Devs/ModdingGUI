using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using ModdingGUI.Core.Logging;
using ModdingGUI.Core.Tools;
using Xunit;

namespace ModdingGUI.Tests.Core.Tools;

/// <summary>
/// Phase 7.9's contract: tools are started directly, with an argument list, so nothing is parsed by a shell.
/// These tests use <c>cmd.exe</c> as a stand-in for a tool because every Windows box has it and it can be made to
/// print and fail deterministically — it is a fixture, not part of the pipeline.
/// </summary>
public sealed class ToolRunnerTests
{
    private static string TempDir()
    {
        string dir = Path.Combine(Path.GetTempPath(), "ModdingGUI.Run-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static ToolStep Echo(string message, string? workingDirectory = null)
        => new("echo", "cmd.exe", new[] { "/c", "echo", message }, workingDirectory);

    [Fact]
    public void RunStep_ReturnsZeroAndCapturesStandardOutput()
    {
        var log = AppLog.CreateForRun();
        BuildStepResult result = new ToolRunner(log).RunStep(Echo("build-ok"));

        Assert.Equal(0, result.ExitCode);
        Assert.True(result.Succeeded);
        Assert.Contains(result.Output, line => line.Contains("build-ok", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(log.Tail(200), e => e.Message.Contains("build-ok", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void RunStep_PropagatesTheExitCode()
    {
        var step = new ToolStep("fail", "cmd.exe", new[] { "/c", "exit", "7" });

        BuildStepResult result = new ToolRunner().RunStep(step);

        Assert.Equal(7, result.ExitCode);
        Assert.False(result.Succeeded);
    }

    [Fact]
    public void RunStep_LogsTheExactCommandItRan()
    {
        var log = AppLog.CreateForRun();
        new ToolRunner(log).RunStep(Echo("x"));

        Assert.Contains(log.Tail(200), e => e.Message.Contains("cmd.exe", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void RunStep_APathWithParenthesesIsPassedThroughUntouched()
    {
        // The Phase 7.9 complaint: parentheses broke batch parsing even when quoted. As an argument they are
        // ordinary, and this is the assertion that keeps them that way.
        string dir = Path.Combine(Path.GetTempPath(), "ModdingGUI (phase 7.9) a&b-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            // "cd" prints the process's working directory, so the echo proves the directory survived.
            var step = new ToolStep("pwd", "cmd.exe", new[] { "/c", "cd" }, dir);

            BuildStepResult result = new ToolRunner().RunStep(step);

            Assert.Equal(0, result.ExitCode);
            string printed = string.Join("\n", result.Output);
            Assert.Contains("(phase 7.9)", printed);
            Assert.Contains("a&b", printed);
            Assert.DoesNotContain("^", printed);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void RunStep_AMissingExecutableIsReportedNotThrown()
    {
        var log = AppLog.CreateForRun();
        var step = new ToolStep("ghost", "definitely-not-a-real-tool-12345.exe", new[] { "--version" });

        BuildStepResult result = new ToolRunner(log).RunStep(step);

        Assert.False(result.Succeeded);
        Assert.Contains(log.Tail(200), e => e.IsProblem);
    }

    [Fact]
    public void RunStep_AMissingWorkingDirectoryIsReportedNotThrown()
    {
        var log = AppLog.CreateForRun();
        var step = new ToolStep("ghost", "cmd.exe", new[] { "/c", "echo", "x" },
            Path.Combine(Path.GetTempPath(), "no-such-dir-" + Guid.NewGuid().ToString("N")));

        BuildStepResult result = new ToolRunner(log).RunStep(step);

        Assert.False(result.Succeeded);
        Assert.Contains(log.Tail(200), e => e.Message.Contains("working directory", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Run_StopsAtTheFirstFailure()
    {
        var steps = new[]
        {
            Echo("first"),
            new ToolStep("fail", "cmd.exe", new[] { "/c", "exit", "3" }),
            Echo("never"),
        };

        BuildResult result = new ToolRunner().Run(steps);

        Assert.Equal(3, result.ExitCode);
        Assert.Equal(2, result.Steps.Count);
        Assert.Equal("fail", result.FirstFailure!.Name);
    }

    [Fact]
    public void Run_WithNoStepsSucceeds()
    {
        BuildResult result = new ToolRunner().Run(Array.Empty<ToolStep>());

        Assert.True(result.Succeeded);
        Assert.Empty(result.Steps);
    }

    [Fact]
    public void Run_HonoursAnAlreadyCancelledToken()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();

        BuildResult result = new ToolRunner().Run(new[] { Echo("never") }, cts.Token);

        Assert.Equal(ToolRunner.CancelledExitCode, result.ExitCode);
        Assert.Single(result.Steps);
        Assert.Equal(ToolRunner.CancelledExitCode, result.Steps[0].ExitCode);
    }

    [Fact]
    public void Run_CancellingMidStepKillsTheChildAndReturnsTheCancelledCode()
    {
        var step = new ToolStep("sleep", "cmd.exe", new[] { "/c", "ping", "-n", "20", "127.0.0.1" });
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));

        var watch = Stopwatch.StartNew();
        BuildResult result = new ToolRunner().Run(new[] { step }, cts.Token);
        watch.Stop();

        Assert.Equal(ToolRunner.CancelledExitCode, result.ExitCode);
        // A 20-ping run would take ~19 s; cancellation has to return long before that.
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(10), $"cancellation took {watch.Elapsed}");
    }

    [Fact]
    public void CommandLine_SplitsAQuotedInterpreterPathAndItsArguments()
    {
        var (executable, arguments) = CommandLine.Split(@"""C:\Program Files\Python312\python.exe"" -3");

        Assert.Equal(@"C:\Program Files\Python312\python.exe", executable);
        Assert.Equal(new[] { "-3" }, arguments);
    }

    [Fact]
    public void PythonStep_PutsTheInterpreterArgumentsBeforeTheScript()
    {
        ToolStep step = ToolStep.Python("py -3", @"C:\app\tools\bec-tool-all.py", "--platform", "GC");

        Assert.Equal("py", step.Executable);
        Assert.Equal(
            new[] { "-3", @"C:\app\tools\bec-tool-all.py", "--platform", "GC" },
            step.Arguments);
        Assert.Equal("bec-tool-all.py", step.Name);
    }
}
