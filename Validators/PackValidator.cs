using System.Text.RegularExpressions;

namespace ModdingGUI
{
    public static class PackValidator
    {
        // -------------------------------------------------------------------------
        // Public validators
        // -------------------------------------------------------------------------

        public static ValidationResult ValidateItemsets(IEnumerable<string> itemsetLines)
        {
            var issues = new List<ValidationIssue>();
            var weaponLevels = new Dictionary<int, List<(int MinLevel, int MaxLevel)>>();
            var allItemsets = new HashSet<int>();

            var itemsetRegex = new Regex(@"Itemset (\d+):", RegexOptions.Compiled);
            var weaponRegex = new Regex(@"^(\d+)\s+(\d+)\s+.*\[Weapon\]", RegexOptions.Compiled);

            int currentItemset = -1;
            int stepCount = 0;

            foreach (string line in itemsetLines)
            {
                Match itemsetMatch = itemsetRegex.Match(line);
                if (itemsetMatch.Success)
                {
                    currentItemset = int.Parse(itemsetMatch.Groups[1].Value);
                    allItemsets.Add(currentItemset);
                    if (!weaponLevels.ContainsKey(currentItemset))
                        weaponLevels[currentItemset] = new List<(int, int)>();

                    stepCount++;
                    continue;
                }

                Match weaponMatch = weaponRegex.Match(line);
                if (weaponMatch.Success && currentItemset != -1)
                {
                    int minLevel = int.Parse(weaponMatch.Groups[1].Value);
                    int maxLevel = int.Parse(weaponMatch.Groups[2].Value);
                    weaponLevels[currentItemset].Add((minLevel, maxLevel));
                }
            }

            bool overallIsValid = true;
            foreach (int itemset in allItemsets)
            {
                if (!weaponLevels.ContainsKey(itemset) || weaponLevels[itemset].Count == 0)
                    continue;

                var levels = weaponLevels[itemset].OrderBy(x => x.MinLevel).ToList();
                int currentEnd = 0;
                var gaps = new List<(int Start, int End)>();

                foreach (var (minLevel, maxLevel) in levels)
                {
                    if (minLevel > currentEnd + 1)
                        gaps.Add((currentEnd + 1, minLevel - 1));
                    currentEnd = Math.Max(currentEnd, maxLevel);
                }

                if (currentEnd < 99)
                    gaps.Add((currentEnd + 1, 99));

                foreach (var (startGap, endGap) in gaps)
                {
                    issues.Add(new ValidationIssue(
                        $"Itemset {itemset} is missing weapon coverage between levels {startGap} and {endGap}.",
                        ValidationSeverity.Error));
                    overallIsValid = false;
                }
            }

            return new ValidationResult(overallIsValid, issues, stepCount);
        }

        public static ValidationResult ValidateGladiatorsFile(IEnumerable<string> gladiatorLines)
        {
            var issues = new List<ValidationIssue>();
            bool isValid = true;
            int stepCount = 0;

            var fieldRegex = new Regex(
                @"^(Name|Class|Outfit|Affinity|Tint set|Skill set|Stat set|Item set|School):\s*(.+)$",
                RegexOptions.Compiled);

            foreach (string line in gladiatorLines)
            {
                if (line.Trim().Length == 0) continue;

                stepCount++;

                Match match = fieldRegex.Match(line);
                if (!match.Success || string.IsNullOrWhiteSpace(match.Groups[2].Value))
                {
                    issues.Add(new ValidationIssue(
                        $"Error in gladiators.txt: Missing or empty field in line: {line}",
                        ValidationSeverity.Error));
                    isValid = false;
                }
            }

            return new ValidationResult(isValid, issues, stepCount);
        }

        public static ValidationResult ValidateGladiatorsAndSkills(
            IEnumerable<string> gladiatorLines,
            IEnumerable<string> skillsetLines,
            IEnumerable<string> skillsTokLines,
            IEnumerable<string> classDefLines)
        {
            var issues = new List<ValidationIssue>();
            bool validationPassed = true;

            var gladiatorData = ParseGladiatorClassesAndSkillSets(gladiatorLines);
            var classDefs = ParseClassDefs(classDefLines);
            var skillSets = ParseSkillSets(skillsetLines);
            var skills = ParseSkills(skillsTokLines);

            int totalSkills = gladiatorData.Sum(g =>
                skillSets.ContainsKey(g.SkillSetId) ? skillSets[g.SkillSetId].Count : 0);
            int stepCount = gladiatorData.Count + totalSkills;

            foreach (var gladiator in gladiatorData)
            {
                string className = gladiator.Class;
                int skillSetId = gladiator.SkillSetId;
                string unitName = gladiator.UnitName;

                if (!classDefs.TryGetValue(className, out string skillUseName))
                {
                    if (skillSets.TryGetValue(skillSetId, out List<string> subSkillNames) &&
                        subSkillNames.Any(s => s.IndexOf("Sub", StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        continue;
                    }

                    issues.Add(new ValidationIssue(
                        $"Error: SKILLUSENAME not found for class '{className}' in classdefs.tok.",
                        ValidationSeverity.Error));
                    validationPassed = false;
                    continue;
                }

                if (!skillSets.TryGetValue(skillSetId, out List<string> skillNames) || skillNames.Count == 0)
                {
                    issues.Add(new ValidationIssue(
                        $"Warning: Skill set {skillSetId} for class '{className}' is empty or not found.",
                        ValidationSeverity.Warning));
                    continue;
                }

                foreach (string skillName in skillNames)
                {
                    if (skillName.IndexOf("Sub", StringComparison.OrdinalIgnoreCase) >= 0)
                        continue;

                    if (!skills.TryGetValue(skillName, out List<string> skillUseClasses) || skillUseClasses.Count == 0)
                    {
                        issues.Add(new ValidationIssue(
                            $"Error: Skill '{skillName}' not found in skills.tok.",
                            ValidationSeverity.Error));
                        validationPassed = false;
                        continue;
                    }

                    bool isMatch = skillUseClasses.Any(suc => IsSkillUseClassValid(skillUseName, suc));
                    if (!isMatch)
                    {
                        issues.Add(new ValidationIssue(
                            $"Error: Skill '{skillName}' in skill set {skillSetId} does not align with SKILLUSENAME '{skillUseName}'. Unit: {unitName}",
                            ValidationSeverity.Error));
                        validationPassed = false;
                    }
                }
            }

            return new ValidationResult(validationPassed, issues, stepCount);
        }

        // -------------------------------------------------------------------------
        // Internal parsers (accessible to tests via InternalsVisibleTo)
        // -------------------------------------------------------------------------

        internal static List<(string Class, int SkillSetId, string UnitName)> ParseGladiatorClassesAndSkillSets(
            IEnumerable<string> lines)
        {
            var gladiatorClasses = new List<(string Class, int SkillSetId, string UnitName)>();
            string unitName = string.Empty;
            string className = string.Empty;
            int skillSetId = -1;

            foreach (string line in lines)
            {
                if (line.StartsWith("Class: "))
                {
                    className = line.Split(": ")[1].Trim();
                }
                else if (line.StartsWith("Skill set: "))
                {
                    skillSetId = int.Parse(line.Split(": ")[1].Trim());
                }
                else if (line.StartsWith("Name: ") && !string.IsNullOrEmpty(className) && skillSetId >= 0)
                {
                    gladiatorClasses.Add((className, skillSetId, unitName));
                    unitName = line.Split(": ")[1].Trim();
                    className = string.Empty;
                    skillSetId = -1;
                }
            }
            return gladiatorClasses;
        }

        internal static Dictionary<string, string> ParseClassDefs(IEnumerable<string> lines)
        {
            var classDefs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string currentClass = null;
            string skillUseName = null;

            var classRegex = new Regex(@"^CREATECLASS:\s*(.+)", RegexOptions.Compiled);
            var skillUseNameRegex = new Regex(@"^\s*SKILLUSENAME:\s*(.+)", RegexOptions.Compiled);

            foreach (string line in lines)
            {
                Match classMatch = classRegex.Match(line);
                if (classMatch.Success)
                {
                    if (currentClass != null && skillUseName != null)
                        classDefs[currentClass] = skillUseName;

                    currentClass = classMatch.Groups[1].Value.Trim().Trim('"');
                    skillUseName = null;
                    continue;
                }

                if (currentClass != null)
                {
                    Match skillUseNameMatch = skillUseNameRegex.Match(line);
                    if (skillUseNameMatch.Success)
                        skillUseName = skillUseNameMatch.Groups[1].Value.Trim().Trim('"');

                    if (line.StartsWith("CREATECLASS:"))
                    {
                        currentClass = null;
                        skillUseName = null;
                    }
                }
            }

            if (currentClass != null && skillUseName != null)
                classDefs[currentClass] = skillUseName;

            return classDefs;
        }

        internal static Dictionary<int, List<string>> ParseSkillSets(IEnumerable<string> lines)
        {
            var skillSets = new Dictionary<int, List<string>>();
            int currentSkillSetId = -1;
            var skillSetRegex = new Regex(@"^Skillset\s+(\d+):", RegexOptions.Compiled);

            foreach (string line in lines)
            {
                Match match = skillSetRegex.Match(line);
                if (match.Success)
                {
                    currentSkillSetId = int.Parse(match.Groups[1].Value);
                    skillSets[currentSkillSetId] = new List<string>();
                    continue;
                }

                if (currentSkillSetId >= 0)
                {
                    if (line.StartsWith("Skillset "))
                    {
                        currentSkillSetId = -1;
                        continue;
                    }

                    var skillMatch = Regex.Match(line, @"\d+\s+\d+\s+(.*)");
                    if (skillMatch.Success)
                        skillSets[currentSkillSetId].Add(skillMatch.Groups[1].Value.Trim());
                }
            }

            return skillSets;
        }

        internal static Dictionary<string, List<string>> ParseSkills(IEnumerable<string> lines)
        {
            var skills = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            string currentSkill = null;
            var skillCreateRegex = new Regex(@"^\s*SKILLCREATE:\s*""(.+?)"",", RegexOptions.IgnoreCase);

            foreach (string line in lines)
            {
                Match skillMatch = skillCreateRegex.Match(line);
                if (skillMatch.Success)
                {
                    currentSkill = skillMatch.Groups[1].Value.Trim();
                    skills[currentSkill] = new List<string>();
                    continue;
                }

                if (currentSkill != null)
                {
                    if (line.TrimStart().StartsWith("SKILLUSECLASS:", StringComparison.OrdinalIgnoreCase))
                    {
                        string skillUseClass = line.Split(":")[1].Trim().Trim('"');
                        skills[currentSkill].Add(skillUseClass);
                    }

                    if (line.TrimStart().StartsWith("SKILLCREATE:", StringComparison.OrdinalIgnoreCase))
                        currentSkill = null;
                }
            }

            return skills;
        }

        internal static bool IsSkillUseClassValid(string skillUseName, string skillUseClass)
        {
            var allowedSkillUseClasses = new List<string> { skillUseName };

            var specialSkillUseClasses = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
            {
                { "Urlan",            new List<string> { "Bear", "Cat", "Wolf", "Scarab" } },
                { "Barbarian",        new List<string> { "Bear", "Cat", "Wolf", "Scarab" } },
                { "Gungnir",          new List<string> { "Bear", "Cat", "Wolf", "Scarab" } },
                { "BerserkerEnraged", new List<string> { "Bear", "Cat", "Wolf", "Scarab" } }
            };

            if (specialSkillUseClasses.TryGetValue(skillUseName, out var extraClasses))
                allowedSkillUseClasses.AddRange(extraClasses);

            return allowedSkillUseClasses.Contains(skillUseClass, StringComparer.OrdinalIgnoreCase);
        }
    }
}
