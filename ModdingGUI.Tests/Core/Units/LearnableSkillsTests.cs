using System.Collections.Generic;
using System.Linq;
using ModdingGUI.Core.Api;
using ModdingGUI.Core.Units;
using Xunit;

namespace ModdingGUI.Tests.Core.Units;

/// <summary>
/// Two rules live here, and they are deliberately not the same.
/// <para>
/// <see cref="LearnableSkills.IsLearnableBy"/> is the engine's: a skill with no <c>SKILLUSECLASS</c> is open to any
/// class (<c>dol/skill_rules.json</c>). <see cref="LearnableSkills.For"/> is the builder's: it also drops the records
/// the game never teaches — combo hits, which are granted by their root, and <c>Innate</c>/<c>Command</c> records,
/// which a roster gets for free. Getting these confused is what made an Amazon's skill list show another class's
/// combo hits, so the tests pin both.
/// </para>
/// </summary>
public sealed class LearnableSkillsTests
{
    private static SkillDto Skill(
        string name,
        int level,
        string[] classes,
        string? category = "Attack",
        string? subskill = null,
        string? meter = null,
        string[]? extraSubskills = null,
        int cost = 10)
    {
        var fields = new List<SkillFieldEntry>
        {
            new() { FieldName = "SKILLLEVEL", RawValue = level.ToString() },
            new() { FieldName = "SKILLJOBPOINTCOST", RawValue = cost.ToString() },
        };

        if (category is not null)
        {
            fields.Insert(0, new SkillFieldEntry
            {
                FieldName = "SKILLCREATE",
                RawValue = $"\"{name}\", \"{category}\", \"Standard\"",
            });
        }

        foreach (string className in classes)
        {
            fields.Add(new SkillFieldEntry { FieldName = "SKILLUSECLASS", RawValue = $"\"{className}\"" });
        }

        if (subskill is not null)
        {
            fields.Add(new SkillFieldEntry { FieldName = "SKILLSUBSKILL", RawValue = $"\"{subskill}\"" });
        }

        foreach (string extra in extraSubskills ?? System.Array.Empty<string>())
        {
            fields.Add(new SkillFieldEntry { FieldName = "SKILLSUBSKILL", RawValue = $"\"{extra}\"" });
        }

        if (meter is not null)
        {
            fields.Add(new SkillFieldEntry { FieldName = "SKILLMETER", RawValue = meter });
        }

        return new SkillDto { Name = name, Fields = fields };
    }

    // ---- The engine's rule ---------------------------------------------------------------------------

    [Fact]
    public void ASkillWithNoClassListIsOpenToEveryone()
    {
        var generic = Skill("Bash", 1, System.Array.Empty<string>());

        Assert.True(LearnableSkills.IsLearnableBy(generic, "Legionnaire"));
        Assert.True(LearnableSkills.IsLearnableBy(generic, "Archer"));
        Assert.Empty(LearnableSkills.ClassesOf(generic));
    }

    [Fact]
    public void ASkillWithAClassListIsRestrictedToIt()
    {
        var archerOnly = Skill("Volley", 2, new[] { "Archer", "ArcherF" });

        Assert.True(LearnableSkills.IsLearnableBy(archerOnly, "Archer"));
        Assert.True(LearnableSkills.IsLearnableBy(archerOnly, "archerf"));
        Assert.False(LearnableSkills.IsLearnableBy(archerOnly, "Legionnaire"));
        Assert.Equal(new[] { "Archer", "ArcherF" }, LearnableSkills.ClassesOf(archerOnly));
    }

    [Fact]
    public void NoClassMeansNothingIsLearnable()
    {
        var skill = Skill("Bash", 1, System.Array.Empty<string>());

        Assert.False(LearnableSkills.IsLearnableBy(skill, null));
        Assert.False(LearnableSkills.IsLearnableBy(skill, "  "));
    }

    // ---- The builder's rule --------------------------------------------------------------------------

    [Fact]
    public void ForOffersTheClassSkillsInLevelThenNameOrder()
    {
        var skills = new[]
        {
            Skill("Zeta", 5, System.Array.Empty<string>()),      // any class: opt-in only
            Skill("Alpha", 1, System.Array.Empty<string>()),     // any class: opt-in only
            Skill("Archer Shot", 1, new[] { "Archer" }),
            Skill("Archer Volley", 1, new[] { "Archer" }),
            Skill("Legion Shield", 2, new[] { "Legionnaire" }),
        };

        var forArcher = LearnableSkills.For(skills, "Archer");

        Assert.Equal(new[] { "Archer Shot", "Archer Volley" }, forArcher.Select(s => s.Name));
    }

    [Fact]
    public void IncludeAnyClassBringsBackTheUnrestrictedRecords()
    {
        var skills = new[]
        {
            Skill("Alpha", 1, System.Array.Empty<string>()),
            Skill("Archer Shot", 1, new[] { "Archer" }),
        };

        var offered = LearnableSkills.For(skills, "Archer",
            new LearnableSkillOptions { IncludeAnyClass = true });

        Assert.Equal(new[] { "Alpha", "Archer Shot" }, offered.Select(s => s.Name));
    }

    [Fact]
    public void AComboHitIsNotOfferedButItsRootIs()
    {
        // Amazon Combo I → Amazon ComboChain I → Amazon ComboChain II. The meter says two hits, so Combo I uses the
        // root plus one record even though the second link exists (Combo II is the skill that uses both).
        var root = Skill("Amazon Combo I", 2, new[] { "Amazon" }, subskill: "Amazon ComboChain I",
            meter: "\"ChainSlow\", 0, 2", cost: 6);
        var hit1 = Skill("Amazon ComboChain I", 0, new[] { "Amazon" }, subskill: "Amazon ComboChain II", cost: 0);
        var hit2 = Skill("Amazon ComboChain II", 0, new[] { "Amazon" }, cost: 0);

        var offered = LearnableSkills.For(new[] { root, hit1, hit2 }, "Amazon");

        Assert.Equal(new[] { "Amazon Combo I" }, offered.Select(s => s.Name));
        Assert.True(LearnableSkills.IsComboRoot(root));
        Assert.Equal(2, LearnableSkills.DeclaredComboHits(root));
        Assert.Equal(new[] { "Amazon Combo I", "Amazon ComboChain I" },
            LearnableSkills.ChainOf(root, new[] { root, hit1, hit2 }).Select(s => s.Name));
    }

    [Fact]
    public void TheMeterBoundsTheChainButAMissingMeterFollowsEveryLink()
    {
        var rootWithMeter = Skill("Combo II", 4, new[] { "Amazon" }, subskill: "Hit I",
            meter: "\"ChainSlow\", 0, 3");
        var rootWithoutMeter = Skill("Combo NoMeter", 4, new[] { "Amazon" }, subskill: "Hit I");
        var hit1 = Skill("Hit I", 0, new[] { "Amazon" }, subskill: "Hit II", cost: 0);
        var hit2 = Skill("Hit II", 0, new[] { "Amazon" }, subskill: "Hit III", cost: 0);
        var hit3 = Skill("Hit III", 0, new[] { "Amazon" }, cost: 0);
        var all = new[] { rootWithMeter, rootWithoutMeter, hit1, hit2, hit3 };

        Assert.Equal(new[] { "Combo II", "Hit I", "Hit II" },
            LearnableSkills.ChainOf(rootWithMeter, all).Select(s => s.Name));
        Assert.Equal(new[] { "Combo NoMeter", "Hit I", "Hit II", "Hit III" },
            LearnableSkills.ChainOf(rootWithoutMeter, all).Select(s => s.Name));
    }

    [Fact]
    public void ASubskillIsRecognisedWhicheverClassTheCallerAsksAbout()
    {
        // The chain hits of the undead classes list no class at all, which is exactly how they leaked into an
        // Amazon's list: nothing links them to their root except this reference.
        var root = Skill("UndeadMelee Combo I", 3, new[] { "UndeadMelee" }, subskill: "UndeadMelee ComboChain I");
        var hit = Skill("UndeadMelee ComboChain I", 0, System.Array.Empty<string>(), cost: 0);

        var targets = LearnableSkills.SubskillTargets(new[] { root, hit });

        Assert.Contains("UndeadMelee ComboChain I", targets);
        Assert.True(LearnableSkills.IsSubskill(hit, targets));
        Assert.DoesNotContain(hit, LearnableSkills.For(
            new[] { root, hit }, "Amazon", new LearnableSkillOptions { IncludeAnyClass = true }));
    }

    [Fact]
    public void InnateAndCommandSkillsAreNotTaught()
    {
        var innate = Skill("Gwazi OffBalance", 19, System.Array.Empty<string>(), category: "Innate", cost: 38);
        var passive = Skill("Pass", 0, System.Array.Empty<string>(), category: "Command", cost: 0);

        Assert.True(LearnableSkills.IsGrantedByDefault(innate));
        Assert.True(LearnableSkills.IsGrantedByDefault(passive));
        Assert.Empty(LearnableSkills.For(new[] { innate, passive }, "Amazon",
            new LearnableSkillOptions { IncludeAnyClass = true }));

        var withGranted = LearnableSkills.For(new[] { innate, passive }, "Amazon",
            new LearnableSkillOptions { IncludeAnyClass = true, IncludeGrantedByDefault = true });
        Assert.Equal(new[] { "Gwazi OffBalance", "Pass" }, withGranted.OrderBy(s => s.Name).Select(s => s.Name));
    }

    [Fact]
    public void TheCategoryComesFromSkillCreatesSecondArgument()
    {
        Assert.Equal("Innate", LearnableSkills.CategoryOf(
            Skill("x", 1, System.Array.Empty<string>(), category: "Innate")));
        Assert.Equal(string.Empty, LearnableSkills.CategoryOf(new SkillDto { Name = "bare" }));
    }

    [Fact]
    public void AChainStopsAtACycleInsteadOfSpinning()
    {
        var a = Skill("A", 1, new[] { "Amazon" }, subskill: "B");
        var b = Skill("B", 0, new[] { "Amazon" }, subskill: "A");

        var chain = LearnableSkills.ChainOf(a, new[] { a, b });

        Assert.Equal(new[] { "A", "B" }, chain.Select(s => s.Name));
    }

    [Fact]
    public void LevelIsReadForGuidance()
    {
        Assert.Equal(5, LearnableSkills.LevelOf(Skill("Zeta", 5, System.Array.Empty<string>())));
        Assert.Equal(0, LearnableSkills.LevelOf(new SkillDto { Name = "No level" }));
    }

    [Fact]
    public void TheBudgetKnowsWhatTheSkillsCost()
    {
        // End to end of the two rules: eligibility decides the offer, JobPoints prices it.
        var skills = new[]
        {
            Skill("Legion Shield", 2, new[] { "Legionnaire" }, cost: 10),
            Skill("Legion Wall", 2, new[] { "Legionnaire" }, cost: 10),
            Skill("Other Class", 2, new[] { "Archer" }, cost: 10),
        };

        var offered = LearnableSkills.For(skills, "Legionnaire");
        var budget = JobPoints.Compute(new[] { 5, 10, 10, 15, 15 }, 2, offered.Select(JobPoints.CostOf));

        Assert.Equal(2, offered.Count);
        Assert.Equal(15, budget.Awarded);
        Assert.Equal(20, budget.Spent);
        Assert.True(budget.OverBudget);
    }
}
