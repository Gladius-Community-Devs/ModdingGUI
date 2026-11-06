using System;
using System.IO;
using System.Linq;
using ModdingGUI.Core.Api;
using ModdingGUI.Core.Randomizer;
using Xunit;

namespace ModdingGUI.Tests.Core.Randomizer;

/// <summary>
/// Item sets are affinity-matched, copying the old <c>RandomizeItemsets</c>: a set's affinity is the affinity of
/// the last weapon it carries, a gladiator may use its own affinity plus the universal Light pool, and a unit
/// with no affinity may use anything. Equipping the wrong affinity is what stops a unit using affinity skills.
/// </summary>
public sealed class RandomizerItemSetTests : IDisposable
{
    private readonly string _project;
    private readonly string _bec;
    private readonly GladiatorsApi _gladiators = new();

    public RandomizerItemSetTests()
    {
        _project = Path.Combine(Path.GetTempPath(), "ModdingGUI.ReGear-" + Guid.NewGuid().ToString("N"));
        _bec = Path.Combine(_project, "proj_BEC");

        foreach (string relative in new[]
                 {
                     "data/units/gladiators.txt", "data/units/statsets.txt", "data/units/skillsets.txt",
                     "data/units/itemsets.txt", "data/units/schools.txt", "data/units/tints.txt",
                     "data/config/classdefs.tok", "data/config/items.tok",
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

    private string VanillaGladiators => Path.Combine(CleanBecCorpus.Root!, "data", "units", "gladiators.txt");
    private string GladiatorsPath => Path.Combine(_bec, "data", "units", "gladiators.txt");
    private string ItemSetsPath => Path.Combine(_bec, "data", "units", "itemsets.txt");

    private void Reset() => File.Copy(VanillaGladiators, GladiatorsPath, overwrite: true);

    [Fact]
    public void MapReadsAffinitiesFromItemWeapons()
    {
        if (!Ready) return;

        var map = ItemSetAffinityMap.Build(_bec);

        // 404 sets exist, but 73 carry no row covering level 1 (28 are completely empty), so only 331 can
        // equip anything and only those are offered.
        Assert.Equal(331, map.Count);
        Assert.Contains(map.CandidatesFor("Fire").ToList(), index => map.AffinityOf(index) == "Fire");
        Assert.All(map.CandidatesFor("Fire"), index =>
            Assert.True(map.AffinityOf(index) is "Fire" or "Light", $"set {index} is {map.AffinityOf(index)}"));
    }

    [Fact]
    public void UnitsWithNoAffinityMayUseAnything()
    {
        if (!Ready) return;

        var map = ItemSetAffinityMap.Build(_bec);
        Assert.Equal(331, map.CandidatesFor("None").Count);
        Assert.Equal(331, map.CandidatesFor(null).Count);
    }

    [Fact]
    public void SameSeed_RegearsTheSameWay()
    {
        if (!Ready) return;

        new RandomizerService().Randomize(_bec, new RandomizerOptions { Seed = "gear", RandomizeItemsets = true });
        var first = _gladiators.GetAll(_bec).ToDictionary(d => d.Name, d => d.ItemSet);

        Reset();
        new RandomizerService().Randomize(_bec, new RandomizerOptions { Seed = "gear", RandomizeItemsets = true });
        var second = _gladiators.GetAll(_bec).ToDictionary(d => d.Name, d => d.ItemSet);

        Assert.Equal(first, second);
    }

    [Fact]
    public void EveryUnitKeepsAnAffinityItCanActuallyUse()
    {
        if (!Ready) return;

        var map = ItemSetAffinityMap.Build(_bec);
        new RandomizerService().Randomize(_bec, new RandomizerOptions { Seed = "affinity", RandomizeItemsets = true });

        foreach (var dto in _gladiators.GetAll(_bec))
        {
            string affinity = map.AffinityOf(dto.ItemSet);
            bool usable = dto.Affinity is null or "None"
                          || affinity.Equals(dto.Affinity, StringComparison.OrdinalIgnoreCase)
                          || affinity.Equals("Light", StringComparison.OrdinalIgnoreCase);

            Assert.True(usable, $"{dto.Name} ({dto.Affinity}) got itemset {dto.ItemSet} ({affinity})");
        }
    }

    [Fact]
    public void TheItemSetTablesThemselvesAreNotTouched()
    {
        if (!Ready) return;

        byte[] before = File.ReadAllBytes(ItemSetsPath);
        new RandomizerService().Randomize(_bec, new RandomizerOptions { Seed = "tables", RandomizeItemsets = true });

        Assert.Equal(before, File.ReadAllBytes(ItemSetsPath));
    }

    [Fact]
    public void TheRunWritesOneBackupForTheWholeBatch()
    {
        if (!Ready) return;

        new RandomizerService().Randomize(_bec, new RandomizerOptions { Seed = "one", RandomizeItemsets = true });

        string history = Path.Combine(_bec, ".mod-history", "data", "units", "gladiators.txt");
        Assert.True(Directory.Exists(history));
        Assert.Single(Directory.GetFiles(history));
    }

    [Fact]
    public void TheResultStillValidates()
    {
        if (!Ready) return;

        new RandomizerService().Randomize(_bec, new RandomizerOptions { Seed = "valid", RandomizeItemsets = true });

        var warnings = _gladiators.Validate(_bec)
            .Where(i => i.Severity == GladiatorValidationSeverity.Warning)
            .ToArray();

        Assert.Empty(warnings);
    }

    [Fact]
    public void HeroesAreRegearedToo()
    {
        if (!Ready) return;

        new RandomizerService().Randomize(_bec, new RandomizerOptions { Seed = "heroes", RandomizeItemsets = true });

        Assert.All(StoryHeroes.Names, hero =>
            Assert.NotNull(_gladiators.Get(_bec, hero)));
    }

    [Fact]
    public void DisabledStep_WritesNothing()
    {
        if (!Ready) return;

        byte[] before = File.ReadAllBytes(GladiatorsPath);
        var result = new RandomizerService().Randomize(_bec,
            new RandomizerOptions { Seed = "off", RandomizeItemsets = false });

        Assert.Equal(before, File.ReadAllBytes(GladiatorsPath));
        Assert.Equal(0, result.ChangedRecords);
    }
}
