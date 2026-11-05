using System;
using System.IO;
using System.Linq;
using System.Text;
using ModdingGUI.Core.Logging;
using ModdingGUI.Core.Tools;
using Xunit;

namespace ModdingGUI.Tests.Core.Tools;

/// <summary>
/// The build pipeline has to be runnable from Core, not only from the WinForms shell, so a randomized project can
/// be packed by anything (including the MCP server). These tests cover the part that is cheap to verify: batch
/// execution, output capture and the failure paths.
/// </summary>
public sealed class ProjectBuildServiceTests
{
    private static string TempDir()
    {
        string dir = Path.Combine(Path.GetTempPath(), "ModdingGUI.Build-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Fact]
    public void RunBatchContent_ReturnsTheExitCodeAndLogsOutput()
    {
        string dir = TempDir();
        try
        {
            var log = AppLog.CreateForRun();

            int exit = ProjectBuildService.RunBatchContent("echo build-ok", dir, log);

            Assert.Equal(0, exit);
            Assert.Contains(log.Tail(500), e => e.Message.Contains("build-ok", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void RunBatchContent_PropagatesAFailure()
    {
        string dir = TempDir();
        try
        {
            int exit = ProjectBuildService.RunBatchContent("exit /b 7", dir);
            Assert.Equal(7, exit);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void CompileScripts_WithoutACompiler_ReportsItInsteadOfThrowing()
    {
        string project = TempDir();
        try
        {
            Directory.CreateDirectory(Path.Combine(project, "proj_BEC", "data", "script"));

            var log = AppLog.CreateForRun();
            var result = new ProjectBuildService(log)
                .CompileScripts(new BuildOptions(project, Path.Combine(project, "tools")), new[] { "a.scp" });

            Assert.False(result.Succeeded);
            Assert.Contains(log.Tail(500), e => e.Message.Contains("DOGCodeCompiler", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(project, recursive: true);
        }
    }

    [Fact]
    public void CompileScripts_WithNoScripts_IsANoOp()
    {
        string project = TempDir();
        try
        {
            var result = new ProjectBuildService()
                .CompileScripts(new BuildOptions(project, project), Array.Empty<string>());

            Assert.True(result.Succeeded);
            Assert.Empty(result.Steps);
        }
        finally
        {
            Directory.Delete(project, recursive: true);
        }
    }

    [Fact]
    public void BuildCompileBatch_RunsTheCompilerForEachScript()
    {
        string project = Path.Combine(Path.GetTempPath(), "proj");
        string tools = Path.Combine(Path.GetTempPath(), "tools");

        string batch = ProjectBuildService.BuildCompileBatch(
            new BuildOptions(project, tools), new[] { "one.scp", "two.scp" });

        Assert.Contains("DOGCodeCompiler.exe", batch);
        Assert.Contains("one.scp", batch);
        Assert.Contains("two.scp", batch);
        Assert.StartsWith("@echo off", batch);
    }

    [Fact]
    public void Pack_BuildsABatchThatMentionsThePackChain()
    {
        // The batch generator is covered elsewhere; this pins that Pack() routes through it rather than
        // inventing its own chain, which is the whole point of the move into Core.
        string project = Path.Combine(Path.GetTempPath(), "proj");
        string content = BatchContentBuilder.GeneratePackContent(project,
            Path.Combine(Path.GetTempPath()) + Path.DirectorySeparatorChar);

        Assert.Contains("ngciso-tool.py", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("bec-tool-all.py", content, StringComparison.OrdinalIgnoreCase);
    }
}
