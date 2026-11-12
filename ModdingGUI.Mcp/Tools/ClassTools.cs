namespace ModdingGUI.Mcp.Tools;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using ModelContextProtocol.Server;
using ModdingGUI.Core.Api;
using ModdingGUI.Mcp.Server;

/// <summary>
/// <c>data/config/classdefs.tok</c>: one <c>CREATECLASS:</c> block per class, edited as raw field lines.
/// </summary>
/// <remarks>
/// A class block mixes positional-ish argument lists with free text, so it is exposed verbatim: keys and raw
/// values, in file order. Validation is the generated class catalog's — an unknown key or a field the
/// engine's table does not know is a warning, because the game ignores those lines silently.
/// </remarks>
[McpServerToolType]
public sealed class ClassTools(McpProject project)
{
    /// <summary>Every class name.</summary>
    [McpServerTool(Name = "classes_list", ReadOnly = true, Idempotent = true)]
    [Description("List every class name in data/config/classdefs.tok, in file order.")]
    public object List() =>
        ToolResult.Call(project, "classes_list", () => project.Classes.List(project.BecPath));

    /// <summary>Every class block.</summary>
    [McpServerTool(Name = "classes_get_all", ReadOnly = true, Idempotent = true)]
    [Description(
        "Read every class block in one pass: name plus its field lines (key, rawValue) in file order. Prefer " +
        "this over classes_get in a loop.")]
    public object GetAll() =>
        ToolResult.Call(project, "classes_get_all", () => project.Classes.GetAll(project.BecPath));

    /// <summary>One class block.</summary>
    [McpServerTool(Name = "classes_get", ReadOnly = true, Idempotent = true)]
    [Description("Read one class block by name. Fails when the class does not exist; use classes_list to see the names.")]
    public object Get(
        [Description("Class name as written after CREATECLASS:, e.g. \"Amazon\".")] string className) =>
        ToolResult.Call(project, $"classes_get '{className}'", () =>
            project.Classes.Get(project.BecPath, className) is { } classDef
                ? classDef
                : ToolResult.NotFound($"class '{className}'", "Use classes_list to see the class names."));

    /// <summary>Replace one field value.</summary>
    [McpServerTool(Name = "classes_set_field")]
    [Description(
        "Replace a class field's value: the first matching line is rewritten in place and any further lines for " +
        "the same key are dropped. rawValue is everything after the colon, e.g. \"Amazon\" or \"2, 2, 0, 1, 0, 1\". " +
        "Fails when the class or the key does not exist (use classes_add_field for a new key).")]
    public object SetField(
        [Description("Class name.")] string className,
        [Description("Field key as written, e.g. LEVELSTATAWARDS.")] string key,
        [Description("Everything after the colon, verbatim.")] string rawValue,
        [Description("true refuses the edit when it introduces a validation warning.")] bool strict = false) =>
        project.WriteVoid($"classes_set_field '{className}' {key}", () => project.Classes.SetField(project.BecPath, className, key, rawValue),
            strict, Scope(className));

    /// <summary>Append a field line.</summary>
    [McpServerTool(Name = "classes_add_field")]
    [Description(
        "Append a key line at the end of a class block, for keys that may repeat (e.g. ATTRIBUTE). Fails when the " +
        "class does not exist.")]
    public object AddField(
        [Description("Class name.")] string className,
        [Description("Field key as written.")] string key,
        [Description("Everything after the colon, verbatim.")] string rawValue,
        [Description("true refuses the edit when it introduces a validation warning.")] bool strict = false) =>
        project.WriteVoid($"classes_add_field '{className}' {key}", () => project.Classes.AddField(project.BecPath, className, key, rawValue),
            strict, Scope(className));

    /// <summary>Remove every line for a key.</summary>
    [McpServerTool(Name = "classes_remove_field", Destructive = true)]
    [Description("Remove every line for a key from a class block. Fails when the class does not exist.")]
    public object RemoveField(
        [Description("Class name.")] string className,
        [Description("Field key to remove.")] string key,
        [Description("true refuses the edit when it introduces a validation warning.")] bool strict = false) =>
        project.WriteVoid($"classes_remove_field '{className}' {key}", () => project.Classes.RemoveField(project.BecPath, className, key),
            strict, Scope(className));

    /// <summary>Rename a class.</summary>
    [McpServerTool(Name = "classes_rename")]
    [Description(
        "Rename a class by rewriting its CREATECLASS header. References to the class in gladiators.txt and the " +
        "encounters are not rewritten, so a rename usually orphans them; prefer classes_set_field for values.")]
    public object Rename(
        [Description("Current class name.")] string className,
        [Description("New class name.")] string newName,
        [Description("accepted for uniformity; a rename has no stable scope to validate, so it cannot refuse.")] bool strict = false) =>
        project.WriteVoid($"classes_rename '{className}' -> '{newName}'", () => project.Classes.Rename(project.BecPath, className, newName), strict);

    /// <summary>Add a class block.</summary>
    [McpServerTool(Name = "classes_add")]
    [Description(
        "Add a new class block at the end of classdefs.tok and return true. Supply the CREATE-only fields you " +
        "want; the block is exactly the lines you give. With strict=true the new block is validated after it is " +
        "written and reverted when it introduces a warning.")]
    public object Add(
        [Description("New class name.")] string className,
        [Description("Field lines for the block, in order.")] IReadOnlyList<ClassFieldDto> fields,
        [Description("true refuses the edit when it introduces a validation warning.")] bool strict = false) =>
        project.Write($"classes_add '{className}'", () =>
        {
            project.Classes.Add(project.BecPath, className, fields);
            return true;
        }, strict, Scope(className, tolerateMissing: true));

    /// <summary>Remove a class block.</summary>
    [McpServerTool(Name = "classes_remove", Destructive = true)]
    [Description(
        "Remove a class block, including the blank separator lines that follow it. References to the class are " +
        "not rewritten. The pre-delete file is in .mod-history, so history_restore can undo it.")]
    public object Remove(
        [Description("Class name to remove.")] string className,
        [Description("accepted for uniformity; after a delete there is no record left to validate.")] bool strict = false) =>
        project.WriteVoid($"classes_remove '{className}'", () => project.Classes.Remove(project.BecPath, className), strict);

    /// <summary>Validate one class block.</summary>
    [McpServerTool(Name = "classes_validate", ReadOnly = true, Idempotent = true)]
    [Description(
        "Validate one class block against the generated class catalog without touching disk. Unknown keys and " +
        "keys the engine ignores are warnings; argument-shape remarks are info.")]
    public object Validate(
        [Description("Class name.")] string className) =>
        ToolResult.Call(project, $"classes_validate '{className}'", () => Scope(className)());

    /// <summary>
    /// The findings of one class block. A block that does not exist (yet, for an add, or any more) has none,
    /// which is what the API's "not found" exception means here.
    /// </summary>
    private Func<IReadOnlyList<ValidationIssue>> Scope(string className, bool tolerateMissing = false) => () =>
    {
        try
        {
            return project.Classes.Validate(project.BecPath, className).Select(ValidationIssue.From).ToArray();
        }
        catch (Exception ex) when (tolerateMissing && ex is InvalidOperationException or KeyNotFoundException)
        {
            return Array.Empty<ValidationIssue>();
        }
    };
}
