namespace ModdingGUI.Core.Discs;

using System.Diagnostics;
using ModdingGUI.Core.Logging;

/// <summary>The outcome of asking for a plain ISO from whatever the user supplied.</summary>
/// <param name="Ok">True when <paramref name="IsoPath"/> is a readable plain image.</param>
/// <param name="IsoPath">The plain ISO to use: the input itself, or a converted temporary copy.</param>
/// <param name="SourceFormat">What the input actually was.</param>
/// <param name="Message">A line for the log: what happened, or why it could not.</param>
/// <param name="IsTemporary">True when the caller owns <paramref name="IsoPath"/> and should delete it afterwards.</param>
public sealed record DiscImagePreparation(
    bool Ok,
    string? IsoPath,
    DiscImageFormat SourceFormat,
    string Message,
    bool IsTemporary = false);

/// <summary>
/// Turns a disc image of any supported format into a plain ISO the tool chain can read.
/// </summary>
/// <remarks>
/// <para>
/// The app's Python tools seek raw offsets in the file (<c>ngciso-tool.py</c> even opens it <c>rb+</c>), so they can only
/// ever be handed a plain ISO. Dolphin's <c>dolphin-tool</c> converts iso/gcz/wia/rvz both ways and verifies the
/// decompressed disc, but it cannot write CISO or rebuild NKit; NKit needs the NKit converter. This class owns those
/// invocations so the shell does not have to.
/// </para>
/// <para>
/// Nothing here ships a converter: Dolphin is GPL-2.0-or-later and NKit is closed-source freeware, so the paths are
/// user-configured (with a guess derived from the configured Dolphin executable) and a missing converter produces a
/// precise message rather than a mystery failure.
/// </para>
/// </remarks>
public sealed class DiscImagePreparer
{
    private readonly string? _converterPath;
    private readonly string? _nkitConverterPath;
    private readonly IAppLog? _log;

    /// <param name="converterPath">Full path to <c>DolphinTool.exe</c> (or a compatible CLI), or null.</param>
    /// <param name="nkitConverterPath">Full path to NKit's <c>ConvertToISO.exe</c>, or null.</param>
    /// <param name="log">Optional log sink.</param>
    public DiscImagePreparer(string? converterPath, string? nkitConverterPath = null, IAppLog? log = null)
    {
        _converterPath = string.IsNullOrWhiteSpace(converterPath) ? null : converterPath;
        _nkitConverterPath = string.IsNullOrWhiteSpace(nkitConverterPath) ? null : nkitConverterPath;
        _log = log;
    }

    /// <summary>True when a converter is configured and exists, so compressed formats can be handled.</summary>
    public bool HasConverter => _converterPath is not null && File.Exists(_converterPath);

    /// <summary>True when NKit images can be rebuilt.</summary>
    public bool HasNkitConverter => _nkitConverterPath is not null && File.Exists(_nkitConverterPath);

    /// <summary>
    /// Work out where <c>dolphin-tool</c> probably is, given the Dolphin executable the user already configured.
    /// </summary>
    /// <remarks>A user who pointed at Dolphin.exe almost always has DolphinTool.exe beside it, or one folder up.</remarks>
    public static string? GuessConverterFrom(string? dolphinExecutablePath)
    {
        if (string.IsNullOrWhiteSpace(dolphinExecutablePath)) return null;

        string? folder = Path.GetDirectoryName(dolphinExecutablePath);
        if (string.IsNullOrWhiteSpace(folder)) return null;

        string? parentFolder = Path.GetDirectoryName(folder);
        foreach (string? candidateFolder in new[] { folder, parentFolder, Path.Combine(folder, "..") })
        {
            if (string.IsNullOrWhiteSpace(candidateFolder)) continue;

            string candidate = Path.GetFullPath(Path.Combine(candidateFolder, "DolphinTool.exe"));
            if (File.Exists(candidate)) return candidate;
        }

        return null;
    }

    /// <summary>
    /// Return a plain ISO for <paramref name="imagePath"/>, converting to a temporary file when needed.
    /// </summary>
    /// <remarks>
    /// The caller must delete <see cref="DiscImagePreparation.IsoPath"/> afterwards when
    /// <see cref="DiscImagePreparation.IsTemporary"/> is true. Conversions are GB-scale, so the check for free space
    /// happens first.
    /// </remarks>
    public DiscImagePreparation Prepare(string imagePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imagePath);

        if (!File.Exists(imagePath))
        {
            return new DiscImagePreparation(false, null, DiscImageFormat.Unknown, $"No file at '{imagePath}'.");
        }

        DiscImageFormat format;
        try
        {
            format = DiscImageProbe.Detect(imagePath);
        }
        catch (IOException ex)
        {
            return new DiscImagePreparation(false, null, DiscImageFormat.Unknown,
                $"Could not read '{Path.GetFileName(imagePath)}': {ex.Message}");
        }

        if (format == DiscImageFormat.PlainIso)
        {
            return new DiscImagePreparation(true, imagePath, format,
                $"'{Path.GetFileName(imagePath)}' is already a plain ISO; no conversion needed.");
        }

        if (format == DiscImageFormat.Unknown)
        {
            return new DiscImagePreparation(false, null, format,
                $"'{Path.GetFileName(imagePath)}' is not a GameCube disc image. If it is a .zip/.7z/.rar, extract it first.");
        }

        string? tempIso = TemporaryIsoPathFor(imagePath);
        if (tempIso is null)
        {
            return new DiscImagePreparation(false, null, format, "Could not work out a temporary path for the conversion.");
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(tempIso)!);

            // dolphin-tool overwrites silently and can exit 0 on garbage input, so the output file is the only proof.
            DiscImagePreparation attempt = format == DiscImageFormat.Nkit
                ? ConvertNkit(imagePath, tempIso, format)
                : ConvertWithDolphinTool(imagePath, tempIso, format);

            if (attempt.Ok) return attempt;

            Cleanup(tempIso);
            return attempt;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Cleanup(tempIso);
            return new DiscImagePreparation(false, null, format, $"Conversion failed: {ex.Message}");
        }
    }

    /// <summary>Delete a temporary ISO produced by <see cref="Prepare"/>, ignoring failures.</summary>
    public static void Cleanup(string? isoPath)
    {
        if (string.IsNullOrWhiteSpace(isoPath) || !File.Exists(isoPath)) return;

        try
        {
            File.Delete(isoPath);
        }
        catch (IOException)
        {
            // A locked temp file is not worth failing the caller over; the OS will get it eventually.
        }
    }

    private DiscImagePreparation ConvertWithDolphinTool(string imagePath, string tempIso, DiscImageFormat format)
    {
        if (!HasConverter)
        {
            return new DiscImagePreparation(false, null, format,
                $"A {DiscImageProbe.Describe(format)} image has to be converted to a plain ISO first. No converter is configured - " +
                "set File > Settings > General > 'Disc image converter' to DolphinTool.exe.");
        }

        if (format == DiscImageFormat.Ciso)
        {
            // dolphin-tool reads CISO but cannot write it; whether it can read one as convert *input* is not documented.
            // Try it, and let the output check decide rather than claiming support we have not verified.
            Log(LogLevel.Info, "CISO input: attempting conversion through the configured converter.");
        }

        var arguments = new List<string> { "convert", "-i", imagePath, "-o", tempIso, "-f", "iso" };
        (int exitCode, string output) = Run(_converterPath!, Path.GetDirectoryName(_converterPath!), arguments);

        if (!File.Exists(tempIso) || new FileInfo(tempIso).Length == 0)
        {
            return new DiscImagePreparation(false, null, format,
                $"The converter did not produce an ISO (exit {exitCode}). {Trim(output)}");
        }

        Log(LogLevel.Success, $"Converted {DiscImageProbe.Describe(format)} to a temporary ISO ({Bytes(new FileInfo(tempIso).Length)}).");
        return new DiscImagePreparation(true, tempIso, format,
            $"Converted {DiscImageProbe.Describe(format)} to a temporary ISO for the tool chain.", IsTemporary: true);
    }

    private DiscImagePreparation ConvertNkit(string imagePath, string tempIso, DiscImageFormat format)
    {
        if (!HasNkitConverter)
        {
            return new DiscImagePreparation(false, null, format,
                "This is an NKit (scrubbed) image: Dolphin can play it but deliberately cannot rebuild it. Point " +
                "'Disc image converter' at NKit's ConvertToISO.exe, or supply a plain ISO.");
        }

        // ConvertToISO always writes into <its own folder>\utils\Processed\GameCube, never to a path of our choosing,
        // and it appends _1 on a name collision. So: remember what was there, run it, then take the newest new file.
        string toolFolder = Path.GetDirectoryName(_nkitConverterPath!)!;
        string processedFolder = Path.Combine(toolFolder, "utils", "Processed", "GameCube");
        HashSet<string> before = Directory.Exists(processedFolder)
            ? Directory.GetFiles(processedFolder).ToHashSet(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        (int exitCode, string output) = Run(_nkitConverterPath!, toolFolder, new List<string> { imagePath });

        string? produced = Directory.Exists(processedFolder)
            ? Directory.GetFiles(processedFolder)
                .Where(file => !before.Contains(file))
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault()
            : null;

        // The exit code is 2 on success as well as on failure, so the output file is the only usable signal.
        if (produced is null || new FileInfo(produced).Length == 0)
        {
            return new DiscImagePreparation(false, null, format,
                $"NKit conversion did not produce an ISO (exit {exitCode}). {Trim(output)}");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(tempIso)!);
        File.Move(produced, tempIso, overwrite: true);

        Log(LogLevel.Success, $"Rebuilt the NKit image into a temporary ISO ({Bytes(new FileInfo(tempIso).Length)}).");
        return new DiscImagePreparation(true, tempIso, format,
            "Rebuilt the NKit image into a temporary ISO for the tool chain.", IsTemporary: true);
    }

    private static string? TemporaryIsoPathFor(string imagePath)
    {
        try
        {
            string root = Path.Combine(Path.GetTempPath(), "GladiusModdingTools", "converted");
            string name = Path.GetFileName(imagePath);
            foreach (string known in new[] { ".nkit.iso", ".iso", ".gcm", ".rvz", ".wia", ".gcz", ".ciso" })
            {
                if (name.EndsWith(known, StringComparison.OrdinalIgnoreCase))
                {
                    name = name[..^known.Length];
                    break;
                }
            }

            if (name.Length == 0) name = "disc";
            return Path.Combine(root, name + ".iso");
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static (int ExitCode, string Output) Run(string executable, string? workingDirectory, IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo(executable)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory ?? Path.GetDirectoryName(executable) ?? string.Empty,
        };

        foreach (string argument in arguments) startInfo.ArgumentList.Add(argument);

        using var process = new Process { StartInfo = startInfo };
        process.Start();
        process.StandardInput.Close();   // NKit's converter ends with "press enter"; never let it block.

        string stdout = process.StandardOutput.ReadToEnd();
        string stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        return (process.ExitCode, string.Join(" ", new[] { stdout, stderr }.Where(s => s.Length > 0)));
    }

    private static string Trim(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        string single = text.ReplaceLineEndings(" ").Trim();
        return single.Length <= 300 ? single : single[..300] + "...";
    }

    private static string Bytes(long size) => size >= 1L << 30
        ? $"{size / (double)(1L << 30):0.00} GB"
        : $"{size / (double)(1L << 20):0.0} MB";

    private void Log(LogLevel level, string message) => _log?.Write(level, message, "disc-image");
}
