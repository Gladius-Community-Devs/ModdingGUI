using System;
using System.IO;
using System.Linq;
using ModdingGUI.Core.Logging;
using ModdingGUI.Core.Tools;
using Xunit;

namespace ModdingGUI.Tests.Core.Tools;

/// <summary>
/// Phase 7.8 moved pack/unpack into Core; Phase 7.9 made them start the tools directly. Together the contract is:
/// the chain is a list of (executable, arguments) steps, no <c>cmd.exe</c> and no <c>.bat</c> in the path, and a
/// project whose folder name contains parentheses is ordinary.
/// </summary>
public sealed class PackUnpackServiceTests
{
    private static string TempDir()
    {
        string dir = Path.Combine(Path.GetTempPath(), "ModdingGUI.Pack-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    // The chain itself ---------------------------------------------------------

    [Fact]
    public void BuildPackSteps_IsSixDirectPythonSteps()
    {
        var steps = BatchContentBuilder.BuildPackSteps(@"C:\work\MyMod", @"C:\app\tools");

        Assert.Equal(6, steps.Count);
        Assert.All(steps, step =>
        {
            Assert.Equal("python", step.Executable);
            Assert.EndsWith(".py", step.Arguments[0], StringComparison.OrdinalIgnoreCase);
        });

        Assert.Equal(
            new[]
            {
                "Tok_Num_Update.py", "tok-tool.py", "Update_Strings_Bin.py",
                "Gladius_Units_IDX_Repack.py", "bec-tool-all.py", "ngciso-tool.py",
            },
            steps.Select(step => step.Name));
    }

    [Fact]
    public void BuildUnpackSteps_IsThreeDirectPythonSteps()
    {
        var steps = BatchContentBuilder.BuildUnpackSteps(
            @"C:\isos\Gladius.iso", "MyMod", @"C:\work", @"C:\app\tools");

        Assert.Equal(3, steps.Count);
        Assert.All(steps, step => Assert.Equal("python", step.Executable));
        Assert.Equal(
            new[] { "ngciso-tool.py", "bec-tool-all.py", "Gladius_Units_IDX_Unpack.py" },
            steps.Select(step => step.Name));
    }

    [Fact]
    public void NoStepRunsTheShell()
    {
        var steps = BatchContentBuilder.BuildPackSteps(@"C:\work\MyMod", @"C:\app\tools")
            .Concat(BatchContentBuilder.BuildUnpackSteps(
                @"C:\isos\Gladius.iso", "MyMod", @"C:\work", @"C:\app\tools"));

        Assert.DoesNotContain(steps, step =>
            step.Executable.Equals("cmd.exe", StringComparison.OrdinalIgnoreCase) ||
            step.Executable.Equals("cmd", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TheInterpreterIsConfigurable()
    {
        var steps = BatchContentBuilder.BuildPackSteps(@"C:\work\MyMod", @"C:\app\tools", "py -3");

        Assert.All(steps, step =>
        {
            Assert.Equal("py", step.Executable);
            Assert.Equal("-3", step.Arguments[0]);
        });
    }

    // Parentheses are ordinary (Phase 7.9) ------------------------------------

    [Fact]
    public void AParenthesisInTheProjectPathSurvivesIntoTheArgumentsUntouched()
    {
        string project = @"C:\Mods (backup)\My Mod & co";

        var steps = BatchContentBuilder.BuildPackSteps(project, @"C:\app\tools");

        string arguments = string.Join("|", steps.SelectMany(step => step.Arguments));
        Assert.Contains("(backup)", arguments);
        Assert.Contains("My Mod & co", arguments);

        // A caret is a batch escape. In an argument list it would be a literal character, so it must not appear.
        Assert.DoesNotContain("^", arguments);
        Assert.DoesNotContain("\"", arguments);
        Assert.DoesNotContain("%%", arguments);
    }

    [Fact]
    public void TheBatchTextStillEscapes_BecauseItIsForABatchParser()
    {
        // Kept for inspection (Save batch files). It is the step list, not this text, that runs.
        string batch = BatchContentBuilder.GeneratePackContent(@"C:\Mods (backup)\My Mod & co", @"C:\app");

        Assert.Contains("^(", batch);
        Assert.Contains("^&", batch);
    }

    [Fact]
    public void PrintingTheChainAsBatchText_MatchesTheStepsOneLineEach()
    {
        var steps = BatchContentBuilder.BuildPackSteps(@"C:\work\MyMod", @"C:\app\tools");

        string batch = BatchContentBuilder.ToBatchText(steps);

        Assert.Equal(steps.Count, batch.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
        foreach (ToolStep step in steps) Assert.Contains(step.ToBatchLine(), batch);
    }

    // Services -----------------------------------------------------------------

    [Fact]
    public void PackService_ReportsAMissingProjectInsteadOfThrowing()
    {
        var log = AppLog.CreateForRun();
        string missing = Path.Combine(Path.GetTempPath(), "no-such-project-" + Guid.NewGuid().ToString("N"));

        BuildResult result = new PackService(log).Pack(new PackRequest(missing, Path.Combine(missing, "tools")));

        Assert.False(result.Succeeded);
        Assert.Contains(log.Tail(200), e => e.Message.Contains("project folder not found", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PackService_ReportsAMissingToolbelt()
    {
        string project = TempDir();
        try
        {
            var log = AppLog.CreateForRun();
            BuildResult result = new PackService(log)
                .Pack(new PackRequest(project, Path.Combine(project, "no-tools")));

            Assert.False(result.Succeeded);
            Assert.Contains(log.Tail(200), e => e.Message.Contains("toolbelt folder not found", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(project, recursive: true);
        }
    }

    [Fact]
    public void PackService_WritesNoBatchFile()
    {
        string root = TempDir();
        string project = Path.Combine(root, "My Project (backup)");
        Directory.CreateDirectory(project);
        try
        {
            // An empty toolbelt: every step fails at start-up, which is fine — the assertion is about what was not
            // created and about the command that was attempted.
            string tools = Path.Combine(project, "tools");
            Directory.CreateDirectory(tools);

            string[] tempBatchesBefore = Directory.GetFiles(Path.GetTempPath(), "*.bat");

            var log = AppLog.CreateForRun();
            BuildResult result = new PackService(log).Pack(new PackRequest(project, tools));

            Assert.False(result.Succeeded);
            Assert.Empty(Directory.GetFiles(project, "*.bat"));
            Assert.Equal(tempBatchesBefore, Directory.GetFiles(Path.GetTempPath(), "*.bat"));
            Assert.Contains(log.Tail(300), e => e.Message.Contains("(backup)", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void UnpackService_CreatesTheProjectFolder()
    {
        string root = TempDir();
        try
        {
            string iso = Path.Combine(root, "Gladius.iso");
            File.WriteAllBytes(iso, Array.Empty<byte>());
            string tools = Path.Combine(root, "tools");
            Directory.CreateDirectory(tools);

            var request = new UnpackRequest(iso, "My Project (backup)", root, tools);
            BuildResult result = new UnpackService().Unpack(request);

            // The toolbelt is empty, so the chain fails — but the project folder is the caller's guarantee.
            Assert.False(result.Succeeded);
            Assert.True(Directory.Exists(UnpackService.ProjectFolderOf(request)));
            Assert.Equal(Path.Combine(root, "My Project (backup)"), UnpackService.ProjectFolderOf(request));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void UnpackService_ReportsAMissingIso()
    {
        string root = TempDir();
        try
        {
            var log = AppLog.CreateForRun();
            var request = new UnpackRequest(
                Path.Combine(root, "nope.iso"), "Proj", root, Path.Combine(root, "tools"));

            BuildResult result = new UnpackService(log).Unpack(request);

            Assert.False(result.Succeeded);
            Assert.Contains(log.Tail(200), e => e.Message.Contains("ISO not found", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void UnpackRequest_StepsPointTheProjectAtTheRequestedFolder()
    {
        var request = new UnpackRequest(
            @"C:\isos\Gladius.iso", "MyMod", @"C:\work", @"C:\app\tools");

        var steps = UnpackService.Steps(request);

        Assert.All(steps, step => Assert.Equal(
            PathHelper.NormalizePath(PathHelper.EnsureTrailingSeparator(UnpackService.ProjectFolderOf(request))),
            step.WorkingDirectory));
    }
}
