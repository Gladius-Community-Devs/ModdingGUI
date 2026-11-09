using System;
using System.IO;
using System.Linq;
using Xunit;

namespace ModdingGUI.Tests.Architecture;

/// <summary>
/// The MCP server is a thin adapter over the Core APIs that operate on project data. Application settings - the
/// Dolphin path, the projects folder, auto-launch, logging - belong to the shell and to the person at the keyboard.
/// </summary>
/// <remarks>
/// These are guard tests, not behaviour tests: they fail when someone points the MCP project at the shell assembly or
/// teaches it about settings. That is cheaper than discovering a remote client can move where projects are scanned.
/// </remarks>
public sealed class McpBoundaryTests
{
    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ModdingGUI.sln"))) return directory.FullName;
            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate ModdingGUI.sln above the test output folder.");
    }

    private static string McpFolder => Path.Combine(RepoRoot(), "ModdingGUI.Mcp");
    private static string CoreFolder => Path.Combine(RepoRoot(), "ModdingGUI.Core");

    [Fact]
    public void TheMcpProjectReferencesOnlyCore()
    {
        string csproj = Path.Combine(McpFolder, "ModdingGUI.Mcp.csproj");
        Assert.True(File.Exists(csproj), $"missing {csproj}");

        string xml = File.ReadAllText(csproj);
        var references = System.Text.RegularExpressions.Regex
            .Matches(xml, @"ProjectReference\s+Include=""([^""]+)""")
            .Select(match => match.Groups[1].Value.Replace('\\', '/'))
            .ToArray();

        Assert.NotEmpty(references);
        Assert.All(references, reference =>
            Assert.EndsWith("ModdingGUI.Core.csproj", reference, StringComparison.OrdinalIgnoreCase));

        // The shell is where AppSettings and every WinForms dependency live.
        Assert.DoesNotContain(references, reference =>
            reference.EndsWith("/ModdingGUI.csproj", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TheMcpProjectKnowsNothingAboutSettings()
    {
        if (!Directory.Exists(McpFolder)) return;

        string[] forbidden = { "AppSettings", "settings.json", "DolphinExecutablePath", "AutoLaunchDolphin",
                               "DefaultProjectDirectory", "ProjectsFolder" };

        var offenders = Directory.EnumerateFiles(McpFolder, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                           && !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .SelectMany(file => File.ReadAllLines(file)
                .Select((line, index) => (file, line, index))
                .Where(entry => forbidden.Any(word =>
                    entry.line.Contains(word, StringComparison.OrdinalIgnoreCase))))
            .Select(entry => $"{Path.GetFileName(entry.file)}:{entry.index + 1}: {entry.line.Trim()}")
            .ToArray();

        Assert.True(offenders.Length == 0,
            "MCP must not touch application settings:" + Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public void SettingsLiveInTheShellNotInCore()
    {
        // If AppSettings ever moves into Core, the MCP server could reach it by reference; this test is the tripwire.
        var inCore = Directory.EnumerateFiles(CoreFolder, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                           && !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Where(file => File.ReadAllText(file).Contains("class AppSettings", StringComparison.Ordinal))
            .ToArray();

        Assert.True(inCore.Length == 0,
            "AppSettings belongs to the shell; found in: " + string.Join(", ", inCore));
    }
}
