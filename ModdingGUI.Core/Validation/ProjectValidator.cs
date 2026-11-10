namespace ModdingGUI.Core.Validation;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ModdingGUI.Core.Api;
using ModdingGUI.Core.Logging;
using ModdingGUI.Core.Units;

/// <summary>How much a finding matters.</summary>
/// <remarks>
/// Nothing here is an <see cref="Error"/> today: every check is advisory and packing is never blocked on one, because
/// a modded ISO can legitimately differ from vanilla in all of these places. The value exists so a future check that
/// genuinely cannot be packed (a corrupt file, say) can say so.
/// </remarks>
public enum ValidationSeverity
{
    /// <summary>Worth knowing; the game copes.</summary>
    Info,

    /// <summary>Likely to crash the packer or misbehave in game.</summary>
    Warning,

    /// <summary>Reserved for a defect that makes packing impossible.</summary>
    Error,
}

/// <summary>One thing a validator noticed, with the area it came from.</summary>
public sealed record ValidationFinding(ValidationSeverity Severity, string Area, string Message);

/// <summary>The result of validating a project before packing.</summary>
public sealed record ValidationReport(IReadOnlyList<ValidationFinding> Findings)
{
    /// <summary>Findings per area, in the order they were checked.</summary>
    public IReadOnlyDictionary<string, int> CountByArea =>
        Findings.GroupBy(finding => finding.Area).ToDictionary(group => group.Key, group => group.Count());

    /// <summary>How many findings are Errors.</summary>
    public int ErrorCount => Findings.Count(finding => finding.Severity == ValidationSeverity.Error);

    /// <summary>How many findings are Warnings.</summary>
    public int WarningCount => Findings.Count(finding => finding.Severity == ValidationSeverity.Warning);

    /// <summary>One line for the log.</summary>
    public string Summary =>
        $"{Findings.Count} finding(s): {ErrorCount} error(s), {WarningCount} warning(s)";
}

/// <summary>What to include in a project validation.</summary>
public sealed record ValidationOptions
{
    /// <summary>Validate the <c>data/school</c> rosters too (they are what the game actually loads).</summary>
    public bool IncludeRosters { get; init; } = true;

    /// <summary>Stop collecting a single area after this many findings, so one bad file cannot flood the log.</summary>
    public int MaxFindingsPerArea { get; init; } = 40;
}

/// <summary>
/// Runs every file validator the Core APIs already have and reports the findings together.
/// </summary>
/// <remarks>
/// <para>
/// The Pack tab used to run three hand-written regex checks that only looked at skillsets <em>reachable from
/// gladiators.txt</em>. A skillset nobody points at was never checked, so a typo there passed validation and then
/// crashed the pack chain inside <c>Gladius_Units_IDX_Repack.py</c> with a Python traceback. The validators in Core
/// already cover every row of every set — this just calls them.
/// </para>
/// <para>
/// No check compares against a vanilla count. A project unpacked from a modded ISO is expected to have different
/// numbers of skills, items, sets and units; what matters is that the references inside the project resolve.
/// </para>
/// </remarks>
public static class ProjectValidator
{
    /// <summary>Validate a project's pack-relevant files.</summary>
    /// <param name="projectRoot">The project folder or its <c>_BEC</c> folder.</param>
    /// <param name="options">What to include; defaults to everything.</param>
    /// <param name="log">Optional progress lines (one per area), so a slow check is not silent.</param>
    public static ValidationReport Validate(
        string projectRoot, ValidationOptions? options = null, IAppLog? log = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);

        var settings = options ?? new ValidationOptions();
        string bec = ProjectRoots.ResolveBecFolder(projectRoot);
        var findings = new List<ValidationFinding>();

        // Each area is isolated: one unreadable file must not stop the rest of the report.
        Add(findings, "gladiators", () => new GladiatorsApi()
            .Validate(bec)
            .Select(issue => new ValidationFinding(
                Severity(issue.Severity), "gladiators",
                $"{issue.Name}: {issue.Message}")),
            settings, log);

        Add(findings, "skillsets", () => new SkillSetsApi()
            .Validate(bec)
            .Select(issue => new ValidationFinding(
                Severity(issue.Severity), "skillsets",
                issue.Index >= 0 ? $"set {issue.Index}: {issue.Message}" : issue.Message)),
            settings, log);

        Add(findings, "itemsets", () => new ItemSetsApi()
            .Validate(bec)
            .Select(issue => new ValidationFinding(
                Severity(issue.Severity), "itemsets",
                issue.Index >= 0 ? $"set {issue.Index}: {issue.Message}" : issue.Message)),
            settings, log);

        Add(findings, "statsets", () => new StatSetsApi()
            .Validate(bec)
            .Select(issue => new ValidationFinding(
                Severity(issue.Severity), "statsets",
                issue.Index >= 0 ? $"set {issue.Index}: {issue.Message}" : issue.Message)),
            settings, log);

        Add(findings, "tints", () => new TintSetsApi()
            .Validate(bec)
            .Select(issue => new ValidationFinding(
                Severity(issue.Severity), "tints",
                issue.Index >= 0 ? $"set {issue.Index}: {issue.Message}" : issue.Message)),
            settings, log);

        Add(findings, "school names", () => new SchoolNamesApi()
            .Validate(bec)
            .Select(issue => new ValidationFinding(
                Severity(issue.Severity), "school names", issue.Message)),
            settings, log);

        if (settings.IncludeRosters)
        {
            var schools = new SchoolsApi();
            Add(findings, "rosters", () => schools.ListFiles(bec)
                .SelectMany(relative => schools.Validate(bec, relative)
                    .Select(issue => new ValidationFinding(
                        Severity(issue.Severity), "rosters",
                        $"{Path.GetFileName(relative)}: {issue.Message}"))),
                settings, log);
        }

        return new ValidationReport(findings);
    }

    private static void Add(
        List<ValidationFinding> findings,
        string area,
        Func<IEnumerable<ValidationFinding>> check,
        ValidationOptions options,
        IAppLog? log)
    {
        log?.Write(LogLevel.Info, $"Validating {area}…", "validate");

        try
        {
            var produced = check().ToList();
            findings.AddRange(produced.Take(options.MaxFindingsPerArea));

            // Report the real count even when only the first few are kept, so a truncated list never reads as "that
            // was all of them".
            bool serious = produced.Any(finding => finding.Severity != ValidationSeverity.Info);
            log?.Write(
                serious ? LogLevel.Warning : LogLevel.Info,
                $"  {area}: {produced.Count} finding(s)" +
                (produced.Count > options.MaxFindingsPerArea
                    ? $" (first {options.MaxFindingsPerArea} kept)"
                    : string.Empty),
                "validate");
        }
        catch (Exception ex) when (ex is IOException or FileNotFoundException or DirectoryNotFoundException)
        {
            findings.Add(new ValidationFinding(
                ValidationSeverity.Warning, area, $"could not be read: {ex.Message}"));
            log?.Write(LogLevel.Warning, $"  {area}: could not be read ({ex.Message})", "validate");
        }
    }

    private static ValidationSeverity Severity(GladiatorValidationSeverity severity) => severity switch
    {
        GladiatorValidationSeverity.Warning => ValidationSeverity.Warning,
        _ => ValidationSeverity.Info,
    };

    private static ValidationSeverity Severity(SetValidationSeverity severity) => severity switch
    {
        SetValidationSeverity.Warning => ValidationSeverity.Warning,
        _ => ValidationSeverity.Info,
    };

    private static ValidationSeverity Severity(StatSetValidationSeverity severity) => severity switch
    {
        StatSetValidationSeverity.Warning => ValidationSeverity.Warning,
        _ => ValidationSeverity.Info,
    };

    private static ValidationSeverity Severity(SchoolNameValidationSeverity severity) => severity switch
    {
        SchoolNameValidationSeverity.Warning => ValidationSeverity.Warning,
        _ => ValidationSeverity.Info,
    };

    private static ValidationSeverity Severity(SchoolValidationSeverity severity) => severity switch
    {
        SchoolValidationSeverity.Warning => ValidationSeverity.Warning,
        _ => ValidationSeverity.Info,
    };
}
