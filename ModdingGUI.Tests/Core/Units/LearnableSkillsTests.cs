using System.Collections.Generic;
using System.Linq;
using ModdingGUI.Core.Api;
using ModdingGUI.Core.Units;
using Xunit;

namespace ModdingGUI.Tests.Core.Units;

/// <summary>
/// Skill eligibility follows the engine's class-mask rule: no <c>SKILLUSECLASS</c> means any class, otherwise the
/// class must be listed. <c>SKILLLEVEL</c> is guidance, not a gate.
/// </summary>
public sealed class LearnableSkillsTests
{
    private static SkillDto Skill(string name, int level, params string[] classes) => new()
    {
        Name = name,
        Fields = classes
            .Select(className => new SkillFieldEntry { FieldName = "SKILLUSECLASS", RawValue = $"\"{className}\"" })
            .Concat(new[]
            {
                new SkillFieldEntry { FieldName = "SKILLLEVEL", RawValue = level.ToString() },
                new SkillFieldEntry { FieldName = "SKILLJOBPOINTCOST", RawValue = "10" },
            })
            .ToList(),
    };

    [Fact]
    public void ASkillWithNoClassListIsOpenToEveryone()
    {
        var generic = Skill("Bash", 1);

        Assert.True(LearnableSkills.IsLearnableBy(generic, "Legionnaire"));
        Assert.True(LearnableSkills.IsLearnableBy(generic, "Archer"));
        Assert.Empty(LearnableSkills.ClassesOf(generic));
    }

    [Fact]
    public void ASkillWithAClassListIsRestrictedToIt()
    {
        var archerOnly = Skill("Volley", 2, "Archer", "ArcherF");

        Assert.True(LearnableSkills.IsLearnableBy(archerOnly, "Archer"));
        Assert.True(LearnableSkills.IsLearnableBy(archerOnly, "archerf"));
        Assert.False(LearnableSkills.IsLearnableBy(archerOnly, "Legionnaire"));
        Assert.Equal(new[] { "Archer", "ArcherF" }, LearnableSkills.ClassesOf(archerOnly));
    }

    [Fact]
    public void NoClassMeansNothingIsLearnable()
    {
        var skill = Skill("Bash", 1);

        Assert.False(LearnableSkills.IsLearnableBy(skill, null));
        Assert.False(LearnableSkills.IsLearnableBy(skill, "  "));
    }

    [Fact]
    public void ForFiltersAndOrdersByLevelThenName()
    {
        var skills = new[]
        {
            Skill("Zeta", 5),
            Skill("Alpha", 1),
            Skill("Archer Shot", 1, "Archer"),
            Skill("Legion Shield", 2, "Legionnaire"),
        };

        var forArcher = LearnableSkills.For(skills, "Archer");

        Assert.Equal(new[] { "Alpha", "Archer Shot", "Zeta" }, forArcher.Select(s => s.Name));
    }

    [Fact]
    public void LevelIsReadForGuidance()
    {
        Assert.Equal(5, LearnableSkills.LevelOf(Skill("Zeta", 5)));
        Assert.Equal(0, LearnableSkills.LevelOf(new SkillDto { Name = "No level" }));
    }

    [Fact]
    public void TheBudgetKnowsWhatTheSkillsCost()
    {
        // End to end of the two rules: eligibility decides the offer, JobPoints prices it.
        var skills = new[] { Skill("Alpha", 1), Skill("Legion Shield", 2, "Legionnaire") };
        var offered = LearnableSkills.For(skills, "Legionnaire");

        var budget = JobPoints.Compute(new[] { 5, 10, 10, 15, 15 }, 2, offered.Select(JobPoints.CostOf));

        Assert.Equal(2, offered.Count);
        Assert.Equal(15, budget.Awarded);
        Assert.Equal(20, budget.Spent);
        Assert.True(budget.OverBudget);
    }
}
