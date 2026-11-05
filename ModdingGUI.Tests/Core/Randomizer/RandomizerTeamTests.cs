using System;
using System.IO;
using System.Linq;
using ModdingGUI.Core.Api;
using ModdingGUI.Core.Randomizer;
using Xunit;

namespace ModdingGUI.Tests.Core.Randomizer;

/// <summary>
/// The team step appends a randomized roster to both campaign files, which is what the old
/// <c>RandomizeTeam</c> did: names are <c>&lt;gladiator&gt;_random</c>, classes come from the gladiator table, and
/// each block carries the class's stat, item and skill set contents so the unit is playable.
/// </summary>
public sealed class RandomizerTeamTests : IDisposable
{
    private readonly string _project;
    private readonly string _bec;
    private readonly SchoolsApi _schools = new();

    private static readonly string[] Rosters =
    {
        "data/school/valensimperia.tok",
        "data/school/ursulanordagh.tok",
    };

    public RandomizerTeamTests()
    {
        _project = Path.Combine(Path.GetTempPath(), "ModdingGUI.Team-" + Guid.NewGuid().ToString("N"));
        _bec = Path.Combine(_project, "proj_BEC");

        foreach (string relative in new[]
                 {
                     "data/units/gladiators.txt", "data/units/statsets.txt", "data/units/skillsets.txt",
                     "data/units/itemsets.txt", "data/units/schools.txt", "data/units/tints.txt",
                     "data/config/classdefs.tok", "data/config/items.tok",
                     "data/school/valensimperia.tok", "data/school/ursulanordagh.tok",
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

    [Fact]
    public void TheTeamIsAppendedToBothRosters()
    {
        if (!Ready) return;

        var before = Rosters.ToDictionary(r => r, r => _schools.Get(_bec, r)!.Units.Count);
        var result = new RandomizerService().Randomize(_bec, new RandomizerOptions { Seed = "team", RandomizeTeam = true });

        Assert.Contains("team", result.Steps);
        foreach (string roster in Rosters)
        {
            Assert.Equal(before[roster] + 14, _schools.Get(_bec, roster)!.Units.Count);
        }

        Assert.Equal(28, result.ChangedRecords);
    }

    [Fact]
    public void TeamMembersArePlayableUnits()
    {
        if (!Ready) return;

        new RandomizerService().Randomize(_bec, new RandomizerOptions { Seed = "playable", RandomizeTeam = true });

        var units = _schools.Get(_bec, Rosters[0])!.Units.Where(u => u.Name.EndsWith("_random")).ToArray();
        Assert.Equal(14, units.Length);
        Assert.Equal(14, units.Select(u => u.Name).Distinct(StringComparer.Ordinal).Count());

        foreach (var unit in units)
        {
            Assert.False(string.IsNullOrWhiteSpace(unit.ClassName));
            Assert.Equal("1", unit.Value("LEVEL"));
            Assert.NotNull(unit.Value("CORESTATSCOMP2"));
            Assert.NotNull(unit.Value("ITEMSCOMP"));

            // Six stats: the engine's loader reads six and stops parsing the block when one is missing, which
            // is what silently dropped every ITEMSCOMP line before this was fixed.
            Assert.Equal(6, unit.Value("CORESTATSCOMP2")!.Split(',').Length);

            // The whole block must parse. Before the arity fix the loader stopped at the short stats line and
            // every slot came out empty, so this asserts real gear survived the round trip.
            var equipped = System.Text.RegularExpressions.Regex
                .Matches(unit.Value("ITEMSCOMP")!, "\"([^\"]*)\"")
                .Select(m => m.Groups[1].Value)
                .Count(name => !string.IsNullOrWhiteSpace(name));
            Assert.True(equipped >= 3, $"only {equipped} of 5 slots equipped");

            // Five quoted item names: count quote pairs rather than commas, which also separate nothing here.
            string items = unit.Value("ITEMSCOMP")!;
            Assert.Equal(5, System.Text.RegularExpressions.Regex.Matches(items, "\"").Count / 2);
        }
    }

    [Fact]
    public void SameSeed_AppendsTheSameTeam()
    {
        if (!Ready) return;

        new RandomizerService().Randomize(_bec, new RandomizerOptions { Seed = "repeat", RandomizeTeam = true });
        var first = _schools.Get(_bec, Rosters[0])!.Units
            .Where(u => u.Name.EndsWith("_random"))
            .Select(u => (u.Name, u.ClassName))
            .ToArray();

        // Fresh project, same seed.
        var other = Path.Combine(Path.GetTempPath(), "ModdingGUI.Team2-" + Guid.NewGuid().ToString("N"));
        var bec2 = Path.Combine(other, "proj_BEC");
        CopyProject(bec2);

        new RandomizerService().Randomize(bec2, new RandomizerOptions { Seed = "repeat", RandomizeTeam = true });
        var second = new SchoolsApi().Get(bec2, Rosters[0])!.Units
            .Where(u => u.Name.EndsWith("_random"))
            .Select(u => (u.Name, u.ClassName))
            .ToArray();

        Assert.Equal(first, second);
        Directory.Delete(other, recursive: true);
    }

    [Fact]
    public void FortyGladiators_AppendsThirtyFour()
    {
        if (!Ready) return;

        new RandomizerService().Randomize(_bec,
            new RandomizerOptions { Seed = "forty", RandomizeTeam = true, FortyGladiators = true });

        var units = _schools.Get(_bec, Rosters[0])!.Units.Where(u => u.Name.EndsWith("_random")).ToArray();
        Assert.Equal(34, units.Length);
    }

    [Fact]
    public void WeightedClasses_AvoidsRepeatingABaseClassEarly()
    {
        if (!Ready) return;

        new RandomizerService().Randomize(_bec,
            new RandomizerOptions { Seed = "weighted", RandomizeTeam = true, WeightedClasses = true });

        var classes = _schools.Get(_bec, Rosters[0])!.Units
            .Where(u => u.Name.EndsWith("_random"))
            .Select(u => RandomizerService.BaseClass(u.ClassName))
            .ToArray();

        // Fourteen slots, and the first pass takes one per base class, so a team of 14 should show at least a
        // handful of distinct bases (vanilla has dozens of base classes available).
        Assert.True(classes.Distinct(StringComparer.Ordinal).Count() >= 6,
            $"only {classes.Distinct(StringComparer.Ordinal).Count()} distinct base classes");
    }

    [Fact]
    public void EachRosterGetsOneBackup()
    {
        if (!Ready) return;

        new RandomizerService().Randomize(_bec, new RandomizerOptions { Seed = "backup", RandomizeTeam = true });

        string history = Path.Combine(_bec, ".mod-history", "data", "school");
        Assert.True(Directory.Exists(history));
        foreach (string roster in Rosters)
        {
            string dir = Path.Combine(history, Path.GetFileName(roster));
            Assert.True(Directory.Exists(dir), $"no backup for {roster}");
            Assert.Single(Directory.GetFiles(dir));
        }
    }

    [Fact]
    public void AppendedUnitsPassSchoolValidation()
    {
        if (!Ready) return;

        new RandomizerService().Randomize(_bec, new RandomizerOptions { Seed = "validated", RandomizeTeam = true });

        var warnings = _schools.Validate(_bec, Rosters[0])
            .Where(i => i.Severity == SchoolValidationSeverity.Warning)
            .ToArray();

        Assert.Empty(warnings);
    }

    [Fact]
    public void DisabledStep_AppendsNothing()
    {
        if (!Ready) return;

        int before = _schools.Get(_bec, Rosters[0])!.Units.Count;
        var result = new RandomizerService().Randomize(_bec, new RandomizerOptions { Seed = "off", RandomizeTeam = false });

        Assert.Equal(before, _schools.Get(_bec, Rosters[0])!.Units.Count);
        Assert.Equal(0, result.ChangedRecords);
    }

    private void CopyProject(string targetBec)
    {
        foreach (string relative in new[]
                 {
                     "data/units/gladiators.txt", "data/units/statsets.txt", "data/units/skillsets.txt",
                     "data/units/itemsets.txt", "data/units/schools.txt", "data/units/tints.txt",
                     "data/config/classdefs.tok", "data/config/items.tok",
                     "data/school/valensimperia.tok", "data/school/ursulanordagh.tok",
                 })
        {
            string target = Path.Combine(targetBec, relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(Path.Combine(CleanBecCorpus.Root!, relative.Replace('/', Path.DirectorySeparatorChar)), target);
        }
    }
}
