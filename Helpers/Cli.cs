namespace ModdingGUI;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

/// <summary>Which pipeline the command line asked for.</summary>
public enum CliMode
{
    /// <summary>Project folder → ISO.</summary>
    Pack,

    /// <summary>ISO → project folder.</summary>
    Unpack,
}

/// <summary>
/// A parsed command line invocation of the non-GUI entry points.
/// </summary>
/// <remarks>
/// The GUI packs and unpacks by writing the batch produced by
/// <see cref="BatchContentBuilder"/> and running it. The command line does exactly the
/// same thing through the same builder, so automation cannot drift from what users do.
/// </remarks>
public sealed class CliInvocation
{
    /// <summary>Requested pipeline.</summary>
    public CliMode Mode { get; init; }

    /// <summary>Absolute project folder for <see cref="CliMode.Pack"/>.</summary>
    public string? ProjectFolder { get; init; }

    /// <summary>Source ISO for <see cref="CliMode.Unpack"/>.</summary>
    public string? IsoPath { get; init; }

    /// <summary>New project name for <see cref="CliMode.Unpack"/>.</summary>
    public string? ProjectName { get; init; }

    /// <summary>Directory containing the Python toolbelt (<c>tools/</c>).</summary>
    public string? ToolsDirectory { get; init; }

    /// <summary>
    /// Python command to invoke, e.g. <c>python</c> or <c>py -3</c>. Defaults to
    /// <c>python</c>, matching the GUI's batch files.
    /// </summary>
    public string? PythonCommand { get; init; }

    /// <summary>True for <c>--help</c>/<c>-h</c> style requests.</summary>
    public bool IsHelp { get; init; }

    /// <summary>Usage text shown for <c>--help</c> and bad arguments.</summary>
    public const string Usage = """
        ModdingGUI — command line pipelines (the GUI stays available when run with no arguments)

          ModdingGUI --pack <projectFolder> [options]
          ModdingGUI --unpack <isoPath> <projectName> [options]

        Options
          --tools <dir>    Folder holding the Python toolbelt (default: <app>/tools)
          --python <cmd>   Python command to run (default: python; use "py -3" for the launcher)
          --help           Show this text

        Both modes replay exactly the batch that the GUI generates
        (Helpers/BatchContentBuilder), so results match packing from the app.
        """;

    /// <summary>
    /// Parse command line arguments. Returns <c>null</c> when the arguments do not
    /// describe a CLI invocation (i.e. the app should start the GUI).
    /// </summary>
    public static CliInvocation? TryParse(string[] args, out string? error)
    {
        error = null;
        if (args is null || args.Length == 0) return null;

        string first = args[0];
        if (first is "--help" or "-h" or "/?") return new CliInvocation { IsHelp = true };

        CliMode mode;
        if (first == "--pack") mode = CliMode.Pack;
        else if (first == "--unpack") mode = CliMode.Unpack;
        else return null;   // not a CLI invocation → GUI

        var positional = new List<string>();
        string? tools = null;
        string? python = null;

        for (int i = 1; i < args.Length; i++)
        {
            string arg = args[i];
            switch (arg)
            {
                case "--tools":
                    if (i + 1 >= args.Length) { error = "--tools needs a directory."; return null; }
                    tools = args[++i];
                    break;
                case "--python":
                    if (i + 1 >= args.Length) { error = "--python needs a command."; return null; }
                    python = args[++i];
                    break;
                case "--help":
                case "-h":
                    return new CliInvocation { IsHelp = true };
                default:
                    if (arg.StartsWith("-", StringComparison.Ordinal))
                    {
                        error = $"unknown option '{arg}'.";
                        return null;
                    }
                    positional.Add(arg);
                    break;
            }
        }

        if (mode == CliMode.Pack)
        {
            if (positional.Count != 1)
            {
                error = "--pack needs exactly one project folder.";
                return null;
            }
            return new CliInvocation
            {
                Mode = mode,
                ProjectFolder = positional[0],
                ToolsDirectory = tools,
                PythonCommand = python,
            };
        }

        if (positional.Count != 2)
        {
            error = "--unpack needs an ISO path and a project name.";
            return null;
        }
        return new CliInvocation
        {
            Mode = mode,
            IsoPath = positional[0],
            ProjectName = positional[1],
            ToolsDirectory = tools,
            PythonCommand = python,
        };
    }

    /// <summary>
    /// Build the batch for this invocation, run it, and return its exit code.
    /// </summary>
    /// <param name="appDirectory">The application directory (tools default, working root).</param>
    public int Run(string appDirectory)
    {
        string tools = ToolsDirectory ?? Path.Combine(appDirectory, "tools");
        if (!Directory.Exists(tools))
        {
            Console.Error.WriteLine($"Toolbelt not found: {tools} (pass --tools <dir>).");
            return 2;
        }

        string workingRoot = Path.GetDirectoryName(tools.TrimEnd(
            Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) ?? appDirectory;
        string python = PythonCommand ?? "python";

        string script;
        string batchName;
        if (Mode == CliMode.Pack)
        {
            string project = Path.GetFullPath(ProjectFolder!);
            if (!Directory.Exists(project))
            {
                Console.Error.WriteLine($"Project folder not found: {project}");
                return 2;
            }
            script = BatchContentBuilder.GeneratePackContent(project, EnsureSeparator(workingRoot));
            batchName = "moddinggui-pack-" + Guid.NewGuid().ToString("N") + ".bat";
        }
        else
        {
            string iso = Path.GetFullPath(IsoPath!);
            if (!File.Exists(iso))
            {
                Console.Error.WriteLine($"ISO not found: {iso}");
                return 2;
            }
            script = BatchContentBuilder.GenerateUnpackContent(iso, ProjectName!, EnsureSeparator(workingRoot));
            batchName = "moddinggui-unpack-" + Guid.NewGuid().ToString("N") + ".bat";
        }

        script = ApplyPython(script, python);
        script = "@echo off\r\ncd /d \"" + workingRoot + "\"\r\n" + script + "exit /b %ERRORLEVEL%\r\n";

        string batchPath = Path.Combine(Path.GetTempPath(), batchName);
        File.WriteAllText(batchPath, script);

        try
        {
            Console.WriteLine($"[{Mode.ToString().ToLowerInvariant()}] {batchPath}");
            var psi = new ProcessStartInfo("cmd.exe", "/c \"" + batchPath + "\"")
            {
                UseShellExecute = false,
            };
            using var process = Process.Start(psi);
            if (process is null)
            {
                Console.Error.WriteLine("Failed to start cmd.exe.");
                return 2;
            }
            process.WaitForExit();
            return process.ExitCode;
        }
        finally
        {
            try { File.Delete(batchPath); } catch (IOException) { /* best effort */ }
        }
    }

    private static string EnsureSeparator(string path) =>
        path.EndsWith(Path.DirectorySeparatorChar) ? path : path + Path.DirectorySeparatorChar;

    /// <summary>Swap the batch's <c>python</c> prefix for the requested command.</summary>
    private static string ApplyPython(string batch, string python)
    {
        if (string.Equals(python, "python", StringComparison.OrdinalIgnoreCase)) return batch;

        return string.Join("\r\n", batch
            .Split(["\r\n"], StringSplitOptions.None)
            .Select(line => line.StartsWith("python ", StringComparison.OrdinalIgnoreCase)
                ? python + line["python".Length..]
                : line));
    }
}
