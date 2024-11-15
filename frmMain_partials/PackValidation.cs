using System.Text.RegularExpressions;

namespace ModdingGUI
{
    public partial class frmMain
    { 

        private bool ValidateItemsets(string projectFolder)
        {
            projectFolder = projectFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string itemsetsPath = Path.Combine(projectFolder, $"{Path.GetFileName(projectFolder)}_BEC", "data", "units", "itemsets.txt");

            if (!File.Exists(itemsetsPath))
            {
                MessageBox.Show("itemsets.txt not found at specified path.");
                return false;
            }

            string[] lines = File.ReadAllLines(itemsetsPath);
            var weaponLevels = new Dictionary<int, List<(int MinLevel, int MaxLevel)>>();
            var allItemsets = new HashSet<int>();

            // Initialize regex patterns
            var itemsetRegex = new Regex(@"Itemset (\d+):", RegexOptions.Compiled);
            var weaponRegex = new Regex(@"^(\d+)\s+(\d+)\s+.*\[Weapon\]", RegexOptions.Compiled);

            // Calculate total itemsets for progress bar
            int totalItemsets = lines.Count(line => itemsetRegex.IsMatch(line));
            pgbValidation.Maximum += totalItemsets; // Increment existing maximum
            // No need to reset pgbValidation.Value here

            int currentItemset = -1;

            foreach (string line in lines)
            {
                // Check if the line is an itemset definition
                Match itemsetMatch = itemsetRegex.Match(line);
                if (itemsetMatch.Success)
                {
                    currentItemset = int.Parse(itemsetMatch.Groups[1].Value);
                    allItemsets.Add(currentItemset);
                    if (!weaponLevels.ContainsKey(currentItemset))
                    {
                        weaponLevels[currentItemset] = new List<(int, int)>();
                    }

                    IncrementProgressBar(); // Increment for each itemset
                    continue;
                }

                // Check if the line is a weapon entry
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
                // No need to increment progress here as it's already handled during itemset definition parsing

                if (!weaponLevels.ContainsKey(itemset) || weaponLevels[itemset].Count == 0)
                    continue;

                var levels = weaponLevels[itemset].OrderBy(x => x.MinLevel).ToList();

                int currentEnd = 0;
                var gaps = new List<(int, int)>();

                foreach (var (minLevel, maxLevel) in levels)
                {
                    if (minLevel > currentEnd + 1)
                        gaps.Add((currentEnd + 1, minLevel - 1));

                    currentEnd = Math.Max(currentEnd, maxLevel);
                }

                if (currentEnd < 99)
                    gaps.Add((currentEnd + 1, 99));

                if (gaps.Count > 0)
                {
                    foreach (var (startGap, endGap) in gaps)
                    {
                        AppendLog($"Itemset {itemset} is missing weapon coverage between levels {startGap} and {endGap}.", ErrorColor, false);
                    }
                    overallIsValid = false;
                }
            }

            return overallIsValid;
        }

        private bool ValidateGladiatorsFile(string projectFolder)
        {
            projectFolder = projectFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string gladiatorsPath = Path.Combine(projectFolder, $"{Path.GetFileName(projectFolder)}_BEC", "data", "units", "gladiators.txt");

            if (!File.Exists(gladiatorsPath))
            {
                MessageBox.Show("gladiators.txt not found at specified path.");
                return false;
            }

            string[] lines = File.ReadAllLines(gladiatorsPath);
            bool isValid = true;

            var fieldRegex = new Regex(@"^(Name|Class|Outfit|Affinity|Tint set|Skill set|Stat set|Item set|School):\s*(.+)$", RegexOptions.Compiled);

            // Set progress bar maximum to the number of non-empty lines
            int totalSteps = lines.Count(line => line.Trim().Length > 0);
            pgbValidation.Maximum += totalSteps; // Increment existing maximum
            // No need to reset pgbValidation.Value here

            foreach (string line in lines)
            {
                if (line.Trim().Length == 0) continue;

                IncrementProgressBar();

                Match match = fieldRegex.Match(line);
                if (!match.Success || string.IsNullOrWhiteSpace(match.Groups[2].Value))
                {
                    AppendLog($"Error in gladiators.txt: Missing or empty field in line: {line}", ErrorColor, false);
                    isValid = false;
                }
            }

            return isValid;
        }

        private bool ValidateGladiatorsAndSkills(string projectFolder)
        {
            projectFolder = projectFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string gladiatorsPath = Path.Combine(projectFolder, $"{Path.GetFileName(projectFolder)}_BEC", "data", "units", "gladiators.txt");
            string skillsetsPath = Path.Combine(projectFolder, $"{Path.GetFileName(projectFolder)}_BEC", "data", "units", "skillsets.txt");
            string skillsTokPath = Path.Combine(projectFolder, $"{Path.GetFileName(projectFolder)}_BEC", "data", "config", "skills.tok");
            string classDefsPath = Path.Combine(projectFolder, $"{Path.GetFileName(projectFolder)}_BEC", "data", "config", "classdefs.tok");

            if (!File.Exists(gladiatorsPath) || !File.Exists(skillsetsPath) || !File.Exists(skillsTokPath) || !File.Exists(classDefsPath))
            {
                AppendLog("One or more required files (gladiators.txt, skillsets.txt, skills.tok, classdefs.tok) are missing.", ErrorColor, false);
                return false;
            }

            // Parse files once and cache data
            var gladiatorData = ParseGladiatorClassesAndSkillSets(gladiatorsPath);
            var classDefs = ParseClassDefs(classDefsPath);
            var skillSets = ParseSkillSetsValidation(skillsetsPath);
            var skills = ParseSkills(skillsTokPath);

            // Calculate total skills to set progress bar correctly
            int totalSkills = gladiatorData.Sum(g => skillSets.ContainsKey(g.SkillSetId) ? skillSets[g.SkillSetId].Count : 0);
            pgbValidation.Maximum += gladiatorData.Count + totalSkills; // Increment existing maximum

            bool validationPassed = true;

            foreach (var gladiator in gladiatorData)
            {
                IncrementProgressBar(); // Increment for gladiator

                string className = gladiator.Class;
                int skillSetId = gladiator.SkillSetId;
                string unitName = gladiator.UnitName;

                if (!classDefs.TryGetValue(className, out string skillUseName))
                {
                    // Check if any skill in the skill set contains "Sub"
                    if (skillSets.TryGetValue(skillSetId, out List<string> subSkillNames) && subSkillNames.Any(s => s.IndexOf("Sub", StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        // Skip logging the missing SKILLUSENAME error
                        continue;
                    }
                    else
                    {
                        AppendLog($"Error: SKILLUSENAME not found for class '{className}' in classdefs.tok.", ErrorColor, false);
                        validationPassed = false;
                        continue;
                    }
                }

                if (!skillSets.TryGetValue(skillSetId, out List<string> skillNames) || skillNames.Count == 0)
                {
                    AppendLog($"Warning: Skill set {skillSetId} for class '{className}' is empty or not found.", WarningColor, false);
                    continue;
                }

                foreach (string skillName in skillNames)
                {
                    IncrementProgressBar(); // Increment for each skill

                    // Skip SKILLUSECLASS validation for skills with "Sub" in the name
                    if (skillName.IndexOf("Sub", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        continue;
                    }

                    if (!skills.TryGetValue(skillName, out List<string> skillUseClasses) || skillUseClasses.Count == 0)
                    {
                        AppendLog($"Error: Skill '{skillName}' not found in skills.tok.", ErrorColor, false);
                        validationPassed = false;
                        continue;
                    }

                    bool isMatch = skillUseClasses.Any(skillUseClass => IsSkillUseClassValid(skillUseName, skillUseClass));
                    if (!isMatch)
                    {
                        AppendLog($"Error: Skill '{skillName}' in skill set {skillSetId} does not align with SKILLUSENAME '{skillUseName}'. Unit: {unitName}", ErrorColor, false);
                        validationPassed = false;
                    }
                }
            }

            return validationPassed;
        }



        private List<(string Class, int SkillSetId, string UnitName)> ParseGladiatorClassesAndSkillSets(string gladiatorsPath)
        {
            var gladiatorClasses = new List<(string Class, int SkillSetId, string UnitName)>();
            string unitName = string.Empty;
            string className = string.Empty;
            int skillSetId = -1;

            foreach (string line in File.ReadLines(gladiatorsPath))
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

        // Updated ParseClassDefs to ensure proper chunk parsing and handling for SKILLUSENAME
        private Dictionary<string, string> ParseClassDefs(string classDefsPath)
{
            var classDefs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string currentClass = null;
            string skillUseName = null;

            // This regex matches "CREATECLASS: <ClassName>" at the start of a new class chunk
            var classRegex = new Regex(@"^CREATECLASS:\s*(.+)", RegexOptions.Compiled);

            // This regex matches "SKILLUSENAME:" with any leading whitespace (including tabs)
            var skillUseNameRegex = new Regex(@"^\s*SKILLUSENAME:\s*(.+)", RegexOptions.Compiled);

            foreach (string line in File.ReadLines(classDefsPath))
            {
                Match classMatch = classRegex.Match(line);
                if (classMatch.Success)
                {
                    // Save the previous class if it had a skillUseName
                    if (currentClass != null && skillUseName != null)
                    {
                        classDefs[currentClass] = skillUseName;
                    }

                    // Start a new class definition
                    currentClass = classMatch.Groups[1].Value.Trim().Trim('"');
                    skillUseName = null; // Reset for the new class
                    continue;
                }

                if (currentClass != null)
                {
                    // Check for the SKILLUSENAME line with any leading whitespace
                    Match skillUseNameMatch = skillUseNameRegex.Match(line);
                    if (skillUseNameMatch.Success)
                    {
                        skillUseName = skillUseNameMatch.Groups[1].Value.Trim().Trim('"');
                    }

                    // End current class if another CREATECLASS starts without finding SKILLUSENAME
                    if (line.StartsWith("CREATECLASS:"))
                    {
                        currentClass = null;
                        skillUseName = null;
                    }
                }
            }

            // Capture the last class processed if SKILLUSENAME was found
            if (currentClass != null && skillUseName != null)
            {
                classDefs[currentClass] = skillUseName;
            }

            return classDefs;
        }


        private Dictionary<int, List<string>> ParseSkillSetsValidation(string skillsetsPath)
        {
            var skillSets = new Dictionary<int, List<string>>();
            int currentSkillSetId = -1;
            var skillSetRegex = new Regex(@"^Skillset\s+(\d+):", RegexOptions.Compiled);

            foreach (string line in File.ReadLines(skillsetsPath))
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
                    {
                        string skillName = skillMatch.Groups[1].Value.Trim();
                        skillSets[currentSkillSetId].Add(skillName);
                    }
                }
            }

            return skillSets;
        }

        private Dictionary<string, List<string>> ParseSkills(string skillsTokPath)
        {
            var skills = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            string currentSkill = null;
            var skillCreateRegex = new Regex(@"^\s*SKILLCREATE:\s*""(.+?)"",", RegexOptions.IgnoreCase);

            AppendLog("Starting to parse skills.tok...", InfoColor, false);

            foreach (string line in File.ReadLines(skillsTokPath))
            {
                // Match the SKILLCREATE line to start a new skill block
                Match skillMatch = skillCreateRegex.Match(line);
                if (skillMatch.Success)
                {
                    currentSkill = skillMatch.Groups[1].Value.Trim();
                    skills[currentSkill] = new List<string>();  // Initialize a new list for SKILLUSECLASS values
                    //AppendLog($"Found skill: {currentSkill}", InfoColor, false);
                    continue;
                }

                // Capture each SKILLUSECLASS if within the current skill's block
                if (currentSkill != null)
                {
                    if (line.TrimStart().StartsWith("SKILLUSECLASS:", StringComparison.OrdinalIgnoreCase))
                    {
                        string skillUseClass = line.Split(":")[1].Trim().Trim('"');
                        skills[currentSkill].Add(skillUseClass);
                        //AppendLog($"Assigned SKILLUSECLASS '{skillUseClass}' to skill '{currentSkill}'", InfoColor, false);
                    }

                    // End the current skill block if we encounter a new SKILLCREATE
                    if (line.TrimStart().StartsWith("SKILLCREATE:", StringComparison.OrdinalIgnoreCase))
                    {
                        currentSkill = null;
                    }
                }
            }

            AppendLog("Finished parsing skills.tok", InfoColor, false);
            return skills;
        }

        private bool IsSkillUseClassValid(string skillUseName, string skillUseClass)
        {
            var allowedSkillUseClasses = new List<string> { skillUseName };

            // Special cases for specific SKILLUSENAME values
            var specialSkillUseClasses = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
            {
                { "Urlan", new List<string> { "Bear", "Cat", "Wolf", "Scarab" } },
                { "Barbarian", new List<string> { "Bear", "Cat", "Wolf", "Scarab" } },
                { "Gungnir", new List<string> { "Bear", "Cat", "Wolf", "Scarab" } },
                { "BerserkerEnraged", new List<string> { "Bear", "Cat", "Wolf", "Scarab" } }
            };

            if (specialSkillUseClasses.TryGetValue(skillUseName, out var extraClasses))
            {
                allowedSkillUseClasses.AddRange(extraClasses);
            }

            return allowedSkillUseClasses.Contains(skillUseClass, StringComparer.OrdinalIgnoreCase);
        }


        private void IncrementProgressBar()
        {
            if (pgbValidation.InvokeRequired)
            {
                pgbValidation.Invoke(new Action(IncrementProgressBar));
            }
            else
            {
                if (pgbValidation.Value < pgbValidation.Maximum)
                {
                    pgbValidation.Value += 1;
                }
            }
        }
    }
}
