namespace ModdingGUI.Core.Schemas.Codecs;

using ModdingGUI.Core.Tok.Values;

/// <summary>
/// Codec for a single boolean value. Decodes common tok forms
/// (<c>true</c>/<c>false</c>, <c>1</c>/<c>0</c>, <c>yes</c>/<c>no</c>, case-insensitive).
/// Encodes canonically as <c>true</c>/<c>false</c>.
/// </summary>
public sealed class BoolCodec : ITokValueCodec<bool>
{
    public bool TryDecode(TokValueList list, out bool value)
    {
        value = false;
        if (list.Tokens.Count == 0) return false;
        string s = list.Tokens[0].Text;
        if (s.Equals("true", System.StringComparison.OrdinalIgnoreCase) || s == "1"
            || s.Equals("yes", System.StringComparison.OrdinalIgnoreCase))
        { value = true; return true; }
        if (s.Equals("false", System.StringComparison.OrdinalIgnoreCase) || s == "0"
            || s.Equals("no", System.StringComparison.OrdinalIgnoreCase))
        { value = false; return true; }
        return false;
    }

    public TokValueList Encode(bool value) =>
        TokValueList.Of(TokValueToken.Identifier(value ? "true" : "false"));
}
