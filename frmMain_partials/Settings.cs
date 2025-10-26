using ModdingGUI.Forms;
using ModdingGUI.Models;
using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

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
            try
            {
                // Require a valid Dolphin executable path
                var dolphinExePath = appSettings?.DolphinExecutablePath;
                if (string.IsNullOrWhiteSpace(dolphinExePath) || !File.Exists(dolphinExePath))
                {
                    AppendLog("Dolphin auto-launch skipped — no valid Dolphin executable path is configured.", WarningColor, rtbPackOutput);
                    return;
                }

                // Respect live user preference (checkbox when visible; fallback to saved setting)
                bool autoLaunch =
                    (chbAutoLaunchDolphin != null && chbAutoLaunchDolphin.Visible)
                        ? chbAutoLaunchDolphin.Checked
                        : (appSettings?.AutoLaunchDolphin == true);
                if (!autoLaunch)
                {
                    AppendLog("Dolphin auto-launch is disabled. Skipping launch.", WarningColor, rtbPackOutput);
                    return;
                }

                // Verify ISO exists
                if (string.IsNullOrWhiteSpace(isoPath) || !File.Exists(isoPath))
                {
                    AppendLog($"ISO not found at path: {isoPath}", ErrorColor, rtbPackOutput);
                    return;
                }

                // Close any existing Dolphin instance (same executable) to ensure a clean start
                string exeName = Path.GetFileNameWithoutExtension(dolphinExePath);
                var existing = Process.GetProcessesByName(exeName);
                foreach (var p in existing)
                {
                    bool sameBinary = false;
                    try
                    {
                        string procPath = p.MainModule?.FileName;
                        if (!string.IsNullOrEmpty(procPath))
                            sameBinary = string.Equals(procPath, dolphinExePath, StringComparison.OrdinalIgnoreCase);
                    }
                    catch
                    {
                        sameBinary = true; // fallback by name
                    }

                    if (!sameBinary || p.HasExited) continue;

                    AppendLog("Closing existing Dolphin instance…", InfoColor, rtbPackOutput);
                    try
                    {
                        if (p.CloseMainWindow())
                        {
                            if (p.WaitForExit(2000))
                            {
                                AppendLog("Existing Dolphin closed gracefully.", SuccessColor, rtbPackOutput);
                                continue;
                            }
                        }
                        if (!p.HasExited)
                        {
                            p.Kill(true);
                            p.WaitForExit(2000);
                            AppendLog("Existing Dolphin was force-terminated.", WarningColor, rtbPackOutput);
                        }
                    }
                    catch (Exception ex)
                    {
                        AppendLog($"Failed to terminate existing Dolphin: {ex.Message}", ErrorColor, rtbPackOutput);
                    }
                }

                // Launch Dolphin with the ISO
                AppendLog($"Launching Dolphin with ISO: {isoPath}", InfoColor, rtbPackOutput);

                var psi = new ProcessStartInfo
                {
                    FileName = dolphinExePath,
                    Arguments = $"\"{isoPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(dolphinExePath)
                };

                Process.Start(psi);
                AppendLog("Dolphin launched successfully.", SuccessColor, rtbPackOutput);
            }
            catch (Exception ex)
            {
                AppendLog($"Failed to launch Dolphin: {ex.Message}", ErrorColor, rtbPackOutput);
            }
        }
        // Call this BEFORE packing to avoid "file in use" errors.
        // Returns true if it's safe to proceed; false if we aborted due to locks/errors.
        private bool EnsureDolphinClosedForPack(string isoPath)
        {
            try
            {
                var dolphinExePath = appSettings?.DolphinExecutablePath;
                string exeName = !string.IsNullOrWhiteSpace(dolphinExePath)
                    ? Path.GetFileNameWithoutExtension(dolphinExePath)
                    : "Dolphin"; // fallback if path not set

                var procs = Process.GetProcessesByName(exeName);
                if (procs.Length == 0)
                    return true;

                // Check if ISO appears locked (when path exists). If we can't tell, we still close Dolphin to be safe.
                bool isoExists = !string.IsNullOrWhiteSpace(isoPath) && File.Exists(isoPath);
                bool isoLocked = isoExists && !CanOpenExclusively(isoPath);

                AppendLog(isoLocked
                    ? "Dolphin appears to be using the ISO. Closing it before packing…"
                    : "Dolphin is running. Closing it before packing to avoid file locks…",
                    InfoColor, rtbPackOutput);

                foreach (var p in procs)
                {
                    bool sameBinary = false;
                    try
                    {
                        // Prefer exact binary match when configured
                        string procPath = p.MainModule?.FileName;
                        if (!string.IsNullOrEmpty(procPath) && !string.IsNullOrWhiteSpace(dolphinExePath))
                            sameBinary = string.Equals(procPath, dolphinExePath, StringComparison.OrdinalIgnoreCase);
                        else
                            sameBinary = true; // no reliable path; proceed by name
                    }
                    catch
                    {
                        sameBinary = true; // access denied; proceed by name
                    }

                    if (!sameBinary || p.HasExited) continue;

                    try
                    {
                        if (p.CloseMainWindow())
                        {
                            if (p.WaitForExit(2000))
                            {
                                AppendLog("Existing Dolphin closed gracefully.", SuccessColor, rtbPackOutput);
                                continue;
                            }
                        }

                        if (!p.HasExited)
                        {
                            p.Kill(true);
                            p.WaitForExit(2000);
                            AppendLog("Existing Dolphin was force-terminated.", WarningColor, rtbPackOutput);
                        }
                    }
                    catch (Exception ex)
                    {
                        AppendLog($"Failed to terminate existing Dolphin: {ex.Message}", ErrorColor, rtbPackOutput);
                        return false;
                    }
                }

                // Re-check ISO lock if applicable
                if (isoExists && !CanOpenExclusively(isoPath))
                {
                    AppendLog("The ISO is still locked by another process. Aborting pack.", ErrorColor, rtbPackOutput);
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                AppendLog($"Error while preparing for pack: {ex.Message}", ErrorColor, rtbPackOutput);
                return false;
            }
        }

        // Helper: try exclusive open to detect locks
        private bool CanOpenExclusively(string path)
        {
            try
            {
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
                return true;
            }
            catch
            {
                return false;
            }
        }

    }
}
