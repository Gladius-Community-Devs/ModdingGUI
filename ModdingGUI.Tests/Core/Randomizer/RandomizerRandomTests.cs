using System;
using System.Collections.Generic;
using System.Linq;
using ModdingGUI.Core.Randomizer;
using Xunit;

namespace ModdingGUI.Tests.Core.Randomizer;

/// <summary>
/// Determinism is the Phase 6 exit criterion: the same seed text must produce the same project, in this
/// process and any other. The old code created <c>new Random()</c> inline, so a displayed seed meant nothing.
/// </summary>
public sealed class RandomizerRandomTests
{
    [Fact]
    public void SameSeed_ProducesTheSameSequence()
    {
        var first = new RandomizerRandom("gladius-2026");
        var second = new RandomizerRandom("gladius-2026");

        var a = Enumerable.Range(0, 50).Select(_ => first.NextInt(0, 10_000)).ToArray();
        var b = Enumerable.Range(0, 50).Select(_ => second.NextInt(0, 10_000)).ToArray();

        Assert.Equal(a, b);
    }

    [Fact]
    public void DifferentSeeds_ProduceDifferentSequences()
    {
        var a = new RandomizerRandom("seed-a");
        var b = new RandomizerRandom("seed-b");

        var left = Enumerable.Range(0, 50).Select(_ => a.NextInt(0, 10_000)).ToArray();
        var right = Enumerable.Range(0, 50).Select(_ => b.NextInt(0, 10_000)).ToArray();

        Assert.NotEqual(left, right);
    }

    [Fact]
    public void EmptySeed_GeneratesOneAndReportsIt()
    {
        var run = new RandomizerRandom("");

        Assert.False(string.IsNullOrWhiteSpace(run.SeedText));
        Assert.Equal(run.SeedText, new RandomizerRandom(run.SeedText).SeedText);
    }

    [Fact]
    public void SeedHash_IsStableAndNotProcessRandomised()
    {
        // Pinned value: if this ever changes, seeds typed by users stop meaning the same thing.
        Assert.Equal(RandomizerRandom.Hash("gladius"), RandomizerRandom.Hash("gladius"));
        Assert.NotEqual(RandomizerRandom.Hash("gladius"), RandomizerRandom.Hash("gladius "));
        Assert.Equal(-109502418, RandomizerRandom.Hash("gladius"));
    }

    [Fact]
    public void NextInt_StaysInsideTheRange()
    {
        var run = new RandomizerRandom("range");

        for (int i = 0; i < 500; i++)
        {
            int value = run.NextInt(5, 9);
            Assert.InRange(value, 5, 8);
        }
    }

    [Fact]
    public void Pick_ReturnsAnItemAndCoversTheList()
    {
        var run = new RandomizerRandom("pick");
        string[] items = { "a", "b", "c" };

        var seen = new HashSet<string>();
        for (int i = 0; i < 200; i++) seen.Add(run.Pick(items));

        Assert.Equal(items.OrderBy(x => x), seen.OrderBy(x => x));
    }

    [Fact]
    public void Pick_FromAnEmptyList_Throws()
    {
        var run = new RandomizerRandom("empty");
        Assert.Throws<ArgumentException>(() => run.Pick(Array.Empty<int>()));
    }

    [Fact]
    public void PickWeighted_SkipsZeroWeights()
    {
        var run = new RandomizerRandom("weights");
        var items = new List<(string Item, int Weight)> { ("never", 0), ("always", 10), ("also", 0) };

        for (int i = 0; i < 100; i++)
        {
            Assert.Equal("always", run.PickWeighted(items));
        }
    }

    [Fact]
    public void PickWeighted_AllZeroFallsBackToUniform()
    {
        var run = new RandomizerRandom("zero-weights");
        var items = new List<(int, int)> { (1, 0), (2, 0), (3, 0) };

        var seen = new HashSet<int>();
        for (int i = 0; i < 200; i++) seen.Add(run.PickWeighted(items));

        Assert.Equal(new[] { 1, 2, 3 }, seen.OrderBy(x => x));
    }

    [Fact]
    public void PickWeighted_RespectsTheRatio()
    {
        var run = new RandomizerRandom("ratio");
        var items = new List<(string Item, int Weight)> { ("common", 90), ("rare", 10) };

        int rare = 0;
        const int runs = 4000;
        for (int i = 0; i < runs; i++)
        {
            if (run.PickWeighted(items) == "rare") rare++;
        }

        // 10% expected; allow a wide band so the test is not flaky.
        Assert.InRange(rare, runs * 0.06, runs * 0.14);
    }

    [Fact]
    public void Shuffle_IsAPermutationAndDeterministicPerSeed()
    {
        var first = new RandomizerRandom("shuffle");
        var second = new RandomizerRandom("shuffle");

        var a = Enumerable.Range(0, 40).ToList();
        var b = Enumerable.Range(0, 40).ToList();
        first.Shuffle(a);
        second.Shuffle(b);

        Assert.Equal(a, b);
        Assert.Equal(Enumerable.Range(0, 40), a.OrderBy(x => x));
        Assert.NotEqual(Enumerable.Range(0, 40).ToList(), a);
    }

    [Fact]
    public void Chance_IsClampedAndRoughlyRight()
    {
        var run = new RandomizerRandom("chance");

        Assert.False(run.Chance(0));
        Assert.True(run.Chance(1));

        int hits = 0;
        const int runs = 2000;
        for (int i = 0; i < runs; i++)
        {
            if (run.Chance(0.25)) hits++;
        }

        Assert.InRange(hits, runs * 0.18, runs * 0.32);
    }
}
