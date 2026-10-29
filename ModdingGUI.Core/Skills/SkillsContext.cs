namespace ModdingGUI.Core.Skills;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ModdingGUI.Core.Api;
using ModdingGUI.Core.Tok;

/// <summary>
/// Project-scoped data the skills editor and validator need beyond the catalog:
/// valid combat-meter names and the lookup-text id table.
/// </summary>
/// <remarks>
/// <para>
/// Meter names come from two places on purpose. <c>data/config/swingmeter.tok</c>
/// defines the 15 named swing meters (<c>METERCREATE: "pound1" "Masher"</c> …),
/// but <c>skills.tok</c> also uses meter kinds that are not declared there
/// (<c>Chain</c>, <c>ChainSlow</c>, <c>Basic</c>, <c>Breakout</c>, <c>None</c>), so
/// observed <c>SKILLMETER</c> values are unioned in. The legacy
/// <c>skillmeter.tok</c> filename is accepted for older/modded layouts.
/// </para>
/// <para>
/// Animation-name resolution (SKILLUSECLASS → classdefs mesh → <c>.pan</c>) stays
/// in the shell for now: it needs <c>classdefs.tok</c>, which Phase 3 brings into
/// Core as <c>IClassesApi</c>. Moving it early would split that work across phases.
/// </para>
/// <para>
/// Construction reads files but never throws for missing data: an unreadable or
/// absent project file simply contributes nothing, and validation degrades to the
/// catalog-only rules.
/// </para>
/// </remarks>
public sealed class SkillsContext
{
    private static readonly Encoding Enc = TokDocument.CanonicalEncoding;

    private static readonly Regex MeterCreateRx = new(
        "\\bMETERCREATE\\s*:\\s*\"(?<id>[^\"]*)\"",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex SkillMeterRx = new(
        "\\bSKILLMETER\\s*:\\s*\"(?<id>[^\"]*)\"",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private SkillsContext(IReadOnlyList<string> meterIds, IReadOnlyDictionary<int, string> lookupText)
    {
        MeterIds = meterIds;
        LookupText = lookupText;
    }

    /// <summary>A context with no project data (validation falls back to catalog rules only).</summary>
    public static SkillsContext Empty { get; } = new(
        Array.Empty<string>(),
        new Dictionary<int, string>());

    /// <summary>Valid <c>SKILLMETER</c> names, <c>None</c> first.</summary>
    public IReadOnlyList<string> MeterIds { get; }

    /// <summary>Lookup-text id → text table; empty when the file is absent.</summary>
    public IReadOnlyDictionary<int, string> LookupText { get; }

    /// <summary>True when at least one meter name was resolved.</summary>
    public bool HasMeterIds => MeterIds.Count > 0;

    /// <summary>True when the lookup table loaded (enables dangling-id checks).</summary>
    public bool HasLookupText => LookupText.Count > 0;

    /// <summary>
    /// Build a context from in-memory data (tests, MCP callers that already hold
    /// these tables). <see cref="Load"/> is the normal path for a project.
    /// </summary>
    public static SkillsContext FromData(
        IReadOnlyList<string>? meterIds = null,
        IReadOnlyDictionary<int, string>? lookupText = null) =>
        new(meterIds ?? Array.Empty<string>(),
            lookupText ?? new Dictionary<int, string>());

    /// <summary>
    /// Build the context for a project. <paramref name="projectPath"/> is the
    /// project's BEC root (the folder containing <c>data/</c>).
    /// </summary>
    /// <param name="projectPath">BEC root of the project.</param>
    /// <param name="lookupTextApi">Optional lookup API override (tests / callers that already loaded it).</param>
    public static SkillsContext Load(string projectPath, ILookupTextApi? lookupTextApi = null)
    {
        if (string.IsNullOrWhiteSpace(projectPath))
            throw new ArgumentException("Project path is required.", nameof(projectPath));

        string configDir = Path.Combine(projectPath, "data", "config");
        var meters = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddMeter(string? id)
        {
            if (string.IsNullOrWhiteSpace(id)) return;
            string trimmed = id.Trim();
            if (seen.Add(trimmed)) meters.Add(trimmed);
        }

        foreach (string id in ReadMeterIds(Path.Combine(configDir, "swingmeter.tok"))) AddMeter(id);
        foreach (string id in ReadMeterIds(Path.Combine(configDir, "skillmeter.tok"))) AddMeter(id);
        foreach (string id in ReadObservedMeters(Path.Combine(configDir, "skills.tok"))) AddMeter(id);

        // "None" is the dominant vanilla value; keep it at the top of the list.
        meters.RemoveAll(m => string.Equals(m, "None", StringComparison.OrdinalIgnoreCase));
        meters.Insert(0, "None");

        var lookup = new Dictionary<int, string>();
        try
        {
            var api = lookupTextApi ?? new LookupTextApi();
            foreach (var kvp in api.Load(projectPath)) lookup[kvp.Key] = kvp.Value;
        }
        catch (FileNotFoundException)
        {
            // No lookup table in this project — dangling-id checks are skipped.
        }
        catch (IOException)
        {
        }

        return new SkillsContext(meters, lookup);
    }

    private static IEnumerable<string> ReadMeterIds(string path)
    {
        if (!File.Exists(path)) yield break;

        IEnumerable<string> lines;
        try
        {
            lines = File.ReadLines(path, Enc);
        }
        catch (IOException)
        {
            yield break;
        }

        foreach (string line in lines)
        {
            var m = MeterCreateRx.Match(line);
            if (m.Success) yield return m.Groups["id"].Value;
        }
    }

    private static IEnumerable<string> ReadObservedMeters(string path)
    {
        if (!File.Exists(path)) yield break;

        IEnumerable<string> lines;
        try
        {
            lines = File.ReadLines(path, Enc);
        }
        catch (IOException)
        {
            yield break;
        }

        foreach (string line in lines)
        {
            var m = SkillMeterRx.Match(line);
            if (m.Success) yield return m.Groups["id"].Value;
        }
    }
}
