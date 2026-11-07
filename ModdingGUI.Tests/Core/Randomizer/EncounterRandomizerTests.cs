using System;
using System.IO;
using System.Linq;
using ModdingGUI.Core.Randomizer;
using Xunit;

namespace ModdingGUI.Tests.Core.Randomizer;

/// <summary>
/// The encounter pass opens the player's team slots and, when asked, turns empty enemy rows into randomizable ones
/// that prohibit Boss, Prop, DarkGod and AffinityBeastGreater — the prohibition lives in the row, not in a list.
/// </summary>
public sealed class EncounterRandomizerTests : IDisposable
{
    private readonly string _project;
    private readonly string _bec;

    // A normal encounter (player team + an enemy team carrying SCHOOL:) and the scripted one.
    private static readonly string[] Fixtures = { "expansedunesencounter01.enc", "scene47u.enc" };

    public EncounterRandomizerTests()
    {
        _project = Path.Combine(Path.GetTempPath(), "ModdingGUI.Encounters-" + Guid.NewGuid().ToString("N"));
        _bec = Path.Combine(_project, "proj_BEC");

        string corpus = Path.Combine(CleanBecCorpus.Root!, "data", "encounters");
        string target = Path.Combine(_bec, "data", "encounters");
        Directory.CreateDirectory(target);

        foreach (string name in Fixtures)
        {
            string? file = Directory.EnumerateFiles(corpus, "*.enc", SearchOption.AllDirectories)
                .FirstOrDefault(path => Path.GetFileName(path).Equals(name, StringComparison.OrdinalIgnoreCase));

            if (file is not null) File.Copy(file, Path.Combine(target, name));
        }
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_project)) Directory.Delete(_project, recursive: true); }
        catch (IOException) { /* best effort */ }
    }

    private bool Ready =>
        Fixtures.All(name => File.Exists(Path.Combine(_bec, "data", "encounters", name)));

    private string[] Lines(string name) =>
        File.ReadAllLines(Path.Combine(_bec, "data", "encounters", name));

    [Fact]
    public void EnemySlotsProhibitBossesAndProps()
    {
        if (!Ready) return;

        var result = new EncounterRandomizer().Apply(_bec, randomizeEnemies: true, permaDeath: false);
        Assert.True(result.EnemiesRandomized > 0, "no enemy slot was randomized");

        var randomizable = Lines("expansedunesencounter01.enc")
            .Where(line => line.Contains("\"Boss\"", StringComparison.Ordinal))
            .ToArray();

        Assert.NotEmpty(randomizable);
        foreach (string line in randomizable)
        {
            Assert.Contains("\"Prop\"", line);
            Assert.Contains("\"DarkGod\"", line);
            Assert.Contains("\"AffinityBeastGreater\"", line);
        }
    }

    [Fact]
    public void RandomizableRowShapeMatchesTheOriginal()
    {
        Assert.Equal(
            "UNITDB:\t\"\", 99, \"Start3\", -2, 2, 1, 0, -1, \"Boss\", \"Prop\", \"DarkGod\", \"AffinityBeastGreater\", " +
            "0, 1, 0, 0, 0, 0, 25, 0, 0",
            EncounterRandomizer.RandomizableRow("Start3"));
    }

    [Fact]
    public void PlayerTeamSlotsStayOpen()
    {
        if (!Ready) return;

        new EncounterRandomizer().Apply(_bec, randomizeEnemies: false, permaDeath: false);

        int team = -1;
        foreach (string line in Lines("expansedunesencounter01.enc"))
        {
            string trimmed = line.Trim();
            if (trimmed.StartsWith("TEAM:", StringComparison.OrdinalIgnoreCase))
            {
                team = int.Parse(trimmed["TEAM:".Length..].Split(',')[0].Trim());
                continue;
            }

            if (team != 0) continue;
            if (!trimmed.StartsWith("UNITDB:", StringComparison.OrdinalIgnoreCase)) continue;

            Assert.StartsWith("UNITDB:\t\"\", ", line);
        }
    }

    [Fact]
    public void SchoolLinesInsideTeamsAreDropped()
    {
        if (!Ready) return;

        new EncounterRandomizer().Apply(_bec, randomizeEnemies: false, permaDeath: false);

        Assert.DoesNotContain(Lines("expansedunesencounter01.enc"),
            line => line.Trim().StartsWith("SCHOOL:", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PermaDeathAddsCandieOnlyWhenMissing()
    {
        if (!Ready) return;

        // Both shipped encounters already carry CANDIE:, so strip it from one and prove the pass puts it back.
        string stripped = Path.Combine(_bec, "data", "encounters", "expansedunesencounter01.enc");
        File.WriteAllLines(stripped, Lines("expansedunesencounter01.enc")
            .Where(line => !line.Trim().Equals("CANDIE:", StringComparison.OrdinalIgnoreCase)));

        var result = new EncounterRandomizer().Apply(_bec, randomizeEnemies: false, permaDeath: true);

        Assert.Equal(1, result.CandieInserted);
        Assert.Equal(1, Lines("scene47u.enc")
            .Count(line => line.Trim().Equals("CANDIE:", StringComparison.OrdinalIgnoreCase)));
        Assert.Equal(1, Lines("expansedunesencounter01.enc")
            .Count(line => line.Trim().Equals("CANDIE:", StringComparison.OrdinalIgnoreCase)));

        // Running again changes nothing.
        var second = new EncounterRandomizer().Apply(_bec, randomizeEnemies: false, permaDeath: true);
        Assert.Equal(0, second.CandieInserted);
        Assert.Equal(0, second.FilesChanged);
    }

    [Fact]
    public void SpecialEncounterKeepsOneSetOfHeroRows()
    {
        if (!Ready) return;

        new EncounterRandomizer().Apply(_bec, randomizeEnemies: false, permaDeath: false);

        var lines = Lines("scene47u.enc");
        Assert.Single(lines, l => l.Contains("\"Ursula\""));
        Assert.Single(lines, l => l.Contains("\"Valens\""));
        Assert.Single(lines, l => l.Contains("\"Urlan\""));
        Assert.Single(lines, l => l.Contains("\"Eiji\""));
    }

    [Fact]
    public void TheServiceRunsTheStep()
    {
        if (!Ready) return;

        var result = new RandomizerService().Randomize(_bec,
            new RandomizerOptions { Seed = "enc", RandomizeEnemies = true });

        Assert.Contains("encounters", result.Steps);
        Assert.True(result.ChangedRecords > 0);
    }
}
