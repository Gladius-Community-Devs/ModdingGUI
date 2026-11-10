namespace ModdingGUI.Tests.Core.Discs;

using ModdingGUI.Core.Discs;
using Xunit;

/// <summary>
/// Tests for <see cref="DiscImageConverter"/>. The external tools are not run here; what is covered is the argument
/// mapping (the part Dolphin rejects outright when it is wrong) and the refusals that stop a bad conversion happening.
/// </summary>
public class DiscImageConverterTests
{
    [Fact]
    public void BuildDolphinArguments_Iso_NeedsNoBlockOrCompressionFlags()
    {
        var arguments = DiscImageConverter.BuildDolphinArguments(@"C:\in.rvz", @"C:\out.iso", DiscImageTarget.Iso);

        Assert.Equal(new[] { "convert", "-i", @"C:\in.rvz", "-o", @"C:\out.iso", "-f", "iso" }, arguments);
    }

    [Fact]
    public void BuildDolphinArguments_Rvz_CarriesCompressionAndBlockSize()
    {
        // Dolphin errors with "Compression method must be set for WIA or RVZ" and "Block size must be set" without these.
        var arguments = DiscImageConverter.BuildDolphinArguments("in.iso", "out.rvz", DiscImageTarget.Rvz);

        Assert.Contains("-c", arguments);
        Assert.Contains("zstd", arguments);
        Assert.Equal("131072", arguments[arguments.Count - 1]);   // RVZ rejects the WIA block size
    }

    [Fact]
    public void BuildDolphinArguments_Wia_UsesItsOwnBlockSize()
    {
        // WIA rejects 131072 with "Block size is not valid for this format"; 2097152 is what it accepts.
        var arguments = DiscImageConverter.BuildDolphinArguments("in.iso", "out.wia", DiscImageTarget.Wia);

        Assert.Equal("2097152", arguments[arguments.Count - 1]);
        Assert.Contains("none", arguments);
    }

    [Fact]
    public void BuildDolphinArguments_Gcz_OnlyNeedsABlockSize()
    {
        var arguments = DiscImageConverter.BuildDolphinArguments("in.iso", "out.gcz", DiscImageTarget.Gcz);

        Assert.Equal(new[] { "convert", "-i", "in.iso", "-o", "out.gcz", "-f", "gcz", "-b", "131072" }, arguments);
    }

    [Theory]
    [InlineData(DiscImageTarget.Iso, ".iso")]
    [InlineData(DiscImageTarget.Rvz, ".rvz")]
    [InlineData(DiscImageTarget.Wia, ".wia")]
    [InlineData(DiscImageTarget.Gcz, ".gcz")]
    public void ExtensionFor_MatchesTheFormat(DiscImageTarget target, string expected)
    {
        Assert.Equal(expected, DiscImageConverter.ExtensionFor(target));
    }

    [Fact]
    public void NkitFolderIn_IsInsideTheToolbelt()
    {
        string folder = DiscImageConverter.NkitFolderIn(@"C:\app");

        Assert.Equal(Path.Combine(@"C:\app", "tools", "nkit"), folder);
    }

    [Fact]
    public void AvailableTargets_IsEmptyWithoutDolphinTool()
    {
        // NKit can only produce an ISO, so it cannot stand in for dolphin-tool on its own.
        var converter = new DiscImageConverter(null, @"C:\app\tools\nkit");

        Assert.Empty(converter.AvailableTargets());
        Assert.False(converter.HasDolphinTool);
    }

    [Fact]
    public void AvailableTargets_ListsAllFourWithDolphinTool()
    {
        string tool = Path.Combine(Path.GetTempPath(), $"DolphinTool-{Guid.NewGuid():N}.exe");
        File.WriteAllText(tool, "stub");

        try
        {
            var converter = new DiscImageConverter(tool);

            Assert.Equal(
                new[] { DiscImageTarget.Iso, DiscImageTarget.Rvz, DiscImageTarget.Wia, DiscImageTarget.Gcz },
                converter.AvailableTargets());
        }
        finally
        {
            File.Delete(tool);
        }
    }

    [Fact]
    public void Convert_MissingSource_IsRefused()
    {
        var converter = new DiscImageConverter(null);

        DiscImageConversion result = converter.Convert(
            Path.Combine(Path.GetTempPath(), $"nope-{Guid.NewGuid():N}.iso"),
            Path.Combine(Path.GetTempPath(), "out.iso"),
            DiscImageTarget.Iso);

        Assert.False(result.Ok);
        Assert.Contains("No file at", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Convert_SameSourceAndDestination_IsRefused()
    {
        string path = Path.Combine(Path.GetTempPath(), $"same-{Guid.NewGuid():N}.iso");
        byte[] header = new byte[0x208];
        header[0x1C] = 0xC2; header[0x1D] = 0x33; header[0x1E] = 0x9F; header[0x1F] = 0x3D;
        File.WriteAllBytes(path, header);

        try
        {
            DiscImageConversion result = new DiscImageConverter(null).Convert(path, path, DiscImageTarget.Iso);

            Assert.False(result.Ok);
            Assert.Contains("same file", result.Message, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Convert_ExistingDestination_IsRefusedRatherThanOverwritten()
    {
        // dolphin-tool overwrites silently, so refusing here is the only warning the user gets.
        string folder = Path.Combine(Path.GetTempPath(), $"conv-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);

        try
        {
            string source = Path.Combine(folder, "game.iso");
            byte[] header = new byte[0x208];
            header[0x1C] = 0xC2; header[0x1D] = 0x33; header[0x1E] = 0x9F; header[0x1F] = 0x3D;
            File.WriteAllBytes(source, header);

            string destination = Path.Combine(folder, "out.iso");
            File.WriteAllText(destination, "already here");

            DiscImageConversion result = new DiscImageConverter(null).Convert(source, destination, DiscImageTarget.Iso);

            Assert.False(result.Ok);
            Assert.Contains("already exists", result.Message, StringComparison.Ordinal);
            Assert.Equal("already here", File.ReadAllText(destination));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void Convert_NkitToContainer_IsRefusedWithAnExplanation()
    {
        string folder = Path.Combine(Path.GetTempPath(), $"nkit-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);

        try
        {
            string source = Path.Combine(folder, "game.nkit.iso");
            byte[] header = new byte[0x208];
            header[0x1C] = 0xC2; header[0x1D] = 0x33; header[0x1E] = 0x9F; header[0x1F] = 0x3D;
            System.Text.Encoding.ASCII.GetBytes("NKIT v01").CopyTo(header, 0x200);
            File.WriteAllBytes(source, header);

            // No NKit tool configured, so it stops at "the bundled converter is missing" - the point is that an NKit
            // source never silently produces a container.
            DiscImageConversion result = new DiscImageConverter(null, null)
                .Convert(source, Path.Combine(folder, "out.rvz"), DiscImageTarget.Rvz);

            Assert.False(result.Ok);
            Assert.Contains("NKit", result.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
