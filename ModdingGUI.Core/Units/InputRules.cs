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

    /// <summary>
    /// Structural ceiling for an amount of money and for an item price: a signed 32-bit integer, because that is how
    /// the field is stored. The in-game HUD is reported to stop displaying correctly at 999,999, which is a display
    /// limit rather than a storage one, so it is not enforced here.
    /// </summary>
    public const long MaxGold = int.MaxValue;

    /// <summary>The HUD is reported to show at most six digits of currency. Display only - not enforced.</summary>
    public const long ReportedHudGoldDisplayLimit = 999_999;

    /// <summary>
    /// Highest affinity amount an item may carry. A unit holds at most 100 points of offensive affinity, and vanilla
    /// items go up to 200.
    /// </summary>
    public const int MaxAffinity = 200;

    /// <summary>Cheapest legal item price: a price of 0 is treated as free and rejected.</summary>
    public const int MinItemCost = 1;

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

    /// <summary>True when the price is one an item may carry.</summary>
    public static bool IsValidItemCost(long cost) => cost >= MinItemCost && cost <= MaxGold;

    /// <summary>Why a price is not acceptable, or null.</summary>
    public static string? ExplainItemCost(long cost) =>
        IsValidItemCost(cost)
            ? null
            : $"Cost must be between {MinItemCost} and {MaxGold:N0}; got {cost:N0}.";

    /// <summary>True when the amount is one an item may carry as an affinity bonus.</summary>
    public static bool IsValidAffinity(long amount) => amount >= 0 && amount <= MaxAffinity;

    /// <summary>Why an affinity amount is not acceptable, or null.</summary>
    public static string? ExplainAffinity(long amount) =>
        IsValidAffinity(amount)
            ? null
            : $"Affinity must be between 0 and {MaxAffinity}; got {amount:N0}.";

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
