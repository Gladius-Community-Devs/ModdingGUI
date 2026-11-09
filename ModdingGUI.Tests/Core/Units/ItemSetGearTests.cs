using System;
using System.Collections.Generic;
using System.Linq;
using ModdingGUI.Core.Api;
using ModdingGUI.Core.Units;
using Xunit;

namespace ModdingGUI.Tests.Core.Units;

/// <summary>
/// Gear selection is by <c>items.tok</c> Type, matching the engine (<c>FUN_800f96f4</c>). The itemset tag is
/// advisory: the packer discards it, so a tag-following tool would disagree with the game on modded data.
/// </summary>
public sealed class ItemSetGearTests
{
    private static readonly Dictionary<string, string> Types = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Fire Rigid Circlet"] = "Helmet",
        ["Bow"] = "Weapon",
        ["Water Circlet of the Gods"] = "Helmet",
        ["Short Bow"] = "Weapon",
        ["Leather Bikini"] = "Armor",
        ["Parma"] = "Shield",
        ["Wrist Brace"] = "Armor",
        ["Earth Emerald"] = "Accessory",
    };

    private static ItemSetRow Row(int min, int max, string item, string? tag) =>
        new(min, max, item, tag is null ? null : $"[{tag}]");

    [Fact]
    public void SlotsComeFromTheItemTypeNotTheRowOrder()
    {
        // The shape that broke the Team Builder: rows in file order are helmet, weapon, helmet, helmet, weapon.
        var rows = new[]
        {
            Row(1, 1, "Fire Rigid Circlet", "Helmet"),
            Row(1, 2, "Bow", "Weapon"),
            Row(2, 3, "Water Circlet of the Gods", "Helmet"),
            Row(3, 5, "Short Bow", "Weapon"),
            Row(3, 5, "Leather Bikini", "Armor"),
        };

        var gear = ItemSetGear.ForLevel(rows, 1, Types);

        Assert.Equal("Bow", gear.Weapon);
        Assert.Equal("Fire Rigid Circlet", gear.Helmet);

        // The set's only armor row starts at level 3-5 and nothing covers level 1, so the nearest window is used:
        // explicit gear is not gated by the tier rule, and a unit is better off equipped than bare.
        Assert.Equal("Leather Bikini", gear.Armor);

        // No rows of these types at all, so those slots stay empty.
        Assert.Null(gear.Shield);
        Assert.Null(gear.Accessory);
        Assert.True(gear.HasWeapon);
    }

    [Fact]
    public void ASetWithNoWeaponReportsNoWeapon()
    {
        var rows = new[] { Row(1, 1, "Parma", "Shield"), Row(1, 1, "Wrist Brace", "Armor") };

        var gear = ItemSetGear.ForLevel(rows, 1, Types);

        Assert.False(gear.HasWeapon);
        Assert.Equal("Parma", gear.Shield);
    }

    [Fact]
    public void RowsCoveringTheLevelWin()
    {
        var rows = new[]
        {
            Row(1, 1, "Bow", "Weapon"),
            Row(5, 5, "Short Bow", "Weapon"),
        };

        Assert.Equal("Bow", ItemSetGear.ForLevel(rows, 1, Types).Weapon);
        Assert.Equal("Short Bow", ItemSetGear.ForLevel(rows, 5, Types).Weapon);
    }

    [Fact]
    public void ALevelGapTakesTheNearestRowRatherThanLeavingTheSlotBare()
    {
        var rows = new[]
        {
            Row(1, 2, "Bow", "Weapon"),
            Row(9, 10, "Short Bow", "Weapon"),
        };

        // Level 4 is covered by neither; 1-2 is two away, 9-10 is five away.
        Assert.Equal("Bow", ItemSetGear.ForLevel(rows, 4, Types).Weapon);

        // And the higher window wins when it is nearer.
        Assert.Equal("Short Bow", ItemSetGear.ForLevel(rows, 8, Types).Weapon);
    }

    [Fact]
    public void AMisTaggedRowFollowsTheItemTypeAndWarns()
    {
        // Tagged Armor, but items.tok says Helmet: the engine would treat it as a helmet, so the tool must too.
        var rows = new[] { Row(1, 1, "Fire Rigid Circlet", "Armor"), Row(1, 1, "Bow", "Weapon") };
        var warnings = new List<string>();

        var gear = ItemSetGear.ForLevel(rows, 1, Types, warnings);

        Assert.Equal("Fire Rigid Circlet", gear.Helmet);
        Assert.Null(gear.Armor);
        Assert.Contains(warnings, warning => warning.Contains("tagged [Armor]") && warning.Contains("Helmet"));
    }

    [Fact]
    public void AnItemMissingFromItemsTokIsSkippedWithAWarning()
    {
        var rows = new[] { Row(1, 1, "Nonexistent Thing", "Weapon"), Row(1, 1, "Bow", "Weapon") };
        var warnings = new List<string>();

        var gear = ItemSetGear.ForLevel(rows, 1, Types, warnings);

        Assert.Equal("Bow", gear.Weapon);
        Assert.Contains(warnings, warning => warning.Contains("not in items.tok"));
    }

    [Fact]
    public void InSlotOrderIsItemsCompOrder()
    {
        var gear = new GearLoadout(Weapon: "Bow", Armor: "Wrist Brace", Shield: "Parma",
            Helmet: "Fire Rigid Circlet", Accessory: "Earth Emerald");

        Assert.Equal(new[] { "Bow", "Wrist Brace", "Parma", "Fire Rigid Circlet", "Earth Emerald" }, gear.InSlotOrder);

        var bare = new GearLoadout(Weapon: "Bow");
        Assert.Equal(new[] { "Bow", "", "", "", "" }, bare.InSlotOrder);
    }

    [Fact]
    public void TagsAreParsedFromAnyNoteShape()
    {
        Assert.Equal("Weapon", ItemSetGear.TagOf("[Weapon]"));
        Assert.Equal("Armor", ItemSetGear.TagOf("  [Armor]  "));
        Assert.Null(ItemSetGear.TagOf(null));
        Assert.Null(ItemSetGear.TagOf(""));
        Assert.Null(ItemSetGear.TagOf("[Weapon"));
    }
}
