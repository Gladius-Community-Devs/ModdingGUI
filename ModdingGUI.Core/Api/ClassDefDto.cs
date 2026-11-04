namespace ModdingGUI.Core.Api;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>One <c>KEY: value</c> line inside a class definition.</summary>
/// <param name="Key">Key exactly as written in the file (case preserved).</param>
/// <param name="RawValue">Everything after the colon, trimmed of surrounding whitespace.</param>
public sealed record ClassFieldDto(string Key, string RawValue);

/// <summary>
/// A single <c>CREATECLASS:</c> block: the class name plus its ordered field lines.
/// </summary>
/// <remarks>
/// The block model is deliberately raw. Class definitions mix positional-ish argument
/// lists (<c>ITEMCAT: weapon, Bow, Plain</c>) with free text, comments and tab indentation,
/// so keeping the lines verbatim is what guarantees a no-op edit reproduces the file.
/// </remarks>
public sealed class ClassDefDto
{
    /// <summary>Class name as written after <c>CREATECLASS:</c>.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Field lines in file order.</summary>
    public IReadOnlyList<ClassFieldDto> Fields { get; init; } = Array.Empty<ClassFieldDto>();

    /// <summary>All values of a key, in file order (a key may repeat, e.g. <c>ATTRIBUTE</c>).</summary>
    public IReadOnlyList<string> Values(string key) =>
        Fields.Where(f => string.Equals(f.Key, key, StringComparison.OrdinalIgnoreCase))
              .Select(f => f.RawValue)
              .ToArray();

    /// <summary>The first value of a key, or null.</summary>
    public string? Value(string key) =>
        Fields.FirstOrDefault(f => string.Equals(f.Key, key, StringComparison.OrdinalIgnoreCase))?.RawValue;

    /// <summary>How many times a key appears.</summary>
    public int Count(string key) =>
        Fields.Count(f => string.Equals(f.Key, key, StringComparison.OrdinalIgnoreCase));
}
