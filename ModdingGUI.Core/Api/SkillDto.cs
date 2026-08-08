namespace ModdingGUI.Core.Api;

using System.Collections.Generic;

/// <summary>
/// A single body field entry inside a <c>SKILLCREATE</c> record.
/// </summary>
public sealed class SkillFieldEntry
{
    /// <summary>Field name exactly as it appears in the file, e.g. <c>SKILLAFFINITY</c>.</summary>
    public string FieldName { get; set; } = string.Empty;

    /// <summary>
    /// Raw value text verbatim from the file, e.g. <c>Fire</c>, <c>"Knockback", 100, 1</c>,
    /// or <c>1, 30</c>. Quotes are preserved exactly as written.
    /// </summary>
    public string RawValue { get; set; } = string.Empty;
}

/// <summary>
/// Wire-level Data Transfer Object for a <c>skills.tok</c> record.
/// </summary>
/// <remarks>
/// The SKILL Box pattern means the GUI round-trips through <see cref="Fields"/>
/// verbatim: what the user sees in the text area is exactly what gets written
/// back to disk. The Core API never re-parses or validates slot types.
/// </remarks>
public sealed class SkillDto
{
    /// <summary>Stable id — the first <c>SKILLCREATE</c> positional. Required on Add.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Second <c>SKILLCREATE</c> positional, e.g. <c>Attack</c>, <c>Innate</c>.</summary>
    public string? SkillType { get; set; }

    /// <summary>Third <c>SKILLCREATE</c> positional, e.g. <c>Standard</c>, <c>Status</c>.</summary>
    public string? SkillSubType { get; set; }

    /// <summary>Body field entries in source order.</summary>
    public List<SkillFieldEntry> Fields { get; set; } = new();
}
