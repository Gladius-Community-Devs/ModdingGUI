using System;
using System.IO;
using System.Linq;
using ModdingGUI.Core.Api;
using ModdingGUI.Core.Units;
using Xunit;

namespace ModdingGUI.Tests.Core.Units;

/// <summary>
/// The two remaining units files: <c>tints.txt</c> (196 sets of six RGB channels) and <c>schools.txt</c>
/// (a 144-entry name table). Both must survive a no-op save byte for byte — tints.txt holds a paragraph
/// of prose in set 0, and schools.txt leaves entry 0 blank.
/// </summary>
public sealed class TintsAndSchoolNamesTests : IDisposable
{
    private readonly string _project;
    private readonly string _bec;
    private readonly TintSetsApi _tints = new();
    private readonly SchoolNamesApi _schools = new();

    public TintsAndSchoolNamesTests()
    {
        _project = Path.Combine(Path.GetTempPath(), "ModdingGUI.Tints-" + Guid.NewGuid().ToString("N"));
        _bec = Path.Combine(_project, "proj_BEC");
        Directory.CreateDirectory(Path.Combine(_bec, "data", "units"));
        foreach (string name in new[] { "tints.txt", "schools.txt" })
        {
            File.Copy(Path.Combine(CleanBecCorpus.Root!, "data", "units", name),
                Path.Combine(_bec, "data", "units", name));
        }
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_project)) Directory.Delete(_project, recursive: true); }
        catch (IOException) { /* best effort */ }
    }

    private bool Ready => CleanBecCorpus.IsAvailable;

    private string TintsPath => Path.Combine(_bec, "data", "units", "tints.txt");
    private string SchoolsPath => Path.Combine(_bec, "data", "units", "schools.txt");

    // ---- tints -------------------------------------------------------------

    [Fact]
    public void Tints_ListReadsEverySet()
    {
        if (!Ready) return;

        var indexes = _tints.List(_bec);
        Assert.Equal(196, indexes.Count);
        Assert.Equal(0, indexes[0]);
        Assert.Equal(195, indexes[^1]);
    }

    [Fact]
    public void Tints_GetReadsSixChannels()
    {
        if (!Ready) return;

        var set = _tints.Get(_bec, 1);
        Assert.NotNull(set);
        Assert.Equal(6, set!.Colors.Count);
        Assert.Equal(new Rgb(255, 255, 54), set["Cloth1"]);
        Assert.Equal(new Rgb(255, 28, 28), set["Armor1"]);

        Assert.Null(_tints.Get(_bec, 9999));
    }

    [Fact]
    public void Tints_SetZeroIsProseAndRoundTripsUntouched()
    {
        if (!Ready) return;

        byte[] before = File.ReadAllBytes(TintsPath);
        var set = _tints.Get(_bec, 0);
        Assert.NotNull(set);
        Assert.Empty(set!.Colors);

        SectionedFile.Load(TintsPath, "Tint set").Save(_bec, "data/units/tints.txt");
        Assert.Equal(before, File.ReadAllBytes(TintsPath));
    }

    [Fact]
    public void Tints_SetColorRewritesOneRow()
    {
        if (!Ready) return;

        string[] before = File.ReadAllLines(TintsPath);
        _tints.SetColor(_bec, 1, "Cloth1", new Rgb(1, 2, 3));
        string[] after = File.ReadAllLines(TintsPath);

        Assert.Equal(before.Length, after.Length);
        Assert.Equal(1, Enumerable.Range(0, before.Length).Count(i => before[i] != after[i]));
        Assert.Equal(new Rgb(1, 2, 3), _tints.Get(_bec, 1)!["Cloth1"]);
    }

    [Fact]
    public void Tints_SetColorRejectsAnUnknownChannel()
    {
        if (!Ready) return;
        Assert.Throws<ArgumentException>(() => _tints.SetColor(_bec, 1, "Hat", new Rgb(1, 1, 1)));
    }

    [Fact]
    public void Tints_SetColorAddsAMissingChannel()
    {
        if (!Ready) return;

        int index = _tints.AddSet(_bec);
        Assert.Empty(_tints.Get(_bec, index)!.Colors);

        _tints.SetColor(_bec, index, "Skin", new Rgb(10, 20, 30));
        Assert.Equal(new Rgb(10, 20, 30), _tints.Get(_bec, index)!["Skin"]);
    }

    [Fact]
    public void Tints_AddAndRemoveSetKeepNumentries()
    {
        if (!Ready) return;

        int index = _tints.AddSet(_bec);
        Assert.Equal(196, index);
        Assert.Contains("NUMENTRIES: 197", File.ReadAllLines(TintsPath));

        Assert.True(_tints.RemoveSet(_bec, index));
        Assert.Contains("NUMENTRIES: 196", File.ReadAllLines(TintsPath));
    }

    [Fact]
    public void Tints_ValidateVanillaHasNoWarnings()
    {
        if (!Ready) return;

        var issues = _tints.Validate(_bec);
        Assert.DoesNotContain(issues, i => i.Severity == SetValidationSeverity.Warning);
    }

    [Fact]
    public void TintRow_ParsesAndRendersColours()
    {
        Assert.True(TintRow.TryParse("Cloth1: 255 255 54", out TintRow row));
        Assert.Equal(new TintRow("Cloth1", new Rgb(255, 255, 54)), row);
        Assert.Equal("Cloth1: 255 255 54", row.ToLine());

        Assert.False(TintRow.TryParse("Cloth1: 300 0 0", out _));
        Assert.False(TintRow.TryParse("Cloth1: 1 2", out _));
        Assert.False(TintRow.TryParse("no colon here", out _));
    }

    // ---- school names ------------------------------------------------------

    [Fact]
    public void SchoolNames_ListReadsTheTable()
    {
        if (!Ready) return;

        var entries = _schools.List(_bec);
        Assert.Equal(144, entries.Count);
        Assert.Equal(0, entries[0].Index);
        Assert.Equal(string.Empty, entries[0].Name);              // vanilla leaves entry 0 blank
        Assert.Equal("Fliuch Falcons", entries[1].Name);
    }

    [Fact]
    public void SchoolNames_GetFindsByNameIndex()
    {
        if (!Ready) return;

        Assert.Equal("NoSchool", _schools.Get(_bec, 7));
        Assert.Null(_schools.Get(_bec, 99_999));
    }

    [Fact]
    public void SchoolNames_SetNameRewritesOneLine()
    {
        if (!Ready) return;

        string[] before = File.ReadAllLines(SchoolsPath);
        _schools.SetName(_bec, 1, "Fliuch Falcons Renamed");
        string[] after = File.ReadAllLines(SchoolsPath);

        Assert.Equal(before.Length, after.Length);
        Assert.Equal(1, Enumerable.Range(0, before.Length).Count(i => before[i] != after[i]));
        Assert.Equal("Fliuch Falcons Renamed", _schools.Get(_bec, 1));
    }

    [Fact]
    public void SchoolNames_AddAppendsAndUpdatesNumentries()
    {
        if (!Ready) return;

        int index = _schools.Add(_bec, "A Brand New School");
        Assert.Equal(144, index);
        Assert.Equal("A Brand New School", _schools.Get(_bec, index));
        Assert.Contains("NUMENTRIES: 145", File.ReadAllLines(SchoolsPath));
    }

    [Fact]
    public void SchoolNames_RemoveOnlyTouchesTheLastEntry()
    {
        if (!Ready) return;

        Assert.True(_schools.RemoveLast(_bec));
        Assert.Null(_schools.Get(_bec, 143));
        Assert.Contains("NUMENTRIES: 143", File.ReadAllLines(SchoolsPath));
    }

    [Fact]
    public void SchoolNames_NoOpSaveIsByteIdentical()
    {
        if (!Ready) return;

        byte[] before = File.ReadAllBytes(SchoolsPath);
        _ = _schools.List(_bec);
        Assert.Equal(before, File.ReadAllBytes(SchoolsPath));
    }

    [Fact]
    public void SchoolNames_ValidateVanillaHasNoWarnings()
    {
        if (!Ready) return;

        var issues = _schools.Validate(_bec);
        Assert.DoesNotContain(issues, i => i.Severity == SchoolNameValidationSeverity.Warning);
    }

    [Fact]
    public void SchoolNames_ValidateDetectsADuplicate()
    {
        if (!Ready) return;

        _schools.SetName(_bec, 2, "Fliuch Falcons");

        var codes = _schools.Validate(_bec).Select(i => i.Code).ToArray();
        Assert.Contains(SchoolNameValidationCodes.DuplicateName, codes);
    }
}
