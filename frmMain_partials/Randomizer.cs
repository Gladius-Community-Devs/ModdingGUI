using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Drawing;
using System.Threading;
using System.Linq;
using System.Security.Cryptography;
using System.IO;
using System.Windows.Forms;
using System.Diagnostics;

namespace ModdingGUI
{
    public partial class frmMain : Form
    {
        // Class-level Random object initialized with the seed
        private Random random;

        // Log buffer to collect log messages
        private List<(string message, Color color)> randomizerLogBuffer = new List<(string, Color)>();

        // Define colors for logging
        private readonly Color WarningColor = Color.Orange;

        // Blacklisted classes
        string[] blacklistedClasses = new string[]
        {
            "BeastAir",
            "BeastDarkAir",
            "BeastDarkEarth",
            "BeastDarkFire",
            "BeastDarkWater",
            "BeastEarth",
            "BeastFire",
            "BeastWater",
            "BeastAirGreater",
            "BeastDarkAirGreater",
            "BeastDarkEarthGreater",
            "BeastDarkFireGreater",
            "BeastDarkWaterGreater",
            "BeastEarthGreater",
            "BeastFireGreater",
            "BeastWaterGreater",
            "BearGreater",
            "BerserkerEnraged",
            "BerserkerEnragedF",
            "Boss",
            "CatGreater",
            "CitizenExp",
            "CitizenExpF",
            "CitizenImp",
            "CitizenImpF",
            "CitizenNor",
            "CitizenNorF",
            "CitizenSte",
            "CitizenSteF",
            "CyclopsGreater",
            "DarkGod",
            "DarkGodDragon",
            "DarkGodDragon1",
            "DarkGodDragon2",
            "DarkGodDragon3",
            "DarkGodFace",
            "DarkGodKnight",
            "DarkGodShiva",
            "Mutuus",
            "MutuusDark",
            "MutuusSenate",
            "Orin",
            "PropBarrel",
            "PropPracticePost",
            "PropStatue",
            "PropTombstone",
            "TitanAir",
            "TitanEarth",
            "TitanFire",
            "TitanWater",
            "Usus",
            "WolfGreater",
            "Jötunn",
            "UrsulaCostumeA",
            "UrsulaCostumeB",
            "ValensCostumeA",
            "ValensCostumeB"
        };

        // Method to initialize Random with the seed from txtSeed
        private void InitializeRandom()
        {
            string seedInput = txtSeed.Text;
            if (string.IsNullOrEmpty(seedInput) || seedInput.Equals("Enter a seed here!"))
            {
                // If no seed is entered, use a default seed
                int defaultSeed = Environment.TickCount;
                random = new Random(defaultSeed);
                AppendRandomizerLog("No seed entered. Using system tick count as seed.", InfoColor);
                AppendRandomizerLog($"Using seed: {defaultSeed}", InfoColor);
            }
            else
            {
                // Convert the alphanumeric seed into an integer seed using a hash function
                int seed = GetDeterministicHashCode(seedInput);
                random = new Random(seed);
                AppendRandomizerLog($"Using seed: \"{seedInput}\" (hash code: {seed})", InfoColor);
            }
        }

        // Method to generate a deterministic hash code from a string
        private int GetDeterministicHashCode(string str)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(str));
                // Use the first 4 bytes to create a 32-bit integer
                int seed = BitConverter.ToInt32(hashBytes, 0);
                return seed;
            }
        }

        // Modified method to collect logs instead of directly appending to the UI
        private void AppendRandomizerLog(string message, Color color)
        {
            randomizerLogBuffer.Add((message, color));
        }

        private async void RandomizeHeroes(string projectFolder)
        {
            // Disable the randomize button
            btnRandomize.Enabled = false;

            // Clear the log buffer
            randomizerLogBuffer.Clear();

            try
            {
                // Initialize the random number generator
                InitializeRandom();

                // Array of main hero names
                string[] heroNames = new string[]
                {
            "Valens",
            "Ursula",
            "Ludo",
            "Urlan",
            "Eiji",
            "Gwazi"
                };

                // Load gladiators, stat sets, item sets, and skill sets
                var gladiatorEntries = ParseGladiators(projectFolder);
                var statSets = ParseStatSets(projectFolder);
                var itemSets = ParseItemSets(projectFolder);
                var skillSets = ParseSkillSets(projectFolder);

                // Load eligible classes from classdefs.tok
                var eligibleClasses = GetEligibleClasses(projectFolder);

                // Filter eligible classes to those that have corresponding gladiator entries
                var classesWithGladiators = eligibleClasses.Where(c => gladiatorEntries.Any(g => g.Class == c)).ToList();

                // Check if there are enough classes
                if (classesWithGladiators.Count < heroNames.Length)
                {
                    AppendRandomizerLog("Not enough classes with gladiator entries to randomize heroes.", ErrorColor);
                    return;
                }

                // Shuffle classes and hero names using the seeded random
                classesWithGladiators.Shuffle(random);
                heroNames = heroNames.OrderBy(x => random.Next()).ToArray();

                // Assign classes to heroes ensuring each hero gets a class with gladiators
                List<string> assignedClasses = new List<string>();
                Dictionary<string, string> heroClassMap = new Dictionary<string, string>();

                for (int i = 0; i < heroNames.Length; i++)
                {
                    string heroName = heroNames[i];
                    bool classAssigned = false;

                    while (classesWithGladiators.Count > 0)
                    {
                        string candidateClass = classesWithGladiators[0];
                        classesWithGladiators.RemoveAt(0);

                        // Check if the class has gladiator entries
                        if (gladiatorEntries.Any(g => g.Class == candidateClass))
                        {
                            assignedClasses.Add(candidateClass);
                            heroClassMap[heroName] = candidateClass;
                            classAssigned = true;
                            break;
                        }
                    }

                    if (!classAssigned)
                    {
                        AppendRandomizerLog($"Could not assign a class with gladiators to hero '{heroName}'.", ErrorColor);
                        return;
                    }
                }

                // Write heroes to the files
                WriteUnitsToFile(projectFolder, heroNames.ToList(), assignedClasses, gladiatorEntries, statSets, itemSets, skillSets, "valensimperia.tok", "Valens");
                WriteUnitsToFile(projectFolder, heroNames.ToList(), assignedClasses, gladiatorEntries, statSets, itemSets, skillSets, "ursulanordagh.tok", "Ursula");

                AppendRandomizerLog("Heroes randomized and saved.", SuccessColor);
                // Add entries for Valens and Ursula to worldmap.tok based on their assigned classes
                if (rbnValens.Checked)
                {
                    if (heroClassMap.TryGetValue("Valens", out string valensClass))
                    {
                        UpdateWorldmapWithCharacter(projectFolder, "Valens", valensClass);
                        AppendRandomizerLog("Valens entry added to worldmap.tok.", SuccessColor);
                    }
                }
                else
                {
                    if (heroClassMap.TryGetValue("Ursula", out string ursulaClass))
                    {
                        UpdateWorldmapWithCharacter(projectFolder, "Ursula", ursulaClass);
                        AppendRandomizerLog("Ursula entry added to worldmap.tok.", SuccessColor);
                    }
                }
                
            }
            finally
            {
                // Output the collected logs if logging is enabled
                if (randomizerLogsMenuItem.Checked)
                {
                    foreach (var logEntry in randomizerLogBuffer)
                    {
                        AppendLog(logEntry.message, logEntry.color, false);
                    }
                }

                // Re-enable the randomize button
                btnRandomize.Enabled = true;
                Thread.Sleep(1000);
                tabContainer.SelectedTab = tabPacking;
                AppendLog("Randomization complete.(Yes it is that fast) Proceed to pack your project!", InfoColor, false);
            }
        }

        private async void RandomizeTeam(string projectFolder)
        {
            // Disable the randomize button
            btnRandomize.Enabled = false;

            // Clear the log buffer
            randomizerLogBuffer.Clear();

            try
            {
                // Initialize the random number generator
                InitializeRandom();

                // Load gladiators, stat sets, item sets, and skill sets
                var gladiatorEntries = ParseGladiators(projectFolder);
                var statSets = ParseStatSets(projectFolder);
                var itemSets = ParseItemSets(projectFolder);
                var skillSets = ParseSkillSets(projectFolder);

                List<string> gladiatorNames = gladiatorEntries.Select(g => g.Name + "_random").ToList();

                if (gladiatorNames.Count < 14)
                {
                    AppendRandomizerLog("Not enough gladiator names available for team randomization.", ErrorColor);
                    return;
                }

                // Shuffle gladiator names
                gladiatorNames.Shuffle(random);

                // Load eligible classes from classdefs.tok
                var eligibleClasses = GetEligibleClasses(projectFolder);

                // Filter eligible classes to those that have corresponding gladiator entries
                var classesWithGladiators = eligibleClasses.Where(c => gladiatorEntries.Any(g => g.Class == c)).ToList();

                if (classesWithGladiators.Count < 14)
                {
                    AppendRandomizerLog("Not enough classes with gladiator entries available for team randomization.", ErrorColor);
                    return;
                }

                // Assign classes to team members ensuring each gets a class with gladiators
                List<string> teamNames = new List<string>();
                List<string> assignedClasses = new List<string>();

                for (int i = 0; i < 14; i++)
                {
                    string gladiatorName = gladiatorNames[i];
                    bool classAssigned = false;

                    while (classesWithGladiators.Count > 0)
                    {
                        string candidateClass = classesWithGladiators[0];
                        classesWithGladiators.RemoveAt(0);

                        // Check if the class has gladiator entries
                        if (gladiatorEntries.Any(g => g.Class == candidateClass))
                        {
                            teamNames.Add(gladiatorName);
                            assignedClasses.Add(candidateClass);
                            classAssigned = true;
                            break;
                        }
                    }

                    if (!classAssigned)
                    {
                        AppendRandomizerLog($"Could not assign a class with gladiators to team member '{gladiatorName}'.", ErrorColor);
                        return;
                    }
                }

                // Append the team to the existing hero files
                AppendUnitsToFile(projectFolder, teamNames, assignedClasses, gladiatorEntries, statSets, itemSets, skillSets, "valensimperia.tok");
                AppendUnitsToFile(projectFolder, teamNames, assignedClasses, gladiatorEntries, statSets, itemSets, skillSets, "ursulanordagh.tok");

                AppendRandomizerLog("Team randomized and saved.", SuccessColor);
            }
            finally
            {
                // Output the collected logs if logging is enabled
                if (randomizerLogsMenuItem.Checked)
                {
                    foreach (var logEntry in randomizerLogBuffer)
                    {
                        AppendLog(logEntry.message, logEntry.color, false);
                    }
                }

                // Re-enable the randomize button
                btnRandomize.Enabled = true;
            }
        }

        private void RemoveAllRecruits(string projectFolder)
        {
            string leaguesPath = Path.Combine(projectFolder, $"{Path.GetFileName(projectFolder)}_BEC", "data", "towns", "leagues");
            var tokFiles = Directory.GetFiles(leaguesPath, "*.tok", SearchOption.AllDirectories);

            AppendRandomizerLog("Starting to remove all recruits...", InfoColor);

            foreach (var tokFile in tokFiles)
            {
                try
                {
                    bool fileModified = false;

                    // Read all lines from the file
                    var lines = File.ReadAllLines(tokFile).ToList();

                    // Filter out lines starting with "RECRUIT"
                    var filteredLines = lines.Where(line => !line.TrimStart().StartsWith("RECRUIT")).ToList();

                    // Check if any lines were removed
                    if (filteredLines.Count != lines.Count)
                    {
                        fileModified = true;
                    }

                    // Rewrite file only if modifications were made
                    if (fileModified)
                    {
                        File.WriteAllLines(tokFile, filteredLines);
                        AppendRandomizerLog($"Recruits removed from file: {tokFile}", SuccessColor);
                    }
                    else
                    {
                        AppendRandomizerLog($"No recruits found in file: {tokFile}", InfoColor);
                    }
                }
                catch (Exception ex)
                {
                    AppendRandomizerLog($"Error processing file {tokFile}: {ex.Message}", ErrorColor);
                }
            }

            AppendRandomizerLog("All recruits have been processed.", SuccessColor);
        }



        private List<string> GetEligibleClasses(string projectFolder)
        {
            string classDefsPath = Path.Combine(projectFolder, $"{Path.GetFileName(projectFolder)}_BEC", "data", "config", "classdefs.tok");
            List<string> eligibleClasses = new List<string>();

            foreach (string line in File.ReadLines(classDefsPath))
            {
                if (line.StartsWith("CREATECLASS:"))
                {
                    string className = line.Split(':')[1].Trim();
                    if (!blacklistedClasses.Contains(className))
                    {
                        eligibleClasses.Add(className);
                    }
                }
            }
            return eligibleClasses;
        }

        // Method to add CHARACTER entries to worldmap.tok for specified heroes
        private void UpdateWorldmapWithCharacter(string projectFolder, string heroName, string assignedClass)
        {
            string worldmapPath = Path.Combine(projectFolder, $"{Path.GetFileName(projectFolder)}_BEC", "data", "config", "worldmap.tok");

            // Check if worldmap.tok exists; if not, create it
            List<string> worldmapLines = File.Exists(worldmapPath) ? File.ReadAllLines(worldmapPath).ToList() : new List<string>();

            // Construct the CHARACTER line
            string characterLine = $"CHARACTER \"{heroName}\" \"{assignedClass}\" \"idlePassive\" \"moveWalk\" \"moveRun\"";

            // Append the CHARACTER line to the worldmap file
            worldmapLines.Add(characterLine);

            // Write the modified content back to worldmap.tok
            File.WriteAllLines(worldmapPath, worldmapLines);

            AppendRandomizerLog($"Added CHARACTER entry for {heroName} with class {assignedClass} to worldmap.tok.", InfoColor);
        }

        private void WriteUnitsToFile(string projectFolder, List<string> unitNames, List<string> unitClasses, List<GladiatorEntry> gladiatorEntries, Dictionary<int, List<int>> statSets, Dictionary<int, List<string>> itemSets, Dictionary<int, List<string>> skillSets, string fileName, string heroName)
        {
            string filePath = Path.Combine(projectFolder, $"{Path.GetFileName(projectFolder)}_BEC", "data", "school", fileName);
            List<string> outputLines = new List<string> { $"NAME: \"Random's School\"\nHERO: \"{heroName}\"\nGOLD: 2500" };

            // Add CREATEUNIT blocks for each hero
            AppendUnitBlocks(outputLines, unitNames, unitClasses, gladiatorEntries, statSets, itemSets, skillSets);

            // Write to file
            File.WriteAllLines(filePath, outputLines);
            AppendRandomizerLog($"Randomized units written to {filePath}", SuccessColor);
        }

        private void AppendUnitsToFile(string projectFolder, List<string> unitNames, List<string> unitClasses, List<GladiatorEntry> gladiatorEntries, Dictionary<int, List<int>> statSets, Dictionary<int, List<string>> itemSets, Dictionary<int, List<string>> skillSets, string fileName)
        {
            string filePath = Path.Combine(projectFolder, $"{Path.GetFileName(projectFolder)}_BEC", "data", "school", fileName);
            List<string> outputLines = new List<string>();

            // Load existing content to avoid overwriting
            if (File.Exists(filePath))
            {
                outputLines.AddRange(File.ReadAllLines(filePath));
            }

            // Append CREATEUNIT blocks for each team member
            AppendUnitBlocks(outputLines, unitNames, unitClasses, gladiatorEntries, statSets, itemSets, skillSets);

            // Write back to file
            File.WriteAllLines(filePath, outputLines);
            AppendRandomizerLog($"Team units appended to {filePath}", SuccessColor);
        }

        private void AppendUnitBlocks(List<string> outputLines, List<string> unitNames, List<string> unitClasses, List<GladiatorEntry> gladiatorEntries, Dictionary<int, List<int>> statSets, Dictionary<int, List<string>> itemSets, Dictionary<int, List<string>> skillSets)
        {
            for (int i = 0; i < unitNames.Count; i++)
            {
                string unitName = unitNames[i];
                string unitClass = unitClasses[i];

                // Get gladiator entries of the class
                var gladiatorsOfClass = gladiatorEntries.Where(g => g.Class == unitClass).ToList();
                if (gladiatorsOfClass.Count == 0)
                {
                    AppendRandomizerLog($"No gladiator entries found for class '{unitClass}'. Skipping unit '{unitName}'.", WarningColor);
                    continue;
                }

                // Select a random gladiator of the class
                var gladiator = gladiatorsOfClass[random.Next(gladiatorsOfClass.Count)];

                var stats = statSets.GetValueOrDefault(gladiator.StatSet, new List<int> { 0, 0, 0, 0, 0 });
                var items = itemSets.GetValueOrDefault(gladiator.ItemSet, new List<string> { "", "", "", "", "" });
                var skills = skillSets.GetValueOrDefault(gladiator.SkillSet, new List<string>());

                // Add CREATEUNIT block
                outputLines.Add($"CREATEUNIT: \"{unitName}\", \"{unitClass}\"    // Name, class");
                outputLines.Add("\tLEVEL: 1");
                outputLines.Add("\tEXPERIENCE: 0");
                outputLines.Add("\tJOBPOINTS: 5");
                outputLines.Add("\tCUSTOMIZE: 0, \"\"");

                // Insert stats
                outputLines.Add("\t//            CON, PWR, ACC, DEF, INT");
                outputLines.Add($"\tCORESTATSCOMP2: {string.Join(", ", stats)}");

                // Insert items
                outputLines.Add("\t//	weapon,	armor,	shield,	helmet,	accessory");
                outputLines.Add($"\tITEMSCOMP: \"{string.Join("\", \"", items)}\"");

                // Insert skills
                if (skills.Count > 0)
                {
                    outputLines.Add("\t// basic skills");
                    foreach (var skill in skills)
                    {
                        outputLines.Add($"\tSKILL: \"{skill}\"");
                    }
                }
            }
        }

        private List<GladiatorEntry> ParseGladiators(string projectFolder)
        {
            string filePath = Path.Combine(projectFolder, $"{Path.GetFileName(projectFolder)}_BEC", "data", "units", "gladiators.txt");
            List<GladiatorEntry> entries = new List<GladiatorEntry>();

            if (File.Exists(filePath))
            {
                string[] lines = File.ReadAllLines(filePath);
                GladiatorEntry entry = null;

                foreach (var line in lines)
                {
                    if (line.StartsWith("Name: "))
                    {
                        entry = new GladiatorEntry { Name = line.Split(": ")[1] };
                    }
                    else if (line.StartsWith("Class: ") && entry != null)
                    {
                        entry.Class = line.Split(": ")[1];
                    }
                    else if (line.StartsWith("Outfit: ") && entry != null)
                    {
                        entry.Outfit = int.Parse(line.Split(": ")[1]);
                    }
                    else if (line.StartsWith("Tint: ") && entry != null)
                    {
                        entry.TintSet = int.Parse(line.Split(": ")[1]);
                    }
                    else if (line.StartsWith("Skill set: ") && entry != null)
                    {
                        entry.SkillSet = int.Parse(line.Split(": ")[1]);
                    }
                    else if (line.StartsWith("Stat set: ") && entry != null)
                    {
                        entry.StatSet = int.Parse(line.Split(": ")[1]);
                    }
                    else if (line.StartsWith("Item set: ") && entry != null)
                    {
                        entry.ItemSet = int.Parse(line.Split(": ")[1]);
                        entries.Add(entry);
                        entry = null;
                    }
                }
            }
            return entries;
        }

        private Dictionary<int, List<int>> ParseStatSets(string projectFolder)
        {
            string filePath = Path.Combine(projectFolder, $"{Path.GetFileName(projectFolder)}_BEC", "data", "units", "statsets.txt");
            Dictionary<int, List<int>> statSets = new Dictionary<int, List<int>>();

            if (File.Exists(filePath))
            {
                string[] lines = File.ReadAllLines(filePath);
                int currentStatSet = -1;

                foreach (var line in lines)
                {
                    if (line.StartsWith("Statset "))
                    {
                        currentStatSet = int.Parse(line.Split(" ")[1].Trim(':'));
                    }
                    else if (currentStatSet >= 0 && line.StartsWith("1:"))
                    {
                        var stats = line.Substring(2).Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();
                        statSets[currentStatSet] = stats;
                        currentStatSet = -1;
                    }
                }
            }
            return statSets;
        }

        private Dictionary<int, List<string>> ParseItemSets(string projectFolder)
        {
            string filePath = Path.Combine(projectFolder, $"{Path.GetFileName(projectFolder)}_BEC", "data", "units", "itemsets.txt");
            Dictionary<int, List<string>> itemSets = new Dictionary<int, List<string>>();

            if (!File.Exists(filePath))
            {
                return itemSets;
            }

            string[] lines = File.ReadAllLines(filePath);
            int currentItemSet = -1;
            List<string> itemsForSet = new List<string> { "", "", "", "", "" }; // Prepare placeholders

            foreach (var line in lines)
            {
                if (line.StartsWith("Itemset "))
                {
                    // Save the previous item set before starting a new one
                    if (currentItemSet >= 0)
                    {
                        itemSets[currentItemSet] = new List<string>(itemsForSet);
                    }

                    // Begin a new item set
                    currentItemSet = int.Parse(line.Split(" ")[1].Trim(':'));
                    itemsForSet = new List<string> { "", "", "", "", "" };
                    continue;
                }

                if (currentItemSet < 0)
                {
                    continue;
                }

                // Identify item type in square brackets and map it to the correct index
                var match = Regex.Match(line, @"\[\w+\]");
                if (!match.Success)
                {
                    continue;
                }

                int index = match.Value switch
                {
                    "[Weapon]" => 0,
                    "[Armor]" => 1,
                    "[Shield]" => 2,
                    "[Helmet]" => 3,
                    "[Accessory]" => 4,
                    _ => -1
                };

                // Skip if index is invalid or the slot is already filled
                if (index < 0 || !string.IsNullOrEmpty(itemsForSet[index]))
                {
                    continue;
                }

                // Extract item name, removing leading numbers and brackets
                string itemName = Regex.Replace(line, @"^\d+\s+\d+\s+|\s*\[.*?\]", "").Trim();
                itemsForSet[index] = itemName;
            }

            // Save the last item set
            if (currentItemSet >= 0)
            {
                itemSets[currentItemSet] = new List<string>(itemsForSet);
            }

            return itemSets;
        }

        private Dictionary<int, List<string>> ParseSkillSets(string projectFolder)
        {
            string filePath = Path.Combine(projectFolder, $"{Path.GetFileName(projectFolder)}_BEC", "data", "units", "skillsets.txt");
            Dictionary<int, List<string>> skillSets = new Dictionary<int, List<string>>();

            if (File.Exists(filePath))
            {
                string[] lines = File.ReadAllLines(filePath);
                int currentSkillSet = -1;

                foreach (var line in lines)
                {
                    if (line.StartsWith("Skillset "))
                    {
                        currentSkillSet = int.Parse(line.Split(" ")[1].Trim(':'));
                    }
                    else if (currentSkillSet >= 0 && line.StartsWith("0 "))
                    {
                        // Extract the skill name (everything after "0 30 ")
                        int index = line.IndexOf("0 30 ");
                        if (index >= 0)
                        {
                            string skillName = line.Substring(index + 5).Trim();
                            if (!skillSets.ContainsKey(currentSkillSet))
                            {
                                skillSets[currentSkillSet] = new List<string>();
                            }
                            skillSets[currentSkillSet].Add(skillName);
                        }
                    }
                }
            }
            return skillSets;
        }

        // Precompile the regex patterns
        private static readonly Regex teamZeroRegex = new Regex(@"^TEAM:\s*0\s*,\s*0\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex teamRegex = new Regex(@"^TEAM:", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex unitDbRegex = new Regex(@"^UNITDB:", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex unitDbLineRegex = new Regex(@"UNITDB:\s*""(.*?)""\s*,\s*99\s*,\s*""(.*?)""\s*,", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Precompile the new regex pattern
        private static readonly Regex teamLineRegex = new Regex(@"^TEAM:\s*(\d+)\s*,\s*(\d+)\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private void EditEncounterFiles(string projectFolder, bool addCandie)
        {
            // Disable the randomize button
            btnRandomize.Enabled = false;

            // Clear the log buffer
            randomizerLogBuffer.Clear();

            string encountersPath = Path.Combine(projectFolder, $"{Path.GetFileName(projectFolder)}_BEC", "data", "encounters");
            var encFiles = Directory.GetFiles(encountersPath, "*.enc", SearchOption.AllDirectories);

            pgbRandomizeStatus.Minimum = 0;
            pgbRandomizeStatus.Maximum = encFiles.Length;
            pgbRandomizeStatus.Value = 0;

            AppendRandomizerLog("Starting to edit encounter files...", InfoColor);

            // Use ConcurrentQueue for thread-safe logging
            ConcurrentQueue<(string message, Color color)> logMessages = new ConcurrentQueue<(string, Color)>();

            int filesProcessed = 0;

            // Define a progress reporter
            var progress = new Progress<int>(value =>
            {
                pgbRandomizeStatus.Value = value;
            });
            try
            {
                // Process files sequentially for debugging purposes
                foreach (var encFile in encFiles)
                {
                    try
                    {
                        bool fileModified = false;
                        bool inTeam0Section = false;
                        bool candieExists = false;

                        // Read all lines at once
                        var allLines = File.ReadAllLines(encFile).ToList();

                        // Check if CANDIE exists
                        candieExists = allLines.Any(line => line.Trim().Equals("CANDIE:", StringComparison.OrdinalIgnoreCase));

                        // Use StringBuilder for modified lines
                        StringBuilder modifiedContent = new StringBuilder();

                        for (int i = 0; i < allLines.Count; i++)
                        {
                            string line = allLines[i];
                            string trimmedLine = line.Trim();

                            // Check for TEAM line
                            var teamMatch = teamLineRegex.Match(trimmedLine);
                            if (teamMatch.Success)
                            {
                                int teamNumber = int.Parse(teamMatch.Groups[1].Value);

                                if (teamNumber == 0)
                                {
                                    // Before adding TEAM: 0,X, add CANDIE: if needed
                                    if (addCandie && !candieExists)
                                    {
                                        modifiedContent.AppendLine("CANDIE:");
                                        candieExists = true;
                                        fileModified = true;
                                    }

                                    modifiedContent.AppendLine(line); // Add TEAM: 0,X
                                    inTeam0Section = true;
                                }
                                else
                                {
                                    modifiedContent.AppendLine(line);
                                    inTeam0Section = false;
                                }
                                continue;
                            }

                            // Modify UNITDB lines within TEAM: 0,X section
                            if (inTeam0Section && unitDbRegex.IsMatch(trimmedLine))
                            {
                                // Update the regex to capture the StartX value
                                var match = Regex.Match(trimmedLine, @"UNITDB:\s*""([^""]*)""\s*,\s*(\d+)\s*,\s*""([^""]+)""\s*,(.*)", RegexOptions.IgnoreCase);

                                if (match.Success)
                                {
                                    string startPosition = match.Groups[3].Value; // The "StartX" value

                                    // Construct the new UNITDB line using the extracted startPosition
                                    string newUnitDbLine = $"UNITDB:\t\"\", 99, \"{startPosition}\", 0, 0, 0, 0, -1, \"\", \"\", \"\", \"\", 0, 0, 0, 0, 0, 0, 0, 0, 0";

                                    modifiedContent.AppendLine(newUnitDbLine);
                                    fileModified = true;
                                }
                                else
                                {
                                    // If the line doesn't match, keep it as is
                                    modifiedContent.AppendLine(line);
                                }
                                continue;
                            }

                            // Add the line
                            modifiedContent.AppendLine(line);
                        }

                        // Write back the modified content if changes were made
                        if (fileModified)
                        {
                            File.WriteAllText(encFile, modifiedContent.ToString());
                            // Enqueue logs
                            logMessages.Enqueue(($"Updated file: {encFile}", SuccessColor));
                        }
                        else
                        {
                            logMessages.Enqueue(($"No changes needed for file: {encFile}", InfoColor));
                        }
                    }
                    catch (Exception ex)
                    {
                        logMessages.Enqueue(($"Error processing file {encFile}: {ex.Message}", ErrorColor));
                    }
                    finally
                    {
                        int processed = Interlocked.Increment(ref filesProcessed);
                        ((IProgress<int>)progress).Report(processed);
                    }
                }
            }
            catch (Exception ex)
            {
                logMessages.Enqueue(($"Error during processing: {ex.Message}", ErrorColor));
            }

            // After processing is complete, update the progress bar and display logs
            this.Invoke((Action)(() =>
            {
                pgbRandomizeStatus.Value = pgbRandomizeStatus.Maximum;

                // Dequeue and collect log messages
                while (logMessages.TryDequeue(out var logMessage))
                {
                    randomizerLogBuffer.Add(logMessage);
                }

                AppendRandomizerLog("All encounter files have been processed.", SuccessColor);

                // Output the collected logs if logging is enabled
                if (randomizerLogsMenuItem.Checked)
                {
                    foreach (var logEntry in randomizerLogBuffer)
                    {
                        AppendLog(logEntry.message, logEntry.color, false);
                    }
                }

                // Re-enable the randomize button
                btnRandomize.Enabled = true;
            }));
        }
    }

    // Extension method for shuffling lists using the Random object
    public static class ListExtensions
    {
        public static void Shuffle<T>(this IList<T> list, Random rng)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = rng.Next(n + 1);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }
    }

    // GladiatorEntry class definition
    public class GladiatorEntry
    {
        public string Name { get; set; }
        public string Class { get; set; }
        public int ItemSet { get; set; }
        public int SkillSet { get; set; }
        public int TintSet { get; set; }
        public int Outfit { get; set; }
        public int StatSet { get; set; }
    }
}