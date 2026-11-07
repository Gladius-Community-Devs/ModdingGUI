namespace ModdingGUI.Core.Randomizer;

using System;

/// <summary>Which campaign's files the randomizer touches.</summary>
public enum HeroChoice
{
    /// <summary>Ursula's Nordagh campaign.</summary>
    Ursula,

    /// <summary>Valens' Imperia campaign.</summary>
    Valens,
}

/// <summary>Which game version the project is based on.</summary>
public enum GameVersion
{
    /// <summary>Retail files.</summary>
    Vanilla,

    /// <summary>Ragnarok's modded files.</summary>
    Ragnaroks,

    /// <summary>Leonarth's modded files.</summary>
    Leonarths,
}

/// <summary>How starting gold is chosen.</summary>
public enum CashMode
{
    /// <summary>Leave the school's gold alone.</summary>
    Unchanged,

    /// <summary>Set <see cref="RandomizerOptions.CashAmount"/>.</summary>
    Fixed,

    /// <summary>Pick an amount from the seed.</summary>
    Random,
}

/// <summary>
/// Everything a randomization run needs. The GUI becomes a parameter builder over this record, and the MCP
/// server will pass the same type, so one shape describes a run for every entry point.
/// </summary>
/// <remarks>
/// The option names mirror the settings the GUI has always exposed, so the rewrite keeps feature parity
/// even though it defines its own choice logic (PLAN.md Phase 6).
/// </remarks>
public sealed record RandomizerOptions
{
    /// <summary>
    /// Free-text seed. The same text always produces the same randomization; empty means "generate one and
    /// report it" (see <see cref="SeedText"/>).
    /// </summary>
    public string Seed { get; init; } = string.Empty;

    /// <summary>Which campaign is being randomized.</summary>
    public HeroChoice Hero { get; init; } = HeroChoice.Ursula;

    /// <summary>Which file set the project is built from.</summary>
    public GameVersion Version { get; init; } = GameVersion.Vanilla;

    /// <summary>Reassign classes, stat sets, skill sets, item sets and tints across the gladiator table.</summary>
    // Default off: an options object with nothing selected must not write anything, and every step is an
    // explicit choice in the GUI.
    public bool RandomizeHeroes { get; init; }

    /// <summary>Randomize the starting roster in the school file.</summary>
    public bool RandomizeTeam { get; init; }

    /// <summary>Remove recruitable units from the encounters.</summary>
    public bool RemoveRecruits { get; init; }

    /// <summary>
    /// Randomize enemy teams: an empty enemy row becomes a randomizable slot that prohibits <c>Boss</c>,
    /// <c>Prop</c>, <c>DarkGod</c> and <c>AffinityBeastGreater</c>. The player's team is always opened up.
    /// </summary>
    public bool RandomizeEnemies { get; init; }

    /// <summary>
    /// School name written to both rosters. Null keeps whatever the project already has; the panel exposes a text
    /// box for it, so it has to travel with the options.
    /// </summary>
    public string? SchoolName { get; init; }

    /// <summary>
    /// Start from the pinned vanilla baseline: the first run captures one, every later run restores it first so
    /// runs never stack. Turn this off only when randomizing on top of an existing run on purpose.
    /// </summary>
    public bool ResetToBaseline { get; init; } = true;

    /// <summary>Randomize the stat tables.</summary>
    public bool RandomizeStatsets { get; init; }

    /// <summary>Randomize the item tables.</summary>
    public bool RandomizeItemsets { get; init; }

    /// <summary>Apply the "no recruits" style toggles that the gecko codes provide.</summary>
    public bool PermaDeath { get; init; }

    /// <summary>Enable the 40-gladiator option.</summary>
    public bool FortyGladiators { get; init; }

    /// <summary>How the starting gold is chosen.</summary>
    public CashMode Cash { get; init; } = CashMode.Unchanged;

    /// <summary>Gold amount used by <see cref="CashMode.Fixed"/>.</summary>
    public int CashAmount { get; init; }

    /// <summary>Weight class choices for diversity instead of picking uniformly.</summary>
    public bool WeightedClasses { get; init; }

    /// <summary>True when nothing would be changed.</summary>
    public bool IsEmpty =>
        !RandomizeHeroes && !RandomizeTeam && !RemoveRecruits && !RandomizeStatsets && !PermaDeath
        && !RandomizeItemsets && !FortyGladiators && !RandomizeEnemies
        && Cash == CashMode.Unchanged;
}
