namespace ModdingGUI.Core.Units;

using System;
using System.Collections.Generic;
using System.Linq;
using ModdingGUI.Core.Api;

/// <summary>
/// Which skills a class can be given, following the engine's learn check (see <c>dol/skill_rules.json</c>).
/// </summary>
/// <remarks>
/// <para>
/// The engine builds a 64-bit mask from a skill's repeating <c>SKILLUSECLASS</c> lines and intersects it with the
/// unit's own class mask (<c>FUN_800f95d0</c>). A skill whose mask is empty has no class restriction and is learnable
/// by anyone (<c>FUN_800f95a0</c>).
/// </para>
/// <para>
/// So the tool rule is the same thing at name level: a skill is offered when it lists no classes, or lists the unit's
/// class. <c>SKILLLEVEL</c> is not part of the engine's learn check, so it is reported for guidance and not enforced.
/// </para>
/// </remarks>
public static class LearnableSkills
{
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

    /// <summary>True when the class may learn the skill.</summary>
    public static bool IsLearnableBy(SkillDto skill, string? className)
    {
        if (string.IsNullOrWhiteSpace(className)) return false;

        var classes = ClassesOf(skill);
        return classes.Count == 0
               || classes.Any(name => name.Equals(className, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Every skill the class may learn, ordered by level then name.</summary>
    /// <param name="skills">All skills, usually <c>ISkillsApi.GetAll</c>.</param>
    /// <param name="className">The unit's class.</param>
    public static IReadOnlyList<SkillDto> For(IEnumerable<SkillDto> skills, string? className)
    {
        ArgumentNullException.ThrowIfNull(skills);

        return skills
            .Where(skill => skill is not null && IsLearnableBy(skill, className))
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
}
