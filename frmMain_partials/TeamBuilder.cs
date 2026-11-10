using ModdingGUI.Core.Api;
using ModdingGUI.Core.Tok.Values;
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

   /// <summary>
   /// Job points written to the roster: what the class's LEVELUPJPGIVEN awards by this level, minus the cost of the
   /// granted skills, unless the designer overrode it.
   /// </summary>
   public int JobPoints { get; set; }

   /// <summary>True when <see cref="JobPoints"/> came from the designer rather than the rule.</summary>
   public bool JobPointsOverridden { get; set; }

   /// <summary>Roster file this unit was loaded from, when it already exists in the project.</summary>
   public string? LoadedFromRoster { get; set; }

   /// <summary>Name the unit had in that roster when it was loaded.</summary>
   public string? LoadedName { get; set; }

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
            /// <summary>
            /// Reads class definitions through <see cref="IClassesApi"/> — the shell never
            /// parses classdefs.tok itself, so Team Builder sees exactly what the editor and
            /// the MCP server see (comments and unknown keys included).
            /// </summary>
            /// <param name="projectRoot">The project folder or its <c>&lt;name&gt;_BEC</c> folder.</param>
            public List<ClassDefinition> ParseClassDefs(string projectRoot)
            {
                var api = new ClassDefsApi();
                var classDefinitions = new List<ClassDefinition>();

                foreach (var dto in api.GetAll(projectRoot))
                {
                    var currentClass = new ClassDefinition { ClassName = dto.Name };

                    foreach (string attribute in dto.Values("ATTRIBUTE"))
                    {
                        string names = Unquote(attribute);
                        if (names.Length > 0) currentClass.Attributes.Add(names);
                    }

                    string? affinity = dto.Value("AFFINITY");
                    if (affinity is not null) currentClass.Affinity = Unquote(affinity);

                    foreach (string itemCat in dto.Values("ITEMCAT"))
                    {
                        var parts = SplitArguments(itemCat);
                        if (parts.Count < 3) continue;

                        currentClass.AllowedGears.Add(new AllowedGear
                        {
                            GearType = parts[0],
                            SubType = parts[1],
                            Style = parts[2],
                        });
                    }

                    classDefinitions.Add(currentClass);
                }

                return classDefinitions;
            }

            /// <summary>Strip surrounding quotes and whitespace from a raw value.</summary>
            private static string Unquote(string raw) => raw.Trim().Trim('"');

            /// <summary>Split a comma-separated value into argument texts (drops empty slots).</summary>
            private static List<string> SplitArguments(string raw) =>
                TokValueReader.Parse(raw).Tokens
                    .Where(t => t.Kind != TokValueKind.Empty)
                    .Select(t => Unquote(t.Text))
                    .ToList();
        }


        /// <summary>
        /// Reads <c>gladiators.txt</c> through <see cref="IGladiatorsApi"/>.
        /// </summary>
        /// <remarks>
        /// The shell used to walk the file itself, which duplicated the Core parser and re-derived the nine field
        /// labels. Phase 7 puts Team Builder on the APIs like every other editor, so a record means the same thing
        /// here as it does in the randomizer and (later) the MCP server.
        /// </remarks>
        public class GladiatorParser
        {
            /// <summary>Every gladiator record in the project, in file order.</summary>
            /// <param name="projectRoot">The project folder or its <c>_BEC</c> folder.</param>
            public List<GladiatorEntry> ParseGladiators(string projectRoot)
            {
                return new GladiatorsApi().GetAll(projectRoot)
                    .Select(dto => new GladiatorEntry
                    {
                        Name = dto.Name,
                        Class = dto.Class,
                        Affinity = dto.Affinity,
                        Outfit = dto.Outfit,
                        TintSet = dto.TintSet,
                        SkillSet = dto.SkillSet,
                        StatSet = dto.StatSet,
                        ItemSet = dto.ItemSet,
                        School = dto.School,
                    })
                    .ToList();
            }
        }

        /// <summary>
        /// Reads <c>statsets.txt</c> through <see cref="IStatSetsApi"/>.
        /// </summary>
        /// <remarks>
        /// The shape stays the one the preview code wants — set index → level → five stats — but the parse is Core's,
        /// including its handling of the section headers and the level rows.
        /// </remarks>
        public class StatSetsParser
        {
            /// <summary>Every stat set, keyed by set index and then by level.</summary>
            /// <param name="projectRoot">The project folder or its <c>_BEC</c> folder.</param>
            public Dictionary<int, Dictionary<int, List<int>>> ParseStatSets(string projectRoot)
            {
                var sets = new Dictionary<int, Dictionary<int, List<int>>>();
                foreach (StatSetDto dto in new StatSetsApi().GetAll(projectRoot))
                {
                    var rows = new Dictionary<int, List<int>>();
                    foreach (StatSetRow row in dto.Rows)
                    {
                        rows[row.Level] = new List<int> { row.Con, row.Pow, row.Acc, row.Def, row.IniMov };
                    }

                    sets[dto.Index] = rows;
                }

                return sets;
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
            try
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

                // Step 2: resolve through the Core resolver, so a project whose BEC folder is not literally
                // "<name>_BEC" (the REALDATA fixtures, for instance) still opens.
                string becFolder = ModdingGUI.Core.ProjectRoots.ResolveBecFolder(projectPath);
                var required = new (string Label, string Path)[]
                {
                    ("data/config/classdefs.tok", Path.Combine(becFolder, "data", "config", "classdefs.tok")),
                    ("data/units/gladiators.txt", Path.Combine(becFolder, "data", "units", "gladiators.txt")),
                    ("data/units/statsets.txt", Path.Combine(becFolder, "data", "units", "statsets.txt")),
                };

                string[] missing = required
                    .Where(file => !File.Exists(file.Path))
                    .Select(file => file.Label)
                    .ToArray();

                if (missing.Length > 0)
                {
                    string message = $"{string.Join(", ", missing)} not found under '{becFolder}'. " +
                                     "Please ensure the project is correctly unpacked.";
                    MessageBox.Show(message, "File Not Found", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    AppendLog(message, Color.Red);

                    // Switch back to the packing tab; the builder cannot stand up without these files.
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

                // Proceed with loading class definitions, gladiators, and statsets — all through the Core APIs, so the
                // shell parses none of these files itself (PLAN.md Phase 7).
                try
                {
                    var classParser = new ClassParser();
                    classDefinitions = await Task.Run(() => classParser.ParseClassDefs(projectPath));

                    var gladiatorParser = new GladiatorParser();
                    gladiatorEntries = await Task.Run(() => gladiatorParser.ParseGladiators(projectPath));

                    var statSetsParser = new StatSetsParser();
                    statSets = await Task.Run(() => statSetsParser.ParseStatSets(projectPath));

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
            catch (Exception ex)
            {
                AppendLog($"Loading classes failed: {ex.Message}", ErrorColor);
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

            // Validate Unit Name. Quotes and line breaks would corrupt the CREATEUNIT line; spaces and the
            // display separator '_' are legitimate (see InputRules).
            var nameProblems = ModdingGUI.Core.Units.InputRules.ValidateUnitName(unitName);
            if (nameProblems.Count > 0)
            {
                MessageBox.Show(string.Join(Environment.NewLine, nameProblems), "Unit Name",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
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

            // Validate Level Input: 1..30, the range every stat set provides rows for.
            if (!int.TryParse(levelText, out unitLevel) || !ModdingGUI.Core.Units.InputRules.IsValidLevel(unitLevel))
            {
                MessageBox.Show(ModdingGUI.Core.Units.InputRules.ExplainLevel(
                        int.TryParse(levelText, out int parsedLevel) ? parsedLevel : 0) ??
                        "Please enter a unit level between 1 and 30.",
                    "Invalid Level", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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

            // Job points: the class's award by this level (minus granted skills, none yet), unless overridden.
            var budget = CurrentJobPointBudget();
            unit.JobPoints = budget?.Remaining ?? 0;
            unit.JobPointsOverridden = false;

            if (_chbTeamOverrideJp is { Checked: true })
            {
                if (int.TryParse(_txtTeamJobPoints?.Text.Trim(), out int manual) && manual >= 0)
                {
                    unit.JobPoints = manual;
                    unit.JobPointsOverridden = true;
                }
                else
                {
                    MessageBox.Show("Override JP is on, but the value is not a number.", "Job Points",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            if (budget is { OverBudget: true })
            {
                AppendLog($"'{unitName}' spends more JP on skills than level {unitLevel} awards.", Color.Orange);
            }

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
                    _selectedUnit = unit;
                    RefreshLoadoutEditors();
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
        private CheckBox? _chbTeamOverrideJp;
        private TextBox? _txtTeamJobPoints;

        private void InitializeTeamBuilderUI()
        {
            BuildJobPointControls();

            // Set Campaign mode as default
            rbnTeamCampaign.Checked = true;
            
            // Disable PVP radio button since it's not ready yet
            rbnTeamPVP.Enabled = false;
            
            // Set team level text and enable/disable based on mode
            txtTeamLevel.Text = "1";
            
            // Hide PVP-only controls
            UpdateEquipmentRulesVisibility(false);
            
            // The gear and skill pages used to be placeholders that were removed. They are editors now.
            tabTeamGearSelection.Text = "Gear";
            tabTeamSkillSelection.Text = "Skills";
            BuildLoadoutEditors();
        }

        /// <summary>
        /// Job points are derived from the class's LEVELUPJPGIVEN and the unit's level, so a level-N unit is worth
        /// what one that levelled to N would be. The override exists for deliberate departures.
        /// </summary>
        private void BuildJobPointControls()
        {
            Control parent = txtTeamLevel.Parent ?? tabTeamBuilder;

            _chbTeamOverrideJp = new CheckBox
            {
                Text = "Override JP",
                Location = new Point(txtTeamLevel.Left, txtTeamLevel.Bottom + 6),
                AutoSize = true,
            };

            _txtTeamJobPoints = new TextBox
            {
                Location = new Point(txtTeamLevel.Left, txtTeamLevel.Bottom + 30),
                Size = new Size(txtTeamLevel.Width, txtTeamLevel.Height),
                ReadOnly = true,
            };

            parent.Controls.Add(_chbTeamOverrideJp);
            parent.Controls.Add(_txtTeamJobPoints);

            // Which rosters to write to. Both campaign schools ship with the same starting team, so both is the
            // sensible default, but a mod may only want one.
            chbTeamRosterValens = new CheckBox
            {
                Text = "Valens school",
                Location = new Point(txtTeamLevel.Left, txtTeamLevel.Bottom + 60),
                AutoSize = true,
                Checked = true,
            };

            chbTeamRosterUrsula = new CheckBox
            {
                Text = "Ursula school",
                Location = new Point(txtTeamLevel.Left, txtTeamLevel.Bottom + 84),
                AutoSize = true,
                Checked = true,
            };

            parent.Controls.Add(chbTeamRosterValens);
            parent.Controls.Add(chbTeamRosterUrsula);

            _chbTeamOverrideJp.CheckedChanged += (_, _) =>
            {
                _txtTeamJobPoints.ReadOnly = !_chbTeamOverrideJp.Checked;
                UpdateJobPointPreview();
            };

            txtTeamLevel.TextChanged += (_, _) => UpdateJobPointPreview();
            ddlTeamClasses.SelectedIndexChanged += (_, _) => UpdateJobPointPreview();

            UpdateJobPointPreview();
        }

        /// <summary>The budget for the class and level currently selected, or null when nothing is selected.</summary>
        private ModdingGUI.Core.Units.JobPointBudget? CurrentJobPointBudget()
        {
            string project = txtPackPath.Text.Trim();
            string? className = ddlTeamClasses.SelectedItem?.ToString();

            if (string.IsNullOrEmpty(project) || !Directory.Exists(project) || string.IsNullOrWhiteSpace(className))
            {
                return null;
            }

            int level = int.TryParse(txtTeamLevel.Text.Trim(), out int parsed) ? Math.Clamp(parsed, 1, 30) : 1;
            var classDef = new ModdingGUI.Core.Api.ClassDefsApi().Get(project, className!);

            return ModdingGUI.Core.Units.JobPoints.Compute(classDef, level);
        }

        /// <summary>Show the derived JP, or leave the designer's value alone while the override is on.</summary>
        private void UpdateJobPointPreview()
        {
            if (_txtTeamJobPoints is null || _chbTeamOverrideJp is null) return;
            if (_chbTeamOverrideJp.Checked) return;

            var budget = CurrentJobPointBudget();
            _txtTeamJobPoints.Text = budget is null ? string.Empty : budget.Remaining.ToString();
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
        /// <summary>
        /// Build the roster block for one unit: stats, gear and skills as the school file records them.
        /// </summary>
        private ModdingGUI.Core.Api.SchoolUnitDto BuildSchoolUnit(Unit unit, Dictionary<int, ModdingGUI.Core.Api.ItemSetDto> itemSets,
            Dictionary<string, string> itemTypes, Dictionary<string, ModdingGUI.Core.Api.ItemDto> itemsByName)
        {
            var fields = new List<ModdingGUI.Core.Api.SchoolFieldDto>
            {
                new("LEVEL", unit.Level.ToString()),
                new("EXPERIENCE", "0"),
                new("JOBPOINTS", unit.JobPoints.ToString()),
                new("CUSTOMIZE", "0, \"\""),
            };

            var stats = unit.BaseStats;
            fields.Add(new ModdingGUI.Core.Api.SchoolFieldDto("CORESTATSCOMP2",
                $"{stats.CON}, {stats.PWR}, {stats.ACC}, {stats.DEF}, {stats.INI}, {stats.MOV}"));

            // Gear comes from the class's own gladiator records, chosen by items.tok Type at the unit's level.
            string weapon = GetItemNameByType(unit.EquippedGear, GearType.Weapon);
            string armor = GetItemNameByType(unit.EquippedGear, GearType.Armor);
            string shield = GetItemNameByType(unit.EquippedGear, GearType.Shield);
            string helmet = GetItemNameByType(unit.EquippedGear, GearType.Helmet);
            string accessory = GetItemNameByType(unit.EquippedGear, GearType.Accessory);

            if ((string.IsNullOrEmpty(weapon) || string.IsNullOrEmpty(armor) || string.IsNullOrEmpty(shield)
                 || string.IsNullOrEmpty(helmet) || string.IsNullOrEmpty(accessory))
                && itemSets is not null && itemTypes is not null && gladiatorEntries is { Count: > 0 })
            {
                var matching = gladiatorEntries
                    .Where(g => g.Class.Equals(unit.Class.ClassName, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (matching.Count > 0 && itemSets.TryGetValue(matching[0].ItemSet, out var sourceSet))
                {
                    var gearWarnings = new List<string>();
                    var gear = ModdingGUI.Core.Units.ItemSetGear.ForLevel(
                        sourceSet.Rows, unit.Level, itemTypes, gearWarnings);

                    foreach (string warning in gearWarnings)
                    {
                        AppendLog($"Item set {matching[0].ItemSet}: {warning}", Color.Orange);
                    }

                    if (string.IsNullOrEmpty(weapon)) weapon = gear.Weapon ?? string.Empty;
                    if (string.IsNullOrEmpty(armor)) armor = gear.Armor ?? string.Empty;
                    if (string.IsNullOrEmpty(shield)) shield = gear.Shield ?? string.Empty;
                    if (string.IsNullOrEmpty(helmet)) helmet = gear.Helmet ?? string.Empty;
                    if (string.IsNullOrEmpty(accessory)) accessory = gear.Accessory ?? string.Empty;
                }
            }

            fields.Add(new ModdingGUI.Core.Api.SchoolFieldDto("ITEMSCOMP",
                $"\"{weapon}\", \"{armor}\", \"{shield}\", \"{helmet}\", \"{accessory}\""));

            if (string.IsNullOrEmpty(weapon))
            {
                AppendLog($"Warning: '{unit.UnitName}' has no weapon; the unit will not be able to attack.", Color.Red);
            }

            // Category legality: the engine would refuse these by hand, even though ITEMSCOMP itself is not checked.
            var loadout = new ModdingGUI.Core.Units.GearLoadout(weapon, armor, shield, helmet, accessory);
            if (itemsByName is { Count: > 0 } && projectPathForValidation is { Length: > 0 })
            {
                var classDef = new ModdingGUI.Core.Api.ClassDefsApi().Get(projectPathForValidation, unit.Class.ClassName);
                foreach (string problem in ModdingGUI.Core.Units.ItemCategoryRules.Validate(classDef, loadout, itemsByName))
                {
                    AppendLog($"Category warning for '{unit.UnitName}': {problem}", Color.Orange);
                }
            }

            foreach (var skill in unit.LearnedSkills)
            {
                fields.Add(new ModdingGUI.Core.Api.SchoolFieldDto("SKILL", $"\"{skill.InternalName}\""));
            }

            return new ModdingGUI.Core.Api.SchoolUnitDto
            {
                Name = unit.UnitName,
                ClassName = unit.Class.ClassName,
                Fields = fields,
            };
        }

        /// <summary>Set while building a unit, so the category check can read the class block.</summary>
        private string? projectPathForValidation;

        /// <summary>
        /// Write the designed team into the chosen school rosters through <c>ISchoolsApi</c>: the lock, the
        /// <c>.mod-history</c> backup and round-trip fidelity come with it, and duplicate names are refused rather
        /// than appended twice.
        /// </summary>
        private void btnTeamAddToFile_Click(object sender, EventArgs e)
        {
            string projectPath = txtPackPath.Text.Trim();
            if (string.IsNullOrEmpty(projectPath) || !Directory.Exists(projectPath))
            {
                MessageBox.Show("Please select a valid project folder first.", "Project Not Selected",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                AppendLog("Attempted to export team without selecting a valid project.", Color.Red);
                return;
            }

            if (tvwTeam.Nodes.Count == 0)
            {
                MessageBox.Show("Your team is empty. Please add units before exporting.", "Empty Team",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var rosters = new List<string>();
            if (chbTeamRosterValens is null || chbTeamRosterValens.Checked) rosters.Add("valensimperia.tok");
            if (chbTeamRosterUrsula is null || chbTeamRosterUrsula.Checked) rosters.Add("ursulanordagh.tok");

            if (rosters.Count == 0)
            {
                MessageBox.Show("Choose at least one school roster to write to.", "No Roster Selected",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                projectPathForValidation = projectPath;

                var itemSets = new ModdingGUI.Core.Api.ItemSetsApi().GetAll(projectPath)
                    .ToDictionary(set => set.Index);
                var itemTypes = new ModdingGUI.Core.Api.ItemsApi().GetAll(projectPath)
                    .Where(item => !string.IsNullOrWhiteSpace(item.Type))
                    .ToDictionary(item => item.Name, item => item.Type!, StringComparer.OrdinalIgnoreCase);
                var itemsByName = new ModdingGUI.Core.Api.ItemsApi().GetAll(projectPath)
                    .ToDictionary(item => item.Name, item => item, StringComparer.OrdinalIgnoreCase);

                // The DTO is what gets written; the Unit carries where it came from, so exports can update an
                // existing roster entry rather than appending a second copy.
                var built = new List<(ModdingGUI.Core.Api.SchoolUnitDto Dto, Unit Source)>();
                foreach (TreeNode node in tvwTeam.Nodes)
                {
                    if (node.Tag is Unit unit)
                    {
                        built.Add((BuildSchoolUnit(unit, itemSets, itemTypes, itemsByName), unit));
                    }
                }

                var units = built.Select(entry => entry.Dto).ToList();

                var schools = new ModdingGUI.Core.Api.SchoolsApi();
                int written = 0;
                var skipped = new List<string>();

                foreach (string roster in rosters)
                {
                    string relative = "data/school/" + roster;

                    var existing = schools.Get(projectPath, relative)?.Units
                        .Select(u => u.Name)
                        .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    foreach (var unit in units.Where(unit => existing.Contains(unit.Name)))
                    {
                        skipped.Add($"{unit.Name} (already in {roster})");
                    }

                    // Units loaded from this roster are edited in place rather than appended again.
                    int updated = 0;
                    foreach (var (unit, _) in built.Where(entry =>
                        string.Equals(entry.Source.LoadedFromRoster, roster, StringComparison.OrdinalIgnoreCase)))
                    {
                        foreach (var field in unit.Fields)
                        {
                            if (field.Key.Equals("SKILL", StringComparison.OrdinalIgnoreCase)) continue;
                            schools.SetUnitField(projectPath, relative, unit.Name, field.Key, field.RawValue);
                        }

                        schools.SetUnitSkills(projectPath, relative, unit.Name,
                            unit.Fields.Where(f => f.Key.Equals("SKILL", StringComparison.OrdinalIgnoreCase))
                                .Select(f => f.RawValue).Select(v => v.Trim().Trim('"')).ToArray());
                        updated++;
                    }

                    var fresh = built
                        .Where(entry => !string.Equals(entry.Source.LoadedFromRoster, roster, StringComparison.OrdinalIgnoreCase))
                        .Select(entry => entry.Dto)
                        .ToList();

                    // Loaded units that are no longer in the tree were deleted by the designer: remove their blocks.
                    int removedUnits = 0;
                    if (_loadedRosterUnits.TryGetValue(roster, out var loadedNames))
                    {
                        var present = units.Select(u => u.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
                        foreach (string gone in loadedNames.Where(name => !present.Contains(name)))
                        {
                            if (schools.RemoveUnit(projectPath, relative, gone)) removedUnits++;
                        }
                    }

                    written += schools.AddUnits(projectPath, relative, fresh);
                    AppendLog($"'{roster}': +{fresh.Count} added, {updated} updated, {removedUnits} removed.", Color.Blue);
                }

                string summary = $"Added {written} unit(s) across {rosters.Count} roster(s).";
                if (skipped.Count > 0)
                {
                    summary += $"\n\nSkipped {skipped.Count} duplicate(s):\n" + string.Join("\n", skipped.Distinct().Take(10));
                }

                AppendLog(summary, written > 0 ? Color.Green : Color.Orange);
                MessageBox.Show(summary, "Team Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred while exporting the team: {ex.Message}", "Export Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                AppendLog($"Team export error: {ex.Message}", Color.Red);
            }
            finally
            {
                projectPathForValidation = null;
            }
        }

        /// <summary>The name of the equipped item of a given kind, or an empty string.</summary>
        private static string GetItemNameByType(List<Gear> equippedGear, GearType type)
        {
            var item = equippedGear.FirstOrDefault(gear => gear.GearType == type);
            return item?.InternalName ?? string.Empty;
        }

        private CheckBox? chbTeamRosterValens;
        private CheckBox? chbTeamRosterUrsula;
        #endregion
        #region Loadout editors (gear, skills, sets, roster)

        private Unit? _selectedUnit;

        // ---- Gear page ----
        private ListBox _lstLegalItems = new();
        private TextBox _txtGearSearch = new();
        private ComboBox _cmbGearType = new();
        private Label _lblGearCount = new();
        private TextBox _txtItemDetails = new();
        private Label _lblSelectedUnit = new();
        private TextBox _txtUnitLevel = new();
        private readonly Dictionary<GearType, Label> _slotValues = new();

        /// <summary>Every item the selected unit's class may use, before the search/slot filter.</summary>
        private List<ItemChoice> _classItems = new();

        // ---- Skills page ----
        private ListBox _lstGrantedSkills = new();
        private ListBox _lstLearnableSkills = new();
        private TextBox _txtSkillDetails = new();
        private Label _lblLoadoutJp = new();

        // ---- Sets / roster ----
        private ComboBox _cmbStatSet = new();
        private ComboBox _cmbItemSet = new();
        private ComboBox _cmbRoster = new();

        /// <summary>Roster name to the unit names that were loaded from it, so removals can be written back.</summary>
        private readonly Dictionary<string, HashSet<string>> _loadedRosterUnits =
            new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Fill the Gear and Skills pages. Gear is offered only for items the class may actually use (ITEMCAT), and
        /// skills only for those the class may learn (SKILLUSECLASS); see dol/equipment_rules.json and
        /// dol/skill_rules.json.
        /// </summary>
        private bool _loadoutBuilt;

        private void BuildLoadoutEditors()
        {
            // InitializeTeamBuilderUI runs on every project selection; the controls must only be created once.
            if (_loadoutBuilt)
            {
                RefreshRosterList();
                RefreshLoadoutEditors();
                return;
            }

            _loadoutBuilt = true;

            tabTeamGearSelection.Controls.Clear();
            tabTeamGearSelection.Controls.Add(BuildGearPage());

            tabTeamSkillSelection.Controls.Clear();
            tabTeamSkillSelection.Controls.Add(BuildSkillsPage());

            // ---- Roster loader, on the rules page ----
            Control parent = txtTeamLevel.Parent ?? tabTeamBuilder;
            var rosterRow = new FlowLayoutPanel
            {
                Name = "pnlRosterPicker",
                Location = new Point(txtTeamLevel.Left, txtTeamLevel.Bottom + 112),
                Size = new Size(400, 34),
            };
            rosterRow.Controls.Add(new Label
            {
                Text = "Roster", AutoSize = true, Margin = new Padding(0, 8, 6, 0),
            });
            _cmbRoster = new ComboBox { Name = "cmbRoster", Width = 200, DropDownStyle = ComboBoxStyle.DropDownList };
            var loadRoster = new Button { Name = "btnLoadRoster", Text = "Load roster", Width = 110, Height = 26 };
            loadRoster.Click += (_, _) => LoadSelectedRoster();
            rosterRow.Controls.Add(_cmbRoster);
            rosterRow.Controls.Add(loadRoster);
            parent.Controls.Add(rosterRow);

            RefreshRosterList();
            RefreshLoadoutEditors();
        }

        /// <summary>
        /// The gear page: browse legal items on the left (with a filter), the unit's five slots and the selected
        /// item's numbers on the right.
        /// </summary>
        private Control BuildGearPage()
        {
            var root = new TableLayoutPanel
            {
                Name = "pnlGearPage",
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(8),
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));

            // Filter row: search text, slot, and how much of the list is showing.
            var filter = new FlowLayoutPanel
            {
                Name = "pnlGearFilter",
                Dock = DockStyle.Fill,
                WrapContents = false,
                Margin = new Padding(0),
            };
            filter.Controls.Add(SmallLabel("Search"));
            _txtGearSearch = new TextBox
            {
                Name = "txtGearSearch",
                Width = 230,
                PlaceholderText = "name, subtype or style",
                Margin = new Padding(0, 4, 14, 0),
            };
            _txtGearSearch.TextChanged += (_, _) => RefreshGearList();
            filter.Controls.Add(_txtGearSearch);

            filter.Controls.Add(SmallLabel("Slot"));
            _cmbGearType = new ComboBox
            {
                Name = "cmbGearType",
                Width = 130,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Margin = new Padding(0, 3, 14, 0),
            };
            _cmbGearType.Items.AddRange(new object[] { "All slots", "Weapon", "Armor", "Shield", "Helmet", "Accessory" });
            _cmbGearType.SelectedIndex = 0;
            _cmbGearType.SelectedIndexChanged += (_, _) => RefreshGearList();
            filter.Controls.Add(_cmbGearType);

            _lblGearCount = new Label
            {
                Name = "lblGearCount",
                AutoSize = true,
                Margin = new Padding(0, 8, 0, 0),
                ForeColor = SystemColors.GrayText,
            };
            filter.Controls.Add(_lblGearCount);

            var content = new TableLayoutPanel
            {
                Name = "pnlGearContent",
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0, 6, 0, 0),
            };
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52F));
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48F));

            _lstLegalItems = new ListBox
            {
                Name = "lstLegalItems",
                Dock = DockStyle.Fill,
                IntegralHeight = false,
                Margin = new Padding(0, 0, 10, 0),
            };
            _lstLegalItems.SelectedIndexChanged += (_, _) => ShowSelectedItemDetails();
            _lstLegalItems.DoubleClick += (_, _) => AssignSelectedItem();
            content.Controls.Add(_lstLegalItems, 0, 0);
            content.Controls.Add(BuildLoadoutPanel(), 1, 0);

            var buttons = new FlowLayoutPanel
            {
                Name = "pnlGearButtons",
                Dock = DockStyle.Fill,
                WrapContents = false,
                Margin = new Padding(0, 6, 0, 0),
            };
            buttons.Controls.Add(ActionButton("btnAssignGear", "Assign to slot", 120, (_, _) => AssignSelectedItem()));
            buttons.Controls.Add(ActionButton("btnClearGear", "Clear all gear", 110, (_, _) =>
            {
                if (_selectedUnit is null) return;
                _selectedUnit.EquippedGear.Clear();
                RefreshLoadoutEditors();
                DisplayUnitStats(_selectedUnit);
            }));
            buttons.Controls.Add(ActionButton("btnGearFromSet", "From item set", 120, (_, _) => FillGearFromItemSet()));

            root.Controls.Add(filter, 0, 0);
            root.Controls.Add(content, 0, 1);
            root.Controls.Add(buttons, 0, 2);
            return root;
        }

        /// <summary>The right-hand column of the gear page: who is selected, the five slots, item details, sets.</summary>
        private Control BuildLoadoutPanel()
        {
            var panel = new TableLayoutPanel
            {
                Name = "pnlLoadout",
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Margin = new Padding(0),
            };
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 176F));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 88F));

            var unitRow = new TableLayoutPanel
            {
                Name = "pnlSelectedUnit",
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                Margin = new Padding(0),
            };
            unitRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            unitRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 50F));
            unitRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 76F));

            _lblSelectedUnit = new Label
            {
                Name = "lblSelectedUnit",
                Text = "No unit selected",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                Font = new Font(Font, FontStyle.Bold),
            };
            _txtUnitLevel = new TextBox { Name = "txtUnitLevel", Text = "1", Width = 46, Anchor = AnchorStyles.Left };
            _txtUnitLevel.Leave += (_, _) => ApplyUnitLevel();
            _txtUnitLevel.KeyDown += (_, e) =>
            {
                if (e.KeyCode != Keys.Enter) return;
                ApplyUnitLevel();
                e.SuppressKeyPress = true;
            };
            var applyLevel = new Button
            {
                Name = "btnApplyLevel",
                Text = "Set level",
                Width = 74,
                Height = 24,
                Anchor = AnchorStyles.Left,
            };
            applyLevel.Click += (_, _) => ApplyUnitLevel();

            unitRow.Controls.Add(_lblSelectedUnit, 0, 0);
            unitRow.Controls.Add(_txtUnitLevel, 1, 0);
            unitRow.Controls.Add(applyLevel, 2, 0);
            panel.Controls.Add(unitRow, 0, 0);

            var loadout = new GroupBox
            {
                Name = "grpLoadout",
                Text = "Loadout",
                Dock = DockStyle.Fill,
                Padding = new Padding(8, 4, 8, 4),
                Margin = new Padding(0, 4, 0, 0),
            };
            var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 5, Margin = new Padding(0) };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90F));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            foreach (GearType type in new[] { GearType.Weapon, GearType.Armor, GearType.Shield, GearType.Helmet, GearType.Accessory })
            {
                grid.RowStyles.Add(new RowStyle(SizeType.Percent, 20F));

                var caption = new Label
                {
                    Text = type.ToString(),
                    AutoSize = true,
                    Anchor = AnchorStyles.Left,
                    ForeColor = SystemColors.GrayText,
                };

                var value = new Label
                {
                    Name = $"lblSlot{type}",
                    Text = "(empty)",
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleLeft,
                    AutoEllipsis = true,
                    Anchor = AnchorStyles.Left | AnchorStyles.Right,
                };

                _slotValues[type] = value;
                grid.Controls.Add(caption);
                grid.Controls.Add(value);
            }

            loadout.Controls.Add(grid);
            panel.Controls.Add(loadout, 0, 1);

            var details = new GroupBox
            {
                Name = "grpItemDetails",
                Text = "Selected item",
                Dock = DockStyle.Fill,
                Padding = new Padding(8, 4, 8, 4),
                Margin = new Padding(0, 6, 0, 0),
            };
            _txtItemDetails = new TextBox
            {
                Name = "txtItemDetails",
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BorderStyle = BorderStyle.None,
                BackColor = SystemColors.Window,
            };
            details.Controls.Add(_txtItemDetails);
            panel.Controls.Add(details, 0, 2);

            var sets = new GroupBox
            {
                Name = "grpSets",
                Text = "Item and stat sets",
                Dock = DockStyle.Fill,
                Padding = new Padding(8, 4, 8, 4),
                Margin = new Padding(0, 6, 0, 0),
            };
            var setsRow = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Margin = new Padding(0) };
            setsRow.Controls.Add(SmallLabel("Item set"));
            _cmbItemSet = new ComboBox { Name = "cmbItemSet", Width = 70, DropDownStyle = ComboBoxStyle.DropDownList };
            setsRow.Controls.Add(_cmbItemSet);
            setsRow.Controls.Add(SmallLabel("Stat set"));
            _cmbStatSet = new ComboBox { Name = "cmbStatSet", Width = 70, DropDownStyle = ComboBoxStyle.DropDownList };
            setsRow.Controls.Add(_cmbStatSet);
            setsRow.Controls.Add(ActionButton("btnApplySets", "Apply both", 96, (_, _) => ApplySetsToSelected()));
            sets.Controls.Add(setsRow);
            panel.Controls.Add(sets, 0, 3);

            return panel;
        }

        /// <summary>The skills page: learnable on the left, granted on the right, JP status underneath.</summary>
        private Control BuildSkillsPage()
        {
            var root = new TableLayoutPanel
            {
                Name = "pnlSkillsPage",
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(8),
            };
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 84F));

            var lists = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = new Padding(0) };
            lists.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55F));
            lists.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45F));

            var learnable = new GroupBox
            {
                Name = "grpLearnable",
                Text = "Available for this class",
                Dock = DockStyle.Fill,
                Padding = new Padding(6),
                Margin = new Padding(0, 0, 8, 0),
            };
            _lstLearnableSkills = new ListBox { Name = "lstLearnableSkills", Dock = DockStyle.Fill, IntegralHeight = false };
            _lstLearnableSkills.SelectedIndexChanged += (_, _) => ShowSelectedSkillDetails();
            _lstLearnableSkills.DoubleClick += (_, _) => AddSelectedSkill();
            learnable.Controls.Add(_lstLearnableSkills);

            var granted = new GroupBox
            {
                Name = "grpGranted",
                Text = "Granted (written as SKILL lines)",
                Dock = DockStyle.Fill,
                Padding = new Padding(6),
                Margin = new Padding(0),
            };
            _lstGrantedSkills = new ListBox { Name = "lstGrantedSkills", Dock = DockStyle.Fill, IntegralHeight = false };
            _lstGrantedSkills.SelectedIndexChanged += (_, _) => ShowSelectedSkillDetails();
            _lstGrantedSkills.DoubleClick += (_, _) => RemoveSelectedSkill();
            granted.Controls.Add(_lstGrantedSkills);

            lists.Controls.Add(learnable, 0, 0);
            lists.Controls.Add(granted, 1, 0);

            var buttons = new FlowLayoutPanel
            {
                Name = "pnlSkillButtons",
                Dock = DockStyle.Fill,
                WrapContents = false,
                Margin = new Padding(0, 6, 0, 0),
            };
            buttons.Controls.Add(ActionButton("btnAddSkill", "Add →", 90, (_, _) => AddSelectedSkill()));
            buttons.Controls.Add(ActionButton("btnRemoveSkill", "← Remove", 100, (_, _) => RemoveSelectedSkill()));

            _lblLoadoutJp = new Label
            {
                Name = "lblLoadoutJp",
                AutoSize = true,
                Margin = new Padding(18, 11, 0, 0),
                TextAlign = ContentAlignment.MiddleLeft,
            };
            buttons.Controls.Add(_lblLoadoutJp);

            _txtSkillDetails = new TextBox
            {
                Name = "txtSkillDetails",
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = SystemColors.Window,
                Margin = new Padding(0, 6, 0, 0),
            };

            root.Controls.Add(lists, 0, 0);
            root.Controls.Add(buttons, 0, 1);
            root.Controls.Add(_txtSkillDetails, 0, 2);
            return root;
        }

        private static Label SmallLabel(string text) => new()
        {
            Text = text,
            AutoSize = true,
            Margin = new Padding(0, 8, 6, 0),
            ForeColor = SystemColors.GrayText,
        };

        private static Button ActionButton(string name, string text, int width, EventHandler onClick)
        {
            var button = new Button { Name = name, Text = text, Width = width, Height = 28, Margin = new Padding(0, 2, 8, 0) };
            button.Click += onClick;
            return button;
        }


        /// <summary>Populate the roster drop-down from the project's school files.</summary>
        private void RefreshRosterList()
        {
            _cmbRoster.Items.Clear();

            string project = txtPackPath.Text.Trim();
            if (string.IsNullOrEmpty(project) || !Directory.Exists(project)) return;

            foreach (string file in new ModdingGUI.Core.Api.SchoolsApi().ListFiles(project))
            {
                _cmbRoster.Items.Add(Path.GetFileName(file));
            }

            if (_cmbRoster.Items.Count > 0) _cmbRoster.SelectedIndex = 0;
        }

        /// <summary>
        /// Load an existing roster into the tree. Units keep a note of where they came from, so exporting edits them
        /// in place and removes the ones deleted here.
        /// </summary>
        private void LoadSelectedRoster()
        {
            string project = txtPackPath.Text.Trim();
            string? roster = _cmbRoster.SelectedItem?.ToString();

            if (string.IsNullOrEmpty(project) || !Directory.Exists(project) || string.IsNullOrWhiteSpace(roster))
            {
                MessageBox.Show("Select a project and a roster first.", "Load Roster",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var school = new ModdingGUI.Core.Api.SchoolsApi().Get(project, "data/school/" + roster);
            if (school is null)
            {
                MessageBox.Show($"Could not read '{roster}'.", "Load Roster", MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            tvwTeam.Nodes.Clear();
            teamUnits.Clear();
            _loadedRosterUnits[roster!] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var dto in school.Units)
            {
                var unit = new Unit
                {
                    UnitName = dto.Name,
                    Class = new ClassDefinition { ClassName = dto.ClassName },
                    Level = int.TryParse(dto.Value("LEVEL"), out int level) ? level : 1,
                    JobPoints = int.TryParse(dto.Value("JOBPOINTS"), out int jp) ? jp : 0,
                    LoadedFromRoster = roster,
                    LoadedName = dto.Name,
                };

                var stats = dto.CoreStats;
                if (stats.Count >= 6)
                {
                    unit.BaseStats = new Stats
                    {
                        CON = stats[0], PWR = stats[1], ACC = stats[2], DEF = stats[3], INI = stats[4], MOV = stats[5],
                    };
                }

                foreach (string skill in dto.Skills)
                {
                    unit.LearnedSkills.Add(new Skill { InternalName = skill, Name = skill });
                }

                teamUnits.Add(unit);
                AddUnitToTreeView(unit);
                _loadedRosterUnits[roster!].Add(unit.UnitName);
            }

            AppendLog($"Loaded {school.Units.Count} unit(s) from '{roster}'.", Color.Green);
            UpdateUnitCountAndButtons();
        }

        /// <summary>Repopulate the loadout pages from the selected unit.</summary>
        private void RefreshLoadoutEditors()
        {
            if (_lstLegalItems is null) return;

            try
            {
                RefreshLoadoutEditorsCore();
            }
            catch (Exception ex)
            {
                // Runs from the tree selection handler; a partial project must not take the window down.
                AppendLog($"Could not populate the loadout editors: {ex.Message}", Color.Orange);
            }
        }

        private void RefreshLoadoutEditorsCore()
        {
            _lstLegalItems.Items.Clear();
            _lstGrantedSkills.Items.Clear();
            _lstLearnableSkills.Items.Clear();
            _cmbItemSet.Items.Clear();
            _cmbStatSet.Items.Clear();
            _classItems = new List<ItemChoice>();
            _txtItemDetails.Text = string.Empty;
            _txtSkillDetails.Text = string.Empty;
            foreach (Label value in _slotValues.Values) value.Text = "(empty)";

            Unit? unit = _selectedUnit;
            string project = txtPackPath.Text.Trim();

            if (unit is null || string.IsNullOrEmpty(project) || !Directory.Exists(project))
            {
                _lblSelectedUnit.Text = "No unit selected";
                _lblGearCount.Text = "Select a unit to load its gear.";
                _lblLoadoutJp.Text = string.Empty;
                _lblLoadoutJp.ForeColor = SystemColors.ControlText;
                return;
            }

            _lblSelectedUnit.Text = $"{unit.UnitName} — {unit.Class.ClassName}";
            _txtUnitLevel.Text = unit.Level.ToString();

            string className = unit.Class.ClassName;
            var classes = new ModdingGUI.Core.Api.ClassDefsApi();
            var classDef = classes.Get(project, className);
            var categories = ModdingGUI.Core.Units.ItemCategoryRules.Parse(classDef);

            // Legal items for this class. The label carries the numbers a picker needs: slot, subtype, style, tier.
            var items = new ModdingGUI.Core.Api.ItemsApi().GetAll(project);
            _classItems = items
                .Where(item => ModdingGUI.Core.Units.ItemCategoryRules.Allows(
                    categories, item.Type, item.SubType, item.Style))
                .Select(item => new ItemChoice(item,
                    $"{item.Name}  [{item.Type} / {item.SubType} / {item.Style}]" +
                    (item.MinLevel is > 0 ? $"  lvl {item.MinLevel}" : string.Empty)))
                .ToList();

            RefreshGearList();

            foreach (GearType type in _slotValues.Keys)
            {
                _slotValues[type].Text = Slot(unit, type);
            }

            // Learnable skills, priced.
            var allSkills = new ModdingGUI.Core.Api.SkillsApi().GetAll(project);
            foreach (var skill in ModdingGUI.Core.Units.LearnableSkills.For(allSkills, className))
            {
                int cost = ModdingGUI.Core.Units.JobPoints.CostOf(skill);
                int level = ModdingGUI.Core.Units.LearnableSkills.LevelOf(skill);
                _lstLearnableSkills.Items.Add(new SkillChoice(skill, $"{skill.Name}  ({cost} JP, lvl {level})"));
            }

            foreach (var skill in unit.LearnedSkills)
            {
                _lstGrantedSkills.Items.Add(skill.InternalName);
            }

            // Job points: award from the class and level, spent on the granted skills.
            var budget = ModdingGUI.Core.Units.JobPoints.Compute(
                ModdingGUI.Core.Units.JobPoints.ParseAwards(classDef?.Value("LEVELUPJPGIVEN")),
                unit.Level,
                unit.LearnedSkills.Select(_ => 0));

            int spent = GrantedSkillCost(allSkills, unit);
            int effective = unit.JobPointsOverridden ? unit.JobPoints : budget.Awarded;
            int left = effective - spent;

            _lblLoadoutJp.Text = $"Level {unit.Level}: {effective} JP" +
                                 (unit.JobPointsOverridden ? " (overridden)" : " awarded") +
                                 $", {spent} spent, {Math.Max(0, left)} left" +
                                 (left < 0 ? "  (over budget)" : string.Empty);
            _lblLoadoutJp.ForeColor = left < 0 ? Color.Firebrick : SystemColors.ControlText;

            // Sets used by this class's existing gladiators.
            var records = gladiatorEntries.Where(g => g.Class.Equals(className, StringComparison.OrdinalIgnoreCase)).ToList();
            foreach (int index in records.Select(r => r.StatSet).Distinct().OrderBy(i => i))
            {
                _cmbStatSet.Items.Add(index);
            }

            foreach (int index in records.Select(r => r.ItemSet).Distinct().OrderBy(i => i))
            {
                _cmbItemSet.Items.Add(index);
            }

            if (_cmbStatSet.Items.Count > 0) _cmbStatSet.SelectedIndex = 0;
            if (_cmbItemSet.Items.Count > 0) _cmbItemSet.SelectedIndex = 0;
        }

        /// <summary>Apply the search box and slot filter to the cached list of items this class may use.</summary>
        private void RefreshGearList()
        {
            if (_lstLegalItems is null) return;

            string needle = _txtGearSearch?.Text?.Trim() ?? string.Empty;
            string slot = _cmbGearType?.SelectedItem as string ?? "All slots";

            // Keep the highlight across a refresh: assigning an item or typing in the filter must not lose the
            // selection the user is working with.
            string? keep = (_lstLegalItems.SelectedItem as ItemChoice)?.Item.Name;

            _lstLegalItems.BeginUpdate();
            try
            {
                _lstLegalItems.Items.Clear();

                foreach (ItemChoice choice in _classItems)
                {
                    if (!slot.StartsWith("All", StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(choice.Item.Type, slot, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (needle.Length > 0 && !ItemMatches(choice.Item, needle)) continue;

                    _lstLegalItems.Items.Add(choice);
                }

                if (keep is not null)
                {
                    for (int i = 0; i < _lstLegalItems.Items.Count; i++)
                    {
                        if (_lstLegalItems.Items[i] is ItemChoice candidate
                            && candidate.Item.Name.Equals(keep, StringComparison.OrdinalIgnoreCase))
                        {
                            _lstLegalItems.SelectedIndex = i;
                            break;
                        }
                    }
                }
            }
            finally
            {
                _lstLegalItems.EndUpdate();
            }

            if (_lblGearCount is not null)
            {
                _lblGearCount.Text = _classItems.Count == 0
                    ? "No legal items for this class"
                    : $"{_lstLegalItems.Items.Count} of {_classItems.Count} legal items";
            }

            ShowSelectedItemDetails();
        }

        private static bool ItemMatches(ModdingGUI.Core.Api.ItemDto item, string needle) =>
            (item.Name?.Contains(needle, StringComparison.OrdinalIgnoreCase) ?? false)
            || (item.Type?.Contains(needle, StringComparison.OrdinalIgnoreCase) ?? false)
            || (item.SubType?.Contains(needle, StringComparison.OrdinalIgnoreCase) ?? false)
            || (item.Style?.Contains(needle, StringComparison.OrdinalIgnoreCase) ?? false)
            || (item.Rarity?.Contains(needle, StringComparison.OrdinalIgnoreCase) ?? false);

        /// <summary>Describe the highlighted item: what it is, what it needs, what it does.</summary>
        private void ShowSelectedItemDetails()
        {
            if (_txtItemDetails is null) return;

            if (_lstLegalItems.SelectedItem is not ItemChoice choice)
            {
                _txtItemDetails.Text = _classItems.Count == 0
                    ? "No legal items: this class's ITEMCAT lines allow nothing."
                    : "Select an item to see its numbers.";
                return;
            }

            var item = choice.Item;
            var lines = new List<string>
            {
                item.Name,
                $"{item.Type} / {item.SubType} / {item.Style}",
                $"min level {item.MinLevel?.ToString() ?? "0"}" +
                (item.StarRating is > 0 ? $"   stars {item.StarRating}" : string.Empty) +
                (item.Cost is > 0 ? $"   cost {item.Cost:N0}" : string.Empty) +
                (string.IsNullOrWhiteSpace(item.Rarity) ? string.Empty : $"   {item.Rarity}"),
            };

            if (item.Affinity is { } affinity)
            {
                lines.Add($"affinity {affinity.Element} {affinity.Amount}");
            }

            if (item.StatMods.Count > 0)
            {
                lines.Add("stats: " + string.Join(", ", item.StatMods.Select(mod => $"{mod.Stat} {mod.Value:+#;-#;0}")));
            }

            if (item.Skills.Count > 0)
            {
                lines.Add("skills: " + string.Join(", ", item.Skills));
            }

            if (item.Attributes.Count > 0)
            {
                lines.Add("attributes: " + string.Join(", ", item.Attributes));
            }

            _txtItemDetails.Text = string.Join(Environment.NewLine, lines);
        }

        /// <summary>Describe the highlighted skill, from whichever list it was picked in.</summary>
        private void ShowSelectedSkillDetails()
        {
            if (_txtSkillDetails is null) return;

            var choice = _lstLearnableSkills.SelectedItem as SkillChoice
                         ?? _lstGrantedSkills.SelectedItem as SkillChoice;

            if (choice is null)
            {
                _txtSkillDetails.Text = _lstGrantedSkills.SelectedItem is string granted
                    ? $"{granted}{Environment.NewLine}A granted skill. Its record is in skills.tok."
                    : string.Empty;
                return;
            }

            var skill = choice.Skill;
            var lines = new List<string>
            {
                $"{skill.Name}  —  {ModdingGUI.Core.Units.JobPoints.CostOf(skill)} JP, " +
                $"level {ModdingGUI.Core.Units.LearnableSkills.LevelOf(skill)}",
            };

            if (!string.IsNullOrWhiteSpace(skill.SkillType) || !string.IsNullOrWhiteSpace(skill.SkillSubType))
            {
                lines.Add($"{skill.SkillType} / {skill.SkillSubType}");
            }

            foreach (var field in skill.Fields.Take(8))
            {
                lines.Add($"{field.FieldName} {field.RawValue}");
            }

            _txtSkillDetails.Text = string.Join(Environment.NewLine, lines);
        }

        /// <summary>What the granted skills cost, by the catalog's prices.</summary>
        private static int GrantedSkillCost(IReadOnlyList<ModdingGUI.Core.Api.SkillDto> allSkills, Unit unit)
        {
            var byName = allSkills.ToDictionary(skill => skill.Name, StringComparer.OrdinalIgnoreCase);
            int spent = 0;

            foreach (var skill in unit.LearnedSkills)
            {
                if (byName.TryGetValue(skill.InternalName, out var dto))
                {
                    spent += ModdingGUI.Core.Units.JobPoints.CostOf(dto);
                }
            }

            return spent;
        }

        /// <summary>Set the selected unit's level, then re-read its stat set at that level.</summary>
        private void ApplyUnitLevel()
        {
            if (_selectedUnit is null) return;

            if (!int.TryParse(_txtUnitLevel.Text.Trim(), out int level)
                || !ModdingGUI.Core.Units.InputRules.IsValidLevel(level))
            {
                AppendLog(ModdingGUI.Core.Units.InputRules.ExplainLevel(level) ??
                          $"'{_txtUnitLevel.Text}' is not a level.", Color.Orange);
                _txtUnitLevel.Text = _selectedUnit.Level.ToString();
                return;
            }

            _selectedUnit.Level = level;
            ApplyStatSetToSelected();
            RefreshLoadoutEditors();
            DisplayUnitStats(_selectedUnit);
            UpdatePreview();
        }

        private static string Slot(Unit unit, GearType type) =>
            unit.EquippedGear.FirstOrDefault(gear => gear.GearType == type)?.InternalName ?? "(empty)";

        private void AssignSelectedItem()
        {
            if (_selectedUnit is null || _lstLegalItems.SelectedItem is not ItemChoice choice) return;

            // The slot comes from the item's own Type, which is what the engine uses.
            if (!Enum.TryParse<GearType>(choice.Item.Type, ignoreCase: true, out GearType slot)) return;

            _selectedUnit.EquippedGear.RemoveAll(gear => gear.GearType == slot);
            _selectedUnit.EquippedGear.Add(new Gear
            {
                InternalName = choice.Item.Name,
                Name = choice.Item.Name,
                GearType = slot,
                SubType = choice.Item.SubType ?? string.Empty,
                Style = choice.Item.Style ?? string.Empty,
            });

            RefreshLoadoutEditors();
            DisplayUnitStats(_selectedUnit);
        }

        private void FillGearFromItemSet()
        {
            if (_selectedUnit is null) return;

            string project = txtPackPath.Text.Trim();
            if (_cmbItemSet.SelectedItem is not int setIndex || string.IsNullOrEmpty(project)) return;

            var set = new ModdingGUI.Core.Api.ItemSetsApi().Get(project, setIndex);
            if (set is null) return;

            var types = new ModdingGUI.Core.Api.ItemsApi().GetAll(project)
                .Where(item => !string.IsNullOrWhiteSpace(item.Type))
                .ToDictionary(item => item.Name, item => item.Type!, StringComparer.OrdinalIgnoreCase);

            var warnings = new List<string>();
            var gear = ModdingGUI.Core.Units.ItemSetGear.ForLevel(set.Rows, _selectedUnit.Level, types, warnings);

            foreach (string warning in warnings) AppendLog($"Item set {setIndex}: {warning}", Color.Orange);

            _selectedUnit.EquippedGear.Clear();
            AddGear(_selectedUnit, gear.Weapon, GearType.Weapon);
            AddGear(_selectedUnit, gear.Armor, GearType.Armor);
            AddGear(_selectedUnit, gear.Shield, GearType.Shield);
            AddGear(_selectedUnit, gear.Helmet, GearType.Helmet);
            AddGear(_selectedUnit, gear.Accessory, GearType.Accessory);

            RefreshLoadoutEditors();
            DisplayUnitStats(_selectedUnit);
        }

        private static void AddGear(Unit unit, string? name, GearType type)
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            unit.EquippedGear.Add(new Gear { InternalName = name!, Name = name!, GearType = type });
        }

        private void ApplySetsToSelected()
        {
            if (_selectedUnit is null) return;

            string project = txtPackPath.Text.Trim();
            if (string.IsNullOrEmpty(project)) return;

            ApplyStatSetToSelected();
            FillGearFromItemSet();
            DisplayUnitStats(_selectedUnit);
        }

        /// <summary>Read the selected stat set at the unit's level and adopt those stats.</summary>
        private void ApplyStatSetToSelected()
        {
            if (_selectedUnit is null) return;
            if (_cmbStatSet.SelectedItem is not int statIndex) return;
            if (!statSets.TryGetValue(statIndex, out var rows)) return;

            if (!rows.TryGetValue(_selectedUnit.Level, out var values) || values.Count < 5)
            {
                AppendLog($"Stat set {statIndex} has no row for level {_selectedUnit.Level}.", Color.Orange);
                return;
            }

            _selectedUnit.BaseStats = new Stats
            {
                CON = values[0], PWR = values[1], ACC = values[2], DEF = values[3], INI = values[4],
                MOV = values.Count > 5 ? values[5] : 1,
            };
        }

        private void AddSelectedSkill()
        {
            if (_selectedUnit is null || _lstLearnableSkills.SelectedItem is not SkillChoice choice) return;

            if (_selectedUnit.LearnedSkills.All(skill => !skill.InternalName.Equals(choice.Skill.Name, StringComparison.OrdinalIgnoreCase)))
            {
                _selectedUnit.LearnedSkills.Add(new Skill { InternalName = choice.Skill.Name, Name = choice.Skill.Name });
            }

            OfferSkillJobPoints();

            // The JP line and the stats panel both derive from the grant list, so refresh after the offer.
            RefreshLoadoutEditors();
            DisplayUnitStats(_selectedUnit);
        }

        private void RemoveSelectedSkill()
        {
            if (_selectedUnit is null || _lstGrantedSkills.SelectedItem is not string name) return;

            _selectedUnit.LearnedSkills.RemoveAll(skill =>
                skill.InternalName.Equals(name, StringComparison.OrdinalIgnoreCase));

            RefreshLoadoutEditors();
            DisplayUnitStats(_selectedUnit);
        }

        /// <summary>
        /// A granted skill costs JP, so adding one can push a unit over its award. Offer to raise <c>JOBPOINTS</c>
        /// instead of leaving a roster the engine would call illegal — the designer can always decline and keep the
        /// over-budget build.
        /// </summary>
        private void OfferSkillJobPoints()
        {
            if (_selectedUnit is null) return;

            string project = txtPackPath.Text.Trim();
            if (string.IsNullOrEmpty(project)) return;

            var classDef = new ModdingGUI.Core.Api.ClassDefsApi().Get(project, _selectedUnit.Class.ClassName);
            var budget = ModdingGUI.Core.Units.JobPoints.Compute(classDef, _selectedUnit.Level);
            var allSkills = new ModdingGUI.Core.Api.SkillsApi().GetAll(project);

            int spent = GrantedSkillCost(allSkills, _selectedUnit);
            int awarded = _selectedUnit.JobPointsOverridden ? _selectedUnit.JobPoints : budget.Awarded;

            if (spent <= awarded) return;

            string unitName = _selectedUnit.UnitName;
            string question =
                $"'{unitName}' now spends {spent} JP on granted skills but is awarded {awarded}.\n\n" +
                $"Raise this unit's JOBPOINTS to {spent} so the roster stays legal?";

            if (!UiConfirm(question, "Job points"))
            {
                AppendLog($"'{unitName}' is over its JP budget ({spent} of {awarded}); left as built.", Color.Orange);
                return;
            }

            _selectedUnit.JobPoints = spent;
            _selectedUnit.JobPointsOverridden = true;
            AppendLog($"Raised '{unitName}' JOBPOINTS to {spent} so its granted skills stay legal.", Color.Green);
        }

        private sealed record ItemChoice(ModdingGUI.Core.Api.ItemDto Item, string Label)
        {
            public override string ToString() => Label;
        }

        private sealed record SkillChoice(ModdingGUI.Core.Api.SkillDto Skill, string Label)
        {
            public override string ToString() => Label;
        }

        #endregion
    }
}
