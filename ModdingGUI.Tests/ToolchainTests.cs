using System.Collections.Generic;
using System.IO;
using System.Linq;
using ModdingGUI.Core.Tools;
using Xunit;

namespace ModdingGUI.Tests;

/// <summary>
/// The two toolchains the app can run: the Python scripts (v007) and the bundled v009 executable.
/// </summary>
/// <remarks>
/// The exe exists so a user never has to install Python, so these tests pin the two things that make that safe: the
/// command shapes are the v009 subcommands, and choosing the toolchain falls back to the scripts only when the exe is
/// genuinely absent (a broken install must not silently produce a different build).
/// </remarks>
public class ToolchainTests
{
    private const string Exe = @"C:\app\tools\v009\tools_v009.exe";
    private const string Scripts = @"C:\app\tools";

    [Fact]
    public void Bundled_RequiresNoPython()
    {
        Toolchain toolchain = Toolchain.Bundled(Exe);

        Assert.Equal(ToolchainMode.BundledExecutable, toolchain.Mode);
        Assert.False(toolchain.RequiresPython);
        Assert.Equal(Exe, toolchain.ExecutablePath);
    }

    [Fact]
    public void PythonScripts_RequiresPython()
    {
        Toolchain toolchain = Toolchain.PythonScripts(Scripts, "py -3");

        Assert.True(toolchain.RequiresPython);
        Assert.Equal("py -3", toolchain.PythonCommand);
        Assert.Null(toolchain.ExecutablePath);
    }

    [Fact]
    public void Choose_PrefersTheBundledExe_WhenTheFileExists()
    {
        string folder = Path.Combine(Path.GetTempPath(), "toolchain-" + System.Guid.NewGuid().ToString("N")[..8]);
        string exe = Toolchain.BundledExecutableIn(folder);

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(exe)!);
            File.WriteAllText(exe, "stub");

            Toolchain chosen = Toolchain.Choose(folder, Scripts);

            Assert.Equal(ToolchainMode.BundledExecutable, chosen.Mode);
            Assert.Equal(exe, chosen.ExecutablePath);
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void Choose_FallsBackToTheScripts_WhenTheExeIsMissing()
    {
        string folder = Path.Combine(Path.GetTempPath(), "toolchain-" + System.Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(folder);

        try
        {
            Toolchain chosen = Toolchain.Choose(folder, Scripts);

            Assert.Equal(ToolchainMode.PythonScripts, chosen.Mode);
            Assert.True(chosen.RequiresPython);
            Assert.Equal(Scripts, chosen.ScriptsFolder);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void Choose_CanBeToldToIgnoreTheExe()
    {
        string folder = Path.Combine(Path.GetTempPath(), "toolchain-" + System.Guid.NewGuid().ToString("N")[..8]);
        string exe = Toolchain.BundledExecutableIn(folder);

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(exe)!);
            File.WriteAllText(exe, "stub");

            Toolchain chosen = Toolchain.Choose(folder, Scripts, preferBundled: false);

            Assert.Equal(ToolchainMode.PythonScripts, chosen.Mode);
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void BuildUnpackSteps_Bundled_UsesV009Subcommands()
    {
        IReadOnlyList<ToolStep> steps = BatchContentBuilder.BuildUnpackSteps(
            @"C:\isos\Gladius.iso", "MyMod", @"C:\projects", Toolchain.Bundled(Exe));

        // iso unpack, bec unpack, idx unpack - the maps step needs the Python converter, so it is not added.
        Assert.Equal(3, steps.Count);
        Assert.All(steps, step => Assert.Equal(Exe, step.Executable));

        Assert.Equal(new[] { "iso", "unpack" }, steps[0].Arguments.Take(2));
        Assert.Equal(new[] { "bec", "unpack" }, steps[1].Arguments.Take(2));
        Assert.Contains("--platform", steps[1].Arguments);
        Assert.Equal(new[] { "idx", "unpack" }, steps[2].Arguments.Take(2));
    }

    [Fact]
    public void BuildUnpackSteps_PythonScripts_KeepsTheV007Flags()
    {
        IReadOnlyList<ToolStep> steps = BatchContentBuilder.BuildUnpackSteps(
            @"C:\isos\Gladius.iso", "MyMod", @"C:\projects", Scripts, "python");

        Assert.Equal(3, steps.Count);
        Assert.All(steps, step => Assert.Equal("python", step.Executable));
        Assert.Contains(steps, step => step.Arguments.Any(a => a.EndsWith("ngciso-tool.py", System.StringComparison.Ordinal)));
        Assert.Contains(steps, step => step.Arguments.Contains("--platform"));
    }

    [Fact]
    public void BuildPackSteps_Bundled_RunsTheSixV009SubcommandsInOrder()
    {
        IReadOnlyList<ToolStep> steps = BatchContentBuilder.BuildPackSteps(
            @"C:\projects\MyMod", Toolchain.Bundled(Exe));

        // The order matters: NUMENTRIES and the skills blob must be regenerated before the archive is written, and the
        // IDX tables before the unit .idx binaries are folded back in.
        Assert.Equal(
            new[] { "toknum", "tok compress", "strings", "idx repack", "bec pack", "iso pack" },
            steps.Select(step => step.Name).ToArray());
        Assert.All(steps, step => Assert.Equal(Exe, step.Executable));

        Assert.Equal(new[] { "tok", "compress" }, steps[1].Arguments.Take(2));
        Assert.Equal(new[] { "idx", "repack" }, steps[3].Arguments.Take(2));
        Assert.Equal(new[] { "bec", "pack" }, steps[4].Arguments.Take(2));
        Assert.Equal(new[] { "iso", "pack" }, steps[5].Arguments.Take(2));
        Assert.Contains(steps[4].Arguments, a => a.EndsWith("filelist.txt", System.StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BuildPackSteps_PythonScripts_StillNamesEveryScript()
    {
        IReadOnlyList<ToolStep> steps = BatchContentBuilder.BuildPackSteps(@"C:\projects\MyMod", Scripts, "python");

        Assert.Equal(6, steps.Count);
        Assert.All(steps, step => Assert.Equal("python", step.Executable));

        string[] scripts = steps
            .Select(step => Path.GetFileName(step.Arguments.First(a => a.EndsWith(".py", System.StringComparison.OrdinalIgnoreCase))))
            .ToArray();

        Assert.Contains("Tok_Num_Update.py", scripts);
        Assert.Contains("bec-tool-all.py", scripts);
        Assert.Contains("ngciso-tool.py", scripts);
    }

    [Fact]
    public void BuildUnpackSteps_Bundled_HasNoPythonStep()
    {
        IReadOnlyList<ToolStep> steps = BatchContentBuilder.BuildUnpackSteps(
            @"C:\isos\Gladius.iso", "MyMod", @"C:\projects", Toolchain.Bundled(Exe));

        Assert.DoesNotContain(steps, step => step.Executable.Equals("python", System.StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(steps, step => step.Arguments.Any(a => a.EndsWith(".py", System.StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void BuildPackSteps_Bundled_HasNoPythonStep()
    {
        IReadOnlyList<ToolStep> steps = BatchContentBuilder.BuildPackSteps(
            @"C:\projects\MyMod", Toolchain.Bundled(Exe));

        Assert.DoesNotContain(steps, step => step.Executable.Equals("python", System.StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(steps, step => step.Arguments.Any(a => a.EndsWith(".py", System.StringComparison.OrdinalIgnoreCase)));
    }
}
