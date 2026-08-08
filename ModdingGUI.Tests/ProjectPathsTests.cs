using ModdingGUI;
using Xunit;

namespace ModdingGUI.Tests;

/// <summary>
/// Tests for <see cref="ProjectPaths"/> path-builder helpers.
/// All methods are pure functions, so no file system is needed.
/// </summary>
public class ProjectPathsTests
{
    private const string Root = @"C:\projects\MyMod";

    // BecFolder ---------------------------------------------------------------

    [Fact]
    public void BecFolder_ReturnsBaseName_BEC_Suffix()
    {
        string expected = Path.Combine(Root, "MyMod_BEC");
        Assert.Equal(expected, ProjectPaths.BecFolder(Root));
    }

    [Fact]
    public void BecFolder_StripsTrailingSeparator()
    {
        string expected = Path.Combine(Root, "MyMod_BEC");
        Assert.Equal(expected, ProjectPaths.BecFolder(Root + Path.DirectorySeparatorChar));
    }

    // DataFolder / sub-folders ------------------------------------------------

    [Fact]
    public void DataFolder_NestsUnderBec()
    {
        string expected = Path.Combine(Root, "MyMod_BEC", "data");
        Assert.Equal(expected, ProjectPaths.DataFolder(Root));
    }

    [Fact]
    public void UnitsFolder_NestsUnderData()
    {
        string expected = Path.Combine(Root, "MyMod_BEC", "data", "units");
        Assert.Equal(expected, ProjectPaths.UnitsFolder(Root));
    }

    [Fact]
    public void ConfigFolder_NestsUnderData()
    {
        string expected = Path.Combine(Root, "MyMod_BEC", "data", "config");
        Assert.Equal(expected, ProjectPaths.ConfigFolder(Root));
    }

    [Fact]
    public void SchoolFolder_NestsUnderData()
    {
        string expected = Path.Combine(Root, "MyMod_BEC", "data", "school");
        Assert.Equal(expected, ProjectPaths.SchoolFolder(Root));
    }

    [Fact]
    public void TownsFolder_NestsUnderData()
    {
        string expected = Path.Combine(Root, "MyMod_BEC", "data", "towns");
        Assert.Equal(expected, ProjectPaths.TownsFolder(Root));
    }

    [Fact]
    public void ScriptFolder_NestsUnderData()
    {
        string expected = Path.Combine(Root, "MyMod_BEC", "data", "script");
        Assert.Equal(expected, ProjectPaths.ScriptFolder(Root));
    }

    [Fact]
    public void EncountersFolder_NestsUnderData()
    {
        string expected = Path.Combine(Root, "MyMod_BEC", "data", "encounters");
        Assert.Equal(expected, ProjectPaths.EncountersFolder(Root));
    }

    // Named files -------------------------------------------------------------

    [Fact]
    public void GladiatorsFile_HasCorrectName()
    {
        string expected = Path.Combine(Root, "MyMod_BEC", "data", "units", "gladiators.txt");
        Assert.Equal(expected, ProjectPaths.GladiatorsFile(Root));
    }

    [Fact]
    public void StatSetsFile_HasCorrectName()
    {
        string expected = Path.Combine(Root, "MyMod_BEC", "data", "units", "statsets.txt");
        Assert.Equal(expected, ProjectPaths.StatSetsFile(Root));
    }

    [Fact]
    public void ItemSetsFile_HasCorrectName()
    {
        string expected = Path.Combine(Root, "MyMod_BEC", "data", "units", "itemsets.txt");
        Assert.Equal(expected, ProjectPaths.ItemSetsFile(Root));
    }

    [Fact]
    public void SkillSetsFile_HasCorrectName()
    {
        string expected = Path.Combine(Root, "MyMod_BEC", "data", "units", "skillsets.txt");
        Assert.Equal(expected, ProjectPaths.SkillSetsFile(Root));
    }

    [Fact]
    public void ClassDefsFile_HasCorrectName()
    {
        string expected = Path.Combine(Root, "MyMod_BEC", "data", "config", "classdefs.tok");
        Assert.Equal(expected, ProjectPaths.ClassDefsFile(Root));
    }

    [Fact]
    public void ItemsTokFile_HasCorrectName()
    {
        string expected = Path.Combine(Root, "MyMod_BEC", "data", "config", "items.tok");
        Assert.Equal(expected, ProjectPaths.ItemsTokFile(Root));
    }

    [Fact]
    public void LookupTextFile_HasCorrectName()
    {
        string expected = Path.Combine(Root, "MyMod_BEC", "data", "config", "lookuptext_eng.txt");
        Assert.Equal(expected, ProjectPaths.LookupTextFile(Root));
    }

    [Fact]
    public void SkillsTokFile_HasCorrectName()
    {
        string expected = Path.Combine(Root, "MyMod_BEC", "data", "config", "skills.tok");
        Assert.Equal(expected, ProjectPaths.SkillsTokFile(Root));
    }

    [Fact]
    public void WorldmapFile_HasCorrectName()
    {
        string expected = Path.Combine(Root, "MyMod_BEC", "data", "config", "worldmap.tok");
        Assert.Equal(expected, ProjectPaths.WorldmapFile(Root));
    }

    [Fact]
    public void SchoolFile_AppendsFileNameUnderSchoolFolder()
    {
        string expected = Path.Combine(Root, "MyMod_BEC", "data", "school", "roster.txt");
        Assert.Equal(expected, ProjectPaths.SchoolFile(Root, "roster.txt"));
    }
}
