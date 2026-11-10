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
        public bool ValidateBeforePacking { get; set; } = true;

        /// <summary>
        /// Which palette to draw with: <c>dark</c> (the default) or <c>light</c>. Stored as text so a future third
        /// scheme does not need a schema bump.
        /// </summary>
        public string Theme { get; set; } = "dark";

        /// <summary>
        /// Settings-schema version for this file. Version 1 turned <see cref="ValidateBeforePacking"/> back on (it used
        /// to default to off, and a saved file keeps that), so the Pack tab runs the validation pass unless the user
        /// deliberately skips it.
        /// </summary>
        public int ValidationPassVersion { get; set; }
        public bool AutoOpenFolderAfterOperation { get; set; } = false;
        public string DefaultProjectDirectory { get; set; } = string.Empty;
        public string DolphinExecutablePath { get; set; } = string.Empty;
        /// <summary>
        /// Launch the packed ISO in Dolphin automatically. Off by default: it used to default to true, so simply
        /// setting a Dolphin path made every pack launch the emulator.
        /// </summary>
        public bool AutoLaunchDolphin { get; set; }

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
            ValidationPassVersion = source.ValidationPassVersion;
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
