using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace ModdingGUI.Core.Units;

/// <summary>
/// The <c>Header (…): n</c> line at the top of <c>schools.txt</c> and <c>tints.txt</c>.
/// </summary>
/// <remarks>
/// <para>
/// Not decoration: the unpack tool writes this value from byte 0 of <c>unitschools.idx</c> /
/// <c>unittints.idx</c>, and the repack tool writes it back into that byte
/// (<c>f.write(int(schoolsHeader).to_bytes(1, "big"))</c>). Both files carry 205 in vanilla — the
/// meaning of the byte is unknown, but it is data the game reads, so it must survive a round trip.
/// </para>
/// <para>
/// The line is also load-bearing for the tools: the repack parses it with a regex on the file's first
/// line and throws when it is missing or not a number, so a hand-edited file without it cannot be packed.
/// </para>
/// </remarks>
public static class UnitHeader
{
    private static readonly Regex HeaderLine =
        new(@"^\s*Header[^:]*:\s*(?<value>-?\d+)\s*$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    /// <summary>Find and validate the header line. Returns null when it is present and usable.</summary>
    public static string? Validate(IReadOnlyList<string> lines, string fileName)
    {
        for (int i = 0; i < Math.Min(lines.Count, 5); i++)
        {
            Match match = HeaderLine.Match(lines[i]);
            if (!match.Success) continue;

            if (!int.TryParse(match.Groups["value"].Value, NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out int value))
            {
                return $"{fileName}: the header value is not a number; the repack cannot write it";
            }

            if (value is < 0 or > 255)
            {
                return $"{fileName}: header {value} does not fit in the one byte the repack writes (0-255)";
            }

            return null;
        }

        return $"{fileName}: no 'Header (…): <n>' line; the repack reads it from the first line and will fail";
    }
}
