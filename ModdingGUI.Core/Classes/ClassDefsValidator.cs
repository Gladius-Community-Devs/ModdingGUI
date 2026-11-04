namespace ModdingGUI.Core.Classes;

using System;
using System.Collections.Generic;
using System.Linq;
using ModdingGUI.Core.Tok.Values;

/// <summary>Severity of a class definition finding. Nothing here blocks a save.</summary>
public enum ClassValidationSeverity
{
    /// <summary>Informational finding.</summary>
    Info,

    /// <summary>Something that will not behave as the modder expects.</summary>
    Warning,
}

/// <summary>Stable finding codes, so the GUI and tests never match on message text.</summary>
public static class ClassValidationCodes
{
    /// <summary>The key is not in the extracted key set at all (suspicious syntax).</summary>
    public const string UnknownField = "unknown-field";

    /// <summary>The key parses but the engine's field table has no such key, so it is ignored.</summary>
    public const string FieldNotInEngine = "field-not-in-engine";

    /// <summary>An argument holds a value the corpus never uses for that position.</summary>
    public const string UnknownValue = "unknown-enum-value";

    /// <summary>Wrong number of arguments for the key.</summary>
    public const string ArgumentCount = "argument-count";

    /// <summary>A key that the engine keeps once appears more than once.</summary>
    public const string DuplicateSingleCardinality = "duplicate-single-cardinality";

    /// <summary>A repeated key beyond the engine's slot count.</summary>
    public const string ExceedsEngineSlots = "exceeds-engine-slots";
}

/// <summary>One finding about one class block.</summary>
/// <param name="Severity">How much it matters.</param>
/// <param name="Code">Stable code from <see cref="ClassValidationCodes"/>.</param>
/// <param name="ClassName">Class the finding belongs to.</param>
/// <param name="Field">Key it concerns, when applicable.</param>
/// <param name="Message">Human-readable explanation.</param>
public sealed record ClassValidationIssue(
    ClassValidationSeverity Severity,
    string Code,
    string ClassName,
    string? Field,
    string Message);

/// <summary>
/// Advisory checks for a class block: keys the engine does not know, argument shapes that
/// do not match any observed usage, and repetition past a single slot.
/// </summary>
/// <remarks>
/// The class definitions are freeform in the same way skills are, so nothing here is an
/// error: the engine skips unknown keys silently and reuses the last value for repeated
/// scalar keys, which means a typo costs a modder hours rather than failing loudly.
/// </remarks>
public static class ClassDefsValidator
{
    /// <summary>Validate a parsed block against the catalog.</summary>
    public static IReadOnlyList<ClassValidationIssue> Validate(
        Api.ClassDefDto classDef,
        ClassDefsCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(classDef);
        ArgumentNullException.ThrowIfNull(catalog);

        var issues = new List<ClassValidationIssue>();
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var field in classDef.Fields)
        {
            string key = ClassDefsCatalog.NormalizeKey(field.Key);
            counts[key] = counts.TryGetValue(key, out int seen) ? seen + 1 : 1;

            var spec = catalog.Find(key);
            if (spec is null)
            {
                Add(issues, ClassValidationSeverity.Warning, ClassValidationCodes.UnknownField,
                    classDef.Name, key,
                    $"'{key}' is not a class definition key; the game will ignore this line");
                continue;
            }

            if (!spec.EngineKnown)
            {
                Add(issues, ClassValidationSeverity.Warning, ClassValidationCodes.FieldNotInEngine,
                    classDef.Name, key,
                    $"'{key}' is not in the engine's field table, so the game ignores it");
            }

            CheckArguments(classDef.Name, key, field.RawValue, spec, issues);
        }

        foreach (var (key, count) in counts)
        {
            var spec = catalog.Find(key);
            if (spec is null || count <= 1) continue;
            if (spec.AllowsRepetition) continue;

            string consequence = spec.EngineKnown
                ? "the engine keeps only the last value"
                : "repeating it has no effect";
            Add(issues, ClassValidationSeverity.Warning, ClassValidationCodes.DuplicateSingleCardinality,
                classDef.Name, key,
                $"'{key}' appears {count} times but takes a single value — {consequence}");
        }

        return issues;
    }

    private static void CheckArguments(
        string className,
        string key,
        string rawValue,
        ClassFieldSpec spec,
        List<ClassValidationIssue> issues)
    {
        var tokens = TokValueReader.Parse(rawValue).Tokens
            .Where(t => t.Kind != TokValueKind.Empty)
            .ToList();

        if (spec.Arity > 0 && tokens.Count != spec.Arity)
        {
            Add(issues, ClassValidationSeverity.Warning, ClassValidationCodes.ArgumentCount,
                className, key,
                $"'{key}' takes {spec.Arity} argument(s) but this line has {tokens.Count}");
            return;
        }

        for (int i = 0; i < tokens.Count && i < spec.Slots.Count; i++)
        {
            var slot = spec.Slots[i];
            var token = tokens[i];
            if (!slot.IsEnumerable) continue;
            if (token.Kind is not (TokValueKind.Quoted or TokValueKind.Identifier)) continue;
            if (slot.Allows(token.Text)) continue;

            Add(issues, ClassValidationSeverity.Warning, ClassValidationCodes.UnknownValue,
                className, key,
                $"argument {i + 1} of '{key}' is '{token.Text}', which no class uses " +
                $"(expected one of {FormatValues(slot.Values)})");
        }
    }

    private static string FormatValues(IReadOnlyList<string> values)
    {
        const int limit = 8;
        var shown = values.Take(limit).Select(v => $"'{v}'");
        string joined = string.Join(", ", shown);
        return values.Count > limit ? joined + $", … ({values.Count} total)" : joined;
    }

    private static void Add(
        List<ClassValidationIssue> issues,
        ClassValidationSeverity severity,
        string code,
        string className,
        string? field,
        string message) =>
        issues.Add(new ClassValidationIssue(severity, code, className, field, message));
}
