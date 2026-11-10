namespace ModdingGUI.Core.Settings;

using System;
using System.IO;
using System.Text.Json;

/// <summary>
/// Reads <c>settings.json</c> into an <see cref="AppSettingsSnapshot"/>. Reading only - Core has no settings writer,
/// which is how "viewable, not changeable" is enforced rather than promised.
/// </summary>
public static class AppSettingsReader
{
    /// <summary>The file name the shell writes, beside the executable.</summary>
    public const string FileName = "settings.json";

    /// <summary>
    /// Read the settings beside an application directory.
    /// </summary>
    /// <param name="appDirectory">The folder the application runs from.</param>
    public static AppSettingsSnapshot ReadFromAppDirectory(string appDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appDirectory);
        return Read(Path.Combine(appDirectory, FileName), appDirectory);
    }

    /// <summary>
    /// Read a specific settings file. A missing or unreadable file yields defaults plus the reason, because a caller
    /// asking "where is this configured?" needs an answer even on a fresh install.
    /// </summary>
    /// <param name="settingsFilePath">Path to the settings file.</param>
    /// <param name="appDirectory">The application folder, used to resolve the effective projects folder.</param>
    public static AppSettingsSnapshot Read(string settingsFilePath, string? appDirectory = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(settingsFilePath);

        string app = string.IsNullOrWhiteSpace(appDirectory)
            ? Path.GetDirectoryName(Path.GetFullPath(settingsFilePath)) ?? Directory.GetCurrentDirectory()
            : appDirectory!;

        if (!File.Exists(settingsFilePath))
        {
            return Build(Defaults(), settingsFilePath, app, existing: false,
                problem: $"No settings file at '{settingsFilePath}'; defaults shown. The shell writes it on first run.");
        }

        try
        {
            var stored = JsonSerializer.Deserialize<Stored>(File.ReadAllText(settingsFilePath),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true, AllowTrailingCommas = true });

            if (stored is null)
            {
                return Build(Defaults(), settingsFilePath, app, existing: false,
                    problem: "The settings file was empty; defaults shown.");
            }

            return Build(stored, settingsFilePath, app, existing: true, problem: null);
        }
        catch (JsonException ex)
        {
            return Build(Defaults(), settingsFilePath, app, existing: false,
                problem: $"The settings file is not valid JSON ({ex.Message}); defaults shown.");
        }
        catch (IOException ex)
        {
            return Build(Defaults(), settingsFilePath, app, existing: false,
                problem: $"The settings file could not be read ({ex.Message}); defaults shown.");
        }
        catch (UnauthorizedAccessException ex)
        {
            return Build(Defaults(), settingsFilePath, app, existing: false,
                problem: $"The settings file is not accessible ({ex.Message}); defaults shown.");
        }
    }

    private static AppSettingsSnapshot Build(Stored stored, string settingsFilePath, string appDirectory,
        bool existing, string? problem)
    {
        string projectsFolder;
        try
        {
            projectsFolder = ProjectRoots.ResolveProjectsFolder(stored.DefaultProjectDirectory, appDirectory);
        }
        catch (Exception)
        {
            projectsFolder = appDirectory;
        }

        return new AppSettingsSnapshot(
            SettingsFilePath: settingsFilePath,
            ProjectsFolder: projectsFolder,
            DolphinExecutablePath: stored.DolphinExecutablePath ?? string.Empty,
        DiscImageConverterPath: stored.DiscImageConverterPath ?? string.Empty,
            AutoLaunchDolphin: stored.AutoLaunchDolphin,
            ValidateBeforePacking: stored.ValidateBeforePacking,
            AutoOpenFolderAfterOperation: stored.AutoOpenFolderAfterOperation,
            DefaultProjectDirectory: stored.DefaultProjectDirectory ?? string.Empty,
            EnableRandomizerLogs: stored.EnableRandomizerLogs,
            DefaultRandomIterations: stored.DefaultRandomIterations,
            AutoCheckUpdates: stored.AutoCheckUpdates,
            LastUsedPatchServer: stored.LastUsedPatchServer ?? string.Empty,
            ShowAdvancedOptions: stored.ShowAdvancedOptions,
            LastScpRecompileUtc: stored.LastScpRecompileUtc,
            Existing: existing,
            Problem: problem);
    }

    private static Stored Defaults() => new();

    /// <summary>
    /// The on-disk shape. Deliberately its own type: Core must not own the shell's editable model, and an unknown
    /// field in the file must not break the read.
    /// </summary>
    private sealed class Stored
    {
        public bool ValidateBeforePacking { get; set; }
        public bool AutoOpenFolderAfterOperation { get; set; }
        public string? DefaultProjectDirectory { get; set; } = string.Empty;
        public string? DolphinExecutablePath { get; set; } = string.Empty;
    public string? DiscImageConverterPath { get; set; } = string.Empty;
        public bool AutoLaunchDolphin { get; set; }
        public bool EnableRandomizerLogs { get; set; }
        public int DefaultRandomIterations { get; set; } = 10;
        public bool AutoCheckUpdates { get; set; } = true;
        public string? LastUsedPatchServer { get; set; } = string.Empty;
        public bool ShowAdvancedOptions { get; set; }
        public DateTime? LastScpRecompileUtc { get; set; }
    }
}
