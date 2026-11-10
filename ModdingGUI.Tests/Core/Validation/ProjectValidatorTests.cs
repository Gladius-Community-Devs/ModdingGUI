using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ModdingGUI.Core.Logging;
using ModdingGUI.Core.Validation;
using Xunit;

namespace ModdingGUI.Tests.Core.Validation;

/// <summary>
/// The pack pass validates references inside the project. It must not compare against vanilla counts (a project
/// unpacked from a modded ISO legitimately differs), and it must catch the class of defect that crashes the packer:
/// a skill name in <c>skillsets.txt</c> that <c>skills.tok</c> does not define.
/// </summary>
public sealed class ProjectValidatorTests : IDisposable
{
    private readonly string _project;

    public ProjectValidatorTests()
    {
        _project = Path.Combine(Path.GetTempPath(), "ModdingGUI.Validate-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_project);
        if (!CleanBecCorpus.IsAvailable) return;

        // Only the files the validators read: a few MB, not the 1.4 GB corpus.
        foreach (string relative in new[] { "data/units", "data/config", "data/school" })
        {
            foreach (string file in Directory.EnumerateFiles(
                         Path.Combine(CleanBecCorpus.Root!, relative), "*", SearchOption.AllDirectories))
            {
                string name = Path.GetFileName(file);
                if (relative == "data/config" && name is not ("skills.tok" or "items.tok" or "classdefs.tok")) continue;

                string target = Path.Combine(_project, relative.Replace('/', Path.DirectorySeparatorChar), name);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(file, target, overwrite: true);
            }
        }
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_project)) Directory.Delete(_project, recursive: true);
        }
        catch (IOException)
        {
            // best effort
        }
    }

    private static bool Ready => CleanBecCorpus.IsAvailable;

    [Fact]
    public void VanillaHasNoFindingsThatWouldBreakAPack()
    {
        if (!Ready) return;

        ValidationReport report = ProjectValidator.Validate(_project);

        Assert.Equal(0, report.ErrorCount);
        Assert.Equal(0, report.WarningCount);
        // Info notes are expected and allowed: vanilla does things the validators only mention.
        Assert.NotNull(report.Findings);
    }

    [Fact]
    public void ASkillNameInAnUnreferencedSetIsReported()
    {
        if (!Ready) return;

        // The shape of the bug that produced "ValueError: 'Wolf Attack' is not in list" from the packer: a skill name
        // inside a skillset that no gladiator points at. A validator that only walks gladiators never sees it, while
        // the packer reads every set — which is why the pack died on a project that had passed validation.
        string skillsets = Path.Combine(_project, "data", "units", "skillsets.txt");
        var rows = File.ReadAllLines(skillsets).ToList();
        int header = rows.FindIndex(line => line.Trim() == "Skillset 209:");
        Assert.True(header >= 0, "expected a 'Skillset 209:' section in the corpus");

        // Keep the row's level window, replace only the name.
        string[] parts = rows[header + 1].Split(' ', 3);
        Assert.Equal(3, parts.Length);
        rows[header + 1] = $"{parts[0]} {parts[1]} Test Only Skill That Does Not Exist";
        File.WriteAllLines(skillsets, rows);

        ValidationReport report = ProjectValidator.Validate(_project);

        var finding = Assert.Single(report.Findings, f => f.Area == "skillsets" &&
            f.Message.Contains("Test Only Skill That Does Not Exist", StringComparison.Ordinal));
        Assert.Equal(ModdingGUI.Core.Validation.ValidationSeverity.Warning, finding.Severity);
        Assert.Contains("set 209", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NoCountIsComparedAgainstVanilla()
    {
        if (!Ready) return;

        string skillsTok = Path.Combine(_project, "data", "config", "skills.tok");

        // A project whose skills.tok is a subset of vanilla is valid: it came from a modded ISO, not from a mistake.
        var kept = File.ReadAllLines(skillsTok)
            .Where(line => !line.StartsWith("SKILLCREATE:", StringComparison.Ordinal))
            .ToArray();
        File.WriteAllLines(skillsTok, kept);

        ValidationReport report = ProjectValidator.Validate(_project);

        // The skillsets now reference names that are gone, and that is a genuine finding — but nothing complains that
        // the project does not have the vanilla number of skills.
        Assert.DoesNotContain(report.Findings, f => f.Message.Contains("NUMENTRIES", StringComparison.Ordinal));
        Assert.All(report.Findings, f => Assert.False(f.Message.Contains("1811", StringComparison.Ordinal)));
    }

    [Fact]
    public void AMissingFileIsReportedInsteadOfThrown()
    {
        if (!Ready) return;

        File.Delete(Path.Combine(_project, "data", "units", "statsets.txt"));

        ValidationReport report = ProjectValidator.Validate(_project);

        Assert.Contains(report.Findings, f => f.Area == "statsets");
    }

    [Fact]
    public void FindingsAreCappedPerAreaButTheOtherAreasStillReport()
    {
        if (!Ready) return;

        string itemsets = Path.Combine(_project, "data", "units", "itemsets.txt");
        var lines = File.ReadAllLines(itemsets).ToList();
        lines.Add("Item set 0: NoSuchItemName");
        File.WriteAllLines(itemsets, lines);

        ValidationReport capped = ProjectValidator.Validate(_project, new ValidationOptions { MaxFindingsPerArea = 1 });

        // The cap trims one area; the other areas still contribute.
        Assert.NotEmpty(capped.Findings);
        Assert.True(capped.Findings.Count(f => f.Area == "itemsets") <= 1);
        Assert.Contains(capped.Findings, f => f.Area != "itemsets");
    }

    [Fact]
    public void ProgressIsWrittenToTheLogPerArea()
    {
        if (!Ready) return;

        var log = AppLog.CreateForRun();
        ProjectValidator.Validate(_project, log: log);

        Assert.Contains(log.Tail(500), entry => entry.Message.Contains("Validating gladiators"));
        Assert.Contains(log.Tail(500), entry => entry.Message.Contains("Validating skillsets"));
        Assert.Contains(log.Tail(500), entry => entry.Message.Contains("Validating school names"));
        Assert.Contains(log.Tail(500), entry => entry.Message.Contains("Validating rosters"));
    }
}
