namespace ModdingGUI.Core.Units;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Input rules for the values the tools write, with the evidence for each bound.
/// </summary>
/// <remarks>
/// <para>
/// Levels: every one of the 169 stat sets in <c>statsets.txt</c> provides rows 1 to 30 and nothing higher, so 30 is
/// the ceiling the game can price a unit at.
/// </para>
/// <para>
/// Money: vanilla rosters carry <c>GOLD</c> values up to 999,980,999. No cap is visible in the executable, so the
/// structural bound is a signed 32-bit integer; values above the vanilla maximum are allowed but flagged, because a
/// mod may want them and nothing observed breaks.
/// </para>
/// <para>
/// Names: a unit name is a quoted token in <c>CREATEUNIT:</c>, so a double quote or a line break would corrupt the
/// file. Everything else is allowed, including spaces (40 shipped names contain one) and underscores: the display
/// name is looked up from the part before the first underscore, which is how the randomizer makes
/// <c>ludo_random</c> show as Ludo without colliding with Ludo's own record.
/// </para>
/// </remarks>
public static class InputRules
{
    /// <summary>Lowest level a unit can be created at.</summary>
    public const int MinLevel = 1;

    /// <summary>Highest level: every stat set stops at row 30.</summary>
    public const int MaxLevel = 30;

    /// <summary>Largest value vanilla rosters use for <c>GOLD</c>.</summary>
    public const long VanillaMaxGold = 999_980_999;

    /// <summary>Structural ceiling for an amount of money: a signed 32-bit integer.</summary>
    public const long MaxGold = int.MaxValue;

    /// <summary>Longest name any vanilla unit or gladiator carries is 28; allow a little room.</summary>
    public const int MaxNameLength = 31;

    /// <summary>
    /// The character that ends a display name. Documented here because it looks like a bug when a unit shows as
    /// "Ludo" after being named "Ludo_random".
    /// </summary>
    public const char DisplaySeparator = '_';

    /// <summary>True when the level is one a unit can be created at.</summary>
    public static bool IsValidLevel(int level) => level >= MinLevel && level <= MaxLevel;

    /// <summary>Why a level is not acceptable, or null.</summary>
    public static string? ExplainLevel(int level) =>
        IsValidLevel(level) ? null : $"Level must be between {MinLevel} and {MaxLevel}; got {level}.";

    /// <summary>True when the amount is a value the roster field can hold.</summary>
    public static bool IsValidGold(long amount) => amount >= 0 && amount <= MaxGold;

    /// <summary>Why an amount is not acceptable, or null.</summary>
    public static string? ExplainGold(long amount) =>
        IsValidGold(amount) ? null : $"Money must be between 0 and {MaxGold:N0}; got {amount:N0}.";

    /// <summary>True when the amount is above anything vanilla ships, so it deserves a note rather than a refusal.</summary>
    public static bool IsAboveVanillaGold(long amount) => amount > VanillaMaxGold;

    /// <summary>Every reason a unit name would be rejected; empty when it is fine.</summary>
    public static IReadOnlyList<string> ValidateUnitName(string? name)
    {
        var problems = new List<string>();

        if (string.IsNullOrWhiteSpace(name))
        {
            problems.Add("A unit name is required.");
            return problems;
        }

        string value = name!;

        if (value != value.Trim())
        {
            problems.Add("A unit name cannot start or end with a space.");
        }

        if (value.Contains('"', StringComparison.Ordinal))
        {
            problems.Add("A unit name cannot contain a double quote; it would break the CREATEUNIT line.");
        }

        if (value.Any(ch => ch is '\r' or '\n' or '\t'))
        {
            problems.Add("A unit name cannot contain line breaks or tabs.");
        }

        if (value.Trim().Length > MaxNameLength)
        {
            problems.Add($"A unit name cannot be longer than {MaxNameLength} characters.");
        }

        return problems;
    }

    /// <summary>Every reason a school name would be rejected; empty when it is fine.</summary>
    public static IReadOnlyList<string> ValidateSchoolName(string? name)
    {
        var problems = new List<string>();

        if (string.IsNullOrWhiteSpace(name))
        {
            problems.Add("A school name is required.");
            return problems;
        }

        string value = name!;

        if (value.Contains('"', StringComparison.Ordinal))
        {
            problems.Add("A school name cannot contain a double quote; it would break the NAME line.");
        }

        if (value.Any(ch => ch is '\r' or '\n'))
        {
            problems.Add("A school name cannot contain line breaks.");
        }

        if (value.Length > 64)
        {
            problems.Add("A school name cannot be longer than 64 characters.");
        }

        return problems;
    }

    /// <summary>
    /// The display name the game will show for a unit name: everything before the first underscore.
    /// </summary>
    public static string DisplayNameOf(string name)
    {
        if (string.IsNullOrEmpty(name)) return string.Empty;

        int separator = name.IndexOf(DisplaySeparator);
        return separator < 0 ? name : name[..separator];
    }
}
