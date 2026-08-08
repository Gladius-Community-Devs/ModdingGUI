namespace ModdingGUI.Core.Api;

using System;
using System.Collections.Generic;
using System.IO;
using ModdingGUI.Core.Formats.Items;
using ModdingGUI.Core.Schemas;
using ModdingGUI.Core.Tok;

/// <summary>
/// Default <see cref="IItemsApi"/> implementation. Locked, stateless, and
/// backup-before-write: every mutation (1) acquires
/// <see cref="ProjectLock"/>, (2) reads the file into a <see cref="TokDocument"/>,
/// (3) mutates via <see cref="ItemsSchema"/>'s typed record layer, (4) calls
/// <see cref="ChangeHistoryService.Backup"/>, and (5) writes the document
/// byte-for-byte back to the target file.
/// </summary>
/// <remarks>
/// The file location is conventionally <c>&lt;projectPath&gt;/data/config/items.tok</c>
/// under the project's BEC directory. This API takes the project root as input
/// and computes the canonical relative path itself; alternative layouts are
/// not supported in Phase 2.
/// </remarks>
public sealed class ItemsApi : IItemsApi
{
    /// <summary>Canonical relative path inside a project's BEC tree.</summary>
    public const string RelativePath = "data/config/items.tok";

    private readonly RecordSchema<Item> _schema;

    public ItemsApi()
    {
        // Registry is consulted but we also keep a direct reference for speed;
        // Core.Initialize is idempotent so it is safe to call here.
        Core.Initialize();
        _schema = FileFormatRegistry.TryGet<Item>("items")
            ?? throw new InvalidOperationException("Items schema not registered.");
    }

    /// <inheritdoc />
    public IReadOnlyList<string> List(string projectPath)
    {
        using (ProjectLock.Acquire(projectPath))
        {
            var (_, set) = Open(projectPath);
            var ids = new List<string>(set.Records.Count);
            foreach (var v in set.Records) ids.Add(v.StableId);
            return ids;
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<ItemDto> GetAll(string projectPath)
    {
        using (ProjectLock.Acquire(projectPath))
        {
            var (_, set) = Open(projectPath);
            var result = new List<ItemDto>(set.Records.Count);
            foreach (var v in set.Records) result.Add(ToDto(v.Record));
            return result;
        }
    }

    /// <inheritdoc />
    public ItemDto? Get(string projectPath, string name)
    {
        using (ProjectLock.Acquire(projectPath))
        {
            var (_, set) = Open(projectPath);
            var view = set.FindByStableId(name);
            return view is null ? null : ToDto(view.Record);
        }
    }

    /// <inheritdoc />
    public ItemDto Patch(string projectPath, string name, ItemPatch patch)
    {
        if (patch is null) throw new ArgumentNullException(nameof(patch));

        using (ProjectLock.Acquire(projectPath))
        {
            var (doc, set) = Open(projectPath);
            var view = set.FindByStableId(name)
                ?? throw new KeyNotFoundException($"Item '{name}' not found.");

            ApplyPatch(view, patch);

            WriteBack(projectPath, doc);
            return ToDto(view.Record);
        }
    }

    /// <inheritdoc />
    public ItemDto Add(string projectPath, ItemDto item)
    {
        if (item is null) throw new ArgumentNullException(nameof(item));
        if (string.IsNullOrWhiteSpace(item.Name))
            throw new ArgumentException("ItemDto.Name is required.", nameof(item));

        using (ProjectLock.Acquire(projectPath))
        {
            var (doc, set) = Open(projectPath);
            if (set.FindByStableId(item.Name) is not null)
                throw new InvalidOperationException($"Item '{item.Name}' already exists.");

            var record = FromDto(item);
            var view = set.AddRecord(record);

            WriteBack(projectPath, doc);
            return ToDto(view.Record);
        }
    }

    /// <inheritdoc />
    public bool Remove(string projectPath, string name)
    {
        using (ProjectLock.Acquire(projectPath))
        {
            var (doc, set) = Open(projectPath);
            bool removed = set.RemoveRecord(name);
            if (removed) WriteBack(projectPath, doc);
            return removed;
        }
    }

    /// <inheritdoc />
    public ItemDto Rename(string projectPath, string oldName, string newName)
    {
        using (ProjectLock.Acquire(projectPath))
        {
            var (doc, set) = Open(projectPath);
            var view = set.Rename(oldName, newName);
            WriteBack(projectPath, doc);
            return ToDto(view.Record);
        }
    }

    // ---- internals ------------------------------------------------------

    private (TokDocument doc, RecordSet<Item> set) Open(string projectPath)
    {
        string path = Path.Combine(projectPath, RelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path))
            throw new FileNotFoundException("items.tok not found.", path);
        byte[] bytes = File.ReadAllBytes(path);
        var doc = TokReader.Read(bytes, path);
        var set = _schema.Read(doc);
        return (doc, set);
    }

    private static void WriteBack(string projectPath, TokDocument doc)
    {
        string path = Path.Combine(projectPath, RelativePath.Replace('/', Path.DirectorySeparatorChar));
        ChangeHistoryService.Backup(projectPath, path);
        byte[] output = TokWriter.Write(doc);
        File.WriteAllBytes(path, output);
    }

    private static void ApplyPatch(RecordView<Item> view, ItemPatch p)
    {
        // Header positionals.
        if (p.Type is not null) view.SetHeaderPositional(nameof(Item.Type), p.Type);
        if (p.SubType is not null) view.SetHeaderPositional(nameof(Item.SubType), p.SubType);
        if (p.Style is not null) view.SetHeaderPositional(nameof(Item.Style), p.Style);
        if (p.StarRating.HasValue) view.SetHeaderPositional(nameof(Item.StarRating), p.StarRating.Value);

        // Scalar body keys.
        if (p.DescriptionId.HasValue) view.SetField("ITEMDESCRIPTIONID", p.DescriptionId.Value);
        if (p.DisplayNameId.HasValue) view.SetField("ITEMDISPLAYNAMEID", p.DisplayNameId.Value);
        if (p.Cost.HasValue) view.SetField("ITEMCOST", p.Cost.Value);
        if (p.MinLevel.HasValue) view.SetField("ITEMMINLEVEL", p.MinLevel.Value);
        if (p.Rarity is not null) view.SetField("ITEMRARITY", p.Rarity);
        if (p.Mesh is not null) view.SetField("ITEMMESH", p.Mesh);
        if (p.Mesh2 is not null) view.SetField("ITEMMESH2", p.Mesh2);
        if (p.Material is not null) view.SetField("ITEMMATERIAL", p.Material);
        if (p.ClearAffinity)
            view.ClearField("ITEMAFFINITY");
        else if (p.Affinity.HasValue)
        {
            var a = p.Affinity.Value;
            view.SetField<(string, long)>("ITEMAFFINITY", (a.Element, a.Amount));
        }
        if (p.ShowSet is not null) view.SetField("ITEMSHOWSET", p.ShowSet);

        // List body keys: removes first, then adds.
        ApplyListPatch(view, "ITEMREGION", p.RegionsRemove, p.RegionsAdd);
        ApplyListPatch(view, "ITEMSKILL", p.SkillsRemove, p.SkillsAdd);
        ApplyListPatch(view, "ITEMATTRIBUTE", p.AttributesRemove, p.AttributesAdd);
        ApplyListPatch(view, "ITEMHIDESET", p.HideSetsRemove, p.HideSetsAdd);

        if (p.StatModsRemove is not null)
            foreach (var s in p.StatModsRemove)
                view.RemoveListItem<(string, long)>("ITEMSTATMOD", (s.Stat, s.Value));
        if (p.StatModsAdd is not null)
            foreach (var s in p.StatModsAdd)
                view.AddListItem<(string, long)>("ITEMSTATMOD", (s.Stat, s.Value));
    }

    private static void ApplyListPatch(
        RecordView<Item> view, string key,
        List<string>? removes, List<string>? adds)
    {
        if (removes is not null)
            foreach (var v in removes) view.RemoveListItem(key, v);
        if (adds is not null)
            foreach (var v in adds) view.AddListItem(key, v);
    }

    // ---- DTO <-> record mapping ----------------------------------------

    private static ItemDto ToDto(Item r)
    {
        var dto = new ItemDto
        {
            Name = r.Name.HasValue ? r.Name.Value! : string.Empty,
            Type = r.Type.HasValue ? r.Type.Value : null,
            SubType = r.SubType.HasValue ? r.SubType.Value : null,
            Style = r.Style.HasValue ? r.Style.Value : null,
            StarRating = r.StarRating.HasValue ? r.StarRating.Value : null,

            DescriptionId = r.DescriptionId.HasValue ? r.DescriptionId.Value : null,
            DisplayNameId = r.DisplayNameId.HasValue ? r.DisplayNameId.Value : null,
            Cost = r.Cost.HasValue ? r.Cost.Value : null,
            MinLevel = r.MinLevel.HasValue ? r.MinLevel.Value : null,
            Rarity = r.Rarity.HasValue ? r.Rarity.Value : null,
            Mesh = r.Mesh.HasValue ? r.Mesh.Value : null,
            Mesh2 = r.Mesh2.HasValue ? r.Mesh2.Value : null,
            Material = r.Material.HasValue ? r.Material.Value : null,
            Affinity = r.Affinity.HasValue
                ? (r.Affinity.Value.Item1, r.Affinity.Value.Item2)
                : ((string, long)?)null,
            ShowSet = r.ShowSet.HasValue ? r.ShowSet.Value : null,
        };
        foreach (var v in r.Regions) dto.Regions.Add(v);
        foreach (var v in r.Skills) dto.Skills.Add(v);
        foreach (var v in r.Attributes) dto.Attributes.Add(v);
        foreach (var v in r.HideSets) dto.HideSets.Add(v);
        foreach (var v in r.StatMods) dto.StatMods.Add((v.Item1, v.Item2));
        return dto;
    }

    private static Item FromDto(ItemDto d)
    {
        var r = new Item();
        r.Name.Set(d.Name);
        if (d.Type is not null) r.Type.Set(d.Type);
        if (d.SubType is not null) r.SubType.Set(d.SubType);
        if (d.Style is not null) r.Style.Set(d.Style);
        if (d.StarRating.HasValue) r.StarRating.Set(d.StarRating.Value);

        if (d.DescriptionId.HasValue) r.DescriptionId.Set(d.DescriptionId.Value);
        if (d.DisplayNameId.HasValue) r.DisplayNameId.Set(d.DisplayNameId.Value);
        if (d.Cost.HasValue) r.Cost.Set(d.Cost.Value);
        if (d.MinLevel.HasValue) r.MinLevel.Set(d.MinLevel.Value);
        if (d.Rarity is not null) r.Rarity.Set(d.Rarity);
        if (d.Mesh is not null) r.Mesh.Set(d.Mesh);
        if (d.Mesh2 is not null) r.Mesh2.Set(d.Mesh2);
        if (d.Material is not null) r.Material.Set(d.Material);
        if (d.Affinity.HasValue) r.Affinity.Set((d.Affinity.Value.Element, d.Affinity.Value.Amount));
        if (d.ShowSet is not null) r.ShowSet.Set(d.ShowSet);

        foreach (var v in d.Regions) r.Regions.Add(v);
        foreach (var v in d.Skills) r.Skills.Add(v);
        foreach (var v in d.Attributes) r.Attributes.Add(v);
        foreach (var v in d.HideSets) r.HideSets.Add(v);
        foreach (var v in d.StatMods) r.StatMods.Add((v.Stat, v.Value));
        return r;
    }
}
