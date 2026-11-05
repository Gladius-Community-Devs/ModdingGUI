using System;
using System.IO;
using System.Linq;
using ModdingGUI.Core.Api;
using ModdingGUI.Core.Randomizer;
using Xunit;

namespace ModdingGUI.Tests.Core.Randomizer;

/// <summary>
/// A hero's table entry is not what the campaign starts from: the starting units are the <c>CREATEUNIT</c> blocks
/// in the roster files, which spell out class, stats, items and skills. Randomizing the table alone left Valens
/// and Ludo walking into the first bout with the vanilla kit.
/// </summary>
public sealed class RandomizerRosterHeroTests : IDisposable
{
    private readonly string _project;
    private readonly string _bec;
    private readonly SchoolsApi _schools = new();
    private readonly GladiatorsApi _gladiators = new();

    private const string ValensRoster = "data/school/valensimperia.tok";
    private const string UrsulaRoster = "data/school/ursulanordagh.tok";

    public RandomizerRosterHeroTests()
    {
        _project = Path.Combine(Path.GetTempPath(), "ModdingGUI.RosterHero-" + Guid.NewGuid().ToString("N"));
        _bec = Path.Combine(_project, "proj_BEC");

        foreach (string relative in new[]
                 {
                     "data/units/gladiators.txt", "data/units/statsets.txt", "data/units/skillsets.txt",
                     "data/units/itemsets.txt", "data/units/schools.txt", "data/units/tints.txt",
                     "data/config/classdefs.tok", "data/config/items.tok",
                     ValensRoster, UrsulaRoster,
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

    private SchoolUnitDto Unit(string roster, string name) =>
        _schools.Get(_bec, roster)!.Units.First(u => u.Name == name);

    [Fact]
    public void VanillaRosterShipsFourHeroes()
    {
        if (!Ready) return;

        Assert.Equal(new[] { "Valens", "Ludo" },
            _schools.Get(_bec, ValensRoster)!.Units.Select(u => u.Name).ToArray());
        Assert.Equal(new[] { "Urlan", "Ursula" },
            _schools.Get(_bec, UrsulaRoster)!.Units.Select(u => u.Name).ToArray());
    }

    [Fact]
    public void RosterHeroesFollowTheirRandomizedClass()
    {
        if (!Ready) return;

        new RandomizerService().Randomize(_bec, new RandomizerOptions { Seed = "roster-heroes", RandomizeHeroes = true });

        foreach (var (roster, hero) in new[] { (ValensRoster, "Valens"), (ValensRoster, "Ludo"),
                                              (UrsulaRoster, "Urlan"), (UrsulaRoster, "Ursula") })
        {
            var table = _gladiators.Get(_bec, hero)!;
            var block = Unit(roster, hero);

            Assert.Equal(table.Class, block.ClassName);
            Assert.NotEqual(hero, block.ClassName); // the six bespoke hero classes are no longer 1:1 with names
        }
    }

    [Fact]
    public void RosterHeroesGetTheNewKit()
    {
        if (!Ready) return;

        new RandomizerService().Randomize(_bec, new RandomizerOptions { Seed = "kit", RandomizeHeroes = true });

        foreach (var (roster, hero) in new[] { (ValensRoster, "Valens"), (UrsulaRoster, "Ursula") })
        {
            var block = Unit(roster, hero);
            var table = _gladiators.Get(_bec, hero)!;

            // Six stats, because a short line makes the engine drop the rest of the block.
            Assert.Equal(6, block.Value("CORESTATSCOMP2")!.Split(',').Length);

            // The block's stats are the loadout's level-1 stat row, not the vanilla hand-tuned values.
            var row = new StatSetsApi().Get(_bec, table.StatSet)!.Row(1)!;
            Assert.StartsWith($"{row.Con}, {row.Pow}", block.Value("CORESTATSCOMP2")!);

            // Level, experience and outfit are preserved.
            Assert.Equal("1", block.Value("LEVEL"));
            Assert.NotNull(block.Value("CUSTOMIZE"));

            // Skills come from the new skill set, filtered to what level 1 may actually learn.
            var expected = new SkillSetsApi().Get(_bec, table.SkillSet)!.Rows
                .Where(r => r.MinLevel <= 1 && 1 <= r.MaxLevel)
                .Select(r => r.SkillName)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .ToArray();
            Assert.Equal(expected, block.Values("SKILL").Select(v => v.Trim('"')).ToArray());
        }
    }

    [Fact]
    public void BothRostersListAllSixHeroes()
    {
        if (!Ready) return;

        new RandomizerService().Randomize(_bec, new RandomizerOptions { Seed = "six", RandomizeHeroes = true });

        var expected = new[] { "Valens", "Ursula", "Ludo", "Urlan", "Eiji", "Gwazi" };
        foreach (string roster in new[] { ValensRoster, UrsulaRoster })
        {
            var names = _schools.Get(_bec, roster)!.Units.Select(u => u.Name).ToArray();
            Assert.Equal(expected, names);
        }
    }

    [Fact]
    public void DefaultGoldMatchesTheOriginal()
    {
        if (!Ready) return;

        new RandomizerService().Randomize(_bec, new RandomizerOptions { Seed = "gold", RandomizeHeroes = true });

        Assert.Equal(15000, _schools.Get(_bec, ValensRoster)!.Gold);
    }

    [Fact]
    public void FortyGladiatorsUsesTheHigherGold()
    {
        if (!Ready) return;

        new RandomizerService().Randomize(_bec,
            new RandomizerOptions { Seed = "gold40", RandomizeHeroes = true, FortyGladiators = true });

        Assert.Equal(75000, _schools.Get(_bec, ValensRoster)!.Gold);
    }

    [Fact]
    public void FixedCashIsWritten()
    {
        if (!Ready) return;

        new RandomizerService().Randomize(_bec,
            new RandomizerOptions { Seed = "cash", RandomizeHeroes = true, Cash = CashMode.Fixed, CashAmount = 123456 });

        Assert.Equal(123456, _schools.Get(_bec, ValensRoster)!.Gold);
    }

    [Fact]
    public void RandomCashStaysInRange()
    {
        if (!Ready) return;

        new RandomizerService().Randomize(_bec,
            new RandomizerOptions { Seed = "cashrandom", RandomizeHeroes = true, Cash = CashMode.Random });

        int gold = _schools.Get(_bec, ValensRoster)!.Gold ?? -1;
        Assert.InRange(gold, 0, 999_999);
    }

    [Fact]
    public void HeroProgressionIsCarriedOver()
    {
        if (!Ready) return;

        // Ludo starts with 450 experience in vanilla; a rewrite must not reset it.
        new RandomizerService().Randomize(_bec, new RandomizerOptions { Seed = "exp", RandomizeHeroes = true });

        Assert.Equal("450", Unit(ValensRoster, "Ludo").Value("EXPERIENCE"));
    }

    [Fact]
    public void TheHeroLineNamesTheChosenCampaign()
    {
        if (!Ready) return;

        new RandomizerService().Randomize(_bec,
            new RandomizerOptions { Seed = "hero", RandomizeHeroes = true, Hero = HeroChoice.Valens });

        Assert.Equal("Valens", _schools.Get(_bec, ValensRoster)!.Hero);
        Assert.Equal("Valens", _schools.Get(_bec, UrsulaRoster)!.Hero);
    }

    [Fact]
    public void EveryHeroBlockAsksForTheDefaultLook()
    {
        if (!Ready) return;

        new RandomizerService().Randomize(_bec, new RandomizerOptions { Seed = "outfit", RandomizeHeroes = true });

        foreach (string roster in new[] { ValensRoster, UrsulaRoster })
        {
            foreach (var unit in _schools.Get(_bec, roster)!.Units)
            {
                // CUSTOMIZE selects data/characters/<mesh>/<mesh>_varNN.ptx; a variant the new class does not ship
                // locks the game up, so every block written by the randomizer asks for the default (0).
                Assert.Equal("0, \"\"", unit.Value("CUSTOMIZE")!.Trim());
            }
        }
    }

    [Fact]
    public void TheSixHeroesGetDistinctClasses()
    {
        if (!Ready) return;

        // The original dealt classes off a shuffled list, so no two heroes shared one.
        new RandomizerService().Randomize(_bec, new RandomizerOptions { Seed = "distinct", RandomizeHeroes = true });

        var classes = _schools.Get(_bec, ValensRoster)!.Units
            .Select(unit => unit.ClassName)
            .ToArray();

        Assert.Equal(classes.Length, classes.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void TheSchoolNameComesFromTheOptions()
    {
        if (!Ready) return;

        new RandomizerService().Randomize(_bec, new RandomizerOptions
        {
            Seed = "named",
            RandomizeHeroes = true,
            SchoolName = "QA School",
        });

        Assert.Equal("QA School", _schools.Get(_bec, ValensRoster)!.Name);
        Assert.Equal("QA School", _schools.Get(_bec, UrsulaRoster)!.Name);
    }

    [Fact]
    public void RewrittenRostersStillValidate()
    {
        if (!Ready) return;

        new RandomizerService().Randomize(_bec, new RandomizerOptions { Seed = "valid", RandomizeHeroes = true });

        foreach (string roster in new[] { ValensRoster, UrsulaRoster })
        {
            var warnings = _schools.Validate(_bec, roster)
                .Where(i => i.Severity == SchoolValidationSeverity.Warning)
                .ToArray();

            Assert.Empty(warnings);
        }
    }

    [Fact]
    public void SameSeedRewritesTheRosterTheSameWay()
    {
        if (!Ready) return;

        new RandomizerService().Randomize(_bec, new RandomizerOptions { Seed = "repeat", RandomizeHeroes = true });
        var first = Unit(ValensRoster, "Valens");

        File.Copy(Path.Combine(CleanBecCorpus.Root!, ValensRoster.Replace('/', Path.DirectorySeparatorChar)),
            Path.Combine(_bec, ValensRoster.Replace('/', Path.DirectorySeparatorChar)), overwrite: true);
        File.Copy(Path.Combine(CleanBecCorpus.Root!, "data/units/gladiators.txt"),
            Path.Combine(_bec, "data/units/gladiators.txt"), overwrite: true);

        new RandomizerService().Randomize(_bec, new RandomizerOptions { Seed = "repeat", RandomizeHeroes = true });
        var second = Unit(ValensRoster, "Valens");

        Assert.Equal(first.ClassName, second.ClassName);
        Assert.Equal(first.Value("CORESTATSCOMP2"), second.Value("CORESTATSCOMP2"));
        Assert.Equal(first.Value("ITEMSCOMP"), second.Value("ITEMSCOMP"));
    }

    [Fact]
    public void DisabledHeroStepLeavesTheRosterAlone()
    {
        if (!Ready) return;

        string path = Path.Combine(_bec, ValensRoster.Replace('/', Path.DirectorySeparatorChar));
        byte[] before = File.ReadAllBytes(path);

        new RandomizerService().Randomize(_bec, new RandomizerOptions { Seed = "off", RandomizeHeroes = false });

        Assert.Equal(before, File.ReadAllBytes(path));
    }
}
