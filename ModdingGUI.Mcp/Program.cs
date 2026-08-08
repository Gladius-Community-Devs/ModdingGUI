using ModdingGUI.Core;

namespace ModdingGUI.Mcp;

/// <summary>
/// MCP stdio server entrypoint. Exposes the <c>ModdingGUI.Core</c> API layer as
/// Model Context Protocol tools so AI clients (Claude Desktop, OpenCode) can
/// perform the same edits the WinForms GUI performs, through the same code paths.
/// </summary>
/// <remarks>
/// <para>Invocation contract (subject to change as Phase 8 lands):</para>
/// <code>
///   ModdingGUI.Mcp &lt;projectPath&gt;
/// </code>
/// <para>
/// The WinForms shell launches this process with the currently-selected project
/// path when MCP mode is enabled in settings. Switching projects in the GUI kills
/// and respawns this process.
/// </para>
/// <para>
/// Transport: JSON-RPC 2.0 over stdio (MCP spec). Compatible with Claude Desktop
/// and OpenCode. Implementation lands in Phase 8; this program currently just
/// validates arguments so the project builds and links against Core.
/// </para>
/// </remarks>
public static class Program
{
    public static int Main(string[] args)
    {
        // Phase 0 scaffolding: the real StdioServer / ToolRegistry / McpHostService
        // wiring arrives in Phase 8. Keeping the program runnable but inert ensures
        // the solution builds and the project structure is exercised end-to-end.
        if (args.Length < 1)
        {
            Console.Error.WriteLine($"{CoreAssemblyMarker.Name} MCP host: missing <projectPath> argument.");
            Console.Error.WriteLine("Usage: ModdingGUI.Mcp <projectPath>");
            return 2;
        }

        var projectPath = args[0];
        Console.Error.WriteLine(
            $"{CoreAssemblyMarker.Name} MCP host: scaffolding-only build. " +
            $"Project='{projectPath}'. Stdio server not yet implemented (Phase 8).");
        return 0;
    }
}
