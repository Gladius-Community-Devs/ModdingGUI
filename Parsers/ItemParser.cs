using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ModdingGUI.Core.Api;

namespace ModdingGUI
{
    /// <summary>
    /// Parses <c>skills.tok</c> into the list of <see cref="SkillInfo"/> objects the shell
    /// shows in its picker.
    /// </summary>
    /// <remarks>
    /// This only builds a view model for display. Reading and writing the file belongs to
    /// <see cref="ISkillsApi"/>; lookup text comes from <see cref="ILookupTextApi"/> and is
    /// passed in pre-loaded so a caller that already has the table does not read it twice.
    /// </remarks>
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
            IReadOnlyDictionary<int, string> lookup,
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
                    current.DisplayName = LookupTextResolver.ResolveValue(trimmedLine, lookup, out _, current.InternalName);
                }
            }
            if (current != null) list.Add(current);
            logAction?.Invoke($"Skill Parsing complete. {list.Count} skills loaded.", Color.Green);
            return list;
        }
    }
}
