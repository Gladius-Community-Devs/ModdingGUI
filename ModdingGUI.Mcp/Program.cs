namespace ModdingGUI.Mcp;

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ModdingGUI.Mcp.Server;
using CoreLogLevel = ModdingGUI.Core.Logging.LogLevel;

/// <summary>
/// MCP stdio server entrypoint. Exposes the <c>ModdingGUI.Core</c> API layer as Model Context Protocol
/// tools so AI clients (Claude Desktop, OpenCode) can perform the same edits the WinForms GUI performs,
/// through the same code paths: project lock, <c>.mod-history</c> backup, byte-faithful writers.
/// </summary>
/// <remarks>
/// <para>Invocation contract:</para>
/// <code>
///   ModdingGUI.Mcp [--project &lt;path&gt;] [--tools &lt;dir&gt;] [--python &lt;command&gt;] [--log &lt;file&gt;]
/// </code>
/// <para>
/// The project defaults to the current directory and is validated at startup: a path that is not a
/// Gladius project is a clear stderr message and exit code 2, never a half-working server. The project is
/// fixed for the lifetime of the process — tools do not take a project path — so one server instance
/// edits exactly one project, which is what makes "strict mode" and the history guard unambiguous.
/// </para>
/// <para>
/// The transport owns stdout. Everything a human would read goes to stderr through the host's console
/// logger or through <see cref="ModdingGUI.Core.Logging.IAppLog"/>; anything written to stdout would be
/// parsed as JSON-RPC and break the session.
/// </para>
/// </remarks>
public static class Program
{
    /// <summary>Usage text, printed to stderr for <c>--help</c> and for bad arguments.</summary>
    public const string Usage = """
        ModdingGUI.Mcp — Model Context Protocol (stdio) server for Gladius mod projects

          ModdingGUI.Mcp [--project <path>] [--tools <dir>] [--python <command>] [--log <file>] [--help]

        Options
          --project <path>   Project root or its _BEC folder (default: the current directory).
          --tools <dir>      Folder holding the toolbelt (default: <app>/tools).
          --python <cmd>     Interpreter for the script toolchain (default: python; "py -3" works).
          --log <file>       Append the app log here (default: %LOCALAPPDATA%\GladiusModding\last-run.log).
          --help             Show this text.

        The server speaks JSON-RPC over stdin/stdout; it prints nothing else there. A client such as
        Claude Desktop configures it as a stdio server, e.g.

          { "command": "ModdingGUI.Mcp", "args": ["--project", "C:\\mods\\MyMod"] }
        """;

    /// <summary>Run the server. Returns 0 on a clean session end, 2 for a bad invocation.</summary>
    public static async Task<int> Main(string[] args)
    {
        if (Array.IndexOf(args, "--help") >= 0 || Array.IndexOf(args, "-h") >= 0)
        {
            Console.Error.WriteLine(Usage);
            return 0;
        }

        McpProject project;
        try
        {
            project = McpProject.Resolve(args);
        }
        catch (McpStartupException ex)
        {
            Console.Error.WriteLine($"ModdingGUI.Mcp: {ex.Message}");
            Console.Error.WriteLine();
            Console.Error.WriteLine(Usage);
            return 2;
        }

        // Deliberately no command-line provider: the arguments are this process's own (--project, …), not
        // host configuration, and a path containing '=' must not be reinterpreted as a config key.
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();

        // Stdout belongs to the transport. Redirect every logging provider to stderr.
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);

        builder.Services.AddSingleton(project);
        builder.Services
            .AddMcpServer(options =>
            {
                options.ServerInfo = new Implementation
                {
                    Name = "gladius-modding-tools",
                    Title = "Gladius Modding Tools",
                    Version = "1.0.0",
                    Description = "Edit a Gladius (GameCube/PS2) mod project through the same Core APIs the GUI uses.",
                };
                options.ServerInstructions = McpProject.Instructions;
            })
            .WithStdioServerTransport()
            .WithToolsFromAssembly();

        using IHost host = builder.Build();

        project.Log.Write(CoreLogLevel.Info,
            $"MCP server starting on stdio: project='{project.ProjectPath}' bec='{project.BecPath}'", "mcp");

        try
        {
            await host.RunAsync();
        }
        finally
        {
            // Keep this line last: it is the evidence that the session ended because the client closed
            // stdin, rather than because a tool crashed the process.
            project.Log.Write(CoreLogLevel.Info, "MCP server stopped (stdin closed).", "mcp");
        }

        return 0;
    }
}
