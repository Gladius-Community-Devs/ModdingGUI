using ModdingGUI.Models;
using Xunit;

namespace ModdingGUI.Tests;

/// <summary>
/// Tests for <see cref="AppSettings"/> — default values and the deep-copy constructor.
/// </summary>
public class AppSettingsTests
{
    [Fact]
    public void DefaultConstructor_ValidateBeforePacking_DefaultsFalse()
    {
        Assert.False(new AppSettings().ValidateBeforePacking);
    }

    [Fact]
    public void DefaultConstructor_AutoOpenFolderAfterOperation_DefaultsFalse()
    {
        Assert.False(new AppSettings().AutoOpenFolderAfterOperation);
    }

    [Fact]
    public void DefaultConstructor_AutoLaunchDolphin_DefaultsTrue()
    {
        Assert.True(new AppSettings().AutoLaunchDolphin);
    }

    [Fact]
    public void DefaultConstructor_AutoCheckUpdates_DefaultsTrue()
    {
        Assert.True(new AppSettings().AutoCheckUpdates);
    }

    [Fact]
    public void DefaultConstructor_DefaultRandomIterations_Is10()
    {
        Assert.Equal(10, new AppSettings().DefaultRandomIterations);
    }

    [Fact]
    public void DefaultConstructor_StringProperties_DefaultToEmpty()
    {
        var s = new AppSettings();
        Assert.Equal(string.Empty, s.DefaultProjectDirectory);
        Assert.Equal(string.Empty, s.DolphinExecutablePath);
        Assert.Equal(string.Empty, s.LastUsedPatchServer);
    }

    [Fact]
    public void DeepCopyConstructor_CopiesAllProperties()
    {
        var original = new AppSettings
        {
            ValidateBeforePacking       = true,
            AutoOpenFolderAfterOperation = true,
            DefaultProjectDirectory     = @"C:\mods",
            DolphinExecutablePath       = @"C:\dolphin\dolphin.exe",
            AutoLaunchDolphin           = false,
            EnableRandomizerLogs        = true,
            DefaultRandomIterations     = 25,
            AutoCheckUpdates            = false,
            LastUsedPatchServer         = "https://example.com",
            ShowAdvancedOptions         = true
        };

        var copy = new AppSettings(original);

        Assert.Equal(original.ValidateBeforePacking,        copy.ValidateBeforePacking);
        Assert.Equal(original.AutoOpenFolderAfterOperation, copy.AutoOpenFolderAfterOperation);
        Assert.Equal(original.DefaultProjectDirectory,      copy.DefaultProjectDirectory);
        Assert.Equal(original.DolphinExecutablePath,        copy.DolphinExecutablePath);
        Assert.Equal(original.AutoLaunchDolphin,            copy.AutoLaunchDolphin);
        Assert.Equal(original.EnableRandomizerLogs,         copy.EnableRandomizerLogs);
        Assert.Equal(original.DefaultRandomIterations,      copy.DefaultRandomIterations);
        Assert.Equal(original.AutoCheckUpdates,             copy.AutoCheckUpdates);
        Assert.Equal(original.LastUsedPatchServer,          copy.LastUsedPatchServer);
        Assert.Equal(original.ShowAdvancedOptions,          copy.ShowAdvancedOptions);
    }

    [Fact]
    public void DeepCopyConstructor_IsIndependent_MutatingCopyDoesNotAffectOriginal()
    {
        var original = new AppSettings { DefaultProjectDirectory = @"C:\original" };
        var copy = new AppSettings(original);

        copy.DefaultProjectDirectory = @"C:\changed";

        Assert.Equal(@"C:\original", original.DefaultProjectDirectory);
    }
}
