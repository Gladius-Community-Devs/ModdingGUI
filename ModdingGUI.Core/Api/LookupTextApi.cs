namespace ModdingGUI.Core.Api;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

/// <summary>
/// Locked, backup-before-write API for <c>lookuptext_eng.txt</c>.
/// </summary>
/// <remarks>
/// Supports both vanilla <c>id^speak^listen^text</c> rows and modder
/// <c>id^text</c> rows. Updates preserve all non-text columns on the edited
/// row and leave unrelated rows byte-for-byte at line granularity.
/// </remarks>
public sealed class LookupTextApi : ILookupTextApi
{
    public const string RelativePath = "data/config/lookuptext_eng.txt";

    private static readonly Encoding Enc = Encoding.UTF8;
    private static readonly HashSet<string> VoiceFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "Either", "Masculine", "Feminine"
    };

    public IReadOnlyDictionary<int, string> Load(string projectPath)
    {
        using (ProjectLock.Acquire(projectPath))
        {
            var (lines, _) = Read(projectPath);
            return Parse(lines);
        }
    }

    public bool Update(string projectPath, int id, string text)
    {
        using (ProjectLock.Acquire(projectPath))
        {
            var (lines, trailing) = Read(projectPath);
            for (int i = 0; i < lines.Count; i++)
            {
                var parts = lines[i].Split('^');
                if (parts.Length == 0 || !int.TryParse(parts[0].Trim(), out var currentId) || currentId != id)
                    continue;

                lines[i] = RewriteText(parts, text);
                Write(projectPath, lines, trailing);
                return true;
            }

            return false;
        }
    }

    public int Add(string projectPath, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Lookup text is required.", nameof(text));

        using (ProjectLock.Acquire(projectPath))
        {
            var (lines, trailing) = Read(projectPath);
            var table = Parse(lines);
            int id = table.Count == 0 ? 1 : table.Keys.Max() + 1;

            lines.Add($"{id}^{text}");
            Write(projectPath, lines, trailing: true);
            return id;
        }
    }

    private static string PathFor(string projectPath) =>
        Path.Combine(projectPath, RelativePath.Replace('/', Path.DirectorySeparatorChar));

    private static (List<string> lines, bool trailing) Read(string projectPath)
    {
        string path = PathFor(projectPath);
        if (!File.Exists(path))
            throw new FileNotFoundException("lookuptext_eng.txt not found.", path);

        string text = File.ReadAllText(path, Enc);
        string normalised = text.Replace("\r\n", "\n").Replace("\r", "\n");
        bool trailing = normalised.Length > 0 && normalised[^1] == '\n';
        if (normalised.Length == 0) return (new List<string>(), trailing);
        return (normalised.TrimEnd('\n').Split('\n').ToList(), trailing);
    }

    private static void Write(string projectPath, List<string> lines, bool trailing)
    {
        string path = PathFor(projectPath);
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
