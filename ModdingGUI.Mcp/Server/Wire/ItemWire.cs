namespace ModdingGUI.Mcp.Server.Wire;

using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using ModdingGUI.Core.Api;

/// <summary>An elemental affinity as a named pair — the wire form of <c>(string Element, long Amount)</c>.</summary>
public sealed class AffinityWire
{
    /// <summary>Element name, e.g. <c>Fire</c>.</summary>
    public string Element { get; set; } = string.Empty;

    /// <summary>Affinity amount.</summary>
    public long Amount { get; set; }
}

/// <summary>A stat modifier as a named pair — the wire form of <c>(string Stat, long Value)</c>.</summary>
public sealed class StatModWire
{
    /// <summary>Stat name, e.g. <c>PWR</c>.</summary>
    public string Stat { get; set; } = string.Empty;

    /// <summary>Modifier value; negative is a penalty.</summary>
    public long Value { get; set; }
}

/// <summary>
/// <see cref="ItemDto"/> on the wire.
/// </summary>
/// <remarks>
/// Every property is identical to <see cref="ItemDto"/> except the two tuple fields: System.Text.Json
/// cannot represent a <c>ValueTuple</c> (its members are fields, so it round-trips as <c>{}</c>), and
/// losing an item's affinity silently is worse than one extra mapping type. The pair fields become
/// <c>{"element": "Fire", "amount": 100}</c> / <c>{"stat": "PWR", "value": 2}</c>.
/// </remarks>
public sealed class ItemWire
{
    /// <summary>Stable id — the first <c>ITEMCREATE</c> positional. Required on add.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Item type, e.g. <c>weapon</c>.</summary>
    public string? Type { get; set; }

    /// <summary>Item sub-type, e.g. <c>Sword</c>.</summary>
    public string? SubType { get; set; }

    /// <summary>Style, e.g. <c>Plain</c>.</summary>
    public string? Style { get; set; }

    /// <summary>Star rating (1-5 in vanilla).</summary>
    public long? StarRating { get; set; }

    /// <summary>Lookup-text id of the description.</summary>
    public long? DescriptionId { get; set; }

    /// <summary>Lookup-text id of the display name.</summary>
    public long? DisplayNameId { get; set; }

    /// <summary>Shop cost.</summary>
    public long? Cost { get; set; }

    /// <summary>Minimum unit level that can use it.</summary>
    public long? MinLevel { get; set; }

    /// <summary>Rarity, e.g. <c>Common</c>.</summary>
    public string? Rarity { get; set; }

    /// <summary>Mesh name.</summary>
    public string? Mesh { get; set; }

    /// <summary>Second mesh name.</summary>
    public string? Mesh2 { get; set; }

    /// <summary>Material name.</summary>
    public string? Material { get; set; }

    /// <summary>Elemental affinity, or null when the item has none.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AffinityWire? Affinity { get; set; }

    /// <summary><c>SHOWSET</c> value.</summary>
    public string? ShowSet { get; set; }

    /// <summary>Regions the item appears in (<c>ITEMREGION</c> lines, in order).</summary>
    public List<string> Regions { get; set; } = new();

    /// <summary>Skills granted by the item (<c>ITEMSKILL</c> lines, in order).</summary>
    public List<string> Skills { get; set; } = new();

    /// <summary>Attributes the item grants.</summary>
    public List<string> Attributes { get; set; } = new();

    /// <summary>Hide sets the item belongs to.</summary>
    public List<string> HideSets { get; set; } = new();

    /// <summary>Stat modifiers (<c>ITEMSTATMOD</c> lines, in order).</summary>
    public List<StatModWire> StatMods { get; set; } = new();

    /// <summary>Project a Core DTO onto the wire shape.</summary>
    public static ItemWire From(ItemDto item) => new()
    {
        Name = item.Name,
        Type = item.Type,
        SubType = item.SubType,
        Style = item.Style,
        StarRating = item.StarRating,
        DescriptionId = item.DescriptionId,
        DisplayNameId = item.DisplayNameId,
        Cost = item.Cost,
        MinLevel = item.MinLevel,
        Rarity = item.Rarity,
        Mesh = item.Mesh,
        Mesh2 = item.Mesh2,
        Material = item.Material,
        Affinity = item.Affinity is { } affinity
            ? new AffinityWire { Element = affinity.Element, Amount = affinity.Amount }
            : null,
        ShowSet = item.ShowSet,
        Regions = new List<string>(item.Regions),
        Skills = new List<string>(item.Skills),
        Attributes = new List<string>(item.Attributes),
        HideSets = new List<string>(item.HideSets),
        StatMods = item.StatMods.Select(mod => new StatModWire { Stat = mod.Stat, Value = mod.Value }).ToList(),
    };

    /// <summary>Project the wire shape onto a Core DTO.</summary>
    public ItemDto ToDto() => new()
    {
        Name = Name,
        Type = Type,
        SubType = SubType,
        Style = Style,
        StarRating = StarRating,
        DescriptionId = DescriptionId,
        DisplayNameId = DisplayNameId,
        Cost = Cost,
        MinLevel = MinLevel,
        Rarity = Rarity,
        Mesh = Mesh,
        Mesh2 = Mesh2,
        Material = Material,
        Affinity = Affinity is null ? null : (Affinity.Element, Affinity.Amount),
        ShowSet = ShowSet,
        Regions = new List<string>(Regions),
        Skills = new List<string>(Skills),
        Attributes = new List<string>(Attributes),
        HideSets = new List<string>(HideSets),
        StatMods = StatMods.Select(mod => (mod.Stat, mod.Value)).ToList(),
    };
}

/// <summary>
/// <see cref="ItemPatch"/> on the wire. Null means "leave unchanged"; list fields use the
/// <c>…Add</c>/<c>…Remove</c> verb pairs the Core patch already defines, and removes run before adds.
/// </summary>
public sealed class ItemPatchWire
{
    /// <summary>New type, or null to leave it.</summary>
    public string? Type { get; set; }

    /// <summary>New sub-type, or null to leave it.</summary>
    public string? SubType { get; set; }

    /// <summary>New style, or null to leave it.</summary>
    public string? Style { get; set; }

    /// <summary>New star rating, or null to leave it.</summary>
    public long? StarRating { get; set; }

    /// <summary>New description lookup id, or null to leave it.</summary>
    public long? DescriptionId { get; set; }

    /// <summary>New display-name lookup id, or null to leave it.</summary>
    public long? DisplayNameId { get; set; }

    /// <summary>New cost, or null to leave it.</summary>
    public long? Cost { get; set; }

    /// <summary>New minimum level, or null to leave it.</summary>
    public long? MinLevel { get; set; }

    /// <summary>New rarity, or null to leave it.</summary>
    public string? Rarity { get; set; }

    /// <summary>New mesh, or null to leave it.</summary>
    public string? Mesh { get; set; }

    /// <summary>New second mesh, or null to leave it.</summary>
    public string? Mesh2 { get; set; }

    /// <summary>New material, or null to leave it.</summary>
    public string? Material { get; set; }

    /// <summary>New elemental affinity, or null to leave it.</summary>
    public AffinityWire? Affinity { get; set; }

    /// <summary>True to remove the item's affinity line entirely.</summary>
    public bool ClearAffinity { get; set; }

    /// <summary>New <c>SHOWSET</c>, or null to leave it.</summary>
    public string? ShowSet { get; set; }

    /// <summary>Regions to append.</summary>
    public List<string>? RegionsAdd { get; set; }

    /// <summary>Regions to remove.</summary>
    public List<string>? RegionsRemove { get; set; }

    /// <summary>Skills to append.</summary>
    public List<string>? SkillsAdd { get; set; }

    /// <summary>Skills to remove.</summary>
    public List<string>? SkillsRemove { get; set; }

    /// <summary>Attributes to append.</summary>
    public List<string>? AttributesAdd { get; set; }

    /// <summary>Attributes to remove.</summary>
    public List<string>? AttributesRemove { get; set; }

    /// <summary>Hide sets to append.</summary>
    public List<string>? HideSetsAdd { get; set; }

    /// <summary>Hide sets to remove.</summary>
    public List<string>? HideSetsRemove { get; set; }

    /// <summary>Stat modifiers to append.</summary>
    public List<StatModWire>? StatModsAdd { get; set; }

    /// <summary>Stat modifiers to remove.</summary>
    public List<StatModWire>? StatModsRemove { get; set; }

    /// <summary>Project the wire shape onto the Core patch.</summary>
    public ItemPatch ToPatch() => new()
    {
        Type = Type,
        SubType = SubType,
        Style = Style,
        StarRating = StarRating,
        DescriptionId = DescriptionId,
        DisplayNameId = DisplayNameId,
        Cost = Cost,
        MinLevel = MinLevel,
        Rarity = Rarity,
        Mesh = Mesh,
        Mesh2 = Mesh2,
        Material = Material,
        Affinity = Affinity is null ? null : (Affinity.Element, Affinity.Amount),
        ClearAffinity = ClearAffinity,
        ShowSet = ShowSet,
        RegionsAdd = RegionsAdd,
        RegionsRemove = RegionsRemove,
        SkillsAdd = SkillsAdd,
        SkillsRemove = SkillsRemove,
        AttributesAdd = AttributesAdd,
        AttributesRemove = AttributesRemove,
        HideSetsAdd = HideSetsAdd,
        HideSetsRemove = HideSetsRemove,
        StatModsAdd = StatModsAdd?.Select(mod => (mod.Stat, mod.Value)).ToList(),
        StatModsRemove = StatModsRemove?.Select(mod => (mod.Stat, mod.Value)).ToList(),
    };
}
