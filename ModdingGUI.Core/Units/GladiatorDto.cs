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

/// <summary>
/// The affinity values the files use, and what the field is actually for.
/// </summary>
/// <remarks>
/// <para>
/// Purpose: this is the unit's affinity for <b>slot eligibility</b>. An encounter UNITDB row can require an
/// affinity (index 7) or prohibit one (index 16), and the matcher FUN_8010ad40 compares that requirement
/// against this value. It is not the affinity a unit fights with - that comes from its equipped weapon
/// (offense) and armour/accessories (defense), which is why changing this field looks inert in the unit
/// panel.
/// </para>
/// <para>
/// Two numberings, one name: the file uses names, the pack chain turns the name into this order's index and
/// packs it into three bits of unitunits.idx, and FUN_800dfac4 remaps that index onto the engine ids -
/// Earth 1, Water 2, Dark 3, Air 4, Fire 5, Light 6 (remap 1-4, 2-3, 3-1, 4-5, 5-6, 6-2). So an encounter
/// requirement of 5 means Fire, while Fire is index 4 here.
/// </para>
/// <para>
/// Slot 5 is a deliberate hole in the repack table and "Light" has no name there, so neither a made-up
/// value nor Light can be repacked today even though the engine knows six affinities.
/// </para>
/// </remarks>
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
