namespace ModdingGUI.Mcp.Tools;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using ModelContextProtocol.Server;
using ModdingGUI.Core.Api;
using ModdingGUI.Mcp.Server;

/// <summary>
/// <c>data/towns/leagues/*.tok</c>: an office header, <c>LEAGUE</c> sections and their <c>ENCOUNTER</c> bouts.
/// </summary>
/// <remarks>
/// <c>leagues_set_encounter_file</c> is the in-game harness in one call: pointing a bout at a different
/// <c>.enc</c> is how a test build drops into a chosen fight. Story battles are the exception — they are
/// started by the game-flow script by encounter name, so use <c>encounters_replace_with</c> for those.
/// </remarks>
[McpServerToolType]
public sealed class LeagueTools(McpProject project)
{
    /// <summary>Every league file.</summary>
    [McpServerTool(Name = "leagues_list_files", ReadOnly = true, Idempotent = true)]
    [Description("List the project-relative paths of every league file, sorted. These are the paths the other leagues_* tools take.")]
    public object ListFiles() =>
        ToolResult.Call(project, "leagues_list_files", () => project.Leagues.ListFiles(project.BecPath));

    /// <summary>Every league name in one file.</summary>
    [McpServerTool(Name = "leagues_list", ReadOnly = true, Idempotent = true)]
    [Description("List the LEAGUE names in one league file, in file order.")]
    public object List(
        [Description("Project-relative league path, e.g. data/towns/leagues/altahrunruins_leagues.tok.")] string path) =>
        ToolResult.Call(project, $"leagues_list '{path}'", () => project.Leagues.List(project.BecPath, path));

    /// <summary>One parsed league file.</summary>
    [McpServerTool(Name = "leagues_get", ReadOnly = true, Idempotent = true)]
    [Description(
        "Read one league file: its office header fields, each LEAGUE with its fields, and each bout with its " +
        "fields (ENCFILE, ENCPTS, TEAMS, FREQUENCY, …). Fails when the path does not exist.")]
    public object Get(
        [Description("Project-relative league path.")] string path) =>
        ToolResult.Call(project, $"leagues_get '{path}'", () =>
            project.Leagues.Get(project.BecPath, path) is { } file
                ? file
                : ToolResult.NotFound($"league file '{path}'", "Use leagues_list_files to see the paths."));

    /// <summary>Replace a file-level field.</summary>
    [McpServerTool(Name = "leagues_set_header_field")]
    [Description("Replace a file-level field (OFFICENAME, OFFICER, RECRUIT, SCHOOL, …). rawValue is everything after the colon.")]
    public object SetHeaderField(
        [Description("Project-relative league path.")] string path,
        [Description("Header key as written.")] string key,
        [Description("Everything after the colon, verbatim.")] string rawValue,
        [Description("true refuses the edit when it introduces a validation warning.")] bool strict = false) =>
        project.WriteVoid($"leagues_set_header_field '{path}' {key}",
            () => project.Leagues.SetHeaderField(project.BecPath, path, key, rawValue), strict, Scope(path));

    /// <summary>Remove every recruit line from every league file.</summary>
    [McpServerTool(Name = "leagues_remove_recruits", Destructive = true)]
    [Description(
        "Remove every RECRUIT line from every league file and return how many files changed — the randomizer's " +
        "\"no recruits\" option. Each changed file gets its own backup. The ordinary way to disable recruiting is " +
        "the RECRUIT value; use this when the lines have to go.")]
    public object RemoveRecruits(
        [Description("accepted for uniformity; this touches every league file at once, so it has no single record scope to validate.")] bool strict = false) =>
        ToolResult.Call(project, "leagues_remove_recruits", () => project.Leagues.RemoveRecruits(project.BecPath));

    /// <summary>Replace a league field.</summary>
    [McpServerTool(Name = "leagues_set_league_field")]
    [Description("Replace a league-level field (LEAGUEDESC, LEAGUEPTS, HERO, …). Fails when the league or key does not exist.")]
    public object SetLeagueField(
        [Description("Project-relative league path.")] string path,
        [Description("League name from the LEAGUE line.")] string league,
        [Description("Field key as written.")] string key,
        [Description("Everything after the colon, verbatim.")] string rawValue,
        [Description("true refuses the edit when it introduces a validation warning.")] bool strict = false) =>
        project.WriteVoid($"leagues_set_league_field '{path}' {league} {key}",
            () => project.Leagues.SetLeagueField(project.BecPath, path, league, key, rawValue), strict, Scope(path));

    /// <summary>Replace a bout field.</summary>
    [McpServerTool(Name = "leagues_set_encounter_field")]
    [Description("Replace a field of one bout (ENCPTS, TEAMS, FREQUENCY, …). Fails when the league, bout or key does not exist.")]
    public object SetEncounterField(
        [Description("Project-relative league path.")] string path,
        [Description("League name.")] string league,
        [Description("Bout (ENCOUNTER) name.")] string encounter,
        [Description("Field key as written.")] string key,
        [Description("Everything after the colon, verbatim.")] string rawValue,
        [Description("true refuses the edit when it introduces a validation warning.")] bool strict = false) =>
        project.WriteVoid($"leagues_set_encounter_field '{path}' {league}/{encounter} {key}",
            () => project.Leagues.SetEncounterField(project.BecPath, path, league, encounter, key, rawValue), strict, Scope(path));

    /// <summary>Point a bout at a different encounter file.</summary>
    [McpServerTool(Name = "leagues_set_encounter_file")]
    [Description(
        "Point a bout at a different .enc — the one-call harness for a test build. Accepts a project-relative path " +
        "(data/encounters/…enc) or the game's own form (Nordagh\\thefen\\x.enc). A story battle ignores this: it " +
        "is started by the game-flow script by encounter name, so use encounters_replace_with instead.")]
    public object SetEncounterFile(
        [Description("Project-relative league path.")] string path,
        [Description("League name.")] string league,
        [Description("Bout (ENCOUNTER) name.")] string encounter,
        [Description("The .enc to load: project-relative or the game's backslash form.")] string encounterFile,
        [Description("true refuses the edit when it introduces a validation warning.")] bool strict = false) =>
        project.WriteVoid($"leagues_set_encounter_file '{path}' {league}/{encounter} -> '{encounterFile}'",
            () => project.Leagues.SetEncounterFile(project.BecPath, path, league, encounter, encounterFile), strict, Scope(path));

    /// <summary>Append a bout.</summary>
    [McpServerTool(Name = "leagues_add_encounter")]
    [Description("Append a bout (an ENCOUNTER line plus its fields) to a league.")]
    public object AddEncounter(
        [Description("Project-relative league path.")] string path,
        [Description("League name.")] string league,
        [Description("New bout name.")] string encounter,
        [Description("Everything after the bout name on the ENCOUNTER line (usually a lookup-text id).")] string rawValue,
        [Description("Field lines for the bout, in order, e.g. ENCFILE/ENCPTS/TEAMS.")] IReadOnlyList<LeagueFieldDto> fields,
        [Description("true refuses the edit when it introduces a validation warning.")] bool strict = false) =>
        project.WriteVoid($"leagues_add_encounter '{path}' {league}/{encounter}",
            () => project.Leagues.AddEncounter(project.BecPath, path, league, encounter, rawValue, fields), strict, Scope(path));

    /// <summary>Remove a bout.</summary>
    [McpServerTool(Name = "leagues_remove_encounter", Destructive = true)]
    [Description("Remove a bout from a league. Fails when the league or bout does not exist.")]
    public object RemoveEncounter(
        [Description("Project-relative league path.")] string path,
        [Description("League name.")] string league,
        [Description("Bout name to remove.")] string encounter,
        [Description("true refuses the edit when it introduces a validation warning.")] bool strict = false) =>
        project.WriteVoid($"leagues_remove_encounter '{path}' {league}/{encounter}",
            () => project.Leagues.RemoveEncounter(project.BecPath, path, league, encounter), strict, Scope(path));

    /// <summary>Validate one league file.</summary>
    [McpServerTool(Name = "leagues_validate", ReadOnly = true, Idempotent = true)]
    [Description(
        "Validate one league file without touching disk: unknown keys, bout fields the engine ignores, team shapes " +
        "that are not five slots, and ENCFILE values that do not resolve inside the project. Vanilla has leagues " +
        "with no bouts; that is info, not a warning.")]
    public object Validate(
        [Description("Project-relative league path.")] string path) =>
        ToolResult.Call(project, $"leagues_validate '{path}'", () => Scope(path)());

    private Func<IReadOnlyList<ValidationIssue>> Scope(string path) => () =>
        project.Leagues.Validate(project.BecPath, path).Select(ValidationIssue.From).ToArray();
}
