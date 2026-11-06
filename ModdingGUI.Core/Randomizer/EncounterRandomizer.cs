namespace ModdingGUI.Core.Randomizer;

using System;
using System.Collections.Generic;
using System.Linq;
using ModdingGUI.Core.Api;
using ModdingGUI.Core.Logging;

/// <summary>What the encounter pass changed.</summary>
/// <param name="FilesChanged">Encounters rewritten.</param>
/// <param name="TeamUnitsBlanked">Player-team unit rows replaced with a "any unit" placeholder.</param>
/// <param name="EnemiesRandomized">Enemy rows turned into randomizable slots.</param>
/// <param name="CandieInserted">Encounters that gained a <c>CANDIE:</c> line.</param>
public sealed record EncounterEditResult(
    int FilesChanged, int TeamUnitsBlanked, int EnemiesRandomized, int CandieInserted);

/// <summary>
/// The encounter rewrite the randomizer has always done, in Core.
/// </summary>
/// <remarks>
/// <para>
/// Two things happen here. The player's team (team 0) is emptied out — each <c>UNITDB</c> row becomes a blank-name
/// placeholder, so the game fills those slots from the player's own school instead of the authored units. Enemy
/// teams are left alone unless enemy randomization is on, in which case a row with an empty name becomes a
/// randomizable slot that <b>prohibits</b> the classes that must never appear at random: <c>Boss</c>, <c>Prop</c>,
/// <c>DarkGod</c> and <c>AffinityBeastGreater</c>. That prohibition lives in the row itself (class-name fields plus
/// the prohibit flag), not in a global blacklist, which is why the shipped randomizer wrote it per row.
/// </para>
/// <para>
/// Two encounters are special: <c>scene47u.enc</c> and <c>scene47v.enc</c> get four named hero rows instead
/// (Ursula, Valens, Urlan, Eiji on their own start slots), because their player team is scripted.
/// </para>
/// </remarks>
public sealed class EncounterRandomizer
{
    /// <summary>Classes that may never be picked for a randomized enemy.</summary>
    public static readonly string[] ProhibitedEnemyClasses =
    {
        "Boss", "Prop", "DarkGod", "AffinityBeastGreater",
    };

    private static readonly string[] SpecialFiles = { "scene47u.enc", "scene47v.enc" };

    /// <summary>The four hero rows the special encounters get, in the original's order.</summary>
    private static readonly (string Unit, string Slot)[] SpecialRows =
    {
        ("Ursula", "Start2"), ("Valens", "Start1"), ("Urlan", "Start3"), ("Eiji", "Start4"),
    };

    private readonly IEncountersApi _encounters;
    private readonly IAppLog? _log;

    /// <summary>Create the pass.</summary>
    public EncounterRandomizer(IEncountersApi? encounters = null, IAppLog? log = null)
    {
        _encounters = encounters ?? new EncountersApi();
        _log = log;
    }

    /// <summary>
    /// Rewrite every encounter. <paramref name="randomizeEnemies"/> turns empty-name enemy rows into randomizable
    /// slots; <paramref name="permaDeath"/> adds the <c>CANDIE:</c> line the mode needs.
    /// </summary>
    public EncounterEditResult Apply(string projectRoot, bool randomizeEnemies, bool permaDeath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);

        int changed = 0, blanked = 0, enemies = 0, candie = 0;

        foreach (string relative in _encounters.ListFiles(projectRoot))
        {
            var (lines, trailing) = _encounters.ReadLines(projectRoot, relative);
            bool special = SpecialFiles.Any(name =>
                Path.GetFileName(relative).Equals(name, StringComparison.OrdinalIgnoreCase));

            bool modified = false;
            bool candiePresent = lines.Any(line => line.Trim().Equals("CANDIE:", StringComparison.OrdinalIgnoreCase));
            bool specialRowsAdded = false;
            int team = -1;
            bool inTeamZero = false;

            var output = new List<string>(lines.Count + 8);

            foreach (string line in lines)
            {
                string trimmed = line.Trim();

                int? teamLine = ReadTeamIndex(trimmed);
                if (teamLine is not null)
                {
                    team = teamLine.Value;
                    inTeamZero = team == 0;

                    if (permaDeath && team == 0 && !candiePresent)
                    {
                        output.Add("CANDIE:");
                        candiePresent = true;
                        candie++;
                        modified = true;
                    }

                    output.Add(line);

                    // The scripted encounters cannot use placeholders: give them the four named heroes, but only
                    // when they are not already there - the shipped scene47u/v blocks already name all four.
                    if (special && team == 0 && !specialRowsAdded && !HasSpecialRows(lines))
                    {
                        foreach (var (unit, slot) in SpecialRows)
                        {
                            output.Add(UnitRow(unit, slot));
                        }

                        specialRowsAdded = true;
                        modified = true;
                    }

                    continue;
                }

                // SCHOOL: is dropped from player and opponent teams, but the prop team's school is what makes
                // its scenery work: the old randomizer skipped the line only when the team was not -1.
                if (trimmed.StartsWith("SCHOOL:", StringComparison.OrdinalIgnoreCase) && team >= 0)
                {
                    modified = true;
                    continue;
                }

                // The ease values are pinned to 0; only a different value counts as a change.
                if (trimmed.StartsWith("URSULAEASE:", StringComparison.OrdinalIgnoreCase))
                {
                    output.Add(trimmed == "URSULAEASE: 0" ? line : "URSULAEASE: 0");
                    if (trimmed != "URSULAEASE: 0") modified = true;
                    continue;
                }

                if (trimmed.StartsWith("VALENSEASE:", StringComparison.OrdinalIgnoreCase))
                {
                    output.Add(trimmed == "VALENSEASE: 0" ? line : "VALENSEASE: 0");
                    if (trimmed != "VALENSEASE: 0") modified = true;
                    continue;
                }

                if (trimmed.StartsWith("UNITDB:", StringComparison.OrdinalIgnoreCase))
                {
                    string slot = ReadStartSlot(trimmed);
                    string unitName = ReadUnitName(trimmed);

                    if (inTeamZero && !special)
                    {
                        string blank = BlankRow(slot);
                        if (blank == line)
                        {
                            output.Add(line);      // already a placeholder: leave the file untouched
                            continue;
                        }

                        output.Add(blank);
                        blanked++;
                        modified = true;
                        continue;
                    }

                    // Only real opponent teams are randomized. Team -1 is the prop/neutral team, whose
                    // rows carry count 0 and exist to give a match its scenery - replacing those with
                    // gladiators puts fighters on a team that never gets a turn, and the old randomizer
                    // guarded this the same way (`currentTeamNumber > 0`).
                    if (team > 0 && randomizeEnemies && string.IsNullOrEmpty(unitName) && !IsDisabledRow(trimmed))
                    {
                        string randomizable = RandomizableRow(slot);
                        if (randomizable == line)
                        {
                            output.Add(line);
                            continue;
                        }

                        output.Add(randomizable);
                        enemies++;
                        modified = true;
                        continue;
                    }
                }

                output.Add(line);
            }

            if (!modified) continue;

            _encounters.WriteLines(projectRoot, relative, output);
            changed++;
        }

        _log?.Write(LogLevel.Info,
            $"Encounters rewritten: {changed} file(s), {blanked} player slot(s) opened, " +
            $"{enemies} enemy slot(s) randomized, {candie} CANDIE insertion(s).", "randomizer");

        return new EncounterEditResult(changed, blanked, enemies, candie);
    }

    /// <summary>
    /// A row whose count (second argument) is 0. Vanilla only uses these on the prop team, where they mean
    /// "nothing spawns here"; they must never be turned into a random unit.
    /// </summary>
    private static bool IsDisabledRow(string trimmed)
    {
        var parts = SplitArguments(trimmed);
        return parts.Count > 1 && parts[1].Trim() == "0";
    }

    /// <summary>True when every special hero already appears on a <c>UNITDB</c> row.</summary>
    private static bool HasSpecialRows(IReadOnlyList<string> lines) =>
        SpecialRows.All(row => lines.Any(line =>
            line.TrimStart().StartsWith("UNITDB:", StringComparison.OrdinalIgnoreCase)
            && ReadUnitName(line.Trim()).Equals(row.Unit, StringComparison.OrdinalIgnoreCase)));

    /// <summary>A player-team slot the game fills from the school.</summary>
    internal static string BlankRow(string startSlot) =>
        $"UNITDB:\t\"\", 99, \"{startSlot}\", 0, 0, 0, 0, -1, \"\", \"\", \"\", \"\", 0, 0, 0, 0, 0, 0, 0, 0, 0";

    /// <summary>A named unit row, used for the scripted encounters.</summary>
    internal static string UnitRow(string unit, string startSlot) =>
        $"UNITDB:\t\"{unit}\", 99, \"{startSlot}\", 0, 0, 0, 0, -1, \"\", \"\", \"\", \"\", 0, 0, 0, 0, 0, 0, 0, 0, 0";

    /// <summary>
    /// A randomizable enemy slot: level -2..+2 around the school average, no affinity requirement, and the four
    /// classes that must never appear listed for prohibition.
    /// </summary>
    internal static string RandomizableRow(string startSlot) =>
        $"UNITDB:\t\"\", 99, \"{startSlot}\", -2, 2, 1, 0, -1, " +
        string.Join(", ", ProhibitedEnemyClasses.Select(name => $"\"{name}\"")) +
        ", 0, 1, 0, 0, 0, 0, 25, 0, 0";

    /// <summary>
    /// Team index of a <c>TEAM:</c> line, or null when the line is not one. Null and -1 must be distinguishable:
    /// -1 is the prop team, whose rows and school have to survive untouched.
    /// </summary>
    private static int? ReadTeamIndex(string trimmed)
    {
        if (!trimmed.StartsWith("TEAM:", StringComparison.OrdinalIgnoreCase)) return null;

        string rest = trimmed["TEAM:".Length..];
        int comma = rest.IndexOf(',');
        string value = (comma >= 0 ? rest[..comma] : rest).Trim();
        return int.TryParse(value, out int index) ? index : null;
    }

    /// <summary>Third argument of a <c>UNITDB</c> row (the start slot), or Start1 when absent.</summary>
    private static string ReadStartSlot(string trimmed)
    {
        var parts = SplitArguments(trimmed);
        return parts.Count >= 3 && parts[2].Length > 0 ? parts[2] : "Start1";
    }

    /// <summary>First argument of a <c>UNITDB</c> row (the unit name).</summary>
    private static string ReadUnitName(string trimmed)
    {
        var parts = SplitArguments(trimmed);
        return parts.Count > 0 ? parts[0] : string.Empty;
    }

    /// <summary>Split a row's arguments on commas that are outside double quotes.</summary>
    private static List<string> SplitArguments(string trimmed)
    {
        int colon = trimmed.IndexOf(':');
        if (colon < 0) return new List<string>();

        var parts = new List<string>();
        var current = new List<char>();
        bool quoted = false;

        foreach (char c in trimmed[(colon + 1)..])
        {
            if (c == '"')
            {
                quoted = !quoted;
                continue;
            }

            if (c == ',' && !quoted)
            {
                parts.Add(new string(current.ToArray()).Trim());
                current.Clear();
                continue;
            }

            if (!quoted && (c == '/' && current.Count == 1 && current[0] == '/'))
            {
                break;   // trailing comment
            }

            current.Add(c);
        }

        parts.Add(new string(current.ToArray()).Trim());
        return parts;
    }
}
