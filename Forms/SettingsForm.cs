using System;
using System.IO;
using System.Windows.Forms;
using ModdingGUI.Models;

namespace ModdingGUI.Forms
{
    public partial class SettingsForm : Form
    {
        private readonly AppSettings _originalSettings;
        private AppSettings _currentSettings;
        private readonly Action<AppSettings> _onSave;

        public SettingsForm(AppSettings settings, Action<AppSettings> onSave)
        {
            InitializeComponent();
            _originalSettings = settings;
            _currentSettings = new AppSettings(settings);
            _onSave = onSave;
            PopulateFormFromSettings();
        }

        private void PopulateFormFromSettings()
        {
            // General settings
            chkValidateBeforePacking.Checked = _currentSettings.ValidateBeforePacking;
            chkAutoOpenFolder.Checked = _currentSettings.AutoOpenFolderAfterOperation;
            txtDefaultProjectDir.Text = _currentSettings.DefaultProjectDirectory;
            txtDolphinPath.Text = _currentSettings.DolphinExecutablePath;

            // Randomizer settings
            chkEnableRandomizerLogs.Checked = _currentSettings.EnableRandomizerLogs;
            numDefaultRandomIterations.Value = _currentSettings.DefaultRandomIterations;

            // Patching settings
            chkAutoCheckUpdates.Checked = _currentSettings.AutoCheckUpdates;
            txtLastUsedPatchServer.Text = _currentSettings.LastUsedPatchServer;

            // UI settings
            chkShowAdvancedOptions.Checked = _currentSettings.ShowAdvancedOptions;
        }

        private void UpdateSettingsFromForm()
        {
            // General settings
            _currentSettings.ValidateBeforePacking = chkValidateBeforePacking.Checked;
            _currentSettings.AutoOpenFolderAfterOperation = chkAutoOpenFolder.Checked;
            _currentSettings.DefaultProjectDirectory = txtDefaultProjectDir.Text;
            _currentSettings.DolphinExecutablePath = txtDolphinPath.Text;

            // Randomizer settings
            _currentSettings.EnableRandomizerLogs = chkEnableRandomizerLogs.Checked;
            _currentSettings.DefaultRandomIterations = (int)numDefaultRandomIterations.Value;

            // Patching settings
            _currentSettings.AutoCheckUpdates = chkAutoCheckUpdates.Checked;
            _currentSettings.LastUsedPatchServer = txtLastUsedPatchServer.Text;

            // UI settings
            _currentSettings.ShowAdvancedOptions = chkShowAdvancedOptions.Checked;
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            try
            {
                UpdateSettingsFromForm();
                _onSave(_currentSettings);
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving settings: {ex.Message}", "Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void btnBrowseProjectDir_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog folderDialog = new FolderBrowserDialog())
            {
                if (!string.IsNullOrEmpty(txtDefaultProjectDir.Text) && Directory.Exists(txtDefaultProjectDir.Text))
                {
                    folderDialog.SelectedPath = txtDefaultProjectDir.Text;
                }

                if (folderDialog.ShowDialog() == DialogResult.OK)
                {
                    txtDefaultProjectDir.Text = folderDialog.SelectedPath;
                }
            }
        }

        private void btnBrowseDolphinPath_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "Executable files (*.exe)|*.exe|All files (*.*)|*.*";
                openFileDialog.Title = "Select Dolphin Emulator Executable";

                if (!string.IsNullOrEmpty(txtDolphinPath.Text) && File.Exists(txtDolphinPath.Text))
                {
                    openFileDialog.InitialDirectory = Path.GetDirectoryName(txtDolphinPath.Text);
                }

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    string selectedPath = openFileDialog.FileName;
                    
                    // Verify if the selected file is likely to be Dolphin
                    string fileName = Path.GetFileName(selectedPath).ToLower();
                    if (!fileName.Contains("dolphin"))
                    {
                        var result = MessageBox.Show(
                            "The selected file does not appear to be Dolphin Emulator. Are you sure you want to use this file?",
                            "Verification",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Question);

                        if (result == DialogResult.No)
                        {
                            return;
                        }
                    }

                    txtDolphinPath.Text = selectedPath;
                }
            }
        }

        private void btnReset_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("Are you sure you want to reset all settings to their default values?", 
                "Reset Settings", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                _currentSettings = new AppSettings();
                PopulateFormFromSettings();
            }
        }
    }
}
