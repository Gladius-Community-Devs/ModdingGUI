using System;
using System.IO;
using System.Linq;
using ModdingGUI.Core.Api;
using ModdingGUI.Core.Units;
using Xunit;

namespace ModdingGUI.Tests.Core.Units;

/// <summary>
/// The <c>Header (…): n</c> line of <c>schools.txt</c> and <c>tints.txt</c> is data: the unpack tool writes it
/// from byte 0 of <c>unitschools.idx</c>/<c>unittints.idx</c> and the repack writes it back into that byte.
/// The repack also parses it with a regex on the first line, so a file without it cannot be packed at all.
/// </summary>
public sealed class UnitsHeaderTests : IDisposable
{
    private readonly string _project;
    private readonly string _bec;

    public UnitsHeaderTests()
    {
        _project = Path.Combine(Path.GetTempPath(), "ModdingGUI.Header-" + Guid.NewGuid().ToString("N"));
        _bec = Path.Combine(_project, "proj_BEC");
        Directory.CreateDirectory(Path.Combine(_bec, "data", "units"));

        foreach (string name in new[] { "tints.txt", "schools.txt" })
        {
            File.Copy(Path.Combine(CleanBecCorpus.Root!, "data", "units", name),
                Path.Combine(_bec, "data", "units", name));
        }
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_project)) Directory.Delete(_project, recursive: true); }
        catch (IOException) { /* best effort */ }
    }

    private string PathOf(string name) => Path.Combine(_bec, "data", "units", name);

    private string[] TintHeaderCodes() =>
        new TintSetsApi().Validate(_bec)
            .Where(i => i.Code == SetValidationCodes.Header).Select(i => i.Code).ToArray();

    private string[] SchoolHeaderCodes() =>
        new SchoolNamesApi().Validate(_bec)
            .Where(i => i.Code == SchoolNameValidationCodes.Header).Select(i => i.Code).ToArray();

    [Fact]
    public void VanillaHeaders_AreAccepted()
    {
        Assert.Empty(TintHeaderCodes());
        Assert.Empty(SchoolHeaderCodes());
    }

    [Theory]
    [InlineData("tints.txt")]
    [InlineData("schools.txt")]
    public void MissingHeader_IsReported(string name)
    {
        string path = PathOf(name);
        string[] lines = File.ReadAllLines(path);
        File.WriteAllLines(path, lines.Where(l => !l.TrimStart().StartsWith("Header", StringComparison.OrdinalIgnoreCase)));

        if (name == "tints.txt") Assert.Single(TintHeaderCodes());
        else Assert.Single(SchoolHeaderCodes());
    }

    [Theory]
    [InlineData("tints.txt")]
    [InlineData("schools.txt")]
    public void HeaderOutOfByteRange_IsReported(string name)
    {
        string path = PathOf(name);
        string[] lines = File.ReadAllLines(path);
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].TrimStart().StartsWith("Header", StringComparison.OrdinalIgnoreCase))
            {
                lines[i] = "Header (test): 300";
                break;
            }
        }
        File.WriteAllLines(path, lines);

        if (name == "tints.txt") Assert.Single(TintHeaderCodes());
        else Assert.Single(SchoolHeaderCodes());
    }
}
