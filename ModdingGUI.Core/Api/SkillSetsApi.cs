using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ModdingGUI.Core.Units;

namespace ModdingGUI.Core.Api;

/// <summary>One skillset: its index and the skill rows that apply at each level.</summary>
public sealed record SkillSetDto(int Index, IReadOnlyList<SkillSetRow> Rows)
{
    /// <summary>Skills available at a level, in file order.</summary>
    public IReadOnlyList<SkillSetRow> At(int level) => Rows.Where(r => r.Covers(level)).ToArray();
}

/// <summary>
/// Edit <c>data/units/skillsets.txt</c>: 230 sets of <c>minLevel maxLevel SkillName</c> rows, which is
/// what a gladiator's <c>Skill set</c> index selects.
/// </summary>
/// <remarks>
/// Skill names are validated against <c>data/config/skills.tok</c> because the pack chain resolves them
/// by exact name; a typo silently drops the skill from the compiled index.
/// </remarks>
public interface ISkillSetsApi
{
    /// <summary>Every set index, in file order.</summary>
    IReadOnlyList<int> List(string projectRoot);

    /// <summary>One set, or null when the index does not exist.</summary>
    SkillSetDto? Get(string projectRoot, int index);

    /// <summary>Replace one row by position.</summary>
    void SetRow(string projectRoot, int index, int ordinal, SkillSetRow row);

    /// <summary>Add a row, inserted in (minLevel, maxLevel, name) order.</summary>
    void AddRow(string projectRoot, int index, SkillSetRow row);

    /// <summary>Remove a row by position; false when the ordinal is out of range.</summary>
    bool RemoveRow(string projectRoot, int index, int ordinal);

    /// <summary>Append a new, empty set.</summary>
    int AddSet(string projectRoot);

    /// <summary>Remove a whole set.</summary>
    bool RemoveSet(string projectRoot, int index);

    /// <summary>Advisory checks for one set or all of them.</summary>
    IReadOnlyList<SetValidationIssue> Validate(string projectRoot, int? index = null);
}

/// <summary>Default <see cref="ISkillSetsApi"/>.</summary>
public sealed class SkillSetsApi : ISkillSetsApi
{
    internal const string RelativePath = "data/units/skillsets.txt";
    private const string SectionWord = "Skillset";

    private readonly ISkillsApi _skills;

    /// <summary>Create the API; the skill list is used for validation only.</summary>
    public SkillSetsApi(ISkillsApi? skills = null) => _skills = skills ?? new SkillsApi();

    /// <inheritdoc />
    public IReadOnlyList<int> List(string projectRoot) => Load(projectRoot).Indexes;

    /// <inheritdoc />
    public SkillSetDto? Get(string projectRoot, int index)
    {
        var file = Load(projectRoot);
        return ToDto(file, index);
    }

    /// <inheritdoc />
    public void SetRow(string projectRoot, int index, int ordinal, SkillSetRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        string bec = ProjectRoots.ResolveBecFolder(projectRoot);

        using (ProjectLock.Acquire(bec))
        {
            var file = Load(bec);
            var rows = ParseRows(file, index);
            if (ordinal < 0 || ordinal >= rows.Count) throw new ArgumentOutOfRangeException(nameof(ordinal));
            if (rows[ordinal] == row) return;

            file.ReplaceRow(index, ordinal, row.ToLine());
            file.Save(bec, RelativePath);
        }
    }

    /// <inheritdoc />
    public void AddRow(string projectRoot, int index, SkillSetRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentException.ThrowIfNullOrWhiteSpace(row.SkillName);
        string bec = ProjectRoots.ResolveBecFolder(projectRoot);

        using (ProjectLock.Acquire(bec))
        {
            var file = Load(bec);
            var rows = ParseRows(file, index);

            int after = -1;
            for (int i = 0; i < rows.Count; i++)
            {
                if (Compare(rows[i], row) <= 0) after = i;
            }

            file.InsertRow(index, after, row.ToLine());
            file.Save(bec, RelativePath);
        }
    }

    /// <inheritdoc />
    public bool RemoveRow(string projectRoot, int index, int ordinal)
    {
        string bec = ProjectRoots.ResolveBecFolder(projectRoot);

        using (ProjectLock.Acquire(bec))
        {
            var file = Load(bec);
            var rows = ParseRows(file, index);
            if (ordinal < 0 || ordinal >= rows.Count) return false;

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
    public IReadOnlyList<SetValidationIssue> Validate(string projectRoot, int? index = null)
    {
        string bec = ProjectRoots.ResolveBecFolder(projectRoot);
        var file = Load(bec);
        var issues = new List<SetValidationIssue>();

        if (file.DeclaredCount is int declared && declared != file.Count)
        {
            issues.Add(new SetValidationIssue(SetValidationSeverity.Info, SetValidationCodes.CountMismatch, -1,
                $"NUMENTRIES says {declared} but the file has {file.Count} sets"));
        }

        HashSet<string>? known = null;
        try
        {
            known = new HashSet<string>(_skills.List(bec), StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is IOException or FileNotFoundException)
        {
            known = null;   // no skills.tok in this project: skip the cross-check
        }

        foreach (int setIndex in file.Indexes)
        {
            if (index is not null && setIndex != index) continue;

            var lines = file.Rows(setIndex);
            var windows = new List<(int Min, int Max)>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int ordinal = 0; ordinal < lines.Count; ordinal++)
            {
                if (!SkillSetRow.TryParse(lines[ordinal], out SkillSetRow row))
                {
                    issues.Add(new SetValidationIssue(SetValidationSeverity.Warning,
                        SetValidationCodes.MalformedRow, setIndex,
                        $"row {ordinal + 1} is not 'min max SkillName': '{lines[ordinal]}'"));
                    continue;
                }

                windows.Add((row.MinLevel, row.MaxLevel));
                SetValidation.CheckLevelWindow(setIndex, row.MinLevel, row.MaxLevel, "skill", issues.Add);

                if (known is not null && !known.Contains(row.SkillName))
                {
                    issues.Add(new SetValidationIssue(SetValidationSeverity.Warning,
                        SetValidationCodes.UnknownName, setIndex,
                        $"'{row.SkillName}' is not a skill in skills.tok"));
                }

                if (!seen.Add($"{row.MinLevel}|{row.MaxLevel}|{row.SkillName}"))
                {
                    issues.Add(new SetValidationIssue(SetValidationSeverity.Info,
                        SetValidationCodes.DuplicateEntry, setIndex,
                        $"'{row.SkillName}' is listed twice for levels {row.MinLevel}-{row.MaxLevel}"));
                }
            }

            SetValidation.CoversAll("skillset", setIndex, windows, issues.Add);
        }

        return issues;
    }

    private static int Compare(SkillSetRow a, SkillSetRow b)
    {
        int byMin = a.MinLevel.CompareTo(b.MinLevel);
        if (byMin != 0) return byMin;
        int byMax = a.MaxLevel.CompareTo(b.MaxLevel);
        if (byMax != 0) return byMax;
        return string.Compare(a.SkillName, b.SkillName, StringComparison.OrdinalIgnoreCase);
    }

    private static SectionedFile Load(string projectRootOrBec)
    {
        string bec = ProjectRoots.ResolveBecFolder(projectRootOrBec);
        return SectionedFile.Load(
            Path.Combine(bec, RelativePath.Replace('/', Path.DirectorySeparatorChar)), SectionWord);
    }

    private static List<SkillSetRow> ParseRows(SectionedFile file, int index)
    {
        if (!file.Indexes.Contains(index)) throw new InvalidOperationException($"No skillset {index}.");

        return file.Rows(index)
            .Select(line => SkillSetRow.TryParse(line, out SkillSetRow row) ? row : null)
            .Where(row => row is not null)
            .Select(row => row!)
            .ToList();
    }

    private static SkillSetDto? ToDto(SectionedFile file, int index)
    {
        if (!file.Indexes.Contains(index)) return null;

        var rows = file.Rows(index)
            .Select(line => SkillSetRow.TryParse(line, out SkillSetRow row) ? row : null)
            .Where(row => row is not null)
            .Select(row => row!)
            .ToArray();

        return new SkillSetDto(index, rows);
    }
}
