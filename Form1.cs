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
                txtPackPath.Text = topLevelFolder;
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

            // Validate that the selected folder exists
            if (!Directory.Exists(selectedFolder))
            {
                MessageBox.Show("Please select a valid folder."); // Show an error message
                return; // Exit the method
            }

            AppendLog("Starting pack operation...", InfoColor, false); // Log the start of the pack operation

            try
            {
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
            // Disable the randomize button
            btnRandomize.Enabled = false;

            // Initialize the random number generator
            InitializeRandom();

            // Clear the log buffer
            randomizerLogBuffer.Clear();

            try
            {
                if (chbRandomHeroes.Checked)
                {
                    lblRandomizeStatus.Text = "Randomizing heroes...";
                    await Task.Run(() => RandomizeHeroes(txtRandomizerPath.Text));
                }

                if (chbRandomTeam.Checked)
                {
                    lblRandomizeStatus.Text = "Randomizing team...";
                    await Task.Run(() => RandomizeTeam(txtRandomizerPath.Text));
                }

                if (chbRandomNoRecruits.Checked)
                {
                    lblRandomizeStatus.Text = "Removing all recruits...";

                    // Initialize progress bar on UI thread
                    pgbRandomizeStatus.Minimum = 0;
                    pgbRandomizeStatus.Value = 0;

                    // Prepare paths and files
                    string leaguesPath = Path.Combine(txtRandomizerPath.Text, $"{Path.GetFileName(txtRandomizerPath.Text)}_BEC", "data", "towns", "leagues");
                    var tokFiles = Directory.GetFiles(leaguesPath, "*.tok", SearchOption.AllDirectories);
                    pgbRandomizeStatus.Maximum = tokFiles.Length;

                    // Create progress reporter on UI thread
                    var removeRecruitsProgress = new Progress<int>(value =>
                    {
                        pgbRandomizeStatus.Value = value;
                    });

                    // Create log message queue
                    ConcurrentQueue<(string message, Color color)> removeRecruitsLogMessages = new ConcurrentQueue<(string, Color)>();

                    // Run the method on a background thread
                    await Task.Run(() => RemoveAllRecruits(txtRandomizerPath.Text, removeRecruitsProgress, removeRecruitsLogMessages));

                    if(randomizerLogsMenuItem.Checked)
                    {
                        // Process log messages on UI thread
                        while (removeRecruitsLogMessages.TryDequeue(out var logMessage))
                        {
                            AppendLog(logMessage.message, logMessage.color, false);
                        }
                    }
                }

                lblRandomizeStatus.Text = "Editing encounter files...";

                // Initialize progress bar on UI thread
                pgbRandomizeStatus.Minimum = 0;
                pgbRandomizeStatus.Value = 0;

                // Prepare paths and files
                string encountersPath = Path.Combine(txtRandomizerPath.Text, $"{Path.GetFileName(txtRandomizerPath.Text)}_BEC", "data", "encounters");
                var encFiles = Directory.GetFiles(encountersPath, "*.enc", SearchOption.AllDirectories);
                pgbRandomizeStatus.Maximum = encFiles.Length;

                // Create progress reporter on UI thread
                var editEncountersProgress = new Progress<int>(value =>
                {
                    pgbRandomizeStatus.Value = value;
                });

                // Create log message queue
                ConcurrentQueue<(string message, Color color)> editEncountersLogMessages = new ConcurrentQueue<(string, Color)>();

                // Run the method on a background thread
                await Task.Run(() => EditEncounterFiles(txtRandomizerPath.Text, chbRandomPermaDeath.Checked, editEncountersProgress, editEncountersLogMessages));

               

                // Reset lblRandomizeStatus and progress bar
                lblRandomizeStatus.Text = "Ready";
                pgbRandomizeStatus.Value = 0;

                // Output the collected logs if logging is enabled
                if (randomizerLogsMenuItem.Checked)
                {
                    foreach (var logEntry in randomizerLogBuffer)
                    {
                        AppendLog(logEntry.message, logEntry.color, false);
                    }
                    // Process log messages on UI thread
                    while (editEncountersLogMessages.TryDequeue(out var logMessage))
                    {
                        AppendLog(logMessage.message, logMessage.color, false);
                    }
                }
            }
            finally
            {
                // Re-enable the randomize button
                btnRandomize.Enabled = true;
                AppendLog("Randomization completed. (That was snappy!)", SuccessColor, false);
                tabContainer.SelectedTab = tabPacking;
            }
        }



        private void txtUnpackPath_Click(object sender, EventArgs e)
        {
            var textBox = (TextBox)sender;
            textBox.SelectAll();
            textBox.Focus();
        }
    }
}
