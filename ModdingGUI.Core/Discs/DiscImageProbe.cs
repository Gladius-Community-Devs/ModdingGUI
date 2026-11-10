namespace ModdingGUI.Core.Discs;

/// <summary>The disc image containers a user might hand the app.</summary>
public enum DiscImageFormat
{
    /// <summary>Nothing recognisable, or a compressed archive rather than a disc.</summary>
    Unknown = 0,

    /// <summary>A plain GameCube image: an <c>.iso</c> or the older <c>.gcm</c> name for the same bytes.</summary>
    PlainIso,

    /// <summary>Dolphin's compressed container, zstd by default.</summary>
    Rvz,

    /// <summary>Dolphin's other compressed container, older than RVZ.</summary>
    Wia,

    /// <summary>Dolphin's legacy zlib container.</summary>
    Gcz,

    /// <summary>Dolphin's compressed ISO, read-only for Dolphin itself.</summary>
    Ciso,

    /// <summary>A "scrubbed" NKit image: a valid disc with padding removed and a rebuild table.</summary>
    Nkit,
}

/// <summary>
/// Identifies a disc image by its bytes, not its extension.
/// </summary>
/// <remarks>
/// <para>
/// Extensions lie: NKit images are usually named <c>.nkit.iso</c> but are not ISOs, and a plain image is often renamed
/// <c>.gcm</c>. The offsets below were each verified against a real file produced by Dolphin 2606a on this machine, not
/// taken from a comment - the Dolphin source's own comment for the GCZ magic is stale, which is exactly the sort of thing
/// that produces a detector that never matches.
/// </para>
/// <para>
/// Order matters: containers first, then the NKit marker, then the plain-image magic word at 0x1C, because
/// <c>"NKIT v01"</c> sits at 0x200 in a region that is zero on a plain image.
/// </para>
/// </remarks>
public static class DiscImageProbe
{
    /// <summary>How many bytes <see cref="Detect(string)"/> needs to read.</summary>
    public const int HeaderBytes = 0x208;   // through the end of the 8-byte NKit marker at 0x200

    private const int NkitMarkerOffset = 0x200;
    private const int DiscMagicOffset = 0x1C;

    /// <summary>The GameCube "DVD magic word" at offset 0x1C of a plain image.</summary>
    private static readonly byte[] DiscMagic = { 0xC2, 0x33, 0x9F, 0x3D };

    private static readonly byte[] RvzMagic = { 0x52, 0x56, 0x5A, 0x01 };
    private static readonly byte[] WiaMagic = { 0x57, 0x49, 0x41, 0x01 };
    private static readonly byte[] GczMagic = { 0x01, 0xC0, 0x0B, 0xB1 };
    private static readonly byte[] CisoMagic = { 0x43, 0x49, 0x53, 0x4F };
    private static readonly byte[] NkitMagic = "NKIT v01"u8.ToArray();

    /// <summary>Read the start of a file and say what it is.</summary>
    /// <exception cref="IOException">The file cannot be opened or is shorter than the headers it needs.</exception>
    public static DiscImageFormat Detect(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        using FileStream stream = File.OpenRead(path);
        return Detect(stream);
    }

    /// <summary>Detect from an open stream; the stream is read from position 0 and left open.</summary>
    public static DiscImageFormat Detect(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        byte[] buffer = new byte[HeaderBytes];
        stream.Position = 0;

        int read = 0;
        while (read < buffer.Length)
        {
            int chunk = stream.Read(buffer, read, buffer.Length - read);
            if (chunk == 0) break;
            read += chunk;
        }

        return Detect(buffer.AsSpan(0, read));
    }

    /// <summary>Detect from a header buffer of at least <see cref="HeaderBytes"/> bytes (shorter buffers are fine).</summary>
    public static DiscImageFormat Detect(ReadOnlySpan<byte> header)
    {
        if (StartsWith(header, 0, RvzMagic)) return DiscImageFormat.Rvz;
        if (StartsWith(header, 0, WiaMagic)) return DiscImageFormat.Wia;
        if (StartsWith(header, 0, GczMagic)) return DiscImageFormat.Gcz;
        if (StartsWith(header, 0, CisoMagic)) return DiscImageFormat.Ciso;
        if (StartsWith(header, NkitMarkerOffset, NkitMagic)) return DiscImageFormat.Nkit;
        if (StartsWith(header, DiscMagicOffset, DiscMagic)) return DiscImageFormat.PlainIso;

        return DiscImageFormat.Unknown;
    }

    /// <summary>True for the formats the app's own Python tools can read directly.</summary>
    public static bool IsReadableByTools(DiscImageFormat format) => format == DiscImageFormat.PlainIso;

    /// <summary>A short description for logs and error messages.</summary>
    public static string Describe(DiscImageFormat format) => format switch
    {
        DiscImageFormat.PlainIso => "plain ISO/GCM",
        DiscImageFormat.Rvz => "RVZ (Dolphin compressed)",
        DiscImageFormat.Wia => "WIA (Dolphin compressed)",
        DiscImageFormat.Gcz => "GCZ (Dolphin compressed)",
        DiscImageFormat.Ciso => "CISO (Dolphin compressed)",
        DiscImageFormat.Nkit => "NKit (scrubbed image)",
        _ => "unrecognised file",
    };

    private static bool StartsWith(ReadOnlySpan<byte> buffer, int offset, ReadOnlySpan<byte> magic)
    {
        if (offset < 0 || offset + magic.Length > buffer.Length) return false;
        return buffer.Slice(offset, magic.Length).SequenceEqual(magic);
    }
}
