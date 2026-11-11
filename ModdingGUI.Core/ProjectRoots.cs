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
            var candidates = EnumerateBecChildren(full);
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

            // Nothing exists here yet: a caller can legitimately resolve a path it is about to create (the Grid
            // Builder writing a grid into an arena folder that is not there). Enumerating a missing directory
            // throws DirectoryNotFoundException, which used to surface as "could not find a part of the path".
            var nested = EnumerateBecChildren(current);
            if (nested.Length == 1) return nested[0];
        }

        return full;
    }

    /// <summary>
    /// The folder projects are kept in. A configured folder wins when it exists; otherwise the app folder, which is
    /// where the tooling (and <c>tools/</c>) lives. Keeps one bad setting from making every project disappear.
    /// </summary>
    public static string ResolveProjectsFolder(string? configuredFolder, string appDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appDirectory);

        // Normalise away a trailing separator so callers compare and combine paths consistently.
        static string Clean(string path) => Path.GetFullPath(path.Trim()).TrimEnd(
            Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        if (!string.IsNullOrWhiteSpace(configuredFolder))
        {
            string configured = Clean(configuredFolder);
            if (Directory.Exists(configured)) return configured;
        }

        return Clean(appDirectory);
    }

    /// <summary>
    /// The project folder that owns a BEC folder, or the argument itself when this is not a real project layout.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Used where a caller only has the BEC path but needs somewhere that is not packed into the archive: the history
    /// folder is the case in point, because the packer ships every file under _BEC that its file list does not name.
    /// </para>
    /// <para>
    /// The check is strict - the folder must end in <c>_BEC</c> and have a sibling <c>*_ISO</c> - so a test fixture or a
    /// hand-made folder called <c>something_BEC</c> is returned unchanged. Real projects always have both halves.
    /// </para>
    /// </remarks>
    public static string ProjectFolderOf(string becFolder)
    {
        if (string.IsNullOrWhiteSpace(becFolder)) return becFolder ?? string.Empty;

        string full = Path.GetFullPath(becFolder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string name = Path.GetFileName(full);

        if (!name.EndsWith("_BEC", StringComparison.OrdinalIgnoreCase)) return full;

        string? parent = Directory.GetParent(full)?.FullName;
        if (string.IsNullOrEmpty(parent)) return full;

        string stem = name[..^4];
        return Directory.Exists(Path.Combine(parent, stem + "_ISO")) ? parent : full;
    }

    /// <summary>
    /// The <c>*_BEC</c> children of a folder, or an empty array when the folder is missing or unreadable.
    /// </summary>
    private static string[] EnumerateBecChildren(string folder)
    {
        if (!Directory.Exists(folder)) return Array.Empty<string>();

        try
        {
            return Directory.EnumerateDirectories(folder, "*_BEC").Take(2).ToArray();
        }
        catch (DirectoryNotFoundException)
        {
            return Array.Empty<string>();
        }
        catch (UnauthorizedAccessException)
        {
            return Array.Empty<string>();
        }
        catch (IOException)
        {
            return Array.Empty<string>();
        }
    }

    /// <summary>True when <paramref name="path"/> looks like a BEC folder (has <c>data/config</c>).</summary>
    public static bool IsBecFolder(string path) =>
        !string.IsNullOrWhiteSpace(path)
        && Directory.Exists(Path.Combine(path, "data", "config"));
}
