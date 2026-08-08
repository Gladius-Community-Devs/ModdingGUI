using System.Drawing;
using ModdingGUI;
using Xunit;

namespace ModdingGUI.Tests;

/// <summary>
/// Tests for <see cref="LookupHelper"/> — the internal helper that loads and
/// queries the lookuptext_eng.txt ID-to-string table.
/// </summary>
public class LookupHelperTests : IDisposable
{
    private readonly string _tempDir;

    public LookupHelperTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"LookupTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private string TempFile(string name, string content)
    {
        string path = Path.Combine(_tempDir, name);
        File.WriteAllText(path, content);
        return path;
    }

    // LoadLookupTable ---------------------------------------------------------

    [Fact]
    public void LoadLookupTable_ParsesIdAndText()
    {
        string path = TempFile("lookup.tok", "1^Iron Sword\n2^Wooden Shield\n");
        var table = LookupHelper.LoadLookupTable(path);

        Assert.Equal(2, table.Count);
        Assert.Equal("Iron Sword", table[1]);
        Assert.Equal("Wooden Shield", table[2]);
    }

    [Fact]
    public void LoadLookupTable_HandlesExtraCaretColumns()
    {
        // Format is id^text^optional_extra — only text between first two ^ is used
        string path = TempFile("lookup.tok", "5^Fire Ring^some extra data\n");
        var table = LookupHelper.LoadLookupTable(path);

        Assert.Equal("Fire Ring", table[5]);
    }

    [Fact]
    public void LoadLookupTable_SkipsLinesWithNonIntegerId()
    {
        string path = TempFile("lookup.tok", "abc^Bad Line\n10^Good Line\n");
        var table = LookupHelper.LoadLookupTable(path);

        Assert.Single(table);
        Assert.Equal("Good Line", table[10]);
    }

    [Fact]
    public void LoadLookupTable_ReturnsEmpty_WhenFileDoesNotExist()
    {
        var table = LookupHelper.LoadLookupTable(@"C:\nonexistent\lookup.tok");
        Assert.Empty(table);
    }

    [Fact]
    public void LoadLookupTable_ReturnsEmpty_ForBlankFile()
    {
        string path = TempFile("empty.tok", "");
        var table = LookupHelper.LoadLookupTable(path);
        Assert.Empty(table);
    }

    [Fact]
    public void LoadLookupTable_VanillaFormat_StripsVoiceFieldsAndReturnsText()
    {
        // Vanilla format: id^speak^listen^localized text
        string path = TempFile("lookup.tok", "1^Either^Either^Iron Sword\n2^Masculine^Feminine^Wooden Shield\n");
        var table = LookupHelper.LoadLookupTable(path);

        Assert.Equal(2, table.Count);
        Assert.Equal("Iron Sword", table[1]);
        Assert.Equal("Wooden Shield", table[2]);
    }

    [Fact]
    public void LoadLookupTable_VanillaFormat_HeaderLinesSkipped()
    {
        // Header comment lines (## or //) must not crash and must be skipped.
        string content = "##8220\n// id^speak^listen^localized text\n1^Either^Either^Ruby\n";
        string path = TempFile("lookup.tok", content);
        var table = LookupHelper.LoadLookupTable(path);

        Assert.Single(table);
        Assert.Equal("Ruby", table[1]);
    }

    [Fact]
    public void LoadLookupTable_MixedFormat_BothFormatsInSameFile()
    {
        // Modder may partially strip the voice fields; both must parse correctly.
        string content = "1^Either^Either^Vanilla Entry\n2^Modder Entry\n";
        string path = TempFile("lookup.tok", content);
        var table = LookupHelper.LoadLookupTable(path);

        Assert.Equal("Vanilla Entry", table[1]);
        Assert.Equal("Modder Entry", table[2]);
    }

    // ResolveLookupValue ------------------------------------------------------

    [Fact]
    public void ResolveLookupValue_ReturnsText_WhenIdFound()
    {
        var lookup = new Dictionary<int, string> { [42] = "Gladius" };
        string result = LookupHelper.ResolveLookupValue("ITEMDISPLAYNAMEID: 42", lookup, out int? id);

        Assert.Equal("Gladius", result);
        Assert.Equal(42, id);
    }

    [Fact]
    public void ResolveLookupValue_ReturnsFallback_WhenIdNotInTable()
    {
        var lookup = new Dictionary<int, string>();
        string result = LookupHelper.ResolveLookupValue("ITEMDISPLAYNAMEID: 99", lookup, out int? id, "DefaultName");

        Assert.Equal("DefaultName", result);
        Assert.Equal(99, id);
    }

    [Fact]
    public void ResolveLookupValue_ReturnsFallback_WhenValueIsNotAnInt()
    {
        var lookup = new Dictionary<int, string> { [1] = "Whatever" };
        string result = LookupHelper.ResolveLookupValue("ITEMDISPLAYNAMEID: notanumber", lookup, out int? id, "Fallback");

        Assert.Equal("Fallback", result);
        Assert.Null(id);
    }

    [Fact]
    public void ResolveLookupValue_ReturnsFallback_WhenNoColon()
    {
        var lookup = new Dictionary<int, string> { [1] = "Whatever" };
        string result = LookupHelper.ResolveLookupValue("NOCOLON", lookup, out int? id, "Fallback");

        Assert.Equal("Fallback", result);
        Assert.Null(id);
    }

    // GetNextAvailableLookupId ------------------------------------------------

    [Fact]
    public void GetNextAvailableLookupId_ReturnsMaxPlusOne()
    {
        var lookup = new Dictionary<int, string> { [1] = "A", [5] = "B", [3] = "C" };
        Assert.Equal(6, LookupHelper.GetNextAvailableLookupId(lookup));
    }

    [Fact]
    public void GetNextAvailableLookupId_Returns1_ForEmptyTable()
    {
        Assert.Equal(1, LookupHelper.GetNextAvailableLookupId(new Dictionary<int, string>()));
    }

    [Fact]
    public void GetNextAvailableLookupId_Returns1_ForNullTable()
    {
        Assert.Equal(1, LookupHelper.GetNextAvailableLookupId(null!));
    }
}
