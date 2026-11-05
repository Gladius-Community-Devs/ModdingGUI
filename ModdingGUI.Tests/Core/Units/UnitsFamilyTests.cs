using System;
using System.IO;
using System.Linq;
using ModdingGUI.Core.Api;
using ModdingGUI.Core.Units;
using Xunit;

namespace ModdingGUI.Tests.Core.Units;

/// <summary>
/// The whole <c>data/units</c> family in one gate: every file round-trips byte for byte, every family
/// validates without warnings against vanilla, and the cross-file references (gladiator → set indexes)
/// hold for the entire corpus.
/// </summary>
public sealed class UnitsFamilyTests : IDisposable
{
    private static readonly string[] Files =
    {
        "gladiators.txt", "statsets.txt", "skillsets.txt", "itemsets.txt", "schools.txt", "tints.txt",
    };

    private readonly string _project;
    private readonly string _bec;

    public UnitsFamilyTests()
    {
        _project = Path.Combine(Path.GetTempPath(), "ModdingGUI.Family-" + Guid.NewGuid().ToString("N"));
        _bec = Path.Combine(_project, "proj_BEC");

        foreach (string relative in Files.Select(f => "data/units/" + f)
                     .Concat(new[] { "data/config/classdefs.tok", "data/config/skills.tok", "data/config/items.tok" }))
        {
            string target = Path.Combine(_bec, relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(Path.Combine(CleanBecCorpus.Root!, relative.Replace('/', Path.DirectorySeparatorChar)), target);
        }
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_project)) Directory.Delete(_project, recursive: true); }
        catch (IOException) { /* best effort */ }
    }

    private bool Ready => CleanBecCorpus.IsAvailable;

    private string UnitsPath(string name) => Path.Combine(_bec, "data", "units", name);

    [Fact]
    public void EveryUnitsFile_RoundTripsByteForByte_WhenNothingChanges()
    {
        if (!Ready) return;

        foreach (string name in Files)
        {
            byte[] before = File.ReadAllBytes(UnitsPath(name));
            var parsed = UnitTextFile.Load(UnitsPath(name));
            Assert.True(before.AsSpan().SequenceEqual(parsed.ToBytes()), $"{name} changed on a no-op");
        }
    }

    [Fact]
    public void EveryUnitsFile_IsAsciiOnly()
    {
        if (!Ready) return;

        foreach (string name in Files)
        {
            byte[] bytes = File.ReadAllBytes(UnitsPath(name));
            Assert.DoesNotContain(bytes, b => b > 0x7F);
        }
    }

    [Fact]
    public void VanillaCorpus_HasNoUnitWarnings()
    {
        if (!Ready) return;

        // Gladiators: the only finding is the malformed Muduk record, reported as Info.
        var gladiators = new GladiatorsApi().Validate(_bec);
        Assert.DoesNotContain(gladiators, i => i.Severity == GladiatorValidationSeverity.Warning);

        // Stat sets.
        var statSets = new StatSetsApi().Validate(_bec);
        Assert.DoesNotContain(statSets, i => i.Severity == StatSetValidationSeverity.Warning);

        // Skill and item sets.
        Assert.DoesNotContain(new SkillSetsApi().Validate(_bec), i => i.Severity == SetValidationSeverity.Warning);
        Assert.DoesNotContain(new ItemSetsApi().Validate(_bec), i => i.Severity == SetValidationSeverity.Warning);

        // Tints and the school name table.
        Assert.DoesNotContain(new TintSetsApi().Validate(_bec), i => i.Severity == SetValidationSeverity.Warning);
        Assert.DoesNotContain(new SchoolNamesApi().Validate(_bec),
            i => i.Severity == SchoolNameValidationSeverity.Warning);
    }

    [Fact]
    public void EveryGladiatorSetIndex_ResolvesToARealSet()
    {
        if (!Ready) return;

        var catalog = UnitCatalogLoader.Load(_bec);

        // The catalogue must describe the corpus: 169 statsets / 230 skillsets / 404 itemsets / 144
        // schools / 196 tints.
        Assert.Equal(new UnitCatalog(169, 230, 404, 144, 196), catalog);

        var records = GladiatorsFile.LoadProject(_bec).Records;
        Assert.Equal(2170, records.Count);

        Assert.All(records, r =>
        {
            Assert.InRange(r.StatSet, 0, catalog.StatSets - 1);
            Assert.InRange(r.SkillSet, 0, catalog.SkillSets - 1);
            Assert.InRange(r.ItemSet, 0, catalog.ItemSets - 1);
            Assert.InRange(r.TintSet, 0, catalog.Tints - 1);
            Assert.InRange(r.School, 0, catalog.Schools - 1);
        });
    }

    [Fact]
    public void EverySetFile_ReportsItsDeclaredCountConsistently()
    {
        if (!Ready) return;

        foreach (var (file, word) in new[]
                 {
                     ("statsets.txt", "Statset"), ("skillsets.txt", "Skillset"),
                     ("itemsets.txt", "Itemset"), ("tints.txt", "Tint set"),
                 })
        {
            var parsed = SectionedFile.Load(UnitsPath(file), word);
            Assert.NotNull(parsed.DeclaredCount);
            Assert.Equal(parsed.DeclaredCount, parsed.Count);
        }

        var schools = new SchoolNamesApi().Validate(_bec);
        Assert.DoesNotContain(schools, i => i.Code == SchoolNameValidationCodes.CountMismatch);
    }

    [Fact]
    public void EveryFamily_ReadsWithoutTouchingTheFiles()
    {
        if (!Ready) return;

        var before = Files.ToDictionary(f => f, f => File.ReadAllBytes(UnitsPath(f)));

        _ = new GladiatorsApi().List(_bec);
        _ = new StatSetsApi().List(_bec);
        _ = new SkillSetsApi().List(_bec);
        _ = new ItemSetsApi().List(_bec);
        _ = new TintSetsApi().List(_bec);
        _ = new SchoolNamesApi().List(_bec);

        foreach (string name in Files)
        {
            Assert.Equal(before[name], File.ReadAllBytes(UnitsPath(name)));
        }
    }
}
