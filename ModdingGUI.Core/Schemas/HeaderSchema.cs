namespace ModdingGUI.Core.Schemas;

using System.Collections.Generic;
using ModdingGUI.Core.Tok;
using ModdingGUI.Core.Tok.Values;

/// <summary>
/// Describes the <i>record-start</i> header line of a tok record, e.g.
/// <c>ITEMCREATE: "name", "Type", "SubType", "Style", stars</c>.
/// </summary>
/// <remarks>
/// <para>
/// A header carries a fixed ordered list of positional arguments. Exactly one of
/// them may be designated the <b>stable id</b> (see
/// <see cref="PositionalFieldDescriptor.IsStableId"/>); callers find records by
/// that id and cannot alter it via patch.
/// </para>
/// </remarks>
public sealed class HeaderSchema
{
    /// <summary>The tok key that opens a record (e.g. <c>ITEMCREATE</c>).</summary>
    public string StartKey { get; }

    /// <summary>Positional argument descriptors, in order.</summary>
    public IReadOnlyList<PositionalFieldDescriptor> Positionals { get; }

    /// <summary>Convenience accessor for the stable-id positional (exactly one is required).</summary>
    public PositionalFieldDescriptor StableId { get; }

    public HeaderSchema(string startKey, IReadOnlyList<PositionalFieldDescriptor> positionals)
    {
        StartKey = startKey;
        Positionals = positionals;
        PositionalFieldDescriptor? stable = null;
        foreach (var p in positionals)
        {
            if (p.IsStableId)
            {
                if (stable is not null) throw new System.InvalidOperationException("HeaderSchema: multiple stable-id positionals defined.");
                stable = p;
            }
        }
        StableId = stable ?? throw new System.InvalidOperationException("HeaderSchema: no stable-id positional defined.");
    }

    /// <summary>
    /// Parse <paramref name="line"/>'s value list and populate <paramref name="record"/>'s positionals.
    /// Unknown extra positionals in the source are silently ignored at the typed layer but preserved
    /// on write by <see cref="RecordSchema{TRecord}.WriteHeader"/> because we keep the original
    /// header <see cref="TokValueList"/> alongside the typed record.
    /// </summary>
    public TokValueList ReadInto(object record, TokKeyValueLine line)
    {
        var list = TokValueReader.Parse(line.ValueRaw);
        foreach (var p in Positionals)
            p.ReadInto(record, list);
        return list;
    }
}
