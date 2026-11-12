namespace ModdingGUI.Mcp.Tools;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using ModelContextProtocol.Server;
using ModdingGUI.Core.Api;
using ModdingGUI.Mcp.Server;

/// <summary>
/// <c>data/encounters/**/*.enc</c>: one encounter per file, a header plus <c>TEAM:</c> sections holding
/// positional <c>UNITDB</c> rows.
/// </summary>
/// <remarks>
/// <para>
/// Every tool takes a project-relative path (<c>data/encounters/…enc</c>) because a project holds hundreds of
/// encounters; <c>encounters_list_files</c> is how to find one.
/// </para>
/// <para>
/// A <c>UNITDB</c> row is 21 comma-separated slots. The row is the wire form for unit writes
/// (<c>raw</c>) rather than a 21-property object: the engine reads it positionally, the file stores it as
/// text, and a positional string round-trips exactly. <c>encounters_fields</c> documents the slots, and
/// <c>encounters_get</c> returns both the raw rows and their parsed form.
/// </para>
/// </remarks>
[McpServerToolType]
public sealed class EncounterTools(McpProject project)
{
    /// <summary>Every encounter file.</summary>
    [McpServerTool(Name = "encounters_list_files", ReadOnly = true, Idempotent = true)]
    [Description("List the project-relative paths of every .enc file, sorted. These are the paths the other encounters_* tools take.")]
    public object ListFiles() =>
        ToolResult.Call(project, "encounters_list_files", () => project.Encounters.ListFiles(project.BecPath));

    /// <summary>Every encounter name.</summary>
    [McpServerTool(Name = "encounters_list", ReadOnly = true, Idempotent = true)]
    [Description("List every encounter's ENCOUNTERNAME, in sorted path order. Story battles are found by this name, not by path.")]
    public object List() =>
        ToolResult.Call(project, "encounters_list", () => project.Encounters.List(project.BecPath));

    /// <summary>One parsed encounter.</summary>
    [McpServerTool(Name = "encounters_get", ReadOnly = true, Idempotent = true)]
    [Description(
        "Read one encounter: its header fields and its TEAM sections, each with fields, raw UNITDB rows and the " +
        "parsed rows (21 named slots). Fails when the path does not exist.")]
    public object Get(
        [Description("Project-relative path, e.g. data/encounters/expanse/expansedunes/expansedunesencounter01.enc.")]
        string path) =>
        ToolResult.Call(project, $"encounters_get '{path}'", () =>
            project.Encounters.Get(project.BecPath, path) is { } encounter
                ? encounter
                : ToolResult.NotFound($"encounter '{path}'", "Use encounters_list_files to see the paths."));

    /// <summary>Replace a header field.</summary>
    [McpServerTool(Name = "encounters_set_field")]
    [Description(
        "Replace a header field's value, keeping the line's layout: ENCOUNTERNAME, SCENE, GRIDFILE, CUTSCENE, … " +
        "rawValue is everything after the colon. Fails when the key is not already in the header (use " +
        "encounters_add_field). Unknown keys do nothing in game, and with strict=true such a write is refused.")]
    public object SetField(
        [Description("Project-relative encounter path.")] string path,
        [Description("Header key as written, e.g. GRIDFILE.")] string key,
        [Description("Everything after the colon, verbatim.")] string rawValue,
        [Description("true refuses the edit when it introduces a validation warning.")] bool strict = false) =>
        project.WriteVoid($"encounters_set_field '{path}' {key}",
            () => project.Encounters.SetField(project.BecPath, path, key, rawValue), strict, Scope(path));

    /// <summary>Append a header field.</summary>
    [McpServerTool(Name = "encounters_add_field")]
    [Description("Append a header field at the end of the header section. Use encounters_fields keys where the engine documents one.")]
    public object AddField(
        [Description("Project-relative encounter path.")] string path,
        [Description("Header key.")] string key,
        [Description("Everything after the colon, verbatim.")] string rawValue,
        [Description("true refuses the edit when it introduces a validation warning.")] bool strict = false) =>
        project.WriteVoid($"encounters_add_field '{path}' {key}",
            () => project.Encounters.AddField(project.BecPath, path, key, rawValue), strict, Scope(path));

    /// <summary>Remove a header field.</summary>
    [McpServerTool(Name = "encounters_remove_field", Destructive = true)]
    [Description("Remove every header line for a key.")]
    public object RemoveField(
        [Description("Project-relative encounter path.")] string path,
        [Description("Header key to remove.")] string key,
        [Description("true refuses the edit when it introduces a validation warning.")] bool strict = false) =>
        project.WriteVoid($"encounters_remove_field '{path}' {key}",
            () => project.Encounters.RemoveField(project.BecPath, path, key), strict, Scope(path));

    /// <summary>Replace a team field.</summary>
    [McpServerTool(Name = "encounters_set_team_field")]
    [Description(
        "Replace a TEAM section's field value (GRIDFILE, SCHOOL, …). teamIndex is the TEAM: number, not the " +
        "section's position. Fails when the team or key does not exist.")]
    public object SetTeamField(
        [Description("Project-relative encounter path.")] string path,
        [Description("Team number from the TEAM: line (0, 1, or -1 for the neutral team).")] int team,
        [Description("Field key.")] string key,
        [Description("Everything after the colon, verbatim.")] string rawValue,
        [Description("true refuses the edit when it introduces a validation warning.")] bool strict = false) =>
        project.WriteVoid($"encounters_set_team_field '{path}' team {team} {key}",
            () => project.Encounters.SetTeamField(project.BecPath, path, team, key, rawValue), strict, Scope(path));

    /// <summary>Add a team field.</summary>
    [McpServerTool(Name = "encounters_add_team_field")]
    [Description("Add a field line to a TEAM block, e.g. a SCHOOL the team did not have.")]
    public object AddTeamField(
        [Description("Project-relative encounter path.")] string path,
        [Description("Team number.")] int team,
        [Description("Field key.")] string key,
        [Description("Everything after the colon, verbatim.")] string rawValue,
        [Description("true refuses the edit when it introduces a validation warning.")] bool strict = false) =>
        project.WriteVoid($"encounters_add_team_field '{path}' team {team} {key}",
            () => project.Encounters.AddTeamField(project.BecPath, path, team, key, rawValue), strict, Scope(path));

    /// <summary>Remove a team field.</summary>
    [McpServerTool(Name = "encounters_remove_team_field", Destructive = true)]
    [Description("Remove every line for a key from a TEAM section.")]
    public object RemoveTeamField(
        [Description("Project-relative encounter path.")] string path,
        [Description("Team number.")] int team,
        [Description("Field key to remove.")] string key,
        [Description("true refuses the edit when it introduces a validation warning.")] bool strict = false) =>
        project.WriteVoid($"encounters_remove_team_field '{path}' team {team} {key}",
            () => project.Encounters.RemoveTeamField(project.BecPath, path, team, key), strict, Scope(path));

    /// <summary>Replace every unit row of a team.</summary>
    [McpServerTool(Name = "encounters_set_team_units")]
    [Description(
        "Replace all UNITDB rows of a team with the raw rows you supply, in order. Pass the existing rows to make " +
        "a no-op write. Every row must be a 21-slot comma-separated UNITDB list; encounters_fields describes the " +
        "slots and encounters_get shows current rows.")]
    public object SetTeamUnits(
        [Description("Project-relative encounter path.")] string path,
        [Description("Team number.")] int team,
        [Description("The complete list of raw UNITDB rows for the team, in order.")] IReadOnlyList<string> units,
        [Description("true refuses the edit when it introduces a validation warning.")] bool strict = false) =>
        project.WriteVoid($"encounters_set_team_units '{path}' team {team}",
            () => project.Encounters.SetTeamUnits(project.BecPath, path, team, units), strict, Scope(path));

    /// <summary>Replace one unit row.</summary>
    [McpServerTool(Name = "encounters_set_team_unit")]
    [Description(
        "Replace one UNITDB row of a team (0-based position within the team) and return the new unit. The " +
        "row is the raw 21-slot text, so no field is lost.")]
    public object SetUnit(
        [Description("Project-relative encounter path.")] string path,
        [Description("Team number.")] int team,
        [Description("0-based unit position within the team.")] int index,
        [Description("The new raw UNITDB row (21 comma-separated slots).")] string raw,
        [Description("true refuses the edit when it introduces a validation warning.")] bool strict = false) =>
        project.WriteVoid($"encounters_set_team_unit '{path}' team {team} unit {index}",
            () => project.Encounters.SetTeamUnit(project.BecPath, path, team, index, ParseRow(raw)), strict, Scope(path));

    /// <summary>Append a unit row.</summary>
    [McpServerTool(Name = "encounters_add_team_unit")]
    [Description("Append a UNITDB row to a team and return the parsed row.")]
    public object AddUnit(
        [Description("Project-relative encounter path.")] string path,
        [Description("Team number.")] int team,
        [Description("The raw UNITDB row to append (21 comma-separated slots).")] string raw,
        [Description("true refuses the edit when it introduces a validation warning.")] bool strict = false) =>
        project.WriteVoid($"encounters_add_team_unit '{path}' team {team}",
            () => project.Encounters.AddTeamUnit(project.BecPath, path, team, ParseRow(raw)), strict, Scope(path));

    /// <summary>Remove a unit row.</summary>
    [McpServerTool(Name = "encounters_remove_team_unit", Destructive = true)]
    [Description("Remove one UNITDB row from a team (0-based position within the team).")]
    public object RemoveUnit(
        [Description("Project-relative encounter path.")] string path,
        [Description("Team number.")] int team,
        [Description("0-based unit position within the team.")] int index,
        [Description("true refuses the edit when it introduces a validation warning.")] bool strict = false) =>
        project.WriteVoid($"encounters_remove_team_unit '{path}' team {team} unit {index}",
            () => project.Encounters.RemoveTeamUnit(project.BecPath, path, team, index), strict, Scope(path));

    /// <summary>Copy one encounter over another.</summary>
    [McpServerTool(Name = "encounters_replace_with")]
    [Description(
        "Overwrite one encounter file with another's content — the way to make a story or staged battle run a " +
        "different fight. Story battles are started by the game-flow script by encounter name, so replacing the " +
        "file works where repointing a league's ENCFILE does nothing. Pass the destination's existing " +
        "encounterName to keep whatever looks it up finding it; stripCutscene drops CUTSCENE lines so the " +
        "destination cannot play the source's cinematic.")]
    public object ReplaceWith(
        [Description("Project-relative path of the encounter to copy from.")] string source,
        [Description("Project-relative path of the encounter file to overwrite.")] string destination,
        [Description("ENCOUNTERNAME to write into the destination; omit to keep the source's name.")] string? encounterName = null,
        [Description("Drop CUTSCENE lines from the copy (default true).")] bool stripCutscene = true,
        [Description("true refuses the edit when it introduces a validation warning.")] bool strict = false) =>
        project.Write($"encounters_replace_with '{source}' -> '{destination}'", () =>
        {
            project.Encounters.ReplaceWith(project.BecPath, source, destination, encounterName, stripCutscene);
            return project.Encounters.Get(project.BecPath, destination);
        }, strict, Scope(destination));

    /// <summary>The 21 UNITDB slots and their choices.</summary>
    [McpServerTool(Name = "encounters_fields", ReadOnly = true, Idempotent = true)]
    [Description(
        "Describe the 21 UNITDB slots of an encounter unit: slot number, key, label, input kind, engine " +
        "description, bounds, fixed choices and the values the vanilla corpus actually uses. Read this before " +
        "building a raw row for encounters_set_team_unit or encounters_add_team_unit.")]
    public object Fields() =>
        ToolResult.Call(project, "encounters_fields", () => new EncounterFieldCatalogue(
            EncounterUnitFields.All,
            EncounterUnitFields.AffinityIds,
            EncounterUnitFields.AffinityNames,
            EncounterUnitFields.AttributeTable.Select(entry => new EncounterAttribute(entry.Bit, entry.Name)).ToArray()));

    /// <summary>Raw lines of one encounter.</summary>
    [McpServerTool(Name = "encounters_read_lines", ReadOnly = true, Idempotent = true)]
    [Description(
        "Read an encounter as raw lines, preserving per-line terminators and trailing data. Line-level access " +
        "exists for callers that rewrite whole UNITDB rows. Pair it with encounters_write_lines.")]
    public object ReadLines(
        [Description("Project-relative encounter path.")] string path) =>
        ToolResult.Call(project, $"encounters_read_lines '{path}'", () =>
        {
            (IReadOnlyList<string> lines, int trailing) = project.Encounters.ReadLines(project.BecPath, path);
            return new EncounterLines(lines, trailing);
        });

    /// <summary>Replace an encounter's lines.</summary>
    [McpServerTool(Name = "encounters_write_lines")]
    [Description(
        "Replace an encounter with the given lines: one write, one backup, terminators preserved. This is the " +
        "raw escape hatch; prefer the field and unit tools, which keep the file's layout.")]
    public object WriteLines(
        [Description("Project-relative encounter path.")] string path,
        [Description("The complete file content, one string per line (without terminators).")] IReadOnlyList<string> lines,
        [Description("true refuses the edit when it introduces a validation warning.")] bool strict = false) =>
        project.WriteVoid($"encounters_write_lines '{path}'", () => project.Encounters.WriteLines(project.BecPath, path, lines),
            strict, Scope(path));

    /// <summary>Validate one encounter.</summary>
    [McpServerTool(Name = "encounters_validate", ReadOnly = true, Idempotent = true)]
    [Description(
        "Validate one .enc file without touching disk: unknown header/team keys, teams with no units or no start " +
        "grid, UNITDB rows with the wrong argument count, duplicate start slots, and ENCFILE/GRIDFILE values that " +
        "do not resolve. Warnings are what strict mode refuses; info is vanilla-but-odd.")]
    public object Validate(
        [Description("Project-relative encounter path.")] string path) =>
        ToolResult.Call(project, $"encounters_validate '{path}'", () => Scope(path)());

    private Func<IReadOnlyList<ValidationIssue>> Scope(string path) => () =>
        project.Encounters.Validate(project.BecPath, path).Select(ValidationIssue.From).ToArray();

    private static EncounterUnit ParseRow(string raw)
    {
        EncounterUnit? unit = EncounterUnit.TryParse(raw);
        if (unit is null)
        {
            throw new McpToolException(
                $"'{raw}' is not a UNITDB row. A row has {EncounterUnit.SlotCount} comma-separated slots; " +
                "encounters_fields describes them and encounters_get shows the rows the file already has.");
        }

        return unit;
    }
}

/// <summary>The UNITDB slot catalogue, as <c>encounters_fields</c> answers.</summary>
/// <param name="Fields">The 21 slots and how to fill each.</param>
/// <param name="AffinityIds">Affinity ids the corpus uses (-1 means no requirement).</param>
/// <param name="AffinityNames">Affinity id to name, where the mapping is known.</param>
/// <param name="Attributes">Attribute bit to name.</param>
public sealed record EncounterFieldCatalogue(
    IReadOnlyList<EncounterUnitField> Fields,
    IReadOnlyList<string> AffinityIds,
    IReadOnlyDictionary<string, string> AffinityNames,
    IReadOnlyList<EncounterAttribute> Attributes);

/// <summary>One attribute bit of the UNITDB attribute mask.</summary>
/// <param name="Bit">Bit index.</param>
/// <param name="Name">Attribute name.</param>
public sealed record EncounterAttribute(int Bit, string Name);

/// <summary>The lines of an encounter, plus how much trailing data followed them.</summary>
/// <param name="Lines">Every line, without terminators.</param>
/// <param name="Trailing">Bytes of trailing data after the last line (kept verbatim on write).</param>
public sealed record EncounterLines(IReadOnlyList<string> Lines, int Trailing);
