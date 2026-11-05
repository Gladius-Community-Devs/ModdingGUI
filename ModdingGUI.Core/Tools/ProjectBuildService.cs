namespace ModdingGUI.Core.Tools;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using ModdingGUI.Core.Logging;

/// <summary>Where a build finds the project and the toolchain.</summary>
/// <param name="ProjectPath">Project folder (the one holding <c>_BEC</c> and <c>_ISO</c>).</param>
/// <param name="ToolsPath">Folder holding the Python tools and the script compiler.</param>
/// <param name="PythonCommand">Interpreter used for the Python tools, e.g. <c>py -3</c>.</param>
public sealed record BuildOptions(string ProjectPath, string ToolsPath, string PythonCommand = "py -3");

/// <summary>One command that ran during a build.</summary>
/// <param name="Name">What the step does, for logs and the UI.</param>
/// <param name="ExitCode">Process exit code; 0 means success.</param>
/// <param name="Output">Captured lines, in order.</param>
public sealed record BuildStepResult(string Name, int ExitCode, IReadOnlyList<string> Output)
{
    /// <summary>True when the step succeeded.</summary>
    public bool Succeeded => ExitCode == 0;
}

/// <summary>The result of a whole build.</summary>
/// <param name="ExitCode">First non-zero step exit code, or 0.</param>
/// <param name="Steps">Every step that ran, in order.</param>
public sealed record BuildResult(int ExitCode, IReadOnlyList<BuildStepResult> Steps)
{
    /// <summary>True when every step succeeded.</summary>
    public bool Succeeded => ExitCode == 0;
}

/// <summary>
/// Runs the project's build pipeline from Core: the script compiler and the pack chain.
/// </summary>
/// <remarks>
/// <para>
/// The pipeline is the one the GUI has always used — <see cref="BatchContentBuilder"/> generates the batch,
/// which then runs the Python tools through <c>cmd</c> — so packing from here produces exactly what packing
/// from the app produces. Moving it into Core is what lets a randomized project be built by anything, including
/// the MCP server, instead of only by the WinForms shell.
/// </para>
/// <para>
/// Output is streamed to <see cref="IAppLog"/> as it arrives, because a pack takes minutes and a silent gap
/// looks like a hang.
/// </para>
/// </remarks>
public sealed class ProjectBuildService
{
    private readonly IAppLog? _log;
    private readonly string _scope;

    /// <summary>Create the service; pass a log to observe the build.</summary>
    public ProjectBuildService(IAppLog? log = null, string scope = "build")
    {
        _log = log;
        _scope = scope;
    }

    /// <summary>Compile the given <c>.scp</c> scripts with the project's script compiler.</summary>
    public BuildResult CompileScripts(BuildOptions options, IEnumerable<string> scriptNames)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(scriptNames);

        var scripts = scriptNames.Where(name => !string.IsNullOrWhiteSpace(name)).ToList();
        if (scripts.Count == 0) return new BuildResult(0, Array.Empty<BuildStepResult>());

        string compiler = Path.Combine(options.ToolsPath, "DOGCodeCompiler.exe");
        if (!File.Exists(compiler))
        {
            string message = $"script compiler not found at {compiler}";
            _log?.Write(LogLevel.Error, message, _scope);
            return new BuildResult(1, new[] { new BuildStepResult("compile scripts", 1, new[] { message }) });
        }

        string scriptFolder = Path.Combine(ResolveBec(options.ProjectPath), "data", "script");
        string batch = BuildCompileBatch(options, scripts);

        int exitCode = RunBatchContent(batch, scriptFolder, _log, _scope);
        return new BuildResult(
            exitCode,
            new[] { new BuildStepResult("compile scripts", exitCode, Array.Empty<string>()) });
    }

    /// <summary>
    /// The batch that compiles the given scripts. Separated from execution so the command line can be asserted
    /// without launching the compiler.
    /// </summary>
    internal static string BuildCompileBatch(BuildOptions options, IReadOnlyList<string> scripts)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(scripts);

        string compiler = Path.Combine(options.ToolsPath, "DOGCodeCompiler.exe");
        string scriptFolder = Path.Combine(ResolveBec(options.ProjectPath), "data", "script");

        var builder = new StringBuilder("@echo off" + Environment.NewLine);
        foreach (string script in scripts)
        {
            // The compiler takes an input .scp and an output .scb in data/script/bin, which is what the game
            // loads; the original randomizer passed both paths and so must this.
            string source = Path.Combine(scriptFolder, script);
            string binary = Path.Combine(scriptFolder, "bin",
                Path.GetFileNameWithoutExtension(script) + ".scb");

            builder.Append('"').Append(compiler).Append('"')
                   .Append(' ').Append('"').Append(source).Append('"')
                   .Append(' ').Append('"').Append(binary).Append('"')
                   .Append(Environment.NewLine);
        }

        builder.Append("exit /b %ERRORLEVEL%").Append(Environment.NewLine);
        return builder.ToString();
    }

    /// <summary>Run the pack chain: units repack, strings, bec and ISO.</summary>
    public BuildResult Pack(BuildOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        string project = options.ProjectPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string workingRoot = Path.GetDirectoryName(project) ?? project;

        string content = BatchContentBuilder.GeneratePackContent(project, EnsureTrailingSeparator(workingRoot));
        string name = $"pack {Path.GetFileName(project)}";

        int exitCode = RunBatchContent(content, workingRoot, _log, _scope);
        return new BuildResult(exitCode, new[] { new BuildStepResult(name, exitCode, Array.Empty<string>()) });
    }

    /// <summary>
    /// Write a batch file and run it through <c>cmd</c>, streaming stdout and stderr into the log.
    /// </summary>
    /// <returns>The process exit code.</returns>
    public static int RunBatchContent(string content, string workingDirectory, IAppLog? log = null,
        string scope = "build")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        string directory = Directory.Exists(workingDirectory) ? workingDirectory : Path.GetTempPath();
        string batchPath = Path.Combine(Path.GetTempPath(), $"gladius-build-{Guid.NewGuid():N}.bat");

        try
        {
            File.WriteAllText(batchPath, content, Encoding.Default);

            var startInfo = new ProcessStartInfo("cmd.exe", "/c \"" + batchPath + "\"")
            {
                WorkingDirectory = directory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                log?.Write(LogLevel.Error, "could not start cmd.exe", scope);
                return 1;
            }

            process.OutputDataReceived += (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data)) log?.Write(LogLevel.Info, e.Data, scope);
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data)) log?.Write(LogLevel.Warning, e.Data, scope);
            };

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();
            return process.ExitCode;
        }
        finally
        {
            try { if (File.Exists(batchPath)) File.Delete(batchPath); }
            catch (IOException) { /* the batch is in temp; a stale one is harmless */ }
        }
    }

    private static string ResolveBec(string projectPath) => ProjectRoots.ResolveBecFolder(projectPath);

    private static string EnsureTrailingSeparator(string path) =>
        path.EndsWith(Path.DirectorySeparatorChar) ? path : path + Path.DirectorySeparatorChar;
}
