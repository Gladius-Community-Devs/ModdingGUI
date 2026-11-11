namespace ModdingGUI.Core.Api;

using System.Collections.Generic;

/// <summary>
/// Read and edit <c>data/encounters/**/*.enc</c> files through raw, comment-preserving blocks.
/// </summary>
/// <remarks>
/// <para>
/// Every member is synchronous, takes the project lock and backs the file up into
/// <c>.mod-history/</c> before writing. Edits replace only the value portion of a line, so a
/// no-op write is byte-identical and unknown keys, comments and indentation survive.
/// </para>
/// <para>
/// <c>projectPath</c> may be the project root or its BEC folder; see
/// <see cref="ProjectRoots.ResolveBecFolder"/>.
/// </para>
/// </remarks>
public interface IEncountersApi
{
    /// <summary>Project-relative paths of every <c>.enc</c> file, in sorted order.</summary>
    IReadOnlyList<string> ListFiles(string projectPath);

    /// <summary>Encounter names (<c>ENCOUNTERNAME</c>) for every file, in sorted path order.</summary>
    IReadOnlyList<string> List(string projectPath);

    /// <summary>Parse one encounter; null when the file does not exist.</summary>
    EncounterDto? Get(string projectPath, string relativePath);

    /// <summary>Replace a header field's value, keeping the line's original layout.</summary>
    void SetField(string projectPath, string relativePath, string key, string rawValue);

    /// <summary>Append a header field at the end of the header section.</summary>
    void AddField(string projectPath, string relativePath, string key, string rawValue);

    /// <summary>Remove every header line for a key.</summary>
    void RemoveField(string projectPath, string relativePath, string key);

    /// <summary>Replace a team section's field value (e.g. its <c>GRIDFILE</c> or <c>SCHOOL</c>).</summary>
    void SetTeamField(string projectPath, string relativePath, int teamIndex, string key, string rawValue);

    /// <summary>Add a field line to a team block (e.g. a <c>SCHOOL</c> the team did not have).</summary>
    void AddTeamField(string projectPath, string relativePath, int teamIndex, string key, string rawValue);

    /// <summary>Remove every line for a key from a team section.</summary>
    void RemoveTeamField(string projectPath, string relativePath, int teamIndex, string key);

    /// <summary>
    /// Replace all <c>UNITDB:</c> rows of a team with the supplied raw argument lists, in
    /// order. Pass the existing values to make a no-op write.
    /// </summary>
    void SetTeamUnits(string projectPath, string relativePath, int teamIndex, IReadOnlyList<string> unitRawValues);

    /// <summary>Replace one unit row of a team (0-based position within the team).</summary>
    void SetTeamUnit(string projectPath, string relativePath, int teamIndex, int unitIndex, EncounterUnit unit);

    /// <summary>Append a unit to a team.</summary>
    void AddTeamUnit(string projectPath, string relativePath, int teamIndex, EncounterUnit unit);

    /// <summary>Remove one unit from a team (0-based position within the team).</summary>
    void RemoveTeamUnit(string projectPath, string relativePath, int teamIndex, int unitIndex);

    /// <summary>
    /// Overwrite one encounter file with another's content — the way to make a story or staged
    /// battle run a different fight.
    /// </summary>
    /// <remarks>
    /// Story battles (<c>Scene04U</c>, <c>Scene06U</c>, …) are started by the compiled game-flow
    /// script by <b>encounter name</b>: the engine resolves that name by scanning the encounters
    /// tree for a matching <c>ENCOUNTERNAME</c>. Repointing a league's <c>ENCFILE</c> therefore
    /// does nothing for them (verified in game), while replacing the encounter file does.
    /// </remarks>
    /// <param name="projectPath">Project root or BEC folder.</param>
    /// <param name="sourceRelativePath">Encounter to copy from.</param>
    /// <param name="destinationRelativePath">Encounter file to overwrite.</param>
    /// <param name="encounterName">
    /// <c>ENCOUNTERNAME</c> to write into the destination. Pass the destination's existing name
    /// (the usual case) so whatever looks it up still finds it; null keeps the source's name.
    /// </param>
    /// <param name="stripCutscene">
    /// Drop <c>CUTSCENE</c> lines. Story battles fire their own cinematics, so a copied-in
    /// cutscene reference would play the wrong scene.
    /// </param>
    void ReplaceWith(
        string projectPath,
        string sourceRelativePath,
        string destinationRelativePath,
        string? encounterName = null,
        bool stripCutscene = true);

    /// <summary>
    /// Read an encounter as raw lines, preserving per-line terminators and trailing data. Line-level access exists
    /// for the randomizer, which rewrites whole <c>UNITDB</c> rows instead of setting field values.
    /// </summary>
    (IReadOnlyList<string> Lines, int Trailing) ReadLines(string projectPath, string relativePath);

    /// <summary>Replace an encounter with the given lines: one write, one backup, terminators preserved.</summary>
    void WriteLines(string projectPath, string relativePath, IReadOnlyList<string> lines);

    /// <summary>
    /// Advisory checks: unknown header keys, teams with no units or no start grid,
    /// <c>UNITDB</c> rows with the wrong argument count, and duplicate start slots.
    /// </summary>
    IReadOnlyList<EncounterValidationIssue> Validate(string projectPath, string relativePath);
}
