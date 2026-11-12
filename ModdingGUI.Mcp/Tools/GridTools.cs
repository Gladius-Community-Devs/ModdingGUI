namespace ModdingGUI.Mcp.Tools;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using ModelContextProtocol.Server;
using ModdingGUI.Core.Grids;
using ModdingGUI.Mcp.Server;

/// <summary>
/// <c>data/levels/**/*.grd</c>: 32×32 battle maps whose cells are a bitmask of named tags (Start1, NoMove, …).
/// </summary>
/// <remarks>
/// <para>
/// Tag bit numbers are per file: <c>Start1</c> is bit 1 in one arena and bit 0 in another, so a client must
/// never assume a mask. <c>grids_set_tag</c> resolves the tag against the loaded file with
/// <see cref="GridFile.IndexOfTag"/> and takes care of it.
/// </para>
/// <para>
/// 27 of the 430 vanilla grids are layout version 1 (per-cell slot lists). Both layouts read and write, and a
/// no-op save is byte-identical, so editing a version-1 grid is safe; a rebuilt payload only happens after an
/// edit. There is no advisory validator for this family.
/// </para>
/// </remarks>
[McpServerToolType]
public sealed class GridTools(McpProject project)
{
    /// <summary>Every grid path.</summary>
    [McpServerTool(Name = "grids_list", ReadOnly = true, Idempotent = true)]
    [Description("List every .grd in the project as a forward-slash project-relative path, sorted.")]
    public object List() =>
        ToolResult.Call(project, "grids_list", () => project.Grids.List(project.BecPath));

    /// <summary>One grid, as cell values.</summary>
    [McpServerTool(Name = "grids_get", ReadOnly = true, Idempotent = true)]
    [Description(
        "Read one grid: its layout version, the tag held by each of the 30 slots, and the 32×32 cell values " +
        "(cells[row][col], one bitmask per cell). Use grids_describe when only the summary is needed.")]
    public object Get(
        [Description("Project-relative grid path, e.g. data/levels/expanse/expansedunes/expansedunesnomove.grd.")] string path) =>
        ToolResult.Call(project, $"grids_get '{path}'", () =>
            project.Grids.Get(project.BecPath, path) is { } grid
                ? Snapshot(path, grid)
                : ToolResult.NotFound($"grid '{path}'", "Use grids_list to see the paths."));

    /// <summary>One grid's summary.</summary>
    [McpServerTool(Name = "grids_describe", ReadOnly = true, Idempotent = true)]
    [Description("Summarise one grid without handing over its cells: path, layout version, writable, tag count and painted cell count.")]
    public object Describe(
        [Description("Project-relative grid path.")] string path) =>
        ToolResult.Call(project, $"grids_describe '{path}'", () =>
            project.Grids.Describe(project.BecPath, path) is { } summary
                ? summary
                : ToolResult.NotFound($"grid '{path}'", "Use grids_list to see the paths."));

    /// <summary>Write the whole cell map back.</summary>
    [McpServerTool(Name = "grids_save")]
    [Description(
        "Write a grid's 32×32 cell values back and return the backup path (null for a first-time write). This is " +
        "the pair of grids_get: post the same shape back after changing cells. Tags and slot assignments are kept " +
        "as loaded; only the cell values are replaced. Fails when a cell is outside 0..1023 or the grid is not 32×32.")]
    public object Save(
        [Description("Project-relative grid path.")] string path,
        [Description("32 rows of 32 cell values, each a bitmask of tag bits.")] IReadOnlyList<IReadOnlyList<int>> cells,
        [Description("grids have no advisory validator, so strict cannot refuse this edit.")] bool strict = false) =>
        project.Write($"grids_save '{path}'", () =>
        {
            GridFile grid = Require(path);

            if (cells.Count != GridFile.ArenaSize)
            {
                throw new McpToolException($"cells must have {GridFile.ArenaSize} rows, not {cells.Count}.");
            }

            for (int row = 0; row < GridFile.ArenaSize; row++)
            {
                if (cells[row].Count != GridFile.ArenaSize)
                {
                    throw new McpToolException(
                        $"row {row} must have {GridFile.ArenaSize} cells, not {cells[row].Count}.");
                }

                for (int col = 0; col < GridFile.ArenaSize; col++)
                {
                    int value = cells[row][col];
                    if (value < 0)
                    {
                        throw new McpToolException($"cell [{row}][{col}] is negative ({value}).");
                    }

                    grid.SetValue(row, col, value);
                }
            }

            return new GridSaved(path, project.Grids.Save(project.BecPath, path, grid));
        }, strict);

    /// <summary>Set or clear one tag on one cell.</summary>
    [McpServerTool(Name = "grids_set_tag")]
    [Description(
        "Set (on=true) or clear (on=false) one named tag on one cell and return the new cell value plus the backup " +
        "path. The tag is resolved against the loaded file, so per-file bit numbers cannot be got wrong. Fails with " +
        "the available tags when the name is not one this grid uses.")]
    public object SetTag(
        [Description("Project-relative grid path.")] string path,
        [Description("0-based row.")] int row,
        [Description("0-based column.")] int col,
        [Description("Tag name, case-insensitive, e.g. Start1 or NoMove.")] string tag,
        [Description("true to set the bit, false to clear it.")] bool on = true,
        [Description("grids have no advisory validator, so strict cannot refuse this edit.")] bool strict = false) =>
        project.Write($"grids_set_tag '{path}' [{row},{col}] {tag}", () =>
        {
            GridFile grid = Require(path);
            CheckCell(row, col);

            int slot = grid.IndexOfTag(tag);
            if (slot < 0)
            {
                string[] available = Enumerable.Range(0, GridFile.NumSlots)
                    .Select(index => grid.GetTagNameForSlot(index))
                    .Where(name => !string.IsNullOrWhiteSpace(name) && name != GridFile.NO_VALUE)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                throw new McpToolException(
                    $"'{tag}' is not a tag this grid uses. Available: " +
                    (available.Length == 0 ? "(none — the grid declares no tags)" : string.Join(", ", available)));
            }

            int value = grid.GetValue(row, col);
            int mask = 1 << slot;
            int updated = on ? value | mask : value & ~mask;
            grid.SetValue(row, col, updated);

            return new GridTagResult(
                row, col, tag, slot, on, value, updated,
                project.Grids.Save(project.BecPath, path, grid));
        }, strict);

    /// <summary>Set one cell to a raw value.</summary>
    [McpServerTool(Name = "grids_set_value")]
    [Description(
        "Set one cell to a raw bitmask value and return the backup path. Use grids_set_tag unless you already know " +
        "the file's bit numbers; this is the escape hatch for a mask you computed yourself.")]
    public object SetValue(
        [Description("Project-relative grid path.")] string path,
        [Description("0-based row.")] int row,
        [Description("0-based column.")] int col,
        [Description("New raw cell value (a bitmask of tag bits).")] int value,
        [Description("grids have no advisory validator, so strict cannot refuse this edit.")] bool strict = false) =>
        project.Write($"grids_set_value '{path}' [{row},{col}] = {value}", () =>
        {
            GridFile grid = Require(path);
            CheckCell(row, col);

            int before = grid.GetValue(row, col);
            grid.SetValue(row, col, value);
            return new GridTagResult(
                row, col, string.Empty, -1, value != 0, before, value,
                project.Grids.Save(project.BecPath, path, grid));
        }, strict);

    private GridFile Require(string path) =>
        project.Grids.Get(project.BecPath, path)
        ?? throw new McpToolException($"grid '{path}' was not found. Use grids_list to see the paths.");

    private static void CheckCell(int row, int col)
    {
        if (row < 0 || row >= GridFile.ArenaSize || col < 0 || col >= GridFile.ArenaSize)
        {
            throw new McpToolException(
                $"cell [{row},{col}] is outside the {GridFile.ArenaSize}×{GridFile.ArenaSize} arena.");
        }
    }

    private static GridSnapshot Snapshot(string path, GridFile grid)
    {
        var slots = new List<GridSlotInfo>(GridFile.NumSlots);
        for (int slot = 0; slot < GridFile.NumSlots; slot++)
        {
            slots.Add(new GridSlotInfo(slot, grid.GetTagNameForSlot(slot), grid.IsSlotActive(slot)));
        }

        // GridFile.GetValue is public, but its backing array is not, so the cells are copied row by row.
        var cells = new int[GridFile.ArenaSize][];
        for (int row = 0; row < GridFile.ArenaSize; row++)
        {
            cells[row] = new int[GridFile.ArenaSize];
            for (int col = 0; col < GridFile.ArenaSize; col++) cells[row][col] = grid.GetValue(row, col);
        }

        return new GridSnapshot(path, grid.LayoutVersion, grid.IsWritableLayout, slots, cells);
    }
}

/// <summary>One grid, as <c>grids_get</c> answers.</summary>
/// <param name="Path">Project-relative path.</param>
/// <param name="LayoutVersion">Layout version: 1 (legacy) or 2.</param>
/// <param name="IsWritable">True when the layout is understood and can be written.</param>
/// <param name="Slots">The 30 slots: which tag each holds and whether it is active.</param>
/// <param name="Cells">32 rows of 32 cell values.</param>
public sealed record GridSnapshot(
    string Path,
    int LayoutVersion,
    bool IsWritable,
    IReadOnlyList<GridSlotInfo> Slots,
    IReadOnlyList<IReadOnlyList<int>> Cells);

/// <summary>One of the 30 tag slots of a grid.</summary>
/// <param name="Slot">Slot index, which is also the bit number in a cell value.</param>
/// <param name="Tag">Tag name at that slot (<c>**NONE**</c> for an unused slot).</param>
/// <param name="Active">Whether the slot is active in this file.</param>
public sealed record GridSlotInfo(int Slot, string Tag, bool Active);

/// <summary>What a grid write changed, and where the previous bytes went.</summary>
/// <param name="Path">Project-relative grid path.</param>
/// <param name="BackupPath">The backup the write created, or null for a first-time write.</param>
public sealed record GridSaved(string Path, string? BackupPath);

/// <summary>The outcome of setting one cell.</summary>
/// <param name="Row">0-based row.</param>
/// <param name="Col">0-based column.</param>
/// <param name="Tag">Tag name, or empty for a raw value write.</param>
/// <param name="Slot">Bit number the tag occupies, or -1 for a raw value write.</param>
/// <param name="On">Whether the bit was set.</param>
/// <param name="Before">Cell value before the write.</param>
/// <param name="After">Cell value after the write.</param>
/// <param name="BackupPath">The backup the write created.</param>
public sealed record GridTagResult(
    int Row,
    int Col,
    string Tag,
    int Slot,
    bool On,
    int Before,
    int After,
    string? BackupPath);
