namespace ModdingGUI.Core.Formats.Items;

using System.Collections.Generic;
using ModdingGUI.Core.Schemas;

/// <summary>
/// Typed in-memory representation of a single record in <c>data/config/items.tok</c>.
/// </summary>
/// <remarks>
/// <para>
/// Every field corresponds to one tok header positional or body key catalogued
/// in the Phase 2 diagnostic pass against the vanilla corpus (1,852 records,
/// 15 distinct body keys). All scalar fields use <see cref="FieldSlot{T}"/> so
/// that an absent source line remains absent after round-trip.
/// </para>
/// <para>
/// Fields correspond to source keys as follows:
/// </para>
/// <list type="bullet">
///   <item><c>ITEMCREATE</c> positionals: <see cref="Name"/>, <see cref="Type"/>, <see cref="SubType"/>, <see cref="Style"/>, <see cref="StarRating"/></item>
///   <item><c>ITEMDESCRIPTIONID</c> → <see cref="DescriptionId"/></item>
///   <item><c>ITEMDISPLAYNAMEID</c> → <see cref="DisplayNameId"/></item>
///   <item><c>ITEMCOST</c> → <see cref="Cost"/></item>
///   <item><c>ITEMMINLEVEL</c> → <see cref="MinLevel"/></item>
///   <item><c>ITEMRARITY</c> → <see cref="Rarity"/></item>
///   <item><c>ITEMMESH</c> / <c>ITEMMESH2</c> → <see cref="Mesh"/> / <see cref="Mesh2"/></item>
///   <item><c>ITEMMATERIAL</c> → <see cref="Material"/></item>
///   <item><c>ITEMAFFINITY</c> → <see cref="Affinity"/> (element + amount)</item>
///   <item><c>ITEMSHOWSET</c> → <see cref="ShowSet"/></item>
///   <item><c>ITEMREGION</c> → <see cref="Regions"/> (list)</item>
///   <item><c>ITEMSKILL</c> → <see cref="Skills"/> (list)</item>
///   <item><c>ITEMATTRIBUTE</c> → <see cref="Attributes"/> (list)</item>
///   <item><c>ITEMHIDESET</c> → <see cref="HideSets"/> (list)</item>
///   <item><c>ITEMSTATMOD</c> → <see cref="StatMods"/> (list of (stat, value))</item>
/// </list>
/// </remarks>
public sealed class Item
{
    // Header positionals
    public FieldSlot<string> Name { get; } = new();
    public FieldSlot<string> Type { get; } = new();
    public FieldSlot<string> SubType { get; } = new();
    public FieldSlot<string> Style { get; } = new();
    public FieldSlot<long> StarRating { get; } = new();

    // Scalar body fields
    public FieldSlot<long> DescriptionId { get; } = new();
    public FieldSlot<long> DisplayNameId { get; } = new();
    public FieldSlot<long> Cost { get; } = new();
    public FieldSlot<long> MinLevel { get; } = new();
    public FieldSlot<string> Rarity { get; } = new();
    public FieldSlot<string> Mesh { get; } = new();
    public FieldSlot<string> Mesh2 { get; } = new();
    public FieldSlot<string> Material { get; } = new();
    /// <summary>Elemental affinity as (Element name, Amount). Decoded from <c>ITEMAFFINITY</c>.</summary>
    public FieldSlot<(string, long)> Affinity { get; } = new();
    public FieldSlot<string> ShowSet { get; } = new();

    // List body fields
    public IList<string> Regions { get; } = new List<string>();
    public IList<string> Skills { get; } = new List<string>();
    public IList<string> Attributes { get; } = new List<string>();
    public IList<string> HideSets { get; } = new List<string>();
    /// <summary>Stat modifiers as (Stat name, Value). Decoded from <c>ITEMSTATMOD</c> (multiple per record).</summary>
    public IList<(string, long)> StatMods { get; } = new List<(string, long)>();

    /// <inheritdoc />
    public override string ToString() => Name.HasValue ? $"Item({Name.Value})" : "Item(?)";
}
