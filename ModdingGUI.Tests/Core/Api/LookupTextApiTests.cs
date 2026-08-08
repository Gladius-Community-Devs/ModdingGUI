using System;
using System.IO;
using ModdingGUI.Core.Api;

namespace ModdingGUI.Tests.Core.Api;

public sealed class LookupTextApiTests : IDisposable
{
    private readonly string _projectDir;
    private readonly string _lookupPath;

    public LookupTextApiTests()
    {
        _projectDir = Path.Combine(Path.GetTempPath(), "ModdingGUI.LookupTextApiTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_projectDir, "data", "config"));
        _lookupPath = Path.Combine(_projectDir, "data", "config", "lookuptext_eng.txt");
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_projectDir)) Directory.Delete(_projectDir, recursive: true); }
        catch { }
    }

    [Xunit.Fact]
    public void Load_ReadsVanillaAndModderFormats()
    {
        File.WriteAllText(_lookupPath, "1^Either^Either^Vanilla Text\r\n2^Modder Text\r\n");
        var api = new LookupTextApi();

        var table = api.Load(_projectDir);

        Xunit.Assert.Equal("Vanilla Text", table[1]);
        Xunit.Assert.Equal("Modder Text", table[2]);
    }

    [Xunit.Fact]
    public void Update_WritesBackupAndPreservesVoiceColumns()
    {
        string original = "1^Either^Either^Old Text\r\n2^Other\r\n";
        File.WriteAllText(_lookupPath, original);
        var api = new LookupTextApi();

        bool updated = api.Update(_projectDir, 1, "New Text");

        Xunit.Assert.True(updated);
        Xunit.Assert.Equal("1^Either^Either^New Text\r\n2^Other\r\n", File.ReadAllText(_lookupPath));

        string historyDir = Path.Combine(_projectDir, ".mod-history", "data", "config", "lookuptext_eng.txt");
        string backup = Xunit.Assert.Single(Directory.GetFiles(historyDir, "*.bak"));
        Xunit.Assert.Equal(original, File.ReadAllText(backup));
    }

    [Xunit.Fact]
    public void Add_AppendsNewIdAndWritesBackup()
    {
        string original = "1^First\r\n5^Fifth\r\n";
        File.WriteAllText(_lookupPath, original);
        var api = new LookupTextApi();

        int id = api.Add(_projectDir, "Sixth");

        Xunit.Assert.Equal(6, id);
        Xunit.Assert.Contains("6^Sixth", File.ReadAllText(_lookupPath));

        string historyDir = Path.Combine(_projectDir, ".mod-history", "data", "config", "lookuptext_eng.txt");
        string backup = Xunit.Assert.Single(Directory.GetFiles(historyDir, "*.bak"));
        Xunit.Assert.Equal(original, File.ReadAllText(backup));
    }
}
