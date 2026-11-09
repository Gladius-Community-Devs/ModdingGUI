namespace ModdingGUI.Core.Api;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ModdingGUI.Core.Tok;

/// <summary>One entry of a flat tok table, with the value as written.</summary>
/// <param name="Key">Key as written (upper-case in the corpus).</param>
/// <param name="RawValue">Everything after the key/separator, trimmed.</param>
public sealed record ConfigEntryDto(string Key, string RawValue);

/// <summary>A flat <c>KEY value</c> tok file (shops, world tables, font definitions, …).</summary>
public sealed class ConfigTableDto
{
    /// <summary>Project-relative path.</summary>
    public string FilePath { get; init; } = string.Empty;

    /// <summary>Entries in file order; keys may repeat (e.g. a shop's item lines).</summary>
    public IReadOnlyList<ConfigEntryDto> Entries { get; init; } = Array.Empty<ConfigEntryDto>();

    /// <summary>All values for a key, in file order.</summary>
    public IReadOnlyList<string> Values(string key) =>
        Entries.Where(e => string.Equals(e.Key, key, StringComparison.OrdinalIgnoreCase))
               .Select(e => e.RawValue)
               .ToArray();

    /// <summary>The first value for a key, or null.</summary>
    public string? Value(string key) =>
        Entries.FirstOrDefault(e => string.Equals(e.Key, key, StringComparison.OrdinalIgnoreCase))?.RawValue;

    /// <summary>How many times a key appears.</summary>
    public int Count(string key) =>
        Entries.Count(e => string.Equals(e.Key, key, StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// Read and edit flat tok tables that are "just a pile of <c>KEY value</c> lines": the shops in
/// <c>data/towns/shops</c>, the world/map/font tables in <c>data/config</c>, and the
/// space-separated <c>.tnt</c> tint files.
/// </summary>
/// <remarks>
/// This is deliberately one generic API rather than a class per file: those formats have no
/// cross-field rules and no positional records, so a typed schema would add ceremony without
/// catching anything. Callers pass the project-relative path they want.
/// </remarks>
public interface IConfigTableApi
{
    /// <summary>Relative paths of matching files under a directory, sorted.</summary>
    IReadOnlyList<string> ListFiles(string projectPath, string relativeDirectory, string searchPattern = "*.tok");

    /// <summary>Read a table; null when the file does not exist.</summary>
    ConfigTableDto? Get(string projectPath, string relativePath);

    /// <summary>Replace the first value of a key (further lines for it are dropped).</summary>
    void Set(string projectPath, string relativePath, string key, string rawValue);

    /// <summary>Append a new line for a key (for genuinely repeating keys).</summary>
    void Add(string projectPath, string relativePath, string key, string rawValue);

    /// <summary>Remove every line for a key.</summary>
    void Remove(string projectPath, string relativePath, string key);

    /// <summary>Replace all lines for a key with the supplied values, in order.</summary>
    void SetValues(string projectPath, string relativePath, string key, IReadOnlyList<string> rawValues);
}

/// <summary>Default <see cref="IConfigTableApi"/>: raw line editing with lock and backup.</summary>
public sealed class ConfigTableApi : IConfigTableApi
{
    /// <inheritdoc />
    public IReadOnlyList<string> ListFiles(string projectPath, string relativeDirectory, string searchPattern = "*.tok")
    {
        string bec = ProjectRoots.ResolveBecFolder(projectPath);
        string root = Path.Combine(bec, relativeDirectory.Replace('/', Path.DirectorySeparatorChar));
        if (!Directory.Exists(root)) return Array.Empty<string>();

        return Directory.EnumerateFiles(root, searchPattern, SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(bec, path).Replace(Path.DirectorySeparatorChar, '/'))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <inheritdoc />
    public ConfigTableDto? Get(string projectPath, string relativePath)
    {
        string bec = ProjectRoots.ResolveBecFolder(projectPath);
        string path = FullPath(bec, relativePath);
        if (!File.Exists(path)) return null;

        var (lines, _) = RawLineFile.Read(bec, relativePath, Path.GetFileName(relativePath));
        var entries = new List<ConfigEntryDto>();
        foreach (string line in lines)
        {
            if (TokLineEditing.CommentRx.IsMatch(line)) continue;
            if (TokLineEditing.TryKey(line, out string key, out _, out string value))
                entries.Add(new ConfigEntryDto(key, value));
        }

        return new ConfigTableDto { FilePath = relativePath, Entries = entries };
    }

    /// <inheritdoc />
    public void Set(string projectPath, string relativePath, string key, string rawValue) =>
        Edit(projectPath, relativePath, (lines, _) =>
        {
            var matches = TokLineEditing.KeyLines(lines, 0, lines.Count, key);
            if (matches.Count == 0)
                throw new InvalidOperationException($"'{key}' not found in {relativePath}; use Add.");

            TokLineEditing.ReplaceValue(lines, matches[0], rawValue);
            for (int i = matches.Count - 1; i >= 1; i--) lines.RemoveAt(matches[i]);
            return true;
        });

    /// <inheritdoc />
    public void Add(string projectPath, string relativePath, string key, string rawValue) =>
        Edit(projectPath, relativePath, (lines, _) =>
        {
            int at = lines.Count;
            while (at > 0 && string.IsNullOrWhiteSpace(lines[at - 1])) at--;
            TokLineEditing.Insert(lines, at, key, rawValue, "\t");
            return true;
        });

    /// <inheritdoc />
    public void Remove(string projectPath, string relativePath, string key) =>
        Edit(projectPath, relativePath, (lines, _) =>
            TokLineEditing.RemoveKeys(lines, 0, lines.Count, key) > 0);

    /// <inheritdoc />
    public void SetValues(string projectPath, string relativePath, string key, IReadOnlyList<string> rawValues)
    {
        ArgumentNullException.ThrowIfNull(rawValues);

        Edit(projectPath, relativePath, (lines, _) =>
        {
            var matches = TokLineEditing.KeyLines(lines, 0, lines.Count, key);
            string indent = matches.Count > 0 ? TokLineEditing.IndentOf(lines[matches[0]]) : "\t";
            int insertAt = matches.Count > 0 ? matches[0] : lines.Count;

            for (int i = matches.Count - 1; i >= 0; i--) lines.RemoveAt(matches[i]);

            var replacement = rawValues
                .Select(v => $"{indent}{key}: {v.Trim()}")
                .ToList();
            lines.InsertRange(Math.Clamp(insertAt, 0, lines.Count), replacement);
            return true;
        });
    }

    private static string FullPath(string bec, string relativePath) =>
        Path.Combine(bec, relativePath.Replace('/', Path.DirectorySeparatorChar));

    private static void Edit(string projectPath, string relativePath, Func<List<string>, int, bool> mutate)
    {
        string bec = ProjectRoots.ResolveBecFolder(projectPath);
        using (ProjectLock.Acquire(bec))
        {
            string path = FullPath(bec, relativePath);
            if (!File.Exists(path))
        {
            throw new FileNotFoundException($"tok table '{Path.GetFileName(path)}' not found for '{projectPath}'.", path);
        }

            var (lines, trailing) = RawLineFile.Read(bec, relativePath, Path.GetFileName(relativePath));
            if (!mutate(lines, trailing)) return;
            RawLineFile.Write(bec, relativePath, lines, trailing);
        }
    }
}
