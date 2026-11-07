using System;
using System.IO;
using System.Linq;
using ModdingGUI.Core.Api;
using ModdingGUI.Core.Randomizer;
using Xunit;

namespace ModdingGUI.Tests.Core.Randomizer;

/// <summary>
/// The old flow ran the encounter and script steps on every pass, not only when their own options were ticked:
/// the player's team slots have to be opened for the randomized school to fight, and the campaign scripts always
/// carry the hero branches. Pinning that parity, plus the base-class collapse the weighted mode relies on.
/// </summary>
public sealed class RandomizerFlowParityTests : IDisposable
{
    private readonly string _project;
    private readonly string _bec;

    public RandomizerFlowParityTests()
    {
        _project = Path.Combine(Path.GetTempPath(), "ModdingGUI.Flow-" + Guid.NewGuid().ToString("N"));
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

        string source = Path.Combine(CleanBecCorpus.Root!, "data", "script");
        string scriptTarget = Path.Combine(_bec, "data", "script");
        Directory.CreateDirectory(Path.Combine(scriptTarget, "bin"));
        foreach (string name in new[]
                 {
                     "gameflowvalenssteppes.scp", "gameflowvalensexpanse.scp", "gameflowursulasteppes.scp",
                     "gameflowursulaexpanse.scp", "gameflowvalensendgame.scp", "gameflowursulaendgame.scp",
                 })
        {
            string file = Path.Combine(source, name);
            if (File.Exists(file)) File.Copy(file, Path.Combine(scriptTarget, name));
        }
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_project)) Directory.Delete(_project, recursive: true); }
        catch (IOException) { /* best effort */ }
    }

    private bool Ready => CleanBecCorpus.IsAvailable &&
                          File.Exists(Path.Combine(_bec, "data", "script", "gameflowvalenssteppes.scp"));

    [Fact]
    public void TheEncounterStepRunsEvenWhenOnlyStatsetsAreSelected()
    {
        if (!Ready) return;

        var result = new RandomizerService().Randomize(_bec,
            new RandomizerOptions { Seed = "enc-flow", RandomizeStatsets = true });

        Assert.Contains("encounters", result.Steps);
    }

    [Fact]
    public void TheScriptStepRunsEvenWhenHeroesAreNotSelected()
    {
        if (!Ready) return;

        var result = new RandomizerService().Randomize(_bec,
            new RandomizerOptions { Seed = "script-flow", RandomizeTeam = true });

        Assert.Contains("scripts", result.Steps);
        Assert.NotEmpty(result.ModifiedScripts);
    }

    [Theory]
    [InlineData("UndeadMeleeImpA", "UndeadMelee")]
    [InlineData("UndeadMeleeNorBF", "UndeadMelee")]
    [InlineData("BanditAF", "Bandit")]
    [InlineData("BanditB", "Bandit")]
    [InlineData("ArcherF", "Archer")]
    [InlineData("Legionnaire", "Legionnaire")]
    public void BaseClassCollapsesVariants(string className, string expected)
    {
        Assert.Equal(expected, RandomizerService.BaseClass(className));
    }
}
