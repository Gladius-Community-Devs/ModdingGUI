using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ModdingGUI.Core.Api;
using ModdingGUI.Core.Skills;
using Xunit;
using Xunit.Abstractions;

namespace ModdingGUI.Tests.Core.Skills;

/// <summary>
/// End-to-end validation tests against a staged copy of the real vanilla
/// <c>skills.tok</c> (never the read-only corpus itself). The headline test is
/// <see cref="Validate_EveryVanillaSkill_HasNoWarnings"/>: the shipped catalog
/// must not report a single false warning across all 1,811 vanilla records.
/// </summary>
public sealed class SkillsApiValidationTests : IDisposable
{
    private const int VanillaSkillCount = 1811;

    private readonly string _projectDir;
    private readonly string _skillsPath;
    private readonly ITestOutputHelper _output;

    public SkillsApiValidationTests(ITestOutputHelper output)
    {
        _output = output;
        _projectDir = Path.Combine(Path.GetTempPath(),
            "ModdingGUI.SkillsValidation-" + Guid.NewGuid().ToString("N"));

        string configDir = Path.Combine(_projectDir, "data", "config");
        Directory.CreateDirectory(configDir);
        _skillsPath = Path.Combine(configDir, "skills.tok");

        if (!CleanBecCorpus.IsAvailable) return;

        string corpusConfig = Path.Combine(CleanBecCorpus.Root!, "data", "config");
        File.Copy(Path.Combine(corpusConfig, "skills.tok"), _skillsPath, overwrite: true);
        foreach (string name in new[] { "lookuptext_eng.txt", "swingmeter.tok" })
        {
            string source = Path.Combine(corpusConfig, name);
            if (File.Exists(source)) File.Copy(source, Path.Combine(configDir, name), overwrite: true);
        }
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_projectDir)) Directory.Delete(_projectDir, recursive: true);
        }
        catch
        {
            // best effort temp cleanup
        }
    }

    private string HistoryRoot => Path.Combine(_projectDir, ".mod-history");

    [CleanBecFact]
    public void Validate_EveryVanillaSkill_HasNoWarnings()
    {
        var api = new SkillsApi();
        var catalog = SkillsCatalogTests.ShippedCatalog();
        var context = SkillsContext.Load(_projectDir);

        var skills = api.GetAll(_projectDir);
        Assert.Equal(VanillaSkillCount, skills.Count);

        var warnings = new List<(string Skill, SkillValidationIssue Issue)>();
        int infoCount = 0;

        foreach (var skill in skills)
        {
            foreach (var issue in SkillsValidator.Validate(skill, catalog, context))
            {
                if (issue.Severity == SkillValidationSeverity.Warning)
                    warnings.Add((skill.Name, issue));
                else
                    infoCount++;
            }
        }

        _output.WriteLine($"info-level findings across vanilla corpus: {infoCount}");

        string grouped = string.Join("\n  ", warnings
            .GroupBy(w => (w.Issue.Code, w.Issue.Field))
            .Select(g => $"{g.Key.Code} / {g.Key.Field}: {g.Count()} skill(s), " +
                         $"e.g. {string.Join(", ", g.Take(3).Select(x => x.Skill))}"));

        Assert.True(warnings.Count == 0,
            $"expected zero warnings on vanilla data, got {warnings.Count}:\n  {grouped}");
    }

    [CleanBecFact]
    public void Validate_IsReadOnly_DoesNotTouchTheFile()
    {
        var api = new SkillsApi();
        var catalog = SkillsCatalogTests.ShippedCatalog();
        byte[] before = File.ReadAllBytes(_skillsPath);

        var skill = api.Get(_projectDir, "AffinityBeast Attack");
        Assert.NotNull(skill);
        SkillsValidator.Validate(skill, catalog, SkillsContext.Load(_projectDir));

        Assert.Equal(before, File.ReadAllBytes(_skillsPath));
        Assert.False(Directory.Exists(HistoryRoot), "validation must not create backups");
    }

    [CleanBecFact]
    public void Update_RejectMode_ThrowsAndLeavesFileUntouched()
    {
        var api = new SkillsApi();
        var catalog = SkillsCatalogTests.ShippedCatalog();
        var context = SkillsContext.Load(_projectDir);

        var skill = api.Get(_projectDir, "AffinityBeast Attack");
        Assert.NotNull(skill);
        skill!.SkillType = string.Empty;   // missing positional => Warning

        byte[] before = File.ReadAllBytes(_skillsPath);

        var ex = Assert.Throws<SkillValidationException>(() =>
            api.Update(_projectDir, skill.Name, skill, SkillValidationMode.Reject, catalog, context));

        Assert.Contains(SkillValidationCodes.MissingPositional,
            ex.Issues.Select(i => i.Code));
        Assert.Equal(before, File.ReadAllBytes(_skillsPath));
        Assert.False(Directory.Exists(HistoryRoot), "a rejected write must not back up or write");
    }

    [CleanBecFact]
    public void Update_WarnMode_WritesAndReturnsIssues()
    {
        var api = new SkillsApi();
        var catalog = SkillsCatalogTests.ShippedCatalog();
        var context = SkillsContext.Load(_projectDir);

        var skill = api.Get(_projectDir, "AffinityBeast Attack");
        Assert.NotNull(skill);
        skill!.SkillType = string.Empty;

        byte[] before = File.ReadAllBytes(_skillsPath);

        var result = api.Update(_projectDir, skill.Name, skill, SkillValidationMode.Warn, catalog, context);

        Assert.True(result.HasWarnings);
        Assert.Contains(result.Issues, i => i.Code == SkillValidationCodes.MissingPositional);
        Assert.NotEqual(before, File.ReadAllBytes(_skillsPath));

        string backupDir = Path.Combine(HistoryRoot, "data", "config", "skills.tok");
        Assert.True(Directory.Exists(backupDir), $"expected backups under {backupDir}");
        Assert.NotEmpty(Directory.GetFiles(backupDir, "*.bak"));
    }

    [CleanBecFact]
    public void Context_ResolvesMeterNamesFromSwingmeterAndObservedValues()
    {
        var context = SkillsContext.Load(_projectDir);

        Assert.True(context.HasMeterIds);
        Assert.Equal("None", context.MeterIds[0]);
        Assert.Contains("pound1", context.MeterIds);       // declared in swingmeter.tok
        Assert.Contains("FastButton1", context.MeterIds);  // declared in swingmeter.tok
        Assert.Contains("Chain", context.MeterIds);        // only observed in skills.tok
        Assert.Equal(context.MeterIds.Count,
            context.MeterIds.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [CleanBecFact]
    public void Context_LoadsLookupTextForDanglingIdChecks()
    {
        var context = SkillsContext.Load(_projectDir);

        Assert.True(context.HasLookupText);
        Assert.True(context.LookupText.Count > 8000, $"got {context.LookupText.Count} entries");
        Assert.True(context.LookupText.ContainsKey(1608));
    }

    [CleanBecFact]
    public void Context_OnEmptyProject_DegradesWithoutThrowing()
    {
        string bare = Path.Combine(Path.GetTempPath(), "ModdingGUI.SkillsBare-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(bare);
        try
        {
            var context = SkillsContext.Load(bare);

            Assert.True(context.HasMeterIds);      // "None" is always offered
            Assert.False(context.HasLookupText);
            Assert.Single(context.MeterIds);
        }
        finally
        {
            Directory.Delete(bare, recursive: true);
        }
    }
}
