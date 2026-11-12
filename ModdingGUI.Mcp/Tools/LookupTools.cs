namespace ModdingGUI.Mcp.Tools;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using ModelContextProtocol.Server;
using ModdingGUI.Core.Api;
using ModdingGUI.Mcp.Server;

/// <summary>
/// <c>data/config/lookuptext_&lt;lang&gt;.txt</c>: the id-to-text table five localisations share.
/// </summary>
/// <remarks>
/// The files are ISO-8859-1 (the corpus is Latin-1, not UTF-8). The Core API reads and writes them with that
/// encoding, so text passed through these tools is stored verbatim; nothing here decodes or normalises.
/// There is no advisory validator for this family.
/// </remarks>
[McpServerToolType]
public sealed class LookupTools(McpProject project)
{
    /// <summary>Every entry of one language's table.</summary>
    [McpServerTool(Name = "lookup_load", ReadOnly = true, Idempotent = true)]
    [Description(
        "Read the English lookup-text table (lookuptext_eng.txt) as {id, text} entries sorted by id. This is the " +
        "table item names, skill names, league and encounter display names resolve through. The table is large " +
        "(thousands of entries); lookup_load_language reads another language.")]
    public object Load() =>
        ToolResult.Call(project, "lookup_load", () => Entries(project.LookupText.Load(project.BecPath)));

    /// <summary>Every entry of one language's table.</summary>
    [McpServerTool(Name = "lookup_load_language")]
    [Description("Read one language's lookup-text table as {id, text} entries sorted by id.")]
    public object LoadLanguage(
        [Description("Language: eng, fra, ger, ita or spa (English/French/German/Italian/Spanish).")] string language) =>
        ToolResult.Call(project, $"lookup_load_language '{language}'", () =>
            Entries(project.LookupText.Load(project.BecPath, ParseLanguage(language))));

    /// <summary>Update one English entry.</summary>
    [McpServerTool(Name = "lookup_update")]
    [Description(
        "Replace the text of one English lookup id and return true, or false when the id is absent (use " +
        "lookup_add for a new entry). Writes are ISO-8859-1: characters outside that encoding cannot be " +
        "represented and the file's encoding wins, so stick to Latin-1 text.")]
    public object Update(
        [Description("Lookup-text id, e.g. 1683.")] int id,
        [Description("New text, without quotes.")] string text,
        [Description("lookup-text files have no advisory validator, so strict cannot refuse this edit.")] bool strict = false) =>
        project.Write($"lookup_update {id}", () => project.LookupText.Update(project.BecPath, id, text), strict);

    /// <summary>Update one entry of one language.</summary>
    [McpServerTool(Name = "lookup_update_language")]
    [Description("Replace the text of one id in one language's table and return true, or false when the id is absent.")]
    public object UpdateLanguage(
        [Description("Lookup-text id.")] int id,
        [Description("New text, without quotes.")] string text,
        [Description("Language: eng, fra, ger, ita or spa.")] string language,
        [Description("lookup-text files have no advisory validator, so strict cannot refuse this edit.")] bool strict = false) =>
        project.Write($"lookup_update_language {id} [{language}]",
            () => project.LookupText.Update(project.BecPath, id, text, ParseLanguage(language)), strict);

    /// <summary>Append an English entry.</summary>
    [McpServerTool(Name = "lookup_add")]
    [Description("Append a new English text entry and return the id that was allocated for it.")]
    public object Add(
        [Description("Text for the new entry, without quotes.")] string text,
        [Description("lookup-text files have no advisory validator, so strict cannot refuse this edit.")] bool strict = false) =>
        project.Write("lookup_add", () => project.LookupText.Add(project.BecPath, text), strict);

    /// <summary>Append an entry to one language.</summary>
    [McpServerTool(Name = "lookup_add_language")]
    [Description("Append a new entry to one language's table and return the id that was allocated for it.")]
    public object AddLanguage(
        [Description("Text for the new entry, without quotes.")] string text,
        [Description("Language: eng, fra, ger, ita or spa.")] string language,
        [Description("lookup-text files have no advisory validator, so strict cannot refuse this edit.")] bool strict = false) =>
        project.Write($"lookup_add_language [{language}]",
            () => project.LookupText.Add(project.BecPath, text, ParseLanguage(language)), strict);

    /// <summary>Which language tables the project has.</summary>
    [McpServerTool(Name = "lookup_available_languages", ReadOnly = true, Idempotent = true)]
    [Description("List the lookup-text languages whose file exists in this project, as three-letter codes (eng, fra, …).")]
    public object AvailableLanguages() =>
        ToolResult.Call(project, "lookup_available_languages", () =>
            project.LookupText.AvailableLanguages(project.BecPath)
                .Select(language => LookupLanguages.Suffix(language))
                .ToArray());

    private static LookupEntry[] Entries(IReadOnlyDictionary<int, string> table) =>
        table.OrderBy(pair => pair.Key)
            .Select(pair => new LookupEntry(pair.Key, pair.Value))
            .ToArray();

    private static LookupLanguage ParseLanguage(string language)
    {
        string text = (language ?? string.Empty).Trim();
        foreach (LookupLanguage candidate in LookupLanguages.All)
        {
            if (candidate.ToString().Equals(text, StringComparison.OrdinalIgnoreCase)
                || LookupLanguages.Suffix(candidate).Equals(text, StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        throw new McpToolException(
            $"unknown language '{language}'; use eng, fra, ger, ita or spa (or the English name)");
    }
}

/// <summary>One lookup-text row.</summary>
/// <param name="Id">Numeric id the game references.</param>
/// <param name="Text">The text, as stored.</param>
public sealed record LookupEntry(int Id, string Text);
