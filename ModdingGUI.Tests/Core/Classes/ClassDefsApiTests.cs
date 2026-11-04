using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ModdingGUI.Core.Api;
using ModdingGUI.Core.Classes;
using Xunit;
using Xunit.Abstractions;

namespace ModdingGUI.Tests.Core.Classes;

/// <summary>
/// End-to-end tests for the classdefs catalog and API against a staged copy of the real
/// vanilla <c>classdefs.tok</c> (never the read-only corpus itself).
/// </summary>
public sealed class ClassDefsApiTests : IDisposable
{
    private const int VanillaClassCount = 128;

    private readonly string _projectDir;
    private readonly string _classDefsPath;
    private readonly ITestOutputHelper _output;

    public ClassDefsApiTests(ITestOutputHelper output)
    {
        _output = output;
        _projectDir = Path.Combine(Path.GetTempPath(),
            "ModdingGUI.ClassDefs-" + Guid.NewGuid().ToString("N"));

        string configDir = Path.Combine(_projectDir, "data", "config");
        Directory.CreateDirectory(configDir);
        _classDefsPath = Path.Combine(configDir, "classdefs.tok");

        if (!CleanBecCorpus.IsAvailable) return;

        File.Copy(Path.Combine(CleanBecCorpus.Root!, "data", "config", "classdefs.tok"),
            _classDefsPath, overwrite: true);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_projectDir)) Directory.Delete(_projectDir, recursive: true);
        }
        catch (IOException)
        {
            // best effort temp cleanup
        }
    }

    private static bool Ready => CleanBecCorpus.IsAvailable;

    // ---- catalog ------------------------------------------------------------

    [Fact]
    public void ShippedCatalog_DescribesTheCorpus()
    {
        var catalog = ClassDefsCatalog.Shipped();

        Assert.Equal(VanillaClassCount, catalog.Classes.Count);
        Assert.NotNull(catalog.FindClass("Amazon"));

        // ITEMCAT: three arguments, repeatable, engine id 27.
        var itemCat = catalog.Find("ITEMCAT");
        Assert.NotNull(itemCat);
        Assert.Equal(3, itemCat!.Arity);
        Assert.True(itemCat.AllowsRepetition);
        Assert.Equal(27, itemCat.EngineId);
        Assert.Contains("weapon", itemCat.Slots[0].Values);
    }

    [Fact]
    public void ShippedCatalog_CarriesEngineOnlyKeys()
    {
        var catalog = ClassDefsCatalog.Shipped();

        // In the engine's table (0x803038B0) but never used by the corpus.
        foreach (var key in new[] { "BASECLASSNAME", "ITEMINDIV", "SHADOWSET" })
        {
            var spec = catalog.Find(key);
            Assert.NotNull(spec);
            Assert.True(spec!.EngineOnly, $"{key} should be marked engine-only");
            Assert.True(spec.EngineKnown);
        }
    }

    // ---- read ---------------------------------------------------------------

    [Fact]
    public void List_ReturnsEveryClassInFileOrder()
    {
        if (!Ready) return;

        var names = new ClassDefsApi().List(_projectDir);

        Assert.Equal(VanillaClassCount, names.Count);
        Assert.Equal("Amazon", names[0]);
        Assert.Contains("Legionnaire", names);
    }

    [Fact]
    public void Get_ReturnsFieldsInFileOrder_CommentsExcluded()
    {
        if (!Ready) return;

        var amazon = new ClassDefsApi().Get(_projectDir, "Amazon");

        Assert.NotNull(amazon);
        Assert.Equal("Amazon", amazon!.Name);
        Assert.Equal("4458", amazon.Value("DISPLAYNAMEID"));
        Assert.Equal("\"Amazon\"", amazon.Value("MESH"));

        // Repeating keys come back in file order, comments are not fields.
        var attributes = amazon.Values("ATTRIBUTE");
        Assert.Equal(new[] { "\"Support\"", "\"Steppes\"", "\"Female\"", "\"Human\"" }, attributes);
        Assert.DoesNotContain(amazon.Fields, f => f.Key.Contains('/'));
    }

    // ---- write --------------------------------------------------------------

    [Fact]
    public void SetField_ThenRevert_IsByteIdentical()
    {
        if (!Ready) return;

        byte[] original = File.ReadAllBytes(_classDefsPath);
        var api = new ClassDefsApi();

        api.SetField(_projectDir, "Amazon", "MESH", "\"Archer\"");
        string changed = File.ReadAllText(_classDefsPath, ModdingGUI.Core.Tok.TokDocument.CanonicalEncoding);
        Assert.Contains("MESH: \"Archer\"", changed);

        api.SetField(_projectDir, "Amazon", "MESH", "\"Amazon\"");
        byte[] restored = File.ReadAllBytes(_classDefsPath);
        Assert.Equal(original, restored);
    }

    [Fact]
    public void SetField_OnRepeatingKey_KeepsOnlyOneLine()
    {
        if (!Ready) return;

        var api = new ClassDefsApi();
        int before = api.Get(_projectDir, "Amazon")!.Count("ATTRIBUTE");
        Assert.True(before > 1);

        api.SetField(_projectDir, "Amazon", "ATTRIBUTE", "\"Support\"");
        Assert.Equal(1, api.Get(_projectDir, "Amazon")!.Count("ATTRIBUTE"));

        api.RemoveField(_projectDir, "Amazon", "ATTRIBUTE");
        Assert.Equal(0, api.Get(_projectDir, "Amazon")!.Count("ATTRIBUTE"));
    }

    [Fact]
    public void AddField_ThenRemoveField_RestoresTheFile()
    {
        if (!Ready) return;

        byte[] original = File.ReadAllBytes(_classDefsPath);
        var api = new ClassDefsApi();

        api.AddField(_projectDir, "Amazon", "SHADOWSET", "\"AmazonShadow\"");
        Assert.Equal("\"AmazonShadow\"", api.Get(_projectDir, "Amazon")!.Value("SHADOWSET"));

        api.RemoveField(_projectDir, "Amazon", "SHADOWSET");
        Assert.Null(api.Get(_projectDir, "Amazon")!.Value("SHADOWSET"));
        Assert.Equal(original, File.ReadAllBytes(_classDefsPath));
    }

    [Fact]
    public void Add_ThenRemove_ClassRestoresTheFile()
    {
        if (!Ready) return;

        byte[] original = File.ReadAllBytes(_classDefsPath);
        var api = new ClassDefsApi();

        api.Add(_projectDir, "TestClass", new[]
        {
            new ClassFieldDto("DISPLAYNAMEID", "4458"),
            new ClassFieldDto("MESH", "\"Amazon\""),
        });

        Assert.Equal(VanillaClassCount + 1, api.List(_projectDir).Count);
        var added = api.Get(_projectDir, "TestClass");
        Assert.NotNull(added);
        Assert.Equal("\"Amazon\"", added!.Value("MESH"));

        api.Remove(_projectDir, "TestClass");
        Assert.Equal(VanillaClassCount, api.List(_projectDir).Count);
        Assert.Equal(original, File.ReadAllBytes(_classDefsPath));
    }

    [Fact]
    public void Rename_ChangesOnlyTheHeader()
    {
        if (!Ready) return;

        var api = new ClassDefsApi();
        api.Rename(_projectDir, "Amazon", "AmazonRenamed");

        Assert.Null(api.Get(_projectDir, "Amazon"));
        var renamed = api.Get(_projectDir, "AmazonRenamed");
        Assert.NotNull(renamed);
        Assert.Equal("4458", renamed!.Value("DISPLAYNAMEID"));

        api.Rename(_projectDir, "AmazonRenamed", "Amazon");
    }

    [Fact]
    public void UnknownClass_Throws()
    {
        if (!Ready) return;

        var api = new ClassDefsApi();
        Assert.Null(api.Get(_projectDir, "NoSuchClass"));
        Assert.Throws<InvalidOperationException>(
            () => api.SetField(_projectDir, "NoSuchClass", "MESH", "\"Amazon\""));
    }

    [Fact]
    public void Writes_LeaveABackupInModHistory()
    {
        if (!Ready) return;

        var api = new ClassDefsApi();
        api.SetField(_projectDir, "Amazon", "MESH", "\"Archer\"");

        // History mirrors the edited file's relative path, with the file name itself
        // acting as the leaf directory: .mod-history/data/config/classdefs.tok/*.bak
        string historyDir = Path.Combine(_projectDir, ".mod-history", "data", "config", "classdefs.tok");
        Assert.True(Directory.Exists(historyDir), "expected a .mod-history backup folder");
        Assert.NotEmpty(Directory.GetFiles(historyDir, "*.bak"));

        api.SetField(_projectDir, "Amazon", "MESH", "\"Amazon\"");
    }

    // ---- validation ---------------------------------------------------------

    [Fact]
    public void Validate_EveryVanillaClass_HasNoWarnings()
    {
        if (!Ready) return;

        var api = new ClassDefsApi();
        var catalog = ClassDefsCatalog.Shipped();

        var warnings = new List<ClassValidationIssue>();
        foreach (var classDef in api.GetAll(_projectDir))
        {
            warnings.AddRange(ClassDefsValidator.Validate(classDef, catalog)
                .Where(i => i.Severity == ClassValidationSeverity.Warning));
        }

        string grouped = string.Join("\n  ", warnings
            .GroupBy(w => (w.Code, w.Field))
            .Select(g => $"{g.Key.Code} / {g.Key.Field}: {g.Count()} line(s), " +
                         $"e.g. {g.First().ClassName}: {g.First().Message}"));

        Assert.True(warnings.Count == 0,
            $"{warnings.Count} warning(s) on vanilla class data:\n  {grouped}");
    }
}
