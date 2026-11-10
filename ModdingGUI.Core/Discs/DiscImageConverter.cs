namespace ModdingGUI.Core.Discs;

using System.Diagnostics;
using ModdingGUI.Core.Logging;

/// <summary>A container the app can write.</summary>
public enum DiscImageTarget
{
    /// <summary>A plain ISO: what the tool chain, xdelta and most other tools want.</summary>
    Iso,

    /// <summary>Dolphin's recommended compressed format (zstd).</summary>
    Rvz,

    /// <summary>Dolphin's older compressed container.</summary>
    Wia,

    /// <summary>Dolphin's legacy zlib container.</summary>
    Gcz,
}

/// <summary>What happened when a conversion was asked for.</summary>
public sealed record DiscImageConversion(bool Ok, string? OutputPath, string Message);

/// <summary>
/// Converts a disc image from one format to another, writing to a destination the user chose.
/// </summary>
/// <remarks>
/// <para>
/// This is the "Convert" page's engine: Dolphin's <c>dolphin-tool</c> writes iso/gcz/wia/rvz, and NKit images are rebuilt
/// with the NKit converter that ships in <c>tools/nkit</c>. Nothing else is bundled.
/// </para>
/// <para>
/// The Dolphin arguments are the ones that were actually required on Dolphin 2606a: <c>-b</c> for gcz/wia/rvz,
/// <c>-c</c> and <c>-l</c> for wia/rvz, and each container accepts only particular block sizes (RVZ 131072, WIA
/// 2097152). Guessing any of them produces "Block size is not valid for this format" rather than a file.
/// </para>
/// <para>
/// Both tools are untrustworthy about exit codes - dolphin-tool exits 0 on garbage input and writes a file anyway, and
/// NKit's converter exits 2 on success as well as failure - so every conversion is judged by the output file, and the
/// Dolphin path additionally re-reads the header to confirm it is a GameCube image.
/// </para>
/// </remarks>
public sealed class DiscImageConverter
{
    private readonly string? _dolphinToolPath;
    private readonly string? _nkitToolFolder;
    private readonly IAppLog? _log;

    /// <param name="dolphinToolPath">Path to <c>DolphinTool.exe</c>, or null when Dolphin is not configured.</param>
    /// <param name="nkitToolFolder">Folder holding NKit's <c>utils/ConvertToISO.exe</c>, or null.</param>
    /// <param name="log">Optional log sink.</param>
    public DiscImageConverter(string? dolphinToolPath, string? nkitToolFolder = null, IAppLog? log = null)
    {
        _dolphinToolPath = string.IsNullOrWhiteSpace(dolphinToolPath) ? null : dolphinToolPath;
        _nkitToolFolder = string.IsNullOrWhiteSpace(nkitToolFolder) ? null : nkitToolFolder;
        _log = log;
    }

    /// <summary>True when Dolphin's CLI is available, i.e. iso/rvz/wia/gcz targets are possible.</summary>
    public bool HasDolphinTool => _dolphinToolPath is not null && File.Exists(_dolphinToolPath);

    /// <summary>True when the bundled NKit converter is available, i.e. NKit sources can be rebuilt.</summary>
    public bool HasNkitTool =>
        _nkitToolFolder is not null && File.Exists(Path.Combine(_nkitToolFolder, "utils", "ConvertToISO.exe"));

    /// <summary>Where the app looks for the bundled NKit converter.</summary>
    public static string NkitFolderIn(string applicationDirectory) =>
        Path.Combine(applicationDirectory, "tools", "nkit");

    /// <summary>Find <c>DolphinTool.exe</c> from the configured Dolphin executable.</summary>
    public static string? GuessDolphinToolFrom(string? dolphinExecutablePath) =>
        DiscImagePreparer.GuessConverterFrom(dolphinExecutablePath);

    /// <summary>The target formats that can be produced with what is available right now.</summary>
    public IReadOnlyList<DiscImageTarget> AvailableTargets()
    {
        // Every target needs dolphin-tool: NKit only ever produces an ISO, so it cannot be the only converter.
        return HasDolphinTool
            ? new[] { DiscImageTarget.Iso, DiscImageTarget.Rvz, DiscImageTarget.Wia, DiscImageTarget.Gcz }
            : Array.Empty<DiscImageTarget>();
    }

    /// <summary>
    /// The dolphin-tool arguments for a conversion. Exposed for tests: the flags are the part that is easy to get wrong.
    /// </summary>
    public static IReadOnlyList<string> BuildDolphinArguments(string inputPath, string outputPath, DiscImageTarget target)
    {
        var arguments = new List<string> { "convert", "-i", inputPath, "-o", outputPath, "-f", FormatName(target) };

        switch (target)
        {
            case DiscImageTarget.Rvz:
                arguments.AddRange(new[] { "-c", "zstd", "-l", "1", "-b", "131072" });
                break;

            case DiscImageTarget.Wia:
                arguments.AddRange(new[] { "-c", "none", "-b", "2097152" });
                break;

            case DiscImageTarget.Gcz:
                arguments.AddRange(new[] { "-b", "131072" });
                break;
        }

        return arguments;
    }

    private static string FormatName(DiscImageTarget target) => target switch
    {
        DiscImageTarget.Rvz => "rvz",
        DiscImageTarget.Wia => "wia",
        DiscImageTarget.Gcz => "gcz",
        _ => "iso",
    };

    /// <summary>The default file extension for a target, including the dot.</summary>
    public static string ExtensionFor(DiscImageTarget target) => "." + FormatName(target);

    /// <summary>Convert <paramref name="inputPath"/> to <paramref name="outputPath"/>.</summary>
    public DiscImageConversion Convert(string inputPath, string outputPath, DiscImageTarget target)
    {
        if (!File.Exists(inputPath))
        {
            return new DiscImageConversion(false, null, $"No file at '{inputPath}'.");
        }

        DiscImageFormat source;
        try
        {
            source = DiscImageProbe.Detect(inputPath);
        }
        catch (IOException ex)
        {
            return new DiscImageConversion(false, null, $"Could not read '{Path.GetFileName(inputPath)}': {ex.Message}");
        }

        if (source == DiscImageFormat.Unknown)
        {
            return new DiscImageConversion(false, null, $"'{Path.GetFileName(inputPath)}' is not a GameCube disc image.");
        }

        if (string.Equals(Path.GetFullPath(inputPath), Path.GetFullPath(outputPath), StringComparison.OrdinalIgnoreCase))
        {
            return new DiscImageConversion(false, null, "Source and destination are the same file.");
        }

        if (File.Exists(outputPath))
        {
            // Both tools overwrite silently; refusing is the only way the user finds out.
            return new DiscImageConversion(false, null,
                $"'{Path.GetFileName(outputPath)}' already exists. Choose another name or delete it first.");
        }

        string? folder = Path.GetDirectoryName(Path.GetFullPath(outputPath));
        if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);

        try
        {
            if (source == DiscImageFormat.Nkit)
            {
                return ConvertFromNkit(inputPath, outputPath, target);
            }

            if (!HasDolphinTool)
            {
                return new DiscImageConversion(false, null,
                    "No disc image converter is configured. Set File > Settings > General > 'Disc image converter' to " +
                    "DolphinTool.exe (it ships with Dolphin).");
            }

            return ConvertWithDolphinTool(inputPath, outputPath, target, source);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return new DiscImageConversion(false, null, $"Conversion failed: {ex.Message}");
        }
    }

    private DiscImageConversion ConvertWithDolphinTool(string inputPath, string outputPath, DiscImageTarget target, DiscImageFormat source)
    {
        (int exitCode, string output) = Run(_dolphinToolPath!, Path.GetDirectoryName(_dolphinToolPath), BuildDolphinArguments(inputPath, outputPath, target));

        if (!File.Exists(outputPath) || new FileInfo(outputPath).Length == 0)
        {
            return new DiscImageConversion(false, null,
                $"The converter did not produce a file (exit {exitCode}). {Trim(output)}");
        }

        // dolphin-tool exits 0 on garbage and still writes something, so a container is accepted on its magic and an ISO
        // is confirmed by re-reading the disc header through the tool itself.
        DiscImageFormat written;
        try
        {
            written = DiscImageProbe.Detect(outputPath);
        }
        catch (IOException ex)
        {
            return new DiscImageConversion(false, null, $"Wrote '{Path.GetFileName(outputPath)}' but could not re-read it: {ex.Message}");
        }

        if (target == DiscImageTarget.Iso)
        {
            string? id = ReadGameId(outputPath);
            if (string.IsNullOrWhiteSpace(id))
            {
                TryDelete(outputPath);
                return new DiscImageConversion(false, null,
                    "The converted ISO has no readable GameCube header, so the source was not a usable disc image.");
            }

            Log(LogLevel.Success, $"Converted {DiscImageProbe.Describe(source)} to ISO ({id}).");
            return new DiscImageConversion(true, outputPath, $"Converted to ISO ({id}).");
        }

        if (written != target.ToFormat())
        {
            TryDelete(outputPath);
            return new DiscImageConversion(false, null,
                $"The converter wrote '{DiscImageProbe.Describe(written)}' where {target} was asked for.");
        }

        Log(LogLevel.Success, $"Converted {DiscImageProbe.Describe(source)} to {target} ({Bytes(new FileInfo(outputPath).Length)}).");
        return new DiscImageConversion(true, outputPath, $"Converted to {target} ({Bytes(new FileInfo(outputPath).Length)}).");
    }

    private DiscImageConversion ConvertFromNkit(string inputPath, string outputPath, DiscImageTarget target)
    {
        if (!HasNkitTool)
        {
            return new DiscImageConversion(false, null,
                "This is an NKit image and the bundled NKit converter is missing from tools/nkit.");
        }

        if (target != DiscImageTarget.Iso)
        {
            return new DiscImageConversion(false, null,
                "An NKit image is rebuilt as an ISO first; Dolphin can then compress that ISO to " + target + ".");
        }

        // ConvertToISO always writes into <toolFolder>/utils/Processed/GameCube and appends _1 on a collision, and it
        // resolves relative paths against the working directory. So run it on a copy of the tool folder in temp: the
        // install directory may be read-only (Program Files), and this keeps Processed clutter out of it.
        string sandbox = Path.Combine(Path.GetTempPath(), "GladiusModdingTools", "nkit-" + Guid.NewGuid().ToString("N"));
        try
        {
            CopyFolder(_nkitToolFolder!, sandbox);

            string executable = Path.Combine(sandbox, "utils", "ConvertToISO.exe");
            (int exitCode, string output) = Run(executable, sandbox, new[] { inputPath });

            string processed = Path.Combine(sandbox, "utils", "Processed", "GameCube");
            string? produced = Directory.Exists(processed)
                ? Directory.GetFiles(processed, "*.iso").OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault()
                : null;

            // Exit code is 2 on success as well as failure; the output file is the signal, and a failed rebuild leaves
            // nothing behind.
            if (produced is null || new FileInfo(produced).Length == 0)
            {
                return new DiscImageConversion(false, null,
                    $"NKit conversion produced no ISO (exit {exitCode}). {Trim(output)}");
            }

            File.Move(produced, outputPath, overwrite: true);
            string? id = ReadGameId(outputPath);
            Log(LogLevel.Success, $"Rebuilt the NKit image as ISO ({Bytes(new FileInfo(outputPath).Length)}).");
            return new DiscImageConversion(true, outputPath, $"Rebuilt the NKit image as ISO ({id ?? "no header id"}).");
        }
        finally
        {
            try { Directory.Delete(sandbox, recursive: true); } catch (IOException) { }
        }
    }

    /// <summary>The six-character game id from a plain ISO, or null when this is not a plain ISO.</summary>
    private string? ReadGameId(string isoPath)
    {
        if (!HasDolphinTool) return null;

        // dolphin-tool header -j prints clean JSON; it is the only thing here that can validate a disc by structure.
        (int exitCode, string output) = Run(_dolphinToolPath!, Path.GetDirectoryName(_dolphinToolPath),
            new[] { "header", "-i", isoPath, "-j" });

        if (exitCode != 0 || output.Length == 0) return null;

        int start = output.IndexOf('{');
        int end = output.LastIndexOf('}');
        if (start < 0 || end <= start) return null;

        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(output[start..(end + 1)]);
            return document.RootElement.TryGetProperty("game_id", out var id) ? id.GetString() : null;
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    private static void CopyFolder(string source, string destination)
    {
        Directory.CreateDirectory(destination);

        foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(source, file);
            string target = Path.Combine(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { }
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
        process.StandardInput.Close();

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

    private void Log(LogLevel level, string message) => _log?.Write(level, message, "convert");
}

/// <summary>Small bridge so callers do not repeat the enum-to-format mapping.</summary>
internal static class DiscImageTargetExtensions
{
    public static DiscImageFormat ToFormat(this DiscImageTarget target) => target switch
    {
        DiscImageTarget.Rvz => DiscImageFormat.Rvz,
        DiscImageTarget.Wia => DiscImageFormat.Wia,
        DiscImageTarget.Gcz => DiscImageFormat.Gcz,
        _ => DiscImageFormat.PlainIso,
    };
}
