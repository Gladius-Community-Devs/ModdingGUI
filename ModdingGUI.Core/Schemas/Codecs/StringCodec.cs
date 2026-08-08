namespace ModdingGUI.Core.Schemas.Codecs;

using ModdingGUI.Core.Tok.Values;

/// <summary>
/// Codec for a single string value. Round-trips both quoted and bare-identifier forms on read;
/// on write, defaults to a quoted literal (canonical form used by the game's tok files for strings
/// containing spaces) unless the value parses cleanly as a bare identifier (no spaces, no commas,
/// no quotes).
/// </summary>
public sealed class StringCodec : ITokValueCodec<string>
{
    private readonly bool _alwaysQuote;

    /// <summary>
    /// Create a string codec.
    /// </summary>
    /// <param name="alwaysQuote">
    /// When <see langword="true"/>, encoded values are always quoted. When <see langword="false"/>
    /// (the default), values that look like bare identifiers (no whitespace, comma, or quote) are
    /// emitted unquoted to match the dominant source style for identifier-like fields.
    /// </param>
    public StringCodec(bool alwaysQuote = false)
    {
        _alwaysQuote = alwaysQuote;
    }

    public bool TryDecode(TokValueList list, out string value)
    {
        if (list.Tokens.Count == 0) { value = string.Empty; return false; }
        var t = list.Tokens[0];
        if (t.Kind == TokValueKind.Empty) { value = string.Empty; return false; }
        value = t.Text;
        return true;
    }

    public TokValueList Encode(string value)
    {
        if (value is null) return new TokValueList();
        bool needsQuote = _alwaysQuote || RequiresQuoting(value);
        var tok = needsQuote ? TokValueToken.Quoted(value) : TokValueToken.Identifier(value);
        return TokValueList.Of(tok);
    }

    private static bool RequiresQuoting(string s)
    {
        if (s.Length == 0) return true;
        foreach (char c in s)
        {
            if (c == ' ' || c == '\t' || c == ',' || c == '"') return true;
        }
        return false;
    }
}
