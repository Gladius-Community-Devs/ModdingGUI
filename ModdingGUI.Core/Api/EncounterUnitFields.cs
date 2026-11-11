namespace ModdingGUI.Core.Api;

/// <summary>What kind of input a <c>UNITDB</c> slot wants.</summary>
public enum EncounterFieldKind
{
    /// <summary>Free text (unit name, start marker).</summary>
    Text,

    /// <summary>A whole number, bounded by <see cref="EncounterUnitField.Minimum"/> and <see cref="EncounterUnitField.Maximum"/>.</summary>
    Integer,

    /// <summary>A 0/1 switch (the engine reads it as "non-zero means on").</summary>
    Switch,

    /// <summary>One of a fixed set of values (an enumeration the engine reads).</summary>
    Choice,

    /// <summary>A class-definition name, e.g. <c>Summoner</c>.</summary>
    ClassTag,

    /// <summary>A bitmask; the individual bits are offered as checkboxes.</summary>
    Bitmask,
}

/// <summary>
/// One editable slot of a <c>UNITDB</c> row: a name, a kind, the acceptable range and how to fill a dropdown.
/// </summary>
/// <param name="Slot">1-based slot number as the engine reads it.</param>
/// <param name="Key">Stable identifier used by the driver and the UI.</param>
/// <param name="Label">Short label for a row of inputs.</param>
/// <param name="Kind">What input to show.</param>
/// <param name="Description">What the engine does with the value, and how confident that is.</param>
/// <param name="Minimum">Lowest acceptable value for numeric kinds, when bounded.</param>
/// <param name="Maximum">Highest acceptable value for numeric kinds, when bounded.</param>
/// <param name="Choices">Fixed choices for <see cref="EncounterFieldKind.Choice"/>, else null.</param>
/// <param name="ObservedValues">Values actually present in the vanilla corpus, for reference.</param>
public sealed record EncounterUnitField(
    int Slot,
    string Key,
    string Label,
    EncounterFieldKind Kind,
    string Description,
    int? Minimum = null,
    int? Maximum = null,
    IReadOnlyList<string>? Choices = null,
    IReadOnlyList<string>? ObservedValues = null);

/// <summary>
/// The 21 <c>UNITDB</c> slots as editable fields.
/// </summary>
/// <remarks>
/// <para>
/// The slot meanings come from the engine (see <see cref="EncounterUnit"/>): slots 1-3 and 4-6 and 20-21 are confirmed by
/// the code that reads them, 7-8 and 13-19 are partly engine-confirmed and partly modder-reported, and slots 9-12 are
/// class tags. The <see cref="ObservedValues"/> here were measured across all 6,702 <c>UNITDB</c> rows in the vanilla
/// corpus, so an input can offer what the game actually uses rather than a guess.
/// </para>
/// <para>
/// Two mappings are deliberately still open and surfaced as raw numbers rather than invented names: the attribute bitmask
/// (slot 7) has no attribute-name table anywhere in the data, and the affinity ids (slot 8) are only known to be
/// <c>-1</c> and <c>1..5</c>. Both are exposed with their observed values so the input remains usable, and both are
/// listed as open questions in the docs.
/// </para>
/// </remarks>
public static class EncounterUnitFields
{
    /// <summary>The affinity ids seen in the corpus: -1 means "no requirement".</summary>
    public static IReadOnlyList<string> AffinityIds { get; } = new[] { "-1", "1", "2", "3", "4", "5" };

    /// <summary>All 21 slots, in the order the engine reads them.</summary>
    public static IReadOnlyList<EncounterUnitField> All { get; } = new EncounterUnitField[]
    {
        new(1, "unitName", "Unit", EncounterFieldKind.Text,
            "A specific gladiator name, or empty to generate one from the team's school."),

        new(2, "spawnChance", "Chance %", EncounterFieldKind.Integer,
            "Percentage chance this row spawns ((rand % 100) + 1 <= value). 100 always spawns; below 100 makes the unit " +
            "count as mission-critical, so the engine reports it missing.",
            Minimum: 0, Maximum: 1000, ObservedValues: new[] { "0", "20", "45", "49", "50", "51", "52", "70", "75", "98", "99", "100", "1000" }),

        new(3, "startMarker", "Start", EncounterFieldKind.Text,
            "Grid start marker this row spawns at, e.g. Start1. The values come from the arena's .grd tags."),

        new(4, "positionX", "X", EncounterFieldKind.Integer,
            "Placement X in grid cells (jittered when slot 6 is set).", Minimum: -32, Maximum: 63,
            ObservedValues: new[] { "-4", "-3", "-2", "-1", "0", "1", "2", "3", "4", "5", "6", "7", "10", "15" }),

        new(5, "positionY", "Y", EncounterFieldKind.Integer,
            "Placement Y in grid cells (jittered when slot 6 is set).", Minimum: -32, Maximum: 63,
            ObservedValues: new[] { "-4", "-2", "-1", "0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "10" }),

        new(6, "jitter", "Jitter", EncounterFieldKind.Switch,
            "Non-zero adds rand() to the coordinates, so the unit scatters around the marker."),

        new(7, "attributeMask", "Attribute mask", EncounterFieldKind.Bitmask,
            "Bitmask tested against the candidate's class tags. Bit meanings are not in the data; only the mask values are " +
            "known, so this is edited as a number (or by ticking known bits).",
            ObservedValues: new[] { "0", "1", "2", "4", "8", "16", "17", "24", "28", "32", "56", "64", "68", "128" }),

        new(8, "affinityRequirement", "Affinity", EncounterFieldKind.Choice,
            "Affinity the spawned unit must have; -1 means no requirement. The ids are known, the id-to-name table is not.",
            Choices: AffinityIds),

        new(9, "classTag1", "Class 1", EncounterFieldKind.ClassTag,
            "Extra class tag added to the unit, on top of the school's own class."),

        new(10, "classTag2", "Class 2", EncounterFieldKind.ClassTag, "Second extra class tag."),

        new(11, "classTag3", "Class 3", EncounterFieldKind.ClassTag, "Third extra class tag."),

        new(12, "classTag4", "Class 4", EncounterFieldKind.ClassTag, "Fourth extra class tag."),

        new(13, "prohibitClassTags", "Not class tags", EncounterFieldKind.Switch,
            "Inverts the class-tag test: the unit must NOT carry those tags."),

        new(14, "prohibitClasses", "Not classes", EncounterFieldKind.Switch,
            "Inverts the class requirement."),

        new(15, "prohibitAttribute", "Not attribute", EncounterFieldKind.Switch,
            "Inverts the attribute-mask test."),

        new(16, "prohibitAffinity", "Not affinity", EncounterFieldKind.Switch,
            "Inverts the affinity test: the unit must not have the listed affinity."),

        new(17, "slot17", "Slot 17", EncounterFieldKind.Integer,
            "Passed into unit creation; purpose unknown, vanilla only uses 0-3.",
            Minimum: 0, Maximum: 3, ObservedValues: new[] { "0", "1", "2", "3" }),

        new(18, "requiresMercenary", "Mercenary", EncounterFieldKind.Switch,
            "Requires a mercenary for this slot (modder-reported; every vanilla row is 0).",
            ObservedValues: new[] { "0" }),

        new(19, "itemDropChance", "Item drop %", EncounterFieldKind.Integer,
            "Per-item chance to drop a chest (modder-reported). Vanilla uses multiples of 5 up to 90.",
            Minimum: 0, Maximum: 100,
            ObservedValues: new[] { "0", "20", "25", "30", "33", "35", "40", "50", "60", "70", "75", "80", "90" }),

        new(20, "flag100", "Spawn flag 0x100", EncounterFieldKind.Switch,
            "Sets spawn flag 0x100 (engine-confirmed; OR-ed straight into unit creation)."),

        new(21, "flag200", "Spawn flag 0x200", EncounterFieldKind.Switch,
            "Sets spawn flag 0x200 (engine-confirmed)."),
    };

    /// <summary>The field for a 1-based slot, or null when the slot is out of range.</summary>
    public static EncounterUnitField? ForSlot(int slot) => All.FirstOrDefault(f => f.Slot == slot);

    /// <summary>The field for a driver/UI key, or null.</summary>
    public static EncounterUnitField? ForKey(string key) =>
        All.FirstOrDefault(f => string.Equals(f.Key, key, StringComparison.OrdinalIgnoreCase));
}
