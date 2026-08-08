namespace ModdingGUI.Core;

/// <summary>
/// Marker type for the <c>ModdingGUI.Core</c> assembly.
/// Acts as a stable reference point for <c>typeof(CoreAssemblyMarker).Assembly</c>
/// in hosting code (WinForms shell, MCP server, tests) and guarantees the
/// assembly contains at least one type while real Core content is being built out.
/// </summary>
/// <remarks>
/// This type will remain even after Phase 1+ lands real content. Do not remove it
/// without updating any <c>Assembly.GetAssembly(typeof(CoreAssemblyMarker))</c>
/// call sites.
/// </remarks>
public static class CoreAssemblyMarker
{
    /// <summary>
    /// Human-readable identifier for the Core assembly. Used by diagnostic / about screens.
    /// </summary>
    public const string Name = "ModdingGUI.Core";
}
