using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Forms;
using ModdingGUI.Models;
using ModdingGUI.Core.Tools;
using ModdingGUI.Core.Logging;
using ModdingGUI.Core.Randomizer;
using ModdingGUI.Core;
using ModdingGUI.Core.Api;

namespace ModdingGUI
{
    // Partial class definition for the main form of the application
    public partial class frmMain : Form
    {
        // Grid-builder painter embedded in the tab
        private readonly GridBuilderView _gridBuilderView;

        // Skills editor embedded in the Skill Editor tab
        private readonly SkillEditorView _skillEditorView;

        // Constructor for the main form
        public frmMain()
        {
            InitializeComponent(); // Initialize form components

            ApplyLudoIcon();
            HideRetiredRandomizerTesting();

            // Hover help for every control the designer built. The Team Builder installs its own when it builds its
            // pages, because those controls only exist at runtime.
            _uiHelp.Install(this);

            // The pages are laid out at a fixed 1747x1212 from the designer, so on a 1080p screen the bottom of a tab
            // (Patching's "Create xDelta" panel, for one) was simply unreachable. Let them scroll instead of clipping.
            UiHelp.EnableTabScrolling(this);
            UiHelp.FixPackingFooter(this);

            // Check if Python 3 is installed on the system
            if (!IsPythonInstalled())
            {
                // Display a message box if Python 3 is not installed
                MessageBox.Show("Python is either not installed or not in PATH. Please fix this to use the program!\nhttps://www.python.org/downloads/ \nDuring install, check the box labelled 'Add python.exe to PATH'");
                Environment.Exit(0); // Exit the application
            }
            _gridBuilderView = new GridBuilderView { Log = _log };
            tabGridBuilder.Controls.Add(_gridBuilderView);

            _skillEditorView = new SkillEditorView();
            tabEditorSkills.Controls.Add(_skillEditorView);

            BuildBaselineControls();
            UiHelp.FixSidebarLayout(this);
            UiHelp.FixUnitSelectionLayout(this);
            PatchingPage.Build(this);
            BuildPages.Build(this);
            RandomizerPage.Build(this);

            // Right-click must move the tree selection, otherwise the context menu acts on a stale or missing node.
            tvwProjects.MouseDown += SelectNodeUnderCursor;

            tabContainer.SelectedIndexChanged += TabContainer_SelectedIndexChanged;
        }

        // Event handler for the Unpack button click event
        private async void btnUnpack_Click(object sender, EventArgs e)
        {
            btnUnpack.Enabled = false; // Disable the button to prevent multiple clicks
            string isoPath = txtISOPath.Text; // Get the ISO path from the text box

            // Parentheses are ordinary characters now: the tool is started directly with an argument list, so
            // there is no batch parser to confuse (PLAN.md Phase 7.9).

            // Get the user input for the folder name, trim whitespace, and replace spaces with underscores
            string userInput = txtUnpackPath.Text.Trim().Replace(" ", "_");

            // Added validation for empty userInput
            if (string.IsNullOrEmpty(userInput))
            {
                MessageBox.Show("Please enter a valid folder name.");
                btnUnpack.Enabled = true; // Re-enable the button to allow the user to correct their input
                return;
            }

            // Validate that the ISO file exists and the user input is not empty
            if (!ValidateFilePath(isoPath, "ISO"))
            {
                btnUnpack.Enabled = true; // Re-enable the button to allow the user to correct their input
                return;
            }

            AppendLog("Starting unpack operation...", InfoColor); // Log the start of the unpack operation

            try
            {
                // Create the project where the tree scans for them (the configured projects folder, else the app
                // folder). The tooling path stays app-relative, so the two are resolved separately.
                string topLevelFolder = Path.Combine(ProjectsFolder(), userInput);

                // Ensure the top-level folder exists
                if (!Directory.Exists(topLevelFolder))
                {
                    Directory.CreateDirectory(topLevelFolder);
                    AppendLog($"Top-level folder created: {topLevelFolder}", InfoColor); // Log the creation of the folder
                }

                // Normalize the path for the project tree and the grid builder
                string normalizedTopLevelFolder = PathHelper.NormalizePath(PathHelper.EnsureTrailingSeparator(topLevelFolder));

                // Run the unpack chain through Core (no batch file, output streamed into the log)
                BuildResult unpack = await RunUnpackAsync(isoPath, userInput);
                if (!unpack.Succeeded)
                {
                    AppendLog(
                        $"Unpacking failed at '{unpack.FirstFailure?.Name ?? "an unknown step"}' " +
                        $"(exit {unpack.ExitCode}). See the log above for the tool's output.",
                        ErrorColor);
                    btnUnpack.Enabled = true;
                    return;
                }

                AppendLog("Unpacking completed successfully.", SuccessColor); // Log successful completion

                // Update the pack path text box to point to the created unpack folder
                txtPackPath.Text = topLevelFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                txtRandomizerPath.Text = topLevelFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                txtPatchingCreationModISOPath.Text = PathHelper.NormalizePath(topLevelFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + $"/{userInput}.iso");
                txtPatchingCreationOutputPath.Text = PathHelper.NormalizePath(topLevelFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + $"/{userInput}.xdelta");
                btnRandomize.Enabled = true;
                btnPack.Enabled = true;
                btnUnpack.Enabled = true; // Re-enable the unpack button
                LoadProjects();
                _gridBuilderView.SetProjectRoot(normalizedTopLevelFolder);
            }
            catch (Exception ex)
            {
                // Log any exceptions that occur during unpacking
                AppendLog("Error during unpacking: " + ex.Message, ErrorColor);
            }
        }


        // Event handler for the Pack button click event
        private async void btnPack_Click(object sender, EventArgs e)
        {
            btnPack.Enabled = false; // Disable the button to prevent multiple clicks
            // Get the selected folder path from the text box and ensure it ends with a directory separator
            string selectedFolder = txtPackPath.Text;
            selectedFolder = PathHelper.NormalizePath(PathHelper.EnsureTrailingSeparator(selectedFolder));
            selectedFolder = selectedFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string projectName = Path.GetFileName(selectedFolder);
            string isoPath = Path.Combine(selectedFolder, $"{projectName}.iso");
            // Validate that the selected folder exists
            if (!Directory.Exists(selectedFolder))
            {
                MessageBox.Show("Please select a valid folder."); // Show an error message
                return; // Exit the method
            }
            if (!EnsureDolphinClosedForPack(isoPath))
            {
                return;
            }
            AppendLog("Starting pack operation...", InfoColor, rtbPackOutput); // Log the start of the pack operation

            try
            {
                // Advisory: reports what would break a pack, never stops it. The checkbox is the "don't even look"
                // escape hatch, for a project the user knows the validator will not like.
                if (!chbValidationSkip.Checked)
                {
                    RunPackValidation(selectedFolder);
                }
                else
                {
                    AppendLog("Validation skipped.", InfoColor, rtbPackOutput);
                }
                if (chbRecompileScripts.Checked)
                {
                    AppendLog("Recompiling all .scp files before packing...", InfoColor, rtbPackOutput);
                    await RecompileAllScpAsync(selectedFolder);
                }
                // Run the pack chain through Core (no batch file, output streamed into the log)
                BuildResult pack = await RunPackAsync(selectedFolder, rtbPackOutput);
                if (!pack.Succeeded)
                {
                    AppendLog(
                        $"Packing failed at '{pack.FirstFailure?.Name ?? "an unknown step"}' (exit {pack.ExitCode}). " +
                        "See the log above for the tool's output.",
                        ErrorColor, rtbPackOutput);
                    btnPack.Enabled = true;
                    return;
                }

                AppendLog("Packing completed successfully.", SuccessColor, rtbPackOutput); // Log successful completion
                btnRandomize.Enabled = true;
                btnToPatching.Enabled = true;
                btnPack.Enabled = true;
                // Follow the tickbox the user can see. The saved setting defaults to off, and the checkbox is the
                // live intent; checking the setting alone is what made every pack launch the emulator.
                if (chbAutoLaunchDolphin.Visible && chbAutoLaunchDolphin.Checked)
                {
                    LaunchGameWithDolphin(isoPath);
                }
            }
            catch (Exception ex)
            {
                // Log any exceptions that occur during packing
                AppendLog("Error during packing: " + ex.Message, ErrorColor, rtbPackOutput);
            }
        }


        // Event handler for the 'Select ISO' button click event
        private void btnSelectISO_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "ISO files (*.iso)|*.iso|All files (*.*)|*.*";
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    txtISOPath.Text = openFileDialog.FileName;
                }
            }
        }

        // Event handler for the 'Select Pack Path' button click event
        private void btnPackPath_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog())
            {
                if (folderBrowserDialog.ShowDialog() == DialogResult.OK)
                {
                    string selectedPath = folderBrowserDialog.SelectedPath;
                    txtPackPath.Text = selectedPath;
                    txtRandomizerPath.Text = selectedPath;
                    btnRandomize.Enabled = true;
                    btnPack.Enabled = true;
                }
            }
        }

        // Event handler for the 'Open Pack Location' button click event
        private void btnOpenPackLocation_Click(object sender, EventArgs e)
        {
            // The packing tab is about the selected project, not the unpack folder name.
            string project = txtPackPath.Text.Trim();
            OpenLocation(string.IsNullOrWhiteSpace(project) ? txtUnpackPath.Text : project);
        }

        // Event handler for the 'Open Unpack Location' button click event
        private void btnOpenUnpackLocation_Click(object sender, EventArgs e)
        {
            OpenLocation(txtUnpackPath.Text); // Open the unpack path location
        }

        private void frmMain_Load(object sender, EventArgs e)
        {
            try
            {
                LoadAppSettings(); // Apply application settings

            // The palette is a setting: apply it once the tree exists and the settings are known.
            ApplySavedTheme();

                // Record the process context once: a low-integrity (sandboxed) instance cannot drive the shell, and
                // knowing that up front saves chasing features that are blocked by it.
                AppendLog($"Running as: {ModdingGUI.Helpers.ProcessContext.Describe()}", InfoColor);
                if (ModdingGUI.Helpers.ProcessContext.IsLowIntegrity())
                {
                    AppendLog("This copy is running at LOW integrity (a sandbox started it). Windows blocks shell " +
                              "integration - opening folders, launching Dolphin - from here. Run the .exe directly.",
                        WarningColor);
                }

                // Check for updates at startup
                RunGuarded("Startup update check", CheckForUpdatesAtStartupAsync);

                // Retrieve the original executable directory using the helper method
                string appDirectory = GetAppDirectory();
                AppendLog($"App Directory: {appDirectory}", InfoColor);

                // No parenthesis check: tools are started with an argument list, so "C:\Mods (backup)\..." is as
                // valid as any other path (PLAN.md Phase 7.9).

                // Define the full path to the tools folder
                string toolsPath = Path.Combine(appDirectory, "tools");

                // Check for the existence of the tools folder
                if (!Directory.Exists(toolsPath))
                {
                    MessageBox.Show("Tools folder not found. Download modding tools v007 or higher from the Discord!", "Missing Tools Folder", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Environment.Exit(0); // Exit the application
                }

                // Proceed with loading the file list asynchronously. Fire-and-forget on purpose: this runs
                // during form load and the tree fills in when the scan finishes; the discard says so
                // instead of leaving the compiler to guess.
                RunGuarded("Loading the file list", LoadFileListAsync);

                // Remove specific tabs on load
                // tabContainer.TabPages.Remove(tabRandomizer);
                tabContainer.TabPages.Remove(tabIngameRandom);
                // tabContainer.TabPages.Remove(tabTeamBuilder);

                // Load projects
                LoadProjects();
                InitializeGameLauncher();
            }
            catch (Exception ex)
            {
                // Log the exception and notify the user
                AppendLog($"Error during application load: {ex.Message}", ErrorColor);
                MessageBox.Show($"An unexpected error occurred:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Environment.Exit(0); // Exit the application
            }
        }

        private async Task CheckForUpdatesAtStartupAsync()
        {
            if (!appSettings.AutoCheckUpdates) return;

            try
            {
                ProgramInfo programInfo = new ProgramInfo();
                var (updateAvailable, latestVersion) = await programInfo.CheckForUpdateAsync(silent: true);

                if (updateAvailable)
                {
                    DialogResult result = MessageBox.Show(
                        $"A new version ({latestVersion}) is available. Would you like to update now?",
                        "Update Available",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);

                    if (result == DialogResult.Yes)
                    {
                        await PerformUpdateAsync(latestVersion);
                    }
                }
            }
            catch (Exception ex)
            {
                AppendLog($"Error checking for updates: {ex.Message}", ErrorColor);
            }
        }

        private async Task PerformUpdateAsync(string latestVersion)
        {
            string downloadUrl = $"https://github.com/Gladius-Community-Devs/ModdingGUI/releases/download/{latestVersion}/ModdingGUI-{latestVersion}.zip";
            string savePath = Path.Combine(Application.StartupPath, $"ModdingGUI-{latestVersion}.zip");

            using (HttpClient client = new HttpClient())
            {
                try
                {
                    AppendLog($"Downloading update from {downloadUrl}...", InfoColor);
                    byte[] fileBytes = await client.GetByteArrayAsync(downloadUrl);
                    await File.WriteAllBytesAsync(savePath, fileBytes);
                    AppendLog($"Update downloaded successfully to {savePath}.", SuccessColor);

                    if (await InstallUpdateAsync(savePath, latestVersion))
                    {
                        MessageBox.Show(
                            $"Update to version {latestVersion} has been installed. The application will now close. Restart it to use the new features!",
                            "Update Complete",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                        RestartApplication();
                    }
                }
                catch (Exception ex)
                {
                    AppendLog($"Update failed: {ex.Message}", ErrorColor);
                    MessageBox.Show($"Failed to update: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

                    if (File.Exists(savePath))
                    {
                        try { File.Delete(savePath); } catch { /* Ignore cleanup errors */ }
                    }
                }
            }
        }

        private void randomizerMenuItem_Click(object sender, EventArgs e)
        {
            if (randomizerMenuItem.Checked)
            {
                tabContainer.TabPages.Add(tabRandomizer);
            }
            else
            {
                tabContainer.TabPages.Remove(tabRandomizer);
            }
        }

        private void teamBuilderToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (teamBuilderToolStripMenuItem.Checked)
            {
                // Check if the tab is already added to prevent duplicates
                if (!tabContainer.TabPages.Contains(tabTeamBuilder))
                {
                    tabContainer.TabPages.Add(tabTeamBuilder);
                    LoadClassesIntoDropdown();
                    InitializeTeamBuilderUI(); // Initialize the UI settings
                }
            }
            else
            {
                if (tabContainer.TabPages.Contains(tabTeamBuilder))
                {
                    tabContainer.TabPages.Remove(tabTeamBuilder);
                }
            }
        }

        private async void btnRandomizerPath_Click(object sender, EventArgs e)
        {
            try
            {
                // Check if a valid project path is already selected
                string currentPath = txtRandomizerPath.Text.Trim();
                bool hasValidProject = IsProjectFolder(currentPath);

                if (!hasValidProject)
                {
                    // No valid project selected, ask if user wants to select an ISO to randomize
                    var selectChoice = MessageBox.Show(
                        "No valid project selected. Do you want to select an ISO file to randomize?",
                        "Select ISO for Randomization",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);

                    if (selectChoice == DialogResult.Yes) // User wants to select an ISO file
                    {
                        // Open file dialog for ISO selection
                        using (OpenFileDialog openFileDialog = new OpenFileDialog())
                        {
                            openFileDialog.Filter = "ISO files (*.iso)|*.iso|All files (*.*)|*.*";
                            if (openFileDialog.ShowDialog() == DialogResult.OK)
                            {
                                string isoPath = openFileDialog.FileName;

                                // Parentheses no longer matter (Phase 7.9); only an empty selection is rejected.
                                if (!string.IsNullOrWhiteSpace(isoPath))
                                {
                                    // Ask user for project name
                                    string projectName = Path.GetFileNameWithoutExtension(isoPath);
                                    string userInput = Microsoft.VisualBasic.Interaction.InputBox(
                                        "Enter a name for the project folder:",
                                        "Project Name",
                                        projectName);

                                    if (string.IsNullOrEmpty(userInput))
                                    {
                                        MessageBox.Show("Please enter a valid folder name.");
                                        return;
                                    }

                                    // Replace spaces with underscores for consistency
                                    userInput = userInput.Trim().Replace(" ", "_");

                                    // Validate the ISO file exists
                                    if (!ValidateFilePath(isoPath, "ISO"))
                                    {
                                        return;
                                    }

                                    // Store the current tab to return to later
                                    TabPage currentTab = tabContainer.SelectedTab;

                                    // Switch to the unpack tab to show progress
                                    tabContainer.SelectedTab = tabUnpacking;

                                    AppendLog("Starting unpack operation...", InfoColor);

                                    try
                                    {
                                        // Populate the unpack tab fields to show what's being unpacked
                                        txtISOPath.Text = isoPath;
                                        txtUnpackPath.Text = userInput;

                                        // Same base as the unpack tab: the configured projects folder.
                                        string topLevelFolder = Path.Combine(ProjectsFolder(), userInput);

                                        // Ensure the top-level folder exists
                                        if (!Directory.Exists(topLevelFolder))
                                        {
                                            Directory.CreateDirectory(topLevelFolder);
                                            AppendLog($"Top-level folder created: {topLevelFolder}", InfoColor);
                                        }

                                        // Normalize the path for the project tree and the grid builder
                                        string normalizedTopLevelFolder = PathHelper.NormalizePath(PathHelper.EnsureTrailingSeparator(topLevelFolder));

                                        // Run the unpack chain through Core (no batch file, output streamed into the log)
                                        BuildResult unpack = await RunUnpackAsync(isoPath, userInput);
                                        if (!unpack.Succeeded)
                                        {
                                            AppendLog(
                                                $"Unpacking failed at '{unpack.FirstFailure?.Name ?? "an unknown step"}' " +
                                                $"(exit {unpack.ExitCode}).",
                                                ErrorColor);
                                            return;
                                        }

                                        AppendLog("Unpacking completed successfully.", SuccessColor);

                                        // Update the pack path text box to point to the created unpack folder
                                        txtPackPath.Text = topLevelFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                                        txtRandomizerPath.Text = topLevelFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                                        txtPatchingCreationModISOPath.Text = PathHelper.NormalizePath(topLevelFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + $"/{userInput}.iso");
                                        txtPatchingCreationOutputPath.Text = PathHelper.NormalizePath(topLevelFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + $"/{userInput}.xdelta");
                                        btnRandomize.Enabled = true;
                                        btnPack.Enabled = true;
                                        LoadProjects();

                                        // Show a message that the ISO was successfully unpacked
                                        MessageBox.Show("ISO unpacked successfully. You can now randomize the project.", "Unpacking Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);

                                        // Switch back to the randomizer tab
                                        tabContainer.SelectedTab = tabRandomizer;
                                    }
                                    catch (Exception ex)
                                    {
                                        AppendLog("Error during unpacking: " + ex.Message, ErrorColor);
                                        MessageBox.Show($"Error during unpacking: {ex.Message}", "Unpacking Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

                                        // Switch back to the randomizer tab even if there was an error
                                        tabContainer.SelectedTab = tabRandomizer;
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        // User doesn't want to select an ISO
                        MessageBox.Show("Please select a project from the sidebar or unpack an ISO in the Unpack tab.");
                    }
                }
                else
                {
                    // Project already selected, show folder browser to select a different project
                    using (FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog())
                    {
                        if (folderBrowserDialog.ShowDialog() == DialogResult.OK)
                        {
                            string selectedPath = folderBrowserDialog.SelectedPath;

                            // Check for the project layout, tolerating a folder that disappears mid-dialog.
                            if (IsProjectFolder(selectedPath))
                            {
                                txtRandomizerPath.Text = selectedPath;
                                btnRandomize.Enabled = true;
                                txtPackPath.Text = selectedPath;
                                btnPack.Enabled = true;

                                // Try to load randomizer settings for the selected project
                                LoadRandomizerSettings(selectedPath);
                            }
                            else
                            {
                                MessageBox.Show("Expected an unpacked project. Use the Unpack tab to create one, or ensure the selected folder contains _ISO and _BEC folders.");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AppendLog($"Randomizer path failed: {ex.Message}", ErrorColor);
            }
        }

        private Button? _btnResetToVanilla;
        private Label? _lblBaselineStatus;

        /// <summary>
        /// The reset controls live beside the Randomize button: the baseline is what makes repeat runs honest, and a
        /// modder needs to throw the randomization away without unpacking again.
        /// </summary>
        private void BuildBaselineControls()
        {
            _btnResetToVanilla = new Button
            {
                Name = "btnRandomizerReset",
                Text = "Reset to Vanilla",
                Location = new Point(200, 542),
                Size = new Size(150, 38),
                UseVisualStyleBackColor = true,
            };
            _btnResetToVanilla.Click += (_, _) => ResetToVanilla();
            tabRandomizer.Controls.Add(_btnResetToVanilla);

            _lblBaselineStatus = new Label
            {
                Name = "lblRandomizerBaseline",
                Text = "Baseline: not pinned yet",
                Location = new Point(370, 552),
                Size = new Size(430, 22),
                AutoEllipsis = true,
            };
            tabRandomizer.Controls.Add(_lblBaselineStatus);

            tabRandomizer.Enter += (_, _) => RefreshBaselineStatus();
            RefreshBaselineStatus();
        }

        /// <summary>
        /// Open the backup browser for the current project: the tree selection first, then the pack box.
        /// </summary>
        private void restoreFromBackupMenuItem_Click(object? sender, EventArgs e)
        {
            string project = tvwProjects.SelectedNode?.Tag as string ?? txtPackPath.Text.Trim();

            if (string.IsNullOrEmpty(project) || !Directory.Exists(project))
            {
                MessageBox.Show("Select a project in the list first.", "Restore from Backup",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var dialog = new ModdingGUI.Forms.HistoryForm(project);
            dialog.ShowDialog(this);

            // A restore rewrites files behind the editors' backs: reload them so nothing saves a stale copy.
            if (dialog.RestoredAnything)
            {
                AppendLog($"Restored from backup in '{Path.GetFileName(project)}'; reload any open editor.",
                    WarningColor);
                RefreshBaselineStatus();
            }
        }

        /// <summary>
        /// Files every editor needs. A folder can carry <c>_BEC</c>/<c>_ISO</c> and still be a partial unpack, and
        /// then the first editor to load throws from a background task.
        /// </summary>
        private static readonly string[] ProjectEssentials =
        {
            "data/config/items.tok",
            "data/config/classdefs.tok",
            "data/config/lookuptext_eng.txt",
        };

        /// <summary>True when the project has the files the editors read.</summary>
        private static bool IsCompleteProject(string? projectFolder)
        {
            if (string.IsNullOrWhiteSpace(projectFolder) || !Directory.Exists(projectFolder)) return false;

            string bec = Path.Combine(projectFolder, $"{Path.GetFileName(projectFolder.TrimEnd(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))}_BEC");

            if (!Directory.Exists(bec)) bec = projectFolder;

            return ProjectEssentials.All(relative => File.Exists(
                Path.Combine(bec, relative.Replace('/', Path.DirectorySeparatorChar))));
        }

        /// <summary>The essentials a project is missing, for a log line.</summary>
        private static IReadOnlyList<string> MissingProjectFiles(string? projectFolder)
        {
            if (string.IsNullOrWhiteSpace(projectFolder) || !Directory.Exists(projectFolder))
            {
                return ProjectEssentials;
            }

            string bec = Path.Combine(projectFolder, $"{Path.GetFileName(projectFolder.TrimEnd(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))}_BEC");

            if (!Directory.Exists(bec)) bec = projectFolder;

            return ProjectEssentials
                .Where(relative => !File.Exists(Path.Combine(bec, relative.Replace('/', Path.DirectorySeparatorChar))))
                .ToArray();
        }

        /// <summary>
        /// True when the folder holds a <c>_BEC</c> and an <c>_ISO</c> folder. Uses exact suffixes rather than a
        /// substring test, and tolerates the folder vanishing between the check and the enumeration.
        /// </summary>
        private static bool IsProjectFolder(string? folder)
        {
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) return false;

            try
            {
                var names = Directory.GetDirectories(folder).Select(Path.GetFileName).ToArray();
                return names.Any(name => name is not null && name.EndsWith("_BEC", StringComparison.OrdinalIgnoreCase))
                       && names.Any(name => name is not null && name.EndsWith("_ISO", StringComparison.OrdinalIgnoreCase));
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        /// <summary>Show whether this project has a pinned baseline and how many files it covers.</summary>
        private void RefreshBaselineStatus()
        {
            if (_lblBaselineStatus is null) return;

            try
            {
                string project = txtRandomizerPath.Text.Trim();
                if (string.IsNullOrEmpty(project) || !Directory.Exists(project))
                {
                    _lblBaselineStatus.Text = "Baseline: no project selected";
                    _lblBaselineStatus.ForeColor = WarningColor;
                    return;
                }

                var baseline = new RandomizerBaseline();
                string bec = ProjectRoots.ResolveBecFolder(project);

                if (!baseline.Exists(project))
                {
                    _lblBaselineStatus.Text = "Baseline: not pinned yet (the first run pins it)";
                    _lblBaselineStatus.ForeColor = WarningColor;
                    return;
                }

                int files = baseline.RandomizerFiles(bec).Count(path =>
                    File.Exists(Path.Combine(bec, path.Replace('/', Path.DirectorySeparatorChar))));

                _lblBaselineStatus.Text = $"Baseline: pinned ({files} files) - every run starts from it";
                _lblBaselineStatus.ForeColor = SuccessColor;
            }
            catch (Exception ex)
            {
                _lblBaselineStatus.Text = $"Baseline: {ex.Message}";
                _lblBaselineStatus.ForeColor = ErrorColor;
            }
        }

        /// <summary>Put the project back to the pinned vanilla state, no unpack required.</summary>
        private void ResetToVanilla()
        {
            string project = txtRandomizerPath.Text.Trim();
            if (string.IsNullOrEmpty(project) || !Directory.Exists(project))
            {
                MessageBox.Show("Select a valid project folder first.", "Reset to Vanilla",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var baseline = new RandomizerBaseline();
            if (!baseline.Exists(project))
            {
                MessageBox.Show(
                    "This project has no pinned baseline yet. Run the randomizer once and it will pin the state " +
                    "it started from.",
                    "Reset to Vanilla", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // UiConfirm logs and answers itself when the window is driven by a script, instead of leaving a modal box
            // nobody is there to click (which hangs the run).
            if (!UiConfirm(
                    "Restore every file the randomizer touches to the pinned vanilla state? " +
                    "The current files are backed up first, so this is undoable from .mod-history.",
                    "Reset to Vanilla"))
            {
                return;
            }

            try
            {
                var result = baseline.Restore(project);
                AppendLog($"Reset to vanilla: {result.Files} file(s) restored.", SuccessColor, rtbPackOutput);
                RefreshBaselineStatus();
            }
            catch (Exception ex)
            {
                AppendLog($"Reset failed: {ex.Message}", ErrorColor, rtbPackOutput);
                MessageBox.Show($"Reset failed:\n\n{ex.Message}", "Reset to Vanilla",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Build <see cref="RandomizerOptions"/> from the panel. Every control here is a parameter for Core; the
        /// shell itself no longer edits a single game file.
        /// </summary>
        private RandomizerOptions ReadRandomizerOptions()
        {
            var cash = CashMode.Unchanged;
            int cashAmount = 15000;

            if (chbRandomCustomCash.Checked)
            {
                if (txtRandomCustomCash.Text.Equals("RANDOM", StringComparison.OrdinalIgnoreCase))
                {
                    cash = CashMode.Random;
                }
                else if (int.TryParse(txtRandomCustomCash.Text, out int parsed)
                         && ModdingGUI.Core.Units.InputRules.IsValidGold(parsed))
                {
                    cash = CashMode.Fixed;
                    cashAmount = parsed;
                }
            }

            return new RandomizerOptions
            {
                Seed = txtSeed.Text,
                Hero = rbnValens.Checked ? HeroChoice.Valens : HeroChoice.Ursula,
                Version = rbnRagnaroks.Checked
                    ? GameVersion.Ragnaroks
                    : rbnLeonarths.Checked ? GameVersion.Leonarths : GameVersion.Vanilla,
                // The old randomizer treated an empty box as "Random's School"; keep that so the roster header
                // matches what modders are used to seeing.
                SchoolName = string.IsNullOrWhiteSpace(txtRandomCustomSchoolName.Text)
                    ? "Random's School"
                    : txtRandomCustomSchoolName.Text.Trim(),
                RandomizeHeroes = chbRandomHeroes.Checked,
                RandomizeTeam = chbRandomTeam.Checked,
                RemoveRecruits = chbRandomNoRecruits.Checked,
                RandomizeStatsets = chbRandomStatsets.Checked,
                RandomizeItemsets = chbRandomItemsets.Checked,
                RandomizeEnemies = chbRandomizedEnemies.Checked,
                PermaDeath = chbRandomPermaDeath.Checked,
                FortyGladiators = chbRandom40Glads.Checked,
                WeightedClasses = chbRandomWeighted.Checked,
                Cash = cash,
                CashAmount = cashAmount,
            };
        }

        private async void btnRandomize_Click(object sender, EventArgs e)
        {
            btnRandomize.Enabled = false;

            try
            {
                string projectFolder = txtRandomizerPath.Text.Trim();
                if (string.IsNullOrEmpty(projectFolder) || !Directory.Exists(projectFolder))
                {
                    MessageBox.Show("Please select a valid project folder.", "Invalid Path",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    AppendLog("Invalid project folder selected.", WarningColor, rtbPackOutput);
                    return;
                }

                SaveRandomizerSettings(projectFolder);

                var options = ReadRandomizerOptions();

                // A quote or line break in the school name would corrupt the roster header.
                var schoolNameProblems = ModdingGUI.Core.Units.InputRules.ValidateSchoolName(options.SchoolName);
                if (schoolNameProblems.Count > 0)
                {
                    MessageBox.Show(string.Join(Environment.NewLine, schoolNameProblems), "School Name",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    btnRandomize.Enabled = true;
                    lblRandomizeStatus.Text = "Ready";
                    return;
                }
                if (options.IsEmpty)
                {
                    AppendLog("Nothing selected: pick at least one randomizer option.", WarningColor, rtbPackOutput);
                    // Re-enable before leaving: the method disabled the button on entry, and returning without this left
                    // it dead until the app was restarted.
                    btnRandomize.Enabled = true;
                    lblRandomizeStatus.Text = "Ready";
                    return;
                }

                lblRandomizeStatus.Text = "Randomizing...";
                pgbRandomizeStatus.Style = ProgressBarStyle.Marquee;
                pgbRandomizeStatus.MarqueeAnimationSpeed = 30;
                AppendLog($"Randomizing with seed '{options.Seed}'.", InfoColor, rtbPackOutput);

                // Core logs as it works; mirror those entries into the panel while the run is going. The entries
                // are rendered with AppendLogText, never AppendLog: AppendLog writes back into the same log, which
                // raised Written again and pinned the UI thread in an endless loop.
                var pending = new ConcurrentQueue<LogEntry>();
                int draining = 0;

                void Drain()
                {
                    Interlocked.Exchange(ref draining, 0);
                    while (pending.TryDequeue(out LogEntry entry))
                    {
                        AppendLogText(entry.Message, ColorFor(entry.Level), rtbPackOutput);
                    }
                }

                void Mirror(object? sender, LogEntry entry)
                {
                    pending.Enqueue(entry);
                    if (IsDisposed || IsHandleCreated is false) return;

                    // One queued drain at a time, so a chatty step cannot flood the message pump.
                    if (Interlocked.CompareExchange(ref draining, 1, 0) != 0) return;

                    try
                    {
                        BeginInvoke(Drain);
                    }
                    catch (InvalidOperationException)
                    {
                        // The form went away mid-run; the log file still has everything.
                    }
                }

                var log = Output;
                log.Written += Mirror;

                try
                {
                    var result = await Task.Run(() => new RandomizerRunner(log: log).Run(projectFolder, options));

                    AppendLog($"Seed: {result.Randomize.Seed}", SuccessColor, rtbPackOutput);
                    AppendLog($"Steps: {string.Join(", ", result.Randomize.Steps)}", InfoColor, rtbPackOutput);
                    AppendLog($"Records changed: {result.Randomize.ChangedRecords}", SuccessColor, rtbPackOutput);

                    // Patched campaign scripts have to be compiled, or the edits never reach the game.
                    if (result.Randomize.ModifiedScripts.Count > 0)
                    {
                        var build = new BuildOptions(
                            projectFolder, Path.Combine(GetAppDirectory(), "tools"), "python");
                        var compiled = await Task.Run(() => new ProjectBuildService(log)
                            .CompileScripts(build, result.Randomize.ModifiedScripts));

                        AppendLog(compiled.Succeeded
                                ? $"Compiled {result.Randomize.ModifiedScripts.Count} campaign script(s)."
                                : "Script compilation failed - pack anyway?",
                            compiled.Succeeded ? SuccessColor : ErrorColor,
                            rtbPackOutput);
                    }

                    AddMenuEntry(projectFolder, "Game is Randomized!");
                }
                finally
                {
                    log.Written -= Mirror;
                    pending.Clear();
                }

                lblRandomizeStatus.Text = "Ready";
                AppendLog("Randomization completed.", SuccessColor, rtbPackOutput);
                MessageBox.Show("Click the Pack button to build your ISO!", "Packing step",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                tabContainer.SelectedTab = tabPacking;
                await FlashButtonAsync(btnPack, Color.LightGreen, 10, 300);
            }
            catch (Exception ex)
            {
                AppendLog($"Randomization failed: {ex.Message}", ErrorColor, rtbPackOutput);
                MessageBox.Show($"Randomization failed:\n\n{ex.Message}", "Randomizer",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                pgbRandomizeStatus.Style = ProgressBarStyle.Blocks;
                pgbRandomizeStatus.Value = 0;
                lblRandomizeStatus.Text = "Ready";
                btnRandomize.Enabled = true;
            }
        }

        /// <summary>Log level to the panel's colour convention.</summary>
        private static Color ColorFor(LogLevel level) => level switch
        {
            LogLevel.Error => ErrorColor,
            LogLevel.Warning => WarningColor,
            LogLevel.Success => SuccessColor,
            _ => InfoColor,
        };

        private void txtUnpackPath_Click(object sender, EventArgs e)
        {
            var textBox = (TextBox)sender;
            textBox.SelectAll();
            textBox.Focus();
        }

        private void chbValidationSkip_CheckedChanged(object sender, EventArgs e)
        {
            /*if (!chbValidationSkip.Checked)
            {
                var result = MessageBox.Show("Are you sure you want to enable validation?\nThis is typically for Modders to double check their work!", "Confirmation", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (result == DialogResult.No)
                {
                    chbValidationSkip.Checked = true;
                    return;
                }
                pgbValidation.Visible = true;
            }*/
        }

        private void chbIngameRandom_CheckedChanged(object sender, EventArgs e)
        {
            if (chbIngameRandom.Checked)
            {
                if (chbRandomTeam.Checked)
                {
                    chbRandomTeam.Checked = false;
                }
                chbRandomTeam.Enabled = false;
            }
            else
            {
                chbRandomTeam.Enabled = true;
            }
        }

        private void chbIngameRandom_MouseHover(object sender, EventArgs e)
        {
            ttpInform.SetToolTip(chbIngameRandom, "Randomizes your team whenever you back out of a town's main screen to the shop and school selection menu.\nMake sure you are ready before choosing a fight!\nDisables whole random team.");
        }

        private void tvwProjects_BeforeExpand(object sender, TreeViewCancelEventArgs e)
        {
            TreeNode node = e.Node;

            // If the node has a dummy child, load actual children
            if (node.Nodes.Count == 1 && node.Nodes[0].Text == "Loading...")
            {
                node.Nodes.Clear(); // Remove the dummy node

                string path = node.Tag as string;
                if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
                    return;

                try
                {
                    // Load subdirectories
                    string[] subDirs = Directory.GetDirectories(path);
                    foreach (var subDir in subDirs)
                    {
                        string subDirName = Path.GetFileName(subDir);
                        TreeNode subDirNode = new TreeNode(subDirName)
                        {
                            Tag = subDir,
                            ImageKey = "folder",
                            SelectedImageKey = "folder"
                        };

                        // Check if the subdirectory has further subdirectories or files
                        if (HasSubDirectories(subDir) || HasFiles(subDir))
                        {
                            subDirNode.Nodes.Add(new TreeNode("Loading...")); // Add dummy node
                        }

                        node.Nodes.Add(subDirNode);
                    }

                    // Load files
                    string[] files = Directory.GetFiles(path);
                    foreach (var file in files)
                    {
                        string fileName = Path.GetFileName(file);
                        TreeNode fileNode = new TreeNode(fileName)
                        {
                            Tag = file,
                            ImageKey = "file",
                            SelectedImageKey = "file"
                        };
                        node.Nodes.Add(fileNode);
                    }
                }
                catch (Exception ex)
                {
                    AppendLog($"Error expanding node '{node.Text}': {ex.Message}", ErrorColor);
                }
            }
        }
        private void tvwProjects_NodeMouseHover(object sender, TreeNodeMouseHoverEventArgs e)
        {
            if (e.Node != null && e.Node.Tag is string path && File.Exists(path))
            {
                tvwProjects.Cursor = Cursors.Hand;
                ttpInform.SetToolTip(tvwProjects, path);
            }
            else
            {
                tvwProjects.Cursor = Cursors.Default;
            }
        }

        private void tvwProjects_NodeMouseDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            TreeNode selectedNode = e.Node;

            if (selectedNode != null && selectedNode.Tag is string path && File.Exists(path))
            {
                try
                {
                    // Open the file with the default associated application
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo()
                    {
                        FileName = path,
                        UseShellExecute = true // Ensures the OS uses the default application
                    });
                }
                catch (Exception ex)
                {
                    AppendLog($"Error opening file '{path}': {ex.Message}", ErrorColor);
                    MessageBox.Show($"Unable to open the file.\n\nError: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void tvwProjects_AfterSelect(object sender, TreeViewEventArgs e)
        {
            try
            {
                TreeNode selectedNode = e.Node;

                // Check if the selected node is a top-level project node
                if (selectedNode.Parent == null)
                {
                    string projectPath = selectedNode.Tag as string;
                    if (!string.IsNullOrEmpty(projectPath) && Directory.Exists(projectPath))
                    {
                        projectPath = PathHelper.NormalizePath(projectPath);
                        string projectName = Path.GetFileName(projectPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                        txtPackPath.Text = projectPath;
                        txtRandomizerPath.Text = projectPath;
                        txtPatchingCreationOutputPath.Text = projectPath + $"/{projectName}.xDelta";
                        txtPatchingCreationModISOPath.Text = projectPath + $"/{projectName}.iso";
                        txtUnpackPath.Text = projectName;
                        btnPack.Enabled = true;
                        btnRandomize.Enabled = true;
                        _gridBuilderView.SetProjectRoot(projectPath);

                        // Try to load randomizer settings for the selected project
                        LoadRandomizerSettings(projectPath);

                        if (IsCompleteProject(projectPath))
                        {
                            InitialiseItemEditor(projectPath);
                            _skillEditorView.InitializeEditor(projectPath);
                        }
                        else
                        {
                            // A partial unpack still appears in the tree; say why the editors stay empty instead of
                            // letting a background load throw.
                            var missing = MissingProjectFiles(projectPath);
                            AppendLog($"'{projectName}' is an incomplete project; editors not loaded. Missing: " +
                                      string.Join(", ", missing) + ".", WarningColor);
                            ClearEditorState();
                            _skillEditorView.ReleaseProject();
                        }
                    }
                    else
                    {
                        txtPackPath.Text = string.Empty;
                    }
                    LoadClassesIntoDropdown();
                    InitializeTeamBuilderUI();
                }
                else
                {
                    // Optional: Clear txtPackPath or handle selections of sub-nodes
                    // txtPackPath.Text = string.Empty;
                }
            }
            catch (Exception ex)
            {
                // Selecting a project drives several editors; a bad project must never take the window down.
                AppendLog($"Selecting a project failed: {ex.Message}", ErrorColor);
            }
        }

        private void txtTeamLevel_TextChanged(object sender, EventArgs e)
        {
            if (int.TryParse(txtTeamLevel.Text, out int value))
            {
                if (value >= 1 && value <= 30)
                {
                    // Valid input
                    lblTeamLevel.ForeColor = UiTheme.Success; // Highlight in green
                    UpdatePreview();
                }
                else
                {
                    // Invalid input
                    txtTeamLevel.Clear(); // Clear invalid input
                    txtTeamLevel.Focus(); // Refocus on the control
                    lblTeamLevel.Text = "Please enter a number between 1 and 30.";
                    lblTeamLevel.ForeColor = UiTheme.Error; // Highlight in red
                }
            }
            else if (!string.IsNullOrEmpty(txtTeamLevel.Text)) // Handle non-numeric cases
            {
                txtTeamLevel.Clear(); // Clear invalid input
                txtTeamLevel.Focus(); // Refocus on the control
                lblTeamLevel.Text = "Please enter a number between 1 and 30.";
                lblTeamLevel.ForeColor = UiTheme.Error; // Highlight in red
            }
            else
            {
                // Clear the message and reset color if the input is empty
                lblTeamLevel.Text = "";
                lblTeamLevel.ForeColor = UiTheme.Ink;
            }
        }

        private void txtTeamLevel_Leave(object sender, EventArgs e)
        {
            if (int.TryParse(txtTeamLevel.Text, out int value))
            {
                if (value >= 1 && value <= 30)
                {
                    // Valid input
                    lblTeamLevel.Text = "Team level selected:";
                    lblTeamLevel.ForeColor = UiTheme.Success; // Highlight in green
                }
                else
                {
                    // Invalid input
                    txtTeamLevel.Clear(); // Clear invalid input
                    lblTeamLevel.Text = "Input a team level. (All units will have this level):";
                    lblTeamLevel.ForeColor = UiTheme.Ink; // Highlight in red
                }
            }
            else
            {
                txtTeamLevel.Clear(); // Clear invalid input
                lblTeamLevel.Text = "Input a team level. (All units will have this level):";
                lblTeamLevel.ForeColor = UiTheme.Ink; // Highlight in red
            }
        }

        private void rbnTeamCampaign_CheckedChanged(object sender, EventArgs e)
        {
            if (rbnTeamCampaign.Checked)
            {
                // A campaign unit is created at whatever level the designer picks (1-30). Locking this to 1 made the
                // level field look broken, since PvP — the only mode that used to unlock it — is not implemented.
                if (string.IsNullOrWhiteSpace(txtTeamLevel.Text)) txtTeamLevel.Text = "1";
                txtTeamLevel.Enabled = true;

                // Update equipment rules visibility
                UpdateEquipmentRulesVisibility(false);

                // Update unit count and button states
                UpdateUnitCountAndButtons();
            }
        }

        private void chbTeamEquipRestrict_CheckedChanged(object sender, EventArgs e)
        {
            if (chbTeamEquipRestrict.Checked)
            {
                var result = MessageBox.Show("Are you sure you want to remove equipment restrictions? This will invalidate your ISO for usage in a PvP setup unless agreed upon by all participants.\nJust by checking this box, your ISO will be marked. Do not do this unless you are absolutely sure.", "Confirmation", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (result == DialogResult.No)
                {
                    chbTeamEquipRestrict.Checked = false;
                }
                else
                {
                    AddMenuEntry(txtPackPath.Text, "Equipment Restrictions Removed");
                    chbTeamEquipRestrict.Enabled = false;
                }
            }
        }

        private void chbRandomizedEnemies_MouseHover(object sender, EventArgs e)
        {
            ttpInform.SetToolTip(chbRandomizedEnemies, "Randomizes the enemies you encounter in the game. This includes the enemies in the main story, side quests, and random encounters.\nSpecific enemies will not randomize. Levels are randomized -3 to 3 above school average level");
        }

        private void chbRandomNoRecruits_MouseHover(object sender, EventArgs e)
        {
            ttpInform.SetToolTip(chbRandomNoRecruits, "Removes all recruits from the game. This includes the recruits you can hire in towns.\nThis will not remove the recruits you have already hired.");
        }

        private void chbRandomPermaDeath_MouseHover(object sender, EventArgs e)
        {
            ttpInform.SetToolTip(chbRandomPermaDeath, "Sets all fights to be perma-death. Some 'hero' level characters will not die. Main character death is Game Over.");
        }

        private void chbRandomHeroes_MouseHover(object sender, EventArgs e)
        {
            ttpInform.SetToolTip(chbRandomHeroes, "Randomizes your heroes. You start with all 6 heroes. Gwazi and Ludo will not leave the team.");
        }

        private void chbRandomTeam_MouseHover(object sender, EventArgs e)
        {
            ttpInform.SetToolTip(chbRandomTeam, "Randomizes your team fully with 14 units. This leaves space for your heroes.");
        }

        private void txtSeed_MouseHover(object sender, EventArgs e)
        {
            ttpInform.SetToolTip(txtSeed, "Sets the seed for the randomizer. This will allow you to replay the same randomization or share it with friends!");
        }


        private void btnToPatching_Click(object sender, EventArgs e)
        {
            tabContainer.SelectedTab = tabPatching;
        }

        private void tvwxdeltaFiles_NodeMouseHover(object sender, TreeNodeMouseHoverEventArgs e)
        {
            ttpInform.SetToolTip(tvwxdeltaFiles, "Double click a file to start the download!");
        }

        private void chbRandomStatsets_MouseHover(object sender, EventArgs e)
        {
            ttpInform.SetToolTip(chbRandomStatsets, "Randomizes the statsets of all enemy units in the game. WARNING: this makes the game very unpredictable!!");
        }

        private void chbRandomItemsets_MouseHover(object sender, EventArgs e)
        {
            ttpInform.SetToolTip(chbRandomItemsets, "Randomizes the itemsets of all enemy units in the game. WARNING: visual glitches may appear!");
        }
        private void chbRecompileScripts_MouseHover(object sender, EventArgs e)
        {
            ttpInform.SetToolTip(chbRecompileScripts, "Recompiles all modified .scp files to ensure changes take effect.");
        }

        private void chbRandom40Glads_CheckedChanged(object sender, EventArgs e)
        {
            if (chbRandom40Glads.Checked)
            {
                var result = MessageBox.Show(
                    "This feature requires the '40 Gladiators' Gecko code to be enabled.\n" +
                    "Without this code, the game will crash.\n\n" +
                    "Do you already have this Gecko code enabled?",
                    "Gecko Code Required",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

                if (result == DialogResult.No)
                {
                    chbRandom40Glads.Checked = false;
                }
            }
        }

        private void chbRandom40Glads_MouseHover(object sender, EventArgs e)
        {
            ttpInform.SetToolTip(chbRandom40Glads, "Enables 40 gladiators. REQUIRES the '40 Gladiators' Gecko code to be enabled.\nWithout the code, the game will crash.");
        }
        private async Task FlashButtonAsync(Button button, Color flashColor, int flashCount = 3, int flashDuration = 300)
        {
            Color originalColor = button.BackColor;
            bool originalUseVisualStyleBackColor = button.UseVisualStyleBackColor;

            try
            {
                for (int i = 0; i < flashCount; i++)
                {
                    // Flash on
                    button.UseVisualStyleBackColor = false;
                    button.BackColor = flashColor;
                    await Task.Delay(flashDuration);

                    // Flash off
                    button.UseVisualStyleBackColor = originalUseVisualStyleBackColor;
                    button.BackColor = originalColor;
                    await Task.Delay(flashDuration / 2);
                }
            }
            finally
            {
                // Ensure the button returns to its original state
                button.UseVisualStyleBackColor = originalUseVisualStyleBackColor;
                button.BackColor = originalColor;
            }
        }

        /// <summary>
        /// The old randomizer test harness drove the shell's pipeline N times; that pipeline is gone, so the item
        /// is hidden rather than left pointing at deleted code.
        /// </summary>
        private void HideRetiredRandomizerTesting()
        {
            randomizerTestingMenuItem.Visible = false;
            randomizerTestingMenuItem.Enabled = false;
        }

        private void chbRandomMaxMoney_MouseHover(object sender, EventArgs e)
        {
            ttpInform.SetToolTip(chbRandomCustomCash, "Customize your starting cash! Try: RANDOM\nDefault: 15000\nDefault 40 glads: 75000");
        }


        private void chbRandomCustomCash_CheckedChanged(object sender, EventArgs e)
        {
            txtRandomCustomCash.Visible = chbRandomCustomCash.Checked;
            if (chbRandomCustomCash.Checked)
            {
                // Set default value if empty
                if (string.IsNullOrWhiteSpace(txtRandomCustomCash.Text))
                {
                    txtRandomCustomCash.Text = "15000";
                }
            }
        }

        // Add validation to only allow numeric input (0-9) or the word "RANDOM"
        private void txtRandomCustomCash_KeyPress(object sender, KeyPressEventArgs e)
        {
            // Allow control keys (backspace, delete, etc)
            if (char.IsControl(e.KeyChar))
                return;

            // Check if we're typing "RANDOM"
            string currentText = txtRandomCustomCash.Text;
            string potentialText = currentText.Substring(0, txtRandomCustomCash.SelectionStart) +
                                   e.KeyChar +
                                   currentText.Substring(txtRandomCustomCash.SelectionStart + txtRandomCustomCash.SelectionLength);

            if (potentialText.Equals("RANDOM", StringComparison.OrdinalIgnoreCase) ||
                "RANDOM".StartsWith(potentialText, StringComparison.OrdinalIgnoreCase))
            {
                // Allow typing "RANDOM"
                return;
            }

            // If we already have "RANDOM", only allow overwriting it completely
            if (currentText.Equals("RANDOM", StringComparison.OrdinalIgnoreCase) &&
                txtRandomCustomCash.SelectionLength < currentText.Length)
            {
                e.Handled = true;
                return;
            }

            // Otherwise only allow digits
            if (!char.IsDigit(e.KeyChar))
            {
                e.Handled = true;
            }
            else
            {
                // Check if adding this digit would exceed the max value
                if (potentialText.Length > 0)
                {
                    if (long.TryParse(potentialText, out long value))
                    {
                        if (value > 999999999)
                        {
                            e.Handled = true;
                        }
                    }
                }
            }
        }

        private void txtRandomCustomCash_Validating(object sender, System.ComponentModel.CancelEventArgs e)
        {
            string text = txtRandomCustomCash.Text.Trim();

            // Allow "RANDOM" string
            if (string.Equals(text, "RANDOM", StringComparison.OrdinalIgnoreCase))
            {
                // Normalize to uppercase
                txtRandomCustomCash.Text = "RANDOM";
                return;
            }

            // Check for valid numeric value
            if (string.IsNullOrEmpty(text) || !int.TryParse(text, out int value) || value < 0 || value > 999999999)
            {
                txtRandomCustomCash.Text = "15000"; // Default value
            }
        }

        // Add tooltip for the chbRandomWeighted checkbox
        private void chbRandomWeighted_MouseHover(object sender, EventArgs e)
        {
            ttpInform.SetToolTip(chbRandomWeighted, "When selected, team randomization will prioritize class diversity by adding new classes before creating duplicate units with the same class.");
        }

        // Add to Form1.cs
        private void infoMenuItem_Click(object sender, EventArgs e)
        {
            var programInfo = new Models.ProgramInfo();
            var infoForm = new Forms.ProgramInfoForm(programInfo);
            infoForm.ShowDialog();
        }

        private async void updateMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                ProgramInfo programInfo = new ProgramInfo();
                var (updateAvailable, latestVersion) = await programInfo.CheckForUpdateAsync();

                if (updateAvailable)
                {
                    DialogResult result = MessageBox.Show(
                        $"A new version ({latestVersion}) is available. Would you like to update?",
                        "Update Available",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);
                    if (result == DialogResult.Yes)
                    {
                        await PerformUpdateAsync(latestVersion);
                    }
                }
                else if (latestVersion != null)
                {
                    MessageBox.Show(
                        "You are already using the latest version.",
                        "No Update Available",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                AppendLog($"Update check failed: {ex.Message}", ErrorColor);
            }
        }

        /// <summary>
        /// Extracts and installs the update from the downloaded zip file
        /// </summary>
        /// <param name="zipFilePath">Path to the downloaded zip file</param>
        /// <param name="version">Version being installed</param>
        /// <returns>True if installation succeeds, false otherwise</returns>
        /// <summary>
        /// Extracts and installs the update from the downloaded zip file
        /// </summary>
        /// <param name="zipFilePath">Path to the downloaded zip file</param>
        /// <param name="version">Version being installed</param>
        /// <returns>True if installation succeeds, false otherwise</returns>
        private async Task<bool> InstallUpdateAsync(string zipFilePath, string version)
        {
            string tempExtractPath = Path.Combine(Path.GetTempPath(), $"ModdingGUI-Update-{Guid.NewGuid()}");
            string appDirectory = Application.StartupPath;
            string backupPath = Path.Combine(Path.GetTempPath(), $"ModdingGUI-Backup-{DateTime.Now:yyyyMMdd_HHmmss}");

            // Backup the current settings file before doing anything else
            string settingsPath = Path.Combine(appDirectory, APP_SETTINGS_FILENAME);
            string settingsBackupPath = Path.Combine(Path.GetTempPath(), $"ModdingGUI-Settings-Backup-{Guid.NewGuid()}.json");
            bool hasSettings = File.Exists(settingsPath);

            try
            {
                // Backup current settings if they exist
                if (hasSettings)
                {
                    File.Copy(settingsPath, settingsBackupPath, true);
                    AppendLog($"Backed up settings to temporary location.", InfoColor);
                }

                // Create temp directory for extraction
                AppendLog($"Extracting update to temporary location...", InfoColor);
                Directory.CreateDirectory(tempExtractPath);

                // Extract the zip file
                System.IO.Compression.ZipFile.ExtractToDirectory(zipFilePath, tempExtractPath, true);

                // Delete the settings.json from extracted files to prevent overwriting user settings
                string extractedSettingsPath = Path.Combine(tempExtractPath, APP_SETTINGS_FILENAME);
                if (File.Exists(extractedSettingsPath))
                {
                    File.Delete(extractedSettingsPath);
                    AppendLog($"Removed default settings from update package to preserve user settings.", InfoColor);
                }

                // Create a backup of the current installation
                AppendLog($"Creating backup of current installation...", InfoColor);
                DirectoryCopy(appDirectory, backupPath, true, new[] { Path.GetFileName(zipFilePath) });

                // Write a batch file that will:
                // 1. Wait for our process to exit
                // 2. Copy files from temp location to app directory
                // 3. Restore the settings file if it was backed up
                // 4. Delete the temp directory and zip file
                // 5. Start the application again
                string batchFile = Path.Combine(Path.GetTempPath(), $"ModdingGUI-Update-{Guid.NewGuid()}.bat");

                using (StreamWriter writer = new StreamWriter(batchFile))
                {
                    // Wait for our process to exit
                    writer.WriteLine("@echo off");
                    writer.WriteLine("echo Updating ModdingGUI, please wait...");
                    writer.WriteLine($"timeout /t 2 /nobreak >nul");

                    // Wait for process to fully exit
                    writer.WriteLine($"set /a counter=0");
                    writer.WriteLine(":wait_loop");
                    writer.WriteLine($"tasklist | find \"{System.Diagnostics.Process.GetCurrentProcess().ProcessName}\" >nul");
                    writer.WriteLine("if %errorlevel% equ 0 (");
                    writer.WriteLine("    set /a counter+=1");
                    writer.WriteLine("    if %counter% gtr 10 (");
                    writer.WriteLine("        echo Update failed: Application is still running after 10 seconds.");
                    writer.WriteLine("        pause");
                    writer.WriteLine("        exit /b 1");
                    writer.WriteLine("    )");
                    writer.WriteLine("    timeout /t 1 /nobreak >nul");
                    writer.WriteLine("    goto wait_loop");
                    writer.WriteLine(")");

                    // Copy files from temp location to app directory
                    writer.WriteLine($"xcopy \"{tempExtractPath}\\*\" \"{appDirectory}\" /E /H /C /I /Y");

                    // After copying all files, restore settings from backup if it exists
                    if (hasSettings)
                    {
                        writer.WriteLine($"echo Restoring user settings...");
                        writer.WriteLine($"copy /Y \"{settingsBackupPath}\" \"{settingsPath}\"");
                        writer.WriteLine($"del \"{settingsBackupPath}\"");
                        writer.WriteLine($"echo Settings restored successfully.");
                    }

                    // Delete the temp directory and zip file
                    writer.WriteLine($"rmdir /S /Q \"{tempExtractPath}\"");
                    writer.WriteLine($"del \"{zipFilePath}\"");

                    // Start the application again
                    // writer.WriteLine($"start \"\" \"{Path.Combine(appDirectory, "ModdingGUI.exe")}\"");

                    // Delete this batch file
                    writer.WriteLine("del \"%~f0\"");
                }

                AppendLog($"Update ready for installation.", SuccessColor);

                // Execute the batch file
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = batchFile,
                    CreateNoWindow = false,
                    UseShellExecute = true,
                    WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
                });

                return true;
            }
            catch (Exception ex)
            {
                AppendLog($"Failed to install update: {ex.Message}", ErrorColor);

                // Clean up if extraction failed
                if (Directory.Exists(tempExtractPath))
                {
                    try { Directory.Delete(tempExtractPath, true); } catch { /* Ignore cleanup errors */ }
                }

                // Delete the zip file
                if (File.Exists(zipFilePath))
                {
                    try { File.Delete(zipFilePath); } catch { /* Ignore cleanup errors */ }
                }

                // Delete the settings backup if something went wrong
                if (File.Exists(settingsBackupPath))
                {
                    try { File.Delete(settingsBackupPath); } catch { /* Ignore cleanup errors */ }
                }

                return false;
            }
        }


        /// <summary>
        /// Copies a directory and its contents to a new location
        /// </summary>
        private void DirectoryCopy(string sourceDirName, string destDirName, bool copySubDirs, string[] excludeFiles = null)
        {
            // Get the subdirectories for the specified directory
            DirectoryInfo dir = new DirectoryInfo(sourceDirName);

            if (!dir.Exists)
            {
                throw new DirectoryNotFoundException($"Source directory does not exist or could not be found: {sourceDirName}");
            }

            // Create the destination directory if it doesn't exist
            Directory.CreateDirectory(destDirName);

            // Get the files in the directory and copy them to the new location
            FileInfo[] files = dir.GetFiles();
            foreach (FileInfo file in files)
            {
                // Skip excluded files
                if (excludeFiles != null && excludeFiles.Contains(file.Name))
                    continue;

                string tempPath = Path.Combine(destDirName, file.Name);
                file.CopyTo(tempPath, true);
            }

            // If copying subdirectories, copy them and their contents
            if (copySubDirs)
            {
                DirectoryInfo[] subDirs = dir.GetDirectories();
                foreach (DirectoryInfo subDir in subDirs)
                {
                    string tempPath = Path.Combine(destDirName, subDir.Name);
                    DirectoryCopy(subDir.FullName, tempPath, copySubDirs, excludeFiles);
                }
            }
        }

        /// <summary>
        /// Restarts the application
        /// </summary>
        private void RestartApplication()
        {
            Application.Exit();
        }

        private void chbAutoLaunchDolphin_CheckedChanged(object sender, EventArgs e)
        {
            try
            {
                // Update the setting immediately to match the checkbox
                appSettings.AutoLaunchDolphin = chbAutoLaunchDolphin.Checked;

                // Persist to disk using your existing save routine
                SaveAppSettings();

                // Log the change for clarity
                string state = chbAutoLaunchDolphin.Checked ? "enabled" : "disabled";
                AppendLog($"Auto-launch Dolphin {state}.", InfoColor, rtbPackOutput);
            }
            catch (Exception ex)
            {
                AppendLog($"Failed to update Dolphin auto-launch setting: {ex.Message}", ErrorColor, rtbPackOutput);
            }
        }

        // ── Editors tab — no-project guard ───────────────────────────────────

        private void TabContainer_SelectedIndexChanged(object? sender, EventArgs e)
        {
            // Opening the Editors tab used to pop a modal "Select a Project" picker whenever the sidebar had no
            // selection - which is the state the app starts in - so merely looking at the tab blocked the window.
            // Pick the project the list would have preselected instead, and say so in the log. An empty install gets a
            // sentence, not a dialog: a tab switch is not the place to demand input.
            // The Team Builder's pages are built in code; make sure they exist before the tab is shown, however the
            // user got here (menu, tab click, driver), so nobody ever sees the retired PvP-era Rules page.
            if (tabContainer.SelectedTab == tabTeamBuilder)
            {
                InitializeTeamBuilderUI();
                return;
            }

            if (tabContainer.SelectedTab != tabEditors || tvwProjects.SelectedNode is not null) return;

            var first = tvwProjects.Nodes.Cast<TreeNode>().FirstOrDefault(node => node.Parent is null);
            if (first is null)
            {
                AppendLog("No projects yet - unpack an ISO on the Unpacking tab to start editing.", WarningColor);
                return;
            }

            tvwProjects.SelectedNode = first;
            AppendLog($"Editing {first.Text}. Choose another project in the sidebar to switch.", InfoColor);
        }
    }
}
