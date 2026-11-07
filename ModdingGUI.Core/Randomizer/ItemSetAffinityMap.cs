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

    /// <summary>Weapon subtypes that a class can only fight with when it has no melee option.</summary>
    private static readonly string[] RangedSubtypes = { "Bow", "Javelin" };

    private readonly Dictionary<int, string> _bySet;
    private readonly Dictionary<int, string> _weaponSubtypeBySet;
    private readonly Dictionary<string, List<int>> _pools;
    private readonly int[] _all;

    private ItemSetAffinityMap(
        Dictionary<int, string> bySet, Dictionary<string, List<int>> pools, int[] all,
        Dictionary<int, string> weaponSubtypeBySet)
    {
        _bySet = bySet;
        _weaponSubtypeBySet = weaponSubtypeBySet;
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

        // ItemsApi addresses data/config/items.tok directly, so hand it the _BEC folder rather than the project
        // root the caller passes to the rest of the API surface.
        string bec = ProjectRoots.ResolveBecFolder(projectRoot);

        var weaponAffinity = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in (items ?? new ItemsApi()).GetAll(bec))
        {
            if (!string.Equals(item.Type, "Weapon", StringComparison.OrdinalIgnoreCase)) continue;
            if (item.Affinity is not var (element, _)) continue;
            if (string.IsNullOrWhiteSpace(element)) continue;

            weaponAffinity[item.Name] = element;
        }

        // Item subtype (Bow, Sword, …) per name, so a set's level-1 weapon can be classified.
        var subtypeOfItem = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in (items ?? new ItemsApi()).GetAll(bec))
        {
            if (!string.IsNullOrWhiteSpace(item.SubType)) subtypeOfItem[item.Name] = item.SubType!;
        }

        var weaponSubtypeBySet = new Dictionary<int, string>();
        var bySet = new Dictionary<int, string>();
        var pools = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
        var all = new List<int>();

        foreach (var set in (itemSets ?? new ItemSetsApi()).GetAll(bec))
        {
            // A set that carries no row covering level 1 equips nothing at all, and 28 of vanilla's 404 sets
            // are completely empty. Handing one to a unit produces a gladiator with no gear and, for a team
            // member, a block with an empty ITEMSCOMP.
            bool usable = set.Rows.Any(row => row.MinLevel <= 1 && row.MaxLevel >= 1);
            if (!usable) continue;

            all.Add(set.Index);

            // The old randomizer read the last weapon line of the set; typed rows make that explicit.
            var weapon = set.Rows
                .LastOrDefault(row => row.Note is not null
                                      && row.Note.Contains("Weapon", StringComparison.OrdinalIgnoreCase));

            string affinity = weapon is not null
                              && weaponAffinity.TryGetValue(weapon.ItemName, out string? element)
                ? element
                : NoAffinity;

            var levelOneWeapon = set.Rows.FirstOrDefault(row =>
                row.MinLevel <= 1 && row.MaxLevel >= 1
                && row.Note is not null
                && row.Note.Contains("Weapon", StringComparison.OrdinalIgnoreCase));

            if (levelOneWeapon is not null && subtypeOfItem.TryGetValue(levelOneWeapon.ItemName, out string? sub))
            {
                weaponSubtypeBySet[set.Index] = sub;
            }

            bySet[set.Index] = affinity;
            if (!pools.TryGetValue(affinity, out var pool)) pools[affinity] = pool = new List<int>();
            pool.Add(set.Index);
        }

        all.Sort();
        foreach (var pool in pools.Values) pool.Sort();

        return new ItemSetAffinityMap(bySet, pools, all.ToArray(), weaponSubtypeBySet);
    }

    /// <summary>The affinity a set grants (<see cref="NoAffinity"/> when it has no weapon).</summary>
    public string AffinityOf(int index) => _bySet.TryGetValue(index, out string? affinity) ? affinity : NoAffinity;

    /// <summary>The level-1 weapon subtype a set carries, or null when it has no weapon.</summary>
    public string? WeaponSubtypeOf(int index) =>
        _weaponSubtypeBySet.TryGetValue(index, out string? subtype) ? subtype : null;

    /// <summary>True when the subtype is one a class cannot fight without (bow or javelin).</summary>
    public static bool IsRanged(string? subtype) =>
        subtype is not null && RangedSubtypes.Contains(subtype, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Sets a gladiator with the given affinity may use, in ascending order. When
    /// <paramref name="requiredWeaponSubtype"/> is given, only sets whose level-1 weapon is that subtype are
    /// returned — a bow class with a sword is a class that passes every turn.
    /// </summary>
    public IReadOnlyList<int> CandidatesFor(string? gladiatorAffinity, string? requiredWeaponSubtype = null)
    {
        // No affinity, or "None": anything goes (vanilla's own comment says a None unit can equip any).
        IReadOnlyList<int> Filter(IReadOnlyList<int> indexes) =>
            requiredWeaponSubtype is null
                ? indexes
                : indexes.Where(index =>
                    string.Equals(WeaponSubtypeOf(index), requiredWeaponSubtype, StringComparison.OrdinalIgnoreCase))
                    .ToArray();

        if (string.IsNullOrWhiteSpace(gladiatorAffinity)
            || gladiatorAffinity.Trim().Equals(NoAffinity, StringComparison.OrdinalIgnoreCase))
        {
            return Filter(_all);
        }

        string affinity = gladiatorAffinity.Trim();
        var result = new SortedSet<int>();

        if (_pools.TryGetValue(affinity, out var own)) result.UnionWith(own);
        if (_pools.TryGetValue(UniversalAffinity, out var universal)) result.UnionWith(universal);

        return Filter(result.ToArray());
    }
}
