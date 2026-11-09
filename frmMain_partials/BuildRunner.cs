using System.Collections.Concurrent;
using System.Drawing;
using System.IO;
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

        /// <summary>Unpack <paramref name="isoPath"/> into a project named <paramref name="projectName"/>.</summary>
        private async Task<BuildResult> RunUnpackAsync(string isoPath, string projectName, RichTextBox? target = null)
        {
            var request = new UnpackRequest(
                isoPath, projectName, ProjectsFolder(), ToolbeltDirectory(), PythonCommand);

            SaveToolCommandsOnce(UnpackService.ProjectFolderOf(request), UnpackService.Steps(request), "unpack");

            using (MirrorLogInto(target ?? rtbUnpackOutput))
            {
                return await Task.Run(() => new UnpackService(_log, "unpack").Unpack(request));
            }
        }

        /// <summary>Pack <paramref name="projectFolder"/> into its ISO.</summary>
        private async Task<BuildResult> RunPackAsync(string projectFolder, RichTextBox? target = null)
        {
            var request = new PackRequest(projectFolder, ToolbeltDirectory(), PythonCommand);

            SaveToolCommandsOnce(projectFolder, PackService.Steps(request), "pack");

            using (MirrorLogInto(target ?? rtbPackOutput))
            {
                return await Task.Run(() => new PackService(_log, "pack").Pack(request));
            }
        }

        /// <summary>Compile one campaign script, reporting its output into <paramref name="target"/>.</summary>
        private async Task<BuildStepResult> RunCompileScriptAsync(
            string projectFolder, string scriptName, RichTextBox? target = null)
        {
            var options = new BuildOptions(projectFolder, ToolbeltDirectory(), PythonCommand);

            using (MirrorLogInto(target ?? rtbPackOutput))
            {
                return await Task.Run(() => new ProjectBuildService(_log, "build").CompileScript(options, scriptName));
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
