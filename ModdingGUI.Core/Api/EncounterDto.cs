namespace ModdingGUI.Core.Api;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>One <c>KEY: value</c> line, with the key as written and the raw value.</summary>
/// <param name="Key">Key exactly as written (case preserved).</param>
/// <param name="RawValue">Everything after the colon, trimmed.</param>
public sealed record EncounterFieldDto(string Key, string RawValue);

/// <summary>One <c>TEAM: n, flag</c> section of an encounter, with its own fields and units.</summary>
public sealed class EncounterTeamDto
{
    /// <summary>Team number from the <c>TEAM:</c> line.</summary>
    public int Index { get; init; }

    /// <summary>The rest of the <c>TEAM:</c> line (the second number, usually 0).</summary>
    public string RawValue { get; init; } = string.Empty;

    /// <summary>Fields of the team section, in file order (<c>GRIDFILE</c>, <c>SCHOOL</c>, …).</summary>
    public IReadOnlyList<EncounterFieldDto> Fields { get; init; } = Array.Empty<EncounterFieldDto>();

    /// <summary>Raw <c>UNITDB:</c> argument lists, one per unit, in file order.</summary>
    public IReadOnlyList<string> Units { get; init; } = Array.Empty<string>();

    /// <summary>All values of a field, in file order.</summary>
    public IReadOnlyList<string> Values(string key) =>
        Fields.Where(f => string.Equals(f.Key, key, StringComparison.OrdinalIgnoreCase))
              .Select(f => f.RawValue)
              .ToArray();

    /// <summary>The first value of a field, or null.</summary>
    public string? Value(string key) =>
        Fields.FirstOrDefault(f => string.Equals(f.Key, key, StringComparison.OrdinalIgnoreCase))?.RawValue;
}

/// <summary>
/// A parsed <c>.enc</c> encounter: header fields plus its team sections.
/// </summary>
/// <remarks>
/// Encounters share the tok grammar but not its shape: a file is a single encounter, with
/// header keys (<c>ENCOUNTERNAME</c>, <c>SCENE</c>, <c>GRIDFILE</c>, <c>CUTSCENE</c>, …) followed
/// by one section per team. <c>UNITDB</c> rows are positional and long (20 arguments: unit
/// name, level, start slot, skills, items, …), so they are exposed verbatim.
/// </remarks>
public sealed class EncounterDto
{
    /// <summary>Project-relative path, e.g. <c>data/encounters/Nordagh/thefen/sx_arenacert6.enc</c>.</summary>
    public string FilePath { get; init; } = string.Empty;

    /// <summary>Value of <c>ENCOUNTERNAME</c>, or the file stem when absent.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Header fields (everything before the first <c>TEAM:</c>), in file order.</summary>
    public IReadOnlyList<EncounterFieldDto> Fields { get; init; } = Array.Empty<EncounterFieldDto>();

    /// <summary>Team sections, in file order.</summary>
    public IReadOnlyList<EncounterTeamDto> Teams { get; init; } = Array.Empty<EncounterTeamDto>();

    /// <summary>All values of a header field, in file order.</summary>
    public IReadOnlyList<string> Values(string key) =>
        Fields.Where(f => string.Equals(f.Key, key, StringComparison.OrdinalIgnoreCase))
              .Select(f => f.RawValue)
              .ToArray();

    /// <summary>The first value of a header field, or null.</summary>
    public string? Value(string key) =>
        Fields.FirstOrDefault(f => string.Equals(f.Key, key, StringComparison.OrdinalIgnoreCase))?.RawValue;

    /// <summary>The team with the given index, or null.</summary>
    public EncounterTeamDto? Team(int index) => Teams.FirstOrDefault(t => t.Index == index);

    /// <summary>Total unit count across all teams.</summary>
    public int UnitCount => Teams.Sum(t => t.Units.Count);
}
