namespace ModdingGUI.Mcp.Tools;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using ModelContextProtocol.Server;
using ModdingGUI.Core.Api;
using ModdingGUI.Mcp.Server;

/// <summary>
/// <c>data/school/*.tok</c>: a roster (NAME, HERO, GOLD) plus one <c>CREATEUNIT</c> block per starting unit.
/// </summary>
/// <remarks>
/// A unit block is a unit name and class on the <c>CREATEUNIT</c> line, then its fields — <c>SKILL</c> repeats,
/// the rest are single. The two campaign rosters the game actually loads are <c>valensimperia.tok</c> and
/// <c>ursulanordagh.tok</c>; the rest of <c>data/school</c> are test rosters.
/// </remarks>
[McpServerToolType]
public sealed class SchoolTools(McpProject project)
{
    /// <summary>Every roster path.</summary>
    [McpServerTool(Name = "schools_list_files", ReadOnly = true, Idempotent = true)]
    [Description("List the project-relative paths of every roster in data/school, sorted. These are the paths the other schools_* tools take.")]
    public object ListFiles() =>
        ToolResult.Call(project, "schools_list_files", () => project.Schools.ListFiles(project.BecPath));

    /// <summary>Every roster name.</summary>
    [McpServerTool(Name = "schools_list", ReadOnly = true, Idempotent = true)]
    [Description("List every roster's NAME, in path order.")]
    public object List() =>
        ToolResult.Call(project, "schools_list", () => project.Schools.List(project.BecPath));

    /// <summary>One parsed roster.</summary>
    [McpServerTool(Name = "schools_get", ReadOnly = true, Idempotent = true)]
    [Description(
        "Read one roster: header fields (NAME, HERO, GOLD, …) and each CREATEUNIT block with its name, class, " +
        "fields, granted skills and five equipment slots. Fails when the path does not exist.")]
    public object Get(
        [Description("Project-relative roster path, e.g. data/school/ursulanordagh.tok.")] string path) =>
        ToolResult.Call(project, $"schools_get '{path}'", () =>
            project.Schools.Get(project.BecPath, path) is { } roster
                ? roster
                : ToolResult.NotFound($"roster '{path}'", "Use schools_list_files to see the paths."));

    /// <summary>Replace a roster header field.</summary>
    [McpServerTool(Name = "schools_set_header_field")]
    [Description("Replace a header field (NAME, HERO, GOLD). rawValue is everything after the colon, e.g. \"\\\"Ursula\\\"\" or 9999.")]
    public object SetHeaderField(
        [Description("Project-relative roster path.")] string path,
        [Description("Header key as written.")] string key,
        [Description("Everything after the colon, verbatim.")] string rawValue,
        [Description("true refuses the edit when it introduces a validation warning.")] bool strict = false) =>
        project.WriteVoid($"schools_set_header_field '{path}' {key}",
            () => project.Schools.SetHeaderField(project.BecPath, path, key, rawValue), strict, Scope(path));

    /// <summary>Replace a unit's class.</summary>
    [McpServerTool(Name = "schools_set_unit_class")]
    [Description(
        "Replace the class on a unit's CREATEUNIT line, keeping the trailing comment. The class is the line's " +
        "second argument, not a field, which is why it has its own tool. The class must exist in classdefs.tok " +
        "for the unit to work in game.")]
    public object SetUnitClass(
        [Description("Project-relative roster path.")] string path,
        [Description("Unit name (first CREATEUNIT argument, case-insensitive).")] string unitName,
        [Description("New class name, e.g. Amazon.")] string className,
        [Description("true refuses the edit when it introduces a validation warning.")] bool strict = false) =>
        project.WriteVoid($"schools_set_unit_class '{path}' {unitName} -> {className}",
            () => project.Schools.SetUnitClass(project.BecPath, path, unitName, className), strict, Scope(path));

    /// <summary>Replace a unit field.</summary>
    [McpServerTool(Name = "schools_set_unit_field")]
    [Description(
        "Replace a single-valued field of a unit block: LEVEL, JOBPOINTS, CORESTATSCOMP2 (CON, PWR, ACC, DEF, " +
        "INI, MOVE), ITEMSCOMP (five quoted slot names), CUSTOMIZE, EXPERIENCE. Fails when the unit or key does " +
        "not exist; for repeatable keys such as SKILL use the skill tools.")]
    public object SetUnitField(
        [Description("Project-relative roster path.")] string path,
        [Description("Unit name.")] string unitName,
        [Description("Field key as written.")] string key,
        [Description("Everything after the colon, verbatim.")] string rawValue,
        [Description("true refuses the edit when it introduces a validation warning.")] bool strict = false) =>
        project.WriteVoid($"schools_set_unit_field '{path}' {unitName} {key}",
            () => project.Schools.SetUnitField(project.BecPath, path, unitName, key, rawValue), strict, Scope(path));

    /// <summary>Append a unit field line.</summary>
    [McpServerTool(Name = "schools_add_unit_field")]
    [Description("Append a field to a unit block, for repeatable keys such as SKILL (the skill tools are the better way for those).")]
    public object AddUnitField(
        [Description("Project-relative roster path.")] string path,
        [Description("Unit name.")] string unitName,
        [Description("Field key.")] string key,
        [Description("Everything after the colon, verbatim.")] string rawValue,
        [Description("true refuses the edit when it introduces a validation warning.")] bool strict = false) =>
        project.WriteVoid($"schools_add_unit_field '{path}' {unitName} {key}",
            () => project.Schools.AddUnitField(project.BecPath, path, unitName, key, rawValue), strict, Scope(path));

    /// <summary>Remove a unit field line.</summary>
    [McpServerTool(Name = "schools_remove_unit_field", Destructive = true)]
    [Description("Remove every line for a key from a unit block.")]
    public object RemoveUnitField(
        [Description("Project-relative roster path.")] string path,
        [Description("Unit name.")] string unitName,
        [Description("Field key to remove.")] string key,
        [Description("true refuses the edit when it introduces a validation warning.")] bool strict = false) =>
        project.WriteVoid($"schools_remove_unit_field '{path}' {unitName} {key}",
            () => project.Schools.RemoveUnitField(project.BecPath, path, unitName, key), strict, Scope(path));

    /// <summary>Replace a unit's granted skills.</summary>
    [McpServerTool(Name = "schools_set_unit_skills")]
    [Description(
        "Replace the whole granted-skill list of a unit, in order. Each name must be a skill in skills.tok; a " +
        "name that is not is a warning (and the pack chain fails on it), which is exactly what strict mode refuses.")]
    public object SetUnitSkills(
        [Description("Project-relative roster path.")] string path,
        [Description("Unit name.")] string unitName,
        [Description("The complete list of skill names to grant.")] IReadOnlyList<string> skills,
        [Description("true refuses the edit when it introduces a validation warning.")] bool strict = false) =>
        project.WriteVoid($"schools_set_unit_skills '{path}' {unitName}",
            () => project.Schools.SetUnitSkills(project.BecPath, path, unitName, skills), strict, Scope(path));

    /// <summary>Grant one skill.</summary>
    [McpServerTool(Name = "schools_add_unit_skill")]
    [Description("Add one granted skill to a unit.")]
    public object AddUnitSkill(
        [Description("Project-relative roster path.")] string path,
        [Description("Unit name.")] string unitName,
        [Description("Skill name to grant.")] string skill,
        [Description("true refuses the edit when it introduces a validation warning.")] bool strict = false) =>
        project.WriteVoid($"schools_add_unit_skill '{path}' {unitName} {skill}",
            () => project.Schools.AddUnitSkill(project.BecPath, path, unitName, skill), strict, Scope(path));

    /// <summary>Delete a unit block.</summary>
    [McpServerTool(Name = "schools_remove_unit", Destructive = true)]
    [Description("Delete a unit block from a roster and return true, or false when the roster had no such unit.")]
    public object RemoveUnit(
        [Description("Project-relative roster path.")] string path,
        [Description("Unit name to delete.")] string unitName,
        [Description("true refuses the edit when it introduces a validation warning.")] bool strict = false) =>
        project.Write($"schools_remove_unit '{path}' {unitName}",
            () => project.Schools.RemoveUnit(project.BecPath, path, unitName), strict, Scope(path));

    /// <summary>Append unit blocks.</summary>
    [McpServerTool(Name = "schools_add_units")]
    [Description(
        "Append new unit blocks to a roster in one write and return how many were added. Each unit's name must not " +
        "already exist. This is how a whole team is written at once.")]
    public object AddUnits(
        [Description("Project-relative roster path.")] string path,
        [Description("The unit blocks to append.")] IReadOnlyList<SchoolUnitDto> units,
        [Description("true refuses the edit when it introduces a validation warning.")] bool strict = false) =>
        project.Write($"schools_add_units '{path}'",
            () => project.Schools.AddUnits(project.BecPath, path, units), strict, Scope(path));

    /// <summary>Replace a whole roster.</summary>
    [McpServerTool(Name = "schools_replace_roster")]
    [Description(
        "Replace a whole roster: header (name, hero, gold) followed by the given unit blocks in order. Safe to " +
        "discard a roster's contents because a roster is only read when a new game starts; an in-progress run " +
        "keeps its own <name>Saved.tok. Returns true.")]
    public object ReplaceRoster(
        [Description("Project-relative roster path.")] string path,
        [Description("New NAME value.")] string name,
        [Description("New HERO value.")] string hero,
        [Description("New GOLD value.")] int gold,
        [Description("The complete list of unit blocks.")] IReadOnlyList<SchoolUnitDto> units,
        [Description("true refuses the edit when it introduces a validation warning.")] bool strict = false) =>
        project.WriteVoid($"schools_replace_roster '{path}'",
            () => project.Schools.ReplaceRoster(project.BecPath, path, name, hero, gold, units), strict, Scope(path));

    /// <summary>Validate one roster.</summary>
    [McpServerTool(Name = "schools_validate", ReadOnly = true, Idempotent = true)]
    [Description(
        "Validate one roster without touching disk: unknown header/unit keys, skill names that are not in " +
        "skills.tok, item names that do not resolve, stat/outfit values out of range and JP below what the granted " +
        "skills cost. Warnings are what strict mode refuses; a roster with no HERO is info.")]
    public object Validate(
        [Description("Project-relative roster path.")] string path) =>
        ToolResult.Call(project, $"schools_validate '{path}'", () => Scope(path)());

    private Func<IReadOnlyList<ValidationIssue>> Scope(string path) => () =>
        project.Schools.Validate(project.BecPath, path).Select(ValidationIssue.From).ToArray();
}

/// <summary>
/// <c>data/units/schools.txt</c>: the 144-entry index of school <em>names</em> a gladiator's School value points at.
/// </summary>
/// <remarks>
/// This is not the roster format — rosters are <c>data/school/*.tok</c> and are handled by the schools_* tools.
/// This file only maps an index to a display name.
/// </remarks>
[McpServerToolType]
public sealed class SchoolNameTools(McpProject project)
{
    /// <summary>Every entry.</summary>
    [McpServerTool(Name = "schoolnames_list", ReadOnly = true, Idempotent = true)]
    [Description("List every school-name entry (index, name) in data/units/schools.txt, in file order.")]
    public object List() =>
        ToolResult.Call(project, "schoolnames_list", () => project.SchoolNames.List(project.BecPath));

    /// <summary>One entry.</summary>
    [McpServerTool(Name = "schoolnames_get", ReadOnly = true, Idempotent = true)]
    [Description("Read the school name at an index. Fails when the index is not in the table.")]
    public object Get(
        [Description("School index a gladiator's School value uses.")] int index) =>
        ToolResult.Call(project, $"schoolnames_get {index}", () =>
            project.SchoolNames.Get(project.BecPath, index) is { } name
                ? name
                : ToolResult.NotFound($"school index {index}", "Use schoolnames_list to see the indices."));

    /// <summary>Rename an entry.</summary>
    [McpServerTool(Name = "schoolnames_set_name")]
    [Description("Rename the school at an index in place. Fails when the index is not in the table.")]
    public object SetName(
        [Description("School index.")] int index,
        [Description("New display name.")] string name,
        [Description("true refuses the edit when it introduces a validation warning.")] bool strict = false) =>
        project.WriteVoid($"schoolnames_set_name {index} -> '{name}'",
            () => project.SchoolNames.SetName(project.BecPath, index, name), strict, Scope());

    /// <summary>Append an entry.</summary>
    [McpServerTool(Name = "schoolnames_add")]
    [Description("Append a school name with the next free index and return that index.")]
    public object Add(
        [Description("Display name.")] string name,
        [Description("true refuses the edit when it introduces a validation warning.")] bool strict = false) =>
        project.Write($"schoolnames_add '{name}'", () => project.SchoolNames.Add(project.BecPath, name), strict, Scope());

    /// <summary>Remove the last entry.</summary>
    [McpServerTool(Name = "schoolnames_remove_last", Destructive = true)]
    [Description("Remove the highest-numbered school-name entry and return true, or false when the table is empty.")]
    public object RemoveLast(
        [Description("true refuses the edit when it introduces a validation warning.")] bool strict = false) =>
        project.Write("schoolnames_remove_last", () => project.SchoolNames.RemoveLast(project.BecPath), strict, Scope());

    /// <summary>Validate the table.</summary>
    [McpServerTool(Name = "schoolnames_validate", ReadOnly = true, Idempotent = true)]
    [Description("Validate data/units/schools.txt: gaps or duplicates in the index sequence, and an unusable header line.")]
    public object Validate() =>
        ToolResult.Call(project, "schoolnames_validate", () => Scope()());

    private Func<IReadOnlyList<ValidationIssue>> Scope() => () =>
        project.SchoolNames.Validate(project.BecPath).Select(ValidationIssue.From).ToArray();
}
