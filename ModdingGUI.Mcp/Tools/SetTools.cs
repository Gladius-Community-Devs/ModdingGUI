namespace ModdingGUI.Mcp.Tools;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using ModelContextProtocol.Server;
using ModdingGUI.Core.Api;
using ModdingGUI.Core.Units;
using ModdingGUI.Mcp.Server;

/// <summary>
/// <c>data/units/statsets.txt</c>: one set of level rows (level → CON, PWR, ACC, DEF, INI/MOV) per stat set.
/// </summary>
/// <remarks>
/// A gladiator's <c>Stat set</c> indexes this file (169 sets in vanilla). Each row is one level; a set with holes
/// in its level coverage is info, not a warning, because vanilla has them.
/// </remarks>
[McpServerToolType]
public sealed class StatSetTools(McpProject project)
{
    /// <summary>Every set index.</summary>
    [McpServerTool(Name = "statsets_list", ReadOnly = true, Idempotent = true)]
    [Description("List every stat-sets index in data/units/statsets.txt, in file order.")]
    public object List() =>
        ToolResult.Call(project, "statsets_list", () => project.StatSets.List(project.BecPath));

    /// <summary>One set.</summary>
    [McpServerTool(Name = "statsets_get", ReadOnly = true, Idempotent = true)]
    [Description("Read one stat set: its index and its level rows (level, con, pow, acc, def, iniMov). Fails when the index does not exist.")]
    public object Get(
        [Description("Set index a gladiator's Stat set points at.")] int index) =>
        ToolResult.Call(project, $"statsets_get {index}", () =>
            project.StatSets.Get(project.BecPath, index) is { } set
                ? set
                : ToolResult.NotFound($"stat set {index}", "Use statsets_list to see the indices."));

    /// <summary>Every set.</summary>
    [McpServerTool(Name = "statsets_get_all", ReadOnly = true, Idempotent = true)]
    [Description("Read every stat set in one pass (169 sets). Prefer this over statsets_get in a loop.")]
    public object GetAll() =>
        ToolResult.Call(project, "statsets_get_all", () => project.StatSets.GetAll(project.BecPath));

    /// <summary>Replace one level row.</summary>
    [McpServerTool(Name = "statsets_set_row")]
    [Description("Replace the values of one level row of a set (the level comes from the row). Fails when the set or that level does not exist.")]
    public object SetRow(
        [Description("Set index.")] int index,
        [Description("The replacement row; its level selects the row to overwrite.")] StatSetRow row,
        [Description("true refuses the edit when it introduces a validation warning.")] bool strict = false) =>
        project.WriteVoid($"statsets_set_row {index} level {row.Level}",
            () => project.StatSets.SetRow(project.BecPath, index, row), strict, Scope(index));

    /// <summary>Replace many rows in one pass.</summary>
    [McpServerTool(Name = "statsets_apply_many")]
    [Description(
        "Replace many level rows across sets in one pass (one read, one write, one backup). Rows whose level is " +
        "not in the set, or that are already identical, are skipped. Returns how many were written.")]
    public object ApplyMany(
        [Description("The rows to write, each naming its set index.")] IReadOnlyList<StatSetRowWrite> rows,
        [Description("true refuses the batch when it introduces a validation warning.")] bool strict = false) =>
        project.Write("statsets_apply_many",
            () => project.StatSets.ApplyMany(project.BecPath, rows.Select(entry => (entry.Index, entry.Row))),
            strict,
            () => project.StatSets.Validate(project.BecPath).Select(ValidationIssue.From).ToArray());

    /// <summary>Append a level row.</summary>
    [McpServerTool(Name = "statsets_add_row")]
    [Description("Append a level row to a set. Fails when the set does not exist or already has that level.")]
    public object AddRow(
        [Description("Set index.")] int index,
        [Description("The row to append.")] StatSetRow row,
        [Description("true refuses the edit when it introduces a validation warning.")] bool strict = false) =>
        project.WriteVoid($"statsets_add_row {index} level {row.Level}",
            () => project.StatSets.AddRow(project.BecPath, index, row), strict, Scope(index));

    /// <summary>Remove a level row.</summary>
    [McpServerTool(Name = "statsets_remove_row", Destructive = true)]
    [Description("Remove a level row from a set and return true, or false when that level is not in the set.")]
    public object RemoveRow(
        [Description("Set index.")] int index,
        [Description("Level to remove.")] int level,
        [Description("true refuses the edit when it introduces a validation warning.")] bool strict = false) =>
        project.Write($"statsets_remove_row {index} level {level}",
            () => project.StatSets.RemoveRow(project.BecPath, index, level), strict, Scope(index));

    /// <summary>Append a set.</summary>
    [McpServerTool(Name = "statsets_add_set")]
    [Description("Append a new, empty stat set and return its index. A set starts as you fill it with level rows.")]
    public object AddSet(
        [Description("accepted for uniformity; a new empty set has no rows to validate, so it cannot refuse.")] bool strict = false) =>
        project.Write("statsets_add_set", () => project.StatSets.AddSet(project.BecPath), strict);

    /// <summary>Remove a set.</summary>
    [McpServerTool(Name = "statsets_remove_set", Destructive = true)]
    [Description("Remove a whole stat set and return true, or false when the index does not exist. Gladiators that pointed at it are not rewritten.")]
    public object RemoveSet(
        [Description("Set index to remove.")] int index,
        [Description("accepted for uniformity; after the delete there is no set left to validate.")] bool strict = false) =>
        project.Write($"statsets_remove_set {index}", () => project.StatSets.RemoveSet(project.BecPath, index), strict);

    /// <summary>Validate one set or all of them.</summary>
    [McpServerTool(Name = "statsets_validate", ReadOnly = true, Idempotent = true)]
    [Description("Validate one stat set (or every set when index is omitted): level windows, duplicate levels, and the NUMENTRIES count.")]
    public object Validate(
        [Description("Set index, or omit to check every set.")] int? index = null) =>
        ToolResult.Call(project, $"statsets_validate {index?.ToString() ?? "(all)"}", () => Scope(index)());

    private Func<IReadOnlyList<ValidationIssue>> Scope(int index) => () =>
        project.StatSets.Validate(project.BecPath, index).Select(ValidationIssue.From).ToArray();

    private Func<IReadOnlyList<ValidationIssue>> Scope(int? index) => () =>
        project.StatSets.Validate(project.BecPath, index).Select(ValidationIssue.From).ToArray();
}

/// <summary>One row write for <c>statsets_apply_many</c>.</summary>
/// <param name="Index">Set index.</param>
/// <param name="Row">The row to write.</param>
public sealed record StatSetRowWrite(int Index, StatSetRow Row);

/// <summary>
/// <c>data/units/itemsets.txt</c>: one set of level rows (level → item name) per item set.
/// </summary>
/// <remarks>
/// A gladiator's <c>Item set</c> indexes this file (404 sets in vanilla). Each row names an item that must exist
/// in <c>items.tok</c>, which is what strict mode checks.
/// </remarks>
[McpServerToolType]
public sealed class ItemSetTools(McpProject project)
{
    /// <summary>Every set index.</summary>
    [McpServerTool(Name = "itemsets_list", ReadOnly = true, Idempotent = true)]
    [Description("List every item-sets index in data/units/itemsets.txt, in file order.")]
    public object List() =>
        ToolResult.Call(project, "itemsets_list", () => project.ItemSets.List(project.BecPath));

    /// <summary>One set.</summary>
    [McpServerTool(Name = "itemsets_get", ReadOnly = true, Idempotent = true)]
    [Description("Read one item set: its index and its rows (minLevel, maxLevel, itemName, note). Fails when the index does not exist.")]
    public object Get(
        [Description("Set index a gladiator's Item set points at.")] int index) =>
        ToolResult.Call(project, $"itemsets_get {index}", () =>
            project.ItemSets.Get(project.BecPath, index) is { } set
                ? set
                : ToolResult.NotFound($"item set {index}", "Use itemsets_list to see the indices."));

    /// <summary>Every set.</summary>
    [McpServerTool(Name = "itemsets_get_all", ReadOnly = true, Idempotent = true)]
    [Description("Read every item set in one pass (404 sets). Prefer this over itemsets_get in a loop.")]
    public object GetAll() =>
        ToolResult.Call(project, "itemsets_get_all", () => project.ItemSets.GetAll(project.BecPath));

    /// <summary>Replace one row by position.</summary>
    [McpServerTool(Name = "itemsets_set_row")]
    [Description("Replace one row of a set by its position (0-based). Fails when the set or the position does not exist.")]
    public object SetRow(
        [Description("Set index.")] int index,
        [Description("0-based row position within the set.")] int ordinal,
        [Description("The replacement row.")] ItemSetRow row,
        [Description("true refuses the edit when it introduces a validation warning.")] bool strict = false) =>
        project.WriteVoid($"itemsets_set_row {index} row {ordinal}",
            () => project.ItemSets.SetRow(project.BecPath, index, ordinal, row), strict, Scope(index));

    /// <summary>Replace many rows in one pass.</summary>
    [McpServerTool(Name = "itemsets_apply_many")]
    [Description(
        "Replace many rows across sets in one pass (one read, one write, one backup). Out-of-range positions and " +
        "identical rows are skipped. Returns how many were written.")]
    public object ApplyMany(
        [Description("The rows to write, each naming its set index and position.")] IReadOnlyList<ItemSetRowWrite> rows,
        [Description("true refuses the batch when it introduces a validation warning.")] bool strict = false) =>
        project.Write("itemsets_apply_many",
            () => project.ItemSets.ApplyMany(project.BecPath, rows.Select(entry => (entry.Index, entry.Ordinal, entry.Row))),
            strict,
            () => project.ItemSets.Validate(project.BecPath).Select(ValidationIssue.From).ToArray());

    /// <summary>Add a row.</summary>
    [McpServerTool(Name = "itemsets_add_row")]
    [Description("Add a row to a set, inserted in (minLevel, maxLevel, name) order. The item must exist in items.tok.")]
    public object AddRow(
        [Description("Set index.")] int index,
        [Description("The row to add.")] ItemSetRow row,
        [Description("true refuses the edit when it introduces a validation warning.")] bool strict = false) =>
        project.WriteVoid($"itemsets_add_row {index} '{row.ItemName}'",
            () => project.ItemSets.AddRow(project.BecPath, index, row), strict, Scope(index));

    /// <summary>Remove a row.</summary>
    [McpServerTool(Name = "itemsets_remove_row", Destructive = true)]
    [Description("Remove a row by position and return true, or false when the position is out of range.")]
    public object RemoveRow(
        [Description("Set index.")] int index,
        [Description("0-based row position.")] int ordinal,
        [Description("true refuses the edit when it introduces a validation warning.")] bool strict = false) =>
        project.Write($"itemsets_remove_row {index} row {ordinal}",
            () => project.ItemSets.RemoveRow(project.BecPath, index, ordinal), strict, Scope(index));

    /// <summary>Append a set.</summary>
    [McpServerTool(Name = "itemsets_add_set")]
    [Description("Append a new, empty item set and return its index.")]
    public object AddSet(
        [Description("accepted for uniformity; a new empty set has no rows to validate, so it cannot refuse.")] bool strict = false) =>
        project.Write("itemsets_add_set", () => project.ItemSets.AddSet(project.BecPath), strict);

    /// <summary>Remove a set.</summary>
    [McpServerTool(Name = "itemsets_remove_set", Destructive = true)]
    [Description("Remove a whole item set and return true, or false when the index does not exist.")]
    public object RemoveSet(
        [Description("Set index to remove.")] int index,
        [Description("accepted for uniformity; after the delete there is no set left to validate.")] bool strict = false) =>
        project.Write($"itemsets_remove_set {index}", () => project.ItemSets.RemoveSet(project.BecPath, index), strict);

    /// <summary>Validate one set or all of them.</summary>
    [McpServerTool(Name = "itemsets_validate", ReadOnly = true, Idempotent = true)]
    [Description(
        "Validate one item set (or every set when index is omitted): item names against items.tok, level windows, " +
        "duplicate entries and level coverage. Duplicate and coverage findings are info; an unknown item is a warning.")]
    public object Validate(
        [Description("Set index, or omit to check every set.")] int? index = null) =>
        ToolResult.Call(project, $"itemsets_validate {index?.ToString() ?? "(all)"}",
            () => project.ItemSets.Validate(project.BecPath, index).Select(ValidationIssue.From).ToArray());

    private Func<IReadOnlyList<ValidationIssue>> Scope(int index) => () =>
        project.ItemSets.Validate(project.BecPath, index).Select(ValidationIssue.From).ToArray();
}

/// <summary>One row write for <c>itemsets_apply_many</c>.</summary>
/// <param name="Index">Set index.</param>
/// <param name="Ordinal">0-based row position.</param>
/// <param name="Row">The replacement row.</param>
public sealed record ItemSetRowWrite(int Index, int Ordinal, ItemSetRow Row);

/// <summary>
/// <c>data/units/skillsets.txt</c>: one set of level rows (level range → skill name) per skill set.
/// </summary>
/// <remarks>
/// A gladiator's <c>Skill set</c> indexes this file (230 sets in vanilla). Each row names a skill that must exist
/// in <c>skills.tok</c> — a name that does not is what once killed the pack chain.
/// </remarks>
[McpServerToolType]
public sealed class SkillSetTools(McpProject project)
{
    /// <summary>Every set index.</summary>
    [McpServerTool(Name = "skillsets_list", ReadOnly = true, Idempotent = true)]
    [Description("List every skill-sets index in data/units/skillsets.txt, in file order.")]
    public object List() =>
        ToolResult.Call(project, "skillsets_list", () => project.SkillSets.List(project.BecPath));

    /// <summary>One set.</summary>
    [McpServerTool(Name = "skillsets_get", ReadOnly = true, Idempotent = true)]
    [Description("Read one skill set: its index and its rows (minLevel, maxLevel, skillName). Fails when the index does not exist.")]
    public object Get(
        [Description("Set index a gladiator's Skill set points at.")] int index) =>
        ToolResult.Call(project, $"skillsets_get {index}", () =>
            project.SkillSets.Get(project.BecPath, index) is { } set
                ? set
                : ToolResult.NotFound($"skill set {index}", "Use skillsets_list to see the indices."));

    /// <summary>Replace one row by position.</summary>
    [McpServerTool(Name = "skillsets_set_row")]
    [Description("Replace one row of a set by its position (0-based). Fails when the set or the position does not exist.")]
    public object SetRow(
        [Description("Set index.")] int index,
        [Description("0-based row position within the set.")] int ordinal,
        [Description("The replacement row.")] SkillSetRow row,
        [Description("true refuses the edit when it introduces a validation warning.")] bool strict = false) =>
        project.WriteVoid($"skillsets_set_row {index} row {ordinal}",
            () => project.SkillSets.SetRow(project.BecPath, index, ordinal, row), strict, Scope(index));

    /// <summary>Add a row.</summary>
    [McpServerTool(Name = "skillsets_add_row")]
    [Description("Add a row to a set, inserted in (minLevel, maxLevel, name) order. The skill must exist in skills.tok.")]
    public object AddRow(
        [Description("Set index.")] int index,
        [Description("The row to add.")] SkillSetRow row,
        [Description("true refuses the edit when it introduces a validation warning.")] bool strict = false) =>
        project.WriteVoid($"skillsets_add_row {index} '{row.SkillName}'",
            () => project.SkillSets.AddRow(project.BecPath, index, row), strict, Scope(index));

    /// <summary>Remove a row.</summary>
    [McpServerTool(Name = "skillsets_remove_row", Destructive = true)]
    [Description("Remove a row by position and return true, or false when the position is out of range.")]
    public object RemoveRow(
        [Description("Set index.")] int index,
        [Description("0-based row position.")] int ordinal,
        [Description("true refuses the edit when it introduces a validation warning.")] bool strict = false) =>
        project.Write($"skillsets_remove_row {index} row {ordinal}",
            () => project.SkillSets.RemoveRow(project.BecPath, index, ordinal), strict, Scope(index));

    /// <summary>Append a set.</summary>
    [McpServerTool(Name = "skillsets_add_set")]
    [Description("Append a new, empty skill set and return its index.")]
    public object AddSet(
        [Description("accepted for uniformity; a new empty set has no rows to validate, so it cannot refuse.")] bool strict = false) =>
        project.Write("skillsets_add_set", () => project.SkillSets.AddSet(project.BecPath), strict);

    /// <summary>Remove a set.</summary>
    [McpServerTool(Name = "skillsets_remove_set", Destructive = true)]
    [Description("Remove a whole skill set and return true, or false when the index does not exist.")]
    public object RemoveSet(
        [Description("Set index to remove.")] int index,
        [Description("accepted for uniformity; after the delete there is no set left to validate.")] bool strict = false) =>
        project.Write($"skillsets_remove_set {index}", () => project.SkillSets.RemoveSet(project.BecPath, index), strict);

    /// <summary>Validate one set or all of them.</summary>
    [McpServerTool(Name = "skillsets_validate", ReadOnly = true, Idempotent = true)]
    [Description(
        "Validate one skill set (or every set when index is omitted): skill names against skills.tok, level windows, " +
        "duplicate entries and level coverage. An unknown skill is a warning — the pack chain fails on it.")]
    public object Validate(
        [Description("Set index, or omit to check every set.")] int? index = null) =>
        ToolResult.Call(project, $"skillsets_validate {index?.ToString() ?? "(all)"}",
            () => project.SkillSets.Validate(project.BecPath, index).Select(ValidationIssue.From).ToArray());

    private Func<IReadOnlyList<ValidationIssue>> Scope(int index) => () =>
        project.SkillSets.Validate(project.BecPath, index).Select(ValidationIssue.From).ToArray();
}

/// <summary>
/// <c>data/units/tints.txt</c>: named colour channels (Cloth1, Skin, Hair, …) per tint set.
/// </summary>
/// <remarks>
/// A gladiator's <c>Tint set</c> indexes this file (196 sets in vanilla). Set 0 holds a paragraph of prose
/// instead of colours and is treated as a set with no rows; its text is never touched.
/// </remarks>
[McpServerToolType]
public sealed class TintSetTools(McpProject project)
{
    /// <summary>Every set index.</summary>
    [McpServerTool(Name = "tintsets_list", ReadOnly = true, Idempotent = true)]
    [Description("List every tint-sets index in data/units/tints.txt, in file order.")]
    public object List() =>
        ToolResult.Call(project, "tintsets_list", () => project.TintSets.List(project.BecPath));

    /// <summary>One set.</summary>
    [McpServerTool(Name = "tintsets_get", ReadOnly = true, Idempotent = true)]
    [Description("Read one tint set: its index and a channel → colour map (r, g, b, 0-255). Fails when the index does not exist.")]
    public object Get(
        [Description("Set index a gladiator's Tint set points at.")] int index) =>
        ToolResult.Call(project, $"tintsets_get {index}", () =>
            project.TintSets.Get(project.BecPath, index) is { } set
                ? set
                : ToolResult.NotFound($"tint set {index}", "Use tintsets_list to see the indices."));

    /// <summary>Set one channel's colour.</summary>
    [McpServerTool(Name = "tintsets_set_color")]
    [Description(
        "Set one channel's colour, rewriting that row (or adding it when the channel is missing). channel is the " +
        "name as written, e.g. Cloth1 or Skin.")]
    public object SetColor(
        [Description("Set index.")] int index,
        [Description("Channel name as written in the file.")] string channel,
        [Description("The colour: {r, g, b}, each 0-255.")] Rgb color,
        [Description("true refuses the edit when it introduces a validation warning.")] bool strict = false) =>
        project.WriteVoid($"tintsets_set_color {index} {channel}",
            () => project.TintSets.SetColor(project.BecPath, index, channel, color), strict, Scope(index));

    /// <summary>Append a set.</summary>
    [McpServerTool(Name = "tintsets_add_set")]
    [Description("Append a new, empty tint set and return its index.")]
    public object AddSet(
        [Description("accepted for uniformity; a new empty set has no rows to validate, so it cannot refuse.")] bool strict = false) =>
        project.Write("tintsets_add_set", () => project.TintSets.AddSet(project.BecPath), strict);

    /// <summary>Remove a set.</summary>
    [McpServerTool(Name = "tintsets_remove_set", Destructive = true)]
    [Description("Remove a tint set and return true, or false when the index does not exist.")]
    public object RemoveSet(
        [Description("Set index to remove.")] int index,
        [Description("accepted for uniformity; after the delete there is no set left to validate.")] bool strict = false) =>
        project.Write($"tintsets_remove_set {index}", () => project.TintSets.RemoveSet(project.BecPath, index), strict);

    /// <summary>Validate one set or all of them.</summary>
    [McpServerTool(Name = "tintsets_validate", ReadOnly = true, Idempotent = true)]
    [Description("Validate one tint set (or every set when index is omitted): unknown channels, channel counts and the NUMENTRIES count.")]
    public object Validate(
        [Description("Set index, or omit to check every set.")] int? index = null) =>
        ToolResult.Call(project, $"tintsets_validate {index?.ToString() ?? "(all)"}",
            () => project.TintSets.Validate(project.BecPath, index).Select(ValidationIssue.From).ToArray());

    private Func<IReadOnlyList<ValidationIssue>> Scope(int index) => () =>
        project.TintSets.Validate(project.BecPath, index).Select(ValidationIssue.From).ToArray();
}
