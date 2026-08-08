namespace ModdingGUI.Core.Api;

using System.Collections.Generic;

/// <summary>
/// Read/write API for <c>items.tok</c> in a single project. Every method
/// acquires the project lock for the duration of its call and writes a backup
/// under <c>&lt;project&gt;/.mod-history/</c> before overwriting the file.
/// </summary>
/// <remarks>
/// The API is stateless across calls: each call opens the file, mutates the
/// in-memory document, and writes it back. Callers should batch multi-record
/// edits through a single entry point when possible (e.g.
/// <see cref="Patch"/> accepts one record per call, but the underlying tok
/// parse+write cost is dominated by the batch).
/// </remarks>
public interface IItemsApi
{
    /// <summary>Return every item's stable id (Name) in source order.</summary>
    IReadOnlyList<string> List(string projectPath);

    /// <summary>
    /// Return all items as DTOs in source order. Reads the file exactly once;
    /// prefer this over <see cref="List"/> + N×<see cref="Get"/> for bulk loads.
    /// </summary>
    IReadOnlyList<ItemDto> GetAll(string projectPath);

    /// <summary>Return the item with the given stable id, or null if absent.</summary>
    ItemDto? Get(string projectPath, string name);

    /// <summary>
    /// Apply <paramref name="patch"/> to the item named <paramref name="name"/>.
    /// Removes run before adds on each list field. Returns the updated item.
    /// </summary>
    ItemDto Patch(string projectPath, string name, ItemPatch patch);

    /// <summary>Append a new item record with the given initial field values.</summary>
    ItemDto Add(string projectPath, ItemDto item);

    /// <summary>Delete the item with the given stable id. Returns true if found.</summary>
    bool Remove(string projectPath, string name);

    /// <summary>
    /// Change an item's stable id. Throws if <paramref name="newName"/> is
    /// already in use by another item in the same file.
    /// </summary>
    ItemDto Rename(string projectPath, string oldName, string newName);
}
