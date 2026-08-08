namespace ModdingGUI.Core.Api;

using System.Collections.Generic;

/// <summary>
/// Wire-level Data Transfer Object for an <c>items.tok</c> record. Each property
/// is nullable so that absent fields survive the round trip: an unset
/// <see cref="Cost"/> on read means the source line was missing, not that the
/// cost is zero.
/// </summary>
/// <remarks>
/// <see cref="ItemDto"/> is the shape the GUI and MCP tool surface exchanges;
/// it does not carry line numbers, comments, or the raw text of the underlying
/// document. The tok round-trip guarantee is maintained entirely inside
/// <see cref="IItemsApi"/> implementations.
/// </remarks>
public sealed class ItemDto
{
    /// <summary>Stable id — the first <c>ITEMCREATE</c> positional. Required on Add.</summary>
    public string Name { get; set; } = string.Empty;

    public string? Type { get; set; }
    public string? SubType { get; set; }
    public string? Style { get; set; }
    public long? StarRating { get; set; }

    public long? DescriptionId { get; set; }
    public long? DisplayNameId { get; set; }
    public long? Cost { get; set; }
    public long? MinLevel { get; set; }
    public string? Rarity { get; set; }
    public string? Mesh { get; set; }
    public string? Mesh2 { get; set; }
    public string? Material { get; set; }

    /// <summary>Elemental affinity as (Element, Amount) — null when absent.</summary>
    public (string Element, long Amount)? Affinity { get; set; }

    public string? ShowSet { get; set; }

    /// <summary>Unmodifiable list of regions (<c>ITEMREGION</c> lines, in order).</summary>
    public List<string> Regions { get; set; } = new();

    /// <summary>Unmodifiable list of skills (<c>ITEMSKILL</c> lines, in order).</summary>
    public List<string> Skills { get; set; } = new();

    public List<string> Attributes { get; set; } = new();
    public List<string> HideSets { get; set; } = new();

    /// <summary>Stat modifiers as (Stat, Value) — <c>ITEMSTATMOD</c> lines in order.</summary>
    public List<(string Stat, long Value)> StatMods { get; set; } = new();
}
