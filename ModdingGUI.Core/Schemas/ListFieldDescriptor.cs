namespace ModdingGUI.Core.Schemas;

using System;
using System.Collections.Generic;
using ModdingGUI.Core.Schemas.Codecs;
using ModdingGUI.Core.Tok;
using ModdingGUI.Core.Tok.Values;

/// <summary>
/// Typed descriptor for a <see cref="Cardinality.Multiple"/> body field on a
/// record of type <typeparamref name="TRecord"/>, whose repeated values decode
/// to a list of <typeparamref name="TElement"/>.
/// </summary>
/// <remarks>
/// <para>
/// Each source body line contributes one <typeparamref name="TElement"/> to the
/// list via the element codec. Lines that fail to decode are skipped at the
/// typed layer, but the surrounding schema infrastructure keeps the raw line in
/// place so round-trip is preserved.
/// </para>
/// <para>
/// Patch DTOs for list-valued fields use Option B verbs: a separate
/// <c>{Field}Add</c> and <c>{Field}Remove</c> property. Remove is processed
/// before Add, and removes ALL matching lines (matched by element equality).
/// </para>
/// </remarks>
public sealed class ListFieldDescriptor<TRecord, TElement> : FieldDescriptor
    where TRecord : class
{
    private readonly Func<TRecord, IList<TElement>> _list;
    private readonly ITokValueCodec<TElement> _codec;

    private ListFieldDescriptor(string key, ITokValueCodec<TElement> codec, Func<TRecord, IList<TElement>> list)
        : base(key, Cardinality.Multiple)
    {
        _codec = codec;
        _list = list;
    }

    /// <summary>
    /// Create a descriptor for a list-valued field.
    /// </summary>
    /// <param name="key">Tok body key (e.g. <c>ITEMSTATMOD</c>).</param>
    /// <param name="codec">Codec for a single element (one source line).</param>
    /// <param name="list">Accessor returning the mutable list on the record instance.</param>
    public static ListFieldDescriptor<TRecord, TElement> Create(
        string key, ITokValueCodec<TElement> codec, Func<TRecord, IList<TElement>> list)
        => new(key, codec, list);

    /// <inheritdoc />
    public override void ReadInto(object record, TokKeyValueLine line)
    {
        var rec = (TRecord)record;
        var parsed = TokValueReader.Parse(line.ValueRaw);
        if (_codec.TryDecode(parsed, out TElement element))
            _list(rec).Add(element);
        // else: undecodable line; typed layer ignores it, raw line stays in place.
    }

    /// <inheritdoc />
    public override IEnumerable<TokValueList> EncodeCurrent(object record)
    {
        var rec = (TRecord)record;
        foreach (var element in _list(rec))
            yield return _codec.Encode(element);
    }

    internal IList<TElement> GetList(TRecord record) => _list(record);
    internal ITokValueCodec<TElement> Codec => _codec;
}
