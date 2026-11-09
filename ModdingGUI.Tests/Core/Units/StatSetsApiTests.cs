using System;
using System.IO;
using System.Linq;
using ModdingGUI.Core.Api;
using ModdingGUI.Core.Units;
using Xunit;

namespace ModdingGUI.Tests.Core.Units;

/// <summary>
/// <see cref="IStatSetsApi"/> over real corpus data: 169 sets of 30 level rows, byte-identical no-op
/// saves, structural edits that keep NUMENTRIES honest, and validation.
/// </summary>
public sealed class StatSetsApiTests : IDisposable
{
    private readonly string _project;
    private readonly string _bec;
    private readonly StatSetsApi _api = new();

    public StatSetsApiTests()
    {
        _project = Path.Combine(Path.GetTempPath(), "ModdingGUI.Stats-" + Guid.NewGuid().ToString("N"));
        _bec = Path.Combine(_project, "proj_BEC");
        string target = Path.Combine(_bec, "data", "units", "statsets.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.Copy(Path.Combine(CleanBecCorpus.Root!, "data", "units", "statsets.txt"), target);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_project)) Directory.Delete(_project, recursive: true); }
        catch (IOException) { /* best effort */ }
    }

    private bool Ready => CleanBecCorpus.IsAvailable;

    private string Path_ => Path.Combine(_bec, "data", "units", "statsets.txt");

    [Fact]
    public void List_ReturnsEverySectionIndex()
    {
        if (!Ready) return;

        var indexes = _api.List(_bec);
        Assert.Equal(169, indexes.Count);
        Assert.Equal(0, indexes[0]);
        Assert.Equal(168, indexes[^1]);
    }

    [Fact]
    public void Get_ReadsTheLevelRows()
    {
        if (!Ready) return;

        var set = _api.Get(_bec, 0);
        Assert.NotNull(set);
        Assert.Equal(30, set!.Rows.Count);
        Assert.Equal(new StatSetRow(1, 4, 4, 15, 9, 26), set.Rows[0]);
        Assert.Equal(new StatSetRow(30, 86, 92, 160, 153, 173), set.Rows[^1]);

        Assert.Null(_api.Get(_bec, 9999));
    }

    [Fact]
    public void GetAll_ReadsEverySetInOnePass()
    {
        if (!Ready) return;

        var all = _api.GetAll(_bec);

        Assert.Equal(169, all.Count);
        Assert.Equal(_api.List(_bec), all.Select(set => set.Index).ToArray());
        Assert.All(all, set => Assert.Equal(30, set.Rows.Count));

        // Same content whichever way it is read; GetAll exists so the Team Builder does not parse 169 times.
        var first = _api.Get(_bec, 0)!;
        Assert.Equal(first.Rows, all[0].Rows);
    }

    [Fact]
    public void NoOpRead_LeavesTheFileUntouched()
    {
        if (!Ready) return;

        byte[] before = File.ReadAllBytes(Path_);
        _ = _api.Get(_bec, 0);
        Assert.Equal(before, File.ReadAllBytes(Path_));
    }

    [Fact]
    public void SetRow_ChangesOneLineAndRoundTrips()
    {
        if (!Ready) return;

        var original = _api.Get(_bec, 0)!.Row(5)!;
        _api.SetRow(_bec, 0, original with { Con = original.Con + 3 });

        var updated = _api.Get(_bec, 0)!.Row(5)!;
        Assert.Equal(original.Con + 3, updated.Con);

        byte[] before = File.ReadAllBytes(Path_);
        var file = SectionedFile.Load(Path_, "Statset");
        Assert.Equal(before, file.ToBytes());
    }

    [Fact]
    public void SetRow_OnAMissingLevel_Throws()
    {
        if (!Ready) return;
        Assert.Throws<InvalidOperationException>(() => _api.SetRow(_bec, 0, new StatSetRow(99, 1, 1, 1, 1, 1)));
    }

    [Fact]
    public void AddRow_InsertsInLevelOrder()
    {
        if (!Ready) return;

        _api.RemoveRow(_bec, 3, 31);
        _api.AddRow(_bec, 3, new StatSetRow(31, 90, 90, 90, 90, 90));

        var levels = _api.Get(_bec, 3)!.Rows.Select(r => r.Level).ToArray();
        Assert.Equal(levels.OrderBy(l => l).ToArray(), levels);
        Assert.Equal(90, _api.Get(_bec, 3)!.Row(31)!.Con);
    }

    [Fact]
    public void AddRow_RefusesADuplicateLevel()
    {
        if (!Ready) return;
        Assert.Throws<InvalidOperationException>(() => _api.AddRow(_bec, 0, _api.Get(_bec, 0)!.Row(1)!));
    }

    [Fact]
    public void RemoveRow_DropsItAndKeepsTheRest()
    {
        if (!Ready) return;

        Assert.True(_api.RemoveRow(_bec, 0, 30));
        Assert.Equal(29, _api.Get(_bec, 0)!.Rows.Count);
        Assert.Null(_api.Get(_bec, 0)!.Row(30));
        Assert.False(_api.RemoveRow(_bec, 0, 30));
    }

    [Fact]
    public void AddSet_AppendsASectionAndUpdatesNumentries()
    {
        if (!Ready) return;

        int index = _api.AddSet(_bec);
        Assert.Equal(169, index);

        var file = SectionedFile.Load(Path_, "Statset");
        Assert.Equal(170, file.Count);
        Assert.Equal(170, file.DeclaredCount);
        Assert.Contains("NUMENTRIES: 170", File.ReadAllLines(Path_));
        Assert.Empty(_api.Get(_bec, index)!.Rows);
    }

    [Fact]
    public void RemoveSet_DropsTheSectionAndUpdatesNumentries()
    {
        if (!Ready) return;

        Assert.True(_api.RemoveSet(_bec, 168));
        Assert.False(_api.RemoveSet(_bec, 168));

        var file = SectionedFile.Load(Path_, "Statset");
        Assert.Equal(168, file.Count);
        Assert.Equal(168, file.DeclaredCount);
    }

    [Fact]
    public void Validate_VanillaCorpus_IsClean()
    {
        if (!Ready) return;

        var issues = _api.Validate(_bec);

        Assert.DoesNotContain(issues, i => i.Severity == StatSetValidationSeverity.Warning);
        Assert.DoesNotContain(issues, i => i.Code == StatSetValidationCodes.CountMismatch);
    }

    [Fact]
    public void Validate_ReportsAMalformedRowAndADuplicateLevel()
    {
        if (!Ready) return;

        _api.AddRow(_bec, 0, new StatSetRow(31, 1, 1, 1, 1, 1));
        // Corrupt the file the way a bad edit would.
        string[] lines = File.ReadAllLines(Path_);
        int firstRow = Array.FindIndex(lines, l => l.TrimStart().StartsWith("1: ", StringComparison.Ordinal));
        lines[firstRow] = "1: 4 4 15";                                  // malformed: three values
        lines[firstRow + 1] = lines[firstRow + 1].Replace("2: ", "3: "); // duplicate of the real level 3
        File.WriteAllLines(Path_, lines);

        var codes = _api.Validate(_bec, 0).Select(i => i.Code).ToArray();
        Assert.Contains(StatSetValidationCodes.MalformedRow, codes);
        Assert.Contains(StatSetValidationCodes.DuplicateLevel, codes);
    }

    [Fact]
    public void Validate_FlagsASetOutsideTheVanillaShape_AsInfo()
    {
        if (!Ready) return;

        _api.RemoveRow(_bec, 2, 30);

        var issue = _api.Validate(_bec, 2).Single(i => i.Code == StatSetValidationCodes.UnexpectedCoverage);
        Assert.Equal(StatSetValidationSeverity.Info, issue.Severity);
    }

    [Fact]
    public void Parse_EveryVanillaSetFitsTheRowGrammar()
    {
        if (!Ready) return;

        var file = SectionedFile.Load(Path_, "Statset");
        Assert.Equal(169, file.Count);

        foreach (int index in file.Indexes)
        {
            foreach (string line in file.Rows(index))
            {
                Assert.True(StatSetRow.TryParse(line, out _), $"set {index}: '{line}'");
            }
        }
    }
}
