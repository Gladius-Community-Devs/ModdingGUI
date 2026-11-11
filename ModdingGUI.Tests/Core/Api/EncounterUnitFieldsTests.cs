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
    public void AttributeTable_MatchesTheEngineBitAssignments()
    {
        // UnitConditionalLookUpAttr (0x800f1444) ORs these bits for the "unitattr req/ign" tokens.
        Assert.Equal(0x4, EncounterUnitFields.AttributeBitFor("Heavy"));      // devnotes' "4 = heavy"
        Assert.Equal(0x1, EncounterUnitFields.AttributeBitFor("Light"));
        Assert.Equal(0x20, EncounterUnitFields.AttributeBitFor("Beast"));
        Assert.Equal(0x80, EncounterUnitFields.AttributeBitFor("Female"));
        Assert.Equal(0x800, EncounterUnitFields.AttributeBitFor("Steppes"));
        Assert.Equal(0, EncounterUnitFields.AttributeBitFor("nonsense"));
        Assert.Equal(12, EncounterUnitFields.AttributeTable.Count);
        Assert.Equal(12, EncounterUnitFields.AttributeTable.Select(a => a.Bit).Distinct().Count());
    }

    [Fact]
    public void AttributeNamesFor_DecodesAVanillaMask()
    {
        // 1056 = 0x420 = Beast|Expanse; 4224 = 0x1080 = Female plus 0x1000, which no attribute claims.
        Assert.Equal(new[] { "Beast", "Expanse" }, EncounterUnitFields.AttributeNamesFor(1056));
        Assert.Equal(new[] { "Female" }, EncounterUnitFields.AttributeNamesFor(4224));
        Assert.Empty(EncounterUnitFields.AttributeNamesFor(0));
    }

    [Fact]
    public void SplitMask_SeparatesTheThreeGroupsTheEngineTests()
    {
        // FUN_8010ad40 reads one integer as three masks: 0x103f attributes, 0xf00 class tags, 0xc0 unclaimed bits.
        (int attributes, int classTags, int unclaimed) = EncounterUnitFields.SplitMask(0x103F);
        Assert.Equal(0x103F, attributes);
        Assert.Equal(0, classTags);
        Assert.Equal(0, unclaimed);

        (attributes, classTags, unclaimed) = EncounterUnitFields.SplitMask(0x0F00);
        Assert.Equal(0, attributes);
        Assert.Equal(0x0F00, classTags);
        Assert.Equal(0, unclaimed);

        (attributes, classTags, unclaimed) = EncounterUnitFields.SplitMask(0x00C0);
        Assert.Equal(0, attributes);
        Assert.Equal(0, classTags);
        Assert.Equal(0x00C0, unclaimed);
    }

    [Fact]
    public void SplitMask_GroupsAddBackUpForVanillaValues()
    {
        // 1056 = 1024 + 32 and 4224 = 4096 + 128 are real vanilla masks.
        foreach (int value in new[] { 0, 1, 4, 17, 56, 1056, 4224 })
        {
            (int attributes, int classTags, int unclaimed) = EncounterUnitFields.SplitMask(value);
            Assert.Equal(value, attributes | classTags | unclaimed);
        }
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
