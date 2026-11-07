namespace ModdingGUI.Core.Randomizer;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ModdingGUI.Core.Api;
using ModdingGUI.Core.Logging;
using ModdingGUI.Core.Units;

/// <summary>
/// A gladiator's loadout: the seven fields that make a unit play the way it does, taken together.
/// </summary>
/// <remarks>
/// <para>
/// The randomizer swaps these as a <b>bundle</b> rather than shuffling columns independently. That is the
/// cross-reference the game data supports: a bundle is copied from a real gladiator record, so a class keeps
/// the stat set, skill set and item set it was authored with. Nothing in the files maps a class to its sets
/// (<c>classdefs.tok</c> carries only <c>SKILLUSENAME</c> for animations), so picking them separately would be
/// invention.
/// </para>
/// <para>
/// <see cref="Name"/> and <see cref="School"/> are not part of the bundle: a hero keeps its identity and its
/// school, and school membership is what puts a unit in a roster.
/// </para>
/// </remarks>
public sealed record GladiatorLoadout(
    string ClassName,
    int Outfit,
    string Affinity,
    int TintSet,
    int SkillSet,
    int StatSet,
    int ItemSet)
{
    /// <summary>Take the loadout half of a record.</summary>
    public static GladiatorLoadout From(GladiatorDto dto) => new(
        dto.Class, dto.Outfit, dto.Affinity, dto.TintSet, dto.SkillSet, dto.StatSet, dto.ItemSet);

    /// <summary>The patch that applies this loadout to an existing record.</summary>
    public GladiatorPatch ToPatch() => new()
    {
        Class = ClassName,
        Outfit = Outfit,
        Affinity = Affinity,
        TintSet = TintSet,
        SkillSet = SkillSet,
        StatSet = StatSet,
        ItemSet = ItemSet,
    };

    /// <summary>True when the record already carries this loadout.</summary>
    public bool Matches(GladiatorDto dto) =>
        dto.Class == ClassName && dto.Outfit == Outfit && dto.Affinity == Affinity
        && dto.TintSet == TintSet && dto.SkillSet == SkillSet && dto.StatSet == StatSet
        && dto.ItemSet == ItemSet;
}

/// <summary>The story heroes. Their classes are their own, so they are excluded from the general pool.</summary>
public static class StoryHeroes
{
    /// <summary>Hero names, in the order the game introduces them.</summary>
    public static readonly string[] Names = { "Valens", "Ursula", "Ludo", "Urlan", "Eiji", "Gwazi" };

    /// <summary>True when the name belongs to a hero.</summary>
    public static bool IsHero(string name) =>
        Names.Contains(name, StringComparer.Ordinal);
}

/// <summary>What one randomization run did.</summary>
/// <param name="Seed">The seed in use, for reproducing the run.</param>
/// <param name="Steps">Named steps that ran, in order.</param>
/// <param name="ChangedRecords">Records actually written across every step.</param>
/// <param name="ModifiedScripts">Campaign scripts the run edited, which therefore need compiling before packing.</param>
public sealed record RandomizerResult(
    string Seed,
    IReadOnlyList<string> Steps,
    int ChangedRecords,
    IReadOnlyList<string> ModifiedScripts);

/// <summary>
/// Runs a randomization pass over a project through the Core APIs.
/// </summary>
/// <remarks>
/// One <see cref="RandomizerRandom"/> per run, one batch write per file, and progress through
/// <see cref="IAppLog"/> so the GUI and (later) the MCP server observe the same run. The old implementation
/// seeded only two of its six steps and wrote files directly; this one owns no file I/O.
/// </remarks>
public sealed class RandomizerService
{
    private readonly IGladiatorsApi _gladiators;
    private readonly IStatSetsApi _statSets;
    private readonly IItemSetsApi _itemSets;
    private readonly ISkillSetsApi _skillSets;
    private readonly ILeaguesApi _leagues;
    private readonly IAppLog? _log;

    /// <summary>Create the service; every dependency is injectable for tests.</summary>
    public RandomizerService(
        IGladiatorsApi? gladiators = null,
        IStatSetsApi? statSets = null,
        IItemSetsApi? itemSets = null,
        ISkillSetsApi? skillSets = null,
        ILeaguesApi? leagues = null,
        IAppLog? log = null)
    {
        _gladiators = gladiators ?? new GladiatorsApi();
        _statSets = statSets ?? new StatSetsApi();
        _itemSets = itemSets ?? new ItemSetsApi();
        _skillSets = skillSets ?? new SkillSetsApi();
        _leagues = leagues ?? new LeaguesApi();
        _log = log;
    }

    /// <summary>Run the requested steps. Returns what happened, including the seed for reproduction.</summary>
    public RandomizerResult Randomize(string projectRoot, RandomizerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);

        var random = new RandomizerRandom(options.Seed);
        var steps = new List<string>();
        int changed = 0;

        // Runs must not stack. The first run pins the project's current state as the baseline; every later run
        // restores it first, so the same seed reproduces the run and a second pass is not applied on top of the
        // first. This happens before any step reads a file.
        if (options.ResetToBaseline)
        {
            var baseline = new RandomizerBaseline(_log, "randomizer");

            bool hadBaseline = baseline.Exists(projectRoot);
            if (hadBaseline) baseline.Restore(projectRoot);
            else baseline.Capture(projectRoot);

            steps.Add(hadBaseline ? "baseline-restored" : "baseline-captured");
        }

        using var scope = _log?.Scope("randomizer");

        _log?.Write(LogLevel.Info, $"Randomizer run started with seed '{random.SeedText}'.", "randomizer");

        var modifiedScripts = new List<string>();

        if (options.RandomizeHeroes)
        {
            int heroes = RandomizeHeroes(projectRoot, random, options);
            changed += heroes;
            steps.Add("heroes");
            _log?.Write(LogLevel.Success, $"Hero loadouts reassigned: {heroes} changed.", "randomizer");
        }

        // The campaign scripts branch on the original heroes: costume-change tests and the later removal of Ludo
        // and Gwazi. The old flow patched them on every run, not only when heroes were randomized, and the edits
        // have to be compiled or they never reach the game.
        {
            var patched = new ScriptPatcher().Apply(projectRoot, options.Hero);
            modifiedScripts.AddRange(patched.Modified);
            steps.Add("scripts");
            _log?.Write(LogLevel.Success,
                $"Campaign scripts patched: {patched.Modified.Count} file(s).", "randomizer");

            foreach (string missing in patched.Missing)
            {
                _log?.Write(LogLevel.Warning, $"Script not found, skipped: {missing}", "randomizer");
            }
        }

        if (options.RandomizeStatsets)
        {
            int restatted = RandomizeStatSetAssignments(projectRoot, random);
            changed += restatted;
            steps.Add("statsets");
            _log?.Write(LogLevel.Success, $"Stat sets reassigned: {restatted} changed.", "randomizer");
        }

        if (options.RandomizeItemsets)
        {
            int regeared = RandomizeItemSetAssignments(projectRoot, random);
            changed += regeared;
            steps.Add("itemsets");
            _log?.Write(LogLevel.Success, $"Item sets reassigned: {regeared} changed.", "randomizer");
        }
        if (options.RandomizeTeam)
        {
            int team = RandomizeTeam(projectRoot, options, random);
            changed += team;
            steps.Add("team");
            _log?.Write(LogLevel.Success, $"Team units appended: {team}.", "randomizer");
            ValidateRosters(projectRoot);
        }

        // Always runs: opening the player's team slots is what lets the randomized school fight at all, so it
        // cannot be tied to the enemy-randomization option. Enemy rows and CANDIE stay behind their own flags.
        {
            var encounters = new EncounterRandomizer(log: _log).Apply(
                projectRoot, options.RandomizeEnemies, options.PermaDeath);

            changed += encounters.FilesChanged;
            steps.Add("encounters");
            _log?.Write(LogLevel.Success,
                $"Encounters rewritten: {encounters.FilesChanged} file(s), " +
                $"{encounters.EnemiesRandomized} enemy slot(s).", "randomizer");
        }

        if (options.RemoveRecruits)
        {
            int files = _leagues.RemoveRecruits(projectRoot);
            changed += files;
            steps.Add("recruits");
            _log?.Write(LogLevel.Success, $"Recruit lines removed from {files} league file(s).", "randomizer");
        }


        _log?.Write(LogLevel.Info, $"Randomizer run finished: {changed} record(s) changed.", "randomizer");
        return new RandomizerResult(random.SeedText, steps, changed, modifiedScripts);
    }

    /// <summary>
    /// Write both campaign rosters from scratch: header (<c>NAME</c>, <c>HERO</c>, gold) followed by a block for
    /// every story hero. This is what the original randomizer did, and it is safe because a roster is only read
    /// when a new game starts — an in-progress run keeps its own <c>&lt;name&gt;Saved.tok</c>, written by the
    /// engine's <c>SchoolSave</c>, so rewriting a roster cannot disturb a game in progress.
    /// </summary>
    private int WriteRosters(
        string projectRoot, IReadOnlyDictionary<string, GladiatorLoadout> assigned, RandomizerOptions options,
        RandomizerRandom random)
    {
        if (assigned.Count == 0) return 0;

        var schools = new SchoolsApi();
        int gold = GoldFor(options, random);
        string hero = options.Hero == HeroChoice.Valens ? "Valens" : "Ursula";
        int written = 0;

        foreach (string roster in CampaignRosters)
        {
            var existing = schools.Get(projectRoot, roster);
            if (existing is null) continue;

            string name = !string.IsNullOrWhiteSpace(options.SchoolName)
                ? options.SchoolName!.Trim()
                : string.IsNullOrWhiteSpace(existing.Name) ? "Gladius" : existing.Name;

            var units = new List<SchoolUnitDto>(StoryHeroes.Names.Length);
            foreach (string heroName in StoryHeroes.Names)
            {
                if (!assigned.TryGetValue(heroName, out var loadout)) continue;
                units.Add(BuildHeroUnit(heroName, loadout, projectRoot, existing.Unit(heroName)));
            }

            schools.ReplaceRoster(projectRoot, roster, name, hero, gold, units);
            written++;
        }

        return written;
    }

    /// <summary>
    /// Starting gold, matching the original randomizer: the chosen amount, a random one, 75000 in the
    /// 40-gladiator mode and 15000 otherwise.
    /// </summary>
    private static int GoldFor(RandomizerOptions options, RandomizerRandom random) => options.Cash switch
    {
        CashMode.Fixed => options.CashAmount,
        CashMode.Random => random.NextInt(0, 1_000_000),
        _ => options.FortyGladiators ? 75_000 : 15_000,
    };

    /// <summary>
    /// A hero block built from its loadout. Level, experience, job points and CUSTOMIZE are carried over from the
    /// block the roster already had, so a hero keeps its progression and outfit.
    /// </summary>
    private SchoolUnitDto BuildHeroUnit(
        string hero, GladiatorLoadout loadout, string projectRoot, SchoolUnitDto? previous)
    {
        string level = previous?.Value("LEVEL") ?? "1";

        var fields = new List<SchoolFieldDto>
        {
            new("LEVEL", level),
            new("EXPERIENCE", previous?.Value("EXPERIENCE") ?? "0"),
            new("JOBPOINTS", previous?.Value("JOBPOINTS") ?? "5"),
            // Always the default look. CUSTOMIZE selects data/characters/<mesh>/<mesh>_varNN.ptx and asking for a
            // variant the class does not ship locks the game up; 99 of the 182 mesh folders ship no variants at
            // all, and a hero's class changes here, so the previous outfit cannot be trusted.
            new("CUSTOMIZE", "0, \"\""),
            new("CORESTATSCOMP2", BuildStatLine(projectRoot, loadout.StatSet)),
            new("ITEMSCOMP", BuildItemLine(projectRoot, loadout.ItemSet)),
        };

        int parsedLevel = int.TryParse(level, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
            ? value
            : 1;

        foreach (string skill in SkillNames(projectRoot, loadout.SkillSet, parsedLevel))
        {
            fields.Add(new SchoolFieldDto("SKILL", $"\"{skill}\""));
        }

        return new SchoolUnitDto { Name = hero, ClassName = loadout.ClassName, Fields = fields };
    }

    /// <summary>
    /// For each class, the ranged weapon subtype it cannot fight without, derived from the project's own records:
    /// when every level-1 weapon its gladiators carry is one ranged subtype (a bow or a javelin), that subtype is
    /// required. Vanilla makes this Archer, ArcherF and Amazon bows; a Peltast carries javelins and spears, so it
    /// is not constrained.
    /// </summary>
    private Dictionary<string, string> RequiredRangedWeapons(string projectRoot, ItemSetAffinityMap map) =>
        DeriveRequiredRangedWeapons(_gladiators.GetAll(projectRoot), map);

    /// <summary>
    /// The derivation itself, split out so it can be asserted without running a whole pass.
    /// </summary>
    internal static Dictionary<string, string> DeriveRequiredRangedWeapons(
        IReadOnlyList<GladiatorDto> records, ItemSetAffinityMap map)
    {
        var seenByClass = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var dto in records)
        {
            string? subtype = map.WeaponSubtypeOf(dto.ItemSet);
            if (subtype is null) continue;

            if (!seenByClass.TryGetValue(dto.Class, out var seen))
            {
                seenByClass[dto.Class] = seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            seen.Add(subtype);
        }

        var required = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (className, subtypes) in seenByClass)
        {
            if (subtypes.Count != 1) continue;

            string only = subtypes.First();
            if (ItemSetAffinityMap.IsRanged(only)) required[className] = only;
        }

        return required;
    }

    /// <summary>
    /// <c>CORESTATSCOMP2</c> is six numbers (CON, PWR, ACC, DEF, INT, MOVE); a short line makes the loader stop
    /// and silently drop the rest of the block. MOVE is written as 1, the way vanilla blocks do.
    /// </summary>
    private string BuildStatLine(string projectRoot, int statSet)
    {
        var row = _statSets.Get(projectRoot, statSet)?.Row(1);
        return row is null
            ? "0, 0, 0, 0, 0, 1"
            : string.Create(CultureInfo.InvariantCulture,
                $"{row.Con}, {row.Pow}, {row.Acc}, {row.Def}, {row.IniMov}, 1");
    }

    /// <summary>
    /// <c>ITEMSCOMP</c> is five quoted names in the order weapon, armor, shield, helmet, accessory; each slot
    /// takes the row of the set that covers level 1.
    /// </summary>
    private string BuildItemLine(string projectRoot, int itemSet)
    {
        var rows = _itemSets.Get(projectRoot, itemSet)?.Rows ?? Array.Empty<ItemSetRow>();
        var items = new List<string>(5);

        foreach (string slot in new[] { "Weapon", "Armor", "Shield", "Helmet", "Accessory" })
        {
            var candidates = rows
                .Where(row => row.Note is not null
                              && row.Note.Contains(slot, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            var chosen = candidates.FirstOrDefault(row => row.MinLevel <= 1 && row.MaxLevel >= 1)
                         ?? candidates.FirstOrDefault();

            items.Add(chosen?.ItemName ?? string.Empty);
        }

        return string.Join(", ", items.Select(item => $"\"{item}\""));
    }

    /// <summary>
    /// The skills a set grants a unit of the given level. A skill set is a list of level windows, so granting
    /// every row would hand a level-1 hero Combo IV and every shape-shift tier; the old code did exactly that.
    /// </summary>
    private IReadOnlyList<string> SkillNames(string projectRoot, int skillSet, int level) =>
        _skillSets.Get(projectRoot, skillSet)?.Rows
            .Where(row => row.MinLevel <= level && level <= row.MaxLevel)
            .Select(row => row.SkillName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToArray() ?? Array.Empty<string>();

    /// <summary>
    /// Give every gladiator a random stat set index — the same thing the old randomizer did, and what the user
    /// asked for: "each unit getting a completely random statset".
    /// </summary>
    /// <remarks>
    /// Only the index in <c>gladiators.txt</c> changes; the stat tables themselves are untouched. Heroes are
    /// included because they are ordinary records in the same table.
    /// </remarks>
    private int RandomizeStatSetAssignments(string projectRoot, RandomizerRandom random)
    {
        var catalog = UnitCatalogLoader.Load(projectRoot);
        if (catalog.StatSets <= 0)
        {
            _log?.Write(LogLevel.Warning, "No stat sets found; skipping the statset step.", "randomizer");
            return 0;
        }

        var patches = new List<(string Name, GladiatorPatch Patch)>();
        foreach (var dto in _gladiators.GetAll(projectRoot))
        {
            int next = random.NextInt(0, catalog.StatSets);
            if (next == dto.StatSet) continue;

            patches.Add((dto.Name, new GladiatorPatch { StatSet = next }));
        }

        return patches.Count == 0 ? 0 : _gladiators.ApplyMany(projectRoot, patches);
    }

    /// <summary>The campaign rosters the team step appends to, matching the old randomizer.</summary>
    private static readonly string[] CampaignRosters =
    {
        "data/school/valensimperia.tok",
        "data/school/ursulanordagh.tok",
    };

    /// <summary>
    /// Validate the rosters just written. A block the engine cannot fully parse (a short CORESTATSCOMP2, an
    /// item count the loader rejects) silently loses its gear and skills, so surfacing the warnings in the run
    /// log is the difference between finding that here and finding it in game.
    /// </summary>
    private void ValidateRosters(string projectRoot)
    {
        var schools = new SchoolsApi();
        foreach (string roster in CampaignRosters)
        {
            foreach (var issue in schools.Validate(projectRoot, roster))
            {
                if (issue.Severity == SchoolValidationSeverity.Info) continue;
                _log?.Write(LogLevel.Warning, $"{roster}: {issue.Unit} {issue.Key}: {issue.Message}", "randomizer");
            }
        }
    }

    /// <summary>
    /// Build a randomized starting roster and append it to both campaign files, copying the old
    /// <c>RandomizeTeam</c>: names are <c>&lt;gladiator&gt;_random</c>, the class pool is every class that has a
    /// gladiator record, and each block carries that class's stat, item and skill set contents.
    /// </summary>
    /// <remarks>
    /// With <see cref="RandomizerOptions.WeightedClasses"/> the selection takes one of each base class first and
    /// only then duplicates from what was already picked, so a team is varied before it repeats.
    /// </remarks>
    private int RandomizeTeam(string projectRoot, RandomizerOptions options, RandomizerRandom random)
    {
        int teamSize = options.FortyGladiators ? 34 : 14;

        var records = _gladiators.GetAll(projectRoot);
        if (records.Count < teamSize) return 0;

        var blacklist = ClassEligibility.BlacklistFor(options.Version);
        var classes = records
            .Select(dto => dto.Class)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Where(c => ClassEligibility.IsEligible(c, options.Version, blacklist))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(c => c, StringComparer.Ordinal)
            .ToList();

        if (classes.Count == 0) return 0;

        var memberNames = random.Shuffled(records.Select(dto => dto.Name))
            .Take(teamSize)
            .Select(name => name + "_random")
            .ToList();

        List<string> chosenClasses = options.WeightedClasses
            ? WeightedClasses(classes, teamSize, random)
            : Enumerable.Range(0, teamSize).Select(_ => random.Pick(classes)).ToList();

        var units = new List<SchoolUnitDto>(teamSize);
        for (int i = 0; i < teamSize; i++)
        {
            string className = chosenClasses[i];
            var source = random.Pick(records.Where(dto => dto.Class == className).ToList());
            units.Add(BuildTeamUnit(memberNames[i], className, source, projectRoot));
        }

        int unitCount = units.Count;
        var schools = new SchoolsApi();
        int added = 0;
        foreach (string roster in CampaignRosters)
        {
            added += schools.AddUnits(projectRoot, roster, units);
        }

        // Names are <gladiator>_random and duplicates are refused, so a second run on the same project adds
        // nothing. Say so instead of silently doing less than the options asked for.
        if (added == 0 && unitCount > 0)
        {
            _log?.Write(LogLevel.Warning,
                "Team units already exist in both rosters; nothing was added. Unpack again for a clean run.",
                "randomizer");
        }

        return added;
    }

    /// <summary>One <c>CREATEUNIT</c> block built from the class's shipped sets.</summary>
    private SchoolUnitDto BuildTeamUnit(
        string name, string className, GladiatorDto source, string projectRoot)
    {
        // CORESTATSCOMP2 is six values (CON, PWR, ACC, DEF, INT, MOVE). Writing five makes SchoolLoad stop
        // parsing the block at the missing value - it sets its error flag and leaves the token loop - so the
        // ITEMSCOMP and SKILL lines after it are never read and the unit arrives with no gear. Verified in the
        // DOL: case 0x16 reads six ints into offsets 0x70-0x88 and copies MOVE from INT.
        var statRow = _statSets.Get(projectRoot, source.StatSet)?.Row(1);
        string stats = statRow is null
            ? "0, 0, 0, 0, 0, 0"
            : string.Create(CultureInfo.InvariantCulture,
                $"{statRow.Con}, {statRow.Pow}, {statRow.Acc}, {statRow.Def}, {statRow.IniMov}, 1");

        // ITEMSCOMP is exactly five slots (weapon, armor, shield, helmet, accessory). An item set lists level
        // windows per slot, so take the row that covers level 1 - writing every row would malform the block.
        var rows = _itemSets.Get(projectRoot, source.ItemSet)?.Rows ?? Array.Empty<ItemSetRow>();
        var items = new List<string>(5);
        foreach (string slot in new[] { "Weapon", "Armor", "Shield", "Helmet", "Accessory" })
        {
            var candidates = rows
                .Where(row => row.Note is not null
                              && row.Note.Contains(slot, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            var chosen = candidates.FirstOrDefault(row => row.MinLevel <= 1 && row.MaxLevel >= 1)
                         ?? candidates.FirstOrDefault();

            items.Add(chosen?.ItemName ?? string.Empty);
        }

        // Defensive: if no slot matched, fall back to the set's first row so the unit is not left bare.
        if (items.All(item => item.Length == 0))
        {
            var first = rows.FirstOrDefault();
            if (first is not null) items[0] = first.ItemName;
        }

        string itemList = string.Join(", ", items.Select(item => $"\"{item}\""));

        // Team members are created at level 1, so only the skills a level-1 unit may actually learn are granted.
        var skills = SkillNames(projectRoot, source.SkillSet, 1);

        var fields = new List<SchoolFieldDto>
        {
            new("LEVEL", "1"),
            new("EXPERIENCE", "0"),
            new("JOBPOINTS", "8"),
            new("CUSTOMIZE", "0, \"\""),
            new("CORESTATSCOMP2", stats),
            new("ITEMSCOMP", itemList),
        };

        foreach (string skill in skills) fields.Add(new SchoolFieldDto("SKILL", $"\"{skill}\""));

        return new SchoolUnitDto { Name = name, ClassName = className, Fields = fields };
    }

    /// <summary>
    /// Pick classes with diversity first: one of each base class (variants such as <c>BanditAF</c> and
    /// <c>BanditA</c> collapse to <c>BanditA</c>'s base), then pad by repeating already-selected classes.
    /// </summary>
    private static List<string> WeightedClasses(IReadOnlyList<string> classes, int teamSize, RandomizerRandom random)
    {
        var available = random.Shuffled(classes);
        var selected = new List<string>();

        foreach (string candidate in available)
        {
            if (selected.Count >= teamSize) break;
            if (!selected.Any(chosen => BaseClass(chosen) == BaseClass(candidate))) selected.Add(candidate);
        }

        while (selected.Count < teamSize) selected.Add(selected[random.NextInt(0, selected.Count)]);

        random.Shuffle(selected);
        return selected;
    }

    /// <summary>
    /// The base class behind a variant name, using the same cascade the old randomizer used: every undead melee
    /// variant collapses to <c>UndeadMelee</c>, then regional and A/B/F suffixes are stripped, then a lone F.
    /// </summary>
    internal static string BaseClass(string className)
    {
        if (string.IsNullOrWhiteSpace(className)) return string.Empty;

        var undead = System.Text.RegularExpressions.Regex.Match(
            className, "^(.+?)(?:Exp|Imp|Nor|Ste)[AB]F?$", System.Text.RegularExpressions.RegexOptions.None,
            TimeSpan.FromSeconds(1));
        if (undead.Success && className.StartsWith("UndeadMelee", StringComparison.Ordinal))
        {
            return "UndeadMelee";
        }

        var regional = System.Text.RegularExpressions.Regex.Match(
            className, "^(.+?)(?:Imp|Nor|Ste|Exp|[AB])F?$");
        if (regional.Success) return regional.Groups[1].Value;

        var gender = System.Text.RegularExpressions.Regex.Match(className, "^(.+)F$");
        if (gender.Success) return gender.Groups[1].Value;

        return className;
    }

    /// <summary>
    /// Give each gladiator an item set whose weapons grant its own affinity, which is what the old
    /// <c>RandomizeItemsets</c> did: a Fire unit needs Fire-affinity gear (or the universal Light pool) or it
    /// cannot use its affinity skills.
    /// </summary>
    private int RandomizeItemSetAssignments(string projectRoot, RandomizerRandom random)
    {
        var map = ItemSetAffinityMap.Build(projectRoot);
        var required = RequiredRangedWeapons(projectRoot, map);
        if (map.Count == 0)
        {
            _log?.Write(LogLevel.Warning, "No item sets found; skipping the itemset step.", "randomizer");
            return 0;
        }

        var patches = new List<(string Name, GladiatorPatch Patch)>();
        foreach (var dto in _gladiators.GetAll(projectRoot))
        {
            // A bow class handed a sword passes every turn: the engine's attack follows the equipped weapon. So
            // a class that carries exactly one ranged subtype in the corpus only receives sets with that weapon.
            required.TryGetValue(dto.Class, out string? requiredSubtype);

            // A usable weapon matters more than affinity: try the affinity pool, then any set with the required
            // subtype, and only then fall back to the affinity pool with a warning.
            var candidates = map.CandidatesFor(dto.Affinity, requiredSubtype);
            if (candidates.Count == 0 && requiredSubtype is not null)
            {
                candidates = map.CandidatesFor(null, requiredSubtype);
            }

            if (candidates.Count == 0 && requiredSubtype is not null)
            {
                candidates = map.CandidatesFor(dto.Affinity);
                _log?.Write(LogLevel.Warning,
                    $"{dto.Name} ({dto.Class}) found no {requiredSubtype} set; falling back to any set, so it may " +
                    "not be able to attack.", "randomizer");
            }

            if (candidates.Count == 0) continue;

            int next = random.Pick(candidates);
            if (next == dto.ItemSet) continue;

            patches.Add((dto.Name, new GladiatorPatch { ItemSet = next }));
        }

        return patches.Count == 0 ? 0 : _gladiators.ApplyMany(projectRoot, patches);
    }

    /// <summary>
    /// Give each story hero the loadout of another, randomly chosen gladiator. The hero keeps its name and
    /// school; the bundle keeps the class consistent with its stats, skills and gear because it came from a
    /// real record.
    /// </summary>
    private int RandomizeHeroes(string projectRoot, RandomizerRandom random, RandomizerOptions options)
    {
        var records = _gladiators.GetAll(projectRoot);

        // The pool is every record except the classes this game version does not let a player use (bosses and
        // summon creatures). Hero classes are deliberately included: a unit may wear one as long as the scripts
        // are recompiled, which the build step does.
        var blacklist = ClassEligibility.BlacklistFor(options.Version);
        var pool = records
            .Where(dto => ClassEligibility.IsEligible(dto.Class, options.Version, blacklist))
            .Select(GladiatorLoadout.From)
            .ToList();

        if (pool.Count == 0) return 0;

        // One loadout per class, and the classes are dealt out shuffled so the six heroes never share one - the
        // original took each class off a shuffled list the same way.
        var classes = pool
            .GroupBy(loadout => loadout.ClassName, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToList();
        random.Shuffle(classes);

        var patches = new List<(string Name, GladiatorPatch Patch)>();
        var assigned = new Dictionary<string, GladiatorLoadout>(StringComparer.Ordinal);
        int dealt = 0;

        foreach (string hero in StoryHeroes.Names)
        {
            var current = records.FirstOrDefault(dto => dto.Name == hero);
            if (current is null) continue;

            // classes[dealt] is an IGrouping, not a list, so materialise it before picking from it.
            var loadout = dealt < classes.Count
                ? random.Pick(classes[dealt++].ToList())
                : random.Pick(pool);
            assigned[hero] = loadout;
            if (loadout.Matches(current)) continue;

            patches.Add((hero, loadout.ToPatch()));

        }

        int changed = patches.Count == 0 ? 0 : _gladiators.ApplyMany(projectRoot, patches);

        // The gladiator table is not what the campaign starts from: the starting units are the CREATEUNIT blocks
        // in the roster files, which spell out class, stats, items and skills. A hero whose table entry changed
        // but whose block did not would still walk into the first bout with the vanilla kit.
        changed += WriteRosters(projectRoot, assigned, options, random);

        return changed;
    }
}
