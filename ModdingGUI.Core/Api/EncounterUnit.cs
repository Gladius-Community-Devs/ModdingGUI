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
/// Slots 1-3 (name, spawn chance, start marker), 4-6 (level bounds and their jitter) and 20-21 (spawn
/// flags 0x100, 0x200) are confirmed by the engine. Slots 4-6 are LEVELS, not coordinates:
/// <c>EncounterSetup</c> adds the school's average level (<c>FUN_800f7c50</c>) to slot 4 and slot 5, adds it
/// again at random when slot 6 is set, clamps both to at least 1, and <c>FUN_80108828</c> stores the pair on the
/// created unit at +0x10 and +0x14. Placement comes from the start marker (slot 3) on the arena grid instead.
/// A previous note in this file - and devnotes.txt before it - called these three slots coordinates; tracing the
/// struct into <c>FUN_80108828</c> showed otherwise.
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

    /// <summary>Slot 4 - lower bound on the spawned level, as an offset added to the school's average level.</summary>
    public int Slot4LevelMinOffset { get; init; }

    /// <summary>Slot 5 - upper bound on the spawned level, added to the school average like slot 4.</summary>
    public int Slot5LevelMaxOffset { get; init; }

    /// <summary>Slot 6 - non-zero makes both bounds random per spawn: the school average is added twice, independently.</summary>
    public int Slot6LevelJitter { get; init; }

    /// <summary>
    /// Slot 7 — attribute requirement as a bitmask over the unit's class tags (vanilla uses 1, 2, 4, 8,
    /// 16, 32, 256, 512, 1056, 4224 …). <c>FUN_8010ad40</c> tests it against the candidate's class tag
    /// bits (<c>classdef &amp; 0xf00</c>), and slot 15 flips it to "must NOT have".
    /// </summary>
    public int AttributeRequirement { get; init; }

    /// <summary>
    /// Slot 8 — affinity requirement in <b>engine</b> affinity ids: <c>-1</c> = none, otherwise
    /// 1 Earth, 2 Water, 3 Dark, 4 Air, 5 Fire, 6 Light. Vanilla uses <c>-1</c> (6,302 rows) or 1-5
    /// (400 rows).
    /// </summary>
    /// <remarks>
    /// Checked against the candidate unit's affinity, which comes from its <c>gladiators.txt</c> record:
    /// <c>FUN_8010ad40</c> compares this value with the byte at unit +0x82 and rejects a mismatch, unless
    /// slot 16 (<see cref="ProhibitAffinity"/>) is set, in which case a match is rejected instead. This is
    /// what the gladiator <c>Affinity</c> field is for — it decides whether a unit may fill a slot, not
    /// how it fights.
    /// </remarks>
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
    /// Slot 13 — flips the class-tag bitmask (the <c>0xf00</c> bits of <see cref="AttributeRequirement"/>)
    /// to "prohibited". This is the flag the modder notes listed as "unsure": vanilla's 40 rows that set it
    /// all carry class-tag bits and none carry an affinity requirement, and <c>FUN_8010ad40</c> inverts the
    /// tag test on it.
    /// </summary>
    public int ProhibitClassTags { get; init; }

    /// <summary>
    /// Slot 14 — flips the class list from "required" to "prohibited". <c>FUN_8010ad40</c> reads it as the
    /// byte at requirement +0x12 and inverts the class-tag test.
    /// </summary>
    public int ProhibitClasses { get; init; }

    /// <summary>
    /// Slot 15 — flips the attribute bits (the <c>0x103f</c> range of
    /// <see cref="AttributeRequirement"/>) to "prohibited"; the engine reads it as the byte at
    /// requirement +0x10 and inverts that test. Vanilla's 235 rows that set it all carry such bits.
    /// </summary>
    public int ProhibitAttribute { get; init; }

    /// <summary>
    /// Slot 16 — flips <see cref="AffinityRequirement"/> to "prohibited". 34 vanilla rows use it: the unit
    /// filling that slot must NOT have the listed affinity.
    /// </summary>
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
            Slot4LevelMinOffset = numbers[3],
            Slot5LevelMaxOffset = numbers[4],
            Slot6LevelJitter = numbers[5],
            AttributeRequirement = numbers[6],
            AffinityRequirement = numbers[7],
            ClassTag1 = Unquote(slots[8]),
            ClassTag2 = Unquote(slots[9]),
            ClassTag3 = Unquote(slots[10]),
            ClassTag4 = Unquote(slots[11]),
            ProhibitClassTags = numbers[12],
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
        slots[3] = Slot4LevelMinOffset.ToString(CultureInfo.InvariantCulture);
        slots[4] = Slot5LevelMaxOffset.ToString(CultureInfo.InvariantCulture);
        slots[5] = Slot6LevelJitter.ToString(CultureInfo.InvariantCulture);
        slots[6] = AttributeRequirement.ToString(CultureInfo.InvariantCulture);
        slots[7] = AffinityRequirement.ToString(CultureInfo.InvariantCulture);
        slots[8] = Quote(ClassTag1);
        slots[9] = Quote(ClassTag2);
        slots[10] = Quote(ClassTag3);
        slots[11] = Quote(ClassTag4);
        slots[12] = ProhibitClassTags.ToString(CultureInfo.InvariantCulture);
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
            Slot4LevelMinOffset = Slot4LevelMinOffset,
            Slot5LevelMaxOffset = Slot5LevelMaxOffset,
            Slot6LevelJitter = Slot6LevelJitter,
            AttributeRequirement = AttributeRequirement,
            AffinityRequirement = AffinityRequirement,
            ClassTag1 = ClassTag1,
            ClassTag2 = ClassTag2,
            ClassTag3 = ClassTag3,
            ClassTag4 = ClassTag4,
            ProhibitClassTags = ProhibitClassTags,
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
