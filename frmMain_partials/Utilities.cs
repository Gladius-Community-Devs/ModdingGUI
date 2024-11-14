using System.Diagnostics;
using System.Text.RegularExpressions;

namespace ModdingGUI
{
    public partial class frmMain
    {
        // Define static readonly colors for use in logging messages
        private static readonly Color SuccessColor = Color.Green; // Color for success messages
        private static readonly Color ErrorColor = Color.Red;     // Color for error messages
        private static readonly Color InfoColor = Color.Blue;     // Color for informational messages
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
            if (string.IsNullOrWhiteSpace(path))
            {
                return "\"\"";
            }

            // Escape special characters used in batch files
            string escapedPath = path
                .Replace("^", "^^")   // Escape caret
                .Replace("&", "^&")   // Escape ampersand
                .Replace("<", "^<")   // Escape less than
                .Replace(">", "^>")   // Escape greater than
                .Replace("|", "^|")   // Escape pipe
                .Replace("(", "^(")   // Escape opening parenthesis
                .Replace(")", "^)")   // Escape closing parenthesis
                .Replace("%", "%%")   // Escape percent sign
                .Replace("\"", "\"\""); // Escape double quotes

            // Enclose the path in double quotes
            return $"\"{escapedPath}\"";
        }


        // Method to ensure a path ends with a directory separator character
        private string EnsureTrailingSeparator(string path)
        {
            // Trim any existing trailing separators
            path = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            // Add the directory separator
            path += Path.DirectorySeparatorChar;
            return path;
        }

        // Method to normalize a file path by replacing backslashes with forward slashes
        private string NormalizePath(string path)
        {
            // Replace backslashes with forward slashes
            path = path.Replace("\\", "/");
            // Replace multiple forward slashes with a single slash
            path = Regex.Replace(path, "/{2,}", "/");
            return path;
        }

        // Method to open a folder location in Windows Explorer
        private void OpenLocation(string path)
        {
            AppendLog($"Opening location: {path}", InfoColor, false); // Log the opening of the location
            if (Directory.Exists(path)) // Check if the directory exists
            {
                Process.Start("explorer.exe", QuotePath(path)); // Open the folder in Explorer
            }
            else
            {
                MessageBox.Show("Selected location does not exist."); // Show an error message
            }
        }
        private void LoadProjects()
        {
            tvwProjects.Nodes.Clear();

            string appDirectory = AppDomain.CurrentDomain.BaseDirectory;
            AppendLog($"App Directory: {appDirectory}", InfoColor, true);

            var directories = Directory.GetDirectories(appDirectory);
            AppendLog($"Found {directories.Length} directories in app directory. (including tools folder)", InfoColor, true);

            foreach (var dir in directories)
            {
                string projectName = Path.GetFileName(dir);
                if (projectName == "tools")
                {
                    AppendLog($"Skipping directory: {projectName}", InfoColor, true);
                    continue;
                }
                AppendLog($"Checking directory: {projectName}", InfoColor, true);

                if (IsProjectDirectory(dir))
                {
                    AppendLog($"Directory '{projectName}' is a valid project.", SuccessColor, true);

                    // Create a tree node for the project
                    TreeNode projectNode = new TreeNode(projectName)
                    {
                        Tag = dir,
                        ImageKey = "folder",
                        SelectedImageKey = "folder"
                    };

                    // Define paths for _ISO and _BEC
                    string isoPath = Path.Combine(dir, $"{projectName}_ISO");
                    string becPath = Path.Combine(dir, $"{projectName}_BEC");

                    // Add _BEC node if it exists
                    if (Directory.Exists(becPath))
                    {
                        //AppendLog($"_BEC directory found for project: {projectName}", SuccessColor, true);
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
                        AppendLog($"_BEC directory not found for project: {projectName}", ErrorColor, true);
                    }

                    // Add _ISO node if it exists
                    if (Directory.Exists(isoPath))
                    {
                        //AppendLog($"_ISO directory found for project: {projectName}", SuccessColor, true);
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
                        AppendLog($"_ISO directory not found for project: {projectName}", ErrorColor, true);
                    }

                    // Add the project node to the TreeView
                    tvwProjects.Nodes.Add(projectNode);
                }
                else
                {
                    AppendLog($"Directory '{projectName}' is not a valid project.", WarningColor, true);
                }
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
                AppendLog($"Error accessing directory '{path}': {ex.Message}", ErrorColor, true);
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
                AppendLog($"Error accessing files in directory '{path}': {ex.Message}", ErrorColor, true);
                return false;
            }
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
                AppendLog($"Project '{projectName}' has both _ISO and _BEC directories.", SuccessColor, true);
            }
            else
            {
                AppendLog($"Project '{projectName}' is missing required subdirectories.", ErrorColor, true);
            }

            return isValid;
        }

    }
}
