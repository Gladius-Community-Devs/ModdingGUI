namespace ModdingGUI.Core.Schemas.Codecs;

using System.Globalization;
using ModdingGUI.Core.Tok.Values;

/// <summary>Codec for a single decimal value (invariant culture, <c>.</c> decimal separator).</summary>
public sealed class DecimalCodec : ITokValueCodec<decimal>
{
    public bool TryDecode(TokValueList list, out decimal value)
    {
        value = 0m;
        if (list.Tokens.Count == 0) return false;
        return decimal.TryParse(list.Tokens[0].Text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    public TokValueList Encode(decimal value) => TokValueList.Of(TokValueToken.DecimalOf(value));
}
