using System;
using System.IO;
using ModdingGUI.Core;
using Xunit;

namespace ModdingGUI.Tests.Core;

/// <summary>
/// The projects folder setting: a configured folder wins, a missing one falls back to the app folder rather than
/// hiding every project.
/// </summary>
public sealed class ProjectsFolderTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ModdingGUI.Projects-" + Guid.NewGuid().ToString("N"));
    private readonly string _app;
    private readonly string _projects;

    public ProjectsFolderTests()
    {
        _app = Path.Combine(_root, "app");
        _projects = Path.Combine(_root, "projects");
        Directory.CreateDirectory(_app);
        Directory.CreateDirectory(_projects);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
        catch (IOException) { /* best effort */ }
    }

    [Fact]
    public void AConfiguredFolderWins()
    {
        Assert.Equal(Path.GetFullPath(_projects), ProjectRoots.ResolveProjectsFolder(_projects, _app));
    }

    [Fact]
    public void BlankKeepsTheAppFolder()
    {
        Assert.Equal(Path.GetFullPath(_app), ProjectRoots.ResolveProjectsFolder(string.Empty, _app));
        Assert.Equal(Path.GetFullPath(_app), ProjectRoots.ResolveProjectsFolder(null, _app));
        Assert.Equal(Path.GetFullPath(_app), ProjectRoots.ResolveProjectsFolder("   ", _app));
    }

    [Fact]
    public void AMissingFolderFallsBackInsteadOfLosingEveryProject()
    {
        string gone = Path.Combine(_root, "not-here");
        Assert.Equal(Path.GetFullPath(_app), ProjectRoots.ResolveProjectsFolder(gone, _app));
    }

    [Fact]
    public void AFolderWithTrailingSeparatorIsAccepted()
    {
        string withSlash = _projects + Path.DirectorySeparatorChar;
        Assert.Equal(Path.GetFullPath(_projects), ProjectRoots.ResolveProjectsFolder(withSlash, _app));
    }

    [Fact]
    public void ARelativeFolderResolvesAgainstTheProcessDirectory()
    {
        string resolved = ProjectRoots.ResolveProjectsFolder(".", _app);
        Assert.True(Directory.Exists(resolved));
    }
}
