using System.Collections.Concurrent;
using System.Diagnostics;      // Provides classes for working with processes
using System.Text;             // Contains classes representing ASCII and Unicode character encodings
using System.Text.RegularExpressions; // Provides classes for regular expressions

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
                MessageBox.Show("Python is either not installed or not in PATH. Please fix this to use the program!");
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

            // Validate that the ISO file exists and the user input is not empty
            if (!File.Exists(isoPath) || string.IsNullOrEmpty(userInput))
            {
                MessageBox.Show("Please select a valid ISO file and enter a folder name."); // Show an error message
                return; // Exit the method
            }

            AppendLog("Starting unpack operation...", InfoColor, true); // Log the start of the unpack operation

            try
            {
                // Create the top-level folder based on the sanitized user input
                string topLevelFolder = Path.Combine(Directory.GetCurrentDirectory(), userInput);

                // Ensure the top-level folder exists
                if (!Directory.Exists(topLevelFolder))
                {
                    Directory.CreateDirectory(topLevelFolder);
                    AppendLog($"Top-level folder created: {topLevelFolder}", InfoColor, true); // Log the creation of the folder
                }

                // Normalize the path for batch file generation
                string normalizedTopLevelFolder = NormalizePath(EnsureTrailingSeparator(topLevelFolder));

                // Generate the batch file content for unpacking
                string batchContent = GenerateUnpackBatchFileContent(isoPath, userInput);

                // Run the batch file asynchronously within the top-level folder
                await RunBatchFileAsync(batchContent, topLevelFolder, true);

                AppendLog("Unpacking completed successfully.", SuccessColor, true); // Log successful completion

                // Update the pack path text box to point to the created unpack folder
                txtPackPath.Text = topLevelFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                txtRandomizerPath.Text = topLevelFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                btnRandomize.Enabled = true;
            }
            catch (Exception ex)
            {
                // Log any exceptions that occur during unpacking
                AppendLog("Error during unpacking: " + ex.Message, ErrorColor, true);
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

            AppendLog("Starting pack operation...", InfoColor, false); // Log the start of the pack operation

            try
            {
                bool valid = true; // Initialize a flag to track the overall validation result
                // Run itemsets validation before packing
                if (!chbValidationSkip.Checked) { 
                    if (!ValidateItemsets(selectedFolder))
                    {
                        valid = false; // Exit the method if validation fails
                    };
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
                        AppendLog("Validation failed. Please check the log for details.", ErrorColor, false); // Log validation failure
                        return; // Exit the method
                    }
                }
                else
                {
                    AppendLog("Validation skipped.", InfoColor, false);
                }
                // Generate the batch file content for packing
                string batchContent = GeneratePackBatchFileContent(selectedFolder);

                // Run the batch file asynchronously within the selected folder
                await RunBatchFileAsync(batchContent, selectedFolder, false);

                AppendLog("Packing completed successfully.", SuccessColor, false); // Log successful completion
                btnRandomize.Enabled = true;
            }
            catch (Exception ex)
            {
                // Log any exceptions that occur during packing
                AppendLog("Error during packing: " + ex.Message, ErrorColor, false);
            }
        }


        // Event handler for the 'Select ISO' button click event
        private void btnSelectISO_Click(object sender, EventArgs e)
        {
            // Create and configure an OpenFileDialog
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "ISO files (*.iso)|*.iso|All files (*.*)|*.*"; // Set the file filter
                if (openFileDialog.ShowDialog() == DialogResult.OK) // Show the dialog and check if the user selected a file
                {
                    txtISOPath.Text = openFileDialog.FileName; // Set the ISO path text box to the selected file
                }
            }
        }

        // Event handler for the 'Select Pack Path' button click event
        private void btnPackPath_Click(object sender, EventArgs e)
        {
            // Create and configure a FolderBrowserDialog
            using (FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog())
            {
                if (folderBrowserDialog.ShowDialog() == DialogResult.OK) // Show the dialog and check if the user selected a folder
                {
                    txtPackPath.Text = folderBrowserDialog.SelectedPath; // Set the pack path text box to the selected folder
                    txtRandomizerPath.Text = folderBrowserDialog.SelectedPath; // Set the randomizer path text box to the selected folder
                    btnRandomize.Enabled = true; // Enable the randomize button
                    btnPack.Enabled = true; // Enable the pack button   
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
            // Check for parentheses in the executable path
            string exePath = Application.ExecutablePath;
            if (exePath.Contains("(") || exePath.Contains(")"))
            {
                MessageBox.Show("The program cannot run from a file path containing parentheses. Please move it to a different location.");
                Environment.Exit(0); // Exit the application
            }

            // Check for the existence of the tools folder
            string path = @"tools/";
            if (!Directory.Exists(path))
            {
                MessageBox.Show("Tools folder not found. Download modding tools v007 or higher from the Discord!");
                Environment.Exit(0); // Exit the application
            }

            // Remove the randomizer tab on load
            tabContainer.TabPages.Remove(tabRandomizer);
            tabContainer.TabPages.Remove(tabIngameRandom);
            LoadProjects();
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

        private void btnRandomizerPath_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog())
            {
                if (folderBrowserDialog.ShowDialog() == DialogResult.OK) // Show the dialog and check if the user selected a folder
                {
                    string selectedPath = folderBrowserDialog.SelectedPath; // Get the selected folder path
                    var directories = Directory.GetDirectories(selectedPath); // Get all directories inside the selected folder

                    // Check if there are exactly 2 folders and if one contains '_ISO' and the other contains '_BEC'
                    if (directories.Length == 2 && directories.Any(d => d.Contains("_ISO")) && directories.Any(d => d.Contains("_BEC")))
                    {
                        txtRandomizerPath.Text = selectedPath; // Set the randomizer path text box to the selected folder
                        btnRandomize.Enabled = true; // Enable the randomize button
                        txtPackPath.Text = selectedPath; // Set the pack path text box to the selected folder
                        btnPack.Enabled = true; // Enable the pack button 
                    }
                    else
                    {
                        MessageBox.Show("Expected an unpacked project. Use the Unpack tab to create one, or ensure the selected folder contains _ISO and _BEC folders.");// Show an error message
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
                    AppendLog("Invalid project folder selected.", WarningColor, false);
                    return;
                }

                // 1. Randomize Heroes if the corresponding checkbox is checked
                if (chbRandomHeroes.Checked)
                {
                    lblRandomizeStatus.Text = "Randomizing heroes...";
                    AppendLog("Starting hero randomization...", InfoColor, false);
                    await Task.Run(() => RandomizeHeroes(projectFolder));
                    AppendLog("Heroes randomized successfully.", SuccessColor, false);
                }

                // 2. Randomize Team if the corresponding checkbox is checked
                if (chbRandomTeam.Checked)
                {
                    lblRandomizeStatus.Text = "Randomizing team...";
                    AppendLog("Starting team randomization...", InfoColor, false);
                    await Task.Run(() => RandomizeTeam(projectFolder));
                    AppendLog("Team randomized successfully.", SuccessColor, false);
                }

                // 3. Remove All Recruits if the corresponding checkbox is checked
                if (chbRandomNoRecruits.Checked)
                {
                    lblRandomizeStatus.Text = "Removing all recruits...";
                    AppendLog("Starting removal of all recruits...", InfoColor, false);

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
                            AppendLog(logMessage.message, logMessage.color, false);
                        }
                    }

                    AppendLog("All recruits removed successfully.", SuccessColor, false);
                }

                // 4. Edit Encounter Files
                lblRandomizeStatus.Text = "Editing encounter files...";
                AppendLog("Starting editing of encounter files...", InfoColor, false);

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
                AddRandomizedMenuEntry(projectFolder);
                AppendLog("Encounter files edited and menu entry added successfully.", SuccessColor, false);

                // 5. Edit and Compile .scp Files
                AppendLog("Starting .scp files editing and compilation...", InfoColor, false);
                AppendLog("Editing and compiling .scp files...", InfoColor, false);
                await Task.Run(() => EditScpFilesAndCompileAsync(projectFolder));
                AppendLog(".scp files edited and compiled successfully.", SuccessColor, false);

                // 6. Apply In-Game Randomization if the checkbox is checked
                if (chbIngameRandom.Checked)
                {
                    AppendLog("Applying in-game randomization...", InfoColor, false);
                    await Task.Run(() => ApplyIngameRandomAsync(projectFolder));
                    AppendLog("In-game randomization applied successfully.", SuccessColor, false);
                }

                // If logging is enabled, process and display the collected log messages
                if (randomizerLogsMenuItem.Checked)
                {
                    // Append general log entries
                    foreach (var logEntry in randomizerLogBuffer)
                    {
                        AppendLog(logEntry.message, logEntry.color, false);
                    }

                    // Append encounter-specific log entries
                    while (editEncountersLogMessages.TryDequeue(out var logMessage))
                    {
                        AppendLog(logMessage.message, logMessage.color, false);
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

                // Append completion log
                AppendLog("Randomization completed. (That was snappy!)", SuccessColor, false);

                // Switch to the packing tab
                tabContainer.SelectedTab = tabPacking;
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
                    AppendLog($"Error expanding node '{node.Text}': {ex.Message}", ErrorColor, true);
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
                    AppendLog($"Error opening file '{path}': {ex.Message}", ErrorColor, true);
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
                    txtPackPath.Text = projectPath;
                    txtRandomizerPath.Text = projectPath;
                    string projectName = Path.GetFileName(projectPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                    txtUnpackPath.Text = projectName;
                    btnPack.Enabled = true;
                    btnRandomize.Enabled = true;
                }
                else
                {
                    txtPackPath.Text = string.Empty;
                }
            }
            else
            {
                // Optional: Clear txtPackPath or handle selections of sub-nodes
                txtPackPath.Text = string.Empty;
            }
        }
    }
}
