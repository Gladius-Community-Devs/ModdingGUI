using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace ModdingGUI.Models
{
    // Model class to store application settings
    public class AppSettings
    {
        // General settings
        public bool ValidateBeforePacking { get; set; } = false;
        public bool AutoOpenFolderAfterOperation { get; set; } = false;
        public string DefaultProjectDirectory { get; set; } = string.Empty;
        public string DolphinExecutablePath { get; set; } = string.Empty;
        public bool AutoLaunchDolphin { get; set; } = true;

        // Randomizer settings
        public bool EnableRandomizerLogs { get; set; } = false;
        public int DefaultRandomIterations { get; set; } = 10;
        
        // Patching settings
        public bool AutoCheckUpdates { get; set; } = true;
        public string LastUsedPatchServer { get; set; } = string.Empty;

        // UI settings
        public bool ShowAdvancedOptions { get; set; } = false;
        public DateTime? LastScpRecompileUtc { get; set; }


        // Default constructor for deserialization
        public AppSettings() { }

        // Deep copy constructor
        public AppSettings(AppSettings source)
        {
            ValidateBeforePacking = source.ValidateBeforePacking;
            AutoOpenFolderAfterOperation = source.AutoOpenFolderAfterOperation;
            DefaultProjectDirectory = source.DefaultProjectDirectory;
            DolphinExecutablePath = source.DolphinExecutablePath;
            AutoLaunchDolphin = source.AutoLaunchDolphin;
            EnableRandomizerLogs = source.EnableRandomizerLogs;
            DefaultRandomIterations = source.DefaultRandomIterations;
            AutoCheckUpdates = source.AutoCheckUpdates;
            LastUsedPatchServer = source.LastUsedPatchServer;
            ShowAdvancedOptions = source.ShowAdvancedOptions;
        }
    }
    public class ProjectSettings
    {
        public DateTime? LastScpRecompileUtc { get; set; }
    }
}
