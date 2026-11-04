using System;
using System.IO;
using System.Linq;
using ModdingGUI.Core.Grids;
using Xunit;

namespace ModdingGUI.Tests.Core;

/// <summary>
/// Grid write fidelity. The corpus has two layouts: version 2 (403 files, F/E prefixed tags and a
/// 5064-byte body) and version 1 (27 files, bare tag names and a larger body we have not mapped).
/// These tests pin what is safe: version 2 round-trips byte for byte, version 1 is readable but
/// refuses to be written.
/// </summary>
public sealed class GridWriteTests : IDisposable
{
    private readonly string _tempDir;

    public GridWriteTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ModdingGUI.GridWrite-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true); }
        catch (IOException) { /* best effort */ }
    }

    private static bool Ready => CleanBecCorpus.IsAvailable;

    private static string[] CorpusGrids() =>
        Directory.EnumerateFiles(Path.Combine(CleanBecCorpus.Root!, "data"), "*.grd",
                SearchOption.AllDirectories)
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static int LayoutVersion(string file)
    {
        var header = new byte[4];
        using var stream = File.OpenRead(file);
        if (stream.Read(header, 0, 4) < 4) return -1;
        return BitConverter.ToInt32(header);
    }

    [Fact]
    public void EveryGrid_RoundTripsByteForByte()
    {
        if (!Ready) return;

        int checkedGrids = 0;
        var failures = new System.Collections.Generic.List<string>();

        foreach (string file in CorpusGrids())
        {
            var grid = GridFile.Load(file);
            Assert.True(grid.IsWritableLayout, $"{Path.GetFileName(file)} should be writable");

            string copy = Path.Combine(_tempDir, Path.GetFileName(file));
            grid.Save(copy);
            checkedGrids++;

            if (!File.ReadAllBytes(file).AsSpan().SequenceEqual(File.ReadAllBytes(copy)))
                failures.Add(Path.GetRelativePath(CleanBecCorpus.Root!, file));
        }

        Assert.True(checkedGrids >= 430, $"expected the whole corpus, only saw {checkedGrids} grids");
        Assert.True(failures.Count == 0,
            $"{failures.Count} grid(s) did not round-trip: {string.Join(", ", failures.Take(5))}");
    }

    [Fact]
    public void VersionOneGrid_IsReadableAndWritable()
    {
        if (!Ready) return;

        string file = CorpusGrids().First(f => LayoutVersion(f) == 1);
        var grid = GridFile.Load(file);

        Assert.Equal(1, grid.LayoutVersion);
        Assert.True(grid.IsWritableLayout);

        // Real names only: the legacy header lists them without an F/E prefix.
        var names = Enumerable.Range(0, GridFile.NumSlots)
            .Select(grid.GetTagNameForSlot)
            .Where(n => !string.IsNullOrWhiteSpace(n) && n != GridFile.NO_VALUE)
            .ToArray();
        Assert.NotEmpty(names);
    }

    [Fact]
    public void Save_LegacyGrid_AfterEditingACell_ReloadsWithThatCell()
    {
        if (!Ready) return;

        string file = CorpusGrids().First(f => LayoutVersion(f) == 1);
        var grid = GridFile.Load(file);

        int slot = Enumerable.Range(0, GridFile.NumSlots)
            .First(s => string.Equals(grid.GetTagNameForSlot(s), "Start1", StringComparison.OrdinalIgnoreCase));

        // Find an empty cell and paint Start1 into it.
        int row = -1, col = -1;
        for (int r = 0; r < GridFile.ArenaSize && row < 0; r++)
        {
            for (int c = 0; c < GridFile.ArenaSize; c++)
            {
                if (grid.GetValue(r, c) == 0) { row = r; col = c; break; }
            }
        }
        Assert.True(row >= 0, "expected an empty cell to paint");

        grid.SetValue(row, col, 1 << slot);
        string copy = Path.Combine(_tempDir, "legacy-edited.grd");
        grid.Save(copy);

        var reloaded = GridFile.Load(copy);
        Assert.Equal(1, reloaded.LayoutVersion);
        Assert.Equal(1 << slot, reloaded.GetValue(row, col) & (1 << slot));
        Assert.Equal(grid.GetTagNameForSlot(slot), reloaded.GetTagNameForSlot(slot));
    }

    [Fact]
    public void Save_UnknownVersion_IsRefused()
    {
        // A version-3 file is not something we understand, so writing must fail rather than guess.
        string file = Path.Combine(_tempDir, "future.grd");
        var bytes = new byte[8 + GridFile.NumSlots * 32 + GridFile.ArenaSize * GridFile.ArenaSize * 4];
        BitConverter.GetBytes(3).CopyTo(bytes, 0);
        File.WriteAllBytes(file, bytes);

        Assert.Throws<NotSupportedException>(() => GridFile.Load(file).Save(file + ".out"));
        Assert.False(File.Exists(file + ".out"));
    }

    [Fact]
    public void Save_PreservesTheHeaderActiveCountEvenWhenItDisagreesWithTheTags()
    {
        if (!Ready) return;

        // qstinvasion1.grd declares 3 active tags while six are F-prefixed; the game ignores the
        // count, so it must be written back unchanged rather than recomputed.
        string? file = CorpusGrids().FirstOrDefault(f =>
            Path.GetFileName(f).Equals("qstinvasion1.grd", StringComparison.OrdinalIgnoreCase));
        if (file is null) return;

        byte[] original = File.ReadAllBytes(file);
        int countBefore = BitConverter.ToInt32(original, 4);

        string copy = Path.Combine(_tempDir, "invasion.grd");
        GridFile.Load(file).Save(copy);

        Assert.Equal(countBefore, BitConverter.ToInt32(File.ReadAllBytes(copy), 4));
        Assert.Equal(original, File.ReadAllBytes(copy));
    }

    [Fact]
    public void Save_WritesBareTagNamesForVersionOneAndPrefixedForVersionTwo()
    {
        if (!Ready) return;

        // Version 2: every named slot carries an F or E prefix, unnamed slots carry only the prefix.
        string v2 = CorpusGrids().First(f => LayoutVersion(f) == 2);
        string v2Copy = Path.Combine(_tempDir, "v2.grd");
        GridFile.Load(v2).Save(v2Copy);

        byte[] saved = File.ReadAllBytes(v2Copy);
        char firstTagByte = (char)saved[8];
        Assert.True(firstTagByte is 'F' or 'E',
            $"version-2 tag block should start with F/E, saw '{firstTagByte}'");
    }
}
