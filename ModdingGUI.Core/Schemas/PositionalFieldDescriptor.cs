namespace ModdingGUI.Core.Schemas;

using System;
using System.Collections.Generic;
using ModdingGUI.Core.Schemas.Codecs;
using ModdingGUI.Core.Tok.Values;

/// <summary>
/// Describes one positional argument in a <see cref="RecordSchema{TRecord}"/>'s
/// header line, e.g. the <c>"Type"</c> or <c>StarRating</c> argument of
/// <c>ITEMCREATE: "name", "Type", "SubType", "Style", stars</c>.
/// </summary>
/// <remarks>
/// <para>
/// Each positional has a stable 0-based <see cref="Index"/> into the header's
/// <see cref="TokValueList"/>. Positionals may be read-only
/// (<see cref="IsStableId"/> = <see langword="true"/>) — the stable id cannot be
/// edited via a patch; to change it, use the schema's rename operation.
/// </para>
/// </remarks>
public abstract class PositionalFieldDescriptor
{
    /// <summary>Zero-based position of this argument within the header value list.</summary>
    public int Index { get; }

    /// <summary>Human-readable field name (for diagnostics and schema-defined getter mapping).</summary>
    public string Name { get; }

    /// <summary>
    /// When <see langword="true"/>, the value at this position is the record's
    /// stable id (e.g. Name for Item). It is returned by
    /// <see cref="RecordSchema{TRecord}.GetStableId"/> and cannot be edited via
    /// patch.
    /// </summary>
    public bool IsStableId { get; }

    protected PositionalFieldDescriptor(int index, string name, bool isStableId)
    {
        if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));
        if (string.IsNullOrEmpty(name)) throw new ArgumentException("Name required.", nameof(name));
        Index = index;
        Name = name;
        IsStableId = isStableId;
    }

    /// <summary>Read the value at <see cref="Index"/> out of <paramref name="header"/> into <paramref name="record"/>.</summary>
    public abstract void ReadInto(object record, TokValueList header);

    /// <summary>Produce a single-token <see cref="TokValueList"/> for this positional from <paramref name="record"/>.</summary>
    public abstract TokValueList? EncodeCurrent(object record);
}

/// <summary>
/// Typed positional descriptor using a <see cref="FieldSlot{T}"/> accessor.
/// </summary>
public sealed class PositionalFieldDescriptor<TRecord, TValue> : PositionalFieldDescriptor
    where TRecord : class
{
    private readonly Func<TRecord, FieldSlot<TValue>> _slot;
    private readonly ITokValueCodec<TValue> _codec;

    private PositionalFieldDescriptor(
        int index, string name, bool isStableId,
        ITokValueCodec<TValue> codec, Func<TRecord, FieldSlot<TValue>> slot)
        : base(index, name, isStableId)
    {
        _codec = codec;
        _slot = slot;
    }

    public static PositionalFieldDescriptor<TRecord, TValue> Create(
        int index, string name, ITokValueCodec<TValue> codec, Func<TRecord, FieldSlot<TValue>> slot, bool isStableId = false)
        => new(index, name, isStableId, codec, slot);

    /// <inheritdoc />
    public override void ReadInto(object record, TokValueList header)
    {
        if (Index >= header.Tokens.Count) return;
        var rec = (TRecord)record;
        var sublist = TokValueList.Of(header.Tokens[Index]);
        if (_codec.TryDecode(sublist, out TValue value))
            _slot(rec).Set(value);
    }

    /// <inheritdoc />
    public override TokValueList? EncodeCurrent(object record)
    {
        var rec = (TRecord)record;
        var slot = _slot(rec);
        if (!slot.HasValue) return null;
        return _codec.Encode(slot.Value!);
    }

    internal FieldSlot<TValue> GetSlot(TRecord record) => _slot(record);
    internal ITokValueCodec<TValue> Codec => _codec;
}
