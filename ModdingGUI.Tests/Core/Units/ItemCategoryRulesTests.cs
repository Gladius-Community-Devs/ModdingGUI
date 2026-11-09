using System.Collections.Generic;
using System.Linq;
using ModdingGUI.Core.Api;
using ModdingGUI.Core.Units;
using Xunit;

namespace ModdingGUI.Tests.Core.Units;

/// <summary>
/// The class category rule from <c>FUN_800f96f4</c>: type and style must match a category, subtype may be wildcarded
/// with <c>&lt;Any&gt;</c>.
/// </summary>
public sealed class ItemCategoryRulesTests
{
    private static ClassDefDto Class(string name, params string[] itemCats) => new()
    {
        Name = name,
        Fields = itemCats.Select(line => new ClassFieldDto("ITEMCAT", line)).ToArray(),
    };

    [Fact]
    public void ACategoryMatchesOnTypeSubtypeAndStyle()
    {
        var archer = Class("Archer", "weapon, Bow, Plain", "weapon, Bow, Archer");

        Assert.True(ItemCategoryRules.Allows(ItemCategoryRules.Parse(archer), "Weapon", "Bow", "Plain"));
        Assert.True(ItemCategoryRules.Allows(ItemCategoryRules.Parse(archer), "weapon", "bow", "archer"));
        Assert.False(ItemCategoryRules.Allows(ItemCategoryRules.Parse(archer), "Weapon", "Sword", "Plain"));
        Assert.False(ItemCategoryRules.Allows(ItemCategoryRules.Parse(archer), "Weapon", "Bow", "Heavy"));
        Assert.False(ItemCategoryRules.Allows(ItemCategoryRules.Parse(archer), "Armor", "Bow", "Plain"));
    }

    [Fact]
    public void AnyAcceptsEverySubtypeOfThatType()
    {
        var amazon = Class("Amazon", "weapon, <Any>, Amazon");

        Assert.True(ItemCategoryRules.Allows(ItemCategoryRules.Parse(amazon), "Weapon", "Bow", "Amazon"));
        Assert.True(ItemCategoryRules.Allows(ItemCategoryRules.Parse(amazon), "Weapon", "Sword", "Amazon"));
        Assert.False(ItemCategoryRules.Allows(ItemCategoryRules.Parse(amazon), "Weapon", "Bow", "Plain"));
    }

    [Fact]
    public void AClassWithNoCategoriesAcceptsNothing()
    {
        Assert.False(ItemCategoryRules.Allows(ItemCategoryRules.Parse(Class("Prop")), "Weapon", "Bow", "Plain"));
        Assert.False(ItemCategoryRules.Allows(ItemCategoryRules.Parse(null), "Weapon", "Bow", "Plain"));
    }

    [Fact]
    public void MalformedCategoriesAreSkipped()
    {
        var odd = Class("Odd", "weapon, Bow", "not a category", "helmet, Helmet, Plain");

        var categories = ItemCategoryRules.Parse(odd);

        Assert.Single(categories);
        Assert.Equal("helmet", categories[0].Type);
    }

    [Fact]
    public void ExplainNamesTheItemAndTheClass()
    {
        var legionnaire = Class("Legionnaire", "weapon, Sword, Medium");

        string? problem = ItemCategoryRules.Explain(legionnaire, "Short Bow", "Weapon", "Bow", "Plain");

        Assert.NotNull(problem);
        Assert.Contains("Short Bow", problem);
        Assert.Contains("Legionnaire", problem);
        Assert.Contains("weapon Sword Medium", problem);

        Assert.Null(ItemCategoryRules.Explain(legionnaire, "Gladius", "Weapon", "Sword", "Medium"));
    }

    [Fact]
    public void ValidateReportsEveryUnusableItemAndUnknownOnes()
    {
        var classDef = Class("Legionnaire", "weapon, Sword, Medium", "armor, Armor, Medium");

        var items = new Dictionary<string, ItemDto>(System.StringComparer.OrdinalIgnoreCase)
        {
            ["Gladius"] = new() { Name = "Gladius", Type = "Weapon", SubType = "Sword", Style = "Medium" },
            ["Short Bow"] = new() { Name = "Short Bow", Type = "Weapon", SubType = "Bow", Style = "Plain" },
            ["Lorica"] = new() { Name = "Lorica", Type = "Armor", SubType = "Armor", Style = "Medium" },
        };

        var gear = new GearLoadout(Weapon: "Short Bow", Armor: "Lorica", Shield: "Missing Thing");

        var problems = ItemCategoryRules.Validate(classDef, gear, items);

        Assert.Equal(2, problems.Count);
        Assert.Contains(problems, p => p.Contains("Short Bow") && p.Contains("cannot use"));
        Assert.Contains(problems, p => p.Contains("not in items.tok"));

        Assert.Empty(ItemCategoryRules.Validate(classDef, new GearLoadout(Weapon: "Gladius", Armor: "Lorica"), items));
    }
}
