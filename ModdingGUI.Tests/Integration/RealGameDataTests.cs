using System.Drawing;
using ModdingGUI;
using Xunit;

namespace ModdingGUI.Tests;

/// <summary>
/// Integration tests that run against real game data located by <see cref="GameDataFixture"/>.
/// The fixture is generated from the vanilla ISO by <c>setup-realdata.ps1</c> and byte-verified
/// against <c>clean-bec-unpacked</c>, so every count/assertion here is pinned to the exact
/// certified vanilla values (NOT a threshold). Modified-data expectations belong in later,
/// separate tests.
///
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

        Assert.Equal(8156, table.Count);
    }

    [GameDataFact]
    public void LookupHelper_RealLookupTable_AllValuesNonEmpty()
    {
        var table = LookupHelper.LoadLookupTable(GameDataFixture.LookupText);

        int emptyCount = table.Count(kvp => string.IsNullOrWhiteSpace(kvp.Value));

        // Vanilla has exactly zero empty lookup values.
        Assert.Equal(0, emptyCount);
    }

    // -------------------------------------------------------------------------
    // SkillParser
    // -------------------------------------------------------------------------

    [GameDataFact]
    public void SkillParser_ParsesAllRealSkills_CountMatchesVanilla()
    {
        var skills = SkillParser.ParseSkillsTok(
            GameDataFixture.SkillsTok,
            GameDataFixture.LookupText,
            Log);

        Assert.Equal(1811, skills.Count);
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
        Assert.Equal(55, itemSkills.Count);
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

        Assert.Equal(127, classDefs.Count);
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

        Assert.Equal(230, skillSets.Count);
    }

    [GameDataFact]
    public void PackValidator_ParseSkills_LoadsAllRealSkills()
    {
        var skills = PackValidator.ParseSkills(File.ReadLines(GameDataFixture.SkillsTok));

        Assert.Equal(1811, skills.Count);
    }

    [GameDataFact]
    public void PackValidator_ParseGladiatorClassesAndSkillSets_LoadsRealGladiators()
    {
        var gladiators = PackValidator.ParseGladiatorClassesAndSkillSets(
            File.ReadLines(GameDataFixture.GladiatorsFile));

        Assert.Equal(2169, gladiators.Count);

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

        // StepCount counts "Itemset N:" header lines. Vanilla has 404 (IDs 0-403).
        Assert.Equal(404, result.StepCount);
    }

    [GameDataFact]
    public void PackValidator_ValidateGladiatorsFile_OnRealData_ReturnsResult()
    {
        var result = PackValidator.ValidateGladiatorsFile(File.ReadLines(GameDataFixture.GladiatorsFile));

        // StepCount counts non-empty lines in gladiators.txt. Vanilla has 19531.
        Assert.Equal(19531, result.StepCount);
    }

    [GameDataFact]
    public void PackValidator_ValidateGladiatorsAndSkills_OnRealData_ReturnsResult()
    {
        var result = PackValidator.ValidateGladiatorsAndSkills(
            File.ReadLines(GameDataFixture.GladiatorsFile),
            File.ReadLines(GameDataFixture.SkillsetsFile),
            File.ReadLines(GameDataFixture.SkillsTok),
            File.ReadLines(GameDataFixture.ClassDefs));

        // StepCount = parsed gladiators + their skill-set skill counts. Vanilla = 50070.
        Assert.NotNull(result);
        Assert.Equal(50070, result.StepCount);
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
        // Gladius GC vanilla ISO is exactly 1,459,978,240 bytes.
        Assert.Equal(1_459_978_240L, info.Length);
    }
}
