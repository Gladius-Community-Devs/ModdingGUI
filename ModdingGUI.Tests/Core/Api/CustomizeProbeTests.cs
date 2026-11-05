using System.IO;
using System.Linq;
using ModdingGUI.Core.Api;
using ModdingGUI.Core.Tok;
using Xunit;

namespace ModdingGUI.Tests.Core.Api;

/// <summary>
/// The school file's CUSTOMIZE line is a plain unit key to the line editor and must stay one: it carries
/// the starting outfit, which the validator checks (an invalid outfit can crash the game).
/// </summary>
public sealed class SchoolCustomizeLineTests
{
    [Fact]
    public void TryKey_OnTheRealCustomizeLine()
    {
        string line = "        \tCUSTOMIZE:      1, \"\"";
        bool ok = TokLineEditing.TryKey(line, out string key, out string prefix, out string value);

        Assert.True(ok, $"TryKey failed for '{line}'");
        Assert.Equal("CUSTOMIZE", key);
        Assert.Equal("1, \"\"", value);
    }

    [Fact]
    public void RawLines_ContainCustomize()
    {
        if (!CleanBecCorpus.IsAvailable) return;

        string bec = CleanBecCorpus.Root!;
        string relative = "data/school/ursulanordagh.tok";
        var (lines, _) = RawLineFile.Read(bec, relative, "roster");

        var withKey = lines.Where(l => TokLineEditing.TryKey(l, out string k, out _, out _)
                                       && k == "CUSTOMIZE").ToArray();
        Assert.NotEmpty(withKey);
    }
}
