namespace ModdingGUI.Core.Api;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

/// <summary>Severity of a league finding; nothing blocks a save.</summary>
public enum LeagueValidationSeverity
{
    /// <summary>Informational.</summary>
    Info,

    /// <summary>Will not behave as the modder expects.</summary>
    Warning,
}

/// <summary>Stable finding codes for league files.</summary>
public static class LeagueValidationCodes
{
    /// <summary>File-level key the corpus never uses.</summary>
    public const string UnknownHeaderKey = "unknown-header-key";

    /// <summary>League-level key the corpus never uses.</summary>
    public const string UnknownLeagueKey = "unknown-league-key";

    /// <summary>Bout-level key the corpus never uses.</summary>
    public const string UnknownEncounterKey = "unknown-encounter-key";

    /// <summary>The bout has no <c>ENCFILE</c>, so it cannot load an encounter.</summary>
    public const string MissingEncounterFile = "missing-encounter-file";

    /// <summary>The <c>ENCFILE</c> does not exist in the project.</summary>
    public const string EncounterFileMissing = "encounter-file-missing";

    /// <summary><c>TEAMS</c> is not five numbers.</summary>
    public const string TeamsShape = "teams-shape";

    /// <summary>A league with no bouts at all.</summary>
    public const string EmptyLeague = "empty-league";
}

/// <summary>One finding about a league file.</summary>
/// <param name="Severity">How much it matters.</param>
/// <param name="Code">Stable code from <see cref="LeagueValidationCodes"/>.</param>
/// <param name="League">League the finding belongs to, or <c>file</c>.</param>
/// <param name="Encounter">Bout it concerns, when applicable.</param>
/// <param name="Message">Human-readable explanation.</param>
public sealed record LeagueValidationIssue(
    LeagueValidationSeverity Severity,
    string Code,
    string League,
    string? Encounter,
    string Message);

/// <summary>Which header/league/bout keys the league corpus uses.</summary>
public sealed class LeaguesCatalog
{
    /// <summary>Shipped catalog path, relative to the app directory.</summary>
    public const string ShippedRelativePath = "tables/leagues_catalog.json";

    private LeaguesCatalog(
        IReadOnlyDictionary<string, int> headerKeys,
        IReadOnlyDictionary<string, int> leagueKeys,
        IReadOnlyDictionary<string, int> encounterKeys)
    {
        HeaderKeys = headerKeys;
        LeagueKeys = leagueKeys;
        EncounterKeys = encounterKeys;
    }

    /// <summary>File-level keys.</summary>
    public IReadOnlyDictionary<string, int> HeaderKeys { get; }

    /// <summary>League-level keys.</summary>
    public IReadOnlyDictionary<string, int> LeagueKeys { get; }

    /// <summary>Bout-level keys.</summary>
    public IReadOnlyDictionary<string, int> EncounterKeys { get; }

    /// <summary>Load from an explicit path.</summary>
    public static LeaguesCatalog Load(string path)
    {
        using var stream = File.OpenRead(path);
        return Load(stream);
    }

    /// <summary>Load from a stream.</summary>
    public static LeaguesCatalog Load(Stream stream)
    {
        using var document = JsonDocument.Parse(stream);
        var root = document.RootElement;
        return new LeaguesCatalog(
            SchoolsCatalog.ReadCounts(root, "header_keys"),
            SchoolsCatalog.ReadCounts(root, "league_keys"),
            SchoolsCatalog.ReadCounts(root, "encounter_keys"));
    }

    /// <summary>Load the shipped catalog, or null when it is not deployed.</summary>
    public static LeaguesCatalog? Shipped()
    {
        foreach (string candidate in CatalogPaths.Candidates(ShippedRelativePath))
        {
            if (File.Exists(candidate)) return Load(candidate);
        }
        return null;
    }
}

/// <summary>Advisory checks for a league file.</summary>
public static class LeagueValidator
{
    private const int TeamSlotCount = 5;   // e.g. 2 2 3 0 0

    /// <summary>
    /// Validate a parsed league file.
    /// </summary>
    /// <param name="file">Parsed file.</param>
    /// <param name="catalog">Key catalog, or null to skip key checks.</param>
    /// <param name="encounterFileExists">
    /// Tells whether an <c>ENCFILE</c> value resolves inside the project; pass null to skip.
    /// </param>
    public static IReadOnlyList<LeagueValidationIssue> Validate(
        LeagueFileDto file,
        LeaguesCatalog? catalog,
        Func<string, bool>? encounterFileExists = null)
    {
        ArgumentNullException.ThrowIfNull(file);

        var issues = new List<LeagueValidationIssue>();

        if (catalog is not null)
        {
            foreach (var field in file.Fields)
            {
                if (catalog.HeaderKeys.ContainsKey(field.Key)) continue;
                issues.Add(new LeagueValidationIssue(LeagueValidationSeverity.Warning,
                    LeagueValidationCodes.UnknownHeaderKey, "file", null,
                    $"'{field.Key}' is not a known league-file key"));
            }
        }

        foreach (var league in file.Leagues)
        {
            if (catalog is not null)
            {
                foreach (var field in league.Fields)
                {
                    if (catalog.LeagueKeys.ContainsKey(field.Key)) continue;
                    issues.Add(new LeagueValidationIssue(LeagueValidationSeverity.Warning,
                        LeagueValidationCodes.UnknownLeagueKey, league.Name, null,
                        $"'{field.Key}' is not a known league key; the game ignores it"));
                }
            }

            if (league.Encounters.Count == 0)
            {
                issues.Add(new LeagueValidationIssue(LeagueValidationSeverity.Info,
                    LeagueValidationCodes.EmptyLeague, league.Name, null,
                    "league has no bouts"));
            }

            foreach (var encounter in league.Encounters)
            {
                if (catalog is not null)
                {
                    foreach (var field in encounter.Fields)
                    {
                        if (catalog.EncounterKeys.ContainsKey(field.Key)) continue;
                        issues.Add(new LeagueValidationIssue(LeagueValidationSeverity.Warning,
                            LeagueValidationCodes.UnknownEncounterKey, league.Name, encounter.Name,
                            $"'{field.Key}' is not a known bout key; the game ignores it"));
                    }
                }

                string? encounterFile = encounter.EncounterFile?.Trim().Trim('"');
                if (string.IsNullOrWhiteSpace(encounterFile))
                {
                    issues.Add(new LeagueValidationIssue(LeagueValidationSeverity.Warning,
                        LeagueValidationCodes.MissingEncounterFile, league.Name, encounter.Name,
                        "bout has no ENCFILE, so it cannot load an encounter"));
                    continue;
                }

                if (encounterFileExists is not null && !encounterFileExists(encounterFile))
                {
                    issues.Add(new LeagueValidationIssue(LeagueValidationSeverity.Warning,
                        LeagueValidationCodes.EncounterFileMissing, league.Name, encounter.Name,
                        $"ENCFILE '{encounterFile}' does not exist under data/encounters"));
                }

                string? teams = encounter.Teams;
                if (teams is not null)
                {
                    int count = teams.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
                    if (count != TeamSlotCount)
                    {
                        issues.Add(new LeagueValidationIssue(LeagueValidationSeverity.Warning,
                            LeagueValidationCodes.TeamsShape, league.Name, encounter.Name,
                            $"TEAMS has {count} values; the corpus uses {TeamSlotCount}"));
                    }
                }
            }
        }

        return issues;
    }
}
