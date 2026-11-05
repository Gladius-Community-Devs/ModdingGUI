using System;
using System.IO;
using System.Linq;
using ModdingGUI.Core.Api;
using ModdingGUI.Core.Randomizer;
using ModdingGUI.Core.Tools;
using Xunit;

namespace ModdingGUI.Tests.Core.Randomizer;

/// <summary>
/// Randomizing a hero's class invalidates campaign scripts that branch on the original heroes: the cameo
/// "costume change" tests and the later removal of Ludo and Gwazi. The original randomizer edited six flow files
/// (and the Fjord league when Valens is the hero) and compiled them; this pins that behaviour.
/// </summary>
public sealed class ScriptPatcherTests : IDisposable
{
    private readonly string _project;
    private readonly string _bec;

    private static readonly string[] FlowScripts =
    {
        "gameflowvalenssteppes.scp",
        "gameflowvalensexpanse.scp",
        "gameflowursulasteppes.scp",
        "gameflowursulaexpanse.scp",
        "gameflowvalensendgame.scp",
        "gameflowursulaendgame.scp",
    };

    public ScriptPatcherTests()
    {
        _project = Path.Combine(Path.GetTempPath(), "ModdingGUI.Scripts-" + Guid.NewGuid().ToString("N"));
        _bec = Path.Combine(_project, "proj_BEC");

        // A full project, because one test runs the whole randomizer and the hero step reads the tables.
        foreach (string relative in new[]
                 {
                     "data/units/gladiators.txt", "data/units/statsets.txt", "data/units/skillsets.txt",
                     "data/units/itemsets.txt", "data/units/schools.txt", "data/units/tints.txt",
                     "data/config/classdefs.tok", "data/config/items.tok",
                     "data/school/valensimperia.tok", "data/school/ursulanordagh.tok",
                 })
        {
            string target = Path.Combine(_bec, relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(Path.Combine(CleanBecCorpus.Root!, relative.Replace('/', Path.DirectorySeparatorChar)), target);
        }

        Directory.CreateDirectory(Path.Combine(_bec, "data", "script", "bin"));
        foreach (string script in FlowScripts.Append("leaguefjordfallen.scp"))
        {
            string source = Path.Combine(CleanBecCorpus.Root!, "data", "script", script);
            if (File.Exists(source))
            {
                File.Copy(source, Path.Combine(_bec, "data", "script", script));
            }
        }
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_project)) Directory.Delete(_project, recursive: true); }
        catch (IOException) { /* best effort */ }
    }

    private bool Ready => CleanBecCorpus.IsAvailable &&
                          FlowScripts.All(s => File.Exists(Path.Combine(_bec, "data", "script", s)));

    private string Text(string script) =>
        File.ReadAllText(Path.Combine(_bec, "data", "script", script));

    [Fact]
    public void FlowScripts_LoseTheirCostumeChangeTest_AndTheirRemoveCharacterLines()
    {
        if (!Ready) return;

        var result = new ScriptPatcher().Apply(_bec, HeroChoice.Ursula);

        Assert.Contains("gameflowvalenssteppes.scp", result.Modified);
        foreach (string script in FlowScripts)
        {
            string text = Text(script);
            Assert.Contains("!= 0)", text);
            Assert.DoesNotContain("gb.SchoolRemoveCharacter(\"Ludo\")", text);
            Assert.DoesNotContain("gb.SchoolRemoveCharacter(\"Gwazi\")", text);
        }
    }

    [Fact]
    public void EndgameScripts_RewriteBothTests()
    {
        if (!Ready) return;

        new ScriptPatcher().Apply(_bec, HeroChoice.Ursula);

        foreach (string script in new[] { "gameflowvalensendgame.scp", "gameflowursulaendgame.scp" })
        {
            string text = Text(script);
            Assert.DoesNotContain("EndGameCostumeChange\" != 1)", text);
            Assert.DoesNotContain("EndGameCostumeChangeAgain\" != 1)", text);
        }
    }

    [Fact]
    public void ValensAlsoRewritesTheFjordLeagueBranch()
    {
        if (!Ready) return;

        const string marker = "school.IsMember(\"Urlan\")";
        int before = Text("leaguefjordfallen.scp").Split(marker).Length - 1;

        var result = new ScriptPatcher().Apply(_bec, HeroChoice.Valens);

        string text = Text("leaguefjordfallen.scp");
        int after = text.Split(marker).Length - 1;

        Assert.Contains("leaguefjordfallen.scp", result.Modified);
        // Five talk-box branches also test for Urlan; only the league-name branch is replaced.
        Assert.Equal(before - 1, after);
        Assert.DoesNotContain("name == \"AheadOfThePack\"", text);
        Assert.Contains("PackValens", text);
    }

    [Fact]
    public void UrsulaLeavesTheFjordLeagueAlone()
    {
        if (!Ready) return;

        string path = Path.Combine(_bec, "data", "script", "leaguefjordfallen.scp");
        byte[] before = File.ReadAllBytes(path);

        new ScriptPatcher().Apply(_bec, HeroChoice.Ursula);

        Assert.Equal(before, File.ReadAllBytes(path));
    }

    [Fact]
    public void ApplyingTwiceIsANoOp()
    {
        if (!Ready) return;

        new ScriptPatcher().Apply(_bec, HeroChoice.Ursula);
        var second = new ScriptPatcher().Apply(_bec, HeroChoice.Ursula);

        Assert.False(second.HasChanges);
    }

    [Fact]
    public void PatchedScriptsGetBackups()
    {
        if (!Ready) return;

        var result = new ScriptPatcher().Apply(_bec, HeroChoice.Ursula);

        string history = Path.Combine(_bec, ".mod-history", "data", "script");
        Assert.True(Directory.Exists(history));
        foreach (string script in result.Modified)
        {
            Assert.NotEmpty(Directory.GetFiles(Path.Combine(history, script)));
        }
    }

    [Fact]
    public void MissingScriptsAreReportedNotFatal()
    {
        if (!Ready) return;

        File.Delete(Path.Combine(_bec, "data", "script", "gameflowursulasteppes.scp"));

        var result = new ScriptPatcher().Apply(_bec, HeroChoice.Ursula);

        Assert.Contains("gameflowursulasteppes.scp", result.Missing);
    }

    [Fact]
    public void CompileBatchPassesTheScbOutput()
    {
        string batch = ProjectBuildService.BuildCompileBatch(
            new BuildOptions(_bec, Path.Combine(_project, "tools")), new[] { "gameflowvalenssteppes.scp" });

        Assert.Contains("gameflowvalenssteppes.scp\"", batch);
        Assert.Contains(".scb\"", batch);
        Assert.Contains("bin", batch);
    }

    [Fact]
    public void TheRunReportsTheScriptsItPatched()
    {
        if (!Ready) return;

        var result = new RandomizerService().Randomize(_bec,
            new RandomizerOptions { Seed = "scripts", RandomizeHeroes = true });

        // A full project is required for the hero step; this fixture only stages scripts, so the run must still
        // report the patched scripts it found rather than failing the whole step.
        Assert.Contains("scripts", result.Steps);
        Assert.Contains("gameflowvalenssteppes.scp", result.ModifiedScripts);
    }
}
