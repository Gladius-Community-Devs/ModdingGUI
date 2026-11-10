using ModdingGUI.Forms;
using ModdingGUI.Models;
using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using ModdingGUI.Core;

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

                // Older builds defaulted this to true, so a settings file could say "auto-launch" while no Dolphin
                // was configured at all - and once one was set, every pack launched the emulator. A flag that could
                // never have been a real choice is normalised away; ticking the box later still works.
                if (string.IsNullOrWhiteSpace(appSettings.DolphinExecutablePath))
                {
                    appSettings.AutoLaunchDolphin = false;
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
            // Migration: the pack pass used to be skipped by default and a saved settings.json keeps that off for
            // existing users. Version 1 turns it back on, once.
            if (appSettings.ValidationPassVersion < 1)
            {
                appSettings.ValidateBeforePacking = true;
                appSettings.ValidationPassVersion = 1;
            }

            chbValidationSkip.Checked = !appSettings.ValidateBeforePacking;

            // The scheme is applied by the shell (it owns the control tree); this only records the choice.
            if (darkThemeMenuItem is not null) darkThemeMenuItem.Checked = UiTheme.Scheme == UiColorScheme.Dark;
            
            // Apply randomizer settings
            randomizerLogsMenuItem.Checked = appSettings.EnableRandomizerLogs;
            
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

            // The projects folder may have moved: re-scan rather than waiting for the next refresh.
            LoadProjects();
        }

        /// <summary>
        /// The folder to scan for projects: the configured one when it exists, otherwise the app folder. Warns once
        /// when a configured folder has gone missing, so the fallback is not silent.
        /// </summary>
        private string AppDirectoryForProjects()
        {
            if (appSettings == null) LoadAppSettings();

            string configured = appSettings?.DefaultProjectDirectory ?? string.Empty;
            string resolved = ProjectRoots.ResolveProjectsFolder(configured, GetAppDirectory());

            if (!string.IsNullOrWhiteSpace(configured) && !Directory.Exists(configured))
            {
                AppendLog($"Configured projects folder not found ({configured}); using the app folder.",
                    WarningColor);
            }

            return resolved;
        }

        /// <summary>Where new projects are created.</summary>
        private string ProjectsFolder() => AppDirectoryForProjects();

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

                // No preference check here: an explicit "Launch with Dolphin" click must launch even when
                // auto-launch is off, and the pack handler already decides whether to call this at all.

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
