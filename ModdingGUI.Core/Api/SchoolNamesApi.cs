using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ModdingGUI.Core.Units;

namespace ModdingGUI.Core.Api;

/// <summary>One entry of the school name table.</summary>
public sealed record SchoolNameEntry(int Index, string Name);

/// <summary>How serious a school-name finding is.</summary>
public enum SchoolNameValidationSeverity
{
    Info,
    Warning,
}

/// <summary>Stable finding codes for the name table.</summary>
public static class SchoolNameValidationCodes
{
    /// <summary>Two entries share a name.</summary>
    public const string DuplicateName = "duplicate-name";

    /// <summary>The entry numbering has a gap or is not ascending.</summary>
    public const string Numbering = "numbering";

    /// <summary>NUMENTRIES disagrees with the number of entries.</summary>
    public const string CountMismatch = "count-mismatch";

    /// <summary>An entry has no name (entry 0 is blank in vanilla, so this can be legitimate).</summary>
    public const string EmptyName = "empty-name";
}

/// <summary>One advisory finding about the school name table.</summary>
public sealed record SchoolNameValidationIssue(
    SchoolNameValidationSeverity Severity, string Code, int Index, string Message);

/// <summary>
/// Edit <c>data/units/schools.txt</c>: the 144-entry table of school *names* (<c>1: Fliuch Falcons</c>),
/// which a gladiator's <c>School</c> index points into.
/// </summary>
/// <remarks>
/// <para>
/// This is not the school roster format — those live in <c>data/school/*.tok</c> and are handled by
/// <see cref="ISchoolsApi"/>. This file only maps an index to a display name.
/// </para>
/// <para>
/// Indexes are referenced by gladiators, so entries can be renamed and appended freely, but only the
/// last entry can be removed: deleting a middle one would shift every index after it and silently
/// repoint units at a different school.
/// </para>
/// </remarks>
public interface ISchoolNamesApi
{
    /// <summary>Every entry, in file order.</summary>
    IReadOnlyList<SchoolNameEntry> List(string projectRoot);

    /// <summary>The name at an index, or null when the index is not present.</summary>
    string? Get(string projectRoot, int index);

    /// <summary>Rename an entry in place.</summary>
    void SetName(string projectRoot, int index, string name);

    /// <summary>Append a new entry with the next free index; returns that index.</summary>
    int Add(string projectRoot, string name);

    /// <summary>Remove the highest-numbered entry; false when the table is empty.</summary>
    bool RemoveLast(string projectRoot);

    /// <summary>Advisory checks.</summary>
    IReadOnlyList<SchoolNameValidationIssue> Validate(string projectRoot);
}

/// <summary>Default <see cref="ISchoolNamesApi"/>.</summary>
public sealed class SchoolNamesApi : ISchoolNamesApi
{
    internal const string RelativePath = "data/units/schools.txt";

    private static readonly Regex Entry = new(@"^(\d+):\s?(.*)$", RegexOptions.CultureInvariant);
    private static readonly Regex NumEntries = new(@"^NUMENTRIES:\s*(\d+)\s*$", RegexOptions.CultureInvariant);

    /// <inheritdoc />
    public IReadOnlyList<SchoolNameEntry> List(string projectRoot) => Parse(Load(projectRoot)).Entries;

    /// <inheritdoc />
    public string? Get(string projectRoot, int index) =>
        Parse(Load(projectRoot)).Entries.FirstOrDefault(e => e.Index == index)?.Name;

    /// <inheritdoc />
    public void SetName(string projectRoot, int index, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        string bec = ProjectRoots.ResolveBecFolder(projectRoot);

        using (ProjectLock.Acquire(bec))
        {
            var file = Load(bec);
            var parsed = Parse(file);
            var entry = parsed.Entries.FirstOrDefault(e => e.Index == index)
                        ?? throw new InvalidOperationException($"No school name at index {index}.");

            if (string.Equals(entry.Name, name, StringComparison.Ordinal)) return;

            file.ReplaceLine(parsed.LineOf[index], $"{index}: {name}");
            file.Save(bec, RelativePath);
        }
    }

    /// <inheritdoc />
    public int Add(string projectRoot, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        string bec = ProjectRoots.ResolveBecFolder(projectRoot);

        using (ProjectLock.Acquire(bec))
        {
            var file = Load(bec);
            var parsed = Parse(file);
            int index = parsed.Entries.Count == 0 ? 0 : parsed.Entries.Max(e => e.Index) + 1;

            file.InsertLine(file.Count, $"{index}: {name}");
            UpdateNumEntries(file, index + 1);
            file.Save(bec, RelativePath);
            return index;
        }
    }

    /// <inheritdoc />
    public bool RemoveLast(string projectRoot)
    {
        string bec = ProjectRoots.ResolveBecFolder(projectRoot);

        using (ProjectLock.Acquire(bec))
        {
            var file = Load(bec);
            var parsed = Parse(file);
            if (parsed.Entries.Count == 0) return false;

            var entry = parsed.Entries.MaxBy(e => e.Index)!;
            file.RemoveLine(parsed.LineOf[entry.Index]);
            UpdateNumEntries(file, entry.Index);
            file.Save(bec, RelativePath);
            return true;
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<SchoolNameValidationIssue> Validate(string projectRoot)
    {
        string bec = ProjectRoots.ResolveBecFolder(projectRoot);
        var file = Load(bec);
        var parsed = Parse(file);
        var issues = new List<SchoolNameValidationIssue>();

        if (parsed.DeclaredCount is int declared && declared != parsed.Entries.Count)
        {
            issues.Add(new SchoolNameValidationIssue(SchoolNameValidationSeverity.Info,
                SchoolNameValidationCodes.CountMismatch, -1,
                $"NUMENTRIES says {declared} but the file has {parsed.Entries.Count} entries"));
        }

        foreach (var group in parsed.Entries.GroupBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
                     .Where(g => g.Count() > 1))
        {
            issues.Add(new SchoolNameValidationIssue(SchoolNameValidationSeverity.Warning,
                SchoolNameValidationCodes.DuplicateName, group.First().Index,
                $"the name '{group.Key}' is used by indexes {string.Join(", ", group.Select(e => e.Index))}"));
        }

        for (int i = 0; i < parsed.Entries.Count; i++)
        {
            if (parsed.Entries[i].Index == i) continue;

            issues.Add(new SchoolNameValidationIssue(SchoolNameValidationSeverity.Warning,
                SchoolNameValidationCodes.Numbering, parsed.Entries[i].Index,
                $"entry {i + 1} of the table is numbered {parsed.Entries[i].Index}; indexes must be contiguous from 0"));
            break;
        }

        foreach (var entry in parsed.Entries.Where(e => string.IsNullOrWhiteSpace(e.Name)))
        {
            issues.Add(new SchoolNameValidationIssue(SchoolNameValidationSeverity.Info,
                SchoolNameValidationCodes.EmptyName, entry.Index,
                $"entry {entry.Index} has no name (vanilla leaves entry 0 blank)"));
        }

        return issues;
    }

    private static UnitTextFile Load(string projectRootOrBec)
    {
        string bec = ProjectRoots.ResolveBecFolder(projectRootOrBec);
        return UnitTextFile.Load(Path.Combine(bec, RelativePath.Replace('/', Path.DirectorySeparatorChar)));
    }

    private static void UpdateNumEntries(UnitTextFile file, int count)
    {
        for (int i = 0; i < file.Count; i++)
        {
            Match match = NumEntries.Match(file.GetLine(i));
            if (!match.Success) continue;

            if (match.Groups[1].Value != count.ToString(CultureInfo.InvariantCulture))
            {
                file.ReplaceLine(i, $"NUMENTRIES: {count}");
            }
            return;
        }
    }

    private static Parsed Parse(UnitTextFile file)
    {
        var entries = new List<SchoolNameEntry>();
        var lineOf = new Dictionary<int, int>();
        int? declared = null;

        for (int i = 0; i < file.Count; i++)
        {
            string line = file.GetLine(i);

            Match count = NumEntries.Match(line);
            if (count.Success)
            {
                declared = int.Parse(count.Groups[1].Value, CultureInfo.InvariantCulture);
                continue;
            }

            Match match = Entry.Match(line);
            if (!match.Success) continue;

            int index = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            entries.Add(new SchoolNameEntry(index, match.Groups[2].Value.Trim()));
            lineOf[index] = i;
        }

        return new Parsed(entries, lineOf, declared);
    }

    private sealed record Parsed(
        IReadOnlyList<SchoolNameEntry> Entries,
        IReadOnlyDictionary<int, int> LineOf,
        int? DeclaredCount);
}
