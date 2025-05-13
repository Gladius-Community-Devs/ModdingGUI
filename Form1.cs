using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Forms;
using ModdingGUI.Models;

namespace ModdingGUI
{
    // Partial class definition for the main form of the application
    public partial class frmMain : Form
    {
        // Constructor for the main form
        public frmMain()
        {
            InitializeComponent(); // Initialize form components

            // Check if Python 3 is installed on the system
            if (!IsPythonInstalled())
            {
                // Display a message box if Python 3 is not installed
                MessageBox.Show("Python is either not installed or not in PATH. Please fix this to use the program!\nhttps://www.python.org/downloads/ \nDuring install, check the box labelled 'Add python.exe to PATH'");
                Environment.Exit(0); // Exit the application
            }
        }

        // Event handler for the Unpack button click event
        private async void btnUnpack_Click(object sender, EventArgs e)
        {
            string isoPath = txtISOPath.Text; // Get the ISO path from the text box

            // Check if the ISO path contains parentheses
            if (isoPath.Contains("(") || isoPath.Contains(")"))
            {
                MessageBox.Show("The ISO path cannot contain parentheses. Please select a different path.");
                return; // Exit the method
            }

            // Get the user input for the folder name, trim whitespace, and replace spaces with underscores
            string userInput = txtUnpackPath.Text.Trim().Replace(" ", "_");

            // Added validation for empty userInput
            if (string.IsNullOrEmpty(userInput))
            {
                MessageBox.Show("Please enter a valid folder name.");
                return;
            }

            // Validate that the ISO file exists and the user input is not empty
            if (!ValidateFilePath(isoPath, "ISO"))
            {
                return;
            }

            AppendLog("Starting unpack operation...", InfoColor); // Log the start of the unpack operation

            try
            {
                // Create the top-level folder based on the sanitized user input
                string topLevelFolder = Path.Combine(Directory.GetCurrentDirectory(), userInput);

                // Ensure the top-level folder exists
                if (!Directory.Exists(topLevelFolder))
                {
                    Directory.CreateDirectory(topLevelFolder);
                    AppendLog($"Top-level folder created: {topLevelFolder}", InfoColor); // Log the creation of the folder
                }

                // Normalize the path for batch file generation
                string normalizedTopLevelFolder = NormalizePath(EnsureTrailingSeparator(topLevelFolder));

                // Generate the batch file content for unpacking
                string batchContent = GenerateUnpackBatchFileContent(isoPath, userInput);

                // Run the batch file asynchronously within the top-level folder
                await RunBatchFileAsync(batchContent, topLevelFolder);

                AppendLog("Unpacking completed successfully.", SuccessColor); // Log successful completion

                // Update the pack path text box to point to the created unpack folder
                txtPackPath.Text = topLevelFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                txtRandomizerPath.Text = topLevelFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                txtPatchingCreationModISOPath.Text = NormalizePath(topLevelFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + $"/{userInput}.iso");
                txtPatchingCreationOutputPath.Text = NormalizePath(topLevelFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + $"/{userInput}.xdelta");
                btnRandomize.Enabled = true;
                btnPack.Enabled = true;
                LoadProjects();
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
            selectedFolder = NormalizePath(EnsureTrailingSeparator(selectedFolder));
            selectedFolder = selectedFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            // Validate that the selected folder exists
            if (!Directory.Exists(selectedFolder))
            {
                MessageBox.Show("Please select a valid folder."); // Show an error message
                return; // Exit the method
            }

            AppendLog("Starting pack operation...", InfoColor, rtbPackOutput); // Log the start of the pack operation

            try
            {
                bool valid = true; // Initialize a flag to track the overall validation result
                // Run itemsets validation before packing
                if (!chbValidationSkip.Checked)
                {
                    if (!ValidateItemsets(selectedFolder))
                    {
                        valid = false; // Exit the method if validation fails
                    }
                    ;
                    if (!ValidateGladiatorsFile(selectedFolder))
                    {
                        valid = false;
                    }
                    if (!ValidateGladiatorsAndSkills(selectedFolder))
                    {
                        valid = false;
                    }
                    if (!valid)
                    {
                        AppendLog("Validation failed. Please check the log for details.", ErrorColor, rtbPackOutput); // Log validation failure
                        return; // Exit the method
                    }
                }
                else
                {
                    AppendLog("Validation skipped.", InfoColor, rtbPackOutput);
                }
                // Generate the batch file content for packing
                string batchContent = GeneratePackBatchFileContent(selectedFolder);

                // Run the batch file asynchronously within the selected folder
                await RunBatchFileAsync(batchContent, selectedFolder, rtbPackOutput);

                AppendLog("Packing completed successfully.", SuccessColor, rtbPackOutput); // Log successful completion
                btnRandomize.Enabled = true;
                btnToPatching.Enabled = true;
                btnPack.Enabled = true;
                if (appSettings.AutoLaunchDolphin)
                {
                    // Launch the packed ISO with Dolphin
                    string projectName = Path.GetFileName(selectedFolder);
                    string isoPath = Path.Combine(selectedFolder, $"{projectName}.iso");
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
                    if (ValidateFilePathNoParentheses(openFileDialog.FileName))
                    {
                        txtISOPath.Text = openFileDialog.FileName;
                    }
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
                    if (ValidateFilePathNoParentheses(selectedPath))
                    {
                        txtPackPath.Text = selectedPath;
                        txtRandomizerPath.Text = selectedPath;
                        btnRandomize.Enabled = true;
                        btnPack.Enabled = true;
                    }
                }
            }
        }

        // Event handler for the 'Open Pack Location' button click event
        private void btnOpenPackLocation_Click(object sender, EventArgs e)
        {
            OpenLocation(txtUnpackPath.Text); // Open the pack path location
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

                // Check for updates at startup
                _ = CheckForUpdatesAtStartupAsync(); // Fire and forget async call

                // Retrieve the original executable directory using the helper method
                string appDirectory = GetAppDirectory();
                AppendLog($"App Directory: {appDirectory}", InfoColor);

                // Check for parentheses in the executable path
                if (appDirectory.Contains("(") || appDirectory.Contains(")"))
                {
                    MessageBox.Show("The program cannot run from a file path containing parentheses. Please move it to a different location.", "Invalid Path", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Environment.Exit(0); // Exit the application
                }

                // Define the full path to the tools folder
                string toolsPath = Path.Combine(appDirectory, "tools");

                // Check for the existence of the tools folder
                if (!Directory.Exists(toolsPath))
                {
                    MessageBox.Show("Tools folder not found. Download modding tools v007 or higher from the Discord!", "Missing Tools Folder", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Environment.Exit(0); // Exit the application
                }

                // Proceed with loading the file list asynchronously
                LoadFileListAsync();

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
            // Check if a valid project path is already selected
            string currentPath = txtRandomizerPath.Text.Trim();
            bool hasValidProject = !string.IsNullOrEmpty(currentPath) &&
                                  Directory.Exists(currentPath) &&
                                  Directory.GetDirectories(currentPath)
                                          .Any(d => d.Contains("_ISO")) &&
                                  Directory.GetDirectories(currentPath)
                                          .Any(d => d.Contains("_BEC"));

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

                            if (ValidateFilePathNoParentheses(isoPath))
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

                                    // Create the top-level folder based on the sanitized user input
                                    string topLevelFolder = Path.Combine(Directory.GetCurrentDirectory(), userInput);

                                    // Ensure the top-level folder exists
                                    if (!Directory.Exists(topLevelFolder))
                                    {
                                        Directory.CreateDirectory(topLevelFolder);
                                        AppendLog($"Top-level folder created: {topLevelFolder}", InfoColor);
                                    }

                                    // Normalize the path for batch file generation
                                    string normalizedTopLevelFolder = NormalizePath(EnsureTrailingSeparator(topLevelFolder));

                                    // Generate the batch file content for unpacking
                                    string batchContent = GenerateUnpackBatchFileContent(isoPath, userInput);

                                    // Run the batch file asynchronously within the top-level folder
                                    await RunBatchFileAsync(batchContent, topLevelFolder);

                                    AppendLog("Unpacking completed successfully.", SuccessColor);

                                    // Update the pack path text box to point to the created unpack folder
                                    txtPackPath.Text = topLevelFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                                    txtRandomizerPath.Text = topLevelFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                                    txtPatchingCreationModISOPath.Text = NormalizePath(topLevelFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + $"/{userInput}.iso");
                                    txtPatchingCreationOutputPath.Text = NormalizePath(topLevelFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + $"/{userInput}.xdelta");
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
                        var directories = Directory.GetDirectories(selectedPath);

                        // Check if there are folders containing '_ISO' and '_BEC'
                        if (directories.Any(d => d.Contains("_ISO")) && directories.Any(d => d.Contains("_BEC")))
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

        private async void btnRandomize_Click(object sender, EventArgs e)
        {
            // Disable the randomize button to prevent multiple clicks during processing
            btnRandomize.Enabled = false;

            // Initialize the random number generator based on user input
            InitializeRandom();

            // Clear any existing log messages to start fresh
            randomizerLogBuffer.Clear();

            try
            {
                // Trim and validate the project folder path from user input
                string projectFolder = txtRandomizerPath.Text.Trim();
                if (string.IsNullOrEmpty(projectFolder) || !Directory.Exists(projectFolder))
                {
                    MessageBox.Show("Please select a valid project folder.", "Invalid Path", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    AppendLog("Invalid project folder selected.", WarningColor, rtbPackOutput);
                    return;
                }

                // Save the current randomizer settings before proceeding
                SaveRandomizerSettings(projectFolder);

                // 1. Randomize Heroes if the corresponding checkbox is checked
                if (chbRandomHeroes.Checked)
                {
                    lblRandomizeStatus.Text = "Randomizing heroes...";
                    AppendLog("Starting hero randomization...", InfoColor, rtbPackOutput);
                    await Task.Run(() => RandomizeHeroes(projectFolder));
                    AppendLog("Heroes randomized successfully.", SuccessColor, rtbPackOutput);
                }

                // 2. Randomize Team if the corresponding checkbox is checked
                if (chbRandomTeam.Checked)
                {
                    lblRandomizeStatus.Text = "Randomizing team...";
                    AppendLog("Starting team randomization...", InfoColor, rtbPackOutput);
                    await Task.Run(() => RandomizeTeam(projectFolder));
                    AppendLog("Team randomized successfully.", SuccessColor, rtbPackOutput);
                }

                // 3. Remove All Recruits if the corresponding checkbox is checked
                if (chbRandomNoRecruits.Checked)
                {
                    lblRandomizeStatus.Text = "Removing all recruits...";
                    AppendLog("Starting removal of all recruits...", InfoColor, rtbPackOutput);

                    // Setup progress bar for recruit removal
                    pgbRandomizeStatus.Minimum = 0;
                    pgbRandomizeStatus.Value = 0;

                    // Define the path to the leagues directory
                    string leaguesPath = Path.Combine(projectFolder, $"{Path.GetFileName(projectFolder)}_BEC", "data", "towns", "leagues");

                    // Retrieve all .tok files within the leagues directory and its subdirectories
                    var tokFiles = Directory.GetFiles(leaguesPath, "*.tok", SearchOption.AllDirectories);
                    pgbRandomizeStatus.Maximum = tokFiles.Length;

                    // Setup progress reporting for recruit removal
                    var removeRecruitsProgress = new Progress<int>(value =>
                    {
                        pgbRandomizeStatus.Value = value;
                    });

                    // Initialize a concurrent queue to collect log messages from the background thread
                    ConcurrentQueue<(string message, Color color)> removeRecruitsLogMessages = new ConcurrentQueue<(string, Color)>();

                    // Execute the recruit removal process asynchronously
                    await Task.Run(() => RemoveAllRecruits(projectFolder, removeRecruitsProgress, removeRecruitsLogMessages));

                    // If logging is enabled, process and display the collected log messages
                    if (randomizerLogsMenuItem.Checked)
                    {
                        while (removeRecruitsLogMessages.TryDequeue(out var logMessage))
                        {
                            AppendLog(logMessage.message, logMessage.color, rtbPackOutput);
                        }
                    }

                    AppendLog("All recruits removed successfully.", SuccessColor, rtbPackOutput);
                }

                // 4. Edit Encounter Files
                lblRandomizeStatus.Text = "Editing encounter files...";
                AppendLog("Starting editing of encounter files...", InfoColor, rtbPackOutput);

                // Setup progress bar for encounter file editing
                pgbRandomizeStatus.Minimum = 0;
                pgbRandomizeStatus.Value = 0;

                // Define the path to the encounters directory
                string encountersPath = Path.Combine(projectFolder, $"{Path.GetFileName(projectFolder)}_BEC", "data", "encounters");

                // Retrieve all .enc files within the encounters directory and its subdirectories
                var encFiles = Directory.GetFiles(encountersPath, "*.enc", SearchOption.AllDirectories);
                pgbRandomizeStatus.Maximum = encFiles.Length;

                // Setup progress reporting for encounter file editing
                var editEncountersProgress = new Progress<int>(value =>
                {
                    pgbRandomizeStatus.Value = value;
                });

                // Initialize a concurrent queue to collect log messages from the background thread
                ConcurrentQueue<(string message, Color color)> editEncountersLogMessages = new ConcurrentQueue<(string, Color)>();

                // Execute the encounter file editing process asynchronously
                await Task.Run(() => EditEncounterFiles(projectFolder, chbRandomPermaDeath.Checked, editEncountersProgress, editEncountersLogMessages));

                // Add the randomized menu entry after editing encounter files
                AddMenuEntry(projectFolder, "Game is Randomized!");
                AppendLog("Encounter files edited and menu entry added successfully.", SuccessColor, rtbPackOutput);

                // 5. Edit and Compile .scp Files
                AppendLog("Starting .scp files editing and compilation...", InfoColor, rtbPackOutput);
                AppendLog("Editing and compiling .scp files...", InfoColor, rtbPackOutput);
                await Task.Run(() => EditScpFilesAndCompileAsync(projectFolder));
                AppendLog(".scp files edited and compiled successfully.", SuccessColor, rtbPackOutput);

                // 6. Apply In-Game Randomization if the checkbox is checked
                if (chbIngameRandom.Checked)
                {
                    AppendLog("Applying in-game randomization...", InfoColor, rtbPackOutput);
                    await Task.Run(() => ApplyIngameRandomAsync(projectFolder));
                    AppendLog("In-game randomization applied successfully.", SuccessColor, rtbPackOutput);
                }

                // 7. Randomize Statsets if the corresponding checkbox is checked
                if (chbRandomStatsets.Checked)
                {
                    lblRandomizeStatus.Text = "Randomizing statsets...";
                    AppendLog("Starting statset randomization...", InfoColor, rtbPackOutput);

                    // Initialize progress reporting
                    pgbRandomizeStatus.Minimum = 0;
                    pgbRandomizeStatus.Value = 0;

                    // Parse gladiators to determine total work
                    var gladiators = ParseGladiators(projectFolder);
                    var totalGladiators = gladiators.Count;
                    pgbRandomizeStatus.Maximum = totalGladiators;

                    var statSets = ParseStatSets(projectFolder)?.Keys.ToList();
                    // Added null check here
                    if (statSets == null || statSets.Count == 0)
                    {
                        MessageBox.Show("No statsets available to randomize.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        AppendLog("No statsets available to randomize.", ErrorColor, rtbPackOutput);
                    }
                    else
                    {
                        var statsetProgress = new Progress<int>(value =>
                        {
                            pgbRandomizeStatus.Value = value;
                        });

                        await Task.Run(() => RandomizeStatsets(projectFolder, statsetProgress));
                        AppendLog("Statsets randomized successfully.", SuccessColor, rtbPackOutput);
                    }
                }

                // 8. Randomize Itemsets if the corresponding checkbox is checked
                if (chbRandomItemsets.Checked)
                {
                    lblRandomizeStatus.Text = "Randomizing itemsets...";
                    AppendLog("Starting itemset randomization...", InfoColor, rtbPackOutput);

                    // Initialize progress reporting
                    pgbRandomizeStatus.Minimum = 0;
                    pgbRandomizeStatus.Value = 0;

                    // Parse gladiators to determine total work
                    var gladiatorsForItemset = ParseGladiators(projectFolder);
                    var totalGladiatorsForItemset = gladiatorsForItemset.Count;
                    pgbRandomizeStatus.Maximum = totalGladiatorsForItemset;

                    // Execute itemset randomization asynchronously
                    var itemsetProgress = new Progress<int>(value =>
                    {
                        pgbRandomizeStatus.Value = value;
                    });

                    await Task.Run(() => RandomizeItemsets(projectFolder, itemsetProgress));
                    AppendLog("Itemsets randomized successfully.", SuccessColor, rtbPackOutput);
                }


                // If logging is enabled, process and display the collected log messages
                if (randomizerLogsMenuItem.Checked)
                {
                    // Append general log entries
                    foreach (var logEntry in randomizerLogBuffer)
                    {
                        AppendLog(logEntry.message, logEntry.color, rtbPackOutput);
                    }

                    // Append encounter-specific log entries
                    while (editEncountersLogMessages.TryDequeue(out var logMessage))
                    {
                        AppendLog(logMessage.message, logMessage.color, rtbPackOutput);
                    }

                    // Clear the log buffer after processing
                    randomizerLogBuffer.Clear();
                }
                // Update the status label to indicate completion
                lblRandomizeStatus.Text = "Ready";
                pgbRandomizeStatus.Value = 0;
            }
            finally
            {
                // Re-enable the randomize button regardless of success or failure
                btnRandomize.Enabled = true;

                // Clear all message queues to prevent memory leaks
                randomizerLogBuffer.Clear();

                // Make sure any other concurrent queues used in the method are also cleared
                // (Even though they may be local variables that would be garbage collected)

                // Append completion log
                AppendLog("Randomization completed. (That was snappy!)", SuccessColor, rtbPackOutput);

                // Re-enable the randomize button regardless of success or failure
                btnRandomize.Enabled = true;

                // Clear all message queues to prevent memory leaks
                randomizerLogBuffer.Clear();

                // Make sure any other concurrent queues used in the method are also cleared
                // (Even though they may be local variables that would be garbage collected)

                // Append completion log
                AppendLog("Randomization completed. (That was snappy!)", SuccessColor, rtbPackOutput);

                // Switch to the packing tab
                MessageBox.Show("Click the Pack button to build your ISO!", "Packing step", MessageBoxButtons.OK, MessageBoxIcon.Information);
                tabContainer.SelectedTab = tabPacking;

                // Flash the Pack button to draw the user's attention to it
                await FlashButtonAsync(btnPack, Color.LightGreen, 10, 300);
            }
        }

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
            TreeNode selectedNode = e.Node;

            // Check if the selected node is a top-level project node
            if (selectedNode.Parent == null)
            {
                string projectPath = selectedNode.Tag as string;
                if (!string.IsNullOrEmpty(projectPath) && Directory.Exists(projectPath))
                {
                    projectPath = NormalizePath(projectPath);
                    string projectName = Path.GetFileName(projectPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                    txtPackPath.Text = projectPath;
                    txtRandomizerPath.Text = projectPath;
                    txtPatchingCreationOutputPath.Text = projectPath + $"/{projectName}.xDelta";
                    txtPatchingCreationModISOPath.Text = projectPath + $"/{projectName}.iso";
                    txtUnpackPath.Text = projectName;
                    btnPack.Enabled = true;
                    btnRandomize.Enabled = true;

                    // Try to load randomizer settings for the selected project
                    LoadRandomizerSettings(projectPath);
                }
                else
                {
                    txtPackPath.Text = string.Empty;
                }
                if (tabContainer.TabPages.Contains(tabTeamBuilder))
                    LoadClassesIntoDropdown();
                InitializeTeamBuilderUI();
            }
            else
            {
                // Optional: Clear txtPackPath or handle selections of sub-nodes
                // txtPackPath.Text = string.Empty;
            }
        }

        private void txtTeamLevel_TextChanged(object sender, EventArgs e)
        {
            if (int.TryParse(txtTeamLevel.Text, out int value))
            {
                if (value >= 1 && value <= 30)
                {
                    // Valid input
                    lblTeamLevel.ForeColor = Color.Green; // Highlight in green
                    UpdatePreview();
                }
                else
                {
                    // Invalid input
                    txtTeamLevel.Clear(); // Clear invalid input
                    txtTeamLevel.Focus(); // Refocus on the control
                    lblTeamLevel.Text = "Please enter a number between 1 and 30.";
                    lblTeamLevel.ForeColor = Color.Red; // Highlight in red
                }
            }
            else if (!string.IsNullOrEmpty(txtTeamLevel.Text)) // Handle non-numeric cases
            {
                txtTeamLevel.Clear(); // Clear invalid input
                txtTeamLevel.Focus(); // Refocus on the control
                lblTeamLevel.Text = "Please enter a number between 1 and 30.";
                lblTeamLevel.ForeColor = Color.Red; // Highlight in red
            }
            else
            {
                // Clear the message and reset color if the input is empty
                lblTeamLevel.Text = "";
                lblTeamLevel.ForeColor = Color.Black;
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
                    lblTeamLevel.ForeColor = Color.Green; // Highlight in green
                }
                else
                {
                    // Invalid input
                    txtTeamLevel.Clear(); // Clear invalid input
                    lblTeamLevel.Text = "Input a team level. (All units will have this level):";
                    lblTeamLevel.ForeColor = Color.Black; // Highlight in red
                }
            }
            else
            {
                txtTeamLevel.Clear(); // Clear invalid input
                lblTeamLevel.Text = "Input a team level. (All units will have this level):";
                lblTeamLevel.ForeColor = Color.Black; // Highlight in red
            }
        }

        private void rbnTeamCampaign_CheckedChanged(object sender, EventArgs e)
        {
            if (rbnTeamCampaign.Checked)
            {
                // Set team level to 1 for campaign mode
                txtTeamLevel.Text = "1";
                txtTeamLevel.Enabled = false;

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

        private void chbRandomMaxMoney_MouseHover(object sender, EventArgs e)
        {
            ttpInform.SetToolTip(chbRandomCustomCash, "Customize your starting cash! Try: RANDOM\nDefault: 15000\nDefault 40 glads: 75000");
        }

        private void randomizerTestingMenuItem_Click(object sender, EventArgs e)
        {
            // Check if randomizer testing is already running
            if (randomizerTestingMenuItem.Checked)
            {
                MessageBox.Show("Randomizer testing is already running. Please wait for it to complete.",
                    "Testing In Progress", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Enable the menu item to indicate testing is in progress
            randomizerTestingMenuItem.Checked = true;

            try
            {
                // Configure progress bar for visibility
                pgbRandomizeStatus.Visible = true;
                pgbRandomizeStatus.Value = 0;
                lblRandomizeStatus.Text = "Initializing randomizer testing...";

                // Launch randomizer testing
                HandleRandomizerTesting();
            }
            catch (Exception ex)
            {
                AppendLog($"Error during randomizer testing: {ex.Message}", ErrorColor, rtbPackOutput);
                MessageBox.Show($"Error during randomizer testing: {ex.Message}", "Testing Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                // Uncheck the menu item after testing is complete or if it errors out
                randomizerTestingMenuItem.Checked = false;
                lblRandomizeStatus.Text = "Ready";
                pgbRandomizeStatus.Value = 0;
            }
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
    }
}
