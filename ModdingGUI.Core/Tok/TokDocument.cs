using System.Text;

namespace ModdingGUI.Core.Tok;

/// <summary>
/// An in-memory, ordered representation of a tok-format file.
/// </summary>
/// <remarks>
/// <para>
/// Produced by <see cref="TokReader"/> and serialized by <see cref="TokWriter"/>.
/// A <see cref="TokDocument"/> that is read and immediately written produces a
/// byte-for-byte identical byte sequence; this is enforced by the Phase 1
/// round-trip test gate across every <c>.tok</c> and <c>.enc</c> file in
/// <c>clean-bec-unpacked/</c>.
/// </para>
/// <para>
/// Newline policy: the vanilla game corpus uses CRLF exclusively. The reader
/// captures the actual terminator of each line individually, so mixed-newline
/// files (should they ever appear) also round-trip cleanly.
/// </para>
/// <para>
/// Encoding policy: tok files contain mostly ASCII but a small number contain
/// bytes &gt; 0x7F (e.g. an <c>©</c> in comments). To guarantee exact
/// byte preservation regardless of origin encoding, the framework uses ISO-8859-1
/// (Latin-1), which is a bijection on all 256 byte values. UTF-8 BOM if ever
/// encountered is captured in <see cref="BomBytes"/> and emitted verbatim.
/// </para>
/// </remarks>
public sealed class TokDocument
{
    /// <summary>
    /// The canonical encoding the tok framework uses for in-memory string I/O.
    /// ISO-8859-1 (Latin-1) is used because it's a lossless round-trip for any
    /// byte stream, which is required to preserve non-ASCII content bit-exactly.
    /// </summary>
    public static readonly Encoding CanonicalEncoding = Encoding.GetEncoding("ISO-8859-1");

    /// <summary>
    /// Any leading BOM bytes present in the source file. Empty for the vanilla
    /// corpus (no tok file uses a BOM) but preserved for forward compatibility.
    /// </summary>
    public byte[] BomBytes { get; internal set; } = Array.Empty<byte>();

    /// <summary>
    /// All parsed lines in original document order. Line terminators are NOT
    /// included in <see cref="TokLine.Raw"/>; they live in <see cref="LineTerminators"/>,
    /// one per line, in the same order.
    /// </summary>
    public List<TokLine> Lines { get; } = new();

    /// <summary>
    /// Exact terminator bytes that followed each line in the source, in line
    /// order. Same length as <see cref="Lines"/>. An empty string at the last
    /// index indicates the file's final line had no terminator.
    /// </summary>
    public List<string> LineTerminators { get; } = new();

    /// <summary>
    /// Absolute or project-relative path this document was loaded from, when known.
    /// Informational only; never consulted for round-trip behavior.
    /// </summary>
    public string? SourcePath { get; set; }

    /// <summary>
    /// Total line count (shorthand for <c>Lines.Count</c>).
    /// </summary>
    public int LineCount => Lines.Count;
}
