using System;
using System.ComponentModel;
using System.IO;
using System.Windows.Forms;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using ModdingGUI.Core.Tools;

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
            string fullPath = Path.Combine(GetAppDirectory(), patchedIsoPath);

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
                if (!TryGetSelectedProject(out string projectFolder, out string projectName))
                {
                    return;
                }

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
            copyMenuItem.Click += (sender, e) =>
            {
                if (!TryGetSelectedProject(out string projectFolder, out string originalName))
                {
                    return;
                }

                try
                {
                    // Create and show the project copy dialog but don't start copying yet
                    using var progressDialog = new ProjectCopyDialog(originalName, projectFolder);

                    // Show dialog and wait for the user to press Start Copy or Cancel
                    if (progressDialog.ShowDialog(this) == DialogResult.OK)
                    {
                        // The user clicked Start Copy, and the dialog will handle the copy process
                        // After the copy completes successfully, refresh the project tree
                        LoadProjects();
                    }
                }
                catch (Exception ex)
                {
                    // Copying is a lot of file I/O; a failure here must be reported, not thrown into the void.
                    AppendLog($"Copy project failed: {ex.Message}", ErrorColor);
                    MessageBox.Show($"Copy project failed:\n\n{ex.Message}", "Copy Project",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            // Add the menu item to the context menu
            projectContextMenu.Items.Add(copyMenuItem);

            // Assign the updated context menu to the project tree
            tvwProjects.ContextMenuStrip = projectContextMenu;
        }

        /// <summary>
        /// Refresh and "open folder" on the project tree. Refresh is also on F5.
        /// </summary>
        private void AddProjectTreeExtras()
        {
            ContextMenuStrip menu = tvwProjects.ContextMenuStrip ?? new ContextMenuStrip();

            var refresh = new ToolStripMenuItem("Refresh Projects");
            refresh.ShortcutKeys = Keys.F5;
            refresh.Click += (_, _) => LoadProjects();

            var openFolder = new ToolStripMenuItem("Open Project Folder");
            openFolder.Click += (_, _) =>
            {
                if (!TryGetSelectedProject(out string projectFolder, out _)) return;

                try
                {
                    // Same route as everywhere else: ask the running shell, never spawn explorer.exe ourselves.
                    if (!ModdingGUI.Helpers.ShellFolderOpener.TryOpen(projectFolder, out string shellError))
                    {
                        throw new InvalidOperationException(shellError);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Could not open the folder:\n\n{ex.Message}", "Open Project Folder",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            var backups = new ToolStripMenuItem("Restore from Backup...");
            backups.Click += (_, _) =>
            {
                if (!TryGetSelectedProject(out string folder, out _)) return;
                using var dialog = new ModdingGUI.Forms.HistoryForm(folder);
                if (dialog.ShowDialog(this) == DialogResult.OK || dialog.RestoredAnything)
                {
                    LoadProjects();
                }
            };

            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(refresh);
            menu.Items.Add(openFolder);
            menu.Items.Add(backups);
            tvwProjects.ContextMenuStrip = menu;

            // F5 works whether or not the context menu is open.
            tvwProjects.KeyDown += (_, e) =>
            {
                if (e.KeyCode != Keys.F5) return;

                e.Handled = true;
                e.SuppressKeyPress = true;
                LoadProjects();
            };
        }

        /// <summary>
        /// A right-click does not move the TreeView selection on its own, and <see cref="LoadProjects"/> clears the
        /// tree (and therefore the selection) after every delete. Without this the second delete found
        /// <c>SelectedNode == null</c> and silently did nothing.
        /// </summary>
        private void SelectNodeUnderCursor(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right) return;

            TreeNode? node = tvwProjects.GetNodeAt(e.X, e.Y);
            if (node is not null) tvwProjects.SelectedNode = node;
        }

        /// <summary>
        /// The editors that currently have this project open, by name. Used to warn before deleting and to release
        /// them afterwards.
        /// </summary>
        private List<string> EditorsHoldingProject(string projectFolder)
        {
            string folder = projectFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            bool Holds(string? path) =>
                !string.IsNullOrEmpty(path) &&
                path!.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    .StartsWith(folder, StringComparison.OrdinalIgnoreCase);

            var holders = new List<string>();
            if (Holds(_becFolder)) holders.Add("Item editor");
            if (Holds(_skillEditorView?.ProjectFolder)) holders.Add("Skill editor");
            if (Holds(_gridBuilderView?.ProjectRoot)) holders.Add("Grid builder");
            if (Holds(txtPackPath.Text)) holders.Add("Team builder");
            return holders;
        }

        /// <summary>True when an open editor has edits that deleting the project would throw away.</summary>
        private bool EditorsHaveUnsavedWork(string projectFolder)
        {
            string folder = projectFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            bool Holds(string? path) =>
                !string.IsNullOrEmpty(path) &&
                path!.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    .StartsWith(folder, StringComparison.OrdinalIgnoreCase);

            return (_gridBuilderView is not null && Holds(_gridBuilderView.ProjectRoot) &&
                    _gridBuilderView.HasUnsavedChanges)
                   || (!string.IsNullOrEmpty(_currentDto?.Name) && Holds(_becFolder));
        }

        /// <summary>
        /// Clear every editor bound to a project, so no view keeps a path into a folder that is gone.
        /// </summary>
        private void ReleaseEditorsForProject(string projectFolder)
        {
            string folder = projectFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            bool Holds(string? path) =>
                !string.IsNullOrEmpty(path) &&
                path!.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    .StartsWith(folder, StringComparison.OrdinalIgnoreCase);

            if (Holds(_becFolder))
            {
                ClearEditorFields();
                ddlItemEditorAllItems.DataSource = null;
                _bindingListOfItems = new BindingList<ItemRow>();
                _becFolder = string.Empty;
                AppendLog("Item editor released: its project was deleted.", WarningColor);
            }

            if (Holds(_skillEditorView?.ProjectFolder)) _skillEditorView.ReleaseProject();
            if (Holds(_gridBuilderView?.ProjectRoot)) _gridBuilderView.ReleaseProject();

            if (Holds(txtPackPath.Text))
            {
                teamUnits.Clear();
                classDefinitions.Clear();
                gladiatorEntries.Clear();
                statSets.Clear();
                ddlTeamClasses.Items.Clear();
                ClearPreview();
                AppendLog("Team builder released: its project was deleted.", WarningColor);
            }
        }

        /// <summary>
        /// Resolve the project behind the tree selection, telling the user why nothing happens when it cannot.
        /// The menus used to return silently, which made a stale tree look like a broken feature.
        /// </summary>
        private bool TryGetSelectedProject(out string projectFolder, out string projectName)
        {
            projectFolder = string.Empty;
            projectName = string.Empty;

            TreeNode? node = tvwProjects.SelectedNode;
            if (node is null)
            {
                MessageBox.Show("Right-click a project first.", "Project",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            if (node.Parent is not null)
            {
                MessageBox.Show("Select the project itself, not one of its folders.", "Project",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            string? folder = node.Tag as string;
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                MessageBox.Show("That project folder no longer exists; refreshing the list.", "Project",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                LoadProjects();
                return false;
            }

            projectFolder = folder;
            projectName = node.Text;
            return true;
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
                // Prefer the node under the cursor: right-click selection can still race with a tree reload.
                TreeNode? node = tvwProjects.SelectedNode;
                if (node is null)
                {
                    MessageBox.Show("Right-click a project to delete it.", "Delete Project",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                if (node.Parent is not null)
                {
                    MessageBox.Show("Select the project itself, not one of its folders.", "Delete Project",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                string? projectFolder = node.Tag as string;
                if (string.IsNullOrEmpty(projectFolder) || !Directory.Exists(projectFolder))
                {
                    // The tree is stale (the folder went away behind the app): refresh instead of doing nothing.
                    MessageBox.Show("That project folder no longer exists; refreshing the list.", "Delete Project",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    LoadProjects();
                    return;
                }

                // Get the project name
                string projectName = node.Text;

                // Show confirmation dialog
                // An open editor loses its subject with the folder: say so before anything is deleted.
                var holders = EditorsHoldingProject(projectFolder);
                if (holders.Count > 0)
                {
                    bool unsaved = EditorsHaveUnsavedWork(projectFolder);
                    string warning = $"The {string.Join(" and ", holders)} currently "
                                     + (holders.Count == 1 ? "has" : "have")
                                     + $" '{projectName}' open.\n\n"
                                     + (unsaved
                                         ? "There is unsaved work in that project. Deleting it discards those edits."
                                         : "It will be closed when the project is deleted.");

                    if (MessageBox.Show(warning, "Project Is Open",
                            MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                            MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                    {
                        return;
                    }
                }

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
                            // Deletion was successful: forget the project anywhere it was selected.
                            if (string.Equals(txtRandomizerPath.Text.Trim(), projectFolder,
                                    StringComparison.OrdinalIgnoreCase))
                            {
                                txtRandomizerPath.Text = string.Empty;
                                btnRandomize.Enabled = false;
                            }

                            if (string.Equals(txtPackPath.Text.Trim(), projectFolder,
                                    StringComparison.OrdinalIgnoreCase))
                            {
                                txtPackPath.Text = string.Empty;
                                btnPack.Enabled = false;
                            }

                            // No editor may keep a path into a folder that is gone.
                            ReleaseEditorsForProject(projectFolder);
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
            private readonly CancellationTokenSource cancellationTokenSource = new();
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
                        if (MessageBox.Show("Are you sure you want to cancel the copy?", "Cancel Copy",
                                MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                        {
                            cancellationTokenSource.Cancel();
                            SetStatus("Cancelling...");
                        }

                        return;
                    }

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
            /// <summary>Where this attempt is writing, so a cancel can clean up after itself.</summary>
            private string? partialDestination;
            private string partialName = string.Empty;

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
                    partialDestination = newProjectFolder;

                    // Handle cases where the copy name already exists
                    int copyIndex = 1;
                    while (Directory.Exists(newProjectFolder))
                    {
                        newProjectName = $"{originalName}-copy-{copyIndex}";
                        newProjectFolder = Path.Combine(Path.GetDirectoryName(projectFolder), newProjectName);
                        copyIndex++;
                    }

                    partialDestination = newProjectFolder;
                    partialName = newProjectName;

                    // Count first so the bar has a denominator, then copy with per-file progress.
                    SetStatus("Counting files...");
                    var totals = ProjectFileOperations.Measure(projectFolder);
                    InitializeProgressBar(totals.Files);

                    var progress = new Progress<FileOperationProgress>(p =>
                    {
                        UpdateProgress(p.FilesDone,
                            $"Copying: {p.FilesDone:N0} / {p.FilesTotal:N0} files ({p.Percent}%)");
                    });

                    SetStatus("Copying project...");

                    await Task.Run(() =>
                        ProjectFileOperations.Copy(projectFolder, newProjectFolder, originalName, newProjectName,
                            progress, cancellationTokenSource.Token),
                        cancellationTokenSource.Token);
                    
                    // Enable controls and update status
                    operationCompleted = true;
                    btnStartCopy.Enabled = true;
                    btnStartCopy.Text = "Close";
                    SetStatus($"Project copied successfully to: {newProjectName}");
                    
                    // Set dialog result to OK so the caller knows the operation was successful
                    this.DialogResult = DialogResult.OK;
                }
                catch (OperationCanceledException)
                {
                    isCopying = false;

                    // A half copy is not a project: remove it, or - if a file is still locked - rename it so it is
                    // obviously incomplete instead of looking like a real project in the tree.
                    string? remnant = partialDestination is null
                        ? null
                        : CleanUpPartialCopy(partialDestination, partialName);
                    SetStatus(remnant is null
                        ? "Copy cancelled; the incomplete copy was removed."
                        : $"Copy cancelled. Incomplete copy kept as '{Path.GetFileName(remnant)}'.");

                    btnStartCopy.Enabled = true;
                    btnStartCopy.Text = "Try Again";
                }
                catch (Exception ex)
                {
                    isCopying = false;
                    SetStatus($"Error: {ex.Message}");
                    btnStartCopy.Enabled = true;
                    btnStartCopy.Text = "Try Again";
                    MessageBox.Show($"Failed to copy project: {ex.Message}", "Copy Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }

            /// <summary>
            /// Remove what a cancelled copy left behind. Returns the folder it kept, or null when nothing remains.
            /// </summary>
            private string? CleanUpPartialCopy(string folder, string newName)
            {
                try
                {
                    if (!Directory.Exists(folder)) return null;

                    ProjectFileOperations.Delete(folder);
                    return null;
                }
                catch (Exception)
                {
                    // Something is still holding a file. Label the remnant rather than leaving a plausible-looking
                    // project in the tree: '<name>-copy-partial'.
                    try
                    {
                        string partial = folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                                         + "-copy-partial";

                        int suffix = 1;
                        string candidate = partial;
                        while (Directory.Exists(candidate))
                        {
                            candidate = $"{partial}-{suffix++}";
                        }

                        Directory.Move(folder, candidate);
                        return candidate;
                    }
                    catch (Exception)
                    {
                        return folder;
                    }
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
                    Style = ProgressBarStyle.Blocks,
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

                    // Delete file by file so the bar means something and Cancel can actually stop it.
                    var progress = new Progress<FileOperationProgress>(p =>
                    {
                        InitializeProgressBar(p.FilesTotal);
                        UpdateProgress(p.FilesDone,
                            $"Deleting: {p.FilesDone:N0} / {p.FilesTotal:N0} files ({p.Percent}%)");
                    });

                    await Task.Run(() =>
                    {
                        try
                        {
                            ProjectFileOperations.Delete(projectFolder, progress, cancellationTokenSource.Token);
                        }
                        catch (OperationCanceledException)
                        {
                            throw;
                        }
                        catch (Exception ex)
                        {
                            throw new Exception($"Failed to delete project: {ex.Message}", ex);
                        }
                    }, cancellationTokenSource.Token);

                    SetStatus("Project deleted successfully.");
                    btnCancel.Text = "Close";

                    // Setting DialogResult closes a modal dialog, so it has to be the last thing that happens:
                    // the old code set it first and then touched a disposed form.
                    await Task.Delay(600);
                    if (!IsDisposed) DialogResult = DialogResult.OK;
                }
                catch (OperationCanceledException)
                {
                    isDeleting = false;
                    SetStatus("Deletion cancelled.");
                    btnCancel.Text = "Close";
                    if (!IsDisposed) DialogResult = DialogResult.Cancel;
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

            /// <summary>Set the bar up for a known number of files.</summary>
            private void InitializeProgressBar(int maximum)
            {
                if (InvokeRequired)
                {
                    Invoke(new Action(() => InitializeProgressBar(maximum)));
                    return;
                }

                progressBar.Maximum = Math.Max(1, maximum);
                progressBar.Value = 0;
            }

            /// <summary>Advance the bar and the status line together.</summary>
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
                        // Cancel and let the form go: the deletion task reports back through DialogResult.
                        isDeleting = false;
                        cancellationTokenSource.Cancel();
                    }
                    else
                    {
                        e.Cancel = true;
                    }
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
            AddDeleteContextMenuToProjectTree();
            AddProjectTreeExtras();

            // The project node's tooltip carries the path and whether the project has been randomized.
            tvwProjects.ShowNodeToolTips = true;
        }
    }
}
