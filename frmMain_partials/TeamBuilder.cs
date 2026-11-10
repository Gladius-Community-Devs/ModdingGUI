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

   /// <summary>
   /// The <c>EXPERIENCE</c> value the roster block carried, or null for a unit made here. Written back on save, so
   /// editing one unit does not reset the others' progress or outfit.
   /// </summary>
   public string? Experience { get; set; }

   /// <summary>The raw <c>CUSTOMIZE</c> value the roster block carried, e.g. <c>1, ""</c>.</summary>
   public string? Customize { get; set; }

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
            BuildRulesPage();

            // The gear and skill pages used to be placeholders that were removed. They are editors now.
            tabTeamGearSelection.Text = "Gear";
            tabTeamSkillSelection.Text = "Skills";
            BuildLoadoutEditors();
        }

        /// <summary>
        /// The Rules page, rebuilt as labelled groups: what team type this build writes, how big it is, what level new
        /// units start at, what job points they get, and which two rosters are written.
        /// </summary>
        /// <remarks>
        /// The designer puts these controls on a bare panel with absolute positions, which is why "Override JP" and
        /// its box read as unrelated to anything. They are re-parented into groups here so each one says what it does,
        /// and the roster choice is limited to the two files the campaign actually loads — the other 60-odd files in
        /// <c>data/school</c> are test rosters, and picking one produced a team the game never reads.
        /// </remarks>
        private void BuildRulesPage()
        {
            if (_rulesBuilt) return;
            _rulesBuilt = true;

            tabTeamRulesSelection.Controls.Clear();

            var root = new TableLayoutPanel
            {
                Name = "pnlRulesPage",
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 6,
                Padding = new Padding(12),
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            for (int i = 0; i < 5; i++) root.RowStyles.Add(new RowStyle(SizeType.Absolute, 0F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            var campaign = new GroupBox
            {
                Name = "grpCampaign",
                Text = "Team type",
                Dock = DockStyle.Fill,
                Padding = new Padding(10, 6, 10, 6),
                Margin = new Padding(0, 0, 0, 10),
            };
            var campaignContent = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = new Padding(0),
            };
            campaignContent.RowStyles.Add(new RowStyle(SizeType.Absolute, 26F));
            campaignContent.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            var campaignTitle = new Label
            {
                Name = "lblCampaignTitle",
                Text = "Campaign team",
                AutoSize = true,
                Font = new Font(Font, FontStyle.Bold),
            };
            var campaignHint = HintLabel(
                "This build writes the campaign rosters the game loads. PvP teams are not implemented yet, so the " +
                "old PvP/campaign switch is gone.");
            campaignContent.Controls.Add(campaignTitle, 0, 0);
            campaignContent.Controls.Add(campaignHint, 0, 1);

            // The radios stay parented and checked: several handlers read them, but the user never needs to see them.
            grpTeamType.Controls.Clear();
            rbnTeamCampaign.Checked = true;
            rbnTeamPVP.Enabled = false;
            campaignContent.Controls.Add(grpTeamType);
            grpTeamType.Visible = false;
            campaignContent.Controls.Add(rbnTeamCampaign);
            campaignContent.Controls.Add(rbnTeamPVP);
            rbnTeamCampaign.Visible = false;
            rbnTeamPVP.Visible = false;

            campaign.Controls.Add(campaignContent);
            root.Controls.Add(campaign, 0, 0);

            // ---- Size and level ----
            var size = new GroupBox
            {
                Name = "grpSize",
                Text = "Team size and starting level",
                Dock = DockStyle.Fill,
                Padding = new Padding(10, 6, 10, 6),
                Margin = new Padding(0, 0, 0, 10),
            };
            var sizeGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Margin = new Padding(0),
            };
            sizeGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210F));
            sizeGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            sizeGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            sizeGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));

            chbTeam40Glads.Text = "Allow 34 units";
            sizeGrid.Controls.Add(chbTeam40Glads, 0, 0);
            sizeGrid.Controls.Add(HintLabel(
                "Campaign rosters hold 14 units; 34 needs the 40-gladiator gecko code enabled in Dolphin."), 1, 0);

            txtTeamLevel.Enabled = true;
            sizeGrid.Controls.Add(RowLabel("New units start at level"), 0, 1);
            var levelRow = new FlowLayoutPanel { Dock = DockStyle.Fill, Margin = new Padding(0), WrapContents = false };
            levelRow.Controls.Add(txtTeamLevel);
            levelRow.Controls.Add(HintLabel("1–30. An existing unit's level is set on the Gear page."));
            sizeGrid.Controls.Add(levelRow, 1, 1);

            size.Controls.Add(sizeGrid);
            root.Controls.Add(size, 0, 1);

            // ---- Job points ----
            var jp = new GroupBox
            {
                Name = "grpJobPoints",
                Text = "Job points for new units",
                Dock = DockStyle.Fill,
                Padding = new Padding(10, 6, 10, 6),
                Margin = new Padding(0, 0, 0, 10),
            };
            var jpGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3, Margin = new Padding(0),
            };
            jpGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210F));
            jpGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            jpGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            jpGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            jpGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            _lblJpDerived = RowLabel("Awarded by class and level");
            jpGrid.Controls.Add(_lblJpDerived, 0, 0);

            _txtTeamJobPoints = new TextBox { Name = "txtTeamJobPoints", Width = 70, ReadOnly = true };
            var jpRow = new FlowLayoutPanel { Dock = DockStyle.Fill, Margin = new Padding(0), WrapContents = false };
            jpRow.Controls.Add(_txtTeamJobPoints);
            _lblJpHint = HintLabel(string.Empty);
            jpRow.Controls.Add(_lblJpHint);
            jpGrid.Controls.Add(jpRow, 1, 0);

            _chbTeamOverrideJp = new CheckBox
            {
                Name = "chbTeamOverrideJp",
                Text = "Give new units a custom amount instead",
                AutoSize = true,
                Margin = new Padding(0, 4, 0, 0),
            };
            jpGrid.Controls.Add(_chbTeamOverrideJp, 0, 1);
            jpGrid.SetColumnSpan(_chbTeamOverrideJp, 2);
            jpGrid.Controls.Add(HintLabel(
                "Job points are what the class's LEVELUPJPGIVEN awards by that level. The selected unit's own budget, " +
                "and what it has spent, is shown on the Skills page."), 0, 2);
            jpGrid.SetColumnSpan(jpGrid.GetControlFromPosition(0, 2)!, 2);

            _chbTeamOverrideJp.CheckedChanged += (_, _) =>
            {
                _txtTeamJobPoints.ReadOnly = !_chbTeamOverrideJp.Checked;
                UpdateJobPointPreview();
            };
            txtTeamLevel.TextChanged += (_, _) => UpdateJobPointPreview();
            ddlTeamClasses.SelectedIndexChanged += (_, _) => UpdateJobPointPreview();

            jp.Controls.Add(jpGrid);
            root.Controls.Add(jp, 0, 2);

            // ---- Rosters ----
            var rosters = new GroupBox
            {
                Name = "grpRosters",
                Text = "Rosters to save into",
                Dock = DockStyle.Fill,
                Padding = new Padding(10, 6, 10, 6),
                Margin = new Padding(0, 0, 0, 10),
            };
            var rosterGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 4, Margin = new Padding(0),
            };
            rosterGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210F));
            rosterGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            for (int i = 0; i < 4; i++) rosterGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));

            chbTeamRosterValens = new CheckBox
            {
                Name = "chbTeamRosterValens",
                Text = "Valens campaign",
                AutoSize = true,
                Checked = true,
                Margin = new Padding(0, 4, 0, 0),
            };
            chbTeamRosterUrsula = new CheckBox
            {
                Name = "chbTeamRosterUrsula",
                Text = "Ursula campaign",
                AutoSize = true,
                Checked = true,
                Margin = new Padding(0, 4, 0, 0),
            };

            rosterGrid.Controls.Add(chbTeamRosterValens, 0, 0);
            rosterGrid.Controls.Add(HintLabel("valensimperia.tok"), 1, 0);
            rosterGrid.Controls.Add(chbTeamRosterUrsula, 0, 1);
            rosterGrid.Controls.Add(HintLabel("ursulanordagh.tok"), 1, 1);

            var saveRow = new FlowLayoutPanel { Dock = DockStyle.Fill, Margin = new Padding(0), WrapContents = false };
            btnTeamAddToFile.Text = "Save team to rosters";
            btnTeamAddToFile.Width = 170;
            btnTeamAddToFile.Height = 28;
            btnTeamAddToFile.Margin = new Padding(0, 2, 10, 0);
            saveRow.Controls.Add(btnTeamAddToFile);
            saveRow.Controls.Add(HintLabel(
                "Writes the team list above into the ticked rosters. Existing units are updated in place; new ones " +
                "are appended."));
            rosterGrid.Controls.Add(saveRow, 0, 2);
            rosterGrid.SetColumnSpan(saveRow, 2);
            rosterGrid.Controls.Add(HintLabel(
                "The campaign reads only these two school files. The rest of data/school is test rosters that a " +
                "normal playthrough never loads."), 0, 3);
            rosterGrid.SetColumnSpan(rosterGrid.GetControlFromPosition(0, 3)!, 2);

            rosters.Controls.Add(rosterGrid);
            root.Controls.Add(rosters, 0, 3);

            // ---- Load an existing team ----
            var load = new GroupBox
            {
                Name = "grpLoadTeam",
                Text = "Edit an existing team",
                Dock = DockStyle.Fill,
                Padding = new Padding(10, 6, 10, 6),
                Margin = new Padding(0, 0, 0, 0),
            };
            var loadRow = new FlowLayoutPanel { Dock = DockStyle.Fill, Margin = new Padding(0), WrapContents = false };
            _cmbRoster = new ComboBox
            {
                Name = "cmbRoster",
                Width = 200,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Margin = new Padding(0, 2, 8, 0),
            };
            var loadRoster = new Button
            {
                Name = "btnLoadRoster",
                Text = "Load into the editor",
                Width = 150,
                Height = 26,
                Margin = new Padding(0, 2, 10, 0),
            };
            loadRoster.Click += (_, _) => LoadSelectedRoster();
            loadRow.Controls.Add(_cmbRoster);
            loadRow.Controls.Add(loadRoster);
            loadRow.Controls.Add(HintLabel(
                "Adds that roster's units to the team list so they can be edited; saving rewrites them in place."));
            load.Controls.Add(loadRow);
            root.Controls.Add(load, 0, 4);

            // Rows 0-4 are absolute; give each its content height so the page does not clip on a small window.
            root.RowStyles[0] = new RowStyle(SizeType.Absolute, 96F);
            root.RowStyles[1] = new RowStyle(SizeType.Absolute, 108F);
            root.RowStyles[2] = new RowStyle(SizeType.Absolute, 120F);
            root.RowStyles[3] = new RowStyle(SizeType.Absolute, 156F);
            root.RowStyles[4] = new RowStyle(SizeType.Absolute, 76F);

            tabTeamRulesSelection.Controls.Add(root);

            // A new unit needs a level: the designer left the box empty and the campaign handler no longer fills it.
            if (string.IsNullOrWhiteSpace(txtTeamLevel.Text)) txtTeamLevel.Text = "1";

            UpdateJobPointPreview();
            UpdateUnitCountAndButtons();
        }

        private bool _rulesBuilt;
        private Label _lblJpDerived = new();
        private Label _lblJpHint = new();

        private static Label RowLabel(string text) => new()
        {
            Text = text,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 6, 8, 0),
        };

        private static Label HintLabel(string text) => new()
        {
            Text = text,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(6, 6, 0, 0),
        };

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

            if (_chbTeamOverrideJp.Checked)
            {
                _lblJpHint.Text = "custom — new units start with this amount";
                return;
            }

            var budget = CurrentJobPointBudget();
            _txtTeamJobPoints.Text = budget is null ? string.Empty : budget.Awarded.ToString();
            _lblJpHint.Text = budget is null
                ? "select a project and a class"
                : "auto — what the class awards by this level";
        }

        /// <summary>
        /// Handles the CheckedChanged event of rbnTeamPVP radio button.
        /// </summary>
        private void rbnTeamPVP_CheckedChanged(object sender, EventArgs e)
        {
            bool isPvpMode = rbnTeamPVP.Checked;
            
            // The level of a new unit is always the designer's choice; PvP (the old reason to lock it) is not built.
            txtTeamLevel.Enabled = true;
            
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
                // A modal here would deadlock the UI driver; UiConfirm logs and answers under automation.
                bool confirmed = UiConfirm(
                    "This option requires the 40 glads gecko code to be enabled in Dolphin.\n\n" +
                    "Do you have this code enabled?",
                    "40 Glads Gecko Code Required");

                if (!confirmed)
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
                new("EXPERIENCE", string.IsNullOrWhiteSpace(unit.Experience) ? "0" : unit.Experience!.Trim()),
                new("JOBPOINTS", unit.JobPoints.ToString()),
                new("CUSTOMIZE", string.IsNullOrWhiteSpace(unit.Customize) ? "0, \"\"" : unit.Customize!.Trim()),
            };

            var stats = unit.BaseStats;
            fields.Add(new ModdingGUI.Core.Api.SchoolFieldDto("CORESTATSCOMP2",
                $"{stats.CON}, {stats.PWR}, {stats.ACC}, {stats.DEF}, {stats.INI}, {stats.MOV}"));

            // Gear comes from the unit's own loadout. The class item-set fallback is for a unit made here, which
            // starts with nothing; filling a roster unit's empty slots would invent equipment the designer did not
            // ask for (vanilla leaves Valens' shield and helmet empty on purpose).
            string weapon = GetItemNameByType(unit.EquippedGear, GearType.Weapon);
            string armor = GetItemNameByType(unit.EquippedGear, GearType.Armor);
            string shield = GetItemNameByType(unit.EquippedGear, GearType.Shield);
            string helmet = GetItemNameByType(unit.EquippedGear, GearType.Helmet);
            string accessory = GetItemNameByType(unit.EquippedGear, GearType.Accessory);

            if (unit.LoadedFromRoster is null
                && (string.IsNullOrEmpty(weapon) || string.IsNullOrEmpty(armor) || string.IsNullOrEmpty(shield)
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
                ModdingGUI.Core.Values.QuotedList.Join(new[]
                {
                    weapon, armor, shield, helmet, accessory,
                })));

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
                var skipped = new List<string>();
                var perRoster = new List<string>();
                int added = 0, updatedTotal = 0, unchangedTotal = 0, removedTotal = 0;

                foreach (string roster in rosters)
                {
                    string relative = "data/school/" + roster;

                    var rosterUnits = schools.Get(projectPath, relative)?.Units ?? Array.Empty<ModdingGUI.Core.Api.SchoolUnitDto>();
                    var existing = rosterUnits
                        .ToDictionary(u => u.Name, StringComparer.OrdinalIgnoreCase);

                    foreach (var unit in units.Where(unit => existing.ContainsKey(unit.Name)))
                    {
                        skipped.Add($"{unit.Name} (already in {roster})");
                    }

                    // Units loaded from this roster are edited in place rather than appended again — and only where
                    // the value actually differs, so a save that changes one unit leaves the others byte-identical
                    // (experience, outfit and hand-placed comments included).
                    int updated = 0;
                    int unchanged = 0;
                    foreach (var (unit, _) in built.Where(entry =>
                        string.Equals(entry.Source.LoadedFromRoster, roster, StringComparison.OrdinalIgnoreCase)))
                    {
                        if (!existing.TryGetValue(unit.Name, out var current))
                        {
                            continue;   // renamed out of the roster; the old block is removed below
                        }

                        bool changed = false;

                        foreach (var field in unit.Fields)
                        {
                            if (field.Key.Equals("SKILL", StringComparison.OrdinalIgnoreCase)) continue;
                            if (SameValue(current.Value(field.Key), field.RawValue)) continue;

                            schools.SetUnitField(projectPath, relative, unit.Name, field.Key, field.RawValue);
                            changed = true;
                        }

                        var wantedSkills = unit.Fields
                            .Where(f => f.Key.Equals("SKILL", StringComparison.OrdinalIgnoreCase))
                            .Select(f => f.RawValue.Trim().Trim('"'))
                            .ToArray();

                        if (!wantedSkills.SequenceEqual(current.Skills, StringComparer.OrdinalIgnoreCase))
                        {
                            schools.SetUnitSkills(projectPath, relative, unit.Name, wantedSkills);
                            changed = true;
                        }

                        if (changed) updated++; else unchanged++;
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

                    int freshAdded = schools.AddUnits(projectPath, relative, fresh);
                    added += freshAdded;
                    updatedTotal += updated;
                    unchangedTotal += unchanged;
                    removedTotal += removedUnits;

                    perRoster.Add($"{roster}: {freshAdded} added, {updated} updated, {unchanged} unchanged, " +
                                  $"{removedUnits} removed");
                    AppendLog($"'{roster}': +{freshAdded} added, {updated} updated, {unchanged} unchanged, " +
                              $"{removedUnits} removed.", Color.Blue);
                }

                // One unit into two rosters is one unit, not two: the old message summed the per-roster counts and read
                // as a phantom second unit. The headline counts units, the lines below count writes.
                string summary = added + updatedTotal + removedTotal == 0
                    ? $"Nothing to write: {units.Count} unit(s) already match the ticked roster(s)."
                    : $"Team of {units.Count} unit(s) written to {rosters.Count} roster(s):" +
                      Environment.NewLine + Environment.NewLine +
                      string.Join(Environment.NewLine, perRoster);

                if (skipped.Count > 0)
                {
                    summary += Environment.NewLine + Environment.NewLine +
                               $"{skipped.Distinct().Count()} unit(s) already existed in a roster, so they were matched " +
                               "in place instead of being added twice:" + Environment.NewLine +
                               string.Join(Environment.NewLine, skipped.Distinct().Take(10));
                }

                AppendLog(summary, added + updatedTotal > 0 ? Color.Green : Color.Orange);
                UiAlert(summary, "Team Export");
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

        /// <summary>
        /// Compare a roster value with the one we would write, ignoring whitespace runs. Tab/space differences in the
        /// file are not edits, and treating them as such rewrote every block on every save.
        /// </summary>
        private static bool SameValue(string? current, string? wanted)
        {
            static string Normalise(string? value) =>
                value is null ? string.Empty : string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

            return string.Equals(Normalise(current), Normalise(wanted), StringComparison.Ordinal);
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

        /// <summary>Every skill in the project, cached for the details panel's chain lookup.</summary>
        private IReadOnlyList<ModdingGUI.Core.Api.SkillDto> _skillsCache = Array.Empty<ModdingGUI.Core.Api.SkillDto>();

        /// <summary>Every item the selected unit's class may use, before the search/slot filter.</summary>
        private List<ItemChoice> _classItems = new();

        // ---- Skills page ----
        private ListBox _lstGrantedSkills = new();
        private ListBox _lstLearnableSkills = new();
        private CheckBox _chkAnyClassSkills = new();
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
            _gearPage = BuildGearPage();
            tabTeamGearSelection.Controls.Add(_gearPage);
            _lblGearEmpty = AddEmptyStateOverlay(tabTeamGearSelection, "lblGearEmpty");

            tabTeamSkillSelection.Controls.Clear();
            _skillsPage = BuildSkillsPage();
            tabTeamSkillSelection.Controls.Add(_skillsPage);
            _lblSkillsEmpty = AddEmptyStateOverlay(tabTeamSkillSelection, "lblSkillsEmpty");

            RefreshRosterList();
            RefreshLoadoutEditors();
        }

        private Control? _gearPage;
        private Control? _skillsPage;
        private Label? _lblGearEmpty;
        private Label? _lblSkillsEmpty;

        /// <summary>
        /// A message that covers the page until there is a unit to edit. Gear and skills are per unit, so showing an
        /// empty list with live buttons invited clicks that did nothing.
        /// </summary>
        private static Label AddEmptyStateOverlay(Control page, string name)
        {
            var label = new Label
            {
                Name = name,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = SystemColors.Control,
                ForeColor = SystemColors.GrayText,
                Padding = new Padding(40),
                Font = new Font(SystemFonts.DefaultFont.FontFamily, 12F, FontStyle.Regular),
            };

            page.Controls.Add(label);
            label.BringToFront();
            return label;
        }

        /// <summary>Show the "make a unit first" message, or the editors when there is a unit.</summary>
        /// <remarks>
        /// The message replaces the page rather than covering it: two Dock=Fill siblings paint in an order the window
        /// renderer does not always honour, and a blank list with live buttons invited clicks that did nothing.
        /// </remarks>
        private void UpdateLoadoutEmptyState(Unit? unit)
        {
            string message = unit is not null
                ? string.Empty
                : teamUnits.Count > 0
                    ? "Select a unit in the team list on the right to edit its gear and skills."
                    : "No units yet.\n\nCreate one on the Unit Selection tab — type a name, pick a class, press Add Unit " +
                      "— then come back here to give it gear and skills.";

            ShowPageOrMessage(tabTeamGearSelection, _gearPage, _lblGearEmpty, unit is not null, message);
            ShowPageOrMessage(tabTeamSkillSelection, _skillsPage, _lblSkillsEmpty, unit is not null, message);
        }

        private static void ShowPageOrMessage(
            TabPage host, Control? page, Label? message, bool showPage, string text)
        {
            if (page is null || message is null) return;

            if (showPage)
            {
                if (host.Controls.Count == 1 && ReferenceEquals(host.Controls[0], page)) return;

                host.Controls.Clear();
                page.Enabled = true;
                host.Controls.Add(page);
                return;
            }

            if (host.Controls.Count == 1 && ReferenceEquals(host.Controls[0], message)) return;

            host.Controls.Clear();
            message.Text = text;
            host.Controls.Add(message);
        }

        /// <summary>The two rosters the campaign loads. Everything else in <c>data/school</c> is test content.</summary>
        private static readonly string[] CampaignRosters = { "valensimperia.tok", "ursulanordagh.tok" };

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

            _chkAnyClassSkills = new CheckBox
            {
                Name = "chkAnyClassSkills",
                Text = "Also list skills any class can learn",
                AutoSize = true,
                Margin = new Padding(8, 8, 0, 0),
            };
            _chkAnyClassSkills.CheckedChanged += (_, _) => RefreshLoadoutEditors();
            buttons.Controls.Add(_chkAnyClassSkills);

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
        /// <summary>Populate the roster drop-down with the two campaign rosters, in a fixed order.</summary>
        private void RefreshRosterList()
        {
            _cmbRoster.Items.Clear();

            string project = txtPackPath.Text.Trim();
            if (string.IsNullOrEmpty(project) || !Directory.Exists(project)) return;

            var present = new ModdingGUI.Core.Api.SchoolsApi().ListFiles(project)
                .Select(Path.GetFileName)
                .Where(name => !string.IsNullOrEmpty(name))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Only the campaign's two files: the other ~60 in data/school are test rosters, and offering them
            // produced a team the game never reads.
            foreach (string roster in CampaignRosters)
            {
                if (present.Contains(roster)) _cmbRoster.Items.Add(roster);
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

                    // A roster states its job points outright, so show that number rather than the class/level award.
                    JobPointsOverridden = true,

                    LoadedFromRoster = roster,
                    LoadedName = dto.Name,

                    // Carried verbatim so a save writes them back instead of resetting them to 0.
                    Experience = dto.Value("EXPERIENCE"),
                    Customize = dto.Value("CUSTOMIZE"),
                };

                var stats = dto.CoreStats;
                if (stats.Count >= 6)
                {
                    unit.BaseStats = new Stats
                    {
                        CON = stats[0], PWR = stats[1], ACC = stats[2], DEF = stats[3], INI = stats[4], MOV = stats[5],
                    };
                }

                // The five ITEMSCOMP slots, in the engine's order. Without this the Gear page opened empty for a
                // loaded unit — and, worse, the next save wrote five empty slots over the roster's equipment.
                var slots = dto.ItemsComp;
                GearType[] slotOrder =
                {
                    GearType.Weapon, GearType.Armor, GearType.Shield, GearType.Helmet, GearType.Accessory,
                };
                for (int i = 0; i < slotOrder.Length && i < slots.Count; i++)
                {
                    AddGear(unit, slots[i], slotOrder[i]);
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
                UpdateLoadoutEmptyState(null);
                return;
            }

            UpdateLoadoutEmptyState(unit);

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

            // Learnable skills. The picker shows what a designer can actually place: the class's own skills, without
            // the combo hits (granted by their root) or the innate/command records a roster gets for free.
            var allSkills = new ModdingGUI.Core.Api.SkillsApi().GetAll(project);
            _skillsCache = allSkills;

            var options = new ModdingGUI.Core.Units.LearnableSkillOptions
            {
                IncludeAnyClass = _chkAnyClassSkills.Checked,
            };

            foreach (var skill in ModdingGUI.Core.Units.LearnableSkills.For(allSkills, className, options))
            {
                int cost = ModdingGUI.Core.Units.JobPoints.CostOf(skill);
                int level = ModdingGUI.Core.Units.LearnableSkills.LevelOf(skill);

                string tags = string.Empty;
                if (ModdingGUI.Core.Units.LearnableSkills.IsComboRoot(skill))
                {
                    // The chain length is what a hit list shows: Combo I uses two of the shared combo records.
                    int hits = ModdingGUI.Core.Units.LearnableSkills.ChainOf(skill, allSkills).Count;
                    tags += hits > 1 ? $"  · combo ×{hits}" : "  · combo";
                }

                if (ModdingGUI.Core.Units.LearnableSkills.ClassesOf(skill).Count == 0) tags += "  · any class";

                _lstLearnableSkills.Items.Add(new SkillChoice(skill, $"{skill.Name}  ({cost} JP, lvl {level}){tags}"));
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

            string category = ModdingGUI.Core.Units.LearnableSkills.CategoryOf(skill);
            if (category.Length > 0) lines.Add($"category: {category}");

            var classes = ModdingGUI.Core.Units.LearnableSkills.ClassesOf(skill);
            lines.Add(classes.Count == 0
                ? "classes: any — the record has no SKILLUSECLASS line"
                : "classes: " + string.Join(", ", classes));

            if (ModdingGUI.Core.Units.LearnableSkills.IsComboRoot(skill))
            {
                var chain = ModdingGUI.Core.Units.LearnableSkills.ChainOf(skill, _skillsCache);
                lines.Add(string.Empty);
                lines.Add($"combo of {chain.Count}: the later hits come with this skill and are never bought on their " +
                          "own, which is why they cost no job points.");
                for (int i = 0; i < chain.Count; i++)
                {
                    var hit = chain[i];
                    string label = i == 0 ? "buys" : $"hit {i + 1}";
                    lines.Add($"  {label}: {hit.Name} ({ModdingGUI.Core.Units.JobPoints.CostOf(hit)} JP)");
                }
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
