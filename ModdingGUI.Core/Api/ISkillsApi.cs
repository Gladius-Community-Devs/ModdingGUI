namespace ModdingGUI.Core.Api;

using System.Collections.Generic;
using ModdingGUI.Core.Skills;

/// <summary>
/// Read/write API for <c>skills.tok</c> in a single project. Every mutating
/// method acquires the project lock, writes a timestamped backup under
/// <c>&lt;project&gt;/.mod-history/</c>, and rewrites the file.
/// </summary>
/// <remarks>
/// <para>
/// The API is stateless: each call opens the file, mutates the in-memory line
/// list, and writes it back. Skills are manipulated as raw line blocks — no
/// typed schema is used at this phase, and none is planned.
/// </para>
/// <para>
/// Validation is advisory (PLAN rule 7): <see cref="Validate"/> reports findings
/// without touching the file, the plain <see cref="Add"/> / <see cref="Update"/>
/// overloads never validate, and the optional-mode overloads only refuse to write
/// when the caller explicitly asks for <see cref="SkillValidationMode.Reject"/>.
/// </para>
/// </remarks>
public interface ISkillsApi
{
    /// <summary>Return every skill's stable name in source order.</summary>
    IReadOnlyList<string> List(string projectPath);

    /// <summary>
    /// Return all skills as DTOs in source order. Reads the file exactly once;
    /// prefer this over <see cref="List"/> + N×<see cref="Get"/> for bulk loads.
    /// </summary>
    IReadOnlyList<SkillDto> GetAll(string projectPath);

    /// <summary>Return the skill with the given stable name, or <c>null</c> if absent.</summary>
    SkillDto? Get(string projectPath, string name);

    /// <summary>
    /// Replace the body (and optionally identity) of the skill named
    /// <paramref name="name"/> with the fields from <paramref name="skill"/>.
    /// Throws <see cref="System.Collections.Generic.KeyNotFoundException"/> if
    /// not found.
    /// </summary>
    SkillDto Update(string projectPath, string name, SkillDto skill);

    /// <summary>Append a new skill record at the end of the file.</summary>
    SkillDto Add(string projectPath, SkillDto skill);

    /// <summary>
    /// Delete the skill with the given stable name. Returns <c>true</c> if found
    /// and removed.
    /// </summary>
    bool Remove(string projectPath, string name);

    /// <summary>
    /// Validate a skill record without touching disk. Findings are advisory:
    /// catalog-derived ones are <see cref="SkillValidationSeverity.Info"/> (a
    /// shipped catalog cannot know modded values) and structural ones are
    /// <see cref="SkillValidationSeverity.Warning"/>.
    /// </summary>
    /// <param name="projectPath">BEC root of the project; used to resolve the catalog and context when not supplied.</param>
    /// <param name="skill">The record to check.</param>
    /// <param name="catalog">Catalog to validate against; resolved for the project when <c>null</c>.</param>
    /// <param name="context">External reference data; loaded from the project when <c>null</c>.</param>
    /// <param name="options">Rule switches; defaults to catalog + external references.</param>
    IReadOnlyList<SkillValidationIssue> Validate(
        string projectPath,
        SkillDto skill,
        SkillsCatalog? catalog = null,
        SkillsContext? context = null,
        SkillValidationOptions? options = null);

    /// <summary>
    /// <see cref="Update"/> with advisory validation. <see cref="SkillValidationMode.Warn"/>
    /// always writes and returns the findings; <see cref="SkillValidationMode.Reject"/>
    /// throws <see cref="SkillValidationException"/> instead of writing when any
    /// <see cref="SkillValidationSeverity.Warning"/> is found.
    /// </summary>
    SkillMutationResult Update(
        string projectPath,
        string name,
        SkillDto skill,
        SkillValidationMode mode,
        SkillsCatalog? catalog = null,
        SkillsContext? context = null);

    /// <summary>
    /// <see cref="Add"/> with advisory validation. Same mode semantics as the
    /// validated <see cref="Update"/> overload.
    /// </summary>
    SkillMutationResult Add(
        string projectPath,
        SkillDto skill,
        SkillValidationMode mode,
        SkillsCatalog? catalog = null,
        SkillsContext? context = null);
}
