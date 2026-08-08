namespace ModdingGUI.Core.Api;

using System.Collections.Generic;

/// <summary>
/// Read/write API for <c>data/config/lookuptext_eng.txt</c>. This is a narrow
/// Phase 2.6 bridge for Item Editor display-name/description text edits; the
/// broader lookup-text API lands in Phase 3.
/// </summary>
public interface ILookupTextApi
{
    /// <summary>Load the flat id-to-text table.</summary>
    IReadOnlyDictionary<int, string> Load(string projectPath);

    /// <summary>Update an existing text id. Returns false when the id is absent.</summary>
    bool Update(string projectPath, int id, string text);

    /// <summary>Append a new text entry and return its allocated id.</summary>
    int Add(string projectPath, string text);
}
