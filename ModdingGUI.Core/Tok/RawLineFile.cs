namespace ModdingGUI.Core.Tok;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

/// <summary>
/// Reads and writes a line-oriented tok file while preserving everything the caller does
/// not touch: unknown lines, comments, indentation and the exact number of trailing
/// newlines.
/// </summary>
/// <remarks>
/// Line-oriented editors (skills, classdefs) share this behaviour, so it lives here
/// instead of being copied per API. The canonical encoding is ISO-8859-1, which is
/// bijective over all 256 byte values, and writes normalise to CRLF like the corpus.
/// </remarks>
internal static class RawLineFile
{
    internal static string GetPath(string projectPath, string relativePath) =>
        Path.Combine(projectPath, relativePath.Replace('/', Path.DirectorySeparatorChar));

    /// <summary>
    /// Read the file as individual lines plus the count of trailing newlines.
    /// </summary>
    internal static (List<string> Lines, int TrailingNewlines) Read(
        string projectPath,
        string relativePath,
        string displayName)
    {
        string path = GetPath(projectPath, relativePath);
        if (!File.Exists(path))
            throw new FileNotFoundException($"{displayName} not found.", path);

        string text = File.ReadAllText(path, TokDocument.CanonicalEncoding);
        string normalised = text.Replace("\r\n", "\n").Replace("\r", "\n");

        int trailingNewlines = 0;
        for (int i = normalised.Length - 1; i >= 0 && normalised[i] == '\n'; i--)
            trailingNewlines++;

        string body = normalised.Substring(0, normalised.Length - trailingNewlines);
        var lines = body.Length == 0 ? new List<string>() : new List<string>(body.Split('\n'));
        return (lines, trailingNewlines);
    }

    /// <summary>
    /// Back the file up into <c>.mod-history/</c> and write it back with CRLF endings.
    /// </summary>
    internal static void Write(
        string projectPath,
        string relativePath,
        List<string> lines,
        int trailingNewlines)
    {
        string path = GetPath(projectPath, relativePath);
        ChangeHistoryService.Backup(projectPath, path);

        var text = new StringBuilder(string.Join("\r\n", lines));
        for (int i = 0; i < trailingNewlines; i++) text.Append("\r\n");
        File.WriteAllText(path, text.ToString(), TokDocument.CanonicalEncoding);
    }
}
