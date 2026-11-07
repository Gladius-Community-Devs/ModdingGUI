using System;
using System.IO;
using System.Linq;
using ModdingGUI.Core.Api;
using ModdingGUI.Core.Randomizer;
using Xunit;

namespace ModdingGUI.Tests.Core.Randomizer;

/// <summary>
/// A class whose corpus weapons are all one ranged subtype must keep that subtype when its item set is reassigned:
/// a bow class with a sword passes every turn, because the engine's attack follows the equipped weapon.
/// </summary>
public sealed class RandomizerRangedWeaponTests : IDisposable
{
    private readonly string _project;
    private readonly string _bec;

    public RandomizerRangedWeaponTests()
    {
        _project = Path.Combine(Path.GetTempPath(), "ModdingGUI.Bow-" + Guid.NewGuid().ToString("N"));
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

    [Fact]
    public void BowClassesKeepABow()
    {
        if (!Ready) return;

        new RandomizerService().Randomize(_bec, new RandomizerOptions { Seed = "bows", RandomizeItemsets = true });

        // The map's classification is the one the assignment uses, so assert against it.
        var map = ItemSetAffinityMap.Build(_bec);
        var gladiators = new GladiatorsApi().GetAll(_bec);
        foreach (var dto in gladiators.Where(g => g.Class is "Archer" or "ArcherF" or "Amazon"))
        {
            Assert.Equal("Bow", map.WeaponSubtypeOf(dto.ItemSet));
        }
    }

    [Fact]
    public void OnlyRangedSpecialistsAreConstrained()
    {
        if (!Ready) return;

        var map = ItemSetAffinityMap.Build(_bec);
        var records = new GladiatorsApi().GetAll(_bec);
        var required = RandomizerService.DeriveRequiredRangedWeapons(records, map);

        // Vanilla's ranged specialists.
        Assert.Equal("Bow", required["Archer"]);
        Assert.Equal("Bow", required["ArcherF"]);
        Assert.Equal("Bow", required["Amazon"]);

        // A Peltast carries javelins and spears, so it must not be pinned to either; melee classes are free too.
        Assert.False(required.ContainsKey("Peltast"));
        Assert.False(required.ContainsKey("Legionnaire"));
        Assert.All(required.Values, subtype => Assert.True(ItemSetAffinityMap.IsRanged(subtype)));
    }

    [Fact]
    public void TheRequirementIsDerivedFromTheRecords()
    {
        if (!Ready) return;

        var map = ItemSetAffinityMap.Build(_bec);
        var required = RandomizerService.DeriveRequiredRangedWeapons(new GladiatorsApi().GetAll(_bec), map);

        // Every class that carries one ranged subtype only is listed with it; anything mixed is not.
        Assert.Equal("Bow", required["Archer"]);
        Assert.Equal("Bow", required["ArcherF"]);
        Assert.Equal("Bow", required["Amazon"]);
        Assert.All(required.Values, subtype => Assert.True(ItemSetAffinityMap.IsRanged(subtype)));

        Assert.True(ItemSetAffinityMap.IsRanged("Bow"));
        Assert.True(ItemSetAffinityMap.IsRanged("Javelin"));
        Assert.False(ItemSetAffinityMap.IsRanged("Sword"));
    }

    [Fact]
    public void TheMapCanFilterByRequiredSubtype()
    {
        if (!Ready) return;

        var map = ItemSetAffinityMap.Build(_bec);
        var bows = map.CandidatesFor("None", "Bow");

        Assert.NotEmpty(bows);
        Assert.All(bows, index => Assert.Equal("Bow", map.WeaponSubtypeOf(index)));
        Assert.True(bows.Count < map.CandidatesFor("None").Count);
    }
}
