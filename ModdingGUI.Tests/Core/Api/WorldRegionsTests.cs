namespace ModdingGUI.Tests.Core.Api;

using ModdingGUI.Core.Api;
using Xunit;

/// <summary>
/// Tests for <see cref="WorldRegions"/>: the region → town → arena table, and the two runtime school tokens it resolves.
/// </summary>
/// <remarks>
/// These need the reference corpus (<c>clean-bec-unpacked</c> beside the solution). Without it they return quietly, because
/// the project deliberately does not check game data into the repository.
/// </remarks>
public class WorldRegionsTests
{
    private static string? CorpusRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, "clean-bec-unpacked");
            if (Directory.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }

        return null;
    }

    [Fact]
    public void Read_FindsTheFourRegions()
    {
        string? corpus = CorpusRoot();
        if (corpus is null) return;

        IReadOnlyList<WorldRegion> regions = WorldRegions.Read(corpus);

        Assert.Equal(new[] { "Expanse", "Imperia", "Nordagh", "Steppes" }, regions.Select(r => r.Name));
        Assert.All(regions, region => Assert.NotEmpty(region.Towns));
    }

    [Fact]
    public void Read_AssignsArenasToTowns()
    {
        string? corpus = CorpusRoot();
        if (corpus is null) return;

        WorldRegion imperia = WorldRegions.Read(corpus).First(r => r.Name == "Imperia");
        WorldTown? pirgos = imperia.Towns.FirstOrDefault(t => t.LeagueFileStem == "pirgosarena");

        Assert.NotNull(pirgos);
        Assert.Equal("pirgosarena", pirgos!.LeagueFileStem);
    }

    [Fact]
    public void ArenaSchools_ReturnsTheLeagueFilesSchoolList()
    {
        string? corpus = CorpusRoot();
        if (corpus is null) return;

        // altahrunruins_leagues.tok lists five schools under its RECRUIT block.
        IReadOnlyList<string> schools = WorldRegions.ArenaSchools(corpus, "altahrunruins");

        Assert.Equal(
            new[] { "Borte Hulagu", "Eyes of the Beast", "Krakens", "School of Past Glory", "School of Suyhron" },
            schools);
    }

    [Fact]
    public void RegionSchools_IsTheUnionOfItsArenasSchools()
    {
        string? corpus = CorpusRoot();
        if (corpus is null) return;

        IReadOnlyList<string> steppes = WorldRegions.RegionSchools(corpus, "Steppes");

        Assert.NotEmpty(steppes);
        Assert.Contains("Krakens", steppes);                  // from altahrunruins
        Assert.Contains("Rangers", steppes);                  // from arenasuren
        Assert.Equal(steppes.Count, steppes.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void ForArena_FindsTheOwningRegion()
    {
        string? corpus = CorpusRoot();
        if (corpus is null) return;

        IReadOnlyList<WorldRegion> regions = WorldRegions.Read(corpus);

        Assert.Equal("Imperia", WorldRegions.ForArena(regions, "pirgosarena")?.Name);
        Assert.Equal("Steppes", WorldRegions.ForArena(regions, "arenasuren")?.Name);
        Assert.Null(WorldRegions.ForArena(regions, "not-an-arena"));
    }

    [Fact]
    public void MissingProject_YieldsNothingRatherThanThrowing()
    {
        string missing = Path.Combine(Path.GetTempPath(), $"no-project-{Guid.NewGuid():N}");

        Assert.Empty(WorldRegions.Read(missing));
        Assert.Empty(WorldRegions.ArenaSchools(missing, "pirgosarena"));
        Assert.Empty(WorldRegions.RegionSchools(missing, "Imperia"));
    }
}
