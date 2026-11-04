using System;
using System.IO;
using System.Linq;
using System.Text;
using ModdingGUI.Core.Api;
using Xunit;

namespace ModdingGUI.Tests.Core.Api;

/// <summary>
/// Tests for the lookup text tables, including the encoding regression: the corpus files
/// are Latin-1 (the pack tool reads them as "ansi"), so decoding them as UTF-8 replaced
/// high bytes with U+FFFD and re-encoded them as three-byte sequences.
/// </summary>
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

    private string ConfigDir => Path.Combine(_projectDir, "data", "config");

    /// <summary>Copy a real language table into the staged project; returns its path.</summary>
    private string Stage(string suffix)
    {
        string name = $"lookuptext_{suffix}.txt";
        string destination = Path.Combine(ConfigDir, name);
        File.Copy(Path.Combine(CleanBecCorpus.Root!, "data", "config", name), destination, overwrite: true);
        return destination;
    }

    // ---- parsing ------------------------------------------------------------

    [Fact]
    public void Load_ReadsVanillaAndModderFormats()
    {
        File.WriteAllText(_lookupPath, "1^Either^Either^Vanilla Text\r\n2^Modder Text\r\n");
        var api = new LookupTextApi();

        var table = api.Load(_projectDir);

        Assert.Equal("Vanilla Text", table[1]);
        Assert.Equal("Modder Text", table[2]);
    }

    [Fact]
    public void Paths_AreLanguageSpecific()
    {
        Assert.Equal("data/config/lookuptext_eng.txt", LookupLanguages.RelativePath(LookupLanguage.English));
        Assert.Equal("data/config/lookuptext_fra.txt", LookupLanguages.RelativePath(LookupLanguage.French));
        Assert.Equal("data/config/lookuptext_ger.txt", LookupLanguages.RelativePath(LookupLanguage.German));
        Assert.Equal("data/config/lookuptext_ita.txt", LookupLanguages.RelativePath(LookupLanguage.Italian));
        Assert.Equal("data/config/lookuptext_spa.txt", LookupLanguages.RelativePath(LookupLanguage.Spanish));
        Assert.Equal(5, LookupLanguages.All.Count);
    }

    // ---- encoding regression ------------------------------------------------

    [Fact]
    public void Load_VanillaEnglishTable_HasNoReplacementCharacters()
    {
        if (!CleanBecCorpus.IsAvailable) return;
        Stage("eng");

        var table = new LookupTextApi().Load(_projectDir);

        Assert.NotEmpty(table);
        var broken = table.Where(kv => kv.Value.Contains('\uFFFD')).Take(3).ToList();
        Assert.True(broken.Count == 0,
            "decoded replacement characters — file read with the wrong encoding: " +
            string.Join("; ", broken.Select(kv => $"{kv.Key}: {kv.Value}")));
    }

    [Fact]
    public void Update_ThenRevert_IsByteIdentical()
    {
        if (!CleanBecCorpus.IsAvailable) return;
        string path = Stage("eng");

        byte[] original = File.ReadAllBytes(path);
        var api = new LookupTextApi();

        var table = api.Load(_projectDir);
        int id = table.Keys.First();

        Assert.True(api.Update(_projectDir, id, "Temp edit"));
        Assert.Equal("Temp edit", api.Load(_projectDir)[id]);
        Assert.True(api.Update(_projectDir, id, table[id]));

        Assert.Equal(original, File.ReadAllBytes(path));
    }

    [Fact]
    public void Update_OnEveryLanguage_RoundTripsByteIdentically()
    {
        if (!CleanBecCorpus.IsAvailable) return;

        var api = new LookupTextApi();
        foreach (string suffix in new[] { "eng", "fra", "ger", "ita", "spa" })
        {
            string path = Stage(suffix);
            byte[] original = File.ReadAllBytes(path);
            var language = LanguageOf(suffix);

            var table = api.Load(_projectDir, language);
            int id = table.Keys.First();
            string text = table[id];

            Assert.True(api.Update(_projectDir, id, "Temp edit", language));
            Assert.True(api.Update(_projectDir, id, text, language));

            Assert.Equal(original, File.ReadAllBytes(path));
        }
    }

    [Fact]
    public void AvailableLanguages_ReportsWhatTheProjectShips()
    {
        var api = new LookupTextApi();
        Assert.Empty(api.AvailableLanguages(_projectDir));

        foreach (string suffix in new[] { "eng", "fra" })
        {
            File.WriteAllText(Path.Combine(ConfigDir, $"lookuptext_{suffix}.txt"),
                "1^Either^Either^x\r\n", Encoding.Latin1);
        }

        Assert.Equal(new[] { LookupLanguage.English, LookupLanguage.French },
            api.AvailableLanguages(_projectDir));
    }

    // ---- writes -------------------------------------------------------------

    [Fact]
    public void Update_WritesBackupAndPreservesVoiceColumns()
    {
        string original = "1^Either^Either^Old Text\r\n2^Other\r\n";
        File.WriteAllText(_lookupPath, original);
        var api = new LookupTextApi();

        bool updated = api.Update(_projectDir, 1, "New Text");

        Assert.True(updated);
        Assert.Equal("1^Either^Either^New Text\r\n2^Other\r\n", File.ReadAllText(_lookupPath));

        string historyDir = Path.Combine(_projectDir, ".mod-history", "data", "config", "lookuptext_eng.txt");
        string backup = Assert.Single(Directory.GetFiles(historyDir, "*.bak"));
        Assert.Equal(original, File.ReadAllText(backup));
    }

    [Fact]
    public void Add_AppendsNewIdAndWritesBackup()
    {
        string original = "1^First\r\n5^Fifth\r\n";
        File.WriteAllText(_lookupPath, original);
        var api = new LookupTextApi();

        int id = api.Add(_projectDir, "Sixth");

        Assert.Equal(6, id);
        Assert.Contains("6^Sixth", File.ReadAllText(_lookupPath));

        string historyDir = Path.Combine(_projectDir, ".mod-history", "data", "config", "lookuptext_eng.txt");
        string backup = Assert.Single(Directory.GetFiles(historyDir, "*.bak"));
        Assert.Equal(original, File.ReadAllText(backup));
    }

    [Fact]
    public void Add_KeepsTheNewIdAsTheLastAndHighestRow()
    {
        // Update_Strings_Bin.py derives the entry count from the LAST line, so
        // Update() must never reorder rows and Add() must append at the end.
        File.WriteAllText(_lookupPath, "1^Either^Either^One\r\n5^Either^Either^Five\r\n", Encoding.Latin1);

        int id = new LookupTextApi().Add(_projectDir, "Six");

        Assert.Equal(6, id);
        string last = File.ReadAllLines(_lookupPath)[^1];
        Assert.Equal("6^Six", last);
    }

    [Fact]
    public void Update_UnknownId_ReturnsFalseWithoutWriting()
    {
        File.WriteAllText(_lookupPath, "1^Either^Either^One\r\n", Encoding.Latin1);
        byte[] original = File.ReadAllBytes(_lookupPath);

        Assert.False(new LookupTextApi().Update(_projectDir, 999, "nope"));
        Assert.Equal(original, File.ReadAllBytes(_lookupPath));
    }

    private static LookupLanguage LanguageOf(string suffix) => suffix switch
    {
        "eng" => LookupLanguage.English,
        "fra" => LookupLanguage.French,
        "ger" => LookupLanguage.German,
        "ita" => LookupLanguage.Italian,
        "spa" => LookupLanguage.Spanish,
        _ => throw new ArgumentOutOfRangeException(nameof(suffix)),
    };
}
