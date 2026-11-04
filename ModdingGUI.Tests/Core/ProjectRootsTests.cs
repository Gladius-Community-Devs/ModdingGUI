using System;
using System.IO;
using ModdingGUI.Core;
using Xunit;

namespace ModdingGUI.Tests.Core;

/// <summary>
/// Tests for <see cref="ProjectRoots"/>, which papers over the project-root vs BEC-folder
/// ambiguity that produced "classdefs.tok not found" in the skill editor.
/// </summary>
public sealed class ProjectRootsTests : IDisposable
{
    private readonly string _root;

    public ProjectRootsTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "ModdingGUI.ProjectRoots-" + Guid.NewGuid().ToString("N"));
        // Real layout: the project folder holds <name>_BEC and <name>_ISO as children.
        Directory.CreateDirectory(Path.Combine(_root, "MyMod", "MyMod_BEC", "data", "config"));
        Directory.CreateDirectory(Path.Combine(_root, "MyMod", "MyMod_ISO"));
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
        catch (IOException) { /* best effort */ }
    }

    private string ProjectFolder => Path.Combine(_root, "MyMod");

    private string BecFolder => Path.Combine(ProjectFolder, "MyMod_BEC");

    [Fact]
    public void ResolveBecFolder_FromProjectRoot_FindsTheBecChild()
    {
        Assert.Equal(BecFolder, ProjectRoots.ResolveBecFolder(ProjectFolder));
    }

    [Fact]
    public void ResolveBecFolder_FromBecFolder_IsANoOp()
    {
        Assert.Equal(BecFolder, ProjectRoots.ResolveBecFolder(BecFolder));
    }

    [Fact]
    public void ResolveBecFolder_FindsRenamedProjectFolderWithOneBecChild()
    {
        // A project folder renamed independently of its children (the CSproj-style case).
        string renamed = Path.Combine(_root, "RenamedRoot");
        Directory.CreateDirectory(Path.Combine(renamed, "Original_BEC", "data", "config"));

        Assert.Equal(Path.Combine(renamed, "Original_BEC"),
            ProjectRoots.ResolveBecFolder(renamed));
    }

    [Fact]
    public void ResolveBecFolder_UnknownPath_ReturnsInputUnchanged()
    {
        string missing = Path.Combine(_root, "NoSuchProject");

        // Unchanged so the API's own error message names what the caller supplied.
        Assert.Equal(missing, ProjectRoots.ResolveBecFolder(missing));
    }

    [Fact]
    public void IsBecFolder_DetectsTheDataConfigLayout()
    {
        Assert.True(ProjectRoots.IsBecFolder(BecFolder));
        Assert.False(ProjectRoots.IsBecFolder(Path.Combine(ProjectFolder, "MyMod_ISO")));
        Assert.False(ProjectRoots.IsBecFolder(""));
    }

    [Fact]
    public void ResolveBecFolder_EmptyInput_Throws()
    {
        Assert.Throws<ArgumentException>(() => ProjectRoots.ResolveBecFolder("  "));
    }
}
