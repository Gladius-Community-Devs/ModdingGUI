namespace ModdingGUI.Core.Schemas.Codecs;

using ModdingGUI.Core.Tok.Values;

/// <summary>
/// Converts between a <see cref="TokValueList"/> (the parsed representation of the
/// text after a tok <c>KEY:</c>) and a typed value of type <typeparamref name="T"/>.
/// </summary>
/// <remarks>
/// <para>
/// Codecs are stateless and are owned by <see cref="Schema.FieldDescriptor"/>s in a
/// <see cref="Schema.RecordSchema{TRecord}"/>. Decoding must be tolerant: if the
/// value cannot be represented as <typeparamref name="T"/>, <see cref="TryDecode"/>
/// returns <see langword="false"/> and higher layers keep the raw line unchanged.
/// Encoding produces a canonical, well-formed value list.
/// </para>
/// <para>
/// The <see cref="TryDecode"/>/<see cref="Encode"/> pair is used (rather than a
/// nullable return) so that a single interface works uniformly for both value
/// types (e.g. <see cref="int"/>) and reference types (e.g. <see cref="string"/>).
/// </para>
/// </remarks>
/// <typeparam name="T">The typed representation of the value.</typeparam>
public interface ITokValueCodec<T>
{
    /// <summary>
    /// Attempt to decode <paramref name="list"/> as a <typeparamref name="T"/>. Returns
    /// <see langword="false"/> without throwing if the value cannot be represented;
    /// callers MUST retain the raw line in that case so round-trip is preserved.
    /// </summary>
    bool TryDecode(TokValueList list, out T value);

    /// <summary>
    /// Encode a typed value into a canonical <see cref="TokValueList"/>. The result is
    /// a synthetic (non-pristine) list; the calling <see cref="Schema.FieldDescriptor"/>
    /// is responsible for rebuilding the surrounding line.
    /// </summary>
    TokValueList Encode(T value);
}
