namespace ModdingGUI.Core.Tools;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using ModdingGUI.Core.Logging;

/// <summary>What to unpack, and where the project should land.</summary>
/// <param name="IsoPath">The ISO to unpack.</param>
/// <param name="ProjectName">Folder name of the project to create.</param>
/// <param name="ProjectsFolder">Folder the project is created in.</param>
/// <param name="ToolsPath">Folder holding the Python toolbelt scripts.</param>
/// <param name="PythonCommand">Interpreter command line, e.g. <c>python</c> or <c>py -3</c>.</param>
public sealed record UnpackRequest(
    string IsoPath,
    string ProjectName,
    string ProjectsFolder,
    string ToolsPath,
    string PythonCommand = "python",
    Toolchain? Toolchain = null);

/// <summary>
/// Unpacks an ISO into a project folder without a WinForms control in the path.
/// </summary>
/// <remarks>
/// The counterpart to <see cref="PackService"/>: the same chain the Unpack button runs, started directly rather
/// than through a generated <c>.bat</c>, with every child-process line emitted to <see cref="IAppLog"/>. The
/// project folder is created here, because a headless caller has no shell to create it first.
/// </remarks>
public sealed class UnpackService
{
    private readonly IAppLog? _log;
    private readonly string _scope;

    /// <summary>Create the service; pass a log to observe the run.</summary>
    public UnpackService(IAppLog? log = null, string scope = "unpack")
    {
        _log = log;
        _scope = string.IsNullOrWhiteSpace(scope) ? "unpack" : scope;
    }

    /// <summary>The unpack chain this service runs, for inspection or a dry run.</summary>
    public static IReadOnlyList<ToolStep> Steps(UnpackRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return BatchContentBuilder.BuildUnpackSteps(
            request.IsoPath, request.ProjectName, request.ProjectsFolder,
            request.Toolchain ?? Toolchain.PythonScripts(request.ToolsPath, request.PythonCommand));
    }

    /// <summary>The project folder an <see cref="UnpackRequest"/> describes.</summary>
    public static string ProjectFolderOf(UnpackRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Path.Combine(request.ProjectsFolder, request.ProjectName);
    }

    /// <summary>Run the unpack chain and wait for it.</summary>
    /// <param name="request">ISO, project name, destination and toolchain.</param>
    /// <param name="cancellationToken">Kills the running step (and its tree) when signalled.</param>
    /// <returns>Every step that ran; <see cref="BuildResult.Succeeded"/> is the verdict.</returns>
    public BuildResult Unpack(UnpackRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.IsoPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ProjectName);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ProjectsFolder);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ToolsPath);

        if (!File.Exists(request.IsoPath))
        {
            return Fail($"ISO not found: {request.IsoPath}");
        }

        if (!Directory.Exists(request.ToolsPath))
        {
            return Fail($"toolbelt folder not found: {request.ToolsPath}");
        }

        string projectFolder = ProjectFolderOf(request);
        try
        {
            Directory.CreateDirectory(projectFolder);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Fail($"could not create {projectFolder}: {ex.Message}");
        }

        _log?.Write(LogLevel.Info, $"Unpacking {Path.GetFileName(request.IsoPath)} into {projectFolder}…", _scope);

        BuildResult result;
        try
        {
            result = new ToolRunner(_log, _scope).Run(Steps(request), cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return Fail($"unpack failed: {ex.Message}");
        }

        // Record what the derived-binary steps were fed, so a pack can skip regenerating them while the text is untouched.
        if (result.Succeeded)
        {
            try
            {
                DerivedToolState.Capture(projectFolder, Path.Combine(projectFolder, $"{request.ProjectName}_BEC"));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _log?.Write(LogLevel.Warning, _scope, $"Could not record the derived-file baseline: {ex.Message}");
            }
        }

        _log?.Write(
            result.Succeeded ? LogLevel.Success : LogLevel.Error,
            result.Succeeded
                ? $"Unpacked into {projectFolder}."
                : $"Unpack failed at '{result.FirstFailure?.Name ?? "unknown step"}' (exit {result.ExitCode}).",
            _scope);

        return result;
    }

    private BuildResult Fail(string message)
    {
        _log?.Write(LogLevel.Error, message, _scope);
        return new BuildResult(1, new[] { new BuildStepResult("unpack", 1, new[] { message }) });
    }
}
