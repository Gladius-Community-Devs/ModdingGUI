namespace ModdingGUI.Core.Tok;

/// <summary>
/// Serializes a <see cref="TokDocument"/> back to the byte form it was loaded from.
/// </summary>
/// <remarks>
/// <para>
/// For documents produced by <see cref="TokReader"/> and not mutated, the writer
/// is guaranteed to reproduce the input bytes exactly. This guarantee is verified
/// on every build by the Phase 1 round-trip test, which parses and re-serializes
/// all 977 <c>.tok</c> and <c>.enc</c> files in <c>clean-bec-unpacked/</c> and
/// asserts byte equality.
/// </para>
/// <para>
/// For mutated documents, callers are responsible for keeping <see cref="TokLine.Raw"/>
/// in sync with structured fields (schemas layered on top of the tok framework
/// handle this automatically).
/// </para>
/// </remarks>
public static class TokWriter
{
    /// <summary>
    /// Serialize a <see cref="TokDocument"/> to a fresh byte array.
    /// </summary>
    public static byte[] Write(TokDocument doc)
    {
        if (doc.Lines.Count != doc.LineTerminators.Count)
        {
            throw new InvalidOperationException(
                $"TokDocument is internally inconsistent: Lines.Count={doc.Lines.Count} " +
                $"but LineTerminators.Count={doc.LineTerminators.Count}. Every line must " +
                $"have a recorded terminator (empty string for the final unterminated line).");
        }

        // Predict byte length to avoid reallocations.
        int predicted = doc.BomBytes.Length;
        for (int i = 0; i < doc.Lines.Count; i++)
        {
            predicted += TokDocument.CanonicalEncoding.GetByteCount(doc.Lines[i].Raw);
            predicted += doc.LineTerminators[i].Length; // terminators are always ASCII (\r \n)
        }

        using var ms = new MemoryStream(capacity: predicted);
        if (doc.BomBytes.Length > 0)
            ms.Write(doc.BomBytes, 0, doc.BomBytes.Length);

        var enc = TokDocument.CanonicalEncoding;
        for (int i = 0; i < doc.Lines.Count; i++)
        {
            string raw = doc.Lines[i].Raw;
            if (raw.Length > 0)
            {
                byte[] bytes = enc.GetBytes(raw);
                ms.Write(bytes, 0, bytes.Length);
            }
            string term = doc.LineTerminators[i];
            if (term.Length > 0)
            {
                byte[] termBytes = enc.GetBytes(term);
                ms.Write(termBytes, 0, termBytes.Length);
            }
        }

        return ms.ToArray();
    }

    /// <summary>
    /// Serialize a <see cref="TokDocument"/> directly to disk. Uses the document's
    /// own encoding/BOM/terminator metadata, not any platform defaults.
    /// </summary>
    public static void WriteFile(TokDocument doc, string path)
    {
        byte[] bytes = Write(doc);
        File.WriteAllBytes(path, bytes);
    }
}
