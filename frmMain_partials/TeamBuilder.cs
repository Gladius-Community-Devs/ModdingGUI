using System.Text.RegularExpressions;

namespace ModdingGUI
{
    /// <summary>
    /// Partial class containing all Team Builder related functionalities.
    /// </summary>
    public partial class frmMain : Form
    {
        #region Data Models

        /// <summary>
        /// Represents a unit class with its properties.
        /// </summary>
        public class ClassDefinition
        {
            public string ClassName { get; set; }
            public List<string> Attributes { get; set; } = new List<string>();
            public string Affinity { get; set; } // Optional

            // Future Properties: Description, Mesh, etc.
        }

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
            public List<Equipment> EquippedItems { get; set; } = new List<Equipment>();
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
                foreach (var item in EquippedItems)
                {
                    switch (statName.ToUpper())
                    {
                        case "HP":
                            if (item.Type == EquipmentType.Helmet || item.Type == EquipmentType.Armor)
                                bonus += item.HP;
                            break;
                        case "DAM":
                            if (item.Type == EquipmentType.Weapon)
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
            public string Description { get; set; }
            public List<string> ClassesThatCanUse { get; set; } = new List<string>();
            public int DisplayID { get; set; }

            public override string ToString()
            {
                return Name;
            }
        }

        /// <summary>
        /// Represents an equipment item with its properties.
        /// </summary>
        public enum EquipmentType
        {
            Weapon,
            Armor,
            Helmet,
            Shield,
            Accessory
            // Add more types as needed
        }

        public class Equipment
        {
            public string Name { get; set; }
            public string Description { get; set; }
            public EquipmentType Type { get; set; }
            public List<string> Effects { get; set; } = new List<string>(); // Optional
            public int? Durability { get; set; } // Applicable only to shields

            // Stat bonuses
            public int HP { get; set; }
            public int Damage { get; set; }
            public int Accuracy { get; set; }
            public int Defense { get; set; }
            public int Initiative { get; set; }

            public override string ToString()
            {
                return Name;
            }
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
            /// <summary>
            /// Parses the classdefs.tok file and returns a list of ClassDefinition objects.
            /// </summary>
            /// <param name="filePath">Path to the classdefs.tok file.</param>
            /// <returns>List of ClassDefinition objects.</returns>
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

                    // Check for the start of a new class definition
                    if (trimmedLine.StartsWith("CREATECLASS:", StringComparison.OrdinalIgnoreCase))
                    {
                        // If there's an existing class being parsed, add it to the list
                        if (currentClass != null)
                        {
                            classDefinitions.Add(currentClass);
                        }

                        // Initialize a new ClassDefinition object
                        currentClass = new ClassDefinition();

                        // Extract the class name
                        var match = Regex.Match(trimmedLine, @"CREATECLASS:\s*(\w+)", RegexOptions.IgnoreCase);
                        if (match.Success)
                        {
                            currentClass.ClassName = match.Groups[1].Value.Trim();
                        }
                        else
                        {
                            // If class name is not found, skip this class definition
                            currentClass = null;
                        }

                        continue;
                    }

                    // If currently parsing a class, extract relevant fields
                    if (currentClass != null)
                    {
                        // Extract ATTRIBUTE lines
                        if (trimmedLine.StartsWith("ATTRIBUTE:", StringComparison.OrdinalIgnoreCase))
                        {
                            var attrMatch = Regex.Match(trimmedLine, @"ATTRIBUTE:\s*""([^""]+)""", RegexOptions.IgnoreCase);
                            if (attrMatch.Success)
                            {
                                currentClass.Attributes.Add(attrMatch.Groups[1].Value.Trim());
                            }
                        }

                        // Extract AFFINITY line (assuming only one per class)
                        if (trimmedLine.StartsWith("AFFINITY:", StringComparison.OrdinalIgnoreCase))
                        {
                            var affMatch = Regex.Match(trimmedLine, @"AFFINITY:\s*""([^""]+)""", RegexOptions.IgnoreCase);
                            if (affMatch.Success)
                            {
                                currentClass.Affinity = affMatch.Groups[1].Value.Trim();
                            }
                        }

                        // Future Parsing: Description, Mesh, etc.
                    }
                }

                // After the loop, add the last parsed class if it exists
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
                AppendLog("Attempted to access Team Builder without selecting a valid project.", Color.Red, true);

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
                AppendLog($"classdefs.tok not found at '{classDefsPath}'.", Color.Red, true);

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
                AppendLog($"gladiators.txt not found at '{gladiatorsPath}'.", Color.Red, true);

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
                AppendLog($"statsets.txt not found at '{statSetsPath}'.", Color.Red, true);

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
                    AppendLog("No classes found in classdefs.tok.", Color.Blue, true);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading classes, gladiators, or statsets: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                AppendLog($"Error loading classes, gladiators, or statsets: {ex.Message}", Color.Red, true);

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
                // Clear the preview if no class is selected
                ClearPreview();
                return;
            }

            var selectedClass = classDefinitions.FirstOrDefault(c => c.ClassName.Equals(selectedClassName, StringComparison.OrdinalIgnoreCase));

            if (selectedClass == null)
            {
                // Clear the preview if class not found
                ClearPreview();
                return;
            }

            // Attempt to parse the level input
            if (!int.TryParse(levelText, out unitLevel) || unitLevel < 1 || unitLevel > 30)
            {
                // Invalid level input; show placeholders or an error message
                txtPreviewStats.Text = "Invalid level input. Please enter a level between 1 and 30.";
                return;
            }

            // Determine the most used statset for the selected class
            int mostUsedStatSet = GetMostUsedStatSet(selectedClassName);
            if (mostUsedStatSet == -1)
            {
                // No statset found for the selected class
                txtPreviewStats.Text = $"No statset found for class '{selectedClassName}'.";
                return;
            }

            // Retrieve stats for the selected statset and level
            if (!statSets.ContainsKey(mostUsedStatSet) || !statSets[mostUsedStatSet].ContainsKey(unitLevel))
            {
                // Stats not found for the given statset and level
                txtPreviewStats.Text = $"Stats not found for statset '{mostUsedStatSet}' at level '{unitLevel}'.";
                return;
            }

            var statsList = statSets[mostUsedStatSet][unitLevel];
            if (statsList.Count < 5)
            {
                // Insufficient stats data
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

            // Display in txtPreviewStats
            txtPreviewStats.Text = statsText;
            txtPreviewUnitName.Text = unitName;
        }

        /// <summary>
        /// Clears the grpPreview controls.
        /// </summary>
        private void ClearPreview()
        {
            txtPreviewUnitName.Text = string.Empty;
            txtPreviewAttributes.Text = string.Empty;
            txtPreviewAffinity.Text = string.Empty;
            txtPreviewStats.Text = string.Empty; // Clear stats as well
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
                AppendLog("Attempted to add a unit with an invalid class.", Color.Red, true);
                return;
            }

            // Check for Duplicate Unit Names
            if (tvwTeam.Nodes.Cast<TreeNode>().Any(n => n.Text.Equals(unitName, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show("A unit with this name already exists.", "Duplicate Unit", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtTeamUnitName.Focus();
                AppendLog($"Attempted to add duplicate unit '{unitName}'.", Color.Orange, true);
                return;
            }

            // Determine the most used statset for the selected class
            int mostUsedStatSet = GetMostUsedStatSet(selectedClassName);
            if (mostUsedStatSet == -1)
            {
                MessageBox.Show($"No statset found for class '{selectedClassName}'. Cannot create unit.", "StatSet Not Found", MessageBoxButtons.OK, MessageBoxIcon.Error);
                AppendLog($"No statset found for class '{selectedClassName}'.", Color.Red, true);
                return;
            }

            // Retrieve stats for the selected statset and level
            if (!statSets.ContainsKey(mostUsedStatSet) || !statSets[mostUsedStatSet].ContainsKey(unitLevel))
            {
                MessageBox.Show($"Stats not found for statset '{mostUsedStatSet}' at level '{unitLevel}'.", "Stats Not Found", MessageBoxButtons.OK, MessageBoxIcon.Error);
                AppendLog($"Stats not found for statset '{mostUsedStatSet}' at level '{unitLevel}'.", Color.Red, true);
                return;
            }

            var statsList = statSets[mostUsedStatSet][unitLevel];
            if (statsList.Count < 5)
            {
                MessageBox.Show($"Insufficient stats data for statset '{mostUsedStatSet}' at level '{unitLevel}'.", "Insufficient Stats", MessageBoxButtons.OK, MessageBoxIcon.Error);
                AppendLog($"Insufficient stats data for statset '{mostUsedStatSet}' at level '{unitLevel}'.", Color.Red, true);
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
                // EquippedItems and LearnedSkills are initialized as empty lists
            };

            teamUnits.Add(unit);
            // Add the unit to the team TreeView
            AddUnitToTreeView(unit);

            // Clear input fields after adding
            txtTeamUnitName.Clear();
            ddlTeamClasses.SelectedIndex = 0;

            // Log the addition
            AppendLog($"Added unit '{unitName}' with class '{selectedClassName}' at level {unitLevel}.", Color.Green, false);
            int maxUnits = rbnTeamPVP.Checked ? 20 : rbnTeamCampaign.Checked ? 14 : 20;
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
            TreeNode unitNode = new TreeNode(unit.UnitName)
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
            foreach (var equipment in unit.EquippedItems)
            {
                TreeNode equipmentItemNode = new TreeNode(equipment.Name)
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
                TreeNode skillNode = new TreeNode(skill.Name)
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
            AppendLog("Selected node: " + e.Node.Text, Color.Blue, false);
            // Update other UI elements if necessary
            UpdateUnitCountAndButtons();
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
                AppendLog($"Failed to remove unit '{selectedUnit.UnitName}' from the internal list.", Color.Red, true);
                return;
            }

            // Remove the unit node from the TreeView
            tvwTeam.Nodes.Remove(tvwTeam.SelectedNode);

            // Clear the selection to prevent lingering references
            tvwTeam.SelectedNode = null;

            // Update UI Elements (e.g., Unit Count, Button States)
            UpdateUnitCountAndButtons();

            // Log the removal
            AppendLog($"Removed unit '{selectedUnit.UnitName}'.", Color.Blue, true);
        }


        /// <summary>
        /// Updates the unit count display and the state of action buttons based on the current teamUnits count.
        /// </summary>
        private void UpdateUnitCountAndButtons()
        {
            // Determine the maximum number of units based on the selected mode
            int maxUnits = rbnTeamPVP.Checked ? 20 : rbnTeamCampaign.Checked ? 14 : 20;

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
    }
}
