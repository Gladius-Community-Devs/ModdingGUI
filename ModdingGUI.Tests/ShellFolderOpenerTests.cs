using System;
using System.IO;
using ModdingGUI.Helpers;
using Xunit;

namespace ModdingGUI.Tests;

/// <summary>
/// The folder opener must report a missing folder rather than throw, must not spawn explorer.exe, and must hand the
/// shell a backslash path: <c>SHParseDisplayName</c> returns <c>E_INVALIDARG</c> for forward slashes, which is what
/// made "Open ISO location" fail while the same folder opened fine by hand.
/// </summary>
/// <remarks>
/// Only the failure path is asserted: opening a real folder would pop an Explorer window during the test run.
/// </remarks>
public sealed class ShellFolderOpenerTests
{
    [Fact]
    public void AMissingFolderIsRefusedWithAReason()
    {
        string missing = Path.Combine(Path.GetTempPath(), "ModdingGUI.NoSuchFolder-" + Guid.NewGuid().ToString("N"));

        Assert.False(ShellFolderOpener.TryOpen(missing, out string error));
        Assert.False(string.IsNullOrWhiteSpace(error));
        Assert.Contains("shell", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AnEmptyPathIsRejectedNotThrown()
    {
        Assert.Throws<ArgumentException>(() => ShellFolderOpener.TryOpen(string.Empty, out _));
    }

    [Fact]
    public void ForwardSlashesBecomeBackslashes()
    {
        string path = Path.Combine(Path.GetTempPath(), "ModdingGUI", "proj");

        string normalised = ShellFolderOpener.NormaliseForShell(path.Replace('\\', '/'));

        Assert.DoesNotContain('/', normalised);
        Assert.Equal(Path.GetFullPath(path), normalised);
    }

    [Fact]
    public void ATrailingSeparatorIsTrimmedButADriveRootSurvives()
    {
        string path = Path.Combine(Path.GetTempPath(), "ModdingGUI");

        Assert.Equal(Path.GetFullPath(path),
            ShellFolderOpener.NormaliseForShell(path + Path.DirectorySeparatorChar));

        // A drive root must not become "E:" - that reads as a relative drive path.
        string root = Path.GetPathRoot(Path.GetTempPath())!;
        string normalisedRoot = ShellFolderOpener.NormaliseForShell(root);
        Assert.EndsWith(new string(Path.DirectorySeparatorChar, 1), normalisedRoot);
        Assert.Equal(root.Length > 2 ? root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar : root,
            normalisedRoot);
    }

    [Fact]
    public void MixingSeparatorsIsNormalised()
    {
        string path = Path.Combine(Path.GetTempPath(), "a", "b", "c");

        string normalised = ShellFolderOpener.NormaliseForShell(path.Replace("\\a", "/a").Replace("\\b", "/b"));

        Assert.Equal(Path.GetFullPath(path), normalised);
    }
}
