namespace ModdingGUI.Core.Api;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ModdingGUI.Core.Tok;
using System.Globalization;

/// <summary>One <c>KEY: value</c> line of a school file.</summary>
/// <param name="Key">Key as written.</param>
/// <param name="RawValue">Value as written, trimmed.</param>
public sealed record SchoolFieldDto(string Key, string RawValue);

/// <summary>One <c>CREATEUNIT</c> block: a unit the school starts with, and its loadout.</summary>
public sealed class SchoolUnitDto
{
    /// <summary>Unit name, from the first <c>CREATEUNIT</c> argument.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Class name, from the second <c>CREATEUNIT</c> argument.</summary>
    public string ClassName { get; init; } = string.Empty;

    /// <summary>Fields in file order. <c>SKILL</c> repeats; the rest are single.</summary>
    public IReadOnlyList<SchoolFieldDto> Fields { get; init; } = Array.Empty<SchoolFieldDto>();

    /// <summary>All values of a key, in file order.</summary>
    public IReadOnlyList<string> Values(string key) =>
        Fields.Where(f => string.Equals(f.Key, key, StringComparison.OrdinalIgnoreCase))
              .Select(f => f.RawValue)
              .ToArray();

    /// <summary>The first value of a key, or null.</summary>
    public string? Value(string key) =>
        Fields.FirstOrDefault(f => string.Equals(f.Key, key, StringComparison.OrdinalIgnoreCase))?.RawValue;

    /// <summary>
    /// <c>CORESTATSCOMP2</c> as numbers: <c>CON, PWR, ACC, DEF, INI, MOVE</c>.
    /// </summary>
    /// <remarks>
    /// The comment in the files says <c>INT</c> for the fifth value, but it is initiative —
    /// <c>statsets.txt</c> documents the order as <c>Con Pow Acc Def Ini/Mov</c> and the class
    /// table's <c>LEVELSTATAWARDS</c> rows agree.
    /// </remarks>
    public IReadOnlyList<int> CoreStats =>
        (Value("CORESTATSCOMP2") ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => int.TryParse(part, out int n) ? n : 0)
            .ToArray();

    /// <summary>The granted skills, in file order.</summary>
    public IReadOnlyList<string> Skills =>
        Values("SKILL").Select(v => v.Trim().Trim('"')).Where(v => v.Length > 0).ToArray();
}

/// <summary>A parsed <c>data/school/*.tok</c> roster: school metadata plus its starting units.</summary>
public sealed class SchoolDto
{
    /// <summary>Project-relative path.</summary>
    public string FilePath { get; init; } = string.Empty;

    /// <summary>School name from <c>NAME</c>.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Starting hero from <c>HERO</c>.</summary>
    public string? Hero { get; init; }

    /// <summary>Starting gold from <c>GOLD</c>.</summary>
    public int? Gold { get; init; }

    /// <summary>Header fields (<c>NAME</c>, <c>HERO</c>, <c>GOLD</c>, …), in file order.</summary>
    public IReadOnlyList<SchoolFieldDto> Fields { get; init; } = Array.Empty<SchoolFieldDto>();

    /// <summary>Unit blocks, in file order.</summary>
    public IReadOnlyList<SchoolUnitDto> Units { get; init; } = Array.Empty<SchoolUnitDto>();

    /// <summary>Find a unit by name (case-insensitive).</summary>
    public SchoolUnitDto? Unit(string name) =>
        Units.FirstOrDefault(u => string.Equals(u.Name, name, StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// Read and edit <c>data/school/*.tok</c> rosters through raw, comment-preserving blocks.
/// </summary>
/// <remarks>
/// A roster is a header (<c>NAME</c>, <c>HERO</c>, <c>GOLD</c>) followed by one
/// <c>CREATEUNIT</c> block per starting unit. Each block carries the unit's level, job points,
/// core stats, equipment and — repeating — its granted skills, which is why the skill
/// operations are list-shaped rather than single-value.
/// </remarks>
public interface ISchoolsApi
{
    /// <summary>Project-relative paths of every school roster, sorted.</summary>
    IReadOnlyList<string> ListFiles(string projectPath);

    /// <summary>School names, in path order.</summary>
    IReadOnlyList<string> List(string projectPath);

    /// <summary>Parse one roster; null when the file does not exist.</summary>
    SchoolDto? Get(string projectPath, string relativePath);

    /// <summary>Replace a header field (<c>NAME</c>, <c>HERO</c>, <c>GOLD</c>).</summary>
    void SetHeaderField(string projectPath, string relativePath, string key, string rawValue);

    /// <summary>
    /// Replace the class on a unit's <c>CREATEUNIT</c> line, keeping the trailing comment. The class is the second
    /// argument, so it is not a field and cannot go through <see cref="SetUnitField"/>.
    /// </summary>
    void SetUnitClass(string projectPath, string relativePath, string unitName, string className);

    /// <summary>Replace a single-valued field of a unit block (<c>LEVEL</c>, <c>CORESTATSCOMP2</c>, …).</summary>
    void SetUnitField(string projectPath, string relativePath, string unitName, string key, string rawValue);

    /// <summary>Append a field to a unit block (for repeatable keys such as <c>SKILL</c>).</summary>
    void AddUnitField(string projectPath, string relativePath, string unitName, string key, string rawValue);

    /// <summary>Remove every line for a key from a unit block.</summary>
    void RemoveUnitField(string projectPath, string relativePath, string unitName, string key);

    /// <summary>Replace the whole granted-skill list of a unit, in order.</summary>
    void SetUnitSkills(string projectPath, string relativePath, string unitName, IReadOnlyList<string> skills);

    /// <summary>Add one granted skill to a unit.</summary>
    void AddUnitSkill(string projectPath, string relativePath, string unitName, string skill);

    /// <summary>
    /// Append new unit blocks to a roster (the randomizer builds a team this way). One read, one write, one
    /// backup for the whole batch; returns how many blocks were added. The unit name must not already exist.
    /// </summary>
    int AddUnits(string projectPath, string relativePath, IReadOnlyList<SchoolUnitDto> units);

    /// <summary>
    /// Replace a whole roster: header fields (<paramref name="name"/>, <paramref name="hero"/>, gold) followed by
    /// the given unit blocks in order. This is how the randomizer writes a campaign, and it is safe to discard a
    /// roster's contents because a roster is only read when a new game starts (an in-progress run keeps its own
    /// <c>&lt;name&gt;Saved.tok</c>, written by the engine's <c>SchoolSave</c>).
    /// </summary>
    void ReplaceRoster(
        string projectPath, string relativePath, string name, string hero, int gold,
        IReadOnlyList<SchoolUnitDto> units);

    /// <summary>Advisory checks against the generated schools catalog.</summary>
    IReadOnlyList<SchoolValidationIssue> Validate(string projectPath, string relativePath);
}

/// <summary>Default <see cref="ISchoolsApi"/>: raw line editing with lock and backup.</summary>
public sealed class SchoolsApi : ISchoolsApi
{
    private const string Root = "data/school";

    private static readonly Regex CreateUnitRx = new(
        @"^\s*CREATEUNIT\s*:\s*""(?<name>[^""]*)""\s*,\s*""(?<class>[^""]*)""",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <inheritdoc />
    public IReadOnlyList<string> ListFiles(string projectPath)
    {
        string bec = ProjectRoots.ResolveBecFolder(projectPath);
        string root = Path.Combine(bec, Root.Replace('/', Path.DirectorySeparatorChar));
        if (!Directory.Exists(root)) return Array.Empty<string>();

        return Directory.EnumerateFiles(root, "*.tok")
            .Select(path => Path.GetRelativePath(bec, path).Replace(Path.DirectorySeparatorChar, '/'))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <inheritdoc />
    public IReadOnlyList<string> List(string projectPath)
    {
        var names = new List<string>();
        foreach (string file in ListFiles(projectPath))
        {
            var school = Get(projectPath, file);
            if (school is not null) names.Add(school.Name);
        }
        return names;
    }

    /// <inheritdoc />
    public SchoolDto? Get(string projectPath, string relativePath)
    {
        string bec = ProjectRoots.ResolveBecFolder(projectPath);
        string path = FullPath(bec, relativePath);
        if (!File.Exists(path)) return null;

        var (lines, _) = RawLineFile.Read(bec, relativePath, Path.GetFileName(relativePath));
        return Parse(relativePath, lines);
    }

    /// <inheritdoc />
    public void SetHeaderField(string projectPath, string relativePath, string key, string rawValue) =>
        Edit(projectPath, relativePath, lines =>
        {
            int end = FirstUnitLine(lines);
            var matches = TokLineEditing.KeyLines(lines, 0, end, key);
            if (matches.Count == 0)
                throw new InvalidOperationException($"school has no header '{key}' line.");

            TokLineEditing.ReplaceValue(lines, matches[0], rawValue);
            for (int i = matches.Count - 1; i >= 1; i--) lines.RemoveAt(matches[i]);
            return true;
        });

    /// <inheritdoc />
    public void SetUnitClass(string projectPath, string relativePath, string unitName, string className) =>
        EditUnit(projectPath, relativePath, unitName, (lines, block) =>
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(className);

            string line = lines[block.Start];
            var match = CreateUnitRx.Match(line);
            if (!match.Success) return false;

            string replaced = line[..match.Groups["class"].Index]
                              + className
                              + line[(match.Groups["class"].Index + match.Groups["class"].Length)..];

            if (string.Equals(replaced, line, StringComparison.Ordinal)) return false;

            lines[block.Start] = replaced;
            return true;
        });

    /// <inheritdoc />
    public void SetUnitField(string projectPath, string relativePath, string unitName, string key, string rawValue) =>
        EditUnit(projectPath, relativePath, unitName, (lines, block) =>
        {
            if (string.Equals(key, "SKILL", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("SKILL repeats; use SetUnitSkills or AddUnitSkill.");

            var matches = TokLineEditing.KeyLines(lines, block.Start + 1, block.End, key);
            if (matches.Count == 0)
                throw new InvalidOperationException($"unit '{unitName}' has no '{key}' line.");

            TokLineEditing.ReplaceValue(lines, matches[0], rawValue);
            for (int i = matches.Count - 1; i >= 1; i--) lines.RemoveAt(matches[i]);
            return true;
        });

    /// <inheritdoc />
    public void AddUnitField(string projectPath, string relativePath, string unitName, string key, string rawValue) =>
        EditUnit(projectPath, relativePath, unitName, (lines, block) =>
        {
            int at = block.End;
            while (at > block.Start + 1 && string.IsNullOrWhiteSpace(lines[at - 1])) at--;
            TokLineEditing.Insert(lines, at, key, rawValue, "\t");
            return true;
        });

    /// <inheritdoc />
    public void RemoveUnitField(string projectPath, string relativePath, string unitName, string key) =>
        EditUnit(projectPath, relativePath, unitName, (lines, block) =>
            TokLineEditing.RemoveKeys(lines, block.Start + 1, block.End, key) > 0);

    /// <inheritdoc />
    public void SetUnitSkills(string projectPath, string relativePath, string unitName, IReadOnlyList<string> skills)
    {
        ArgumentNullException.ThrowIfNull(skills);

        EditUnit(projectPath, relativePath, unitName, (lines, block) =>
        {
            var existing = TokLineEditing.KeyLines(lines, block.Start + 1, block.End, "SKILL");
            int insertAt = existing.Count > 0 ? existing[0] : block.End;
            string indent = existing.Count > 0 ? TokLineEditing.IndentOf(lines[existing[0]]) : "\t";

            for (int i = existing.Count - 1; i >= 0; i--) lines.RemoveAt(existing[i]);

            var replacement = skills
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => $"{indent}SKILL: \"{s.Trim().Trim('"')}\"")
                .ToList();
            lines.InsertRange(Math.Clamp(insertAt, 0, lines.Count), replacement);
            return true;
        });
    }

    /// <inheritdoc />
    public void ReplaceRoster(
        string projectPath, string relativePath, string name, string hero, int gold,
        IReadOnlyList<SchoolUnitDto> units)
    {
        ArgumentNullException.ThrowIfNull(units);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(hero);

        Edit(projectPath, relativePath, lines =>
        {
            lines.Clear();
            lines.Add($"NAME:\t\"{name}\"");
            lines.Add($"HERO:\t\"{hero}\"");
            lines.Add($"GOLD:\t{gold.ToString(CultureInfo.InvariantCulture)}");
            lines.Add(string.Empty);

            foreach (var unit in units)
            {
                if (unit is null || string.IsNullOrWhiteSpace(unit.Name)) continue;

                lines.Add(string.Empty);
                lines.Add($"CREATEUNIT:\t\t\"{unit.Name}\", \"{unit.ClassName}\"\t\t// Name, class");

                foreach (var field in unit.Fields)
                {
                    if (string.IsNullOrWhiteSpace(field.Key)) continue;
                    lines.Add($"\t{field.Key}:\t{field.RawValue}");
                }
            }

            lines.Add(string.Empty);
            return true;
        });
    }

    /// <inheritdoc />
    public int AddUnits(string projectPath, string relativePath, IReadOnlyList<SchoolUnitDto> units)
    {
        ArgumentNullException.ThrowIfNull(units);
        if (units.Count == 0) return 0;

        int added = 0;
        Edit(projectPath, relativePath, lines =>
        {
            var existing = UnitBlocks(lines)
                .Select(block => block.Name)
                .ToHashSet(StringComparer.Ordinal);

            foreach (var unit in units)
            {
                if (unit is null) continue;
                if (string.IsNullOrWhiteSpace(unit.Name) || string.IsNullOrWhiteSpace(unit.ClassName)) continue;
                if (!existing.Add(unit.Name)) continue;

                // Append after any trailing blank line so blocks stay separated the way vanilla writes them.
                int at = lines.Count;
                while (at > 0 && string.IsNullOrWhiteSpace(lines[at - 1])) at--;

                lines.Insert(at, $"CREATEUNIT: \"{unit.Name}\", \"{unit.ClassName}\"    // Name, class");

                int cursor = at + 1;
                foreach (var field in unit.Fields)
                {
                    if (string.IsNullOrWhiteSpace(field.Key)) continue;
                    lines.Insert(cursor++, $"\t{field.Key}: {field.RawValue}");
                }

                lines.Insert(cursor, string.Empty);
                added++;
            }

            return added > 0;
        });

        return added;
    }

    /// <inheritdoc />
    public void AddUnitSkill(string projectPath, string relativePath, string unitName, string skill) =>
        AddUnitField(projectPath, relativePath, unitName, "SKILL", $"\"{skill.Trim().Trim('"')}\"");

    /// <inheritdoc />
    public IReadOnlyList<SchoolValidationIssue> Validate(string projectPath, string relativePath)
    {
        var school = Get(projectPath, relativePath)
            ?? throw new FileNotFoundException($"school not found: {relativePath}");

        // The per-class CUSTOMIZE check needs the characters tree; it is skipped when the mesh cannot be read.
        var school_1 = SchoolValidator.Validate(school, SchoolsCatalog.Shipped(),
            new ModdingGUI.Core.Units.OutfitVariants(ProjectRoots.ResolveBecFolder(projectPath), new ClassDefsApi()));
        return school_1;
    }

    // ---- editing plumbing ---------------------------------------------------

    private void Edit(string projectPath, string relativePath, Func<List<string>, bool> mutate)
    {
        string bec = ProjectRoots.ResolveBecFolder(projectPath);
        using (ProjectLock.Acquire(bec))
        {
            string path = FullPath(bec, relativePath);
            if (!File.Exists(path)) throw new FileNotFoundException("school not found.", path);

            var (lines, trailing) = RawLineFile.Read(bec, relativePath, Path.GetFileName(relativePath));
            if (!mutate(lines)) return;
            RawLineFile.Write(bec, relativePath, lines, trailing);
        }
    }

    private void EditUnit(
        string projectPath, string relativePath, string unitName,
        Func<List<string>, (int Start, int End), bool> mutate)
    {
        Edit(projectPath, relativePath, lines =>
        {
            var block = FindUnit(lines, unitName)
                ?? throw new InvalidOperationException($"unit '{unitName}' not found in {relativePath}");
            return mutate(lines, block);
        });
    }

    private static string FullPath(string bec, string relativePath) =>
        Path.Combine(bec, relativePath.Replace('/', Path.DirectorySeparatorChar));

    private static List<(int Start, int End, string Name)> UnitBlocks(List<string> lines)
    {
        var starts = new List<(int Index, string Name)>();
        for (int i = 0; i < lines.Count; i++)
        {
            var match = CreateUnitRx.Match(lines[i]);
            if (match.Success) starts.Add((i, match.Groups["name"].Value));
        }

        var blocks = new List<(int, int, string)>(starts.Count);
        for (int i = 0; i < starts.Count; i++)
        {
            int end = i + 1 < starts.Count ? starts[i + 1].Index : lines.Count;
            blocks.Add((starts[i].Index, end, starts[i].Name));
        }
        return blocks;
    }

    private static (int Start, int End)? FindUnit(List<string> lines, string unitName) =>
        UnitBlocks(lines)
            .Where(b => string.Equals(b.Name, unitName, StringComparison.OrdinalIgnoreCase))
            .Select(b => ((int, int)?)(b.Start, b.End))
            .FirstOrDefault();

    private static int FirstUnitLine(List<string> lines)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            if (CreateUnitRx.IsMatch(lines[i])) return i;
        }
        return lines.Count;
    }

    internal static SchoolDto Parse(string relativePath, List<string> lines)
    {
        int firstUnit = FirstUnitLine(lines);

        var fields = new List<SchoolFieldDto>();
        for (int i = 0; i < firstUnit; i++)
        {
            if (TokLineEditing.CommentRx.IsMatch(lines[i])) continue;
            if (TokLineEditing.TryKey(lines[i], out string key, out _, out string value))
                fields.Add(new SchoolFieldDto(key, value));
        }

        var units = new List<SchoolUnitDto>();
        foreach (var (start, end, name) in UnitBlocks(lines))
        {
            var match = CreateUnitRx.Match(lines[start]);
            var unitFields = new List<SchoolFieldDto>();
            for (int i = start + 1; i < end; i++)
            {
                if (TokLineEditing.CommentRx.IsMatch(lines[i])) continue;
                if (TokLineEditing.TryKey(lines[i], out string key, out _, out string value))
                    unitFields.Add(new SchoolFieldDto(key, value));
            }

            units.Add(new SchoolUnitDto
            {
                Name = name,
                ClassName = match.Groups["class"].Value,
                Fields = unitFields,
            });
        }

        string nameValue = fields
            .FirstOrDefault(f => string.Equals(f.Key, "NAME", StringComparison.OrdinalIgnoreCase))
            ?.RawValue.Trim().Trim('"') ?? Path.GetFileNameWithoutExtension(relativePath);

        string? hero = fields
            .FirstOrDefault(f => string.Equals(f.Key, "HERO", StringComparison.OrdinalIgnoreCase))
            ?.RawValue.Trim().Trim('"');

        int? gold = int.TryParse(
            fields.FirstOrDefault(f => string.Equals(f.Key, "GOLD", StringComparison.OrdinalIgnoreCase))?.RawValue,
            out int parsedGold) ? parsedGold : null;

        return new SchoolDto
        {
            FilePath = relativePath,
            Name = nameValue,
            Hero = hero,
            Gold = gold,
            Fields = fields,
            Units = units,
        };
    }
}
