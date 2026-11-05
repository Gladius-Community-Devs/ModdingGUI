namespace ModdingGUI.Core.Randomizer;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ModdingGUI.Core.Api;
using ModdingGUI.Core.Tok;

/// <summary>One substitution in a script: a line fragment that must be rewritten.</summary>
/// <param name="Find">Fragment to look for, matched with <see cref="StringComparison.Ordinal"/>.</param>
/// <param name="Replace">Replacement fragment.</param>
public sealed record ScriptCondition(string Find, string Replace);

/// <summary>A script to patch, with the whole lines it must lose.</summary>
/// <param name="FileName">File under <c>data/script</c>.</param>
/// <param name="Conditions">Fragment substitutions; a missing fragment is reported, not fatal.</param>
/// <param name="RemoveLines">Lines to delete, compared after trimming.</param>
public sealed record ScriptPatch(
    string FileName,
    IReadOnlyList<ScriptCondition> Conditions,
    IReadOnlyList<string> RemoveLines);

/// <summary>
/// A whole block to replace, matched line by line after trimming — used where the campaign branches on which
/// hero is in the school.
/// </summary>
/// <param name="FileName">File under <c>data/script</c>.</param>
/// <param name="Original">Block to find; lines compared trimmed.</param>
/// <param name="Replacement">Block to put in its place.</param>
public sealed record ScriptBlockPatch(string FileName, IReadOnlyList<string> Original, IReadOnlyList<string> Replacement);

/// <summary>What patching the scripts changed.</summary>
/// <param name="Modified">Scripts that were actually rewritten, in patch order.</param>
/// <param name="Missing">Scripts that were listed but absent from the project.</param>
public sealed record ScriptPatchResult(IReadOnlyList<string> Modified, IReadOnlyList<string> Missing)
{
    /// <summary>True when at least one script changed and therefore needs compiling.</summary>
    public bool HasChanges => Modified.Count > 0;
}

/// <summary>
/// The script edits the randomizer has always made, and the machinery to apply them.
/// </summary>
/// <remarks>
/// <para>
/// Randomizing a hero's class breaks the campaign scripts in two ways: the cameo "costume change" branches test
/// for the original hero, and the flow scripts remove Ludo and Gwazi from the school because they used to be
/// recruitable later. Both are edited here, exactly as the original randomizer did, and the edited scripts are
/// compiled afterwards so the change actually reaches the game.
/// </para>
/// <para>
/// The list is data, not prose: four flow scripts gain a rewritten <c>CostumeChange</c> test and lose their
/// <c>SchoolRemoveCharacter</c> lines, the two endgame scripts gain two rewritten tests, and — only when Valens is
/// the hero — the Fjord league's Urlan-specific branch is replaced with its Valens branch.
/// </para>
/// </remarks>
public sealed class ScriptPatcher
{
    /// <summary>Scripts patched for every run, in the order the original randomizer applied them.</summary>
    public static IReadOnlyList<ScriptPatch> Patches(HeroChoice hero)
    {
        var list = new List<ScriptPatch>
        {
            FlowScript("gameflowvalenssteppes.scp"),
            FlowScript("gameflowvalensexpanse.scp"),
            FlowScript("gameflowursulasteppes.scp"),
            FlowScript("gameflowursulaexpanse.scp"),
            EndgameScript("gameflowvalensendgame.scp"),
            EndgameScript("gameflowursulaendgame.scp"),
        };

        return list;
    }

    private static ScriptPatch FlowScript(string file) => new(
        file,
        new[] { new ScriptCondition("if (GetIntVar(\"CostumeChange\") != 1)", "if (GetIntVar(\"CostumeChange\") != 0)") },
        new[] { "gb.SchoolRemoveCharacter(\"Ludo\")", "gb.SchoolRemoveCharacter(\"Gwazi\")" });

    private static ScriptPatch EndgameScript(string file) => new(
        file,
        new[]
        {
            new ScriptCondition("if (GetIntVar(\"EndGameCostumeChangeAgain\") != 1)",
                "if (GetIntVar(\"EndGameCostumeChangeAgain\") != 0)"),
            new ScriptCondition("if (GetIntVar(\"EndGameCostumeChange\") != 1)",
                "if (GetIntVar(\"EndGameCostumeChange\") != 0)"),
        },
        Array.Empty<string>());

    /// <summary>
    /// The Fjord league branches on Urlan being in the school; when Valens is the hero that branch can never be
    /// taken, so the original randomizer rewrote it to test the Valens league name directly.
    /// </summary>
    private static ScriptBlockPatch ValensLeaguePatch() => new(
        "leaguefjordfallen.scp",
        new[]
        {
            "if ( school.IsMember(\"Urlan\") )",
            "if ( name == \"AheadOfThePack\" )",
            "return (greyleague)",
            "else",
            "return (hideleague)",
            "endif",
            "else",
            "if ( name == \"PackValens\" )",
            "return (greyleague)",
            "else",
            "return (hideleague)",
            "endif",
            "endif",
        },
        new[]
        {
            "if ( name == \"PackValens\" )",
            "return (greyleague)",
            "else",
            "return (hideleague)",
            "endif",
        });

    /// <summary>
    /// Apply every patch for the chosen hero. Scripts are read and written through <see cref="RawLineFile"/> so
    /// per-line terminators survive, and each changed file gets a backup like every other write in Core.
    /// </summary>
    public ScriptPatchResult Apply(string projectRoot, HeroChoice hero)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);

        string bec = ProjectRoots.ResolveBecFolder(projectRoot);
        var modified = new List<string>();
        var missing = new List<string>();

        foreach (var patch in Patches(hero))
        {
            string relative = "data/script/" + patch.FileName;
            string path = Path.Combine(bec, relative.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path))
            {
                missing.Add(patch.FileName);
                continue;
            }

            using (ProjectLock.Acquire(bec))
            {
                var (lines, trailing) = RawLineFile.Read(bec, relative, patch.FileName);
                bool changed = false;

                foreach (var condition in patch.Conditions)
                {
                    for (int i = 0; i < lines.Count; i++)
                    {
                        if (!lines[i].Contains(condition.Find, StringComparison.Ordinal)) continue;
                        lines[i] = lines[i].Replace(condition.Find, condition.Replace, StringComparison.Ordinal);
                        changed = true;
                    }
                }

                if (patch.RemoveLines.Count > 0)
                {
                    int removed = lines.RemoveAll(line =>
                        patch.RemoveLines.Any(target =>
                            line.Trim().Equals(target, StringComparison.Ordinal)));
                    changed |= removed > 0;
                }

                if (!changed) continue;

                RawLineFile.Write(bec, relative, lines, trailing);
                modified.Add(patch.FileName);
            }
        }

        foreach (var blockPatch in hero == HeroChoice.Valens
                     ? new[] { ValensLeaguePatch() }
                     : Array.Empty<ScriptBlockPatch>())
        {
            string relative = "data/script/" + blockPatch.FileName;
            string path = Path.Combine(bec, relative.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path))
            {
                missing.Add(blockPatch.FileName);
                continue;
            }

            using (ProjectLock.Acquire(bec))
            {
                var (lines, trailing) = RawLineFile.Read(bec, relative, blockPatch.FileName);

                // Six branches in this file test IsMember("Urlan"); only the league-name one returns greyleague
                // and hideleague, so pick the occurrence followed by that call.
                int start = FindLeagueBranch(lines);
                if (start < 0) continue;

                int end = FindIfBlockEnd(lines, start);
                if (end < 0) continue;

                lines.RemoveRange(start, end - start + 1);
                lines.InsertRange(start, blockPatch.Replacement);
                RawLineFile.Write(bec, relative, lines, trailing);

                if (!modified.Contains(blockPatch.FileName)) modified.Add(blockPatch.FileName);
            }
        }

        return new ScriptPatchResult(modified, missing);
    }

    /// <summary>
    /// First <c>IsMember("Urlan")</c> branch whose body returns <c>greyleague</c> — the league-name test, as
    /// opposed to the five talk-box branches that also test for Urlan.
    /// </summary>
    private static int FindLeagueBranch(IReadOnlyList<string> lines)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            if (!lines[i].Contains("school.IsMember(\"Urlan\")", StringComparison.Ordinal)) continue;

            for (int j = i + 1; j < Math.Min(lines.Count, i + 30); j++)
            {
                if (lines[j].Contains("greyleague", StringComparison.Ordinal)) return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// Index of the <c>endif</c> that closes the <c>if</c> at <paramref name="start"/>, counting nesting. Scripts
    /// are tab-indented and full of blank lines, so matching the block text exactly is brittle; counting the
    /// tokens is not.
    /// </summary>
    private static int FindIfBlockEnd(IReadOnlyList<string> lines, int start)
    {
        int depth = 0;

        for (int i = start; i < lines.Count; i++)
        {
            string text = lines[i].Trim();

            if (text.StartsWith("if", StringComparison.Ordinal) && text.Contains('(')) depth++;
            else if (text.Equals("endif", StringComparison.Ordinal))
            {
                depth--;
                if (depth == 0) return i;
            }
        }

        return -1;
    }
}
