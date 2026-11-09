using System.Drawing;
using System.IO;
using ModdingGUI.Core.Logging;
using ModdingGUI.Core.Randomizer;
using ModdingGUI.Models;
using System.Diagnostics;
using System.Text.Json;

namespace ModdingGUI
{
    public partial class frmMain
    {
        /// <summary>
        /// The application's output stream. Everything AppendLog receives is stored here before it is
        /// rendered, so the same run is observable by the GUI, by tests and by the MCP server.
        /// </summary>
        private readonly AppLog _log = AppLog.CreateForRun();

        /// <summary>Read access for anything that needs the stream (future MCP bridge, tests).</summary>
        public IAppLog Output => _log;

        // Define static readonly colors for use in logging messages
        private static readonly Color SuccessColor = Color.Green; // Color for success messages
        private static readonly Color ErrorColor = Color.Red;     // Color for error messages
        private static readonly Color InfoColor = Color.Blue;     // Color for informational messages
        private static readonly Color WarningColor = Color.Orange; // Assuming WarningColor was intended
        /// <summary>
        /// Window and taskbar icon. Read from the icon embedded in the executable (the project sets
        /// <c>ApplicationIcon=ludo.ico</c>), falling back to a loose <c>ludo.ico</c> beside the exe and
        /// then to the framework default.
        /// </summary>
        /// <remarks>
        /// Deliberately not a designer resource. This form's .resx existed only to carry the icon, and
        /// the WinForms designer kept rewriting it with the full ResX template, which VS's MSBuild then
        /// refused when the file carried a mark-of-the-web ("MSB3821 ... Internet or Restricted zone").
        /// Setting it here means the build never has to process that resource, and the icon survives a
        /// designer save.
        /// </remarks>
        private void ApplyLudoIcon()
        {
            try
            {
                Icon? embedded = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                if (embedded is not null)
                {
                    Icon = embedded;
                    return;
                }

                string loose = Path.Combine(GetAppDirectory(), "ludo.ico");
                if (File.Exists(loose))
                {
                    Icon = new Icon(loose);
                }
            }
            catch (Exception ex)
            {
                // A missing icon must never stop the app from opening.
                Debug.WriteLine($"Could not load the application icon: {ex.Message}");
            }
        }

        // Method to check if Python 3 is installed
        private bool IsPythonInstalled()
        {
            try
            {
                // Create a new process start info to run 'python --version'
                ProcessStartInfo pythonInfo = new ProcessStartInfo("python", "--version")
                {
                    RedirectStandardOutput = true, // Redirect the standard output stream
                    UseShellExecute = false,       // Do not use the operating system shell to start the process
                    CreateNoWindow = true          // Do not create a window for the process
                };

                // Start the process using the process start info
                using (Process process = Process.Start(pythonInfo))
                {
                    process.WaitForExit(); // Wait for the process to exit
                    return process.ExitCode == 0; // Return true if exit code is 0 (success)
                }
            }
            catch
            {
                // If any exception occurs, return false indicating Python is not installed
                return false;
            }
        }
        // Method to append a message to the log with optional color and target (unpack or pack).
        // The message is recorded in the Core log first, then rendered: the RichTextBox is a viewer,
        // not the store, so headless runs (tests, MCP) see the same output.
        /// <summary>
        /// Run an async operation whose result nobody awaits, reporting a failure instead of letting it surface as an
        /// unhandled task exception. Every fire-and-forget load should go through here.
        /// </summary>
        /// <param name="what">Short description used in the log line.</param>
        /// <param name="work">The operation.</param>
        private async void RunGuarded(string what, Func<Task> work)
        {
            try
            {
                await work().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                AppendLog($"{what} failed: {ex.Message}", ErrorColor);
            }
        }

        private void AppendLog(string message, Color? color = null, RichTextBox targetRtb = null)
        {
            targetRtb ??= rtbUnpackOutput;
            color ??= Color.Black;
            _log.Write(LevelFor(color.Value), message ?? string.Empty);
            AppendLogText(message, color, targetRtb);
        }

        /// <summary>
        /// Render a line without writing it to <see cref="IAppLog"/>. Use this to *display* entries that came from
        /// the log: <see cref="AppendLog"/> writes back into it, which would raise Written again and loop.
        /// </summary>
        private void AppendLogText(string message, Color? color = null, RichTextBox? targetRtb = null)
        {
            targetRtb ??= rtbUnpackOutput;
            color ??= Color.Black;

            // The line is already in the log stream, so rendering is best-effort: a control without a handle yet (during
            // startup) or one that has been disposed (during close) must not turn logging into a crash.
            if (targetRtb.IsDisposed || !targetRtb.IsHandleCreated) return;

            try
            {
                if (targetRtb.InvokeRequired)
                {
                    targetRtb.Invoke(new Action(() => AppendLogText(message, color, targetRtb)));
                    return;
                }

                targetRtb.SelectionStart = targetRtb.TextLength;
                targetRtb.SelectionColor = color.Value;
                targetRtb.AppendText(message + Environment.NewLine);
                targetRtb.ScrollToCaret();
            }
            catch (InvalidOperationException)
            {
                // Handle destroyed between the check and the call.
            }
        }


        /// <summary>
        /// The log level a legacy colour implies. The shell has always signalled severity by colour;
        /// this keeps that mapping in one place while the stream itself carries a real level.
        /// </summary>
        private static LogLevel LevelFor(Color color)
        {
            if (color.R > 150 && color.G < 100 && color.B < 100) return LogLevel.Error;      // red
            if (color.R > 150 && color.G is > 100 and < 200 && color.B < 80) return LogLevel.Warning; // orange
            if (color.G > 100 && color.R < 120 && color.B < 120) return LogLevel.Success;     // green
            if (color.B > 120 && color.R < 120) return LogLevel.Info;                          // blue
            return LogLevel.Info;
        }

        // Method to handle process output data and log it appropriately
        private void LogProcessOutput(DataReceivedEventArgs e, Color? color = null, RichTextBox targetRtb = null)
        {
            if (!string.IsNullOrEmpty(e.Data)) // Check if data is not null or empty
            {
                AppendLog(e.Data, color ?? Color.Black, targetRtb); // Append the data to the log
            }
        }

        public static string GetAppDirectory()
        {
            // Retrieve the full path to the executable
            string executablePath = Process.GetCurrentProcess().MainModule.FileName;

            // Extract and return the directory path
            return Path.GetDirectoryName(executablePath) ?? throw new InvalidOperationException("Unable to determine the application directory.");
        }

        // Method to open a folder location in Windows Explorer
        private void OpenLocation(string path)
        {
            // The tool's folder fields hold names relative to the app directory (the project folder sits
            // next to the exe). Handing a relative path to the shell would resolve it against whatever
            // working directory the shell happens to use, so make it absolute first.
            if (!string.IsNullOrWhiteSpace(path) && !Path.IsPathRooted(path))
            {
                path = Path.Combine(GetAppDirectory(), path);
            }

            // The path fields are normalised to forward slashes for the Python tooling; Windows APIs want backslashes.
            if (!string.IsNullOrWhiteSpace(path))
            {
                path = ModdingGUI.Helpers.ShellFolderOpener.NormaliseForShell(path);
            }

            if (!Directory.Exists(path)) // Check if the directory exists
            {
                AppendLog($"Cannot open '{path}': not a folder.", WarningColor, rtbPackOutput);
                MessageBox.Show("Selected location does not exist."); // Show an error message
                return;
            }

            AppendLog($"Opening location: {path}", InfoColor, rtbPackOutput);

            // Ask the shell that is already running first. SHOpenFolderAndSelectItems opens the folder through the
            // shell namespace, so nothing is spawned: neither the 0xC0000142 loader failure of starting explorer.exe
            // ourselves (it inherits this process's environment) nor a refused ShellExecute verb can stop it.
            if (ModdingGUI.Helpers.ShellFolderOpener.TryOpen(path, out string shellError))
            {
                return;
            }

            AppendLog($"Could not open '{path}' through the shell: {shellError}", WarningColor, rtbPackOutput);
            AppendLog($"Process context: {ModdingGUI.Helpers.ProcessContext.Describe()}", WarningColor, rtbPackOutput);

            // One fallback only: the ShellExecute verb, on a normalised path. Spawning explorer.exe is not offered at
            // all - it dies at load with 0xC0000142 when it inherits this process environment, which is a worse
            // experience than an honest message.
            try
            {
                string shellPath = ModdingGUI.Helpers.ShellFolderOpener.NormaliseForShell(path);
                Process.Start(new ProcessStartInfo { FileName = shellPath, UseShellExecute = true });
                return;
            }
            catch (Exception ex)
            {
                AppendLog($"Could not open '{path}' via ShellExecute: {ex.Message}", WarningColor, rtbPackOutput);
            }

            // Nothing worked. Copy the path so it can be pasted into Explorer, and say why it failed: a process in a
            // restricted job or a non-interactive window station cannot drive the shell at all.
            bool copied = false;
            try
            {
                Clipboard.SetText(path);
                copied = true;
            }
            catch (Exception ex)
            {
                AppendLog($"Could not copy the path to the clipboard: {ex.Message}", WarningColor, rtbPackOutput);
            }

            MessageBox.Show(
                $"Windows would not open the folder:\n\n{path}\n\n" +
                (copied ? "The path is on the clipboard - paste it into Explorer's address bar.\n\n" : string.Empty) +
                "If this keeps happening, the app is running in a restricted context " +
                $"({ModdingGUI.Helpers.ProcessContext.Describe()}). Launch it normally from Explorer.",
                "Open folder", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        private void LoadProjects()
        {
            // Rebuilding drops the selection, which is what made "delete a second project" do nothing. Remember
            // what was selected and expanded, and put it back afterwards.
            string? previousSelection = tvwProjects.SelectedNode?.Tag as string ??
                                        tvwProjects.SelectedNode?.Parent?.Tag as string;
            var previousSelectionText = tvwProjects.SelectedNode?.Text;
            var expanded = tvwProjects.Nodes.Cast<TreeNode>()
                .Where(node => node.IsExpanded)
                .Select(node => node.Tag as string)
                .Where(tag => tag is not null)
                .Select(tag => tag!)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            tvwProjects.BeginUpdate();
            tvwProjects.Nodes.Clear();

            try
            {
                string appDirectory = AppDirectoryForProjects();
                int projects = 0;

                // Sorted, so the list does not jump around between refreshes.
                var directories = Directory.GetDirectories(appDirectory)
                    .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase);

                foreach (var dir in directories)
                {
                    string projectName = Path.GetFileName(dir);
                    if (projectName.Equals("tools", StringComparison.OrdinalIgnoreCase)) continue;

                    if (IsProjectDirectory(dir))
                    {
                        projects++;

                        string becFolder = Path.Combine(dir, $"{projectName}_BEC");
                        bool randomized = Directory.Exists(
                            Path.Combine(becFolder, ".mod-history", RandomizerBaseline.Folder));

                        // Create a tree node for the project
                        bool complete = IsCompleteProject(dir);
                        string label = projectName;
                        if (randomized) label += " (randomized)";
                        if (!complete) label += " (incomplete)";

                        // Create a tree node for the project
                        TreeNode projectNode = new TreeNode(label)
                        {
                            Tag = dir,
                            ImageKey = "folder",
                            SelectedImageKey = "folder",
                            ToolTipText = (randomized
                                    ? $"{dir}\nRandomizer baseline pinned: a run can be undone with Reset to Vanilla."
                                    : dir)
                                + (complete ? string.Empty : "\nIncomplete project: missing " + string.Join(", ", MissingProjectFiles(dir))),
                        };

                        // Define paths for _ISO and _BEC
                        string isoPath = Path.Combine(dir, $"{projectName}_ISO");
                        string becPath = Path.Combine(dir, $"{projectName}_BEC");

                        // Add _BEC node if it exists
                        if (Directory.Exists(becPath))
                        {
                            TreeNode becNode = new TreeNode("BEC")
                            {
                                Tag = becPath,
                                ImageKey = "folder",
                                SelectedImageKey = "folder"
                            };

                            // Check if BEC has subdirectories or files
                            if (HasSubDirectories(becPath) || HasFiles(becPath))
                            {
                                becNode.Nodes.Add(new TreeNode("Loading...")); // Add dummy node
                            }

                            projectNode.Nodes.Add(becNode);
                        }
                        else
                        {
                            //AppendLog($"_BEC directory not found for project: {projectName}", ErrorColor);
                        }

                        // Add _ISO node if it exists
                        if (Directory.Exists(isoPath))
                        {
                            TreeNode isoNode = new TreeNode("ISO")
                            {
                                Tag = isoPath,
                                ImageKey = "folder",
                                SelectedImageKey = "folder"
                            };

                            // Check if ISO has subdirectories or files
                            if (HasSubDirectories(isoPath) || HasFiles(isoPath))
                            {
                                isoNode.Nodes.Add(new TreeNode("Loading...")); // Add dummy node
                            }

                            projectNode.Nodes.Add(isoNode);
                        }
                        else
                        {
                            //AppendLog($"_ISO directory not found for project: {projectName}", ErrorColor);
                        }

                        // Add the project node to the TreeView
                        tvwProjects.Nodes.Add(projectNode);
                    }
                }

                // One line instead of one line per directory: this runs after every delete, copy and pack.
                if (projects == 0)
                {
                    AppendLog($"No projects found in {appDirectory}. Unpack an ISO to create one.", WarningColor);
                }
                else if (!string.Equals(appDirectory, GetAppDirectory(), StringComparison.OrdinalIgnoreCase))
                {
                    AppendLog($"Projects folder: {appDirectory}", InfoColor);
                }
                else
                {
                    AppendLog($"Loaded {projects} project(s) from {appDirectory}.", InfoColor);
                }

                // Put the selection and expansion back.
                foreach (TreeNode node in tvwProjects.Nodes)
                {
                    if (node.Tag is string path && expanded.Contains(path)) node.Expand();

                    if (node.Tag is string selectedPath && previousSelection is not null &&
                        selectedPath.Equals(previousSelection, StringComparison.OrdinalIgnoreCase))
                    {
                        tvwProjects.SelectedNode = node;
                    }
                    else if (previousSelectionText is not null && node.Text == previousSelectionText)
                    {
                        tvwProjects.SelectedNode = node;
                    }
                }
            }
            catch (Exception ex)
            {
                AppendLog($"Error loading projects: {ex.Message}", ErrorColor);
            }
            finally
            {
                tvwProjects.EndUpdate();
            }
        }


        // Helper method to check if a directory has subdirectories
        private bool HasSubDirectories(string path)
        {
            try
            {
                return Directory.GetDirectories(path).Length > 0;
            }
            catch (Exception ex)
            {
                AppendLog($"Error accessing directory '{path}': {ex.Message}", ErrorColor);
                return false;
            }
        }
        private bool HasFiles(string path)
        {
            try
            {
                return Directory.GetFiles(path).Length > 0;
            }
            catch (Exception ex)
            {
                AppendLog($"Error accessing files in directory '{path}': {ex.Message}", ErrorColor);
                return false;
            }
        }

        // Helper function to validate that the file path contains no parentheses
        private bool ValidateFilePathNoParentheses(string filepath)
        {
            if (filepath.Contains('(') || filepath.Contains(')'))
            {
                MessageBox.Show("File path cannot contain parentheses.", "Invalid Path", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            return true;
        }

        private bool IsProjectDirectory(string projectPath)
        {
            projectPath = projectPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string projectName = Path.GetFileName(projectPath);
            string isoPath = Path.Combine(projectPath, $"{projectName}_ISO");
            string becPath = Path.Combine(projectPath, $"{projectName}_BEC");
            bool isValid = Directory.Exists(isoPath) && Directory.Exists(becPath);

            // Optional: Log the validation result
            if (isValid)
            {
                //AppendLog($"Project '{projectName}' has both _ISO and _BEC directories.", SuccessColor);
            }
            else
            {
                //AppendLog($"Project '{projectName}' is missing required subdirectories.", ErrorColor);
            }

            return isValid;
        }

        /// <summary>
        /// Validates the existence of the specified files.
        /// </summary>
        private bool ValidateFilePath(string path, string description)
        {
            if (!File.Exists(path))
            {
                MessageBox.Show($"Please select a valid {description} file.", "Invalid File", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            return true;
        }
        private void AddMenuEntry(string projectFolder, string userInput)
        {
            // Ensure projectFolder has no trailing directory separators
            projectFolder = projectFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            // Path to main.mnu
            string menuFilePath = Path.Combine(projectFolder, $"{Path.GetFileName(projectFolder)}_BEC", "data", "menu", "main.mnu");

            // Check if the file exists
            if (!File.Exists(menuFilePath))
            {
                MessageBox.Show("Menu file not found. Please check the project structure.");
                return;
            }

            // Ensure userInput is sanitized for potential invalid characters
            userInput = userInput.Replace("\"", "\\\""); // Escape quotes to avoid breaking the entry

            // Entry to add, incorporating userInput
            string newEntry = $"ENTRY \"..\",    \"{userInput}\",    \"\"";

            // Read all lines from the file
            List<string> lines = File.ReadAllLines(menuFilePath).ToList();

            // Check if the entry already exists
            if (lines.Any(line => line.Trim() == newEntry))
            {
                // Entry already exists, no need to add it
                return;
            }

            // Add the new entry to the end of the file
            lines.Add(newEntry);

            // Write the updated lines back to the file
            File.WriteAllLines(menuFilePath, lines);
        }
        // Replaces the previous version: now reports determinate progress via pgbValidation
        // Incremental recompilation with progress + settings.json timestamp persistence
        // Incremental recompilation with progress + per-project settings beside the EXE
        private async Task RecompileAllScpAsync(string projectFolder)
        {
            projectFolder = projectFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string projectName = Path.GetFileName(projectFolder);
            string scriptFolder = Path.Combine(projectFolder, $"{projectName}_BEC", "data", "script");
            string binFolder = Path.Combine(scriptFolder, "bin");

            if (!Directory.Exists(scriptFolder))
            {
                AppendLog($"Script folder not found: {scriptFolder}", ErrorColor, rtbPackOutput);
                return;
            }

            if (!Directory.Exists(binFolder))
            {
                try
                {
                    Directory.CreateDirectory(binFolder);
                    AppendLog($"Created bin folder: {binFolder}", InfoColor, rtbPackOutput);
                }
                catch (Exception ex)
                {
                    AppendLog($"Failed to create bin folder: {ex.Message}", ErrorColor, rtbPackOutput);
                    return;
                }
            }

            // Tools path from EXE root (not CWD)
            string toolsPath = Path.Combine(GetAppDirectory(), "tools");
            string compilerPath = Path.Combine(toolsPath, "DOGCodeCompiler.exe");
            if (!File.Exists(compilerPath))
            {
                AppendLog($"Compiler not found at: {compilerPath}\nPlease use the tools folder from the latest release!", ErrorColor, rtbPackOutput);
                return;
            }

            // Gather all .scp files and their timestamps
            var scpFiles = Directory.GetFiles(scriptFolder, "*.scp", SearchOption.TopDirectoryOnly)
                                    .Select(p => new { Path = p, MtimeUtc = File.GetLastWriteTimeUtc(p) })
                                    .OrderBy(p => p.Path, StringComparer.OrdinalIgnoreCase)
                                    .ToList();

            if (scpFiles.Count == 0)
            {
                AppendLog("No .scp files found to compile.", WarningColor, rtbPackOutput);
                return;
            }

            // Load per-project cutoff from <EXE ROOT>\<ProjectName>\settings.json
            var projectSettings = LoadProjectSettings(projectFolder);
            DateTime cutoffUtc = projectSettings.LastScpRecompileUtc ?? DateTime.MinValue;

            // Snapshot latest modified time across all scripts (saved after run)
            DateTime latestScpWriteUtc = scpFiles.Max(f => f.MtimeUtc);

            // Only compile files edited after cutoff
            var changed = scpFiles.Where(f => f.MtimeUtc > cutoffUtc).ToList();

            if (changed.Count == 0)
            {
                AppendLog("Scripts are up-to-date. No changes since the last compile.", SuccessColor, rtbPackOutput);
                return;
            }

            // Progress bar: show during compile even if validation is skipped; then restore
            bool showBarTemporarily = chbValidationSkip.Checked;

            void ShowAndPrepProgress()
            {
                if (pgbValidation.InvokeRequired)
                {
                    pgbValidation.Invoke(new Action(ShowAndPrepProgress));
                    return;
                }
                if (showBarTemporarily)
                {
                    pgbValidation.Visible = true;
                    pgbValidation.Minimum = 0;
                    pgbValidation.Value = 0;
                    pgbValidation.Maximum = 0;
                }
                pgbValidation.Maximum += changed.Count;
                if (pgbValidation.Value > pgbValidation.Maximum)
                    pgbValidation.Value = pgbValidation.Maximum;
            }

            void HideProgressIfTemporary()
            {
                if (pgbValidation.InvokeRequired)
                {
                    pgbValidation.Invoke(new Action(HideProgressIfTemporary));
                    return;
                }
                if (showBarTemporarily)
                {
                    pgbValidation.Visible = false;
                    pgbValidation.Value = 0;
                    pgbValidation.Maximum = 0;
                }
            }

            ShowAndPrepProgress();

            AppendLog($"Starting incremental script recompilation... ({changed.Count} changed file(s))", InfoColor, rtbPackOutput);

            int ok = 0, fail = 0;

            // Compile only changed files, one-by-one (deterministic progress)
            foreach (var f in changed)
            {
                string scbPath = Path.Combine(binFolder, System.IO.Path.GetFileNameWithoutExtension(f.Path) + ".scb");
                string oneLineBatch = $"\"{compilerPath}\" \"{f.Path}\" \"{scbPath}\"";

                try
                {
                    await RunBatchFileAsync(oneLineBatch, scriptFolder, rtbPackOutput);
                    ok++;
                }
                catch (Exception ex)
                {
                    fail++;
                    AppendLog($"Failed compiling {System.IO.Path.GetFileName(f.Path)}: {ex.Message}", ErrorColor, rtbPackOutput);
                }
                finally
                {
                    IncrementProgressBar();
                }
            }

            // Persist new cutoff = latest script mtime at compile start
            try
            {
                projectSettings.LastScpRecompileUtc = latestScpWriteUtc;
                SaveProjectSettings(projectFolder, projectSettings);
                AppendLog($"Updated per-project compile cutoff to {latestScpWriteUtc:O} (UTC).", InfoColor, rtbPackOutput);
            }
            catch (Exception ex)
            {
                AppendLog($"Warning: failed saving per-project compile timestamp: {ex.Message}", WarningColor, rtbPackOutput);
            }

            AppendLog($"Recompile finished. Succeeded: {ok}, Failed: {fail}.", (fail == 0 ? SuccessColor : WarningColor), rtbPackOutput);

            HideProgressIfTemporary();
        }

        // Per-project settings path helpers
        private string GetProjectSettingsDir(string projectFolder)
        {
            string exeRoot = GetAppDirectory(); // already in Utilities.cs
            string projectName = Path.GetFileName(projectFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            return Path.Combine(exeRoot, projectName);
        }
        private string GetProjectSettingsPath(string projectFolder)
        {
            return Path.Combine(GetProjectSettingsDir(projectFolder), "settings.json");
        }

        private ProjectSettings LoadProjectSettings(string projectFolder)
        {
            try
            {
                string dir = GetProjectSettingsDir(projectFolder);
                Directory.CreateDirectory(dir); // ensure <EXE ROOT>\<ProjectName> exists
                string path = GetProjectSettingsPath(projectFolder);

                if (!File.Exists(path)) return new ProjectSettings();

                string json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json)) return new ProjectSettings();

                return JsonSerializer.Deserialize<ProjectSettings>(json) ?? new ProjectSettings();
            }
            catch
            {
                return new ProjectSettings();
            }
        }

        private void SaveProjectSettings(string projectFolder, ProjectSettings settings)
        {
            try
            {
                string dir = GetProjectSettingsDir(projectFolder);
                Directory.CreateDirectory(dir);
                string path = GetProjectSettingsPath(projectFolder);

                string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                AppendLog($"Warning: failed saving per-project settings: {ex.Message}", WarningColor, rtbPackOutput);
            }
        }

    }
}