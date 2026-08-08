namespace ModdingGUI.Core.Schemas;

using System.Collections.Generic;
using ModdingGUI.Core.Tok;

/// <summary>
/// Result of reading a record-based tok document through a <see cref="RecordSchema{TRecord}"/>.
/// Holds the shared <see cref="Document"/> plus the partition into preamble and record views.
/// </summary>
/// <remarks>
/// <para>
/// Mutation of records is done through <see cref="RecordView{TRecord}"/>;
/// adding or removing whole records is done through helpers on this type. In
/// all cases, <see cref="Document"/>'s line list is the single source of truth —
/// views simply reference ranges within it.
/// </para>
/// </remarks>
public sealed partial class RecordSet<TRecord> where TRecord : class, new()
{
    /// <summary>The owning <see cref="TokDocument"/>. Mutations update its lines in place.</summary>
    public TokDocument Document { get; }

    /// <summary>Index (inclusive) of the last preamble line, or <c>-1</c> if there is no preamble.</summary>
    public int PreambleEndIndex { get; }

    /// <summary>All record views in document order.</summary>
    public IReadOnlyList<RecordView<TRecord>> Records => _records;

    internal RecordSet(RecordSchema<TRecord> schema, TokDocument document, int preambleEndIndex, List<RecordView<TRecord>> records)
    {
        Schema = schema;
        Document = document;
        PreambleEndIndex = preambleEndIndex;
        _records = records;
    }
}
