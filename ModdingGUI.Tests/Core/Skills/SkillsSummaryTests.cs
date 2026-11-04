using System;
using System.Linq;
using ModdingGUI.Core.Skills;
using Xunit;

namespace ModdingGUI.Tests.Core.Skills;

/// <summary>
/// The editor shows a one-line summary and puts the derivation behind a details toggle, so every
/// documented field needs a summary that fits on one line.
/// </summary>
public sealed class SkillsSummaryTests
{
    [Fact]
    public void Summarize_KeepsOnlyTheFirstSentence()
    {
        string note = "Animation name for the skill. Valid values are sourced at runtime from .pan files.";
        Assert.Equal("Animation name for the skill.", SkillsCatalog.Summarize(null, note));
    }

    [Fact]
    public void Summarize_PrefersAnExplicitSummary()
    {
        Assert.Equal("Short.", SkillsCatalog.Summarize("Short.", "Long. Explanation here."));
    }

    [Fact]
    public void Summarize_CapsARunOnNote()
    {
        string note = new string('a', 400);
        string? summary = SkillsCatalog.Summarize(null, note);
        Assert.NotNull(summary);
        Assert.True(summary!.Length <= 141, $"summary was {summary.Length} characters");
        Assert.EndsWith("…", summary);
    }

    [Fact]
    public void Summarize_ReturnsNullWithoutContent()
    {
        Assert.Null(SkillsCatalog.Summarize(null, null));
        Assert.Null(SkillsCatalog.Summarize("  ", ""));
    }

    [Fact]
    public void Catalog_EveryDocumentedFieldHasAShortSummary()
    {
        var catalog = SkillsCatalogTests.ShippedCatalog();
        var withNotes = catalog.Fields.Where(f => !string.IsNullOrWhiteSpace(f.Note)).ToArray();

        Assert.NotEmpty(withNotes);
        foreach (var field in withNotes)
        {
            Assert.False(string.IsNullOrWhiteSpace(field.Summary), $"{field.Name} has no summary");
            Assert.True(field.Summary!.Length <= 141,
                $"{field.Name} summary is {field.Summary.Length} characters: {field.Summary}");

            // The full derivation is still available for the details toggle.
            Assert.False(string.IsNullOrWhiteSpace(field.Note), $"{field.Name} lost its note");
        }
    }
}
