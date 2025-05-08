using System;
using System.IO;
using System.Windows.Forms;
using System.Diagnostics;
using System.Threading.Tasks;

namespace ModdingGUI
{
    public partial class frmMain
    {
        // Add "Play in Dolphin" button handlers for the output ISOs
        private void InitializeGameLauncherFeatures()
        {
            // You would need to add buttons to the UI in the designer file
            // and connect them to these event handlers
            
            // Initialize the auto launch checkbox
            if (chbAutoLaunchDolphin != null)
            {
                chbAutoLaunchDolphin.CheckedChanged += chbAutoLaunchDolphin_CheckedChanged;
                UpdateAutoLaunchDolphinCheckbox();
            }
        }

        // Event handler for launching a packed ISO with Dolphin
        private void btnLaunchPackedGame_Click(object sender, EventArgs e)
        {
            // Get the path to the packed ISO
            string projectFolder = txtPackPath.Text.Trim();
            if (string.IsNullOrEmpty(projectFolder) || !Directory.Exists(projectFolder))
            {
                MessageBox.Show("Please select a valid project folder first.");
                return;
            }

            // Construct the ISO path
            string projectName = Path.GetFileName(projectFolder);
            string isoPath = Path.Combine(projectFolder, $"{projectName}.iso");

            // Check if the ISO exists
            if (!File.Exists(isoPath))
            {
                MessageBox.Show(
                    "The packed ISO file does not exist. Please pack the project first.",
                    "Missing ISO File",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            // Launch the game with Dolphin
            LaunchGameWithDolphin(isoPath);
        }

        // Event handler for launching a patched ISO with Dolphin
        private void btnLaunchPatchedGame_Click(object sender, EventArgs e)
        {
            string patchedIsoPath = "Gladius_Patched.ISO";
            string currentDirectory = Directory.GetCurrentDirectory();
            string fullPath = Path.Combine(currentDirectory, patchedIsoPath);

            // Check if the patched ISO exists
            if (!File.Exists(fullPath))
            {
                MessageBox.Show(
                    "The patched ISO file does not exist. Please apply a patch first.",
                    "Missing ISO File",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            // Launch the game with Dolphin
            LaunchGameWithDolphin(fullPath);
        }

        // Add a context menu item to launch a project ISO when right-clicking in the project tree
        private void AddLaunchContextMenuToProjectTree()
        {
            // Create a context menu for the project tree
            ContextMenuStrip projectContextMenu = new ContextMenuStrip();
            
            // Add a menu item to launch the ISO with Dolphin
            ToolStripMenuItem launchMenuItem = new ToolStripMenuItem("Launch with Dolphin");
            launchMenuItem.Click += (sender, e) => 
            {
                // Check if a node is selected
                if (tvwProjects.SelectedNode == null || tvwProjects.SelectedNode.Parent != null)
                {
                    return;
                }

                // Get the project folder
                string projectFolder = tvwProjects.SelectedNode.Tag as string;
                if (string.IsNullOrEmpty(projectFolder) || !Directory.Exists(projectFolder))
                {
                    return;
                }

                // Construct the ISO path
                string projectName = tvwProjects.SelectedNode.Text;
                string isoPath = Path.Combine(projectFolder, $"{projectName}.iso");

                // Launch the game with Dolphin if the ISO exists
                if (File.Exists(isoPath))
                {
                    LaunchGameWithDolphin(isoPath);
                }
                else
                {
                    MessageBox.Show(
                        "The ISO file does not exist. Please pack the project first.",
                        "Missing ISO File",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
            };

            // Add the menu item to the context menu
            projectContextMenu.Items.Add(launchMenuItem);

            // Assign the context menu to the project tree
            tvwProjects.ContextMenuStrip = projectContextMenu;
        }

        // Add a context menu item to copy a project when right-clicking in the project tree
        private void AddCopyContextMenuToProjectTree()
        {
            // Create a context menu for the project tree
            ContextMenuStrip projectContextMenu = tvwProjects.ContextMenuStrip ?? new ContextMenuStrip();

            // Add a menu item to copy the project
            ToolStripMenuItem copyMenuItem = new ToolStripMenuItem("Copy Project");
            copyMenuItem.Click += async (sender, e) =>
            {
                // Check if a node is selected
                if (tvwProjects.SelectedNode == null || tvwProjects.SelectedNode.Parent != null)
                {
                    return;
                }

                // Get the project folder
                string projectFolder = tvwProjects.SelectedNode.Tag as string;
                if (string.IsNullOrEmpty(projectFolder) || !Directory.Exists(projectFolder))
                {
                    return;
                }

                // Get the original project name
                string originalName = tvwProjects.SelectedNode.Text;
                
                // Create and show the project copy dialog but don't start copying yet
                using (var progressDialog = new ProjectCopyDialog(originalName, projectFolder))
                {
                    // Show dialog and wait for the user to press Start Copy or Cancel
                    if (progressDialog.ShowDialog(this) == DialogResult.OK)
                    {
                        // The user clicked Start Copy, and the dialog will handle the copy process
                        // After the copy completes successfully, refresh the project tree
                        LoadProjects();
                    }
                }
            };

            // Add the menu item to the context menu
            projectContextMenu.Items.Add(copyMenuItem);

            // Assign the updated context menu to the project tree
            tvwProjects.ContextMenuStrip = projectContextMenu;
        }

        // Add a context menu item to delete a project when right-clicking in the project tree
        private void AddDeleteContextMenuToProjectTree()
        {
            // Get or create context menu for the project tree
            ContextMenuStrip projectContextMenu = tvwProjects.ContextMenuStrip ?? new ContextMenuStrip();

            // Add a menu item to delete the project
            ToolStripMenuItem deleteMenuItem = new ToolStripMenuItem("Delete Project");
            deleteMenuItem.ForeColor = System.Drawing.Color.Red;  // Visual indicator for destructive action
            deleteMenuItem.Click += async (sender, e) =>
            {
                // Check if a node is selected
                if (tvwProjects.SelectedNode == null || tvwProjects.SelectedNode.Parent != null)
                {
                    return;
                }

                // Get the project folder
                string projectFolder = tvwProjects.SelectedNode.Tag as string;
                if (string.IsNullOrEmpty(projectFolder) || !Directory.Exists(projectFolder))
                {
                    return;
                }

                // Get the project name
                string projectName = tvwProjects.SelectedNode.Text;

                // Show confirmation dialog
                if (MessageBox.Show(
                    $"Are you sure you want to delete the project '{projectName}'?\n\nThis action cannot be undone!",
                    "Confirm Project Deletion",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2) == DialogResult.Yes)
                {
                    using (var deleteDialog = new ProjectDeleteDialog(projectName, projectFolder))
                    {
                        if (deleteDialog.ShowDialog(this) == DialogResult.OK)
                        {
                            // Deletion was successful, refresh the project tree
                            LoadProjects();
                        }
                    }
                }
            };

            // Add the menu item to the context menu
            projectContextMenu.Items.Add(deleteMenuItem);

            // Assign the updated context menu to the project tree
            tvwProjects.ContextMenuStrip = projectContextMenu;
        }

        // Custom dialog for project copying with progress bar
        private class ProjectCopyDialog : Form
        {
            private TextBox txtProjectName;
            private ProgressBar progressBar;
            private Label lblStatus;
            private Button btnStartCopy;
            private Button btnCancel;
            private bool isCopying = false;
            private bool operationCompleted = false;
            private string projectFolder;
            private string originalName;

            public ProjectCopyDialog(string originalName, string projectFolder)
            {
                this.originalName = originalName;
                this.projectFolder = projectFolder;
                
                // Configure the dialog
                Text = "Copy Project";
                Width = 450;
                Height = 220;
                FormBorderStyle = FormBorderStyle.FixedDialog;
                StartPosition = FormStartPosition.CenterParent;
                MaximizeBox = false;
                MinimizeBox = false;
                
                // Create project name label
                var lblPrompt = new Label
                {
                    Text = "Enter a name for the new project:",
                    Left = 10,
                    Top = 15,
                    AutoSize = true
                };
                Controls.Add(lblPrompt);

                // Create project name text box
                txtProjectName = new TextBox
                {
                    Text = $"{originalName}-copy",
                    Left = 10,
                    Top = lblPrompt.Bottom + 10,
                    Width = 420
                };
                Controls.Add(txtProjectName);
                
                // Create progress bar
                progressBar = new ProgressBar
                {
                    Style = ProgressBarStyle.Blocks,
                    Left = 10,
                    Top = txtProjectName.Bottom + 20,
                    Width = 420,
                    Height = 23,
                    Minimum = 0,
                    Maximum = 100,
                    Value = 0
                };
                Controls.Add(progressBar);
                
                // Create status label
                lblStatus = new Label
                {
                    Text = "Click 'Start Copy' to begin copying the project",
                    Left = 10,
                    Top = progressBar.Bottom + 10,
                    Width = 420,
                    AutoSize = true
                };
                Controls.Add(lblStatus);

                // Create Start Copy button
                btnStartCopy = new Button
                {
                    Text = "Start Copy",
                    Left = 260,
                    Top = lblStatus.Bottom + 15,
                    Width = 85,
                    Height = 30
                };
                Controls.Add(btnStartCopy);

                // Create Cancel button
                btnCancel = new Button
                {
                    Text = "Cancel",
                    Left = 350,
                    Top = btnStartCopy.Top,
                    Width = 85,
                    Height = 30
                };
                Controls.Add(btnCancel);
                
                // Wire up event handlers
                btnStartCopy.Click += async (s, e) => 
                {
                    if (operationCompleted)
                    {
                        DialogResult = DialogResult.OK;
                        Close();
                        return;
                    }
                    
                    if (!isCopying)
                    {
                        isCopying = true;
                        await StartCopyProcess();
                    }
                };
                
                btnCancel.Click += (s, e) => 
                {
                    if (isCopying && !operationCompleted)
                    {
                        // Ask for confirmation if the operation is ongoing
                        if (MessageBox.Show("Are you sure you want to cancel the copy operation?",
                            "Cancel Copy", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                        {
                            DialogResult = DialogResult.Cancel;
                            Close();
                        }
                    }
                    else
                    {
                        DialogResult = DialogResult.Cancel;
                        Close();
                    }
                };
                
                // Handle form closing
                FormClosing += (s, e) => 
                {
                    if (isCopying && !operationCompleted && e.CloseReason == CloseReason.UserClosing)
                    {
                        // Ask for confirmation if the operation is ongoing
                        if (MessageBox.Show("Are you sure you want to cancel the copy operation?",
                            "Cancel Copy", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                        {
                            e.Cancel = true; // Cancel the form closing
                        }
                    }
                };
            }

            // Start the copy process after user clicks the Start Copy button
            private async Task StartCopyProcess()
            {
                try
                {
                    // Disable controls during copy
                    txtProjectName.ReadOnly = true;
                    btnStartCopy.Enabled = false;
                    btnStartCopy.Text = "Copying...";
                    
                    // Get the project name from the text box
                    string newProjectName = txtProjectName.Text.Trim();
                    if (string.IsNullOrWhiteSpace(newProjectName))
                    {
                        newProjectName = $"{originalName}-copy";
                    }
                    
                    // Sanitize the name to be a valid folder name
                    newProjectName = SanitizeFileName(newProjectName);
                    
                    string newProjectFolder = Path.Combine(Path.GetDirectoryName(projectFolder), newProjectName);

                    // Handle cases where the copy name already exists
                    int copyIndex = 1;
                    while (Directory.Exists(newProjectFolder))
                    {
                        newProjectName = $"{originalName}-copy-{copyIndex}";
                        newProjectFolder = Path.Combine(Path.GetDirectoryName(projectFolder), newProjectName);
                        copyIndex++;
                    }

                    // Count total files for progress tracking
                    SetStatus("Counting files...");
                    int totalFiles = CountFilesRecursively(projectFolder);
                    InitializeProgressBar(totalFiles);
                    
                    // Create a shared counter for tracking copy progress across recursive calls
                    var progress = new SharedCopyProgress();
                    
                    // Create progress reporter that updates dialog with the current progress
                    var progressReporter = new Progress<SharedCopyProgress>(p => {
                        UpdateProgress(p.FilesCopied, $"Copying files: {p.FilesCopied}/{totalFiles}");
                    });

                    // Set status
                    SetStatus("Copying project...");

                    // Perform the copy operation asynchronously with proper renaming
                    await Task.Run(() => DirectoryCopy(projectFolder, newProjectFolder, true, 
                                                      originalName, newProjectName, progress, progressReporter));
                    
                    // Enable controls and update status
                    operationCompleted = true;
                    btnStartCopy.Enabled = true;
                    btnStartCopy.Text = "Close";
                    SetStatus($"Project copied successfully to: {newProjectName}");
                    
                    // Set dialog result to OK so the caller knows the operation was successful
                    this.DialogResult = DialogResult.OK;
                }
                catch (Exception ex)
                {
                    SetStatus($"Error: {ex.Message}");
                    btnStartCopy.Enabled = true;
                    btnStartCopy.Text = "Try Again";
                    MessageBox.Show($"Failed to copy project: {ex.Message}", "Copy Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }

            // Helper method to count files recursively
            private int CountFilesRecursively(string directory)
            {
                int count = 0;
                try
                {
                    // Add all files in the current directory
                    count += Directory.GetFiles(directory).Length;

                    // Recursively count files in all subdirectories
                    foreach (var subDir in Directory.GetDirectories(directory))
                    {
                        count += CountFilesRecursively(subDir);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error counting files in {directory}: {ex.Message}");
                }
                return count;
            }

            // Helper method to sanitize file names
            private string SanitizeFileName(string fileName)
            {
                // Remove invalid characters
                char[] invalidChars = Path.GetInvalidFileNameChars();
                foreach (char c in invalidChars)
                {
                    fileName = fileName.Replace(c.ToString(), "");
                }
                return fileName.Trim();
            }

            // Helper method to copy directories
            private void DirectoryCopy(string sourceDirName, string destDirName, bool copySubDirs, 
                                      string originalName, string newName, SharedCopyProgress progress, 
                                      IProgress<SharedCopyProgress> progressReporter)
            {
                DirectoryInfo dir = new DirectoryInfo(sourceDirName);

                if (!dir.Exists)
                {
                    throw new DirectoryNotFoundException($"Source directory does not exist or could not be found: {sourceDirName}");
                }

                DirectoryInfo[] dirs = dir.GetDirectories();
                Directory.CreateDirectory(destDirName);

                // Copy and rename all files
                FileInfo[] files = dir.GetFiles();
                foreach (FileInfo file in files)
                {
                    string destFileName = file.Name;
                    
                    // Handle ISO file renaming
                    if (file.Name.Equals($"{originalName}.iso", StringComparison.OrdinalIgnoreCase))
                    {
                        destFileName = $"{newName}.iso";
                    }
                    
                    string tempPath = Path.Combine(destDirName, destFileName);
                    file.CopyTo(tempPath, false);
                    
                    // Update progress
                    progress.FilesCopied++;
                    progressReporter?.Report(progress);
                }

                // Copy subdirectories with special handling for BEC and ISO folders
                if (copySubDirs)
                {
                    foreach (DirectoryInfo subdir in dirs)
                    {
                        string newSubDirName = subdir.Name;
                        
                        // Handle BEC folder renaming
                        if (subdir.Name.Equals($"{originalName}_BEC", StringComparison.OrdinalIgnoreCase))
                        {
                            newSubDirName = $"{newName}_BEC";
                        }
                        // Handle ISO folder renaming
                        else if (subdir.Name.Equals($"{originalName}_ISO", StringComparison.OrdinalIgnoreCase))
                        {
                            newSubDirName = $"{newName}_ISO";
                        }
                        
                        string tempPath = Path.Combine(destDirName, newSubDirName);
                        DirectoryCopy(subdir.FullName, tempPath, true, originalName, newName, progress, progressReporter);
                    }
                }
            }

            // Initialize the progress bar with the total file count
            private void InitializeProgressBar(int maximum)
            {
                if (InvokeRequired)
                {
                    Invoke(new Action(() => InitializeProgressBar(maximum)));
                    return;
                }
                
                progressBar.Minimum = 0;
                progressBar.Maximum = maximum;
                progressBar.Value = 0;
            }

            // Update the progress bar and status message
            private void UpdateProgress(int value, string statusText)
            {
                if (InvokeRequired)
                {
                    Invoke(new Action(() => UpdateProgress(value, statusText)));
                    return;
                }
                
                progressBar.Value = Math.Min(value, progressBar.Maximum);
                lblStatus.Text = statusText;
            }

            // Set status message
            private void SetStatus(string status)
            {
                if (InvokeRequired)
                {
                    Invoke(new Action(() => SetStatus(status)));
                    return;
                }
                
                lblStatus.Text = status;
            }
        }

        // Custom dialog for project deletion with progress bar
        private class ProjectDeleteDialog : Form
        {
            private ProgressBar progressBar;
            private Label lblStatus;
            private Button btnCancel;
            private bool isDeleting = false;
            private string projectFolder;
            private string projectName;
            private CancellationTokenSource cancellationTokenSource;

            public ProjectDeleteDialog(string projectName, string projectFolder)
            {
                this.projectName = projectName;
                this.projectFolder = projectFolder;
                this.cancellationTokenSource = new CancellationTokenSource();

                // Configure the dialog
                Text = "Deleting Project";
                Width = 400;
                Height = 150;
                FormBorderStyle = FormBorderStyle.FixedDialog;
                StartPosition = FormStartPosition.CenterParent;
                MaximizeBox = false;
                MinimizeBox = false;

                // Create progress bar
                progressBar = new ProgressBar
                {
                    Style = ProgressBarStyle.Marquee,
                    MarqueeAnimationSpeed = 30,
                    Left = 10,
                    Top = 20,
                    Width = 370,
                    Height = 23
                };
                Controls.Add(progressBar);

                // Create status label
                lblStatus = new Label
                {
                    Text = "Preparing to delete project...",
                    Left = 10,
                    Top = progressBar.Bottom + 10,
                    Width = 370,
                    AutoSize = true
                };
                Controls.Add(lblStatus);

                // Create Cancel button
                btnCancel = new Button
                {
                    Text = "Cancel",
                    Left = 295,
                    Top = lblStatus.Bottom + 10,
                    Width = 85,
                    Height = 30
                };
                Controls.Add(btnCancel);

                // Wire up event handlers
                btnCancel.Click += (s, e) => 
                {
                    if (isDeleting)
                    {
                        if (MessageBox.Show("Are you sure you want to cancel the deletion?",
                            "Cancel Deletion", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                        {
                            cancellationTokenSource.Cancel();
                        }
                    }
                    else
                    {
                        DialogResult = DialogResult.Cancel;
                        Close();
                    }
                };

                // Start deletion process when form is shown
                Shown += async (s, e) => await StartDeletionProcess();
            }

            private async Task StartDeletionProcess()
            {
                try
                {
                    isDeleting = true;
                    btnCancel.Text = "Cancel";

                    // Set status
                    SetStatus("Deleting project files...");

                    // Delete the project directory and its contents
                    await Task.Run(() => 
                    {
                        try
                        {
                            // First, attempt to remove read-only attributes recursively
                            RemoveReadOnlyAttribute(new DirectoryInfo(projectFolder));

                            // Delete the directory and all its contents
                            Directory.Delete(projectFolder, true);
                        }
                        catch (OperationCanceledException)
                        {
                            throw; // Rethrow cancellation
                        }
                        catch (Exception ex)
                        {
                            throw new Exception($"Failed to delete project: {ex.Message}", ex);
                        }
                    }, cancellationTokenSource.Token);

                    SetStatus("Project deleted successfully.");
                    btnCancel.Text = "Close";
                    DialogResult = DialogResult.OK;
                    await Task.Delay(1000); // Brief pause to show success
                    Close();
                }
                catch (OperationCanceledException)
                {
                    SetStatus("Deletion cancelled.");
                    btnCancel.Text = "Close";
                    DialogResult = DialogResult.Cancel;
                }
                catch (Exception ex)
                {
                    SetStatus($"Error: {ex.Message}");
                    btnCancel.Text = "Close";
                    MessageBox.Show(ex.Message, "Delete Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    DialogResult = DialogResult.Cancel;
                }
                finally
                {
                    isDeleting = false;
                }
            }

            private void RemoveReadOnlyAttribute(DirectoryInfo dirInfo)
            {
                foreach (var file in dirInfo.GetFiles())
                {
                    if (file.IsReadOnly)
                        file.IsReadOnly = false;
                }

                foreach (var dir in dirInfo.GetDirectories())
                {
                    RemoveReadOnlyAttribute(dir);
                }
            }

            private void SetStatus(string status)
            {
                if (InvokeRequired)
                {
                    Invoke(new Action(() => SetStatus(status)));
                    return;
                }

                lblStatus.Text = status;
            }

            protected override void OnFormClosing(FormClosingEventArgs e)
            {
                if (isDeleting && e.CloseReason == CloseReason.UserClosing)
                {
                    if (MessageBox.Show("Are you sure you want to cancel the deletion?",
                        "Cancel Deletion", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        cancellationTokenSource.Cancel();
                    }
                    e.Cancel = true;
                }
                base.OnFormClosing(e);
            }
        }

        // Class to track copy progress across recursive calls
        private class SharedCopyProgress
        {
            public int FilesCopied { get; set; } = 0;
        }

        // Helper method to count total files recursively
        private int CountFilesRecursively(string directory)
        {
            int count = 0;
            try
            {
                // Add all files in the current directory
                count += Directory.GetFiles(directory).Length;

                // Recursively count files in all subdirectories
                foreach (var subDir in Directory.GetDirectories(directory))
                {
                    count += CountFilesRecursively(subDir);
                }
            }
            catch (Exception ex)
            {
                // Log error but continue counting
                Console.WriteLine($"Error counting files in {directory}: {ex.Message}");
            }
            return count;
        }

        // Helper method to copy directories with proper renaming and progress reporting
        private static void DirectoryCopy(
            string sourceDirName, 
            string destDirName, 
            bool copySubDirs, 
            string originalName, 
            string newName, 
            SharedCopyProgress progress, 
            IProgress<SharedCopyProgress> progressReporter = null)
        {
            DirectoryInfo dir = new DirectoryInfo(sourceDirName);

            if (!dir.Exists)
            {
                throw new DirectoryNotFoundException($"Source directory does not exist or could not be found: {sourceDirName}");
            }

            DirectoryInfo[] dirs = dir.GetDirectories();
            Directory.CreateDirectory(destDirName);

            // Copy and rename all files (e.g., originalName.iso to newName.iso)
            FileInfo[] files = dir.GetFiles();
            foreach (FileInfo file in files)
            {
                string destFileName = file.Name;
                
                // Handle ISO file renaming
                if (file.Name.Equals($"{originalName}.iso", StringComparison.OrdinalIgnoreCase))
                {
                    destFileName = $"{newName}.iso";
                }
                
                string tempPath = Path.Combine(destDirName, destFileName);
                file.CopyTo(tempPath, false);
                
                // Update progress
                progress.FilesCopied++;
                progressReporter?.Report(progress);
            }

            // Copy subdirectories with special handling for BEC and ISO folders
            if (copySubDirs)
            {
                foreach (DirectoryInfo subdir in dirs)
                {
                    string newSubDirName = subdir.Name;
                    
                    // Handle BEC folder renaming
                    if (subdir.Name.Equals($"{originalName}_BEC", StringComparison.OrdinalIgnoreCase))
                    {
                        newSubDirName = $"{newName}_BEC";
                    }
                    // Handle ISO folder renaming
                    else if (subdir.Name.Equals($"{originalName}_ISO", StringComparison.OrdinalIgnoreCase))
                    {
                        newSubDirName = $"{newName}_ISO";
                    }
                    
                    string tempPath = Path.Combine(destDirName, newSubDirName);
                    DirectoryCopy(subdir.FullName, tempPath, true, originalName, newName, progress, progressReporter);
                }
            }
        }

        // Helper method to sanitize file/folder names
        private string SanitizeFileName(string fileName)
        {
            // Remove invalid characters
            char[] invalidChars = Path.GetInvalidFileNameChars();
            foreach (char c in invalidChars)
            {
                fileName = fileName.Replace(c.ToString(), "");
            }
            return fileName.Trim();
        }

        // This method should be called during form initialization (in the Form_Load event)
        private void InitializeGameLauncher()
        {
            // Load app settings to ensure Dolphin path is available
            if (appSettings == null)
            {
                LoadAppSettings();
            }
            
            // Initialize the auto launch checkbox
            UpdateAutoLaunchDolphinCheckbox();
            
            // Add context menu to project tree
            AddLaunchContextMenuToProjectTree();
            AddCopyContextMenuToProjectTree();
            AddDeleteContextMenuToProjectTree(); // Add this line
        }
    }
}
