namespace ModdingGUI.Core.Api;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ModdingGUI.Core.Tok.Values;

/// <summary>
/// One <c>UNITDB</c> row of an encounter team: a single unit placed on the battle grid.
/// </summary>
/// <remarks>
/// <para>
/// The argument order is not guesswork — it comes from <c>EncounterSetup</c> in the GameCube DOL
/// (<c>0x80106ddc</c>, case <c>0x1D</c> of the token switch), which reads exactly 21 values:
/// <c>string, int, string, int×5, string×4, int×12</c>. Reading them in that order is what makes
/// a row valid; the engine aborts the record on a type mismatch.
/// </para>
/// <para>
/// The names come from what the surrounding code does with each value. Three slots are passed
/// straight into unit creation without any visible test, so they are exposed by index
/// (<see cref="Slot17"/>, <see cref="Slot18"/>, <see cref="Slot19"/>) rather than given a
/// misleading name.
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

    /// <summary>Slot 7 — passed into the spawned unit unchanged (purpose not identified).</summary>
    public int Slot7 { get; init; }

    /// <summary>Slot 8 — passed into the spawned unit unchanged (purpose not identified).</summary>
    public int Slot8 { get; init; }

    /// <summary>Slot 9 — first extra class tag added to the unit (accumulates, like skill classes).</summary>
    public string ClassTag1 { get; init; } = string.Empty;

    /// <summary>Slot 10 — second extra class tag.</summary>
    public string ClassTag2 { get; init; } = string.Empty;

    /// <summary>Slot 11 — third extra class tag.</summary>
    public string ClassTag3 { get; init; } = string.Empty;

    /// <summary>Slot 12 — fourth extra class tag.</summary>
    public string ClassTag4 { get; init; } = string.Empty;

    /// <summary>Slot 13 — boolean flag (0/1) carried on the spawned unit.</summary>
    public int FlagA { get; init; }

    /// <summary>Slot 14 — boolean flag (0/1).</summary>
    public int FlagB { get; init; }

    /// <summary>Slot 15 — boolean flag (0/1).</summary>
    public int FlagC { get; init; }

    /// <summary>Slot 16 — boolean flag (0/1).</summary>
    public int FlagD { get; init; }

    /// <summary>Slot 17 — passed to unit creation (purpose not identified).</summary>
    public int Slot17 { get; init; }

    /// <summary>Slot 18 — stored on the spawned unit (purpose not identified).</summary>
    public int Slot18 { get; init; }

    /// <summary>Slot 19 — passed to unit creation (purpose not identified).</summary>
    public int Slot19 { get; init; }

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
            Slot7 = numbers[6],
            Slot8 = numbers[7],
            ClassTag1 = Unquote(slots[8]),
            ClassTag2 = Unquote(slots[9]),
            ClassTag3 = Unquote(slots[10]),
            ClassTag4 = Unquote(slots[11]),
            FlagA = numbers[12],
            FlagB = numbers[13],
            FlagC = numbers[14],
            FlagD = numbers[15],
            Slot17 = numbers[16],
            Slot18 = numbers[17],
            Slot19 = numbers[18],
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
        slots[6] = Slot7.ToString(CultureInfo.InvariantCulture);
        slots[7] = Slot8.ToString(CultureInfo.InvariantCulture);
        slots[8] = Quote(ClassTag1);
        slots[9] = Quote(ClassTag2);
        slots[10] = Quote(ClassTag3);
        slots[11] = Quote(ClassTag4);
        slots[12] = FlagA.ToString(CultureInfo.InvariantCulture);
        slots[13] = FlagB.ToString(CultureInfo.InvariantCulture);
        slots[14] = FlagC.ToString(CultureInfo.InvariantCulture);
        slots[15] = FlagD.ToString(CultureInfo.InvariantCulture);
        slots[16] = Slot17.ToString(CultureInfo.InvariantCulture);
        slots[17] = Slot18.ToString(CultureInfo.InvariantCulture);
        slots[18] = Slot19.ToString(CultureInfo.InvariantCulture);
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
            Slot7 = Slot7,
            Slot8 = Slot8,
            ClassTag1 = ClassTag1,
            ClassTag2 = ClassTag2,
            ClassTag3 = ClassTag3,
            ClassTag4 = ClassTag4,
            FlagA = FlagA,
            FlagB = FlagB,
            FlagC = FlagC,
            FlagD = FlagD,
            Slot17 = Slot17,
            Slot18 = Slot18,
            Slot19 = Slot19,
            Flag100 = Flag100,
            Flag200 = Flag200,
        };
        copy.Raw = copy.RebuildRaw();
        return copy;
    }

    private static string Unquote(string value) => value.Trim().Trim('"');

    private static string Quote(string value) => $"\"{value.Trim().Trim('"')}\"";
}
