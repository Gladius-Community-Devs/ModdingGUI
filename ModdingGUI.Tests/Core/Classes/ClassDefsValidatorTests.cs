using System.IO;
using System.Linq;
using ModdingGUI.Core.Api;
using ModdingGUI.Core.Classes;
using Xunit;

namespace ModdingGUI.Tests.Core.Classes;

/// <summary>
/// Rule-level tests for <see cref="ClassDefsValidator"/>, run against a small synthetic
/// catalog so each finding is exercised on purpose.
/// </summary>
public sealed class ClassDefsValidatorTests
{
    private const string CatalogJson = """
    {
      "_meta": { "format_version": 1, "class_count": 1 },
      "fields": {
        "MESH": {
          "engine": true, "engine_id": 14, "multi": false, "max_occurrences": 1, "arity": 1,
          "slots": [ { "types": ["string"], "values": [] } ], "classes_using": 1
        },
        "ATTRIBUTE": {
          "engine": true, "engine_id": 19, "multi": true, "max_occurrences": 9, "arity": 1,
          "slots": [ { "types": ["string"], "values": ["Female", "Human", "Support"] } ],
          "classes_using": 1
        },
        "ITEMCAT": {
          "engine": true, "engine_id": 27, "multi": true, "max_occurrences": 20, "arity": 3,
          "slots": [
            { "types": ["string"], "values": ["weapon", "armor"] },
            { "types": ["string"], "values": ["Bow", "<Any>"] },
            { "types": ["string"], "values": ["Plain"] }
          ],
          "classes_using": 1
        },
        "DISPLAYNAMEID": {
          "engine": true, "engine_id": 30, "multi": false, "max_occurrences": 1, "arity": 1,
          "slots": [ { "types": ["int"], "values": [] } ], "classes_using": 1
        },
        "SHADOWSET": {
          "engine": true, "engine_id": 23, "multi": false, "max_occurrences": 1, "arity": 1,
          "slots": [], "classes_using": 0, "engine_only": true
        },
        "LEGACYKEY": {
          "engine": false, "multi": false, "max_occurrences": 1, "arity": 1,
          "slots": [], "classes_using": 1
        }
      },
      "classes": {}
    }
    """;

    private static ClassDefsCatalog Catalog()
    {
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(CatalogJson));
        return ClassDefsCatalog.Load(stream);
    }

    private static ClassDefDto Class(params (string Key, string Value)[] fields) => new()
    {
        Name = "Test",
        Fields = fields.Select(f => new ClassFieldDto(f.Key, f.Value)).ToArray(),
    };

    [Fact]
    public void Validate_CleanBlock_HasNoIssues()
    {
        var issues = ClassDefsValidator.Validate(
            Class(("MESH", "\"Amazon\""), ("ATTRIBUTE", "\"Support\"")), Catalog());

        Assert.Empty(issues);
    }

    [Fact]
    public void Validate_KeyMissingFromTheCatalog_IsUnknownField()
    {
        var issue = Assert.Single(ClassDefsValidator.Validate(Class(("NOTAKEY", "1")), Catalog()));

        Assert.Equal(ClassValidationCodes.UnknownField, issue.Code);
        Assert.Equal(ClassValidationSeverity.Warning, issue.Severity);
        Assert.Contains("ignore", issue.Message);
    }

    [Fact]
    public void Validate_KeyTheEngineDoesNotKnow_IsFieldNotInEngine()
    {
        var issue = Assert.Single(ClassDefsValidator.Validate(Class(("LEGACYKEY", "1")), Catalog()));

        Assert.Equal(ClassValidationCodes.FieldNotInEngine, issue.Code);
        Assert.Contains("engine", issue.Message);
    }

    [Fact]
    public void Validate_UnknownEnumValue_IsReported()
    {
        var issue = Assert.Single(ClassDefsValidator.Validate(Class(("ATTRIBUTE", "\"Wizard\"")), Catalog()));

        Assert.Equal(ClassValidationCodes.UnknownValue, issue.Code);
        Assert.Contains("Wizard", issue.Message);
        Assert.Contains("Support", issue.Message);   // lists the known values
    }

    [Fact]
    public void Validate_EnumCheckIsCaseInsensitive_AndAcceptsAnyWildcard()
    {
        Assert.Empty(ClassDefsValidator.Validate(Class(("ATTRIBUTE", "support")), Catalog()));
        Assert.Empty(ClassDefsValidator.Validate(
            Class(("ITEMCAT", "weapon, <Any>, Plain")), Catalog()));
    }

    [Fact]
    public void Validate_WrongArgumentCount_IsReported()
    {
        // ITEMCAT takes three arguments.
        var issue = Assert.Single(ClassDefsValidator.Validate(
            Class(("ITEMCAT", "weapon, Bow")), Catalog()));

        Assert.Equal(ClassValidationCodes.ArgumentCount, issue.Code);
        Assert.Contains("3", issue.Message);
    }

    [Fact]
    public void Validate_TrailingComma_DoesNotCountAsAnArgument()
    {
        Assert.Empty(ClassDefsValidator.Validate(Class(("ITEMCAT", "weapon, Bow, Plain,")), Catalog()));
    }

    [Fact]
    public void Validate_RepeatingASingleValuedKey_IsReported()
    {
        var issues = ClassDefsValidator.Validate(
            Class(("MESH", "\"A\""), ("MESH", "\"B\"")), Catalog());

        var issue = Assert.Single(issues);
        Assert.Equal(ClassValidationCodes.DuplicateSingleCardinality, issue.Code);
        Assert.Contains("appears 2 times", issue.Message);
    }

    [Fact]
    public void Validate_RepeatingAMultiValuedKey_IsClean()
    {
        Assert.Empty(ClassDefsValidator.Validate(
            Class(("ATTRIBUTE", "\"Support\""), ("ATTRIBUTE", "\"Human\"")), Catalog()));
    }
}
