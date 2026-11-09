using System;
using System.IO;
using ModdingGUI.Helpers;
using Xunit;

namespace ModdingGUI.Tests;

/// <summary>
/// The folder opener must report a missing folder rather than throw, and must not depend on spawning explorer.exe.
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
}
