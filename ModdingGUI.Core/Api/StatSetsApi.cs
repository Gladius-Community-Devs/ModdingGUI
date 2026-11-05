using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace ModdingGUI.Core.Api;

using ModdingGUI.Core.Units;

/// <summary>One level line of a stat set: <c>level: con pow acc def ini/mov</c>.</summary>
public sealed record StatSetRow(int Level, int Con, int Pow, int Acc, int Def, int IniMov)
{
    /// <summary>Render the line the file uses.</summary>
    public string ToLine() => string.Create(CultureInfo.InvariantCulture,
        $"{Level}: {Con} {Pow} {Acc} {Def} {IniMov}");

    /// <summary>Parse a row; false when the line is not five integers after a level prefix.</summary>
    public static bool TryParse(string line, out StatSetRow row)
    {
        row = new StatSetRow(0, 0, 0, 0, 0, 0);
        int colon = line.IndexOf(':');
        if (colon <= 0) return false;

        if (!int.TryParse(line[..colon].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int level))
            return false;

        string[] parts = line[(colon + 1)..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 5) return false;

        var values = new int[5];
        for (int i = 0; i < 5; i++)
        {
            if (!int.TryParse(parts[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out values[i]))
                return false;
        }

        row = new StatSetRow(level, values[0], values[1], values[2], values[3], values[4]);
        return true;
    }
}

/// <summary>One stat set: its index and its level rows.</summary>
public sealed record StatSetDto(int Index, IReadOnlyList<StatSetRow> Rows)
{
    /// <summary>The row for a level, or null.</summary>
    public StatSetRow? Row(int level) => Rows.FirstOrDefault(r => r.Level == level);
}

/// <summary>
/// Edit <c>data/units/statsets.txt</c>: 169 sets of 30 level rows, which is where every gladiator's
/// per-level CON/POW/ACC/DEF/INI+MOV comes from.
/// </summary>
public interface IStatSetsApi
{
    /// <summary>Every set index, in file order.</summary>
    IReadOnlyList<int> List(string projectRoot);

    /// <summary>One set, or null when the index does not exist.</summary>
    StatSetDto? Get(string projectRoot, int index);

    /// <summary>Replace the values of one level row (the level comes from <paramref name="row"/>).</summary>
    void SetRow(string projectRoot, int index, StatSetRow row);

    /// <summary>Append a row to a set.</summary>
    void AddRow(string projectRoot, int index, StatSetRow row);

    /// <summary>Remove a level row; false when that level is not in the set.</summary>
    bool RemoveRow(string projectRoot, int index, int level);

    /// <summary>Append a new set (30 empty-ish rows are not created: a set starts as you fill it).</summary>
    int AddSet(string projectRoot);

    /// <summary>Remove a whole set.</summary>
    bool RemoveSet(string projectRoot, int index);

    /// <summary>Advisory checks for one set or all of them.</summary>
    IReadOnlyList<StatSetValidationIssue> Validate(string projectRoot, int? index = null);
}

/// <summary>How serious a stat-set finding is.</summary>
public enum StatSetValidationSeverity
{
    Info,
    Warning,
}

/// <summary>Stable finding codes.</summary>
public static class StatSetValidationCodes
{
    /// <summary>A row does not have five integers after the level.</summary>
    public const string MalformedRow = "malformed-row";

    /// <summary>Levels are not strictly ascending (the file reads as a table of levels).</summary>
    public const string LevelOutOfOrder = "level-out-of-order";

    /// <summary>A level appears twice in one set.</summary>
    public const string DuplicateLevel = "duplicate-level";

    /// <summary>The set does not cover levels 1-30, which is what the corpus does.</summary>
    public const string UnexpectedCoverage = "unexpected-coverage";

    /// <summary>A stat is negative or implausibly high.</summary>
    public const string StatOutOfRange = "stat-out-of-range";

    /// <summary>NUMENTRIES disagrees with the number of sections.</summary>
    public const string CountMismatch = "count-mismatch";
}

/// <summary>One advisory finding about a stat set.</summary>
public sealed record StatSetValidationIssue(
    StatSetValidationSeverity Severity, string Code, int Index, string Message);

/// <summary>Default <see cref="IStatSetsApi"/>.</summary>
public sealed class StatSetsApi : IStatSetsApi
{
    internal const string RelativePath = "data/units/statsets.txt";
    private const string SectionWord = "Statset";

    /// <summary>Highest stat value the corpus uses (INI+MOV reaches ~200 at level 30).</summary>
    private const int MaxStatValue = 999;

    /// <inheritdoc />
    public IReadOnlyList<int> List(string projectRoot) => Load(projectRoot).Indexes;

    /// <inheritdoc />
    public StatSetDto? Get(string projectRoot, int index) => ToDto(Load(projectRoot), index);

    /// <inheritdoc />
    public void SetRow(string projectRoot, int index, StatSetRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        string bec = ProjectRoots.ResolveBecFolder(projectRoot);

        using (ProjectLock.Acquire(bec))
        {
            var file = Load(bec);
            IReadOnlyList<StatSetRow> rows = Rows(file, index);
            int ordinal = rows.ToList().FindIndex(r => r.Level == row.Level);
            if (ordinal < 0) throw new InvalidOperationException($"Stat set {index} has no level {row.Level}.");

            if (rows[ordinal] == row) return;
            file.ReplaceRow(index, ordinal, row.ToLine());
            file.Save(bec, RelativePath);
        }
    }

    /// <inheritdoc />
    public void AddRow(string projectRoot, int index, StatSetRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        string bec = ProjectRoots.ResolveBecFolder(projectRoot);

        using (ProjectLock.Acquire(bec))
        {
            var file = Load(bec);
            var rows = Rows(file, index).ToList();
            if (rows.Any(r => r.Level == row.Level))
                throw new InvalidOperationException($"Stat set {index} already has level {row.Level}.");

            int after = -1;
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].Level < row.Level) after = i;
            }

            file.InsertRow(index, after, row.ToLine());
            file.Save(bec, RelativePath);
        }
    }

    /// <inheritdoc />
    public bool RemoveRow(string projectRoot, int index, int level)
    {
        string bec = ProjectRoots.ResolveBecFolder(projectRoot);

        using (ProjectLock.Acquire(bec))
        {
            var file = Load(bec);
            var rows = Rows(file, index).ToList();
            int ordinal = rows.FindIndex(r => r.Level == level);
            if (ordinal < 0) return false;

            file.RemoveRow(index, ordinal);
            file.Save(bec, RelativePath);
            return true;
        }
    }

    /// <inheritdoc />
    public int AddSet(string projectRoot)
    {
        string bec = ProjectRoots.ResolveBecFolder(projectRoot);

        using (ProjectLock.Acquire(bec))
        {
            var file = Load(bec);
            int index = file.AddSection(Array.Empty<string>());
            file.Save(bec, RelativePath);
            return index;
        }
    }

    /// <inheritdoc />
    public bool RemoveSet(string projectRoot, int index)
    {
        string bec = ProjectRoots.ResolveBecFolder(projectRoot);

        using (ProjectLock.Acquire(bec))
        {
            var file = Load(bec);
            if (!file.RemoveSection(index)) return false;
            file.Save(bec, RelativePath);
            return true;
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<StatSetValidationIssue> Validate(string projectRoot, int? index = null)
    {
        string bec = ProjectRoots.ResolveBecFolder(projectRoot);
        var file = Load(bec);
        var issues = new List<StatSetValidationIssue>();

        if (file.DeclaredCount is int declared && declared != file.Count)
        {
            issues.Add(new StatSetValidationIssue(StatSetValidationSeverity.Info,
                StatSetValidationCodes.CountMismatch, -1,
                $"NUMENTRIES says {declared} but the file has {file.Count} sets"));
        }

        foreach (int setIndex in file.Indexes)
        {
            if (index is not null && setIndex != index) continue;

            var lines = file.Rows(setIndex);
            int previousLevel = int.MinValue;
            var seen = new HashSet<int>();

            for (int ordinal = 0; ordinal < lines.Count; ordinal++)
            {
                if (!StatSetRow.TryParse(lines[ordinal], out StatSetRow row))
                {
                    issues.Add(new StatSetValidationIssue(StatSetValidationSeverity.Warning,
                        StatSetValidationCodes.MalformedRow, setIndex,
                        $"row {ordinal + 1} is not 'level: con pow acc def ini': '{lines[ordinal]}'"));
                    continue;
                }

                if (!seen.Add(row.Level))
                {
                    issues.Add(new StatSetValidationIssue(StatSetValidationSeverity.Warning,
                        StatSetValidationCodes.DuplicateLevel, setIndex, $"level {row.Level} appears twice"));
                }

                if (row.Level <= previousLevel)
                {
                    issues.Add(new StatSetValidationIssue(StatSetValidationSeverity.Info,
                        StatSetValidationCodes.LevelOutOfOrder, setIndex,
                        $"level {row.Level} follows level {previousLevel}"));
                }
                previousLevel = row.Level;

                foreach (int value in new[] { row.Con, row.Pow, row.Acc, row.Def, row.IniMov })
                {
                    if (value is >= 0 and <= MaxStatValue) continue;
                    issues.Add(new StatSetValidationIssue(StatSetValidationSeverity.Warning,
                        StatSetValidationCodes.StatOutOfRange, setIndex,
                        $"level {row.Level} has an implausible stat value ({value}); expected 0-{MaxStatValue}"));
                    break;
                }
            }

            bool coversVanillaShape = lines.Count == 30 && seen.SetEquals(Enumerable.Range(1, 30));
            if (!coversVanillaShape)
            {
                issues.Add(new StatSetValidationIssue(StatSetValidationSeverity.Info,
                    StatSetValidationCodes.UnexpectedCoverage, setIndex,
                    $"{lines.Count} level row(s); vanilla sets cover levels 1-30"));
            }
        }

        return issues;
    }

    private static SectionedFile Load(string projectRootOrBec)
    {
        string bec = ProjectRoots.ResolveBecFolder(projectRootOrBec);
        return SectionedFile.Load(Path.Combine(bec, RelativePath.Replace('/', Path.DirectorySeparatorChar)), SectionWord);
    }

    private static IReadOnlyList<StatSetRow> Rows(SectionedFile file, int index) =>
        file.Rows(index)
            .Select(line => StatSetRow.TryParse(line, out StatSetRow row) ? row : null)
            .Where(row => row is not null)
            .Select(row => row!)
            .ToArray();

    private static StatSetDto? ToDto(SectionedFile file, int index)
    {
        if (!file.Indexes.Contains(index)) return null;
        return new StatSetDto(index, Rows(file, index));
    }
}
