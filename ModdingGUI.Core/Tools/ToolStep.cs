namespace ModdingGUI.Core.Tools;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

/// <summary>
/// One external command in a build: an executable plus its arguments, exactly as the process will receive
/// them — unquoted, unescaped, unparsed.
/// </summary>
/// <remarks>
/// <para>
/// The build used to be a single text blob handed to <c>cmd.exe</c>, so every path had to survive batch
/// parsing. Parentheses broke it even when quoted, which is why the app refused paths containing them and why
/// <see cref="PathHelper.QuotePath"/> escapes <c>^&amp;&lt;&gt;|()%</c>. A <see cref="ToolStep"/> is passed to
/// <see cref="System.Diagnostics.ProcessStartInfo.ArgumentList"/>, which no shell parses, so all of those
/// characters are ordinary in a path (PLAN.md Phase 7.9).
/// </para>
/// <para>
/// The batch text did not disappear: <see cref="ToBatchLine"/> renders a step the way the old generator did,
/// so a user can still see or copy the command chain (the shell's "Save tool commands" option writes it to disk).
/// </para>
/// </remarks>
/// <param name="Name">Short label used in logs and results, e.g. <c>bec-tool-all.py</c>.</param>
/// <param name="Executable">Program to start: <c>python</c>, a tool exe, or an absolute path.</param>
/// <param name="Arguments">Arguments in order; no quoting needed and none wanted.</param>
/// <param name="WorkingDirectory">Directory the process runs in, or null for the current one.</param>
public sealed record ToolStep(
    string Name,
    string Executable,
    IReadOnlyList<string> Arguments,
    string? WorkingDirectory = null)
{
    /// <summary>
    /// Build a step that runs a toolbelt script with the configured interpreter.
    /// </summary>
    /// <param name="pythonCommand">Interpreter command line, e.g. <c>python</c> or <c>py -3</c>.</param>
    /// <param name="scriptPath">The script to run.</param>
    /// <param name="scriptArguments">The script's arguments, in order.</param>
    public static ToolStep Python(
        string pythonCommand, string scriptPath, params string[] scriptArguments)
        => PythonIn(null, pythonCommand, scriptPath, scriptArguments);

    /// <summary>As <see cref="Python(string,string,string[])"/>, but running in a given directory.</summary>
    public static ToolStep PythonIn(
        string? workingDirectory, string pythonCommand, string scriptPath, params string[] scriptArguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pythonCommand);
        ArgumentException.ThrowIfNullOrWhiteSpace(scriptPath);

        var (executable, interpreterArguments) = CommandLine.Split(pythonCommand);

        var arguments = new List<string>(interpreterArguments.Count + 1 + (scriptArguments?.Length ?? 0));
        arguments.AddRange(interpreterArguments);
        arguments.Add(scriptPath);
        if (scriptArguments is not null) arguments.AddRange(scriptArguments);

        return new ToolStep(Path.GetFileName(scriptPath), executable, arguments, workingDirectory);
    }

    /// <summary>
    /// Render the step as a batch line, the way the pre-7.9 generator did: switches stay bare, paths are quoted and
    /// batch-escaped. This is for inspection and reuse only — it is never executed (Phase 7.9).
    /// </summary>
    public string ToBatchLine()
    {
        var line = new StringBuilder(BatchToken(Executable));
        foreach (string argument in Arguments) line.Append(' ').Append(BatchToken(argument));
        return line.ToString();

        static string BatchToken(string value)
        {
            if (value.Length == 0) return "\"\"";
            // Paths are quoted (that is the shape the generator always produced and the tests pin); a bare switch or
            // word such as -unpack or GC needs nothing.
            if (value.IndexOfAny(NeedsQuoting) < 0) return value;
            return PathHelper.QuotePath(value);
        }
    }

    /// <summary>Characters whose presence means a batch token has to be quoted.</summary>
    private static readonly char[] NeedsQuoting =
        { ' ', '\t', '^', '&', '<', '>', '|', '(', ')', '%', '"', '/', '\\' };

    /// <summary>
    /// Render the step as it is actually started: quoting only where an argument contains a space, and no batch
    /// escapes — a caret before <c>(</c> would be a literal character to the process, so it must not be in the log.
    /// </summary>
    public string ToDisplayString()
    {
        var line = new StringBuilder(Quote(Executable));
        foreach (string argument in Arguments) line.Append(' ').Append(Quote(argument));
        return line.ToString();

        static string Quote(string value) =>
            value.Contains(' ') || value.Contains('"') ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
    }

    /// <summary>Readable form for logs and tests: <c>python script.py -unpack "a" "b"</c>.</summary>
    public override string ToString() => ToDisplayString();
}
