namespace ModdingGUI.Core.Api;

using System.Globalization;

/// <summary>
/// Applies a named, validated value to one <c>UNITDB</c> slot.
/// </summary>
/// <remarks>
/// <para>
/// The point is to keep the "nice input" and the file format apart: an editor (or an agent) says
/// <c>spawnChance = 50</c> or <c>classTag1 = Summoner</c>, and this decides whether the value is acceptable and writes it in
/// the file's own style - quoted where the engine expects a string, bare where it expects a number. A value that the engine
/// would not read the same way is rejected with a reason instead of being written and silently ignored in game.
/// </para>
/// <para>
/// Validation uses <see cref="EncounterUnitFields"/>: a choice field must be one of its choices, a numeric field must parse
/// and sit inside its range (when one is declared), and a switch must be 0 or 1.
/// </para>
/// </remarks>
public static class EncounterUnitEdit
{
    /// <summary>
    /// Apply one field. Returns false and a reason when the value is not acceptable; on success
    /// <paramref name="updated"/> is a new unit and the original is untouched.
    /// </summary>
    public static bool TryApply(EncounterUnit source, int slot, string text, out EncounterUnit? updated, out string? error)
    {
        ArgumentNullException.ThrowIfNull(source);
        updated = null;
        error = null;

        EncounterUnitField? field = EncounterUnitFields.ForSlot(slot);
        if (field is null)
        {
            error = $"slot {slot} is outside 1..{EncounterUnit.SlotCount}";
            return false;
        }

        string trimmed = (text ?? string.Empty).Trim();
        string stored;

        switch (field.Kind)
        {
            case EncounterFieldKind.Text:
            case EncounterFieldKind.ClassTag:
                // Strings go into the file quoted; the slot keeps whatever the caller typed inside the quotes.
                stored = "\"" + trimmed.Trim('"') + "\"";
                break;

            case EncounterFieldKind.Switch:
            {
                if (trimmed is not ("0" or "1"))
                {
                    error = $"{field.Key} takes 0 or 1, not '{trimmed}'";
                    return false;
                }

                stored = trimmed;
                break;
            }

            case EncounterFieldKind.Choice:
            {
                if (field.Choices is null || !field.Choices.Contains(trimmed, StringComparer.OrdinalIgnoreCase))
                {
                    error = $"{field.Key} must be one of {string.Join(", ", field.Choices ?? Array.Empty<string>())}, not '{trimmed}'";
                    return false;
                }

                stored = trimmed;
                break;
            }

            case EncounterFieldKind.Integer:
            case EncounterFieldKind.Bitmask:
            {
                if (!int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number))
                {
                    error = $"{field.Key} takes a whole number, not '{trimmed}'";
                    return false;
                }

                if (field.Minimum is int minimum && number < minimum)
                {
                    error = $"{field.Key} must be at least {minimum}, not {number}";
                    return false;
                }

                if (field.Maximum is int maximum && number > maximum)
                {
                    error = $"{field.Key} must be at most {maximum}, not {number}";
                    return false;
                }

                if (field.Kind == EncounterFieldKind.Bitmask && number < 0)
                {
                    error = $"{field.Key} is a bitmask, so it cannot be negative";
                    return false;
                }

                stored = number.ToString(CultureInfo.InvariantCulture);
                break;
            }

            default:
                error = $"{field.Key} has an unsupported kind {field.Kind}";
                return false;
        }

        if (source.Slots.Count != EncounterUnit.SlotCount)
        {
            error = "the row does not have 21 slots, so it cannot be edited field by field";
            return false;
        }

        string[] slots = source.Slots.ToArray();
        slots[slot - 1] = stored;

        EncounterUnit? rebuilt = EncounterUnit.TryParse(string.Join(", ", slots));
        if (rebuilt is null)
        {
            error = "the edited row would not parse back as a UNITDB value";
            return false;
        }

        updated = rebuilt;
        return true;
    }

    /// <summary>Apply several fields at once; stops at the first rejected value.</summary>
    public static bool TryApplyAll(
        EncounterUnit source,
        IReadOnlyDictionary<string, string> values,
        out EncounterUnit? updated,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(values);

        EncounterUnit current = source;
        error = null;

        foreach ((string key, string value) in values)
        {
            EncounterUnitField? field = EncounterUnitFields.ForKey(key);
            if (field is null)
            {
                updated = null;
                error = $"unknown unit field '{key}'";
                return false;
            }

            if (!TryApply(current, field.Slot, value, out EncounterUnit? next, out error) || next is null)
            {
                updated = null;
                return false;
            }

            current = next;
        }

        updated = current;
        return true;
    }
}
