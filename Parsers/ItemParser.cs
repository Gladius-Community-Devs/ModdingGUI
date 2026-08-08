using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace ModdingGUI
{
    /// <summary>
    /// Parses <c>skills.tok</c> into a list of <see cref="SkillInfo"/> objects.
    /// Does not write — skills.tok is read-only from the GUI until Phase 2.7.
    /// </summary>
    public static class SkillParser
    {
        private static readonly Regex _skillCreateRegex = new(@"SKILLCREATE:\s*""(?<name>[^""]+)""", RegexOptions.Compiled);

        /// <summary>
        /// Parse skills.tok using a caller-supplied lookup table (no second file read).
        /// Pass <c>null</c> for <paramref name="logAction"/> to suppress logging
        /// (safe to call from a background thread).
        /// </summary>
        public static IReadOnlyList<SkillInfo> ParseSkillsTok(
            string skillsTokPath,
            Dictionary<int, string> lookup,
            Action<string, Color?>? logAction)
        {
            if (!File.Exists(skillsTokPath))
            {
                logAction?.Invoke($"Skills.tok file not found at: {skillsTokPath}", Color.Red);
                return new List<SkillInfo>();
            }
            var list = new List<SkillInfo>();
            SkillInfo? current = null;

            foreach (var line in File.ReadLines(skillsTokPath).Select(raw => raw.TrimEnd()))
            {
                if (string.IsNullOrWhiteSpace(line)) { if (current != null) { list.Add(current); current = null; } continue; }
                string trimmedLine = line.TrimStart();
                if (trimmedLine.StartsWith("SKILLCREATE:", StringComparison.Ordinal))
                {
                    if (current != null) list.Add(current);
                    var m = _skillCreateRegex.Match(trimmedLine);
                    if (!m.Success) { logAction?.Invoke($"Invalid SKILLCREATE line: {line}", Color.Orange); current = null; continue; }
                    current = new SkillInfo { InternalName = m.Groups["name"].Value };
                    current.DisplayName = current.InternalName;
                    continue;
                }
                if (current == null) continue;
                if (trimmedLine.StartsWith("SKILLDISPLAYNAMEID:", StringComparison.Ordinal))
                {
                    current.DisplayName = LookupHelper.ResolveLookupValue(trimmedLine, lookup, out _, current.InternalName);
                }
            }
            if (current != null) list.Add(current);
            logAction?.Invoke($"Skill Parsing complete. {list.Count} skills loaded.", Color.Green);
            return list;
        }

        /// <summary>
        /// Parse skills.tok, loading the lookup table from <paramref name="lookupTextPath"/>.
        /// Kept for backward compatibility; prefer the overload that accepts a pre-loaded
        /// lookup dictionary to avoid a redundant file read.
        /// </summary>
        public static IReadOnlyList<SkillInfo> ParseSkillsTok(string skillsTokPath, string lookupTextPath, Action<string, Color?>? logAction)
        {
            var lookup = LookupHelper.LoadLookupTable(lookupTextPath);
            return ParseSkillsTok(skillsTokPath, lookup, logAction);
        }
    }

    internal static class LookupHelper
    {
        // The vanilla lookuptext_eng.txt uses the format:
        //   id^speak^listen^localized text
        // where speak/listen are "Either", "Masculine", or "Feminine".
        // Modders often strip those fields, leaving:
        //   id^localized text
        // Both formats must be read and written correctly.

        private static readonly HashSet<string> s_voiceFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Either", "Masculine", "Feminine"
        };

        /// <summary>
        /// Returns true when the split parts of a lookup line have the vanilla
        /// <c>id^speak^listen^text</c> layout (speak and listen are voice specifiers).
        /// </summary>
        private static bool IsVanillaFormat(string[] parts) =>
            parts.Length >= 4
            && s_voiceFields.Contains(parts[1])
            && s_voiceFields.Contains(parts[2]);

        public static Dictionary<int, string> LoadLookupTable(string path)
        {
            var d = new Dictionary<int, string>();
            if (!File.Exists(path)) return d;
            foreach (var ln in File.ReadLines(path))
            {
                var parts = ln.Split('^');
                if (parts.Length >= 2 && int.TryParse(parts[0].Trim(), out var id))
                {
                    // Vanilla: id^speak^listen^text  → text starts at index 3
                    // Modder:  id^text               → text is at index 1
                    // (any extra trailing columns are ignored in both cases)
                    string text = IsVanillaFormat(parts) ? parts[3] : parts[1];
                    d[id] = text.Trim();
                }
            }
            return d;
        }

        public static string ResolveLookupValue(string line, Dictionary<int, string> lookup, out int? foundId, string fallback = "")
        {
            foundId = null;
            var valuePart = line.Split(new[] { ':' }, 2);
            if (valuePart.Length < 2) return fallback;
            if (int.TryParse(valuePart[1].Trim(), out var id))
            {
                foundId = id;
                return lookup.TryGetValue(id, out var txt) ? txt : fallback;
            }
            return fallback;
        }

        public static bool UpdateLookupEntry(string lookupTextPath, int idToUpdate, string newText)
        {
            if (!File.Exists(lookupTextPath)) return false;
            var lines = File.ReadAllLines(lookupTextPath).ToList();
            bool updated = false;
            for (int i = 0; i < lines.Count; i++)
            {
                var parts = lines[i].Split('^');
                if (parts.Length > 0 && int.TryParse(parts[0], out var currentId) && currentId == idToUpdate)
                {
                    if (IsVanillaFormat(parts))
                    {
                        // Preserve the speak/listen voice fields (indices 1 and 2);
                        // replace only the text field (index 3).
                        lines[i] = $"{parts[0]}^{parts[1]}^{parts[2]}^{newText}";
                    }
                    else
                    {
                        // Modder format: replace text field (index 1) and preserve
                        // any extra trailing columns (index 2+) unchanged.
                        var newParts = new List<string> { parts[0], newText };
                        if (parts.Length > 2) newParts.AddRange(parts.Skip(2));
                        lines[i] = string.Join("^", newParts);
                    }
                    updated = true;
                    break;
                }
            }
            if (updated) File.WriteAllLines(lookupTextPath, lines);
            return updated;
        }

        public static int GetNextAvailableLookupId(Dictionary<int, string> lookupTable)
        {
            if (lookupTable == null || !lookupTable.Any()) return 1;
            return lookupTable.Keys.Max() + 1;
        }

        public static int? AddNewLookupEntry(string lookupTextPath, string newText, Dictionary<int, string> lookupTable, Action<string, Color?>? logAction = null)
        {
            if (string.IsNullOrWhiteSpace(newText))
            {
                logAction?.Invoke("Cannot add empty text to lookup.", Color.Orange);
                return null;
            }
            try
            {
                int newId = GetNextAvailableLookupId(lookupTable);
                string newLine = $"{newId}^{newText}";
                File.AppendAllText(lookupTextPath, newLine + Environment.NewLine);
                lookupTable[newId] = newText;
                logAction?.Invoke($"Added new lookup entry: ID {newId}, Text: \"{newText}\"", Color.Green);
                return newId;
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"Error adding new lookup entry: {ex.Message}", Color.Red);
                return null;
            }
        }
    }
}
