namespace ModdingGUI.Core.Units;

using System;
using System.Collections.Generic;
using System.Linq;
using ModdingGUI.Core.Api;

/// <summary>
/// A unit's job-point position: what its class and level award, what its granted skills consume, and what is left.
/// </summary>
/// <param name="Level">The unit's level (clamped to at least 1).</param>
/// <param name="Awarded">Job points earned by reaching that level.</param>
/// <param name="Spent">Job points the granted skills cost.</param>
/// <param name="Remaining">What is left to spend; the value written to <c>JOBPOINTS</c>.</param>
/// <param name="Overridden">True when the caller supplied the remaining value instead of the rule.</param>
public sealed record JobPointBudget(int Level, int Awarded, int Spent, int Remaining, bool Overridden = false)
{
    /// <summary>True when the granted skills cost more than the level awards.</summary>
    public bool OverBudget => Spent > Awarded && !Overridden;
}

/// <summary>
/// Job points: what a class awards per level, what skills cost, and the balance left for a unit.
/// </summary>
/// <remarks>
/// <para>
/// <c>classdefs.tok</c> carries <c>LEVELUPJPGIVEN</c>: the job points awarded on arriving at levels 1 to 5, in order.
/// Levels beyond the listed ones continue with the last listed award, which is why vanilla's common profile
/// <c>5, 10, 10, 15, 15</c> (106 of 128 classes) pays 15 per level afterwards. Props and creatures carry
/// <c>0, 0, 0, 0, 0</c> and therefore never award anything.
/// </para>
/// <para>
/// A skill's price is <c>SKILLJOBPOINTCOST</c> in <c>skills.tok</c>.
/// </para>
/// <para>
/// Note that shipped rosters do <em>not</em> follow this: 40 of 46 level-1 vanilla units carry
/// <c>JOBPOINTS: 0</c> while still granting skills, and trainer NPCs carry thousands. The rule here is a design
/// budget for units built in the tool - it makes a level-N unit comparable to one that levelled up to N - not an
/// attempt to reproduce hand-authored content.
/// </para>
/// </remarks>
public static class JobPoints
{
    /// <summary>
    /// The value vanilla uses for levels past the fifth (the common profile ends in 15). Kept for documentation;
    /// the rule is <em>the last listed award continues</em>, so a class whose profile ends in 20 keeps awarding 20.
    /// </summary>
    public const int VanillaContinuationAward = 15;

    /// <summary>How many awards the vanilla profiles list; further levels continue with the last one.</summary>
    public const int VanillaAwardCount = 5;

    /// <summary>
    /// Parse a <c>LEVELUPJPGIVEN</c> value such as <c>"5, 10, 10, 15, 15"</c>. Missing or malformed entries read as 0.
    /// </summary>
    public static IReadOnlyList<int> ParseAwards(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue)) return Array.Empty<int>();

        var numbers = new List<int>();
        foreach (string part in rawValue!.Split(new[] { ',', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries))
        {
            numbers.Add(int.TryParse(part, out int value) ? value : 0);
        }

        return numbers;
    }

    /// <summary>The job points a class's awards give a unit that reaches <paramref name="level"/>.</summary>
    public static int Awarded(IReadOnlyList<int> awards, int level)
    {
        ArgumentNullException.ThrowIfNull(awards);

        if (awards.Count == 0) return 0;

        int target = Math.Max(1, level);
        int total = 0;

        // Levels the profile lists use their own value; later levels continue with the last listed award. That keeps
        // the vanilla convention (a profile ending in 15 pays 15 per level) without inventing points for a class whose
        // profile is all zeros, such as props and creatures.
        int continuation = awards[^1];

        for (int reached = 1; reached <= target; reached++)
        {
            total += reached <= awards.Count ? awards[reached - 1] : continuation;
        }

        return total;
    }

    /// <summary>The <c>SKILLJOBPOINTCOST</c> of a skill, or 0 when it has none.</summary>
    public static int CostOf(SkillDto? skill)
    {
        if (skill is null) return 0;

        string? raw = skill.Fields
            .FirstOrDefault(field => field.FieldName.Equals("SKILLJOBPOINTCOST", StringComparison.OrdinalIgnoreCase))
            ?.RawValue;

        return int.TryParse(raw, out int cost) ? cost : 0;
    }

    /// <summary>Total cost of the given skills.</summary>
    public static int CostOf(IEnumerable<SkillDto?> skills) => skills?.Sum(CostOf) ?? 0;

    /// <summary>
    /// The balance for a unit: awards from its class profile and level, minus what its skills cost.
    /// </summary>
    /// <param name="awards">The class's <c>LEVELUPJPGIVEN</c> values.</param>
    /// <param name="level">Unit level.</param>
    /// <param name="skillCosts">Cost of each granted skill.</param>
    /// <param name="overrideRemaining">Designer-supplied value that replaces the rule, if any.</param>
    public static JobPointBudget Compute(
        IReadOnlyList<int> awards,
        int level,
        IEnumerable<int>? skillCosts = null,
        int? overrideRemaining = null)
    {
        int target = Math.Max(1, level);
        int awarded = Awarded(awards, target);
        int spent = skillCosts?.Sum() ?? 0;

        return overrideRemaining is int supplied
            ? new JobPointBudget(target, awarded, spent, Math.Max(0, supplied), Overridden: true)
            : new JobPointBudget(target, awarded, spent, Math.Max(0, awarded - spent));
    }

    /// <summary>Convenience: the budget for a class block and a set of skills.</summary>
    public static JobPointBudget Compute(
        ClassDefDto? classDef,
        int level,
        IEnumerable<SkillDto?>? skills = null,
        int? overrideRemaining = null)
    {
        IReadOnlyList<int> awards = ParseAwards(classDef?.Value("LEVELUPJPGIVEN"));
        IEnumerable<int> costs = skills?.Select(CostOf) ?? Enumerable.Empty<int>();
        return Compute(awards, level, costs, overrideRemaining);
    }
}
