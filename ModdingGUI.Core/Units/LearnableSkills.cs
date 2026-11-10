namespace ModdingGUI.Core.Units;

using System;
using System.Collections.Generic;
using System.Linq;
using ModdingGUI.Core.Api;

/// <summary>What a skill picker may offer, and how it treats the odd records.</summary>
/// <remarks>
/// The engine's learn check only asks two things: is the skill flagged unlearnable, and does its class mask match
/// (see <c>dol/skill_rules.json</c>). The shipped table carries two more distinctions that matter to a builder and
/// are not visible in that check, so they are modelled here:
/// <list type="bullet">
/// <item><description>
/// <b>Subskills.</b> A combo's later hits are separate records referenced by their parent's <c>SKILLSUBSKILL</c>
/// (<c>Amazon Combo I</c> → <c>Amazon ComboChain I</c> → <c>II</c> → <c>III</c>). They are bought with the root, carry
/// no job-point price, and are never taught on their own.
/// </description></item>
/// <item><description>
/// <b>Category.</b> <c>SKILLCREATE</c>'s second argument separates the skills a designer places from the ones the
/// game places for them: <c>Innate</c> (a class's built-in passives, e.g. <c>Gwazi OffBalance</c>) and
/// <c>Command</c> (<c>Pass</c>, <c>Continue</c>).
/// </description></item>
/// </list>
/// </remarks>
public sealed record LearnableSkillOptions
{
    /// <summary>
    /// Offer skills with no <c>SKILLUSECLASS</c> line. The engine lets any class learn those, but most are engine
    /// records (<c>Crowd Status I-IV</c>) or hero innates, so they are opt-in.
    /// </summary>
    public bool IncludeAnyClass { get; init; }

    /// <summary>Offer <c>Innate</c>/<c>Command</c> skills, which a roster gets without being taught.</summary>
    public bool IncludeGrantedByDefault { get; init; }

    /// <summary>The picker's default: the class's own teachable skills, without combo hits.</summary>
    public static LearnableSkillOptions Default { get; } = new();
}

/// <summary>
/// Which skills a class can be given, following the engine's learn check (see <c>dol/skill_rules.json</c>) and the
/// table's own structure.
/// </summary>
/// <remarks>
/// <para>
/// The engine builds a 64-bit mask from a skill's repeating <c>SKILLUSECLASS</c> lines and intersects it with the
/// unit's own class mask (<c>FUN_800f95d0</c>). A skill whose mask is empty has no class restriction and is learnable
/// by anyone (<c>FUN_800f95a0</c>) — <see cref="IsLearnableBy"/> is that rule, unchanged.
/// </para>
/// <para>
/// <see cref="For"/> is the <em>builder's</em> rule, which is narrower on purpose: it drops the records the game never
/// teaches. See <see cref="LearnableSkillOptions"/> for why.
/// </para>
/// </remarks>
public static class LearnableSkills
{
    private const string SubskillField = "SKILLSUBSKILL";
    private const string CategoryField = "SKILLCREATE";

    /// <summary>The classes a skill grants itself to.</summary>
    public static IReadOnlyList<string> ClassesOf(SkillDto skill)
    {
        ArgumentNullException.ThrowIfNull(skill);

        return skill.Fields
            .Where(field => field.FieldName.Equals("SKILLUSECLASS", StringComparison.OrdinalIgnoreCase))
            .Select(field => field.RawValue.Trim().Trim('"'))
            .Where(value => value.Length > 0)
            .ToArray();
    }

    /// <summary>
    /// <c>SKILLCREATE</c>'s second argument — <c>Attack</c>, <c>Support</c>, <c>Secondary</c>, <c>Innate</c> or
    /// <c>Command</c> — or an empty string when the line is missing.
    /// </summary>
    public static string CategoryOf(SkillDto skill)
    {
        ArgumentNullException.ThrowIfNull(skill);

        string? raw = skill.Fields
            .FirstOrDefault(field => field.FieldName.Equals(CategoryField, StringComparison.OrdinalIgnoreCase))
            ?.RawValue;

        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

        // The raw value is `"Name", "Category", "SubCategory"`.
        var parts = SplitArguments(raw);
        return parts.Count >= 2 ? parts[1].Trim('"') : string.Empty;
    }

    /// <summary>True when the table marks the skill as granted by the game rather than taught.</summary>
    public static bool IsGrantedByDefault(SkillDto skill)
    {
        string category = CategoryOf(skill);
        return category.Equals("Innate", StringComparison.OrdinalIgnoreCase)
               || category.Equals("Command", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Every skill name that another skill names as its <c>SKILLSUBSKILL</c> — the later hits of a combo, which are
    /// granted by their root and never bought.
    /// </summary>
    public static IReadOnlySet<string> SubskillTargets(IEnumerable<SkillDto> skills)
    {
        ArgumentNullException.ThrowIfNull(skills);

        var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (SkillDto skill in skills)
        {
            foreach (string target in SubskillNamesOf(skill)) targets.Add(target);
        }

        return targets;
    }

    /// <summary>True when the skill is a combo hit rather than a skill a designer places.</summary>
    public static bool IsSubskill(SkillDto skill, IReadOnlySet<string> targets)
    {
        ArgumentNullException.ThrowIfNull(skill);
        ArgumentNullException.ThrowIfNull(targets);

        return targets.Contains(skill.Name);
    }

    /// <summary>The skill's own <c>SKILLSUBSKILL</c> targets, in order, unquoted.</summary>
    public static IReadOnlyList<string> SubskillNamesOf(SkillDto skill)
    {
        ArgumentNullException.ThrowIfNull(skill);

        return skill.Fields
            .Where(field => field.FieldName.Equals(SubskillField, StringComparison.OrdinalIgnoreCase))
            .SelectMany(field => SplitArguments(field.RawValue))
            .Select(name => name.Trim().Trim('"'))
            .Where(name => name.Length > 0)
            .ToArray();
    }

    /// <summary>
    /// The hits of a combo, starting at the skill the designer buys: the root, then each <c>SKILLSUBSKILL</c> target
    /// until the chain ends. Empty for a skill that is not a combo root.
    /// </summary>
    /// <remarks>
    /// The chain records are shared: <c>Amazon Combo IV</c> links the same <c>ComboChain I</c> the first combo does,
    /// and it is <c>SKILLMETER</c>'s third value that says how many hits a given combo actually uses (its meter reads
    /// <c>"ChainSlow", 0, 5</c>). So the walk stops at the declared count — the same bound the game applies, and the
    /// one UsusBot's combo reader uses.
    /// </remarks>
    public static IReadOnlyList<SkillDto> ChainOf(SkillDto root, IEnumerable<SkillDto> skills)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(skills);

        var byName = skills
            .Where(skill => skill is not null)
            .GroupBy(skill => skill.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        int declared = DeclaredComboHits(root);
        var chain = new List<SkillDto> { root };
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { root.Name };

        SkillDto current = root;
        while (declared <= 0 || chain.Count < declared)
        {
            string? next = SubskillNamesOf(current).FirstOrDefault();
            if (next is null || !byName.TryGetValue(next, out SkillDto? hit)) break;
            if (!seen.Add(hit.Name)) break;   // a cycle would be a data bug; stop rather than spin

            chain.Add(hit);
            current = hit;
        }

        return chain;
    }

    /// <summary>
    /// How many hits the skill's <c>SKILLMETER</c> declares — <c>"ChainSlow", 0, 2</c> means two hits — or 0 when the
    /// meter is not a chain.
    /// </summary>
    public static int DeclaredComboHits(SkillDto skill)
    {
        ArgumentNullException.ThrowIfNull(skill);

        string? meter = skill.Fields
            .FirstOrDefault(field => field.FieldName.Equals("SKILLMETER", StringComparison.OrdinalIgnoreCase))
            ?.RawValue;

        if (string.IsNullOrWhiteSpace(meter)) return 0;

        var parts = SplitArguments(meter);
        return parts.Count >= 3 && int.TryParse(parts[2].Trim(' ', '"'), out int hits) && hits > 0 ? hits : 0;
    }

    /// <summary>True when the skill starts a combo chain.</summary>
    public static bool IsComboRoot(SkillDto skill) => SubskillNamesOf(skill).Count > 0;

    /// <summary>True when the class may learn the skill, by the engine's rule (no class list means anyone).</summary>
    public static bool IsLearnableBy(SkillDto skill, string? className)
    {
        if (string.IsNullOrWhiteSpace(className)) return false;

        var classes = ClassesOf(skill);
        return classes.Count == 0
               || classes.Any(name => name.Equals(className, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Every skill the builder should offer for the class: its own teachable skills, in level then name order.
    /// </summary>
    /// <param name="skills">All skills, usually <c>ISkillsApi.GetAll</c>.</param>
    /// <param name="className">The unit's class.</param>
    /// <param name="options">Whether to include any-class and game-granted records.</param>
    public static IReadOnlyList<SkillDto> For(
        IEnumerable<SkillDto> skills, string? className, LearnableSkillOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(skills);

        if (string.IsNullOrWhiteSpace(className)) return Array.Empty<SkillDto>();

        var settings = options ?? LearnableSkillOptions.Default;
        var all = skills.Where(skill => skill is not null).ToList();
        var targets = SubskillTargets(all);

        return all
            .Where(skill => !IsSubskill(skill, targets))
            .Where(skill => settings.IncludeGrantedByDefault || !IsGrantedByDefault(skill))
            .Where(skill => settings.IncludeAnyClass
                ? IsLearnableBy(skill, className)
                : ClassesOf(skill).Any(name => name.Equals(className, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(LevelOf)
            .ThenBy(skill => skill.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>The skill's <c>SKILLLEVEL</c>, or 0 when it has none.</summary>
    public static int LevelOf(SkillDto skill)
    {
        ArgumentNullException.ThrowIfNull(skill);

        string? raw = skill.Fields
            .FirstOrDefault(field => field.FieldName.Equals("SKILLLEVEL", StringComparison.OrdinalIgnoreCase))
            ?.RawValue;

        return int.TryParse(raw, out int level) ? level : 0;
    }

    /// <summary>
    /// Split a raw value into its arguments, honouring quotes: <c>"Name", "Type", 3</c> becomes three parts with the
    /// quotes kept so callers can trim what they need.
    /// </summary>
    private static List<string> SplitArguments(string raw)
    {
        var parts = new List<string>();
        if (string.IsNullOrWhiteSpace(raw)) return parts;

        var current = new System.Text.StringBuilder();
        bool quoted = false;

        foreach (char c in raw)
        {
            if (c == '"') quoted = !quoted;

            if (c == ',' && !quoted)
            {
                parts.Add(current.ToString().Trim());
                current.Clear();
                continue;
            }

            current.Append(c);
        }

        if (current.Length > 0) parts.Add(current.ToString().Trim());
        return parts;
    }
}
