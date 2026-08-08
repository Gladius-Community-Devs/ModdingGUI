using ModdingGUI.Core.Tok;
using Xunit;

namespace ModdingGUI.Tests.Core.Tok;

/// <summary>
/// Pins the canonical tok encoding policy: ISO-8859-1 (Latin-1), which is a
/// bijection over all 256 byte values. This is what lets the Tok framework
/// round-trip non-ASCII content bit-exactly (PLAN.md: "Tok I/O: ISO-8859-1").
/// </summary>
public class TokEncodingTests
{
    [Fact]
    public void CanonicalEncoding_IsIso8859_ReturnsLatin1()
    {
        Assert.Equal("iso-8859-1", TokDocument.CanonicalEncoding.WebName);
    }

    [Fact]
    public void CanonicalEncoding_RoundTripsEveryByteValue()
    {
        for (int b = 0; b < 256; b++)
        {
            byte input = (byte)b;
            byte[] output = TokDocument.CanonicalEncoding.GetBytes(
                TokDocument.CanonicalEncoding.GetString(new[] { input }));
            Assert.Equal(new byte[] { input }, output);
        }
    }
}
