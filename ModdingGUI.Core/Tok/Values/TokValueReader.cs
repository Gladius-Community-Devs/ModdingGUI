using System.Globalization;

namespace ModdingGUI.Core.Tok.Values;

/// <summary>
/// Parses the value portion of a tok <c>KEY: value</c> line into a <see cref="TokValueList"/>.
/// </summary>
/// <remarks>
/// <para>
/// The tok dialect supports:
/// </para>
/// <list type="bullet">
///   <item>Double-quoted strings: <c>"Water Anklet of Oasis"</c>. No escape sequences.</item>
///   <item>Bare identifiers: <c>Water</c>, <c>Plain</c>, <c>accuracy</c>.</item>
///   <item>Integer literals, optionally signed: <c>1188</c>, <c>-10</c>.</item>
///   <item>Decimal literals with a dot: <c>0.4</c>, <c>1.1</c>, <c>-5.5</c>.</item>
///   <item>Comma-separated argument lists.</item>
///   <item>Arbitrary whitespace between tokens and separators, preserved verbatim for round-trip fidelity.</item>
///   <item>Inline comments are NOT recognized here — the line already kept them in its raw form at the
///         <see cref="TokLine"/> layer. If a schema wants to strip inline comments, it does so above this layer.</item>
/// </list>
/// <para>
/// The parser is deliberately lenient: anything it can't classify as quoted/int/decimal becomes an
/// <see cref="TokValueKind.Identifier"/>. Unparseable values never throw; they just come back as identifiers,
/// which preserves round-trip (the raw lexeme is retained) even when higher layers ignore the data.
/// </para>
/// </remarks>
public static class TokValueReader
{
    /// <summary>
    /// Parse the value portion of a <c>KEY: value</c> line. The input should be everything after the colon,
    /// including any leading whitespace (e.g. for <c>"ITEMCOST: 1188"</c> pass <c>" 1188"</c>).
    /// </summary>
    public static TokValueList Parse(string valueRaw)
    {
        var tokens = new List<TokValueToken>();
        var separators = new List<string>();
        int i = 0;

        while (i < valueRaw.Length)
        {
            // Capture leading whitespace for the next token.
            int wsStart = i;
            while (i < valueRaw.Length && (valueRaw[i] == ' ' || valueRaw[i] == '\t')) i++;
            string leading = valueRaw.Substring(wsStart, i - wsStart);

            // If we hit EOL/end with only whitespace, and the tokens list is empty, produce zero tokens.
            if (i >= valueRaw.Length)
            {
                // Trailing whitespace after the last token: splice it onto the previous token's trailing.
                if (tokens.Count > 0)
                {
                    var last = tokens[^1];
                    tokens[^1] = new TokValueToken(last.Kind, last.Text, last.RawLexeme,
                        last.LeadingWhitespace, last.TrailingWhitespace + leading);
                }
                break;
            }

            // Handle separator (comma) — emitted before reading the token when we're past the first one.
            if (valueRaw[i] == ',')
            {
                // This only happens when the value starts with whitespace + "," — i.e. an empty first slot.
                // Produce an empty token for position.
                tokens.Add(new TokValueToken(TokValueKind.Empty, string.Empty, string.Empty, leading, string.Empty));
                // Fall through to separator consumption below.
                goto ConsumeSeparator;
            }

            // Read a token.
            TokValueToken tok = ReadToken(valueRaw, ref i, leading);
            tokens.Add(tok);

            // After the token, capture any whitespace up to the next separator or end.
            int postWsStart = i;
            while (i < valueRaw.Length && (valueRaw[i] == ' ' || valueRaw[i] == '\t')) i++;
            string postWs = valueRaw.Substring(postWsStart, i - postWsStart);

            // Attach that trailing whitespace to the token.
            var cur = tokens[^1];
            tokens[^1] = new TokValueToken(cur.Kind, cur.Text, cur.RawLexeme,
                cur.LeadingWhitespace, cur.TrailingWhitespace + postWs);

            if (i >= valueRaw.Length) break;

            ConsumeSeparator:
            if (i < valueRaw.Length && valueRaw[i] == ',')
            {
                // Separator spans the comma plus any trailing whitespace up to the next token.
                // We've already consumed trailing whitespace into the previous token's trailing field.
                // The separator itself is just "," here; inter-token whitespace is on the tokens' edges.
                separators.Add(",");
                i++;
                // Any whitespace after the comma becomes the next token's leading (handled by the top of the loop).
            }
            else
            {
                // Unexpected char after token without a comma — treat the rest as an identifier-ish continuation.
                // This is defensive; the corpus does not exhibit this.
                break;
            }
        }

        return new TokValueList(tokens, separators, valueRaw);
    }

    private static TokValueToken ReadToken(string s, ref int i, string leading)
    {
        int start = i;
        char c = s[i];

        // Quoted string.
        if (c == '"')
        {
            i++; // skip opening quote
            int textStart = i;
            while (i < s.Length && s[i] != '"') i++;
            string text = s.Substring(textStart, i - textStart);
            if (i < s.Length) i++; // skip closing quote
            string raw = s.Substring(start, i - start);
            return new TokValueToken(TokValueKind.Quoted, text, raw, leading, string.Empty);
        }

        // Number or identifier. Read everything until whitespace or comma.
        while (i < s.Length && s[i] != ',' && s[i] != ' ' && s[i] != '\t') i++;
        string lexeme = s.Substring(start, i - start);

        if (lexeme.Length == 0)
        {
            return new TokValueToken(TokValueKind.Empty, string.Empty, string.Empty, leading, string.Empty);
        }

        // Classify as int / decimal / identifier.
        if (TryParseInt(lexeme, out long _))
            return new TokValueToken(TokValueKind.Integer, lexeme, lexeme, leading, string.Empty);
        if (TryParseDecimal(lexeme, out decimal _))
            return new TokValueToken(TokValueKind.Decimal, lexeme, lexeme, leading, string.Empty);

        return new TokValueToken(TokValueKind.Identifier, lexeme, lexeme, leading, string.Empty);
    }

    private static bool TryParseInt(string s, out long value) =>
        long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

    private static bool TryParseDecimal(string s, out decimal value) =>
        decimal.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
}
