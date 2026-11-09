namespace ModdingGUI.Core.Tools;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

/// <summary>How far a copy or a delete has got.</summary>
/// <param name="FilesDone">Files processed so far.</param>
/// <param name="FilesTotal">Files the operation will process.</param>
/// <param name="BytesDone">Bytes processed so far.</param>
/// <param name="BytesTotal">Bytes the operation will process.</param>
/// <param name="Current">The file being processed, for a status line.</param>
public sealed record FileOperationProgress(
    int FilesDone,
    int FilesTotal,
    long BytesDone,
    long BytesTotal,
    string? Current)
{
    /// <summary>Whole-percent completion, 0-100.</summary>
    public int Percent => FilesTotal <= 0 ? 0 : (int)Math.Round(100.0 * FilesDone / FilesTotal);
}

/// <summary>What a copy or delete actually did.</summary>
public sealed record ProjectFileResult(int Files, long Bytes);

/// <summary>
/// Copying and deleting a project tree, with progress and cancellation.
/// </summary>
/// <remarks>
/// <para>
/// The shell used to call <c>Directory.Delete(path, recursive: true)</c>, which cannot report progress and cannot
/// be cancelled, and its copy walked the tree itself. Both live here now so the same code is testable and the
/// progress bar can be a real percentage instead of a spinner.
/// </para>
/// <para>
/// Copy applies the project rename: a file <c>&lt;original&gt;.iso</c> becomes <c>&lt;new&gt;.iso</c>, and folders
/// <c>&lt;original&gt;_BEC</c> / <c>&lt;original&gt;_ISO</c> become the new project's, which is what the copy dialog
/// offers.
/// </para>
/// </remarks>
public static class ProjectFileOperations
{
    /// <summary>Report progress at most this often, unless the percentage changes.</summary>
    private const int ReportEveryFiles = 25;

    /// <summary>Count the files and bytes under a folder.</summary>
    public static ProjectFileResult Measure(string folder)
    {
        if (!Directory.Exists(folder)) return new ProjectFileResult(0, 0);

        int files = 0;
        long bytes = 0;

        string[] all;
        try
        {
            all = Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).ToArray();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new IOException($"Could not enumerate '{folder}': {ex.Message}", ex);
        }

        foreach (string file in all)
        {
            files++;
            try
            {
                bytes += new FileInfo(file).Length;
            }
            catch (IOException)
            {
                // A file that vanished mid-measure simply does not count.
            }
        }

        return new ProjectFileResult(files, bytes);
    }

    /// <summary>
    /// Delete a folder tree, reporting progress per file and honouring cancellation between files.
    /// </summary>
    /// <exception cref="OperationCanceledException">The token was cancelled; the tree is partially deleted.</exception>
    public static ProjectFileResult Delete(
        string folder,
        IProgress<FileOperationProgress>? progress = null,
        CancellationToken token = default)
    {
        if (!Directory.Exists(folder)) return new ProjectFileResult(0, 0);

        var totals = Measure(folder);
        int done = 0;
        long bytes = 0;
        int lastPercent = -1;

        foreach (string file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
        {
            token.ThrowIfCancellationRequested();

            long length;
            try
            {
                length = new FileInfo(file).Length;
            }
            catch (IOException)
            {
                length = 0;
            }

            // Read-only files are common in an unpacked project and would fail the delete.
            var info = new FileInfo(file);
            if (info.Exists && info.IsReadOnly) info.IsReadOnly = false;

            try
            {
                File.Delete(file);
            }
            catch (DirectoryNotFoundException)
            {
                // Already gone; nothing to do.
            }

            done++;
            bytes += length;

            var snapshot = new FileOperationProgress(done, totals.Files, bytes, totals.Bytes, file);
            if (snapshot.Percent != lastPercent || done % ReportEveryFiles == 0 || done == totals.Files)
            {
                lastPercent = snapshot.Percent;
                progress?.Report(snapshot);
            }
        }

        // Deepest first, so each directory is empty by the time it is removed.
        var directories = Directory.EnumerateDirectories(folder, "*", SearchOption.AllDirectories)
            .OrderByDescending(path => path.Count(c => c == Path.DirectorySeparatorChar))
            .ToArray();

        foreach (string directory in directories)
        {
            token.ThrowIfCancellationRequested();

            try
            {
                Directory.Delete(directory, recursive: false);
            }
            catch (DirectoryNotFoundException)
            {
                // Already removed with its parent.
            }
        }

        token.ThrowIfCancellationRequested();
        Directory.Delete(folder, recursive: false);

        progress?.Report(new FileOperationProgress(totals.Files, totals.Files, totals.Bytes, totals.Bytes, null));
        return new ProjectFileResult(done, bytes);
    }

    /// <summary>
    /// Copy a folder tree, optionally renaming the project inside it, reporting progress and honouring cancellation.
    /// </summary>
    /// <exception cref="OperationCanceledException">The token was cancelled; the destination is incomplete.</exception>
    public static ProjectFileResult Copy(
        string source,
        string destination,
        string? originalName = null,
        string? newName = null,
        IProgress<FileOperationProgress>? progress = null,
        CancellationToken token = default)
    {
        if (!Directory.Exists(source))
        {
            throw new DirectoryNotFoundException($"Source directory does not exist: {source}");
        }

        var totals = Measure(source);
        int done = 0;
        long bytes = 0;
        int lastPercent = -1;

        void Walk(string from, string to)
        {
            token.ThrowIfCancellationRequested();
            Directory.CreateDirectory(to);

            foreach (string file in Directory.EnumerateFiles(from, "*", SearchOption.TopDirectoryOnly))
            {
                token.ThrowIfCancellationRequested();

                string name = Path.GetFileName(file);
                if (!string.IsNullOrEmpty(originalName) && !string.IsNullOrEmpty(newName))
                {
                    if (name.Equals($"{originalName}.iso", StringComparison.OrdinalIgnoreCase))
                    {
                        name = $"{newName}.iso";
                    }
                }

                string target = Path.Combine(to, name);
                long length;
                try
                {
                    length = new FileInfo(file).Length;
                }
                catch (IOException)
                {
                    length = 0;
                }

                File.Copy(file, target, overwrite: false);

                done++;
                bytes += length;

                var snapshot = new FileOperationProgress(done, totals.Files, bytes, totals.Bytes, file);
                if (snapshot.Percent != lastPercent || done % ReportEveryFiles == 0 || done == totals.Files)
                {
                    lastPercent = snapshot.Percent;
                    progress?.Report(snapshot);
                }
            }

            foreach (string directory in Directory.EnumerateDirectories(from, "*", SearchOption.TopDirectoryOnly))
            {
                string name = Path.GetFileName(directory);
                if (!string.IsNullOrEmpty(originalName) && !string.IsNullOrEmpty(newName))
                {
                    if (name.Equals($"{originalName}_BEC", StringComparison.OrdinalIgnoreCase))
                    {
                        name = $"{newName}_BEC";
                    }
                    else if (name.Equals($"{originalName}_ISO", StringComparison.OrdinalIgnoreCase))
                    {
                        name = $"{newName}_ISO";
                    }
                }

                Walk(directory, Path.Combine(to, name));
            }
        }

        Walk(source, destination);

        progress?.Report(new FileOperationProgress(totals.Files, totals.Files, totals.Bytes, totals.Bytes, null));
        return new ProjectFileResult(done, bytes);
    }
}
