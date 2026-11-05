using System;
using System.IO;
using System.Linq;
using ModdingGUI.Core.Logging;
using Xunit;

namespace ModdingGUI.Tests.Core.Logging;

/// <summary>
/// The output stream that makes a run observable outside the GUI: recorded lines, levels, scopes, a
/// ring buffer, an optional file sink, and a single event consumers render or stream.
/// </summary>
public sealed class AppLogTests : IDisposable
{
    private readonly string _dir;

    public AppLogTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "ModdingGUI.Log-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); }
        catch (IOException) { /* best effort */ }
    }

    [Fact]
    public void Write_KeepsOrderAndLevels()
    {
        var log = new AppLog();

        log.Write(LogLevel.Info, "starting");
        log.Write(LogLevel.Warning, "careful");
        log.Write(LogLevel.Error, "failed");

        var tail = log.Tail();
        Assert.Equal(new[] { "starting", "careful", "failed" }, tail.Select(e => e.Message));
        Assert.Equal(new[] { LogLevel.Info, LogLevel.Warning, LogLevel.Error }, tail.Select(e => e.Level));
        Assert.False(tail[0].IsProblem);
        Assert.True(tail[1].IsProblem);
        Assert.True(tail[2].IsProblem);
    }

    [Fact]
    public void Tail_ReturnsTheMostRecentEntriesOldestFirst()
    {
        var log = new AppLog();
        for (int i = 0; i < 20; i++) log.Write(LogLevel.Info, $"line {i}");

        var tail = log.Tail(3);
        Assert.Equal(new[] { "line 17", "line 18", "line 19" }, tail.Select(e => e.Message));
    }

    [Fact]
    public void RingBuffer_DropsTheOldestEntries()
    {
        var log = new AppLog(capacity: 5);
        for (int i = 0; i < 9; i++) log.Write(LogLevel.Info, $"line {i}");

        var all = log.Tail(100);
        Assert.Equal(5, all.Count);
        Assert.Equal("line 4", all[0].Message);
        Assert.Equal("line 8", all[^1].Message);
    }

    [Fact]
    public void Scope_TagsEntriesAndRestoresOnDispose()
    {
        var log = new AppLog();
        log.Write(LogLevel.Info, "outside");

        using (log.Scope("grid-builder"))
        {
            log.Write(LogLevel.Info, "inside");

            using (log.Scope("nested"))
            {
                log.Write(LogLevel.Info, "deeper");
            }

            log.Write(LogLevel.Info, "back to grid-builder");
        }

        log.Write(LogLevel.Info, "outside again");

        Assert.Equal(
            new[] { "app", "grid-builder", "nested", "grid-builder", "app" },
            log.Tail().Select(e => e.Source));
    }

    [Fact]
    public void Write_ExplicitSourceWinsOverScope()
    {
        var log = new AppLog();
        using (log.Scope("wrapper"))
        {
            log.Write(LogLevel.Info, "tagged", source: "pack");
        }

        Assert.Equal("pack", log.Tail().Single().Source);
    }

    [Fact]
    public void Written_RaisesPerEntryAndSurvivesAFailingConsumer()
    {
        var log = new AppLog();
        int seen = 0;
        log.Written += (_, _) => throw new InvalidOperationException("consumer blew up");
        log.Written += (_, entry) => seen += entry.IsProblem ? 10 : 1;

        log.Write(LogLevel.Info, "one");       // first handler throws
        log.Write(LogLevel.Error, "two");

        Assert.Equal(11, seen);
        Assert.Equal(2, log.Tail().Count);
    }

    [Fact]
    public void Clear_EmptiesTheBufferButNotTheSink()
    {
        string sink = Path.Combine(_dir, "run.log");
        var log = new AppLog(sinkPath: sink);

        log.Write(LogLevel.Info, "kept on disk");
        log.Clear();

        Assert.Empty(log.Tail());
        Assert.Contains("kept on disk", File.ReadAllText(sink));
    }

    [Fact]
    public void Sink_AppendsOneTabSeparatedLinePerEntry()
    {
        string sink = Path.Combine(_dir, "run.log");
        var log = new AppLog(sinkPath: sink);

        log.Write(LogLevel.Success, "saved", source: "items");

        string[] lines = File.ReadAllLines(sink);
        Assert.Single(lines);
        Assert.Contains("Success", lines[0]);
        Assert.Contains("items", lines[0]);
        Assert.Contains("saved", lines[0]);
    }

    [Fact]
    public void Sink_UnwritablePath_FallsBackToMemoryOnly()
    {
        // A path whose parent is a file cannot be created; logging must still work.
        string blocker = Path.Combine(_dir, "blocker");
        File.WriteAllText(blocker, "x");

        var log = new AppLog(sinkPath: Path.Combine(blocker, "nested", "run.log"));
        log.Write(LogLevel.Info, "still recorded");

        Assert.Single(log.Tail());
    }

    [Fact]
    public void CreateForRun_TruncatesTheSinkAndPointsAtIt()
    {
        string sink = Path.Combine(_dir, "nested", "last-run.log");
        var first = AppLog.CreateForRun(sink);
        first.Write(LogLevel.Info, "from the previous run");

        var second = AppLog.CreateForRun(sink);
        second.Write(LogLevel.Info, "this run");

        string text = File.ReadAllText(sink);
        Assert.DoesNotContain("from the previous run", text);
        Assert.Contains("this run", text);
        Assert.Equal(Path.GetFullPath(sink), second.SinkPath);
    }

    [Fact]
    public void DefaultSinkPath_IsUnderLocalAppData()
    {
        string path = AppLog.DefaultSinkPath();
        Assert.Contains("GladiusModding", path, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(".log", path, StringComparison.OrdinalIgnoreCase);
    }
}
