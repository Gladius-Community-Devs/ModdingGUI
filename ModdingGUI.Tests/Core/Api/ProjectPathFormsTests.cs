using System;
using System.IO;
using System.Linq;
using ModdingGUI.Core.Api;
using Xunit;

namespace ModdingGUI.Tests.Core.Api;

/// <summary>
/// Every API accepts either the project root or its <c>_BEC</c> folder, and either separator style. The Team Builder
/// hands over the project root with forward slashes, which used to make the class and skill APIs look for
/// <c>data/config/classdefs.tok</c> directly under the project folder.
/// </summary>
public sealed class ProjectPathFormsTests : IDisposable
{
    private readonly string _project = Path.Combine(Path.GetTempPath(), "ModdingGUI.PathForms-" + Guid.NewGuid().ToString("N"));
    private readonly string _bec;

    public ProjectPathFormsTests()
    {
        _bec = Path.Combine(_project, "proj_BEC");
        Directory.CreateDirectory(Path.Combine(_bec, "data", "config"));

        File.WriteAllText(Path.Combine(_bec, "data", "config", "classdefs.tok"),
            "NUMCLASSDEFS: 1\nCREATECLASS: Legionnaire\n\tMESH: \"legionnaire\"\n");
        File.WriteAllText(Path.Combine(_bec, "data", "config", "skills.tok"),
            "NUMENTRIES: 1\n\nSKILLCREATE: \"Bash\", \"Attack\", \"Standard\"\n\tSKILLJOBPOINTCOST: 5\n");
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_project)) Directory.Delete(_project, recursive: true); }
        catch (IOException) { /* best effort */ }
    }

    private string ForwardSlashProjectRoot => _project.Replace('\\', '/');

    [Fact]
    public void ClassesLoadFromTheProjectRoot()
    {
        var classes = new ClassDefsApi().GetAll(_project);

        Assert.Single(classes);
        Assert.Equal("Legionnaire", classes[0].Name);
    }

    [Fact]
    public void ClassesLoadFromTheBecFolder()
    {
        Assert.Equal("Legionnaire", new ClassDefsApi().GetAll(_bec)[0].Name);
    }

    [Fact]
    public void ClassesLoadFromAForwardSlashProjectRoot()
    {
        // This is the shape the Team Builder passes: the tree normalises separators.
        Assert.Equal("Legionnaire", new ClassDefsApi().GetAll(ForwardSlashProjectRoot)[0].Name);
    }

    [Fact]
    public void SkillsLoadFromEveryPathForm()
    {
        var api = new SkillsApi();

        Assert.Single(api.List(_project));
        Assert.Single(api.List(_bec));
        Assert.Single(api.List(ForwardSlashProjectRoot));
    }

    [Fact]
    public void WritingThroughTheProjectRootLandsInTheBecFolder()
    {
        var api = new ClassDefsApi();

        // A key that is not present is added rather than replaced.
        api.SetField(ForwardSlashProjectRoot, "Legionnaire", "MESH", "\"legionnaire_f\"");

        string text = File.ReadAllText(Path.Combine(_bec, "data", "config", "classdefs.tok"));
        Assert.Contains("legionnaire_f", text);
    }
}
