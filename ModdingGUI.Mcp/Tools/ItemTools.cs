namespace ModdingGUI.Mcp.Tools;

using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using ModelContextProtocol.Server;
using ModdingGUI.Mcp.Server;
using ModdingGUI.Mcp.Server.Wire;

/// <summary>
/// <c>data/config/items.tok</c>: the item table (weapons, armour, accessories) addressed by stable id.
/// </summary>
/// <remarks>
/// This family has no advisory validator — the typed tok layer rejects a malformed value as it writes, and
/// there is no catalog of legal item fields to check against. <c>strict</c> is accepted for uniformity and
/// still reports findings, but it cannot refuse an item write.
/// </remarks>
[McpServerToolType]
public sealed class ItemTools(McpProject project)
{
    private const string NoValidator =
        "items.tok has no advisory validator, so strict cannot refuse this edit (malformed values are rejected " +
        "by the typed tok layer as it writes).";

    /// <summary>Every item id.</summary>
    [McpServerTool(Name = "items_list", ReadOnly = true, Idempotent = true)]
    [Description("List every item id in data/config/items.tok, in source order. Ids are the stable key every other items_* tool takes.")]
    public object List() =>
        ToolResult.Call(project, "items_list", () => project.Items.List(project.BecPath));

    /// <summary>Every item as a record.</summary>
    [McpServerTool(Name = "items_get_all", ReadOnly = true, Idempotent = true)]
    [Description(
        "Read every item in data/config/items.tok in one pass. Prefer this over items_get in a loop: each call " +
        "re-parses the whole file. Records are in source order, with affinity as {element, amount} and stat " +
        "modifiers as {stat, value}.")]
    public object GetAll() =>
        ToolResult.Call(project, "items_get_all",
            () => project.Items.GetAll(project.BecPath).Select(ItemWire.From).ToArray());

    /// <summary>One item.</summary>
    [McpServerTool(Name = "items_get", ReadOnly = true, Idempotent = true)]
    [Description(
        "Read one item by its stable id. Fails when no item has that id, so a typo is visible; use items_list " +
        "to see the ids.")]
    public object Get(
        [Description("Stable item id, e.g. \"Iron Sword\".")] string name) =>
        ToolResult.Call(project, $"items_get '{name}'", () =>
            project.Items.Get(project.BecPath, name) is { } item
                ? ItemWire.From(item)
                : ToolResult.NotFound($"item '{name}'", "Use items_list to see the ids."));

    /// <summary>Patch one item.</summary>
    [McpServerTool(Name = "items_patch")]
    [Description(
        "Set selected fields of one item and return the updated record. Omitted (null) fields are left alone. " +
        "List fields use add/remove verb pairs (RegionsAdd/RegionsRemove, SkillsAdd, …) and removes run before " +
        "adds. Affinity takes {element, amount}; set clearAffinity=true to delete the affinity line. Fails when " +
        "the id does not exist or a value is malformed for the file's type.")]
    public object Patch(
        [Description("Stable item id to edit.")] string name,
        [Description("Fields to change; anything omitted is left as it is.")] ItemPatchWire patch,
        [Description(NoValidator)] bool strict = false) =>
        project.Write($"items_patch '{name}'",
            () => ItemWire.From(project.Items.Patch(project.BecPath, name, patch.ToPatch())), strict);

    /// <summary>Append an item.</summary>
    [McpServerTool(Name = "items_add")]
    [Description(
        "Append a new item record and return it. Name is required and must not already exist (items_rename is " +
        "how an id changes). The record is written with the fields you supply; omitted fields stay absent.")]
    public object Add(
        [Description("The record to append; name identifies it.")] ItemWire item,
        [Description(NoValidator)] bool strict = false) =>
        project.Write($"items_add '{item.Name}'",
            () => ItemWire.From(project.Items.Add(project.BecPath, item.ToDto())), strict);

    /// <summary>Delete an item.</summary>
    [McpServerTool(Name = "items_remove", Destructive = true)]
    [Description(
        "Delete the item with this id and return true, or false when no item had it. The pre-delete file is in " +
        ".mod-history, so history_restore can undo it.")]
    public object Remove(
        [Description("Stable item id to delete.")] string name,
        [Description(NoValidator)] bool strict = false) =>
        project.Write($"items_remove '{name}'", () => project.Items.Remove(project.BecPath, name), strict);

    /// <summary>Rename an item.</summary>
    [McpServerTool(Name = "items_rename")]
    [Description(
        "Change an item's stable id and return the record. Fails when the new id is already used by another " +
        "item. Only the id in items.tok changes: nothing rewrites references to it in other files.")]
    public object Rename(
        [Description("Current stable id.")] string name,
        [Description("New stable id, unique within items.tok.")] string newName,
        [Description(NoValidator)] bool strict = false) =>
        project.Write($"items_rename '{name}' -> '{newName}'",
            () => ItemWire.From(project.Items.Rename(project.BecPath, name, newName)), strict);
}
