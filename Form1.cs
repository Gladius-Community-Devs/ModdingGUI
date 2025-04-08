using System.Collections.Concurrent;

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
                tabContainer.TabPages.Remove(tabTeamBuilder);

                // Load projects
                LoadProjects();
            }
            catch (Exception ex)
            {
                // Log the exception and notify the user
                AppendLog($"Error during application load: {ex.Message}", ErrorColor);
                MessageBox.Show($"An unexpected error occurred:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Environment.Exit(0); // Exit the application
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
            if (!chbValidationSkip.Checked)
            {
                var result = MessageBox.Show("Are you sure you want to enable validation?\nThis is typically for Modders to double check their work!", "Confirmation", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (result == DialogResult.No)
                {
                    chbValidationSkip.Checked = true;
                    return;
                }
                pgbValidation.Visible = true;
            }
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
                txtTeamLevel.Text = "1";
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
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

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
        // Add this method to the frmMain class to handle button flashing animation
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
            ttpInform.SetToolTip(chbRandomMaxMoney, "Start with the maximum amount of money");
        }
    }
}
