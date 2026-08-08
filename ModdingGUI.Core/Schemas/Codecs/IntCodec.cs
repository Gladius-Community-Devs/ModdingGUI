namespace ModdingGUI.Core.Schemas.Codecs;

using System.Globalization;
using ModdingGUI.Core.Tok.Values;

/// <summary>Codec for a single signed 64-bit integer value (no thousands separators).</summary>
public sealed class IntCodec : ITokValueCodec<long>
{
    public bool TryDecode(TokValueList list, out long value)
    {
        value = 0;
        if (list.Tokens.Count == 0) return false;
        return long.TryParse(list.Tokens[0].Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    public TokValueList Encode(long value) => TokValueList.Of(TokValueToken.Integer(value));
}
