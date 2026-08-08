namespace ModdingGUI
{
    public partial class frmMain
    {
        private bool ValidateItemsets(string projectFolder)
        {
            string itemsetsPath = ProjectPaths.ItemSetsFile(projectFolder);

            if (!File.Exists(itemsetsPath))
            {
                MessageBox.Show("itemsets.txt not found at specified path.");
                return false;
            }

            var result = PackValidator.ValidateItemsets(File.ReadLines(itemsetsPath));
            ApplyValidationResult(result);
            return result.IsValid;
        }

        private bool ValidateGladiatorsFile(string projectFolder)
        {
            string gladiatorsPath = ProjectPaths.GladiatorsFile(projectFolder);

            if (!File.Exists(gladiatorsPath))
            {
                MessageBox.Show("gladiators.txt not found at specified path.");
                return false;
            }

            var result = PackValidator.ValidateGladiatorsFile(File.ReadLines(gladiatorsPath));
            ApplyValidationResult(result);
            return result.IsValid;
        }

        private bool ValidateGladiatorsAndSkills(string projectFolder)
        {
            string gladiatorsPath = ProjectPaths.GladiatorsFile(projectFolder);
            string skillsetsPath = ProjectPaths.SkillSetsFile(projectFolder);
            string skillsTokPath = ProjectPaths.SkillsTokFile(projectFolder);
            string classDefsPath = ProjectPaths.ClassDefsFile(projectFolder);

            if (!File.Exists(gladiatorsPath) || !File.Exists(skillsetsPath) ||
                !File.Exists(skillsTokPath) || !File.Exists(classDefsPath))
            {
                AppendLog("One or more required files (gladiators.txt, skillsets.txt, skills.tok, classdefs.tok) are missing.", ErrorColor, rtbPackOutput);
                return false;
            }

            AppendLog("Starting to parse skills.tok...", InfoColor, rtbPackOutput);
            var result = PackValidator.ValidateGladiatorsAndSkills(
                File.ReadLines(gladiatorsPath),
                File.ReadLines(skillsetsPath),
                File.ReadLines(skillsTokPath),
                File.ReadLines(classDefsPath));
            AppendLog("Finished parsing skills.tok", InfoColor, rtbPackOutput);

            ApplyValidationResult(result);
            return result.IsValid;
        }

        // Feeds a ValidationResult into the progress bar and log output.
        private void ApplyValidationResult(ValidationResult result)
        {
            pgbValidation.Maximum += result.StepCount;
            for (int i = 0; i < result.StepCount; i++)
                IncrementProgressBar();

            foreach (var issue in result.Issues)
            {
                Color color = issue.Severity switch
                {
                    ValidationSeverity.Error   => ErrorColor,
                    ValidationSeverity.Warning => WarningColor,
                    _                          => InfoColor
                };
                AppendLog(issue.Message, color, rtbPackOutput);
            }
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
