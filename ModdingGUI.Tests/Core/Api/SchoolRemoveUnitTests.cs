using System;
using System.IO;
using ModdingGUI.Core.Api;
using Xunit;

namespace ModdingGUI.Tests.Core.Api;

/// <summary>
/// Removing a unit from a roster: the block and its separator go, everything else stays.
/// </summary>
public sealed class SchoolRemoveUnitTests : IDisposable
{
    private readonly string _project = Path.Combine(Path.GetTempPath(), "ModdingGUI.RemoveUnit-" + Guid.NewGuid().ToString("N"));
    private readonly string _bec;
    private const string Roster = "data/school/valensimperia.tok";

    public SchoolRemoveUnitTests()
    {
        _bec = Path.Combine(_project, "proj_BEC");
        Directory.CreateDirectory(Path.Combine(_bec, "data", "school"));

        File.WriteAllText(Path.Combine(_bec, "data", "school", "valensimperia.tok"),
            "NAME: \"Test\"\nHERO: \"Valens\"\nGOLD: 100\n\n" +
            "CREATEUNIT: \"A\", \"Legionnaire\"    // Name, class\n\tLEVEL: 1\n\tJOBPOINTS: 0\n\n" +
            "CREATEUNIT: \"B\", \"Archer\"    // Name, class\n\tLEVEL: 1\n\tJOBPOINTS: 0\n\n" +
            "CREATEUNIT: \"C\", \"Peltast\"    // Name, class\n\tLEVEL: 1\n\tJOBPOINTS: 0\n\n");
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_project)) Directory.Delete(_project, recursive: true); }
        catch (IOException) { /* best effort */ }
    }

    [Fact]
    public void TheMiddleBlockGoesAndTheNeighboursStay()
    {
        var api = new SchoolsApi();

        Assert.True(api.RemoveUnit(_project, Roster, "B"));

        var school = api.Get(_project, Roster)!;
        Assert.Equal(new[] { "A", "C" }, System.Linq.Enumerable.Select(school.Units, unit => unit.Name));
        Assert.Equal("Test", school.Name);
        Assert.Equal(100, school.Gold);
    }

    [Fact]
    public void TheLastBlockGoesToo()
    {
        var api = new SchoolsApi();

        Assert.True(api.RemoveUnit(_project, Roster, "C"));

        var school = api.Get(_project, Roster)!;
        Assert.Equal(2, school.Units.Count);
        Assert.Equal("B", school.Units[1].Name);
    }

    [Fact]
    public void AMissingNameIsNotAnError()
    {
        Assert.False(new SchoolsApi().RemoveUnit(_project, Roster, "Nobody"));
    }

    [Fact]
    public void RemovalIsBackedUp()
    {
        new SchoolsApi().RemoveUnit(_project, Roster, "A");

        string history = Path.Combine(_bec, ".mod-history", "data", "school", "valensimperia.tok");
        Assert.True(Directory.Exists(history));
        Assert.NotEmpty(Directory.GetFiles(history, "*.bak"));
    }
}
