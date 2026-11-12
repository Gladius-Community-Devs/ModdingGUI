namespace ModdingGUI.Mcp.Server;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using ModdingGUI.Core;
using ModdingGUI.Core.Api;
using ModdingGUI.Core.History;
using ModdingGUI.Core.Jobs;
using ModdingGUI.Core.Logging;
using ModdingGUI.Core.Tools;
using ModdingGUI.Core.Units;

/// <summary>Thrown for a bad invocation; the host prints it to stderr and exits non-zero.</summary>
public sealed class McpStartupException : Exception
{
    /// <summary>Create the exception with a message the user can act on.</summary>
    public McpStartupException(string message) : base(message)
    {
    }
}

/// <summary>
/// The process-wide state every tool shares: the one project this server edits, the Core API instances,
/// the app log, the job registry, and the write gate that makes strict mode's apply-then-verify atomic.
/// </summary>
/// <remarks>
/// <para>
/// The project is resolved once, at startup, from <c>--project</c> (or the working directory). Tools take
/// no project path: one server process edits one project. That is what the GUI does too — it holds one
/// selected project and every button acts on it — and it means a tool call never has to guess which
/// project a relative path belongs to.
/// </para>
/// <para>
/// Writes are serialised process-wide by <see cref="_writeGate"/>. The Core APIs take
/// <see cref="ProjectLock"/> themselves, so this is not about file safety: it is what lets a
/// <c>strict</c> write validate the result and, when the change introduced a warning, restore the backup
/// the write just made without another caller's edit slipping in between.
/// </para>
/// </remarks>
public sealed class McpProject
{
    /// <summary>Text handed to the client in the <c>initialize</c> result.</summary>
    public const string Instructions =
        "Tools for editing one Gladius mod project (data/config, data/encounters, data/school, data/units, …). " +
        "Every edit goes through the same Core API the GUI uses, so it takes the project lock and writes a " +
        "timestamped backup under <project>/.mod-history before changing a file. " +
        "Every write tool takes `strict` (default false): with strict=true the affected file is validated after " +
        "the edit and the edit is reverted when it introduced a Warning, so a rejected write leaves the file " +
        "byte-identical. With strict=false the same findings are logged and returned in `warnings`, and the " +
        "write stands — that matches the GUI. Tools return data, never console text. " +
        "Long operations (project_pack, project_unpack) are Core jobs: they report progress notifications and " +
        "cancel when the client sends notifications/cancelled.";

    private readonly SemaphoreSlim _writeGate = new(1, 1);

    private McpProject(
        string projectPath,
        string becPath,
        string appDirectory,
        string toolsPath,
        string pythonCommand,
        IAppLog log)
    {
        ProjectPath = projectPath;
        BecPath = becPath;
        AppDirectory = appDirectory;
        ToolsPath = toolsPath;
        PythonCommand = pythonCommand;
        Log = log;

        // One API instance per family. They are stateless (open → mutate → write), so sharing an instance
        // across concurrent reads is safe; the instances that take collaborators share the same ones here
        // so a read never re-parses a file twice.
        Items = new ItemsApi();
        Skills = new SkillsApi();
        LookupText = new LookupTextApi();
        Classes = new ClassDefsApi();
        Encounters = new EncountersApi();
        Leagues = new LeaguesApi();
        Schools = new SchoolsApi();
        SchoolNames = new SchoolNamesApi();
        Grids = new GridsApi();
        Gladiators = new GladiatorsApi(Classes);
        StatSets = new StatSetsApi();
        SkillSets = new SkillSetsApi(Skills);
        ItemSets = new ItemSetsApi(Items);
        TintSets = new TintSetsApi();
        Config = new ConfigTableApi();
        History = new HistoryApi();
    }

    /// <summary>Absolute path of the project folder (the one holding <c>_BEC</c> and <c>_ISO</c>).</summary>
    public string ProjectPath { get; }

    /// <summary>Absolute path of the BEC folder — the root every Core API works against.</summary>
    public string BecPath { get; }

    /// <summary>Folder the server executable lives in; the toolbelt default is resolved against it.</summary>
    public string AppDirectory { get; }

    /// <summary>Folder holding the unpack/pack toolbelt.</summary>
    public string ToolsPath { get; }

    /// <summary>Interpreter command for the script toolchain (unused when the bundled exe is present).</summary>
    public string PythonCommand { get; }

    /// <summary>The app log shared by every tool; the shell reads the same stream in the GUI.</summary>
    public IAppLog Log { get; }

    /// <summary>Long-running operations started by this server (pack, unpack).</summary>
    public JobRegistry Jobs { get; } = new();

    /// <summary>The toolchain a build runs, chosen once so pack and unpack cannot disagree.</summary>
    public Toolchain Toolchain => Toolchain.Choose(AppDirectory, ToolsPath, PythonCommand);

    // ---- APIs exposed as tools (one tool class per family) --------------------
    public IItemsApi Items { get; }
    public ISkillsApi Skills { get; }
    public ILookupTextApi LookupText { get; }
    public IClassesApi Classes { get; }
    public IEncountersApi Encounters { get; }
    public ILeaguesApi Leagues { get; }
    public ISchoolsApi Schools { get; }
    public ISchoolNamesApi SchoolNames { get; }
    public IGridsApi Grids { get; }
    public IGladiatorsApi Gladiators { get; }
    public IStatSetsApi StatSets { get; }
    public ISkillSetsApi SkillSets { get; }
    public IItemSetsApi ItemSets { get; }
    public ITintSetsApi TintSets { get; }
    public IConfigTableApi Config { get; }
    public IHistoryApi History { get; }

    /// <summary>
    /// Resolve the invocation into a project. Throws <see cref="McpStartupException"/> with a message that
    /// names the offending path and what was expected.
    /// </summary>
    public static McpProject Resolve(string[] args)
    {
        string? projectArg = null;
        string? toolsArg = null;
        string? pythonArg = null;
        string? logArg = null;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--project":
                    projectArg = Take(args, ref i);
                    break;
                case "--tools":
                    toolsArg = Take(args, ref i);
                    break;
                case "--python":
                    pythonArg = Take(args, ref i);
                    break;
                case "--log":
                    logArg = Take(args, ref i);
                    break;
                default:
                    if (args[i].StartsWith('-'))
                    {
                        throw new McpStartupException($"unknown option '{args[i]}'");
                    }

                    if (projectArg is not null)
                    {
                        throw new McpStartupException(
                            $"more than one project path given ('{projectArg}' and '{args[i]}')");
                    }

                    // A bare path keeps the pre-Phase-8 stub's contract working.
                    projectArg = args[i];
                    break;
            }
        }

        string appDirectory = AppContext.BaseDirectory;
        string requested = projectArg is null
            ? Directory.GetCurrentDirectory()
            : Path.GetFullPath(projectArg);

        if (!Directory.Exists(requested))
        {
            throw new McpStartupException($"project folder not found: {requested}");
        }

        string bec = ProjectRoots.ResolveBecFolder(requested);
        if (!ProjectRoots.IsBecFolder(bec))
        {
            throw new McpStartupException(
                $"'{requested}' is not a Gladius project: no data/config folder under '{bec}'. " +
                "Pass --project <folder> with the project root or its _BEC folder.");
        }

        string tools = toolsArg is null ? Path.Combine(appDirectory, "tools") : Path.GetFullPath(toolsArg);
        string python = string.IsNullOrWhiteSpace(pythonArg) ? "python" : pythonArg.Trim();

        return new McpProject(
            ProjectFolderOf(requested, bec),
            bec,
            appDirectory,
            tools,
            python,
            CreateLog(logArg));
    }

    /// <summary>
    /// Run a write under the process-wide gate, with optional strict validation of the affected scope.
    /// </summary>
    /// <param name="operation">Short description for log lines, e.g. <c>items_patch 'Sword'</c>.</param>
    /// <param name="body">The mutation; its return value becomes the tool result.</param>
    /// <param name="strict">True to refuse (and revert) when the write introduces a Warning.</param>
    /// <param name="validate">
    /// Reads the findings of the scope the write affects (one record, one file, one set). Null for a family
    /// that has no validator — the flag is then accepted and changes nothing, which the tool description
    /// says explicitly rather than pretending otherwise.
    /// </param>
    /// <returns>
    /// A <see cref="WriteResult"/> on success, or an MCP error result (<c>isError: true</c>) when the write
    /// threw or strict mode refused it.
    /// </returns>
    public object Write(
        string operation,
        Func<object?> body,
        bool strict = false,
        Func<IReadOnlyList<ValidationIssue>>? validate = null)
    {
        _writeGate.Wait();
        try
        {
            IReadOnlyList<ValidationIssue> baseline = Array.Empty<ValidationIssue>();
            HashSet<string> backupsBefore = new(StringComparer.OrdinalIgnoreCase);

            if (validate is not null)
            {
                try
                {
                    // Taken before the write so a finding the project already had never counts against this
                    // call: vanilla content has legitimate oddities, and only a warning this edit introduced
                    // can make strict mode refuse.
                    baseline = validate();
                }
                catch (Exception ex)
                {
                    Log.Write(LogLevel.Error, $"{operation}: could not validate before writing: {ex}", "mcp");
                    return ToolResult.Fail($"{operation} was not attempted: validating the current state failed ({ex.Message}).");
                }

                backupsBefore = SnapshotBackups();
            }

            object? result;
            try
            {
                result = body();
            }
            catch (McpToolException ex)
            {
                return ToolResult.Fail(ex.Message);
            }
            catch (Exception ex)
            {
                Log.Write(LogLevel.Error, $"{operation} failed: {ex}", "mcp");
                return ToolResult.Fail($"{operation} failed: {ex.GetType().Name}: {ex.Message}");
            }

            IReadOnlyList<ValidationIssue> introduced = Array.Empty<ValidationIssue>();
            if (validate is not null)
            {
                IReadOnlyList<ValidationIssue> after;
                try
                {
                    after = validate();
                }
                catch (Exception ex)
                {
                    string reverted = RevertBackups(backupsBefore);
                    Log.Write(LogLevel.Error, $"{operation}: validating the result failed, reverted ({reverted}): {ex}", "mcp");
                    return ToolResult.Fail(
                        $"{operation}: the edit was made but could not be validated ({ex.Message}), so it was " +
                        $"reverted ({reverted}). The file is byte-identical to before the call.");
                }

                introduced = after
                    .Where(issue => !baseline.Contains(issue))
                    .OrderByDescending(issue => issue.IsWarning)
                    .ToArray();
            }

            foreach (ValidationIssue issue in introduced)
            {
                Log.Write(issue.IsWarning ? LogLevel.Warning : LogLevel.Info, $"{operation}: {issue}", "mcp");
            }

            if (strict && introduced.Any(issue => issue.IsWarning))
            {
                string reverted = RevertBackups(backupsBefore);
                Log.Write(LogLevel.Warning,
                    $"{operation}: refused by strict mode ({introduced.Count(issue => issue.IsWarning)} warning(s)), {reverted}",
                    "mcp");

                return ToolResult.Fail(
                    $"{operation}: refused by strict mode. The edit introduced " +
                    $"{introduced.Count(issue => issue.IsWarning)} warning(s); it was reverted ({reverted}) " +
                    "and the file is byte-identical to before the call." +
                    Environment.NewLine +
                    string.Join(Environment.NewLine, introduced.Select(issue => "  " + issue)) +
                    Environment.NewLine +
                    "Retry with strict=false to write anyway (the GUI's advisory behaviour).");
            }

            if (introduced.Count > 0)
            {
                Log.Write(LogLevel.Info,
                    $"{operation}: {introduced.Count} finding(s) reported; written anyway" +
                    (strict ? " (strict mode, no warnings)" : string.Empty), "mcp");
            }

            // Every write leaves one line, like the GUI's "Item 'X' saved." The log is the audit trail a client
            // reads back with logs_tail, so a silent success would be indistinguishable from a no-op.
            Log.Write(LogLevel.Success, $"{operation}: written", "mcp");

            return new WriteResult
            {
                Result = result,
                Warnings = introduced,
                Strict = strict,
            };
        }
        finally
        {
            _writeGate.Release();
        }
    }

    /// <summary>
    /// <see cref="Write(string, Func{object?}, bool, Func{IReadOnlyList{ValidationIssue}}?)"/> for an API that
    /// returns <c>void</c>; the tool result is then <c>true</c> (the write happened).
    /// </summary>
    public object WriteVoid(
        string operation,
        Action body,
        bool strict = false,
        Func<IReadOnlyList<ValidationIssue>>? validate = null) =>
        Write(operation, () =>
        {
            body();
            return true;
        }, strict, validate);

    /// <summary>
    /// Run a write whose validation is about the payload rather than the file (skills): validate first, then
    /// write. With <paramref name="strict"/>, a warning refuses the write before anything touches disk.
    /// </summary>
    /// <param name="operation">Short description for log lines.</param>
    /// <param name="validatePayload">Findings for the record about to be written.</param>
    /// <param name="body">The mutation; its return value becomes the tool result.</param>
    /// <param name="strict">True to refuse when the payload has a warning.</param>
    public object WriteChecked(
        string operation,
        Func<IReadOnlyList<ValidationIssue>> validatePayload,
        Func<object?> body,
        bool strict)
    {
        _writeGate.Wait();
        try
        {
            IReadOnlyList<ValidationIssue> issues;
            try
            {
                issues = validatePayload();
            }
            catch (Exception ex)
            {
                Log.Write(LogLevel.Error, $"{operation}: could not validate the payload: {ex}", "mcp");
                return ToolResult.Fail($"{operation} was not attempted: validation failed ({ex.Message}).");
            }

            foreach (ValidationIssue issue in issues.OrderByDescending(issue => issue.IsWarning))
            {
                Log.Write(issue.IsWarning ? LogLevel.Warning : LogLevel.Info, $"{operation}: {issue}", "mcp");
            }

            if (strict && issues.Any(issue => issue.IsWarning))
            {
                Log.Write(LogLevel.Warning,
                    $"{operation}: refused by strict mode ({issues.Count(issue => issue.IsWarning)} warning(s)); nothing was written",
                    "mcp");

                return ToolResult.Fail(
                    $"{operation}: refused by strict mode — nothing was written. " +
                    $"{issues.Count(issue => issue.IsWarning)} warning(s):" +
                    Environment.NewLine +
                    string.Join(Environment.NewLine, issues.Select(issue => "  " + issue)) +
                    Environment.NewLine +
                    "Retry with strict=false to write anyway (the GUI's advisory behaviour), or fix the record.");
            }

            try
            {
                object? value = body();
                Log.Write(LogLevel.Success, $"{operation}: written", "mcp");
                return new WriteResult
                {
                    Result = value,
                    Warnings = issues,
                    Strict = strict,
                };
            }
            catch (McpToolException ex)
            {
                return ToolResult.Fail(ex.Message);
            }
            catch (Exception ex)
            {
                Log.Write(LogLevel.Error, $"{operation} failed: {ex}", "mcp");
                return ToolResult.Fail($"{operation} failed: {ex.GetType().Name}: {ex.Message}");
            }
        }
        finally
        {
            _writeGate.Release();
        }
    }

    /// <summary>Every <c>.mod-history</c> folder a Core API might write into for this project.</summary>
    private IEnumerable<string> HistoryRoots()
    {
        yield return BecPath;

        string project = ProjectRoots.ProjectFolderOf(BecPath);
        if (!string.Equals(project, BecPath, StringComparison.OrdinalIgnoreCase)) yield return project;
    }

    private HashSet<string> SnapshotBackups()
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string root in HistoryRoots())
        {
            foreach (BackupEntry entry in ChangeHistoryService.ListBackups(root)) paths.Add(entry.BackupPath);
        }

        return paths;
    }

    /// <summary>
    /// Put back every backup the write created (there is normally one). Returns a human-readable summary of
    /// what was restored, for the refusal message.
    /// </summary>
    private string RevertBackups(HashSet<string> before)
    {
        var created = new List<(string Root, BackupEntry Entry)>();
        foreach (string root in HistoryRoots())
        {
            foreach (BackupEntry entry in ChangeHistoryService.ListBackups(root))
            {
                if (!before.Contains(entry.BackupPath)) created.Add((root, entry));
            }
        }

        if (created.Count == 0)
        {
            return "no backup was created, so there was nothing to revert — inspect the file";
        }

        var restored = new List<string>(created.Count);
        // Newest first, so a call that touched several files ends with all of them at their pre-call bytes.
        foreach ((string root, BackupEntry entry) in created.OrderByDescending(item => item.Entry.Timestamp))
        {
            ChangeHistoryService.RestoreBackup(root, entry.BackupPath);
            restored.Add(entry.RelativePath);
        }

        return "restored " + string.Join(", ", restored.Distinct(StringComparer.OrdinalIgnoreCase));
    }

    private static string Take(string[] args, ref int index)
    {
        if (index + 1 >= args.Length)
        {
            throw new McpStartupException($"'{args[index]}' needs a value");
        }

        index++;
        if (string.IsNullOrWhiteSpace(args[index]))
        {
            throw new McpStartupException($"'{args[index - 1]}' needs a value");
        }

        return args[index];
    }

    private static IAppLog CreateLog(string? requestedSink)
    {
        string sink = requestedSink is null ? AppLog.DefaultSinkPath() : Path.GetFullPath(requestedSink);
        try
        {
            string? folder = Path.GetDirectoryName(sink);
            if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            sink = string.Empty;   // memory-only; the server still works
        }

        // Append rather than truncate: a GUI session may own the same file, and wiping another process's
        // evidence of a run is worse than a long file.
        var log = new AppLog(AppLog.DefaultCapacity, sink.Length == 0 ? null : sink);
        if (sink.Length > 0) log.Write(LogLevel.Info, $"---- MCP session started (pid {Environment.ProcessId}) ----", "mcp");

        // Every line the Core APIs write also reaches stderr, so a terminal client shows the same record the
        // GUI would. Never stdout: that stream is the transport.
        log.Written += (_, entry) =>
        {
            try
            {
                Console.Error.WriteLine($"[{entry.Level.ToString().ToLowerInvariant()}] {entry.Source}: {entry.Message}");
            }
            catch (IOException)
            {
                // A closed stderr must not break an edit.
            }
        };

        return log;
    }

    /// <summary>
    /// The project folder that owns the BEC folder: the folder the user named when that is the project root,
    /// otherwise the BEC folder's parent when this is a real <c>_BEC</c>/<c>_ISO</c> pair, otherwise the BEC
    /// folder itself (a hand-made fixture has no project half).
    /// </summary>
    private static string ProjectFolderOf(string requested, string bec)
    {
        string normalized = Path.GetFullPath(requested)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        if (string.Equals(normalized, bec, StringComparison.OrdinalIgnoreCase))
        {
            return ProjectRoots.ProjectFolderOf(bec);
        }

        return normalized;
    }
}