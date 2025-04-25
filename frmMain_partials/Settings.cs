using System;
using System.IO;
using System.Text.Json;
using ModdingGUI.Forms;
using ModdingGUI.Models;

namespace ModdingGUI
{
    public partial class frmMain
    {
        // Constants for the settings file
        private const string APP_SETTINGS_FILENAME = "settings.json";
        private AppSettings appSettings;

        // Method to load application settings
        private void LoadAppSettings()
        {
            try
            {
                string settingsPath = Path.Combine(GetAppDirectory(), APP_SETTINGS_FILENAME);
                
                // If settings file doesn't exist, create default settings
                if (!File.Exists(settingsPath))
                {
                    appSettings = new AppSettings();
                    SaveAppSettings(); // Create the file with default settings
                    AppendLog("Default settings created.", InfoColor);
                    MessageBox.Show(
                        "Settings file not found. Head to File>Settings to customize your experience!",
                        "Settings File Created",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }
                
                // Read and deserialize the settings
                string jsonString = File.ReadAllText(settingsPath);
                appSettings = JsonSerializer.Deserialize<AppSettings>(jsonString);
                
                if (appSettings == null)
                {
                    appSettings = new AppSettings();
                }
                
                // Apply settings to the application
                ApplyAppSettings();
                
                AppendLog("Settings loaded successfully.", InfoColor);
            }
            catch (Exception ex)
            {
                AppendLog($"Error loading settings: {ex.Message}", ErrorColor);
                appSettings = new AppSettings(); // Use default settings on error
            }
        }
        
        // Method to save application settings
        private void SaveAppSettings()
        {
            try
            {
                string settingsPath = Path.Combine(GetAppDirectory(), APP_SETTINGS_FILENAME);
                
                // Serialize the settings with indentation
                var options = new JsonSerializerOptions { WriteIndented = true };
                string jsonString = JsonSerializer.Serialize(appSettings, options);
                
                // Write to the settings file
                File.WriteAllText(settingsPath, jsonString);
                
                AppendLog("Settings saved successfully.", SuccessColor);
            }
            catch (Exception ex)
            {
                AppendLog($"Error saving settings: {ex.Message}", ErrorColor);
            }
        }
        
        // Method to apply settings throughout the application
        private void ApplyAppSettings()
        {
            // Apply general settings
            chbValidationSkip.Checked = !appSettings.ValidateBeforePacking;
            
            // Apply randomizer settings
            randomizerLogsMenuItem.Checked = appSettings.EnableRandomizerLogs;
            testIterations = appSettings.DefaultRandomIterations;
            
            // Apply patching settings
            // (Apply any patching settings here)
            
            // Apply UI settings
            if (appSettings.ShowAdvancedOptions)
            {
                // Show advanced options/tabs if any
            }
            
            // Apply Dolphin auto-launch setting
            UpdateAutoLaunchDolphinCheckbox();
        }

        // Method to update the auto launch Dolphin checkbox visibility and state
        private void UpdateAutoLaunchDolphinCheckbox()
        {
            // Only show the checkbox if Dolphin path is valid
            bool isDolphinValid = IsDolphinPathValid();
            
            chbAutoLaunchDolphin.Visible = isDolphinValid;
            
            // If visible, set its checked state from settings
            if (isDolphinValid)
            {
                chbAutoLaunchDolphin.Checked = appSettings.AutoLaunchDolphin;
            }
        }
        
        // Event handler for the auto launch Dolphin checkbox
        private void chbAutoLaunchDolphin_CheckedChanged(object sender, EventArgs e)
        {
            // Update the setting in the AppSettings object
            if (appSettings != null)
            {
                appSettings.AutoLaunchDolphin = chbAutoLaunchDolphin.Checked;
                SaveAppSettings();
            }
        }
        
        // Event handler for the Settings menu item
        private void settingsMenuItem_Click(object sender, EventArgs e)
        {
            // Make sure settings are loaded
            if (appSettings == null)
            {
                LoadAppSettings();
            }
            
            using (var settingsForm = new SettingsForm(appSettings, OnSettingsSaved))
            {
                settingsForm.ShowDialog(this);
            }
        }
        
        // Callback when settings are saved in the settings form
        private void OnSettingsSaved(AppSettings newSettings)
        {
            // Update the application settings
            appSettings = newSettings;
            
            // Save the settings to file
            SaveAppSettings();
            
            // Apply the new settings
            ApplyAppSettings();
        }

        // Method to check if Dolphin path is set and valid
        private bool IsDolphinPathValid()
        {
            if (appSettings == null)
            {
                LoadAppSettings();
            }

            return !string.IsNullOrEmpty(appSettings.DolphinExecutablePath) && 
                   File.Exists(appSettings.DolphinExecutablePath);
        }

        // Method to launch a game with Dolphin
        private void LaunchGameWithDolphin(string isoPath)
        {
            if (!IsDolphinPathValid())
            {
                MessageBox.Show(
                    "Dolphin Emulator path is not set or invalid. Please set it in Settings.",
                    "Invalid Dolphin Path",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            if (!File.Exists(isoPath))
            {
                MessageBox.Show(
                    "The ISO file does not exist.",
                    "Invalid ISO Path",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            // Check if auto-launch is enabled (use the setting or default to true if not set)
            bool autoLaunch = appSettings?.AutoLaunchDolphin ?? true;
            
            // If auto-launch is disabled, ask for confirmation
            if (!autoLaunch)
            {
                DialogResult result = MessageBox.Show(
                    $"Would you like to launch {Path.GetFileName(isoPath)} with Dolphin Emulator?",
                    "Launch Game",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);
                
                if (result != DialogResult.Yes)
                {
                    return; // User decided not to launch
                }
            }

            try
            {
                AppendLog($"Launching game: {Path.GetFileName(isoPath)} with Dolphin...", InfoColor, rtbPackOutput);
                
                // Create process to launch Dolphin with the ISO
                System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = appSettings.DolphinExecutablePath,
                    Arguments = $"\"{isoPath}\"",
                    UseShellExecute = true
                };

                System.Diagnostics.Process.Start(psi);
                AppendLog("Game launched successfully.", SuccessColor, rtbPackOutput);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to launch game: {ex.Message}",
                    "Launch Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                AppendLog($"Error launching game: {ex.Message}", ErrorColor, rtbPackOutput);
            }
        }
    }
}
