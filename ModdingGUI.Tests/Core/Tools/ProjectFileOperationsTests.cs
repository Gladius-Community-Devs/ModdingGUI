using System;
using System.IO;
using System.Linq;
using System.Threading;
using ModdingGUI.Core.Tools;
using Xunit;

namespace ModdingGUI.Tests.Core.Tools;

/// <summary>
/// Copy and delete with real progress and cancellation. The dialogs used to call
/// <c>Directory.Delete(recursive: true)</c>, which reports nothing and cannot be stopped.
/// </summary>
public sealed class ProjectFileOperationsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ModdingGUI.Files-" + Guid.NewGuid().ToString("N"));

    public ProjectFileOperationsTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
            {
                foreach (string file in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories))
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                }

                Directory.Delete(_root, recursive: true);
            }
        }
        catch (IOException)
        {
            // best effort
        }
    }

    private string Build(string name = "proj")
    {
        string project = Path.Combine(_root, name);
        Directory.CreateDirectory(Path.Combine(project, $"{name}_BEC", "data", "units"));
        Directory.CreateDirectory(Path.Combine(project, $"{name}_ISO", "files"));

        File.WriteAllText(Path.Combine(project, $"{name}.iso"), "iso");
        File.WriteAllText(Path.Combine(project, $"{name}_BEC", "data", "units", "gladiators.txt"), new string('x', 4096));
        File.WriteAllText(Path.Combine(project, $"{name}_ISO", "files", "boot.bin"), new string('y', 2048));
        return project;
    }

    [Fact]
    public void MeasureCountsFilesAndBytes()
    {
        string project = Build();

        var measured = ProjectFileOperations.Measure(project);

        Assert.Equal(3, measured.Files);
        Assert.Equal(3 + 4096 + 2048, measured.Bytes);
    }

    [Fact]
    public void DeleteRemovesEverythingAndReportsEveryFile()
    {
        string project = Build();
        var seen = new System.Collections.Generic.List<FileOperationProgress>();
        var progress = new Progress<FileOperationProgress>(seen.Add);

        var result = ProjectFileOperations.Delete(project, progress);

        Assert.False(Directory.Exists(project));
        Assert.Equal(3, result.Files);
        Assert.Equal(3 + 4096 + 2048, result.Bytes);
    }

    [Fact]
    public void DeleteProgressReachesOneHundredPercent()
    {
        string project = Build();
        int last = -1;
        var progress = new Progress<FileOperationProgress>(p => last = Math.Max(last, p.Percent));

        ProjectFileOperations.Delete(project, progress);

        // The reporter posts asynchronously; give it a moment to drain.
        for (int attempt = 0; attempt < 50 && last < 100; attempt++) Thread.Sleep(10);
        Assert.Equal(100, last);
    }

    [Fact]
    public void DeleteRemovesReadOnlyFiles()
    {
        string project = Build();
        string locked = Path.Combine(project, "proj_BEC", "data", "units", "gladiators.txt");
        File.SetAttributes(locked, FileAttributes.ReadOnly);

        ProjectFileOperations.Delete(project);

        Assert.False(Directory.Exists(project));
    }

    [Fact]
    public void DeleteHonoursCancellation()
    {
        string project = Build("big");
        for (int i = 0; i < 20; i++)
        {
            File.WriteAllText(Path.Combine(project, "big_BEC", "data", "units", $"f{i}.txt"), "z");
        }

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.Throws<OperationCanceledException>(() => ProjectFileOperations.Delete(project, null, cts.Token));
        Assert.True(Directory.Exists(project));
    }

    [Fact]
    public void CopyRenamesTheProjectInsideTheTree()
    {
        string project = Build("old");
        string destination = Path.Combine(_root, "new");

        var result = ProjectFileOperations.Copy(project, destination, "old", "new");

        Assert.Equal(3, result.Files);
        Assert.True(File.Exists(Path.Combine(destination, "new.iso")));
        Assert.False(File.Exists(Path.Combine(destination, "old.iso")));
        Assert.True(Directory.Exists(Path.Combine(destination, "new_BEC", "data", "units")));
        Assert.True(Directory.Exists(Path.Combine(destination, "new_ISO", "files")));
        Assert.Equal(
            new FileInfo(Path.Combine(project, "old_BEC", "data", "units", "gladiators.txt")).Length,
            new FileInfo(Path.Combine(destination, "new_BEC", "data", "units", "gladiators.txt")).Length);
    }

    [Fact]
    public void CopyWithoutNamesKeepsTheTreeAsIs()
    {
        string project = Build("plain");
        string destination = Path.Combine(_root, "copy");

        ProjectFileOperations.Copy(project, destination);

        Assert.True(File.Exists(Path.Combine(destination, "plain.iso")));
        Assert.True(Directory.Exists(Path.Combine(destination, "plain_BEC")));
    }

    [Fact]
    public void CopyReportsProgress()
    {
        string project = Build("src");
        var percentages = new System.Collections.Generic.List<int>();
        var progress = new Progress<FileOperationProgress>(p => percentages.Add(p.Percent));

        ProjectFileOperations.Copy(project, Path.Combine(_root, "dst"), "src", "dst", progress);

        for (int attempt = 0; attempt < 50 && (percentages.Count == 0 || percentages.Max() < 100); attempt++)
        {
            Thread.Sleep(10);
        }

        Assert.NotEmpty(percentages);
        Assert.Equal(100, percentages.Max());
    }

    [Fact]
    public void CopyHonoursCancellation()
    {
        string project = Build("cancel");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.Throws<OperationCanceledException>(() =>
            ProjectFileOperations.Copy(project, Path.Combine(_root, "never"), "cancel", "never", null, cts.Token));
    }

    [Fact]
    public void MissingSourceOrTargetIsReportedClearly()
    {
        Assert.Equal(0, ProjectFileOperations.Measure(Path.Combine(_root, "nope")).Files);
        Assert.Equal(0, ProjectFileOperations.Delete(Path.Combine(_root, "nope")).Files);
        Assert.Throws<DirectoryNotFoundException>(() =>
            ProjectFileOperations.Copy(Path.Combine(_root, "nope"), Path.Combine(_root, "dst")));
    }
}
