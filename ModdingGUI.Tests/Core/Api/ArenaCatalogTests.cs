namespace ModdingGUI.Tests.Core.Api;

using System.Linq;
using ModdingGUI.Core.Api;
using Xunit;

/// <summary>
/// Navigation data the editors use to replace flat file lists: region, town, arena, then the arena's encounters, grids and
/// leagues.
/// </summary>
public sealed class ArenaCatalogTests
{
    // the same read-only corpus fixture the other Core tests use; a project root is just a folder whose
    // ResolveBecFolder lands on the BEC tree, and clean-bec-unpacked is one
    private static bool Ready => ModdingGUI.Tests.CleanBecCorpus.IsAvailable;

    private static string Project => ModdingGUI.Tests.CleanBecCorpus.Root!;

    [Fact]
    public void Arenas_ComeFromWorldTok_WithEncountersAndGrids()
    {
        if (!Ready) return;

        var arenas = ArenaCatalog.Arenas(Project);

        Assert.True(arenas.Count > 10, $"expected many arenas, got {arenas.Count}");
        Assert.All(arenas, a => Assert.False(string.IsNullOrWhiteSpace(a.Region)));
        Assert.All(arenas, a => Assert.False(string.IsNullOrWhiteSpace(a.Town)));
        Assert.All(arenas, a => Assert.False(string.IsNullOrWhiteSpace(a.Arena)));

        ArenaCatalog.ArenaEntry pirates = arenas.Single(a => a.Arena == "pirgosarena");
        Assert.Equal("Imperia", pirates.Region);
        Assert.Contains(pirates.Encounters, e => e.EndsWith("/Battle1.enc", System.StringComparison.OrdinalIgnoreCase));
        Assert.Contains(pirates.Grids, g => g.Contains("pirgosarenaopenstart.grd", System.StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Leagues_ListTheEncountersTheyOffer_WithResolvablePaths()
    {
        if (!Ready) return;

        var leagues = ArenaCatalog.Leagues(Project, "pirgosarena");

        Assert.True(leagues.Count > 0, $"leagues={leagues.Count}, files total={leagues.Sum(l => l.EncounterPaths.Count)}, first={leagues.FirstOrDefault()?.Name}/{leagues.FirstOrDefault()?.EncounterPaths.Count}");

        ArenaCatalog.LeagueEntry open = leagues.Single(l => l.Name == "Apprentice Open League");
        Assert.Contains(open.EncounterPaths, p => p.EndsWith("/Battle1.enc", System.StringComparison.OrdinalIgnoreCase));

        // every league path must be an encounter that exists in the arena, or the picker shows nothing for that league
        var arena = ArenaCatalog.Arenas(Project).Single(a => a.Arena == "pirgosarena");
        var known = new System.Collections.Generic.HashSet<string>(arena.Encounters, System.StringComparer.OrdinalIgnoreCase);
        Assert.All(open.EncounterPaths, p => Assert.Contains(p, known));
    }

    [Fact]
    public void GridsIn_IncludesBothTheEncounterStartGridsAndTheArenaBoard()
    {
        if (!Ready) return;

        var grids = ArenaCatalog.GridsIn(Project, "Imperia", "pirgosarena");

        Assert.Contains(grids, g => g.StartsWith("data/encounters/", System.StringComparison.OrdinalIgnoreCase));
        Assert.Contains(grids, g => g.StartsWith("data/levels/", System.StringComparison.OrdinalIgnoreCase));
        Assert.Contains(grids, g => g.Contains("pirgosarenanomove", System.StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void EncounterDisplayNames_ResolveTheNameTheGameShows()
    {
        if (!Ready) return;

        // Battle1.enc is listed as ENCOUNTER "Battle1", 2228 in the arena's open league, and 2228 is "Open Battle I" in
        // lookuptext_eng.txt - the file's own ENCOUNTERNAME ("Battle1") is not what a player ever sees.
        var names = ArenaCatalog.EncounterDisplayNames(Project);

        Assert.True(names.Count > 0, "no display names resolved at all");
        Assert.Equal("Open Battle I", names["battle1"]);
    }
}
