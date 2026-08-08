using System.Drawing;
using ModdingGUI;
using Xunit;

namespace ModdingGUI.Tests;

/// <summary>
/// Tests for <see cref="GridFile"/> — covers default construction, dirty
/// tracking, tag assignment, cell read/write, and value inspection.
/// </summary>
public class GridFileTests
{
    // Construction -------------------------------------------------------------

    [Fact]
    public void Constructor_NewGrid_IsNotDirty()
    {
        var grid = new GridFile();
        Assert.False(grid.IsDirty);
    }

    [Fact]
    public void Constructor_DefaultTeamTags_AreAssignedToFirstSlots()
    {
        var grid = new GridFile();
        string[] expected = GridFile.DefaultTeamTagsMasterList;

        for (int i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i], grid.GetTagNameForSlot(i));
            Assert.True(grid.IsSlotActive(i), $"Slot {i} ({expected[i]}) should be active by default.");
        }
    }

    [Fact]
    public void Constructor_DefaultGameplayTags_AreAssignedAfterTeamTags()
    {
        var grid = new GridFile();
        int offset = GridFile.DefaultTeamTagsMasterList.Length;
        string[] gameTags = GridFile.DefaultGameplayTagsMasterList;

        for (int i = 0; i < gameTags.Length; i++)
        {
            int slot = offset + i;
            Assert.Equal(gameTags[i], grid.GetTagNameForSlot(slot));
            Assert.True(grid.IsSlotActive(slot));
        }
    }

    [Fact]
    public void Constructor_AllCells_AreZero()
    {
        var grid = new GridFile();
        for (int r = 0; r < GridFile.ArenaSize; r++)
            for (int c = 0; c < GridFile.ArenaSize; c++)
                Assert.Equal(0, grid.GetValue(r, c));
    }

    // GetValue / SetValue ------------------------------------------------------

    [Fact]
    public void SetValue_ThenGetValue_ReturnsSameValue()
    {
        var grid = new GridFile();
        grid.SetValue(5, 10, 42);
        Assert.Equal(42, grid.GetValue(5, 10));
    }

    [Fact]
    public void SetValue_MarksDirty()
    {
        var grid = new GridFile();
        Assert.False(grid.IsDirty);
        grid.SetValue(0, 0, 1);
        Assert.True(grid.IsDirty);
    }

    [Fact]
    public void SetValue_SameValueTwice_DoesNotChangeDirtyState_IfAlreadyDirty()
    {
        var grid = new GridFile();
        grid.SetValue(0, 0, 5);
        Assert.True(grid.IsDirty);
        // Setting the same value again — still dirty because it was already marked
        grid.SetValue(0, 0, 5);
        Assert.True(grid.IsDirty);
    }

    [Fact]
    public void SetValue_SettingZeroOnFreshGrid_DoesNotMarkDirty()
    {
        var grid = new GridFile();
        grid.SetValue(3, 3, 0); // Same as default
        Assert.False(grid.IsDirty);
    }

    [Fact]
    public void SetValue_DifferentCells_DoNotInterfer()
    {
        var grid = new GridFile();
        grid.SetValue(0, 0, 7);
        grid.SetValue(1, 1, 13);

        Assert.Equal(7, grid.GetValue(0, 0));
        Assert.Equal(13, grid.GetValue(1, 1));
        Assert.Equal(0, grid.GetValue(2, 2));
    }

    // AssignTagToSlot ----------------------------------------------------------

    [Fact]
    public void AssignTagToSlot_UpdatesTagName()
    {
        var grid = new GridFile();
        grid.AssignTagToSlot(0, "CustomTag", true);
        Assert.Equal("CustomTag", grid.GetTagNameForSlot(0));
    }

    [Fact]
    public void AssignTagToSlot_UpdatesActiveState()
    {
        var grid = new GridFile();
        grid.AssignTagToSlot(0, "Start1", false);
        Assert.False(grid.IsSlotActive(0));
    }

    [Fact]
    public void AssignTagToSlot_MarksDirty_WhenNameChanges()
    {
        var grid = new GridFile();
        Assert.False(grid.IsDirty);
        grid.AssignTagToSlot(0, "NewName", true);
        Assert.True(grid.IsDirty);
    }

    [Fact]
    public void AssignTagToSlot_NegativeIndex_DoesNotThrow()
    {
        var grid = new GridFile();
        var ex = Record.Exception(() => grid.AssignTagToSlot(-1, "Tag", true));
        Assert.Null(ex);
        Assert.False(grid.IsDirty);
    }

    [Fact]
    public void AssignTagToSlot_IndexEqualToNumSlots_DoesNotThrow()
    {
        var grid = new GridFile();
        var ex = Record.Exception(() => grid.AssignTagToSlot(GridFile.NumSlots, "Tag", true));
        Assert.Null(ex);
        Assert.False(grid.IsDirty);
    }

    [Fact]
    public void AssignTagToSlot_EmptyName_StoresNoValue()
    {
        var grid = new GridFile();
        grid.AssignTagToSlot(0, string.Empty, false);
        Assert.Equal(GridFile.NO_VALUE, grid.GetTagNameForSlot(0));
    }

    // GetTagNameForSlot / IsSlotActive boundary --------------------------------

    [Fact]
    public void GetTagNameForSlot_OutOfRange_ReturnsEmpty()
    {
        var grid = new GridFile();
        Assert.Equal(string.Empty, grid.GetTagNameForSlot(-1));
        Assert.Equal(string.Empty, grid.GetTagNameForSlot(GridFile.NumSlots));
    }

    [Fact]
    public void IsSlotActive_OutOfRange_ReturnsFalse()
    {
        var grid = new GridFile();
        Assert.False(grid.IsSlotActive(-1));
        Assert.False(grid.IsSlotActive(GridFile.NumSlots));
    }

    // GetInfoForValue ----------------------------------------------------------

    [Fact]
    public void GetInfoForValue_Zero_ReturnsEmptyLabel()
    {
        var grid = new GridFile();
        Assert.Equal("(empty)", grid.GetInfoForValue(0));
    }

    [Fact]
    public void GetInfoForValue_Bit0Set_ReturnsFirstTagName()
    {
        var grid = new GridFile();
        // Bit 0 → slot 0 → "Start1" by default
        string info = grid.GetInfoForValue(1);
        Assert.Contains("Start1", info);
    }

    [Fact]
    public void GetInfoForValue_MultipleBitsSet_ContainsAllTagNames()
    {
        var grid = new GridFile();
        // Bit 0 = Start1, Bit 1 = Start2
        string info = grid.GetInfoForValue(3);
        Assert.Contains("Start1", info);
        Assert.Contains("Start2", info);
    }

    [Fact]
    public void GetInfoForValue_InactiveSlotBit_ReportsInactiveSlot()
    {
        var grid = new GridFile();
        // Deactivate slot 0 so its bit is "inactive"
        grid.AssignTagToSlot(0, "Start1", false);

        string info = grid.GetInfoForValue(1); // bit 0 set, but slot 0 is inactive
        Assert.Contains("Inactive Slot", info);
    }

    // GetColor -----------------------------------------------------------------

    [Fact]
    public void GetColor_ZeroCell_ReturnsWhite()
    {
        var grid = new GridFile();
        Assert.Equal(Color.White, grid.GetColor(0, 0));
    }

    [Fact]
    public void GetColor_NonZeroCell_ReturnsNonWhite()
    {
        var grid = new GridFile();
        grid.SetValue(0, 0, 1); // bit 0 set
        Color c = grid.GetColor(0, 0);
        Assert.NotEqual(Color.White, c);
    }

    // Constants ----------------------------------------------------------------

    [Fact]
    public void Constants_ArenaSize_Is32()
    {
        Assert.Equal(32, GridFile.ArenaSize);
    }

    [Fact]
    public void Constants_NumSlots_Is30()
    {
        Assert.Equal(30, GridFile.NumSlots);
    }

    [Fact]
    public void AllKnownTagsMasterList_ContainsTeamAndGameplayTags()
    {
        foreach (var tag in GridFile.DefaultTeamTagsMasterList)
            Assert.Contains(tag, GridFile.AllKnownTagsMasterList);

        foreach (var tag in GridFile.DefaultGameplayTagsMasterList)
            Assert.Contains(tag, GridFile.AllKnownTagsMasterList);
    }
}
