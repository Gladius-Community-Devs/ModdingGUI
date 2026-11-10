using System.Text;

namespace ModdingGUI.Tests.Core.Discs;

using ModdingGUI.Core.Discs;
using Xunit;

/// <summary>
/// Tests for <see cref="DiscImageProbe"/>. The byte patterns here are the ones observed on this machine: RVZ/WIA/GCZ/CISO
/// headers, the NKit marker at 0x200, and the GameCube magic word at 0x1C.
/// </summary>
public class DiscImageProbeTests
{
    private static byte[] Header(Action<byte[]> fill)
    {
        byte[] buffer = new byte[DiscImageProbe.HeaderBytes];
        fill(buffer);
        return buffer;
    }

    [Fact]
    public void Detect_PlainIso_ByMagicWordAt0x1C()
    {
        byte[] header = Header(b => { b[0x1C] = 0xC2; b[0x1D] = 0x33; b[0x1E] = 0x9F; b[0x1F] = 0x3D; });

        Assert.Equal(DiscImageFormat.PlainIso, DiscImageProbe.Detect(header));
    }

    [Theory]
    [InlineData(new byte[] { 0x52, 0x56, 0x5A, 0x01 }, DiscImageFormat.Rvz)]
    [InlineData(new byte[] { 0x57, 0x49, 0x41, 0x01 }, DiscImageFormat.Wia)]
    [InlineData(new byte[] { 0x01, 0xC0, 0x0B, 0xB1 }, DiscImageFormat.Gcz)]
    [InlineData(new byte[] { 0x43, 0x49, 0x53, 0x4F }, DiscImageFormat.Ciso)]
    public void Detect_Containers_ByFirstFourBytes(byte[] magic, DiscImageFormat expected)
    {
        byte[] header = Header(b => magic.CopyTo(b, 0));

        Assert.Equal(expected, DiscImageProbe.Detect(header));
    }

    [Fact]
    public void Detect_Nkit_ByMarkerAt0x200()
    {
        byte[] header = Header(b => Encoding.ASCII.GetBytes("NKIT v01").CopyTo(b, 0x200));

        Assert.Equal(DiscImageFormat.Nkit, DiscImageProbe.Detect(header));
    }

    [Fact]
    public void Detect_NkitIsNotMistakenForPlainIso_WhenBothMarkersPresent()
    {
        // A real NKit image carries the disc magic too; the marker has to win or the app would hand a scrubbed image to
        // the unpacker and get a file table that does not match.
        byte[] header = Header(b =>
        {
            b[0x1C] = 0xC2; b[0x1D] = 0x33; b[0x1E] = 0x9F; b[0x1F] = 0x3D;
            Encoding.ASCII.GetBytes("NKIT v01").CopyTo(b, 0x200);
        });

        Assert.Equal(DiscImageFormat.Nkit, DiscImageProbe.Detect(header));
    }

    [Fact]
    public void Detect_UnknownForZerosAndArchives()
    {
        Assert.Equal(DiscImageFormat.Unknown, DiscImageProbe.Detect(new byte[DiscImageProbe.HeaderBytes]));
        Assert.Equal(DiscImageFormat.Unknown, DiscImageProbe.Detect(new byte[] { 0x1F, 0x8B }));           // gzip
        Assert.Equal(DiscImageFormat.Unknown, DiscImageProbe.Detect(new byte[] { 0x37, 0x7A, 0xBC, 0xAF })); // 7z
        Assert.Equal(DiscImageFormat.Unknown, DiscImageProbe.Detect(Array.Empty<byte>()));
    }

    [Fact]
    public void Detect_HandlesShortFilesWithoutThrowing()
    {
        // A truncated or tiny file must not throw: detection happens on whatever the user picked.
        byte[] tiny = { 0x52, 0x56, 0x5A, 0x01 };

        Assert.Equal(DiscImageFormat.Rvz, DiscImageProbe.Detect(tiny));
    }

    [Fact]
    public void IsReadableByTools_IsTrueOnlyForPlainIso()
    {
        Assert.True(DiscImageProbe.IsReadableByTools(DiscImageFormat.PlainIso));
        Assert.False(DiscImageProbe.IsReadableByTools(DiscImageFormat.Rvz));
        Assert.False(DiscImageProbe.IsReadableByTools(DiscImageFormat.Nkit));
    }

    [Fact]
    public void Detect_FromFile_ReadsTheRealHeader()
    {
        string path = Path.Combine(Path.GetTempPath(), $"probe-{Guid.NewGuid():N}.bin");
        try
        {
            byte[] bytes = Header(b => { b[0x1C] = 0xC2; b[0x1D] = 0x33; b[0x1E] = 0x9F; b[0x1F] = 0x3D; });
            File.WriteAllBytes(path, bytes);

            Assert.Equal(DiscImageFormat.PlainIso, DiscImageProbe.Detect(path));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
