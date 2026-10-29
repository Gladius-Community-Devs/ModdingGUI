namespace ModdingGUI.Core.Skills;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>
/// Typed view of <c>skills_catalog.json</c> — the generated inventory of every
/// field and value observed in <c>skills.tok</c>, plus the structural rules
/// documented in <c>skills_field_reference.txt</c>.
/// </summary>
/// <remarks>
/// <para>
/// Skills are edited as raw lines (see <see cref="Api.ISkillsApi"/>), so no typed
/// record schema exists for them. The catalog fills that gap with data: which
/// fields exist, which slots enumerate, what values have been seen, which
/// fields may repeat, which must appear together, and which depend on another
/// field. The GUI uses it for dropdowns; <see cref="SkillsValidator"/> uses it
/// for advisory validation; the MCP tool surface can use it to describe fields.
/// </para>
/// <para>
/// Loading is deliberately tolerant: unknown JSON members are ignored, missing
/// members fall back to defaults, and malformed field entries are skipped rather
/// than throwing. A stale or hand-edited catalog must never break the editor.
/// </para>
/// </remarks>
public sealed class SkillsCatalog
{
    /// <summary>Catalog format version this loader understands.</summary>
    public const int SupportedFormatVersion = 2;

    // Numbered "mirror" fields collapse onto their base name (SKILLSTATUS2 -> SKILLSTATUS).
    private static readonly Regex MirrorSuffixRx =
        new(@"^(SKILL[A-Z]+?)(\d+)$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private readonly Dictionary<string, SkillFieldSpec> _fields;
    private readonly List<string> _order;

    private SkillsCatalog(Dictionary<string, SkillFieldSpec> fields, List<string> order, string? sourcePath)
    {
        _fields = fields;
        _order = order;
        SourcePath = sourcePath;
    }

    /// <summary>An empty catalog — every lookup misses, validation degrades to structural checks.</summary>
    public static SkillsCatalog Empty { get; } = new(
        new Dictionary<string, SkillFieldSpec>(StringComparer.OrdinalIgnoreCase),
        new List<string>(),
        null);

    /// <summary>Full path the catalog was loaded from, or <c>null</c> for <see cref="Empty"/>.</summary>
    public string? SourcePath { get; }

    /// <summary>Declared format version from <c>_meta.format_version</c> (0 when absent).</summary>
    public int FormatVersion { get; private set; }

    /// <summary>Number of documented fields.</summary>
    public int FieldCount => _order.Count;

    /// <summary>Field names in stable (case-insensitive alphabetical) order.</summary>
    public IReadOnlyList<string> FieldNames => _order;

    /// <summary>Every field spec, in <see cref="FieldNames"/> order.</summary>
    public IEnumerable<SkillFieldSpec> Fields => _order.Select(n => _fields[n]);

    /// <summary>True when the catalog documents <paramref name="fieldName"/> (after mirror normalization).</summary>
    public bool Contains(string fieldName) => Find(fieldName) is not null;

    /// <summary>
    /// Look up a field, normalizing numbered mirrors (<c>SKILLSTATUS2</c> →
    /// <c>SKILLSTATUS</c>). Returns <c>null</c> when the catalog does not
    /// document the field.
    /// </summary>
    public SkillFieldSpec? Find(string fieldName)
    {
        if (string.IsNullOrWhiteSpace(fieldName)) return null;
        string normalized = NormalizeFieldName(fieldName.Trim());
        return _fields.TryGetValue(normalized, out var spec) ? spec : null;
    }

    /// <summary>Collapse <c>&lt;BASE&gt;&lt;digits&gt;</c> mirror names onto their base.</summary>
    public static string NormalizeFieldName(string fieldName)
    {
        if (string.IsNullOrEmpty(fieldName)) return string.Empty;
        var m = MirrorSuffixRx.Match(fieldName);
        return m.Success ? m.Groups[1].Value : fieldName;
    }

    /// <summary>Load a catalog from disk. Throws only if the file cannot be read or is not JSON.</summary>
    public static SkillsCatalog Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Catalog path is required.", nameof(path));
        string json = File.ReadAllText(path);
        return Parse(json, path);
    }

    /// <summary>Load a catalog, returning <c>null</c> when the path is missing or unreadable.</summary>
    public static SkillsCatalog? TryLoad(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;
        try
        {
            return Load(path);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Resolve the catalog to use for a project: a project-local
    /// <c>skills_catalog.json</c> wins (modders who regenerated against their own
    /// <c>skills.tok</c>), otherwise the copy shipped next to the executable
    /// (<c>&lt;app&gt;/skills/skills_catalog.json</c>, then <c>&lt;app&gt;/skills_catalog.json</c>).
    /// </summary>
    public static SkillsCatalog? FindForProject(string? projectPath, string? appBaseDirectory)
    {
        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(projectPath))
            candidates.Add(Path.Combine(projectPath, "skills_catalog.json"));
        if (!string.IsNullOrWhiteSpace(appBaseDirectory))
        {
            candidates.Add(Path.Combine(appBaseDirectory, "skills", "skills_catalog.json"));
            candidates.Add(Path.Combine(appBaseDirectory, "skills_catalog.json"));
        }

        foreach (string candidate in candidates)
        {
            var catalog = TryLoad(candidate);
            if (catalog is not null) return catalog;
        }
        return null;
    }

    /// <summary>Parse catalog JSON. Tolerant: bad field entries are skipped, not fatal.</summary>
    public static SkillsCatalog Parse(string json, string? sourcePath = null)
    {
        using var doc = JsonDocument.Parse(json);
        var fields = new Dictionary<string, SkillFieldSpec>(StringComparer.OrdinalIgnoreCase);
        var order = new List<string>();
        int formatVersion = 0;

        if (doc.RootElement.ValueKind != JsonValueKind.Object)
            return Empty;

        if (doc.RootElement.TryGetProperty("_meta", out var meta) &&
            meta.ValueKind == JsonValueKind.Object &&
            meta.TryGetProperty("format_version", out var fv) &&
            fv.ValueKind == JsonValueKind.Number)
        {
            formatVersion = fv.TryGetInt32(out int v) ? v : 0;
        }

        if (!doc.RootElement.TryGetProperty("fields", out var fieldsEl) || fieldsEl.ValueKind != JsonValueKind.Object)
            return new SkillsCatalog(fields, order, sourcePath) { FormatVersion = formatVersion };

        foreach (var prop in fieldsEl.EnumerateObject())
        {
            if (prop.Value.ValueKind != JsonValueKind.Object) continue;
            var spec = ParseField(prop.Name, prop.Value);
            if (spec is null) continue;
            if (fields.ContainsKey(spec.Name)) continue;   // first definition wins
            fields[spec.Name] = spec;
        }

        order = fields.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList();
        return new SkillsCatalog(fields, order, sourcePath) { FormatVersion = formatVersion };
    }

    private static SkillFieldSpec? ParseField(string name, JsonElement el)
    {
        try
        {
            var slots = new List<SkillSlotSpec>();
            if (el.TryGetProperty("slots", out var slotsEl) && slotsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var slotEl in slotsEl.EnumerateArray())
                {
                    if (slotEl.ValueKind != JsonValueKind.Object) continue;
                    slots.Add(new SkillSlotSpec
                    {
                        Types = ReadStringArray(slotEl, "types"),
                        Values = ReadStringArray(slotEl, "values"),
                        Enumerate = ReadNullableBool(slotEl, "enumerate"),
                        Note = ReadString(slotEl, "note"),
                    });
                }
            }

            var requirements = new List<SkillRequirement>();
            if (el.TryGetProperty("requires", out var reqEl) && reqEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var r in reqEl.EnumerateArray())
                {
                    if (r.ValueKind != JsonValueKind.Object) continue;
                    requirements.Add(new SkillRequirement
                    {
                        Field = ReadString(r, "field"),
                        Part = ReadString(r, "part"),
                        Value = ReadString(r, "equals"),
                        Present = ReadNullableBool(r, "present") ?? false,
                        Raw = ReadString(r, "raw"),
                        Strict = ReadNullableBool(r, "strict") ?? true,
                        Violations = (int)(ReadNullableLong(r, "violations") ?? 0),
                    });
                }
            }

            string? numericType = null;
            long? numericMin = null;
            long? numericMax = null;
            if (el.TryGetProperty("numeric", out var numEl) && numEl.ValueKind == JsonValueKind.Object)
            {
                numericType = ReadString(numEl, "type");
                numericMin = ReadNullableLong(numEl, "min");
                numericMax = ReadNullableLong(numEl, "max");
            }

            var mirrors = ReadStringArray(el, "mirrors");
            string normalized = NormalizeFieldName(name);
            bool hasNumberedVariants = mirrors.Any(m =>
                !string.Equals(m, normalized, StringComparison.OrdinalIgnoreCase));

            return new SkillFieldSpec
            {
                Name = normalized,
                Enumerate = ReadNullableBool(el, "enumerate") ?? false,
                Note = ReadString(el, "note"),
                Format = ReadString(el, "format"),
                Multi = ReadNullableBool(el, "multi") ?? false,
                Mirrors = mirrors,
                Links = ReadStringArray(el, "links"),
                LinksStrict = ReadNullableBool(el, "links_strict") ?? true,
                Requirements = requirements,
                Slots = slots,
                NumericType = numericType,
                NumericMin = numericMin,
                NumericMax = numericMax,
                ReferenceOnly = ReadNullableBool(el, "reference_only") ?? false,
                HasNumberedVariants = hasNumberedVariants,
                EngineKnown = ReadNullableBool(el, "engine"),
                MaxOccurrences = (int?)ReadNullableLong(el, "max_occurrences"),
                Accumulates = ReadNullableBool(el, "accumulates") ?? false,
                EngineOnly = ReadNullableBool(el, "engine_only") ?? false,
            };
        }
        catch (InvalidOperationException)
        {
            return null;   // wrong JSON shape for this field; skip it
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static string? ReadString(JsonElement el, string name) =>
        el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

    private static bool? ReadNullableBool(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var p)) return null;
        return p.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null,
        };
    }

    private static long? ReadNullableLong(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var p) || p.ValueKind != JsonValueKind.Number) return null;
        return p.TryGetInt64(out long v) ? v : null;
    }

    private static IReadOnlyList<string> ReadStringArray(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var arr) || arr.ValueKind != JsonValueKind.Array)
            return Array.Empty<string>();

        var list = new List<string>(arr.GetArrayLength());
        foreach (var item in arr.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                string? s = item.GetString();
                if (!string.IsNullOrEmpty(s)) list.Add(s);
            }
        }
        return list;
    }
}

/// <summary>One documented <c>skills.tok</c> field.</summary>
public sealed class SkillFieldSpec
{
    /// <summary>Normalized field name (mirrors collapsed), e.g. <c>SKILLSTATUS</c>.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>True when slot values are an enumerable set worth offering as choices.</summary>
    public bool Enumerate { get; init; }

    /// <summary>Human-readable guidance (from the generator policy or the reference file).</summary>
    public string? Note { get; init; }

    /// <summary>Documented value shape, e.g. <c>"name", chance, value</c>.</summary>
    public string? Format { get; init; }

    /// <summary>True when the field may legally appear more than once in one record.</summary>
    public bool Multi { get; init; }

    /// <summary>Original observed spellings, including numbered variants (<c>SKILLSTATUS2</c>).</summary>
    public IReadOnlyList<string> Mirrors { get; init; } = Array.Empty<string>();

    /// <summary>Fields that must appear together with this one.</summary>
    public IReadOnlyList<string> Links { get; init; } = Array.Empty<string>();

    /// <summary>
    /// True when the LINK rule holds across the whole observed corpus. A rule that
    /// vanilla data violates is documented but advisory, so it cannot produce
    /// false warnings on valid records.
    /// </summary>
    public bool LinksStrict { get; init; } = true;

    /// <summary>Prerequisites; entries with only <see cref="SkillRequirement.Raw"/> are prose and unenforced.</summary>
    public IReadOnlyList<SkillRequirement> Requirements { get; init; } = Array.Empty<SkillRequirement>();

    /// <summary>Per-positional-argument shape and value sets.</summary>
    public IReadOnlyList<SkillSlotSpec> Slots { get; init; } = Array.Empty<SkillSlotSpec>();

    /// <summary>Declared numeric type (<c>int</c>/<c>float</c>) when the field is range-constrained.</summary>
    public string? NumericType { get; init; }

    /// <summary>Declared minimum, inclusive; <c>null</c> when unbounded.</summary>
    public long? NumericMin { get; init; }

    /// <summary>Declared maximum, inclusive; <c>null</c> when unbounded.</summary>
    public long? NumericMax { get; init; }

    /// <summary>True when the field exists only in the reference documentation, never observed on disk.</summary>
    public bool ReferenceOnly { get; init; }

    /// <summary>True when numbered variants were observed, which legitimises repetition.</summary>
    public bool HasNumberedVariants { get; init; }

    /// <summary>
    /// Whether the game engine recognises this field name. <c>null</c> when the
    /// catalog carries no engine data (older catalogs). <c>false</c> means the
    /// engine's tokenizer will never match the name — the line is silently ignored
    /// in game, so the edit has no effect.
    /// </summary>
    public bool? EngineKnown { get; init; }

    /// <summary>
    /// How many occurrences the engine can hold in one record: <c>1</c> for a
    /// scalar (last wins), <c>2</c> for the paired status slots, <c>null</c> for
    /// repeating or accumulating fields.
    /// </summary>
    public int? MaxOccurrences { get; init; }

    /// <summary>
    /// True when repeated lines combine instead of overwriting: bit-set fields
    /// (attribute / projectile flags), class sets and conditional-modifier fields.
    /// </summary>
    public bool Accumulates { get; init; }

    /// <summary>True for engine-legal fields that never occur in the vanilla corpus.</summary>
    public bool EngineOnly { get; init; }

    /// <summary>True when repetition is legal (engine allows it, or explicit MULTI / numbered variants).</summary>
    public bool AllowsRepetition =>
        Accumulates
        || Multi
        || HasNumberedVariants
        || (EngineKnown == true && (MaxOccurrences is null || MaxOccurrences > 1));
}

/// <summary>Shape and known values for one positional argument of a field.</summary>
public sealed class SkillSlotSpec
{
    /// <summary>Observed token kinds: <c>int</c>, <c>float</c>, <c>string</c>.</summary>
    public IReadOnlyList<string> Types { get; init; } = Array.Empty<string>();

    /// <summary>Known string values (observed plus reference-documented).</summary>
    public IReadOnlyList<string> Values { get; init; } = Array.Empty<string>();

    /// <summary>Per-slot override of the field-level enumerate flag.</summary>
    public bool? Enumerate { get; init; }

    /// <summary>Per-slot guidance.</summary>
    public string? Note { get; init; }
}

/// <summary>
/// A prerequisite relationship: "this field only makes sense when
/// <see cref="Field"/> is present (or equals <see cref="Equals"/>)".
/// </summary>
public sealed class SkillRequirement
{
    /// <summary>Prerequisite field name; <c>null</c> for prose-only entries.</summary>
    public string? Field { get; init; }

    /// <summary>Optional sub-part selector (<c>FIELD.part = value</c>).</summary>
    public string? Part { get; init; }

    /// <summary>When set, the prerequisite field's first argument must equal this (case-insensitive).</summary>
    public string? Value { get; init; }

    /// <summary>True for the "field must be present" form.</summary>
    public bool Present { get; init; }

    /// <summary>Unparsed prose kept for humans — never enforced.</summary>
    public string? Raw { get; init; }

    /// <summary>
    /// True when the rule holds across the observed corpus. Rules contradicted by
    /// vanilla data are kept (documented) but reported as
    /// <see cref="SkillValidationSeverity.Info"/> instead of a warning.
    /// </summary>
    public bool Strict { get; init; } = true;

    /// <summary>Number of observed records that violate the rule (0 when strict).</summary>
    public int Violations { get; init; }

    /// <summary>True when this entry can be enforced.</summary>
    public bool IsEnforceable => !string.IsNullOrEmpty(Field);
}
