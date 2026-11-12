namespace ModdingGUI.Mcp.Tools;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ModdingGUI.Core.Jobs;
using ModdingGUI.Core.Logging;
using ModdingGUI.Core.Tools;
using ModdingGUI.Core.Validation;
using ModdingGUI.Mcp.Server;

/// <summary>
/// The project itself: what it is, whether it would pack, and the two long jobs.
/// </summary>
/// <remarks>
/// <para>
/// <c>project_pack</c> and <c>project_unpack</c> run through <see cref="JobRegistry"/>, the same object the GUI
/// and the UI driver use, so the answer to "is it done?" is data rather than a guessed timeout. Progress is
/// forwarded as MCP progress notifications: one when the job starts, one per log line (capped, because a pack
/// can print hundreds), and one when it finishes. A client that sends <c>notifications/cancelled</c> cancels the
/// request's token, which is registered straight to <see cref="BuildJob.Cancel"/> — the tool runner kills the
/// child process tree and the tool still returns the (cancelled) report.
/// </para>
/// <para>
/// The job result is the same <see cref="BuildResult"/> the GUI shows: per-step exit codes, captured output and
/// durations, flattened to seconds because a client reads JSON, not <see cref="TimeSpan"/>.
/// </para>
/// </remarks>
[McpServerToolType]
public sealed class ProjectTools(McpProject project)
{
    /// <summary>How many log lines are forwarded as progress before the stream is summarised.</summary>
    private const int ProgressLineBudget = 200;

    /// <summary>What this server is bound to.</summary>
    [McpServerTool(Name = "project_info", ReadOnly = true, Idempotent = true)]
    [Description(
        "Report which project this server edits (project folder, BEC folder), where its app and toolbelt live, " +
        "which toolchain a build would use, and where the log is written. Call this first in a session.")]
    public object Info() =>
        ToolResult.Call(project, "project_info", () =>
        {
            Toolchain toolchain = project.Toolchain;
            return new ProjectInfo(
                project.ProjectPath,
                project.BecPath,
                project.AppDirectory,
                project.ToolsPath,
                project.PythonCommand,
                toolchain.Mode.ToString(),
                toolchain.RequiresPython,
                toolchain.ExecutablePath,
                Directory.Exists(project.ToolsPath),
                project.Log.SinkPath,
                Environment.ProcessId);
        });

    /// <summary>Would this project pack?</summary>
    [McpServerTool(Name = "project_validate", ReadOnly = true, Idempotent = true)]
    [Description(
        "Run every file validator the pack step relies on — gladiators, skillsets, itemsets, statsets, tints, school " +
        "names and every data/school roster — and return one list of findings plus counts. Advisory: packing is " +
        "never blocked on a finding, and a project unpacked from a modded ISO is expected to differ from vanilla. " +
        "No check compares against a vanilla count.")]
    public object Validate(
        [Description("Include the data/school rosters (default true; they are what the game actually loads).")]
        bool includeRosters = true,
        [Description("Stop collecting one area after this many findings, so one bad file cannot flood the reply. Default 40.")]
        int maxFindingsPerArea = 40) =>
        ToolResult.Call(project, "project_validate", () =>
        {
            ValidationReport report = ProjectValidator.Validate(
                project.BecPath,
                new ValidationOptions
                {
                    IncludeRosters = includeRosters,
                    MaxFindingsPerArea = Math.Clamp(maxFindingsPerArea, 1, 1000),
                },
                project.Log);

            project.Log.Write(LogLevel.Info, $"project_validate: {report.Summary}", "mcp");
            return new ValidationReportDto(
                report.Findings.Select(ValidationIssue.From).ToArray(),
                report.Findings.Count,
                report.WarningCount,
                report.ErrorCount,
                report.CountByArea);
        });

    /// <summary>Pack the project into an ISO.</summary>
    [McpServerTool(Name = "project_pack")]
    [Description(
        "Pack the project into an ISO, running the app's own toolchain as a Core job, and return the job's " +
        "BuildResult (state, exit code, every step with its output). This is the same pipeline as the GUI's Pack " +
        "button and `ModdingGUI --pack`, so no batch file is written and no step is skipped. It can take minutes: " +
        "pass a progress token to follow it, and cancel the request to stop it. Use project_validate first to see " +
        "what would go wrong; packing is not blocked on validation findings.")]
    public async Task<object> Pack(
        RequestContext<CallToolRequestParams> context,
        CancellationToken cancellationToken)
    {
        BuildJob job = project.Jobs.Start(
            "pack",
            project.ProjectPath,
            token => new PackService(project.Log, "pack").Pack(
                new PackRequest(project.ProjectPath, project.ToolsPath, project.PythonCommand, project.Toolchain),
                token),
            project.Log);

        await FollowAsync(job, context, cancellationToken);

        // BatchContentBuilder writes <project>/<project folder name>.iso. Report it only when it is really there,
        // so the answer cannot name a file the run did not produce.
        string iso = Path.Combine(project.ProjectPath, Path.GetFileName(project.ProjectPath) + ".iso");
        return BuildReport.From(job, File.Exists(iso) ? iso : null);
    }

    /// <summary>Unpack an ISO into a project.</summary>
    [McpServerTool(Name = "project_unpack")]
    [Description(
        "Unpack an ISO into a new project folder, running the app's own toolchain as a Core job, and return the " +
        "job's BuildResult. Unpacking does not change which project this server edits: the next tool call still " +
        "acts on project_info's project. Pass a progress token to follow it, and cancel the request to stop it.")]
    public async Task<object> Unpack(
        RequestContext<CallToolRequestParams> context,
        CancellationToken cancellationToken,
        [Description("Path to the .iso to unpack.")] string iso,
        [Description("Folder name of the project to create. Default: this project's folder name.")] string? projectName = null,
        [Description("Folder the project is created in. Default: this project's parent folder.")] string? projectsFolder = null)
    {
        string name = string.IsNullOrWhiteSpace(projectName)
            ? Path.GetFileName(project.ProjectPath)
            : projectName!.Trim();

        string folder = string.IsNullOrWhiteSpace(projectsFolder)
            ? Path.GetDirectoryName(project.ProjectPath) ?? project.ProjectPath
            : Path.GetFullPath(projectsFolder!);

        var request = new UnpackRequest(
            Path.GetFullPath(iso),
            name,
            folder,
            project.ToolsPath,
            project.PythonCommand,
            project.Toolchain);

        BuildJob job = project.Jobs.Start(
            "unpack",
            UnpackService.ProjectFolderOf(request),
            token => new UnpackService(project.Log, "unpack").Unpack(request, token),
            project.Log);

        await FollowAsync(job, context, cancellationToken);
        return BuildReport.From(job, UnpackService.ProjectFolderOf(request));
    }

    /// <summary>
    /// Wait for a job while forwarding its life and its log as progress notifications.
    /// </summary>
    private async Task FollowAsync(
        BuildJob job,
        RequestContext<CallToolRequestParams>? context,
        CancellationToken cancellationToken)
    {
        ProgressToken? token = context?.Params?.ProgressToken;
        McpServer? server = context?.Server;
        var budget = new ProgressBudget(ProgressLineBudget);

        EventHandler<LogEntry>? relay = null;
        if (token is { } progressToken && server is not null)
        {
            await NotifyAsync(server, progressToken, 0, $"{job.Kind} started ({job.Id})");

            relay = (_, entry) =>
            {
                if (!budget.TryTake()) return;

                // Fire-and-forget on purpose: the log write must never wait on the transport.
                _ = NotifyAsync(server, progressToken, budget.Used, entry.Message);
            };

            project.Log.Written += relay;
        }

        // notifications/cancelled cancels the request's token; that is the whole mapping. Core's tool runner
        // kills the child process tree, the job settles as Cancelled, and the caller still gets the report.
        using CancellationTokenRegistration registration = cancellationToken.Register(job.Cancel);

        try
        {
            // Deliberately waits without the token: cancelling must stop the work and still produce a result,
            // not abandon the request with no answer.
            await job.Completion.ConfigureAwait(false);
        }
        finally
        {
            if (relay is not null) project.Log.Written -= relay;
        }

        if (token is { } finishedToken && server is not null)
        {
            string message = $"{job.Kind} finished ({job.Id}): {job.State} (exit {job.Result?.ExitCode}) after " +
                             $"{job.Elapsed.TotalSeconds:0.0}s" +
                             (budget.Used >= budget.Capacity ? "; progress stream capped — use logs_tail" : string.Empty);

            await NotifyAsync(server, finishedToken, budget.Used + 1, message);
        }
    }

    private static async Task NotifyAsync(McpServer server, ProgressToken token, int progress, string message)
    {
        try
        {
            await server.NotifyProgressAsync(
                token,
                new ProgressNotificationValue { Progress = progress, Message = message },
                options: null,
                cancellationToken: CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Progress is advisory: a client that stopped listening must not fail the operation.
        }
    }

    /// <summary>A small cap so one chatty pack cannot turn into a thousand notifications.</summary>
    private sealed class ProgressBudget(int capacity)
    {
        private int _used;

        public int Capacity { get; } = capacity;

        public int Used => _used;

        public bool TryTake() => Interlocked.Increment(ref _used) <= Capacity;
    }
}

/// <summary>What <c>project_info</c> answers.</summary>
/// <param name="ProjectPath">Project folder (the one holding <c>_BEC</c> and <c>_ISO</c>).</param>
/// <param name="BecPath">BEC folder every API reads and writes.</param>
/// <param name="AppDirectory">Folder the server executable lives in.</param>
/// <param name="ToolsPath">Toolbelt folder a build would use.</param>
/// <param name="PythonCommand">Interpreter command for the script toolchain.</param>
/// <param name="ToolchainMode">Which program a build would run: BundledExecutable or PythonScripts.</param>
/// <param name="RequiresPython">True when the chosen toolchain needs an interpreter on PATH.</param>
/// <param name="BundledToolExe">Path of the bundled tool exe when it exists.</param>
/// <param name="ToolsFolderExists">Whether the configured toolbelt folder exists.</param>
/// <param name="LogSinkPath">Where log lines are appended on disk, or null when the sink is unavailable.</param>
/// <param name="ProcessId">This server's process id, for correlating with the log file.</param>
public sealed record ProjectInfo(
    string ProjectPath,
    string BecPath,
    string AppDirectory,
    string ToolsPath,
    string PythonCommand,
    string ToolchainMode,
    bool RequiresPython,
    string? BundledToolExe,
    bool ToolsFolderExists,
    string? LogSinkPath,
    int ProcessId);

/// <summary>What <c>project_validate</c> answers.</summary>
/// <param name="Findings">Every finding, warnings first.</param>
/// <param name="Total">How many were reported.</param>
/// <param name="Warnings">How many are warnings.</param>
/// <param name="Errors">How many are errors (no current check produces one).</param>
/// <param name="ByArea">Findings per area, in the order the areas were checked.</param>
public sealed record ValidationReportDto(
    IReadOnlyList<ValidationIssue> Findings,
    int Total,
    int Warnings,
    int Errors,
    IReadOnlyDictionary<string, int> ByArea);
