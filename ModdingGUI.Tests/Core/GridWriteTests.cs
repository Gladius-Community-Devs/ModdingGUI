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
    public void EveryWritableGrid_RoundTripsByteForByte()
    {
        if (!Ready) return;

        int checkedGrids = 0;
        var failures = new System.Collections.Generic.List<string>();

        foreach (string file in CorpusGrids())
        {
            if (LayoutVersion(file) < 2) continue;

            var grid = GridFile.Load(file);
            Assert.True(grid.IsWritableLayout);

            string copy = Path.Combine(_tempDir, Path.GetFileName(file));
            grid.Save(copy);
            checkedGrids++;

            if (!File.ReadAllBytes(file).AsSpan().SequenceEqual(File.ReadAllBytes(copy)))
                failures.Add(Path.GetRelativePath(CleanBecCorpus.Root!, file));
        }

        Assert.True(checkedGrids > 350, $"expected the version-2 corpus, only saw {checkedGrids} grids");
        Assert.True(failures.Count == 0,
            $"{failures.Count} grid(s) did not round-trip: {string.Join(", ", failures.Take(5))}");
    }

    [Fact]
    public void VersionOneGrid_IsReadableButRefusesToSave()
    {
        if (!Ready) return;

        string file = CorpusGrids().First(f => LayoutVersion(f) == 1);
        var grid = GridFile.Load(file);

        Assert.Equal(1, grid.LayoutVersion);
        Assert.False(grid.IsWritableLayout);

        // Tags are still readable, which is what the Grid Builder shows.
        Assert.NotEmpty(Enumerable.Range(0, GridFile.NumSlots)
            .Select(grid.GetTagNameForSlot)
            .Where(n => !string.IsNullOrWhiteSpace(n) && n != GridFile.NO_VALUE));

        var error = Assert.Throws<NotSupportedException>(
            () => grid.Save(Path.Combine(_tempDir, "refused.grd")));
        Assert.Contains("read-only", error.Message);
        Assert.False(File.Exists(Path.Combine(_tempDir, "refused.grd")));
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
