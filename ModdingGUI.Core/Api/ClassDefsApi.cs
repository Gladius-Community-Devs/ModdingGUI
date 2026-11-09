namespace ModdingGUI.Core.Api;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ModdingGUI.Core.Classes;
using ModdingGUI.Core.Tok;

/// <summary>
/// Default <see cref="IClassesApi"/>: raw line manipulation over <c>classdefs.tok</c> so
/// comments, indentation and unknown keys survive an edit untouched.
/// </summary>
public sealed class ClassDefsApi : IClassesApi
{
    /// <summary>Canonical relative path inside a project's BEC tree.</summary>
    public const string RelativePath = "data/config/classdefs.tok";

    private const string DisplayName = "classdefs.tok";

    // CREATECLASS: Amazon      (names are bare words, not quoted)
    private static readonly Regex CreateRx = new(
        @"^\s*CREATECLASS:\s*(\S+)\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // INDENT KEY: value        — key must start with a letter and contain no spaces
    private static readonly Regex KeyRx = new(
        @"^(?<indent>\s*)(?<key>[A-Za-z][A-Za-z0-9_]*)\s*:(?<rest>.*)$",
        RegexOptions.Compiled);

    private static readonly Regex CommentRx = new(
        @"^\s*//", RegexOptions.Compiled);

    // ---- IClassesApi --------------------------------------------------------

    /// <inheritdoc />
    public IReadOnlyList<string> List(string projectPath)
    {
        using (ProjectLock.Acquire(projectPath))
        {
            var (lines, _) = Read(projectPath);
            return ExtractNames(lines);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<ClassDefDto> GetAll(string projectPath)
    {
        using (ProjectLock.Acquire(projectPath))
        {
            var (lines, _) = Read(projectPath);
            return AllBlocks(lines).Select(b => ParseBlock(lines, b.Start, b.End)).ToArray();
        }
    }

    /// <inheritdoc />
    public ClassDefDto? Get(string projectPath, string className)
    {
        using (ProjectLock.Acquire(projectPath))
        {
            var (lines, _) = Read(projectPath);
            var block = FindBlock(lines, className);
            return block is null ? null : ParseBlock(lines, block.Value.Start, block.Value.End);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<ClassValidationIssue> Validate(string projectPath, string className)
    {
        var classDef = Get(projectPath, className)
            ?? throw new InvalidOperationException($"class '{className}' not found.");

        return ClassDefsValidator.Validate(classDef, ClassDefsCatalog.Shipped());
    }

    /// <inheritdoc />
    public void SetField(string projectPath, string className, string key, string rawValue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(rawValue);

        using (ProjectLock.Acquire(projectPath))
        {
            var (lines, trailing) = Read(projectPath);
            var block = RequireBlock(lines, className);

            var matches = KeyLines(lines, block, key);
            if (matches.Count == 0)
                throw new InvalidOperationException($"class '{className}' has no '{key}' line; use AddField.");

            int first = matches[0];
            // Keep the original indentation, replace everything from the colon on.
            var match = KeyRx.Match(lines[first]);
            string indent = match.Groups["indent"].Value;
            lines[first] = $"{indent}{match.Groups["key"].Value}: {rawValue}";

            // Further lines for a single-valued key would silently overwrite: drop them.
            for (int i = matches.Count - 1; i >= 1; i--) lines.RemoveAt(matches[i]);

            RawLineFile.Write(projectPath, RelativePath, lines, trailing);
        }
    }

    /// <inheritdoc />
    public void AddField(string projectPath, string className, string key, string rawValue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(rawValue);

        using (ProjectLock.Acquire(projectPath))
        {
            var (lines, trailing) = Read(projectPath);
            var block = RequireBlock(lines, className);

            int insertAt = block.End;
            // Sit above the blank separator lines that end the block.
            while (insertAt > block.Start + 1 && string.IsNullOrWhiteSpace(lines[insertAt - 1])) insertAt--;

            lines.Insert(insertAt, $"\t{key}: {rawValue}");
            RawLineFile.Write(projectPath, RelativePath, lines, trailing);
        }
    }

    /// <inheritdoc />
    public void RemoveField(string projectPath, string className, string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        using (ProjectLock.Acquire(projectPath))
        {
            var (lines, trailing) = Read(projectPath);
            var block = RequireBlock(lines, className);

            var matches = KeyLines(lines, block, key);
            if (matches.Count == 0) return;   // nothing to do; leave the file untouched

            for (int i = matches.Count - 1; i >= 0; i--) lines.RemoveAt(matches[i]);
            RawLineFile.Write(projectPath, RelativePath, lines, trailing);
        }
    }

    /// <inheritdoc />
    public void Rename(string projectPath, string className, string newName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newName);
        if (newName.Contains(' ')) throw new ArgumentException("class names cannot contain spaces.", nameof(newName));

        using (ProjectLock.Acquire(projectPath))
        {
            var (lines, trailing) = Read(projectPath);
            var block = RequireBlock(lines, className);

            if (FindBlock(lines, newName) is not null)
                throw new InvalidOperationException($"class '{newName}' already exists.");

            var header = CreateRx.Match(lines[block.Start]);
            if (!header.Success)
                throw new InvalidOperationException($"malformed CREATECLASS line for '{className}'.");

            lines[block.Start] = $"CREATECLASS: {newName}";

            RawLineFile.Write(projectPath, RelativePath, lines, trailing);
        }
    }

    /// <inheritdoc />
    public void Add(string projectPath, string className, IReadOnlyList<ClassFieldDto> fields)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(className);
        if (className.Contains(' ')) throw new ArgumentException("class names cannot contain spaces.", nameof(className));
        ArgumentNullException.ThrowIfNull(fields);

        using (ProjectLock.Acquire(projectPath))
        {
            var (lines, trailing) = Read(projectPath);
            if (FindBlock(lines, className) is not null)
                throw new InvalidOperationException($"class '{className}' already exists.");

            while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[^1])) lines.RemoveAt(lines.Count - 1);

            lines.Add(string.Empty);
            lines.Add($"CREATECLASS: {className}");
            lines.Add(string.Empty);
            foreach (var field in fields)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(field.Key);
                lines.Add($"\t{field.Key}: {field.RawValue}");
            }

            // Keep the file's own trailing-newline count so add+remove is a no-op.
            RawLineFile.Write(projectPath, RelativePath, lines, trailing);
        }
    }

    /// <inheritdoc />
    public void Remove(string projectPath, string className)
    {
        using (ProjectLock.Acquire(projectPath))
        {
            var (lines, trailing) = Read(projectPath);
            var block = RequireBlock(lines, className);

            int end = block.End;
            // Drop the separator blank lines that followed the block.
            while (end < lines.Count && string.IsNullOrWhiteSpace(lines[end])) end++;

            lines.RemoveRange(block.Start, end - block.Start);

            // Removing the last block leaves its leading separator behind; the file's
            // own trailing newline count then supplies the blank line, so trim it to
            // keep add+remove byte-identical.
            while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[^1])) lines.RemoveAt(lines.Count - 1);

            RawLineFile.Write(projectPath, RelativePath, lines, trailing);
        }
    }

    // ---- internals ----------------------------------------------------------

    private static (List<string> Lines, int TrailingNewlines) Read(string projectPath) =>
        File.Exists(RawLineFile.GetPath(projectPath, RelativePath))
            ? RawLineFile.Read(projectPath, RelativePath, DisplayName)
            : throw new FileNotFoundException($"classdefs.tok not found for '{projectPath}'.",
                RawLineFile.GetPath(projectPath, RelativePath));

    private static List<string> ExtractNames(List<string> lines)
    {
        var names = new List<string>();
        foreach (string line in lines)
        {
            var match = CreateRx.Match(line);
            if (match.Success) names.Add(match.Groups[1].Value);
        }
        return names;
    }

    internal static List<(int Start, int End)> AllBlocks(List<string> lines)
    {
        var starts = new List<int>();
        for (int i = 0; i < lines.Count; i++)
        {
            if (CreateRx.IsMatch(lines[i])) starts.Add(i);
        }

        var blocks = new List<(int, int)>(starts.Count);
        for (int i = 0; i < starts.Count; i++)
        {
            int end = i + 1 < starts.Count ? starts[i + 1] : lines.Count;
            blocks.Add((starts[i], end));
        }
        return blocks;
    }

    private static (int Start, int End)? FindBlock(List<string> lines, string className)
    {
        foreach (var block in AllBlocks(lines))
        {
            var match = CreateRx.Match(lines[block.Start]);
            if (match.Success && string.Equals(match.Groups[1].Value, className, StringComparison.Ordinal))
                return block;
        }
        return null;
    }

    private static (int Start, int End) RequireBlock(List<string> lines, string className) =>
        FindBlock(lines, className)
        ?? throw new InvalidOperationException($"class '{className}' not found.");

    /// <summary>Line indices (absolute) of every field line for <paramref name="key"/> in a block.</summary>
    private static List<int> KeyLines(List<string> lines, (int Start, int End) block, string key)
    {
        var found = new List<int>();
        for (int i = block.Start + 1; i < block.End; i++)
        {
            string line = lines[i];
            if (CommentRx.IsMatch(line)) continue;

            var match = KeyRx.Match(line);
            if (!match.Success) continue;
            if (string.Equals(match.Groups["key"].Value, key, StringComparison.OrdinalIgnoreCase))
                found.Add(i);
        }
        return found;
    }

    internal static ClassDefDto ParseBlock(List<string> lines, int start, int end)
    {
        var fields = new List<ClassFieldDto>();
        for (int i = start + 1; i < end; i++)
        {
            string line = lines[i];
            if (CommentRx.IsMatch(line)) continue;

            var match = KeyRx.Match(line);
            if (!match.Success) continue;

            fields.Add(new ClassFieldDto(match.Groups["key"].Value, StripInlineComment(match.Groups["rest"].Value)));
        }

        var header = CreateRx.Match(lines[start]);
        return new ClassDefDto
        {
            Name = header.Success ? header.Groups[1].Value : string.Empty,
            Fields = fields,
        };
    }

    /// <summary>Trim trailing whitespace and a trailing <c>// comment</c> from a raw value.</summary>
    private static string StripInlineComment(string raw)
    {
        int comment = raw.IndexOf("//", StringComparison.Ordinal);
        if (comment >= 0) raw = raw.Substring(0, comment);
        return raw.Trim();
    }
}
