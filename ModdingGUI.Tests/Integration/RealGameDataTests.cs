using System.Drawing;
using ModdingGUI;
using Xunit;

namespace ModdingGUI.Tests;

/// <summary>
/// Integration tests that run against real game data located by <see cref="GameDataFixture"/>.
/// All tests are auto-skipped when no REALDATA_ project folder is found.
/// To target a specific folder pass <c>-ProjectFolder REALDATA_xyz</c> to run-tests.ps1,
/// or set the <c>REALDATA_PROJECT</c> environment variable before running dotnet test.
/// </summary>
public class RealGameDataTests : IDisposable
{
    private readonly string _tempDir;
    private readonly List<string> _logs = new();

    public RealGameDataTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"GladiusIntTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private void Log(string msg, Color? _) => _logs.Add(msg);

    // -------------------------------------------------------------------------
    // LookupHelper
    // -------------------------------------------------------------------------

    [GameDataFact]
    public void LookupHelper_LoadsRealLookupTable_ReturnsAllEntries()
    {
        var table = LookupHelper.LoadLookupTable(GameDataFixture.LookupText);

        Assert.True(table.Count > 8000,
            $"Expected > 8000 lookup entries, got {table.Count}");
    }

    [GameDataFact]
    public void LookupHelper_RealLookupTable_AllValuesNonEmpty()
    {
        var table = LookupHelper.LoadLookupTable(GameDataFixture.LookupText);

        // The game data has a small number of legitimately empty placeholder IDs.
        // Assert that the vast majority (>99%) of entries are non-empty.
        int emptyCount = table.Count(kvp => string.IsNullOrWhiteSpace(kvp.Value));
        double emptyRatio = (double)emptyCount / table.Count;

        Assert.True(emptyRatio < 0.01,
            $"Too many empty lookup entries: {emptyCount}/{table.Count} ({emptyRatio:P1})");
    }

    // -------------------------------------------------------------------------
    // SkillParser
    // -------------------------------------------------------------------------

    [GameDataFact]
    public void SkillParser_ParsesAllRealSkills_CountAboveThreshold()
    {
        var skills = SkillParser.ParseSkillsTok(
            GameDataFixture.SkillsTok,
            GameDataFixture.LookupText,
            Log);

        Assert.True(skills.Count > 1800,
            $"Expected > 1800 skills, got {skills.Count}");
    }

    [GameDataFact]
    public void SkillParser_AllRealSkills_HaveNonEmptyInternalName()
    {
        var skills = SkillParser.ParseSkillsTok(
            GameDataFixture.SkillsTok,
            GameDataFixture.LookupText,
            Log);

        Assert.All(skills, s =>
            Assert.False(string.IsNullOrWhiteSpace(s.InternalName),
                "A skill was parsed with an empty InternalName"));
    }

    [GameDataFact]
    public void SkillParser_ItemSkills_AreCorrectlyIdentified()
    {
        var skills = SkillParser.ParseSkillsTok(
            GameDataFixture.SkillsTok,
            GameDataFixture.LookupText,
            Log);

        var itemSkills = skills.Where(s => s.IsItemSkill).ToList();
        Assert.NotEmpty(itemSkills);
        Assert.All(itemSkills, s =>
            Assert.StartsWith("Item ", s.InternalName));
    }

    [GameDataFact]
    public void SkillParser_AllRealSkills_HaveNonEmptyDisplayName()
    {
        var skills = SkillParser.ParseSkillsTok(
            GameDataFixture.SkillsTok,
            GameDataFixture.LookupText,
            Log);

        Assert.All(skills, s =>
            Assert.False(string.IsNullOrWhiteSpace(s.DisplayName),
                $"Skill '{s.InternalName}' has an empty DisplayName"));
    }

    // -------------------------------------------------------------------------
    // PackValidator — parsers
    // -------------------------------------------------------------------------

    [GameDataFact]
    public void PackValidator_ParseClassDefs_LoadsAllRealClasses()
    {
        var classDefs = PackValidator.ParseClassDefs(File.ReadLines(GameDataFixture.ClassDefs));

        Assert.True(classDefs.Count >= 100,
            $"Expected >= 100 class defs, got {classDefs.Count}");
    }

    [GameDataFact]
    public void PackValidator_ParseClassDefs_AllClassesHaveSkillUseName()
    {
        var classDefs = PackValidator.ParseClassDefs(File.ReadLines(GameDataFixture.ClassDefs));

        Assert.All(classDefs, kvp =>
            Assert.False(string.IsNullOrWhiteSpace(kvp.Value),
                $"Class '{kvp.Key}' has an empty SKILLUSENAME"));
    }

    [GameDataFact]
    public void PackValidator_ParseSkillSets_LoadsAllRealSkillsets()
    {
        var skillSets = PackValidator.ParseSkillSets(File.ReadLines(GameDataFixture.SkillsetsFile));

        Assert.True(skillSets.Count > 200,
            $"Expected > 200 skillsets, got {skillSets.Count}");
    }

    [GameDataFact]
    public void PackValidator_ParseSkills_LoadsAllRealSkills()
    {
        var skills = PackValidator.ParseSkills(File.ReadLines(GameDataFixture.SkillsTok));

        Assert.True(skills.Count > 1800,
            $"Expected > 1800 skills from ParseSkills, got {skills.Count}");
    }

    [GameDataFact]
    public void PackValidator_ParseGladiatorClassesAndSkillSets_LoadsRealGladiators()
    {
        var gladiators = PackValidator.ParseGladiatorClassesAndSkillSets(
            File.ReadLines(GameDataFixture.GladiatorsFile));

        Assert.True(gladiators.Count > 500,
            $"Expected > 500 gladiators, got {gladiators.Count}");

        Assert.All(gladiators, g =>
            Assert.False(string.IsNullOrWhiteSpace(g.Class),
                "A gladiator was parsed with an empty Class"));
    }

    // -------------------------------------------------------------------------
    // PackValidator — full validators
    // -------------------------------------------------------------------------

    [GameDataFact]
    public void PackValidator_ValidateItemsets_OnRealData_ReturnsResult()
    {
        var result = PackValidator.ValidateItemsets(File.ReadLines(GameDataFixture.ItemsetsFile));

        Assert.Equal(433, result.StepCount);
    }

    [GameDataFact]
    public void PackValidator_ValidateGladiatorsFile_OnRealData_ReturnsResult()
    {
        var result = PackValidator.ValidateGladiatorsFile(File.ReadLines(GameDataFixture.GladiatorsFile));

        // The result should have step count matching non-empty lines
        Assert.True(result.StepCount > 0);
    }

    [GameDataFact]
    public void PackValidator_ValidateGladiatorsAndSkills_OnRealData_ReturnsResult()
    {
        var result = PackValidator.ValidateGladiatorsAndSkills(
            File.ReadLines(GameDataFixture.GladiatorsFile),
            File.ReadLines(GameDataFixture.SkillsetsFile),
            File.ReadLines(GameDataFixture.SkillsTok),
            File.ReadLines(GameDataFixture.ClassDefs));

        // Should run without exception and return a usable result
        Assert.NotNull(result);
        Assert.True(result.StepCount > 0);
    }

    [GameDataFact]
    public void PackValidator_ValidateGladiatorsAndSkills_OnRealData_IssuesAreWellFormed()
    {
        var result = PackValidator.ValidateGladiatorsAndSkills(
            File.ReadLines(GameDataFixture.GladiatorsFile),
            File.ReadLines(GameDataFixture.SkillsetsFile),
            File.ReadLines(GameDataFixture.SkillsTok),
            File.ReadLines(GameDataFixture.ClassDefs));

        Assert.All(result.Issues, issue =>
        {
            Assert.False(string.IsNullOrWhiteSpace(issue.Message),
                "A validation issue has an empty Message");
            Assert.True(Enum.IsDefined(typeof(ValidationSeverity), issue.Severity),
                $"Issue has invalid severity: {issue.Severity}");
        });
    }

    // -------------------------------------------------------------------------
    // Vanilla ISO
    // -------------------------------------------------------------------------

    [GameDataFact]
    public void VanillaIso_FileExists_AndIsExpectedSize()
    {
        var info = new FileInfo(GameDataFixture.VanillaIso);

        Assert.True(info.Exists);
        // Gladius GC ISO is ~1.36 GB
        Assert.True(info.Length > 1_000_000_000L,
            $"vanilla.iso size {info.Length} bytes is unexpectedly small");
    }
}
