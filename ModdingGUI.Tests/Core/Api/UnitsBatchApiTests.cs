using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ModdingGUI.Core.Api;
using ModdingGUI.Core.Units;
using Xunit;

namespace ModdingGUI.Tests.Core.Api;

/// <summary>
/// Batch mutation is what makes a randomization pass practical: one read, one write, <b>one</b> backup. The
/// per-record APIs reload and back up the whole file each call, which for 2,170 gladiators would mean
/// thousands of backups and hundreds of megabytes of history.
/// </summary>
public sealed class UnitsBatchApiTests : IDisposable
{
    private readonly string _project;
    private readonly string _bec;

    public UnitsBatchApiTests()
    {
        _project = Path.Combine(Path.GetTempPath(), "ModdingGUI.Batch-" + Guid.NewGuid().ToString("N"));
        _bec = Path.Combine(_project, "proj_BEC");

        foreach (string relative in new[]
                 {
                     "data/units/gladiators.txt", "data/units/statsets.txt",
                     "data/units/skillsets.txt", "data/units/itemsets.txt",
                     "data/units/schools.txt", "data/units/tints.txt",
                     "data/config/classdefs.tok", "data/config/skills.tok", "data/config/items.tok",
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
    private string StatSetsPath => Path.Combine(_bec, "data", "units", "statsets.txt");

    private int BackupCount(string fileName)
    {
        string dir = Path.Combine(_bec, ".mod-history", "data", "units", fileName);
        return Directory.Exists(dir) ? Directory.GetFiles(dir).Length : 0;
    }

    // ---- gladiators --------------------------------------------------------

    [Fact]
    public void Gladiators_ApplyMany_ChangesEverythingInOneBackup()
    {
        if (!Ready) return;

        var gladiators = new GladiatorsApi();
        var names = gladiators.List(_bec).Take(50).ToArray();

        // Flip each outfit so every patch is a real change (some already sit at 0).
        var expected = names.ToDictionary(
            n => n,
            n => gladiators.Get(_bec, n)!.Outfit == 0 ? 1 : 0);
        var patches = expected.Select(kv => (kv.Key, new GladiatorPatch { Outfit = kv.Value })).ToArray();

        int backupsBefore = BackupCount("gladiators.txt");
        int changed = gladiators.ApplyMany(_bec, patches);

        Assert.Equal(50, changed);
        Assert.Equal(backupsBefore + 1, BackupCount("gladiators.txt"));
        Assert.All(expected, kv => Assert.Equal(kv.Value, gladiators.Get(_bec, kv.Key)!.Outfit));
    }

    [Fact]
    public void Gladiators_ApplyMany_ThatChangesNothing_WritesNothing()
    {
        if (!Ready) return;

        var gladiators = new GladiatorsApi();
        string name = gladiators.List(_bec)[0];
        var current = gladiators.Get(_bec, name)!;

        byte[] before = File.ReadAllBytes(GladiatorsPath);
        int backupsBefore = BackupCount("gladiators.txt");

        int changed = gladiators.ApplyMany(_bec, new[]
        {
            (name, new GladiatorPatch { Outfit = current.Outfit, Affinity = current.Affinity }),
        });

        Assert.Equal(0, changed);
        Assert.Equal(before, File.ReadAllBytes(GladiatorsPath));
        Assert.Equal(backupsBefore, BackupCount("gladiators.txt"));
    }

    [Fact]
    public void Gladiators_ApplyMany_SkipsUnknownNamesAndEmptyPatches()
    {
        if (!Ready) return;

        var gladiators = new GladiatorsApi();
        string name = gladiators.List(_bec)[0];

        int changed = gladiators.ApplyMany(_bec, new (string, GladiatorPatch)[]
        {
            ("No Such Gladiator", new GladiatorPatch { Outfit = 1 }),
            (name, new GladiatorPatch()),
        });

        Assert.Equal(0, changed);
    }

    [Fact]
    public void Gladiators_ApplyMany_LeavesTheFileParseableAndRoundTrippable()
    {
        if (!Ready) return;

        new GladiatorsApi().ApplyMany(_bec,
            new[] { ("Aaden", new GladiatorPatch { Class = "BanditA", Affinity = "Fire" }) });

        byte[] bytes = File.ReadAllBytes(GladiatorsPath);
        Assert.Equal(bytes, GladiatorsFile.Load(GladiatorsPath).ToBytes());
        Assert.Equal(2170, GladiatorsFile.Load(GladiatorsPath).Records.Count);
    }

    // ---- stat sets ---------------------------------------------------------

    [Fact]
    public void StatSets_ApplyMany_ChangesEverythingInOneBackup()
    {
        if (!Ready) return;

        var statSets = new StatSetsApi();
        var rows = statSets.List(_bec)
            .SelectMany(index => statSets.Get(_bec, index)!.Rows.Take(30).Select(r => (index, r)))
            .ToList();

        var bumped = rows
            .Select(r => (r.index, r.r with { Con = r.r.Con + 1 }))
            .ToArray();

        int backupsBefore = BackupCount("statsets.txt");
        int written = statSets.ApplyMany(_bec, bumped);

        Assert.Equal(bumped.Length, written);
        Assert.Equal(backupsBefore + 1, BackupCount("statsets.txt"));
        Assert.Equal(rows[0].r.Con + 1, statSets.Get(_bec, rows[0].index)!.Row(rows[0].r.Level)!.Con);
    }

    [Fact]
    public void StatSets_ApplyMany_ThatChangesNothing_WritesNothing()
    {
        if (!Ready) return;

        var statSets = new StatSetsApi();
        var row = statSets.Get(_bec, 0)!.Rows[0];

        byte[] before = File.ReadAllBytes(StatSetsPath);
        int backupsBefore = BackupCount("statsets.txt");

        int written = statSets.ApplyMany(_bec, new[] { (0, row) });

        Assert.Equal(0, written);
        Assert.Equal(before, File.ReadAllBytes(StatSetsPath));
        Assert.Equal(backupsBefore, BackupCount("statsets.txt"));
    }

    [Fact]
    public void StatSets_ApplyMany_SkipsUnknownSetsAndLevels()
    {
        if (!Ready) return;

        var statSets = new StatSetsApi();
        int missingSet = 99_999;
        int missingLevel = 999;

        int written = statSets.ApplyMany(_bec, new[]
        {
            (missingSet, new StatSetRow(1, 1, 1, 1, 1, 1)),
            (0, new StatSetRow(missingLevel, 1, 1, 1, 1, 1)),
        });

        Assert.Equal(0, written);
    }

    // ---- item sets ---------------------------------------------------------

    [Fact]
    public void ItemSets_ApplyMany_ChangesEverythingInOneBackup()
    {
        if (!Ready) return;

        var itemSets = new ItemSetsApi();
        var edits = new List<(int Index, int Ordinal, ItemSetRow Row)>();
        foreach (int index in itemSets.List(_bec).Take(20))
        {
            var rows = itemSets.Get(_bec, index)!.Rows;
            if (rows.Count == 0) continue;
            edits.Add((index, 0, rows[0] with { Note = "QA" }));
        }

        Assert.NotEmpty(edits);
        int backupsBefore = BackupCount("itemsets.txt");
        int written = itemSets.ApplyMany(_bec, edits);

        Assert.Equal(edits.Count, written);
        Assert.Equal(backupsBefore + 1, BackupCount("itemsets.txt"));
        foreach (var (index, _, row) in edits)
        {
            Assert.Equal("QA", itemSets.Get(_bec, index)!.Rows[0].Note);
        }
    }

    [Fact]
    public void ItemSets_ApplyMany_ThatChangesNothing_WritesNothing()
    {
        if (!Ready) return;

        var itemSets = new ItemSetsApi();
        var rows = itemSets.Get(_bec, 1)!.Rows;
        byte[] before = File.ReadAllBytes(Path.Combine(_bec, "data", "units", "itemsets.txt"));
        int backupsBefore = BackupCount("itemsets.txt");

        int written = itemSets.ApplyMany(_bec, new[] { (1, 0, rows[0]) });

        Assert.Equal(0, written);
        Assert.Equal(before, File.ReadAllBytes(Path.Combine(_bec, "data", "units", "itemsets.txt")));
        Assert.Equal(backupsBefore, BackupCount("itemsets.txt"));
    }

    [Fact]
    public void StatSets_ApplyMany_KeepsTheCorpusShapeValid()
    {
        if (!Ready) return;

        var statSets = new StatSetsApi();
        var row = statSets.Get(_bec, 5)!.Rows[10];
        statSets.ApplyMany(_bec, new[] { (5, row with { IniMov = row.IniMov + 2 }) });

        Assert.DoesNotContain(statSets.Validate(_bec, 5),
            i => i.Severity == StatSetValidationSeverity.Warning);
    }
}
