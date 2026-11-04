using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ModdingGUI.Core.Api;
using Xunit;

namespace ModdingGUI.Tests.Core.Api;

/// <summary>
/// <c>ReplaceWith</c> — the operation that makes a story or staged battle run a different
/// fight — plus the duplicate-start-slot rule that a QA session exposed (a unit moved onto an
/// occupied slot simply did not spawn).
/// </summary>
public sealed class EncounterReplaceTests : IDisposable
{
    private const string Arena = "data/encounters/nordagh/thefen/sx_arenacert6.enc";
    private const string Tutorial = "data/encounters/nordagh/orinskeep/Scene04U.enc";

    private readonly string _projectDir;

    public EncounterReplaceTests()
    {
        _projectDir = Path.Combine(Path.GetTempPath(),
            "ModdingGUI.Replace-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_projectDir);
        if (!CleanBecCorpus.IsAvailable) return;

        string source = Path.Combine(CleanBecCorpus.Root!, "data", "encounters");
        foreach (string file in Directory.EnumerateFiles(source, "*.enc", SearchOption.AllDirectories))
        {
            string destination = Path.Combine(_projectDir, "data", "encounters",
                Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination, overwrite: true);
        }
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_projectDir)) Directory.Delete(_projectDir, recursive: true); }
        catch (IOException) { /* best effort */ }
    }

    private static bool Ready => CleanBecCorpus.IsAvailable;

    [Fact]
    public void ReplaceWith_CopiesTheSourceAndKeepsTheDestinationName()
    {
        if (!Ready) return;

        var api = new EncountersApi();
        api.ReplaceWith(_projectDir, Arena, Tutorial, encounterName: "Scene04U");

        var replaced = api.Get(_projectDir, Tutorial)!;
        Assert.Equal("Scene04U", replaced.Name);
        Assert.Equal(2, replaced.Teams.Count);
        Assert.Equal(2, replaced.Team(0)!.Units.Count);
        Assert.Equal(3, replaced.Team(1)!.Units.Count);
        // The arena's grid comes along, and the story cutscene is stripped.
        Assert.Contains("arenacertification4.grd", replaced.Team(0)!.Value("GRIDFILE"));
        Assert.Null(replaced.Value("CUTSCENE"));
    }

    [Fact]
    public void ReplaceWith_IsIdempotent()
    {
        if (!Ready) return;

        var api = new EncountersApi();
        api.ReplaceWith(_projectDir, Arena, Tutorial, encounterName: "Scene04U");
        byte[] first = File.ReadAllBytes(Path.Combine(_projectDir, Tutorial));

        api.ReplaceWith(_projectDir, Arena, Tutorial, encounterName: "Scene04U");
        Assert.Equal(first, File.ReadAllBytes(Path.Combine(_projectDir, Tutorial)));
    }

    [Fact]
    public void ReplaceWith_LeavesTheDestinationValid()
    {
        if (!Ready) return;

        var api = new EncountersApi();
        api.ReplaceWith(_projectDir, Arena, Tutorial, encounterName: "Scene04U");

        var issues = api.Validate(_projectDir, Tutorial);
        Assert.DoesNotContain(issues, i => i.Severity == EncounterValidationSeverity.Warning);
    }

    [Fact]
    public void ReplaceWith_WritesABackupOfTheDestination()
    {
        if (!Ready) return;

        new EncountersApi().ReplaceWith(_projectDir, Arena, Tutorial, encounterName: "Scene04U");

        string history = Path.Combine(_projectDir, ".mod-history", "data", "encounters",
            "nordagh", "orinskeep", "Scene04U.enc");
        Assert.True(Directory.Exists(history));
        Assert.NotEmpty(Directory.GetFiles(history, "*.bak"));
    }

    [Fact]
    public void Validate_DuplicateStartSlot_IsReported()
    {
        if (!Ready) return;

        var api = new EncountersApi();
        var team = api.Get(_projectDir, Arena)!.Team(1)!;

        // Move unit 0 onto a slot another unit already holds - the exact mistake seen in QA.
        string occupied = team.Unit(1)!.StartSlot;
        Assert.NotEqual(team.Unit(0)!.StartSlot, occupied);

        api.SetTeamUnit(_projectDir, Arena, 1, 0, team.Unit(0)!.WithStartSlot(occupied));

        var issues = api.Validate(_projectDir, Arena);
        var issue = Assert.Single(issues.Where(i => i.Code == EncounterValidationCodes.DuplicateStartSlot));
        // Information, not a warning: vanilla itself shares slots 4171 times.
        Assert.Equal(EncounterValidationSeverity.Info, issue.Severity);
    }

    [Fact]
    public void Validate_VanillaArenaHasDistinctStartSlots()
    {
        if (!Ready) return;

        var api = new EncountersApi();
        var team = api.Get(_projectDir, Arena)!.Team(1)!;

        var slots = team.ParsedUnits.Select(u => u!.StartSlot).ToArray();
        Assert.Equal(slots.Length, slots.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.DoesNotContain(api.Validate(_projectDir, Arena),
            i => i.Code == EncounterValidationCodes.DuplicateStartSlot);
    }
}
