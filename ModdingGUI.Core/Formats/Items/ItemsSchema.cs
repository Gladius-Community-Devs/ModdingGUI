namespace ModdingGUI.Core.Formats.Items;

using System.Collections.Generic;
using ModdingGUI.Core.Schemas;
using ModdingGUI.Core.Schemas.Codecs;

/// <summary>
/// Schema for <c>data/config/items.tok</c>. See <see cref="Item"/> for the typed
/// record. Captures all 15 body keys observed in the vanilla corpus, plus the
/// 5 header positionals on <c>ITEMCREATE</c>.
/// </summary>
/// <remarks>
/// <para>
/// Body indent is a single tab character (matches the vanilla corpus). Records
/// are separated by blank lines in the source; blanks between records belong to
/// the preceding record's line range and therefore round-trip unchanged.
/// </para>
/// </remarks>
public sealed class ItemsSchema : RecordSchema<Item>
{
    /// <inheritdoc />
    public override string FormatId => "items";

    /// <inheritdoc />
    public override string StartKey => "ITEMCREATE";

    /// <inheritdoc />
    public override bool BodyIsIndented => true;

    /// <inheritdoc />
    public override string BodyIndent => "\t";

    private static readonly StringCodec s_quotedString = new(alwaysQuote: true);
    private static readonly StringCodec s_bareString = new();
    private static readonly IntCodec s_int = new();
    /// <summary>Used for ITEMSTATMOD — corpus format is <c>defense, 5</c> (comma-space).</summary>
    private static readonly Tuple2Codec<string, long> s_identIntPair = new(s_bareString, s_int, ", ");
    /// <summary>Used for ITEMAFFINITY — corpus format is <c>Water , 10</c> (space-comma-space).</summary>
    private static readonly Tuple2Codec<string, long> s_affinityPair = new(s_bareString, s_int, " , ");

    private readonly HeaderSchema _header;
    private readonly IReadOnlyDictionary<string, FieldDescriptor> _bodyFields;

    public ItemsSchema()
    {
        _header = new HeaderSchema(
            StartKey,
            new PositionalFieldDescriptor[]
            {
                PositionalFieldDescriptor<Item, string>.Create(0, nameof(Item.Name), s_quotedString, r => r.Name, isStableId: true),
                PositionalFieldDescriptor<Item, string>.Create(1, nameof(Item.Type), s_quotedString, r => r.Type),
                PositionalFieldDescriptor<Item, string>.Create(2, nameof(Item.SubType), s_quotedString, r => r.SubType),
                PositionalFieldDescriptor<Item, string>.Create(3, nameof(Item.Style), s_quotedString, r => r.Style),
                PositionalFieldDescriptor<Item, long>.Create(4, nameof(Item.StarRating), s_int, r => r.StarRating),
            });

        var body = new Dictionary<string, FieldDescriptor>(System.StringComparer.Ordinal)
        {
            ["ITEMDESCRIPTIONID"] = FieldDescriptor<Item, long>.Create("ITEMDESCRIPTIONID", s_int, r => r.DescriptionId),
            ["ITEMDISPLAYNAMEID"] = FieldDescriptor<Item, long>.Create("ITEMDISPLAYNAMEID", s_int, r => r.DisplayNameId),
            ["ITEMCOST"] = FieldDescriptor<Item, long>.Create("ITEMCOST", s_int, r => r.Cost),
            ["ITEMMINLEVEL"] = FieldDescriptor<Item, long>.Create("ITEMMINLEVEL", s_int, r => r.MinLevel),
            ["ITEMRARITY"] = FieldDescriptor<Item, string>.Create("ITEMRARITY", s_quotedString, r => r.Rarity),
            ["ITEMMESH"] = FieldDescriptor<Item, string>.Create("ITEMMESH", s_quotedString, r => r.Mesh),
            ["ITEMMESH2"] = FieldDescriptor<Item, string>.Create("ITEMMESH2", s_quotedString, r => r.Mesh2),
            ["ITEMMATERIAL"] = FieldDescriptor<Item, string>.Create("ITEMMATERIAL", s_quotedString, r => r.Material),
            ["ITEMAFFINITY"] = FieldDescriptor<Item, (string, long)>.Create("ITEMAFFINITY", s_affinityPair, r => r.Affinity),
            ["ITEMSHOWSET"] = FieldDescriptor<Item, string>.Create("ITEMSHOWSET", s_quotedString, r => r.ShowSet),
            ["ITEMREGION"] = ListFieldDescriptor<Item, string>.Create("ITEMREGION", s_quotedString, r => r.Regions),
            ["ITEMSKILL"] = ListFieldDescriptor<Item, string>.Create("ITEMSKILL", s_quotedString, r => r.Skills),
            ["ITEMATTRIBUTE"] = ListFieldDescriptor<Item, string>.Create("ITEMATTRIBUTE", s_quotedString, r => r.Attributes),
            ["ITEMHIDESET"] = ListFieldDescriptor<Item, string>.Create("ITEMHIDESET", s_quotedString, r => r.HideSets),
            ["ITEMSTATMOD"] = ListFieldDescriptor<Item, (string, long)>.Create("ITEMSTATMOD", s_identIntPair, r => r.StatMods),
        };
        _bodyFields = body;
    }

    /// <inheritdoc />
    public override HeaderSchema Header => _header;

    /// <inheritdoc />
    public override IReadOnlyDictionary<string, FieldDescriptor> BodyFields => _bodyFields;
}
