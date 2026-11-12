namespace ModdingGUI.Core.Api;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

/// <summary>
/// Navigation over the world's arenas: region, town, arena, then the encounters and grids that belong to one arena.
/// </summary>
/// <remarks>
/// <para>
/// The modding surfaces are organised by folder, and the folders are unreadable
/// (<c>data/encounters/imperia/pirgosarena/openbattleursula3.enc</c>). But the game already knows the structure, in
/// <c>data/config/world.tok</c>: a region holds towns, and a town holds one arena. That is the dropdown order the editors
/// want, and it is why this type exists rather than each page walking the tree on its own.
/// </para>
/// <para>
/// A league is the arena's own grouping (<c>data/towns/leagues/&lt;arena&gt;_leagues.tok</c>): each league lists the
/// encounters it offers, by <c>ENCFILE</c>. That gives a fourth level - region, town, arena, league - and the encounters in
/// a league are the ones a player actually reaches, which is exactly what the affinity experiment needed and did not have.
/// </para>
/// </remarks>
public static class ArenaCatalog
{
    /// <summary>One arena: the folder name plus the encounters and grids that live in it.</summary>
    public sealed record ArenaEntry(
        string Region,
        string Town,
        string Arena,
        IReadOnlyList<string> Encounters,
        IReadOnlyList<string> Grids);

    /// <summary>One league in an arena, with the encounters it lists (project-relative paths).</summary>
    /// <param name="Name">Internal name the file uses, e.g. <c>Apprentice Open League</c>.</param>
    /// <param name="EncounterPaths">The league's <c>ENCFILE</c>s as <c>data/encounters/...</c> paths.</param>
    /// <param name="DisplayName">Name the game shows, e.g. <c>Civilis League of Pirgos</c>; null when unknown.</param>
    public sealed record LeagueEntry(string Name, IReadOnlyList<string> EncounterPaths, string? DisplayName = null);

    /// <summary>Every arena in the project, in world.tok order.</summary>
    public static IReadOnlyList<ArenaEntry> Arenas(string projectPath)
    {
        string bec = ProjectRoots.ResolveBecFolder(projectPath);
        var entries = new List<ArenaEntry>();

        foreach (WorldRegion region in WorldRegions.Read(bec))
        {
            foreach (WorldTown town in region.Towns)
            {
                if (string.IsNullOrWhiteSpace(town.Arena)) continue;

                string arena = town.Arena!.Trim();
                entries.Add(new ArenaEntry(
                    region.Name,
                    town.Name,
                    arena,
                    EncountersIn(bec, region.Name, arena),
                    GridsIn(bec, region.Name, arena)));
            }
        }

        return entries;
    }

    /// <summary>Encounter files under <c>data/encounters/&lt;region&gt;/&lt;arena&gt;/</c>.</summary>
    public static IReadOnlyList<string> EncountersIn(string projectPath, string region, string arena)
    {
        string bec = ProjectRoots.ResolveBecFolder(projectPath);
        string folder = Path.Combine(bec, "data", "encounters", region, arena);
        if (!Directory.Exists(folder)) return Array.Empty<string>();

        return Directory.EnumerateFiles(folder, "*.enc", SearchOption.TopDirectoryOnly)
            .Select(path => Relative(bec, path))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Every <c>.grd</c> belonging to an arena: the start grids beside its encounters and the level-side grids.
    /// </summary>
    /// <remarks>
    /// An encounter names two kinds of grid - <c>$d\encounters\...\</c> (where teams start) and <c>$l\levels\...\</c> (the
    /// arena's own board) - and both are worth editing, so both are listed, with the encounter-side ones first.
    /// </remarks>
    public static IReadOnlyList<string> GridsIn(string projectPath, string region, string arena)
    {
        string bec = ProjectRoots.ResolveBecFolder(projectPath);

        var found = new List<string>();
        foreach (string root in new[] { "data\\encounters", "data\\levels" })
        {
            string folder = Path.Combine(bec, root, region, arena);
            if (!Directory.Exists(folder)) continue;

            found.AddRange(Directory.EnumerateFiles(folder, "*.grd", SearchOption.AllDirectories)
                .Select(path => Relative(bec, path))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase));
        }

        return found.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// The leagues in one arena and the encounters each offers.
    /// </summary>
    public static IReadOnlyList<LeagueEntry> Leagues(string projectPath, string arena)
    {
        string bec = ProjectRoots.ResolveBecFolder(projectPath);
        string path = Path.Combine(bec, "data", "towns", "leagues", $"{arena.ToLowerInvariant()}_leagues.tok");
        if (!File.Exists(path)) return Array.Empty<LeagueEntry>();

        // A league block runs from its LEAGUE line to the next one; its encounters are the ENCFILE lines inside it.
        // Scanning the text beats a cursor here: a block mixes LEAGUE, ENCOUNTER, ENCFILE and ~20 other keys.
        string text = File.ReadAllText(path);
        IReadOnlyDictionary<int, string> lookup = LookupText(bec);
        var leagues = new List<LeagueEntry>();

        foreach (System.Text.RegularExpressions.Match block in System.Text.RegularExpressions.Regex.Matches(
                     text,
                     "^LEAGUE\\s+\"(?<name>[^\"]+)\"(?:\\s*,\\s*(?<id>\\d+))?(?<body>[\\s\\S]*?)(?=^LEAGUE\\s|\\z)",
                     System.Text.RegularExpressions.RegexOptions.Multiline))
        {
            string name = block.Groups["name"].Value;
            var files = new List<string>();

            foreach (System.Text.RegularExpressions.Match file in System.Text.RegularExpressions.Regex.Matches(
                         block.Groups["body"].Value,
                         "^ENCFILE\\s+\"(?<file>[^\"]+)\"",
                         System.Text.RegularExpressions.RegexOptions.Multiline))
            {
                // ENCFILE values are Windows paths relative to data/encounters/: "imperia\pirgosarena\Battle1.enc"
                files.Add("data/encounters/" + file.Groups["file"].Value.Replace('\\', '/'));
            }

            // The league's own display name is another lookup id:
            //   LEAGUE "Apprentice Open League", 2179   ->   2179 = "Civilis League of Pirgos"
            string? display = null;
            if (int.TryParse(block.Groups["id"].Value, out int leagueId) && lookup.TryGetValue(leagueId, out string? leagueName))
            {
                display = leagueName;
            }

            leagues.Add(new LeagueEntry(name, files, display));
        }

        return leagues;
    }

    private static string? ReadQuoted(string line)
    {
        int first = line.IndexOf('"');
        if (first < 0) return null;
        int second = line.IndexOf('"', first + 1);
        if (second < 0) return null;
        return line[(first + 1)..second];
    }

    private static string Relative(string root, string path) =>
        Path.GetRelativePath(root, path).Replace('\\', '/');

    /// <summary>
    /// In-game display names for encounters, keyed by file stem (lower case): <c>battle1</c> to <c>Open Battle I</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An encounter file calls itself <c>Battle1</c>, and that is not what the player ever sees. The name the game shows comes
    /// from the league that offers the encounter: <c>ENCOUNTER "Battle1", 2228</c>, where 2228 is an id in
    /// <c>lookuptext_&lt;lang&gt;.txt</c> ("<c>Open Battle I</c>"). So the display name is two hops away from the file, and
    /// everything needed is already in the project.
    /// </para>
    /// <para>
    /// Every league file is scanned once and cached per project; encounters that no league lists simply have no entry and the
    /// caller falls back to the file's own name.
    /// </para>
    /// </remarks>
    public static IReadOnlyDictionary<string, string> EncounterDisplayNames(string projectPath)
    {
        string bec = ProjectRoots.ResolveBecFolder(projectPath);
        string cacheKey = Path.GetFullPath(bec);

        lock (DisplayNameCache)
        {
            if (DisplayNameCache.TryGetValue(cacheKey, out IReadOnlyDictionary<string, string>? cached)) return cached;
        }

        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            IReadOnlyDictionary<int, string> text = new LookupTextApi().Load(bec);
            string leaguesFolder = Path.Combine(bec, "data", "towns", "leagues");
            if (Directory.Exists(leaguesFolder))
            {
                foreach (string file in Directory.EnumerateFiles(leaguesFolder, "*_leagues.tok"))
                {
                    string content = File.ReadAllText(file);

                    foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(
                                 content,
                                 "ENCOUNTER\\s+\"(?<stem>[^\"]+)\"\\s*,\\s*(?<id>\\d+)",
                                 System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                    {
                        string stem = Path.GetFileNameWithoutExtension(match.Groups["stem"].Value);
                        if (!int.TryParse(match.Groups["id"].Value, out int id)) continue;
                        if (!text.TryGetValue(id, out string? name) || string.IsNullOrWhiteSpace(name)) continue;

                        // first league wins: the earlier entry is the one the arena shows first
                        names.TryAdd(stem, name.Trim());
                    }
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException
                                       or ArgumentException or FormatException)
        {
            // a project without league text or lookup text simply has no display names
        }

        lock (DisplayNameCache) DisplayNameCache[cacheKey] = names;
        return names;
    }

    private static readonly Dictionary<string, IReadOnlyDictionary<string, string>> DisplayNameCache =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Localised text table for a BEC folder, read once per process.</summary>
    private static IReadOnlyDictionary<int, string> LookupText(string bec)
    {
        string key = Path.GetFullPath(bec);

        lock (LookupCache)
        {
            if (LookupCache.TryGetValue(key, out IReadOnlyDictionary<int, string>? cached)) return cached;
        }

        IReadOnlyDictionary<int, string> table;
        try
        {
            table = new LookupTextApi().Load(bec);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException
                                       or ArgumentException or FormatException)
        {
            table = new Dictionary<int, string>();
        }

        lock (LookupCache) LookupCache[key] = table;
        return table;
    }

    private static readonly Dictionary<string, IReadOnlyDictionary<int, string>> LookupCache =
        new(StringComparer.OrdinalIgnoreCase);
}
