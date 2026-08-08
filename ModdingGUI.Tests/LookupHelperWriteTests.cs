using System.Drawing;
using ModdingGUI;
using Xunit;

namespace ModdingGUI.Tests;

/// <summary>
/// Additional tests for <see cref="LookupHelper"/> write operations
/// (UpdateLookupEntry, AddNewLookupEntry) that are not covered in LookupHelperTests.
/// </summary>
public class LookupHelperWriteTests : IDisposable
{
    private readonly string _tempDir;

    public LookupHelperWriteTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"LookupWriteTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private string WriteTempFile(string name, string content)
    {
        string path = Path.Combine(_tempDir, name);
        File.WriteAllText(path, content);
        return path;
    }

    // UpdateLookupEntry -------------------------------------------------------

    [Fact]
    public void UpdateLookupEntry_ExistingId_ReturnsTrue()
    {
        string path = WriteTempFile("lookup.tok", "1^OldText\n2^OtherText\n");
        bool result = LookupHelper.UpdateLookupEntry(path, 1, "NewText");
        Assert.True(result);
    }

    [Fact]
    public void UpdateLookupEntry_ExistingId_UpdatesTextInFile()
    {
        string path = WriteTempFile("lookup.tok", "1^OldText\n2^OtherText\n");
        LookupHelper.UpdateLookupEntry(path, 1, "NewText");

        var table = LookupHelper.LoadLookupTable(path);
        Assert.Equal("NewText", table[1]);
    }

    [Fact]
    public void UpdateLookupEntry_ExistingId_DoesNotChangeOtherEntries()
    {
        string path = WriteTempFile("lookup.tok", "1^OldText\n2^OtherText\n");
        LookupHelper.UpdateLookupEntry(path, 1, "NewText");

        var table = LookupHelper.LoadLookupTable(path);
        Assert.Equal("OtherText", table[2]);
    }

    [Fact]
    public void UpdateLookupEntry_MissingId_ReturnsFalse()
    {
        string path = WriteTempFile("lookup.tok", "1^OldText\n");
        bool result = LookupHelper.UpdateLookupEntry(path, 99, "NewText");
        Assert.False(result);
    }

    [Fact]
    public void UpdateLookupEntry_MissingId_DoesNotModifyFile()
    {
        string path = WriteTempFile("lookup.tok", "1^OldText\n");
        string originalContent = File.ReadAllText(path);
        LookupHelper.UpdateLookupEntry(path, 99, "NewText");
        Assert.Equal(originalContent, File.ReadAllText(path));
    }

    [Fact]
    public void UpdateLookupEntry_FileNotFound_ReturnsFalse()
    {
        bool result = LookupHelper.UpdateLookupEntry(
            Path.Combine(_tempDir, "nonexistent.tok"), 1, "text");
        Assert.False(result);
    }

    [Fact]
    public void UpdateLookupEntry_PreservesExtraColumnsOnOtherLines()
    {
        // Format: id^text^extra — update one, verify extra column on another is intact
        string path = WriteTempFile("lookup.tok", "1^TextA^ExtraA\n2^TextB^ExtraB\n");
        LookupHelper.UpdateLookupEntry(path, 1, "TextNew");

        string[] rawLines = File.ReadAllLines(path);
        Assert.Contains("^ExtraB", rawLines[1]); // Other line untouched
    }

    [Fact]
    public void UpdateLookupEntry_VanillaFormat_UpdatesTextFieldOnly()
    {
        // Vanilla line: id^speak^listen^text — updating must replace only the text.
        string path = WriteTempFile("lookup.tok", "1^Either^Either^Old Name\n");
        LookupHelper.UpdateLookupEntry(path, 1, "New Name");

        string[] rawLines = File.ReadAllLines(path);
        Assert.Equal("1^Either^Either^New Name", rawLines[0]);
    }

    [Fact]
    public void UpdateLookupEntry_VanillaFormat_ReloadsCorrectly()
    {
        // After updating a vanilla-format line the new text must be readable.
        string path = WriteTempFile("lookup.tok", "5^Masculine^Either^Old Text\n");
        LookupHelper.UpdateLookupEntry(path, 5, "New Text");

        var table = LookupHelper.LoadLookupTable(path);
        Assert.Equal("New Text", table[5]);
    }

    [Fact]
    public void UpdateLookupEntry_ModderFormat_DoesNotInsertVoiceFields()
    {
        // Modder format has no voice fields; updating must not add them.
        string path = WriteTempFile("lookup.tok", "3^Old Entry\n");
        LookupHelper.UpdateLookupEntry(path, 3, "New Entry");

        string[] rawLines = File.ReadAllLines(path);
        Assert.Equal("3^New Entry", rawLines[0]);
    }

    // AddNewLookupEntry -------------------------------------------------------

    [Fact]
    public void AddNewLookupEntry_ValidText_ReturnsNewId()
    {
        string path = WriteTempFile("lookup.tok", "1^First\n5^Fifth\n");
        var table = LookupHelper.LoadLookupTable(path);
        int? newId = LookupHelper.AddNewLookupEntry(path, "NewEntry", table);

        Assert.NotNull(newId);
        Assert.Equal(6, newId); // max(1,5)+1 = 6
    }

    [Fact]
    public void AddNewLookupEntry_ValidText_EntryAppearsInFile()
    {
        string path = WriteTempFile("lookup.tok", "1^First\n");
        var table = LookupHelper.LoadLookupTable(path);
        int? newId = LookupHelper.AddNewLookupEntry(path, "NewEntry", table);

        var reloaded = LookupHelper.LoadLookupTable(path);
        Assert.True(reloaded.ContainsKey(newId!.Value));
        Assert.Equal("NewEntry", reloaded[newId.Value]);
    }

    [Fact]
    public void AddNewLookupEntry_EmptyText_ReturnsNull()
    {
        string path = WriteTempFile("lookup.tok", "1^First\n");
        var table = LookupHelper.LoadLookupTable(path);
        int? result = LookupHelper.AddNewLookupEntry(path, "", table);
        Assert.Null(result);
    }

    [Fact]
    public void AddNewLookupEntry_WhitespaceText_ReturnsNull()
    {
        string path = WriteTempFile("lookup.tok", "1^First\n");
        var table = LookupHelper.LoadLookupTable(path);
        int? result = LookupHelper.AddNewLookupEntry(path, "   ", table);
        Assert.Null(result);
    }

    [Fact]
    public void AddNewLookupEntry_EmptyTable_StartsAtId1()
    {
        string path = WriteTempFile("empty.tok", "");
        var table = new Dictionary<int, string>();
        int? newId = LookupHelper.AddNewLookupEntry(path, "First", table);
        Assert.Equal(1, newId);
    }

    [Fact]
    public void AddNewLookupEntry_DoesNotOverwriteExistingEntries()
    {
        string path = WriteTempFile("lookup.tok", "1^Original\n");
        var table = LookupHelper.LoadLookupTable(path);
        LookupHelper.AddNewLookupEntry(path, "NewEntry", table);

        var reloaded = LookupHelper.LoadLookupTable(path);
        Assert.Equal("Original", reloaded[1]);
    }
}
