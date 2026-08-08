namespace ModdingGUI.Core.Api;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ModdingGUI.Core.Tok;

/// <summary>
/// Default <see cref="ISkillsApi"/> implementation. Uses raw line manipulation
/// — no typed schema — so that any unknown fields in an existing file are
/// preserved in place exactly as found.
/// </summary>
/// <remarks>
/// The canonical encoding is ISO-8859-1 (same as all other tok files).
/// Line endings in the corpus are always CRLF; the implementation normalises
/// to CRLF on write regardless of the source endings it reads.
/// </remarks>
public sealed class SkillsApi : ISkillsApi
{
    /// <summary>Canonical relative path inside a project's BEC tree.</summary>
    public const string RelativePath = "data/config/skills.tok";

    private static readonly Encoding Enc = TokDocument.CanonicalEncoding;

    // Matches:  SKILLCREATE: "Name", "Type", "SubType"
    // Groups:   1=Name  2=Type  3=SubType
    private static readonly Regex SkillCreateRx = new Regex(
        @"^SKILLCREATE:\s+""([^""]*)""\s*,\s*""([^""]*)""\s*,\s*""([^""]*)""",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // ---- ISkillsApi ---------------------------------------------------------

    /// <inheritdoc />
    public IReadOnlyList<string> List(string projectPath)
    {
        using (ProjectLock.Acquire(projectPath))
        {
            var (lines, _) = ReadLines(projectPath);
            return ExtractNames(lines);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<SkillDto> GetAll(string projectPath)
    {
        using (ProjectLock.Acquire(projectPath))
        {
            var (lines, _) = ReadLines(projectPath);
            return ParseAllBlocks(lines);
        }
    }

    /// <inheritdoc />
    public SkillDto? Get(string projectPath, string name)
    {
        using (ProjectLock.Acquire(projectPath))
        {
            var (lines, _) = ReadLines(projectPath);
            var range = FindBlock(lines, name);
            return range is null ? null : ParseBlock(lines, range.Value.start, range.Value.end);
        }
    }

    /// <inheritdoc />
    public SkillDto Update(string projectPath, string name, SkillDto skill)
    {
        if (skill is null) throw new ArgumentNullException(nameof(skill));

        using (ProjectLock.Acquire(projectPath))
        {
            var (lines, trailing) = ReadLines(projectPath);
            var range = FindBlock(lines, name)
                ?? throw new KeyNotFoundException($"Skill '{name}' not found in skills.tok.");

            var newBlock = BuildBlock(skill);
            // Replace the existing block (inclusive) with the new lines.
            lines.RemoveRange(range.start, range.end - range.start + 1);
            lines.InsertRange(range.start, newBlock);

            WriteLines(projectPath, lines, trailing);
            return skill;
        }
    }

    /// <inheritdoc />
    public SkillDto Add(string projectPath, SkillDto skill)
    {
        if (skill is null) throw new ArgumentNullException(nameof(skill));
        if (string.IsNullOrWhiteSpace(skill.Name))
            throw new ArgumentException("SkillDto.Name is required.", nameof(skill));

        using (ProjectLock.Acquire(projectPath))
        {
            var (lines, _) = ReadLines(projectPath);
            if (FindBlock(lines, skill.Name) is not null)
                throw new InvalidOperationException($"Skill '{skill.Name}' already exists.");

            var newBlock = BuildBlock(skill);
            // Append a blank separator line then the new record. Always finish
            // with a trailing newline (convention for skills.tok).
            lines.Add(string.Empty);
            lines.AddRange(newBlock);
            WriteLines(projectPath, lines, trailing: true);
            return skill;
        }
    }

    /// <inheritdoc />
    public bool Remove(string projectPath, string name)
    {
        using (ProjectLock.Acquire(projectPath))
        {
            var (lines, trailing) = ReadLines(projectPath);
            var range = FindBlock(lines, name);
            if (range is null) return false;

            int start = range.Value.start;
            int end   = range.Value.end;
            int count = end - start + 1;

            // Remove the blank separator line that immediately follows the block.
            if (end + 1 < lines.Count && string.IsNullOrWhiteSpace(lines[end + 1]))
                count++;

            lines.RemoveRange(start, count);
            WriteLines(projectPath, lines, trailing);
            return true;
        }
    }

    // ---- internals ----------------------------------------------------------

    private static string GetPath(string projectPath) =>
        Path.Combine(projectPath, RelativePath.Replace('/', Path.DirectorySeparatorChar));

    private static (List<string> lines, bool trailing) ReadLines(string projectPath)
    {
        string path = GetPath(projectPath);
        if (!File.Exists(path))
            throw new FileNotFoundException("skills.tok not found.", path);

        string text = File.ReadAllText(path, Enc);
        // Normalise to LF for splitting; remember whether there was a trailing newline.
        string normalised = text.Replace("\r\n", "\n").Replace("\r", "\n");
        bool trailing = normalised.Length > 0 && normalised[normalised.Length - 1] == '\n';
        string[] arr = normalised.TrimEnd('\n').Split('\n');
        return (arr.ToList(), trailing);
    }

    private static void WriteLines(string projectPath, List<string> lines, bool trailing)
    {
        string path = GetPath(projectPath);
        ChangeHistoryService.Backup(projectPath, path);
        // Write with canonical CRLF line endings.
        string text = string.Join("\r\n", lines) + (trailing ? "\r\n" : string.Empty);
        File.WriteAllText(path, text, Enc);
    }

    private static List<string> ExtractNames(List<string> lines)
    {
        var names = new List<string>();
        foreach (var line in lines)
        {
            var m = SkillCreateRx.Match(line);
            if (m.Success) names.Add(m.Groups[1].Value);
        }
        return names;
    }

    private static List<SkillDto> ParseAllBlocks(List<string> lines)
    {
        var result = new List<SkillDto>();
        int i = 0;
        while (i < lines.Count)
        {
            if (SkillCreateRx.IsMatch(lines[i]))
            {
                var (start, end) = BlockEndFrom(lines, i);
                result.Add(ParseBlock(lines, start, end));
                i = end + 1;
            }
            else
            {
                i++;
            }
        }
        return result;
    }

    /// <summary>
    /// Returns the (start, end) inclusive line range for the block beginning at
    /// <paramref name="createLineIdx"/>. <c>end</c> is the last non-blank,
    /// non-SKILLCREATE line (or equal to <c>start</c> for header-only records).
    /// </summary>
    private static (int start, int end) BlockEndFrom(List<string> lines, int createLineIdx)
    {
        int end = createLineIdx;
        for (int j = createLineIdx + 1; j < lines.Count; j++)
        {
            if (string.IsNullOrWhiteSpace(lines[j]) || SkillCreateRx.IsMatch(lines[j]))
                break;
            end = j;
        }
        return (createLineIdx, end);
    }

    /// <summary>
    /// Find the line range (inclusive) of the skill named <paramref name="name"/>,
    /// or <c>null</c> if absent.
    /// </summary>
    private static (int start, int end)? FindBlock(List<string> lines, string name)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            var m = SkillCreateRx.Match(lines[i]);
            if (m.Success && m.Groups[1].Value.Equals(name, StringComparison.Ordinal))
                return BlockEndFrom(lines, i);
        }
        return null;
    }

    private static SkillDto ParseBlock(List<string> lines, int start, int end)
    {
        var m = SkillCreateRx.Match(lines[start]);
        var dto = new SkillDto
        {
            Name        = m.Groups[1].Value,
            SkillType   = m.Groups[2].Value,
            SkillSubType = m.Groups[3].Value,
        };

        for (int i = start + 1; i <= end; i++)
        {
            string line = lines[i];
            int colon = line.IndexOf(':');
            if (colon > 0)
            {
                string fieldName = line.Substring(0, colon).Trim();
                string rawValue  = (colon + 1 < line.Length)
                    ? line.Substring(colon + 1).Trim()
                    : string.Empty;
                dto.Fields.Add(new SkillFieldEntry { FieldName = fieldName, RawValue = rawValue });
            }
        }
        return dto;
    }

    private static List<string> BuildBlock(SkillDto dto)
    {
        var lines = new List<string>();
        string type    = dto.SkillType    ?? string.Empty;
        string subType = dto.SkillSubType ?? string.Empty;
        lines.Add($"SKILLCREATE: \"{dto.Name}\", \"{type}\", \"{subType}\"");
        foreach (var f in dto.Fields)
            lines.Add($"{f.FieldName}: {f.RawValue}");
        return lines;
    }
}
