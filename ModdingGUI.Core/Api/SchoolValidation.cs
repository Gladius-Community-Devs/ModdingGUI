namespace ModdingGUI.Core.Api;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ModdingGUI.Core.Tok.Values;

/// <summary>Severity of a school finding; nothing blocks a save.</summary>
public enum SchoolValidationSeverity
{
    /// <summary>Informational.</summary>
    Info,

    /// <summary>Will not behave as the modder expects.</summary>
    Warning,
}

/// <summary>Stable finding codes for school rosters.</summary>
public static class SchoolValidationCodes
{
    /// <summary>Header key the corpus never uses.</summary>
    public const string UnknownHeaderKey = "unknown-header-key";

    /// <summary>Unit key the corpus never uses.</summary>
    public const string UnknownUnitKey = "unknown-unit-key";

    /// <summary><c>CORESTATSCOMP2</c> does not have six values.</summary>
    public const string CoreStatsArity = "core-stats-arity";

    /// <summary><c>ITEMSCOMP</c> does not have the five equipment columns.</summary>
    public const string ItemsArity = "items-arity";

    /// <summary>
    /// A <c>CUSTOMIZE</c> outfit index is outside the range the corpus uses. Outfits are zero-indexed per
    /// class, nothing in the files lists the valid set, and an invalid value has been reported to crash
    /// the game (research log: "Outfit 1 is not valid … default to 0, hope it prevents crashes").
    /// </summary>
    public const string OutfitRange = "outfit-range";

    /// <summary>The roster declares no <c>HERO</c>.</summary>
    public const string MissingHero = "missing-hero";

    /// <summary>A unit block has no <c>CORESTATSCOMP2</c>.</summary>
    public const string MissingCoreStats = "missing-core-stats";

    /// <summary>A granted skill name appears twice on one unit.</summary>
    public const string DuplicateSkill = "duplicate-skill";
}

/// <summary>One finding about a school roster.</summary>
/// <param name="Severity">How much it matters.</param>
/// <param name="Code">Stable code from <see cref="SchoolValidationCodes"/>.</param>
/// <param name="Unit">Unit it concerns, or <c>header</c>.</param>
/// <param name="Key">Field it concerns, when applicable.</param>
/// <param name="Message">Human-readable explanation.</param>
public sealed record SchoolValidationIssue(
    SchoolValidationSeverity Severity,
    string Code,
    string Unit,
    string? Key,
    string Message);

/// <summary>Which header/unit keys the school corpus uses.</summary>
public sealed class SchoolsCatalog
{
    /// <summary>Shipped catalog path, relative to the app directory.</summary>
    public const string ShippedRelativePath = "tables/schools_catalog.json";

    private SchoolsCatalog(IReadOnlyDictionary<string, int> headerKeys, IReadOnlyDictionary<string, int> unitKeys)
    {
        HeaderKeys = headerKeys;
        UnitKeys = unitKeys;
    }

    /// <summary>Header keys with their corpus frequencies.</summary>
    public IReadOnlyDictionary<string, int> HeaderKeys { get; }

    /// <summary>Unit-block keys with their corpus frequencies.</summary>
    public IReadOnlyDictionary<string, int> UnitKeys { get; }

    /// <summary>Load from an explicit path.</summary>
    public static SchoolsCatalog Load(string path)
    {
        using var stream = File.OpenRead(path);
        return Load(stream);
    }

    /// <summary>Load from a stream.</summary>
    public static SchoolsCatalog Load(Stream stream)
    {
        using var document = JsonDocument.Parse(stream);
        var root = document.RootElement;
        return new SchoolsCatalog(ReadCounts(root, "header_keys"), ReadCounts(root, "unit_keys"));
    }

    /// <summary>Load the shipped catalog, or null when it is not deployed.</summary>
    public static SchoolsCatalog? Shipped()
    {
        foreach (string candidate in CatalogPaths.Candidates(ShippedRelativePath))
        {
            if (File.Exists(candidate)) return Load(candidate);
        }
        return null;
    }

    internal static IReadOnlyDictionary<string, int> ReadCounts(JsonElement root, string property)
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (!root.TryGetProperty(property, out var element) || element.ValueKind != JsonValueKind.Object)
            return result;

        foreach (var pair in element.EnumerateObject())
            result[pair.Name] = pair.Value.ValueKind == JsonValueKind.Number ? pair.Value.GetInt32() : 0;
        return result;
    }
}

/// <summary>Shared catalog lookup: next to the exe, then walking up to the repository copy.</summary>
internal static class CatalogPaths
{
    internal static IEnumerable<string> Candidates(string relativePath)
    {
        yield return Path.Combine(AppContext.BaseDirectory,
            relativePath.Replace('/', Path.DirectorySeparatorChar));

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (int i = 0; i < 6 && dir is not null; i++, dir = dir.Parent)
        {
            yield return Path.Combine(dir.FullName,
                relativePath.Replace('/', Path.DirectorySeparatorChar));
        }
    }
}

/// <summary>Advisory checks for a school roster.</summary>
public static class SchoolValidator
{
    private const int CoreStatsCompactCount = 6;  // CORESTATSCOMP2: CON, PWR, ACC, DEF, INI, MOVE
    private const int CoreStatsLegacyCount = 7;   // CORESTATSCOMP: CON, PWR, ACC, DEF, INT, INI, MOVE
    private const int ItemSlotCount = 5;    // weapon, armor, shield, helmet, accessory
    private const int MaxOutfit = 6;        // highest outfit index the vanilla corpus uses

    /// <summary>Validate a parsed roster (a null catalog skips the key checks).</summary>
    public static IReadOnlyList<SchoolValidationIssue> Validate(SchoolDto school, SchoolsCatalog? catalog)
    {
        ArgumentNullException.ThrowIfNull(school);

        var issues = new List<SchoolValidationIssue>();

        if (school.Hero is null)
        {
            // Legitimate: a few rosters predate the HERO key (story scripts name the hero).
            issues.Add(new SchoolValidationIssue(SchoolValidationSeverity.Info,
                SchoolValidationCodes.MissingHero, "header", "HERO",
                "roster has no HERO; only story-driven rosters omit it"));
        }

        if (catalog is not null)
        {
            foreach (var field in school.Fields)
            {
                if (catalog.HeaderKeys.ContainsKey(field.Key)) continue;
                issues.Add(new SchoolValidationIssue(SchoolValidationSeverity.Warning,
                    SchoolValidationCodes.UnknownHeaderKey, "header", field.Key,
                    $"'{field.Key}' is not a known roster header key"));
            }
        }

        foreach (var unit in school.Units)
        {
            if (catalog is not null)
            {
                foreach (var field in unit.Fields)
                {
                    if (catalog.UnitKeys.ContainsKey(field.Key)) continue;
                    issues.Add(new SchoolValidationIssue(SchoolValidationSeverity.Warning,
                        SchoolValidationCodes.UnknownUnitKey, unit.Name, field.Key,
                        $"'{field.Key}' is not a known unit key; the game ignores it"));
                }
            }

            // Two spellings exist with different layouts, so each has its own expected count.
            string? stats2 = unit.Value("CORESTATSCOMP2");
            string? stats1 = string.IsNullOrWhiteSpace(stats2) ? unit.Value("CORESTATSCOMP") : null;
            string statsKey = string.IsNullOrWhiteSpace(stats1) ? "CORESTATSCOMP2" : "CORESTATSCOMP";
            string? stats = string.IsNullOrWhiteSpace(stats1) ? stats2 : stats1;

            if (string.IsNullOrWhiteSpace(stats))
            {
                // Dev/test rosters ship with an empty stats line and fall back to class defaults.
                issues.Add(new SchoolValidationIssue(SchoolValidationSeverity.Info,
                    SchoolValidationCodes.MissingCoreStats, unit.Name, "CORESTATSCOMP2",
                    "unit has no core stats; the game uses class defaults"));
            }
            else
            {
                int expected = statsKey == "CORESTATSCOMP2" ? CoreStatsCompactCount : CoreStatsLegacyCount;
                int count = CountArguments(stats);
                if (count != expected)
                {
                    string layout = statsKey == "CORESTATSCOMP2"
                        ? "CON, PWR, ACC, DEF, INI, MOVE"
                        : "CON, PWR, ACC, DEF, INT, INI, MOVE";
                    issues.Add(new SchoolValidationIssue(SchoolValidationSeverity.Warning,
                        SchoolValidationCodes.CoreStatsArity, unit.Name, statsKey,
                        $"{statsKey} has {count} values; it is {layout} ({expected})"));
                }
            }

            string? customize = unit.Value("CUSTOMIZE");
            if (customize is not null)
            {
                // CUSTOMIZE: <outfit>, "<name>" - the outfit is the same zero-indexed value a gladiator
                // record's "Outfit" holds, so it carries the same crash risk when it points at a look the
                // class does not have.
                if (CountArguments(customize) == 0)
                {
                    issues.Add(new SchoolValidationIssue(SchoolValidationSeverity.Warning,
                        SchoolValidationCodes.OutfitRange, unit.Name, "CUSTOMIZE",
                        "CUSTOMIZE is empty; expected '<outfit>, \"<name>\"'"));
                }
                else
                {
                    string first = customize.Split(',')[0].Trim();
                    if (!int.TryParse(first, out int outfit) || outfit < 0 || outfit > MaxOutfit)
                    {
                        issues.Add(new SchoolValidationIssue(SchoolValidationSeverity.Warning,
                            SchoolValidationCodes.OutfitRange, unit.Name, "CUSTOMIZE",
                            $"CUSTOMIZE outfit '{first}' is not an outfit index in 0-{MaxOutfit}; outfits are " +
                            "zero-indexed per class and an invalid one can crash the game"));
                    }
                }
            }

            string? items = unit.Value("ITEMSCOMP");
            if (items is not null)
            {
                int count = CountArguments(items);
                // Trailing empty slots are omitted, so the corpus shows 2, 4 and 5 columns;
                // only more than five cannot be a loadout.
                if (count > ItemSlotCount)
                {
                    issues.Add(new SchoolValidationIssue(SchoolValidationSeverity.Warning,
                        SchoolValidationCodes.ItemsArity, unit.Name, "ITEMSCOMP",
                        $"ITEMSCOMP has {count} values; at most {ItemSlotCount} fit " +
                        "(weapon, armor, shield, helmet, accessory)"));
                }
            }

            var duplicates = unit.Skills
                .GroupBy(s => s, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key);
            foreach (string skill in duplicates)
            {
                issues.Add(new SchoolValidationIssue(SchoolValidationSeverity.Info,
                    SchoolValidationCodes.DuplicateSkill, unit.Name, "SKILL",
                    $"'{skill}' is granted more than once"));
            }
        }

        return issues;
    }

    private static int CountArguments(string raw) =>
        TokValueReader.Parse(raw).Tokens.Count(t => t.Kind != TokValueKind.Empty);
}
