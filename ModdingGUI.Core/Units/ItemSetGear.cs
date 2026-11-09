namespace ModdingGUI.Core.Units;

using System;
using System.Collections.Generic;
using System.Linq;
using ModdingGUI.Core.Api;

/// <summary>
/// The five items a unit wears, in <c>ITEMSCOMP</c> order.
/// </summary>
/// <param name="Weapon">Item in the weapon slot, or null.</param>
/// <param name="Armor">Item in the armor slot, or null.</param>
/// <param name="Shield">Item in the shield slot, or null.</param>
/// <param name="Helmet">Item in the helmet slot, or null.</param>
/// <param name="Accessory">Item in the accessory slot, or null.</param>
public sealed record GearLoadout(
    string? Weapon = null,
    string? Armor = null,
    string? Shield = null,
    string? Helmet = null,
    string? Accessory = null)
{
    /// <summary>The five slots in the order <c>ITEMSCOMP</c> writes them; empty string when a slot is bare.</summary>
    public IReadOnlyList<string> InSlotOrder => new[]
    {
        Weapon ?? string.Empty,
        Armor ?? string.Empty,
        Shield ?? string.Empty,
        Helmet ?? string.Empty,
        Accessory ?? string.Empty,
    };

    /// <summary>
    /// False when the unit has nothing in the weapon slot. The engine gives such a unit nothing to attack with, so
    /// it should always be treated as a problem worth reporting.
    /// </summary>
    public bool HasWeapon => !string.IsNullOrWhiteSpace(Weapon);
}

/// <summary>
/// Picks a unit's gear from an item set, the way the engine does it.
/// </summary>
/// <remarks>
/// <para>
/// Classification is by the item's <c>Type</c> from <c>items.tok</c>, never by the <c>[Weapon]</c>-style tag in
/// <c>itemsets.txt</c>: the packer strips that tag when it writes <c>unititems.idx</c> (only
/// <c>min, max, itemId</c> survive), and the engine matches an item to a class category through
/// <c>FUN_800f96f4</c> using Type/SubType/Style. See <c>dol/equipment_rules.json</c>.
/// </para>
/// <para>
/// The level window a row carries is an <em>authoring</em> hint, not an engine rule: the hand-equip restriction is
/// the school's tier against the item's <c>ITEMMINLEVEL</c>, and gear written straight into a roster bypasses even
/// that. So this picks the row that covers the unit's level when one exists, and otherwise the nearest row of the
/// same type, rather than refusing to provide gear.
/// </para>
/// </remarks>
public static class ItemSetGear
{
    /// <summary>The five slots, in <c>ITEMSCOMP</c> order.</summary>
    public static IReadOnlyList<string> SlotOrder { get; } =
        new[] { "Weapon", "Armor", "Shield", "Helmet", "Accessory" };

    /// <summary>
    /// Choose gear for a unit of <paramref name="level"/> from <paramref name="rows"/>.
    /// </summary>
    /// <param name="rows">The item set's rows.</param>
    /// <param name="level">The unit's level; rows covering it are preferred.</param>
    /// <param name="typeOfItem">Item name to <c>items.tok</c> Type (Weapon/Armor/Shield/Helmet/Accessory).</param>
    /// <param name="warnings">Receives one line per row whose tag disagrees with its item type; optional.</param>
    public static GearLoadout ForLevel(
        IEnumerable<ItemSetRow> rows,
        int level,
        IReadOnlyDictionary<string, string> typeOfItem,
        ICollection<string>? warnings = null)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(typeOfItem);

        var classified = new List<(ItemSetRow Row, string? Type)>();
        foreach (ItemSetRow row in rows)
        {
            if (row is null || string.IsNullOrWhiteSpace(row.ItemName)) continue;

            typeOfItem.TryGetValue(row.ItemName, out string? type);
            if (type is null)
            {
                warnings?.Add($"'{row.ItemName}' is not in items.tok, so its slot cannot be determined.");
                continue;
            }

            // The tag is advisory. A disagreement is a modder's mistake that the packer would hide silently.
            string? tag = TagOf(row.Note);
            if (tag is not null && !tag.Equals(type, StringComparison.OrdinalIgnoreCase))
            {
                warnings?.Add($"'{row.ItemName}' is tagged [{tag}] but items.tok says {type}; using {type}.");
            }

            classified.Add((row, type));
        }

        string? Pick(string slot)
        {
            var candidates = classified
                .Where(entry => entry.Type!.Equals(slot, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (candidates.Length == 0) return null;

            var covering = candidates
                .Where(entry => entry.Row.MinLevel <= level && entry.Row.MaxLevel >= level)
                .OrderBy(entry => entry.Row.MinLevel)
                .ToArray();

            if (covering.Length > 0) return covering[0].Row.ItemName;

            // No row covers the level: take the nearest window, earliest on a tie, rather than leaving the slot bare.
            return candidates
                .OrderBy(entry => Distance(entry.Row, level))
                .ThenBy(entry => entry.Row.MinLevel)
                .First()
                .Row.ItemName;
        }

        return new GearLoadout(
            Weapon: Pick("Weapon"),
            Armor: Pick("Armor"),
            Shield: Pick("Shield"),
            Helmet: Pick("Helmet"),
            Accessory: Pick("Accessory"));
    }

    /// <summary>The bracketed tag on a row, if it has one.</summary>
    public static string? TagOf(string? note)
    {
        if (string.IsNullOrWhiteSpace(note)) return null;

        int open = note!.IndexOf('[');
        int close = note.IndexOf(']', open + 1);
        if (open < 0 || close <= open + 1) return null;

        return note[(open + 1)..close].Trim();
    }

    private static int Distance(ItemSetRow row, int level) =>
        level < row.MinLevel ? row.MinLevel - level
        : level > row.MaxLevel ? level - row.MaxLevel
        : 0;
}
