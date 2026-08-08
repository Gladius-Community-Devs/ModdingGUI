using Xunit;

namespace ModdingGUI.Tests;

/// <summary>
/// Locates real game data in the main project's build output.
/// <para>
/// Project folder resolution order:
///   1. <c>REALDATA_PROJECT</c> environment variable — set by run-tests.ps1
///      when -ProjectFolder is passed, or set manually before running dotnet test.
///   2. Auto-scan — the first directory in the build output whose name starts
///      with <c>REALDATA_</c> (case-insensitive).
/// </para>
/// The resolved folder must follow the standard app layout:
/// <c>{root}/{name}/{name}_BEC/data/...</c>
/// </summary>
internal static class GameDataFixture
{
    private static readonly Lazy<string?> _root    = new(FindGameDataRoot);
    private static readonly Lazy<string?> _project = new(FindProjectFolder);

    public static bool IsAvailable => Root != null && ProjectFolder != null;

    /// <summary>The build output directory (win-x64) that contains vanilla.iso.</summary>
    public static string? Root => _root.Value;

    /// <summary>Full path to the resolved REALDATA_ project folder.</summary>
    public static string? ProjectFolder => _project.Value;

    // Derived paths — only valid when IsAvailable is true --------------------

    private static string ProjectName =>
        Path.GetFileName(ProjectFolder!.TrimEnd(Path.DirectorySeparatorChar,
                                                 Path.AltDirectorySeparatorChar))!;

    /// <summary>
    /// The _BEC subfolder inside the project. Scans for any directory whose name
    /// ends with "_BEC" so the fixture works whether or not the outer folder was
    /// renamed (e.g. rags2.0.1 -> REALDATA_rags2.0.1 without renaming its children).
    /// Falls back to {ProjectName}_BEC if no match is found.
    /// </summary>
    public static string BecFolder =>
        Directory.EnumerateDirectories(ProjectFolder!)
            .FirstOrDefault(d => Path.GetFileName(d)
                .EndsWith("_BEC", StringComparison.OrdinalIgnoreCase))
        ?? Path.Combine(ProjectFolder!, $"{ProjectName}_BEC");
    public static string ConfigFolder   => Path.Combine(BecFolder, "data", "config");
    public static string UnitsFolder    => Path.Combine(BecFolder, "data", "units");

    public static string ItemsTok       => Path.Combine(ConfigFolder, "items.tok");
    public static string SkillsTok      => Path.Combine(ConfigFolder, "skills.tok");
    public static string LookupText     => Path.Combine(ConfigFolder, "lookuptext_eng.txt");
    public static string ClassDefs      => Path.Combine(ConfigFolder, "classdefs.tok");
    public static string GladiatorsFile => Path.Combine(UnitsFolder,  "gladiators.txt");
    public static string ItemsetsFile   => Path.Combine(UnitsFolder,  "itemsets.txt");
    public static string SkillsetsFile  => Path.Combine(UnitsFolder,  "skillsets.txt");

    public static string VanillaIso     => Path.Combine(Root!, "vanilla.iso");

    // -------------------------------------------------------------------------

    private static string? FindGameDataRoot()
    {
        string? dir = AppDomain.CurrentDomain.BaseDirectory;
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir, "ModdingGUI.csproj")))
            {
                foreach (string config in new[] { "Debug", "Release" })
                {
                    string candidate = Path.Combine(dir, "bin", config, "net8.0-windows", "win-x64");
                    if (Directory.Exists(candidate) && File.Exists(Path.Combine(candidate, "vanilla.iso")))
                        return candidate;
                }
                return null;
            }

            string? parent = Path.GetDirectoryName(dir);
            if (parent == dir) break;
            dir = parent;
        }
        return null;
    }

    private static string? FindProjectFolder()
    {
        if (Root == null) return null;

        // 1. Explicit override via environment variable
        string? envName = Environment.GetEnvironmentVariable("REALDATA_PROJECT");
        if (!string.IsNullOrWhiteSpace(envName))
        {
            string envPath = Path.IsPathRooted(envName)
                ? envName
                : Path.Combine(Root, envName);
            return Directory.Exists(envPath) ? envPath : null;
        }

        // 2. Auto-scan for the first REALDATA_ folder in the build output
        return Directory.EnumerateDirectories(Root)
            .FirstOrDefault(d => Path.GetFileName(d)
                .StartsWith("REALDATA_", StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>
/// Custom [Fact] attribute that auto-skips when real game data is unavailable.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
internal sealed class GameDataFactAttribute : FactAttribute
{
    public GameDataFactAttribute()
    {
        if (!GameDataFixture.IsAvailable)
            Skip = "Real game data not found. Ensure 'vanilla.iso' exists in the main " +
                   "project's build output and a folder whose name starts with 'REALDATA_' " +
                   "(or set the REALDATA_PROJECT environment variable to point to one).";
    }
}
