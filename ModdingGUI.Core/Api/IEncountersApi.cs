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

    /// <summary>Remove every line for a key from a team section.</summary>
    void RemoveTeamField(string projectPath, string relativePath, int teamIndex, string key);

    /// <summary>
    /// Replace all <c>UNITDB:</c> rows of a team with the supplied raw argument lists, in
    /// order. Pass the existing values to make a no-op write.
    /// </summary>
    void SetTeamUnits(string projectPath, string relativePath, int teamIndex, IReadOnlyList<string> unitRawValues);

    /// <summary>
    /// Advisory checks: unknown header keys, teams with no units or no start grid, and
    /// <c>UNITDB</c> rows with the wrong argument count.
    /// </summary>
    IReadOnlyList<EncounterValidationIssue> Validate(string projectPath, string relativePath);
}
