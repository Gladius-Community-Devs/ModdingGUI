namespace ModdingGUI.Core.Skills;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ModdingGUI.Core.Api;
using ModdingGUI.Core.Tok.Values;

/// <summary>
/// Advisory validation for a single <c>skills.tok</c> record.
/// </summary>
/// <remarks>
/// <para>
/// Rules come from two places: the record itself (<c>SKILLCREATE</c> positionals,
/// token shapes) and <see cref="SkillsCatalog"/>, which carries the enum sets,
/// cardinality (<c>MULTI</c>), co-presence (<c>LINK</c>) and prerequisite
/// (<c>REQUIRES</c>) rules generated from <c>skills_field_reference.txt</c>.
/// </para>
/// <para>
/// Everything here is advisory by contract (PLAN rule 7): the GUI writes
/// regardless, MCP may gate on it via <see cref="SkillValidationMode.Reject"/>.
/// Catalog-derived findings are <see cref="SkillValidationSeverity.Info"/> because
/// a shipped catalog cannot know a modder's new values; structural findings are
/// <see cref="SkillValidationSeverity.Warning"/>.
/// </para>
/// </remarks>
public static class SkillsValidator
{
    /// <summary>Fields whose integer value must resolve in <c>lookuptext_eng.txt</c>.</summary>
    private static readonly string[] LookupIdFields = { "SKILLDISPLAYNAMEID", "SKILLDESCRIPTIONID" };

    /// <summary>
    /// Validate <paramref name="skill"/> against <paramref name="catalog"/>, optionally
    /// checking project files through <paramref name="context"/>.
    /// </summary>
    /// <param name="skill">The record to check (as returned by <see cref="ISkillsApi.Get"/>).</param>
    /// <param name="catalog">Field/value/rule catalog. <see cref="SkillsCatalog.Empty"/> disables catalog rules.</param>
    /// <param name="context">Project data for external references; may be <c>null</c>.</param>
    /// <param name="options">Rule switches; defaults to <see cref="SkillValidationOptions.Default"/>.</param>
    /// <returns>Findings in deterministic order; empty when the record is clean.</returns>
    public static IReadOnlyList<SkillValidationIssue> Validate(
        SkillDto? skill,
        SkillsCatalog? catalog = null,
        SkillsContext? context = null,
        SkillValidationOptions? options = null)
    {
        var issues = new List<SkillValidationIssue>();
        if (skill is null) return issues;

        catalog ??= SkillsCatalog.Empty;
        options ??= SkillValidationOptions.Default;

        CheckPositionals(skill, issues);

        // Group body fields by normalized name so repetition and links can be judged.
        var byField = new Dictionary<string, List<SkillFieldEntry>>(StringComparer.OrdinalIgnoreCase);
        var order = new List<string>();
        foreach (var entry in skill.Fields)
        {
            if (entry is null || string.IsNullOrWhiteSpace(entry.FieldName)) continue;
            string normalized = SkillsCatalog.NormalizeFieldName(entry.FieldName.Trim());
            if (!byField.TryGetValue(normalized, out var list))
            {
                list = new List<SkillFieldEntry>();
                byField[normalized] = list;
                order.Add(normalized);
            }
            list.Add(entry);
        }

        var present = new HashSet<string>(byField.Keys, StringComparer.OrdinalIgnoreCase);

        foreach (string fieldName in order)
        {
            var entries = byField[fieldName];
            var spec = catalog.Find(fieldName);

            if (spec is null)
            {
                issues.Add(new SkillValidationIssue
                {
                    Severity = SkillValidationSeverity.Info,
                    Code = SkillValidationCodes.UnknownField,
                    Field = fieldName,
                    Message = "field is not in the catalog (new or modded field?)",
                });
                continue;
            }

            // Engine truth wins over the corpus: a field the engine never matches is
            // silently dropped in game, however often modded data uses it.
            if (spec.EngineKnown == false)
            {
                issues.Add(new SkillValidationIssue
                {
                    Severity = SkillValidationSeverity.Warning,
                    Code = SkillValidationCodes.FieldNotInEngine,
                    Field = fieldName,
                    Message = "field name is not in the engine's field table — " +
                              "the game ignores this line",
                });
            }

            if (entries.Count > 1 || spec.Accumulates)
                CheckRepetition(fieldName, spec, entries.Count, issues);

            foreach (var entry in entries)
                CheckTokens(fieldName, spec, entry, issues);

            CheckLinks(fieldName, spec, present, issues);
            CheckRequirements(fieldName, spec, byField, issues);
        }

        if (options.CheckExternalReferences && context is not null && context.HasLookupText)
            CheckLookupIds(byField, context, issues);

        return issues;
    }

    private static void CheckPositionals(SkillDto skill, List<SkillValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(skill.Name))
            Add(issues, SkillValidationSeverity.Warning, SkillValidationCodes.MissingPositional,
                "SKILLCREATE", "internal skill name (first positional) is empty");

        if (string.IsNullOrWhiteSpace(skill.SkillType))
            Add(issues, SkillValidationSeverity.Warning, SkillValidationCodes.MissingPositional,
                "SKILLCREATE", "skill type (second positional) is empty");

        if (string.IsNullOrWhiteSpace(skill.SkillSubType))
            Add(issues, SkillValidationSeverity.Warning, SkillValidationCodes.MissingPositional,
                "SKILLCREATE", "skill sub-type (third positional) is empty");
    }

    private static void CheckTokens(
        string fieldName,
        SkillFieldSpec spec,
        SkillFieldEntry entry,
        List<SkillValidationIssue> issues)
    {
        if (spec.Slots.Count == 0 || string.IsNullOrWhiteSpace(entry.RawValue)) return;

        var tokens = TokValueReader.Parse(entry.RawValue).Tokens;
        for (int i = 0; i < tokens.Count && i < spec.Slots.Count; i++)
        {
            var token = tokens[i];
            var slot = spec.Slots[i];

            if (token.Kind is TokValueKind.Empty) continue;

            bool tokenIsText = token.Kind is TokValueKind.Quoted or TokValueKind.Identifier;
            bool slotIsNumericOnly = slot.Types.Count > 0
                                     && !slot.Types.Contains("string", StringComparer.OrdinalIgnoreCase);

            if (tokenIsText && slotIsNumericOnly)
            {
                Add(issues, SkillValidationSeverity.Warning, SkillValidationCodes.NumericShape, fieldName,
                    $"argument {i + 1} should be {string.Join("/", slot.Types)} but is text " +
                    $"'{Describe(token)}'");
                continue;
            }

            if (tokenIsText && slot.Values.Count > 0 && ShouldEnumerate(spec, slot) &&
                !slot.Values.Any(v => string.Equals(v, token.Text, StringComparison.OrdinalIgnoreCase)))
            {
                Add(issues, SkillValidationSeverity.Info, SkillValidationCodes.UnknownEnumValue, fieldName,
                    $"argument {i + 1} value '{Describe(token)}' is not in the catalog " +
                    $"({slot.Values.Count} known values)");
            }
        }
    }

    private static bool ShouldEnumerate(SkillFieldSpec spec, SkillSlotSpec slot) =>
        slot.Enumerate ?? spec.Enumerate;

    /// <summary>
    /// Judge repetition against engine semantics (verified against the GameCube
    /// DOL): accumulating fields — bit-sets, class sets and conditional modifiers —
    /// combine, so repeats are legal; slot-based fields silently overwrite once
    /// their slot count is exceeded.
    /// </summary>
    private static void CheckRepetition(
        string fieldName,
        SkillFieldSpec spec,
        int count,
        List<SkillValidationIssue> issues)
    {
        if (count <= 1 || spec.Accumulates) return;

        if (spec.EngineKnown == true)
        {
            if (spec.MaxOccurrences is null) return;   // engine allows unbounded repeats
            if (count > spec.MaxOccurrences.Value)
            {
                Add(issues, SkillValidationSeverity.Warning, SkillValidationCodes.ExceedsEngineSlots, fieldName,
                    $"appears {count} times but the engine only keeps {spec.MaxOccurrences.Value} " +
                    $"(later line(s) overwrite earlier ones)");
            }
            return;
        }

        // No engine metadata (older catalog): fall back to the documented MULTI flag.
        if (!spec.AllowsRepetition)
        {
            Add(issues, SkillValidationSeverity.Warning, SkillValidationCodes.DuplicateSingleCardinality, fieldName,
                $"appears {count} times but is documented as single-use (MULTI: no)");
        }
    }

    private static void CheckLinks(
        string fieldName,
        SkillFieldSpec spec,
        HashSet<string> present,
        List<SkillValidationIssue> issues)
    {
        foreach (string link in spec.Links)
        {
            string normalized = SkillsCatalog.NormalizeFieldName(link);
            if (string.Equals(normalized, fieldName, StringComparison.OrdinalIgnoreCase)) continue;
            if (present.Contains(normalized)) continue;

            Add(issues,
                spec.LinksStrict ? SkillValidationSeverity.Warning : SkillValidationSeverity.Info,
                SkillValidationCodes.MissingLinkedField, fieldName,
                $"documented LINK with '{normalized}' is absent" +
                (spec.LinksStrict ? string.Empty : " (advisory: vanilla data violates this rule too)"));
        }
    }

    private static void CheckRequirements(
        string fieldName,
        SkillFieldSpec spec,
        Dictionary<string, List<SkillFieldEntry>> byField,
        List<SkillValidationIssue> issues)
    {
        foreach (var req in spec.Requirements)
        {
            if (!req.IsEnforceable) continue;   // prose-only entry

            var severity = req.Strict ? SkillValidationSeverity.Warning : SkillValidationSeverity.Info;
            string suffix = req.Violations > 0
                ? $" ({req.Violations} observed record(s) in the corpus break this rule too)"
                : string.Empty;

            string required = SkillsCatalog.NormalizeFieldName(req.Field!);
            if (!byField.TryGetValue(required, out var entries) || entries.Count == 0)
            {
                Add(issues, severity, SkillValidationCodes.MissingRequirement, fieldName,
                    $"documented REQUIRES '{required}' is absent{suffix}");
                continue;
            }

            if (string.IsNullOrEmpty(req.Value)) continue;

            bool matches = entries.Any(e => FirstTokenText(e.RawValue)
                .Equals(req.Value, StringComparison.OrdinalIgnoreCase));
            if (!matches)
            {
                Add(issues, severity, SkillValidationCodes.MissingRequirement, fieldName,
                    $"documented REQUIRES '{required}' = '{req.Value}' is not satisfied{suffix}");
            }
        }
    }

    private static void CheckLookupIds(
        Dictionary<string, List<SkillFieldEntry>> byField,
        SkillsContext context,
        List<SkillValidationIssue> issues)
    {
        foreach (string fieldName in LookupIdFields)
        {
            if (!byField.TryGetValue(fieldName, out var entries)) continue;

            foreach (var entry in entries)
            {
                string raw = entry.RawValue?.Trim() ?? string.Empty;
                if (raw.Length == 0) continue;
                if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int id))
                {
                    Add(issues, SkillValidationSeverity.Warning, SkillValidationCodes.NumericShape, fieldName,
                        $"'{raw}' is not an integer lookup id");
                    continue;
                }

                if (!context.LookupText.ContainsKey(id))
                {
                    Add(issues, SkillValidationSeverity.Warning, SkillValidationCodes.DanglingLookupId, fieldName,
                        $"lookup id {id} is not present in lookuptext_eng.txt");
                }
            }
        }
    }

    private static string FirstTokenText(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue)) return string.Empty;
        var tokens = TokValueReader.Parse(rawValue).Tokens;
        return tokens.Count > 0 ? tokens[0].Text : string.Empty;
    }

    private static string Describe(TokValueToken token) =>
        token.Kind == TokValueKind.Quoted ? $"\"{token.Text}\"" : token.Text;

    private static void Add(
        List<SkillValidationIssue> issues,
        SkillValidationSeverity severity,
        string code,
        string field,
        string message) =>
        issues.Add(new SkillValidationIssue
        {
            Severity = severity,
            Code = code,
            Field = field,
            Message = message,
        });
}
