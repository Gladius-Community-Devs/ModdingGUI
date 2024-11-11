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
                topLevelFolder = NormalizePath(EnsureTrailingSeparator(topLevelFolder));

                // Generate the batch file content for unpacking
                string batchContent = GenerateUnpackBatchFileContent(isoPath, userInput);

                // Run the batch file asynchronously within the top-level folder
                await RunBatchFileAsync(batchContent, topLevelFolder, true);

                AppendLog("Unpacking completed successfully.", SuccessColor, true); // Log successful completion

                // Update the pack path text box to point to the created unpack folder
                txtPackPath.Text = topLevelFolder;
                txtRandomizerPath.Text = topLevelFolder;
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
            string path = @"tools/";
            if (!Directory.Exists(path))
            {
                MessageBox.Show("Tools folder not found. Download modding tools v007 or higher from the Discord!");
                Environment.Exit(0); // Exit the application
            }
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

        private void btnRandomize_Click(object sender, EventArgs e)
        {
            if (chbRandomHeroes.Checked)
            {
                RandomizeHeroes(txtRandomizerPath.Text); // Randomize the heroes and pass the project path
                EditEncounterFiles(txtRandomizerPath.Text, chbRandomPermaDeath.Checked); // Randomize the perma death and pass the project path
            }

            if (chbRandomTeam.Checked)
            {
                RandomizeTeam(txtRandomizerPath.Text); // Randomize the team and pass the project path
            }

            if (chbRandomNoRecruits.Checked)
            {

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
