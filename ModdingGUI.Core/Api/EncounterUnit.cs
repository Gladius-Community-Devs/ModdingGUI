namespace ModdingGUI.Core.Api;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ModdingGUI.Core.Tok.Values;

/// <summary>
/// One <c>UNITDB</c> row — the 21 values the engine reads in <c>EncounterSetup</c> case <c>0x1D</c>
/// (<c>0x80106ddc</c>), in read order.
/// </summary>
/// <remarks>
/// <para>
/// Slots 1-3 (name, spawn chance, start marker), 4-6 (x, y, jitter) and 20-21 (spawn flags 0x100,
/// 0x200) are confirmed by the engine: the jitter flag adds <c>rand()</c> to the coordinates and the
/// two flags are OR-ed straight into the spawn flags passed to unit creation. A separate modder's
/// notes (devnotes.txt) guessed slots 4-6 were level bounds; the code disproves that — they are
/// coordinates, and <c>0</c> is clamped to <c>1</c> before use.
/// </para>
/// <para>
/// Slots 7-8 (attribute / affinity requirement), 14-16 (the three "prohibited" flips), 18 (mercenary)
/// and 19 (item drop chance) come from those modder notes; slot 19's vanilla values (multiples of 5
/// up to 90) and the engine storing 7-8 on the unit spec both fit. Unknown and left as numbers: slot
/// 13, slot 17 (vanilla 0-3) and the two flags are documented as such rather than guessed.
/// </para>
/// </remarks>
public sealed class EncounterUnit
{
    /// <summary>How many values a <c>UNITDB</c> row carries.</summary>
    public const int SlotCount = 21;

    /// <summary>
    /// The row exactly as it appears in the file, used to reproduce a no-op write byte for byte.
    /// </summary>
    public string Raw { get; set; } = string.Empty;

    /// <summary>Slot 1 — unit name or template id; empty means "generate from the team school".</summary>
    public string UnitName { get; init; } = string.Empty;

    /// <summary>
    /// Slot 2 — spawn chance as a percentage (<c>(rand % 100) + 1 &lt;= value</c>), so 100 always
    /// spawns. Also selects mission-critical units: the engine only reports a missing unit when
    /// this is below 100.
    /// </summary>
    public int SpawnChancePercent { get; init; } = 100;

    /// <summary>Slot 3 — grid start marker, e.g. <c>Start1</c> … <c>Start8</c>.</summary>
    public string StartSlot { get; init; } = string.Empty;

    /// <summary>Slot 4 — placement X (jittered when <see cref="PositionJitter"/> is set).</summary>
    public int PositionX { get; init; }

    /// <summary>Slot 5 — placement Y (jittered when <see cref="PositionJitter"/> is set).</summary>
    public int PositionY { get; init; }

    /// <summary>Slot 6 — non-zero adds a random offset to the placement.</summary>
    public int PositionJitter { get; init; }

    /// <summary>
    /// Slot 7 — attribute requirement for the player's unit (bit flags; the engine stores it on the
    /// unit spec it builds for the slot). Reported by a modder; vanilla values are flag-like
    /// (1, 2, 4, 8, 16, 24, 32, 56, 64, 4224).
    /// </summary>
    public int AttributeRequirement { get; init; }

    /// <summary>
    /// Slot 8 — affinity requirement (<c>-1</c> = none, otherwise an affinity id; vanilla uses 1-5).
    /// Reported by a modder; the engine copies it into the unit spec unchanged, which matches.
    /// </summary>
    public int AffinityRequirement { get; init; }

    /// <summary>Slot 9 — first extra class tag added to the unit (accumulates, like skill classes).</summary>
    public string ClassTag1 { get; init; } = string.Empty;

    /// <summary>Slot 10 — second extra class tag.</summary>
    public string ClassTag2 { get; init; } = string.Empty;

    /// <summary>Slot 11 — third extra class tag.</summary>
    public string ClassTag3 { get; init; } = string.Empty;

    /// <summary>Slot 12 — fourth extra class tag.</summary>
    public string ClassTag4 { get; init; } = string.Empty;

    /// <summary>
    /// Slot 13 — boolean flag (0/1). Purpose unknown: the engine stores it as a byte on the unit spec
    /// and nothing in vanilla distinguishes it from the three flags that follow.
    /// </summary>
    public int FlagA { get; init; }

    /// <summary>
    /// Slot 14 — flips the class list from "required" to "prohibited" (modder-reported). Consistent with
    /// the engine writing it as a byte next to the class names it just parsed.
    /// </summary>
    public int ProhibitClasses { get; init; }

    /// <summary>Slot 15 — flips <see cref="AttributeRequirement"/> to "prohibited" (modder-reported).</summary>
    public int ProhibitAttribute { get; init; }

    /// <summary>Slot 16 — flips <see cref="AffinityRequirement"/> to "prohibited" (modder-reported).</summary>
    public int ProhibitAffinity { get; init; }

    /// <summary>
    /// Slot 17 — passed into unit creation. Purpose unknown; vanilla values are only 0-3 and every
    /// encounter uses 0.
    /// </summary>
    public int Slot17 { get; init; }

    /// <summary>
    /// Slot 18 — requires a mercenary (temporary recruit) for the slot (modder-reported). Every vanilla
    /// encounter leaves it 0, so it is untested in game.
    /// </summary>
    public int RequiresMercenary { get; init; }

    /// <summary>
    /// Slot 19 — chance per item to drop a chest, as a percentage (modder-reported). Vanilla values are
    /// 0, 20, 25, 30, 33, 35, 40, 50, 60, 70, 75, 80, 90, which is what a percentage looks like.
    /// </summary>
    public int ItemDropChancePercent { get; init; }

    /// <summary>Slot 20 — sets spawn flag <c>0x100</c> when non-zero.</summary>
    public int Flag100 { get; init; }

    /// <summary>Slot 21 — sets spawn flag <c>0x200</c> when non-zero.</summary>
    public int Flag200 { get; init; }

    /// <summary>All 21 values in file order, as written (strings keep their quotes).</summary>
    public IReadOnlyList<string> Slots { get; init; } = Array.Empty<string>();

    /// <summary>The four extra class tags, in order.</summary>
    public IReadOnlyList<string> ClassTags =>
        new[] { ClassTag1, ClassTag2, ClassTag3, ClassTag4 }
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .ToArray();

    /// <summary>
    /// Parse a <c>UNITDB</c> value. Returns null when the row has the wrong number of values or a
    /// non-numeric value where the engine reads an integer.
    /// </summary>
    public static EncounterUnit? TryParse(string rawValue)
    {
        if (rawValue is null) return null;

        var tokens = TokValueReader.Parse(rawValue).Tokens
            .Where(t => t.Kind != TokValueKind.Empty)
            .ToList();
        if (tokens.Count != SlotCount) return null;

        var slots = tokens.Select(t => t.RawLexeme.Trim()).ToArray();

        int[] numericIndexes = { 1, 3, 4, 5, 6, 7, 12, 13, 14, 15, 16, 17, 18, 19, 20 };
        var numbers = new int[SlotCount];
        foreach (int index in numericIndexes)
        {
            if (tokens[index].Kind is not (TokValueKind.Integer or TokValueKind.Decimal)) return null;
            if (!int.TryParse(tokens[index].Text, NumberStyles.Integer, CultureInfo.InvariantCulture,
                    out numbers[index]))
            {
                // A decimal where the engine reads an int is still accepted by the engine's read.
                if (!double.TryParse(tokens[index].Text, NumberStyles.Float,
                        CultureInfo.InvariantCulture, out double parsed)) return null;
                numbers[index] = (int)parsed;
            }
        }

        return new EncounterUnit
        {
            Raw = rawValue.Trim(),
            Slots = slots,
            UnitName = Unquote(slots[0]),
            SpawnChancePercent = numbers[1],
            StartSlot = Unquote(slots[2]),
            PositionX = numbers[3],
            PositionY = numbers[4],
            PositionJitter = numbers[5],
            AttributeRequirement = numbers[6],
            AffinityRequirement = numbers[7],
            ClassTag1 = Unquote(slots[8]),
            ClassTag2 = Unquote(slots[9]),
            ClassTag3 = Unquote(slots[10]),
            ClassTag4 = Unquote(slots[11]),
            FlagA = numbers[12],
            ProhibitClasses = numbers[13],
            ProhibitAttribute = numbers[14],
            ProhibitAffinity = numbers[15],
            Slot17 = numbers[16],
            RequiresMercenary = numbers[17],
            ItemDropChancePercent = numbers[18],
            Flag100 = numbers[19],
            Flag200 = numbers[20],
        };
    }

    /// <summary>
    /// Render the row. Returns <see cref="Raw"/> untouched unless a value was changed through one
    /// of the <c>With…</c> helpers, so untouched rows keep their original spacing.
    /// </summary>
    public string ToRaw() => Raw;

    /// <summary>Render the current field values in the corpus style (used after an edit).</summary>
    public string RebuildRaw()
    {
        if (Slots.Count != SlotCount) return Raw;

        var slots = Slots.ToArray();
        slots[0] = Quote(UnitName);
        slots[1] = SpawnChancePercent.ToString(CultureInfo.InvariantCulture);
        slots[2] = Quote(StartSlot);
        slots[3] = PositionX.ToString(CultureInfo.InvariantCulture);
        slots[4] = PositionY.ToString(CultureInfo.InvariantCulture);
        slots[5] = PositionJitter.ToString(CultureInfo.InvariantCulture);
        slots[6] = AttributeRequirement.ToString(CultureInfo.InvariantCulture);
        slots[7] = AffinityRequirement.ToString(CultureInfo.InvariantCulture);
        slots[8] = Quote(ClassTag1);
        slots[9] = Quote(ClassTag2);
        slots[10] = Quote(ClassTag3);
        slots[11] = Quote(ClassTag4);
        slots[12] = FlagA.ToString(CultureInfo.InvariantCulture);
        slots[13] = ProhibitClasses.ToString(CultureInfo.InvariantCulture);
        slots[14] = ProhibitAttribute.ToString(CultureInfo.InvariantCulture);
        slots[15] = ProhibitAffinity.ToString(CultureInfo.InvariantCulture);
        slots[16] = Slot17.ToString(CultureInfo.InvariantCulture);
        slots[17] = RequiresMercenary.ToString(CultureInfo.InvariantCulture);
        slots[18] = ItemDropChancePercent.ToString(CultureInfo.InvariantCulture);
        slots[19] = Flag100.ToString(CultureInfo.InvariantCulture);
        slots[20] = Flag200.ToString(CultureInfo.InvariantCulture);

        return string.Join(", ", slots);
    }

    /// <summary>Copy with a different spawn chance, keeping everything else.</summary>
    public EncounterUnit WithSpawnChance(int percent) => Clone(spawnChance: percent);

    /// <summary>Copy with a different start slot, keeping everything else.</summary>
    public EncounterUnit WithStartSlot(string slot) => Clone(startSlot: slot);

    /// <summary>Copy with a different unit name, keeping everything else.</summary>
    public EncounterUnit WithUnitName(string name) => Clone(unitName: name);

    /// <summary>Clone with the requested changes, rebuilding the raw text.</summary>
    private EncounterUnit Clone(int? spawnChance = null, string? startSlot = null, string? unitName = null)
    {
        var copy = new EncounterUnit
        {
            Slots = Slots,
            UnitName = unitName ?? UnitName,
            SpawnChancePercent = spawnChance ?? SpawnChancePercent,
            StartSlot = startSlot ?? StartSlot,
            PositionX = PositionX,
            PositionY = PositionY,
            PositionJitter = PositionJitter,
            AttributeRequirement = AttributeRequirement,
            AffinityRequirement = AffinityRequirement,
            ClassTag1 = ClassTag1,
            ClassTag2 = ClassTag2,
            ClassTag3 = ClassTag3,
            ClassTag4 = ClassTag4,
            FlagA = FlagA,
            ProhibitClasses = ProhibitClasses,
            ProhibitAttribute = ProhibitAttribute,
            ProhibitAffinity = ProhibitAffinity,
            Slot17 = Slot17,
            RequiresMercenary = RequiresMercenary,
            ItemDropChancePercent = ItemDropChancePercent,
            Flag100 = Flag100,
            Flag200 = Flag200,
        };
        copy.Raw = copy.RebuildRaw();
        return copy;
    }

    private static string Unquote(string value) => value.Trim().Trim('"');

    private static string Quote(string value) => $"\"{value.Trim().Trim('"')}\"";
}
