namespace ModdingGUI.Core.Units;

/// <summary>
/// One gladiator entry from <c>data/units/gladiators.txt</c> — the nine labelled values the file
/// carries, in file order.
/// </summary>
/// <remarks>
/// The labels are the file's own (it names every field), and the values are the ones the compiled
/// <c>.idx</c> needs: <c>Gladius_Units_IDX_Repack.py</c> reads exactly these lines to build
/// <c>unitunits.idx</c>. The sets are zero-based indexes into the other units files:
/// <see cref="StatSet"/> into <c>statsets.txt</c> (169), <see cref="SkillSet"/> into
/// <c>skillsets.txt</c> (230), <see cref="ItemSet"/> into <c>itemsets.txt</c> (404),
/// <see cref="TintSet"/> into <c>tints.txt</c> (196) and <see cref="School"/> into <c>schools.txt</c>
/// (144). <see cref="Class"/> names a record in <c>data/config/classdefs.tok</c>.
/// </remarks>
public sealed record GladiatorDto(
    string Name,
    string Class,
    int Outfit,
    string Affinity,
    int TintSet,
    int SkillSet,
    int StatSet,
    int ItemSet,
    int School);

/// <summary>Partial update for a gladiator: null means "leave as it is".</summary>
public sealed record GladiatorPatch
{
    public string? Class { get; init; }
    public int? Outfit { get; init; }
    public string? Affinity { get; init; }
    public int? TintSet { get; init; }
    public int? SkillSet { get; init; }
    public int? StatSet { get; init; }
    public int? ItemSet { get; init; }
    public int? School { get; init; }

    /// <summary>True when the patch would change nothing.</summary>
    public bool IsEmpty =>
        Class is null && Outfit is null && Affinity is null && TintSet is null
        && SkillSet is null && StatSet is null && ItemSet is null && School is null;
}

/// <summary>The affinity values the files actually use (from the compiled index table).</summary>
public static class GladiatorAffinity
{
    /// <summary>Accepted spellings, in the order the engine enumerates them.</summary>
    public static readonly string[] Values = { "None", "Air", "Dark", "Earth", "Fire", "Water" };

    /// <summary>Case-insensitive membership test for validation.</summary>
    public static bool IsKnown(string? value) =>
        value is not null && Values.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase);

    /// <summary>Canonical spelling, or the input unchanged when it is not a known value.</summary>
    public static string Normalize(string value)
    {
        foreach (string known in Values)
        {
            if (string.Equals(known, value.Trim(), StringComparison.OrdinalIgnoreCase)) return known;
        }
        return value;
    }
}
