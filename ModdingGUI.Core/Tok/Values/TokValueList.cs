namespace ModdingGUI.Core.Tok.Values;

/// <summary>
/// A parsed tok value — the portion of a <c>KEY: ...</c> line after the colon — consisting of zero or more
/// comma-separated <see cref="TokValueToken"/> arguments.
/// </summary>
/// <remarks>
/// <para>
/// A <see cref="TokValueList"/> preserves enough information to reproduce the original raw source slice
/// byte-for-byte when unmodified (see <see cref="RawSource"/>). When mutated, callers should call
/// <see cref="InvalidateRawSource"/> or construct a new <see cref="TokValueList"/> so that <see cref="Rebuild"/>
/// re-renders from the tokens instead of echoing the original.
/// </para>
/// <para>
/// Commas are stored in <see cref="Separators"/> with their surrounding whitespace so that
/// <c>"Water , 10"</c> round-trips distinctly from <c>"Water, 10"</c>.
/// </para>
/// </remarks>
public sealed class TokValueList
{
    /// <summary>The ordered list of argument tokens.</summary>
    public List<TokValueToken> Tokens { get; }

    /// <summary>
    /// Separators between tokens. For N tokens there are N-1 separators; each preserves its surrounding
    /// whitespace (e.g. <c>" , "</c>, <c>", "</c>, or <c>","</c>).
    /// </summary>
    public List<string> Separators { get; }

    /// <summary>
    /// The exact raw source slice that was parsed (everything after the colon, including leading whitespace),
    /// or <see langword="null"/> if this list was constructed synthetically or was marked dirty by a mutation.
    /// </summary>
    public string? RawSource { get; private set; }

    /// <summary>True if this list was parsed from source and has not been mutated.</summary>
    public bool IsPristine => RawSource is not null;

    internal TokValueList(List<TokValueToken> tokens, List<string> separators, string? rawSource)
    {
        Tokens = tokens;
        Separators = separators;
        RawSource = rawSource;
    }

    /// <summary>Construct an empty value list (no tokens).</summary>
    public TokValueList() : this(new List<TokValueToken>(), new List<string>(), null) { }

    /// <summary>Construct a value list with the given tokens and canonical ", " separators.</summary>
    public static TokValueList Of(params TokValueToken[] tokens)
    {
        var list = new List<TokValueToken>(tokens);
        var seps = new List<string>();
        for (int i = 1; i < list.Count; i++) seps.Add(", ");
        return new TokValueList(list, seps, null);
    }

    /// <summary>
    /// Mark this list as mutated. After this call, <see cref="RawSource"/> is cleared and <see cref="Rebuild"/>
    /// will re-render from the tokens and separators. Call this after editing <see cref="Tokens"/> or
    /// <see cref="Separators"/> directly.
    /// </summary>
    public void InvalidateRawSource() => RawSource = null;

    /// <summary>
    /// Render this value list back to its tok-value string form (without the leading <c>":"</c>).
    /// When <see cref="IsPristine"/>, returns <see cref="RawSource"/> byte-identically. Otherwise renders
    /// token-by-token using each token's preserved whitespace + the separators.
    /// </summary>
    public string Rebuild()
    {
        if (RawSource is not null) return RawSource;

        if (Tokens.Count == 0) return string.Empty;

        var sb = new System.Text.StringBuilder();
        sb.Append(Tokens[0].Rebuild());
        for (int i = 1; i < Tokens.Count; i++)
        {
            string sep = i - 1 < Separators.Count ? Separators[i - 1] : ", ";
            sb.Append(sep);
            sb.Append(Tokens[i].Rebuild());
        }
        return sb.ToString();
    }

    /// <inheritdoc />
    public override string ToString() => Rebuild();
}
