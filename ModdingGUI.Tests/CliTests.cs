using System;
using System.IO;
using ModdingGUI;
using Xunit;

namespace ModdingGUI.Tests;

/// <summary>
/// Tests for the non-GUI entry points (<see cref="CliInvocation"/>) and for the batch
/// the app generates for packing. The batch test exists because a hand-rolled pack
/// chain once silently skipped <c>Tok_Num_Update.py</c>, leaving NUMENTRIES stale.
/// </summary>
public sealed class CliTests
{
    [Fact]
    public void Parse_NoArguments_ReturnsNullSoTheGuiStarts()
    {
        Assert.Null(CliInvocation.TryParse(Array.Empty<string>(), out string? error));
        Assert.Null(error);
    }

    [Fact]
    public void Parse_Pack_UsesTheProjectFolder()
    {
        var invocation = CliInvocation.TryParse(
            new[] { "--pack", @"C:\projects\MyMod" }, out string? error);

        Assert.Null(error);
        Assert.NotNull(invocation);
        Assert.Equal(CliMode.Pack, invocation!.Mode);
        Assert.Equal(@"C:\projects\MyMod", invocation.ProjectFolder);
        Assert.Null(invocation.ToolsDirectory);
    }

    [Fact]
    public void Parse_Unpack_TakesIsoAndProjectName()
    {
        var invocation = CliInvocation.TryParse(
            new[] { "--unpack", @"C:\isos\gladius.iso", "MyMod" }, out string? error);

        Assert.Null(error);
        Assert.NotNull(invocation);
        Assert.Equal(CliMode.Unpack, invocation!.Mode);
        Assert.Equal(@"C:\isos\gladius.iso", invocation.IsoPath);
        Assert.Equal("MyMod", invocation.ProjectName);
    }

    [Fact]
    public void Parse_ToolsAndPythonOptions_ArePropagated()
    {
        var invocation = CliInvocation.TryParse(
            new[] { "--pack", "proj", "--tools", @"C:\tb\tools", "--python", "py -3" },
            out string? error);

        Assert.Null(error);
        Assert.Equal(@"C:\tb\tools", invocation!.ToolsDirectory);
        Assert.Equal("py -3", invocation.PythonCommand);
    }

    [Fact]
    public void Parse_Help_IsRecognisedWithoutAPipeline()
    {
        var help = CliInvocation.TryParse(new[] { "--help" }, out _);

        Assert.NotNull(help);
        Assert.True(help!.IsHelp);
        Assert.Contains("--pack", CliInvocation.Usage);
        Assert.Contains("--unpack", CliInvocation.Usage);
    }

    [Theory]
    [InlineData("--pack")]                                   // no project folder
    [InlineData("--pack", "a", "b")]                         // too many
    [InlineData("--unpack", "only.iso")]                     // missing project name
    [InlineData("--pack", "proj", "--tools")]                // option without value
    [InlineData("--pack", "proj", "--nonsense")]             // unknown option
    public void Parse_InvalidArguments_ReportAnError(params string[] args)
    {
        var invocation = CliInvocation.TryParse(args, out string? error);

        Assert.Null(invocation);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void Parse_UiFlags_AreNotTreatedAsCliInvocations()
    {
        // Anything that is not --pack/--unpack/--help falls through to the GUI.
        Assert.Null(CliInvocation.TryParse(new[] { "--randomizer" }, out _));
        Assert.Null(CliInvocation.TryParse(new[] { "MyProject" }, out _));
    }

    [Fact]
    public void PackBatch_RunsEveryPipelineStepInTheSameOrderAsTheGui()
    {
        string batch = BatchContentBuilder.GeneratePackContent(
            Path.Combine(@"C:\projects", "TEST"), Path.Combine(@"C:\app", ""));

        string[] steps =
        {
            "Tok_Num_Update.py",          // NUMENTRIES first — skipping this truncates skill data
            "tok-tool.py",               // compiled skills (.brf + strings/lines bins)
            "Update_Strings_Bin.py",     // lookuptext_eng.bin
            "Gladius_Units_IDX_Repack.py",
            "bec-tool-all.py",
            "ngciso-tool.py",
        };

        int previous = -1;
        foreach (string step in steps)
        {
            int index = batch.IndexOf(step, StringComparison.OrdinalIgnoreCase);
            Assert.True(index >= 0, $"pack batch is missing '{step}':\n{batch}");
            Assert.True(index > previous,
                $"'{step}' is out of order in the pack batch:\n{batch}");
            previous = index;
        }
    }

    [Fact]
    public void UnpackBatch_RunsIsoThenBecThenUnits()
    {
        string batch = BatchContentBuilder.GenerateUnpackContent(
            Path.Combine(@"C:\isos", "gladius.iso"), "MyMod", Path.Combine(@"C:\app", ""));

        int iso = batch.IndexOf("ngciso-tool.py", StringComparison.OrdinalIgnoreCase);
        int bec = batch.IndexOf("bec-tool-all.py", StringComparison.OrdinalIgnoreCase);
        int units = batch.IndexOf("Gladius_Units_IDX_Unpack.py", StringComparison.OrdinalIgnoreCase);

        Assert.True(iso >= 0 && bec > iso && units > bec,
            $"unpack steps are missing or out of order:\n{batch}");
    }
}
