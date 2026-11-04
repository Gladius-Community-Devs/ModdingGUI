using System;
using System.IO;
using ModdingGUI.Core;
using ModdingGUI.Core.Grids;
using Xunit;

namespace ModdingGUI.Tests.Core;

/// <summary>
/// Grid tag parsing and game-path macros. 27 of the 430 vanilla grids are format version 1 and store
/// bare tag names; the loader used to discard them, which hid every slot from the Grid Builder and
/// made the encounter validator report working maps as broken.
/// </summary>
public sealed class GridFormatTests : IDisposable
{
    private readonly string _projectDir;

    public GridFormatTests()
    {
        _projectDir = Path.Combine(Path.GetTempPath(), "ModdingGUI.Grid-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_projectDir, "data", "encounters"));
        Directory.CreateDirectory(Path.Combine(_projectDir, "data", "levels"));
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_projectDir)) Directory.Delete(_projectDir, recursive: true); }
        catch (IOException) { /* best effort */ }
    }

    // ---- game path macros ---------------------------------------------------

    [Theory]
    [InlineData("$d\\encounters\\Nordagh\\TheFen\\arena.grd", "data\\encounters\\Nordagh\\TheFen\\arena.grd")]
    [InlineData("$l\\Nordagh\\thefen\\thefennomove.grd", "data\\levels\\Nordagh\\thefen\\thefennomove.grd")]
    [InlineData("data\\config\\x.tok", "data\\config\\x.tok")]
    [InlineData("\"$d\\encounters\\a.grd\"", "data\\encounters\\a.grd")]
    public void ToProjectRelative_ResolvesKnownMacros(string gamePath, string expected)
    {
        Assert.Equal(expected, GamePaths.ToProjectRelative(gamePath));
    }

    [Theory]
    [InlineData("$x\\unknown\\thing.pan")]
    [InlineData("C:\\absolute\\path.grd")]
    [InlineData("")]
    public void ToProjectRelative_ReturnsNullWhenItCannotResolve(string gamePath)
    {
        Assert.Null(GamePaths.ToProjectRelative(gamePath));
    }

    [Fact]
    public void ToProjectRelative_NullInput_ReturnsNull()
    {
        Assert.Null(GamePaths.ToProjectRelative(null));
    }

    // ---- grid versions ------------------------------------------------------

    [Fact]
    public void Load_VersionOneGrid_ExposesBareTagNames()
    {
        if (!CleanBecCorpus.IsAvailable) return;

        // A vanilla version-1 grid: tag blocks hold plain names such as "Start1".
        string path = FindGrid(version: 1);
        var grid = GridFile.Load(path);

        var names = Enumerable.Range(0, GridFile.NumSlots)
            .Select(grid.GetTagNameForSlot)
            .Where(n => !string.IsNullOrWhiteSpace(n) && n != GridFile.NO_VALUE)
            .ToArray();

        Assert.NotEmpty(names);
        Assert.Contains(names, n => n.StartsWith("Start", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Load_VersionTwoGrid_StillUsesThePrefixFlag()
    {
        if (!CleanBecCorpus.IsAvailable) return;

        string path = FindGrid(version: 2);
        var grid = GridFile.Load(path);

        var active = Enumerable.Range(0, GridFile.NumSlots)
            .Where(grid.IsSlotActive)
            .Select(grid.GetTagNameForSlot)
            .Where(n => !string.IsNullOrWhiteSpace(n) && n != GridFile.NO_VALUE)
            .ToArray();

        Assert.NotEmpty(active);
    }

    [Fact]
    public void VersionOneGrid_ReadsPerCellSlotLists()
    {
        if (!CleanBecCorpus.IsAvailable) return;

        // Legacy layout (FUN_800b0e84): a u16 count plus a u32 offset per cell, pointing at arrays of
        // slot indices. These maps paint whole regions - Start1 covers several tiles - which is why
        // several units may legally share one start slot.
        string? file = Directory
            .EnumerateFiles(Path.Combine(CleanBecCorpus.Root!, "data"), "*.grd", SearchOption.AllDirectories)
            .FirstOrDefault(f => Path.GetFileName(f)
                .Equals("belfortarenamultilevelstart.grd", StringComparison.OrdinalIgnoreCase));
        if (file is null) return;

        var grid = GridFile.Load(file);
        Assert.Equal(1, grid.LayoutVersion);

        var cellsPerTag = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int slot = 0; slot < GridFile.NumSlots; slot++)
        {
            string tag = grid.GetTagNameForSlot(slot);
            if (string.IsNullOrWhiteSpace(tag) || tag == GridFile.NO_VALUE) continue;

            int cells = 0;
            for (int row = 0; row < GridFile.ArenaSize; row++)
            {
                for (int col = 0; col < GridFile.ArenaSize; col++)
                {
                    if ((grid.GetValue(row, col) & (1 << slot)) != 0) cells++;
                }
            }
            cellsPerTag[tag] = cells;
        }

        Assert.True(cellsPerTag.ContainsKey("Start1"), "Start1 should be named");
        Assert.True(cellsPerTag["Start1"] > 1, $"Start1 should paint several tiles, saw {cellsPerTag["Start1"]}");
        Assert.All(cellsPerTag.Keys, tag => Assert.StartsWith("Start", tag, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void VersionOneAndTwoGridsBothReportTheirVersion()
    {
        if (!CleanBecCorpus.IsAvailable) return;

        Assert.NotNull(GridFile.Load(FindGrid(version: 1)));
        Assert.NotNull(GridFile.Load(FindGrid(version: 2)));
    }

    /// <summary>First corpus grid with the requested format version.</summary>
    private static string FindGrid(int version)
    {
        foreach (string file in Directory.EnumerateFiles(
                     Path.Combine(CleanBecCorpus.Root!, "data"), "*.grd", SearchOption.AllDirectories))
        {
            using var stream = File.OpenRead(file);
            Span<byte> header = stackalloc byte[4];
            if (stream.Read(header) < 4) continue;
            if (BitConverter.ToInt32(header) == version) return file;
        }

        throw new InvalidOperationException($"no version {version} grid in the corpus");
    }
}
