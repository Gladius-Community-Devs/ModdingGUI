using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ModdingGUI.Core.Api;
using Xunit;

namespace ModdingGUI.Tests.Core.Api;

/// <summary>
/// Phase 4 APIs against staged corpus copies: schools (<c>data/school</c>), leagues
/// (<c>data/towns/leagues</c>) and flat config tables (<c>data/towns/shops</c>, <c>data/config</c>).
/// The byte-parity assertions are the important ones — they are what keep comments and
/// formatting intact.
/// </summary>
public sealed class Phase4ApisTests : IDisposable
{
    private readonly string _projectDir;

    public Phase4ApisTests()
    {
        _projectDir = Path.Combine(Path.GetTempPath(), "ModdingGUI.Phase4-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_projectDir);
        if (!CleanBecCorpus.IsAvailable) return;

        string root = CleanBecCorpus.Root!;
        CopyTree(Path.Combine(root, "data", "school"), Path.Combine(_projectDir, "data", "school"), "*.tok");
        CopyTree(Path.Combine(root, "data", "towns", "leagues"),
            Path.Combine(_projectDir, "data", "towns", "leagues"), "*.tok");
        CopyTree(Path.Combine(root, "data", "towns", "shops"),
            Path.Combine(_projectDir, "data", "towns", "shops"), "*.tok");
        // A small config subset, enough for the flat-table API.
        Directory.CreateDirectory(Path.Combine(_projectDir, "data", "config"));
        foreach (string name in new[] { "worldmap.tok", "fontdef.tok", "prizes.tok" })
        {
            string source = Path.Combine(root, "data", "config", name);
            if (File.Exists(source))
                File.Copy(source, Path.Combine(_projectDir, "data", "config", name), overwrite: true);
        }

        // League validation checks that each ENCFILE resolves, so the encounters must exist.
        CopyTree(Path.Combine(root, "data", "encounters"),
            Path.Combine(_projectDir, "data", "encounters"), "*.enc");
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_projectDir)) Directory.Delete(_projectDir, recursive: true); }
        catch (IOException) { /* best effort */ }
    }

    private static bool Ready => CleanBecCorpus.IsAvailable;

    private static void CopyTree(string source, string target, string pattern)
    {
        if (!Directory.Exists(source)) return;
        foreach (string file in Directory.EnumerateFiles(source, pattern, SearchOption.AllDirectories))
        {
            string destination = Path.Combine(target, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination, overwrite: true);
        }
    }

    private string FilePath(string relative) =>
        Path.Combine(_projectDir, relative.Replace('/', Path.DirectorySeparatorChar));

    private const string School = "data/school/ursulanordagh.tok";
    private const string Leagues = "data/towns/leagues/orinskeep_leagues.tok";
    private const string Shop = "data/towns/shops/shopakaranpro.tok";

    // ---- schools ------------------------------------------------------------

    [Fact]
    public void Schools_ListAndGet()
    {
        if (!Ready) return;

        var api = new SchoolsApi();
        Assert.Equal(74, api.ListFiles(_projectDir).Count);
        Assert.Contains("Orin's School", api.List(_projectDir));

        var school = api.Get(_projectDir, School);
        Assert.NotNull(school);
        Assert.Equal("Orin's School", school!.Name);
        Assert.Equal("Ursula", school.Hero);
        Assert.Equal(2500, school.Gold);
        Assert.Equal(2, school.Units.Count);
    }

    [Fact]
    public void Schools_ReadUnitStatsAndSkills()
    {
        if (!Ready) return;

        var school = new SchoolsApi().Get(_projectDir, School)!;
        var ursula = school.Unit("Ursula");

        Assert.NotNull(ursula);
        // CON, PWR, ACC, DEF, INI, MOVE — the fifth value is initiative, not intelligence.
        Assert.Equal(new[] { 5, 5, 10, 11, 10, 1 }, ursula!.CoreStats);
        Assert.Contains("Ursula Attack", ursula.Skills);
        Assert.Equal("Ursula", ursula.ClassName);
    }

    [Fact]
    public void Schools_SetUnitField_ThenRevert_IsByteIdentical()
    {
        if (!Ready) return;

        string path = FilePath(School);
        byte[] original = File.ReadAllBytes(path);
        var api = new SchoolsApi();

        api.SetUnitField(_projectDir, School, "Ursula", "LEVEL", "20");
        Assert.Equal("20", api.Get(_projectDir, School)!.Unit("Ursula")!.Value("LEVEL"));

        api.SetUnitField(_projectDir, School, "Ursula", "LEVEL", "1");
        Assert.Equal(original, File.ReadAllBytes(path));
    }

    [Fact]
    public void Schools_SetUnitSkills_ReplacesTheListExactly()
    {
        if (!Ready) return;

        string path = FilePath(School);
        byte[] original = File.ReadAllBytes(path);
        var api = new SchoolsApi();

        var before = api.Get(_projectDir, School)!.Unit("Ursula")!.Skills;
        api.SetUnitSkills(_projectDir, School, "Ursula", new[] { "Ursula Attack", "Ursula Evade" });

        var after = api.Get(_projectDir, School)!.Unit("Ursula")!.Skills;
        Assert.Equal(new[] { "Ursula Attack", "Ursula Evade" }, after);

        api.SetUnitSkills(_projectDir, School, "Ursula", before);
        Assert.Equal(before, api.Get(_projectDir, School)!.Unit("Ursula")!.Skills);
    }

    [Fact]
    public void Schools_Validate_EveryVanillaRoster_HasNoWarnings()
    {
        if (!Ready) return;

        var api = new SchoolsApi();
        var catalog = SchoolsCatalog.Shipped();
        Assert.NotNull(catalog);

        var warnings = new List<SchoolValidationIssue>();
        foreach (string file in api.ListFiles(_projectDir))
        {
            var school = api.Get(_projectDir, file);
            if (school is null) continue;
            warnings.AddRange(SchoolValidator.Validate(school, catalog)
                .Where(i => i.Severity == SchoolValidationSeverity.Warning));
        }

        Assert.True(warnings.Count == 0, string.Join("\n  ",
            warnings.Take(10).Select(w => $"{w.Code} {w.Unit}/{w.Key}: {w.Message}")));
    }

    // ---- leagues ------------------------------------------------------------

    [Fact]
    public void Leagues_ListAndParse()
    {
        if (!Ready) return;

        var api = new LeaguesApi();
        Assert.Equal(44, api.ListFiles(_projectDir).Count);
        Assert.Contains("OrinsKeepStoryLeague", api.List(_projectDir, Leagues));

        var file = api.Get(_projectDir, Leagues);
        Assert.NotNull(file);
        Assert.Equal("\"Orins Keep League Office\", 2004", file!.Value("OFFICENAME"));

        var story = file.League("OrinsKeepStoryLeague");
        Assert.NotNull(story);
        Assert.Equal(2, story!.Encounters.Count);
        Assert.Equal("Nordagh\\orinskeep\\Scene04U.enc", story.Encounter("Scene04U")!.EncounterFile!.Trim('"'));
    }

    [Fact]
    public void Leagues_SetEncounterFile_ThenRevert_IsByteIdentical()
    {
        if (!Ready) return;

        string path = FilePath(Leagues);
        byte[] original = File.ReadAllBytes(path);
        var api = new LeaguesApi();

        api.SetEncounterFile(_projectDir, Leagues, "OrinsKeepStoryLeague", "Scene04U",
            "data/encounters/Nordagh/thefen/sx_arenacert6.enc");

        var changed = api.Get(_projectDir, Leagues)!
            .League("OrinsKeepStoryLeague")!.Encounter("Scene04U")!.EncounterFile!.Trim('"');
        Assert.Equal("Nordagh\\thefen\\sx_arenacert6.enc", changed);

        api.SetEncounterFile(_projectDir, Leagues, "OrinsKeepStoryLeague", "Scene04U",
            "data/encounters/Nordagh/orinskeep/Scene04U.enc");

        Assert.Equal(original, File.ReadAllBytes(path));
    }

    [Fact]
    public void Leagues_SetEncounterField_OnlyTouchesThatBout()
    {
        if (!Ready) return;

        var api = new LeaguesApi();
        api.SetEncounterField(_projectDir, Leagues, "OrinsKeepStoryLeague", "Scene04U", "TEAMS", "2 2 3 0 0");

        var file = api.Get(_projectDir, Leagues)!;
        Assert.Equal("2 2 3 0 0", file.League("OrinsKeepStoryLeague")!.Encounter("Scene04U")!.Teams);
        Assert.Equal("1 1 1 0 0", file.League("OrinsKeepStoryLeague")!.Encounter("Scene06U")!.Teams);
    }

    [Fact]
    public void Leagues_AddThenRemoveBout_RestoresTheBoutCount()
    {
        if (!Ready) return;

        var api = new LeaguesApi();
        int before = api.Get(_projectDir, Leagues)!.League("KingdomOpen")!.Encounters.Count;

        api.AddEncounter(_projectDir, Leagues, "KingdomOpen", "TestBout", "9999", new[]
        {
            new LeagueFieldDto("ENCDESC", "9999"),
            new LeagueFieldDto("ENCFILE", "\"Nordagh\\thefen\\sx_arenacert6.enc\""),
            new LeagueFieldDto("TEAMS", "2 2 3 0 0"),
        });

        var added = api.Get(_projectDir, Leagues)!.League("KingdomOpen")!.Encounter("TestBout");
        Assert.NotNull(added);
        Assert.Equal("2 2 3 0 0", added!.Teams);

        api.RemoveEncounter(_projectDir, Leagues, "KingdomOpen", "TestBout");
        Assert.Equal(before, api.Get(_projectDir, Leagues)!.League("KingdomOpen")!.Encounters.Count);
    }

    /// <summary>
    /// Vanilla league bouts that point at an <c>.enc</c> the shipped corpus does not contain.
    /// The warning is correct — those bouts cannot load as shipped — so the gate pins them and
    /// fails if the set changes either way.
    /// </summary>
    private static readonly (string League, string Encounter)[] KnownMissingEncounters =
    {
        ("Imperia Wilderness Field League", "FieldTestBattle"),
        ("Mordares Den League", "Mordares Den Battle"),
        ("Nordagh Wilderness Forest League", "qstIrony"),
        ("Orins Keep League", "Orins Keep Battle"),
        ("Pirgos Arena League", "LightVersusHeavy"),
        ("RoanorTempleUrsulaStory", "Scene78ua"),
        ("RoanorTempleValensStory", "Scene78V"),
    };

    [Fact]
    public void Leagues_Validate_EveryVanillaFile_OnlyThePinnedWarnings()
    {
        if (!Ready) return;

        var api = new LeaguesApi();
        var warnings = new List<LeagueValidationIssue>();
        foreach (string file in api.ListFiles(_projectDir))
        {
            warnings.AddRange(api.Validate(_projectDir, file)
                .Where(i => i.Severity == LeagueValidationSeverity.Warning));
        }

        var pinned = KnownMissingEncounters.ToHashSet();
        var unexpected = warnings
            .Where(w => !(w.Code == LeagueValidationCodes.EncounterFileMissing
                          && pinned.Contains((w.League, w.Encounter!))))
            .ToList();

        Assert.True(unexpected.Count == 0, string.Join("\n  ",
            unexpected.Take(10).Select(w => $"{w.Code} {w.League}/{w.Encounter}: {w.Message}")));
        Assert.Equal(KnownMissingEncounters.Length, warnings.Count);
    }

    // ---- config tables ------------------------------------------------------

    [Fact]
    public void ConfigTable_ReadsShopHeader()
    {
        if (!Ready) return;

        var table = new ConfigTableApi().Get(_projectDir, Shop);

        Assert.NotNull(table);
        Assert.Equal("\"ShopAkarAnPro\"\t2213", table!.Value("NAME"));
        Assert.Equal("\"Nazin\"", table.Value("SHOPKEEPER"));
    }

    [Fact]
    public void ConfigTable_SetNoOpAndRevert_AreByteIdentical()
    {
        if (!Ready) return;

        string path = FilePath(Shop);
        byte[] original = File.ReadAllBytes(path);
        var api = new ConfigTableApi();

        string current = api.Get(_projectDir, Shop)!.Value("SHOPKEEPER")!;
        api.Set(_projectDir, Shop, "SHOPKEEPER", current);
        Assert.Equal(original, File.ReadAllBytes(path));

        api.Set(_projectDir, Shop, "SHOPKEEPER", "\"Someone Else\"");
        Assert.Equal("\"Someone Else\"", api.Get(_projectDir, Shop)!.Value("SHOPKEEPER"));
        api.Set(_projectDir, Shop, "SHOPKEEPER", current);
        Assert.Equal(original, File.ReadAllBytes(path));
    }

    [Fact]
    public void ConfigTable_AddThenRemove_RestoresTheFile()
    {
        if (!Ready) return;

        var api = new ConfigTableApi();
        api.Add(_projectDir, Shop, "TESTKEY", "\"value\"");
        Assert.Equal("\"value\"", api.Get(_projectDir, Shop)!.Value("TESTKEY"));

        api.Remove(_projectDir, Shop, "TESTKEY");
        Assert.Null(api.Get(_projectDir, Shop)!.Value("TESTKEY"));
    }

    [Fact]
    public void ConfigTable_ListFiles_FindsShopsAndConfigTables()
    {
        if (!Ready) return;

        var api = new ConfigTableApi();
        Assert.Equal(59, api.ListFiles(_projectDir, "data/towns/shops").Count);
        Assert.NotEmpty(api.ListFiles(_projectDir, "data/config"));
    }
}
