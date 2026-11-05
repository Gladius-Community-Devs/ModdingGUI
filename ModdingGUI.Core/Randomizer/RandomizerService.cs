namespace ModdingGUI.Core.Randomizer;

using System;
using System.Collections.Generic;
using System.Linq;
using ModdingGUI.Core.Api;
using ModdingGUI.Core.Logging;
using ModdingGUI.Core.Units;

/// <summary>
/// A gladiator's loadout: the seven fields that make a unit play the way it does, taken together.
/// </summary>
/// <remarks>
/// <para>
/// The randomizer swaps these as a <b>bundle</b> rather than shuffling columns independently. That is the
/// cross-reference the game data supports: a bundle is copied from a real gladiator record, so a class keeps
/// the stat set, skill set and item set it was authored with. Nothing in the files maps a class to its sets
/// (<c>classdefs.tok</c> carries only <c>SKILLUSENAME</c> for animations), so picking them separately would be
/// invention.
/// </para>
/// <para>
/// <see cref="Name"/> and <see cref="School"/> are not part of the bundle: a hero keeps its identity and its
/// school, and school membership is what puts a unit in a roster.
/// </para>
/// </remarks>
public sealed record GladiatorLoadout(
    string ClassName,
    int Outfit,
    string Affinity,
    int TintSet,
    int SkillSet,
    int StatSet,
    int ItemSet)
{
    /// <summary>Take the loadout half of a record.</summary>
    public static GladiatorLoadout From(GladiatorDto dto) => new(
        dto.Class, dto.Outfit, dto.Affinity, dto.TintSet, dto.SkillSet, dto.StatSet, dto.ItemSet);

    /// <summary>The patch that applies this loadout to an existing record.</summary>
    public GladiatorPatch ToPatch() => new()
    {
        Class = ClassName,
        Outfit = Outfit,
        Affinity = Affinity,
        TintSet = TintSet,
        SkillSet = SkillSet,
        StatSet = StatSet,
        ItemSet = ItemSet,
    };

    /// <summary>True when the record already carries this loadout.</summary>
    public bool Matches(GladiatorDto dto) =>
        dto.Class == ClassName && dto.Outfit == Outfit && dto.Affinity == Affinity
        && dto.TintSet == TintSet && dto.SkillSet == SkillSet && dto.StatSet == StatSet
        && dto.ItemSet == ItemSet;
}

/// <summary>The story heroes. Their classes are their own, so they are excluded from the general pool.</summary>
public static class StoryHeroes
{
    /// <summary>Hero names, in the order the game introduces them.</summary>
    public static readonly string[] Names = { "Valens", "Ursula", "Ludo", "Urlan", "Eiji", "Gwazi" };

    /// <summary>True when the name belongs to a hero.</summary>
    public static bool IsHero(string name) =>
        Names.Contains(name, StringComparer.Ordinal);
}

/// <summary>What one randomization run did.</summary>
/// <param name="Seed">The seed in use, for reproducing the run.</param>
/// <param name="Steps">Named steps that ran, in order.</param>
/// <param name="ChangedRecords">Records actually written across every step.</param>
public sealed record RandomizerResult(string Seed, IReadOnlyList<string> Steps, int ChangedRecords);

/// <summary>
/// Runs a randomization pass over a project through the Core APIs.
/// </summary>
/// <remarks>
/// One <see cref="RandomizerRandom"/> per run, one batch write per file, and progress through
/// <see cref="IAppLog"/> so the GUI and (later) the MCP server observe the same run. The old implementation
/// seeded only two of its six steps and wrote files directly; this one owns no file I/O.
/// </remarks>
public sealed class RandomizerService
{
    private readonly IGladiatorsApi _gladiators;
    private readonly IAppLog? _log;

    /// <summary>Create the service; every dependency is injectable for tests.</summary>
    public RandomizerService(IGladiatorsApi? gladiators = null, IAppLog? log = null)
    {
        _gladiators = gladiators ?? new GladiatorsApi();
        _log = log;
    }

    /// <summary>Run the requested steps. Returns what happened, including the seed for reproduction.</summary>
    public RandomizerResult Randomize(string projectRoot, RandomizerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);

        var random = new RandomizerRandom(options.Seed);
        var steps = new List<string>();
        int changed = 0;

        using var scope = _log?.Scope("randomizer");

        _log?.Write(LogLevel.Info, $"Randomizer run started with seed '{random.SeedText}'.", "randomizer");

        if (options.RandomizeHeroes)
        {
            int heroes = RandomizeHeroes(projectRoot, random);
            changed += heroes;
            steps.Add("heroes");
            _log?.Write(LogLevel.Success, $"Hero loadouts reassigned: {heroes} changed.", "randomizer");
        }

        if (options.RandomizeStatsets) steps.Add("statsets (not yet implemented)");
        if (options.RandomizeItemsets) steps.Add("itemsets (not yet implemented)");
        if (options.RandomizeTeam) steps.Add("team (not yet implemented)");
        if (options.RemoveRecruits) steps.Add("recruits (not yet implemented)");

        _log?.Write(LogLevel.Info, $"Randomizer run finished: {changed} record(s) changed.", "randomizer");
        return new RandomizerResult(random.SeedText, steps, changed);
    }

    /// <summary>
    /// Give each story hero the loadout of another, randomly chosen gladiator. The hero keeps its name and
    /// school; the bundle keeps the class consistent with its stats, skills and gear because it came from a
    /// real record.
    /// </summary>
    private int RandomizeHeroes(string projectRoot, RandomizerRandom random)
    {
        var records = _gladiators.GetAll(projectRoot);

        // Pool = everyone who is not a hero, so a hero cannot be given another hero's bespoke class.
        var pool = records
            .Where(dto => !StoryHeroes.IsHero(dto.Name))
            .Select(GladiatorLoadout.From)
            .ToList();

        if (pool.Count == 0) return 0;

        var patches = new List<(string Name, GladiatorPatch Patch)>();
        foreach (string hero in StoryHeroes.Names)
        {
            var current = records.FirstOrDefault(dto => dto.Name == hero);
            if (current is null) continue;

            var loadout = random.Pick(pool);
            if (loadout.Matches(current)) continue;

            patches.Add((hero, loadout.ToPatch()));
        }

        if (patches.Count == 0) return 0;
        return _gladiators.ApplyMany(projectRoot, patches);
    }
}
