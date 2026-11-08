using System;
using System.IO;
using System.Linq;
using ModdingGUI.Core.Api;
using ModdingGUI.Core.Grids;
using Xunit;

namespace ModdingGUI.Tests.Core.Api;

/// <summary>
/// <see cref="GridsApi"/> is the only sanctioned way for the shell to write a grid: it takes the
/// project lock and backs the previous bytes up, so a grid edit is as recoverable as a text edit.
/// </summary>
public sealed class GridsApiTests : IDisposable
{
    private readonly string _projectRoot;
    private readonly string _bec;
    private const string Relative = "data/encounters/test/map.grd";

    public GridsApiTests()
    {
        _projectRoot = Path.Combine(Path.GetTempPath(), "ModdingGUI.GridsApi-" + Guid.NewGuid().ToString("N"));
        _bec = Path.Combine(_projectRoot, "proj_BEC");
        Directory.CreateDirectory(Path.Combine(_bec, "data", "encounters", "test"));

        // Stage a real grid so the API is exercised against vanilla bytes.
        string source = Directory
            .EnumerateFiles(Path.Combine(CleanBecCorpus.Root!, "data"), "*.grd", SearchOption.AllDirectories)
            .FirstOrDefault() ?? throw new InvalidOperationException("no grid in the corpus");
        File.Copy(source, Path.Combine(_bec, Relative.Replace('/', Path.DirectorySeparatorChar)));
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_projectRoot)) Directory.Delete(_projectRoot, recursive: true); }
        catch (IOException) { /* best effort */ }
    }

    private bool Ready => CleanBecCorpus.IsAvailable;

    [Fact]
    public void List_ReturnsProjectRelativeGridPaths()
    {
        if (!Ready) return;

        var files = new GridsApi().List(_projectRoot);
        Assert.Contains(Relative, files);
    }

    [Fact]
    public void Describe_ReportsLayoutAndPaintedCells()
    {
        if (!Ready) return;

        var info = new GridsApi().Describe(_projectRoot, Relative);
        Assert.NotNull(info);
        Assert.InRange(info!.LayoutVersion, 1, 2);
        Assert.True(info.PaintedCells > 0);
    }

    [Fact]
    public void Save_WritesBackupThenWritesTheEditedGrid()
    {
        if (!Ready) return;

        string full = Path.Combine(_bec, Relative.Replace('/', Path.DirectorySeparatorChar));
        byte[] before = File.ReadAllBytes(full);

        var api = new GridsApi();
        var grid = api.Get(_projectRoot, Relative);
        Assert.NotNull(grid);

        int slot = Enumerable.Range(0, GridFile.NumSlots)
            .First(s => grid!.IsSlotActive(s) && grid.GetTagNameForSlot(s) != GridFile.NO_VALUE);
        grid!.SetValue(0, 0, 1 << slot);

        string? backup = api.Save(_projectRoot, Relative, grid);

        Assert.NotNull(backup);
        Assert.True(File.Exists(backup!), "a .mod-history backup must exist after saving");
        Assert.Equal(before, File.ReadAllBytes(backup!));
        Assert.Contains(".mod-history", backup!, StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual(before, File.ReadAllBytes(full));

        // And the new bytes reload through the same reader.
        var reloaded = api.Get(_projectRoot, Relative);
        Assert.NotNull(reloaded);
        Assert.Equal(1 << slot, reloaded!.GetValue(0, 0) & (1 << slot));
    }

    [Fact]
    public void Save_WithoutEdits_LeavesTheFileUntouched()
    {
        if (!Ready) return;

        string full = Path.Combine(_bec, Relative.Replace('/', Path.DirectorySeparatorChar));
        byte[] before = File.ReadAllBytes(full);

        var api = new GridsApi();
        api.Save(_projectRoot, Relative, api.Get(_projectRoot, Relative)!);

        Assert.Equal(before, File.ReadAllBytes(full));
    }

    [Fact]
    public void Save_WritesIntoAProvinceArenaFolderThatDoesNotExistYet()
    {
        if (!Ready) return;

        // Vanilla keeps grids under data/encounters/<province>/<arena>/. A save must land there even when the
        // arena folder has to be created, which is the shape the Grid Builder uses.
        string target = Path.Combine(_bec, "data", "encounters", "expanse", "expansedunes", "qstsarenenutetexdunes.grd");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetDirectoryName(target))!);

        var grid = GridFile.Load(Path.Combine(_bec, Relative.Replace('/', Path.DirectorySeparatorChar)));

        // A file that does not exist yet has nothing to back up; the write must still land.
        string? firstBackup = new GridsApi().Save(target, grid);
        Assert.True(File.Exists(target));
        Assert.Null(firstBackup);
        Assert.StartsWith(Path.Combine(_bec, "data", "encounters", "expanse", "expansedunes"), target);

        // The second save has a previous version, so it is backed up beside the project history.
        string? secondBackup = new GridsApi().Save(target, grid);
        Assert.NotNull(secondBackup);
        Assert.True(File.Exists(secondBackup!));
    }

    [Fact]
    public void Get_RejectsPathsThatEscapeTheProject()
    {
        if (!Ready) return;

        Assert.Throws<InvalidOperationException>(
            () => new GridsApi().Get(_projectRoot, "../../outside.grd"));
    }

    /// <summary>First cell whose value does not already include <paramref name="slot"/>.</summary>
    private static (int Row, int Col) FindCellWithout(GridFile grid, int slot)
    {
        for (int row = 0; row < GridFile.ArenaSize; row++)
        {
            for (int col = 0; col < GridFile.ArenaSize; col++)
            {
                if ((grid.GetValue(row, col) & (1 << slot)) == 0) return (row, col);
            }
        }

        throw new InvalidOperationException("every cell already carries that slot");
    }

    [Fact]
    public void Save_WithTheGridsAbsolutePath_OverwritesItAndKeepsABackup()
    {
        if (!Ready) return;

        // This is how the Grid Builder saves: it knows the file it opened, not the project layout.
        string full = Path.Combine(_bec, Relative.Replace('/', Path.DirectorySeparatorChar));
        byte[] before = File.ReadAllBytes(full);

        var api = new GridsApi();
        var grid = api.Get(_projectRoot, Relative)!;
        int slot = Enumerable.Range(0, GridFile.NumSlots)
            .First(s => grid.IsSlotActive(s) && grid.GetTagNameForSlot(s) != GridFile.NO_VALUE);

        // Paint a cell that does not already carry this slot, so the bytes must change.
        var (row, col) = FindCellWithout(grid, slot);
        grid.SetValue(row, col, grid.GetValue(row, col) | (1 << slot));

        string? backup = api.Save(full, grid);

        Assert.NotNull(backup);
        Assert.Equal(before, File.ReadAllBytes(backup!));
        Assert.True(File.Exists(full), "the grid must be overwritten, not recreated elsewhere");
        Assert.NotEqual(before, File.ReadAllBytes(full));

        // Saving a second time must overwrite again rather than fail on an existing file.
        string? second = api.Save(full, api.Get(_projectRoot, Relative)!);
        Assert.NotNull(second);
        Assert.NotEqual(backup, second);
    }

    [Fact]
    public void Save_WhenTheGridPathIsAFile_DoesNotTryToCreateADirectoryOverIt()
    {
        if (!Ready) return;

        // The reported failure: "Cannot create '<...>.grd' because a file or directory with the same
        // name already exists." It came from resolving the project root out of the file's own path.
        string full = Path.Combine(_bec, Relative.Replace('/', Path.DirectorySeparatorChar));
        var api = new GridsApi();

        var exception = Record.Exception(() => api.Save(full, api.Get(_projectRoot, Relative)!));

        Assert.Null(exception);
    }
}
