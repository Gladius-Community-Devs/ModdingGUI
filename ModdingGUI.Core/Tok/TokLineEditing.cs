namespace ModdingGUI.Core.Tok;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

/// <summary>
/// Line-level helpers shared by the raw tok editors (encounters, schools, leagues, config
/// tables) so the key grammar and commenting rules are defined once.
/// </summary>
/// <remarks>
/// tok spells most entries <c>KEY: value</c>, but the corpus also contains colon-less keys —
/// <c>PRIZETIER&lt;tab&gt;"DrakeDefaultCash" 0</c> in encounters, <c>NAME&lt;tab&gt;"Shop"</c>
/// in shops. Keys are upper-case, which is what keeps prose out of the parse.
/// </remarks>
internal static class TokLineEditing
{
    /// <summary>An upper-case key followed by a colon (with optional padding) or whitespace.</summary>
    internal static readonly Regex KeyRx = new(
        @"^(?<indent>\s*)(?<key>[A-Z][A-Z0-9_]*)(?<sep>\s*:\s*|\s+)(?<value>.*)$",
        RegexOptions.Compiled);

    internal static readonly Regex CommentRx = new(@"^\s*//", RegexOptions.Compiled);

    /// <summary>Parse a <c>KEY value</c> line. The prefix keeps the original spacing verbatim.</summary>
    internal static bool TryKey(string line, out string key, out string prefix, out string rawValue)
    {
        var match = KeyRx.Match(line);
        if (!match.Success)
        {
            key = string.Empty;
            prefix = string.Empty;
            rawValue = string.Empty;
            return false;
        }

        key = match.Groups["key"].Value;
        prefix = match.Groups["indent"].Value + key + match.Groups["sep"].Value;
        rawValue = StripInlineComment(match.Groups["value"].Value);
        return true;
    }

    /// <summary>Trim a trailing <c>//</c> comment and surrounding whitespace.</summary>
    internal static string StripInlineComment(string raw)
    {
        int comment = raw.IndexOf("//", StringComparison.Ordinal);
        if (comment >= 0) raw = raw[..comment];
        return raw.Trim();
    }

    /// <summary>Absolute indices of key lines for <paramref name="key"/> inside a range.</summary>
    internal static List<int> KeyLines(
        IReadOnlyList<string> lines, int start, int end, string key, string? excluded = null)
    {
        var found = new List<int>();
        for (int i = Math.Max(0, start); i < Math.Min(end, lines.Count); i++)
        {
            string line = lines[i];
            if (CommentRx.IsMatch(line)) continue;
            if (!TryKey(line, out string candidate, out _, out _)) continue;
            if (excluded is not null && string.Equals(candidate, excluded, StringComparison.OrdinalIgnoreCase))
                continue;
            if (string.Equals(candidate, key, StringComparison.OrdinalIgnoreCase)) found.Add(i);
        }
        return found;
    }

    /// <summary>First key line for <paramref name="key"/> inside a range, or -1.</summary>
    internal static int FirstKeyLine(IReadOnlyList<string> lines, int start, int end, string key)
    {
        var matches = KeyLines(lines, start, end, key);
        return matches.Count > 0 ? matches[0] : -1;
    }

    /// <summary>
    /// Replace only the value portion of a line, so indentation and the colon spacing survive
    /// and a no-op write reproduces the file byte for byte.
    /// </summary>
    internal static bool ReplaceValue(List<string> lines, int index, string rawValue)
    {
        if (!TryKey(lines[index], out _, out string prefix, out _)) return false;
        lines[index] = prefix + rawValue;
        return true;
    }

    /// <summary>Insert a new <c>KEY: value</c> line at <paramref name="at"/>.</summary>
    internal static void Insert(List<string> lines, int at, string key, string rawValue, string indent = "\t")
    {
        lines.Insert(Math.Clamp(at, 0, lines.Count), $"{indent}{key}: {rawValue}");
    }

    /// <summary>Remove every line for <paramref name="key"/> in a range; returns how many went.</summary>
    internal static int RemoveKeys(List<string> lines, int start, int end, string key, string? excluded = null)
    {
        var matches = KeyLines(lines, start, end, key, excluded);
        for (int i = matches.Count - 1; i >= 0; i--) lines.RemoveAt(matches[i]);
        return matches.Count;
    }

    /// <summary>Leading whitespace of a line, used to match a file's indentation style.</summary>
    internal static string IndentOf(string line) =>
        new(line.TakeWhile(char.IsWhiteSpace).ToArray());
}
