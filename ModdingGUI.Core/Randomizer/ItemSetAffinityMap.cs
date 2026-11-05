namespace ModdingGUI.Core.Randomizer;

using System;
using System.Collections.Generic;
using System.Linq;
using ModdingGUI.Core.Api;

/// <summary>
/// Which affinity each item set grants, and which sets a gladiator of a given affinity may use.
/// </summary>
/// <remarks>
/// <para>
/// This is the cross-reference the old randomizer used for item sets: a set's affinity is the affinity of the
/// <b>last weapon</b> it contains (<c>[Weapon]</c> note), read from <c>items.tok</c>. A set with no weapon is
/// "None". A gladiator may then use sets of its own affinity <i>plus</i> the Light pool, and a gladiator with no
/// affinity may use anything — because it is the equipped weapon that lets a unit use affinity skills at all.
/// </para>
/// <para>
/// Item sets are read once through <see cref="IItemSetsApi.GetAll"/>; asking per set would re-parse the whole
/// 400-set file each time (PLAN.md rule 17).
/// </para>
/// </remarks>
public sealed class ItemSetAffinityMap
{
    /// <summary>Affinity used for sets with no weapon.</summary>
    public const string NoAffinity = "None";

    /// <summary>The affinity every unit can use, on top of its own.</summary>
    public const string UniversalAffinity = "Light";

    private readonly Dictionary<int, string> _bySet;
    private readonly Dictionary<string, List<int>> _pools;
    private readonly int[] _all;

    private ItemSetAffinityMap(Dictionary<int, string> bySet, Dictionary<string, List<int>> pools, int[] all)
    {
        _bySet = bySet;
        _pools = pools;
        _all = all;
    }

    /// <summary>How many sets the map covers.</summary>
    public int Count => _all.Length;

    /// <summary>Build the map for a project.</summary>
    public static ItemSetAffinityMap Build(
        string projectRoot, IItemsApi? items = null, IItemSetsApi? itemSets = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);

        var weaponAffinity = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in (items ?? new ItemsApi()).GetAll(projectRoot))
        {
            if (!string.Equals(item.Type, "Weapon", StringComparison.OrdinalIgnoreCase)) continue;
            if (item.Affinity is not var (element, _)) continue;
            if (string.IsNullOrWhiteSpace(element)) continue;

            weaponAffinity[item.Name] = element;
        }

        var bySet = new Dictionary<int, string>();
        var pools = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
        var all = new List<int>();

        foreach (var set in (itemSets ?? new ItemSetsApi()).GetAll(projectRoot))
        {
            all.Add(set.Index);

            // The old randomizer read the last weapon line of the set; typed rows make that explicit.
            var weapon = set.Rows
                .LastOrDefault(row => row.Note is not null
                                      && row.Note.Contains("Weapon", StringComparison.OrdinalIgnoreCase));

            string affinity = weapon is not null
                              && weaponAffinity.TryGetValue(weapon.ItemName, out string? element)
                ? element
                : NoAffinity;

            bySet[set.Index] = affinity;
            if (!pools.TryGetValue(affinity, out var pool)) pools[affinity] = pool = new List<int>();
            pool.Add(set.Index);
        }

        all.Sort();
        foreach (var pool in pools.Values) pool.Sort();

        return new ItemSetAffinityMap(bySet, pools, all.ToArray());
    }

    /// <summary>The affinity a set grants (<see cref="NoAffinity"/> when it has no weapon).</summary>
    public string AffinityOf(int index) => _bySet.TryGetValue(index, out string? affinity) ? affinity : NoAffinity;

    /// <summary>Sets a gladiator with the given affinity may use, in ascending order.</summary>
    public IReadOnlyList<int> CandidatesFor(string? gladiatorAffinity)
    {
        // No affinity, or "None": anything goes (vanilla's own comment says a None unit can equip any).
        if (string.IsNullOrWhiteSpace(gladiatorAffinity)
            || gladiatorAffinity.Trim().Equals(NoAffinity, StringComparison.OrdinalIgnoreCase))
        {
            return _all;
        }

        string affinity = gladiatorAffinity.Trim();
        var result = new SortedSet<int>();

        if (_pools.TryGetValue(affinity, out var own)) result.UnionWith(own);
        if (_pools.TryGetValue(UniversalAffinity, out var universal)) result.UnionWith(universal);

        return result.ToArray();
    }
}
