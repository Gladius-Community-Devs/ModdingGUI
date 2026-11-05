using System;
using System.Globalization;
using System.Linq;

namespace ModdingGUI.Core.Units;

/// <summary>One line of a skillset: <c>minLevel maxLevel SkillName</c>.</summary>
/// <remarks>
/// The name is everything after the second number, so it may contain spaces ("Bandit AttackSpear") —
/// the pack chain looks it up by exact name in <c>skills.tok</c>.
/// </remarks>
public sealed record SkillSetRow(int MinLevel, int MaxLevel, string SkillName)
{
    /// <summary>Render the line the file uses.</summary>
    public string ToLine() =>
        string.Create(CultureInfo.InvariantCulture, $"{MinLevel} {MaxLevel} {SkillName}");

    /// <summary>Parse a row; false when the shape is not <c>min max name</c>.</summary>
    public static bool TryParse(string line, out SkillSetRow row)
    {
        row = new SkillSetRow(0, 0, string.Empty);
        if (!TryReadLevels(line, out int min, out int max, out string rest)) return false;
        if (rest.Length == 0) return false;

        row = new SkillSetRow(min, max, rest);
        return true;
    }

    /// <summary>True when this row's level window contains <paramref name="level"/>.</summary>
    public bool Covers(int level) => level >= MinLevel && level <= MaxLevel;

    internal static bool TryReadLevels(string line, out int min, out int max, out string rest)
    {
        min = max = 0;
        rest = string.Empty;

        string[] parts = line.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3) return false;

        if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out min)) return false;
        if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out max)) return false;

        rest = parts[2].Trim();
        return true;
    }
}

/// <summary>One line of an itemset: <c>minLevel maxLevel ItemName [optional note]</c>.</summary>
/// <remarks>
/// The file's own legend says any text after the item name is ignored, and vanilla uses it for bracketed
/// notes such as <c>[Weapon]</c>. Notes are kept so an edit never loses them.
/// </remarks>
public sealed record ItemSetRow(int MinLevel, int MaxLevel, string ItemName, string? Note = null)
{
    /// <summary>Render the line the file uses, keeping the note in its brackets.</summary>
    public string ToLine()
    {
        string line = string.Create(CultureInfo.InvariantCulture, $"{MinLevel} {MaxLevel} {ItemName}");
        return string.IsNullOrWhiteSpace(Note) ? line : $"{line} [{Note.Trim()}]";
    }

    /// <summary>Parse a row; false when the shape is not <c>min max item [note]</c>.</summary>
    public static bool TryParse(string line, out ItemSetRow row)
    {
        row = new ItemSetRow(0, 0, string.Empty);
        if (!SkillSetRow.TryReadLevels(line, out int min, out int max, out string rest)) return false;
        if (rest.Length == 0) return false;

        string name = rest;
        string? note = null;

        int open = rest.LastIndexOf('[');
        if (open > 0 && rest.EndsWith(']'))
        {
            name = rest[..open].TrimEnd();
            note = rest[(open + 1)..^1].Trim();
        }

        row = new ItemSetRow(min, max, name, note);
        return true;
    }

    /// <summary>True when this row's level window contains <paramref name="level"/>.</summary>
    public bool Covers(int level) => level >= MinLevel && level <= MaxLevel;
}

/// <summary>Findings shared by the skill-set and item-set validators.</summary>
public static class SetValidationCodes
{
    /// <summary>A row does not have two level numbers followed by a name.</summary>
    public const string MalformedRow = "malformed-row";

    /// <summary>The level window is impossible or outside 1-30.</summary>
    public const string LevelWindow = "level-window";

    /// <summary>The referenced skill or item does not exist.</summary>
    public const string UnknownName = "unknown-name";

    /// <summary>The same entry is listed more than once (vanilla does this, so it is not an error).</summary>
    public const string DuplicateEntry = "duplicate-entry";

    /// <summary>The rows do not cover every level from 1 to 30.</summary>
    public const string IncompleteCoverage = "incomplete-coverage";

    /// <summary>NUMENTRIES disagrees with the number of sections.</summary>
    public const string CountMismatch = "count-mismatch";
}

/// <summary>How serious a set finding is.</summary>
public enum SetValidationSeverity
{
    Info,
    Warning,
}

/// <summary>One advisory finding about a skillset or itemset.</summary>
public sealed record SetValidationIssue(SetValidationSeverity Severity, string Code, int Index, string Message);

/// <summary>Shared helpers so the two set APIs validate identically.</summary>
internal static class SetValidation
{
    internal static void CheckLevelWindow(int index, int min, int max, string kind, Action<SetValidationIssue> report)
    {
        // Vanilla uses "0 30" (from the start) and at least one "0 232" window, so the only impossible
        // shape is a negative floor or a ceiling below the floor.
        if (min < 0 || max < min)
        {
            report(new SetValidationIssue(SetValidationSeverity.Warning, SetValidationCodes.LevelWindow, index,
                $"{kind} window {min}-{max} is impossible (min must be >= 0 and <= max)"));
        }
    }

    internal static bool CoversAll(string kind, int index, System.Collections.Generic.IEnumerable<(int Min, int Max)> windows,
        Action<SetValidationIssue> report)
    {
        var covered = new bool[31];
        foreach (var (min, max) in windows)
        {
            for (int level = Math.Max(1, min); level <= Math.Min(30, max); level++) covered[level] = true;
        }

        var missing = Enumerable.Range(1, 30).Where(level => !covered[level]).ToArray();
        if (missing.Length == 0) return true;

        report(new SetValidationIssue(SetValidationSeverity.Info, SetValidationCodes.IncompleteCoverage, index,
            $"{kind} does not cover level(s) {string.Join(", ", missing)}"));
        return false;
    }
}
