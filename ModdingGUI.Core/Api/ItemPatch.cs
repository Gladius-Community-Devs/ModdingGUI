namespace ModdingGUI.Core.Api;

using System.Collections.Generic;

/// <summary>
/// Mutation payload for <see cref="IItemsApi.Patch"/>. Every scalar field is
/// nullable so that "absent from the patch" is distinct from "explicitly set";
/// list-valued fields use the Option B verb convention: a <c>{Field}Add</c>
/// list and a <c>{Field}Remove</c> list, both optional. Removes are processed
/// before adds, and <see cref="IItemsApi.Patch"/> deletes every line matching a
/// removed value (not just the first).
/// </summary>
/// <remarks>
/// <para>
/// The stable id (<c>Name</c>) is never part of the patch; use
/// <see cref="IItemsApi.Rename"/> to change it. The patch's target is always
/// identified by the stable id parameter passed to <see cref="IItemsApi.Patch"/>.
/// </para>
/// <para>
/// Scalar pair fields (<c>Affinity</c>) are modelled as a tuple; pass null to
/// leave unchanged, a tuple to set, or <see cref="ClearAffinity"/> to remove
/// the <c>ITEMAFFINITY</c> line.
/// </para>
/// </remarks>
public sealed class ItemPatch
{
    // ---- Header positionals (stable id is NOT included) ----

    /// <summary>New <c>ITEMCREATE</c> Type positional, or null to leave unchanged.</summary>
    public string? Type { get; set; }

    /// <summary>New <c>ITEMCREATE</c> SubType positional, or null to leave unchanged.</summary>
    public string? SubType { get; set; }

    /// <summary>New <c>ITEMCREATE</c> Style positional, or null to leave unchanged.</summary>
    public string? Style { get; set; }

    /// <summary>New <c>ITEMCREATE</c> StarRating positional, or null to leave unchanged.</summary>
    public long? StarRating { get; set; }

    // ---- Scalar body fields ----

    public long? DescriptionId { get; set; }
    public long? DisplayNameId { get; set; }
    public long? Cost { get; set; }
    public long? MinLevel { get; set; }
    public string? Rarity { get; set; }
    public string? Mesh { get; set; }
    public string? Mesh2 { get; set; }
    public string? Material { get; set; }

    /// <summary>
    /// Elemental affinity as (Element name, Amount) for <c>ITEMAFFINITY</c>.
    /// Null to leave unchanged. Ignored when <see cref="ClearAffinity"/> is true.
    /// </summary>
    public (string Element, long Amount)? Affinity { get; set; }

    /// <summary>
    /// When true, removes the <c>ITEMAFFINITY</c> line entirely (sets affinity to none).
    /// Takes precedence over <see cref="Affinity"/> when both are supplied.
    /// </summary>
    public bool ClearAffinity { get; set; }

    public string? ShowSet { get; set; }

    // ---- List body fields: Option B verbs (RemoveS run before AddS) ----

    public List<string>? RegionsAdd { get; set; }
    public List<string>? RegionsRemove { get; set; }

    public List<string>? SkillsAdd { get; set; }
    public List<string>? SkillsRemove { get; set; }

    public List<string>? AttributesAdd { get; set; }
    public List<string>? AttributesRemove { get; set; }

    public List<string>? HideSetsAdd { get; set; }
    public List<string>? HideSetsRemove { get; set; }

    /// <summary>Stat modifiers as (Stat, Value) — <c>ITEMSTATMOD</c> add list.</summary>
    public List<(string Stat, long Value)>? StatModsAdd { get; set; }

    /// <summary>Stat modifiers as (Stat, Value) — <c>ITEMSTATMOD</c> remove list.</summary>
    public List<(string Stat, long Value)>? StatModsRemove { get; set; }
}
