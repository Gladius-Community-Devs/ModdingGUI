namespace ModdingGUI.Core.Tok;

/// <summary>
/// Base type for every line in a <see cref="TokDocument"/>.
/// </summary>
/// <remarks>
/// <para>
/// A <see cref="TokLine"/> always carries its exact <see cref="Raw"/> text as it
/// appeared on disk (minus the line terminator). <see cref="TokWriter"/> writes
/// <see cref="Raw"/> back verbatim, which is what gives the tok framework its
/// strict byte-for-byte round-trip guarantee for untouched documents.
/// </para>
/// <para>
/// Concrete subclasses (<see cref="TokCommentLine"/>, <see cref="TokBlankLine"/>,
/// <see cref="TokKeyValueLine"/>, <see cref="TokPositionalLine"/>,
/// <see cref="TokUnknownLine"/>) layer a structured interpretation on top. Higher
/// layers — schemas and API implementations — read and mutate that structured view.
/// When a line is mutated, implementations are responsible for keeping
/// <see cref="Raw"/> in sync via <see cref="Rebuild"/>.
/// </para>
/// <para>
/// All tok files observed in the vanilla <c>clean-bec-unpacked/</c> corpus (977
/// files across <c>.tok</c> and <c>.enc</c>) use CRLF line endings exclusively
/// with no BOM. The reader still tolerates LF-only and BOM for robustness.
/// </para>
/// </remarks>
public abstract class TokLine
{
    /// <summary>
    /// Exact line text as it appeared on disk, excluding the trailing line
    /// terminator. For a perfectly untouched document, <c>Raw</c> round-trips
    /// byte-for-byte.
    /// </summary>
    public string Raw { get; protected set; }

    /// <summary>
    /// Zero-based line index in the owning <see cref="TokDocument"/>. Populated
    /// by the reader. Useful for error messages and for stable diagnostics from
    /// the API layer.
    /// </summary>
    public int LineNumber { get; internal set; }

    protected TokLine(string raw)
    {
        Raw = raw ?? string.Empty;
    }

    /// <summary>
    /// Regenerates <see cref="Raw"/> from the current structured state.
    /// Default implementation is a no-op (subclasses that expose mutation must override).
    /// </summary>
    public virtual void Rebuild() { }

    /// <inheritdoc />
    public override string ToString() => Raw;
}

/// <summary>
/// Comment line. In tok grammar, a line whose first non-whitespace characters are <c>//</c>.
/// Leading whitespace before the <c>//</c> is preserved in <see cref="TokLine.Raw"/>.
/// </summary>
public sealed class TokCommentLine : TokLine
{
    public TokCommentLine(string raw) : base(raw) { }
}

/// <summary>
/// A whitespace-only line (including the empty string). Preserved verbatim so that
/// blank lines used for visual spacing in the source files round-trip unchanged.
/// </summary>
public sealed class TokBlankLine : TokLine
{
    public TokBlankLine(string raw) : base(raw) { }
}

/// <summary>
/// A <c>KEY: value</c> style line (the colon-keyed dialect: items.tok, skills.tok,
/// classdefs.tok, worldmap.tok, gametweaks.tok, most record-style files).
/// </summary>
/// <remarks>
/// For Phase 1 the framework only guarantees round-trip fidelity: structured
/// mutation of keyed lines is layered on top by the schema framework in Phase 2.
/// The <see cref="IndentRaw"/>, <see cref="Key"/> and <see cref="ValueRaw"/>
/// fields capture the best-effort split for higher layers.
/// </remarks>
public sealed class TokKeyValueLine : TokLine
{
    public string IndentRaw { get; }
    public string Key { get; }
    /// <summary>
    /// Raw text after the first colon, including any leading space. This is what
    /// a schema reads from. Byte-for-byte round trip is guaranteed via
    /// <see cref="TokLine.Raw"/> when nothing is mutated.
    /// </summary>
    public string ValueRaw { get; }

    public TokKeyValueLine(string raw, string indentRaw, string key, string valueRaw)
        : base(raw)
    {
        IndentRaw = indentRaw;
        Key = key;
        ValueRaw = valueRaw;
    }
}

/// <summary>
/// A positional / whitespace-keyed line used by flat tables such as schools.tok,
/// badges.tok, enemyschools.tok, skill-set bodies, etc. The framework does not
/// attempt to tokenize fields in Phase 1 beyond capturing the raw line; schemas
/// interpret positional columns where needed.
/// </summary>
public sealed class TokPositionalLine : TokLine
{
    public TokPositionalLine(string raw) : base(raw) { }
}

/// <summary>
/// Any line the reader could not classify confidently. Always round-trips via
/// <see cref="TokLine.Raw"/>.
/// </summary>
public sealed class TokUnknownLine : TokLine
{
    public TokUnknownLine(string raw) : base(raw) { }
}
