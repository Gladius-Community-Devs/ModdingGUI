namespace ModdingGUI.Core.Tok.Values;

/// <summary>
/// The kind of a single argument token parsed from a tok value.
/// </summary>
public enum TokValueKind
{
    /// <summary>A double-quoted string literal, e.g. <c>"Water Anklet of Oasis"</c>.</summary>
    Quoted,
    /// <summary>A bare, unquoted identifier, e.g. <c>Water</c> or <c>Plain</c>.</summary>
    Identifier,
    /// <summary>An integer literal, e.g. <c>1188</c> or <c>-10</c>.</summary>
    Integer,
    /// <summary>A decimal literal, e.g. <c>0.4</c> or <c>1.1</c>.</summary>
    Decimal,
    /// <summary>An empty argument slot (e.g. <c>a, , b</c> — rare but permitted for round-trip).</summary>
    Empty,
}

/// <summary>
/// One argument in a comma-separated tok value, e.g. one of <c>"Water Anklet"</c>, <c>Water</c>, or <c>10</c>
/// in <c>ITEMAFFINITY: Water , 10</c>.
/// </summary>
/// <remarks>
/// <para>
/// The token records its <see cref="Kind"/> and its decoded <see cref="Text"/> (for quoted, the contents
/// between the quotes; for identifiers and numbers, the raw lexeme as written).
/// </para>
/// <para>
/// The token also preserves <see cref="LeadingWhitespace"/>, <see cref="TrailingWhitespace"/>, and
/// the original quote characters for <see cref="Kind"/> = <see cref="TokValueKind.Quoted"/>, so that
/// a value parsed from disk and written back without modification reproduces the original bytes exactly.
/// </para>
/// </remarks>
public sealed class TokValueToken
{
    /// <summary>The kind of token (quoted, identifier, integer, decimal, or empty).</summary>
    public TokValueKind Kind { get; }

    /// <summary>
    /// The decoded text value. For quoted tokens, the contents <i>between</i> the quotes (without escape processing —
    /// the tok dialect does not support escapes). For identifiers and numbers, the exact lexeme.
    /// </summary>
    public string Text { get; }

    /// <summary>Whitespace (spaces/tabs) captured immediately before this token within its argument slot.</summary>
    public string LeadingWhitespace { get; }

    /// <summary>Whitespace (spaces/tabs) captured immediately after this token within its argument slot.</summary>
    public string TrailingWhitespace { get; }

    /// <summary>
    /// When <see cref="Kind"/> is <see cref="TokValueKind.Quoted"/>, this holds the raw source slice including
    /// the surrounding quote characters (e.g. <c>"Water Anklet"</c>). For other kinds this equals <see cref="Text"/>.
    /// Empty tokens have <see cref="RawLexeme"/> = <see cref="string.Empty"/>.
    /// </summary>
    public string RawLexeme { get; }

    internal TokValueToken(TokValueKind kind, string text, string rawLexeme, string leading, string trailing)
    {
        Kind = kind;
        Text = text;
        RawLexeme = rawLexeme;
        LeadingWhitespace = leading;
        TrailingWhitespace = trailing;
    }

    /// <summary>Create a new quoted token with canonical no-whitespace formatting.</summary>
    public static TokValueToken Quoted(string text) =>
        new(TokValueKind.Quoted, text, "\"" + text + "\"", string.Empty, string.Empty);

    /// <summary>Create a new identifier token with canonical no-whitespace formatting.</summary>
    public static TokValueToken Identifier(string text) =>
        new(TokValueKind.Identifier, text, text, string.Empty, string.Empty);

    /// <summary>Create a new integer token with canonical no-whitespace formatting.</summary>
    public static TokValueToken Integer(long value)
    {
        string s = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return new TokValueToken(TokValueKind.Integer, s, s, string.Empty, string.Empty);
    }

    /// <summary>Create a new decimal token with canonical no-whitespace formatting (invariant culture).</summary>
    public static TokValueToken DecimalOf(decimal value)
    {
        string s = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return new TokValueToken(TokValueKind.Decimal, s, s, string.Empty, string.Empty);
    }

    /// <summary>Render this single token with its preserved whitespace and (for quoted) quote characters.</summary>
    public string Rebuild() => LeadingWhitespace + RawLexeme + TrailingWhitespace;

    /// <inheritdoc />
    public override string ToString() => $"{Kind}({Text})";
}
