namespace ModdingGUI.Core.Schemas;

using System;
using System.Collections.Generic;
using ModdingGUI.Core.Schemas.Codecs;
using ModdingGUI.Core.Tok;
using ModdingGUI.Core.Tok.Values;

/// <summary>
/// Typed descriptor for a <see cref="Cardinality.Single"/> body field on a
/// record of type <typeparamref name="TRecord"/> with a value of type
/// <typeparamref name="TValue"/>.
/// </summary>
/// <remarks>
/// <para>
/// Stores a typed getter/setter and a <see cref="ITokValueCodec{TValue}"/>. The
/// field stores an <c>IsSet</c> companion so that absent fields round-trip
/// correctly: on read, if the tok body never contained the key for a given
/// record, the record's slot stays default and <c>HasValue</c> is false.
/// </para>
/// <para>
/// The descriptor is constructed via <see cref="FieldDescriptor{TRecord,TValue}.Create"/>
/// with explicit accessor lambdas; reflection is not used at runtime.
/// </para>
/// </remarks>
public sealed class FieldDescriptor<TRecord, TValue> : FieldDescriptor
    where TRecord : class
{
    private readonly Func<TRecord, FieldSlot<TValue>> _slot;
    private readonly ITokValueCodec<TValue> _codec;

    private FieldDescriptor(string key, ITokValueCodec<TValue> codec, Func<TRecord, FieldSlot<TValue>> slot)
        : base(key, Cardinality.Single)
    {
        _slot = slot;
        _codec = codec;
    }

    /// <summary>
    /// Create a descriptor for a single-valued field.
    /// </summary>
    /// <param name="key">Tok body key (e.g. <c>ITEMCOST</c>).</param>
    /// <param name="codec">Value codec.</param>
    /// <param name="slot">Accessor returning the <see cref="FieldSlot{T}"/> storage on a record instance.</param>
    public static FieldDescriptor<TRecord, TValue> Create(
        string key, ITokValueCodec<TValue> codec, Func<TRecord, FieldSlot<TValue>> slot)
        => new(key, codec, slot);

    /// <inheritdoc />
    public override void ReadInto(object record, TokKeyValueLine line)
    {
        var rec = (TRecord)record;
        var parsed = TokValueReader.Parse(line.ValueRaw);
        if (_codec.TryDecode(parsed, out TValue value))
            _slot(rec).Set(value);
        // else: leave slot unset; caller keeps raw line unchanged.
    }

    /// <inheritdoc />
    public override IEnumerable<TokValueList> EncodeCurrent(object record)
    {
        var rec = (TRecord)record;
        var slot = _slot(rec);
        if (slot.HasValue) yield return _codec.Encode(slot.Value!);
    }

    /// <summary>Strongly-typed getter used by schema-internal code paths.</summary>
    internal FieldSlot<TValue> GetSlot(TRecord record) => _slot(record);

    /// <summary>Expose the codec for framework use (e.g. decoding a patch DTO).</summary>
    internal ITokValueCodec<TValue> Codec => _codec;
}
