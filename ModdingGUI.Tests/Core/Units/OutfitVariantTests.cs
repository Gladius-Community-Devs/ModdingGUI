using System;
using System.IO;
using System.Linq;
using ModdingGUI.Core.Api;
using ModdingGUI.Core.Units;
using Xunit;

namespace ModdingGUI.Tests.Core.Units;

/// <summary>
/// A gladiator's <c>Outfit</c> selects <c>data/characters/&lt;mesh&gt;/&lt;mesh&gt;_varNN.ptx</c>, so the class's
/// mesh folder is the only list of valid values: 0 is the plain model, 1..N the variants. Vanilla never
/// exceeds it (2,170 records checked), and an invalid value has been reported to crash the game.
/// </summary>
public sealed class OutfitVariantTests : IDisposable
{
    private readonly string _project;
    private readonly string _bec;

    public OutfitVariantTests()
    {
        _project = Path.Combine(Path.GetTempPath(), "ModdingGUI.Outfit-" + Guid.NewGuid().ToString("N"));
        _bec = Path.Combine(_project, "proj_BEC");

        Directory.CreateDirectory(Path.Combine(_bec, "data", "units"));
        Directory.CreateDirectory(Path.Combine(_bec, "data", "config"));

        // A class whose mesh has four variant files.
        File.WriteAllText(Path.Combine(_bec, "data", "config", "classdefs.tok"),
            "NUMCLASSDEFS: 1\n\nCREATECLASS: TestClass\n\n\tMESH: \"testmesh\"\n");

        string meshDir = Path.Combine(_bec, "data", "characters", "testmesh");
        Directory.CreateDirectory(meshDir);
        foreach (int n in new[] { 1, 2, 3, 4 })
        {
            File.WriteAllText(Path.Combine(meshDir, $"testmesh_var{n:00}.ptx"), "x");
        }

        WriteGladiator(outfit: 0);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_project)) Directory.Delete(_project, recursive: true); }
        catch (IOException) { /* best effort */ }
    }

    private void WriteGladiator(int outfit)
    {
        string text =
            $"Name: Outfit Test\nClass: TestClass\nOutfit: {outfit}\nAffinity: Earth\nTint set: 0\n" +
            "Skill set: 0\nStat set: 0\nItem set: 0\nSchool: 0\n\n";
        File.WriteAllText(Path.Combine(_bec, "data", "units", "gladiators.txt"), text);
    }

    private string[] OutfitCodes()
    {
        return new GladiatorsApi().Validate(_bec)
            .Where(i => i.Code == GladiatorValidationCodes.OutfitOutOfRange)
            .Select(i => i.Code)
            .ToArray();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void OutfitWithinTheClassVariants_IsAccepted(int outfit)
    {
        WriteGladiator(outfit);
        Assert.Empty(OutfitCodes());
    }

    [Theory]
    [InlineData(5)]
    [InlineData(9)]
    public void OutfitBeyondTheClassVariants_IsReported(int outfit)
    {
        WriteGladiator(outfit);
        Assert.Single(OutfitCodes());
    }

    [Fact]
    public void VariantCount_ComesFromTheMeshFolder()
    {
        var variants = new OutfitVariants(_bec, new ClassDefsApi());

        Assert.Equal(4, variants.TryGetVariantCount("TestClass"));
        Assert.True(variants.IsValid("TestClass", 4));
        Assert.False(variants.IsValid("TestClass", 5));
    }

    [Fact]
    public void UnknownClass_CannotBeChecked()
    {
        var variants = new OutfitVariants(_bec, new ClassDefsApi());
        Assert.Null(variants.TryGetVariantCount("NoSuchClass"));
    }
}
