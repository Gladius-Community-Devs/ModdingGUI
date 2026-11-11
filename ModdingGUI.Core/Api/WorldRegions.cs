namespace ModdingGUI.Core.Api;

using System.Text.RegularExpressions;

/// <summary>One town on the world map, with the arena it hosts.</summary>
/// <param name="Name">Town name as written, e.g. <c>Pirgos</c>.</param>
/// <param name="Arena">Arena folder name the town's league files live under, e.g. <c>pirgosarena</c>.</param>
public sealed record WorldTown(string Name, string? Arena)
{
    /// <summary>The league file stem for this town's arena, or null when it has no arena.</summary>
    public string? LeagueFileStem => string.IsNullOrWhiteSpace(Arena) ? null : Arena!.ToLowerInvariant();
}

/// <summary>One region and the towns inside it.</summary>
public sealed record WorldRegion(string Name, IReadOnlyList<WorldTown> Towns);

/// <summary>
/// Reads <c>data/config/world.tok</c>: the region → town → arena table.
/// </summary>
/// <remarks>
/// <para>
/// This is the table that makes the two runtime school tokens resolvable without touching the DOL. Encounters say
/// <c>SCHOOL: "HOMETOWNSCHOOL"</c> or <c>"REGIONSCHOOL"</c>, which are not names in <c>schools.txt</c>; the schools behind
/// them are per town, and each town's arena owns a league file (<c>data/towns/leagues/&lt;arena&gt;_leagues.tok</c>) whose
/// <c>SCHOOL</c> lines list that town's schools. So:
/// </para>
/// <list type="bullet">
/// <item><description><c>HOMETOWNSCHOOL</c> = the arena's own league-file school list.</description></item>
/// <item><description><c>REGIONSCHOOL</c> = the union of the league-file school lists of every arena in the region.</description></item>
/// </list>
/// <para>
/// Both are derived here from data only, which keeps to the rule that a mod ships as data: no DOL edits and no hard-coded
/// school lists.
/// </para>
/// </remarks>
public static class WorldRegions
{
    /// <summary>Canonical relative path inside a project's BEC tree.</summary>
    public const string RelativePath = "data/config/world.tok";

    private static readonly Regex RegionLine = new("^REGION\\s+\"(?<name>[^\"]+)\"", RegexOptions.CultureInvariant);
    private static readonly Regex TownLine = new("^TOWN\\s+\"(?<name>[^\"]+)\"", RegexOptions.CultureInvariant);
    private static readonly Regex ArenaLine = new("^ARENA\\s+\"(?<name>[^\"]+)\"", RegexOptions.CultureInvariant);

    /// <summary>Every region in the project, in file order.</summary>
    public static IReadOnlyList<WorldRegion> Read(string projectPath)
    {
        string bec = ProjectRoots.ResolveBecFolder(projectPath);
        string path = Path.Combine(bec, "data", "config", "world.tok");
        if (!File.Exists(path)) return Array.Empty<WorldRegion>();

        var regions = new List<WorldRegion>();
        string? regionName = null;
        var towns = new List<WorldTown>();
        string? townName = null;
        string? arena = null;
        var townsWithArena = new List<WorldTown>();

        void CloseTown()
        {
            if (!string.IsNullOrWhiteSpace(townName)) townsWithArena.Add(new WorldTown(townName!, arena));
            townName = null;
            arena = null;
        }

        void CloseRegion()
        {
            CloseTown();
            if (!string.IsNullOrWhiteSpace(regionName)) regions.Add(new WorldRegion(regionName!, townsWithArena.ToArray()));
            townsWithArena = new List<WorldTown>();
            regionName = null;
        }

        foreach (string raw in File.ReadAllLines(path))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("//", StringComparison.Ordinal)) continue;

            Match match = RegionLine.Match(line);
            if (match.Success)
            {
                CloseRegion();
                regionName = match.Groups["name"].Value;
                continue;
            }

            match = TownLine.Match(line);
            if (match.Success)
            {
                CloseTown();
                townName = match.Groups["name"].Value;
                continue;
            }

            match = ArenaLine.Match(line);
            if (match.Success)
            {
                arena = match.Groups["name"].Value;
            }
        }

        CloseRegion();
        _ = towns;
        return regions;
    }

    /// <summary>The region that contains an arena folder name, case-insensitively.</summary>
    public static WorldRegion? ForArena(IReadOnlyList<WorldRegion> regions, string arenaFolder)
    {
        ArgumentNullException.ThrowIfNull(regions);
        if (string.IsNullOrWhiteSpace(arenaFolder)) return null;

        return regions.FirstOrDefault(region => region.Towns.Any(town =>
            string.Equals(town.LeagueFileStem, arenaFolder.Trim().ToLowerInvariant(), StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// The school names listed by one arena's league file, in file order.
    /// </summary>
    public static IReadOnlyList<string> ArenaSchools(string projectPath, string arenaFolder)
    {
        if (string.IsNullOrWhiteSpace(arenaFolder)) return Array.Empty<string>();

        string bec = ProjectRoots.ResolveBecFolder(projectPath);
        string path = Path.Combine(bec, "data", "towns", "leagues", arenaFolder.Trim().ToLowerInvariant() + "_leagues.tok");
        if (!File.Exists(path)) return Array.Empty<string>();

        return ReadSchoolLines(path);
    }

    /// <summary>
    /// Every school reachable in a region: the union of its arenas' league-file school lists, de-duplicated in first-seen
    /// order.
    /// </summary>
    public static IReadOnlyList<string> RegionSchools(string projectPath, string regionName)
    {
        WorldRegion? region = Read(projectPath)
            .FirstOrDefault(r => string.Equals(r.Name, regionName, StringComparison.OrdinalIgnoreCase));

        if (region is null) return Array.Empty<string>();

        var names = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (WorldTown town in region.Towns)
        {
            if (town.LeagueFileStem is null) continue;

            foreach (string school in ArenaSchools(projectPath, town.LeagueFileStem))
            {
                if (seen.Add(school)) names.Add(school);
            }
        }

        return names;
    }

    private static IReadOnlyList<string> ReadSchoolLines(string path)
    {
        var schools = new List<string>();

        foreach (string raw in File.ReadAllLines(path))
        {
            string line = raw.Trim();
            if (!line.StartsWith("SCHOOL", StringComparison.OrdinalIgnoreCase)) continue;

            int firstQuote = line.IndexOf('"');
            int lastQuote = line.LastIndexOf('"');
            if (firstQuote < 0 || lastQuote <= firstQuote) continue;

            string name = line[(firstQuote + 1)..lastQuote];
            if (name.Length > 0) schools.Add(name);
        }

        return schools;
    }
}
