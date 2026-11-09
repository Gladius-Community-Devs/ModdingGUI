using System;
using System.Collections.Generic;
using System.Linq;
using ModdingGUI.Core.Api;
using ModdingGUI.Core.Units;
using Xunit;

namespace ModdingGUI.Tests.Core.Units;

/// <summary>
/// Job points: <c>LEVELUPJPGIVEN</c> awards per level (15 after the fifth), <c>SKILLJOBPOINTCOST</c> as the price,
/// and the balance a designed unit is written with.
/// </summary>
public sealed class JobPointsTests
{
    // The vanilla profile carried by 106 of 128 classes.
    private static readonly int[] Common = { 5, 10, 10, 15, 15 };

    [Fact]
    public void NoAwardsMeansNoPoints()
    {
        Assert.Equal(0, JobPoints.Awarded(new[] { 0, 0, 0, 0, 0 }, 1));
        Assert.Equal(0, JobPoints.Awarded(Array.Empty<int>(), 5));
    }

    [Theory]
    [InlineData(1, 5)]
    [InlineData(2, 15)]
    [InlineData(3, 25)]
    [InlineData(4, 40)]
    [InlineData(5, 55)]
    [InlineData(6, 70)]
    [InlineData(10, 130)]
    [InlineData(15, 205)]
    public void AwardsAccumulateAndThenContinueAtFifteen(int level, int expected)
    {
        Assert.Equal(expected, JobPoints.Awarded(Common, level));
    }

    [Fact]
    public void LevelsBelowOneAreTreatedAsOne()
    {
        Assert.Equal(5, JobPoints.Awarded(Common, 0));
        Assert.Equal(5, JobPoints.Awarded(Common, -3));
    }

    [Fact]
    public void AProfileWithOnlyTheFiveAwardsStillPaysForLaterLevels()
    {
        // Some classes list only five values; beyond them the flat 15 applies.
        Assert.Equal(55 + (15 * 3), JobPoints.Awarded(new[] { 5, 10, 10, 15, 15 }, 8));
    }

    [Fact]
    public void AwardsAreParsedFromTheRawValue()
    {
        Assert.Equal(new[] { 5, 10, 10, 15, 15 }, JobPoints.ParseAwards("5, 10, 10, 15, 15"));
        Assert.Equal(new[] { 5, 20, 20, 20, 20 }, JobPoints.ParseAwards("5,20,20,20,20"));
        Assert.Empty(JobPoints.ParseAwards(null));
        Assert.Empty(JobPoints.ParseAwards("   "));

        // Malformed entries read as zero rather than throwing.
        Assert.Equal(new[] { 5, 0, 10 }, JobPoints.ParseAwards("5, x, 10"));
    }

    [Fact]
    public void SkillCostsAreReadFromSkillJobPointCost()
    {
        var skill = new SkillDto
        {
            Name = "Combo I",
            Fields = new List<SkillFieldEntry>
            {
                new() { FieldName = "SKILLJOBPOINTCOST", RawValue = "10" },
            },
        };

        Assert.Equal(10, JobPoints.CostOf(skill));
        Assert.Equal(0, JobPoints.CostOf(new SkillDto { Name = "Free" }));
        Assert.Equal(0, JobPoints.CostOf((SkillDto?)null));
    }

    [Fact]
    public void GrantedSkillsAreDeductedFromTheAward()
    {
        var budget = JobPoints.Compute(Common, 3, new[] { 10, 5 });

        Assert.Equal(3, budget.Level);
        Assert.Equal(25, budget.Awarded);
        Assert.Equal(15, budget.Spent);
        Assert.Equal(10, budget.Remaining);
        Assert.False(budget.OverBudget);
        Assert.False(budget.Overridden);
    }

    [Fact]
    public void SpendingMoreThanTheLevelAwardsFlagsOverBudget()
    {
        var budget = JobPoints.Compute(Common, 1, new[] { 10, 10 });

        Assert.Equal(5, budget.Awarded);
        Assert.Equal(20, budget.Spent);
        Assert.Equal(0, budget.Remaining);   // never negative: JOBPOINTS cannot be a debt
        Assert.True(budget.OverBudget);
    }

    [Fact]
    public void AnOverrideReplacesTheRule()
    {
        var budget = JobPoints.Compute(Common, 1, new[] { 10 }, overrideRemaining: 42);

        Assert.True(budget.Overridden);
        Assert.Equal(42, budget.Remaining);
        Assert.Equal(5, budget.Awarded);    // the rule's answer is still reported
        Assert.Equal(10, budget.Spent);
        Assert.False(budget.OverBudget);    // an override is the designer's call, not a violation
    }

    [Fact]
    public void AClassBlockAndItsSkillsDriveTheBudget()
    {
        var classDef = new ClassDefDto
        {
            Name = "Legionnaire",
            Fields = new List<ClassFieldDto>
            {
                new("LEVELUPJPGIVEN", "5, 10, 10, 15, 15"),
            },
        };

        var skills = new List<SkillDto?>
        {
            new() { Name = "Shield Bash", Fields = new List<SkillFieldEntry> { new() { FieldName = "SKILLJOBPOINTCOST", RawValue = "15" } } },
        };

        var budget = JobPoints.Compute(classDef, 4, skills);

        Assert.Equal(40, budget.Awarded);
        Assert.Equal(15, budget.Spent);
        Assert.Equal(25, budget.Remaining);
    }

    [Fact]
    public void AClassWithNoProfileAwardsNothing()
    {
        var budget = JobPoints.Compute(new ClassDefDto { Name = "Prop" }, 5);

        Assert.Equal(0, budget.Awarded);
        Assert.Equal(0, budget.Remaining);
    }
}
