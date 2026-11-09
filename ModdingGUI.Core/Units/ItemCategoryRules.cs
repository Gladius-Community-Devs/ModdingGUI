namespace ModdingGUI.Core.Units;

using System;
using System.Collections.Generic;
using System.Linq;
using ModdingGUI.Core.Api;

/// <summary>
/// Whether a class may use an item, following the rule the engine applies in <c>FUN_800f96f4</c>.
/// </summary>
/// <remarks>
/// <para>
/// A class lists what it accepts as <c>ITEMCAT: type, subtype, style</c> lines, e.g. <c>weapon, Bow, Plain</c> or
/// <c>helmet, &lt;Any&gt;, Archer</c>. The engine matches the item's Type, SubType and Style from <c>items.tok</c>
/// against a category: type and style must be equal, and subtype must be equal unless the category's subtype is the
/// wildcard <c>&lt;Any&gt;</c> (byte 1 in the data).
/// </para>
/// <para>
/// This is advisory in the tool because gear written into a roster's <c>ITEMSCOMP</c> is applied directly - the engine
/// validates nothing on that path. It is still the rule a player's equip menu would enforce, so a unit that violates
/// it looks legal and behaves oddly.
/// </para>
/// </remarks>
public static class ItemCategoryRules
{
    /// <summary>The literal a category uses to accept any subtype.</summary>
    public const string Any = "<Any>";

    /// <summary>One parsed <c>ITEMCAT</c> line.</summary>
    /// <param name="Type">Type the category accepts, e.g. <c>weapon</c>.</param>
    /// <param name="SubType">Subtype, or <see cref="Any"/>.</param>
    /// <param name="Style">Style, e.g. <c>Plain</c>.</param>
    public sealed record Category(string Type, string SubType, string Style);

    /// <summary>Parse the class's <c>ITEMCAT</c> values into categories, skipping malformed lines.</summary>
    public static IReadOnlyList<Category> Parse(ClassDefDto? classDef)
    {
        if (classDef is null) return Array.Empty<Category>();

        var categories = new List<Category>();

        foreach (string raw in classDef.Values("ITEMCAT"))
        {
            string[] parts = raw.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3) continue;

            categories.Add(new Category(parts[0], parts[1], parts[2]));
        }

        return categories;
    }

    /// <summary>
    /// True when the class accepts an item with these properties. A class with no categories accepts nothing.
    /// </summary>
    public static bool Allows(
        IReadOnlyList<Category> categories,
        string? type,
        string? subType,
        string? style)
    {
        ArgumentNullException.ThrowIfNull(categories);
        if (string.IsNullOrWhiteSpace(type)) return false;

        return categories.Any(category =>
            category.Type.Equals(type, StringComparison.OrdinalIgnoreCase)
            && (category.SubType.Equals(Any, StringComparison.OrdinalIgnoreCase)
                || category.SubType.Equals(subType ?? string.Empty, StringComparison.OrdinalIgnoreCase))
            && category.Style.Equals(style ?? string.Empty, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Why an item is refused, phrased for a log line; null when it is allowed.</summary>
    public static string? Explain(
        ClassDefDto? classDef,
        string itemName,
        string? type,
        string? subType,
        string? style)
    {
        var categories = Parse(classDef);
        if (Allows(categories, type, subType, style)) return null;

        string classItems = categories.Count == 0
            ? "it lists no ITEMCAT entries"
            : "it accepts " + string.Join("; ", categories.Select(c => $"{c.Type} {c.SubType} {c.Style}"));

        return $"'{itemName}' is {type ?? "?"} {subType ?? "?"} {style ?? "?"}, which " +
               $"{classDef?.Name ?? "the class"} cannot use ({classItems}).";
    }

    /// <summary>
    /// Check a whole loadout against a class. Returns one line per item the class cannot use.
    /// </summary>
    /// <param name="classDef">The class to check.</param>
    /// <param name="gear">The loadout.</param>
    /// <param name="itemsByName">Item properties keyed by name, usually <c>IItemsApi.GetAll</c>.</param>
    public static IReadOnlyList<string> Validate(
        ClassDefDto? classDef,
        GearLoadout gear,
        IReadOnlyDictionary<string, ItemDto> itemsByName)
    {
        ArgumentNullException.ThrowIfNull(gear);
        ArgumentNullException.ThrowIfNull(itemsByName);

        var problems = new List<string>();

        foreach (string name in gear.InSlotOrder)
        {
            if (string.IsNullOrWhiteSpace(name)) continue;

            if (!itemsByName.TryGetValue(name, out ItemDto? item))
            {
                problems.Add($"'{name}' is not in items.tok.");
                continue;
            }

            string? problem = Explain(classDef, item.Name, item.Type, item.SubType, item.Style);
            if (problem is not null) problems.Add(problem);
        }

        return problems;
    }
}
