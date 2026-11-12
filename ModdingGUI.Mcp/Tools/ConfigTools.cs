namespace ModdingGUI.Mcp.Tools;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using ModelContextProtocol.Server;
using ModdingGUI.Mcp.Server;

/// <summary>
/// Flat <c>KEY value</c> tok tables: shops, <c>data/config</c> tables and the space-separated <c>.tnt</c> tints.
/// </summary>
/// <remarks>
/// The API is deliberately generic — these formats have no cross-field rules and no positional records, so one
/// generic surface beats a class per file. They also have no validator: a malformed line is whatever the file
/// already contains, and the writers preserve it.
/// </remarks>
[McpServerToolType]
public sealed class ConfigTools(McpProject project)
{
    /// <summary>Files under a directory.</summary>
    [McpServerTool(Name = "config_list_files", ReadOnly = true, Idempotent = true)]
    [Description(
        "List files under a project-relative directory, sorted, as forward-slash project-relative paths. These are " +
        "the paths the other config_* tools take. Examples: data/towns/shops with *.tok, data/config with *.tok, " +
        "data/config with *.tnt.")]
    public object ListFiles(
        [Description("Project-relative directory, e.g. data/towns/shops.")] string relativeDirectory,
        [Description("Search pattern, e.g. *.tok or *.tnt. Default *.tok.")] string searchPattern = "*.tok") =>
        ToolResult.Call(project, $"config_list_files '{relativeDirectory}' {searchPattern}",
            () => project.Config.ListFiles(project.BecPath, relativeDirectory, searchPattern));

    /// <summary>One table.</summary>
    [McpServerTool(Name = "config_get", ReadOnly = true, Idempotent = true)]
    [Description(
        "Read one flat tok table as its entries (key, rawValue) in file order; keys may repeat, e.g. a shop's item " +
        "lines. Fails when the file does not exist.")]
    public object Get(
        [Description("Project-relative path, e.g. data/towns/shops/altahrunruins_shops.tok.")] string path) =>
        ToolResult.Call(project, $"config_get '{path}'", () =>
            project.Config.Get(project.BecPath, path) is { } table
                ? table
                : ToolResult.NotFound($"config table '{path}'", "Use config_list_files to see the paths."));

    /// <summary>Replace the first value of a key.</summary>
    [McpServerTool(Name = "config_set")]
    [Description("Replace the first value of a key; further lines for the same key are dropped. Fails when the key is absent (use config_add).")]
    public object Set(
        [Description("Project-relative path.")] string path,
        [Description("Key as written, e.g. ITEM or GOLD.")] string key,
        [Description("Everything after the key, verbatim.")] string rawValue,
        [Description("config tables have no advisory validator, so strict cannot refuse this edit.")] bool strict = false) =>
        project.WriteVoid($"config_set '{path}' {key}",
            () => project.Config.Set(project.BecPath, path, key, rawValue), strict);

    /// <summary>Append a line for a key.</summary>
    [McpServerTool(Name = "config_add")]
    [Description("Append a new line for a key — for genuinely repeating keys such as a shop's ITEM lines.")]
    public object Add(
        [Description("Project-relative path.")] string path,
        [Description("Key as written.")] string key,
        [Description("Everything after the key, verbatim.")] string rawValue,
        [Description("config tables have no advisory validator, so strict cannot refuse this edit.")] bool strict = false) =>
        project.WriteVoid($"config_add '{path}' {key}",
            () => project.Config.Add(project.BecPath, path, key, rawValue), strict);

    /// <summary>Remove every line for a key.</summary>
    [McpServerTool(Name = "config_remove", Destructive = true)]
    [Description("Remove every line for a key from a table.")]
    public object Remove(
        [Description("Project-relative path.")] string path,
        [Description("Key to remove.")] string key,
        [Description("config tables have no advisory validator, so strict cannot refuse this edit.")] bool strict = false) =>
        project.WriteVoid($"config_remove '{path}' {key}",
            () => project.Config.Remove(project.BecPath, path, key), strict);

    /// <summary>Replace every line for a key.</summary>
    [McpServerTool(Name = "config_set_values")]
    [Description("Replace all lines for a key with the supplied values, in order — how a repeating key is rewritten in one write.")]
    public object SetValues(
        [Description("Project-relative path.")] string path,
        [Description("Key as written.")] string key,
        [Description("The complete list of values for the key, in order.")] IReadOnlyList<string> values,
        [Description("config tables have no advisory validator, so strict cannot refuse this edit.")] bool strict = false) =>
        project.WriteVoid($"config_set_values '{path}' {key}",
            () => project.Config.SetValues(project.BecPath, path, key, values), strict);
}
