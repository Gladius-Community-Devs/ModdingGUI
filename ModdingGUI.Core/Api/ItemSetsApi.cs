using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ModdingGUI.Core.Units;

namespace ModdingGUI.Core.Api;

/// <summary>One itemset: its index and the item rows that apply at each level.</summary>
public sealed record ItemSetDto(int Index, IReadOnlyList<ItemSetRow> Rows)
{
    /// <summary>Items available at a level, in file order.</summary>
    public IReadOnlyList<ItemSetRow> At(int level) => Rows.Where(r => r.Covers(level)).ToArray();
}

/// <summary>
/// Edit <c>data/units/itemsets.txt</c>: 404 sets of <c>minLevel maxLevel ItemName [note]</c> rows, which
/// is what a gladiator's <c>Item set</c> index selects (starting equipment by level).
/// </summary>
/// <remarks>
/// Item names are validated against <c>data/config/items.tok</c> for the same reason as skills: the pack
/// chain resolves them by exact name. The bracketed text after a name is free-form (vanilla uses it for
/// notes like <c>[Weapon]</c>) and is preserved through every edit.
/// </remarks>
public interface IItemSetsApi
{
    /// <summary>Every set index, in file order.</summary>
    IReadOnlyList<int> List(string projectRoot);

    /// <summary>One set, or null when the index does not exist.</summary>
    ItemSetDto? Get(string projectRoot, int index);

    /// <summary>Replace one row by position.</summary>
    void SetRow(string projectRoot, int index, int ordinal, ItemSetRow row);

    /// <summary>
    /// Replace many rows in one pass (one read, one write, one backup). Out-of-range positions and rows that
    /// are already identical are skipped; returns how many were written.
    /// </summary>
    int ApplyMany(string projectRoot, IEnumerable<(int Index, int Ordinal, ItemSetRow Row)> rows);

    /// <summary>Add a row, inserted in (minLevel, maxLevel, name) order.</summary>
    void AddRow(string projectRoot, int index, ItemSetRow row);

    /// <summary>Remove a row by position; false when the ordinal is out of range.</summary>
    bool RemoveRow(string projectRoot, int index, int ordinal);

    /// <summary>Append a new, empty set.</summary>
    int AddSet(string projectRoot);

    /// <summary>Remove a whole set.</summary>
    bool RemoveSet(string projectRoot, int index);

    /// <summary>Advisory checks for one set or all of them.</summary>
    IReadOnlyList<SetValidationIssue> Validate(string projectRoot, int? index = null);
}

/// <summary>Default <see cref="IItemSetsApi"/>.</summary>
public sealed class ItemSetsApi : IItemSetsApi
{
    internal const string RelativePath = "data/units/itemsets.txt";
    private const string SectionWord = "Itemset";

    private readonly IItemsApi _items;

    /// <summary>Create the API; the item list is used for validation only.</summary>
    public ItemSetsApi(IItemsApi? items = null) => _items = items ?? new ItemsApi();

    /// <inheritdoc />
    public IReadOnlyList<int> List(string projectRoot) => Load(projectRoot).Indexes;

    /// <inheritdoc />
    public ItemSetDto? Get(string projectRoot, int index) => ToDto(Load(projectRoot), index);

    /// <inheritdoc />
    public void SetRow(string projectRoot, int index, int ordinal, ItemSetRow row)
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
    public int ApplyMany(string projectRoot, IEnumerable<(int Index, int Ordinal, ItemSetRow Row)> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var list = rows as ICollection<(int Index, int Ordinal, ItemSetRow Row)> ?? rows.ToList();
        if (list.Count == 0) return 0;

        string bec = ProjectRoots.ResolveBecFolder(projectRoot);
        int written = 0;

        using (ProjectLock.Acquire(bec))
        {
            var file = Load(bec);
            foreach (var (index, ordinal, row) in list)
            {
                if (row is null) continue;
                if (!file.Indexes.Contains(index)) continue;

                var current = file.Rows(index)
                    .Select(line => ItemSetRow.TryParse(line, out ItemSetRow parsed) ? parsed : null)
                    .Where(parsed => parsed is not null)
                    .Select(parsed => parsed!)
                    .ToList();

                if (ordinal < 0 || ordinal >= current.Count) continue;
                if (current[ordinal] == row) continue;

                file.ReplaceRow(index, ordinal, row.ToLine());
                written++;
            }

            if (file.IsModified) file.Save(bec, RelativePath);
        }

        return written;
    }

    /// <inheritdoc />
    public void AddRow(string projectRoot, int index, ItemSetRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentException.ThrowIfNullOrWhiteSpace(row.ItemName);
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

        HashSet<string>? known;
        try
        {
            known = new HashSet<string>(_items.List(bec), StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is IOException or FileNotFoundException)
        {
            known = null;
        }

        foreach (int setIndex in file.Indexes)
        {
            if (index is not null && setIndex != index) continue;

            var lines = file.Rows(setIndex);
            var windows = new List<(int Min, int Max)>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int ordinal = 0; ordinal < lines.Count; ordinal++)
            {
                if (!ItemSetRow.TryParse(lines[ordinal], out ItemSetRow row))
                {
                    issues.Add(new SetValidationIssue(SetValidationSeverity.Warning,
                        SetValidationCodes.MalformedRow, setIndex,
                        $"row {ordinal + 1} is not 'min max ItemName [note]': '{lines[ordinal]}'"));
                    continue;
                }

                windows.Add((row.MinLevel, row.MaxLevel));
                SetValidation.CheckLevelWindow(setIndex, row.MinLevel, row.MaxLevel, "item", issues.Add);

                if (known is not null && !known.Contains(row.ItemName))
                {
                    issues.Add(new SetValidationIssue(SetValidationSeverity.Warning,
                        SetValidationCodes.UnknownName, setIndex,
                        $"'{row.ItemName}' is not an item in items.tok"));
                }

                if (!seen.Add($"{row.MinLevel}|{row.MaxLevel}|{row.ItemName}"))
                {
                    issues.Add(new SetValidationIssue(SetValidationSeverity.Info,
                        SetValidationCodes.DuplicateEntry, setIndex,
                        $"'{row.ItemName}' is listed twice for levels {row.MinLevel}-{row.MaxLevel}"));
                }
            }

            SetValidation.CoversAll("itemset", setIndex, windows, issues.Add);
        }

        return issues;
    }

    private static int Compare(ItemSetRow a, ItemSetRow b)
    {
        int byMin = a.MinLevel.CompareTo(b.MinLevel);
        if (byMin != 0) return byMin;
        int byMax = a.MaxLevel.CompareTo(b.MaxLevel);
        if (byMax != 0) return byMax;
        return string.Compare(a.ItemName, b.ItemName, StringComparison.OrdinalIgnoreCase);
    }

    private static SectionedFile Load(string projectRootOrBec)
    {
        string bec = ProjectRoots.ResolveBecFolder(projectRootOrBec);
        return SectionedFile.Load(
            Path.Combine(bec, RelativePath.Replace('/', Path.DirectorySeparatorChar)), SectionWord);
    }

    private static List<ItemSetRow> ParseRows(SectionedFile file, int index)
    {
        if (!file.Indexes.Contains(index)) throw new InvalidOperationException($"No itemset {index}.");

        return file.Rows(index)
            .Select(line => ItemSetRow.TryParse(line, out ItemSetRow row) ? row : null)
            .Where(row => row is not null)
            .Select(row => row!)
            .ToList();
    }

    private static ItemSetDto? ToDto(SectionedFile file, int index)
    {
        if (!file.Indexes.Contains(index)) return null;

        var rows = file.Rows(index)
            .Select(line => ItemSetRow.TryParse(line, out ItemSetRow row) ? row : null)
            .Where(row => row is not null)
            .Select(row => row!)
            .ToArray();

        return new ItemSetDto(index, rows);
    }
}
