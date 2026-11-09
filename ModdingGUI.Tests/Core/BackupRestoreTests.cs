using System;
using System.IO;
using System.Linq;
using System.Threading;
using ModdingGUI.Core;
using ModdingGUI.Core.Api;
using ModdingGUI.Core.History;
using Xunit;

namespace ModdingGUI.Tests.Core;

/// <summary>
/// Browsing and restoring <c>.mod-history</c>: the undo story for every Api, including binary formats.
/// </summary>
public sealed class BackupRestoreTests : IDisposable
{
    private readonly string _project = Path.Combine(Path.GetTempPath(), "ModdingGUI.History-" + Guid.NewGuid().ToString("N"));
    private readonly string _bec;

    public BackupRestoreTests()
    {
        _bec = Path.Combine(_project, "proj_BEC");
        Directory.CreateDirectory(Path.Combine(_bec, "data", "units"));
        Directory.CreateDirectory(Path.Combine(_bec, "data", "config"));
        Directory.CreateDirectory(Path.Combine(_bec, "data", "encounters", "test"));

        File.WriteAllText(Gladiators, "v1\n");
        File.WriteAllText(Items, "items-v1\n");
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_project)) Directory.Delete(_project, recursive: true); }
        catch (IOException) { /* best effort */ }
    }

    private string GladiaatorsRelative => "data/units/gladiators.txt";
    private string Gladiators => Path.Combine(_bec, "data", "units", "gladiators.txt");
    private string Items => Path.Combine(_bec, "data", "config", "items.tok");

    /// <summary>Simulate an edit: backup then overwrite, the way every Api works.</summary>
    private void Edit(string path, string content)
    {
        ChangeHistoryService.Backup(_bec, path);
        File.WriteAllText(path, content);
        // Distinct millisecond stamps so ordering is deterministic.
        Thread.Sleep(5);
    }

    [Fact]
    public void ListReturnsNewestFirst()
    {
        Edit(Gladiators, "v2\n");
        Edit(Gladiators, "v3\n");

        var entries = new HistoryApi().ListAll(_project);

        Assert.Equal(2, entries.Count);
        Assert.True(entries[0].Timestamp >= entries[1].Timestamp);
        Assert.All(entries, entry => Assert.Equal(GladiaatorsRelative, entry.RelativePath));
    }

    [Fact]
    public void ListFilesGroupsByFile()
    {
        Edit(Gladiators, "v2\n");
        Edit(Items, "items-v2\n");
        Edit(Gladiators, "v3\n");

        var files = new HistoryApi().ListFiles(_project);

        Assert.Equal(2, files.Count);
        Assert.Contains(files, file => file.RelativePath == GladiaatorsRelative && file.Count == 2);
        Assert.Contains(files, file => file.RelativePath == "data/config/items.tok" && file.Count == 1);
    }

    [Fact]
    public void ListForFiltersToOneFile()
    {
        Edit(Gladiators, "v2\n");
        Edit(Items, "items-v2\n");

        var entries = new HistoryApi().ListFor(_project, GladiaatorsRelative);

        Assert.Single(entries);
        Assert.Equal(GladiaatorsRelative, entries[0].RelativePath);
    }

    [Fact]
    public void RestorePutsTheBackedUpBytesBack()
    {
        Edit(Gladiators, "v2\n");
        var backup = new HistoryApi().ListFor(_project, GladiaatorsRelative).Single();

        var restored = new HistoryApi().Restore(_project, backup);

        Assert.Equal("v1\n", File.ReadAllText(Gladiators));
        Assert.Equal(GladiaatorsRelative, restored.RelativePath);
    }

    [Fact]
    public void RestoringIsItselfUndoable()
    {
        Edit(Gladiators, "v2\n");
        var backup = new HistoryApi().ListFor(_project, GladiaatorsRelative).Single();

        new HistoryApi().Restore(_project, backup);

        // The pre-restore state ("v2") is now the newest backup, so restoring it again undoes the undo.
        var afterRestore = new HistoryApi().ListFor(_project, GladiaatorsRelative);
        Assert.Equal(2, afterRestore.Count);

        var undo = afterRestore.First(entry => entry.BackupPath != backup.BackupPath);
        new HistoryApi().Restore(_project, undo);

        Assert.Equal("v2\n", File.ReadAllText(Gladiators));
    }

    [Fact]
    public void RestoreRecreatesADeletedFile()
    {
        Edit(Gladiators, "v2\n");
        var backup = new HistoryApi().ListFor(_project, GladiaatorsRelative).Single();
        File.Delete(Gladiators);

        new HistoryApi().Restore(_project, backup);

        Assert.True(File.Exists(Gladiators));
        Assert.Equal("v1\n", File.ReadAllText(Gladiators));
    }

    [Fact]
    public void RestoreRejectsABackupOutsideTheProject()
    {
        Edit(Gladiators, "v2\n");
        string outside = Path.Combine(Path.GetTempPath(), $"outside-{Guid.NewGuid():N}.bak");
        File.WriteAllText(outside, "not a backup");

        try
        {
            Assert.Throws<InvalidOperationException>(() => new HistoryApi().Restore(_project, outside));
        }
        finally
        {
            File.Delete(outside);
        }
    }

    [Fact]
    public void RestoreRejectsAMissingBackup()
    {
        string missing = Path.Combine(_bec, ChangeHistoryService.RootFolderName,
            "data", "units", "gladiators.txt", "20260101-000000-000.bak");

        Assert.Throws<InvalidOperationException>(() => new HistoryApi().Restore(_project, missing));
    }

    [Fact]
    public void BinaryFilesRestoreByteForByte()
    {
        string grid = Path.Combine(_bec, "data", "encounters", "test", "arena.grd");
        byte[] original = { 2, 0, 0, 0, 9, 8, 7, 6, 5 };
        File.WriteAllBytes(grid, original);

        ChangeHistoryService.Backup(_bec, grid);
        File.WriteAllBytes(grid, new byte[] { 1, 1, 1 });

        var backup = new HistoryApi().ListFor(_project, "data/encounters/test/arena.grd").Single();
        new HistoryApi().Restore(_project, backup);

        Assert.Equal(original, File.ReadAllBytes(grid));
    }

    [Fact]
    public void TheRandomizerBaselineIsNotOfferedAsHistory()
    {
        // The pinned baseline lives under .mod-history too; it is a snapshot, not an edit history.
        string baseline = Path.Combine(_bec, ChangeHistoryService.RootFolderName, "_randomizer-baseline",
            "data", "units", "gladiators.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(baseline)!);
        File.WriteAllText(baseline, "vanilla\n");

        Assert.Empty(new HistoryApi().ListAll(_project));
        Assert.DoesNotContain(new HistoryApi().ListFiles(_project),
            file => file.RelativePath.Contains("_randomizer-baseline", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AnEmptyProjectHasNoHistory()
    {
        Assert.Empty(new HistoryApi().ListAll(_project));
        Assert.Empty(new HistoryApi().ListFiles(_project));
    }
}
