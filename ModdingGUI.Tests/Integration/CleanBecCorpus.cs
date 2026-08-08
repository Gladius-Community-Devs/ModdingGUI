namespace ModdingGUI.Tests;

/// <summary>
/// Locates the vanilla reference corpus at <c>clean-bec-unpacked/</c>, which
/// lives at the repo root (one level above the solution directory).
/// </summary>
/// <remarks>
/// <para>
/// This fixture is distinct from <see cref="GameDataFixture"/>:
/// </para>
/// <list type="bullet">
///   <item><see cref="GameDataFixture"/> locates a built project's <c>REALDATA_*</c>
///   folder for integration tests that simulate unpack/pack flows.</item>
///   <item><see cref="CleanBecCorpus"/> locates the checked-in, untouched vanilla
///   reference data used by the Phase 1 round-trip fidelity gate and, in later
///   phases, as schema fixtures.</item>
/// </list>
/// <para>
/// The <c>clean-bec-unpacked/</c> directory is considered the ground truth for
/// byte-for-byte round-trip tests. If this fixture cannot locate it, all
/// <see cref="CleanBecFactAttribute"/>-gated tests are skipped with a clear
/// diagnostic rather than silently passing.
/// </para>
/// </remarks>
internal static class CleanBecCorpus
{
    private static readonly Lazy<string?> _root = new(FindCorpusRoot);

    public static bool IsAvailable => Root != null;

    /// <summary>Absolute path to <c>clean-bec-unpacked/</c>, or <c>null</c> if not found.</summary>
    public static string? Root => _root.Value;

    /// <summary>
    /// Enumerates every <c>.tok</c> and <c>.enc</c> file in the corpus.
    /// The list is sorted by relative path so test output is stable.
    /// </summary>
    public static IEnumerable<string> EnumerateTokAndEnc()
    {
        if (Root == null) yield break;
        var exts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".tok", ".enc" };
        foreach (var file in Directory
            .EnumerateFiles(Root, "*", SearchOption.AllDirectories)
            .Where(f => exts.Contains(Path.GetExtension(f)))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            yield return file;
        }
    }

    /// <summary>
    /// Converts an absolute corpus path back to a corpus-relative path for
    /// human-readable assertion messages.
    /// </summary>
    public static string Relative(string absolute)
    {
        if (Root == null) return absolute;
        return Path.GetRelativePath(Root, absolute).Replace('\\', '/');
    }

    private static string? FindCorpusRoot()
    {
        // Search strategy:
        //   Walk up from AppDomain.BaseDirectory looking for a directory that
        //   contains either:
        //     - a direct child "clean-bec-unpacked/"  (typical: repo root while
        //       tests run from ModdingGUI/ModdingGUI.Tests/bin/Debug/...).
        //     - a sibling of ModdingGUI.sln whose sibling repo root holds it.
        //   The corpus is recognized by the presence of "data/config/items.tok".
        string? dir = AppDomain.CurrentDomain.BaseDirectory;
        while (dir != null)
        {
            string candidate = Path.Combine(dir, "clean-bec-unpacked");
            if (LooksLikeCorpus(candidate))
                return candidate;

            string? parent = Path.GetDirectoryName(dir);
            if (parent == null || parent == dir) break;
            dir = parent;
        }
        return null;
    }

    private static bool LooksLikeCorpus(string candidate)
    {
        if (!Directory.Exists(candidate)) return false;
        // Canonical marker file from the corpus survey.
        string marker = Path.Combine(candidate, "data", "config", "items.tok");
        return File.Exists(marker);
    }
}

/// <summary>
/// Skippable fact that runs only when the <c>clean-bec-unpacked/</c> corpus is
/// locatable. Used by Phase 1 round-trip fidelity tests and any subsequent test
/// that needs the vanilla reference data.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
internal sealed class CleanBecFactAttribute : Xunit.FactAttribute
{
    public CleanBecFactAttribute()
    {
        if (!CleanBecCorpus.IsAvailable)
        {
            Skip = "clean-bec-unpacked/ not found. This test requires the vanilla " +
                   "reference corpus at the repo root (a sibling of the ModdingGUI/ " +
                   "solution directory) and validates byte-for-byte round trips " +
                   "against data/config/items.tok and friends.";
        }
    }
}
