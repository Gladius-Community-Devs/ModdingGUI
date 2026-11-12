namespace ModdingGUI.Core.Tools;

/// <summary>Which program the build chain runs.</summary>
public enum ToolchainMode
{
    /// <summary>The Python scripts in <c>tools/</c>, run with the configured interpreter.</summary>
    PythonScripts,

    /// <summary>The bundled <c>tools_v009.exe</c>, which carries its own interpreter.</summary>
    BundledExecutable,
}

/// <summary>
/// The toolbelt a build runs against: either the Python scripts (the v007 toolbelt) or the bundled v009 executable.
/// </summary>
/// <remarks>
/// <para>
/// The two produce the same output - a vanilla unpack and repack round-trips the ISO byte-for-byte with either - but the
/// executable removes the Python dependency, which is the whole point of shipping it: a user should not have to install
/// an interpreter to mod the game.
/// </para>
/// <para>
/// The argument shapes differ (the scripts take v007 flags, the executable takes subcommands), so
/// <see cref="BatchContentBuilder"/> branches on <see cref="Mode"/> rather than trying to feed one command line to both.
/// </para>
/// </remarks>
/// <param name="Mode">Which program to run.</param>
/// <param name="ExecutablePath">The tool exe, when <paramref name="Mode"/> is <see cref="ToolchainMode.BundledExecutable"/>.</param>
/// <param name="ScriptsFolder">The folder holding the toolbelt scripts, for Python mode.</param>
/// <param name="PythonCommand">Interpreter command line, e.g. <c>python</c> or <c>py -3</c>.</param>
public sealed record Toolchain(
    ToolchainMode Mode,
    string? ExecutablePath,
    string ScriptsFolder,
    string PythonCommand)
{
    /// <summary>True when this toolchain needs an interpreter on PATH.</summary>
    public bool RequiresPython => Mode == ToolchainMode.PythonScripts;

    /// <summary>The bundled executable toolchain.</summary>
    public static Toolchain Bundled(string executablePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);

        return new Toolchain(
            ToolchainMode.BundledExecutable,
            executablePath,
            Path.GetDirectoryName(executablePath) ?? string.Empty,
            "python");
    }

    /// <summary>The Python script toolchain (the v007 toolbelt).</summary>
    public static Toolchain PythonScripts(string scriptsFolder, string pythonCommand = "python")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scriptsFolder);

        return new Toolchain(ToolchainMode.PythonScripts, null, scriptsFolder, pythonCommand);
    }

    /// <summary>
    /// Where the app looks for the bundled toolchain inside its own folder: <c>tools/v009/tools_v009.exe</c>.
    /// </summary>
    public static string BundledExecutableIn(string applicationDirectory) =>
        Path.Combine(applicationDirectory, "tools", "v009", "tools_v009.exe");

    /// <summary>
    /// Pick the toolchain to use: the bundled executable when the file is there, otherwise the Python scripts.
    /// </summary>
    /// <param name="applicationDirectory">Folder holding the app and its <c>tools</c> subfolder.</param>
    /// <param name="scriptsFolder">Folder holding the v007 scripts.</param>
    /// <param name="pythonCommand">Interpreter for the script toolchain.</param>
    /// <param name="preferBundled">False to force the scripts even when the exe exists (an escape hatch for testing).</param>
    public static Toolchain Choose(
        string applicationDirectory,
        string scriptsFolder,
        string pythonCommand = "python",
        bool preferBundled = true)
    {
        if (preferBundled)
        {
            string bundled = BundledExecutableIn(applicationDirectory);
            if (File.Exists(bundled)) return Bundled(bundled);
        }

        return PythonScripts(scriptsFolder, pythonCommand);
    }
}
