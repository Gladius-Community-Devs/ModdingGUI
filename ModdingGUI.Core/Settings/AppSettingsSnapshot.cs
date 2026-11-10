namespace ModdingGUI.Core.Settings;

using System;
using System.Collections.Generic;

/// <summary>
/// A read-only view of the application's <c>settings.json</c>: what it says, where the file is, and where each
/// setting is surfaced in the app.
/// </summary>
/// <remarks>
/// <para>
/// This exists so a client - the MCP server first - can answer "where is the Dolphin path configured?" without
/// being able to change it. The snapshot is an immutable record and Core exposes no writer: the shell owns editing.
/// </para>
/// <para>
/// <see cref="WhereEachSettingLives"/> is the part that makes the view genuinely useful: the answer to a question
/// about a setting is usually a place in the UI, not the JSON.
/// </para>
/// </remarks>
/// <param name="SettingsFilePath">The settings file the snapshot came from, whether or not it existed.</param>
/// <param name="ProjectsFolder">Where projects are scanned for, resolved the way the shell resolves it.</param>
/// <param name="DolphinExecutablePath">Configured Dolphin executable, empty when unset.</param>
/// <param name="AutoLaunchDolphin">Whether a pack launches Dolphin, as stored. The shell normalises it when no path is set.</param>
/// <param name="ValidateBeforePacking">Whether validation runs before packing.</param>
/// <param name="AutoOpenFolderAfterOperation">Whether Explorer opens after unpack/pack.</param>
/// <param name="DefaultProjectDirectory">The configured projects folder, empty when the app folder is used.</param>
/// <param name="EnableRandomizerLogs">Whether extra randomizer logging is on.</param>
/// <param name="DefaultRandomIterations">Default iteration count in the randomizer panel.</param>
/// <param name="AutoCheckUpdates">Whether the patcher checks for updates.</param>
/// <param name="LastUsedPatchServer">Last patch server used.</param>
/// <param name="ShowAdvancedOptions">Whether advanced UI options are shown.</param>
/// <param name="LastScpRecompileUtc">When scripts were last recompiled, if ever.</param>
/// <param name="Existing">True when the file existed and parsed.</param>
/// <param name="Problem">Why the snapshot fell back to defaults, when it did.</param>
public sealed record AppSettingsSnapshot(
    string SettingsFilePath,
    string ProjectsFolder,
    string DolphinExecutablePath,
    bool AutoLaunchDolphin,
    bool ValidateBeforePacking,
    bool AutoOpenFolderAfterOperation,
    string DefaultProjectDirectory,
    bool EnableRandomizerLogs,
    int DefaultRandomIterations,
    bool AutoCheckUpdates,
    string LastUsedPatchServer,
    bool ShowAdvancedOptions,
    DateTime? LastScpRecompileUtc,
    bool Existing = true,
    string? Problem = null)
{
    /// <summary>
    /// Where each stored setting is edited, so an assistant can point at the UI instead of guessing.
    /// </summary>
    public static IReadOnlyDictionary<string, string> WhereEachSettingLives { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ValidateBeforePacking"] = "File > Settings > General: 'Validate before packing' checkbox.",
            ["ValidationPassVersion"] = "Not user-facing: settings-schema version, written by the app.",
            ["Theme"] = "Options menu: 'Dark theme' (unticked = light). The app opens dark.",
            ["AutoOpenFolderAfterOperation"] = "File > Settings > General: 'Open folder after operation' checkbox.",
            ["DefaultProjectDirectory"] = "File > Settings > General: 'Projects Folder (blank = app folder)' + Browse.",
            ["DolphinExecutablePath"] = "File > Settings > General: 'Dolphin Executable Path' + Browse.",
            ["AutoLaunchDolphin"] = "Packing tab: 'Auto launch into Dolphin' checkbox (visible once a Dolphin path is set).",
            ["EnableRandomizerLogs"] = "File > Settings > Randomizer: 'Enable randomizer logs' checkbox.",
            ["DefaultRandomIterations"] = "File > Settings > Randomizer: 'Default Randomizer Iterations'.",
            ["AutoCheckUpdates"] = "File > Settings > Patching: 'Check for updates' checkbox.",
            ["LastUsedPatchServer"] = "File > Settings > Patching: 'Last Used Patch Server'.",
            ["ShowAdvancedOptions"] = "File > Settings > UI: 'Show advanced options' checkbox.",
            ["LastScpRecompileUtc"] = "Not user-editable; written when scripts are recompiled (Packing tab).",
        };
}
