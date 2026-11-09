namespace ModdingGUI.Core.Tools;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using ModdingGUI.Core.Logging;

/// <summary>What to pack, and with which toolchain.</summary>
/// <param name="ProjectPath">Project folder (the one holding <c>_BEC</c> and <c>_ISO</c>).</param>
/// <param name="ToolsPath">Folder holding the Python toolbelt scripts and the script compiler.</param>
/// <param name="PythonCommand">Interpreter command line, e.g. <c>python</c> or <c>py -3</c>.</param>
public sealed record PackRequest(string ProjectPath, string ToolsPath, string PythonCommand = "python");

/// <summary>
/// Packs a project into an ISO without a WinForms control in the path.
/// </summary>
/// <remarks>
/// <para>
/// Phase 7.8 was blocked on exactly this: the shell owned the child processes, so pack output only ever reached
/// a RichTextBox and nothing outside the GUI could report a packing failure. The service runs the same chain the
/// app has always run (<see cref="BatchContentBuilder.BuildPackSteps"/>) and emits every line through
/// <see cref="IAppLog"/>, so the GUI, the CLI and (Phase 8) the MCP server observe one identical log.
/// </para>
/// <para>
/// Nothing here writes a batch file: the steps are started directly with
/// <see cref="System.Diagnostics.ProcessStartInfo.ArgumentList"/>, which is why a project under a path containing
/// parentheses now packs (PLAN.md Phase 7.9).
/// </para>
/// </remarks>
public sealed class PackService
{
    private readonly IAppLog? _log;
    private readonly string _scope;

    /// <summary>Create the service; pass a log to observe the run.</summary>
    public PackService(IAppLog? log = null, string scope = "pack")
    {
        _log = log;
        _scope = string.IsNullOrWhiteSpace(scope) ? "pack" : scope;
    }

    /// <summary>The pack chain this service runs, for inspection or a dry run.</summary>
    public static IReadOnlyList<ToolStep> Steps(PackRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return BatchContentBuilder.BuildPackSteps(
            request.ProjectPath, request.ToolsPath, request.PythonCommand);
    }

    /// <summary>Run the pack chain and wait for it.</summary>
    /// <param name="request">Project and toolchain.</param>
    /// <param name="cancellationToken">Kills the running step (and its tree) when signalled.</param>
    /// <returns>Every step that ran; <see cref="BuildResult.Succeeded"/> is the verdict.</returns>
    public BuildResult Pack(PackRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ProjectPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ToolsPath);

        string project = request.ProjectPath.TrimEnd(
            Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        if (!Directory.Exists(project))
        {
            return Fail($"project folder not found: {project}");
        }

        if (!Directory.Exists(request.ToolsPath))
        {
            return Fail($"toolbelt folder not found: {request.ToolsPath}");
        }

        _log?.Write(LogLevel.Info, $"Packing {Path.GetFileName(project)}…", _scope);

        BuildResult result;
        try
        {
            result = new ToolRunner(_log, _scope).Run(Steps(request), cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return Fail($"pack failed: {ex.Message}");
        }

        _log?.Write(
            result.Succeeded ? LogLevel.Success : LogLevel.Error,
            result.Succeeded
                ? $"Packed {Path.GetFileName(project)}."
                : $"Pack failed at '{result.FirstFailure?.Name ?? "unknown step"}' (exit {result.ExitCode}).",
            _scope);

        return result;
    }

    private BuildResult Fail(string message)
    {
        _log?.Write(LogLevel.Error, message, _scope);
        return new BuildResult(1, new[] { new BuildStepResult("pack", 1, new[] { message }) });
    }
}
