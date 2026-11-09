namespace ModdingGUI.Core.Tools;

using System;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// Splits a configured interpreter command into an executable and its own arguments.
/// </summary>
/// <remarks>
/// The toolchain is configured as a command line — <c>python</c>, <c>py -3</c>, or a quoted path such as
/// <c>"C:\Program Files\Python312\python.exe" -3</c> — because that is what a user types. Before Phase 7.9
/// it was pasted into a batch file, so the shell did the splitting. Now the split is ours, and it has to
/// honour quotes so an interpreter under <c>Program Files</c> works.
/// </remarks>
public static class CommandLine
{
    /// <summary>Split a command line into the executable and the arguments that precede the script.</summary>
    /// <param name="commandLine">For example <c>py -3</c>.</param>
    /// <returns>The executable, plus any arguments it was given (both trimmed of quotes).</returns>
    public static (string Executable, IReadOnlyList<string> Arguments) Split(string commandLine)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandLine);

        var tokens = Tokenise(commandLine);
        if (tokens.Count == 0) throw new ArgumentException("The command line is empty.", nameof(commandLine));

        string executable = tokens[0];
        var arguments = new List<string>(Math.Max(0, tokens.Count - 1));
        for (int i = 1; i < tokens.Count; i++) arguments.Add(tokens[i]);

        return (executable, arguments);
    }

    /// <summary>
    /// Split on whitespace, keeping quoted runs together. Quotes are removed; an unterminated quote runs to
    /// the end of the string, which is what a user means by it.
    /// </summary>
    public static IReadOnlyList<string> Tokenise(string? text)
    {
        var tokens = new List<string>();
        if (string.IsNullOrWhiteSpace(text)) return tokens;

        var current = new StringBuilder();
        bool inQuotes = false;
        bool hasToken = false;

        foreach (char c in text)
        {
            if (c == '"')
            {
                inQuotes = !inQuotes;
                hasToken = true;   // "" is an empty token, not nothing
                continue;
            }

            if (!inQuotes && char.IsWhiteSpace(c))
            {
                if (hasToken)
                {
                    tokens.Add(current.ToString());
                    current.Clear();
                    hasToken = false;
                }
                continue;
            }

            current.Append(c);
            hasToken = true;
        }

        if (hasToken) tokens.Add(current.ToString());
        return tokens;
    }
}
