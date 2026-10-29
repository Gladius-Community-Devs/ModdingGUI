namespace ModdingGUI.Core.Skills;

using System;
using System.Collections.Generic;
using System.Linq;
using ModdingGUI.Core.Api;

/// <summary>How badly a validation finding should be taken.</summary>
public enum SkillValidationSeverity
{
    /// <summary>
    /// Catalog-derived guess. Emitted when a value or field name is not in the
    /// catalog, which is expected for modded data the shipped catalog predates —
    /// so it is never treated as an error.
    /// </summary>
    Info,

    /// <summary>
    /// A structural fact about the record itself (missing positionals, wrong
    /// token shape, illegal repetition, broken link/requirement, dangling
    /// lookup id). Still advisory: the GUI always writes, MCP may opt to reject.
    /// </summary>
    Warning,
}

/// <summary>One advisory validation finding for a skill record.</summary>
public sealed class SkillValidationIssue
{
    /// <summary>Severity of the finding.</summary>
    public SkillValidationSeverity Severity { get; init; }

    /// <summary>Stable machine-readable code (see <see cref="SkillValidationCodes"/>).</summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>Field the finding is about, when applicable.</summary>
    public string? Field { get; init; }

    /// <summary>Human-readable message, suitable for a status bar or log line.</summary>
    public string Message { get; init; } = string.Empty;

    /// <inheritdoc />
    public override string ToString() =>
        string.IsNullOrEmpty(Field) ? $"[{Code}] {Message}" : $"[{Code}] {Field}: {Message}";
}

/// <summary>Stable issue codes emitted by <see cref="SkillsValidator"/>.</summary>
public static class SkillValidationCodes
{
    /// <summary>One of the three <c>SKILLCREATE</c> positionals is empty.</summary>
    public const string MissingPositional = "missing-positional";

    /// <summary>The field name does not appear in the catalog.</summary>
    public const string UnknownField = "unknown-field";

    /// <summary>A string argument is not among the catalog's known values for that slot.</summary>
    public const string UnknownEnumValue = "unknown-enum-value";

    /// <summary>A slot documented as numeric received a quoted string or identifier.</summary>
    public const string NumericShape = "numeric-shape";

    /// <summary>A single-cardinality field appears more than once in the record.</summary>
    public const string DuplicateSingleCardinality = "duplicate-single-cardinality";

    /// <summary><c>SKILLDISPLAYNAMEID</c>/<c>SKILLDESCRIPTIONID</c> is absent from <c>lookuptext_eng.txt</c>.</summary>
    public const string DanglingLookupId = "dangling-lookup-id";

    /// <summary>A field requires a partner field that is not present.</summary>
    public const string MissingLinkedField = "missing-linked-field";

    /// <summary>A field's documented prerequisite is absent or has the wrong value.</summary>
    public const string MissingRequirement = "missing-requirement";
}

/// <summary>Knobs for <see cref="SkillsValidator.Validate"/>.</summary>
public sealed class SkillValidationOptions
{
    /// <summary>
    /// When true (default), rules that need project files (lookup-text ids) run.
    /// Set false to validate purely from the catalog — used by tests that do not
    /// stage a project, and by callers that have no <see cref="SkillsContext"/>.
    /// </summary>
    public bool CheckExternalReferences { get; init; } = true;

    /// <summary>Default options: catalog + external references.</summary>
    public static SkillValidationOptions Default { get; } = new();

    /// <summary>Catalog-only options: no project files are read.</summary>
    public static SkillValidationOptions StructuralOnly { get; } = new() { CheckExternalReferences = false };
}

/// <summary>How a mutating call should treat validation findings.</summary>
public enum SkillValidationMode
{
    /// <summary>Do not validate (plain write, matches the pre-2.7 behaviour).</summary>
    None,

    /// <summary>Validate, write regardless, and return the findings to the caller (GUI behaviour).</summary>
    Warn,

    /// <summary>Validate and throw <see cref="SkillValidationException"/> when any warning is found (MCP <c>strict: true</c>).</summary>
    Reject,
}

/// <summary>Thrown when a write is rejected because validation produced warnings.</summary>
public sealed class SkillValidationException : Exception
{
    /// <summary>Create the exception for <paramref name="skillName"/> with its findings.</summary>
    public SkillValidationException(string skillName, IReadOnlyList<SkillValidationIssue> issues)
        : base(BuildMessage(skillName, issues))
    {
        Issues = issues;
    }

    /// <summary>The findings that caused the rejection.</summary>
    public IReadOnlyList<SkillValidationIssue> Issues { get; }

    private static string BuildMessage(string skillName, IReadOnlyList<SkillValidationIssue> issues)
    {
        int warnings = issues.Count(i => i.Severity == SkillValidationSeverity.Warning);
        string first = issues.FirstOrDefault(i => i.Severity == SkillValidationSeverity.Warning)?.ToString()
                       ?? "unknown validation failure";
        return $"Skill '{skillName}' failed strict validation with {warnings} warning(s): {first}";
    }
}

/// <summary>Outcome of a validated mutation.</summary>
public sealed class SkillMutationResult
{
    /// <summary>The record as written.</summary>
    public SkillDto Skill { get; init; } = new();

    /// <summary>Advisory findings produced before the write.</summary>
    public IReadOnlyList<SkillValidationIssue> Issues { get; init; } = Array.Empty<SkillValidationIssue>();

    /// <summary>True when any finding is a <see cref="SkillValidationSeverity.Warning"/>.</summary>
    public bool HasWarnings => Issues.Any(i => i.Severity == SkillValidationSeverity.Warning);
}
