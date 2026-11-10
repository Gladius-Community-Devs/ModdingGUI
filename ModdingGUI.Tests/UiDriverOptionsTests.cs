using System;
using ModdingGUI;
using Xunit;

namespace ModdingGUI.Tests;

/// <summary>
/// The UI driver's command-line flags. The rest of the driver needs a live window, but the flag parsing is pure and
/// is what decides whether the app listens on a socket at all — a regression here would either leave the driver
/// unreachable or, worse, open a listener the user never asked for.
/// </summary>
public sealed class UiDriverOptionsTests
{
    [Fact]
    public void NoFlags_MeansNoDriver()
    {
        Assert.Null(UiDriver.Options.Parse(Array.Empty<string>()));
        Assert.Null(UiDriver.Options.Parse(new[] { "--pack", "C:\\mods\\proj" }));
    }

    [Fact]
    public void ServerWithoutAPort_UsesTheDefault()
    {
        var options = UiDriver.Options.Parse(new[] { "--ui-server" });

        Assert.NotNull(options);
        Assert.True(options!.Server);
        Assert.Equal(UiDriver.DefaultPort, options.Port);
        Assert.Null(options.ScriptPath);
        Assert.False(options.ExitWhenDone);
    }

    [Fact]
    public void ServerWithAPort_UsesIt()
    {
        var options = UiDriver.Options.Parse(new[] { "--ui-server", "5000" });

        Assert.NotNull(options);
        Assert.Equal(5000, options!.Port);
    }

    [Fact]
    public void ScriptMode_ReadsThePathAndTheExitFlag()
    {
        var options = UiDriver.Options.Parse(new[] { "--ui-script", "C:\\scripts\\smoke.txt", "--ui-exit" });

        Assert.NotNull(options);
        Assert.False(options!.Server);
        Assert.Equal("C:\\scripts\\smoke.txt", options.ScriptPath);
        Assert.True(options.ExitWhenDone);
    }

    [Fact]
    public void ServerAndScript_CanBeCombined()
    {
        var options = UiDriver.Options.Parse(new[] { "--ui-server", "4572", "--ui-script", "a.txt" });

        Assert.NotNull(options);
        Assert.True(options!.Server);
        Assert.Equal(4572, options.Port);
        Assert.Equal("a.txt", options.ScriptPath);
    }
}
