namespace ModdingGUI.Tests.Core.Api;

using ModdingGUI.Core.Api;
using Xunit;

/// <summary>
/// Tests for the UNITDB field catalog and its dropdown sources. The catalogs are what an input is built from, so a
/// missing or mislabelled slot turns into a broken editor rather than a compile error.
/// </summary>
public class EncounterUnitFieldsTests
{
    [Fact]
    public void All_HasOneFieldPerSlotInEngineOrder()
    {
        Assert.Equal(EncounterUnit.SlotCount, EncounterUnitFields.All.Count);
        Assert.Equal(Enumerable.Range(1, EncounterUnit.SlotCount), EncounterUnitFields.All.Select(f => f.Slot));
    }

    [Fact]
    public void KeysAreUniqueAndLookupWorksBothWays()
    {
        Assert.Equal(EncounterUnitFields.All.Count, EncounterUnitFields.All.Select(f => f.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count());

        foreach (EncounterUnitField field in EncounterUnitFields.All)
        {
            Assert.Same(field, EncounterUnitFields.ForSlot(field.Slot));
            Assert.Same(field, EncounterUnitFields.ForKey(field.Key));
        }

        Assert.Null(EncounterUnitFields.ForSlot(0));
        Assert.Null(EncounterUnitFields.ForSlot(EncounterUnit.SlotCount + 1));
        Assert.Null(EncounterUnitFields.ForKey("nope"));
    }

    [Fact]
    public void EveryFieldCarriesADescription()
    {
        // The description is what the UI shows under the input and what an agent reads; an empty one means the slot was
        // added without saying what it does.
        Assert.All(EncounterUnitFields.All, field => Assert.False(string.IsNullOrWhiteSpace(field.Description)));
        Assert.All(EncounterUnitFields.All, field => Assert.False(string.IsNullOrWhiteSpace(field.Label)));
    }

    [Fact]
    public void ChoiceFieldsDeclareTheirChoices()
    {
        foreach (EncounterUnitField field in EncounterUnitFields.All.Where(f => f.Kind == EncounterFieldKind.Choice))
        {
            Assert.NotNull(field.Choices);
            Assert.NotEmpty(field.Choices!);
        }

        EncounterUnitField affinity = EncounterUnitFields.ForKey("affinityRequirement")!;
        Assert.Equal(new[] { "-1", "1", "2", "3", "4", "5" }, affinity.Choices!);
    }

    [Fact]
    public void NumericFieldsDeclareTheirRange()
    {
        Assert.Equal(0, EncounterUnitFields.ForKey("spawnChance")!.Minimum);
        Assert.Equal(1000, EncounterUnitFields.ForKey("spawnChance")!.Maximum);
        Assert.Equal(0, EncounterUnitFields.ForKey("itemDropChance")!.Minimum);
        Assert.Equal(0, EncounterUnitFields.ForKey("slot17")!.Minimum);
        Assert.Equal(3, EncounterUnitFields.ForKey("slot17")!.Maximum);
    }

    [Fact]
    public void ObservedValues_MatchTheCorpus()
    {
        // These were measured across all 6,702 vanilla rows; if someone edits the list, this is the reminder of why.
        Assert.Contains("1000", EncounterUnitFields.ForKey("spawnChance")!.ObservedValues!);
        Assert.Contains("90", EncounterUnitFields.ForKey("itemDropChance")!.ObservedValues!);
        Assert.Equal(new[] { "0" }, EncounterUnitFields.ForKey("requiresMercenary")!.ObservedValues!);
    }

    [Fact]
    public void StartMarkers_ComeFromTheGridTags()
    {
        // The corpus grids are the real input for this list; a brand-new GridFile has no layout version and refuses to save.
        string? sample = FindCorpusGrid();
        if (sample is null) return;

        IReadOnlyList<string> markers = EncounterChoices.StartMarkers(sample);

        Assert.NotEmpty(markers);
        Assert.All(markers, marker => Assert.StartsWith("Start", marker, StringComparison.OrdinalIgnoreCase));
    }

    private static string? FindCorpusGrid()
    {
        // The tests may run without the reference corpus; look beside the solution first, then give up quietly.
        string here = AppContext.BaseDirectory;
        DirectoryInfo? directory = new(here);
        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, "clean-bec-unpacked");
            if (Directory.Exists(candidate))
            {
                return Directory.EnumerateFiles(candidate, "*.grd", SearchOption.AllDirectories).FirstOrDefault();
            }

            directory = directory.Parent;
        }

        return Directory.EnumerateFiles(Path.GetTempPath(), "*.grd", SearchOption.AllDirectories).FirstOrDefault();
    }

    [Fact]
    public void StartMarkers_MissingFile_IsEmptyNotAThrow()
    {
        Assert.Empty(EncounterChoices.StartMarkers(Path.Combine(Path.GetTempPath(), "does-not-exist.grd")));
    }
}
