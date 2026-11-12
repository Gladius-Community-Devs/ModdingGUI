namespace ModdingGUI.Core.Tools;

using System.Security.Cryptography;
using System.Text.Json;

/// <summary>
/// Records the hashes of the files the pack chain *derives* from editable text, so a pack can tell whether regenerating
/// them would be a no-op.
/// </summary>
/// <remarks>
/// <para>
/// Three pack steps rebuild binaries from text: <c>toknum</c> (NUMENTRIES), <c>tok compress</c> (skills.tok into
/// skills_strings.bin / skills_lines.bin / skills.tok.brf) and <c>strings</c> (lookuptext_*.txt into lookuptext_*.bin).
/// Running them on unedited files <b>does not reproduce the vanilla bytes</b>: the skills tokenizer assigns string indices
/// by frequency and then alphabetically, while the game's own builder evidently used a different tiebreak - decompressing
/// the vanilla skills.tok and recompressing it yields the same 44,351-byte .brf with a different hash, and the lookuptext
/// bins change as well. The v007 Python toolbelt and v009 produce identical hashes to each other, so this is a property of
/// the format rather than a bug in one toolchain.
/// </para>
/// <para>
/// Consequence: a vanilla unpack/pack could never round-trip the ISO byte-for-byte, because those three steps rewrote
/// files with equivalent-but-different bytes. This guard restores the invariant the project wants - an unedited project
/// repacks byte-identically - by skipping the regeneration when the sources are still byte-identical to what unpack
/// produced. Edit skills.tok or a lookuptext file and the steps run as before.
/// </para>
/// <para>
/// The manifest lives in the <b>project</b> folder, never in <c>_BEC</c>: the packer ships every file under _BEC that its
/// list does not name, which is how <c>.mod-history</c> once ended up inside the ISO.
/// </para>
/// </remarks>
public static class DerivedToolState
{
    /// <summary>Manifest file name, in the project folder next to <c>*_BEC</c> and <c>*_ISO</c>.</summary>
    public const string ManifestName = ".derived-state.json";

    /// <summary>Inputs whose hashes decide whether the derived steps can be skipped.</summary>
    private static IEnumerable<string> SourceFiles(string becFolder)
    {
        string config = Path.Combine(becFolder, "data", "config");
        if (!Directory.Exists(config)) yield break;

        string skills = Path.Combine(config, "skills.tok");
        if (File.Exists(skills)) yield return skills;

        foreach (string lookuptext in Directory.EnumerateFiles(config, "lookuptext_*.txt"))
        {
            yield return lookuptext;
        }
    }

    private static string Relative(string becFolder, string file) =>
        Path.GetRelativePath(becFolder, file).Replace('\\', '/');

    /// <summary>Manifest path for a project.</summary>
    public static string PathFor(string projectFolder) =>
        Path.Combine(projectFolder, ManifestName);

    /// <summary>Hash every derived source file and write the manifest for <paramref name="projectFolder"/>.</summary>
    public static void Capture(string projectFolder, string becFolder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectFolder);
        ArgumentException.ThrowIfNullOrWhiteSpace(becFolder);

        var hashes = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (string file in SourceFiles(becFolder))
        {
            hashes[Relative(becFolder, file)] = Hash(file);
        }

        if (hashes.Count == 0) return;

        Directory.CreateDirectory(projectFolder);
        File.WriteAllText(PathFor(projectFolder), JsonSerializer.Serialize(
            new Manifest(1, hashes),
            new JsonSerializerOptions { WriteIndented = true }));
    }

    /// <summary>
    /// True when every recorded source file still has the hash it had at unpack time, so the derived binaries are already
    /// correct and regenerating them would only change bytes.
    /// </summary>
    public static bool SourcesUnchanged(string projectFolder, string becFolder)
    {
        if (string.IsNullOrWhiteSpace(projectFolder) || string.IsNullOrWhiteSpace(becFolder)) return false;

        string manifestPath = PathFor(projectFolder);
        if (!File.Exists(manifestPath)) return false;

        Manifest? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<Manifest>(File.ReadAllText(manifestPath));
        }
        catch (JsonException)
        {
            return false;
        }

        if (manifest?.Files is null || manifest.Files.Count == 0) return false;

        foreach ((string relative, string expected) in manifest.Files)
        {
            string file = Path.Combine(becFolder, relative.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(file)) return false;
            if (!string.Equals(Hash(file), expected, StringComparison.OrdinalIgnoreCase)) return false;
        }

        return true;
    }

    /// <summary>The steps that regenerate derived binaries, by step name.</summary>
    public static readonly string[] DerivedStepNames = { "toknum", "tok compress", "strings", "Tok_Num_Update.py", "tok-tool.py", "Update_Strings_Bin.py" };

    /// <summary>Drop the derived-binary steps; everything else (IDX repack, BEC pack, ISO pack) still runs.</summary>
    public static IReadOnlyList<ToolStep> WithoutDerivedSteps(IReadOnlyList<ToolStep> steps)
    {
        ArgumentNullException.ThrowIfNull(steps);

        var kept = new List<ToolStep>(steps.Count);
        foreach (ToolStep step in steps)
        {
            if (Array.Exists(DerivedStepNames, name => string.Equals(name, step.Name, StringComparison.Ordinal))) continue;
            kept.Add(step);
        }

        return kept;
    }

    private static string Hash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    /// <summary>Manifest shape; the version field makes a future format change detectable instead of silently trusted.</summary>
    private sealed record Manifest(int Version, SortedDictionary<string, string> Files);
}
