using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ModdingGUI.Core.Api;
using Xunit;

namespace ModdingGUI.Tests.Core.Api;

/// <summary>
/// The shape the Team Builder writes a designed unit in. It used to be assembled as text and written straight to the
/// file; now it goes through <see cref="ISchoolsApi.AddUnits"/>, so this pins what that block contains - including the
/// six-value <c>CORESTATSCOMP2</c> that the engine needs to read the rest of the block at all.
/// </summary>
public sealed class TeamExportShapeTests : IDisposable
{
    private readonly string _project = Path.Combine(Path.GetTempPath(), "ModdingGUI.TeamShape-" + Guid.NewGuid().ToString("N"));
    private readonly string _bec;

    public TeamExportShapeTests()
    {
        _bec = Path.Combine(_project, "proj_BEC");
        Directory.CreateDirectory(Path.Combine(_bec, "data", "school"));
        File.WriteAllText(Path.Combine(_bec, "data", "school", "valensimperia.tok"),
            "NAME: \"Test School\"\nHERO: \"Valens\"\nGOLD: 25000\n");
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_project)) Directory.Delete(_project, recursive: true); }
        catch (IOException) { /* best effort */ }
    }

    private static SchoolUnitDto Designed(string name, string className) => new()
    {
        Name = name,
        ClassName = className,
        Fields = new List<SchoolFieldDto>
        {
            new("LEVEL", "3"),
            new("EXPERIENCE", "0"),
            new("JOBPOINTS", "25"),
            new("CUSTOMIZE", "0, \"\""),
            new("CORESTATSCOMP2", "10, 12, 14, 16, 18, 1"),
            new("ITEMSCOMP", "\"Bow\", \"Wrist Brace\", \"\", \"Circlet\", \"\""),
            new("SKILL", "\"Combo I\""),
        },
    };

    [Fact]
    public void ADesignedUnitRoundTripsThroughTheApi()
    {
        var api = new SchoolsApi();
        int added = api.AddUnits(_project, "data/school/valensimperia.tok", new[] { Designed("Uter", "Barbarian") });

        Assert.Equal(1, added);

        var school = api.Get(_project, "data/school/valensimperia.tok");
        var unit = school!.Unit("Uter");

        Assert.NotNull(unit);
        Assert.Equal("Barbarian", unit!.ClassName);
        Assert.Equal("3", unit.Value("LEVEL"));
        Assert.Equal("25", unit.Value("JOBPOINTS"));
        Assert.Equal("0, \"\"", unit.Value("CUSTOMIZE"));

        // Six values, or the loader abandons the block (CORESTATSCOMP2 case 0x16/0x19 reads six ints).
        Assert.Equal(6, unit.CoreStats.Count);
        Assert.Equal(new[] { 10, 12, 14, 16, 18, 1 }, unit.CoreStats);

        Assert.Equal("\"Bow\", \"Wrist Brace\", \"\", \"Circlet\", \"\"", unit.Value("ITEMSCOMP"));
        Assert.Equal(new[] { "Combo I" }, unit.Skills);
    }

    [Fact]
    public void ASecondExportDoesNotDuplicateTheUnit()
    {
        var api = new SchoolsApi();
        api.AddUnits(_project, "data/school/valensimperia.tok", new[] { Designed("Uter", "Barbarian") });

        int again = api.AddUnits(_project, "data/school/valensimperia.tok", new[] { Designed("Uter", "Barbarian") });

        Assert.Equal(0, again);
        Assert.Single(api.Get(_project, "data/school/valensimperia.tok")!.Units);
    }

    [Fact]
    public void TheExportIsBackedUpAndLeavesTheHeaderAlone()
    {
        var api = new SchoolsApi();
        api.AddUnits(_project, "data/school/valensimperia.tok", new[] { Designed("Uter", "Barbarian") });

        string history = Path.Combine(_bec, ".mod-history", "data", "school", "valensimperia.tok");
        Assert.True(Directory.Exists(history));
        Assert.NotEmpty(Directory.GetFiles(history, "*.bak"));

        // Header fields survive the append: name, hero and gold come back typed.
        var school = api.Get(_project, "data/school/valensimperia.tok")!;
        Assert.Equal("Test School", school.Name);
        Assert.Equal("Valens", school.Hero);
        Assert.Equal(25000, school.Gold);
    }
}
