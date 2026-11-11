using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ModdingGUI.Core.Api;
using Xunit;

namespace ModdingGUI.Tests.Core.Api;

/// <summary>
/// The typed <c>UNITDB</c> record. The 21-slot order comes from <c>EncounterSetup</c> in the DOL
/// (<c>case 0x1D</c>: string, int, string, int×5, string×4, int×12), so these tests pin that
/// order and the round-trip behaviour of editing it.
/// </summary>
public sealed class EncounterUnitTests
{
    // Straight out of the vanilla corpus (sx_arenacert6, team 0, first unit).
    private const string HeroRow =
        "\"HERO\", 100, \"Start1\", 0, 0, 0, 0, -1, \"\", \"\", \"\", \"\", 0, 0, 0, 0, 1, 0, 0, 0, 0";

    // A school-generated enemy that the engine may replace with a random unit.
    private const string EnemyRow =
        "\"Bjolf\", 100, \"Start3\", 1, 1, 0, 0, -1, \"\", \"\", \"\", \"\", 0, 0, 0, 0, 3, 0, 0, 0, 0";

    [Fact]
    public void TryParse_ReadsEverySlotInEngineOrder()
    {
        var unit = EncounterUnit.TryParse(EnemyRow);

        Assert.NotNull(unit);
        Assert.Equal(EncounterUnit.SlotCount, unit!.Slots.Count);
        Assert.Equal("Bjolf", unit.UnitName);
        Assert.Equal(100, unit.SpawnChancePercent);
        Assert.Equal("Start3", unit.StartSlot);
        Assert.Equal(1, unit.Slot4LevelMinOffset);
        Assert.Equal(1, unit.Slot5LevelMaxOffset);
        Assert.Equal(0, unit.Slot6LevelJitter);
        Assert.Equal(0, unit.AttributeRequirement);
        Assert.Equal(-1, unit.AffinityRequirement);
        Assert.Empty(unit.ClassTags);
        Assert.Equal(3, unit.Slot17);
        Assert.Equal(0, unit.Flag100);
        Assert.Equal(0, unit.Flag200);
    }

    [Fact]
    public void TryParse_ReadsClassTags()
    {
        var row = "\"X\", 50, \"Start2\", 0, 0, 0, 0, -1, \"UndeadCaster\", \"DarkCat\", \"\", \"\", " +
                  "0, 0, 0, 0, 0, 0, 0, 0, 0";

        var unit = EncounterUnit.TryParse(row);

        Assert.NotNull(unit);
        Assert.Equal(new[] { "UndeadCaster", "DarkCat" }, unit!.ClassTags);
    }

    [Theory]
    [InlineData("")]                                                   // nothing
    [InlineData("\"HERO\", 100, \"Start1\"")]                           // far too short
    [InlineData("\"HERO\", 100, \"Start1\", 0, 0, 0, 0, -1, \"\", \"\", \"\", \"\", 0, 0, 0, 0, 1, 0, 0, 0")] // 20
    [InlineData("\"HERO\", \"lots\", \"Start1\", 0, 0, 0, 0, -1, \"\", \"\", \"\", \"\", 0, 0, 0, 0, 1, 0, 0, 0, 0")]
    public void TryParse_RejectsWrongShapeOrType(string row)
    {
        Assert.Null(EncounterUnit.TryParse(row));
    }

    [Fact]
    public void ParseThenWrite_UntouchedRow_IsIdentical()
    {
        var unit = EncounterUnit.TryParse(HeroRow)!;

        Assert.Equal(HeroRow, unit.ToRaw());
    }

    [Fact]
    public void WithSpawnChance_ChangesOnlyThatSlot()
    {
        var unit = EncounterUnit.TryParse(EnemyRow)!.WithSpawnChance(40);

        Assert.Equal(40, unit.SpawnChancePercent);
        var reparsed = EncounterUnit.TryParse(unit.ToRaw());
        Assert.NotNull(reparsed);
        Assert.Equal(40, reparsed!.SpawnChancePercent);
        Assert.Equal("Bjolf", reparsed.UnitName);
        Assert.Equal("Start3", reparsed.StartSlot);
        Assert.Equal(3, reparsed.Slot17);
    }

    [Fact]
    public void WithStartSlot_ChangesOnlyThatSlot()
    {
        var unit = EncounterUnit.TryParse(HeroRow)!.WithStartSlot("Start8");

        var reparsed = EncounterUnit.TryParse(unit.ToRaw())!;
        Assert.Equal("Start8", reparsed.StartSlot);
        Assert.Equal(100, reparsed.SpawnChancePercent);
        Assert.Equal("HERO", reparsed.UnitName);
    }
}

/// <summary>
/// Typed-unit editing and validation against a staged corpus tree.
/// </summary>
public sealed class EncounterUnitApiTests : IDisposable
{
    private const string Sample = "data/encounters/nordagh/thefen/sx_arenacert6.enc";

    private readonly string _projectDir;

    public EncounterUnitApiTests()
    {
        _projectDir = Path.Combine(Path.GetTempPath(),
            "ModdingGUI.EncounterUnits-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_projectDir);
        if (!CleanBecCorpus.IsAvailable) return;

        string source = Path.Combine(CleanBecCorpus.Root!, "data", "encounters");
        foreach (string file in Directory.EnumerateFiles(source, "*.enc", SearchOption.AllDirectories))
        {
            string destination = Path.Combine(_projectDir, "data", "encounters",
                Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination, overwrite: true);
        }
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_projectDir)) Directory.Delete(_projectDir, recursive: true); }
        catch (IOException) { /* best effort */ }
    }

    private static bool Ready => CleanBecCorpus.IsAvailable;

    private string FilePath(string relative) =>
        Path.Combine(_projectDir, relative.Replace('/', Path.DirectorySeparatorChar));

    [Fact]
    public void Get_ParsesEveryUnitsSlots()
    {
        if (!Ready) return;

        var team = new EncountersApi().Get(_projectDir, Sample)!.Team(1)!;

        Assert.Equal(3, team.Units.Count);
        Assert.All(team.ParsedUnits, u => Assert.NotNull(u));
        Assert.Equal("Bjolf", team.Unit(0)!.UnitName);
        Assert.Equal("Start3", team.Unit(0)!.StartSlot);
        Assert.All(team.ParsedUnits, u => Assert.Equal(100, u!.SpawnChancePercent));
    }

    [Fact]
    public void SetTeamUnit_NoOp_IsByteIdentical()
    {
        if (!Ready) return;

        string path = FilePath(Sample);
        byte[] original = File.ReadAllBytes(path);
        var api = new EncountersApi();

        var unit = api.Get(_projectDir, Sample)!.Team(1)!.Unit(0)!;
        api.SetTeamUnit(_projectDir, Sample, 1, 0, unit);

        Assert.Equal(original, File.ReadAllBytes(path));
    }

    [Fact]
    public void SetTeamUnit_ChangesOneRowOnly()
    {
        if (!Ready) return;

        var api = new EncountersApi();
        var before = api.Get(_projectDir, Sample)!.Team(1)!;
        string untouched = before.Units[1];

        api.SetTeamUnit(_projectDir, Sample, 1, 0, before.Unit(0)!.WithSpawnChance(25));

        var after = api.Get(_projectDir, Sample)!.Team(1)!;
        Assert.Equal(25, after.Unit(0)!.SpawnChancePercent);
        Assert.Equal("Bjolf", after.Unit(0)!.UnitName);
        Assert.Equal(untouched, after.Units[1]);
        Assert.Equal(before.Team1Count(), after.Team1Count());
    }

    [Fact]
    public void AddThenRemoveTeamUnit_RestoresTheTeam()
    {
        if (!Ready) return;

        var api = new EncountersApi();
        var before = api.Get(_projectDir, Sample)!.Team(1)!;
        var clone = before.Unit(0)!.WithSpawnChance(75).WithStartSlot("Start5");

        api.AddTeamUnit(_projectDir, Sample, 1, clone);
        var added = api.Get(_projectDir, Sample)!.Team(1)!;
        Assert.Equal(before.Units.Count + 1, added.Units.Count);
        Assert.Equal("Start5", added.Unit(added.Units.Count - 1)!.StartSlot);

        api.RemoveTeamUnit(_projectDir, Sample, 1, added.Units.Count - 1);
        var restored = api.Get(_projectDir, Sample)!.Team(1)!;
        Assert.Equal(before.Units, restored.Units);
    }

    [Fact]
    public void Validate_TypedUnitRules_FireOnBadData()
    {
        if (!Ready) return;

        var api = new EncountersApi();
        var team = api.Get(_projectDir, Sample)!.Team(1)!;

        // A negative spawn chance and an empty start slot (>=100 is legal: it always spawns).
        api.SetTeamUnit(_projectDir, Sample, 1, 0, team.Unit(0)!.WithSpawnChance(-5).WithStartSlot(""));

        var issues = api.Validate(_projectDir, Sample);
        Assert.Contains(issues, i => i.Code == EncounterValidationCodes.UnitSpawnChance);
        Assert.Contains(issues, i => i.Code == EncounterValidationCodes.UnitStartSlot);
    }

    [Fact]
    public void Validate_EveryVanillaEncounter_HasNoWarnings()
    {
        if (!Ready) return;

        var api = new EncountersApi();
        var catalog = EncountersCatalog.Shipped();
        Assert.NotNull(catalog);

        var warnings = new List<EncounterValidationIssue>();
        foreach (string relative in api.ListFiles(_projectDir))
        {
            var encounter = api.Get(_projectDir, relative);
            if (encounter is null) continue;
            warnings.AddRange(EncounterValidator.Validate(encounter, catalog)
                .Where(i => i.Severity == EncounterValidationSeverity.Warning));
        }

        Assert.True(warnings.Count == 0, string.Join("\n  ",
            warnings.Take(10).Select(w => $"{w.Code} {w.Site}/{w.Key}: {w.Message}")));
    }

    [Fact]
    public void Catalog_KnowsTheEngineKeysAndTheirIds()
    {
        var catalog = EncountersCatalog.Shipped();

        Assert.NotNull(catalog);
        Assert.Equal(21, catalog!.UnitArgumentCount);
        Assert.Equal(0x1D, catalog.EngineKeys["UNITDB"]);
        Assert.Equal(0x19, catalog.EngineKeys["TEAM"]);
        Assert.Contains("VOICEINTRO", catalog.EngineOnlyKeys);
        Assert.Contains("FORCEPLACEMENT", catalog.EngineOnlyKeys);
        Assert.Contains("BLOCK", catalog.EngineOnlyKeys);
    }
}

internal static class EncounterTeamTestExtensions
{
    /// <summary>Count of units on the team a parsed row belongs to (helper for the test above).</summary>
    internal static int Team1Count(this EncounterTeamDto team) => team.Units.Count;
}
