using System;
using System.IO;
using System.Linq;
using ModdingGUI.Core.Api;
using ModdingGUI.Core.Randomizer;
using Xunit;

namespace ModdingGUI.Tests.Core.Randomizer;

/// <summary>
/// Regressions from the Phase 6 QA pass: prop teams must stay untouched, count-0 rows must never become units,
/// and no unit may be handed an item set that equips nothing.
/// </summary>
public sealed class RandomizerQaRegressionTests : IDisposable
{
    private readonly string _project;
    private readonly string _bec;

    public RandomizerQaRegressionTests()
    {
        _project = Path.Combine(Path.GetTempPath(), "ModdingGUI.QaReg-" + Guid.NewGuid().ToString("N"));
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

        // One encounter with a prop team (team -1, count 0) and one normal enemy team.
        string source = Path.Combine(CleanBecCorpus.Root!, "data", "encounters");
        string destination = Path.Combine(_bec, "data", "encounters");
        Directory.CreateDirectory(destination);

        foreach (string name in new[] { "battle1.enc", "expansedunesencounter01.enc" })
        {
            string? file = Directory.EnumerateFiles(source, "*.enc", SearchOption.AllDirectories)
                .FirstOrDefault(path => Path.GetFileName(path).Equals(name, StringComparison.OrdinalIgnoreCase));
            if (file is not null) File.Copy(file, Path.Combine(destination, name));
        }
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_project)) Directory.Delete(_project, recursive: true); }
        catch (IOException) { /* best effort */ }
    }

    private bool Ready =>
        File.Exists(Path.Combine(_bec, "data", "encounters", "battle1.enc")) &&
        File.Exists(Path.Combine(_bec, "data", "units", "gladiators.txt"));

    private string Battle => Path.Combine(_bec, "data", "encounters", "battle1.enc");

    [Fact]
    public void PropTeamsAreLeftExactlyAsTheyWere()
    {
        if (!Ready) return;

        string before = File.ReadAllText(Battle);
        new EncounterRandomizer().Apply(_bec, randomizeEnemies: true, permaDeath: false);
        string after = File.ReadAllText(Battle);

        // The third TEAM: block in this encounter is the prop team; it must come through untouched,
        // including its SCHOOL: line and the two count-0 rows.
        static string PropSection(string text)
        {
            var lines = text.Split('\n');
            int seen = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                if (!lines[i].TrimStart().StartsWith("TEAM:", StringComparison.OrdinalIgnoreCase)) continue;
                if (++seen == 3) return string.Join('\n', lines[i..]);
            }
            return string.Empty;
        }

        Assert.Equal(PropSection(before), PropSection(after));
        Assert.Contains("PropSchool", PropSection(after));
    }

    [Fact]
    public void CountZeroRowsAreNeverTurnedIntoUnits()
    {
        if (!Ready) return;

        new EncounterRandomizer().Apply(_bec, randomizeEnemies: true, permaDeath: false);

        // Whatever else changed, no row with a 0 count may have gained a randomizable payload.
        foreach (string line in File.ReadAllLines(Battle))
        {
            string trimmed = line.Trim();
            if (!trimmed.StartsWith("UNITDB:", StringComparison.OrdinalIgnoreCase)) continue;
            if (!line.Contains("\"Boss\"")) continue;

            var second = trimmed["UNITDB:".Length..].Split(',')[1].Trim();
            Assert.NotEqual("0", second);
        }
    }

    [Fact]
    public void TheAffinityMapOnlyOffersSetsThatEquipSomething()
    {
        if (!Ready) return;

        var map = ItemSetAffinityMap.Build(_bec);
        var itemSets = new ItemSetsApi().GetAll(_bec).ToDictionary(set => set.Index);

        Assert.All(new[] { "Fire", "Earth", "Water", "Air", "Dark", "Light", "None" },
            affinity => Assert.All(map.CandidatesFor(affinity), index =>
                Assert.Contains(itemSets[index].Rows, row => row.MinLevel <= 1 && row.MaxLevel >= 1)));
    }

    [Fact]
    public void NoGladiatorEndsUpWithAnEmptyItemSet()
    {
        if (!Ready) return;

        new RandomizerService().Randomize(_bec, new RandomizerOptions { Seed = "sets", RandomizeItemsets = true });

        var sets = new ItemSetsApi().GetAll(_bec).ToDictionary(set => set.Index);
        foreach (var dto in new GladiatorsApi().GetAll(_bec))
        {
            Assert.Contains(sets[dto.ItemSet].Rows,
                row => row.MinLevel <= 1 && row.MaxLevel >= 1);
        }
    }

    [Fact]
    public void EveryTeamMemberCarriesGear()
    {
        if (!Ready) return;

        new RandomizerService().Randomize(_bec, new RandomizerOptions
        {
            Seed = "team-gear",
            RandomizeItemsets = true,
            RandomizeTeam = true,
        });

        var school = new SchoolsApi().Get(_bec, "data/school/valensimperia.tok")!;
        var team = school.Units.Where(unit => unit.Name.EndsWith("_random", StringComparison.Ordinal)).ToArray();

        Assert.NotEmpty(team);
        foreach (var unit in team)
        {
            string items = unit.Value("ITEMSCOMP")!;
            var worn = System.Text.RegularExpressions.Regex.Matches(items, "\"([^\"]*)\"")
                .Select(match => match.Groups[1].Value)
                .Where(name => name.Length > 0)
                .ToArray();

            Assert.True(worn.Length >= 1, $"{unit.Name} ({unit.ClassName}) has no gear: {items}");
        }
    }
}
