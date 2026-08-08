namespace ModdingGUI.Core.Schemas.Codecs;

using ModdingGUI.Core.Tok.Values;

/// <summary>
/// Composes two single-token codecs into a codec for a 2-tuple, rendered as
/// <c>a{separator}b</c> (default separator is <c>", "</c>). Both components must
/// decode successfully for the composite to return <see langword="true"/>; otherwise
/// the calling schema layer keeps the raw line verbatim.
/// </summary>
public sealed class Tuple2Codec<T1, T2> : ITokValueCodec<(T1, T2)>
{
    private readonly ITokValueCodec<T1> _a;
    private readonly ITokValueCodec<T2> _b;
    private readonly string _separator;

    /// <param name="a">Codec for the first component.</param>
    /// <param name="b">Codec for the second component.</param>
    /// <param name="separator">
    /// Separator written between the two encoded tokens (default <c>", "</c>).
    /// Must include any desired surrounding whitespace, e.g. <c>" , "</c>.
    /// </param>
    public Tuple2Codec(ITokValueCodec<T1> a, ITokValueCodec<T2> b, string separator = ", ")
    {
        _a = a;
        _b = b;
        _separator = separator;
    }

    public bool TryDecode(TokValueList list, out (T1, T2) value)
    {
        value = default!;
        if (list.Tokens.Count < 2) return false;
        if (!_a.TryDecode(TokValueList.Of(list.Tokens[0]), out var a)) return false;
        if (!_b.TryDecode(TokValueList.Of(list.Tokens[1]), out var b)) return false;
        value = (a, b);
        return true;
    }

    public TokValueList Encode((T1, T2) value)
    {
        var aList = _a.Encode(value.Item1);
        var bList = _b.Encode(value.Item2);
        var tokens = new System.Collections.Generic.List<TokValueToken>();
        if (aList.Tokens.Count > 0) tokens.Add(aList.Tokens[0]);
        if (bList.Tokens.Count > 0) tokens.Add(bList.Tokens[0]);
        // Build with the configured separator instead of the TokValueList.Of default (", ").
        var separators = new System.Collections.Generic.List<string>();
        for (int i = 1; i < tokens.Count; i++) separators.Add(_separator);
        return new TokValueList(tokens, separators, null);
    }
}
