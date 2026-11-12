using System.Collections.Concurrent;
using System.Drawing;
using System.IO;
using ModdingGUI.Core.Discs;
using ModdingGUI.Core.Logging;
using ModdingGUI.Core.Tools;

namespace ModdingGUI
{
    /// <summary>
    /// Runs the toolchain through Core, with the log mirrored into the panel the user is watching.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This replaces the batch-file runner (Phase 7.8/7.9). The shell no longer owns child processes: pack, unpack
    /// and script compile all go through <see cref="PackService"/>, <see cref="UnpackService"/> and
    /// <see cref="ProjectBuildService"/>, which start each tool directly — no <c>cmd.exe</c>, no generated
    /// <c>.bat</c> — and emit every line into <see cref="frmMain.Output"/>. Because the same services back the CLI,
    /// the GUI and a headless run print identical logs.
    /// </para>
    /// <para>
    /// The RichTextBox is a <em>view</em> of the log. A run subscribes a mirror that renders Core's entries into the
    /// target box, then unsubscribes; entries are always rendered with <c>AppendLogText</c>, never <c>AppendLog</c>,
    /// because the latter writes back into the log and would loop.
    /// </para>
    /// </remarks>
    public partial class frmMain
    {
        /// <summary>Folder holding the Python toolbelt and the script compiler (always beside the exe).</summary>
        private string ToolbeltDirectory() => Path.Combine(GetAppDirectory(), "tools");

        /// <summary>
        /// Interpreter for the toolbelt. The app has always used a bare <c>python</c>; the CLI can override it with
        /// <c>--python</c> when a launcher such as <c>py -3</c> is wanted.
        /// </summary>
        private const string PythonCommand = "python";


        private Toolchain? _toolchain;

        /// <summary>
        /// The toolchain every build runs against: the bundled <c>tools/v009/tools_v009.exe</c> when it is installed,
        /// otherwise the Python scripts in <c>tools/</c>.
        /// </summary>
        /// <remarks>
        /// The bundled executable carries its own interpreter, so a user does not need Python at all. The scripts stay as
        /// the fallback for a developer tree with no exe built, and both produce byte-identical output: a vanilla
        /// unpack/repack round-trips the ISO, which scripts/validate-toolchain.ps1 checks.
        /// </remarks>
        private Toolchain Toolchain => _toolchain ??= Toolchain.Choose(GetAppDirectory(), ToolbeltDirectory(), PythonCommand);
        /// <summary>Unpack <paramref name="isoPath"/> into a project named <paramref name="projectName"/>.</summary>
        private async Task<BuildResult> RunUnpackAsync(string isoPath, string projectName, RichTextBox? target = null)
        {
            // The unpack tool seeks raw ISO offsets, so a compressed container is converted to a temporary plain ISO
            // first. NKit is the exception: it is a valid disc with padding stripped and its file table still resolves,
            // so unpacking works on it directly - only patching needs the rebuilt image.
            DiscImageFormat detected = DiscImageProbe.Detect(isoPath);
            DiscImagePreparation prepared;

            if (detected == DiscImageFormat.Nkit)
            {
                AppendLog("This is an NKit (scrubbed) image. Unpacking works, but patching needs a rebuilt ISO: an NKit " +
                    "image has had its padding removed, so no vanilla patch can apply to it.", WarningColor, target ?? rtbUnpackOutput);
                prepared = new DiscImagePreparation(true, isoPath, detected, "Unpacking the NKit image directly.");
            }
            else
            {
                prepared = PrepareDiscImage(isoPath, target ?? rtbUnpackOutput);
            }
            if (!prepared.Ok || prepared.IsoPath is null)
            {
                // A one-step result carrying the reason: the caller shows the log, and the text is in it.
                return new BuildResult(1, new[]
                {
                    new BuildStepResult("disc image", 1, new[] { prepared.Message }),
                });
            }

            try
            {
                var request = new UnpackRequest(
                    prepared.IsoPath, projectName, ProjectsFolder(), ToolbeltDirectory(), PythonCommand, Toolchain);

                SaveToolCommandsOnce(UnpackService.ProjectFolderOf(request), UnpackService.Steps(request), "unpack");

                using (MirrorLogInto(target ?? rtbUnpackOutput))
                {
                    // A job, not a task: it has an id, a state and a result, so the driver can wait on completion
                    // instead of guessing how long a 1.4 GB unpack takes.
                    var job = _jobs.Start("unpack", UnpackService.ProjectFolderOf(request),
                        token => new UnpackService(_log, "unpack").Unpack(request, token), _log);

                    return await job.Completion;
                }
            }
            finally
            {
                // The temporary ISO can be a gigabyte and a half; it never outlives the unpack.
                if (prepared.IsTemporary) DiscImagePreparer.Cleanup(prepared.IsoPath);
            }
        }

        /// <summary>Pack <paramref name="projectFolder"/> into its ISO.</summary>
        private async Task<BuildResult> RunPackAsync(string projectFolder, RichTextBox? target = null)
        {
            var request = new PackRequest(projectFolder, ToolbeltDirectory(), PythonCommand, Toolchain);

            SaveToolCommandsOnce(projectFolder, PackService.Steps(request), "pack");

            using (MirrorLogInto(target ?? rtbPackOutput))
            {
                var job = _jobs.Start("pack", projectFolder,
                    token => new PackService(_log, "pack").Pack(request, token), _log);

                return await job.Completion;
            }
        }

        /// <summary>Compile one campaign script, reporting its output into <paramref name="target"/>.</summary>
        private async Task<BuildStepResult> RunCompileScriptAsync(
            string projectFolder, string scriptName, RichTextBox? target = null)
        {
            var options = new BuildOptions(projectFolder, ToolbeltDirectory(), PythonCommand);

            using (MirrorLogInto(target ?? rtbPackOutput))
            {
                var job = _jobs.Start("compile", scriptName,
                    token => new BuildResult(0, new[]
                    {
                        new ProjectBuildService(_log, "build").CompileScript(options, scriptName, token),
                    }), _log);

                var result = await job.Completion;
                return result.Steps.Count > 0
                    ? result.Steps[0]
                    : new BuildStepResult(scriptName, 1, Array.Empty<string>());
            }
        }

        /// <summary>
        /// Subscribe Core's log to one RichTextBox for the duration of a run.
        /// </summary>
        /// <remarks>
        /// Core writes from a worker thread, so entries are queued and drained on the UI thread; one queued drain at
        /// a time keeps a chatty step from flooding the message pump. The caller disposes the mirror to detach.
        /// </remarks>
        private IDisposable MirrorLogInto(RichTextBox target) => new LogMirror(this, target);

        /// <summary>
        /// When the user has asked to keep batch files, write the equivalent command chain next to the project so it
        /// can be inspected or reused. Nothing executes it: the steps run directly (PLAN.md Phase 7.9).
        /// </summary>
        private void SaveToolCommandsOnce(string folder, IReadOnlyList<ToolStep> steps, string kind)
        {
            if (!saveBatMenuItem.Checked) return;

            try
            {
                string path = Path.Combine(folder, $"ModdingGUI-{kind}-commands.bat");
                File.WriteAllText(path, BatchContentBuilder.ToBatchText(steps));
                AppendLog($"Saved the {kind} command chain for inspection: {path}", InfoColor);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // A convenience, never a reason to fail the run.
                AppendLog($"Could not save the {kind} command chain: {ex.Message}", WarningColor);
            }
        }

        /// <summary>Renders Core log entries into one RichTextBox until disposed.</summary>
        private sealed class LogMirror : IDisposable
        {
            private readonly frmMain _owner;
            private readonly RichTextBox _target;
            private readonly ConcurrentQueue<LogEntry> _pending = new();
            private int _draining;
            private int _disposed;

            public LogMirror(frmMain owner, RichTextBox target)
            {
                _owner = owner;
                _target = target;
                owner._log.Written += OnWritten;
            }

            private void OnWritten(object? sender, LogEntry entry)
            {
                if (Volatile.Read(ref _disposed) != 0) return;

                _pending.Enqueue(entry);
                if (_owner.IsDisposed || !_owner.IsHandleCreated) return;

                // One queued drain at a time, so a chatty step cannot flood the message pump.
                if (Interlocked.CompareExchange(ref _draining, 1, 0) != 0) return;

                try
                {
                    _owner.BeginInvoke(Drain);
                }
                catch (InvalidOperationException)
                {
                    // The form went away mid-run; the log file still has everything.
                    Interlocked.Exchange(ref _draining, 0);
                }
            }

            private void Drain()
            {
                Interlocked.Exchange(ref _draining, 0);
                while (_pending.TryDequeue(out LogEntry entry))
                {
                    _owner.AppendLogText(entry.Message, ColorFor(entry.Level), _target);
                }
            }

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

                _owner._log.Written -= OnWritten;

                // Render whatever is still queued. Dispose is called from the UI thread once the run has finished,
                // which is the only moment this queue can still hold lines.
                try
                {
                    Drain();
                }
                catch (InvalidOperationException)
                {
                    // Control already torn down; the file sink is the record.
                }
            }
        }
    }
}
