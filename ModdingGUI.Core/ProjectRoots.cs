namespace ModdingGUI.Core;

using System;
using System.IO;
using System.Linq;

/// <summary>
/// Resolves the folder layout of a project.
/// </summary>
/// <remarks>
/// A project is a folder containing <c>&lt;name&gt;_BEC</c> and <c>&lt;name&gt;_ISO</c>; the
/// Apis operate on the **BEC** folder because that is where <c>data/</c> lives. Callers are
/// inconsistent about which one they hold — the item editor keeps the BEC folder, the skill
/// editor keeps the project root, tests pass BEC — so the boundary accepts either instead of
/// letting a wrong guess surface as "classdefs.tok not found" at some later call.
/// </remarks>
public static class ProjectRoots
{
    /// <summary>
    /// Return the BEC folder for <paramref name="path"/>, which may be either the project
    /// root (<c>...\MyMod</c>) or its BEC folder (<c>...\MyMod_BEC</c>).
    /// </summary>
    /// <remarks>
    /// When neither shape matches, the input is returned unchanged so the caller's own
    /// "file not found" message still names the path the user supplied.
    /// </remarks>
    public static string ResolveBecFolder(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        string full = Path.GetFullPath(path).TrimEnd(
            Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        // A path to a file inside the project (the Grid Builder hands over the .grd it is editing)
        // must resolve to the project, not to the file. Without this, the caller derives a relative
        // path of "." and ends up trying to create a directory where the file already lives.
        if (File.Exists(full))
        {
            full = Path.GetDirectoryName(full) ?? full;
        }

        // Already a BEC folder: data/config is the giveaway, and it is what every Api needs.
        if (Directory.Exists(Path.Combine(full, "data", "config"))) return full;

        string leaf = Path.GetFileName(full);
        if (leaf.Length > 0)
        {
            string sibling = Path.Combine(full, leaf + "_BEC");
            if (Directory.Exists(sibling)) return sibling;
        }

        // Project folder renamed independently of its children: accept a single *_BEC child.
        if (Directory.Exists(full))
        {
            var candidates = Directory.EnumerateDirectories(full, "*_BEC").Take(2).ToArray();
            if (candidates.Length == 1) return candidates[0];
        }

        // Walk up: any path inside a project (data/encounters/..., data/levels/...) resolves to the
        // enclosing BEC folder, which is the only answer an Api can use.
        string current = full;
        for (int depth = 0; depth < 16; depth++)
        {
            string? parent = Path.GetDirectoryName(current);
            if (string.IsNullOrEmpty(parent) || parent.Length >= current.Length) break;
            current = parent;

            if (Directory.Exists(Path.Combine(current, "data", "config"))) return current;

            var nested = Directory.EnumerateDirectories(current, "*_BEC").Take(2).ToArray();
            if (nested.Length == 1) return nested[0];
        }

        return full;
    }

    /// <summary>True when <paramref name="path"/> looks like a BEC folder (has <c>data/config</c>).</summary>
    public static bool IsBecFolder(string path) =>
        !string.IsNullOrWhiteSpace(path)
        && Directory.Exists(Path.Combine(path, "data", "config"));
}
