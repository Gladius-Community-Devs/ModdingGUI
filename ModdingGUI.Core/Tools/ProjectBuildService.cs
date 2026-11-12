namespace ModdingGUI.Core.Tools;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using ModdingGUI.Core.Logging;

/// <summary>Where a build finds the project and the toolchain.</summary>
/// <param name="ProjectPath">Project folder (the one holding <c>_BEC</c> and <c>_ISO</c>).</param>
/// <param name="ToolsPath">Folder holding the Python tools and the script compiler.</param>
/// <param name="PythonCommand">Interpreter used for the Python tools, e.g. <c>py -3</c>.</param>
public sealed record BuildOptions(string ProjectPath, string ToolsPath, string PythonCommand = "py -3", Toolchain? Toolchain = null);

/// <summary>
/// Runs the project's build pipeline from Core: the script compiler and the pack chain.
/// </summary>
/// <remarks>
/// <para>
/// The pipeline is the one the app has always run — script compile through <c>DOGCodeCompiler.exe</c>, then the
/// pack chain — so packing from here produces exactly what packing from the GUI produces. Moving it into Core is
/// what lets a randomized project be built by anything, including the MCP server, instead of only by the WinForms
/// shell.
/// </para>
/// <para>
/// Since Phase 7.9 no batch file is written or run: <see cref="ToolRunner"/> starts each tool directly with
/// <see cref="System.Diagnostics.ProcessStartInfo.ArgumentList"/>. Output still streams to <see cref="IAppLog"/> as
/// it arrives, because a pack takes minutes and a silent gap looks like a hang.
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
    /// <param name="options">Project and toolchain.</param>
    /// <param name="scriptNames">Script file names inside <c>data/script</c>.</param>
    /// <param name="cancellationToken">Stops the running compiler when signalled.</param>
    public BuildResult CompileScripts(
        BuildOptions options, IEnumerable<string> scriptNames, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(scriptNames);

        var scripts = scriptNames.Where(name => !string.IsNullOrWhiteSpace(name)).ToList();
        if (scripts.Count == 0) return BuildResult.Empty;

        var steps = BuildCompileSteps(options, scripts);

        if (!File.Exists(steps[0].Executable))
        {
            string message = $"script compiler not found at {steps[0].Executable}";
            _log?.Write(LogLevel.Error, message, _scope);
            return new BuildResult(1, new[] { new BuildStepResult("compile scripts", 1, new[] { message }) });
        }

        return new ToolRunner(_log, _scope).Run(steps, cancellationToken);
    }

    /// <summary>Compile one script, for the incremental recompile loop.</summary>
    /// <param name="options">Project and toolchain.</param>
    /// <param name="scriptName">Script file name inside <c>data/script</c>.</param>
    /// <param name="cancellationToken">Stops the running compiler when signalled.</param>
    public BuildStepResult CompileScript(
        BuildOptions options, string scriptName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(scriptName);

        ToolStep step = BuildCompileSteps(options, new[] { scriptName })[0];
        if (!File.Exists(step.Executable))
        {
            string message = $"script compiler not found at {step.Executable}";
            _log?.Write(LogLevel.Error, message, _scope);
            return new BuildStepResult(step.Name, 1, new[] { message });
        }

        return new ToolRunner(_log, _scope).RunStep(step, cancellationToken);
    }

    /// <summary>
    /// The steps that compile the given scripts. Separated from execution so the command lines can be asserted
    /// without launching the compiler.
    /// </summary>
    internal static IReadOnlyList<ToolStep> BuildCompileSteps(
        BuildOptions options, IReadOnlyList<string> scripts)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(scripts);

        string scriptFolder = Path.Combine(ResolveBec(options.ProjectPath), "data", "script");

        // The bundled toolchain compiles scripts itself, so the app no longer needs the proprietary DOGCodeCompiler.exe:
        // tools_v009.exe scbc is a reimplementation that matches it byte-for-byte on every vanilla script (694 of 694,
        // scripts/check-script-compiler.py). A user who owns the original can still drop it in tools/ and be served by it.
        Toolchain toolchain = options.Toolchain
            ?? Toolchain.Choose(AppContext.BaseDirectory, options.ToolsPath, options.PythonCommand);
        (string executable, string[] leadingArguments) = ScriptCompilerFor(toolchain, options.ToolsPath);

        var steps = new List<ToolStep>(scripts.Count);
        foreach (string script in scripts)
        {
            // The compiler takes an input .scp and an output .scb in data/script/bin, which is what the game
            // loads; the original randomizer passed both paths and so must this.
            string source = Path.Combine(scriptFolder, script);
            string binary = Path.Combine(scriptFolder, "bin",
                Path.GetFileNameWithoutExtension(script) + ".scb");

            var arguments = new List<string>(leadingArguments.Length + 2);
            arguments.AddRange(leadingArguments);
            arguments.Add(source);
            arguments.Add(binary);

            steps.Add(new ToolStep(script, executable, arguments, scriptFolder));
        }

        return steps;
    }

    /// <summary>
    /// Which program compiles a script: the bundled exe's <c>scbc</c> subcommand, or a user-supplied DOG compiler.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The DOG compiler cannot be redistributed, so it is never bundled - but if the user already has it in the toolbelt it
    /// is used, because their copy is the closest thing to the retail build. Everything else goes through
    /// <c>tools_v009.exe scbc</c>, which was validated byte-for-byte against that compiler over the whole vanilla corpus.
    /// </para>
    /// <para>
    /// The first element is the executable, the rest are arguments that precede the source/output pair.
    /// </para>
    /// </remarks>
    internal static (string Executable, string[] LeadingArguments) ScriptCompilerFor(
        Toolchain toolchain, string toolsPath)
    {
        ArgumentNullException.ThrowIfNull(toolchain);

        // A copy the user already owns wins: it is the closest thing to the retail build, and using it costs nothing.
        string dog = Path.Combine(toolsPath, "DOGCodeCompiler.exe");
        if (File.Exists(dog)) return (dog, Array.Empty<string>());

        if (!string.IsNullOrEmpty(toolchain.ExecutablePath) && File.Exists(toolchain.ExecutablePath))
        {
            return (toolchain.ExecutablePath!, new[] { "scbc" });
        }

        // Nothing to run: hand back the DOG path so the caller's "compiler not found" message names what is missing.
        return (dog, Array.Empty<string>());
    }

    /// <summary>
    /// The compile chain as batch text, for inspection only. Nothing executes this; it exists so the command
    /// chain can be shown or copied, and so the pre-7.9 behaviour stays assertable.
    /// </summary>
    internal static string BuildCompileBatch(BuildOptions options, IReadOnlyList<string> scripts)
    {
        var builder = new StringBuilder("@echo off" + Environment.NewLine);
        foreach (ToolStep step in BuildCompileSteps(options, scripts))
        {
            builder.Append(step.ToBatchLine()).Append(Environment.NewLine);
        }

        builder.Append("exit /b %ERRORLEVEL%").Append(Environment.NewLine);
        return builder.ToString();
    }

    /// <summary>Run the pack chain: units repack, strings, bec and ISO.</summary>
    /// <param name="options">Project and toolchain.</param>
    /// <param name="cancellationToken">Stops the running step when signalled.</param>
    public BuildResult Pack(BuildOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        return new PackService(_log, _scope).Pack(
            new PackRequest(options.ProjectPath, options.ToolsPath, options.PythonCommand),
            cancellationToken);
    }

    private static string ResolveBec(string projectPath) => ProjectRoots.ResolveBecFolder(projectPath);
}
