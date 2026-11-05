using System;
using System.IO;
using System.Linq;
using ModdingGUI.Core.Skills;
using Xunit;

namespace ModdingGUI.Tests.Core.Skills;

/// <summary>
/// Loader/parser tests for <see cref="SkillsCatalog"/>. The "shipped catalog"
/// used here is the <c>skills/skills_catalog.json</c> copied next to the test
/// assembly by the shell project, i.e. exactly what users get with the exe.
/// </summary>
public sealed class SkillsCatalogTests
{
    internal static SkillsCatalog ShippedCatalog()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "skills", "skills_catalog.json");
        Assert.True(File.Exists(path), $"Shipped catalog missing at {path}");
        return SkillsCatalog.Load(path);
    }

    [Fact]
    public void Load_ShippedCatalog_IsFormatVersion2AndDocumentsTheFormat()
    {
        var catalog = ShippedCatalog();

        Assert.Equal(SkillsCatalog.SupportedFormatVersion, catalog.FormatVersion);
        Assert.True(catalog.FieldCount >= 60, $"expected >= 60 fields, got {catalog.FieldCount}");
        Assert.Contains("SKILLCREATE", catalog.FieldNames);
        Assert.Contains("SKILLSTATUS", catalog.FieldNames);

        // Field names are exposed in stable order so callers/tests are deterministic.
        var sorted = catalog.FieldNames.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
        Assert.Equal(sorted, catalog.FieldNames.ToList());
    }

    [Fact]
    public void Find_NormalizesNumberedMirrorFields()
    {
        var catalog = ShippedCatalog();

        var status = catalog.Find("SKILLSTATUS");
        Assert.NotNull(status);
        Assert.Same(status, catalog.Find("SKILLSTATUS2"));
        Assert.Same(status, catalog.Find("skillstatus3"));
        Assert.True(status!.HasNumberedVariants);
        Assert.True(status.AllowsRepetition);
    }

    [Fact]
    public void ShippedCatalog_CarriesRulesFoldedFromFieldReference()
    {
        var catalog = ShippedCatalog();

        // SKILLAFFINITY documents its enum values, and the affinity<->cost LINK that
        // the corpus disproved has been removed from the reference file.
        var affinity = catalog.Find("SKILLAFFINITY");
        Assert.NotNull(affinity);
        Assert.Empty(affinity!.Links);
        Assert.NotEmpty(affinity.Slots);
        Assert.Contains("Fire", affinity.Slots[0].Values);

        // Condition fields repeat in game, so the reference marks them MULTI: yes.
        Assert.True(catalog.Find("SKILLSTATUS")!.Multi);
        Assert.True(catalog.Find("SKILLTARGETCONDITION")!.Multi);

        // SKILLMOVERANGE requires SKILLATTRIBUTE to be present (not specifically
        // "teleport" — translateanim is valid too), and the rule is strict/clean.
        var moverange = catalog.Find("SKILLMOVERANGE");
        Assert.NotNull(moverange);
        var requirement = Assert.Single(moverange!.Requirements, r => r.IsEnforceable);
        Assert.Equal("SKILLATTRIBUTE", requirement.Field);
        Assert.Null(requirement.Value);
        Assert.True(requirement.Strict);
        Assert.Equal(0, requirement.Violations);
    }

    [Fact]
    public void ShippedCatalog_CarriesEngineTruthFromTheDol()
    {
        var catalog = ShippedCatalog();

        // Conditional-modifier fields accumulate. Verified in game: a skill whose
        // 3rd class condition matches still fires it, and Beguile's male-only target
        // condition still holds alongside "enemy only".
        Assert.True(catalog.Find("SKILLTARGETCONDITION")!.Accumulates);
        Assert.True(catalog.Find("SKILLSPLITEFFECTCONDITION")!.Accumulates);

        // Bit-set fields accumulate; the paired status fields offer exactly two slots.
        Assert.True(catalog.Find("SKILLATTRIBUTE")!.Accumulates);
        Assert.Equal(2, catalog.Find("SKILLSTATUS")!.MaxOccurrences);
        Assert.False(catalog.Find("SKILLSTATUS")!.Accumulates);

        // Engine-legal field with no vanilla occurrences.
        Assert.True(catalog.Find("SKILLWEAPONEXC")!.EngineOnly);

        // Field name used only by the modded overhaul, which the engine ignores.
        Assert.False(catalog.Find("SKILLSTATUSSITUATION")!.EngineKnown);
    }

    [Fact]
    public void Parse_ToleratesUnknownMembersMissingKeysAndBadEntries()
    {
        const string json = """
        {
          "_meta": { "format_version": 2, "brand_new_key": true },
          "fields": {
            "SKILLFOO": { "enumerate": true, "unknown_key": 1, "slots": [ { "values": ["a", "b"] } ] },
            "SKILLBAR": "not an object",
            "SKILLSTATUS2": { "mirrors": ["SKILLSTATUS2"], "multi": true }
          }
        }
        """;

        var catalog = SkillsCatalog.Parse(json);

        Assert.Equal(2, catalog.FormatVersion);
        Assert.Equal(2, catalog.FieldCount);              // SKILLBAR is skipped, not fatal
        Assert.Equal(new[] { "SKILLFOO", "SKILLSTATUS" }, catalog.FieldNames.ToArray());
        Assert.Equal(new[] { "a", "b" }, catalog.Find("SKILLFOO")!.Slots[0].Values.ToArray());
        Assert.True(catalog.Find("SKILLSTATUS2")!.Multi);
    }

    [Fact]
    public void TryLoad_MissingOrMalformedFile_ReturnsNullInsteadOfThrowing()
    {
        Assert.Null(SkillsCatalog.TryLoad(null));
        Assert.Null(SkillsCatalog.TryLoad(string.Empty));
        Assert.Null(SkillsCatalog.TryLoad(
            Path.Combine(Path.GetTempPath(), "absent-" + Guid.NewGuid().ToString("N") + ".json")));

        string malformed = Path.Combine(Path.GetTempPath(), "bad-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(malformed, "{ this is not json");
        try
        {
            Assert.Null(SkillsCatalog.TryLoad(malformed));
        }
        finally
        {
            File.Delete(malformed);
        }
    }

    [Fact]
    public void FindForProject_PrefersProjectLocalCatalogOverShippedCopy()
    {
        string dir = Path.Combine(Path.GetTempPath(), "catalog-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "skills_catalog.json"), """
            { "fields": { "SKILLPROJECTLOCAL": { "enumerate": false, "slots": [] } } }
            """);

            var catalog = SkillsCatalog.FindForProject(dir, AppContext.BaseDirectory);

            Assert.NotNull(catalog);
            Assert.True(catalog!.Contains("SKILLPROJECTLOCAL"));
            Assert.False(catalog.Contains("SKILLCREATE"));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
