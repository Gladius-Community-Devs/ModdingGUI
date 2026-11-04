namespace ModdingGUI.Core;

using System;
using System.IO;

/// <summary>
/// Resolves the game's path macros to real files inside a project.
/// </summary>
/// <remarks>
/// Tok files spell paths with macros instead of plain relative paths:
/// <c>$d\encounters\…</c> is <c>data\encounters\…</c> and <c>$l\Nordagh\thefen\…</c> is
/// <c>data\levels\Nordagh\thefen\…</c>. Anything unrecognised resolves to null rather than to a
/// wrong file, so callers can report "does not resolve" instead of silently reading something else.
/// </remarks>
public static class GamePaths
{
    /// <summary>
    /// Turn a game path such as <c>"$d\encounters\Nordagh\TheFen\arena.grd"</c> into a full path
    /// inside <paramref name="projectPath"/>, or null when it cannot be resolved.
    /// </summary>
    public static string? ResolveInProject(string projectPath, string? gamePath)
    {
        string? relative = ToProjectRelative(gamePath);
        return relative is null
            ? null
            : Path.Combine(projectPath, relative);
    }

    /// <summary>
    /// Turn a game path into a project-relative path (backslashes, no leading separator), or null
    /// when the macro is unknown.
    /// </summary>
    public static string? ToProjectRelative(string? gamePath)
    {
        if (string.IsNullOrWhiteSpace(gamePath)) return null;

        string path = gamePath.Trim().Trim('"').Replace('/', '\\').TrimStart('\\');

        if (path.StartsWith("$d\\", StringComparison.OrdinalIgnoreCase))
            return "data\\" + path[3..];
        if (path.StartsWith("$l\\", StringComparison.OrdinalIgnoreCase))
            return "data\\levels\\" + path[3..];
        if (path.StartsWith("$", StringComparison.Ordinal))
            return null;   // unknown macro: do not guess
        if (path.StartsWith("data\\", StringComparison.OrdinalIgnoreCase))
            return path;
        if (path.Length > 0 && char.IsLetter(path[0]) && path.Length > 1 && path[1] == ':')
            return null;   // absolute host path: not ours to resolve

        return "data\\" + path;
    }

    /// <summary>True when <paramref name="gamePath"/> resolves to an existing file.</summary>
    public static bool Exists(string projectPath, string? gamePath)
    {
        string? full = ResolveInProject(projectPath, gamePath);
        return full is not null && File.Exists(full);
    }
}
