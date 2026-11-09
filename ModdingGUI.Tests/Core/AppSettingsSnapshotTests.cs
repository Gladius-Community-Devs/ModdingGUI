using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using ModdingGUI.Core.Settings;
using ModdingGUI.Models;
using Xunit;

namespace ModdingGUI.Tests.Core;

/// <summary>
/// The read-only settings view. Its job is to answer "where is this configured?" for a client that must not be able
/// to change anything, so the tests cover both halves: it reads what the shell writes, and Core exposes no writer.
/// </summary>
public sealed class AppSettingsSnapshotTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ModdingGUI.Settings-" + Guid.NewGuid().ToString("N"));

    public AppSettingsSnapshotTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); }
        catch (IOException) { /* best effort */ }
    }

    private string WriteSettings(AppSettings settings)
    {
        string path = Path.Combine(_dir, AppSettingsReader.FileName);
        File.WriteAllText(path, JsonSerializer.Serialize(settings));
        return path;
    }

    [Fact]
    public void ReadsWhatTheShellWrites()
    {
        var settings = new AppSettings
        {
            DolphinExecutablePath = @"C:\dolphin\Dolphin.exe",
            AutoLaunchDolphin = true,
            ValidateBeforePacking = true,
            AutoOpenFolderAfterOperation = true,
            EnableRandomizerLogs = true,
            DefaultRandomIterations = 42,
            AutoCheckUpdates = false,
            LastUsedPatchServer = "https://example.invalid/patches",
            ShowAdvancedOptions = true,
            LastScpRecompileUtc = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc),
        };

        string path = WriteSettings(settings);
        var snapshot = AppSettingsReader.Read(path, _dir);

        Assert.True(snapshot.Existing);
        Assert.Null(snapshot.Problem);
        Assert.Equal(@"C:\dolphin\Dolphin.exe", snapshot.DolphinExecutablePath);
        Assert.True(snapshot.AutoLaunchDolphin);
        Assert.True(snapshot.ValidateBeforePacking);
        Assert.True(snapshot.AutoOpenFolderAfterOperation);
        Assert.True(snapshot.EnableRandomizerLogs);
        Assert.Equal(42, snapshot.DefaultRandomIterations);
        Assert.False(snapshot.AutoCheckUpdates);
        Assert.Equal("https://example.invalid/patches", snapshot.LastUsedPatchServer);
        Assert.True(snapshot.ShowAdvancedOptions);
        Assert.Equal(settings.LastScpRecompileUtc, snapshot.LastScpRecompileUtc);
    }

    [Fact]
    public void TheProjectsFolderIsResolvedTheWayTheShellResolvesIt()
    {
        string projects = Path.Combine(_dir, "projects");
        Directory.CreateDirectory(projects);

        string path = WriteSettings(new AppSettings { DefaultProjectDirectory = projects });
        var snapshot = AppSettingsReader.Read(path, _dir);

        Assert.Equal(Path.GetFullPath(projects).TrimEnd(Path.DirectorySeparatorChar), snapshot.ProjectsFolder);
    }

    [Fact]
    public void AMissingFileStillAnswersWithDefaults()
    {
        var snapshot = AppSettingsReader.Read(Path.Combine(_dir, "nope.json"), _dir);

        Assert.False(snapshot.Existing);
        Assert.NotNull(snapshot.Problem);
        Assert.Equal(10, snapshot.DefaultRandomIterations);
        Assert.NotEmpty(snapshot.SettingsFilePath);
    }

    [Fact]
    public void ABrokenFileIsReportedRatherThanThrown()
    {
        string path = Path.Combine(_dir, AppSettingsReader.FileName);
        File.WriteAllText(path, "{ this is not json ");

        var snapshot = AppSettingsReader.Read(path, _dir);

        Assert.False(snapshot.Existing);
        Assert.NotNull(snapshot.Problem);
        Assert.Contains("not valid JSON", snapshot.Problem!);
    }

    [Fact]
    public void UnknownFieldsDoNotBreakTheRead()
    {
        string path = Path.Combine(_dir, AppSettingsReader.FileName);
        File.WriteAllText(path, """{ "DefaultRandomIterations": 7, "SomethingFromTheFuture": true }""");

        var snapshot = AppSettingsReader.Read(path, _dir);

        Assert.True(snapshot.Existing);
        Assert.Equal(7, snapshot.DefaultRandomIterations);
    }

    [Fact]
    public void EveryStoredSettingSaysWhereItIsEdited()
    {
        // If a setting is added to the shell and not to the map, an assistant asked "where is X?" would have no
        // answer, so the map is checked against the real model.
        var properties = typeof(AppSettings)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name)
            .ToArray();

        var documented = AppSettingsSnapshot.WhereEachSettingLives.Keys.ToArray();

        var missing = properties.Where(name => !documented.Contains(name, StringComparer.OrdinalIgnoreCase)).ToArray();

        Assert.True(missing.Length == 0,
            "These settings have no documented location: " + string.Join(", ", missing));
    }

    [Fact]
    public void CoreOffersNoWayToWriteSettings()
    {
        string[] writerVerbs = { "Write", "Save", "Set", "Update", "Patch" };

        var writers = typeof(AppSettingsReader)
            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
            .Where(method => writerVerbs.Any(verb => method.Name.Contains(verb, StringComparison.OrdinalIgnoreCase)))
            .Select(method => method.Name)
            .ToArray();

        Assert.True(writers.Length == 0,
            "AppSettingsReader must stay read-only: " + string.Join(", ", writers));

        // The snapshot is immutable: a caller can read a field, not assign one. (Positional records expose init-only
        // setters, which are set at construction only and must not be mistaken for mutators.)
        var mutable = typeof(AppSettingsSnapshot)
            .GetProperties()
            .Where(property => property.CanWrite && !IsInitOnly(property))
            .Select(property => property.Name)
            .ToArray();

        Assert.True(mutable.Length == 0, "Settings snapshot must be immutable: " + string.Join(", ", mutable));
    }

    private static bool IsInitOnly(PropertyInfo property) =>
        property.SetMethod?.ReturnParameter.GetRequiredCustomModifiers()
            .Contains(typeof(System.Runtime.CompilerServices.IsExternalInit)) == true;
}
