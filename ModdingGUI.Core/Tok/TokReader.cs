using System.Text;

namespace ModdingGUI.Core.Tok;

/// <summary>
/// Reads tok-format file bytes into a <see cref="TokDocument"/>.
/// </summary>
/// <remarks>
/// <para>
/// The reader is structured to guarantee <c>Write(Read(x)) == x</c> for every
/// file in the vanilla <c>clean-bec-unpacked/</c> corpus. It achieves this by:
/// </para>
/// <list type="bullet">
///   <item>Stripping and preserving any leading BOM in <see cref="TokDocument.BomBytes"/>.</item>
///   <item>Converting bytes to strings via ISO-8859-1 (Latin-1), which is a bijection
///         over all 256 byte values, so non-ASCII bytes in comments or strings round-trip cleanly.</item>
///   <item>Recording per-line terminators exactly (<c>\r\n</c>, bare <c>\n</c>, bare <c>\r</c>, or empty),
///         so even mixed-terminator files round-trip.</item>
///   <item>Distinguishing a final line that lacks a terminator from one that has one.</item>
///   <item>Classifying each line into a <see cref="TokLine"/> subtype (comment, blank, key:value,
///         positional, unknown) on a best-effort basis — classification never alters <see cref="TokLine.Raw"/>,
///         so misclassification cannot break round-trip fidelity.</item>
/// </list>
/// </remarks>
public static class TokReader
{
    private static readonly byte[] Utf8Bom = new byte[] { 0xEF, 0xBB, 0xBF };

    /// <summary>
    /// Parse the byte contents of a tok/enc file into a <see cref="TokDocument"/>.
    /// </summary>
    /// <param name="bytes">The full file contents.</param>
    /// <param name="sourcePath">Optional informational source path; stored on the document.</param>
    public static TokDocument Read(ReadOnlySpan<byte> bytes, string? sourcePath = null)
    {
        var doc = new TokDocument { SourcePath = sourcePath };

        // 1. BOM detection. None of the vanilla corpus uses a BOM but we preserve one if present.
        int offset = 0;
        if (bytes.Length >= 3 && bytes[0] == Utf8Bom[0] && bytes[1] == Utf8Bom[1] && bytes[2] == Utf8Bom[2])
        {
            doc.BomBytes = bytes.Slice(0, 3).ToArray();
            offset = 3;
        }

        // 2. Line split on CRLF / LF / bare CR, capturing terminator exactly.
        int lineStart = offset;
        int i = offset;
        int lineNumber = 0;
        while (i < bytes.Length)
        {
            byte b = bytes[i];
            if (b == (byte)'\n')
            {
                // bare LF line terminator
                string text = DecodeLine(bytes, lineStart, i - lineStart);
                string term = "\n";
                AddLine(doc, text, term, lineNumber++);
                i++;
                lineStart = i;
            }
            else if (b == (byte)'\r')
            {
                bool isCrLf = i + 1 < bytes.Length && bytes[i + 1] == (byte)'\n';
                string text = DecodeLine(bytes, lineStart, i - lineStart);
                string term = isCrLf ? "\r\n" : "\r";
                AddLine(doc, text, term, lineNumber++);
                i += isCrLf ? 2 : 1;
                lineStart = i;
            }
            else
            {
                i++;
            }
        }

        // 3. Trailing content after the last terminator (or whole-file if no terminators).
        if (lineStart < bytes.Length)
        {
            string text = DecodeLine(bytes, lineStart, bytes.Length - lineStart);
            AddLine(doc, text, string.Empty, lineNumber);
        }

        return doc;
    }

    /// <summary>
    /// Convenience overload: read and parse a file from disk.
    /// </summary>
    public static TokDocument ReadFile(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        return Read(bytes, path);
    }

    private static string DecodeLine(ReadOnlySpan<byte> bytes, int start, int length)
    {
        if (length <= 0) return string.Empty;
        return TokDocument.CanonicalEncoding.GetString(bytes.Slice(start, length));
    }

    private static void AddLine(TokDocument doc, string text, string terminator, int lineNumber)
    {
        TokLine line = Classify(text);
        line.LineNumber = lineNumber;
        doc.Lines.Add(line);
        doc.LineTerminators.Add(terminator);
    }

    /// <summary>
    /// Best-effort classification. Never mutates <see cref="TokLine.Raw"/>; a wrong
    /// guess just means higher layers see an <see cref="TokUnknownLine"/> or
    /// <see cref="TokPositionalLine"/> instead of a keyed line, which only matters
    /// for structured reads — round-trip is unaffected.
    /// </summary>
    internal static TokLine Classify(string raw)
    {
        // Fast path: empty line
        if (raw.Length == 0)
            return new TokBlankLine(raw);

        // Find first non-whitespace character
        int firstNonWs = -1;
        for (int i = 0; i < raw.Length; i++)
        {
            char c = raw[i];
            if (c != ' ' && c != '\t')
            {
                firstNonWs = i;
                break;
            }
        }

        if (firstNonWs < 0)
            return new TokBlankLine(raw);

        // Comment: starts with // (after any leading whitespace)
        if (firstNonWs + 1 < raw.Length && raw[firstNonWs] == '/' && raw[firstNonWs + 1] == '/')
            return new TokCommentLine(raw);

        // Key: value — first non-whitespace word followed by ':' before any whitespace
        // We match the simple case: IDENT ':' (possibly followed by anything).
        // We do NOT try to also split inline // comments here; those stay inside ValueRaw
        // and higher-level schemas can strip them if they want to.
        int colonIdx = -1;
        for (int i = firstNonWs; i < raw.Length; i++)
        {
            char c = raw[i];
            if (c == ':') { colonIdx = i; break; }
            // A space or tab before a colon means this is NOT a "KEY:" line;
            // it's a positional line (whitespace-keyed dialect).
            if (c == ' ' || c == '\t') break;
        }

        if (colonIdx > firstNonWs)
        {
            string indent = raw.Substring(0, firstNonWs);
            string key = raw.Substring(firstNonWs, colonIdx - firstNonWs);
            string valueRaw = raw.Substring(colonIdx + 1);
            return new TokKeyValueLine(raw, indent, key, valueRaw);
        }

        // Otherwise treat it as positional / whitespace-keyed content (flat tables, skillset bodies, etc.).
        return new TokPositionalLine(raw);
    }
}
