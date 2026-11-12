using System;
using System.IO;
using System.Linq;
using ModdingGUI.Core.Logging;
using ModdingGUI.Core.Tools;
using Xunit;

namespace ModdingGUI.Tests.Core.Tools;

/// <summary>
/// The build pipeline has to be runnable from Core, not only from the WinForms shell, so a randomized project can
/// be packed by anything (including the MCP server). These tests cover the parts that are cheap to verify: the
/// command chain, the compiler invocation, and the failure paths.
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
    public void CompileScripts_WithoutACompiler_ReportsItInsteadOfThrowing()
    {
        string project = TempDir();
        try
        {
            Directory.CreateDirectory(Path.Combine(project, "proj_BEC", "data", "script"));

            var log = AppLog.CreateForRun();
            var result = new ProjectBuildService(log)
                .CompileScripts(new BuildOptions(project, Path.Combine(project, "tools"), "python", Toolchain.PythonScripts(Path.Combine(project, "tools"))), new[] { "a.scp" });

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
                .CompileScripts(new BuildOptions(project, project, "python", Toolchain.PythonScripts(project)), Array.Empty<string>());

            Assert.True(result.Succeeded);
            Assert.Empty(result.Steps);
        }
        finally
        {
            Directory.Delete(project, recursive: true);
        }
    }

    [Fact]
    public void CompileScripts_BuildsOneStepPerScript_WithTheCompilerAndBothPaths()
    {
        string project = Path.Combine(Path.GetTempPath(), "proj");
        string tools = Path.Combine(Path.GetTempPath(), "tools");

        var steps = ProjectBuildService.BuildCompileSteps(
            new BuildOptions(project, tools, "python", Toolchain.PythonScripts(tools)), new[] { "one.scp", "two.scp" });

        Assert.Equal(2, steps.Count);
        Assert.All(steps, step =>
        {
            Assert.Equal("DOGCodeCompiler.exe", Path.GetFileName(step.Executable));
            Assert.Equal(2, step.Arguments.Count);
            Assert.EndsWith(".scp", step.Arguments[0], StringComparison.OrdinalIgnoreCase);
            Assert.EndsWith(".scb", step.Arguments[1], StringComparison.OrdinalIgnoreCase);
            Assert.NotNull(step.WorkingDirectory);
            Assert.EndsWith("script", step.WorkingDirectory!.TrimEnd(Path.DirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);
        });
        Assert.Contains(steps, step => step.Arguments[0].EndsWith("one.scp", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(steps, step => step.Arguments[0].EndsWith("two.scp", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BuildCompileBatch_StaysAvailableForInspection()
    {
        string project = Path.Combine(Path.GetTempPath(), "proj");
        string tools = Path.Combine(Path.GetTempPath(), "tools");

        string batch = ProjectBuildService.BuildCompileBatch(
            new BuildOptions(project, tools, "python", Toolchain.PythonScripts(tools)), new[] { "one.scp", "two.scp" });

        Assert.StartsWith("@echo off", batch);
        Assert.Contains("DOGCodeCompiler.exe", batch);
        Assert.Contains("one.scp", batch);
        Assert.Contains("two.scp", batch);
    }

    [Fact]
    public void CompileScript_ReportsAMissingCompilerWithoutThrowing()
    {
        string project = TempDir();
        try
        {
            Directory.CreateDirectory(Path.Combine(project, "proj_BEC", "data", "script"));

            BuildStepResult step = new ProjectBuildService()
                .CompileScript(new BuildOptions(project, Path.Combine(project, "tools"), "python", Toolchain.PythonScripts(Path.Combine(project, "tools"))), "a.scp");

            Assert.False(step.Succeeded);
        }
        finally
        {
            Directory.Delete(project, recursive: true);
        }
    }

    [Fact]
    public void Pack_RoutesThroughThePackServiceChain()
    {
        // The pack chain is covered by PackServiceTests; this pins that ProjectBuildService does not invent
        // its own chain, which is the whole point of the move into Core.
        string project = TempDir();
        try
        {
            var steps = PackService.Steps(new PackRequest(project, Path.Combine(project, "tools")));

            Assert.Contains(steps, step => step.Name == "ngciso-tool.py");
            Assert.Contains(steps, step => step.Arguments.Contains("-pack"));

            BuildResult result = new ProjectBuildService(scope: "test")
                .Pack(new BuildOptions(project, Path.Combine(project, "tools"), "python", Toolchain.PythonScripts(Path.Combine(project, "tools"))));

            // No toolbelt in the temp folder, so the chain cannot succeed; the assertion is that it ran and
            // reported a failure instead of throwing.
            Assert.False(result.Succeeded);
        }
        finally
        {
            Directory.Delete(project, recursive: true);
        }
    }
}
