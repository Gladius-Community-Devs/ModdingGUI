using System;
using System.IO;
using System.Linq;
using System.Text;
using ModdingGUI.Core.Units;
using Xunit;

namespace ModdingGUI.Tests.Core.Units;

/// <summary>
/// The units family must survive a no-op write byte for byte, including the fact that its six files
/// disagree about line endings (gladiators.txt is LF, the rest CRLF).
/// </summary>
public sealed class UnitTextFileTests
{
    private static bool Ready => CleanBecCorpus.IsAvailable;

    private static string UnitsDir => Path.Combine(CleanBecCorpus.Root!, "data", "units");

    private static readonly string[] Files =
    {
        "gladiators.txt", "statsets.txt", "skillsets.txt", "itemsets.txt", "schools.txt", "tints.txt",
    };

    [Fact]
    public void EveryUnitsFile_RoundTripsByteForByte()
    {
        if (!Ready) return;

        foreach (string name in Files)
        {
            string path = Path.Combine(UnitsDir, name);
            byte[] before = File.ReadAllBytes(path);
            byte[] after = UnitTextFile.Load(path).ToBytes();

            Assert.True(before.AsSpan().SequenceEqual(after),
                $"{name} did not round-trip ({before.Length} -> {after.Length} bytes)");
        }
    }

    [Fact]
    public void GladiatorsFile_KeepsBareLineFeeds()
    {
        if (!Ready) return;

        byte[] bytes = File.ReadAllBytes(Path.Combine(UnitsDir, "gladiators.txt"));
        var file = UnitTextFile.Parse(bytes);

        Assert.Equal("\n", file.NewLine);
        Assert.Equal(bytes.Count(b => b == (byte)'\n'), file.Count);

        // The file carries one stray CRLF; it must survive, and no new CR may appear.
        static int CrLfCount(byte[] data) =>
            Enumerable.Range(0, data.Length - 1).Count(i => data[i] == (byte)'\r' && data[i + 1] == (byte)'\n');

        byte[] after = file.ToBytes();
        Assert.Equal(CrLfCount(bytes), CrLfCount(after));
        Assert.Equal(bytes.Count(b => b == (byte)'\r'), after.Count(b => b == (byte)'\r'));
        Assert.True(bytes.AsSpan().SequenceEqual(after));
    }

    [Fact]
    public void OtherUnitsFiles_KeepCrLf()
    {
        if (!Ready) return;

        foreach (string name in Files.Where(n => n != "gladiators.txt"))
        {
            byte[] bytes = File.ReadAllBytes(Path.Combine(UnitsDir, name));
            var file = UnitTextFile.Parse(bytes);

            Assert.Equal("\r\n", file.NewLine);
            Assert.True(file.ToBytes().AsSpan().SequenceEqual(bytes), $"{name} changed");
        }
    }

    [Fact]
    public void Parse_NoTrailingNewline_KeepsTheLastLine()
    {
        var file = UnitTextFile.Parse(Encoding.ASCII.GetBytes("one\r\ntwo"));

        Assert.Equal(2, file.Count);
        Assert.False(file.EndsWithNewLine);
        Assert.Equal("two", file.GetLine(1));
        Assert.Equal("one\r\ntwo", Encoding.ASCII.GetString(file.ToBytes()));
    }

    [Fact]
    public void Parse_EmptyFile_IsEmpty()
    {
        var file = UnitTextFile.Parse(Array.Empty<byte>());

        Assert.Equal(0, file.Count);
        Assert.Empty(file.ToBytes());
    }

    [Fact]
    public void MixedTerminators_ArePreservedUntilALineChanges()
    {
        var file = UnitTextFile.Parse(Encoding.ASCII.GetBytes("a\nb\r\nc\r\nd\r\n"));

        Assert.Equal("a", file.GetLine(0));
        Assert.Equal("d", file.GetLine(3));
        Assert.Equal("a\nb\r\nc\r\nd\r\n", Encoding.ASCII.GetString(file.ToBytes()));
        Assert.False(file.IsModified);
    }

    [Fact]
    public void ReplaceLine_KeepsThatLinesTerminator()
    {
        var file = UnitTextFile.Parse(Encoding.ASCII.GetBytes("a\nb\n"));
        file.ReplaceLine(0, "A");

        Assert.True(file.IsModified);
        Assert.Equal("A\nb\n", Encoding.ASCII.GetString(file.ToBytes()));
    }

    [Fact]
    public void ReplaceLine_WithTheSameText_IsNotAModification()
    {
        var file = UnitTextFile.Parse(Encoding.ASCII.GetBytes("a\r\n"));
        file.ReplaceLine(0, "a");

        Assert.False(file.IsModified);
    }

    [Fact]
    public void InsertLine_InTheMiddle_UsesTheFileTerminator()
    {
        var file = UnitTextFile.Parse(Encoding.ASCII.GetBytes("a\r\nb\r\n"));
        file.InsertLine(1, "x");

        Assert.Equal("a\r\nx\r\nb\r\n", Encoding.ASCII.GetString(file.ToBytes()));
    }

    [Fact]
    public void InsertLine_AfterAFileWithoutTrailingNewline_SeparatesTheLines()
    {
        var file = UnitTextFile.Parse(Encoding.ASCII.GetBytes("a\r\nb"));
        file.InsertLine(2, "c");

        Assert.Equal("a\r\nb\r\nc\r\n", Encoding.ASCII.GetString(file.ToBytes()));
    }

    [Fact]
    public void RemoveLine_DropsItAndKeepsTheRest()
    {
        var file = UnitTextFile.Parse(Encoding.ASCII.GetBytes("a\r\nb\r\nc\r\n"));
        file.RemoveLine(1);

        Assert.Equal("a\r\nc\r\n", Encoding.ASCII.GetString(file.ToBytes()));
    }

    [Fact]
    public void LinesWithEmbeddedBreaks_AreRejected()
    {
        var file = UnitTextFile.Parse(Encoding.ASCII.GetBytes("a\r\n"));

        Assert.Throws<ArgumentException>(() => file.ReplaceLine(0, "a\nb"));
        Assert.Throws<ArgumentException>(() => file.InsertLine(0, "a\r\nb"));
    }

    [Fact]
    public void NonAsciiBytes_SurviveTheRoundTrip()
    {
        // ISO-8859-1 is bijective, so every byte value must come back unchanged.
        byte[] bytes = Enumerable.Range(0, 256).Select(i => (byte)i).ToArray();
        var file = UnitTextFile.Parse(bytes);

        Assert.True(bytes.AsSpan().SequenceEqual(file.ToBytes()));
    }

    [Fact]
    public void Save_WritesABackupThenTheEditedFile()
    {
        if (!Ready) return;

        string project = Path.Combine(Path.GetTempPath(), "ModdingGUI.Units-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(project, "data", "units"));
        string relative = "data/units/gladiators.txt";
        string target = Path.Combine(project, relative.Replace('/', Path.DirectorySeparatorChar));
        File.Copy(Path.Combine(UnitsDir, "gladiators.txt"), target);

        byte[] before = File.ReadAllBytes(target);
        var file = UnitTextFile.Load(target);
        file.ReplaceLine(0, "Name: Renamed");
        file.Save(project, relative);

        Assert.NotEqual(before, File.ReadAllBytes(target));
        Assert.Contains("Name: Renamed", File.ReadAllText(target).Split('\n').Take(2));

        string history = Path.Combine(project, ".mod-history", relative.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(Directory.Exists(history), "a .mod-history entry must exist for the write");
        Assert.Equal(before, File.ReadAllBytes(Directory.GetFiles(history).Single()));

        try { Directory.Delete(project, recursive: true); } catch (IOException) { }
    }
}
