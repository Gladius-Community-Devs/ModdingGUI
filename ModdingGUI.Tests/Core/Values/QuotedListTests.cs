using System;
using System.Linq;
using ModdingGUI.Core.Values;
using Xunit;

namespace ModdingGUI.Tests.Core.Values;

/// <summary>
/// The roster argument lists. Comma splitting is wrong here, and the bug that prompted these tests was a loader that
/// never read <c>ITEMSCOMP</c> at all: a unit loaded from a roster arrived with no gear, and saving rewrote its line
/// with five empty slots.
/// </summary>
public sealed class QuotedListTests
{
    [Fact]
    public void SplitsTheFiveSlotShapeOfAnItemsCompLine()
    {
        var slots = QuotedList.SplitUnquoted("\"Machera\", \"Short Cape\", \"\", \"\", \"Air Badge of Aeris\"");

        Assert.Equal(new[] { "Machera", "Short Cape", "", "", "Air Badge of Aeris" }, slots);
    }

    [Fact]
    public void ACommaInsideQuotesDoesNotSplit()
    {
        var slots = QuotedList.SplitUnquoted("\"Ring of Might, Greater\", \"\", \"\", \"\", \"\"");

        Assert.Equal("Ring of Might, Greater", slots[0]);
        Assert.Equal(5, slots.Count);
    }

    [Fact]
    public void JoinWritesEmptySlotsAsEmptyQuotes()
    {
        Assert.Equal("\"Machera\", \"Short Cape\", \"\", \"\", \"Badge\"",
            QuotedList.Join(new[] { "Machera", "Short Cape", null, "", "Badge" }));
    }

    [Fact]
    public void JoinThenSplitRoundTrips()
    {
        string[] slots = { "Earth Masakari", "Earth Heavy Fur Skirt", "Reinforced Wooden Shield", "", "Earth Ring of Solum" };

        Assert.Equal(slots, QuotedList.SplitUnquoted(QuotedList.Join(slots)));
    }

    [Fact]
    public void AnEmptyValueIsNoParts()
    {
        Assert.Empty(QuotedList.Split(null));
        Assert.Empty(QuotedList.Split("   "));
    }
}
