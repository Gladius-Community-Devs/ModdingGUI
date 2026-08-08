namespace ModdingGUI.Core.Schemas;

using System;
using System.Collections.Generic;
using ModdingGUI.Core.Tok;
using ModdingGUI.Core.Tok.Values;

public sealed partial class RecordView<TRecord> where TRecord : class, new()
{
    /// <summary>Owning set (needed for cross-view line-index bookkeeping).</summary>
    internal RecordSet<TRecord>? OwnerSet { get; set; }

    private int BodyStart => StartLineIndex + 1;
    private int BodyEndInclusive => EndLineIndex;

    /// <summary>
    /// Return the index of the last document line within this record that
    /// corresponds to a known-schema body key (single or multi). Used to decide
    /// where to splice a newly-set field when no existing line exists.
    /// Returns <see cref="StartLineIndex"/> (the header) if no known body lines
    /// are present, so insertions land right after the header.
    /// </summary>
    private int LastKnownSchemaLineIndex()
    {
        int last = StartLineIndex;
        for (int k = BodyStart; k <= BodyEndInclusive && k < Document.Lines.Count; k++)
        {
            if (Document.Lines[k] is TokKeyValueLine kv && Schema.BodyFields.ContainsKey(kv.Key))
                last = k;
        }
        return last;
    }

    /// <summary>
    /// Scan this record's body for all TokKeyValueLine indices matching <paramref name="key"/>.
    /// </summary>
    private List<int> FindBodyLines(string key)
    {
        var hits = new List<int>();
        for (int k = BodyStart; k <= BodyEndInclusive && k < Document.Lines.Count; k++)
        {
            if (Document.Lines[k] is TokKeyValueLine kv && kv.Key == key)
                hits.Add(k);
        }
        return hits;
    }

    /// <summary>
    /// Rewrite the header line after one or more positionals have been edited
    /// on the typed <see cref="Record"/>. Re-encodes the full header.
    /// </summary>
    private void RewriteHeaderLine()
    {
        var newValues = Schema.EncodeHeader(Record);
        string newRaw = Schema.RenderHeaderRaw(newValues);
        string term = Document.LineTerminators[StartLineIndex];
        Document.Lines[StartLineIndex] = Schema.MakeKeyValueLine(newRaw, string.Empty, Schema.StartKey);
        Document.LineTerminators[StartLineIndex] = term;
    }

    /// <summary>
    /// Set a single-valued body field to <paramref name="value"/>. If a body line
    /// already exists for <paramref name="key"/>, its raw text is rewritten in
    /// place. Otherwise a new line is inserted after the last known-schema body
    /// line (or immediately after the header if none exists).
    /// </summary>
    public void SetField<TValue>(string key, TValue value)
    {
        if (!Schema.BodyFields.TryGetValue(key, out var descriptor))
            throw new ArgumentException($"Schema '{Schema.FormatId}' has no body field '{key}'.", nameof(key));
        if (descriptor.Cardinality != Cardinality.Single)
            throw new InvalidOperationException($"Body field '{key}' is list-valued; use AddListItem/RemoveListItem.");
        if (descriptor is not FieldDescriptor<TRecord, TValue> typed)
            throw new InvalidOperationException($"Body field '{key}' has a different value type than {typeof(TValue)}.");

        typed.GetSlot(Record).Set(value);
        var encoded = typed.Codec.Encode(value);

        string indent = Schema.BodyIsIndented ? Schema.BodyIndent : string.Empty;
        string newRaw = Schema.RenderBodyLineRaw(indent, key, encoded);

        var hits = FindBodyLines(key);
        if (hits.Count > 0)
        {
            // Rewrite in place; leave any later duplicates alone (Single fields should not have dups,
            // but do not silently alter unrelated lines).
            int lineIdx = hits[0];
            string term = Document.LineTerminators[lineIdx];
            Document.Lines[lineIdx] = Schema.MakeKeyValueLine(newRaw, indent, key);
            Document.LineTerminators[lineIdx] = term;
        }
        else
        {
            // Insert right after the last known-schema line.
            int anchor = LastKnownSchemaLineIndex();
            int insertAt = anchor + 1;
            string term = Document.LineTerminators[StartLineIndex]; // match the record's terminator style
            Document.Lines.Insert(insertAt, Schema.MakeKeyValueLine(newRaw, indent, key));
            Document.LineTerminators.Insert(insertAt, term);
            EndLineIndex += 1;
            OwnerSet?.ShiftLineIndicesFrom(insertAt, +1, OwnerSet.IndexOfView(this));
        }
    }

    /// <summary>
    /// Clear a single-valued body field: slot is reset and any existing line
    /// for <paramref name="key"/> is removed (all of them, if multiple exist —
    /// consistent with Remove semantics for list fields).
    /// </summary>
    public void ClearField(string key)
    {
        if (!Schema.BodyFields.TryGetValue(key, out var descriptor))
            throw new ArgumentException($"Schema '{Schema.FormatId}' has no body field '{key}'.", nameof(key));
        if (descriptor.Cardinality != Cardinality.Single)
            throw new InvalidOperationException($"Body field '{key}' is list-valued; use RemoveListItem or ClearListField.");

        // Clear slot via reflection on the concrete generic descriptor.
        // All single descriptors are FieldDescriptor<TRecord, TValue>; we can invoke GetSlot via the runtime type.
        var descType = descriptor.GetType();
        var getSlot = descType.GetMethod("GetSlot", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var slotObj = getSlot?.Invoke(descriptor, new object[] { Record });
        slotObj?.GetType().GetMethod("Clear")!.Invoke(slotObj, null);

        RemoveBodyLines(key);
    }

    /// <summary>
    /// Append <paramref name="element"/> to a list-valued field. A new body line
    /// is inserted after the last existing line of the same key (or after the
    /// last known-schema line if none exists yet).
    /// </summary>
    public void AddListItem<TElement>(string key, TElement element)
    {
        if (!Schema.BodyFields.TryGetValue(key, out var descriptor))
            throw new ArgumentException($"Schema '{Schema.FormatId}' has no body field '{key}'.", nameof(key));
        if (descriptor.Cardinality != Cardinality.Multiple)
            throw new InvalidOperationException($"Body field '{key}' is single-valued; use SetField.");
        if (descriptor is not ListFieldDescriptor<TRecord, TElement> typed)
            throw new InvalidOperationException($"Body field '{key}' has a different element type than {typeof(TElement)}.");

        typed.GetList(Record).Add(element);
        var encoded = typed.Codec.Encode(element);

        string indent = Schema.BodyIsIndented ? Schema.BodyIndent : string.Empty;
        string newRaw = Schema.RenderBodyLineRaw(indent, key, encoded);

        var hits = FindBodyLines(key);
        int insertAt = hits.Count > 0 ? hits[hits.Count - 1] + 1 : LastKnownSchemaLineIndex() + 1;
        string term = Document.LineTerminators[StartLineIndex];
        Document.Lines.Insert(insertAt, Schema.MakeKeyValueLine(newRaw, indent, key));
        Document.LineTerminators.Insert(insertAt, term);
        EndLineIndex += 1;
        OwnerSet?.ShiftLineIndicesFrom(insertAt, +1, OwnerSet.IndexOfView(this));
    }

    /// <summary>
    /// Remove every line of a list-valued field whose decoded element equals
    /// <paramref name="element"/>. Both the typed list and the document lines
    /// are updated in lockstep. Returns the number of removed lines.
    /// </summary>
    public int RemoveListItem<TElement>(string key, TElement element)
    {
        if (!Schema.BodyFields.TryGetValue(key, out var descriptor))
            throw new ArgumentException($"Schema '{Schema.FormatId}' has no body field '{key}'.", nameof(key));
        if (descriptor.Cardinality != Cardinality.Multiple)
            throw new InvalidOperationException($"Body field '{key}' is single-valued; use ClearField.");
        if (descriptor is not ListFieldDescriptor<TRecord, TElement> typed)
            throw new InvalidOperationException($"Body field '{key}' has a different element type than {typeof(TElement)}.");

        // Remove ALL matching from the typed list.
        var list = typed.GetList(Record);
        int typedRemoved = 0;
        for (int i = list.Count - 1; i >= 0; i--)
        {
            if (EqualityComparer<TElement>.Default.Equals(list[i], element))
            {
                list.RemoveAt(i);
                typedRemoved++;
            }
        }

        // Remove matching document lines by decoding each.
        int docRemoved = 0;
        for (int k = BodyEndInclusive; k >= BodyStart; k--)
        {
            if (k >= Document.Lines.Count) continue;
            if (Document.Lines[k] is not TokKeyValueLine kv || kv.Key != key) continue;
            var parsed = TokValueReader.Parse(kv.ValueRaw);
            if (typed.Codec.TryDecode(parsed, out TElement decoded)
                && EqualityComparer<TElement>.Default.Equals(decoded, element))
            {
                Document.Lines.RemoveAt(k);
                Document.LineTerminators.RemoveAt(k);
                EndLineIndex -= 1;
                OwnerSet?.ShiftLineIndicesFrom(k, -1, OwnerSet.IndexOfView(this));
                docRemoved++;
            }
        }
        return docRemoved;
    }

    /// <summary>
    /// Remove every body line in this record matching <paramref name="key"/>.
    /// Used internally by ClearField.
    /// </summary>
    private void RemoveBodyLines(string key)
    {
        for (int k = BodyEndInclusive; k >= BodyStart; k--)
        {
            if (k >= Document.Lines.Count) continue;
            if (Document.Lines[k] is TokKeyValueLine kv && kv.Key == key)
            {
                Document.Lines.RemoveAt(k);
                Document.LineTerminators.RemoveAt(k);
                EndLineIndex -= 1;
                OwnerSet?.ShiftLineIndicesFrom(k, -1, OwnerSet.IndexOfView(this));
            }
        }
    }

    /// <summary>
    /// Change a non-stable-id header positional by index. Rewrites the header line.
    /// To change the stable id, use <see cref="Rename"/> (which goes through
    /// the owning set for id uniqueness enforcement).
    /// </summary>
    public void SetHeaderPositional<TValue>(string name, TValue value)
    {
        PositionalFieldDescriptor? found = null;
        foreach (var p in Schema.Header.Positionals)
            if (string.Equals(p.Name, name, StringComparison.Ordinal)) { found = p; break; }
        if (found is null)
            throw new ArgumentException($"Header has no positional named '{name}'.", nameof(name));
        if (found.IsStableId)
            throw new InvalidOperationException($"Cannot set stable-id positional '{name}' via SetHeaderPositional; use Rename.");
        if (found is not PositionalFieldDescriptor<TRecord, TValue> typed)
            throw new InvalidOperationException($"Header positional '{name}' has a different value type than {typeof(TValue)}.");

        typed.GetSlot(Record).Set(value);
        RewriteHeaderLine();
    }

    /// <summary>
    /// Change the stable id of this record. Caller is responsible for ensuring
    /// uniqueness within the owning set — <see cref="RecordSet{TRecord}.Rename"/>
    /// enforces it and is the usual entry point.
    /// </summary>
    public void RenameStableIdUnchecked(string newId)
    {
        var stable = Schema.Header.StableId;
        if (stable is not PositionalFieldDescriptor<TRecord, string> typed)
            throw new InvalidOperationException($"Stable id of schema '{Schema.FormatId}' is not a string; non-string rename not supported yet.");

        typed.GetSlot(Record).Set(newId);
        RewriteHeaderLine();
    }
}
