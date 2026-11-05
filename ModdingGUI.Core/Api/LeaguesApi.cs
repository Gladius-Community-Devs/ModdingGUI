namespace ModdingGUI.Core.Api;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ModdingGUI.Core.Tok;

/// <summary>One <c>KEY value</c> line inside a league or encounter section.</summary>
/// <param name="Key">Key as written.</param>
/// <param name="RawValue">Value as written, trimmed.</param>
public sealed record LeagueFieldDto(string Key, string RawValue);

/// <summary>One <c>ENCOUNTER</c> entry of a league, i.e. a league bout.</summary>
public sealed class LeagueEncounterDto
{
    /// <summary>Encounter name from the <c>ENCOUNTER</c> line.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Everything after the name on the <c>ENCOUNTER</c> line (usually a text id).</summary>
    public string RawValue { get; init; } = string.Empty;

    /// <summary>Fields in file order (<c>ENCFILE</c>, <c>ENCPTS</c>, <c>TEAMS</c>, …).</summary>
    public IReadOnlyList<LeagueFieldDto> Fields { get; init; } = Array.Empty<LeagueFieldDto>();

    /// <summary>The <c>.enc</c> this bout loads, as written in the file.</summary>
    public string? EncounterFile => Value("ENCFILE");

    /// <summary>The team shape, e.g. <c>2 2 3 0 0</c>.</summary>
    public string? Teams => Value("TEAMS");

    /// <summary>First value of a field, or null.</summary>
    public string? Value(string key) =>
        Fields.FirstOrDefault(f => string.Equals(f.Key, key, StringComparison.OrdinalIgnoreCase))?.RawValue;

    /// <summary>All values of a field, in file order.</summary>
    public IReadOnlyList<string> Values(string key) =>
        Fields.Where(f => string.Equals(f.Key, key, StringComparison.OrdinalIgnoreCase))
              .Select(f => f.RawValue)
              .ToArray();
}

/// <summary>One <c>LEAGUE</c> section: its own fields plus its bouts.</summary>
public sealed class LeagueDto
{
    /// <summary>League name from the <c>LEAGUE</c> line.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Everything after the name on the <c>LEAGUE</c> line.</summary>
    public string RawValue { get; init; } = string.Empty;

    /// <summary>Fields in file order (<c>LEAGUEDESC</c>, <c>LEAGUEPTS</c>, <c>HERO</c>, …).</summary>
    public IReadOnlyList<LeagueFieldDto> Fields { get; init; } = Array.Empty<LeagueFieldDto>();

    /// <summary>Bouts of this league, in file order.</summary>
    public IReadOnlyList<LeagueEncounterDto> Encounters { get; init; } = Array.Empty<LeagueEncounterDto>();

    /// <summary>First value of a field, or null.</summary>
    public string? Value(string key) =>
        Fields.FirstOrDefault(f => string.Equals(f.Key, key, StringComparison.OrdinalIgnoreCase))?.RawValue;

    /// <summary>Find a bout by name.</summary>
    public LeagueEncounterDto? Encounter(string name) =>
        Encounters.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));
}

/// <summary>A parsed <c>data/towns/leagues/*.tok</c> file: office metadata plus leagues.</summary>
public sealed class LeagueFileDto
{
    /// <summary>Project-relative path.</summary>
    public string FilePath { get; init; } = string.Empty;

    /// <summary>Header fields (<c>OFFICENAME</c>, <c>RECRUIT</c>, <c>SCHOOL</c>, …), in file order.</summary>
    public IReadOnlyList<LeagueFieldDto> Fields { get; init; } = Array.Empty<LeagueFieldDto>();

    /// <summary>Leagues in file order.</summary>
    public IReadOnlyList<LeagueDto> Leagues { get; init; } = Array.Empty<LeagueDto>();

    /// <summary>All values of a header field.</summary>
    public IReadOnlyList<string> Values(string key) =>
        Fields.Where(f => string.Equals(f.Key, key, StringComparison.OrdinalIgnoreCase))
              .Select(f => f.RawValue)
              .ToArray();

    /// <summary>First value of a header field, or null.</summary>
    public string? Value(string key) =>
        Fields.FirstOrDefault(f => string.Equals(f.Key, key, StringComparison.OrdinalIgnoreCase))?.RawValue;

    /// <summary>Find a league by name.</summary>
    public LeagueDto? League(string name) =>
        Leagues.FirstOrDefault(l => string.Equals(l.Name, name, StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// Read and edit <c>data/towns/leagues/*.tok</c>: office metadata, then <c>LEAGUE</c> sections,
/// each holding <c>ENCOUNTER</c> bouts.
/// </summary>
/// <remarks>
/// This is the API behind the in-game validation harness: pointing a league bout at a different
/// <c>.enc</c> is one call (<see cref="SetEncounterFile"/>) instead of hand-editing
/// <c>ENCFILE</c> and <c>TEAMS</c> lines.
/// </remarks>
public interface ILeaguesApi
{
    /// <summary>Project-relative paths of every league file, sorted.</summary>
    IReadOnlyList<string> ListFiles(string projectPath);

    /// <summary>League names in one file, in file order.</summary>
    IReadOnlyList<string> List(string projectPath, string relativePath);

    /// <summary>Parse one league file; null when it does not exist.</summary>
    LeagueFileDto? Get(string projectPath, string relativePath);

    /// <summary>Replace a file-level field (<c>OFFICENAME</c>, <c>OFFICER</c>, …).</summary>
    void SetHeaderField(string projectPath, string relativePath, string key, string rawValue);

    /// <summary>
    /// Remove every <c>RECRUIT</c> line from every league file, which is how the randomizer's "no recruits"
    /// option empties the recruiting pool. Each changed file is written once, with its own backup; returns the
    /// number of files changed.
    /// </summary>
    int RemoveRecruits(string projectPath);

    /// <summary>Replace a league-level field (<c>LEAGUEDESC</c>, <c>LEAGUEPTS</c>, <c>HERO</c>, …).</summary>
    void SetLeagueField(string projectPath, string relativePath, string leagueName, string key, string rawValue);

    /// <summary>Replace a field of one bout (<c>ENCPTS</c>, <c>TEAMS</c>, <c>FREQUENCY</c>, …).</summary>
    void SetEncounterField(
        string projectPath, string relativePath, string leagueName, string encounterName,
        string key, string rawValue);

    /// <summary>
    /// Point a bout at a different <c>.enc</c>. Accepts a project-relative path
    /// (<c>data/encounters/…</c>) or the game's own form (<c>Nordagh\thefen\x.enc</c>).
    /// </summary>
    void SetEncounterFile(
        string projectPath, string relativePath, string leagueName, string encounterName,
        string encounterRelativePath);

    /// <summary>Append a bout to a league.</summary>
    void AddEncounter(
        string projectPath, string relativePath, string leagueName, string encounterName,
        string rawValue, IReadOnlyList<LeagueFieldDto> fields);

    /// <summary>Remove a bout from a league.</summary>
    void RemoveEncounter(string projectPath, string relativePath, string leagueName, string encounterName);

    /// <summary>Advisory checks; verifies that each <c>ENCFILE</c> resolves inside the project.</summary>
    IReadOnlyList<LeagueValidationIssue> Validate(string projectPath, string relativePath);
}

/// <summary>Default <see cref="ILeaguesApi"/>: raw line editing with lock and backup.</summary>
public sealed class LeaguesApi : ILeaguesApi
{
    private const string Root = "data/towns/leagues";

    private static readonly Regex MarkerRx = new(
        @"^(?<indent>\s*)(?<kind>LEAGUE|ENCOUNTER)\s*:?\s+(?:""(?<name>[^""]*)""(?<rest>.*))?$",
        RegexOptions.Compiled);

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
    public IReadOnlyList<string> List(string projectPath, string relativePath)
    {
        var file = Get(projectPath, relativePath);
        return file is null ? Array.Empty<string>() : file.Leagues.Select(l => l.Name).ToArray();
    }

    /// <inheritdoc />
    public LeagueFileDto? Get(string projectPath, string relativePath)
    {
        string bec = ProjectRoots.ResolveBecFolder(projectPath);
        string path = Path.Combine(bec, relativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path)) return null;

        var (lines, _) = RawLineFile.Read(bec, relativePath, Path.GetFileName(relativePath));
        return Parse(relativePath, lines);
    }

    /// <inheritdoc />
    public void SetHeaderField(string projectPath, string relativePath, string key, string rawValue) =>
        Edit(projectPath, relativePath, lines =>
        {
            int end = FirstMarkerLine(lines);
            var matches = TokLineEditing.KeyLines(lines, 0, end, key, excluded: "LEAGUE");
            if (matches.Count == 0)
                throw new InvalidOperationException($"league file has no header '{key}' line.");

            TokLineEditing.ReplaceValue(lines, matches[0], rawValue);
            for (int i = matches.Count - 1; i >= 1; i--) lines.RemoveAt(matches[i]);
            return true;
        });

    /// <inheritdoc />
    public int RemoveRecruits(string projectPath)
    {
        string bec = ProjectRoots.ResolveBecFolder(projectPath);
        var files = ListFiles(bec);
        int changed = 0;

        foreach (string relativePath in files)
        {
            bool modified = false;
            Edit(bec, relativePath, lines =>
            {
                int removed = lines.RemoveAll(line =>
                    line.TrimStart().StartsWith("RECRUIT", StringComparison.OrdinalIgnoreCase));

                modified = removed > 0;
                return modified;
            });

            if (modified) changed++;
        }

        return changed;
    }

    /// <inheritdoc />
    public void SetLeagueField(
        string projectPath, string relativePath, string leagueName, string key, string rawValue) =>
        Edit(projectPath, relativePath, lines =>
        {
            var (start, end) = RequireLeague(lines, leagueName);
            var encounters = MarkerLines(lines, "ENCOUNTER", start + 1, end);
            int leagueEnd = encounters.Count > 0 ? encounters[0] : end;

            int line = TokLineEditing.FirstKeyLine(lines, start + 1, leagueEnd, key);
            if (line < 0)
                throw new InvalidOperationException($"league '{leagueName}' has no '{key}' line.");

            TokLineEditing.ReplaceValue(lines, line, rawValue);
            return true;
        });

    /// <inheritdoc />
    public void SetEncounterField(
        string projectPath, string relativePath, string leagueName, string encounterName,
        string key, string rawValue) =>
        Edit(projectPath, relativePath, lines =>
        {
            var (start, end) = RequireEncounter(lines, leagueName, encounterName);

            if (string.Equals(key, "ENCFILE", StringComparison.OrdinalIgnoreCase))
                return ReplaceKeyValue(lines, start, end, "ENCFILE", rawValue);

            if (string.Equals(key, "ENCOUNTER", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("rename a bout by removing and re-adding it.");

            return ReplaceKeyValue(lines, start, end, key, rawValue);
        });

    /// <inheritdoc />
    public void SetEncounterFile(
        string projectPath, string relativePath, string leagueName, string encounterName,
        string encounterRelativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(encounterRelativePath);
        SetEncounterField(projectPath, relativePath, leagueName, encounterName, "ENCFILE",
            $"\"{ToGamePath(encounterRelativePath)}\"");
    }

    /// <inheritdoc />
    public void AddEncounter(
        string projectPath, string relativePath, string leagueName, string encounterName,
        string rawValue, IReadOnlyList<LeagueFieldDto> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);

        Edit(projectPath, relativePath, lines =>
        {
            var (start, end) = RequireLeague(lines, leagueName);
            if (FindEncounter(lines, leagueName, encounterName) is not null)
                throw new InvalidOperationException($"league '{leagueName}' already has '{encounterName}'.");

            int at = end;
            while (at > start + 1 && string.IsNullOrWhiteSpace(lines[at - 1])) at--;

            var block = new List<string>
            {
                string.Empty,
                $"ENCOUNTER\t\"{encounterName}\", {rawValue}".TrimEnd(' ', ','),
            };
            block.AddRange(fields.Select(f => $"{f.Key}\t\t{f.RawValue}"));
            lines.InsertRange(at, block);
            return true;
        });
    }

    /// <inheritdoc />
    public void RemoveEncounter(string projectPath, string relativePath, string leagueName, string encounterName) =>
        Edit(projectPath, relativePath, lines =>
        {
            var found = FindEncounter(lines, leagueName, encounterName);
            if (found is null) return false;

            int end = found.Value.End;
            while (end < lines.Count && string.IsNullOrWhiteSpace(lines[end])) end++;
            lines.RemoveRange(found.Value.Start, end - found.Value.Start);
            return true;
        });

    /// <inheritdoc />
    public IReadOnlyList<LeagueValidationIssue> Validate(string projectPath, string relativePath)
    {
        string bec = ProjectRoots.ResolveBecFolder(projectPath);
        var file = Get(projectPath, relativePath)
            ?? throw new FileNotFoundException($"league file not found: {relativePath}");

        return LeagueValidator.Validate(
            file,
            LeaguesCatalog.Shipped(),
            encounterFile => File.Exists(Path.Combine(bec, "data", "encounters",
                encounterFile.Replace('\\', Path.DirectorySeparatorChar))));
    }

    // ---- editing plumbing ---------------------------------------------------

    private void Edit(string projectPath, string relativePath, Func<List<string>, bool> mutate)
    {
        string bec = ProjectRoots.ResolveBecFolder(projectPath);
        using (ProjectLock.Acquire(bec))
        {
            string path = Path.Combine(bec, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path)) throw new FileNotFoundException("league file not found.", path);

            var (lines, trailing) = RawLineFile.Read(bec, relativePath, Path.GetFileName(relativePath));
            if (!mutate(lines)) return;
            RawLineFile.Write(bec, relativePath, lines, trailing);
        }
    }

    private static bool ReplaceKeyValue(List<string> lines, int start, int end, string key, string rawValue)
    {
        int line = TokLineEditing.FirstKeyLine(lines, start + 1, end, key);
        if (line < 0) return false;
        return TokLineEditing.ReplaceValue(lines, line, rawValue);
    }

    /// <summary>Convert <c>data/encounters/Nordagh/thefen/x.enc</c> to <c>Nordagh\thefen\x.enc</c>.</summary>
    internal static string ToGamePath(string path)
    {
        string trimmed = path.Replace('/', '\\');
        const string prefix = "data\\encounters\\";
        if (trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed[prefix.Length..];
        return trimmed;
    }

    // ---- parsing ------------------------------------------------------------

    private static int FirstMarkerLine(List<string> lines)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            if (MarkerRx.IsMatch(lines[i])) return i;
        }
        return lines.Count;
    }

    private static List<int> MarkerLines(List<string> lines, string kind, int start, int end)
    {
        var found = new List<int>();
        for (int i = start; i < end; i++)
        {
            var match = MarkerRx.Match(lines[i]);
            if (match.Success && string.Equals(match.Groups["kind"].Value, kind, StringComparison.OrdinalIgnoreCase))
                found.Add(i);
        }
        return found;
    }

    private static (int Start, int End) RequireLeague(List<string> lines, string leagueName) =>
        FindLeague(lines, leagueName) ?? throw new InvalidOperationException($"league '{leagueName}' not found.");

    private static (int Start, int End)? FindLeague(List<string> lines, string name)
    {
        var starts = MarkerLines(lines, "LEAGUE", 0, lines.Count);
        for (int i = 0; i < starts.Count; i++)
        {
            var match = MarkerRx.Match(lines[starts[i]]);
            if (!string.Equals(match.Groups["name"].Value, name, StringComparison.OrdinalIgnoreCase)) continue;

            int end = i + 1 < starts.Count ? starts[i + 1] : lines.Count;
            return (starts[i], end);
        }
        return null;
    }

    private static (int Start, int End) RequireEncounter(
        List<string> lines, string leagueName, string encounterName) =>
        FindEncounter(lines, leagueName, encounterName)
        ?? throw new InvalidOperationException($"league '{leagueName}' has no bout '{encounterName}'.");

    private static (int Start, int End)? FindEncounter(
        List<string> lines, string leagueName, string encounterName)
    {
        var league = FindLeague(lines, leagueName);
        if (league is null) return null;

        var starts = MarkerLines(lines, "ENCOUNTER", league.Value.Start + 1, league.Value.End);
        for (int i = 0; i < starts.Count; i++)
        {
            var match = MarkerRx.Match(lines[starts[i]]);
            if (!string.Equals(match.Groups["name"].Value, encounterName, StringComparison.OrdinalIgnoreCase))
                continue;

            int end = i + 1 < starts.Count ? starts[i + 1] : league.Value.End;
            return (starts[i], end);
        }
        return null;
    }

    internal static LeagueFileDto Parse(string relativePath, List<string> lines)
    {
        int firstMarker = FirstMarkerLine(lines);

        var fields = new List<LeagueFieldDto>();
        for (int i = 0; i < firstMarker; i++)
        {
            if (TokLineEditing.CommentRx.IsMatch(lines[i])) continue;
            if (TokLineEditing.TryKey(lines[i], out string key, out _, out string value))
                fields.Add(new LeagueFieldDto(key, value));
        }

        var leagues = new List<LeagueDto>();
        var leagueStarts = MarkerLines(lines, "LEAGUE", 0, lines.Count);
        for (int i = 0; i < leagueStarts.Count; i++)
        {
            int start = leagueStarts[i];
            int end = i + 1 < leagueStarts.Count ? leagueStarts[i + 1] : lines.Count;
            var match = MarkerRx.Match(lines[start]);

            var encounterStarts = MarkerLines(lines, "ENCOUNTER", start + 1, end);
            int leagueFieldsEnd = encounterStarts.Count > 0 ? encounterStarts[0] : end;

            var leagueFields = new List<LeagueFieldDto>();
            for (int j = start + 1; j < leagueFieldsEnd; j++)
            {
                if (TokLineEditing.CommentRx.IsMatch(lines[j])) continue;
                if (TokLineEditing.TryKey(lines[j], out string key, out _, out string value))
                    leagueFields.Add(new LeagueFieldDto(key, value));
            }

            var encounters = new List<LeagueEncounterDto>();
            for (int k = 0; k < encounterStarts.Count; k++)
            {
                int encStart = encounterStarts[k];
                int encEnd = k + 1 < encounterStarts.Count ? encounterStarts[k + 1] : end;
                var encMatch = MarkerRx.Match(lines[encStart]);

                var encFields = new List<LeagueFieldDto>();
                for (int j = encStart + 1; j < encEnd; j++)
                {
                    if (TokLineEditing.CommentRx.IsMatch(lines[j])) continue;
                    if (TokLineEditing.TryKey(lines[j], out string key, out _, out string value))
                        encFields.Add(new LeagueFieldDto(key, value));
                }

                encounters.Add(new LeagueEncounterDto
                {
                    Name = encMatch.Groups["name"].Value,
                    RawValue = encMatch.Groups["rest"].Value.Trim().TrimStart(',').Trim(),
                    Fields = encFields,
                });
            }

            leagues.Add(new LeagueDto
            {
                Name = match.Groups["name"].Value,
                RawValue = match.Groups["rest"].Value.Trim().TrimStart(',').Trim(),
                Fields = leagueFields,
                Encounters = encounters,
            });
        }

        return new LeagueFileDto { FilePath = relativePath, Fields = fields, Leagues = leagues };
    }
}
