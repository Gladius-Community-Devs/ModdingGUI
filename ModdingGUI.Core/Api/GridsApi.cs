using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ModdingGUI.Core.Grids;

namespace ModdingGUI.Core.Api;

/// <summary>A grid file in a project, addressed the same way the other APIs address files.</summary>
public sealed record GridFileDto(string Path, int LayoutVersion, int TagCount, int PaintedCells)
{
    /// <summary>True when the layout is fully understood and can be written.</summary>
    public bool IsWritable => LayoutVersion is 1 or 2;
}

/// <summary>
/// Read and write battle grids (<c>.grd</c>). Every write takes the project lock and writes a
/// timestamped backup into <c>.mod-history</c> first, exactly like the text APIs, so a grid edit made
/// from the Grid Builder is as recoverable as any other edit.
/// </summary>
public interface IGridsApi
{
    /// <summary>Every grid in the project, as project-relative paths using forward slashes.</summary>
    IReadOnlyList<string> List(string projectRoot);

    /// <summary>Load a grid, or null when the path does not exist or cannot be parsed.</summary>
    GridFile? Get(string projectRoot, string relativePath);

    /// <summary>Summary of a grid without handing out the mutable model.</summary>
    GridFileDto? Describe(string projectRoot, string relativePath);

    /// <summary>
    /// Write <paramref name="grid"/> back to the project after taking the lock and backing up the
    /// previous bytes. Returns the backup path (null for a first-time write).
    /// </summary>
    string? Save(string projectRoot, string relativePath, GridFile grid);
}

/// <summary>Default <see cref="IGridsApi"/>.</summary>
public sealed class GridsApi : IGridsApi
{
    private static readonly Regex BecFolder = new(@"_BEC$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <inheritdoc />
    public IReadOnlyList<string> List(string projectRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        ProjectRoots.ResolveBecFolder(projectRoot);

        string bec = ProjectRoots.ResolveBecFolder(projectRoot);
        return Directory
            .EnumerateFiles(bec, "*.grd", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(bec, path).Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <inheritdoc />
    public GridFile? Get(string projectRoot, string relativePath)
    {
        string full = Resolve(projectRoot, relativePath);
        if (!File.Exists(full)) return null;

        try
        {
            return GridFile.Load(full);
        }
        catch (Exception ex) when (ex is IOException or EndOfStreamException or InvalidDataException)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public GridFileDto? Describe(string projectRoot, string relativePath)
    {
        var grid = Get(projectRoot, relativePath);
        if (grid is null) return null;

        int tags = 0;
        int painted = 0;
        for (int slot = 0; slot < GridFile.NumSlots; slot++)
        {
            string tag = grid.GetTagNameForSlot(slot);
            if (string.IsNullOrWhiteSpace(tag) || tag == GridFile.NO_VALUE) continue;

            int bit = 1 << slot;
            int cells = 0;
            for (int row = 0; row < GridFile.ArenaSize; row++)
            {
                for (int col = 0; col < GridFile.ArenaSize; col++)
                {
                    if ((grid.GetValue(row, col) & bit) != 0) cells++;
                }
            }

            if (cells > 0)
            {
                tags++;
                painted += cells;
            }
        }

        return new GridFileDto(relativePath.Replace('\\', '/'), grid.LayoutVersion, tags, painted);
    }

    /// <inheritdoc />
    public string? Save(string projectRoot, string relativePath, GridFile grid)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        ArgumentNullException.ThrowIfNull(grid);

        string full = Resolve(projectRoot, relativePath);
        string root = ProjectRoots.ResolveBecFolder(projectRoot);

        using (ProjectLock.Acquire(root))
        {
            string? backup = ChangeHistoryService.Backup(root, full);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            grid.Save(full);
            return backup;
        }
    }

    /// <summary>Absolute path of a project-relative grid, rejecting paths that escape the project.</summary>
    private static string Resolve(string projectRoot, string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        string bec = ProjectRoots.ResolveBecFolder(projectRoot);
        string full = Path.GetFullPath(Path.Combine(bec, relativePath.Replace('/', Path.DirectorySeparatorChar)));

        if (!full.StartsWith(Path.GetFullPath(bec), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"'{relativePath}' is outside the project.");

        return full;
    }
}
