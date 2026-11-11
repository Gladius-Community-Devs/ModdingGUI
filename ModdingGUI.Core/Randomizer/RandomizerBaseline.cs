namespace ModdingGUI.Core.Randomizer;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ModdingGUI.Core;
using ModdingGUI.Core.Api;
using ModdingGUI.Core.Logging;

/// <summary>What capturing or restoring the baseline did.</summary>
/// <param name="Files">Files involved.</param>
/// <param name="AlreadyExisted">True when a baseline was already pinned and left alone.</param>
public sealed record BaselineResult(int Files, bool AlreadyExisted = false);

/// <summary>
/// The pinned vanilla copy of everything the randomizer rewrites.
/// </summary>
/// <remarks>
/// <para>
/// The first randomization captures the project's current state — on a clean unpack, vanilla — into
/// <c>.mod-history/_randomizer-baseline/</c>. Every later run restores it first, so runs never stack: the same
/// seed produces the same project, and a run after a run starts from the same place the first one did. Restoring
/// also backs up the files it overwrites, so a reset is itself undoable through the normal history.
/// </para>
/// <para>
/// Only the families the randomizer writes are captured: the gladiator table, the campaign rosters, the encounters,
/// the leagues and the script files it patches. Compiled <c>.scb</c> files are left out — they are rebuilt from the
/// restored scripts.
/// </para>
/// </remarks>
public sealed class RandomizerBaseline
{
    /// <summary>Folder under <c>.mod-history</c> that holds the pinned copy.</summary>
    public const string Folder = "_randomizer-baseline";

    /// <summary>Scripts the randomizer patches; the only scripts worth pinning.</summary>
    private static readonly string[] PatchedScripts =
    {
        "gameflowvalenssteppes.scp", "gameflowvalensexpanse.scp",
        "gameflowursulasteppes.scp", "gameflowursulaexpanse.scp",
        "gameflowvalensendgame.scp", "gameflowursulaendgame.scp",
        "leaguefjordfallen.scp",
    };

    private readonly IAppLog? _log;
    private readonly string? _scope;

    /// <summary>Create the store; pass a log to report what it does.</summary>
    public RandomizerBaseline(IAppLog? log = null, string? scope = "randomizer")
    {
        _log = log;
        _scope = scope;
    }

    /// <summary>True when a baseline has been pinned for this project.</summary>
    public bool Exists(string projectRoot)
    {
        string root = BaselineRoot(projectRoot);
        return Directory.Exists(root) && Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Any();
    }

    /// <summary>
    /// Pin the current state. Idempotent: an existing baseline is left untouched, so a second call can never
    /// overwrite vanilla with an already-randomized project.
    /// </summary>
    public BaselineResult Capture(string projectRoot)
    {
        string bec = ProjectRoots.ResolveBecFolder(projectRoot);
        string root = BaselineRoot(projectRoot);

        if (Exists(projectRoot))
        {
            _log?.Write(LogLevel.Info,
                "Randomizer baseline already pinned; leaving it alone.", _scope);
            return new BaselineResult(0, AlreadyExisted: true);
        }

        using (ProjectLock.Acquire(bec))
        {
            int copied = 0;
            foreach (string relative in RandomizerFiles(bec))
            {
                string source = Full(bec, relative);
                if (!File.Exists(source)) continue;

                string destination = Full(root, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(source, destination, overwrite: true);
                copied++;
            }

            _log?.Write(LogLevel.Success,
                $"Pinned the vanilla baseline ({copied} file(s)) for repeatable runs.", _scope);

            return new BaselineResult(copied);
        }
    }

    /// <summary>
    /// Put the pinned files back, backing up whatever is there now. Returns 0 files when nothing was pinned.
    /// </summary>
    public BaselineResult Restore(string projectRoot)
    {
        string bec = ProjectRoots.ResolveBecFolder(projectRoot);
        string root = BaselineRoot(projectRoot);

        if (!Exists(projectRoot))
        {
            _log?.Write(LogLevel.Warning,
                "No randomizer baseline yet; nothing to restore. Run the randomizer once to pin one.", _scope);
            return new BaselineResult(0);
        }

        using (ProjectLock.Acquire(bec))
        {
            int restored = 0;
            foreach (string baselineFile in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(root, baselineFile).Replace('\\', '/');
                string target = Full(bec, relative);

                // Preserve the current file first: a reset must be undoable through the same history.
                if (File.Exists(target)) ChangeHistoryService.Backup(ProjectRoots.ProjectFolderOf(bec), target);

                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(baselineFile, target, overwrite: true);
                restored++;
            }

            _log?.Write(LogLevel.Success,
                $"Restored the vanilla baseline ({restored} file(s)).", _scope);

            return new BaselineResult(restored);
        }
    }

    /// <summary>Project-relative paths the baseline covers, for the files that exist.</summary>
    public IReadOnlyList<string> RandomizerFiles(string bec)
    {
        var files = new List<string> { "data/units/gladiators.txt" };

        string school = Path.Combine(bec, "data", "school");
        if (Directory.Exists(school))
        {
            files.AddRange(Directory.EnumerateFiles(school, "*.tok")
                .Select(path => "data/school/" + Path.GetFileName(path)));
        }

        foreach (string folder in new[] { "data/encounters", "data/towns/leagues" })
        {
            string full = Path.Combine(bec, folder.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(full)) continue;

            files.AddRange(Directory.EnumerateFiles(full, "*.enc", SearchOption.AllDirectories)
                .Concat(Directory.EnumerateFiles(full, "*.tok", SearchOption.AllDirectories))
                .Select(path => folder + "/" + Path.GetRelativePath(full, path).Replace('\\', '/')));
        }

        files.AddRange(PatchedScripts.Select(name => "data/script/" + name));
        return files.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static string BaselineRoot(string projectRoot) =>
        Path.Combine(ProjectRoots.ResolveBecFolder(projectRoot), ".mod-history", Folder);

    private static string Full(string bec, string relative) =>
        Path.Combine(bec, relative.Replace('/', Path.DirectorySeparatorChar));
}
