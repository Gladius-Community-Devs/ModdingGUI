namespace ModdingGUI.Mcp.Tools;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using ModelContextProtocol.Server;
using ModdingGUI.Core.Api;
using ModdingGUI.Core.Skills;
using ModdingGUI.Mcp.Server;

/// <summary>
/// <c>data/config/skills.tok</c> and the skills catalog.
/// </summary>
/// <remarks>
/// Skills are freeform: a record is three positionals plus raw body lines, so there is no typed schema and
/// none is planned. The catalog (<c>skills/skills_catalog.json</c>) is what makes validation useful, and
/// <c>skills_catalog_describe_field</c> hands it to a client so a record can be built without guessing.
/// Skills is the one family whose API already implements strict mode
/// (<see cref="SkillValidationMode.Reject"/>), so the two <c>…_validated</c> tools use it directly instead of
/// the generic write gate.
/// </remarks>
[McpServerToolType]
public sealed class SkillTools(McpProject project)
{
    /// <summary>Every skill name.</summary>
    [McpServerTool(Name = "skills_list", ReadOnly = true, Idempotent = true)]
    [Description("List every skill name in data/config/skills.tok, in source order (1,811 in vanilla).")]
    public object List() =>
        ToolResult.Call(project, "skills_list", () => project.Skills.List(project.BecPath));

    /// <summary>Every skill record.</summary>
    [McpServerTool(Name = "skills_get_all", ReadOnly = true, Idempotent = true)]
    [Description(
        "Read every skill in one pass (preferred over skills_get in a loop). Each record is its three " +
        "SKILLCREATE positionals plus the body fields verbatim, in source order.")]
    public object GetAll() =>
        ToolResult.Call(project, "skills_get_all", () => project.Skills.GetAll(project.BecPath));

    /// <summary>One skill record.</summary>
    [McpServerTool(Name = "skills_get", ReadOnly = true, Idempotent = true)]
    [Description("Read one skill by name. Fails when no skill has that name; use skills_list to see the names.")]
    public object Get(
        [Description("Skill name, e.g. \"Cleave\".")] string name) =>
        ToolResult.Call(project, $"skills_get '{name}'", () =>
            project.Skills.Get(project.BecPath, name) is { } skill
                ? skill
                : ToolResult.NotFound($"skill '{name}'", "Use skills_list to see the names."));

    /// <summary>Replace one skill record.</summary>
    [McpServerTool(Name = "skills_update")]
    [Description(
        "Replace the body of an existing skill with the record you supply and return it. Fields are written " +
        "verbatim (no re-typing), so this is how a raw line is edited. With strict=true the record is validated " +
        "first and nothing is written when it has a warning; with strict=false it is written regardless and the " +
        "findings are returned in warnings. Prefer skills_update_validated to also get the findings on success.")]
    public object Update(
        [Description("Name of the skill to replace.")] string name,
        [Description("The complete new record: name, skillType, skillSubType and the field entries in order.")] SkillDto skill,
        [Description("true validates the record first and refuses to write one with a warning.")] bool strict = false) =>
        project.WriteChecked($"skills_update '{name}'",
            () => project.Skills.Validate(project.BecPath, skill).Select(ValidationIssue.From).ToArray(),
            () => project.Skills.Update(project.BecPath, name, skill),
            strict);

    /// <summary>Replace one skill record, returning the findings too.</summary>
    [McpServerTool(Name = "skills_update_validated")]
    [Description(
        "Replace an existing skill through the API's own advisory-validation path and return { skill, issues, " +
        "hasWarnings }. strict=false behaves like the GUI (SkillValidationMode.Warn: validate, write, report); " +
        "strict=true is SkillValidationMode.Reject: validate first and fail without writing when the record has " +
        "a warning. Use this when you want the findings of a successful write as well as of a refusal.")]
    public object UpdateValidated(
        [Description("Name of the skill to replace.")] string name,
        [Description("The complete new record.")] SkillDto skill,
        [Description("true refuses to write a record that has a warning.")] bool strict = false) =>
        ToolResult.Call(project, $"skills_update_validated '{name}'", () =>
            project.Skills.Update(project.BecPath, name, skill, Mode(strict)));

    /// <summary>Append a skill.</summary>
    [McpServerTool(Name = "skills_add")]
    [Description(
        "Append a new skill record and return it. The name must not already exist. With strict=true the record " +
        "is validated first and nothing is written when it has a warning.")]
    public object Add(
        [Description("The record to append: name, skillType, skillSubType and its field entries.")] SkillDto skill,
        [Description("true validates the record first and refuses to write one with a warning.")] bool strict = false) =>
        project.WriteChecked($"skills_add '{skill.Name}'",
            () => project.Skills.Validate(project.BecPath, skill).Select(ValidationIssue.From).ToArray(),
            () => project.Skills.Add(project.BecPath, skill),
            strict);

    /// <summary>Append a skill, returning the findings too.</summary>
    [McpServerTool(Name = "skills_add_validated")]
    [Description(
        "Append a new skill through the API's advisory-validation path and return { skill, issues, hasWarnings }. " +
        "strict=false validates and writes anyway (the GUI's behaviour); strict=true refuses to write a record " +
        "with a warning. Use skills_catalog_describe_field to build a record the catalog agrees with.")]
    public object AddValidated(
        [Description("The record to append.")] SkillDto skill,
        [Description("true refuses to write a record that has a warning.")] bool strict = false) =>
        ToolResult.Call(project, $"skills_add_validated '{skill.Name}'", () =>
            project.Skills.Add(project.BecPath, skill, Mode(strict)));

    /// <summary>Delete a skill.</summary>
    [McpServerTool(Name = "skills_remove", Destructive = true)]
    [Description(
        "Delete the skill with this name and return true, or false when no skill had it. References to it in " +
        "skillsets.tok or a roster are not rewritten.")]
    public object Remove(
        [Description("Skill name to delete.")] string name,
        [Description("skills.tok has no file validator in this family; the flag is accepted and reported but cannot refuse.")] bool strict = false) =>
        project.Write($"skills_remove '{name}'", () => project.Skills.Remove(project.BecPath, name), strict);

    /// <summary>Validate a skill without writing it.</summary>
    [McpServerTool(Name = "skills_validate", ReadOnly = true, Idempotent = true)]
    [Description(
        "Validate a skill record without touching disk and return its findings. Catalog-derived findings (an " +
        "unknown field or enum value) are severity info because a shipped catalog cannot know modded values; " +
        "structural problems are warnings. Use this before skills_add to see what strict mode would refuse.")]
    public object Validate(
        [Description("The record to check.")] SkillDto skill) =>
        ToolResult.Call(project, "skills_validate",
            () => project.Skills.Validate(project.BecPath, skill).Select(ValidationIssue.From).ToArray());

    /// <summary>Describe one catalog field.</summary>
    [McpServerTool(Name = "skills_catalog_describe_field", ReadOnly = true, Idempotent = true)]
    [Description(
        "Describe one skills.tok field from the shipped catalog: its slot types, the known enum values, numeric " +
        "bounds, whether it may repeat, its links/requirements and the reference note. Use it to construct a " +
        "SkillDto the catalog agrees with instead of guessing field names or argument order. Also accepts the " +
        "name of a skill category (e.g. \"innate\", \"attack\") for a summary of what the catalog knows.")]
    public object DescribeField(
        [Description("Catalog field name, e.g. SKILLEFFECTRANGE. Case and underscores are normalised.")] string fieldName)
    {
        return ToolResult.Call(project, $"skills_catalog_describe_field '{fieldName}'", () =>
        {
            SkillsCatalog catalog = SkillsCatalog.FindForProject(project.BecPath, project.AppDirectory)
                ?? throw new McpToolException(
                    "no skills catalog found: expected skills/skills_catalog.json beside the server or in the " +
                    "project. It ships with the app; regenerate it with `py -3 skills/build_catalog.py`.");

            if (string.IsNullOrWhiteSpace(fieldName))
            {
                throw new McpToolException(
                    "pass a field name. Known fields: " + string.Join(", ", catalog.FieldNames));
            }

            SkillFieldSpec? spec = catalog.Find(fieldName);
            if (spec is null)
            {
                // A near miss is the common case (SKILLEFFECTRANGE vs EFFECTRANGE). Say what exists.
                string normalized = SkillsCatalog.NormalizeFieldName(fieldName);
                string[] close = catalog.FieldNames
                    .Where(name => name.Contains(normalized, StringComparison.OrdinalIgnoreCase)
                                   || normalized.Contains(name, StringComparison.OrdinalIgnoreCase))
                    .Take(20)
                    .ToArray();

                throw new McpToolException(
                    $"'{fieldName}' is not a catalog field. " +
                    (close.Length > 0
                        ? "Did you mean: " + string.Join(", ", close) + "?"
                        : "Known fields: " + string.Join(", ", catalog.FieldNames.Take(40)) +
                          (catalog.FieldNames.Count > 40 ? ", …" : string.Empty)));
            }

            return new SkillFieldDescription(
                spec.Name,
                spec.Summary,
                spec.Format,
                spec.Multi,
                spec.NumericType,
                spec.NumericMin,
                spec.NumericMax,
                spec.Slots
                    .Select(slot => new SkillSlotDescription(slot.Types, slot.Values, slot.Note))
                    .ToArray(),
                spec.Mirrors,
                spec.Links,
                spec.Note,
                spec.EngineKnown,
                spec.MaxOccurrences,
                spec.EngineSlot,
                spec.Requirements.Select(requirement => requirement.Raw ?? string.Empty)
                    .Where(raw => raw.Length > 0)
                    .ToArray());
        });
    }

    private static SkillValidationMode Mode(bool strict) =>
        strict ? SkillValidationMode.Reject : SkillValidationMode.Warn;
}

/// <summary>One catalog field, as <c>skills_catalog_describe_field</c> answers.</summary>
/// <param name="Field">Catalog field name.</param>
/// <param name="Summary">The reference file's one-line summary.</param>
/// <param name="Format">The documented argument format, when the catalog has one.</param>
/// <param name="Multi">True when the field may appear more than once.</param>
/// <param name="NumericType">Numeric type when the slot is numeric.</param>
/// <param name="NumericMin">Minimum for a numeric slot.</param>
/// <param name="NumericMax">Maximum for a numeric slot.</param>
/// <param name="Slots">The positional slots and their accepted values.</param>
/// <param name="Mirrors">Fields that must carry the same value.</param>
/// <param name="Links">Fields that must be present together with this one.</param>
/// <param name="Note">The longer reference note.</param>
/// <param name="EngineKnown">False when the engine's field table does not know the field (the line is ignored).</param>
/// <param name="MaxOccurrences">How many slots the engine has for the field.</param>
/// <param name="EngineSlot">The engine's own name for the field, when it differs.</param>
/// <param name="Requirements">Documented prerequisites, verbatim.</param>
public sealed record SkillFieldDescription(
    string Field,
    string? Summary,
    string? Format,
    bool Multi,
    string? NumericType,
    long? NumericMin,
    long? NumericMax,
    IReadOnlyList<SkillSlotDescription> Slots,
    IReadOnlyList<string> Mirrors,
    IReadOnlyList<string> Links,
    string? Note,
    bool? EngineKnown,
    int? MaxOccurrences,
    string? EngineSlot,
    IReadOnlyList<string> Requirements);

/// <summary>One positional slot of a catalog field.</summary>
/// <param name="Types">Slot type names (e.g. <c>int</c>, <c>enum</c>, <c>string</c>).</param>
/// <param name="Values">Known enum values for the slot.</param>
/// <param name="Note">The reference file's remark about the slot.</param>
public sealed record SkillSlotDescription(IReadOnlyList<string> Types, IReadOnlyList<string> Values, string? Note);
