using System.Collections.Generic;
using System.IO;
using System.Linq;
using ModdingGUI.Core.Tools;
using Xunit;

namespace ModdingGUI.Tests;

/// <summary>
/// The derived-binary guard: an unedited project must repack byte-identically, and an edited one must still regenerate.
/// </summary>
/// <remarks>
/// The guard exists because the skills tokenizer is not a byte-exact inverse of the vanilla build: decompressing and
/// recompressing vanilla <c>skills.tok</c> gives the same length but a different hash (and the lookuptext bins change too),
/// with the v007 and v009 toolchains agreeing with each other. Skipping the three regeneration steps while their source
/// text is untouched is what makes <c>vanilla unpack -> pack</c> hash-identical again.
/// </remarks>
public class DerivedToolStateTests : System.IDisposable
{
    private readonly string _project;
    private readonly string _bec;
    private readonly string _config;

    public DerivedToolStateTests()
    {
        _project = Path.Combine(Path.GetTempPath(), "derived-" + System.Guid.NewGuid().ToString("N")[..8]);
        _bec = Path.Combine(_project, "MyMod_BEC");
        _config = Path.Combine(_bec, "data", "config");

        Directory.CreateDirectory(_config);
        File.WriteAllText(Path.Combine(_config, "skills.tok"), "SKILL \"One\"\n");
        File.WriteAllText(Path.Combine(_config, "lookuptext_eng.txt"), "1 Some text\n");
        File.WriteAllText(Path.Combine(_config, "items.tok"), "ITEM \"Sword\"\n");
    }

    public void Dispose()
    {
        if (Directory.Exists(_project)) Directory.Delete(_project, recursive: true);
        System.GC.SuppressFinalize(this);
    }

    [Fact]
    public void Capture_WritesTheManifestToTheProject_NotIntoThePackedTree()
    {
        DerivedToolState.Capture(_project, _bec);

        // The BEC folder is what the packer walks: a manifest in there would be shipped inside the ISO, which is exactly
        // the bug that once put .mod-history there.
        Assert.True(File.Exists(DerivedToolState.PathFor(_project)));
        Assert.False(File.Exists(Path.Combine(_bec, DerivedToolState.ManifestName)));
    }

    [Fact]
    public void SourcesUnchanged_IsTrueRightAfterCapture()
    {
        DerivedToolState.Capture(_project, _bec);

        Assert.True(DerivedToolState.SourcesUnchanged(_project, _bec));
    }

    [Fact]
    public void SourcesUnchanged_IsFalse_WhenSkillsTokWasEdited()
    {
        DerivedToolState.Capture(_project, _bec);
        File.AppendAllText(Path.Combine(_config, "skills.tok"), "SKILL \"Two\"\n");

        Assert.False(DerivedToolState.SourcesUnchanged(_project, _bec));
    }

    [Fact]
    public void SourcesUnchanged_IsFalse_WhenALookuptextFileWasEdited()
    {
        DerivedToolState.Capture(_project, _bec);
        File.WriteAllText(Path.Combine(_config, "lookuptext_eng.txt"), "1 Changed text\n");

        Assert.False(DerivedToolState.SourcesUnchanged(_project, _bec));
    }

    [Fact]
    public void SourcesUnchanged_IsFalse_WhenThereIsNoManifest()
    {
        Assert.False(DerivedToolState.SourcesUnchanged(_project, _bec));
    }

    [Fact]
    public void SourcesUnchanged_IsFalse_WhenARecordedFileWasDeleted()
    {
        DerivedToolState.Capture(_project, _bec);
        File.Delete(Path.Combine(_config, "lookuptext_eng.txt"));

        Assert.False(DerivedToolState.SourcesUnchanged(_project, _bec));
    }

    [Fact]
    public void SourcesUnchanged_IsFalse_ForAProjectThatWasNeverUnpacked()
    {
        string empty = Path.Combine(_project, "Nope");
        Directory.CreateDirectory(empty);

        Assert.False(DerivedToolState.SourcesUnchanged(empty, _bec));
    }

    [Fact]
    public void WithoutDerivedSteps_KeepsTheArchiveAndIsoSteps()
    {
        IReadOnlyList<ToolStep> steps = BatchContentBuilder.BuildPackSteps(
            Path.Combine(_project, "MyMod"), Toolchain.PythonScripts("/tools", "python"));

        IReadOnlyList<ToolStep> kept = DerivedToolState.WithoutDerivedSteps(steps);

        Assert.Equal(3, kept.Count);
        Assert.Equal(new[] { "Gladius_Units_IDX_Repack.py", "bec-tool-all.py", "ngciso-tool.py" }, kept.Select(s => s.Name));
    }

    [Fact]
    public void WithoutDerivedSteps_DropsTheThreeV009Subcommands()
    {
        IReadOnlyList<ToolStep> steps = BatchContentBuilder.BuildPackSteps(
            Path.Combine(_project, "MyMod"), Toolchain.Bundled("/app/tools/v009/tools_v009.exe"));

        IReadOnlyList<ToolStep> kept = DerivedToolState.WithoutDerivedSteps(steps);

        Assert.Equal(new[] { "idx repack", "bec pack", "iso pack" }, kept.Select(s => s.Name));
    }

    [Fact]
    public void Capture_IsIdempotent_SoRepackingTwiceKeepsTheInvariant()
    {
        DerivedToolState.Capture(_project, _bec);
        DerivedToolState.Capture(_project, _bec);

        Assert.True(DerivedToolState.SourcesUnchanged(_project, _bec));
    }
}
