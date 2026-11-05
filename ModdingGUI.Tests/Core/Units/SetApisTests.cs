using System;
using System.IO;
using System.Linq;
using ModdingGUI.Core.Api;
using ModdingGUI.Core.Units;
using Xunit;

namespace ModdingGUI.Tests.Core.Units;

/// <summary>
/// <see cref="ISkillSetsApi"/> and <see cref="IItemSetsApi"/> over the corpus: 230 and 404 sets, rows of
/// <c>min max Name [note]</c>, cross-file name validation, and byte-identical no-op saves.
/// </summary>
public sealed class SetApisTests : IDisposable
{
    private readonly string _project;
    private readonly string _bec;
    private readonly SkillSetsApi _skills = new();
    private readonly ItemSetsApi _items = new();

    public SetApisTests()
    {
        _project = Path.Combine(Path.GetTempPath(), "ModdingGUI.Sets-" + Guid.NewGuid().ToString("N"));
        _bec = Path.Combine(_project, "proj_BEC");

        foreach (string relative in new[]
                 {
                     "data/units/skillsets.txt", "data/units/itemsets.txt",
                     "data/config/skills.tok", "data/config/items.tok",
                 })
        {
            string target = Path.Combine(_bec, relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(Path.Combine(CleanBecCorpus.Root!, relative.Replace('/', Path.DirectorySeparatorChar)), target);
        }
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_project)) Directory.Delete(_project, recursive: true); }
        catch (IOException) { /* best effort */ }
    }

    private bool Ready => CleanBecCorpus.IsAvailable;

    private string SkillSetsPath => Path.Combine(_bec, "data", "units", "skillsets.txt");
    private string ItemSetsPath => Path.Combine(_bec, "data", "units", "itemsets.txt");

    // ---- skillsets ---------------------------------------------------------

    [Fact]
    public void SkillSets_ListAndGetReadTheCorpus()
    {
        if (!Ready) return;

        Assert.Equal(230, _skills.List(_bec).Count);
        var set = _skills.Get(_bec, 0);
        Assert.NotNull(set);
        Assert.Equal(new SkillSetRow(0, 30, "Bandit Attack"), set!.Rows[0]);
        Assert.Null(_skills.Get(_bec, 9999));
    }

    [Fact]
    public void SkillSets_RowsParseEverywhereInTheCorpus()
    {
        if (!Ready) return;

        var file = SectionedFile.Load(SkillSetsPath, "Skillset");
        Assert.Equal(230, file.Count);
        foreach (int index in file.Indexes)
        {
            foreach (string line in file.Rows(index))
            {
                Assert.True(SkillSetRow.TryParse(line, out _), $"set {index}: '{line}'");
            }
        }
    }

    [Fact]
    public void SkillSets_BadEditAndBack_IsByteIdentical()
    {
        if (!Ready) return;

        byte[] before = File.ReadAllBytes(SkillSetsPath);
        _ = _skills.Get(_bec, 3);
        Assert.Equal(before, File.ReadAllBytes(SkillSetsPath));
    }

    [Fact]
    public void SkillSets_SetRowChangesOnlyThatRow()
    {
        if (!Ready) return;

        var rows = _skills.Get(_bec, 0)!.Rows.ToList();
        var replacement = rows[0] with { MaxLevel = 12 };
        _skills.SetRow(_bec, 0, 0, replacement);

        Assert.Equal(replacement, _skills.Get(_bec, 0)!.Rows[0]);
        Assert.Equal(rows[1], _skills.Get(_bec, 0)!.Rows[1]);
    }

    [Fact]
    public void SkillSets_AddRowInsertsInLevelOrder()
    {
        if (!Ready) return;

        _skills.AddRow(_bec, 0, new SkillSetRow(31, 31, "Bandit Attack"));

        var rows = _skills.Get(_bec, 0)!.Rows;
        var keys = rows.Select(r => (r.MinLevel, r.MaxLevel)).ToArray();
        Assert.Equal(keys.OrderBy(k => k.Item1).ThenBy(k => k.Item2).ToArray(), keys);
        Assert.Contains(rows, r => r.MinLevel == 31 && r.SkillName == "Bandit Attack");
    }

    [Fact]
    public void SkillSets_AddAndRemoveSetKeepsNumentries()
    {
        if (!Ready) return;

        int index = _skills.AddSet(_bec);
        Assert.Equal(230, index);
        Assert.Contains("NUMENTRIES: 231", File.ReadAllLines(SkillSetsPath));

        Assert.True(_skills.RemoveSet(_bec, index));
        Assert.Contains("NUMENTRIES: 230", File.ReadAllLines(SkillSetsPath));
    }

    [Fact]
    public void SkillSets_ValidateFindsUnknownSkillAndBadWindow()
    {
        if (!Ready) return;

        _skills.AddRow(_bec, 0, new SkillSetRow(40, 2, "No Such Skill"));

        var issues = _skills.Validate(_bec, 0);
        Assert.Contains(issues, i => i.Code == SetValidationCodes.UnknownName);
        Assert.Contains(issues, i => i.Code == SetValidationCodes.LevelWindow);
    }

    [Fact]
    public void SkillSets_VanillaValidateHasNoWarnings()
    {
        if (!Ready) return;

        var issues = _skills.Validate(_bec);
        Assert.DoesNotContain(issues, i => i.Severity == SetValidationSeverity.Warning);
    }

    // ---- itemsets ----------------------------------------------------------

    [Fact]
    public void ItemSets_ListAndGetReadTheCorpus()
    {
        if (!Ready) return;

        Assert.Equal(404, _items.List(_bec).Count);
        Assert.Empty(_items.Get(_bec, 0)!.Rows);            // vanilla set 0 is empty
        var set = _items.Get(_bec, 1);
        Assert.NotNull(set);
        Assert.Contains(set!.Rows, r => r.ItemName == "Hatchet");
        Assert.Null(_items.Get(_bec, 9999));
    }

    [Fact]
    public void ItemSets_RowsParseEverywhereInTheCorpus()
    {
        if (!Ready) return;

        var file = SectionedFile.Load(ItemSetsPath, "Itemset");
        Assert.Equal(404, file.Count);
        foreach (int index in file.Indexes)
        {
            foreach (string line in file.Rows(index))
            {
                Assert.True(ItemSetRow.TryParse(line, out _), $"set {index}: '{line}'");
            }
        }
    }

    [Fact]
    public void ItemSets_NoOpSaveIsByteIdentical()
    {
        if (!Ready) return;

        byte[] before = File.ReadAllBytes(ItemSetsPath);
        SectionedFile.Load(ItemSetsPath, "Itemset").Save(_bec, "data/units/itemsets.txt");
        Assert.Equal(before, File.ReadAllBytes(ItemSetsPath));
    }

    [Fact]
    public void ItemSets_SetRowKeepsTheBracketedNote()
    {
        if (!Ready) return;

        var rows = _items.Get(_bec, 1)!.Rows.ToList();
        var withNote = rows.First(r => r.Note is not null);
        _items.SetRow(_bec, 1, rows.IndexOf(withNote), withNote with { MinLevel = 2 });

        var reloaded = _items.Get(_bec, 1)!.Rows.Single(r => r.ItemName == withNote.ItemName);
        Assert.Equal(2, reloaded.MinLevel);
        Assert.Equal(withNote.Note, reloaded.Note);
    }

    [Fact]
    public void ItemSets_AddRowAndRemoveRow()
    {
        if (!Ready) return;

        _items.AddRow(_bec, 2, new ItemSetRow(1, 1, "Hatchet", "Test note"));
        var added = _items.Get(_bec, 2)!.Rows.Single(r => r.Note == "Test note");
        Assert.Equal("Hatchet", added.ItemName);

        int ordinal = _items.Get(_bec, 2)!.Rows.ToList().IndexOf(added);
        Assert.True(_items.RemoveRow(_bec, 2, ordinal));
        Assert.DoesNotContain(_items.Get(_bec, 2)!.Rows, r => r.Note == "Test note");
    }

    [Fact]
    public void ItemSets_ValidateFindsUnknownItem()
    {
        if (!Ready) return;

        _items.AddRow(_bec, 1, new ItemSetRow(1, 1, "Not An Item"));

        Assert.Contains(_items.Validate(_bec, 1), i => i.Code == SetValidationCodes.UnknownName);
    }

    // ---- row grammar -------------------------------------------------------

    [Theory]
    [InlineData("0 30 Bandit Attack", 0, 30, "Bandit Attack")]
    [InlineData("12 12 Air Asa", 12, 12, "Air Asa")]
    public void SkillSetRow_ParsesMinMaxAndName(string line, int min, int max, string name)
    {
        Assert.True(SkillSetRow.TryParse(line, out SkillSetRow row));
        Assert.Equal(new SkillSetRow(min, max, name), row);
    }

    [Fact]
    public void ItemSetRow_SplitsTheBracketedNote()
    {
        Assert.True(ItemSetRow.TryParse("1 1 Hatchet [Weapon]", out ItemSetRow row));
        Assert.Equal(new ItemSetRow(1, 1, "Hatchet", "Weapon"), row);
        Assert.Equal("1 1 Hatchet [Weapon]", row.ToLine());

        Assert.True(ItemSetRow.TryParse("1 1 Air Fur Band", out ItemSetRow noNote));
        Assert.Null(noNote.Note);
        Assert.Equal("1 1 Air Fur Band", noNote.ToLine());
    }

    [Theory]
    [InlineData("")]
    [InlineData("0 30")]
    [InlineData("x 30 Name")]
    [InlineData("0 30 ")]
    public void Rows_RejectMalformedLines(string line)
    {
        Assert.False(SkillSetRow.TryParse(line, out _));
        Assert.False(ItemSetRow.TryParse(line, out _));
    }
}
