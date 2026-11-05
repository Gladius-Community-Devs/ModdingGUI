using System;
using System.Collections.Generic;
using ModdingGUI.Core.Units;

namespace ModdingGUI.Core.Api;

/// <summary>How serious a gladiator finding is. Mirrors the other validators.</summary>
public enum GladiatorValidationSeverity
{
    /// <summary>Worth knowing; the game copes.</summary>
    Info,

    /// <summary>Likely to misbehave in game.</summary>
    Warning,
}

/// <summary>Stable codes, so a UI or an MCP client can group findings.</summary>
public static class GladiatorValidationCodes
{
    /// <summary>The class name is not a record in <c>classdefs.tok</c>.</summary>
    public const string UnknownClass = "unknown-class";

    /// <summary>
    /// The affinity name is not one of the six the repack table knows. That breaks the repack (the script
    /// looks the name up in a fixed array), and the value is not inert: it is packed into
    /// <c>unitunits.idx</c> as the low three bits of a byte shared with <c>Outfit</c>, and the unit builder
    /// (<c>FUN_800dfac4</c>) decodes it to an engine affinity id when it creates a unit. What the player
    /// sees still comes from equipment — offense affinity is the weapon's and defense comes from
    /// armour/accessories — which is why changing it here looks like it does nothing.
    /// </summary>
    public const string UnknownAffinity = "unknown-affinity";

    /// <summary>A set index points past the end of its file.</summary>
    public const string SetOutOfRange = "set-out-of-range";

    /// <summary>
    /// The outfit index is outside the range the corpus uses. Outfits are zero-indexed per class, nothing
    /// lists the valid set, and an invalid value has been reported to crash the game.
    /// </summary>
    public const string OutfitOutOfRange = "outfit-out-of-range";

    /// <summary>Two records share a name.</summary>
    public const string DuplicateName = "duplicate-name";

    /// <summary>One of the nine labelled lines is missing.</summary>
    public const string MissingField = "missing-field";

    /// <summary>An unrecognised line sits inside the record (vanilla has one).</summary>
    public const string ExtraLine = "extra-line";
}

/// <summary>One advisory finding about a gladiator record.</summary>
public sealed record GladiatorValidationIssue(
    GladiatorValidationSeverity Severity,
    string Code,
    string Name,
    string Field,
    string Message);

/// <summary>
/// Edit <c>data/units/gladiators.txt</c>: the roster of every recruitable unit, with the nine values the
/// pack chain compiles into <c>unitunits.idx</c>.
/// </summary>
/// <remarks>
/// Writes take the project lock and go through <see cref="ChangeHistoryService"/>, and an untouched file
/// is written back byte for byte (the file is LF-only and contains a malformed vanilla record that must
/// survive). Validation is advisory, like every other API in this project.
/// </remarks>
public interface IGladiatorsApi
{
    /// <summary>Every gladiator name, in file order (2,170 in vanilla).</summary>
    IReadOnlyList<string> List(string projectRoot);

    /// <summary>One record, or null when the name is unknown.</summary>
    GladiatorDto? Get(string projectRoot, string name);

    /// <summary>Change any subset of the eight mutable values.</summary>
    void Patch(string projectRoot, string name, GladiatorPatch patch);

    /// <summary>Rename a gladiator, refusing a name that is already taken.</summary>
    void Rename(string projectRoot, string name, string newName);

    /// <summary>Append a new record.</summary>
    void Add(string projectRoot, GladiatorDto dto);

    /// <summary>Remove a record; false when the name is unknown.</summary>
    bool Remove(string projectRoot, string name);

    /// <summary>
    /// Advisory checks for one record (<paramref name="name"/>) or the whole file. Classes are checked
    /// against <c>classdefs.tok</c> and set indexes against the counts in the other units files.
    /// </summary>
    IReadOnlyList<GladiatorValidationIssue> Validate(string projectRoot, string? name = null);
}

/// <summary>Default <see cref="IGladiatorsApi"/>.</summary>
public sealed class GladiatorsApi : IGladiatorsApi
{
    private readonly IClassesApi _classes;

    /// <summary>Create the API; the class list is used for validation only.</summary>
    public GladiatorsApi(IClassesApi? classes = null) => _classes = classes ?? new ClassDefsApi();

    /// <inheritdoc />
    public IReadOnlyList<string> List(string projectRoot) =>
        GladiatorsFile.LoadProject(projectRoot).Records.Select(r => r.Name).ToArray();

    /// <inheritdoc />
    public GladiatorDto? Get(string projectRoot, string name) =>
        GladiatorsFile.LoadProject(projectRoot).Get(name);

    /// <inheritdoc />
    public void Patch(string projectRoot, string name, GladiatorPatch patch)
    {
        ArgumentNullException.ThrowIfNull(patch);
        string bec = ProjectRoots.ResolveBecFolder(projectRoot);

        using (ProjectLock.Acquire(bec))
        {
            var file = GladiatorsFile.LoadProject(bec);
            file.Patch(name, patch);
            if (file.IsModified) file.Save(bec);
        }
    }

    /// <inheritdoc />
    public void Rename(string projectRoot, string name, string newName)
    {
        string bec = ProjectRoots.ResolveBecFolder(projectRoot);

        using (ProjectLock.Acquire(bec))
        {
            var file = GladiatorsFile.LoadProject(bec);
            file.Rename(name, newName);
            if (file.IsModified) file.Save(bec);
        }
    }

    /// <inheritdoc />
    public void Add(string projectRoot, GladiatorDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        string bec = ProjectRoots.ResolveBecFolder(projectRoot);

        using (ProjectLock.Acquire(bec))
        {
            var file = GladiatorsFile.LoadProject(bec);
            file.Add(dto);
            file.Save(bec);
        }
    }

    /// <inheritdoc />
    public bool Remove(string projectRoot, string name)
    {
        string bec = ProjectRoots.ResolveBecFolder(projectRoot);

        using (ProjectLock.Acquire(bec))
        {
            var file = GladiatorsFile.LoadProject(bec);
            if (!file.Remove(name)) return false;
            file.Save(bec);
            return true;
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<GladiatorValidationIssue> Validate(string projectRoot, string? name = null)
    {
        string bec = ProjectRoots.ResolveBecFolder(projectRoot);
        var file = GladiatorsFile.LoadProject(bec);
        var catalog = UnitCatalogLoader.Load(bec);
        var issueList = new List<GladiatorValidationIssue>();

        IReadOnlyList<string> classes;
        try
        {
            classes = _classes.List(bec);
        }
        catch (Exception ex) when (ex is IOException or FileNotFoundException)
        {
            classes = Array.Empty<string>();
        }
        var classSet = new HashSet<string>(classes, StringComparer.OrdinalIgnoreCase);

        var records = file.Records;
        var duplicates = records
            .GroupBy(r => r.Name, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var record in records)
        {
            if (name is not null && !string.Equals(record.Name, name, StringComparison.Ordinal)) continue;

            if (string.IsNullOrWhiteSpace(record.Name))
            {
                issueList.Add(new GladiatorValidationIssue(GladiatorValidationSeverity.Warning,
                    GladiatorValidationCodes.MissingField, "(unnamed)", "Name",
                    "the record has no name, so it cannot be selected or referenced"));
            }

            if (duplicates.Contains(record.Name))
            {
                issueList.Add(new GladiatorValidationIssue(GladiatorValidationSeverity.Warning,
                    GladiatorValidationCodes.DuplicateName, record.Name, "Name",
                    $"the name '{record.Name}' appears more than once"));
            }

            if (classSet.Count > 0 && !classSet.Contains(record.Class))
            {
                issueList.Add(new GladiatorValidationIssue(GladiatorValidationSeverity.Warning,
                    GladiatorValidationCodes.UnknownClass, record.Name, "Class",
                    $"class '{record.Class}' is not in classdefs.tok"));
            }

            if (!GladiatorAffinity.IsKnown(record.Affinity))
            {
                // Warning: the repack script maps this name through a fixed table and reports anything else
                // as an invalid entry, so an unknown affinity breaks the pack rather than merely looking
                // odd. Runtime affinity is gear-derived, which is why a change here is invisible in game.
                issueList.Add(new GladiatorValidationIssue(GladiatorValidationSeverity.Warning,
                    GladiatorValidationCodes.UnknownAffinity, record.Name, "Affinity",
                    $"affinity '{record.Affinity}' is not one of {string.Join(", ", GladiatorAffinity.Values)}; " +
                    "the pack chain cannot repack an unknown name (runtime affinity comes from the equipped " +
                    "weapon and accessories)"));
            }

            CheckSet(issueList, record.Name, "Tint set", record.TintSet, catalog.Tints);
            CheckSet(issueList, record.Name, "Skill set", record.SkillSet, catalog.SkillSets);
            CheckSet(issueList, record.Name, "Stat set", record.StatSet, catalog.StatSets);
            CheckSet(issueList, record.Name, "Item set", record.ItemSet, catalog.ItemSets);
            CheckSet(issueList, record.Name, "School", record.School, catalog.Schools);

            if (record.Outfit is < 0 or > 6)
            {
                issueList.Add(new GladiatorValidationIssue(GladiatorValidationSeverity.Warning,
                    GladiatorValidationCodes.OutfitOutOfRange, record.Name, "Outfit",
                    $"outfit {record.Outfit} is outside the 0-6 the corpus uses; outfits are zero-indexed " +
                    "per class and an invalid one can crash the game"));
            }
        }

        foreach (var (recordName, field) in file.MissingFields())
        {
            if (name is not null && !string.Equals(recordName, name, StringComparison.Ordinal)) continue;
            issueList.Add(new GladiatorValidationIssue(GladiatorValidationSeverity.Warning,
                GladiatorValidationCodes.MissingField, recordName, field.ToString(),
                $"the record has no '{field}' line"));
        }

        foreach (var (recordName, line, text) in file.ExtraLines())
        {
            if (name is not null && !string.Equals(recordName, name, StringComparison.Ordinal)) continue;
            issueList.Add(new GladiatorValidationIssue(GladiatorValidationSeverity.Info,
                GladiatorValidationCodes.ExtraLine, recordName, $"line {line + 1}",
                $"unexpected line inside the record: '{text}'"));
        }

        return issueList;
    }

    private static void CheckSet(
        List<GladiatorValidationIssue> issues, string name, string field, int index, int count)
    {
        if (count <= 0) return;                 // the target file is missing; nothing to compare
        if (index >= 0 && index < count) return;

        issues.Add(new GladiatorValidationIssue(GladiatorValidationSeverity.Warning,
            GladiatorValidationCodes.SetOutOfRange, name, field,
            $"{field.ToLowerInvariant()} {index} does not exist (valid: 0-{count - 1})"));
    }
}
