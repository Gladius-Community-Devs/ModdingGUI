namespace ModdingGUI.Core.Schemas;

using System.Collections.Generic;
using ModdingGUI.Core.Tok;

/// <summary>
/// Non-generic base for a record-based tok format schema (e.g. items.tok, skills.tok).
/// </summary>
/// <remarks>
/// <para>
/// A <see cref="RecordSchema"/> partitions a <see cref="TokDocument"/> into a
/// preamble (lines before the first header) plus a sequence of records, each
/// opened by a header line whose key equals <see cref="StartKey"/>. Lines that
/// are not header-start keys belong to the currently-open record.
/// </para>
/// <para>
/// Typed subclass <see cref="RecordSchema{TRecord}"/> adds codec-driven field
/// decoding and mutation. The non-generic base exists so that the
/// <see cref="FileFormatRegistry"/> and diagnostic tooling can reason about
/// schemas without their <typeparamref name="TRecord"/> type parameter.
/// </para>
/// </remarks>
public abstract class RecordSchema
{
    /// <summary>Stable identifier for this format (e.g. <c>"items"</c>).</summary>
    public abstract string FormatId { get; }

    /// <summary>The tok body key that opens a record (e.g. <c>ITEMCREATE</c>).</summary>
    public abstract string StartKey { get; }

    /// <summary>
    /// Whether body lines are indented (items) or flush-left (skills). When
    /// <see langword="true"/>, <see cref="BodyIndent"/> is prepended to newly
    /// inserted body lines.
    /// </summary>
    public abstract bool BodyIsIndented { get; }

    /// <summary>
    /// Indent prefix for newly-inserted body lines (e.g. <c>"\t"</c> for items).
    /// Ignored when <see cref="BodyIsIndented"/> is <see langword="false"/>.
    /// </summary>
    public abstract string BodyIndent { get; }
}
