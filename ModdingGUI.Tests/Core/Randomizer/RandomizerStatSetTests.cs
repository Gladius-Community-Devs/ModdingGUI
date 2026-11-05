using System;
using System.IO;
using System.Linq;
using ModdingGUI.Core.Api;
using ModdingGUI.Core.Randomizer;
using ModdingGUI.Core.Units;
using Xunit;

namespace ModdingGUI.Tests.Core.Randomizer;

/// <summary>
/// The statset step copies what the old randomizer did: every gladiator (heroes included) points at a random
/// stat set index. Only <c>gladiators.txt</c> changes; the tables themselves are untouched.
/// </summary>
public sealed class RandomizerStatSetTests : IDisposable
{
    private readonly string _project;
    private readonly string _bec;
    private readonly GladiatorsApi _gladiators = new();

    public RandomizerStatSetTests()
    {
        _project = Path.Combine(Path.GetTempPath(), "ModdingGUI.ReStat-" + Guid.NewGuid().ToString("N"));
        _bec = Path.Combine(_project, "proj_BEC");

        foreach (string relative in new[]
                 {
                     "data/units/gladiators.txt", "data/units/statsets.txt", "data/units/skillsets.txt",
                     "data/units/itemsets.txt", "data/units/schools.txt", "data/units/tints.txt",
                     "data/config/classdefs.tok",
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
    private string StatSetsPath => Path.Combine(_bec, "data", "units", "statsets.txt");

    private void Reset() => File.Copy(VanillaGladiators, GladiatorsPath, overwrite: true);

    [Fact]
    public void SameSeed_ReassignsTheSameWay()
    {
        if (!Ready) return;

        new RandomizerService().Randomize(_bec, new RandomizerOptions { Seed = "re-stat", RandomizeStatsets = true });
        var first = _gladiators.GetAll(_bec).ToDictionary(d => d.Name, d => d.StatSet);

        Reset();
        new RandomizerService().Randomize(_bec, new RandomizerOptions { Seed = "re-stat", RandomizeStatsets = true });
        var second = _gladiators.GetAll(_bec).ToDictionary(d => d.Name, d => d.StatSet);

        Assert.Equal(first, second);
    }

    [Fact]
    public void EveryRecord_EndsUpWithAnExistingStatSet()
    {
        if (!Ready) return;

        var catalog = UnitCatalogLoader.Load(_bec);
        new RandomizerService().Randomize(_bec, new RandomizerOptions { Seed = "bounds", RandomizeStatsets = true });

        var records = _gladiators.GetAll(_bec);
        Assert.Equal(2170, records.Count);
        Assert.All(records, dto => Assert.InRange(dto.StatSet, 0, catalog.StatSets - 1));
    }

    [Fact]
    public void HeroesAreRestattedToo()
    {
        if (!Ready) return;

        var before = StoryHeroes.Names.ToDictionary(n => n, n => _gladiators.Get(_bec, n)!.StatSet);
        new RandomizerService().Randomize(_bec, new RandomizerOptions { Seed = "heroes-too", RandomizeStatsets = true });
        var after = StoryHeroes.Names.ToDictionary(n => n, n => _gladiators.Get(_bec, n)!.StatSet);

        // Any single hero could keep its index by chance; across six of them at least one must move.
        Assert.True(before.Any(kv => after[kv.Key] != kv.Value),
            "no hero's stat set changed in a whole-corpus reassignment");
    }

    [Fact]
    public void TheStatSetTablesThemselvesAreNotTouched()
    {
        if (!Ready) return;

        byte[] before = File.ReadAllBytes(StatSetsPath);
        new RandomizerService().Randomize(_bec, new RandomizerOptions { Seed = "tables", RandomizeStatsets = true });

        Assert.Equal(before, File.ReadAllBytes(StatSetsPath));
    }

    [Fact]
    public void TheRunWritesOneBackupForTheWholeBatch()
    {
        if (!Ready) return;

        new RandomizerService().Randomize(_bec, new RandomizerOptions { Seed = "one-backup", RandomizeStatsets = true });

        string history = Path.Combine(_bec, ".mod-history", "data", "units", "gladiators.txt");
        Assert.True(Directory.Exists(history));
        Assert.Single(Directory.GetFiles(history));
    }

    [Fact]
    public void TheResultIsStillValid()
    {
        if (!Ready) return;

        new RandomizerService().Randomize(_bec, new RandomizerOptions { Seed = "valid", RandomizeStatsets = true });

        var warnings = _gladiators.Validate(_bec)
            .Where(i => i.Severity == GladiatorValidationSeverity.Warning)
            .ToArray();

        Assert.Empty(warnings);
    }

    [Fact]
    public void DisabledStep_LeavesTheTableAlone()
    {
        if (!Ready) return;

        byte[] before = File.ReadAllBytes(GladiatorsPath);
        var result = new RandomizerService().Randomize(_bec,
            new RandomizerOptions { Seed = "off", RandomizeStatsets = false });

        Assert.Equal(before, File.ReadAllBytes(GladiatorsPath));
        Assert.Equal(0, result.ChangedRecords);
    }

    [Fact]
    public void Result_ReportsTheSeedAndSteps()
    {
        if (!Ready) return;

        var result = new RandomizerService().Randomize(_bec,
            new RandomizerOptions { Seed = "reported", RandomizeStatsets = true });

        Assert.Equal("reported", result.Seed);
        Assert.Contains("statsets", result.Steps);
        Assert.True(result.ChangedRecords > 0);
    }
}
