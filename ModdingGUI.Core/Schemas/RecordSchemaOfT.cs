namespace ModdingGUI.Core.Schemas;

using System;
using System.Collections.Generic;
using ModdingGUI.Core.Tok;
using ModdingGUI.Core.Tok.Values;

/// <summary>
/// A typed record-based tok format schema.
/// </summary>
/// <remarks>
/// <para>
/// Concrete subclasses (e.g. <see cref="Formats.Items.ItemsSchema"/>) declare:
/// </para>
/// <list type="bullet">
///   <item>The <see cref="StartKey"/> that opens a record.</item>
///   <item>The <see cref="Header"/> (ordered positional arguments on the start line).</item>
///   <item>The <see cref="BodyFields"/> (keyed body-line descriptors, single or multiple).</item>
///   <item>Body indent policy (<see cref="BodyIsIndented"/> + <see cref="BodyIndent"/>).</item>
///   <item>A <see cref="CreateEmpty"/> factory for a blank typed record.</item>
/// </list>
/// <para>
/// The base class uses those declarations to implement <see cref="Read"/> and
/// <see cref="GetStableId"/>. Mutation (applying a patch, adding/removing a
/// record, renaming a record) is implemented on top of <see cref="RecordView{TRecord}"/>.
/// </para>
/// </remarks>
/// <typeparam name="TRecord">Typed in-memory record class.</typeparam>
public abstract class RecordSchema<TRecord> : RecordSchema where TRecord : class, new()
{
    /// <summary>Header descriptor for the record-start line.</summary>
    public abstract HeaderSchema Header { get; }

    /// <summary>Body field descriptors keyed by tok body key (case-sensitive).</summary>
    public abstract IReadOnlyDictionary<string, FieldDescriptor> BodyFields { get; }

    /// <summary>Create a fresh typed record with all slots unset and all lists empty.</summary>
    public virtual TRecord CreateEmpty() => new();

    /// <summary>Return the stable id (Name, identifier, etc.) of <paramref name="record"/>.</summary>
    public string GetStableId(TRecord record)
    {
        var sub = Header.StableId;
        var encoded = sub.EncodeCurrent(record);
        if (encoded is null || encoded.Tokens.Count == 0)
            throw new InvalidOperationException($"Record has no stable id set for schema '{FormatId}'.");
        return encoded.Tokens[0].Text;
    }

    /// <summary>
    /// Parse <paramref name="document"/> into a sequence of <see cref="RecordView{TRecord}"/>s
    /// plus any preamble line indices. Non-record lines before the first header
    /// are captured as the preamble; lines after the last record's range belong
    /// to the last record unless they are the EOF trailer.
    /// </summary>
    public RecordSet<TRecord> Read(TokDocument document)
    {
        var records = new List<RecordView<TRecord>>();
        int preambleEnd = -1; // last preamble line index (inclusive), -1 if no preamble
        int i = 0;

        // 1. Identify preamble: lines before the first header whose key matches StartKey.
        while (i < document.Lines.Count)
        {
            if (document.Lines[i] is TokKeyValueLine kv && kv.Key == StartKey) break;
            i++;
        }
        preambleEnd = i - 1;

        // 2. Walk records.
        while (i < document.Lines.Count)
        {
            int headerIndex = i;
            var headerLine = (TokKeyValueLine)document.Lines[headerIndex];

            // Advance to find the next header or EOF.
            int j = headerIndex + 1;
            while (j < document.Lines.Count)
            {
                if (document.Lines[j] is TokKeyValueLine kv2 && kv2.Key == StartKey) break;
                j++;
            }
            int endInclusive = j - 1;

            // Build typed record.
            var record = CreateEmpty();
            var headerValues = Header.ReadInto(record, headerLine);

            // Walk body lines; decode those whose key is known to the schema.
            for (int k = headerIndex + 1; k <= endInclusive; k++)
            {
                if (document.Lines[k] is TokKeyValueLine bodyKv
                    && BodyFields.TryGetValue(bodyKv.Key, out var fd))
                {
                    fd.ReadInto(record, bodyKv);
                }
            }

            records.Add(new RecordView<TRecord>(this, document, headerIndex, endInclusive, record, headerValues));
            i = j;
        }

        var set = new RecordSet<TRecord>(this, document, preambleEnd, records);
        foreach (var v in records) v.OwnerSet = set;
        return set;
    }

    /// <summary>
    /// Build a <see cref="TokValueList"/> for this record's header line by
    /// encoding each positional in order. Throws if any positional — including
    /// the stable id — is unset. Used by record insertion paths (the header
    /// must always be fully populated on write).
    /// </summary>
    internal TokValueList EncodeHeader(TRecord record)
    {
        var tokens = new List<TokValueToken>(Header.Positionals.Count);
        foreach (var p in Header.Positionals)
        {
            var sub = p.EncodeCurrent(record)
                ?? throw new InvalidOperationException(
                    $"Record cannot be serialised: header positional '{p.Name}' is unset.");
            if (sub.Tokens.Count != 1)
                throw new InvalidOperationException(
                    $"Record cannot be serialised: header positional '{p.Name}' encoded to {sub.Tokens.Count} tokens (expected 1).");
            tokens.Add(sub.Tokens[0]);
        }
        return TokValueList.Of(tokens.ToArray());
    }

    /// <summary>
    /// Render a header line's raw text: <c>{StartKey}: {valueList}</c>. Canonical
    /// form for synthetic lines uses a single space after the colon.
    /// </summary>
    internal string RenderHeaderRaw(TokValueList values)
    {
        return StartKey + ": " + values.Rebuild();
    }

    /// <summary>
    /// Render a body line's raw text: <c>{indent}{key}: {valueList}</c>.
    /// </summary>
    internal string RenderBodyLineRaw(string indent, string key, TokValueList values)
    {
        return indent + key + ": " + values.Rebuild();
    }

    /// <summary>
    /// Factory for a synthetic <see cref="TokKeyValueLine"/>. Parses its own
    /// produced raw form so IndentRaw/Key/ValueRaw stay consistent with what the
    /// reader would produce.
    /// </summary>
    internal TokKeyValueLine MakeKeyValueLine(string raw, string indent, string key)
    {
        int colonIdx = raw.IndexOf(':');
        string valueRaw = colonIdx >= 0 ? raw.Substring(colonIdx + 1) : string.Empty;
        return new TokKeyValueLine(raw, indent, key, valueRaw);
    }
}
