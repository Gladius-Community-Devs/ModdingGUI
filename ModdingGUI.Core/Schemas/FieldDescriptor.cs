namespace ModdingGUI.Core.Schemas;

using System;
using System.Collections.Generic;
using ModdingGUI.Core.Schemas.Codecs;
using ModdingGUI.Core.Tok;
using ModdingGUI.Core.Tok.Values;

/// <summary>
/// Non-generic base for a body-field descriptor attached to a
/// <see cref="RecordSchema{TRecord}"/>. Provides reflection-friendly boxed
/// accessors so that the API/MCP layer can read and write field values from
/// deserialized patch DTOs without knowing each field's static type.
/// </summary>
/// <remarks>
/// <para>
/// Concrete subclasses are <see cref="FieldDescriptor{TRecord,TValue}"/> (for
/// <see cref="Cardinality.Single"/> fields) and
/// <see cref="ListFieldDescriptor{TRecord,TElement}"/> (for
/// <see cref="Cardinality.Multiple"/> fields). Each knows its
/// <see cref="ITokValueCodec{T}"/> internally and exposes typed getters/setters
/// over boxed <see cref="object"/> for framework-level code.
/// </para>
/// <para>
/// The descriptor does NOT touch <see cref="TokDocument.Lines"/> directly; it
/// only converts between a <typeparamref name="TRecord"/>'s in-memory state and
/// the <see cref="TokValueList"/> for a single body line. Layering
/// (<see cref="RecordSchema{TRecord}"/>, <see cref="RecordView{TRecord}"/>) owns
/// the actual document-line placement and Add/Remove semantics.
/// </para>
/// </remarks>
public abstract class FieldDescriptor
{
    /// <summary>The tok body key this descriptor binds to (e.g. <c>ITEMCOST</c>).</summary>
    public string Key { get; }

    /// <summary>Cardinality: single-valued or list-valued.</summary>
    public Cardinality Cardinality { get; }

    protected FieldDescriptor(string key, Cardinality cardinality)
    {
        if (string.IsNullOrEmpty(key)) throw new ArgumentException("Key must be non-empty.", nameof(key));
        Key = key;
        Cardinality = cardinality;
    }

    /// <summary>
    /// Read the field's value(s) from <paramref name="line"/> (a body line whose
    /// <see cref="TokKeyValueLine.Key"/> equals <see cref="Key"/>) and apply it
    /// to <paramref name="record"/>. The call is destination-typed: for
    /// <see cref="Cardinality.Single"/> the descriptor REPLACES the field value;
    /// for <see cref="Cardinality.Multiple"/> it APPENDS one element to the list.
    /// If decoding fails, <paramref name="record"/> is left untouched and the
    /// raw line is preserved by the caller.
    /// </summary>
    public abstract void ReadInto(object record, TokKeyValueLine line);

    /// <summary>
    /// Encode this field's value(s) out of <paramref name="record"/> into zero or
    /// more synthetic <see cref="TokValueList"/> payloads. For
    /// <see cref="Cardinality.Single"/>, returns 0 or 1 entries; for
    /// <see cref="Cardinality.Multiple"/>, one per list element.
    /// </summary>
    public abstract IEnumerable<TokValueList> EncodeCurrent(object record);
}
