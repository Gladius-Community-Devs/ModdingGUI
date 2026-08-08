using ModdingGUI;
using Xunit;

namespace ModdingGUI.Tests;

/// <summary>
/// Tests for <see cref="PackValidator"/> and its internal parsers.
/// All input is passed as in-memory string arrays — no file I/O.
/// </summary>
public class PackValidatorTests
{
    // -------------------------------------------------------------------------
    // ValidateItemsets
    // -------------------------------------------------------------------------

    [Fact]
    public void ValidateItemsets_FullCoverage_IsValid()
    {
        var lines = new[]
        {
            "Itemset 1:",
            "1  99  SomeSword  [Weapon]"
        };
        var result = PackValidator.ValidateItemsets(lines);
        Assert.True(result.IsValid);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void ValidateItemsets_GapInMiddle_ReportsError()
    {
        var lines = new[]
        {
            "Itemset 1:",
            "1  10  SomeSword  [Weapon]",
            "20  99  BigSword   [Weapon]"
        };
        var result = PackValidator.ValidateItemsets(lines);
        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, i => i.Message.Contains("11") && i.Message.Contains("19"));
    }

    [Fact]
    public void ValidateItemsets_MissingTailCoverage_ReportsGapToLevel99()
    {
        var lines = new[]
        {
            "Itemset 1:",
            "1  50  SomeSword  [Weapon]"
        };
        var result = PackValidator.ValidateItemsets(lines);
        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, i => i.Message.Contains("99"));
    }

    [Fact]
    public void ValidateItemsets_ItemsetWithNoWeapons_IsSkipped()
    {
        var lines = new[]
        {
            "Itemset 1:",
            "1  5  ArmourPiece  [Armor]"
        };
        var result = PackValidator.ValidateItemsets(lines);
        Assert.True(result.IsValid);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void ValidateItemsets_MultipleItemsets_OnlyReportsFailingOne()
    {
        var lines = new[]
        {
            "Itemset 1:",
            "1  99  SomeSword  [Weapon]",
            "Itemset 2:",
            "1  50  BigSword   [Weapon]"
        };
        var result = PackValidator.ValidateItemsets(lines);
        Assert.False(result.IsValid);
        Assert.All(result.Issues, i => Assert.Contains("Itemset 2", i.Message));
    }

    [Fact]
    public void ValidateItemsets_StepCount_EqualsNumberOfItemsets()
    {
        var lines = new[]
        {
            "Itemset 1:",
            "1  99  SomeSword  [Weapon]",
            "Itemset 2:",
            "1  99  BigSword   [Weapon]"
        };
        var result = PackValidator.ValidateItemsets(lines);
        Assert.Equal(2, result.StepCount);
    }

    [Fact]
    public void ValidateItemsets_ErrorsHaveSeverityError()
    {
        var lines = new[]
        {
            "Itemset 1:",
            "1  50  Sword  [Weapon]"
        };
        var result = PackValidator.ValidateItemsets(lines);
        Assert.All(result.Issues, i => Assert.Equal(ValidationSeverity.Error, i.Severity));
    }

    // -------------------------------------------------------------------------
    // ValidateGladiatorsFile
    // -------------------------------------------------------------------------

    [Fact]
    public void ValidateGladiatorsFile_WellFormedFile_IsValid()
    {
        var lines = new[]
        {
            "Name: Valens",
            "Class: Secutor",
            "Outfit: 0",
            "Affinity: None",
            "Tint set: 1",
            "Skill set: 0",
            "Stat set: 0",
            "Item set: 0",
            "School: Gladiator"
        };
        var result = PackValidator.ValidateGladiatorsFile(lines);
        Assert.True(result.IsValid);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void ValidateGladiatorsFile_EmptyValueField_ReportsError()
    {
        var lines = new[] { "Class: " };
        var result = PackValidator.ValidateGladiatorsFile(lines);
        Assert.False(result.IsValid);
        Assert.Single(result.Issues);
    }

    [Fact]
    public void ValidateGladiatorsFile_UnknownFieldPattern_ReportsError()
    {
        var lines = new[] { "RandomJunk 123" };
        var result = PackValidator.ValidateGladiatorsFile(lines);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void ValidateGladiatorsFile_BlankLines_AreIgnored()
    {
        var lines = new[] { "", "   ", "Name: Valens" };
        var result = PackValidator.ValidateGladiatorsFile(lines);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateGladiatorsFile_StepCount_ExcludesBlankLines()
    {
        var lines = new[] { "", "Name: Valens", "Class: Secutor" };
        var result = PackValidator.ValidateGladiatorsFile(lines);
        Assert.Equal(2, result.StepCount);
    }

    // -------------------------------------------------------------------------
    // ValidateGladiatorsAndSkills
    // -------------------------------------------------------------------------

    [Fact]
    public void ValidateGladiatorsAndSkills_MatchingSkillUseClass_IsValid()
    {
        var gladiatorLines = new[]
        {
            "Name: Valens",
            "Class: Secutor",
            "Skill set: 1",
            "Name: EndTrigger"
        };
        var skillsetLines = new[]
        {
            "Skillset 1:",
            "0  1  Slash"
        };
        var skillsTokLines = new[]
        {
            "SKILLCREATE: \"Slash\",",
            "\tSKILLUSECLASS: \"Secutor\""
        };
        var classDefLines = new[]
        {
            "CREATECLASS: Secutor",
            "\tSKILLUSENAME: Secutor"
        };

        var result = PackValidator.ValidateGladiatorsAndSkills(
            gladiatorLines, skillsetLines, skillsTokLines, classDefLines);

        Assert.True(result.IsValid);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void ValidateGladiatorsAndSkills_MismatchedSkillUseClass_ReportsError()
    {
        var gladiatorLines = new[]
        {
            "Name: Valens",
            "Class: Secutor",
            "Skill set: 1",
            "Name: EndTrigger"
        };
        var skillsetLines = new[]
        {
            "Skillset 1:",
            "0  1  Slash"
        };
        var skillsTokLines = new[]
        {
            "SKILLCREATE: \"Slash\",",
            "\tSKILLUSECLASS: \"Murmillo\""
        };
        var classDefLines = new[]
        {
            "CREATECLASS: Secutor",
            "\tSKILLUSENAME: Secutor"
        };

        var result = PackValidator.ValidateGladiatorsAndSkills(
            gladiatorLines, skillsetLines, skillsTokLines, classDefLines);

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, i => i.Message.Contains("Slash") && i.Message.Contains("Secutor"));
    }

    [Fact]
    public void ValidateGladiatorsAndSkills_MissingClassDef_ReportsError()
    {
        var gladiatorLines = new[]
        {
            "Name: Valens",
            "Class: Secutor",
            "Skill set: 1",
            "Name: EndTrigger"
        };
        var skillsetLines = new[] { "Skillset 1:", "0  1  Slash" };
        var skillsTokLines = new[] { "SKILLCREATE: \"Slash\",", "\tSKILLUSECLASS: \"Secutor\"" };
        var classDefLines = Array.Empty<string>();

        var result = PackValidator.ValidateGladiatorsAndSkills(
            gladiatorLines, skillsetLines, skillsTokLines, classDefLines);

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, i => i.Message.Contains("SKILLUSENAME") && i.Message.Contains("Secutor"));
    }

    [Fact]
    public void ValidateGladiatorsAndSkills_SubSkillInSet_SkipsUseClassCheck()
    {
        var gladiatorLines = new[]
        {
            "Name: Valens",
            "Class: Secutor",
            "Skill set: 1",
            "Name: EndTrigger"
        };
        var skillsetLines = new[] { "Skillset 1:", "0  1  SubSlash" };
        var skillsTokLines = Array.Empty<string>();
        var classDefLines = new[] { "CREATECLASS: Secutor", "\tSKILLUSENAME: Secutor" };

        var result = PackValidator.ValidateGladiatorsAndSkills(
            gladiatorLines, skillsetLines, skillsTokLines, classDefLines);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateGladiatorsAndSkills_MissingClassDefWithSubSkill_IsValid()
    {
        var gladiatorLines = new[]
        {
            "Name: Valens",
            "Class: Unknown",
            "Skill set: 1",
            "Name: EndTrigger"
        };
        var skillsetLines = new[] { "Skillset 1:", "0  1  SubSlash" };
        var skillsTokLines = Array.Empty<string>();
        var classDefLines = Array.Empty<string>();

        var result = PackValidator.ValidateGladiatorsAndSkills(
            gladiatorLines, skillsetLines, skillsTokLines, classDefLines);

        Assert.True(result.IsValid);
    }

    // -------------------------------------------------------------------------
    // ParseGladiatorClassesAndSkillSets
    // -------------------------------------------------------------------------

    [Fact]
    public void ParseGladiatorClassesAndSkillSets_ParsesClassAndSkillSetId()
    {
        var lines = new[]
        {
            "Name: Valens",
            "Class: Secutor",
            "Skill set: 3",
            "Name: EndTrigger"  // triggers the commit of the Valens block
        };
        var result = PackValidator.ParseGladiatorClassesAndSkillSets(lines);
        Assert.Single(result);
        Assert.Equal("Secutor", result[0].Class);
        Assert.Equal(3, result[0].SkillSetId);
    }

    [Fact]
    public void ParseGladiatorClassesAndSkillSets_ParsesMultipleGladiators()
    {
        var lines = new[]
        {
            "Name: Valens", "Class: Secutor", "Skill set: 1",
            "Name: Ursula",    // triggers commit of Valens block
            "Class: Samnite", "Skill set: 2",
            "Name: EndTrigger" // triggers commit of Ursula block
        };
        var result = PackValidator.ParseGladiatorClassesAndSkillSets(lines);
        Assert.Equal(2, result.Count);
    }

    // -------------------------------------------------------------------------
    // ParseClassDefs
    // -------------------------------------------------------------------------

    [Fact]
    public void ParseClassDefs_ParsesClassToSkillUseName()
    {
        var lines = new[]
        {
            "CREATECLASS: Secutor",
            "\tSKILLUSENAME: Secutor"
        };
        var result = PackValidator.ParseClassDefs(lines);
        Assert.True(result.ContainsKey("Secutor"));
        Assert.Equal("Secutor", result["Secutor"]);
    }

    [Fact]
    public void ParseClassDefs_IsCaseInsensitiveOnLookup()
    {
        var lines = new[] { "CREATECLASS: Secutor", "\tSKILLUSENAME: Secutor" };
        var result = PackValidator.ParseClassDefs(lines);
        Assert.True(result.ContainsKey("secutor"));
    }

    [Fact]
    public void ParseClassDefs_StripsQuotesFromClassName()
    {
        var lines = new[] { "CREATECLASS: \"Secutor\"", "\tSKILLUSENAME: \"Secutor\"" };
        var result = PackValidator.ParseClassDefs(lines);
        Assert.True(result.ContainsKey("Secutor"));
    }

    [Fact]
    public void ParseClassDefs_MultipleClasses_AllParsed()
    {
        var lines = new[]
        {
            "CREATECLASS: Secutor",
            "\tSKILLUSENAME: Secutor",
            "CREATECLASS: Murmillo",
            "\tSKILLUSENAME: Murmillo"
        };
        var result = PackValidator.ParseClassDefs(lines);
        Assert.Equal(2, result.Count);
    }

    // -------------------------------------------------------------------------
    // ParseSkillSets
    // -------------------------------------------------------------------------

    [Fact]
    public void ParseSkillSets_ParsesSkillNamesForId()
    {
        var lines = new[] { "Skillset 5:", "0  10  Slash", "11  20  Burn" };
        var result = PackValidator.ParseSkillSets(lines);
        Assert.True(result.ContainsKey(5));
        Assert.Equal(2, result[5].Count);
        Assert.Contains("Slash", result[5]);
        Assert.Contains("Burn", result[5]);
    }

    [Fact]
    public void ParseSkillSets_MultipleSkillsets_AllParsed()
    {
        var lines = new[]
        {
            "Skillset 1:", "0  1  Slash",
            "Skillset 2:", "0  1  Burn"
        };
        var result = PackValidator.ParseSkillSets(lines);
        Assert.Equal(2, result.Count);
    }

    // -------------------------------------------------------------------------
    // ParseSkills
    // -------------------------------------------------------------------------

    [Fact]
    public void ParseSkills_ParsesSkillUseClasses()
    {
        var lines = new[]
        {
            "SKILLCREATE: \"Slash\",",
            "\tSKILLUSECLASS: \"Secutor\"",
            "\tSKILLUSECLASS: \"Murmillo\""
        };
        var result = PackValidator.ParseSkills(lines);
        Assert.True(result.ContainsKey("Slash"));
        Assert.Equal(2, result["Slash"].Count);
    }

    [Fact]
    public void ParseSkills_IsCaseInsensitiveOnSkillName()
    {
        var lines = new[] { "SKILLCREATE: \"Slash\",", "\tSKILLUSECLASS: \"Secutor\"" };
        var result = PackValidator.ParseSkills(lines);
        Assert.True(result.ContainsKey("slash"));
    }

    // -------------------------------------------------------------------------
    // IsSkillUseClassValid
    // -------------------------------------------------------------------------

    [Fact]
    public void IsSkillUseClassValid_SameClass_ReturnsTrue()
    {
        Assert.True(PackValidator.IsSkillUseClassValid("Secutor", "Secutor"));
    }

    [Fact]
    public void IsSkillUseClassValid_DifferentClass_ReturnsFalse()
    {
        Assert.False(PackValidator.IsSkillUseClassValid("Secutor", "Murmillo"));
    }

    [Fact]
    public void IsSkillUseClassValid_IsCaseInsensitive()
    {
        Assert.True(PackValidator.IsSkillUseClassValid("Secutor", "secutor"));
    }

    [Theory]
    [InlineData("Urlan")]
    [InlineData("Barbarian")]
    [InlineData("Gungnir")]
    [InlineData("BerserkerEnraged")]
    public void IsSkillUseClassValid_SpecialClasses_AcceptBeastUseClasses(string skillUseName)
    {
        Assert.True(PackValidator.IsSkillUseClassValid(skillUseName, "Bear"));
        Assert.True(PackValidator.IsSkillUseClassValid(skillUseName, "Cat"));
        Assert.True(PackValidator.IsSkillUseClassValid(skillUseName, "Wolf"));
        Assert.True(PackValidator.IsSkillUseClassValid(skillUseName, "Scarab"));
    }

    [Theory]
    [InlineData("Urlan")]
    [InlineData("Barbarian")]
    public void IsSkillUseClassValid_SpecialClasses_RejectUnrelatedUseClasses(string skillUseName)
    {
        Assert.False(PackValidator.IsSkillUseClassValid(skillUseName, "Secutor"));
    }
}
