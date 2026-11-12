namespace ModdingGUI.Mcp.Server;

using System;
using System.Collections.Generic;
using ModelContextProtocol.Protocol;

/// <summary>
/// How a tool answers: the payload itself on success, or an explicit MCP error result on failure.
/// </summary>
/// <remarks>
/// <para>
/// A tool method is declared <c>object</c> and returns either its payload — which the SDK serialises to
/// the tool result's text content, camel-cased like every other MCP payload — or a
/// <see cref="CallToolResult"/> with <c>isError: true</c>.
/// </para>
/// <para>
/// Throwing is not an option: the SDK deliberately replaces an exception with
/// <c>"An error occurred invoking 'x'."</c>, so a client would never see why a write was refused. An
/// explicit error result carries the message, and the exception (if any) still reaches stderr and the
/// app log with its stack.
/// </para>
/// </remarks>
public static class ToolResult
{
    /// <summary>An error result whose text is the message the caller needs to read.</summary>
    public static CallToolResult Fail(string message) => new()
    {
        IsError = true,
        Content = new List<ContentBlock> { new TextContentBlock { Text = message } },
    };

    /// <summary>An error result for a record that does not exist, with a hint about the call that lists them.</summary>
    public static CallToolResult NotFound(string what, string hint) =>
        Fail($"{what} was not found. {hint}");

    /// <summary>
    /// Run a tool body: map an exception onto an error result and record it, so the stack reaches the log
    /// instead of being swallowed by the protocol.
    /// </summary>
    public static object Call(McpProject project, string operation, Func<object?> body)
    {
        object? value;
        try
        {
            value = body();
        }
        catch (McpToolException ex)
        {
            return Fail(ex.Message);
        }
        catch (Exception ex)
        {
            project.Log.Write(ModdingGUI.Core.Logging.LogLevel.Error, $"{operation} failed: {ex}", "mcp");
            return Fail($"{operation} failed: {ex.GetType().Name}: {ex.Message}");
        }

        // A null payload would serialise to an empty content array, which reads as a broken reply.
        return value ?? Fail($"{operation}: the API returned no value.");
    }
}
