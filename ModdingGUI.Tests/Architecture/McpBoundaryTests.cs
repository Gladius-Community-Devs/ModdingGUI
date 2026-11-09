using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace ModdingGUI.Tests.Architecture;

/// <summary>
/// The MCP server may <b>read</b> application settings through Core's immutable snapshot, so an assistant can answer
/// "where is the Dolphin path configured?". It must never be able to change them: settings are machine-local, sit
/// outside the <c>.mod-history</c> undo story, decide where the tool looks and what it launches, and include the
/// switch that enables the MCP transport itself.
/// </summary>
/// <remarks>
/// Guard tests, not behaviour tests: they fail when someone points the MCP project at the shell, teaches it to write
/// settings, or moves the editable model into Core.
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

    private static string[] McpSources() =>
        Directory.Exists(McpFolder)
            ? Directory.EnumerateFiles(McpFolder, "*.cs", SearchOption.AllDirectories)
                .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                               && !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                .ToArray()
            : Array.Empty<string>();

    [Fact]
    public void TheMcpProjectReferencesOnlyCore()
    {
        string csproj = Path.Combine(McpFolder, "ModdingGUI.Mcp.csproj");
        Assert.True(File.Exists(csproj), $"missing {csproj}");

        string xml = File.ReadAllText(csproj);
        var references = Regex.Matches(xml, @"ProjectReference\s+Include=""([^""]+)""")
            .Select(match => match.Groups[1].Value.Replace('\\', '/'))
            .ToArray();

        Assert.NotEmpty(references);
        Assert.All(references, reference =>
            Assert.EndsWith("ModdingGUI.Core.csproj", reference, StringComparison.OrdinalIgnoreCase));

        // The shell is where the editable AppSettings model lives.
        Assert.DoesNotContain(references,
            reference => reference.EndsWith("/ModdingGUI.csproj", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TheMcpProjectNeverWritesSettings()
    {
        // Reading goes through Core's AppSettingsReader/AppSettingsSnapshot; these would mean a second, writable path.
        string[] forbidden =
        {
            "AppSettings ",              // the shell's editable model
            "new AppSettings",
            "SaveAppSettings",
            "File.WriteAllText(settings",
            "settings_set",
            "settings_update",
            "settings_write",
        };

        var offenders = McpSources()
            .SelectMany(file => File.ReadAllLines(file)
                .Select((line, index) => (file, line, index))
                .Where(entry => forbidden.Any(word => entry.line.Contains(word, StringComparison.OrdinalIgnoreCase))))
            .Select(entry => $"{Path.GetFileName(entry.file)}:{entry.index + 1}: {entry.line.Trim()}")
            .ToArray();

        Assert.True(offenders.Length == 0,
            "MCP must only read settings, through Core:" + Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public void NoMcpToolCanSetASetting()
    {
        // Holds vacuously until the tool registry exists (Phase 8), then catches a settings mutator by name.
        var toolNames = McpSources()
            .SelectMany(File.ReadAllLines)
            .Select(line => Regex.Match(line, @"\[McpServerTool[^\]]*\]"))
            .Select(match => match.Success ? match.Value : null)
            .Where(value => value is not null)
            .ToArray();

        var mutators = toolNames
            .Where(name => name!.Contains("setting", StringComparison.OrdinalIgnoreCase)
                           && (name.Contains("set", StringComparison.OrdinalIgnoreCase)
                               || name.Contains("update", StringComparison.OrdinalIgnoreCase)
                               || name.Contains("write", StringComparison.OrdinalIgnoreCase)
                               || name.Contains("save", StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        Assert.True(mutators.Length == 0,
            "Settings are read-only over MCP: " + string.Join(", ", mutators));
    }

    [Fact]
    public void TheEditableModelStaysInTheShell()
    {
        // If AppSettings moved into Core, the MCP server could reach a writable model by reference. Core exposes a
        // read-only snapshot instead, and that separation is what these guards protect.
        var inCore = Directory.EnumerateFiles(CoreFolder, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                           && !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Where(file => Regex.IsMatch(File.ReadAllText(file), @"\b(?:class|record)\s+AppSettings\b"))
            .ToArray();

        Assert.True(inCore.Length == 0, "AppSettings belongs to the shell; found in: " + string.Join(", ", inCore));

        // The read-only view must exist in Core, so a client has a supported way to ask.
        Assert.True(File.Exists(Path.Combine(CoreFolder, "Settings", "AppSettingsReader.cs")));
        Assert.True(File.Exists(Path.Combine(CoreFolder, "Settings", "AppSettingsSnapshot.cs")));
    }
}
