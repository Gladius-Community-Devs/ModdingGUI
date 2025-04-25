using System;
using System.IO;
using System.Windows.Forms;
using System.Diagnostics;

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
        }
    }
}
