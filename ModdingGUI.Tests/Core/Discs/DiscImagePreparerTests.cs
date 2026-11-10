using System.Text;

namespace ModdingGUI.Tests.Core.Discs;

using ModdingGUI.Core.Discs;
using Xunit;

/// <summary>
/// Tests for <see cref="DiscImagePreparer"/>'s decisions. The conversions themselves shell out to Dolphin/NKit tools, so
/// these cover what the class decides *without* an external process: pass-through, refusal messages and converter
/// discovery. A real conversion is verified by hand against a Dolphin-made RVZ.
/// </summary>
public class DiscImagePreparerTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"prep-{Guid.NewGuid():N}");

    public DiscImagePreparerTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        try { Directory.Delete(_folder, recursive: true); } catch (IOException) { }
    }

    private string WriteImage(string name, Action<byte[]> fill, int size = 0x208)
    {
        byte[] bytes = new byte[size];
        fill(bytes);

        string path = Path.Combine(_folder, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private static void PlainIso(byte[] bytes) { bytes[0x1C] = 0xC2; bytes[0x1D] = 0x33; bytes[0x1E] = 0x9F; bytes[0x1F] = 0x3D; }

    private static void Rvz(byte[] bytes) { new byte[] { 0x52, 0x56, 0x5A, 0x01 }.CopyTo(bytes, 0); }

    private static void Nkit(byte[] bytes)
    {
        PlainIso(bytes);
        Encoding.ASCII.GetBytes("NKIT v01").CopyTo(bytes, 0x200);
    }

    [Fact]
    public void Prepare_PlainIso_IsPassedThroughUntouched()
    {
        string iso = WriteImage("game.iso", PlainIso);
        var preparer = new DiscImagePreparer(null);

        DiscImagePreparation result = preparer.Prepare(iso);

        Assert.True(result.Ok);
        Assert.Equal(iso, result.IsoPath);
        Assert.False(result.IsTemporary);
        Assert.False(preparer.HasConverter);
    }

    [Fact]
    public void Prepare_UnknownFile_IsRefusedWithAnExplanation()
    {
        string junk = WriteImage("photo.png", b => b[0] = 0x89);
        var preparer = new DiscImagePreparer(null);

        DiscImagePreparation result = preparer.Prepare(junk);

        Assert.False(result.Ok);
        Assert.Null(result.IsoPath);
        Assert.Contains("not a GameCube disc image", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("extract", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Prepare_CompressedImageWithoutConverter_PointsAtTheSetting()
    {
        string rvz = WriteImage("game.rvz", Rvz);
        var preparer = new DiscImagePreparer(null);

        DiscImagePreparation result = preparer.Prepare(rvz);

        Assert.False(result.Ok);
        Assert.Equal(DiscImageFormat.Rvz, result.SourceFormat);
        Assert.Contains("RVZ", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Disc image converter", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Prepare_NkitWithoutNkitConverter_SaysDolphinCannotRebuildIt()
    {
        string nkit = WriteImage("game.nkit.iso", Nkit);
        var preparer = new DiscImagePreparer(null);

        DiscImagePreparation result = preparer.Prepare(nkit);

        Assert.False(result.Ok);
        Assert.Equal(DiscImageFormat.Nkit, result.SourceFormat);
        Assert.Contains("NKit", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Prepare_MissingFile_IsRefused()
    {
        var preparer = new DiscImagePreparer(null);

        DiscImagePreparation result = preparer.Prepare(Path.Combine(_folder, "nope.iso"));

        Assert.False(result.Ok);
        Assert.Contains("No file at", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GuessConverterFrom_FindsDolphinToolBesideDolphin()
    {
        string tool = Path.Combine(_folder, "DolphinTool.exe");
        File.WriteAllText(tool, "stub");
        string dolphin = Path.Combine(_folder, "Dolphin.exe");
        File.WriteAllText(dolphin, "stub");

        Assert.Equal(tool, DiscImagePreparer.GuessConverterFrom(dolphin));
    }

    [Fact]
    public void GuessConverterFrom_ReturnsNullWhenNothingThere()
    {
        Assert.Null(DiscImagePreparer.GuessConverterFrom(Path.Combine(_folder, "Dolphin.exe")));
        Assert.Null(DiscImagePreparer.GuessConverterFrom(null));
        Assert.Null(DiscImagePreparer.GuessConverterFrom(string.Empty));
    }

    [Fact]
    public void HasConverter_RequiresTheFileToExist()
    {
        var missing = new DiscImagePreparer(Path.Combine(_folder, "not-here.exe"));
        Assert.False(missing.HasConverter);

        string real = Path.Combine(_folder, "DolphinTool.exe");
        File.WriteAllText(real, "stub");
        Assert.True(new DiscImagePreparer(real).HasConverter);
    }
}
