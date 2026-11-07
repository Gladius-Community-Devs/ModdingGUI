using System;
using System.IO;
using System.Linq;
using ModdingGUI.Core.Api;
using ModdingGUI.Core.Randomizer;
using Xunit;

namespace ModdingGUI.Tests.Core.Randomizer;

/// <summary>
/// The baseline is what stops runs stacking: the first run pins the project, every later run restores it, so the
/// same seed reproduces the run and a reset is one call away.
/// </summary>
public sealed class RandomizerBaselineTests : IDisposable
{
    private readonly string _project;
    private readonly string _bec;
    private readonly RandomizerBaseline _baseline = new();

    public RandomizerBaselineTests()
    {
        _project = Path.Combine(Path.GetTempPath(), "ModdingGUI.Baseline-" + Guid.NewGuid().ToString("N"));
        _bec = Path.Combine(_project, "proj_BEC");

        foreach (string relative in new[]
                 {
                     "data/units/gladiators.txt", "data/units/statsets.txt", "data/units/skillsets.txt",
                     "data/units/itemsets.txt", "data/units/schools.txt", "data/units/tints.txt",
                     "data/config/classdefs.tok", "data/config/items.tok",
                     "data/school/valensimperia.tok", "data/school/ursulanordagh.tok",
                 })
        {
            string target = Path.Combine(_bec, relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(Path.Combine(CleanBecCorpus.Root!, relative.Replace('/', Path.DirectorySeparatorChar)), target);
        }

        string encounters = Path.Combine(_bec, "data", "encounters");
        foreach (string name in new[] { "battle1.enc", "expansedunesencounter01.enc" })
        {
            string? file = Directory.EnumerateFiles(Path.Combine(CleanBecCorpus.Root!, "data", "encounters"),
                    "*.enc", SearchOption.AllDirectories)
                .FirstOrDefault(path => Path.GetFileName(path).Equals(name, StringComparison.OrdinalIgnoreCase));

            if (file is not null)
            {
                Directory.CreateDirectory(encounters);
                File.Copy(file, Path.Combine(encounters, name));
            }
        }
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_project)) Directory.Delete(_project, recursive: true); }
        catch (IOException) { /* best effort */ }
    }

    private bool Ready => CleanBecCorpus.IsAvailable &&
                          File.Exists(Path.Combine(_bec, "data", "units", "gladiators.txt"));

    private string Glad => Path.Combine(_bec, "data", "units", "gladiators.txt");
    private string Roster => Path.Combine(_bec, "data", "school", "valensimperia.tok");

    [Fact]
    public void CapturePinsTheCurrentState()
    {
        if (!Ready) return;

        byte[] before = File.ReadAllBytes(Glad);
        Assert.False(_baseline.Exists(_bec));

        var result = _baseline.Capture(_bec);

        Assert.True(_baseline.Exists(_bec));
        Assert.True(result.Files > 0);
        Assert.False(result.AlreadyExisted);

        string pinned = Path.Combine(_bec, ".mod-history", RandomizerBaseline.Folder,
            "data", "units", "gladiators.txt");
        Assert.Equal(before, File.ReadAllBytes(pinned));
    }

    [Fact]
    public void CaptureTwiceLeavesTheFirstPinAlone()
    {
        if (!Ready) return;

        _baseline.Capture(_bec);
        byte[] pinned = File.ReadAllBytes(Path.Combine(_bec, ".mod-history", RandomizerBaseline.Folder,
            "data", "units", "gladiators.txt"));

        // Randomize the project, then try to pin again: the pin must still be vanilla.
        new RandomizerService().Randomize(_bec, new RandomizerOptions { Seed = "pin", RandomizeStatsets = true });

        var second = _baseline.Capture(_bec);
        Assert.True(second.AlreadyExisted);
        Assert.Equal(pinned, File.ReadAllBytes(Path.Combine(_bec, ".mod-history", RandomizerBaseline.Folder,
            "data", "units", "gladiators.txt")));
    }

    [Fact]
    public void RestorePutsTheVanillaBytesBack()
    {
        if (!Ready) return;

        byte[] vanilla = File.ReadAllBytes(Glad);
        byte[] roster = File.ReadAllBytes(Roster);

        _baseline.Capture(_bec);
        new RandomizerService().Randomize(_bec, new RandomizerOptions { Seed = "restore", RandomizeStatsets = true });
        Assert.NotEqual(vanilla, File.ReadAllBytes(Glad));

        var restored = _baseline.Restore(_bec);

        Assert.True(restored.Files > 0);
        Assert.Equal(vanilla, File.ReadAllBytes(Glad));
        Assert.Equal(roster, File.ReadAllBytes(Roster));
    }

    [Fact]
    public void RestoreWithoutABaselineIsHarmless()
    {
        if (!Ready) return;

        byte[] before = File.ReadAllBytes(Glad);
        var result = _baseline.Restore(_bec);

        Assert.Equal(0, result.Files);
        Assert.Equal(before, File.ReadAllBytes(Glad));
    }

    [Fact]
    public void RestoreIsItselfBackedUp()
    {
        if (!Ready) return;

        _baseline.Capture(_bec);
        new RandomizerService().Randomize(_bec, new RandomizerOptions { Seed = "undo", RandomizeStatsets = true });

        string history = Path.Combine(_bec, ".mod-history", "data", "units", "gladiators.txt");
        int before = Directory.GetFiles(history).Length;

        _baseline.Restore(_bec);

        Assert.Equal(before + 1, Directory.GetFiles(history).Length);
    }

    [Fact]
    public void TheSameSeedReproducesAcrossRuns()
    {
        if (!Ready) return;

        var options = new RandomizerOptions { Seed = "repeatable", RandomizeStatsets = true, RandomizeHeroes = false };

        new RandomizerService().Randomize(_bec, options);
        byte[] first = File.ReadAllBytes(Glad);

        // A second run restores the baseline first, so this must match exactly.
        new RandomizerService().Randomize(_bec, options);
        byte[] second = File.ReadAllBytes(Glad);

        Assert.Equal(first, second);
    }

    [Fact]
    public void ARunReportsWhetherItCapturedOrRestored()
    {
        if (!Ready) return;

        var first = new RandomizerService().Randomize(_bec, new RandomizerOptions { Seed = "steps", RandomizeStatsets = true });
        Assert.Contains("baseline-captured", first.Steps);

        var second = new RandomizerService().Randomize(_bec, new RandomizerOptions { Seed = "steps", RandomizeStatsets = true });
        Assert.Contains("baseline-restored", second.Steps);
    }

    [Fact]
    public void WithoutTheOptionNothingIsCaptured()
    {
        if (!Ready) return;

        new RandomizerService().Randomize(_bec,
            new RandomizerOptions { Seed = "noreset", RandomizeStatsets = true, ResetToBaseline = false });

        Assert.False(_baseline.Exists(_bec));
    }
}
