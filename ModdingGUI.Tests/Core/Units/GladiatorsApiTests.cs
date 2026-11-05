using System;
using System.IO;
using System.Linq;
using ModdingGUI.Core.Api;
using ModdingGUI.Core.Units;
using Xunit;

namespace ModdingGUI.Tests.Core.Units;

/// <summary>
/// <see cref="IGladiatorsApi"/> over real corpus data: list/get/patch/rename/add/remove, byte-identical
/// no-op saves, and validation that knows about classdefs and the other units files.
/// </summary>
public sealed class GladiatorsApiTests : IDisposable
{
    private readonly string _project;
    private readonly string _bec;
    private readonly GladiatorsApi _api = new();

    public GladiatorsApiTests()
    {
        _project = Path.Combine(Path.GetTempPath(), "ModdingGUI.Glads-" + Guid.NewGuid().ToString("N"));
        _bec = Path.Combine(_project, "proj_BEC");

        // Stage the files the API and its validator read.
        foreach (string relative in new[]
                 {
                     "data/units/gladiators.txt", "data/units/statsets.txt", "data/units/skillsets.txt",
                     "data/units/itemsets.txt", "data/units/schools.txt", "data/units/tints.txt",
                     "data/config/classdefs.tok",
                 })
        {
            string target = Path.Combine(_bec, relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(Path.Combine(CleanBecCorpus.Root!, relative.Replace('/', Path.DirectorySeparatorChar)), target);
        }
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_project)) Directory.Delete(_project, recursive: true); }
        catch (IOException) { /* best effort */ }
    }

    private bool Ready => CleanBecCorpus.IsAvailable;

    private string GladiatorsPath => Path.Combine(_bec, "data", "units", "gladiators.txt");

    [Fact]
    public void List_ReturnsTheWholeVanillaRoster()
    {
        if (!Ready) return;

        var names = _api.List(_bec);

        Assert.Equal(2170, names.Count);
        Assert.Equal("Aaden", names[0]);
        Assert.Equal("Zyrrell", names[^1]);
    }

    [Fact]
    public void Get_ReadsEveryFieldOfARecord()
    {
        if (!Ready) return;

        var aaden = _api.Get(_bec, "Aaden");
        Assert.NotNull(aaden);
        Assert.Equal("BanditAF", aaden!.Class);
        Assert.Equal(4, aaden.Outfit);
        Assert.Equal("Earth", aaden.Affinity);
        Assert.Equal(1, aaden.TintSet);
        Assert.Equal(0, aaden.SkillSet);
        Assert.Equal(0, aaden.StatSet);
        Assert.Equal(1, aaden.ItemSet);
        Assert.Equal(1, aaden.School);

        Assert.Null(_api.Get(_bec, "nobody-by-that-name"));
    }

    [Fact]
    public void Parse_ReadsEveryRecordWithNineFields()
    {
        if (!Ready) return;

        var file = GladiatorsFile.Load(GladiatorsPath);

        Assert.Equal(2170, file.Records.Count);
        Assert.All(file.Records, r =>
        {
            Assert.False(string.IsNullOrWhiteSpace(r.Name));
            Assert.False(string.IsNullOrWhiteSpace(r.Class));
            Assert.True(r.StatSet >= 0);
        });
    }

    [Fact]
    public void NoOpSave_IsByteIdentical()
    {
        if (!Ready) return;

        byte[] before = File.ReadAllBytes(GladiatorsPath);
        GladiatorsFile.Load(GladiatorsPath).Save(_bec);

        Assert.Equal(before, File.ReadAllBytes(GladiatorsPath));
    }

    [Fact]
    public void Patch_RewritesOnlyTheTouchedLines()
    {
        if (!Ready) return;

        string[] before = File.ReadAllLines(GladiatorsPath);
        _api.Patch(_bec, "Aaden", new GladiatorPatch { Outfit = 3, Affinity = "Fire" });
        string[] after = File.ReadAllLines(GladiatorsPath);

        Assert.Equal(before.Length, after.Length);

        int changed = Enumerable.Range(0, before.Length).Count(i => before[i] != after[i]);
        Assert.Equal(2, changed);
        Assert.Contains("Outfit: 3", after);
        Assert.Contains("Affinity: Fire", after);

        var aaden = _api.Get(_bec, "Aaden");
        Assert.Equal(3, aaden!.Outfit);
        Assert.Equal("Fire", aaden.Affinity);
    }

    [Fact]
    public void Patch_WithEmptyPatch_DoesNotTouchTheFile()
    {
        if (!Ready) return;

        byte[] before = File.ReadAllBytes(GladiatorsPath);
        _api.Patch(_bec, "Aaden", new GladiatorPatch());

        Assert.Equal(before, File.ReadAllBytes(GladiatorsPath));
    }

    [Fact]
    public void Patch_WritesABackupOfThePreviousBytes()
    {
        if (!Ready) return;

        byte[] before = File.ReadAllBytes(GladiatorsPath);
        _api.Patch(_bec, "Aaden", new GladiatorPatch { Outfit = 2 });

        string history = Path.Combine(_bec, ".mod-history", "data", "units", "gladiators.txt");
        Assert.True(Directory.Exists(history));
        string backup = Directory.GetFiles(history).OrderBy(f => f).Last();
        Assert.Equal(before, File.ReadAllBytes(backup));
    }

    [Fact]
    public void Rename_ChangesTheNameAndRefusesCollisions()
    {
        if (!Ready) return;

        _api.Rename(_bec, "Aaden", "Aaden the Bold");

        Assert.Null(_api.Get(_bec, "Aaden"));
        Assert.Equal("Aaden the Bold", _api.Get(_bec, "Aaden the Bold")!.Name);

        Assert.Throws<InvalidOperationException>(() => _api.Rename(_bec, "Aapehty", "Aaden the Bold"));
    }

    [Fact]
    public void Add_AppendsARecordThatRoundTripsThroughTheParser()
    {
        if (!Ready) return;

        var dto = new GladiatorDto("Test Recruit", "BanditAF", 1, "Water", 2, 3, 4, 5, 6);
        _api.Add(_bec, dto);

        var reloaded = GladiatorsFile.Load(GladiatorsPath);
        Assert.Equal(2171, reloaded.Records.Count);

        var added = reloaded.Get("Test Recruit");
        Assert.NotNull(added);
        Assert.Equal(dto, added);

        // No-op save afterwards must still be byte-identical.
        byte[] before = File.ReadAllBytes(GladiatorsPath);
        reloaded.Save(_bec);
        Assert.Equal(before, File.ReadAllBytes(GladiatorsPath));
    }

    [Fact]
    public void Add_RefusesADuplicateName()
    {
        if (!Ready) return;

        Assert.Throws<InvalidOperationException>(() => _api.Add(_bec,
            new GladiatorDto("Aaden", "BanditAF", 0, "Earth", 0, 0, 0, 0, 0)));
    }

    [Fact]
    public void Remove_DeletesTheRecordAndKeepsTheRest()
    {
        if (!Ready) return;

        int before = _api.List(_bec).Count;
        Assert.True(_api.Remove(_bec, "Aaden"));
        Assert.Equal(before - 1, _api.List(_bec).Count);
        Assert.Null(_api.Get(_bec, "Aaden"));

        Assert.False(_api.Remove(_bec, "Aaden"));

        // The file must still parse and round-trip after a delete.
        byte[] bytes = File.ReadAllBytes(GladiatorsPath);
        Assert.Equal(bytes, GladiatorsFile.Parse(bytes).ToBytes());
    }

    [Fact]
    public void Validate_VanillaCorpus_IsCleanApartFromTheKnownMalformedRecord()
    {
        if (!Ready) return;

        var issues = _api.Validate(_bec);

        // The only findings allowed in vanilla: the Muduk record's stray duplicate line.
        var warnings = issues.Where(i => i.Severity == GladiatorValidationSeverity.Warning).ToArray();
        Assert.Empty(warnings);

        var extras = issues.Where(i => i.Code == GladiatorValidationCodes.ExtraLine).ToArray();
        Assert.Single(extras);
        Assert.Equal("Muduk", extras[0].Name);
        Assert.Contains("Muduk", extras[0].Message);
    }

    [Fact]
    public void Validate_ReportsAnUnknownClassAffinityAndSetIndex()
    {
        if (!Ready) return;

        _api.Patch(_bec, "Aaden", new GladiatorPatch
        {
            Class = "NotAClass",
            Affinity = "Cheese",
            StatSet = 99_999,
        });

        var codes = _api.Validate(_bec, "Aaden").Select(i => i.Code).ToArray();
        Assert.Contains(GladiatorValidationCodes.UnknownClass, codes);
        Assert.Contains(GladiatorValidationCodes.UnknownAffinity, codes);
        Assert.Contains(GladiatorValidationCodes.SetOutOfRange, codes);
    }

    [Fact]
    public void Validate_OnOneRecord_OnlyReportsThatRecord()
    {
        if (!Ready) return;

        var issues = _api.Validate(_bec, "Aaden");
        Assert.All(issues, i => Assert.Equal("Aaden", i.Name));
    }

    [Fact]
    public void Validate_DetectsADuplicateName()
    {
        if (!Ready) return;

        // Add refuses a duplicate, so the duplicate has to be written the way a bad mod would: directly.
        File.AppendAllText(GladiatorsPath,
            "Name: Aaden\nClass: BanditAF\nOutfit: 0\nAffinity: Earth\nTint set: 0\n" +
            "Skill set: 0\nStat set: 0\nItem set: 0\nSchool: 1\n\n");

        var codes = _api.Validate(_bec, "Aaden").Select(i => i.Code).ToArray();
        Assert.Contains(GladiatorValidationCodes.DuplicateName, codes);
    }
}
