using System.Collections.Generic;
using System.Linq;
using ModdingGUI.Core.Api;
using ModdingGUI.Core.Skills;
using Xunit;

namespace ModdingGUI.Tests.Core.Skills;

/// <summary>
/// Rule-by-rule tests for <see cref="SkillsValidator"/>. A small synthetic catalog
/// keeps every assertion tied to one rule instead of to catalog drift.
/// </summary>
public sealed class SkillsValidatorTests
{
    private const string TestCatalogJson = """
    {
      "_meta": { "format_version": 2 },
      "fields": {
        "SKILLUSECLASS": { "enumerate": false, "slots": [ { "types": ["string"], "values": [] } ] },
        "SKILLAFFINITY": {
          "enumerate": true, "multi": false,
          "slots": [ { "types": ["string"], "values": ["Fire", "Water"] } ],
          "links": [ "SKILLAFFCOST" ]
        },
        "SKILLAFFCOST": {
          "enumerate": false, "multi": false,
          "slots": [ { "types": ["int"], "values": [] } ],
          "links": [ "SKILLAFFINITY" ]
        },
        "SKILLSTATUS": {
          "enumerate": true, "multi": true,
          "mirrors": [ "SKILLSTATUS", "SKILLSTATUS2" ],
          "slots": [
            { "types": ["string"], "values": ["Poison", "Blind"] },
            { "types": ["int"], "values": [] },
            { "types": ["int"], "values": [] }
          ]
        },
        "SKILLRANGE": {
          "enumerate": false, "multi": false,
          "mirrors": [ "SKILLRANGE" ],
          "slots": [ { "types": ["int"], "values": [] }, { "types": ["string"], "values": [] } ]
        },
        "SKILLATTRIBUTE": {
          "enumerate": true, "multi": true,
          "slots": [ { "types": ["string"], "values": ["teleport", "charge"] } ]
        },
        "SKILLMOVERANGE": {
          "enumerate": false, "multi": false,
          "slots": [ { "types": ["int"], "values": [] } ],
          "requires": [ { "field": "SKILLATTRIBUTE", "equals": "teleport" } ]
        },
        "SKILLEFFECTCONDITION": {
          "enumerate": false, "multi": true,
          "slots": [ { "types": ["string"], "values": [] } ],
          "requires": [ { "field": "SKILLEFFECTRANGE", "present": true } ]
        },
        "SKILLEFFECTRANGE": { "enumerate": false, "multi": false, "slots": [] },
        "SKILLDISPLAYNAMEID": { "enumerate": false, "slots": [ { "types": ["int"], "values": [] } ] },
        "SKILLDESCRIPTIONID": { "enumerate": false, "slots": [ { "types": ["int"], "values": [] } ] },
        "SKILLENGINEDUP": {
          "enumerate": false, "engine": true, "max_occurrences": 1, "accumulates": false,
          "slots": [ { "types": ["string"], "values": [] } ]
        },
        "SKILLENGINEACCUM": {
          "enumerate": false, "engine": true, "max_occurrences": null, "accumulates": true,
          "slots": [ { "types": ["string"], "values": [] } ]
        },
        "SKILLNOTENGINE": { "engine": false, "slots": [] },
        "SKILLSTATUSDURATION": {
          "enumerate": false, "engine": true, "max_occurrences": 1, "accumulates": false,
          "engine_slot": "status slot 1", "slots": []
        }
      }
    }
    """;

    private static readonly SkillsCatalog Catalog = SkillsCatalog.Parse(TestCatalogJson);

    /// <summary>
    /// Build a record from alternating field/value strings, e.g.
    /// <c>Skill("SKILLAFFINITY", "\"Fire\"", "SKILLAFFCOST", "3")</c>.
    /// </summary>
    private static SkillDto Skill(params string[] fieldRawPairs)
    {
        var dto = new SkillDto { Name = "Test Skill", SkillType = "Attack", SkillSubType = "Standard" };
        for (int i = 0; i + 1 < fieldRawPairs.Length; i += 2)
            dto.Fields.Add(new SkillFieldEntry { FieldName = fieldRawPairs[i], RawValue = fieldRawPairs[i + 1] });
        return dto;
    }

    private static IReadOnlyList<SkillValidationIssue> Validate(
        SkillDto dto,
        SkillsContext? context = null) =>
        SkillsValidator.Validate(dto, Catalog, context, SkillValidationOptions.Default);

    [Fact]
    public void Validate_CleanRecord_ReportsNothing()
    {
        var dto = Skill("SKILLUSECLASS", "\"Barbarian\"", "SKILLAFFINITY", "\"Fire\"", "SKILLAFFCOST", "3");

        Assert.Empty(Validate(dto));
    }

    [Fact]
    public void Validate_EmptyPositionals_ReportsOneWarningEach()
    {
        var dto = new SkillDto { Name = "", SkillType = "", SkillSubType = "Standard" };

        var issues = Validate(dto);

        Assert.Equal(2, issues.Count);
        Assert.All(issues, i => Assert.Equal(SkillValidationCodes.MissingPositional, i.Code));
        Assert.All(issues, i => Assert.Equal(SkillValidationSeverity.Warning, i.Severity));
    }

    [Fact]
    public void Validate_UnknownField_IsInfoOnly()
    {
        var issues = Validate(Skill("SKILLBRANDNEW", "1"));

        var issue = Assert.Single(issues);
        Assert.Equal(SkillValidationCodes.UnknownField, issue.Code);
        Assert.Equal(SkillValidationSeverity.Info, issue.Severity);
    }

    [Fact]
    public void Validate_ValueOutsideCatalogEnum_IsInfoOnly()
    {
        var issues = Validate(Skill("SKILLAFFINITY", "\"Lava\"", "SKILLAFFCOST", "1"));

        var issue = Assert.Single(issues);
        Assert.Equal(SkillValidationCodes.UnknownEnumValue, issue.Code);
        Assert.Equal(SkillValidationSeverity.Info, issue.Severity);
        Assert.Equal("SKILLAFFINITY", issue.Field);
    }

    [Fact]
    public void Validate_EnumValuesAreCaseInsensitive()
    {
        Assert.Empty(Validate(Skill("SKILLAFFINITY", "\"fire\"", "SKILLAFFCOST", "1")));
    }

    [Fact]
    public void Validate_TextInNumericSlot_IsWarning()
    {
        // SKILLSTATUS slot 2 is documented as int; slot 0 enum, slot 1 int.
        var issues = Validate(Skill("SKILLSTATUS", "\"Poison\", 50, many"));

        var issue = Assert.Single(issues);
        Assert.Equal(SkillValidationCodes.NumericShape, issue.Code);
        Assert.Equal(SkillValidationSeverity.Warning, issue.Severity);
        Assert.Contains("argument 3", issue.Message);
    }

    [Fact]
    public void Validate_RepeatedSingleCardinalityField_IsWarning()
    {
        var issues = Validate(Skill("SKILLRANGE", "1, \"X\"", "SKILLRANGE", "2, \"Y\""));

        var issue = Assert.Single(issues);
        Assert.Equal(SkillValidationCodes.DuplicateSingleCardinality, issue.Code);
        Assert.Equal(SkillValidationSeverity.Warning, issue.Severity);
    }

    [Fact]
    public void Validate_NumberedVariantRepetition_IsAllowed()
    {
        // SKILLSTATUS is MULTI: yes and has numbered mirrors, so repetition is legal.
        var issues = Validate(Skill("SKILLSTATUS", "\"Poison\", 50, 1", "SKILLSTATUS2", "\"Blind\", 25, 2"));

        Assert.Empty(issues);
    }

    [Fact]
    public void Validate_MissingLinkedField_IsWarning()
    {
        var issues = Validate(Skill("SKILLAFFINITY", "\"Fire\""));

        var issue = Assert.Single(issues);
        Assert.Equal(SkillValidationCodes.MissingLinkedField, issue.Code);
        Assert.Contains("SKILLAFFCOST", issue.Message);
    }

    [Fact]
    public void Validate_MissingRequirement_IsWarning()
    {
        var issues = Validate(Skill("SKILLEFFECTCONDITION", "\"statuscond all\""));

        var issue = Assert.Single(issues);
        Assert.Equal(SkillValidationCodes.MissingRequirement, issue.Code);
        Assert.Contains("SKILLEFFECTRANGE", issue.Message);
    }

    [Fact]
    public void Validate_RequirementValueMismatch_IsWarning()
    {
        // SKILLMOVERANGE needs SKILLATTRIBUTE = teleport.
        var mismatch = Validate(Skill("SKILLATTRIBUTE", "\"charge\"", "SKILLMOVERANGE", "3"));
        var issue = Assert.Single(mismatch);
        Assert.Equal(SkillValidationCodes.MissingRequirement, issue.Code);

        Assert.Empty(Validate(Skill("SKILLATTRIBUTE", "\"teleport\"", "SKILLMOVERANGE", "3")));
    }

    [Fact]
    public void Validate_DanglingLookupId_IsWarning_AndResolvesWhenPresent()
    {
        var context = SkillsContext.FromData(lookupText: new Dictionary<int, string>
        {
            [1608] = "Affinity Beast Attack",
        });

        var dangling = Validate(Skill("SKILLDISPLAYNAMEID", "9999"), context);
        var issue = Assert.Single(dangling);
        Assert.Equal(SkillValidationCodes.DanglingLookupId, issue.Code);
        Assert.Equal(SkillValidationSeverity.Warning, issue.Severity);

        Assert.Empty(Validate(Skill("SKILLDISPLAYNAMEID", "1608"), context));
    }

    [Fact]
    public void Validate_WithoutContext_SkipsExternalIdChecks()
    {
        // No context => the lookup table is unknown, so no false "dangling id" warning.
        Assert.Empty(Validate(Skill("SKILLDISPLAYNAMEID", "9999")));
    }

    [Fact]
    public void Validate_EmptyCatalog_DegradesToStructuralChecksOnly()
    {
        var issues = SkillsValidator.Validate(Skill("ANYFIELD", "\"x\""), SkillsCatalog.Empty);

        var issue = Assert.Single(issues);
        Assert.Equal(SkillValidationCodes.UnknownField, issue.Code);
        Assert.Equal(SkillValidationSeverity.Info, issue.Severity);
    }

    // ---- engine-truth rules (verified against the GameCube DOL) --------------

    [Fact]
    public void Validate_FieldNotInEngineTable_IsWarning()
    {
        // The engine tokenizer would not match this name: the line is ignored in game.
        var issues = Validate(Skill("SKILLNOTENGINE", "\"x\""));

        var issue = Assert.Single(issues);
        Assert.Equal(SkillValidationCodes.FieldNotInEngine, issue.Code);
        Assert.Equal(SkillValidationSeverity.Warning, issue.Severity);
    }

    [Fact]
    public void Validate_RepeatingAccumulatingField_IsAllowed()
    {
        // Bit-set / class-set / conditional fields combine, so repeats are legal.
        var issues = Validate(Skill("SKILLENGINEACCUM", "\"a\"", "SKILLENGINEACCUM", "\"b\"", "SKILLENGINEACCUM", "\"c\""));

        Assert.Empty(issues);
    }

    [Fact]
    public void Validate_ExceedingEngineSlots_IsWarning()
    {
        // Scalar engine field: a second line overwrites the first.
        var issues = Validate(Skill("SKILLENGINEDUP", "\"a\"", "SKILLENGINEDUP", "\"b\""));

        var issue = Assert.Single(issues);
        Assert.Equal(SkillValidationCodes.ExceedsEngineSlots, issue.Code);
        Assert.Equal(SkillValidationSeverity.Warning, issue.Severity);
        Assert.Contains("only keeps 1", issue.Message);
    }

    [Fact]
    public void Validate_SingleOccurrenceOfScalarEngineField_IsClean()
    {
        Assert.Empty(Validate(Skill("SKILLENGINEDUP", "\"a\"")));
    }

    [Fact]
    public void Validate_ThirdStatusLine_WarnsThatItCrashesTheGame()
    {
        // Verified in game (Oct 2026): 1 status works, 2 work, a third freezes the
        // game. The message must say so rather than claim a silent overwrite.
        var issues = Validate(Skill(
            "SKILLSTATUSDURATION", "\"Stack Source Mult\", 1.5",
            "SKILLSTATUSDURATION", "\"Stack Source Mult\", 2.5",
            "SKILLSTATUSDURATION", "\"Stack Source Mult\", 3.5"));

        var issue = Assert.Single(issues);
        Assert.Equal(SkillValidationCodes.ExceedsEngineSlots, issue.Code);
        Assert.Equal(SkillValidationSeverity.Warning, issue.Severity);
        Assert.Contains("freezes the game", issue.Message);
    }
}
