namespace ModdingGUI.Core.Api;

using System.Collections.Generic;

/// <summary>
/// Read/write API for <c>skills.tok</c> in a single project. Every mutating
/// method acquires the project lock, writes a timestamped backup under
/// <c>&lt;project&gt;/.mod-history/</c>, and rewrites the file.
/// </summary>
/// <remarks>
/// The API is stateless: each call opens the file, mutates the in-memory line
/// list, and writes it back. Skills are manipulated as raw line blocks — no
/// typed schema is used at this phase.
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
}
