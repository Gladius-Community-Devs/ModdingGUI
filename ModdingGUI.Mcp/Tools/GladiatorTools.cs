namespace ModdingGUI.Mcp.Tools;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using ModelContextProtocol.Server;
using ModdingGUI.Core.Units;
using ModdingGUI.Mcp.Server;

/// <summary>
/// <c>data/units/gladiators.txt</c>: every recruitable unit and the eight set indexes it points at.
/// </summary>
/// <remarks>
/// Each record names a class and indexes the stat, skill, item and tint sets plus the school-name table. Those
/// indexes are checked against the other units files, which is why writes here validate the affected record.
/// Validation of one name re-parses the whole table (2,170 records) and the unit catalog, so a patch costs a
/// couple of seconds; <c>gladiators_apply_many</c> does one pass for a whole batch.
/// </remarks>
[McpServerToolType]
public sealed class GladiatorTools(McpProject project)
{
    /// <summary>Every gladiator name.</summary>
    [McpServerTool(Name = "gladiators_list", ReadOnly = true, Idempotent = true)]
    [Description("List every gladiator name in data/units/gladiators.txt, in file order (2,170 in vanilla).")]
    public object List() =>
        ToolResult.Call(project, "gladiators_list", () => project.Gladiators.List(project.BecPath));

    /// <summary>One gladiator.</summary>
    [McpServerTool(Name = "gladiators_get", ReadOnly = true, Idempotent = true)]
    [Description(
        "Read one gladiator by name: class, outfit, affinity, and the tint/skill/stat/item set indexes plus its " +
        "school index. Fails when the name is unknown; gladiators_get_all is the one-pass bulk read.")]
    public object Get(
        [Description("Gladiator name.")] string name) =>
        ToolResult.Call(project, $"gladiators_get '{name}'", () =>
            project.Gladiators.Get(project.BecPath, name) is { } record
                ? record
                : ToolResult.NotFound($"gladiator '{name}'", "Use gladiators_list to see the names."));

    /// <summary>Every gladiator.</summary>
    [McpServerTool(Name = "gladiators_get_all", ReadOnly = true, Idempotent = true)]
    [Description(
        "Read every gladiator in one pass (2,170 records). Prefer this over gladiators_get in a loop: each single " +
        "read re-parses the whole table.")]
    public object GetAll() =>
        ToolResult.Call(project, "gladiators_get_all", () => project.Gladiators.GetAll(project.BecPath));

    /// <summary>Patch one gladiator.</summary>
    [McpServerTool(Name = "gladiators_patch")]
    [Description(
        "Change any subset of a gladiator's eight mutable values and return nothing (void). Omitted (null) values " +
        "are left alone. The set indexes must exist in their files and the class must exist in classdefs.tok, " +
        "which is what strict mode checks.")]
    public object Patch(
        [Description("Gladiator name.")] string name,
        [Description("Values to change; anything omitted is left as it is.")] GladiatorPatch patch,
        [Description("true refuses the edit when it introduces a validation warning.")] bool strict = false) =>
        project.WriteVoid($"gladiators_patch '{name}'",
            () => project.Gladiators.Patch(project.BecPath, name, patch), strict, Scope(name));

    /// <summary>Patch many gladiators in one pass.</summary>
    [McpServerTool(Name = "gladiators_apply_many")]
    [Description(
        "Apply many patches in one pass: the file is read once, every patch is applied and the result written " +
        "once, so a batch produces one backup instead of one per record. Returns how many records changed; names " +
        "that do not exist are ignored.")]
    public object ApplyMany(
        [Description("The patches to apply, each a name plus the values to change.")] IReadOnlyList<GladiatorPatchEntry> patches,
        [Description("true refuses the whole batch when it introduces a validation warning.")] bool strict = false) =>
        project.Write("gladiators_apply_many",
            () => project.Gladiators.ApplyMany(
                project.BecPath,
                patches.Select(entry => (entry.Name, entry.Patch))),
            strict,
            Scope(string.Empty));

    /// <summary>Rename a gladiator.</summary>
    [McpServerTool(Name = "gladiators_rename")]
    [Description("Rename a gladiator, refusing a name that is already taken. Nothing rewrites references to the old name.")]
    public object Rename(
        [Description("Current name.")] string name,
        [Description("New name, unique in the table.")] string newName,
        [Description("true refuses the edit when it introduces a validation warning.")] bool strict = false) =>
        project.WriteVoid($"gladiators_rename '{name}' -> '{newName}'",
            () => project.Gladiators.Rename(project.BecPath, name, newName), strict, Scope(newName));

    /// <summary>Append a gladiator.</summary>
    [McpServerTool(Name = "gladiators_add")]
    [Description("Append a new gladiator record. The name must be unique; the set indexes and class must resolve to pass strict mode.")]
    public object Add(
        [Description("The record to append.")] GladiatorDto record,
        [Description("true refuses the edit when it introduces a validation warning.")] bool strict = false) =>
        project.WriteVoid($"gladiators_add '{record.Name}'",
            () => project.Gladiators.Add(project.BecPath, record), strict, Scope(record.Name));

    /// <summary>Delete a gladiator.</summary>
    [McpServerTool(Name = "gladiators_remove", Destructive = true)]
    [Description("Remove a gladiator record and return true, or false when the name is unknown.")]
    public object Remove(
        [Description("Name to remove.")] string name,
        [Description("true refuses the edit when it introduces a validation warning.")] bool strict = false) =>
        project.Write($"gladiators_remove '{name}'", () => project.Gladiators.Remove(project.BecPath, name), strict);

    /// <summary>Validate one record or the whole table.</summary>
    [McpServerTool(Name = "gladiators_validate", ReadOnly = true, Idempotent = true)]
    [Description(
        "Validate one gladiator (or the whole table when name is omitted): class names against classdefs.tok, set " +
        "indexes against the counts in the other units files, affinity against the accepted spellings, and " +
        "duplicate names. Validating the whole table is slow; pass a name when you can.")]
    public object Validate(
        [Description("Gladiator name, or omit to check every record.")] string? name = null) =>
        ToolResult.Call(project, $"gladiators_validate '{name ?? "(all)"}'", () =>
            project.Gladiators.Validate(project.BecPath, string.IsNullOrWhiteSpace(name) ? null : name)
                .Select(ValidationIssue.From)
                .ToArray());

    /// <summary>
    /// Findings for one record (or the whole table when <paramref name="name"/> is empty). A name that does not
    /// exist yields no findings rather than an error, which is what the API does.
    /// </summary>
    private Func<IReadOnlyList<ValidationIssue>> Scope(string name) => () =>
        project.Gladiators.Validate(project.BecPath, name.Length == 0 ? null : name)
            .Select(ValidationIssue.From)
            .ToArray();
}

/// <summary>One entry of a batched gladiator patch.</summary>
/// <param name="Name">Gladiator name.</param>
/// <param name="Patch">Values to change.</param>
public sealed record GladiatorPatchEntry(string Name, GladiatorPatch Patch);
