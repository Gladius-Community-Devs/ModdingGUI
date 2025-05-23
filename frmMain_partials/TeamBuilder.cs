using System.Text.RegularExpressions;

namespace ModdingGUI
{
    /// <summary>
    /// Partial class containing all Team Builder related functionalities.
    /// </summary>
    public partial class frmMain
    {
        #region Data Models

        /// <summary>
        /// Represents a unit class with its properties.
        /// </summary>
        public class ClassDefinition
        {
            public string ClassName { get; set; }
            public List<string> Attributes { get; set; } = new List<string>();
            public string Affinity { get; set; }
            public List<AllowedGear> AllowedGears { get; set; } = new List<AllowedGear>();

            public override string ToString()
            {
                return ClassName;
            }

        }
        /// <summary>
        /// Represents item fields.
        /// </summary>
        /// <summary>
        /// Represents the statistical attributes of a unit.
        /// </summary>
        public class Stats
        {
            // Base Stats
            public int CON { get; set; }
            public int PWR { get; set; }
            public int ACC { get; set; }
            public int DEF { get; set; }
            public int INI { get; set; }

            // Calculated Stats
            public int HP { get; set; }
            public int DAM { get; set; }
            public int MOV { get; set; }

            /// <summary>
            /// Creates a deep copy of the Stats object.
            /// </summary>
            /// <returns>A cloned Stats object.</returns>
            public Stats Clone()
            {
                return new Stats
                {
                    CON = this.CON,
                    PWR = this.PWR,
                    ACC = this.ACC,
                    DEF = this.DEF,
                    INI = this.INI,
                    HP = this.HP,
                    DAM = this.DAM,
                    MOV = this.MOV
                };
            }

            // Override ToString for easy display
            public override string ToString()
            {
                return $"CON: {CON}\nPWR: {PWR}\nACC: {ACC}\nDEF: {DEF}\nINI: {INI}\nHP: {HP}\nDAM: {DAM}\nMOV: {MOV}";
            }
        }

        /// <summary>
        /// Represents a unit with its properties and stats.
        /// </summary>
        public class Unit
        {
            public string UnitName { get; set; }
            public ClassDefinition Class { get; set; }
            public List<Skill> LearnedSkills { get; set; } = new List<Skill>();
            public List<Gear> EquippedGear { get; set; } = new List<Gear>();
            public int Level { get; set; } = 1; // Default to level 1

            // BaseStats are set directly from statsets.txt
            public Stats BaseStats { get; set; } = new Stats();

            // CurrentStats is calculated based on BaseStats and equipment bonuses
            public Stats CurrentStats => CalculateCurrentStats();

            private Stats CalculateCurrentStats()
            {
                // Clone base stats to avoid modifying the original
                Stats calculatedStats = BaseStats.Clone();

                // Calculate HP
                calculatedStats.HP = calculatedStats.CON * 6;

                // Calculate DAM
                calculatedStats.DAM = calculatedStats.PWR + GetEquipmentStatBonus("DAM");

                // Calculate MOV
                calculatedStats.MOV = calculatedStats.INI;

                // Apply other equipment bonuses
                calculatedStats.ACC += GetEquipmentStatBonus("ACC");
                calculatedStats.DEF += GetEquipmentStatBonus("DEF");
                calculatedStats.INI += GetEquipmentStatBonus("INI");
                // MOV can also be influenced if needed

                return calculatedStats;
            }

            private int GetEquipmentStatBonus(string statName)
            {
                int bonus = 0;
                foreach (var item in EquippedGear)
                {
                    switch (statName.ToUpper())
                    {
                        case "HP":
                            if (item.GearType == GearType.Helmet || item.GearType == GearType.Armor)
                                bonus += item.HP;
                            break;
                        case "DAM":
                            if (item.GearType == GearType.Weapon)
                                bonus += item.Damage;
                            break;
                        case "ACC":
                            bonus += item.Accuracy;
                            break;
                        case "DEF":
                            bonus += item.Defense;
                            break;
                        case "INI":
                            bonus += item.Initiative;
                            break;
                            // Add other cases as needed
                    }
                }
                return bonus;
            }
        }

        /// <summary>
        /// Represents a skill with its properties.
        /// </summary>
        public class Skill
        {
            public string Name { get; set; }
            public string InternalName { get; set; }
            public string JPCost { get; set; }
            public List<string> ClassesThatCanUse { get; set; } = new List<string>();
            public int DisplayID { get; set; }

            public override string ToString()
            {
                return InternalName;
            }
        }

        /// <summary>
        /// Represents an equipment item with its properties.
        /// </summary>
        public enum GearType
        {
            Weapon,
            Armor,
            Helmet,
            Shield,
            Accessory
        }

        public class Gear
        {
            public string Name { get; set; }
            public string InternalName { get; set; }
            public GearType GearType { get; set; }
            public string SubType { get; set; }
            public string Style { get; set; }
            public int StarValue { get; set; }
            public int DisplayID { get; set; }
            public int MinLevel { get; set; }
            public string Affinity { get; set; }
            public int AffinityValue { get; set; }
            public List<Skill> Skills { get; set; } = new List<Skill>(); // Optional
            public int? Durability { get; set; } // Applicable only to shields and helmets

            // Stat bonuses
            public int HP { get; set; }
            public int Damage { get; set; }
            public int Accuracy { get; set; }
            public int Defense { get; set; }
            public int Initiative { get; set; }

            public override string ToString()
            {
                return InternalName;
            }
        }
        public class AllowedGear
        {
            public string GearType { get; set; }
            public string SubType { get; set; }
            public string Style { get; set; }
        }

        /// <summary>
        /// Represents a gladiator entry parsed from gladiators.txt.
        /// </summary>
        public class GladiatorEntry
        {
            public string Name { get; set; }
            public string Class { get; set; }
            public string Affinity { get; set; }
            public int Outfit { get; set; }
            public int TintSet { get; set; }
            public int SkillSet { get; set; }
            public int StatSet { get; set; }
            public int ItemSet { get; set; }
            public int School { get; set; }
        }

        #endregion

        #region Parsers

        /// <summary>
        /// Parses the classdefs.tok file to extract class definitions.
        /// </summary>
        public class ClassParser
        {
            public List<ClassDefinition> ParseClassDefs(string filePath)
            {
                var classDefinitions = new List<ClassDefinition>();

                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException($"The file '{filePath}' does not exist.");
                }

                var lines = File.ReadAllLines(filePath);
                ClassDefinition currentClass = null;

                foreach (var line in lines)
                {
                    var trimmedLine = line.Trim();

                    if (trimmedLine.StartsWith("CREATECLASS:", StringComparison.OrdinalIgnoreCase))
                    {
                        if (currentClass != null)
                        {
                            classDefinitions.Add(currentClass);
                        }

                        currentClass = new ClassDefinition();

                        var match = Regex.Match(trimmedLine, @"CREATECLASS:\s*(\w+)", RegexOptions.IgnoreCase);
                        if (match.Success)
                        {
                            currentClass.ClassName = match.Groups[1].Value.Trim();
                        }
                        else
                        {
                            currentClass = null;
                        }

                        continue;
                    }

                    if (currentClass != null)
                    {
                        if (trimmedLine.StartsWith("ATTRIBUTE:", StringComparison.OrdinalIgnoreCase))
                        {
                            var attrMatch = Regex.Match(trimmedLine, @"ATTRIBUTE:\s*""([^""]+)""", RegexOptions.IgnoreCase);
                            if (attrMatch.Success)
                            {
                                currentClass.Attributes.Add(attrMatch.Groups[1].Value.Trim());
                            }
                        }
                        else if (trimmedLine.StartsWith("AFFINITY:", StringComparison.OrdinalIgnoreCase))
                        {
                            var affMatch = Regex.Match(trimmedLine, @"AFFINITY:\s*""([^""]+)""", RegexOptions.IgnoreCase);
                            if (affMatch.Success)
                            {
                                currentClass.Affinity = affMatch.Groups[1].Value.Trim();
                            }
                        }
                        else if (trimmedLine.StartsWith("ITEMCAT:", StringComparison.OrdinalIgnoreCase))
                        {
                            var catMatch = Regex.Match(trimmedLine, @"ITEMCAT:\s*([^,]+)\s*,\s*([^,]+)\s*,\s*([^,]+)", RegexOptions.IgnoreCase);
                            if (catMatch.Success)
                            {
                                var allowedGear = new AllowedGear
                                {
                                    GearType = catMatch.Groups[1].Value.Trim(),
                                    SubType = catMatch.Groups[2].Value.Trim(),
                                    Style = catMatch.Groups[3].Value.Trim()
                                };
                                currentClass.AllowedGears.Add(allowedGear);
                            }
                        }
                    }
                }

                if (currentClass != null)
                {
                    classDefinitions.Add(currentClass);
                }

                return classDefinitions;
            }
        }


        /// <summary>
        /// Parses the gladiators.txt file to extract gladiator entries.
        /// </summary>
        public class GladiatorParser
        {
            /// <summary>
            /// Parses the gladiators.txt file and returns a list of GladiatorEntry objects.
            /// </summary>
            /// <param name="filePath">Path to the gladiators.txt file.</param>
            /// <returns>List of GladiatorEntry objects.</returns>
            public List<GladiatorEntry> ParseGladiators(string filePath)
            {
                var gladiators = new List<GladiatorEntry>();

                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException($"The file '{filePath}' does not exist.");
                }

                var lines = File.ReadAllLines(filePath);
                GladiatorEntry currentGladiator = null;

                foreach (var line in lines)
                {
                    var trimmedLine = line.Trim();

                    if (string.IsNullOrEmpty(trimmedLine))
                        continue;

                    if (trimmedLine.StartsWith("Name:", StringComparison.OrdinalIgnoreCase))
                    {
                        if (currentGladiator != null)
                        {
                            gladiators.Add(currentGladiator);
                        }

                        currentGladiator = new GladiatorEntry
                        {
                            Name = trimmedLine.Substring(5).Trim()
                        };
                        continue;
                    }

                    if (currentGladiator != null)
                    {
                        if (trimmedLine.StartsWith("Class:", StringComparison.OrdinalIgnoreCase))
                        {
                            currentGladiator.Class = trimmedLine.Substring(6).Trim();
                        }
                        else if (trimmedLine.StartsWith("Outfit:", StringComparison.OrdinalIgnoreCase))
                        {
                            if (int.TryParse(trimmedLine.Substring(7).Trim(), out int outfit))
                                currentGladiator.Outfit = outfit;
                        }
                        else if (trimmedLine.StartsWith("Affinity:", StringComparison.OrdinalIgnoreCase))
                        {
                            currentGladiator.Affinity = trimmedLine.Substring(9).Trim();
                        }
                        else if (trimmedLine.StartsWith("Tint set:", StringComparison.OrdinalIgnoreCase))
                        {
                            if (int.TryParse(trimmedLine.Substring(10).Trim(), out int tintSet))
                                currentGladiator.TintSet = tintSet;
                        }
                        else if (trimmedLine.StartsWith("Skill set:", StringComparison.OrdinalIgnoreCase))
                        {
                            if (int.TryParse(trimmedLine.Substring(10).Trim(), out int skillSet))
                                currentGladiator.SkillSet = skillSet;
                        }
                        else if (trimmedLine.StartsWith("Stat set:", StringComparison.OrdinalIgnoreCase))
                        {
                            if (int.TryParse(trimmedLine.Substring(9).Trim(), out int statSet))
                                currentGladiator.StatSet = statSet;
                        }
                        else if (trimmedLine.StartsWith("Item set:", StringComparison.OrdinalIgnoreCase))
                        {
                            if (int.TryParse(trimmedLine.Substring(9).Trim(), out int itemSet))
                                currentGladiator.ItemSet = itemSet;
                        }
                        else if (trimmedLine.StartsWith("School:", StringComparison.OrdinalIgnoreCase))
                        {
                            if (int.TryParse(trimmedLine.Substring(7).Trim(), out int school))
                                currentGladiator.School = school;
                        }

                        // Future Parsing: Add other fields as needed
                    }
                }

                // Add the last parsed gladiator
                if (currentGladiator != null)
                {
                    gladiators.Add(currentGladiator);
                }

                return gladiators;
            }
        }

        /// <summary>
        /// Parses the statsets.txt file to extract stats for each statset and level.
        /// </summary>
        public class StatSetsParser
        {
            /// <summary>
            /// Parses the statsets.txt file and returns a dictionary mapping statset numbers to their level-based stats.
            /// </summary>
            /// <param name="filePath">Path to the statsets.txt file.</param>
            /// <returns>Dictionary of statset numbers to a dictionary of level and stats list.</returns>
            public Dictionary<int, Dictionary<int, List<int>>> ParseStatSets(string filePath)
            {
                var statSets = new Dictionary<int, Dictionary<int, List<int>>>();

                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException($"The file '{filePath}' does not exist.");
                }

                var lines = File.ReadAllLines(filePath);
                int currentStatSet = -1;

                foreach (var line in lines)
                {
                    var trimmedLine = line.Trim();

                    if (string.IsNullOrEmpty(trimmedLine))
                        continue;

                    if (trimmedLine.StartsWith("Statset ", StringComparison.OrdinalIgnoreCase))
                    {
                        var match = Regex.Match(trimmedLine, @"Statset\s+(\d+):", RegexOptions.IgnoreCase);
                        if (match.Success)
                        {
                            currentStatSet = int.Parse(match.Groups[1].Value);
                            if (!statSets.ContainsKey(currentStatSet))
                            {
                                statSets[currentStatSet] = new Dictionary<int, List<int>>();
                            }
                        }
                        else
                        {
                            currentStatSet = -1;
                        }
                        continue;
                    }

                    if (currentStatSet != -1 && Regex.IsMatch(trimmedLine, @"^\d+:\s*\d+\s+\d+\s+\d+\s+\d+\s+\d+"))
                    {
                        var parts = trimmedLine.Split(new[] { ':' }, 2);
                        if (parts.Length == 2)
                        {
                            if (int.TryParse(parts[0].Trim(), out int level))
                            {
                                var statsParts = parts[1].Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                                if (statsParts.Length >= 5)
                                {
                                    var stats = statsParts.Take(5).Select(int.Parse).ToList();
                                    statSets[currentStatSet][level] = stats;
                                }
                            }
                        }
                    }
                }

                return statSets;
            }
        }

        public class GearParser
        {
            public List<Gear> ParseGear(string filePath)
            {
                var gears = new List<Gear>();

                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException($"The file '{filePath}' does not exist.");
                }

                var lines = File.ReadAllLines(filePath);
                Gear currentGear = null;

                foreach (var line in lines)
                {
                    var trimmedLine = line.Trim();

                    if (string.IsNullOrEmpty(trimmedLine))
                        continue;

                    if (trimmedLine.StartsWith("ITEMCREATE:", StringComparison.OrdinalIgnoreCase))
                    {
                        if (currentGear != null)
                        {
                            gears.Add(currentGear);
                        }

                        currentGear = new Gear();

                        var match = Regex.Match(trimmedLine, @"ITEMCREATE:\s*""([^""]+)"",\s*""([^""]+)"",\s*""([^""]+)"",\s*""([^""]+)"",\s*(\d+)", RegexOptions.IgnoreCase);
                        if (match.Success)
                        {
                            currentGear.InternalName = match.Groups[1].Value.Trim();

                            if (Enum.TryParse<GearType>(match.Groups[2].Value.Trim(), out GearType gearType))
                            {
                                currentGear.GearType = gearType;
                            }

                            currentGear.SubType = match.Groups[3].Value.Trim();
                            currentGear.Style = match.Groups[4].Value.Trim();
                            currentGear.StarValue = int.Parse(match.Groups[5].Value.Trim());
                        }
                        else
                        {
                            currentGear = null;
                        }
                        continue;
                    }

                    if (currentGear != null)
                    {
                        if (trimmedLine.StartsWith("ITEMDISPLAYNAMEID:", StringComparison.OrdinalIgnoreCase))
                        {
                            var match = Regex.Match(trimmedLine, @"ITEMDISPLAYNAMEID:\s*(\d+)", RegexOptions.IgnoreCase);
                            if (match.Success)
                            {
                                currentGear.DisplayID = int.Parse(match.Groups[1].Value.Trim());
                            }
                        }
                        else if (trimmedLine.StartsWith("ITEMMINLEVEL:", StringComparison.OrdinalIgnoreCase))
                        {
                            var match = Regex.Match(trimmedLine, @"ITEMMINLEVEL:\s*(\d+)", RegexOptions.IgnoreCase);
                            if (match.Success)
                            {
                                currentGear.MinLevel = int.Parse(match.Groups[1].Value.Trim());
                            }
                        }
                        else if (trimmedLine.StartsWith("ITEMAFFINITY:", StringComparison.OrdinalIgnoreCase))
                        {
                            var match = Regex.Match(trimmedLine, @"ITEMAFFINITY:\s*(\w+)\s*,\s*(\d+)", RegexOptions.IgnoreCase);
                            if (match.Success)
                            {
                                currentGear.Affinity = match.Groups[1].Value.Trim();
                                currentGear.AffinityValue = int.Parse(match.Groups[2].Value.Trim());
                            }
                        }
                        else if (trimmedLine.StartsWith("ITEMSTATMOD:", StringComparison.OrdinalIgnoreCase))
                        {
                            var match = Regex.Match(trimmedLine, @"ITEMSTATMOD:\s*(\w+)\s*,\s*(-?\d+)", RegexOptions.IgnoreCase);
                            if (match.Success)
                            {
                                var statName = match.Groups[1].Value.Trim().ToLower();
                                var statValue = int.Parse(match.Groups[2].Value.Trim());
                                switch (statName)
                                {
                                    case "accuracy":
                                        currentGear.Accuracy = statValue;
                                        break;
                                    case "initiative":
                                        currentGear.Initiative = statValue;
                                        break;
                                    case "defense":
                                        currentGear.Defense = statValue;
                                        break;
                                    case "hp":
                                        currentGear.HP = statValue;
                                        break;
                                    case "damage":
                                        currentGear.Damage = statValue;
                                        break;
                                }
                            }
                        }
                        else if (trimmedLine.StartsWith("ITEMSKILL:", StringComparison.OrdinalIgnoreCase))
                        {
                            var match = Regex.Match(trimmedLine, @"ITEMSKILL:\s*""([^""]+)""", RegexOptions.IgnoreCase);
                            if (match.Success)
                            {
                                var skill = new Skill { InternalName = match.Groups[1].Value.Trim() };
                                currentGear.Skills.Add(skill);
                            }
                        }
                    }
                }

                if (currentGear != null)
                {
                    gears.Add(currentGear);
                }

                return gears;
            }
        }
        #endregion

        #region Class and Stats Management

        private List<Unit> teamUnits = new List<Unit>();
        private List<ClassDefinition> classDefinitions = new List<ClassDefinition>();
        private List<GladiatorEntry> gladiatorEntries = new List<GladiatorEntry>();
        private Dictionary<int, Dictionary<int, List<int>>> statSets = new Dictionary<int, Dictionary<int, List<int>>>();

        #endregion

        #region Team Builder Tab Activation

        /// <summary>
        /// Loads classes into the ddlTeamClasses ComboBox.
        /// </summary>
        private async void LoadClassesIntoDropdown()
        {
            // Verify if a project is selected by checking txtPackPath
            string projectPath = txtPackPath.Text.Trim();
            if (string.IsNullOrEmpty(projectPath) || !Directory.Exists(projectPath))
            {
                MessageBox.Show("No project selected. Please unpack or select a project first.", "Project Not Selected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                AppendLog("Attempted to access Team Builder without selecting a valid project.", Color.Red);

                // Switch back to tabPacking
                foreach (TabPage tab in tabContainer.TabPages)
                {
                    if (tab.Name == "tabPacking")
                    {
                        tabContainer.SelectedTab = tab;
                        break;
                    }
                }

                tabContainer.TabPages.Remove(tabTeamBuilder);
                teamBuilderToolStripMenuItem.Checked = false;
                return;
            }

            // Step 1: Trim trailing directory separators
            projectPath = projectPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            // Step 2: Extract the project folder name
            string projectFolderName = Path.GetFileName(projectPath);

            // Step 3: Construct the paths to classdefs.tok, gladiators.txt, and statsets.txt dynamically
            string classDefsPath = Path.Combine(projectPath, $"{projectFolderName}_BEC", "data", "config", "classdefs.tok");
            string gladiatorsPath = Path.Combine(projectPath, $"{projectFolderName}_BEC", "data", "units", "gladiators.txt");
            string statSetsPath = Path.Combine(projectPath, $"{projectFolderName}_BEC", "data", "units", "statsets.txt");

            // Verify that classDefsPath, gladiatorsPath, and statSetsPath exist
            if (!File.Exists(classDefsPath))
            {
                MessageBox.Show($"classdefs.tok not found at '{classDefsPath}'. Please ensure the project is correctly unpacked.", "File Not Found", MessageBoxButtons.OK, MessageBoxIcon.Error);
                AppendLog($"classdefs.tok not found at '{classDefsPath}'.", Color.Red);

                // Optionally, switch back to tabPacking
                foreach (TabPage tab in tabContainer.TabPages)
                {
                    if (tab.Name == "tabPacking")
                    {
                        tabContainer.SelectedTab = tab;
                        break;
                    }
                }

                tabContainer.TabPages.Remove(tabTeamBuilder);
                teamBuilderToolStripMenuItem.Checked = false;
                return;
            }

            if (!File.Exists(gladiatorsPath))
            {
                MessageBox.Show($"gladiators.txt not found at '{gladiatorsPath}'. Please ensure the project is correctly unpacked.", "File Not Found", MessageBoxButtons.OK, MessageBoxIcon.Error);
                AppendLog($"gladiators.txt not found at '{gladiatorsPath}'.", Color.Red);

                // Optionally, switch back to tabPacking
                foreach (TabPage tab in tabContainer.TabPages)
                {
                    if (tab.Name == "tabPacking")
                    {
                        tabContainer.SelectedTab = tab;
                        break;
                    }
                }

                tabContainer.TabPages.Remove(tabTeamBuilder);
                teamBuilderToolStripMenuItem.Checked = false;
                return;
            }

            if (!File.Exists(statSetsPath))
            {
                MessageBox.Show($"statsets.txt not found at '{statSetsPath}'. Please ensure the project is correctly unpacked.", "File Not Found", MessageBoxButtons.OK, MessageBoxIcon.Error);
                AppendLog($"statsets.txt not found at '{statSetsPath}'.", Color.Red);

                // Optionally, switch back to tabPacking
                foreach (TabPage tab in tabContainer.TabPages)
                {
                    if (tab.Name == "tabPacking")
                    {
                        tabContainer.SelectedTab = tab;
                        break;
                    }
                }

                tabContainer.TabPages.Remove(tabTeamBuilder);
                teamBuilderToolStripMenuItem.Checked = false;
                return;
            }

            // Proceed with loading class definitions, gladiators, and statsets
            try
            {
                var classParser = new ClassParser();
                classDefinitions = await Task.Run(() => classParser.ParseClassDefs(classDefsPath));

                var gladiatorParser = new GladiatorParser();
                gladiatorEntries = await Task.Run(() => gladiatorParser.ParseGladiators(gladiatorsPath));

                var statSetsParser = new StatSetsParser();
                statSets = await Task.Run(() => statSetsParser.ParseStatSets(statSetsPath));

                ddlTeamClasses.Items.Clear();
                foreach (var classDef in classDefinitions)
                {
                    ddlTeamClasses.Items.Add(classDef.ClassName);
                }

                if (ddlTeamClasses.Items.Count > 0)
                {
                    ddlTeamClasses.SelectedIndex = 0; // Select the first class by default
                }
                else
                {
                    MessageBox.Show("No classes found in the selected project.", "No Classes", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    AppendLog("No classes found in classdefs.tok.", Color.Blue);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading classes, gladiators, or statsets: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                AppendLog($"Error loading classes, gladiators, or statsets: {ex.Message}", Color.Red);

                // Optionally, switch back to tabPacking
                foreach (TabPage tab in tabContainer.TabPages)
                {
                    if (tab.Name == "tabPacking")
                    {
                        tabContainer.SelectedTab = tab;
                        break;
                    }
                }

                tabContainer.TabPages.Remove(tabTeamBuilder);
                teamBuilderToolStripMenuItem.Checked = false;
            }
        }

        #endregion

        #region Preview Update

        /// <summary>
        /// Handles the SelectedIndexChanged event of ddlTeamClasses to update the preview.
        /// </summary>
        private void ddlTeamClasses_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdatePreview();
        }

        /// <summary>
        /// Handles the TextChanged event of txtTeamUnitName to update the preview.
        /// </summary>
        private void txtTeamUnitName_TextChanged(object sender, EventArgs e)
        {
            UpdatePreview();
        }

        /// <summary>
        /// Updates the grpPreview with the current unit's information.
        /// </summary>
        private void UpdatePreview()
        {
            string unitName = txtTeamUnitName.Text.Trim();
            string selectedClassName = ddlTeamClasses.SelectedItem?.ToString();
            string levelText = txtTeamLevel.Text.Trim();
            int unitLevel = 1; // Default level

            // Validate Class Selection
            if (string.IsNullOrEmpty(selectedClassName))
            {
                ClearPreview();
                return;
            }

            var selectedClass = classDefinitions.FirstOrDefault(c => c.ClassName.Equals(selectedClassName, StringComparison.OrdinalIgnoreCase));

            if (selectedClass == null)
            {
                ClearPreview();
                return;
            }

            // Attempt to parse the level input
            if (!int.TryParse(levelText, out unitLevel) || unitLevel < 1 || unitLevel > 30)
            {
                txtPreviewStats.Text = "Invalid level input. Please enter a level between 1 and 30.";
                return;
            }

            // Determine the most used statset for the selected class
            int mostUsedStatSet = GetMostUsedStatSet(selectedClassName);
            if (mostUsedStatSet == -1)
            {
                txtPreviewStats.Text = $"No statset found for class '{selectedClassName}'.";
                return;
            }

            // Retrieve stats for the selected statset and level
            if (!statSets.ContainsKey(mostUsedStatSet) || !statSets[mostUsedStatSet].ContainsKey(unitLevel))
            {
                txtPreviewStats.Text = $"Stats not found for statset '{mostUsedStatSet}' at level '{unitLevel}'.";
                return;
            }

            var statsList = statSets[mostUsedStatSet][unitLevel];
            if (statsList.Count < 5)
            {
                txtPreviewStats.Text = $"Insufficient stats data for statset '{mostUsedStatSet}' at level '{unitLevel}'.";
                return;
            }

            // Assign stats based on the order: CON, PWR, ACC, DEF, INI
            Stats baseStats = new Stats
            {
                CON = statsList[0],
                PWR = statsList[1],
                ACC = statsList[2],
                DEF = statsList[3],
                INI = statsList[4]
            };

            // Calculate derived stats
            int HP = baseStats.CON * 6;
            int DAM = baseStats.PWR; // Equipment DAM is 0 for preview
            int MOV = baseStats.INI;

            // Format stats for display
            string statsText = $"HP: {HP}\r\n" +
                               $"DAM: {DAM}\r\n" +
                               $"PWR: {baseStats.PWR}\r\n" +
                               $"ACC: {baseStats.ACC}\r\n" +
                               $"DEF: {baseStats.DEF}\r\n" +
                               $"INI: {baseStats.INI}\r\n" +
                               $"CON: {baseStats.CON}\r\n" +
                               $"MOV: {MOV}";

            // Format AllowedGears for display
            string allowedGearsText = "Allowed Gears:\r\n" +
                string.Join("\r\n", selectedClass.AllowedGears.Select(ag =>
                    $"Type: {ag.GearType}, SubType: {ag.SubType}, Style: {ag.Style}"));

            // Combine Attributes and AllowedGears
            string attributesText = "Attributes:\r\n" +
                string.Join("\r\n", selectedClass.Attributes);

            // Display in txtPreviewStats
            txtPreviewStats.Text = statsText;
            txtPreviewUnitName.Text = unitName;
            txtPreviewAttributes.Text = attributesText;
            txtPreviewAllowedGear.Text = allowedGearsText;
        }


        /// <summary>
        /// Clears the grpPreview controls.
        /// </summary>
        private void ClearPreview()
        {
            txtPreviewUnitName.Text = string.Empty;
            txtPreviewAttributes.Text = string.Empty;
            txtPreviewStats.Text = string.Empty; // Clear stats as well
            txtPreviewAllowedGear.Text = string.Empty;
        }

        #endregion

        #region Add Unit to Team

        /// <summary>
        /// Handles the Click event of btnTeamAddUnit to add a unit to the tvwTeam TreeView.
        /// </summary>
        private void btnTeamAddUnit_Click(object sender, EventArgs e)
        {
            string unitName = txtTeamUnitName.Text.Trim();
            string selectedClassName = ddlTeamClasses.SelectedItem?.ToString();
            string levelText = txtTeamLevel.Text.Trim();
            int unitLevel = 1; // Default level

            // Validate Unit Name
            if (string.IsNullOrEmpty(unitName))
            {
                MessageBox.Show("Please enter a unit name.", "Input Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtTeamUnitName.Focus();
                return;
            }

            // Validate Class Selection
            if (string.IsNullOrEmpty(selectedClassName))
            {
                MessageBox.Show("Please select a class for the unit.", "Input Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                ddlTeamClasses.Focus();
                return;
            }

            // Validate Level Input
            if (!int.TryParse(levelText, out unitLevel) || unitLevel < 1 || unitLevel > 30)
            {
                MessageBox.Show("Please enter a valid unit level (integer between 1 and 30).", "Invalid Level", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtTeamLevel.Focus();
                return;
            }

            var selectedClass = classDefinitions.FirstOrDefault(c => c.ClassName.Equals(selectedClassName, StringComparison.OrdinalIgnoreCase));

            if (selectedClass == null)
            {
                MessageBox.Show("Selected class is invalid.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                AppendLog("Attempted to add a unit with an invalid class.", Color.Red);
                return;
            }

            // Check for Duplicate Unit Names
            if (tvwTeam.Nodes.Cast<TreeNode>().Any(n => n.Text.Equals(unitName, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show("A unit with this name already exists.", "Duplicate Unit", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtTeamUnitName.Focus();
                AppendLog($"Attempted to add duplicate unit '{unitName}'.", Color.Orange);
                return;
            }

            // Determine the most used statset for the selected class
            int mostUsedStatSet = GetMostUsedStatSet(selectedClassName);
            if (mostUsedStatSet == -1)
            {
                MessageBox.Show($"No statset found for class '{selectedClassName}'. Cannot create unit.", "StatSet Not Found", MessageBoxButtons.OK, MessageBoxIcon.Error);
                AppendLog($"No statset found for class '{selectedClassName}'.", Color.Red);
                return;
            }

            // Retrieve stats for the selected statset and level
            if (!statSets.ContainsKey(mostUsedStatSet) || !statSets[mostUsedStatSet].ContainsKey(unitLevel))
            {
                MessageBox.Show($"Stats not found for statset '{mostUsedStatSet}' at level '{unitLevel}'.", "Stats Not Found", MessageBoxButtons.OK, MessageBoxIcon.Error);
                AppendLog($"Stats not found for statset '{mostUsedStatSet}' at level '{unitLevel}'.", Color.Red);
                return;
            }

            var statsList = statSets[mostUsedStatSet][unitLevel];
            if (statsList.Count < 5)
            {
                MessageBox.Show($"Insufficient stats data for statset '{mostUsedStatSet}' at level '{unitLevel}'.", "Insufficient Stats", MessageBoxButtons.OK, MessageBoxIcon.Error);
                AppendLog($"Insufficient stats data for statset '{mostUsedStatSet}' at level '{unitLevel}'.", Color.Red);
                return;
            }

            // Assign stats based on the order: CON, PWR, ACC, DEF, INI
            var unit = new Unit
            {
                UnitName = unitName,
                Class = selectedClass,
                Level = unitLevel,
                BaseStats = new Stats
                {
                    CON = statsList[0],
                    PWR = statsList[1],
                    ACC = statsList[2],
                    DEF = statsList[3],
                    INI = statsList[4]
                }
                // EquippedGear and LearnedSkills are initialized as empty lists
            };

            teamUnits.Add(unit);
            // Add the unit to the team TreeView
            AddUnitToTreeView(unit);

            // Clear input fields after adding
            txtTeamUnitName.Clear();
            ddlTeamClasses.SelectedIndex = 0;

            // Log the addition
            AppendLog($"Added unit '{unitName}' with class '{selectedClassName}' at level {unitLevel}.", Color.Green, rtbPackOutput);
            int maxUnits = (rbnTeamCampaign.Checked && chbTeam40Glads.Checked) ? 34 : rbnTeamPVP.Checked ? 20 : rbnTeamCampaign.Checked ? 14 : 20;
            btnTeamAddUnit.Enabled = teamUnits.Count < maxUnits;
            txtTeamHeader.Text = $"Current Team: ({teamUnits.Count}/{maxUnits})";

        }

        /// <summary>
        /// Determines the most used statset for a given class from gladiators.txt.
        /// </summary>
        /// <param name="className">Name of the class.</param>
        /// <returns>The most frequently used statset number, or -1 if none found.</returns>
        private int GetMostUsedStatSet(string className)
        {
            var relevantStatSets = gladiatorEntries
                .Where(g => g.Class.Equals(className, StringComparison.OrdinalIgnoreCase))
                .GroupBy(g => g.StatSet)
                .Select(group => new { StatSet = group.Key, Count = group.Count() })
                .OrderByDescending(g => g.Count)
                .ThenBy(g => g.StatSet) // To ensure first encountered in case of tie
                .ToList();

            if (relevantStatSets.Any())
            {
                return relevantStatSets.First().StatSet;
            }

            return -1; // Indicates no statset found
        }

        /// <summary>
        /// Adds a Unit to the TreeView.
        /// </summary>
        /// <param name="unit">The Unit to add.</param>
        private void AddUnitToTreeView(Unit unit)
        {
            TreeNode unitNode = new TreeNode(unit.UnitName + " (" + unit.Class.ClassName + ")")
            {
                Tag = unit
            };

            // Class Attributes
            TreeNode attributesNode = new TreeNode("Class Attributes");
            foreach (var attribute in unit.Class.Attributes)
            {
                attributesNode.Nodes.Add(new TreeNode(attribute));
            }
            unitNode.Nodes.Add(attributesNode);

            // Stats Node
            TreeNode statsNode = new TreeNode("Stats");
            var stats = unit.CurrentStats;
            statsNode.Nodes.Add(new TreeNode($"CON: {stats.CON}"));
            statsNode.Nodes.Add(new TreeNode($"PWR: {stats.PWR}"));
            statsNode.Nodes.Add(new TreeNode($"ACC: {stats.ACC}"));
            statsNode.Nodes.Add(new TreeNode($"DEF: {stats.DEF}"));
            statsNode.Nodes.Add(new TreeNode($"INI: {stats.INI}"));
            statsNode.Nodes.Add(new TreeNode($"HP: {stats.HP}"));
            statsNode.Nodes.Add(new TreeNode($"DAM: {stats.DAM}"));
            statsNode.Nodes.Add(new TreeNode($"MOV: {stats.MOV}"));
            unitNode.Nodes.Add(statsNode);

            // Equipped Items
            TreeNode equipmentNode = new TreeNode("Equipped Items");
            foreach (var equipment in unit.EquippedGear)
            {
                TreeNode equipmentItemNode = new TreeNode(equipment.InternalName)
                {
                    Tag = equipment
                };
                equipmentNode.Nodes.Add(equipmentItemNode);
            }
            unitNode.Nodes.Add(equipmentNode);

            // Learned Skills
            TreeNode skillsNode = new TreeNode("Learned Skills");
            foreach (var skill in unit.LearnedSkills)
            {
                TreeNode skillNode = new TreeNode(skill.InternalName)
                {
                    Tag = skill
                };
                skillsNode.Nodes.Add(skillNode);
            }
            unitNode.Nodes.Add(skillsNode);

            // Add the unit node to the TreeView
            tvwTeam.Nodes.Add(unitNode);
        }

        #endregion

        #region Unit Selection/Removal and Preview

        /// <summary>
        /// Handles the AfterSelect event of tvwTeam to display unit stats and manage button states.
        /// </summary>
        private void tvwTeam_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (tabContainer.SelectedTab == tabTeamBuilder)
            {
                // Check if the selected node is a unit node (Level 0)
                if (e.Node.Level == 0 && e.Node.Tag is Unit unit)
                {
                    DisplayUnitStats(unit);
                    btnTeamRemoveUnit.Enabled = true;
                }
                else
                {
                    // If a child node is selected, clear the preview and disable the Remove button
                    ClearPreviewStats();
                    btnTeamRemoveUnit.Enabled = false;
                }
                AppendLog("Selected node: " + e.Node.Text, Color.Blue, rtbPackOutput);
                // Update other UI elements if necessary
                UpdateUnitCountAndButtons();
            }
            else if (tabContainer.SelectedTab == tabTeamGearSelection)
            {

            }
        }

        /// <summary>
        /// Handles the Click event of btnTeamRemoveUnit to remove the selected unit.
        /// </summary>
        private void btnTeamRemoveUnit_Click(object sender, EventArgs e)
        {
            // Check if a node is selected
            if (tvwTeam.SelectedNode == null)
            {
                MessageBox.Show("Please select a unit to remove.", "No Unit Selected", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Ensure that a unit node is selected (Level 0)
            if (tvwTeam.SelectedNode.Level != 0)
            {
                MessageBox.Show("Please select a unit node to remove.", "Invalid Selection", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Retrieve the selected Unit object
            var selectedUnit = tvwTeam.SelectedNode.Tag as Unit;
            if (selectedUnit == null)
            {
                MessageBox.Show("Selected node does not contain a valid unit.", "Invalid Unit", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Confirm removal with the user
            var confirmResult = MessageBox.Show($"Are you sure you want to remove the unit '{selectedUnit.UnitName}'?",
                                                "Confirm Removal",
                                                MessageBoxButtons.YesNo,
                                                MessageBoxIcon.Question);

            if (confirmResult != DialogResult.Yes)
            {
                return; // User canceled the removal
            }

            // Remove the unit from the internal list
            bool removedFromList = teamUnits.Remove(selectedUnit);
            if (!removedFromList)
            {
                MessageBox.Show("Failed to remove the unit from the internal list.", "Removal Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                AppendLog($"Failed to remove unit '{selectedUnit.UnitName}' from the internal list.", Color.Red);
                return;
            }

            // Remove the unit node from the TreeView
            tvwTeam.Nodes.Remove(tvwTeam.SelectedNode);

            // Clear the selection to prevent lingering references
            tvwTeam.SelectedNode = null;

            // Update UI Elements (e.g., Unit Count, Button States)
            UpdateUnitCountAndButtons();

            // Log the removal
            AppendLog($"Removed unit '{selectedUnit.UnitName}'.", Color.Blue);
        }


        /// <summary>
        /// Updates the unit count display and the state of action buttons based on the current teamUnits count.
        /// </summary>
        private void UpdateUnitCountAndButtons()
        {
            // Determine the maximum number of units based on the selected mode
            int maxUnits = (rbnTeamCampaign.Checked && chbTeam40Glads.Checked) ? 34 : rbnTeamPVP.Checked ? 20 : rbnTeamCampaign.Checked ? 14 : 20;

            // Update the team count display (assuming txtTeamHeader is a Label)
            txtTeamHeader.Text = $"Current Team: ({teamUnits.Count}/{maxUnits})";

            // Enable or disable the Add Unit button based on the current count
            btnTeamAddUnit.Enabled = teamUnits.Count < maxUnits;

            // Enable or disable the Remove Unit button based on whether a valid unit is selected
            if (tvwTeam.SelectedNode != null && tvwTeam.SelectedNode.Level == 0)
            {
                btnTeamRemoveUnit.Enabled = true;
            }
            else
            {
                btnTeamRemoveUnit.Enabled = false;
            }
        }

        /// <summary>
        /// Displays the selected unit's stats in txtPreviewStats.
        /// </summary>
        /// <param name="unit">The selected Unit.</param>
        private void DisplayUnitStats(Unit unit)
        {
            // Retrieve current stats
            var stats = unit.CurrentStats;

            // Format stats for display
            string statsText = $"Class: {unit.Class.ClassName}\r\n" +
                               $"Level: {unit.Level}\r\n" +
                               $"CON: {stats.CON}\r\n" +
                               $"PWR: {stats.PWR}\r\n" +
                               $"ACC: {stats.ACC}\r\n" +
                               $"DEF: {stats.DEF}\r\n" +
                               $"INI: {stats.INI}\r\n" +
                               $"HP: {stats.HP}\r\n" +
                               $"DAM: {stats.DAM}\r\n" +
                               $"MOV: {stats.MOV}";

            // Display in txtPreviewStats
            txtPreviewStats.Text = statsText;
            txtPreviewUnitName.Text = unit.UnitName;
        }

        /// <summary>
        /// Clears the txtPreviewStats TextBox.
        /// </summary>
        private void ClearPreviewStats()
        {
            txtPreviewStats.Clear();
        }

        #endregion

        #region Team Builder UI Initialization

        /// <summary>
        /// Handles UI initialization when opening the Team Builder tab.
        /// </summary>
        private void InitializeTeamBuilderUI()
        {
            // Set Campaign mode as default
            rbnTeamCampaign.Checked = true;
            
            // Disable PVP radio button since it's not ready yet
            rbnTeamPVP.Enabled = false;
            
            // Set team level text and enable/disable based on mode
            txtTeamLevel.Text = "1";
            
            // Hide PVP-only controls
            UpdateEquipmentRulesVisibility(false);
            
            // Hide gear and skill selection tabs initially
            if (tabTeamBuilderMaster.TabPages.Contains(tabTeamGearSelection))
                tabTeamBuilderMaster.TabPages.Remove(tabTeamGearSelection);
            
            if (tabTeamBuilderMaster.TabPages.Contains(tabTeamSkillSelection))
                tabTeamBuilderMaster.TabPages.Remove(tabTeamSkillSelection);
        }

        /// <summary>
        /// Handles the CheckedChanged event of rbnTeamPVP radio button.
        /// </summary>
        private void rbnTeamPVP_CheckedChanged(object sender, EventArgs e)
        {
            bool isPvpMode = rbnTeamPVP.Checked;
            
            // Enable/disable team level input based on mode
            txtTeamLevel.Enabled = isPvpMode;
            
            // Show/hide equipment rules
            UpdateEquipmentRulesVisibility(isPvpMode);
            
            // Show/hide gear and skill selection tabs
            if (isPvpMode)
            {
                if (!tabTeamBuilderMaster.TabPages.Contains(tabTeamGearSelection))
                    tabTeamBuilderMaster.TabPages.Add(tabTeamGearSelection);
                    
                if (!tabTeamBuilderMaster.TabPages.Contains(tabTeamSkillSelection))
                    tabTeamBuilderMaster.TabPages.Add(tabTeamSkillSelection);
            }
            else
            {
                if (tabTeamBuilderMaster.TabPages.Contains(tabTeamGearSelection))
                    tabTeamBuilderMaster.TabPages.Remove(tabTeamGearSelection);
                    
                if (tabTeamBuilderMaster.TabPages.Contains(tabTeamSkillSelection))
                    tabTeamBuilderMaster.TabPages.Remove(tabTeamSkillSelection);
            }
            
            // Update unit count and button states
            UpdateUnitCountAndButtons();
        }

        /// <summary>
        /// Updates the visibility of equipment rules controls.
        /// </summary>
        /// <param name="visible">Whether the controls should be visible.</param>
        private void UpdateEquipmentRulesVisibility(bool visible)
        {
            lblEquipmentRules.Visible = visible;
            chbTeamEquipRestrict.Visible = visible;
            label6.Visible = visible;
        }

        /// <summary>
        /// Handles the CheckedChanged event of chbTeam40Glads to prompt about the 40 glads gecko code.
        /// </summary>
        private void chbTeam40Glads_CheckedChanged(object sender, EventArgs e)
        {
            if (chbTeam40Glads.Checked)
            {
                var confirmResult = MessageBox.Show(
                    "This option requires the 40 glads gecko code to be enabled in Dolphin.\n\n" +
                    "Do you have this code enabled?",
                    "40 Glads Gecko Code Required",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (confirmResult != DialogResult.Yes)
                {
                    // User doesn't have the code enabled, uncheck the box
                    chbTeam40Glads.Checked = false;
                    AppendLog("40 glads option was unchecked because the gecko code is not enabled.", Color.Orange);
                }
                else
                {
                    AppendLog("40 glads option enabled.", Color.Green);
                }
            }
        }

        #endregion

        #region Team Export to School Files
        /// <summary>
        /// Appends all units from the team TreeView to an existing school file.
        /// </summary>
        /// <param name="projectFolder">The root project folder path.</param>
        /// <param name="fileName">The name of the school file (e.g. "valens_school.txt" or "ursula_school.txt").</param>
        private void AppendTeamUnitsToFile(string projectFolder, string fileName)
        {
            // Validate parameters
            if (string.IsNullOrEmpty(projectFolder) || !Directory.Exists(projectFolder))
            {
                AppendLog("Invalid project folder path.", Color.Red);
                return;
            }

            try
            {
                // Step 1: Properly normalize and construct the file path
                projectFolder = projectFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string projectFolderName = Path.GetFileName(projectFolder);
                string dataFolder = Path.Combine(projectFolder, $"{projectFolderName}_BEC", "data");
                string schoolFolder = Path.Combine(dataFolder, "school");
                string filePath = Path.Combine(schoolFolder, fileName);
                
                // Log the path being used for better troubleshooting
                AppendLog($"Target school file path: {filePath}", Color.Blue);

                // Step 2: Ensure directories exist
                if (!Directory.Exists(dataFolder))
                {
                    AppendLog($"Data folder not found: {dataFolder}", Color.Red);
                    MessageBox.Show($"The data folder could not be found at:\n{dataFolder}\n\nPlease ensure the project is correctly unpacked.", 
                        "Directory Not Found", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Create school folder if it doesn't exist
                if (!Directory.Exists(schoolFolder))
                {
                    AppendLog($"School folder not found: {schoolFolder}. Creating it now.", Color.Orange);
                    Directory.CreateDirectory(schoolFolder);
                }

                // Step 3: Check and load existing file content
                List<string> outputLines = new List<string>();
                
                // Read existing file content
                AppendLog($"Reading existing school file: {filePath}", Color.Blue);
                outputLines.AddRange(File.ReadAllLines(filePath));

                // Step 4: Append team units with detailed logging
                AppendLog($"Adding {tvwTeam.Nodes.Count} units to {fileName}...", Color.Blue);
                int initialLineCount = outputLines.Count;
                AppendTeamUnitBlocks(outputLines);
                
                if (outputLines.Count <= initialLineCount)
                {
                    AppendLog("Warning: No unit data was added to the output lines.", Color.Orange);
                }

                // Step 5: Write back to file with validation
                File.WriteAllLines(filePath, outputLines);
                
                // Verify file was created/updated
                if (File.Exists(filePath))
                {
                    int unitCount = tvwTeam.Nodes.Count;
                    string statusMessage = $"{unitCount} team units added to existing file {filePath}";
                    
                    AppendLog(statusMessage, Color.Green);
                }
                else
                {
                    throw new IOException($"Failed to verify the existence of the output file after writing: {filePath}");
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                string message = $"Access denied while writing to school file. Try running the application as administrator: {ex.Message}";
                AppendLog(message, Color.Red);
                MessageBox.Show(message, "Access Denied", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (IOException ex)
            {
                string message = $"I/O error while writing school file: {ex.Message}";
                AppendLog(message, Color.Red);
                MessageBox.Show(message, "File Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                string message = $"Error appending team to file: {ex.Message}";
                AppendLog(message, Color.Red);
                MessageBox.Show(message, "Unknown Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Appends CREATEUNIT blocks for all units in the team TreeView to the specified output lines.
        /// </summary>
        /// <param name="outputLines">The list of output lines to append to.</param>
        private void AppendTeamUnitBlocks(List<string> outputLines)
        {
            if (tvwTeam.Nodes.Count == 0)
            {
                AppendLog("No units found in the team to export.", Color.Orange);
                return;
            }

            // Get project path for parsing item sets if needed
            string projectPath = txtPackPath.Text.Trim();
            if (string.IsNullOrEmpty(projectPath) || !Directory.Exists(projectPath))
            {
                AppendLog("Warning: Valid project path not found, default items may not be assigned correctly.", Color.Orange);
            }

            // Parse item sets from the project if possible
            Dictionary<int, List<string>> itemSets = null;
            try
            {
                if (!string.IsNullOrEmpty(projectPath) && Directory.Exists(projectPath))
                {
                    itemSets = ParseItemSets(projectPath);
                    AppendLog($"Loaded {itemSets.Count} item sets for class-based equipment assignment.", Color.Blue);
                }
            }
            catch (Exception ex)
            {
                AppendLog($"Error loading item sets: {ex.Message}. Default items will not be assigned.", Color.Orange);
            }

            foreach (TreeNode unitNode in tvwTeam.Nodes)
            {
                // Get the Unit object from the node's Tag
                if (unitNode.Tag is Unit unit)
                {
                    // Add CREATEUNIT block
                    outputLines.Add($"CREATEUNIT: \"{unit.UnitName}\", \"{unit.Class.ClassName}\"    // Name, class");
                    outputLines.Add($"\tLEVEL: {unit.Level}");
                    outputLines.Add("\tEXPERIENCE: 0");
                    outputLines.Add("\tJOBPOINTS: 8");
                    outputLines.Add("\tCUSTOMIZE: 0, \"\"");

                    // Get the unit's stats
                    var stats = unit.BaseStats;
                    outputLines.Add("\t//            CON, PWR, ACC, DEF, INT, MOV");
                    outputLines.Add($"\tCORESTATSCOMP2: {stats.CON}, {stats.PWR}, {stats.ACC}, {stats.DEF}, {stats.INI}, {stats.MOV}");

                    // Add equipped items - first check the unit's equipped gear, then fall back to class-based defaults
                    outputLines.Add("\t//	weapon,	armor,	shield,	helmet,	accessory");

                    // First, check if the unit has any equpped gear
                    string weapon = GetItemNameByType(unit.EquippedGear, GearType.Weapon);
                    string armor = GetItemNameByType(unit.EquippedGear, GearType.Armor);
                    string shield = GetItemNameByType(unit.EquippedGear, GearType.Shield);
                    string helmet = GetItemNameByType(unit.EquippedGear, GearType.Helmet);
                    string accessory = GetItemNameByType(unit.EquippedGear, GearType.Accessory);

                    // If we're missing any equipment and have gladiator entries, try to assign class-based defaults
                    if ((string.IsNullOrEmpty(weapon) || string.IsNullOrEmpty(armor) || string.IsNullOrEmpty(shield) || 
                        string.IsNullOrEmpty(helmet) || string.IsNullOrEmpty(accessory)) && 
                        gladiatorEntries != null && gladiatorEntries.Count > 0 && itemSets != null && itemSets.Count > 0)
                    {
                        // Find a matching gladiator entry for this class
                        var matchingGladiators = gladiatorEntries.Where(g => g.Class.Equals(unit.Class.ClassName, StringComparison.OrdinalIgnoreCase)).ToList();
                        if (matchingGladiators.Count > 0)
                        {
                            // Use the first matching gladiator's item set
                            var gladiator = matchingGladiators[0];
                            var defaultItems = itemSets.GetValueOrDefault(gladiator.ItemSet, new List<string> { "", "", "", "", "" });

                            // Assign default items only for empty slots
                            if (defaultItems.Count >= 5)
                            {
                                if (string.IsNullOrEmpty(weapon)) weapon = defaultItems[0];
                                if (string.IsNullOrEmpty(armor)) armor = defaultItems[1];
                                if (string.IsNullOrEmpty(shield)) shield = defaultItems[2];
                                if (string.IsNullOrEmpty(helmet)) helmet = defaultItems[3];
                                if (string.IsNullOrEmpty(accessory)) accessory = defaultItems[4];
                                
                                AppendLog($"Applied default equipment from ItemSet {gladiator.ItemSet} for unit '{unit.UnitName}' with class '{unit.Class.ClassName}'", Color.Blue);
                            }
                        }
                    }

                    // Ensure we have a value (empty string if nothing else) for each slot
                    weapon = weapon ?? "";
                    armor = armor ?? "";
                    shield = shield ?? "";
                    helmet = helmet ?? "";
                    accessory = accessory ?? "";

                    outputLines.Add($"\tITEMSCOMP: \"{weapon}\", \"{armor}\", \"{shield}\", \"{helmet}\", \"{accessory}\"");

                    // Add learned skills (if any)
                    if (unit.LearnedSkills.Count > 0)
                    {
                        outputLines.Add("\t// basic skills");
                        foreach (var skill in unit.LearnedSkills)
                        {
                            outputLines.Add($"\tSKILL: \"{skill.InternalName}\"");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Gets the name of an item of a specific type from the equipped gear list.
        /// </summary>
        /// <param name="equippedGear">The list of equipped gear.</param>
        /// <param name="type">The type of gear to find.</param>
        /// <returns>The item name or empty string if not found.</returns>
        private string GetItemNameByType(List<Gear> equippedGear, GearType type)
        {
            var item = equippedGear.FirstOrDefault(g => g.GearType == type);
            return item?.InternalName ?? "";
        }

        /// <summary>
        /// Exports the current team to both Valens and Ursula school files.
        /// </summary>
        /// <param name="projectFolder">The root project folder path.</param>
        /// <param name="writeToValens">Flag indicating whether to write to Valens school file.</param>
        /// <param name="writeToUrsula">Flag indicating whether to write to Ursula school file.</param>
        /// <param name="appendToExisting">Flag indicating whether to append to existing files or create new ones.</param>
        #endregion


        /// <summary>
        /// Handles the Click event of btnTeamAddToFile to export the team to both school files.
        /// </summary>
        private void btnTeamAddToFile_Click(object sender, EventArgs e)
        {
            // Verify project path
            string projectPath = txtPackPath.Text.Trim();
            if (string.IsNullOrEmpty(projectPath) || !Directory.Exists(projectPath))
            {
                MessageBox.Show("Please select a valid project folder first.", "Project Not Selected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                AppendLog("Attempted to export team without selecting a valid project.", Color.Red);
                return;
            }

            // Check if team has units
            if (tvwTeam.Nodes.Count == 0)
            {
                MessageBox.Show("Your team is empty. Please add units before exporting.", "Empty Team", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                // Confirm with the user before proceeding
                DialogResult confirmResult = MessageBox.Show(
                    "This will append your team units to both Valens and Ursula school files.\n\n" +
                    "Do you want to continue?",
                    "Confirm Team Export",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (confirmResult != DialogResult.Yes)
                {
                    AppendLog("Team export canceled by user.", Color.Orange);
                    return;
                }

                // Always append to existing files
                AppendTeamUnitsToFile(projectPath, "valensimperia.tok");
                AppendTeamUnitsToFile(projectPath, "ursulanordagh.tok");

                MessageBox.Show("Team units have been successfully added to both school files!",
                    "Export Successful", MessageBoxButtons.OK, MessageBoxIcon.Information);

                AppendLog("Team units successfully added to both school files.", Color.Green);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred while exporting the team: {ex.Message}",
                    "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                AppendLog($"Team export error: {ex.Message}", Color.Red);
            }
        }
    }
}