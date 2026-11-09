using ModdingGUI.Core.Units;
using Xunit;

namespace ModdingGUI.Tests.Core.Units;

/// <summary>
/// Input rules and their evidence: levels stop at 30 because every stat set does; gold fits a signed 32-bit field;
/// names are quoted tokens so a double quote or line break is illegal, while spaces and underscores are legitimate.
/// </summary>
public sealed class InputRulesTests
{
    [Theory]
    [InlineData(1, true)]
    [InlineData(30, true)]
    [InlineData(0, false)]
    [InlineData(31, false)]
    [InlineData(-5, false)]
    public void LevelsAreOneToThirty(int level, bool valid)
    {
        Assert.Equal(valid, InputRules.IsValidLevel(level));

        string? problem = InputRules.ExplainLevel(level);
        Assert.Equal(valid ? null : problem, valid ? null : problem);
        if (!valid) Assert.Contains("between 1 and 30", problem!);
    }

    [Fact]
    public void GoldFitsASignedThirtyTwoBitField()
    {
        Assert.True(InputRules.IsValidGold(0));
        Assert.True(InputRules.IsValidGold(999_980_999));
        Assert.True(InputRules.IsValidGold(int.MaxValue));
        Assert.False(InputRules.IsValidGold(-1));
        Assert.False(InputRules.IsValidGold((long)int.MaxValue + 1));
    }

    [Fact]
    public void ValuesAboveVanillaMoneyAreAllowed()
    {
        // No vanilla ceiling is imposed: a modder may hand a school more than the game ships with.
        Assert.True(InputRules.IsValidGold(999_980_999));
        Assert.True(InputRules.IsValidGold(1_500_000_000));
        Assert.True(InputRules.IsValidGold(int.MaxValue));
    }

    [Fact]
    public void AnItemCostsAtLeastOneAndAtMostTheMoneyField()
    {
        Assert.False(InputRules.IsValidItemCost(0));
        Assert.True(InputRules.IsValidItemCost(1));
        Assert.True(InputRules.IsValidItemCost(int.MaxValue));
        Assert.False(InputRules.IsValidItemCost(-1));
        Assert.Contains("between 1 and", InputRules.ExplainItemCost(0)!);
    }

    [Fact]
    public void AffinityStopsAtTwoHundred()
    {
        Assert.True(InputRules.IsValidAffinity(0));
        Assert.True(InputRules.IsValidAffinity(200));
        Assert.False(InputRules.IsValidAffinity(201));
        Assert.False(InputRules.IsValidAffinity(-1));
    }

    [Fact]
    public void TheHudLimitIsDocumentedButNotEnforced()
    {
        Assert.Equal(999_999, InputRules.ReportedHudGoldDisplayLimit);
        Assert.True(InputRules.IsValidGold(InputRules.ReportedHudGoldDisplayLimit + 1));
    }

    [Fact]
    public void OrdinaryNamesAreAccepted()
    {
        Assert.Empty(InputRules.ValidateUnitName("Mr Boots"));
        Assert.Empty(InputRules.ValidateUnitName("ludo_random"));
        Assert.Empty(InputRules.ValidateUnitName("O'Brien"));
        Assert.Empty(InputRules.ValidateUnitName("Jean-Luc"));
        Assert.Empty(InputRules.ValidateUnitName("Unit_2"));
    }

    [Fact]
    public void AQuoteOrLineBreakIsRejectedBecauseTheLineIsQuoted()
    {
        Assert.Contains(InputRules.ValidateUnitName("Bad\"Name"), p => p.Contains("double quote"));
        Assert.Contains(InputRules.ValidateUnitName("Bad\nName"), p => p.Contains("line breaks"));
        Assert.Contains(InputRules.ValidateUnitName("Bad\tName"), p => p.Contains("line breaks"));
    }

    [Fact]
    public void EmptyAndOverlongNamesAreRejected()
    {
        Assert.Contains(InputRules.ValidateUnitName(""), p => p.Contains("required"));
        Assert.Contains(InputRules.ValidateUnitName("   "), p => p.Contains("required"));
        Assert.Contains(InputRules.ValidateUnitName(new string('a', 32)), p => p.Contains("longer than 31"));
        Assert.Empty(InputRules.ValidateUnitName(new string('a', 31)));
    }

    [Fact]
    public void ANameWithAPaddedSpaceIsRejected()
    {
        Assert.Contains(InputRules.ValidateUnitName(" Ludo"), p => p.Contains("space"));
        Assert.Contains(InputRules.ValidateUnitName("Ludo "), p => p.Contains("space"));
    }

    [Fact]
    public void TheDisplayNameStopsAtTheFirstUnderscore()
    {
        Assert.Equal("Ludo", InputRules.DisplayNameOf("Ludo_random"));
        Assert.Equal("Ludo", InputRules.DisplayNameOf("Ludo_random_extra"));
        Assert.Equal("Ludo", InputRules.DisplayNameOf("Ludo"));
        Assert.Equal(string.Empty, InputRules.DisplayNameOf(""));
    }

    [Fact]
    public void SchoolNamesFollowTheSameQuotingRule()
    {
        Assert.Empty(InputRules.ValidateSchoolName("Random's School"));
        Assert.Contains(InputRules.ValidateSchoolName("Bad\"School"), p => p.Contains("double quote"));
        Assert.Contains(InputRules.ValidateSchoolName("Two\nLines"), p => p.Contains("line breaks"));
        Assert.Contains(InputRules.ValidateSchoolName(""), p => p.Contains("required"));
    }
}
