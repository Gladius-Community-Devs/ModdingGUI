using System;
using System.IO;
using ModdingGUI.Core;
using Xunit;

namespace ModdingGUI.Tests.Core;

/// <summary>
/// A path to a file inside a project must resolve to the project. The Grid Builder used to hand the
/// edited .grd to <see cref="ProjectRoots.ResolveBecFolder"/>, which returned the file itself, so the
/// save computed a relative path of "." and asked to create a directory where the file already was
/// ("Cannot create ... because a file or directory with the same name already exists").
/// </summary>
public sealed class ProjectRootsFileTests : IDisposable
{
    private readonly string _root;

    public ProjectRootsFileTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "ModdingGUI.RootsFile-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_root, "MyMod", "MyMod_BEC", "data", "config"));
        Directory.CreateDirectory(Path.Combine(_root, "MyMod", "MyMod_ISO"));
        Directory.CreateDirectory(Path.Combine(_root, "MyMod", "MyMod_BEC", "data", "encounters", "imperia"));

        File.WriteAllBytes(GridPath, new byte[] { 1, 0, 0, 0, 0, 0, 0, 0 });
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
        catch (IOException) { /* best effort */ }
    }

    private string BecFolder => Path.Combine(_root, "MyMod", "MyMod_BEC");

    private string GridPath => Path.Combine(BecFolder, "data", "encounters", "imperia", "arena.grd");

    [Fact]
    public void ResolveBecFolder_FromAFileInsideTheProject_ReturnsTheBecFolder()
    {
        Assert.Equal(BecFolder, ProjectRoots.ResolveBecFolder(GridPath));
    }

    [Fact]
    public void ResolveBecFolder_FromAnEmptyFolderInsideTheProject_WalksUpToTheBecFolder()
    {
        string folder = Path.Combine(BecFolder, "data", "encounters", "imperia");
        Assert.Equal(BecFolder, ProjectRoots.ResolveBecFolder(folder));
    }

    [Fact]
    public void ResolveBecFolder_FromADeepFileStillFindsTheRoot()
    {
        string deep = Path.Combine(BecFolder, "data", "levels", "imperia", "arena_grd.grd");
        Directory.CreateDirectory(Path.GetDirectoryName(deep)!);
        File.WriteAllBytes(deep, new byte[] { 1, 0, 0, 0, 0, 0, 0, 0 });

        Assert.Equal(BecFolder, ProjectRoots.ResolveBecFolder(deep));
    }
}
