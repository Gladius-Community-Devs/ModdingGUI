namespace ModdingGUI.Core.Api;

using System;
using System.Collections.Generic;

/// <summary>
/// Resolves a <c>KEY: &lt;lookup id&gt;</c> line against a lookup text table.
/// </summary>
/// <remarks>
/// Tok files reference localised text by numeric id (<c>ITEMDISPLAYNAMEID: 4458</c>), so
/// editors need to turn that line into readable text and back. This is the read side of
/// <see cref="ILookupTextApi"/>; writing still goes through the API so backups and locking
/// apply.
/// </remarks>
public static class LookupTextResolver
{
    /// <summary>
    /// Read the id from <paramref name="line"/> (everything after the first colon) and
    /// return its text, or <paramref name="fallback"/> when the line has no id or the id is
    /// missing from the table.
    /// </summary>
    /// <param name="line">A full tok line such as <c>ITEMDISPLAYNAMEID: 4458</c>.</param>
    /// <param name="lookup">The id-to-text table, typically from <see cref="ILookupTextApi.Load(string)"/>.</param>
    /// <param name="foundId">The parsed id when the line contained a number, otherwise null.</param>
    /// <param name="fallback">Value returned when nothing resolves.</param>
    public static string ResolveValue(
        string line,
        IReadOnlyDictionary<int, string> lookup,
        out int? foundId,
        string fallback = "")
    {
        foundId = null;
        if (line is null || lookup is null) return fallback;

        string[] parts = line.Split(new[] { ':' }, 2);
        if (parts.Length < 2) return fallback;

        if (!int.TryParse(parts[1].Trim(), out int id)) return fallback;

        foundId = id;
        return lookup.TryGetValue(id, out string? text) ? text : fallback;
    }
}
