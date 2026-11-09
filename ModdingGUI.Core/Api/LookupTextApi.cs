namespace ModdingGUI.Core.Api;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ModdingGUI.Core.Tok;

/// <summary>The five shipped localisations of the lookup text table.</summary>
public enum LookupLanguage
{
    /// <summary>English (<c>lookuptext_eng.txt</c>).</summary>
    English,

    /// <summary>French (<c>lookuptext_fra.txt</c>).</summary>
    French,

    /// <summary>German (<c>lookuptext_ger.txt</c>).</summary>
    German,

    /// <summary>Italian (<c>lookuptext_ita.txt</c>).</summary>
    Italian,

    /// <summary>Spanish (<c>lookuptext_spa.txt</c>).</summary>
    Spanish,
}

/// <summary>Path and naming rules for the lookup text tables.</summary>
public static class LookupLanguages
{
    /// <summary>Every shipped language, English first.</summary>
    public static IReadOnlyList<LookupLanguage> All { get; } = new[]
    {
        LookupLanguage.English,
        LookupLanguage.French,
        LookupLanguage.German,
        LookupLanguage.Italian,
        LookupLanguage.Spanish,
    };

    /// <summary>The three letter suffix used in the file name, e.g. <c>eng</c>.</summary>
    public static string Suffix(LookupLanguage language) => language switch
    {
        LookupLanguage.English => "eng",
        LookupLanguage.French => "fra",
        LookupLanguage.German => "ger",
        LookupLanguage.Italian => "ita",
        LookupLanguage.Spanish => "spa",
        _ => throw new ArgumentOutOfRangeException(nameof(language)),
    };

    /// <summary>Project-relative path of a language's text table.</summary>
    public static string RelativePath(LookupLanguage language) =>
        $"data/config/lookuptext_{Suffix(language)}.txt";
}

/// <summary>
/// Locked, backup-before-write API for the <c>lookuptext_&lt;lang&gt;.txt</c> tables.
/// </summary>
/// <remarks>
/// <para>
/// Supports both vanilla <c>id^speak^listen^text</c> rows and modder <c>id^text</c> rows.
/// Updates preserve all non-text columns and leave every other line untouched.
/// </para>
/// <para>
/// The files are <b>not</b> UTF-8: the corpus contains Latin-1 high bytes and the pack
/// tool (<c>Update_Strings_Bin.py</c>) reads them with <c>encoding="ansi"</c>. Reading
/// them as UTF-8 replaced those bytes with U+FFFD and writing them back re-encoded them
/// as three-byte sequences, corrupting the table. They are read and written as
/// ISO-8859-1, which is byte-transparent for every value 0–255.
/// </para>
/// </remarks>
public sealed class LookupTextApi : ILookupTextApi
{
    /// <summary>Canonical (English) relative path; see <see cref="LookupLanguages"/> for others.</summary>
    public const string RelativePath = "data/config/lookuptext_eng.txt";

    private static readonly Encoding Enc = TokDocument.CanonicalEncoding;

    private static readonly HashSet<string> VoiceFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "Either", "Masculine", "Feminine"
    };

    // ---- ILookupTextApi (English) -------------------------------------------

    /// <inheritdoc />
    public IReadOnlyDictionary<int, string> Load(string projectPath) =>
        Load(projectPath, LookupLanguage.English);

    /// <inheritdoc />
    public bool Update(string projectPath, int id, string text) =>
        Update(projectPath, id, text, LookupLanguage.English);

    /// <inheritdoc />
    public int Add(string projectPath, string text) =>
        Add(projectPath, text, LookupLanguage.English);

    // ---- language aware -----------------------------------------------------

    /// <summary>Load one language's table.</summary>
    public IReadOnlyDictionary<int, string> Load(string projectPath, LookupLanguage language)
    {
        using (ProjectLock.Acquire(projectPath))
        {
            var (lines, _) = Read(projectPath, language);
            return Parse(lines);
        }
    }

    /// <summary>Replace the text of one id, preserving its other columns.</summary>
    public bool Update(string projectPath, int id, string text, LookupLanguage language)
    {
        ArgumentNullException.ThrowIfNull(text);

        using (ProjectLock.Acquire(projectPath))
        {
            var (lines, trailing) = Read(projectPath, language);
            for (int i = 0; i < lines.Count; i++)
            {
                var parts = lines[i].Split('^');
                if (parts.Length == 0 || !int.TryParse(parts[0].Trim(), out var currentId) || currentId != id)
                    continue;

                lines[i] = RewriteText(parts, text);
                Write(projectPath, language, lines, trailing);
                return true;
            }

            return false;
        }
    }

    /// <summary>Append a new id (max + 1) with the given text.</summary>
    public int Add(string projectPath, string text, LookupLanguage language)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Lookup text is required.", nameof(text));

        using (ProjectLock.Acquire(projectPath))
        {
            var (lines, trailing) = Read(projectPath, language);
            var table = Parse(lines);
            int id = table.Count == 0 ? 1 : table.Keys.Max() + 1;

            lines.Add($"{id}^{text}");
            Write(projectPath, language, lines, trailing || true);
            return id;
        }
    }

    /// <summary>
    /// Languages whose file exists in the project. Mods ship English only more often
    /// than not, so callers must not assume all five are present.
    /// </summary>
    public IReadOnlyList<LookupLanguage> AvailableLanguages(string projectPath) =>
        LookupLanguages.All
            .Where(language => File.Exists(PathFor(projectPath, language)))
            .ToArray();

    // ---- plumbing -----------------------------------------------------------

    private static string PathFor(string projectPath, LookupLanguage language) =>
        Path.Combine(projectPath,
            LookupLanguages.RelativePath(language).Replace('/', Path.DirectorySeparatorChar));

    private static (List<string> lines, bool trailing) Read(string projectPath, LookupLanguage language)
    {
        string path = PathFor(projectPath, language);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"lookuptext_{LookupLanguages.Suffix(language)}.txt not found for project '{projectPath}'. " +
                $"Expected at '{path}'.", path);
        }

        string text = File.ReadAllText(path, Enc);
        string normalised = text.Replace("\r\n", "\n").Replace("\r", "\n");
        bool trailing = normalised.Length > 0 && normalised[^1] == '\n';
        if (normalised.Length == 0) return (new List<string>(), trailing);
        return (normalised.TrimEnd('\n').Split('\n').ToList(), trailing);
    }

    private static void Write(string projectPath, LookupLanguage language, List<string> lines, bool trailing)
    {
        string path = PathFor(projectPath, language);
        ChangeHistoryService.Backup(projectPath, path);
        string text = string.Join("\r\n", lines) + (trailing ? "\r\n" : string.Empty);
        File.WriteAllText(path, text, Enc);
    }

    private static Dictionary<int, string> Parse(IEnumerable<string> lines)
    {
        var d = new Dictionary<int, string>();
        foreach (var line in lines)
        {
            var parts = line.Split('^');
            if (parts.Length >= 2 && int.TryParse(parts[0].Trim(), out var id))
                d[id] = IsVanillaFormat(parts) ? parts[3].Trim() : parts[1].Trim();
        }
        return d;
    }

    private static string RewriteText(string[] parts, string text)
    {
        if (IsVanillaFormat(parts))
            return $"{parts[0]}^{parts[1]}^{parts[2]}^{text}";

        var newParts = new List<string> { parts[0], text };
        if (parts.Length > 2) newParts.AddRange(parts.Skip(2));
        return string.Join("^", newParts);
    }

    private static bool IsVanillaFormat(string[] parts) =>
        parts.Length >= 4
        && VoiceFields.Contains(parts[1])
        && VoiceFields.Contains(parts[2]);
}
