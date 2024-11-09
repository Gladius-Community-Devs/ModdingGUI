using System;
using System.Diagnostics;      // Provides classes for working with processes
using System.Drawing;          // Provides access to GDI+ basic graphics functionality
using System.IO;               // Provides types for reading and writing to files and data streams
using System.Text;             // Contains classes representing ASCII and Unicode character encodings
using System.Threading.Tasks;  // Provides types that simplify the work of writing concurrent and asynchronous code
using System.Windows.Forms;    // Provides classes for creating Windows-based applications

namespace ModdingGUI
{
    // Partial class definition for the main form of the application
    public partial class frmMain : Form
    {
        // Define static readonly colors for use in logging messages
        private static readonly Color SuccessColor = Color.Green; // Color for success messages
        private static readonly Color ErrorColor = Color.Red;     // Color for error messages
        private static readonly Color InfoColor = Color.Blue;     // Color for informational messages

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

        // Method to append a message to the log with optional color and target (unpack or pack)
        private void AppendLog(string message, Color? color = null, bool isUnpack = true)
        {
            // Determine which RichTextBox to use based on the isUnpack flag
            var targetRtb = isUnpack ? rtbUnpackOutput : rtbPackOutput;
            color ??= Color.Black; // If color is null, default to black

            // Check if invoke is required (if called from a different thread)
            if (targetRtb.InvokeRequired)
            {
                // Invoke the method on the UI thread if required
                targetRtb.Invoke(new Action(() => AppendLog(message, color, isUnpack)));
            }
            else
            {
                // Append the message to the RichTextBox with the specified color
                targetRtb.SelectionStart = targetRtb.TextLength; // Set selection start to the end
                targetRtb.SelectionColor = color.Value;          // Set the selection color
                targetRtb.AppendText(message + Environment.NewLine); // Append the message
                targetRtb.ScrollToCaret(); // Scroll to the caret to ensure the new message is visible
            }
        }

        // Method to handle process output data and log it appropriately
        private void LogProcessOutput(DataReceivedEventArgs e, bool isUnpack, Color? color = null)
        {
            if (!string.IsNullOrEmpty(e.Data)) // Check if data is not null or empty
            {
                AppendLog(e.Data, color ?? Color.Black, isUnpack); // Append the data to the log
            }
        }

        // Method to enclose a file path in quotes if it's not already quoted
        private string QuotePath(string path)
        {
            // Check if the path is not null or whitespace and not already quoted
            if (!string.IsNullOrWhiteSpace(path) && !path.StartsWith("\"") && !path.EndsWith("\""))
            {
                return $"\"{path}\""; // Enclose the path in quotes
            }
            return path; // Return the original path if already quoted
        }

        // Method to ensure a path ends with a directory separator character
        private string EnsureTrailingSeparator(string path)
        {
            // Add a trailing directory separator if it's not already present
            if (!string.IsNullOrEmpty(path) && !path.EndsWith(Path.DirectorySeparatorChar.ToString()))
            {
                path += Path.DirectorySeparatorChar; // Append the directory separator character
            }
            return path; // Return the modified or original path
        }

        // Method to normalize a file path by replacing backslashes with forward slashes
        private string NormalizePath(string path)
        {
            return path.Replace("\\", "/"); // Replace backslashes with forward slashes
        }

        // Method to generate the content of the batch file for unpacking operations
        private string GenerateUnpackBatchFileContent(string isoPath, string userInput)
        {
            StringBuilder sb = new StringBuilder(); // StringBuilder to construct the batch file content

            // Create the top-level folder based on user input in the current directory
            string topLevelFolder = Path.Combine(Directory.GetCurrentDirectory(), userInput);
            Directory.CreateDirectory(topLevelFolder); // Create the directory if it doesn't exist
            AppendLog($"Top-level folder created: {topLevelFolder}"); // Log the creation of the folder

            // Define the tools path relative to the current directory and normalize it
            string toolsPath = NormalizePath(Path.Combine(Directory.GetCurrentDirectory(), "tools"));

            // Define paths for the ISO and BEC folders within the top-level folder
            string isoFolder = NormalizePath(EnsureTrailingSeparator(Path.Combine(topLevelFolder, $"{userInput}_ISO")));
            string becFolder = NormalizePath(EnsureTrailingSeparator(Path.Combine(topLevelFolder, $"{userInput}_BEC")));
            Directory.CreateDirectory(isoFolder); // Ensure the ISO folder exists for the file list

            // Prepare the command to unpack the ISO file
            string ngcisoToolPath = NormalizePath(Path.Combine(toolsPath, "ngciso-tool.py")); // Path to ngciso-tool.py
            string isoPathQuoted = QuotePath(NormalizePath(isoPath));                         // Quoted and normalized ISO path
            string isoFolderQuoted = QuotePath(isoFolder);                                    // Quoted ISO folder path

            // Append the command to the StringBuilder
            sb.AppendLine($"python \"{ngcisoToolPath}\" -unpack {isoPathQuoted} {isoFolderQuoted} {userInput}_FileList.txt");

            // Prepare the command to unpack BEC files
            string becToolPath = NormalizePath(Path.Combine(toolsPath, "bec-tool-all.py"));        // Path to bec-tool-all.py
            string becInputPath = NormalizePath(Path.Combine(isoFolder, "gladius.bec"));           // Path to gladius.bec within ISO folder
            string becInputPathQuoted = QuotePath(becInputPath);                                   // Quoted BEC input path
            string becFolderQuoted = QuotePath(becFolder);                                         // Quoted BEC folder path

            // Append the command to the StringBuilder
            sb.AppendLine($"python \"{becToolPath}\" --platform GC -unpack {becInputPathQuoted} {becFolderQuoted}");

            // Prepare the command to unpack units
            string unitsUnpackToolPath = NormalizePath(Path.Combine(toolsPath, "Gladius_Units_IDX_Unpack.py")); // Path to unit unpack tool
            string dataFolder = NormalizePath(EnsureTrailingSeparator(Path.Combine(becFolder, "Data")));        // Path to Data folder within BEC folder
            string dataFolderQuoted = QuotePath(dataFolder);                                                    // Quoted Data folder path

            // Append the command to the StringBuilder
            sb.AppendLine($"python \"{unitsUnpackToolPath}\" {dataFolderQuoted}");

            return sb.ToString(); // Return the generated batch file content as a string
        }

        // Method to generate the content of the batch file for packing operations
        private string GeneratePackBatchFileContent(string selectedFolder)
        {
            StringBuilder sb = new StringBuilder(); // StringBuilder to construct the batch file content

            // Retrieve the base folder name from the user's input
            string baseName = Path.GetFileName(selectedFolder.TrimEnd(Path.DirectorySeparatorChar));

            // Define paths for tools, BEC folder, ISO folder, and output ISO file
            string toolsPath = NormalizePath(EnsureTrailingSeparator(Path.Combine(Directory.GetCurrentDirectory(), "tools")));
            string becFolder = NormalizePath(EnsureTrailingSeparator(Path.Combine(selectedFolder, $"{baseName}_BEC"))); // Path to BEC folder
            string isoFolder = NormalizePath(EnsureTrailingSeparator(Path.Combine(selectedFolder, $"{baseName}_ISO"))); // Path to ISO folder
            string isoFile = NormalizePath(Path.Combine(selectedFolder, $"{baseName}.iso"));                           // Path to output ISO file

            // Prepare the command to update token numbers
            string tokNumUpdatePath = NormalizePath(Path.Combine(toolsPath, "Tok_Num_Update.py"));              // Path to Tok_Num_Update.py
            string dataConfigFolder = NormalizePath(EnsureTrailingSeparator(Path.Combine(becFolder, "Data", "Config"))); // Path to Config folder
            sb.AppendLine($"python \"{tokNumUpdatePath}\" \"{dataConfigFolder}\"");                             // Append the command

            // Prepare the command to compile skills.tok
            string tokToolPath = NormalizePath(Path.Combine(toolsPath, "tok-tool.py"));               // Path to tok-tool.py
            string skillsTokPath = NormalizePath(Path.Combine(dataConfigFolder, "skills.tok"));       // Path to skills.tok
            string skillsStringsBinPath = NormalizePath(Path.Combine(dataConfigFolder, "skills_strings.bin")); // Path to skills_strings.bin
            string skillsLinesBinPath = NormalizePath(Path.Combine(dataConfigFolder, "skills_lines.bin"));     // Path to skills_lines.bin
            string skillsTokBrfPath = NormalizePath(Path.Combine(dataConfigFolder, "skills.tok.brf"));         // Path to skills.tok.brf

            sb.AppendLine($"python \"{tokToolPath}\" -c \"{skillsTokPath}\" \"{skillsStringsBinPath}\" \"{skillsLinesBinPath}\" \"{skillsTokBrfPath}\""); // Append the command

            // Prepare the command to update strings.bin
            string updateStringsBinPath = NormalizePath(Path.Combine(toolsPath, "Update_Strings_Bin.py")); // Path to Update_Strings_Bin.py
            sb.AppendLine($"python \"{updateStringsBinPath}\" \"{dataConfigFolder}\"");                   // Append the command

            // Prepare the command to repack units
            string unitsRepackToolPath = NormalizePath(Path.Combine(toolsPath, "Gladius_Units_IDX_Repack.py")); // Path to unit repack tool
            string dataFolder = NormalizePath(EnsureTrailingSeparator(Path.Combine(becFolder, "Data")));        // Path to Data folder
            sb.AppendLine($"python \"{unitsRepackToolPath}\" \"{dataFolder}\"");                                // Append the command

            // Prepare the command to pack BEC files
            string becToolPath = NormalizePath(Path.Combine(toolsPath, "bec-tool-all.py"));        // Path to bec-tool-all.py
            string becFilePath = NormalizePath(Path.Combine(becFolder, "gladius.bec"));           // Path to gladius.bec output
            string fileListPath = NormalizePath(Path.Combine(becFolder, "filelist.txt"));         // Path to filelist.txt
            sb.AppendLine($"python \"{becToolPath}\" -pack \"{becFolder}\" \"{becFilePath}\" \"{fileListPath}\" --platform GC"); // Append the command

            // Prepare the command to pack the ISO
            string ngcisoToolPath = NormalizePath(Path.Combine(toolsPath, "ngciso-tool.py"));                // Path to ngciso-tool.py
            string fstBinPath = NormalizePath(Path.Combine(isoFolder, "fst.bin"));                           // Path to fst.bin
            string gladiusFileListPath = NormalizePath(Path.Combine(isoFolder, $"{baseName}_FileList.txt")); // Path to file list
            sb.AppendLine($"python \"{ngcisoToolPath}\" -pack \"{isoFolder}\" \"{fstBinPath}\" \"{gladiusFileListPath}\" \"{isoFile}\""); // Append the command

            return sb.ToString(); // Return the generated batch file content as a string
        }

        // Asynchronous method to run the generated batch file
        private async Task RunBatchFileAsync(string batchContent, string workingDirectory, bool isUnpack)
        {
            // Generate a unique batch file name using a GUID
            var batchFilePath = Path.Combine(workingDirectory, $"GeneratedBatch_{Guid.NewGuid()}.bat");

            try
            {
                // Write the batch content to the batch file
                File.WriteAllText(batchFilePath, batchContent);
                AppendLog($"Saved batch file: {batchFilePath}", InfoColor, isUnpack); // Log that the batch file was saved

                // Set up the process start info to execute the batch file
                var startInfo = new ProcessStartInfo
                {
                    FileName = batchFilePath,          // Batch file to execute
                    WorkingDirectory = workingDirectory, // Set the working directory
                    UseShellExecute = false,           // Do not use the OS shell
                    RedirectStandardOutput = true,     // Redirect standard output
                    RedirectStandardError = true,      // Redirect standard error
                    CreateNoWindow = true,             // Do not create a window
                };

                // Create a new process with the specified start info
                using (var process = new Process { StartInfo = startInfo })
                {
                    // Subscribe to the output and error data received events
                    process.OutputDataReceived += (sender, e) => LogProcessOutput(e, isUnpack);
                    process.ErrorDataReceived += (sender, e) => LogProcessOutput(e, isUnpack, ErrorColor);

                    process.Start(); // Start the process
                    process.BeginOutputReadLine(); // Begin asynchronous read of standard output
                    process.BeginErrorReadLine();  // Begin asynchronous read of standard error

                    await process.WaitForExitAsync(); // Wait for the process to exit asynchronously
                }
            }
            catch (Exception ex)
            {
                // Log any exceptions that occur during execution
                AppendLog($"An error occurred while executing the batch file: {ex.Message}", ErrorColor, isUnpack);
            }
            finally
            {
                // If the user has not chosen to save batch files and the file exists
                if (!saveBatMenuItem.Checked && File.Exists(batchFilePath))
                {
                    File.Delete(batchFilePath); // Delete the batch file
                    AppendLog("Batch file deleted after execution.", InfoColor, isUnpack); // Log that the batch file was deleted
                }
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
                string topLevelFolder = Path.Combine(Directory.GetCurrentDirectory(), NormalizePath(EnsureTrailingSeparator(userInput)));
                // Generate the batch file content for unpacking
                string batchContent = GenerateUnpackBatchFileContent(isoPath, userInput);

                // Run the batch file asynchronously within the top-level folder
                await RunBatchFileAsync(batchContent, topLevelFolder, true);

                AppendLog("Unpacking completed successfully.", SuccessColor, true); // Log successful completion

                // Update the pack path text box to point to the created unpack folder
                txtPackPath.Text = topLevelFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
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
            string selectedFolder = EnsureTrailingSeparator(txtPackPath.Text);

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
                }
            }
        }

        // Method to open a folder location in Windows Explorer
        private void OpenLocation(string path)
        {
            if (Directory.Exists(path)) // Check if the directory exists
            {
                Process.Start("explorer.exe", QuotePath(path)); // Open the folder in Explorer
            }
            else
            {
                MessageBox.Show("Selected location does not exist."); // Show an error message
            }
        }

        // Event handler for the 'Open Pack Location' button click event
        private void btnOpenPackLocation_Click(object sender, EventArgs e)
        {
            OpenLocation(txtPackPath.Text); // Open the pack path location
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
        }
    }
}
