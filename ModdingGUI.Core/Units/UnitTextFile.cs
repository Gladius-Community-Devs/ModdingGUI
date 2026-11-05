using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ModdingGUI.Core.Tok;

namespace ModdingGUI.Core.Units;

/// <summary>
/// A line-oriented game text file that keeps each line's own terminator, so a no-op write is byte
/// identical. This is the base for the <c>data/units</c> family.
/// </summary>
/// <remarks>
/// <para>
/// The six units files are not uniform: <c>gladiators.txt</c> uses bare LF, while
/// <c>statsets/skillsets/itemsets/schools/tints</c> use CRLF. The shared tok helper normalises to CRLF,
/// which is right for <c>.tok</c> and wrong here — it would rewrite 21,700 line endings in
/// <c>gladiators.txt</c> on every save. This type stores the original terminator per line instead, and
/// only new or replaced lines take the file's dominant terminator.
/// </para>
/// <para>
/// Encoding is ISO-8859-1 (bijective over all 256 byte values), the same canonical choice as the tok
/// layer, even though today's corpus is pure ASCII.
/// </para>
/// </remarks>
public sealed class UnitTextFile
{
    private readonly List<Line> _lines;

    private UnitTextFile(List<Line> lines, string dominantNewLine, bool endsWithNewLine)
    {
        _lines = lines;
        NewLine = dominantNewLine;
        EndsWithNewLine = endsWithNewLine;
    }

    /// <summary>The file's dominant terminator (<c>"\n"</c> or <c>"\r\n"</c>), used for new lines.</summary>
    public string NewLine { get; }

    /// <summary>True when the original file ended with a terminator.</summary>
    public bool EndsWithNewLine { get; }

    /// <summary>True once a line has been added, replaced or removed.</summary>
    public bool IsModified { get; private set; }

    /// <summary>Logical lines without their terminators.</summary>
    public IReadOnlyList<string> Lines
    {
        get
        {
            var text = new string[_lines.Count];
            for (int i = 0; i < _lines.Count; i++) text[i] = _lines[i].Text;
            return text;
        }
    }

    /// <summary>Number of lines (a file ending with a newline does not count a phantom last line).</summary>
    public int Count => _lines.Count;

    /// <summary>Load from disk.</summary>
    public static UnitTextFile Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!File.Exists(path)) throw new FileNotFoundException("Units file not found.", path);
        return Parse(File.ReadAllBytes(path));
    }

    /// <summary>Parse raw bytes, remembering every terminator exactly.</summary>
    public static UnitTextFile Parse(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        string text = TokDocument.CanonicalEncoding.GetString(bytes);
        var lines = new List<Line>();
        int crlf = 0;
        int lf = 0;
        int start = 0;

        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] != '\n') continue;

            int end = i;
            if (end > start && text[end - 1] == '\r') end--;

            string terminator = end < i && text[end] == '\r' ? "\r\n" : "\n";
            if (terminator == "\r\n") crlf++; else lf++;

            lines.Add(new Line(text[start..end], terminator));
            start = i + 1;
        }

        bool endsWithNewLine = start == text.Length && text.Length > 0;
        if (start < text.Length)
        {
            lines.Add(new Line(text[start..], string.Empty));
        }

        string dominant = crlf > lf ? "\r\n" : "\n";
        return new UnitTextFile(lines, dominant, endsWithNewLine);
    }

    /// <summary>Get a line's text (without its terminator).</summary>
    public string GetLine(int index)
    {
        CheckIndex(index);
        return _lines[index].Text;
    }

    /// <summary>Replace a line's text, keeping that line's existing terminator.</summary>
    public void ReplaceLine(int index, string text)
    {
        CheckIndex(index);
        ArgumentNullException.ThrowIfNull(text);
        if (text.Contains('\n') || text.Contains('\r'))
            throw new ArgumentException("A line must not contain a line break.", nameof(text));

        if (string.Equals(_lines[index].Text, text, StringComparison.Ordinal)) return;

        _lines[index] = _lines[index] with { Text = text };
        IsModified = true;
    }

    /// <summary>Insert a line before <paramref name="index"/>; index == Count appends.</summary>
    public void InsertLine(int index, string text)
    {
        if (index < 0 || index > _lines.Count) throw new ArgumentOutOfRangeException(nameof(index));
        ArgumentNullException.ThrowIfNull(text);
        if (text.Contains('\n') || text.Contains('\r'))
            throw new ArgumentException("A line must not contain a line break.", nameof(text));

        // A line inserted after a file that had no final terminator now needs one, otherwise the new
        // line would be glued onto the previous one.
        if (index == _lines.Count && _lines.Count > 0 && _lines[^1].Terminator.Length == 0)
            _lines[^1] = _lines[^1] with { Terminator = NewLine };

        _lines.Insert(index, new Line(text, NewLine));
        IsModified = true;
    }

    /// <summary>Remove a line.</summary>
    public void RemoveLine(int index)
    {
        CheckIndex(index);
        _lines.RemoveAt(index);
        IsModified = true;
    }

    /// <summary>Serialise back to bytes. Unmodified files reproduce the input exactly.</summary>
    public byte[] ToBytes()
    {
        var text = new StringBuilder();
        for (int i = 0; i < _lines.Count; i++)
        {
            text.Append(_lines[i].Text);
            text.Append(_lines[i].Terminator);
        }

        return TokDocument.CanonicalEncoding.GetBytes(text.ToString());
    }

    /// <summary>Write through a backup, like every other API.</summary>
    public void Save(string projectPath, string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        string path = Path.Combine(projectPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
        ChangeHistoryService.Backup(projectPath, path);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, ToBytes());
        IsModified = false;
    }

    private void CheckIndex(int index)
    {
        if (index < 0 || index >= _lines.Count) throw new ArgumentOutOfRangeException(nameof(index));
    }

    private readonly record struct Line(string Text, string Terminator);
}
