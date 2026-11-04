namespace ModdingGUI.Core.Api;

using System.Collections.Generic;

/// <summary>
/// Read/write API for the <c>data/config/lookuptext_&lt;lang&gt;.txt</c> tables
/// (English, French, German, Italian, Spanish).
/// </summary>
/// <remarks>
/// The single-language members are the Phase 2.6 bridge used by the item editor and
/// always operate on English; the language-aware members are the Phase 3 API. Every
/// write takes the project lock and backs the file up into <c>.mod-history/</c>.
/// </remarks>
public interface ILookupTextApi
{
    /// <summary>Load the English id-to-text table.</summary>
    IReadOnlyDictionary<int, string> Load(string projectPath);

    /// <summary>Update an English text id. Returns false when the id is absent.</summary>
    bool Update(string projectPath, int id, string text);

    /// <summary>Append a new English text entry and return its allocated id.</summary>
    int Add(string projectPath, string text);

    /// <summary>Load one language's id-to-text table.</summary>
    IReadOnlyDictionary<int, string> Load(string projectPath, LookupLanguage language);

    /// <summary>Update one language's text id. Returns false when the id is absent.</summary>
    bool Update(string projectPath, int id, string text, LookupLanguage language);

    /// <summary>Append a new entry to one language's table and return its allocated id.</summary>
    int Add(string projectPath, string text, LookupLanguage language);

    /// <summary>Languages whose table exists in the project.</summary>
    IReadOnlyList<LookupLanguage> AvailableLanguages(string projectPath);
}
