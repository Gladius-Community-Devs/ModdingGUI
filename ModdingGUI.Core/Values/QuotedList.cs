namespace ModdingGUI.Core.Values;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

/// <summary>
/// Comma-separated, double-quoted argument lists — the shape a roster line like
/// <c>ITEMSCOMP: "Machera", "Short Cape", "", "", "Air Badge of Aeris"</c> uses.
/// </summary>
/// <remarks>
/// Splitting on <c>','</c> alone is wrong: an item name may contain a comma, and the format quotes every slot
/// precisely so it can. Both directions live here so the loader and the writer cannot drift — the loader used to read
/// <c>ITEMSCOMP</c> not at all, which is how a loaded unit arrived with no equipment and then had its roster line
/// rewritten with five empty slots.
/// </remarks>
public static class QuotedList
{
    /// <summary>Split a value into its parts, trimmed, with the quotes still on.</summary>
    public static IReadOnlyList<string> Split(string? value)
    {
        var parts = new List<string>();
        if (string.IsNullOrWhiteSpace(value)) return parts;

        var current = new StringBuilder();
        bool quoted = false;
        bool any = false;

        foreach (char c in value)
        {
            if (c == '"')
            {
                quoted = !quoted;
                any = true;
                current.Append(c);
                continue;
            }

            if (c == ',' && !quoted)
            {
                parts.Add(current.ToString().Trim());
                current.Clear();
                any = false;
                continue;
            }

            current.Append(c);
            any = true;
        }

        if (any || current.Length > 0) parts.Add(current.ToString().Trim());
        return parts;
    }

    /// <summary>Split a value into its parts with the quotes removed. Empty slots stay as empty strings.</summary>
    public static IReadOnlyList<string> SplitUnquoted(string? value) =>
        Split(value).Select(part => part.Trim().Trim('"').Trim()).ToArray();

    /// <summary>
    /// Render parts as a quoted list. An absent slot is written as <c>""</c>, which is what the engine's loader
    /// expects for "nothing in this slot".
    /// </summary>
    public static string Join(IEnumerable<string?> parts)
    {
        ArgumentNullException.ThrowIfNull(parts);

        return string.Join(", ", parts.Select(part =>
            "\"" + (part ?? string.Empty).Trim().Trim('"') + "\""));
    }
}
