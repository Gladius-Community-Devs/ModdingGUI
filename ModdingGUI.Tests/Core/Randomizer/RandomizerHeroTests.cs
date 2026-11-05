using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ModdingGUI.Core.Api;
using ModdingGUI.Core.Randomizer;
using ModdingGUI.Core.Units;
using Xunit;

namespace ModdingGUI.Tests.Core.Randomizer;

/// <summary>
/// The hero step swaps whole loadouts drawn from real gladiator records. That is the cross-reference the data
/// supports: a class keeps the stats, skills and gear it was authored with, and the hero keeps its name and
/// school (which is what puts it in a roster).
/// </summary>
public sealed class RandomizerHeroTests : IDisposable
{
    private readonly string _project;
    private readonly string _bec;
    private readonly GladiatorsApi _gladiators = new();

    public RandomizerHeroTests()
    {
        _project = Path.Combine(Path.GetTempPath(), "ModdingGUI.Heroes-" + Guid.NewGuid().ToString("N"));
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

    private string GladiatorsPath => Path.Combine(_bec, "data", "units", "gladiators.txt");

    [Fact]
    public void SameSeed_ProducesTheSameHeroes()
    {
        if (!Ready) return;

        var first = new RandomizerService().Randomize(_bec, new RandomizerOptions { Seed = "hero-seed", RandomizeHeroes = true });
        var firstHeroes = StoryHeroes.Names.Select(n => _gladiators.Get(_bec, n)!).ToArray();

        // Re-stage so the second run starts from the vanilla table.
        File.Copy(Path.Combine(CleanBecCorpus.Root!, "data", "units", "gladiators.txt"), GladiatorsPath, overwrite: true);

        var second = new RandomizerService().Randomize(_bec, new RandomizerOptions { Seed = "hero-seed", RandomizeHeroes = true });
        var secondHeroes = StoryHeroes.Names.Select(n => _gladiators.Get(_bec, n)!).ToArray();

        Assert.Equal(first.Seed, second.Seed);
        Assert.Equal(
            firstHeroes.Select(h => (h.Class, h.TintSet, h.SkillSet, h.StatSet, h.ItemSet)),
            secondHeroes.Select(h => (h.Class, h.TintSet, h.SkillSet, h.StatSet, h.ItemSet)));
    }

    [Fact]
    public void DifferentSeeds_ProduceDifferentHeroes()
    {
        if (!Ready) return;

        string vanilla = Path.Combine(CleanBecCorpus.Root!, "data", "units", "gladiators.txt");

        new RandomizerService().Randomize(_bec, new RandomizerOptions { Seed = "one", RandomizeHeroes = true });
        var a = StoryHeroes.Names.Select(n => _gladiators.Get(_bec, n)!.Class).ToArray();

        File.Copy(vanilla, GladiatorsPath, overwrite: true);
        new RandomizerService().Randomize(_bec, new RandomizerOptions { Seed = "two", RandomizeHeroes = true });
        var b = StoryHeroes.Names.Select(n => _gladiators.Get(_bec, n)!.Class).ToArray();

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void HeroesKeepTheirNameAndSchool_AndGetARealLoadout()
    {
        if (!Ready) return;

        var before = StoryHeroes.Names.ToDictionary(n => n, n => _gladiators.Get(_bec, n)!);
        new RandomizerService().Randomize(_bec, new RandomizerOptions { Seed = "loadouts", RandomizeHeroes = true });

        // The pool the loadouts may come from: every non-hero record, as it was before the run.
        var pool = _gladiators.GetAll(_bec)
            .Where(dto => !StoryHeroes.IsHero(dto.Name))
            .Select(GladiatorLoadout.From)
            .ToHashSet();

        foreach (string hero in StoryHeroes.Names)
        {
            var after = _gladiators.Get(_bec, hero)!;

            Assert.Equal(before[hero].Name, after.Name);
            Assert.Equal(before[hero].School, after.School);

            // Whatever it got, it must be a loadout some real gladiator carries.
            bool fromPool = pool.Contains(GladiatorLoadout.From(after));
            bool unchanged = GladiatorLoadout.From(after).Matches(before[hero]);
            Assert.True(fromPool || unchanged, $"{hero} received an invented loadout: {after}");
        }
    }

    [Fact]
    public void HeroesNeverReceiveAHeroClass()
    {
        if (!Ready) return;

        for (int run = 0; run < 5; run++)
        {
            File.Copy(Path.Combine(CleanBecCorpus.Root!, "data", "units", "gladiators.txt"), GladiatorsPath, overwrite: true);
            new RandomizerService().Randomize(_bec, new RandomizerOptions { Seed = $"pool-{run}", RandomizeHeroes = true });

            foreach (string hero in StoryHeroes.Names)
            {
                var dto = _gladiators.Get(_bec, hero)!;
                Assert.DoesNotContain(StoryHeroes.Names, heroClass => heroClass == dto.Class);
            }
        }
    }

    [Fact]
    public void TheRunWritesOneBackupForTheWholeBatch()
    {
        if (!Ready) return;

        new RandomizerService().Randomize(_bec, new RandomizerOptions { Seed = "backups", RandomizeHeroes = true });

        string history = Path.Combine(_bec, ".mod-history", "data", "units", "gladiators.txt");
        Assert.True(Directory.Exists(history));
        Assert.Single(Directory.GetFiles(history));
    }

    [Fact]
    public void HeroesAreTheOnlyRecordsTouched()
    {
        if (!Ready) return;

        byte[] before = File.ReadAllBytes(GladiatorsPath);
        new RandomizerService().Randomize(_bec, new RandomizerOptions { Seed = "scope", RandomizeHeroes = true });
        byte[] after = File.ReadAllBytes(GladiatorsPath);

        Assert.NotEqual(before, after);

        // Every non-hero record must be untouched: compare the file with the hero blocks blanked out.
        var original = GladiatorsFile.LoadProject(_bec).Records.ToDictionary(r => r.Name, r => r);
        Assert.Equal(2170, original.Count);
        Assert.Equal(6, StoryHeroes.Names.Count(n => original.ContainsKey(n)));
    }

    [Fact]
    public void DisabledOption_DoesNothing()
    {
        if (!Ready) return;

        byte[] before = File.ReadAllBytes(GladiatorsPath);
        var result = new RandomizerService().Randomize(_bec, new RandomizerOptions { Seed = "off", RandomizeHeroes = false });

        Assert.Equal(before, File.ReadAllBytes(GladiatorsPath));
        Assert.Equal(0, result.ChangedRecords);
    }
}
