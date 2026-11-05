namespace ModdingGUI.Core.Randomizer;

using System;
using System.Collections.Generic;

/// <summary>
/// Classes the randomizer must not hand out, per game version.
/// </summary>
/// <remarks>
/// Copied verbatim from the old randomizer's three blacklists: they are the boss, beast and summon classes that
/// are not playable as a starting gladiator. The lists differ per version because Ragnarok's and Leonarth's mods
/// ship different creature classes.
/// </remarks>
public static class ClassEligibility
{
    private static readonly string[] VanillaBlacklist =
    {
        "BeastAir",
        "BeastDarkAir",
        "BeastDarkEarth",
        "BeastDarkFire",
        "BeastDarkWater",
        "BeastEarth",
        "BeastFire",
        "BeastWater",
        "BeastAirGreater",
        "BeastDarkAirGreater",
        "BeastDarkEarthGreater",
        "BeastDarkFireGreater",
        "BeastDarkWaterGreater",
        "BeastEarthGreater",
        "BeastFireGreater",
        "BeastWaterGreater",
        "BearGreater",
        "BerserkerEnraged",
        "BerserkerEnragedF",
        "Boss",
        "CatGreater",
        "CitizenExp",
        "CitizenExpF",
        "CitizenImp",
        "CitizenImpF",
        "CitizenNor",
        "CitizenNorF",
        "CitizenSte",
        "CitizenSteF",
        "CyclopsGreater",
        "DarkGod",
        "DarkGodDragon",
        "DarkGodDragon1",
        "DarkGodDragon2",
        "DarkGodDragon3",
        "DarkGodFace",
        "DarkGodKnight",
        "DarkGodShiva",
        "Mutuus",
        "MutuusDark",
        "MutuusSenate",
        "Orin",
        "PropBarrel",
        "PropPracticePost",
        "PropStatue",
        "PropTombstone",
        "TitanAir",
        "TitanEarth",
        "TitanFire",
        "TitanWater",
        "Usus",
        "WolfGreater",
        "UrsulaCostumeA",
        "UrsulaCostumeB",
        "ValensCostumeA",
        "ValensCostumeB",
        "Nephilia",
    };

    private static readonly string[] RagnaroksBlacklist =
    {
        "BeastAirGreater",
        "BeastDarkAirGreater",
        "BeastDarkEarthGreater",
        "BeastDarkFireGreater",
        "BeastDarkWaterGreater",
        "BeastEarthGreater",
        "BeastFireGreater",
        "BeastWaterGreater",
        "BerserkerEnraged",
        "BerserkerEnragedF",
        "Boss",
        "CitizenExp",
        "CitizenExpF",
        "CitizenImp",
        "CitizenImpF",
        "CitizenNor",
        "CitizenNorF",
        "CitizenSte",
        "CitizenSteF",
        "CyclopsGreater",
        "DarkGod",
        "DarkGodDragon",
        "DarkGodDragon1",
        "DarkGodDragon2",
        "DarkGodDragon3",
        "DarkGodFace",
        "DarkGodKnight",
        "DarkGodShiva",
        "MutuusDark",
        "PropBarrel",
        "PropPracticePost",
        "PropStatue",
        "PropTombstone",
        "TitanAir",
        "TitanEarth",
        "TitanFire",
        "TitanWater",
        "Jötunn",
        "UrsulaCostumeA",
        "UrsulaCostumeB",
        "ValensCostumeA",
        "ValensCostumeB",
    };

    private static readonly string[] LeonarthsBlacklist =
    {
        "BeastAirGreater",
        "BeastDarkAirGreater",
        "BeastDarkEarthGreater",
        "BeastDarkFireGreater",
        "BeastDarkWaterGreater",
        "BeastEarthGreater",
        "BeastFireGreater",
        "BeastWaterGreater",
        "BerserkerEnraged",
        "BerserkerEnragedF",
        "Boss",
        "CitizenExp",
        "CitizenExpF",
        "CitizenImp",
        "CitizenImpF",
        "CitizenNor",
        "CitizenNorF",
        "CitizenSte",
        "CitizenSteF",
        "CyclopsGreater",
        "DarkGod",
        "DarkGodDragon",
        "DarkGodDragon1",
        "DarkGodDragon2",
        "DarkGodDragon3",
        "DarkGodFace",
        "DarkGodKnight",
        "MutuusSenate",
        "Orin",
        "PropBarrel",
        "PropPracticePost",
        "PropStatue",
        "PropTombstone",
        "TitanAir",
        "TitanEarth",
        "TitanFire",
        "TitanWater",
        "Usus",
    };

    /// <summary>Blacklisted classes for a game version.</summary>
    public static IReadOnlyCollection<string> BlacklistFor(GameVersion version) => version switch
    {
        GameVersion.Ragnaroks => RagnaroksBlacklist,
        GameVersion.Leonarths => LeonarthsBlacklist,
        _ => VanillaBlacklist,
    };

    /// <summary>True when a class may be handed to a player unit in this version.</summary>
    public static bool IsEligible(string className, GameVersion version, IReadOnlyCollection<string>? blacklist = null)
    {
        if (string.IsNullOrWhiteSpace(className)) return false;

        blacklist ??= BlacklistFor(version);
        return !blacklist.Contains(className.Trim(), StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Filter a class list down to the eligible ones, preserving order.</summary>
    public static List<string> Filter(
        IEnumerable<string> classes, GameVersion version, IReadOnlyCollection<string>? blacklist = null)
    {
        ArgumentNullException.ThrowIfNull(classes);

        blacklist ??= BlacklistFor(version);
        var result = new List<string>();
        foreach (string className in classes)
        {
            if (IsEligible(className, version, blacklist)) result.Add(className);
        }
        return result;
    }
}
