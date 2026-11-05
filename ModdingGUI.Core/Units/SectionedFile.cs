using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace ModdingGUI.Core.Units;

/// <summary>
/// A units file made of numbered sections (<c>Statset 0:</c>, <c>Skillset 12:</c>, …) with rows under
/// each, as used by <c>statsets.txt</c>, <c>skillsets.txt</c> and <c>itemsets.txt</c>.
/// </summary>
/// <remarks>
/// <para>
/// The three files share a shape and differ only in their section word and row grammar, so the locating
/// work lives here: find sections, remember the line of every row, and rewrite only what changes. Blank
/// lines separate sections and are never touched, which keeps a no-op save byte identical.
/// </para>
/// <para>
/// Structural edits (insert/remove row or section) re-parse the file afterwards. That costs a few
/// thousand string splits and removes a whole class of line-number bookkeeping bugs — worth it when the
/// alternative is maintaining offsets by hand.
/// </para>
/// <para>
/// <c>NUMENTRIES</c> is maintained the way this project treats it: read for information, and rewritten
/// only when the number of sections actually changes (the pack chain reads it, it does not enforce it).
/// </para>
/// </remarks>
public sealed class SectionedFile
{
    private readonly UnitTextFile _file;
    private readonly string _sectionWord;
    private readonly List<Section> _sections = new();
    private int _numEntriesLine = -1;

    private SectionedFile(UnitTextFile file, string sectionWord)
    {
        _file = file;
        _sectionWord = sectionWord;
    }

    /// <summary>True once a line has been changed, added or removed.</summary>
    public bool IsModified => _file.IsModified;

    /// <summary>How many sections the file has.</summary>
    public int Count => _sections.Count;

    /// <summary>Section indexes in file order.</summary>
    public IReadOnlyList<int> Indexes => _sections.Select(s => s.Index).ToArray();

    /// <summary>The declared <c>NUMENTRIES</c>, or null when the file has no such line.</summary>
    public int? DeclaredCount { get; private set; }

    /// <summary>Row text of a section, in file order.</summary>
    public IReadOnlyList<string> Rows(int index) =>
        (Find(index) ?? throw new InvalidOperationException($"No section {index}."))
        .RowLines.Select(_file.GetLine).ToArray();

    /// <summary>Load and parse a file.</summary>
    /// <param name="path">Absolute path.</param>
    /// <param name="sectionWord">The word before the index, e.g. <c>Statset</c>.</param>
    public static SectionedFile Load(string path, string sectionWord) =>
        Parse(UnitTextFile.Load(path), sectionWord);

    /// <summary>Parse already-read bytes.</summary>
    public static SectionedFile Parse(byte[] bytes, string sectionWord) =>
        Parse(UnitTextFile.Parse(bytes), sectionWord);

    private static SectionedFile Parse(UnitTextFile file, string sectionWord)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sectionWord);

        var result = new SectionedFile(file, sectionWord);
        var header = new Regex($@"^{Regex.Escape(sectionWord)}\s+(\d+):\s*$", RegexOptions.CultureInvariant);
        var numEntries = new Regex(@"^NUMENTRIES:\s*(\d+)\s*$", RegexOptions.CultureInvariant);

        string[] lines = file.Lines.ToArray();
        Section? current = null;

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];

            Match count = numEntries.Match(line);
            if (count.Success)
            {
                result.DeclaredCount = int.Parse(count.Groups[1].Value, CultureInfo.InvariantCulture);
                result._numEntriesLine = i;
                continue;
            }

            Match match = header.Match(line);
            if (match.Success)
            {
                current = new Section
                {
                    Index = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture),
                    HeaderLine = i,
                };
                result._sections.Add(current);
                continue;
            }

            if (current is null) continue;                      // preamble
            if (line.Trim().Length == 0)
            {
                current = null;                                  // a blank line ends the section
                continue;
            }

            current.RowLines.Add(i);
        }

        return result;
    }

    /// <summary>Replace one row's text in place (no structural change).</summary>
    public void ReplaceRow(int sectionIndex, int rowOrdinal, string text)
    {
        var section = Find(sectionIndex) ?? throw new InvalidOperationException($"No section {sectionIndex}.");
        if (rowOrdinal < 0 || rowOrdinal >= section.RowLines.Count)
            throw new ArgumentOutOfRangeException(nameof(rowOrdinal));

        _file.ReplaceLine(section.RowLines[rowOrdinal], text);
    }

    /// <summary>Insert a row after <paramref name="afterOrdinal"/>; -1 puts it first in the section.</summary>
    public void InsertRow(int sectionIndex, int afterOrdinal, string text)
    {
        var section = Find(sectionIndex) ?? throw new InvalidOperationException($"No section {sectionIndex}.");
        if (afterOrdinal < -1 || afterOrdinal >= section.RowLines.Count)
            throw new ArgumentOutOfRangeException(nameof(afterOrdinal));

        int at = afterOrdinal < 0 ? section.HeaderLine + 1 : section.RowLines[afterOrdinal] + 1;
        _file.InsertLine(at, text);
        Rebind();
    }

    /// <summary>Remove one row.</summary>
    public void RemoveRow(int sectionIndex, int rowOrdinal)
    {
        var section = Find(sectionIndex) ?? throw new InvalidOperationException($"No section {sectionIndex}.");
        if (rowOrdinal < 0 || rowOrdinal >= section.RowLines.Count)
            throw new ArgumentOutOfRangeException(nameof(rowOrdinal));

        _file.RemoveLine(section.RowLines[rowOrdinal]);
        Rebind();
    }

    /// <summary>
    /// Append a section using the next free index. <c>NUMENTRIES</c> follows because the count changed.
    /// </summary>
    public int AddSection(IEnumerable<string> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        int index = _sections.Count == 0 ? 0 : _sections.Max(s => s.Index) + 1;
        int at = EndOfLastBlock();

        _file.InsertLine(at, string.Empty);                      // separator before the new section
        _file.InsertLine(at + 1, $"{_sectionWord} {index}:");
        int cursor = at + 2;
        foreach (string row in rows)
        {
            _file.InsertLine(cursor, row);
            cursor++;
        }

        Rebind();
        UpdateNumEntries();
        return index;
    }

    /// <summary>Remove a whole section (header, rows and one separator). <c>NUMENTRIES</c> follows.</summary>
    public bool RemoveSection(int index)
    {
        var section = Find(index);
        if (section is null) return false;

        int last = section.RowLines.Count > 0 ? section.RowLines[^1] : section.HeaderLine;
        if (last + 1 < _file.Count && _file.GetLine(last + 1).Trim().Length == 0) last++;

        for (int line = last; line >= section.HeaderLine; line--) _file.RemoveLine(line);

        Rebind();
        UpdateNumEntries();
        return true;
    }

    /// <summary>Serialise; an unmodified file reproduces the input bytes.</summary>
    public byte[] ToBytes() => _file.ToBytes();

    /// <summary>Write through a backup.</summary>
    public void Save(string projectRootOrBec, string relativePath)
    {
        string bec = ProjectRoots.ResolveBecFolder(projectRootOrBec);
        _file.Save(bec, relativePath);
    }

    /// <summary>Rewrite NUMENTRIES to the section count when they disagree.</summary>
    private void UpdateNumEntries()
    {
        if (_numEntriesLine < 0) return;

        string expected = _sections.Count.ToString(CultureInfo.InvariantCulture);
        Match match = Regex.Match(_file.GetLine(_numEntriesLine), @"^(NUMENTRIES:\s*)(\d+)(.*)$",
            RegexOptions.CultureInvariant);
        if (!match.Success || match.Groups[2].Value == expected) return;

        _file.ReplaceLine(_numEntriesLine, match.Groups[1].Value + expected + match.Groups[3].Value);
        DeclaredCount = _sections.Count;
    }

    /// <summary>Line index where a new section may start: after the last row, before trailing blanks.</summary>
    private int EndOfLastBlock()
    {
        if (_sections.Count == 0) return _file.Count;

        var last = _sections[^1];
        int end = last.RowLines.Count > 0 ? last.RowLines[^1] : last.HeaderLine;

        int trailing = 0;
        for (int i = _file.Count - 1; i > end && _file.GetLine(i).Trim().Length == 0; i--) trailing++;
        return _file.Count - trailing;
    }

    /// <summary>Re-parse after a structural edit so every stored line number is truthful again.</summary>
    private void Rebind()
    {
        var reparsed = Parse(_file.ToBytes(), _sectionWord);
        _sections.Clear();
        _sections.AddRange(reparsed._sections);
        _numEntriesLine = reparsed._numEntriesLine;
        DeclaredCount = reparsed.DeclaredCount;
    }

    private Section? Find(int index) => _sections.FirstOrDefault(s => s.Index == index);

    private sealed class Section
    {
        public int Index { get; set; }
        public int HeaderLine { get; set; }
        public List<int> RowLines { get; } = new();
    }
}
