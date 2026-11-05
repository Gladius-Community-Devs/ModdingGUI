using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace ModdingGUI.Core.Units;

/// <summary>A colour in a tint row.</summary>
public readonly record struct Rgb(byte R, byte G, byte B)
{
    /// <summary>Render as the file writes it: space-separated decimal channels.</summary>
    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{R} {G} {B}");
}

/// <summary>One tint row: a channel name and its colour, e.g. <c>Cloth1: 255 255 54</c>.</summary>
public sealed record TintRow(string Channel, Rgb Color)
{
    /// <summary>Render the line the file uses.</summary>
    public string ToLine() => $"{Channel}: {Color}";

    /// <summary>Parse a colour row; false when it is not <c>Name: r g b</c> with 0-255 channels.</summary>
    public static bool TryParse(string line, out TintRow row)
    {
        row = new TintRow(string.Empty, default);

        int colon = line.IndexOf(':');
        if (colon <= 0) return false;

        string channel = line[..colon].Trim();
        if (channel.Length == 0) return false;

        string[] parts = line[(colon + 1)..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3) return false;

        var channels = new byte[3];
        for (int i = 0; i < 3; i++)
        {
            if (!byte.TryParse(parts[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out channels[i]))
                return false;
        }

        row = new TintRow(channel, new Rgb(channels[0], channels[1], channels[2]));
        return true;
    }
}

/// <summary>The colour channels a tint set can carry.</summary>
public static class TintChannels
{
    /// <summary>Every channel vanilla uses, in file order.</summary>
    public static readonly string[] All = { "Cloth1", "Cloth2", "Armor1", "Armor2", "Skin", "Hair" };

    /// <summary>True when the name is one of the six known channels (case-insensitive).</summary>
    public static bool IsKnown(string? channel) =>
        channel is not null && All.Contains(channel.Trim(), StringComparer.OrdinalIgnoreCase);

    /// <summary>Canonical spelling, or the input unchanged when unknown.</summary>
    public static string Normalize(string channel)
    {
        foreach (string known in All)
        {
            if (string.Equals(known, channel.Trim(), StringComparison.OrdinalIgnoreCase)) return known;
        }
        return channel;
    }
}

/// <summary>One tint set: its index and the colours it assigns.</summary>
public sealed record TintSetDto(int Index, IReadOnlyDictionary<string, Rgb> Colors)
{
    /// <summary>Colour for one channel, or null when the set does not set it.</summary>
    public Rgb? this[string channel] =>
        Colors.TryGetValue(channel, out Rgb color) ? color : null;
}

/// <summary>
/// Edit <c>data/units/tints.txt</c>: 196 sets of six colour rows (<c>Cloth1/2</c>, <c>Armor1/2</c>,
/// <c>Skin</c>, <c>Hair</c>), which a gladiator's <c>Tint set</c> index selects.
/// </summary>
/// <remarks>
/// Tint set 0 holds a paragraph of prose instead of colours ("This tint set is blank; … needs more
/// research") because vanilla uses it as a comment. It is treated as a set with no rows and its text is
/// never touched, so a no-op save keeps it exactly as it is.
/// </remarks>
public interface ITintSetsApi
{
    /// <summary>Every set index, in file order.</summary>
    IReadOnlyList<int> List(string projectRoot);

    /// <summary>One set, or null when the index does not exist.</summary>
    TintSetDto? Get(string projectRoot, int index);

    /// <summary>Set one channel's colour, rewriting that row (or adding it when missing).</summary>
    void SetColor(string projectRoot, int index, string channel, Rgb color);

    /// <summary>Append a new, empty set and return its index.</summary>
    int AddSet(string projectRoot);

    /// <summary>Remove a set.</summary>
    bool RemoveSet(string projectRoot, int index);

    /// <summary>Advisory checks for one set or all of them.</summary>
    IReadOnlyList<SetValidationIssue> Validate(string projectRoot, int? index = null);
}

/// <summary>Default <see cref="ITintSetsApi"/>.</summary>
public sealed class TintSetsApi : ITintSetsApi
{
    internal const string RelativePath = "data/units/tints.txt";
    private const string SectionWord = "Tint set";

    /// <inheritdoc />
    public IReadOnlyList<int> List(string projectRoot) => Load(projectRoot).Indexes;

    /// <inheritdoc />
    public TintSetDto? Get(string projectRoot, int index)
    {
        var file = Load(projectRoot);
        return ToDto(file, index);
    }

    /// <inheritdoc />
    public void SetColor(string projectRoot, int index, string channel, Rgb color)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channel);
        if (!TintChannels.IsKnown(channel))
            throw new ArgumentException($"'{channel}' is not a tint channel.", nameof(channel));

        string canonical = TintChannels.Normalize(channel);
        string bec = ProjectRoots.ResolveBecFolder(projectRoot);

        using (ProjectLock.Acquire(bec))
        {
            var file = Load(bec);
            if (!file.Indexes.Contains(index)) throw new InvalidOperationException($"No tint set {index}.");

            var rows = file.Rows(index).ToList();
            for (int ordinal = 0; ordinal < rows.Count; ordinal++)
            {
                if (!TintRow.TryParse(rows[ordinal], out TintRow row)) continue;
                if (!string.Equals(row.Channel, canonical, StringComparison.OrdinalIgnoreCase)) continue;
                if (row.Color == color) return;

                file.ReplaceRow(index, ordinal, new TintRow(canonical, color).ToLine());
                file.Save(bec, RelativePath);
                return;
            }

            // Channel not present in this set yet: add it after the last colour row.
            int after = -1;
            for (int ordinal = 0; ordinal < rows.Count; ordinal++)
            {
                if (TintRow.TryParse(rows[ordinal], out _)) after = ordinal;
            }

            file.InsertRow(index, after, new TintRow(canonical, color).ToLine());
            file.Save(bec, RelativePath);
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

        foreach (int setIndex in file.Indexes)
        {
            if (index is not null && setIndex != index) continue;

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var prose = new List<string>();

            foreach (string line in file.Rows(setIndex))
            {
                if (TintRow.TryParse(line, out TintRow row))
                {
                    if (!TintChannels.IsKnown(row.Channel))
                    {
                        issues.Add(new SetValidationIssue(SetValidationSeverity.Warning,
                            SetValidationCodes.UnknownName, setIndex,
                            $"'{row.Channel}' is not one of {string.Join(", ", TintChannels.All)}"));
                    }

                    if (!seen.Add(row.Channel))
                    {
                        issues.Add(new SetValidationIssue(SetValidationSeverity.Info,
                            SetValidationCodes.DuplicateEntry, setIndex,
                            $"channel '{row.Channel}' is set more than once"));
                    }

                    continue;
                }

                prose.Add(line);
            }

            if (prose.Count > 0)
            {
                issues.Add(new SetValidationIssue(SetValidationSeverity.Info,
                    SetValidationCodes.MalformedRow, setIndex,
                    $"set {setIndex} contains {prose.Count} line(s) that are not colour rows " +
                    $"(vanilla set 0 is a note): '{prose[0]}'"));
            }

            foreach (string channel in TintChannels.All)
            {
                if (seen.Contains(channel)) continue;
                issues.Add(new SetValidationIssue(SetValidationSeverity.Info,
                    SetValidationCodes.IncompleteCoverage, setIndex,
                    $"set {setIndex} does not set {channel}"));
            }
        }

        return issues;
    }

    private static SectionedFile Load(string projectRootOrBec)
    {
        string bec = ProjectRoots.ResolveBecFolder(projectRootOrBec);
        return SectionedFile.Load(
            Path.Combine(bec, RelativePath.Replace('/', Path.DirectorySeparatorChar)), SectionWord);
    }

    private static TintSetDto? ToDto(SectionedFile file, int index)
    {
        if (!file.Indexes.Contains(index)) return null;

        var colors = new Dictionary<string, Rgb>(StringComparer.OrdinalIgnoreCase);
        foreach (string line in file.Rows(index))
        {
            if (TintRow.TryParse(line, out TintRow row)) colors[row.Channel] = row.Color;
        }

        return new TintSetDto(index, colors);
    }
}
