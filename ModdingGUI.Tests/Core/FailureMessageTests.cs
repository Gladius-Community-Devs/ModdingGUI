using System;
using System.IO;
using System.Linq;
using ModdingGUI.Core;
using ModdingGUI.Core.Api;
using ModdingGUI.Core.Tools;
using Xunit;

namespace ModdingGUI.Tests.Core;

/// <summary>
/// Failure points report clearly and do not cascade: a missing file names the project and the path, an unreadable
/// history folder yields an empty list, and a missing folder is not an exception where "nothing found" is the answer.
/// </summary>
public sealed class FailureMessageTests : IDisposable
{
    private readonly string _project = Path.Combine(Path.GetTempPath(), "ModdingGUI.Failures-" + Guid.NewGuid().ToString("N"));
    private readonly string _bec;

    public FailureMessageTests()
    {
        _bec = Path.Combine(_project, "proj_BEC");
        Directory.CreateDirectory(Path.Combine(_bec, "data", "config"));
        Directory.CreateDirectory(Path.Combine(_bec, "data", "school"));
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_project)) Directory.Delete(_project, recursive: true); }
        catch (IOException) { /* best effort */ }
    }

    [Fact]
    public void AMissingItemsTokNamesTheProjectAndThePath()
    {
        var error = Assert.Throws<FileNotFoundException>(() => new ItemsApi().GetAll(_project));

        Assert.Contains(_project, error.Message);
        Assert.Contains("items.tok", error.Message);
        Assert.False(string.IsNullOrEmpty(error.FileName));
    }

    [Fact]
    public void AMissingSkillsTokNamesTheProjectAndThePath()
    {
        var error = Assert.Throws<FileNotFoundException>(() => new SkillsApi().List(_project));

        Assert.Contains(_project, error.Message);
        Assert.False(string.IsNullOrEmpty(error.FileName));
    }

    [Fact]
    public void AMissingLookupTextNamesTheProjectAndThePath()
    {
        var error = Assert.Throws<FileNotFoundException>(
            () => new LookupTextApi().Load(_project));

        Assert.Contains(_project, error.Message);
        Assert.Contains("lookuptext_eng.txt", error.Message);
        Assert.False(string.IsNullOrEmpty(error.FileName));
    }

    [Fact]
    public void AMissingClassDefsNamesTheProject()
    {
        var error = Assert.Throws<FileNotFoundException>(() => new ClassDefsApi().GetAll(_project));

        Assert.Contains(_project, error.Message);
    }

    [Fact]
    public void NoHistoryGivesAnEmptyListRatherThanThrowing()
    {
        Assert.Empty(ChangeHistoryService.ListBackups(_bec));
        Assert.Empty(new HistoryApi().ListAll(_project));
        Assert.Empty(new HistoryApi().ListFiles(_project));
    }

    [Fact]
    public void MeasuringAMissingFolderIsZeroNotAnException()
    {
        Assert.Equal(0, ProjectFileOperations.Measure(Path.Combine(_project, "nope")).Files);
        Assert.Equal(0, ProjectFileOperations.Delete(Path.Combine(_project, "nope")).Files);
    }

    [Fact]
    public void CopyingFromAMissingFolderSaysSo()
    {
        var error = Assert.Throws<DirectoryNotFoundException>(
            () => ProjectFileOperations.Copy(Path.Combine(_project, "nope"), Path.Combine(_project, "dst")));

        Assert.Contains("nope", error.Message);
    }

    [Fact]
    public void ListingSchoolFilesInAProjectWithoutTheFolderIsEmpty()
    {
        // data/school exists but holds nothing; the API should answer, not throw.
        Assert.Empty(new SchoolsApi().ListFiles(_project));
    }

    [Fact]
    public void ResolvingAMissingFolderDoesNotThrow()
    {
        string missing = Path.Combine(_project, "not-here", "deeper");

        string resolved = ProjectRoots.ResolveBecFolder(missing);

        Assert.False(string.IsNullOrEmpty(resolved));
    }
}
