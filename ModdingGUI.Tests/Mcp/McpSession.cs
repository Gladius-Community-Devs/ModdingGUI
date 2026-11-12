namespace ModdingGUI.Tests.Mcp;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Xunit;

/// <summary>
/// A running <c>ModdingGUI.Mcp</c> process plus a staged copy of the project it edits.
/// </summary>
/// <remarks>
/// <para>
/// These tests drive the real executable over stdio, through the same <see cref="McpClient"/> and
/// <see cref="StdioClientTransport"/> a client such as Claude Desktop uses. Nothing is mocked: the tool
/// schema, the JSON-RPC framing and the Core API calls are all the production ones, and every edit lands in
/// the staged copy's <c>.mod-history</c>.
/// </para>
/// <para>
/// The project is staged from the read-only <c>clean-bec-unpacked/</c> corpus into a temp folder, because a
/// test must never write to the reference tree. The file list is the minimum the families under test need, so
/// a run stays fast.
/// </para>
/// </remarks>
internal sealed class McpSession : IAsyncDisposable
{
    private static readonly string[] StagedFiles =
    {
        "data/config/items.tok",
        "data/config/skills.tok",
        "data/config/classdefs.tok",
        "data/config/lookuptext_eng.txt",
        "data/units/gladiators.txt",
        "data/units/statsets.txt",
        "data/units/skillsets.txt",
        "data/units/itemsets.txt",
        "data/units/tints.txt",
        "data/units/schools.txt",
        "data/school/ursulanordagh.tok",
        "data/encounters/expanse/expansedunes/expansedunesencounter01.enc",
        "data/levels/expanse/expansedunes/expansedunesnomove.grd",
        "data/towns/leagues/altahrunruins_leagues.tok",
    };

    private readonly McpClient _client;
    private readonly StdioClientTransport _transport;

    private McpSession(string projectPath, McpClient client, StdioClientTransport transport, IList<McpClientTool> tools)
    {
        ProjectPath = projectPath;
        _client = client;
        _transport = transport;
        Tools = tools;
    }

    /// <summary>Folder the staged project lives in (contains <c>data/</c> and gets a <c>.mod-history</c>).</summary>
    public string ProjectPath { get; }

    /// <summary>Every tool the server advertises, as <c>tools/list</c> returned it.</summary>
    public IList<McpClientTool> Tools { get; }

    /// <summary>Start a server against a freshly staged project and complete the MCP handshake.</summary>
    public static async Task<McpSession> StartAsync()
    {
        string corpus = CleanBecCorpus.Root
            ?? throw new InvalidOperationException("clean-bec-unpacked/ was not found; this test needs the reference corpus.");

        string project = Path.Combine(Path.GetTempPath(), "gladius-mcp-tests-" + Guid.NewGuid().ToString("N")[..12]);
        Directory.CreateDirectory(project);

        foreach (string relative in StagedFiles)
        {
            string source = Path.Combine(corpus, relative.Replace('/', Path.DirectorySeparatorChar));
            string destination = Path.Combine(project, relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(source, destination);
        }

        var options = new StdioClientTransportOptions
        {
            Command = ExePath(),
            Arguments = new List<string> { "--project", project },
            WorkingDirectory = Path.GetDirectoryName(ExePath())!,
            Name = "gladius-mcp-tests",
            InheritEnvironmentVariables = true,
        };

        var transport = new StdioClientTransport(options);
        McpClient client = await McpClient.CreateAsync(transport);
        IList<McpClientTool> tools = await client.ListToolsAsync();
        return new McpSession(project, client, transport, tools);
    }

    /// <summary>The one tool with this name, failing the test when the server does not advertise it.</summary>
    public McpClientTool Tool(string name)
    {
        McpClientTool? tool = Tools.FirstOrDefault(candidate => candidate.Name == name);
        Assert.True(tool is not null, $"the server does not advertise a tool named '{name}'");
        return tool!;
    }

    /// <summary>Call a tool and fail the test when the call reports an error.</summary>
    public async Task<CallToolResult> CallAsync(string name, IReadOnlyDictionary<string, object?>? arguments = null)
    {
        CallToolResult result = await Tool(name).CallAsync(arguments);
        Assert.True(result.IsError != true,
            $"{name} failed: {Text(result)}");
        return result;
    }

    /// <summary>The text payload of a tool result.</summary>
    public static string Text(CallToolResult result) =>
        string.Join(Environment.NewLine, result.Content.OfType<TextContentBlock>().Select(block => block.Text));

    /// <summary>The text payload of a tool result, parsed as JSON.</summary>
    public static JsonNode? Json(CallToolResult result) => JsonNode.Parse(Text(result));

    /// <summary>True when the call is an error result.</summary>
    public static bool IsError(CallToolResult result) => result.IsError == true;

    /// <summary>The absolute path of the exe under test.</summary>
    /// <remarks>
    /// Resolved from the solution folder rather than hard-coded, so Debug and Release runs each exercise the
    /// build under test. <c>GLADIUS_MCP_EXE</c> overrides it.
    /// </remarks>
    public static string ExePath()
    {
        string? overridden = Environment.GetEnvironmentVariable("GLADIUS_MCP_EXE");
        if (!string.IsNullOrWhiteSpace(overridden) && File.Exists(overridden)) return overridden;

        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ModdingGUI.sln")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException("Could not locate ModdingGUI.sln above the test output folder.");
        }

        // The test output lives under bin/<Configuration>, so try the same configuration first.
        string[] configurations = AppContext.BaseDirectory.Contains(
            $"{Path.DirectorySeparatorChar}Release{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
            ? new[] { "Release", "Debug" }
            : new[] { "Debug", "Release" };

        foreach (string configuration in configurations)
        {
            string candidate = Path.Combine(directory.FullName, "ModdingGUI.Mcp", "bin", configuration, "net8.0", "ModdingGUI.Mcp.exe");
            if (File.Exists(candidate)) return candidate;
        }

        throw new InvalidOperationException(
            "ModdingGUI.Mcp.exe was not found. Build the solution first (dotnet build ModdingGUI.sln) or set " +
            "GLADIUS_MCP_EXE to its path.");
    }

    /// <summary>Read a file in the staged project.</summary>
    public string ReadProjectFile(string relativePath) =>
        File.ReadAllText(Path.Combine(ProjectPath, relativePath.Replace('/', Path.DirectorySeparatorChar)));

    /// <summary>A file's bytes in the staged project.</summary>
    public byte[] ReadProjectBytes(string relativePath) =>
        File.ReadAllBytes(Path.Combine(ProjectPath, relativePath.Replace('/', Path.DirectorySeparatorChar)));

    /// <summary>Every backup of one project-relative file, newest first.</summary>
    public string[] BackupsOf(string relativePath)
    {
        string folder = Path.Combine(ProjectPath, ".mod-history", relativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!Directory.Exists(folder)) return Array.Empty<string>();

        return Directory.EnumerateFiles(folder, "*.bak")
            .OrderByDescending(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>Close the client, then remove the staged project.</summary>
    public async ValueTask DisposeAsync()
    {
        try
        {
            await _client.DisposeAsync();
        }
        catch (Exception)
        {
            // The server may already have exited; the assertions have run by now.
        }

        // Disposing the client closes stdin, which is what ends the child process; the transport object
        // itself is not disposable in this SDK version.

        try
        {
            Directory.Delete(ProjectPath, recursive: true);
        }
        catch (IOException)
        {
            // A leftover temp folder is not worth failing a test run over.
        }
    }
}
