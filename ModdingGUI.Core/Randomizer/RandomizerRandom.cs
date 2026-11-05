namespace ModdingGUI.Core.Randomizer;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

/// <summary>
/// The single source of randomness for a randomization run, seeded from free text.
/// </summary>
/// <remarks>
/// <para>
/// One instance per run, handed to every operation, so the same seed text always produces the same project.
/// The old code created <c>new Random()</c> inline all over the place, which made runs unreproducible even
/// when a seed was displayed.
/// </para>
/// <para>
/// The seed is hashed with FNV-1a rather than <see cref="string.GetHashCode()"/>, because that is randomized
/// per process on .NET and a seed typed into the GUI must mean the same thing tomorrow.
/// </para>
/// </remarks>
public sealed class RandomizerRandom
{
    private readonly Random _random;

    /// <summary>Create a run from a seed; empty text generates one that <see cref="SeedText"/> reports.</summary>
    public RandomizerRandom(string? seedText)
    {
        SeedText = string.IsNullOrWhiteSpace(seedText) ? GenerateSeedText() : seedText.Trim();
        _random = new Random(Hash(SeedText));
    }

    /// <summary>The seed in use — show this to the user so a run can be repeated.</summary>
    public string SeedText { get; }

    /// <summary>Uniform integer in <c>[minInclusive, maxExclusive)</c>.</summary>
    public int NextInt(int minInclusive, int maxExclusive) => _random.Next(minInclusive, maxExclusive);

    /// <summary>Uniform double in <c>[0, 1)</c>.</summary>
    public double NextDouble() => _random.NextDouble();

    /// <summary>True with probability <paramref name="probability"/> (clamped to 0-1).</summary>
    public bool Chance(double probability) => _random.NextDouble() < Math.Clamp(probability, 0d, 1d);

    /// <summary>Pick one item uniformly.</summary>
    public T Pick<T>(IReadOnlyList<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (items.Count == 0) throw new ArgumentException("Cannot pick from an empty list.", nameof(items));
        return items[NextInt(0, items.Count)];
    }

    /// <summary>
    /// Pick one item with weights. Non-positive weights are skipped; every weight being zero makes the pick
    /// uniform, which keeps a degenerate table from crashing a run.
    /// </summary>
    public T PickWeighted<T>(IReadOnlyList<(T Item, int Weight)> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (items.Count == 0) throw new ArgumentException("Cannot pick from an empty list.", nameof(items));

        long total = 0;
        foreach (var (_, weight) in items)
        {
            if (weight > 0) total += weight;
        }

        if (total <= 0) return Pick(items.Select(i => i.Item).ToArray());

        long roll = (long)(_random.NextDouble() * total);
        long running = 0;
        foreach (var (item, weight) in items)
        {
            if (weight <= 0) continue;
            running += weight;
            if (roll < running) return item;
        }

        // Floating-point edge: fall back to the last positive-weight entry.
        for (int i = items.Count - 1; i >= 0; i--)
        {
            if (items[i].Weight > 0) return items[i].Item;
        }

        return items[^1].Item;
    }

    /// <summary>In-place Fisher-Yates shuffle.</summary>
    public void Shuffle<T>(IList<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        for (int i = items.Count - 1; i > 0; i--)
        {
            int j = NextInt(0, i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
    }

    /// <summary>A shuffled copy.</summary>
    public List<T> Shuffled<T>(IEnumerable<T> items)
    {
        var list = new List<T>(items);
        Shuffle(list);
        return list;
    }

    /// <summary>Stable 32-bit FNV-1a hash of the seed text.</summary>
    internal static int Hash(string seedText)
    {
        const uint offsetBasis = 2166136261;
        const uint prime = 16777619;

        uint hash = offsetBasis;
        foreach (byte b in Encoding.UTF8.GetBytes(seedText))
        {
            hash ^= b;
            hash *= prime;
        }

        // Random accepts int.MinValue; the cast is deliberate.
        return unchecked((int)hash);
    }

    private static string GenerateSeedText() =>
        DateTime.UtcNow.Ticks.ToString("X", CultureInfo.InvariantCulture);
}
