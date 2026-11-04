using System.Collections.Generic;
using ModdingGUI.Core.Api;
using Xunit;

namespace ModdingGUI.Tests.Core.Api;

/// <summary>
/// Tests for <see cref="LookupTextResolver"/>, which turns a <c>KEY: &lt;id&gt;</c> tok line
/// into display text. Replaces the old shell-side <c>LookupHelper</c> tests; the file
/// reading/writing half those tests covered now lives in <see cref="LookupTextApi"/> and is
/// covered by <see cref="LookupTextApiTests"/>.
/// </summary>
public sealed class LookupTextResolverTests
{
    private static IReadOnlyDictionary<int, string> Table() => new Dictionary<int, string>
    {
        [42] = "Iron Sword",
        [7] = "Shield",
    };

    [Fact]
    public void ResolveValue_ReturnsTextAndId()
    {
        string result = LookupTextResolver.ResolveValue("ITEMDISPLAYNAMEID: 42", Table(), out int? id);

        Assert.Equal("Iron Sword", result);
        Assert.Equal(42, id);
    }

    [Fact]
    public void ResolveValue_MissingId_ReturnsFallbackButStillReportsTheId()
    {
        string result = LookupTextResolver.ResolveValue("ITEMDISPLAYNAMEID: 99", Table(), out int? id, "DefaultName");

        Assert.Equal("DefaultName", result);
        Assert.Equal(99, id);
    }

    [Fact]
    public void ResolveValue_NonNumericId_ReturnsFallbackAndNoId()
    {
        string result = LookupTextResolver.ResolveValue("ITEMDISPLAYNAMEID: notanumber", Table(), out int? id, "Fallback");

        Assert.Equal("Fallback", result);
        Assert.Null(id);
    }

    [Fact]
    public void ResolveValue_LineWithoutColon_ReturnsFallbackAndNoId()
    {
        string result = LookupTextResolver.ResolveValue("NOCOLON", Table(), out int? id, "Fallback");

        Assert.Equal("Fallback", result);
        Assert.Null(id);
    }

    [Fact]
    public void ResolveValue_ToleratesSurroundingWhitespace()
    {
        Assert.Equal("Shield", LookupTextResolver.ResolveValue("  SKILLDISPLAYNAMEID:   7  ", Table(), out int? id));
        Assert.Equal(7, id);
    }

    [Fact]
    public void ResolveValue_EmptyArguments_ReturnFallback()
    {
        Assert.Equal("x", LookupTextResolver.ResolveValue("", Table(), out _, "x"));
        Assert.Equal("y", LookupTextResolver.ResolveValue("KEY: 42", new Dictionary<int, string>(), out _, "y"));
    }
}
