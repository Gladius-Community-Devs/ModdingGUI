namespace ModdingGUI.Core.Schemas.Codecs;

using System;
using ModdingGUI.Core.Tok.Values;

/// <summary>
/// Codec for an <see cref="Enum"/>-backed single-token value. Decoding is case-insensitive;
/// encoding uses the enum member's exact declared name.
/// </summary>
public sealed class EnumCodec<T> : ITokValueCodec<T> where T : struct, Enum
{
    public bool TryDecode(TokValueList list, out T value)
    {
        value = default;
        if (list.Tokens.Count == 0) return false;
        return Enum.TryParse<T>(list.Tokens[0].Text, ignoreCase: true, out value);
    }

    public TokValueList Encode(T value) => TokValueList.Of(TokValueToken.Identifier(value.ToString()));
}
