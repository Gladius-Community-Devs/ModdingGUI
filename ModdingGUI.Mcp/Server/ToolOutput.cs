namespace ModdingGUI.Mcp.Server;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using ModdingGUI.Core.Api;
using ModdingGUI.Core.Classes;
using ModdingGUI.Core.Skills;
using ModdingGUI.Core.Tools;
using ModdingGUI.Core.Units;
using CoreLogLevel = ModdingGUI.Core.Logging.LogLevel;

/// <summary>Thrown by a tool for a refusal or a caller error; the client sees it as an error result.</summary>
public sealed class McpToolException : Exception
{
    /// <summary>Create the exception with a message written for whoever (or whatever) called the tool.</summary>
    public McpToolException(string message) : base(message)
    {
    }
}

/// <summary>
/// One advisory finding, in the one shape every family's validator is mapped onto so a client parses one
/// schema instead of sixteen. <see cref="Severity"/> is <c>info</c> or <c>warning</c>: a warning is what
/// <c>strict: true</c> refuses.
/// </summary>
/// <param name="Severity">Lower-case severity: <c>info</c> or <c>warning</c>.</param>
/// <param name="Code">Stable machine-readable code, e.g. <c>unknown-field</c>.</param>
/// <param name="Target">Record, file, set index or team the finding is about, when the family has one.</param>
/// <param name="Field">Field or key the finding concerns, when applicable.</param>
/// <param name="Message">Human-readable explanation.</param>
public sealed record ValidationIssue(
    string Severity,
    string Code,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Target,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Field,
    string Message)
{
    /// <summary>True when this finding is a warning (or worse) and therefore blocks a strict write.</summary>
    [JsonIgnore]
    public bool IsWarning => Severity is "warning" or "error";

    /// <inheritdoc />
    public override string ToString() =>
        $"[{Severity}] {Code}" +
        (Field is null ? string.Empty : $" ({Field})") +
        (Target is null ? string.Empty : $" in {Target}") +
        $": {Message}";

    // ---- Family mappers ------------------------------------------------------
    // Each Core validator has its own issue record. Mapping them here keeps the tool surface uniform and
    // keeps the mapping in one place instead of in every tool method.

    /// <summary>Map a skills finding.</summary>
    public static ValidationIssue From(SkillValidationIssue issue) =>
        new(Text(issue.Severity == SkillValidationSeverity.Warning), issue.Code, null, issue.Field, issue.Message);

    /// <summary>Map a class-definition finding.</summary>
    public static ValidationIssue From(ClassValidationIssue issue) =>
        new(Text(issue.Severity == ClassValidationSeverity.Warning), issue.Code, issue.ClassName, issue.Field, issue.Message);

    /// <summary>Map an encounter finding.</summary>
    public static ValidationIssue From(EncounterValidationIssue issue) =>
        new(Text(issue.Severity == EncounterValidationSeverity.Warning), issue.Code, issue.Site, issue.Key, issue.Message);

    /// <summary>Map a league finding.</summary>
    public static ValidationIssue From(LeagueValidationIssue issue) =>
        new(Text(issue.Severity == LeagueValidationSeverity.Warning), issue.Code, issue.League, issue.Encounter, issue.Message);

    /// <summary>Map a roster finding.</summary>
    public static ValidationIssue From(SchoolValidationIssue issue) =>
        new(Text(issue.Severity == SchoolValidationSeverity.Warning), issue.Code, issue.Unit, issue.Key, issue.Message);

    /// <summary>Map a school-name finding.</summary>
    public static ValidationIssue From(SchoolNameValidationIssue issue) =>
        new(Text(issue.Severity == SchoolNameValidationSeverity.Warning), issue.Code,
            issue.Index >= 0 ? issue.Index.ToString() : null, null, issue.Message);

    /// <summary>Map a gladiator finding.</summary>
    public static ValidationIssue From(GladiatorValidationIssue issue) =>
        new(Text(issue.Severity == GladiatorValidationSeverity.Warning), issue.Code, issue.Name, issue.Field, issue.Message);

    /// <summary>Map a set finding (itemsets, skillsets, tintsets).</summary>
    public static ValidationIssue From(SetValidationIssue issue) =>
        new(Text(issue.Severity == SetValidationSeverity.Warning), issue.Code,
            issue.Index >= 0 ? issue.Index.ToString() : null, null, issue.Message);

    /// <summary>Map a stat-set finding.</summary>
    public static ValidationIssue From(StatSetValidationIssue issue) =>
        new(Text(issue.Severity == StatSetValidationSeverity.Warning), issue.Code,
            issue.Index >= 0 ? issue.Index.ToString() : null, null, issue.Message);

    /// <summary>Map a project-validation finding.</summary>
    public static ValidationIssue From(ModdingGUI.Core.Validation.ValidationFinding finding) =>
        new(Text(finding.Severity != ModdingGUI.Core.Validation.ValidationSeverity.Info), finding.Area, null, null, finding.Message);

    private static string Text(bool warning) => warning ? "warning" : "info";
}

/// <summary>
/// What every write tool answers with: the mutation's own result plus what validation said about the change.
/// </summary>
/// <remarks>
/// Read tools return the Core DTO itself, so field names match property names. A write has two things to
/// say — what changed and what validation thought of it — and an envelope is the only shape that carries
/// both without inventing a second record type per family.
/// </remarks>
public sealed class WriteResult
{
    /// <summary>The Core return value: the record as written, a count, a bool, or null for a void write.</summary>
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Result { get; init; }

    /// <summary>Findings the write introduced (warnings first). Empty when the family has no validator.</summary>
    public IReadOnlyList<ValidationIssue> Warnings { get; init; } = Array.Empty<ValidationIssue>();

    /// <summary>Whether the caller asked for strict mode. Echoed so a transcript shows why a call refused.</summary>
    public bool Strict { get; init; }
}

/// <summary>One step of a build, flattened for a client: seconds not <see cref="TimeSpan"/>, output as lines.</summary>
/// <param name="Name">Step name, e.g. <c>pack</c>.</param>
/// <param name="ExitCode">Process exit code; 0 is success.</param>
/// <param name="Succeeded">True when <paramref name="ExitCode"/> is 0.</param>
/// <param name="Seconds">Wall-clock duration.</param>
/// <param name="Output">Captured lines, in order.</param>
public sealed record BuildStepReport(string Name, int ExitCode, bool Succeeded, double Seconds, IReadOnlyList<string> Output);

/// <summary>The outcome of a job-backed tool (<c>project_pack</c>, <c>project_unpack</c>).</summary>
/// <param name="Kind">Job kind: <c>pack</c> or <c>unpack</c>.</param>
/// <param name="JobId">The job's id, useful in the log and in the GUI's job list.</param>
/// <param name="Target">Project folder or ISO the job ran against.</param>
/// <param name="State">Final job state: <c>Succeeded</c>, <c>Failed</c> or <c>Cancelled</c>.</param>
/// <param name="Succeeded">True when every step succeeded.</param>
/// <param name="ExitCode">First failing step's exit code, or 0.</param>
/// <param name="Seconds">Wall-clock duration.</param>
/// <param name="Result">Where the output landed (the ISO for a pack, the project folder for an unpack).</param>
/// <param name="Steps">Every step that ran, in order.</param>
public sealed record BuildReport(
    string Kind,
    string JobId,
    string Target,
    string State,
    bool Succeeded,
    int ExitCode,
    double Seconds,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Result,
    IReadOnlyList<BuildStepReport> Steps)
{
    /// <summary>Project a finished job onto the report a client reads.</summary>
    public static BuildReport From(ModdingGUI.Core.Jobs.BuildJob job, string? result)
    {
        BuildResult build = job.Result ?? BuildResult.Empty;
        return new BuildReport(
            job.Kind,
            job.Id,
            job.Target,
            job.State.ToString(),
            build.Succeeded && job.State == ModdingGUI.Core.Jobs.JobState.Succeeded,
            build.ExitCode,
            Math.Round(job.Elapsed.TotalSeconds, 3),
            result,
            build.Steps
                .Select(step => new BuildStepReport(
                    step.Name, step.ExitCode, step.Succeeded, Math.Round(step.Duration.TotalSeconds, 3), step.Output))
                .ToArray());
    }
}

/// <summary>One app-log line, as an MCP client reads it.</summary>
/// <param name="At">When it was written, ISO-8601 local time.</param>
/// <param name="Level">Lower-case level: <c>info</c>, <c>success</c>, <c>warning</c>, <c>error</c>, <c>debug</c>.</param>
/// <param name="Source">Who wrote it: <c>pack</c>, <c>mcp</c>, <c>items</c>, …</param>
/// <param name="Message">The text.</param>
public sealed record LogLine(string At, string Level, string Source, string Message)
{
    /// <summary>Project a Core entry onto the wire shape.</summary>
    public static LogLine From(ModdingGUI.Core.Logging.LogEntry entry) =>
        new(entry.At.ToString("yyyy-MM-dd HH:mm:ss.fff"),
            entry.Level.ToString().ToLowerInvariant(),
            entry.Source,
            entry.Message);

    /// <summary>Severity for the <c>level</c> filter of <c>logs_tail</c>; null means "everything".</summary>
    public static bool AtLeast(ModdingGUI.Core.Logging.LogEntry entry, CoreLogLevel? minimum) =>
        minimum is null || entry.Level >= minimum;
}
