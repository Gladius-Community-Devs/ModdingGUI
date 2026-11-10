namespace ModdingGUI
{
    using CoreValidation = ModdingGUI.Core.Validation;

    public partial class frmMain
    {
        /// <summary>
        /// The Pack tab's validation pass: every validator Core has, reported to the log, and a count.
        /// </summary>
        /// <remarks>
        /// <para>
        /// It used to run three hand-written regex checks here in the shell. Those only looked at skillsets that
        /// <c>gladiators.txt</c> pointed at, so a typo in an unreferenced set passed validation and then crashed the
        /// pack chain inside <c>Gladius_Units_IDX_Repack.py</c> with a Python traceback — which is exactly how this was
        /// found. Core's APIs already validate every row of every set, so the pass now calls them; nothing is
        /// re-implemented and the GUI and MCP report the same findings.
        /// </para>
        /// <para>
        /// Nothing here blocks packing. A project unpacked from a modded ISO legitimately has a different number of
        /// skills, items, sets and units, so no check compares against a vanilla count — they check that the
        /// references <em>inside</em> the project resolve. The one remaining vanilla-shaped check (item sets covering
        /// every weapon level) is reported as a note, not a gate.
        /// </para>
        /// </remarks>
        /// <returns>Findings that would break the pack, as a count (0 when there are none).</returns>
        private int RunPackValidation(string projectFolder)
        {
            // Core gets no log sink here: the shell reports per area below, so a run does not print each area twice.
            AppendLog("Validating project…", InfoColor, rtbPackOutput);

            CoreValidation.ValidationReport report;
            try
            {
                report = CoreValidation.ProjectValidator.Validate(projectFolder);
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or ArgumentException)
            {
                AppendLog($"Validation could not run: {ex.Message}. Packing can continue.", WarningColor, rtbPackOutput);
                return 0;
            }

            foreach (var area in report.CountByArea)
            {
                AppendLog($"  {area.Key}: {area.Value} finding(s)", InfoColor, rtbPackOutput);
            }

            // Info-only areas are already counted above; the log shows what could actually bite.
            foreach (CoreValidation.ValidationFinding finding in report.Findings)
            {
                if (finding.Severity == CoreValidation.ValidationSeverity.Info) continue;

                Color color = finding.Severity == CoreValidation.ValidationSeverity.Error ? ErrorColor : WarningColor;
                AppendLog($"{finding.Area}: {finding.Message}", color, rtbPackOutput);
            }

            int notes = report.Findings.Count(finding => finding.Severity == CoreValidation.ValidationSeverity.Info);
            if (notes > 0)
            {
                AppendLog(
                    $"{notes} informational note(s) — vanilla patterns the validators accept, not shown.",
                    InfoColor, rtbPackOutput);
            }

            if (report.WarningCount == 0 && report.ErrorCount == 0)
            {
                AppendLog("Validation: nothing that would break a pack.", SuccessColor, rtbPackOutput);
            }
            else
            {
                AppendLog(
                    $"Validation: {report.Summary}. These are advisory — packing continues unless you stop it.",
                    WarningColor, rtbPackOutput);
            }

            // Weapon-level coverage is the one check the APIs do not make: a gap means an item set has no weapon for
            // a level, which the game tolerates badly. Reported, never enforced.
            TryReportItemsetCoverage(projectFolder);

            return report.ErrorCount;
        }

        /// <summary>Bumps the validation progress bar from any thread.</summary>
        private void IncrementProgressBar()
        {
            if (pgbValidation.InvokeRequired)
            {
                pgbValidation.Invoke(new Action(IncrementProgressBar));
            }
            else if (pgbValidation.Value < pgbValidation.Maximum)
            {
                pgbValidation.Value += 1;
            }
        }

        private void TryReportItemsetCoverage(string projectFolder)
        {
            string itemsetsPath = ProjectPaths.ItemSetsFile(projectFolder);
            if (!File.Exists(itemsetsPath)) return;

            try
            {
                ValidationResult coverage = PackValidator.ValidateItemsets(File.ReadLines(itemsetsPath));
                foreach (var issue in coverage.Issues.Take(10))
                {
                    AppendLog(issue.Message, WarningColor, rtbPackOutput);
                }

                if (coverage.Issues.Count > 10)
                {
                    AppendLog($"…and {coverage.Issues.Count - 10} more item-set coverage gap(s).", WarningColor,
                        rtbPackOutput);
                }
            }
            catch (IOException ex)
            {
                AppendLog($"Item set coverage check skipped: {ex.Message}", WarningColor, rtbPackOutput);
            }
        }
    }
}
