namespace ModdingGUI.Core.Api;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ModdingGUI.Core.Tok;

/// <summary>
/// Default <see cref="IEncountersApi"/>: raw line editing over <c>.enc</c> files so comments,
/// indentation and unknown keys survive untouched.
/// </summary>
public sealed class EncountersApi : IEncountersApi
{
    private const string EncountersRoot = "data/encounters";
    private const string FileFilter = "*.enc";

    private static readonly Regex TeamRx = new(
        @"^(?<prefix>\s*TEAM\s*:\s*)(?<index>[^,]*?)\s*,\s*(?<rest>.*)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // tok spells most keys "KEY: value", but the corpus also has colon-less keys such as
    // "PRIZETIER <tab> \"DrakeDefaultCash\" 0". Keys are upper-case, which keeps prose out.
    private static readonly Regex KeyRx = new(
        @"^(?<prefix>\s*(?<key>[A-Z][A-Z0-9_]*)(?:\s*:\s*|\s+))(?<value>.*)$",
        RegexOptions.Compiled);

    private static readonly Regex CommentRx = new(@"^\s*//", RegexOptions.Compiled);

    // ---- IEncountersApi -----------------------------------------------------

    /// <inheritdoc />
    public IReadOnlyList<string> ListFiles(string projectPath) => ListFilesCore(projectPath);

    /// <inheritdoc />
    public IReadOnlyList<string> List(string projectPath)
    {
        string bec = ProjectRoots.ResolveBecFolder(projectPath);
        using (ProjectLock.Acquire(bec))
        {
            var names = new List<string>();
            foreach (string relative in ListFilesCore(bec))
            {
                var encounter = ReadEncounter(bec, relative);
                if (encounter is not null) names.Add(encounter.Name);
            }
            return names;
        }
    }

    /// <inheritdoc />
    public EncounterDto? Get(string projectPath, string relativePath)
    {
        string bec = ProjectRoots.ResolveBecFolder(projectPath);
        using (ProjectLock.Acquire(bec))
        {
            return ReadEncounter(bec, relativePath);
        }
    }

    /// <inheritdoc />
    public void ReplaceWith(
        string projectPath,
        string sourceRelativePath,
        string destinationRelativePath,
        string? encounterName = null,
        bool stripCutscene = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceRelativePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationRelativePath);

        string bec = ProjectRoots.ResolveBecFolder(projectPath);
        using (ProjectLock.Acquire(bec))
        {
            string sourcePath = FullPath(bec, sourceRelativePath);
            if (!File.Exists(sourcePath))
                throw new FileNotFoundException("source encounter not found.", sourcePath);

            var (sourceLines, _) = RawLineFile.Read(bec, sourceRelativePath, Path.GetFileName(sourceRelativePath));

            // Keep the destination's own trailing-newline count so repeated runs are stable.
            int trailing = 2;
            string destinationPath = FullPath(bec, destinationRelativePath);
            if (File.Exists(destinationPath))
            {
                var (_, existingTrailing) = RawLineFile.Read(bec, destinationRelativePath,
                    Path.GetFileName(destinationRelativePath));
                trailing = existingTrailing;
            }

            var lines = new List<string>(sourceLines);

            if (stripCutscene)
                lines.RemoveAll(line => TokLineEditing.TryKey(line, out string key, out _, out _)
                                        && string.Equals(key, "CUTSCENE", StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(encounterName))
            {
                int nameLine = TokLineEditing.FirstKeyLine(lines, 0, lines.Count, "ENCOUNTERNAME");
                if (nameLine >= 0)
                    TokLineEditing.ReplaceValue(lines, nameLine, $"\"{encounterName.Trim().Trim('"')}\"");
                else
                    TokLineEditing.Insert(lines, 0, "ENCOUNTERNAME", $"\"{encounterName}\"");
            }

            ChangeHistoryService.Backup(bec, destinationPath);
            RawLineFile.Write(bec, destinationRelativePath, lines, trailing);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<EncounterValidationIssue> Validate(string projectPath, string relativePath)
    {
        var encounter = Get(projectPath, relativePath)
            ?? throw new FileNotFoundException($"encounter not found: {relativePath}");

        string bec = ProjectRoots.ResolveBecFolder(projectPath);

        return EncounterValidator.Validate(encounter, EncountersCatalog.Shipped(),
            gridPath => LoadGrid(bec, gridPath));
    }

    /// <summary>Load a team grid from a <c>GRIDFILE</c> value, or null when it does not resolve.</summary>
    private static Grids.GridFile? LoadGrid(string bec, string gridPath)
    {
        string? full = GamePaths.ResolveInProject(bec, gridPath);
        if (full is null || !File.Exists(full)) return null;

        try
        {
            return Grids.GridFile.Load(full);
        }
        catch (IOException)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public void SetField(string projectPath, string relativePath, string key, string rawValue) =>
        EditHeader(projectPath, relativePath, key, rawValue, addWhenMissing: false);

    /// <inheritdoc />
    public void AddField(string projectPath, string relativePath, string key, string rawValue) =>
        EditHeader(projectPath, relativePath, key, rawValue, addWhenMissing: true);

    /// <inheritdoc />
    public void RemoveField(string projectPath, string relativePath, string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        Edit(projectPath, relativePath, (lines, trailing) =>
        {
            var matches = HeaderKeyLines(lines, key);
            for (int i = matches.Count - 1; i >= 0; i--) lines.RemoveAt(matches[i]);
            return matches.Count > 0;
        });
    }

    /// <inheritdoc />
    public void SetTeamField(string projectPath, string relativePath, int teamIndex, string key, string rawValue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(rawValue);

        Edit(projectPath, relativePath, (lines, trailing) =>
        {
            var team = RequireTeam(lines, teamIndex);
            var matches = KeyLinesIn(lines, team.Start + 1, team.End, key, excluded: "UNITDB");
            if (matches.Count == 0)
                throw new InvalidOperationException($"team {teamIndex} has no '{key}' line.");

            ReplaceValue(lines, matches[0], rawValue);
            for (int i = matches.Count - 1; i >= 1; i--) lines.RemoveAt(matches[i]);
            return true;
        });
    }

    /// <inheritdoc />
    public void RemoveTeamField(string projectPath, string relativePath, int teamIndex, string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        Edit(projectPath, relativePath, (lines, trailing) =>
        {
            var team = RequireTeam(lines, teamIndex);
            var matches = KeyLinesIn(lines, team.Start + 1, team.End, key, excluded: "UNITDB");
            for (int i = matches.Count - 1; i >= 0; i--) lines.RemoveAt(matches[i]);
            return matches.Count > 0;
        });
    }

    /// <inheritdoc />
    public void SetTeamUnits(string projectPath, string relativePath, int teamIndex, IReadOnlyList<string> unitRawValues)
    {
        ArgumentNullException.ThrowIfNull(unitRawValues);

        Edit(projectPath, relativePath, (lines, trailing) =>
        {
            var team = RequireTeam(lines, teamIndex);

            // Indentation of the existing rows, or one tab when the team has none.
            var existing = KeyLinesIn(lines, team.Start + 1, team.End, "UNITDB");
            string indent = existing.Count > 0
                ? new string(lines[existing[0]].TakeWhile(char.IsWhiteSpace).ToArray())
                : "\t";
            string prefix = indent + "UNITDB: \t";

            // Drop the old rows and insert the new ones where the first used to be.
            int insertAt = existing.Count > 0 ? existing[0] : team.End;
            for (int i = existing.Count - 1; i >= 0; i--) lines.RemoveAt(existing[i]);

            var replacement = unitRawValues.Select(v => prefix + v.Trim()).ToList();
            lines.InsertRange(insertAt, replacement);
            return true;
        });
    }

    /// <inheritdoc />
    public void SetTeamUnit(
        string projectPath, string relativePath, int teamIndex, int unitIndex, EncounterUnit unit)
    {
        ArgumentNullException.ThrowIfNull(unit);

        Edit(projectPath, relativePath, (lines, trailing) =>
        {
            var team = RequireTeam(lines, teamIndex);
            var rows = KeyLinesIn(lines, team.Start + 1, team.End, "UNITDB");
            if (unitIndex < 0 || unitIndex >= rows.Count)
                throw new InvalidOperationException($"team {teamIndex} has no unit {unitIndex}.");

            int index = rows[unitIndex];
            string indent = new string(lines[index].TakeWhile(char.IsWhiteSpace).ToArray());
            lines[index] = $"{indent}UNITDB: \t{unit.RebuildRaw()}";
            return true;
        });
    }

    /// <inheritdoc />
    public void AddTeamUnit(
        string projectPath, string relativePath, int teamIndex, EncounterUnit unit)
    {
        ArgumentNullException.ThrowIfNull(unit);

        Edit(projectPath, relativePath, (lines, trailing) =>
        {
            var team = RequireTeam(lines, teamIndex);
            var rows = KeyLinesIn(lines, team.Start + 1, team.End, "UNITDB");

            int at = rows.Count > 0 ? rows[^1] + 1 : team.End;
            string indent = rows.Count > 0
                ? new string(lines[rows[0]].TakeWhile(char.IsWhiteSpace).ToArray())
                : "\t";
            lines.Insert(at, $"{indent}UNITDB: \t{unit.RebuildRaw()}");
            return true;
        });
    }

    /// <inheritdoc />
    public void RemoveTeamUnit(
        string projectPath, string relativePath, int teamIndex, int unitIndex)
    {
        Edit(projectPath, relativePath, (lines, trailing) =>
        {
            var team = RequireTeam(lines, teamIndex);
            var rows = KeyLinesIn(lines, team.Start + 1, team.End, "UNITDB");
            if (unitIndex < 0 || unitIndex >= rows.Count) return false;

            lines.RemoveAt(rows[unitIndex]);
            return true;
        });
    }

    // ---- editing plumbing ---------------------------------------------------

    private void EditHeader(string projectPath, string relativePath, string key, string rawValue, bool addWhenMissing)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(rawValue);

        Edit(projectPath, relativePath, (lines, trailing) =>
        {
            int headerEnd = FirstTeamLine(lines);
            var matches = KeyLinesIn(lines, 0, headerEnd, key);

            if (matches.Count > 0)
            {
                ReplaceValue(lines, matches[0], rawValue);
                for (int i = matches.Count - 1; i >= 1; i--) lines.RemoveAt(matches[i]);
                return true;
            }

            if (!addWhenMissing)
                throw new InvalidOperationException($"encounter has no header '{key}' line; use AddField.");

            int insertAt = headerEnd;
            while (insertAt > 0 && string.IsNullOrWhiteSpace(lines[insertAt - 1])) insertAt--;
            lines.Insert(insertAt, $"{key}: \t{rawValue}");
            return true;
        });
    }

    private void Edit(string projectPath, string relativePath, Func<List<string>, int, bool> mutate)
    {
        string bec = ProjectRoots.ResolveBecFolder(projectPath);
        using (ProjectLock.Acquire(bec))
        {
            string path = FullPath(bec, relativePath);
            if (!File.Exists(path))
                throw new FileNotFoundException("encounter not found.", path);

            var (lines, trailing) = RawLineFile.Read(bec, relativePath, Path.GetFileName(relativePath));
            if (!mutate(lines, trailing)) return;   // nothing changed: leave the file alone
            RawLineFile.Write(bec, relativePath, lines, trailing);
        }
    }

    /// <summary>Replace only the value portion so the line keeps its original layout.</summary>
    private static void ReplaceValue(List<string> lines, int index, string rawValue)
    {
        var match = KeyRx.Match(lines[index]);
        if (!match.Success)
            throw new InvalidOperationException($"line {index + 1} is not a key/value line.");
        lines[index] = match.Groups["prefix"].Value + rawValue;
    }

    private static List<int> HeaderKeyLines(List<string> lines, string key)
    {
        int headerEnd = FirstTeamLine(lines);
        return KeyLinesIn(lines, 0, headerEnd, key, excluded: "UNITDB");
    }

    private static List<int> KeyLinesIn(
        List<string> lines, int start, int end, string key, string? excluded = null)
    {
        var found = new List<int>();
        for (int i = start; i < end; i++)
        {
            string line = lines[i];
            if (CommentRx.IsMatch(line)) continue;
            if (TeamRx.IsMatch(line)) continue;

            var match = KeyRx.Match(line);
            if (!match.Success) continue;

            string candidate = match.Groups["key"].Value;
            if (string.Equals(candidate, excluded, StringComparison.OrdinalIgnoreCase)) continue;
            if (string.Equals(candidate, key, StringComparison.OrdinalIgnoreCase)) found.Add(i);
        }
        return found;
    }

    private static int FirstTeamLine(List<string> lines)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            if (TeamRx.IsMatch(lines[i])) return i;
        }
        return lines.Count;
    }

    private static (int Start, int End)? FindTeam(List<string> lines, int teamIndex)
    {
        var teams = new List<(int Start, int End, int Index)>();
        for (int i = 0; i < lines.Count; i++)
        {
            var match = TeamRx.Match(lines[i]);
            if (!match.Success) continue;
            teams.Add((i, lines.Count,
                int.TryParse(match.Groups["index"].Value.Trim(), out int n) ? n : int.MinValue));
        }

        for (int i = 0; i < teams.Count; i++)
        {
            var current = teams[i];
            int end = i + 1 < teams.Count ? teams[i + 1].Start : lines.Count;
            if (current.Index == teamIndex) return (current.Start, end);
        }
        return null;
    }

    private static (int Start, int End) RequireTeam(List<string> lines, int teamIndex) =>
        FindTeam(lines, teamIndex) ?? throw new InvalidOperationException($"team {teamIndex} not found.");

    // ---- reading ------------------------------------------------------------

    private static string FullPath(string bec, string relativePath) =>
        Path.Combine(bec, relativePath.Replace('/', Path.DirectorySeparatorChar));

    private static IReadOnlyList<string> ListFilesCore(string projectPath)
    {
        string bec = ProjectRoots.ResolveBecFolder(projectPath);
        string root = Path.Combine(bec, EncountersRoot.Replace('/', Path.DirectorySeparatorChar));
        if (!Directory.Exists(root)) return Array.Empty<string>();

        return Directory.EnumerateFiles(root, FileFilter, SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(bec, path).Replace(Path.DirectorySeparatorChar, '/'))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static EncounterDto? ReadEncounter(string bec, string relativePath)
    {
        string path = FullPath(bec, relativePath);
        if (!File.Exists(path)) return null;

        var (lines, _) = RawLineFile.Read(bec, relativePath, Path.GetFileName(relativePath));

        int firstTeam = FirstTeamLine(lines);
        var headerFields = ParseFields(lines, 0, firstTeam);

        var teams = new List<EncounterTeamDto>();
        var teamStarts = new List<(int Start, int End, int Index)>();
        for (int i = 0; i < lines.Count; i++)
        {
            var match = TeamRx.Match(lines[i]);
            if (!match.Success) continue;
            int index = int.TryParse(match.Groups["index"].Value.Trim(), out int parsed) ? parsed : -1;
            teamStarts.Add((i, lines.Count, index));
        }

        for (int i = 0; i < teamStarts.Count; i++)
        {
            var current = teamStarts[i];
            int end = i + 1 < teamStarts.Count ? teamStarts[i + 1].Start : lines.Count;
            var teamMatch = TeamRx.Match(lines[current.Start]);

            teams.Add(new EncounterTeamDto
            {
                Index = current.Index,
                RawValue = teamMatch.Groups["rest"].Value.Trim(),
                Fields = ParseFields(lines, current.Start + 1, end, excluded: "UNITDB"),
                Units = ParseUnits(lines, current.Start + 1, end),
            });
        }

        string name = headerFields
            .FirstOrDefault(f => string.Equals(f.Key, "ENCOUNTERNAME", StringComparison.OrdinalIgnoreCase))
            ?.RawValue.Trim().Trim('"') is { Length: > 0 } declared
                ? declared
                : Path.GetFileNameWithoutExtension(relativePath);

        return new EncounterDto
        {
            FilePath = relativePath,
            Name = name,
            Fields = headerFields,
            Teams = teams,
        };
    }

    private static IReadOnlyList<EncounterFieldDto> ParseFields(
        List<string> lines, int start, int end, string? excluded = null)
    {
        var fields = new List<EncounterFieldDto>();
        for (int i = start; i < end; i++)
        {
            string line = lines[i];
            if (CommentRx.IsMatch(line)) continue;
            if (TeamRx.IsMatch(line)) continue;

            var match = KeyRx.Match(line);
            if (!match.Success) continue;

            string key = match.Groups["key"].Value;
            if (excluded is not null && string.Equals(key, excluded, StringComparison.OrdinalIgnoreCase))
                continue;

            fields.Add(new EncounterFieldDto(key, StripInlineComment(match.Groups["value"].Value)));
        }
        return fields;
    }

    private static IReadOnlyList<string> ParseUnits(List<string> lines, int start, int end)
    {
        var units = new List<string>();
        for (int i = start; i < end; i++)
        {
            string line = lines[i];
            if (CommentRx.IsMatch(line)) continue;

            var match = KeyRx.Match(line);
            if (!match.Success) continue;

            string key = match.Groups["key"].Value;
            if (!string.Equals(key, "UNITDB", StringComparison.OrdinalIgnoreCase)) continue;

            units.Add(StripInlineComment(match.Groups["value"].Value));
        }
        return units;
    }

    private static string StripInlineComment(string raw)
    {
        int comment = raw.IndexOf("//", StringComparison.Ordinal);
        if (comment >= 0) raw = raw[..comment];
        return raw.Trim();
    }
}
