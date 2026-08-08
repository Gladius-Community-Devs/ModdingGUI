namespace ModdingGUI.Core.Schemas.Codecs;

using ModdingGUI.Core.Tok.Values;

/// <summary>
/// Composes three single-token codecs into a codec for a 3-tuple, rendered as
/// <c>a, b, c</c>. All components must decode successfully for the composite to
/// return <see langword="true"/>; otherwise the calling schema layer keeps the
/// raw line verbatim.
/// </summary>
public sealed class Tuple3Codec<T1, T2, T3> : ITokValueCodec<(T1, T2, T3)>
{
    private readonly ITokValueCodec<T1> _a;
    private readonly ITokValueCodec<T2> _b;
    private readonly ITokValueCodec<T3> _c;

    public Tuple3Codec(ITokValueCodec<T1> a, ITokValueCodec<T2> b, ITokValueCodec<T3> c)
    {
        _a = a; _b = b; _c = c;
    }

    public bool TryDecode(TokValueList list, out (T1, T2, T3) value)
    {
        value = default!;
        if (list.Tokens.Count < 3) return false;
        if (!_a.TryDecode(TokValueList.Of(list.Tokens[0]), out var a)) return false;
        if (!_b.TryDecode(TokValueList.Of(list.Tokens[1]), out var b)) return false;
        if (!_c.TryDecode(TokValueList.Of(list.Tokens[2]), out var c)) return false;
        value = (a, b, c);
        return true;
    }

    public TokValueList Encode((T1, T2, T3) value)
    {
        var aList = _a.Encode(value.Item1);
        var bList = _b.Encode(value.Item2);
        var cList = _c.Encode(value.Item3);
        var tokens = new System.Collections.Generic.List<TokValueToken>();
        if (aList.Tokens.Count > 0) tokens.Add(aList.Tokens[0]);
        if (bList.Tokens.Count > 0) tokens.Add(bList.Tokens[0]);
        if (cList.Tokens.Count > 0) tokens.Add(cList.Tokens[0]);
        return TokValueList.Of(tokens.ToArray());
    }
}
