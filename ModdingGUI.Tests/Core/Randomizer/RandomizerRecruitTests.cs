using System;
using System.IO;
using System.Linq;
using ModdingGUI.Core.Api;
using ModdingGUI.Core.Randomizer;
using Xunit;

namespace ModdingGUI.Tests.Core.Randomizer;

/// <summary>
/// The "no recruits" option empties the recruiting pool by deleting every <c>RECRUIT</c> line from the league
/// files, which is what the old randomizer did. Each changed file is written once, with its own backup.
/// </summary>
public sealed class RandomizerRecruitTests : IDisposable
{
    private readonly string _project;
    private readonly string _bec;
    private readonly LeaguesApi _leagues = new();

    public RandomizerRecruitTests()
    {
        _project = Path.Combine(Path.GetTempPath(), "ModdingGUI.Recruits-" + Guid.NewGuid().ToString("N"));
        _bec = Path.Combine(_project, "proj_BEC");
        CopyProject(_bec);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_project)) Directory.Delete(_project, recursive: true); }
        catch (IOException) { /* best effort */ }
    }

    private static bool Ready => CleanBecCorpus.IsAvailable;

    private static void CopyProject(string bec)
    {
        foreach (string relative in new[]
                 {
                     "data/units/gladiators.txt", "data/units/statsets.txt", "data/units/skillsets.txt",
                     "data/units/itemsets.txt", "data/units/schools.txt", "data/units/tints.txt",
                     "data/config/classdefs.tok", "data/config/items.tok",
                     "data/school/valensimperia.tok", "data/school/ursulanordagh.tok",
                 })
        {
            string target = Path.Combine(bec, relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(Path.Combine(CleanBecCorpus.Root!, relative.Replace('/', Path.DirectorySeparatorChar)), target);
        }

        string source = Path.Combine(CleanBecCorpus.Root!, "data", "towns", "leagues");
        string dest = Path.Combine(bec, "data", "towns", "leagues");
        foreach (string file in Directory.EnumerateFiles(source, "*.tok", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(source, file);
            string target = Path.Combine(dest, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
    }

    private static int RecruitLineCount(string directory) =>
        Directory.EnumerateFiles(directory, "*.tok", SearchOption.AllDirectories)
            .Sum(file => File.ReadAllLines(file)
                .Count(line => line.TrimStart().StartsWith("RECRUIT", StringComparison.OrdinalIgnoreCase)));

    [Fact]
    public void VanillaHasRecruitLinesToRemove()
    {
        if (!Ready) return;

        string leagues = Path.Combine(_bec, "data", "towns", "leagues");
        Assert.True(RecruitLineCount(leagues) > 0, "the corpus copy has no RECRUIT lines");
    }

    [Fact]
    public void RemoveRecruits_EmptiesThePool()
    {
        if (!Ready) return;

        string leagues = Path.Combine(_bec, "data", "towns", "leagues");
        int changed = new LeaguesApi().RemoveRecruits(_bec);

        Assert.True(changed > 0);
        Assert.Equal(0, RecruitLineCount(leagues));
    }

    [Fact]
    public void RemoveRecruits_IsIdempotent()
    {
        if (!Ready) return;

        new LeaguesApi().RemoveRecruits(_bec);
        Assert.Equal(0, new LeaguesApi().RemoveRecruits(_bec));
    }

    [Fact]
    public void RemoveRecruits_KeepsEverythingElse()
    {
        if (!Ready) return;

        string leagues = Path.Combine(_bec, "data", "towns", "leagues");
        var before = Directory.EnumerateFiles(leagues, "*.tok", SearchOption.AllDirectories)
            .ToDictionary(f => f,
                f => File.ReadAllLines(f).Count(l => !l.TrimStart().StartsWith("RECRUIT", StringComparison.OrdinalIgnoreCase)));

        new LeaguesApi().RemoveRecruits(_bec);

        foreach (var (file, expected) in before)
        {
            Assert.Equal(expected, File.ReadAllLines(file).Length);
        }
    }

    [Fact]
    public void EachChangedFileGetsOneBackup()
    {
        if (!Ready) return;

        int changed = new LeaguesApi().RemoveRecruits(_bec);
        string history = Path.Combine(_bec, ".mod-history", "data", "towns", "leagues");

        Assert.True(Directory.Exists(history));
        int backups = Directory.EnumerateFiles(history, "*.bak", SearchOption.AllDirectories).Count();
        Assert.Equal(changed, backups);
    }

    [Fact]
    public void TheServiceRunsTheStep_AndReportsIt()
    {
        if (!Ready) return;

        var result = new RandomizerService().Randomize(_bec,
            new RandomizerOptions { Seed = "recruits", RemoveRecruits = true });

        Assert.Contains("recruits", result.Steps);
        Assert.True(result.ChangedRecords > 0);
        Assert.Equal(0, RecruitLineCount(Path.Combine(_bec, "data", "towns", "leagues")));
    }

    [Fact]
    public void DisabledStep_LeavesLeaguesAlone()
    {
        if (!Ready) return;

        string leagues = Path.Combine(_bec, "data", "towns", "leagues");
        int before = RecruitLineCount(leagues);

        new RandomizerService().Randomize(_bec, new RandomizerOptions { Seed = "off", RemoveRecruits = false });

        Assert.Equal(before, RecruitLineCount(leagues));
    }
}
