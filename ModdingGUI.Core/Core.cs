namespace ModdingGUI.Core;

/// <summary>
/// Entry point for Core assembly initialization. Idempotent; every host
/// (WinForms shell, MCP server, test fixture) must call <see cref="Initialize"/>
/// before using any <see cref="FileFormatRegistry"/>-backed API surface.
/// </summary>
/// <remarks>
/// Initialization is explicit (no module initializer, no static constructor
/// magic) so that tests can fully reset the registry when they need to exercise
/// registration behaviour (see
/// <c>FileFormatRegistry.ResetForTests</c>).
/// </remarks>
public static class Core
{
    private static readonly object s_gate = new();

    /// <summary>
    /// Register the built-in schemas (currently: <c>items</c>). Safe to call
    /// repeatedly from any thread.
    /// </summary>
    public static void Initialize()
    {
        lock (s_gate)
        {
            if (FileFormatRegistry.IsInitialized) return;
            FileFormatRegistry.RegisterBuiltins();
        }
    }
}
