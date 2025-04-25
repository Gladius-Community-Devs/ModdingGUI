using System.Text;

namespace ModdingGUI
{
    public partial class frmMain
    {
        // Constants for testing
        private const int DEFAULT_TEST_ITERATIONS = 10;
        private int testIterations = DEFAULT_TEST_ITERATIONS;
        private const string TEST_FOLDER_NAME = "tests";
        private const string TEST_SEED_CONSTANT = "TestSeed123";
        private const string TEST_SEED_VARYING = "TestSeed";

        // Cancellation support for testing
        private CancellationTokenSource testingCancellationSource;

        private async void HandleRandomizerTesting()
        {
            // Get the project folder
            string projectFolder = txtRandomizerPath.Text.Trim();
            if (string.IsNullOrEmpty(projectFolder) || !Directory.Exists(projectFolder))
            {
                MessageBox.Show("Please select a valid project folder first.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Create test directory if it doesn't exist
            string testFolder = Path.Combine(projectFolder, TEST_FOLDER_NAME);
            Directory.CreateDirectory(testFolder);

            // Show debug popup
            int userEnteredNumber;
            DialogResult result = ShowTestingDebugPopup(out userEnteredNumber);

            if (result == DialogResult.OK)
            {
                // You can now use userEnteredNumber here
                testIterations = userEnteredNumber;
            }
            else
            {
                return; // User clicked Cancel
            }

            // Initialize cancellation token
            testingCancellationSource = new CancellationTokenSource();
            var cancellationToken = testingCancellationSource.Token;

            try
            {
                btnRandomize.Enabled = false;

                // Configure progress bar for the total number of tests
                // We have 3 test phases (same seed, different seeds, randomness analysis)
                // Each phase runs the specified number of iterations
                int totalSteps = testIterations * 3;
                pgbRandomizeStatus.Minimum = 0;
                pgbRandomizeStatus.Maximum = totalSteps;
                pgbRandomizeStatus.Value = 0;
                int currentProgress = 0;

                // Save current settings to restore afterwards
                var currentSettings = new RandomizerSettings(this);
                var currentRandomizerLogSetting = randomizerLogsMenuItem.Checked;

                // Enable logs for testing
                randomizerLogsMenuItem.Checked = true;

                // Run all tests
                AppendLog($"Starting randomizer testing with {testIterations} iterations per test...", InfoColor, rtbPackOutput);

                // Same seed test
                lblRandomizeStatus.Text = $"Running Same Seed Test (0/{testIterations})...";
                await RunSameSeedTest(projectFolder, testFolder, (iteration) =>
                {
                    currentProgress++;
                    pgbRandomizeStatus.Value = currentProgress;
                    lblRandomizeStatus.Text = $"Running Same Seed Test ({iteration}/{testIterations})...";
                }, cancellationToken);

                // Different seeds test
                lblRandomizeStatus.Text = $"Running Different Seeds Test (0/{testIterations})...";
                await RunDifferentSeedsTest(projectFolder, testFolder, (iteration) =>
                {
                    currentProgress++;
                    pgbRandomizeStatus.Value = currentProgress;
                    lblRandomizeStatus.Text = $"Running Different Seeds Test ({iteration}/{testIterations})...";
                }, cancellationToken);

                // Randomness analysis test
                lblRandomizeStatus.Text = $"Running Randomness Analysis (0/{testIterations})...";
                await AnalyzeRandomnessTest(projectFolder, testFolder, (iteration) =>
                {
                    currentProgress++;
                    pgbRandomizeStatus.Value = currentProgress;
                    lblRandomizeStatus.Text = $"Running Randomness Analysis ({iteration}/{testIterations})...";
                }, cancellationToken);

                // Restore original settings
                currentSettings.ApplyToForm(this);
                randomizerLogsMenuItem.Checked = currentRandomizerLogSetting;

                // Show summary of test results
                MessageBox.Show(
                    "All randomizer tests completed. Results saved to 'tests' folder.",
                    "Testing Complete",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (OperationCanceledException)
            {
                AppendLog("Randomizer testing was cancelled by the user.", WarningColor, rtbPackOutput);
                MessageBox.Show("Randomizer testing was cancelled.", "Testing Cancelled", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                AppendLog($"Error during randomizer testing: {ex.Message}", ErrorColor, rtbPackOutput);
                MessageBox.Show($"Error during randomizer testing: {ex.Message}", "Testing Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnRandomize.Enabled = true;
                testingCancellationSource?.Dispose();
                testingCancellationSource = null;
            }
        }

        private DialogResult ShowTestingDebugPopup(out int userNumber)
        {
            // Create a new form to serve as the custom input dialog
            Form inputForm = new Form
            {
                Text = "Randomizer Testing Mode",
                Width = 450,
                Height = 300,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                StartPosition = FormStartPosition.CenterParent
            };

            // Create the label to display the explanatory message.
            Label lblDescription = new Label
            {
                Text = "You are about to run the Randomizer Testing tool.\n\n" +
                       "This will:\n" +
                       "- Run the randomizer multiple times with various seeds\n" +
                       "- Save test outputs to a 'tests' folder\n" +
                       "- Analyze the randomness of the outputs\n\n" +
                       "- WARNING: large numbers will take a long time!! (>100)\n\n" +
                       "Enter a number for test iterations or click Cancel.",
                AutoSize = true,
                Location = new System.Drawing.Point(10, 10)
            };
            inputForm.Controls.Add(lblDescription);

            // Create a NumericUpDown control for numeric input.
            NumericUpDown numericInput = new NumericUpDown
            {
                Minimum = 1,
                Maximum = 1000,
                Value = DEFAULT_TEST_ITERATIONS, // Use default value of 10
                Location = new System.Drawing.Point(10, lblDescription.Bottom + 10),
                Width = 100
            };
            inputForm.Controls.Add(numericInput);

            // Add a label to explain performance impact
            Label lblPerformance = new Label
            {
                Text = "Note: Each additional iteration increases test time proportionally.\n" +
                       "The progress bar will show completion status during testing.",
                ForeColor = Color.DarkBlue,
                AutoSize = true,
                Location = new System.Drawing.Point(10, numericInput.Bottom + 10)
            };
            inputForm.Controls.Add(lblPerformance);

            // Create an OK button that will close the dialog with DialogResult.OK.
            Button btnOK = new Button
            {
                Text = "OK",
                DialogResult = DialogResult.OK,
                Location = new System.Drawing.Point(10, lblPerformance.Bottom + 20)
            };
            inputForm.Controls.Add(btnOK);

            // Create a Cancel button that will close the dialog with DialogResult.Cancel.
            Button btnCancel = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Location = new System.Drawing.Point(btnOK.Right + 10, lblPerformance.Bottom + 20)
            };
            inputForm.Controls.Add(btnCancel);

            // Set the Accept and Cancel buttons for the form.
            inputForm.AcceptButton = btnOK;
            inputForm.CancelButton = btnCancel;

            // Show the dialog box
            DialogResult result = inputForm.ShowDialog();

            // If OK was pressed, output the number entered; otherwise set userNumber to 0 (or any default you choose)
            if (result == DialogResult.OK)
            {
                userNumber = (int)numericInput.Value;
            }
            else
            {
                userNumber = 0;
            }

            return result;
        }

        private async Task RunSameSeedTest(string projectFolder, string testFolder, Action<int> progressCallback, CancellationToken cancellationToken = default)
        {
            AppendLog("Starting Same Seed Test...", InfoColor, rtbPackOutput);

            // Create test subdirectory
            string testSubFolder = Path.Combine(testFolder, "same_seed_test");
            Directory.CreateDirectory(testSubFolder);

            // Reset randomizer log buffer
            randomizerLogBuffer.Clear();

            // Set fixed test seed
            txtSeed.Text = TEST_SEED_CONSTANT;

            // Results storage
            var testOutputFiles = new List<string>();
            var checksums = new List<string>();

            // Run the randomizer multiple times with the same seed
            for (int i = 0; i < testIterations; i++)
            {
                // Check for cancellation
                cancellationToken.ThrowIfCancellationRequested();

                AppendLog($"Same Seed Test - Iteration {i + 1}", InfoColor, rtbPackOutput);

                // Update progress
                progressCallback?.Invoke(i + 1);

                // Clear logs from previous iteration
                randomizerLogBuffer.Clear();

                // Run randomizer
                await Task.Run(() => RandomizeHeroes(projectFolder), cancellationToken);
                await Task.Run(() => RandomizeTeam(projectFolder), cancellationToken);

                // Save output to test file
                string outputFile = Path.Combine(testSubFolder, $"same_seed_output_{i + 1}.txt");
                SaveTestOutput(outputFile, randomizerLogBuffer);
                testOutputFiles.Add(outputFile);

                // Calculate checksum of the output for comparison
                string checksum = CalculateChecksum(randomizerLogBuffer);
                checksums.Add(checksum);

                AppendLog($"Completed iteration {i + 1}, checksum: {checksum}", SuccessColor, rtbPackOutput);
            }

            // Analyze results - all checksums should be identical for the same seed
            bool allIdentical = checksums.All(c => c == checksums[0]);

            // Write test summary
            StringBuilder summary = new StringBuilder();
            summary.AppendLine("Same Seed Test Summary");
            summary.AppendLine("=====================");
            summary.AppendLine($"Test Date: {DateTime.Now}");
            summary.AppendLine($"Number of iterations: {testIterations}");
            summary.AppendLine($"Seed used: {TEST_SEED_CONSTANT}");
            summary.AppendLine($"All outputs identical: {allIdentical}");
            summary.AppendLine("\nChecksums:");
            for (int i = 0; i < checksums.Count; i++)
            {
                summary.AppendLine($"Iteration {i + 1}: {checksums[i]}");
            }

            // Save summary
            string summaryFile = Path.Combine(testSubFolder, "test_summary.txt");
            File.WriteAllText(summaryFile, summary.ToString());

            AppendLog($"Same Seed Test completed. All outputs identical: {allIdentical}",
                allIdentical ? SuccessColor : ErrorColor, rtbPackOutput);
        }

        private async Task RunDifferentSeedsTest(string projectFolder, string testFolder, Action<int> progressCallback, CancellationToken cancellationToken = default)
        {
            AppendLog("Starting Different Seeds Test...", InfoColor, rtbPackOutput);

            // Create test subdirectory
            string testSubFolder = Path.Combine(testFolder, "different_seeds_test");
            Directory.CreateDirectory(testSubFolder);

            // Results storage
            var testOutputFiles = new List<string>();
            var checksums = new List<string>();
            var seeds = new List<string>();

            // Run the randomizer with different seeds
            for (int i = 0; i < testIterations; i++)
            {
                // Check for cancellation
                cancellationToken.ThrowIfCancellationRequested();

                // Generate a unique seed for each iteration
                string seed = $"{TEST_SEED_VARYING}_{i + 1}_{DateTime.Now.Ticks}";
                seeds.Add(seed);
                txtSeed.Text = seed;

                AppendLog($"Different Seeds Test - Iteration {i + 1} with seed: {seed}", InfoColor, rtbPackOutput);

                // Update progress
                progressCallback?.Invoke(i + 1);

                // Clear logs from previous iteration
                randomizerLogBuffer.Clear();

                // Run randomizer
                await Task.Run(() => RandomizeHeroes(projectFolder), cancellationToken);
                await Task.Run(() => RandomizeTeam(projectFolder), cancellationToken);

                // Save output to test file
                string outputFile = Path.Combine(testSubFolder, $"different_seed_output_{i + 1}.txt");
                SaveTestOutput(outputFile, randomizerLogBuffer);
                testOutputFiles.Add(outputFile);

                // Calculate checksum of the output for comparison
                string checksum = CalculateChecksum(randomizerLogBuffer);
                checksums.Add(checksum);

                AppendLog($"Completed iteration {i + 1}, checksum: {checksum}", SuccessColor, rtbPackOutput);
            }

            // Analyze results - checksums should be different for different seeds
            int uniqueChecksums = checksums.Distinct().Count();
            bool allDifferent = uniqueChecksums == checksums.Count;

            // Write test summary
            StringBuilder summary = new StringBuilder();
            summary.AppendLine("Different Seeds Test Summary");
            summary.AppendLine("===========================");
            summary.AppendLine($"Test Date: {DateTime.Now}");
            summary.AppendLine($"Number of iterations: {testIterations}");
            summary.AppendLine($"Unique outputs: {uniqueChecksums} out of {checksums.Count}");
            summary.AppendLine($"All outputs different: {allDifferent}");
            summary.AppendLine("\nSeeds and Checksums:");
            for (int i = 0; i < checksums.Count; i++)
            {
                summary.AppendLine($"Iteration {i + 1}:");
                summary.AppendLine($"  Seed: {seeds[i]}");
                summary.AppendLine($"  Checksum: {checksums[i]}");
            }

            // Save summary
            string summaryFile = Path.Combine(testSubFolder, "test_summary.txt");
            File.WriteAllText(summaryFile, summary.ToString());

            AppendLog($"Different Seeds Test completed. Unique outputs: {uniqueChecksums} out of {checksums.Count}",
                allDifferent ? SuccessColor : ErrorColor, rtbPackOutput);
        }

        private async Task AnalyzeRandomnessTest(string projectFolder, string testFolder, Action<int> progressCallback, CancellationToken cancellationToken = default)
        {
            AppendLog("Starting Randomness Analysis Test...", InfoColor, rtbPackOutput);

            // Create test subdirectory
            string testSubFolder = Path.Combine(testFolder, "randomness_analysis");
            Directory.CreateDirectory(testSubFolder);

            // Run multiple randomizations to collect data for analysis
            var classAssignments = new Dictionary<string, Dictionary<string, int>>();
            var statsetAssignments = new Dictionary<int, int>();
            var itemsetAssignments = new Dictionary<int, int>();
            var teamAssignments = new Dictionary<string, int>();  // Dictionary for teamwide analysis

            for (int i = 0; i < testIterations; i++) // More iterations for better statistical data
            {
                // Check for cancellation
                cancellationToken.ThrowIfCancellationRequested();

                // Generate a unique seed
                string seed = $"RandomnessTest_{i + 1}_{DateTime.Now.Ticks}";
                txtSeed.Text = seed;

                AppendLog($"Randomness Analysis - Iteration {i + 1}", InfoColor, rtbPackOutput);

                // Update progress
                progressCallback?.Invoke(i + 1);

                // Configure for maximum randomization
                chbRandomHeroes.Checked = true;
                chbRandomTeam.Checked = true;
                chbRandomStatsets.Checked = true;
                chbRandomItemsets.Checked = true;

                // Clear logs from previous iteration
                randomizerLogBuffer.Clear();

                // Run randomizer
                await Task.Run(() => RandomizeHeroes(projectFolder), cancellationToken);
                await Task.Run(() => RandomizeTeam(projectFolder), cancellationToken);

                if (chbRandomStatsets.Checked)
                {
                    // Initialize progress reporting for stat randomization
                    var statsetProgress = new Progress<int>(value => {
                        // Progress reporting handled elsewhere
                    });

                    await Task.Run(() => RandomizeStatsets(projectFolder, statsetProgress), cancellationToken);
                }

                if (chbRandomItemsets.Checked)
                {
                    // Initialize progress reporting for item randomization
                    var itemsetProgress = new Progress<int>(value => {
                        // Progress reporting handled elsewhere
                    });

                    await Task.Run(() => RandomizeItemsets(projectFolder, itemsetProgress), cancellationToken);
                }

                // Extract and collect data for analysis (now includes team assignments)
                AnalyzeAndCollectRandomizationData(
                    randomizerLogBuffer,
                    classAssignments,
                    statsetAssignments,
                    itemsetAssignments,
                    teamAssignments);

                // Save this iteration's logs
                string outputFile = Path.Combine(testSubFolder, $"randomness_test_output_{i + 1}.txt");
                SaveTestOutput(outputFile, randomizerLogBuffer);
            }

            // Build analysis report
            var analysis = new StringBuilder();
            analysis.AppendLine("Randomness Analysis Results");
            analysis.AppendLine("==========================");
            analysis.AppendLine($"Test Date: {DateTime.Now}");
            analysis.AppendLine($"Number of iterations: {testIterations}");

            // Analyze hero class assignments per hero
            analysis.AppendLine("\nHero Class Assignment Distribution:");
            foreach (var heroEntry in classAssignments)
            {
                analysis.AppendLine($"\nHero: {heroEntry.Key}");
                var assignments = heroEntry.Value;
                int totalAssignments = assignments.Values.Sum();

                foreach (var classEntry in assignments.OrderByDescending(x => x.Value))
                {
                    double percentage = (double)classEntry.Value / totalAssignments * 100;
                    analysis.AppendLine($"  {classEntry.Key}: {classEntry.Value} times ({percentage:F2}%)");
                }

                // Calculate distribution evenness (simplified)
                double evenness = CalculateDistributionEvenness(assignments.Values.ToList());
                analysis.AppendLine($"  Distribution Evenness: {evenness:F4} (closer to 1.0 is more even)");
            }

            // Analyze statset assignments
            analysis.AppendLine("\nStatset Assignment Distribution:");
            int totalStatsetAssignments = statsetAssignments.Values.Sum();
            foreach (var entry in statsetAssignments.OrderByDescending(x => x.Value))
            {
                double percentage = (double)entry.Value / totalStatsetAssignments * 100;
                analysis.AppendLine($"  StatSet {entry.Key}: {entry.Value} times ({percentage:F2}%)");
            }
            double statsetEvenness = CalculateDistributionEvenness(statsetAssignments.Values.ToList());
            analysis.AppendLine($"  Statset Distribution Evenness: {statsetEvenness:F4}");

            // Analyze itemset assignments
            analysis.AppendLine("\nItemset Assignment Distribution:");
            int totalItemsetAssignments = itemsetAssignments.Values.Sum();
            foreach (var entry in itemsetAssignments.OrderByDescending(x => x.Value))
            {
                double percentage = (double)entry.Value / totalItemsetAssignments * 100;
                analysis.AppendLine($"  ItemSet {entry.Key}: {entry.Value} times ({percentage:F2}%)");
            }
            double itemsetEvenness = CalculateDistributionEvenness(itemsetAssignments.Values.ToList());
            analysis.AppendLine($"  Itemset Distribution Evenness: {itemsetEvenness:F4}");

            // Analyze team assignments (New Teamwide analysis)
            analysis.AppendLine("\nTeamwide Assignment Distribution:");
            int totalTeamAssignments = teamAssignments.Values.Sum();
            foreach (var entry in teamAssignments.OrderByDescending(x => x.Value))
            {
                double percentage = (double)entry.Value / totalTeamAssignments * 100;
                analysis.AppendLine($"  Team {entry.Key}: {entry.Value} times ({percentage:F2}%)");
            }
            double teamEvenness = CalculateDistributionEvenness(teamAssignments.Values.ToList());
            analysis.AppendLine($"  Team Distribution Evenness: {teamEvenness:F4}");

            // Calculate overall randomness score as an average of statset, itemset, and team evenness
            double overallScore = (statsetEvenness + itemsetEvenness + teamEvenness) / 3;
            analysis.AppendLine($"\nOverall Randomness Score: {overallScore:F4} (closer to 1.0 is better)");
            analysis.AppendLine($"Randomness Validation: {(overallScore > 0.7 ? "PASSED" : "NEEDS IMPROVEMENT")}");

            // Save analysis report
            string analysisFile = Path.Combine(testSubFolder, "randomness_analysis.txt");
            File.WriteAllText(analysisFile, analysis.ToString());

            AppendLog($"Randomness Analysis completed. Overall score: {overallScore:F4}",
                overallScore > 0.7 ? SuccessColor : WarningColor, rtbPackOutput);
        }

        // Modified method: now includes teamAssignments as an additional output parameter.
        private void AnalyzeAndCollectRandomizationData(
            List<(string message, Color color)> logs,
            Dictionary<string, Dictionary<string, int>> classAssignments,
            Dictionary<int, int> statsetAssignments,
            Dictionary<int, int> itemsetAssignments,
            Dictionary<string, int> teamAssignments)
        {
            // Process logs to extract assignment data
            foreach (var log in logs)
            {
                string message = log.message;

                // Extract hero class assignments
                var heroClassMatch = System.Text.RegularExpressions.Regex.Match(
                    message, @"Assigned class '([^']+)' to hero '([^']+)'");

                if (heroClassMatch.Success)
                {
                    string className = heroClassMatch.Groups[1].Value;
                    string heroName = heroClassMatch.Groups[2].Value;

                    if (!classAssignments.ContainsKey(heroName))
                    {
                        classAssignments[heroName] = new Dictionary<string, int>();
                    }

                    if (!classAssignments[heroName].ContainsKey(className))
                    {
                        classAssignments[heroName][className] = 0;
                    }

                    classAssignments[heroName][className]++;
                }

                // Extract statset assignments
                var statsetMatch = System.Text.RegularExpressions.Regex.Match(
                    message, @"Assigned StatSet (\d+) to Gladiator");

                if (statsetMatch.Success)
                {
                    int statsetId = int.Parse(statsetMatch.Groups[1].Value);

                    if (!statsetAssignments.ContainsKey(statsetId))
                    {
                        statsetAssignments[statsetId] = 0;
                    }

                    statsetAssignments[statsetId]++;
                }

                // Extract itemset assignments
                var itemsetMatch = System.Text.RegularExpressions.Regex.Match(
                    message, @"Assigned ItemSet (\d+) to Gladiator");

                if (itemsetMatch.Success)
                {
                    int itemsetId = int.Parse(itemsetMatch.Groups[1].Value);

                    if (!itemsetAssignments.ContainsKey(itemsetId))
                    {
                        itemsetAssignments[itemsetId] = 0;
                    }

                    itemsetAssignments[itemsetId]++;
                }

                // Extract team assignments for teamwide analysis
                var teamMatch = System.Text.RegularExpressions.Regex.Match(
                    message, @"Assigned class '([^']+)' to team member '([^']+)'");

                if (teamMatch.Success)
                {
                    string teamName = teamMatch.Groups[1].Value;
                    if (!teamAssignments.ContainsKey(teamName))
                    {
                        teamAssignments[teamName] = 0;
                    }
                    teamAssignments[teamName]++;
                }
            }
        }

        private double CalculateDistributionEvenness(List<int> counts)
        {
            if (counts == null || counts.Count == 0)
                return 0;

            // Calculate Shannon diversity index
            double totalCount = counts.Sum();
            double entropy = 0;

            foreach (int count in counts)
            {
                double proportion = count / totalCount;
                if (proportion > 0) // Avoid log(0)
                {
                    entropy -= proportion * Math.Log(proportion);
                }
            }

            // Normalize to [0, 1] range where 1 means perfectly even distribution
            double maxEntropy = Math.Log(counts.Count);
            if (maxEntropy == 0) // Avoid division by zero
                return 1;

            return entropy / maxEntropy;
        }

        private void SaveTestOutput(string outputPath, List<(string message, Color color)> logs)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Randomizer Test Output");
            sb.AppendLine("=====================");
            sb.AppendLine($"Generated on: {DateTime.Now}");
            sb.AppendLine($"Seed: {txtSeed.Text}");
            sb.AppendLine();

            foreach (var entry in logs)
            {
                sb.AppendLine(entry.message);
            }

            File.WriteAllText(outputPath, sb.ToString());
        }

        private string CalculateChecksum(List<(string message, Color color)> logs)
        {
            // Concatenate all log messages
            var sb = new StringBuilder();
            foreach (var entry in logs)
            {
                sb.AppendLine(entry.message);
            }

            // Generate a simple hash
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(sb.ToString());
                var hash = sha.ComputeHash(bytes);
                return BitConverter.ToString(hash).Replace("-", "").Substring(0, 16);
            }
        }
    }
}
