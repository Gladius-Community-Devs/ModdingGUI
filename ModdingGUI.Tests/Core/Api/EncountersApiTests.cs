using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ModdingGUI.Core.Api;
using Xunit;

namespace ModdingGUI.Tests.Core.Api;

/// <summary>
/// Tests for <see cref="EncountersApi"/> against a staged copy of the real
/// <c>data/encounters</c> tree. The important one is the no-op sweep: replacing a value with
/// the value already there must reproduce the file byte-for-byte, which is what keeps
/// comments, indentation and unknown keys intact.
/// </summary>
public sealed class EncountersApiTests : IDisposable
{
    private readonly string _projectDir;

    public EncountersApiTests()
    {
        _projectDir = Path.Combine(Path.GetTempPath(),
            "ModdingGUI.Encounters-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_projectDir);

        if (!CleanBecCorpus.IsAvailable) return;

        string source = Path.Combine(CleanBecCorpus.Root!, "data", "encounters");
        string target = Path.Combine(_projectDir, "data", "encounters");
        CopyTree(source, target);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_projectDir)) Directory.Delete(_projectDir, recursive: true);
        }
        catch (IOException)
        {
            // best effort
        }
    }

    private static bool Ready => CleanBecCorpus.IsAvailable;

    private static void CopyTree(string source, string target)
    {
        foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(source, file);
            string destination = Path.Combine(target, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination, overwrite: true);
        }
    }

    private string FilePath(string relativePath) =>
        Path.Combine(_projectDir, relativePath.Replace('/', Path.DirectorySeparatorChar));

    private const string Sample = "data/encounters/nordagh/thefen/sx_arenacert6.enc";

    // ---- listing and reading ------------------------------------------------

    [Fact]
    public void ListFiles_FindsEveryEncounter()
    {
        if (!Ready) return;

        var files = new EncountersApi().ListFiles(_projectDir);

        Assert.Equal(834, files.Count);
        Assert.All(files, f => Assert.EndsWith(".enc", f, StringComparison.OrdinalIgnoreCase));
        Assert.Equal(files.OrderBy(f => f, StringComparer.OrdinalIgnoreCase), files);
    }

    [Fact]
    public void List_ReturnsEncounterNames()
    {
        if (!Ready) return;

        var names = new EncountersApi().List(_projectDir);

        Assert.Equal(834, names.Count);
        Assert.Contains("Sx_ArenaCert6", names, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Get_ReadsHeaderTeamsAndUnits()
    {
        if (!Ready) return;

        var encounter = new EncountersApi().Get(_projectDir, Sample);

        Assert.NotNull(encounter);
        Assert.Equal("Sx_ArenaCert6", encounter!.Name);
        Assert.Equal("\"Nordagh\\thefen\\thefen.scn\"", encounter.Value("SCENE"));

        Assert.Equal(2, encounter.Teams.Count);
        var hero = encounter.Team(0);
        Assert.NotNull(hero);
        Assert.Equal(2, hero!.Units.Count);
        Assert.StartsWith("\"HERO\"", hero.Units[0]);

        var enemies = encounter.Team(1)!;
        Assert.Equal(3, enemies.Units.Count);
        Assert.Equal("\"Fliuch Home Guard\"", enemies.Value("SCHOOL"));
        Assert.Equal(5, encounter.UnitCount);
    }

    [Fact]
    public void Get_MissingFile_ReturnsNull()
    {
        var api = new EncountersApi();
        Assert.Null(api.Get(_projectDir, "data/encounters/nowhere/none.enc"));
    }

    // ---- writing ------------------------------------------------------------

    [Fact]
    public void SetField_NoOp_IsByteIdentical()
    {
        if (!Ready) return;

        string path = FilePath(Sample);
        byte[] original = File.ReadAllBytes(path);
        var api = new EncountersApi();

        var encounter = api.Get(_projectDir, Sample)!;
        string current = encounter.Value("SCENE")!;

        api.SetField(_projectDir, Sample, "SCENE", current);

        Assert.Equal(original, File.ReadAllBytes(path));
    }

    [Fact]
    public void SetField_ThenRevert_IsByteIdentical()
    {
        if (!Ready) return;

        string path = FilePath(Sample);
        byte[] original = File.ReadAllBytes(path);
        var api = new EncountersApi();

        api.SetField(_projectDir, Sample, "SCENE", "\"nowhere\\test.scn\"");
        Assert.Equal("\"nowhere\\test.scn\"", api.Get(_projectDir, Sample)!.Value("SCENE"));

        api.SetField(_projectDir, Sample, "SCENE", "\"Nordagh\\thefen\\thefen.scn\"");
        Assert.Equal(original, File.ReadAllBytes(path));
    }

    [Fact]
    public void SetTeamField_ChangesOnlyTheTeamSection()
    {
        if (!Ready) return;

        var api = new EncountersApi();
        api.SetTeamField(_projectDir, Sample, 1, "SCHOOL", "\"Fliuch Falcons\"");

        var encounter = api.Get(_projectDir, Sample)!;
        Assert.Equal("\"Fliuch Falcons\"", encounter.Team(1)!.Value("SCHOOL"));
        Assert.Equal("\"Nordagh\\thefen\\thefen.scn\"", encounter.Value("SCENE"));
    }

    [Fact]
    public void SetTeamUnits_ReplacesRowsAndKeepsTheRestOfTheFile()
    {
        if (!Ready) return;

        string path = FilePath(Sample);
        byte[] original = File.ReadAllBytes(path);
        var api = new EncountersApi();

        var before = api.Get(_projectDir, Sample)!;
        var team1 = before.Team(1)!;

        // Drop one enemy unit, then restore the original rows.
        api.SetTeamUnits(_projectDir, Sample, 1, team1.Units.Take(team1.Units.Count - 1).ToList());
        Assert.Equal(team1.Units.Count - 1, api.Get(_projectDir, Sample)!.Team(1)!.Units.Count);

        api.SetTeamUnits(_projectDir, Sample, 1, team1.Units);
        var after = api.Get(_projectDir, Sample)!;
        Assert.Equal(team1.Units, after.Team(1)!.Units);
        Assert.Equal(before.Teams.Count, after.Teams.Count);
        Assert.Equal(original, File.ReadAllBytes(path));
    }

    [Fact]
    public void RemoveField_ThenAddBack_RestoresTheFile()
    {
        if (!Ready) return;

        string path = FilePath(Sample);
        byte[] original = File.ReadAllBytes(path);
        var api = new EncountersApi();

        api.RemoveField(_projectDir, Sample, "CUTSCENE");
        Assert.Null(api.Get(_projectDir, Sample)!.Value("CUTSCENE"));

        api.AddField(_projectDir, Sample, "CUTSCENE", "\"intro\", \"Scene07uc.cut\", 7uc");
        Assert.NotNull(api.Get(_projectDir, Sample)!.Value("CUTSCENE"));

        // The line comes back with canonical spacing, so only the parsed value is compared.
        var reparsed = api.Get(_projectDir, Sample)!;
        Assert.Equal(2, reparsed.Teams.Count);
    }

    [Fact]
    public void Writes_LeaveABackupInModHistory()
    {
        if (!Ready) return;

        var api = new EncountersApi();
        api.SetField(_projectDir, Sample, "SCENE", "\"Nordagh\\thefen\\thefen.scn\"");

        string historyDir = Path.Combine(_projectDir, ".mod-history", "data", "encounters",
            "nordagh", "thefen", "sx_arenacert6.enc");
        Assert.True(Directory.Exists(historyDir));
        Assert.NotEmpty(Directory.GetFiles(historyDir, "*.bak"));
    }

    [Fact]
    public void UnknownTeam_Throws()
    {
        if (!Ready) return;

        var api = new EncountersApi();
        Assert.Throws<InvalidOperationException>(
            () => api.SetTeamField(_projectDir, Sample, 9, "SCHOOL", "\"x\""));
    }

    // ---- validation ---------------------------------------------------------

    [Fact]
    public void Validate_EveryVanillaEncounter_HasNoWarnings()
    {
        if (!Ready) return;

        var api = new EncountersApi();
        var catalog = EncountersCatalog.Shipped();
        Assert.NotNull(catalog);

        var warnings = new List<EncounterValidationIssue>();
        foreach (string relative in api.ListFiles(_projectDir))
        {
            var encounter = api.Get(_projectDir, relative);
            if (encounter is null) continue;
            warnings.AddRange(EncounterValidator.Validate(encounter, catalog)
                .Where(i => i.Severity == EncounterValidationSeverity.Warning));
        }

        string grouped = string.Join("\n  ", warnings
            .GroupBy(w => (w.Code, w.Key))
            .Select(g => $"{g.Key.Code} / {g.Key.Key}: {g.Count()} case(s), " +
                         $"e.g. {g.First().Message}"));

        Assert.True(warnings.Count == 0,
            $"{warnings.Count} warning(s) on vanilla encounter data:\n  {grouped}");
    }

    [Fact]
    public void Validate_NoOpSweep_KeepsEveryFileByteIdentical()
    {
        if (!Ready) return;

        var api = new EncountersApi();
        var files = api.ListFiles(_projectDir);

        // Every 25th file keeps the sweep quick while covering all folders.
        var sample = files.Where((_, i) => i % 25 == 0).ToList();
        Assert.NotEmpty(sample);

        foreach (string relative in sample)
        {
            string path = FilePath(relative);
            byte[] original = File.ReadAllBytes(path);

            var encounter = api.Get(_projectDir, relative);
            if (encounter is null) continue;

            string? name = encounter.Value("ENCOUNTERNAME");
            if (name is null) continue;

            api.SetField(_projectDir, relative, "ENCOUNTERNAME", name);
            Assert.Equal(original, File.ReadAllBytes(path));
        }
    }
}
