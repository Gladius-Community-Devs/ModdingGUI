namespace ModdingGUI.Core.Schemas;

using ModdingGUI.Core.Tok;
using ModdingGUI.Core.Tok.Values;

/// <summary>
/// A window onto a single record within a <see cref="TokDocument"/>.
/// </summary>
/// <remarks>
/// <para>
/// A <see cref="RecordView{TRecord}"/> carries the typed <see cref="Record"/>
/// and the line range <c>[StartLineIndex..EndLineIndex]</c> in the owning
/// document. Mutation APIs (Phase 2 Items mutation layer) will live here as
/// methods that rewrite only the specific body lines affected by a patch; all
/// untouched lines — including comments, blanks, and body keys not recognised
/// by the schema — are preserved verbatim in the document.
/// </para>
/// <para>
/// The header's parsed <see cref="TokValueList"/> is also kept so that header
/// positional edits (non-stable-id positionals) can rewrite the header line
/// without re-parsing.
/// </para>
/// </remarks>
public sealed partial class RecordView<TRecord> where TRecord : class, new()
{
    /// <summary>The schema that produced this view.</summary>
    public RecordSchema<TRecord> Schema { get; }

    /// <summary>The document this view lives in.</summary>
    public TokDocument Document { get; }

    /// <summary>Document line index of the record's header (inclusive).</summary>
    public int StartLineIndex { get; internal set; }

    /// <summary>Document line index of the record's last line (inclusive).</summary>
    public int EndLineIndex { get; internal set; }

    /// <summary>The typed record decoded from the document range.</summary>
    public TRecord Record { get; }

    /// <summary>Parsed header value list at read-time (for diagnostics and positional rewrites).</summary>
    public TokValueList HeaderValues { get; }

    internal RecordView(
        RecordSchema<TRecord> schema,
        TokDocument document,
        int startLineIndex,
        int endLineIndex,
        TRecord record,
        TokValueList headerValues)
    {
        Schema = schema;
        Document = document;
        StartLineIndex = startLineIndex;
        EndLineIndex = endLineIndex;
        Record = record;
        HeaderValues = headerValues;
    }

    /// <summary>Convenience: the record's stable id (e.g. Name).</summary>
    public string StableId => Schema.GetStableId(Record);
}
