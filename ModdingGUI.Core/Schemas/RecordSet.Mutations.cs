namespace ModdingGUI.Core.Schemas;

using System;
using System.Collections.Generic;
using ModdingGUI.Core.Tok;
using ModdingGUI.Core.Tok.Values;

public sealed partial class RecordSet<TRecord> where TRecord : class, new()
{
    // ---- Internal helpers --------------------------------------------------

    /// <summary>
    /// The schema that produced this set. Stored so mutations can consult the
    /// body field registry, stable id, etc., without needing the caller to pass it.
    /// </summary>
    internal RecordSchema<TRecord> Schema { get; }

    /// <summary>Mutable backing list for <see cref="Records"/>.</summary>
    internal readonly List<RecordView<TRecord>> _records;

    /// <summary>
    /// Shift every record whose StartLineIndex is &gt;= <paramref name="fromLineIndex"/> by <paramref name="delta"/>.
    /// Does not shift the record at index <paramref name="ignoreRecordIndex"/> (typically the one being edited).
    /// </summary>
    internal void ShiftLineIndicesFrom(int fromLineIndex, int delta, int ignoreRecordIndex = -1)
    {
        if (delta == 0) return;
        for (int i = 0; i < _records.Count; i++)
        {
            if (i == ignoreRecordIndex) continue;
            var v = _records[i];
            if (v.StartLineIndex >= fromLineIndex)
            {
                v.StartLineIndex += delta;
                v.EndLineIndex += delta;
            }
            else if (v.EndLineIndex >= fromLineIndex)
            {
                // Insertion/deletion inside this record's body: extend or shrink the end.
                v.EndLineIndex += delta;
            }
        }
    }

    /// <summary>
    /// Locate a record view by its stable id. Case-sensitive match.
    /// </summary>
    public RecordView<TRecord>? FindByStableId(string stableId)
    {
        foreach (var v in _records)
        {
            if (string.Equals(v.StableId, stableId, StringComparison.Ordinal))
                return v;
        }
        return null;
    }

    internal int IndexOfView(RecordView<TRecord> view)
    {
        for (int i = 0; i < _records.Count; i++) if (ReferenceEquals(_records[i], view)) return i;
        return -1;
    }

    // ---- Public mutation API ----------------------------------------------

    /// <summary>
    /// Remove the record identified by <paramref name="stableId"/>, including every
    /// line in its inclusive <c>[StartLineIndex..EndLineIndex]</c> range (which
    /// includes trailing blank lines that separate it from the next record).
    /// Returns <see langword="true"/> if a record was removed.
    /// </summary>
    public bool RemoveRecord(string stableId)
    {
        var view = FindByStableId(stableId);
        if (view is null) return false;

        int idx = IndexOfView(view);
        int removeStart = view.StartLineIndex;
        int removeEnd = view.EndLineIndex; // inclusive
        int count = removeEnd - removeStart + 1;

        Document.Lines.RemoveRange(removeStart, count);
        Document.LineTerminators.RemoveRange(removeStart, count);

        _records.RemoveAt(idx);
        ShiftLineIndicesFrom(removeStart, -count);
        return true;
    }

    /// <summary>
    /// Append a new record to the document. The record is serialized using the
    /// schema's header and every set body field. A leading blank line is inserted
    /// before the header when another record precedes it, matching the vanilla
    /// convention (two trailing blank lines after the previous record).
    /// </summary>
    public RecordView<TRecord> AddRecord(TRecord record, InsertPosition? position = null, string? lineTerminator = null)
    {
        var pos = position ?? InsertPosition.AtEnd;
        string term = lineTerminator ?? DetectTerminator();

        // Render the record's lines.
        var newLines = new List<TokLine>();
        var newTerms = new List<string>();

        // Insertion anchor: 0-based line index where the new block will begin.
        int insertAt;
        int anchorIdx = -1;
        if (pos.Kind == InsertPositionKind.AtEnd || _records.Count == 0)
        {
            insertAt = Document.Lines.Count;
        }
        else
        {
            var anchor = pos.AnchorStableId is null ? null : FindByStableId(pos.AnchorStableId);
            if (anchor is null)
                throw new InvalidOperationException($"AddRecord: anchor record '{pos.AnchorStableId}' not found.");
            anchorIdx = IndexOfView(anchor);
            insertAt = pos.Kind == InsertPositionKind.After
                ? anchor.EndLineIndex + 1
                : anchor.StartLineIndex;
        }

        // Leading blank line to visually separate from preceding content, but only if
        // there's preceding content at the insertion point and it isn't already blank.
        bool needsLeadingBlank = insertAt > 0
            && Document.Lines[insertAt - 1] is not TokBlankLine;
        if (needsLeadingBlank)
        {
            newLines.Add(new TokBlankLine(string.Empty));
            newTerms.Add(term);
        }

        int headerLocalIndex = newLines.Count;

        // Header line.
        var headerValues = Schema.EncodeHeader(record);
        string headerRaw = Schema.RenderHeaderRaw(headerValues);
        newLines.Add(Schema.MakeKeyValueLine(headerRaw, string.Empty, Schema.StartKey));
        newTerms.Add(term);

        // Body lines: iterate body fields in declaration order from BodyFields dictionary.
        // Fields that are unset / empty emit nothing.
        foreach (var kvp in Schema.BodyFields)
        {
            var descriptor = kvp.Value;
            foreach (var valueList in descriptor.EncodeCurrent(record))
            {
                string indent = Schema.BodyIsIndented ? Schema.BodyIndent : string.Empty;
                string raw = Schema.RenderBodyLineRaw(indent, descriptor.Key, valueList);
                newLines.Add(Schema.MakeKeyValueLine(raw, indent, descriptor.Key));
                newTerms.Add(term);
            }
        }

        int blockLen = newLines.Count;

        // Splice into document.
        Document.Lines.InsertRange(insertAt, newLines);
        Document.LineTerminators.InsertRange(insertAt, newTerms);

        // Shift existing views to account for the insertion.
        ShiftLineIndicesFrom(insertAt, blockLen);

        int headerIndex = insertAt + headerLocalIndex;
        int endIndex = insertAt + blockLen - 1;
        var view = new RecordView<TRecord>(Schema, Document, headerIndex, endIndex, record, headerValues);
        view.OwnerSet = this;

        // Place in records list.
        int insertRecordIdx;
        if (pos.Kind == InsertPositionKind.AtEnd) insertRecordIdx = _records.Count;
        else if (pos.Kind == InsertPositionKind.After) insertRecordIdx = anchorIdx + 1;
        else insertRecordIdx = anchorIdx; // Before
        _records.Insert(insertRecordIdx, view);

        return view;
    }

    /// <summary>Best-effort detection of the majority line terminator in the document.</summary>
    private string DetectTerminator()
    {
        foreach (var t in Document.LineTerminators)
            if (t.Length > 0) return t;
        return "\r\n";
    }

    /// <summary>
    /// Change a record's stable id, enforcing global uniqueness within the set.
    /// Returns the updated view.
    /// </summary>
    public RecordView<TRecord> Rename(string oldStableId, string newStableId)
    {
        if (string.IsNullOrEmpty(newStableId))
            throw new ArgumentException("New stable id must be non-empty.", nameof(newStableId));
        if (string.Equals(oldStableId, newStableId, StringComparison.Ordinal))
            return FindByStableId(oldStableId)
                ?? throw new InvalidOperationException($"Rename: record '{oldStableId}' not found.");

        var view = FindByStableId(oldStableId)
            ?? throw new InvalidOperationException($"Rename: record '{oldStableId}' not found.");
        if (FindByStableId(newStableId) is not null)
            throw new InvalidOperationException($"Rename: record '{newStableId}' already exists.");

        view.RenameStableIdUnchecked(newStableId);
        return view;
    }
}
