using System;
using System.IO;
using System.Linq;
using ModdingGUI.Core.Api;
using Xunit;

namespace ModdingGUI.Tests.Core.Api;

/// <summary>
/// The <c>UNITDB</c> affinity requirement is matched against the candidate unit's affinity by
/// <c>FUN_8010ad40</c>, so only engine ids can ever be satisfied: -1 for "none", 1-6 for
/// Earth/Water/Dark/Air/Fire/Light. Vanilla uses -1 and 1-5 (400 rows carry a requirement, 34 of them
/// with the prohibit flag).
/// </summary>
public sealed class EncounterAffinityRequirementTests : IDisposable
{
    private readonly string _project;
    private readonly string _bec;

    public EncounterAffinityRequirementTests()
    {
        _project = Path.Combine(Path.GetTempPath(), "ModdingGUI.AffinityReq-" + Guid.NewGuid().ToString("N"));
        _bec = Path.Combine(_project, "proj_BEC");
        Directory.CreateDirectory(Path.Combine(_bec, "data", "encounters", "qa"));
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_project)) Directory.Delete(_project, recursive: true); }
        catch (IOException) { /* best effort */ }
    }

    private const string Relative = "data/encounters/qa/affinity.enc";

    private void WriteEncounter(int? affinityRequirement, int prohibit = 0)
    {
        string requirement = affinityRequirement is null ? "-1" : affinityRequirement.Value.ToString();
        string text =
            "ENCOUNTERNAME: \"QA Affinity\"\n" +
            "TEAM: 0, 0\n" +
            "GRIDFILE: \"$d\\encounters\\qa\\affinity.grd\"\n" +
            $"UNITDB: \"Test Unit\", 100, \"Start1\", 1, 1, 0, 0, {requirement}, \"\", \"\", \"\", \"\", " +
            $"0, 0, 0, {prohibit}, 0, 0, 0, 0, 0\n";

        File.WriteAllText(Path.Combine(_bec, Relative.Replace('/', Path.DirectorySeparatorChar)), text);
    }

    private string[] CodesFor(int? affinityRequirement, int prohibit = 0)
    {
        WriteEncounter(affinityRequirement, prohibit);
        return new EncountersApi().Validate(_bec, Relative)
            .Where(i => i.Code == EncounterValidationCodes.AffinityRequirement)
            .Select(i => i.Code)
            .ToArray();
    }

    [Fact]
    public void NoRequirement_IsClean()
    {
        Assert.Empty(CodesFor(null));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void EngineIDs_AreAccepted(int requirement)
    {
        Assert.Empty(CodesFor(requirement));
    }

    [Theory]
    [InlineData(-2)]
    [InlineData(7)]
    [InlineData(42)]
    public void ValuesOutsideTheEngineIDs_AreReported(int requirement)
    {
        Assert.Single(CodesFor(requirement));
    }

    [Fact]
    public void TheProhibitFlag_DoesNotChangeTheRangeCheck()
    {
        // 34 vanilla rows prohibit an affinity; the id still has to exist.
        Assert.Empty(CodesFor(5, prohibit: 1));
        Assert.Single(CodesFor(9, prohibit: 1));
    }
}
