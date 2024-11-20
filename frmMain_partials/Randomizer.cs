using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace ModdingGUI
{
    public partial class frmMain
    {
        // Class-level Random object initialized with the seed
        private Random random;

        // Log buffer to collect log messages
        private List<(string message, Color color)> randomizerLogBuffer = new List<(string, Color)>();

        // Define colors for logging
        private readonly Color WarningColor = Color.Orange;

        // Blacklisted Vanilla classes
        string[] blacklistedVanillaClasses = new string[]
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
            "UrsulaCostumeA",
            "UrsulaCostumeB",
            "ValensCostumeA",
            "ValensCostumeB",
            "Nephilia"
        };
        //blacklisted classes from Leonarth's mod
        string[] blacklistedLeonarthClasses = new string[]
        {
            "BeastAirGreater",
            "BeastDarkAirGreater",
            "BeastDarkEarthGreater",
            "BeastDarkFireGreater",
            "BeastDarkWaterGreater",
            "BeastEarthGreater",
            "BeastFireGreater",
            "BeastWaterGreater",
            "BerserkerEnraged",
            "BerserkerEnragedF",
            "Boss",
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
            "Usus"
        };


        // Blacklisted Ragnarok classes
        string[] blacklistedRagnarokClasses = new string[]
        {
            
            "BeastAirGreater",
            "BeastDarkAirGreater",
            "BeastDarkEarthGreater",
            "BeastDarkFireGreater",
            "BeastDarkWaterGreater",
            "BeastEarthGreater",
            "BeastFireGreater",
            "BeastWaterGreater",
            "BerserkerEnraged",
            "BerserkerEnragedF",
            "Boss",
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
            "MutuusDark",
            "PropBarrel",
            "PropPracticePost",
            "PropStatue",
            "PropTombstone",
            "TitanAir",
            "TitanEarth",
            "TitanFire",
            "TitanWater",
            "Jötunn",
            "UrsulaCostumeA",
            "UrsulaCostumeB",
            "ValensCostumeA",
            "ValensCostumeB"
        };

        string[] blacklistedOpenedClasses = new string[]
        {

            "BeastAirGreater",
            "BeastDarkAirGreater",
            "BeastDarkEarthGreater",
            "BeastDarkFireGreater",
            "BeastDarkWaterGreater",
            "BeastEarthGreater",
            "BeastFireGreater",
            "BeastWaterGreater",
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
            "PropBarrel",
            "PropPracticePost",
            "PropStatue",
            "PropTombstone",
            "TitanAir",
            "TitanEarth",
            "TitanFire",
            "TitanWater",
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
            projectFolder = projectFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
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
                //btnRandomize.Enabled = true;
            }
        }

        private async void RandomizeTeam(string projectFolder)
        {
            projectFolder = projectFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
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

                // Generate a list of gladiator names with a "_random" suffix
                List<string> gladiatorNames = gladiatorEntries.Select(g => g.Name + "_random").ToList();

                if (gladiatorNames.Count < 14)
                {
                    AppendRandomizerLog("Not enough gladiator names available for team randomization.", ErrorColor);
                    return;
                }

                // Shuffle gladiator names to randomize selection
                gladiatorNames.Shuffle(random);
                AppendRandomizerLog("Gladiator names shuffled.", InfoColor);

                // Load eligible classes from classdefs.tok
                var eligibleClasses = GetEligibleClasses(projectFolder);

                // Filter eligible classes to those that have corresponding gladiator entries
                var classesWithGladiators = eligibleClasses.Where(c => gladiatorEntries.Any(g => g.Class == c)).ToList();

                if (classesWithGladiators.Count < 1)
                {
                    AppendRandomizerLog("No eligible classes with gladiator entries available for team randomization.", ErrorColor);
                    return;
                }

                // **Shuffle classesWithGladiators to ensure randomness**
                classesWithGladiators.Shuffle(random);
                AppendRandomizerLog("Eligible classes shuffled.", InfoColor);

                // Assign classes to team members allowing repeats
                List<string> teamNames = new List<string>();
                List<string> assignedClasses = new List<string>();

                for (int i = 0; i < 14; i++)
                {
                    string gladiatorName = gladiatorNames[i];

                    // **Randomly select a class with replacement**
                    string candidateClass = classesWithGladiators[random.Next(classesWithGladiators.Count)];

                    // Assign the selected class to the gladiator
                    teamNames.Add(gladiatorName);
                    assignedClasses.Add(candidateClass);

                    AppendRandomizerLog($"Assigned class '{candidateClass}' to team member '{gladiatorName}'.", InfoColor);
                }

                // Append the team to the existing hero files
                AppendUnitsToFile(projectFolder, teamNames, assignedClasses, gladiatorEntries, statSets, itemSets, skillSets, "valensimperia.tok");
                AppendUnitsToFile(projectFolder, teamNames, assignedClasses, gladiatorEntries, statSets, itemSets, skillSets, "ursulanordagh.tok");

                AppendRandomizerLog("Team randomized and saved.", SuccessColor);
            }
            catch (Exception ex)
            {
                AppendRandomizerLog($"Error during team randomization: {ex.Message}", ErrorColor);
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
            }
        }


        private void RemoveAllRecruits(string projectFolder, IProgress<int> progress, ConcurrentQueue<(string message, Color color)> logMessages)
        {
            projectFolder = projectFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string leaguesPath = Path.Combine(projectFolder, $"{Path.GetFileName(projectFolder)}_BEC", "data", "towns", "leagues");
            var tokFiles = Directory.GetFiles(leaguesPath, "*.tok", SearchOption.AllDirectories);

            int filesProcessed = 0;

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
                        logMessages.Enqueue(($"Recruits removed from file: {tokFile}", SuccessColor));
                    }
                    else
                    {
                        logMessages.Enqueue(($"No recruits found in file: {tokFile}", InfoColor));
                    }
                }
                catch (Exception ex)
                {
                    logMessages.Enqueue(($"Error processing file {tokFile}: {ex.Message}", ErrorColor));
                }
                finally
                {
                    int processed = Interlocked.Increment(ref filesProcessed);
                    progress.Report(processed);
                }
            }

            // All processing is done; any final actions can be handled after Task.Run completes
        }
        private List<string> GetEligibleClasses(string projectFolder)
        {
            projectFolder = projectFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string classDefsPath = Path.Combine(projectFolder, $"{Path.GetFileName(projectFolder)}_BEC", "data", "config", "classdefs.tok");
            List<string> eligibleClasses = new List<string>();
            string[] blacklistedClasses = null;
            if (rbnLeonarths.Checked)
            {
                blacklistedClasses = blacklistedLeonarthClasses;
            }
            if(rbnRagnaroks.Checked)
            {
                blacklistedClasses = blacklistedRagnarokClasses;
            }
            if (rbnVanilla.Checked)
            {
                blacklistedClasses = blacklistedVanillaClasses;
            }
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
            projectFolder = projectFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
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
            projectFolder = projectFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
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
            projectFolder = projectFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
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
            projectFolder = projectFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
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
            projectFolder = projectFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
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
            projectFolder = projectFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
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
        private static readonly Regex unitDbRegex = new Regex(@"^UNITDB:", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex teamLineRegex = new Regex(@"^TEAM:\s*(\d+)\s*,\s*(\d+)\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private void EditEncounterFiles(
    string projectFolder,
    bool addCandie,
    IProgress<int> progress,
    ConcurrentQueue<(string message, Color color)> logMessages)
        {
            try
            {
                projectFolder = projectFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string encountersPath = Path.Combine(projectFolder, $"{Path.GetFileName(projectFolder)}_BEC", "data", "encounters");
                var encFiles = Directory.GetFiles(encountersPath, "*.enc", SearchOption.AllDirectories);

                int totalFiles = encFiles.Length;
                int filesProcessed = 0;

                foreach (var encFile in encFiles)
                {
                    try
                    {
                        bool fileModified = false;
                        bool inTeam0Section = false;
                        bool candieExists = false;

                        // Read all lines from the encounter file
                        var allLines = File.ReadAllLines(encFile).ToList();

                        // Check if "CANDIE:" already exists
                        candieExists = allLines.Any(line => line.Trim().Equals("CANDIE:", StringComparison.OrdinalIgnoreCase));

                        // Use StringBuilder to build modified content
                        StringBuilder modifiedContent = new StringBuilder();

                        foreach (var line in allLines)
                        {
                            string trimmedLine = line.Trim();

                            // Detect TEAM lines
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
                                // Extract the StartX value using regex
                                var match = Regex.Match(trimmedLine, @"UNITDB:\s*""([^""]*)""\s*,\s*(\d+)\s*,\s*""([^""]+)""\s*,(.*)", RegexOptions.IgnoreCase);

                                if (match.Success)
                                {
                                    string startPosition = match.Groups[3].Value; // Extracted StartX value

                                    // Construct the new UNITDB line
                                    string newUnitDbLine = $"UNITDB:\t\"\", 99, \"{startPosition}\", 0, 0, 0, 0, -1, \"\", \"\", \"\", \"\", 0, 0, 0, 0, 0, 0, 0, 0, 0";

                                    modifiedContent.AppendLine(newUnitDbLine);
                                    fileModified = true;
                                }
                                else
                                {
                                    // If regex does not match, retain the original line
                                    modifiedContent.AppendLine(line);
                                }
                                continue;
                            }

                            // Retain all other lines as-is
                            modifiedContent.AppendLine(line);
                        }

                        // If modifications were made, write back to the file
                        if (fileModified)
                        {
                            File.WriteAllText(encFile, modifiedContent.ToString());
                            logMessages.Enqueue(($"Updated file: {encFile}", SuccessColor));
                        }
                        else
                        {
                            logMessages.Enqueue(($"No changes needed for file: {encFile}", InfoColor));
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log any errors encountered while processing a file
                        logMessages.Enqueue(($"Error processing file {encFile}: {ex.Message}", ErrorColor));
                    }
                    finally
                    {
                        // Increment the count of processed files and report progress
                        filesProcessed++;
                        progress.Report(filesProcessed);
                    }
                }

                // After processing all files, log completion
                logMessages.Enqueue(("All encounter files have been processed.", SuccessColor));
            }
            catch (Exception ex)
            {
                // Log any unexpected errors during the process
                logMessages.Enqueue(($"Unexpected error: {ex.Message}", ErrorColor));
            }
        }
        // Add this method inside the frmMain partial class in RANDOMIZER.cs

        private enum ModificationType
        {
            ReplaceCondition,
            // Future modification types can be added here
        }

        private class Modification
        {
            public ModificationType Type { get; set; }
            public string Original { get; set; }
            public string Replacement { get; set; }
        }

        private async Task EditScpFilesAndCompileAsync(string projectFolder)
        {
            projectFolder = projectFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string scriptFolder = Path.Combine(projectFolder, $"{Path.GetFileName(projectFolder)}_BEC", "data", "script");
            string binFolder = Path.Combine(scriptFolder, "bin");

            // Ensure the bin directory exists
            if (!Directory.Exists(binFolder))
            {
                MessageBox.Show("The bin folder does not exist. Please unpack your project and check project/project_BEC/data/script/bin");
                AppendRandomizerLog($"No bin folder at: {binFolder}", InfoColor);
                return;
            }

            // List of .scp files to process with their specific modification requirements
            var scpFiles = new List<(string FileName, List<Modification> Mods, List<string> LinesToRemove)>
    {
        // Original four files with conditional modification and line removals
        ("gameflowvalenssteppes.scp", new List<Modification>
            {
                new Modification
                {
                    Type = ModificationType.ReplaceCondition,
                    Original = "if (GetIntVar(\"CostumeChange\") != 1)",
                    Replacement = "if (GetIntVar(\"CostumeChange\") != 0)"
                }
            },
            new List<string>
            {
                "gb.SchoolRemoveCharacter(\"Ludo\")",
                "gb.SchoolRemoveCharacter(\"Gwazi\")"
            }
        ),
        ("gameflowvalensexpanse.scp", new List<Modification>
            {
                new Modification
                {
                    Type = ModificationType.ReplaceCondition,
                    Original = "if (GetIntVar(\"CostumeChange\") != 1)",
                    Replacement = "if (GetIntVar(\"CostumeChange\") != 0)"
                }
            },
            new List<string>
            {
                "gb.SchoolRemoveCharacter(\"Ludo\")",
                "gb.SchoolRemoveCharacter(\"Gwazi\")"
            }
        ),
        ("gameflowursulasteppes.scp", new List<Modification>
            {
                new Modification
                {
                    Type = ModificationType.ReplaceCondition,
                    Original = "if (GetIntVar(\"CostumeChange\") != 1)",
                    Replacement = "if (GetIntVar(\"CostumeChange\") != 0)"
                }
            },
            new List<string>
            {
                "gb.SchoolRemoveCharacter(\"Ludo\")",
                "gb.SchoolRemoveCharacter(\"Gwazi\")"
            }
        ),
        ("gameflowursulaexpanse.scp", new List<Modification>
            {
                new Modification
                {
                    Type = ModificationType.ReplaceCondition,
                    Original = "if (GetIntVar(\"CostumeChange\") != 1)",
                    Replacement = "if (GetIntVar(\"CostumeChange\") != 0)"
                }
            },
            new List<string>
            {
                "gb.SchoolRemoveCharacter(\"Ludo\")",
                "gb.SchoolRemoveCharacter(\"Gwazi\")"
            }
        ),
        // Additional two endgame files with two conditional modifications each
        ("gameflowvalensendgame.scp", new List<Modification>
            {
                new Modification
                {
                    Type = ModificationType.ReplaceCondition,
                    Original = "if (GetIntVar(\"EndGameCostumeChangeAgain\") != 1)",
                    Replacement = "if (GetIntVar(\"EndGameCostumeChangeAgain\") != 0)"
                },
                new Modification
                {
                    Type = ModificationType.ReplaceCondition,
                    Original = "if (GetIntVar(\"EndGameCostumeChange\") != 1)",
                    Replacement = "if (GetIntVar(\"EndGameCostumeChange\") != 0)"
                }
            },
            new List<string>() // No lines to remove
        ),
        ("gameflowursulaendgame.scp", new List<Modification>
            {
                new Modification
                {
                    Type = ModificationType.ReplaceCondition,
                    Original = "if (GetIntVar(\"EndGameCostumeChangeAgain\") != 1)",
                    Replacement = "if (GetIntVar(\"EndGameCostumeChangeAgain\") != 0)"
                },
                new Modification
                {
                    Type = ModificationType.ReplaceCondition,
                    Original = "if (GetIntVar(\"EndGameCostumeChange\") != 1)",
                    Replacement = "if (GetIntVar(\"EndGameCostumeChange\") != 0)"
                }
            },
            new List<string>() // No lines to remove
        )
    };

            // Path to the compiler executable
            string toolsPath = NormalizePath(Path.Combine(Directory.GetCurrentDirectory(), "tools"));
            string compilerPath = Path.Combine(toolsPath, "DOGCodeCompiler.exe");

            if (!File.Exists(compilerPath))
            {
                AppendLog($"Compiler not found at: {compilerPath}\nPlease use the tools folder from the latest release!", ErrorColor);
                return;
            }

            // Prepare batch commands
            StringBuilder batchCommands = new StringBuilder();

            foreach (var (FileName, Mods, LinesToRemove) in scpFiles)
            {
                string scpFilePath = Path.Combine(scriptFolder, FileName);

                AppendRandomizerLog($"Processing file: {scpFilePath}", InfoColor);

                if (!File.Exists(scpFilePath))
                {
                    //AppendRandomizerLog($"File not found: {scpFilePath}", WarningColor);
                    MessageBox.Show($"File not found: {scpFilePath}");
                    return;
                }

                try
                {
                    bool fileModified = false;
                    var lines = File.ReadAllLines(scpFilePath).ToList();

                    // Apply modifications
                    foreach (var mod in Mods)
                    {
                        if (mod.Type == ModificationType.ReplaceCondition)
                        {
                            bool conditionFound = false;
                            for (int i = 0; i < lines.Count; i++)
                            {
                                if (lines[i].Contains(mod.Original))
                                {
                                    lines[i] = lines[i].Replace(mod.Original, mod.Replacement);
                                    fileModified = true;
                                    conditionFound = true;
                                    AppendRandomizerLog($"Modified condition in {FileName}: '{mod.Original}' to '{mod.Replacement}'", InfoColor);
                                }
                            }
                            if (!conditionFound)
                            {
                                AppendRandomizerLog($"Condition '{mod.Original}' not found in {FileName}.", WarningColor);
                            }
                        }
                        // Additional modification types can be handled here if needed
                    }

                    // Remove specified lines
                    foreach (var lineToRemove in LinesToRemove)
                    {
                        int initialCount = lines.Count;
                        lines = lines.Where(line => !line.Trim().Equals(lineToRemove)).ToList();

                        if (lines.Count != initialCount)
                        {
                            fileModified = true;
                            AppendRandomizerLog($"Removed line from {FileName}: {lineToRemove}", InfoColor);
                        }
                    }

                    // Write back if modifications were made
                    if (fileModified)
                    {
                        File.WriteAllLines(scpFilePath, lines);
                        AppendRandomizerLog($"Saved changes to {FileName}", SuccessColor);
                    }
                    else
                    {
                        AppendRandomizerLog($"No changes needed for {FileName}", InfoColor);
                    }

                    // Add compiler command for this file
                    // Determine the output .scb file path
                    string scbFileName = Path.GetFileNameWithoutExtension(scpFilePath) + ".scb";
                    string scbFilePath = Path.Combine(binFolder, scbFileName);

                    // Add compiler command for this file
                    string compileCommand = $"\"{compilerPath}\" \"{scpFilePath}\" \"{scbFilePath}\"";
                    batchCommands.AppendLine(compileCommand);

                }
                catch (Exception ex)
                {
                    AppendRandomizerLog($"Error processing {FileName}: {ex.Message}", ErrorColor);
                }
            }

            // Execute the batch commands if any
            if (batchCommands.Length > 0)
            {
                string batchContent = batchCommands.ToString();
                AppendRandomizerLog("Starting compilation of .scp files...", InfoColor);
                await RunBatchFileAsync(batchContent, scriptFolder, false);
                AppendRandomizerLog("Compilation of .scp files completed.", SuccessColor);
            }
            else
            {
                AppendRandomizerLog("No .scp files were modified. Compilation skipped.", InfoColor);
            }
        }
        private async Task ApplyIngameRandomAsync(string projectFolder)
        {
            try
            {
                // Remove any trailing directory separators from the project folder path
                projectFolder = projectFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                // Define the path to the script folder within the project
                string scriptFolder = Path.Combine(projectFolder, $"{Path.GetFileName(projectFolder)}_BEC", "data", "script");

                // Define the path to the bin folder within the script directory
                string binFolder = Path.Combine(scriptFolder, "bin");

                // Ensure the script directory exists
                if (!Directory.Exists(scriptFolder))
                {
                    AppendRandomizerLog($"Script folder not found at: {scriptFolder}", ErrorColor);
                    return;
                }

                // Ensure the bin directory exists; create it if it doesn't
                if (!Directory.Exists(binFolder))
                {
                    AppendRandomizerLog($"Bin folder not found at: {binFolder}. Creating bin folder.", WarningColor);
                    Directory.CreateDirectory(binFolder);
                }

                // Define the path to the wmglobal.scp file
                string wmGlobalScpPath = Path.Combine(scriptFolder, "wmglobal.scp");

                // Check if wmglobal.scp exists
                if (!File.Exists(wmGlobalScpPath))
                {
                    AppendRandomizerLog($"wmglobal.scp file not found at: {wmGlobalScpPath}", ErrorColor);
                    return;
                }

                AppendRandomizerLog($"Found wmglobal.scp at: {wmGlobalScpPath} for in-game randomization.", InfoColor);

                // Define the lines to add within the OnInit() function
                string[] onInitLinesToAdd = new string[]
                {
            "shared school \t\t= gb.GetSchool()", // Ensure this line is first
            "global math\t\t= InterfaceMath()",
            "shared characterValens \t= school.GetCharacterByName(\"Valens\")",
            "shared characterLudo \t= school.GetCharacterByName(\"Ludo\")",
            "shared characterGwazi \t= school.GetCharacterByName(\"Gwazi\")",
            "shared characterEiji \t= school.GetCharacterByName(\"Eiji\")",
            "shared characterUrsula \t= school.GetCharacterByName(\"Ursula\")",
            "shared characterUrlan \t= school.GetCharacterByName(\"Urlan\")"
                };

                // Define the code block to add within the OnKill() function
                string[] onKillCodeToAdd = new string[]
                {
            "roll = math.RandomInRange(0,3)",
            "if(roll == 0)",
            "\tcharacterValens.ChangeClass(\"Bandit\")",
            "endif",
            "if(roll == 1)",
            "\tcharacterValens.ChangeClass(\"Bear\")",
            "endif",
            "if(roll == 2)",
            "\tcharacterValens.ChangeClass(\"Ogre\")",
            "endif",
            "if(roll == 3)",
            "\tcharacterValens.ChangeClass(\"Mongrel\")",
            "endif"
                };

                // Define the path to the compiler executable
                string toolsPath = NormalizePath(Path.Combine(Directory.GetCurrentDirectory(), "tools"));
                string compilerPath = Path.Combine(toolsPath, "DOGCodeCompiler.exe");

                // Check if the compiler exists
                if (!File.Exists(compilerPath))
                {
                    AppendRandomizerLog($"Compiler not found at: {compilerPath}", ErrorColor);
                    return;
                }

                // Initialize a StringBuilder to accumulate compiler commands
                StringBuilder batchCommands = new StringBuilder();

                string fileName = Path.GetFileName(wmGlobalScpPath);
                AppendRandomizerLog($"Processing wmglobal.scp file: {wmGlobalScpPath}", InfoColor);

                try
                {
                    // Optional: Create a backup of wmglobal.scp if it doesn't already exist
                    string backupScpPath = Path.Combine(scriptFolder, "wmglobal_backup.scp");
                    if (!File.Exists(backupScpPath))
                    {
                        File.Copy(wmGlobalScpPath, backupScpPath);
                        AppendRandomizerLog($"Backup created at: {backupScpPath}", InfoColor);
                    }
                    else
                    {
                        AppendRandomizerLog($"Backup already exists at: {backupScpPath}", InfoColor);
                    }

                    // Read all lines from wmglobal.scp
                    var lines = File.ReadAllLines(wmGlobalScpPath).ToList();
                    bool fileModified = false;

                    // Find the start index of the OnInit function (flexible search)
                    int onInitStart = lines.FindIndex(line => line.Trim().StartsWith("function OnInit", StringComparison.OrdinalIgnoreCase));
                    if (onInitStart == -1)
                    {
                        AppendRandomizerLog($"'function OnInit' not found in {fileName}. Skipping.", WarningColor);
                        // Optionally, you can decide to add the function if it doesn't exist
                        return;
                    }

                    // Find the end index of the OnInit function
                    int onInitEnd = lines.FindIndex(onInitStart, line => line.Trim().Equals("endfunction", StringComparison.OrdinalIgnoreCase));
                    if (onInitEnd == -1)
                    {
                        AppendRandomizerLog($"'endfunction' for 'OnInit' not found in {fileName}. Skipping.", WarningColor);
                        return;
                    }

                    // Insert lines into OnInit in reverse order to maintain desired sequence
                    for (int i = onInitLinesToAdd.Length - 1; i >= 0; i--)
                    {
                        string initLine = onInitLinesToAdd[i];
                        if (!lines.Skip(onInitStart + 1).Take(onInitEnd - onInitStart - 1).Any(l => l.Trim() == initLine))
                        {
                            lines.Insert(onInitEnd, "\t" + initLine); // Insert before 'endfunction'
                            fileModified = true;
                            AppendRandomizerLog($"Added line to OnInit in {fileName}: {initLine}", InfoColor);
                        }
                    }

                    // Find the start index of the OnKill function (flexible search)
                    int onKillStart = lines.FindIndex(line => line.Trim().StartsWith("function OnKill", StringComparison.OrdinalIgnoreCase));
                    if (onKillStart == -1)
                    {
                        AppendRandomizerLog($"'function OnKill' not found in {fileName}. Skipping.", WarningColor);
                        // Optionally, you can decide to add the function if it doesn't exist
                        return;
                    }

                    // Find the end index of the OnKill function
                    int onKillEnd = lines.FindIndex(onKillStart, line => line.Trim().Equals("endfunction", StringComparison.OrdinalIgnoreCase));
                    if (onKillEnd == -1)
                    {
                        AppendRandomizerLog($"'endfunction' for 'OnKill' not found in {fileName}. Skipping.", WarningColor);
                        return;
                    }

                    // Check if the OnKill code already exists
                    bool killCodeExists = onKillCodeToAdd.All(codeLine =>
                        lines.Skip(onKillStart + 1).Take(onKillEnd - onKillStart - 1).Any(l => l.Trim() == codeLine));

                    // Add the OnKill code block if it doesn't already exist
                    if (!killCodeExists)
                    {
                        // Insert the OnKill code before 'endfunction' in reverse order
                        for (int i = onKillCodeToAdd.Length - 1; i >= 0; i--)
                        {
                            lines.Insert(onKillEnd, "\t" + onKillCodeToAdd[i]);
                        }
                        fileModified = true;
                        AppendRandomizerLog($"Added code to OnKill in {fileName}.", InfoColor);
                    }

                    // Write the modified lines back to wmglobal.scp if any changes were made
                    if (fileModified)
                    {
                        File.WriteAllLines(wmGlobalScpPath, lines);
                        AppendRandomizerLog($"Saved changes to {fileName}.", SuccessColor);
                    }
                    else
                    {
                        AppendRandomizerLog($"No changes needed for {fileName}.", InfoColor);
                    }

                    // Determine the output .scb file path
                    string scbFileName = Path.GetFileNameWithoutExtension(wmGlobalScpPath) + ".scb";
                    string scbFilePath = Path.Combine(binFolder, scbFileName);

                    // Construct the compiler command with the correct output file path
                    string compileCommand = $"\"{compilerPath}\" \"{wmGlobalScpPath}\" \"{scbFilePath}\"";
                    batchCommands.AppendLine(compileCommand);
                }
                catch (Exception ex)
                {
                    AppendRandomizerLog($"Error processing {fileName}: {ex.Message}", ErrorColor);
                }

                // Execute the compiler commands if any modifications were made
                if (batchCommands.Length > 0)
                {
                    string batchContent = batchCommands.ToString();
                    AppendRandomizerLog("Starting compilation of in-game randomized wmglobal.scp file...", InfoColor);
                    await RunBatchFileAsync(batchContent, scriptFolder, false);
                    AppendRandomizerLog("Compilation of in-game randomized wmglobal.scp file completed.", SuccessColor);

                    // Validate compilation success
                    string scbFileName = Path.GetFileNameWithoutExtension("wmglobal.scp") + ".scb";
                    string scbFilePath = Path.Combine(binFolder, scbFileName);
                    if (File.Exists(scbFilePath))
                    {
                        AppendRandomizerLog($"Compilation successful: {scbFilePath}", SuccessColor);
                    }
                    else
                    {
                        AppendRandomizerLog($"Compilation failed for: wmglobal.scp", ErrorColor);
                    }
                }
                else
                {
                    AppendRandomizerLog("No in-game randomized wmglobal.scp file was modified. Compilation skipped.", InfoColor);
                }
            }
            catch (Exception ex)
            {
                AppendRandomizerLog($"Unexpected error in ApplyIngameRandomAsync: {ex.Message}", ErrorColor);
            }
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