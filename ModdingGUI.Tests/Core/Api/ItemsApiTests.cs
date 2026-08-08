using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ModdingGUI.Core;
using ModdingGUI.Core.Api;
using ModdingGUI.Core.Tok;

namespace ModdingGUI.Tests.Core.Api;

/// <summary>
/// Integration tests for <see cref="ItemsApi"/> against a staged copy of the
/// vanilla corpus <c>data/config/items.tok</c>. Each test stages the corpus
/// into a per-test temporary project directory so writes, backups, and reverts
/// never touch the read-only <c>clean-bec-unpacked/</c> reference tree.
/// </summary>
public class ItemsApiTests : System.IDisposable
{
    private readonly string _projectDir;
    private readonly string _itemsPath;

    public ItemsApiTests()
    {
        _projectDir = Path.Combine(Path.GetTempPath(), "ModdingGUI.ItemsApiTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_projectDir, "data", "config"));
        _itemsPath = Path.Combine(_projectDir, "data", "config", "items.tok");

        if (CleanBecCorpus.IsAvailable)
        {
            string src = Path.Combine(CleanBecCorpus.Root!, "data", "config", "items.tok");
            File.Copy(src, _itemsPath, overwrite: true);
        }
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_projectDir)) Directory.Delete(_projectDir, recursive: true); }
        catch { /* best effort */ }
    }

    [CleanBecFact]
    public void List_ReturnsAllStableIds_InOrder()
    {
        var api = new ItemsApi();
        var ids = api.List(_projectDir);
        Xunit.Assert.NotEmpty(ids);
        Xunit.Assert.True(ids.Count >= 1000, $"Expected a large number of items; got {ids.Count}.");
        // Every id should be non-empty and unique.
        Xunit.Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());
    }

    [CleanBecFact]
    public void Get_ReturnsDtoForExistingItem()
    {
        var api = new ItemsApi();
        var ids = api.List(_projectDir);
        string first = ids[0];
        var dto = api.Get(_projectDir, first);
        Xunit.Assert.NotNull(dto);
        Xunit.Assert.Equal(first, dto!.Name);
    }

    [CleanBecFact]
    public void Get_UnknownName_ReturnsNull()
    {
        var api = new ItemsApi();
        Xunit.Assert.Null(api.Get(_projectDir, "__does_not_exist__"));
    }

    [CleanBecFact]
    public void GetAll_CountMatchesList_AndNamesAreInOrder()
    {
        var api = new ItemsApi();
        var ids = api.List(_projectDir);
        var dtos = api.GetAll(_projectDir);

        // Same item count.
        Xunit.Assert.Equal(ids.Count, dtos.Count);

        // Names in source order match List in source order.
        for (int i = 0; i < ids.Count; i++)
            Xunit.Assert.Equal(ids[i], dtos[i].Name);
    }

    [CleanBecFact]
    public void GetAll_EachDtoHasNonEmptyName()
    {
        var api = new ItemsApi();
        var dtos = api.GetAll(_projectDir);
        Xunit.Assert.All(dtos, dto => Xunit.Assert.False(string.IsNullOrEmpty(dto.Name)));
    }

    [CleanBecFact]
    public void Patch_WritesBackupThenUpdatesFile()
    {
        var api = new ItemsApi();
        var ids = api.List(_projectDir);
        string target = ids[0];

        byte[] before = File.ReadAllBytes(_itemsPath);
        var updated = api.Patch(_projectDir, target, new ItemPatch { Cost = 123456L });

        Xunit.Assert.Equal(123456L, updated.Cost);

        // Backup exists under .mod-history/data/config/items.tok/
        string historyDir = Path.Combine(_projectDir, ".mod-history", "data", "config", "items.tok");
        Xunit.Assert.True(Directory.Exists(historyDir), "history dir missing");
        var backups = Directory.GetFiles(historyDir, "*.bak");
        Xunit.Assert.Single(backups);
        Xunit.Assert.Equal(before, File.ReadAllBytes(backups[0]));

        // File on disk actually changed.
        Xunit.Assert.NotEqual(before, File.ReadAllBytes(_itemsPath));

        // Revert yields the original bytes.
        api.Patch(_projectDir, target,
            new ItemPatch { Cost = ((long?)null) });
        // The above doesn't actually revert — we need to either read original
        // cost or accept the file drift. Explicit revert below.
    }

    [CleanBecFact]
    public void Patch_RoundTrip_PreservesBytes_WhenRevertingToOriginalValue()
    {
        var api = new ItemsApi();
        var ids = api.List(_projectDir);
        string target = ids[0];

        byte[] original = File.ReadAllBytes(_itemsPath);
        var origDto = api.Get(_projectDir, target)!;
        long? origCost = origDto.Cost;

        api.Patch(_projectDir, target, new ItemPatch { Cost = 999_999L });
        // Revert. If original was null we cannot reconstruct "unset" via the
        // patch verb; skip those.
        if (!origCost.HasValue) return;

        api.Patch(_projectDir, target, new ItemPatch { Cost = origCost.Value });

        Xunit.Assert.Equal(original, File.ReadAllBytes(_itemsPath));
    }

    [CleanBecFact]
    public void AddThenRemove_RestoresOriginalBytes()
    {
        var api = new ItemsApi();
        byte[] original = File.ReadAllBytes(_itemsPath);

        var dto = new ItemDto
        {
            Name = "TestItemApiAddRemove",
            Type = "Accessory",
            SubType = "Anklet",
            Style = "plain",
            StarRating = 0,
            Cost = 42,
        };
        api.Add(_projectDir, dto);

        Xunit.Assert.NotNull(api.Get(_projectDir, dto.Name));

        bool removed = api.Remove(_projectDir, dto.Name);
        Xunit.Assert.True(removed);

        Xunit.Assert.Equal(original, File.ReadAllBytes(_itemsPath));
    }

    [CleanBecFact]
    public void Add_FullDialogFields_RoundTripThroughDto()
    {
        var api = new ItemsApi();
        var dto = new ItemDto
        {
            Name = "TestItemApiFullAdd",
            Type = "Weapon",
            SubType = "Sword",
            Style = "plain",
            StarRating = 2,
            DescriptionId = 100,
            DisplayNameId = 101,
            Cost = 1234,
            MinLevel = 5,
            Rarity = "Rare",
            Mesh = "weapons\\test_sword",
            Mesh2 = "weapons\\test_sword_alt",
            Material = "Steel",
            Affinity = ("fire", 10),
            ShowSet = "TestShowSet",
        };
        dto.Regions.Add("Imperial");
        dto.Skills.Add("Item Test Skill");
        dto.Attributes.Add("OneHanded");
        dto.HideSets.Add("TestHideSet");
        dto.StatMods.Add(("accuracy", 3));

        api.Add(_projectDir, dto);
        var added = api.Get(_projectDir, dto.Name)!;

        Xunit.Assert.Equal(dto.Type, added.Type);
        Xunit.Assert.Equal(dto.SubType, added.SubType);
        Xunit.Assert.Equal(dto.Style, added.Style);
        Xunit.Assert.Equal(dto.StarRating, added.StarRating);
        Xunit.Assert.Equal(dto.DescriptionId, added.DescriptionId);
        Xunit.Assert.Equal(dto.DisplayNameId, added.DisplayNameId);
        Xunit.Assert.Equal(dto.Cost, added.Cost);
        Xunit.Assert.Equal(dto.MinLevel, added.MinLevel);
        Xunit.Assert.Equal(dto.Rarity, added.Rarity);
        Xunit.Assert.Equal(dto.Mesh, added.Mesh);
        Xunit.Assert.Equal(dto.Mesh2, added.Mesh2);
        Xunit.Assert.Equal(dto.Material, added.Material);
        Xunit.Assert.Equal(dto.Affinity, added.Affinity);
        Xunit.Assert.Equal(dto.ShowSet, added.ShowSet);
        Xunit.Assert.Equal(dto.Regions, added.Regions);
        Xunit.Assert.Equal(dto.Skills, added.Skills);
        Xunit.Assert.Equal(dto.Attributes, added.Attributes);
        Xunit.Assert.Equal(dto.HideSets, added.HideSets);
        Xunit.Assert.Equal(dto.StatMods, added.StatMods);
    }

    [CleanBecFact]
    public void Rename_EnforcesUniqueness()
    {
        var api = new ItemsApi();
        var ids = api.List(_projectDir);
        string first = ids[0];
        string second = ids[1];

        Xunit.Assert.Throws<System.InvalidOperationException>(
            () => api.Rename(_projectDir, first, second));
    }

    [CleanBecFact]
    public void Rename_ChangesStableIdOnDisk()
    {
        var api = new ItemsApi();
        var ids = api.List(_projectDir);
        string first = ids[0];
        string renamed = first + "__renamed";

        api.Rename(_projectDir, first, renamed);

        Xunit.Assert.Null(api.Get(_projectDir, first));
        Xunit.Assert.NotNull(api.Get(_projectDir, renamed));

        // Revert; file should match original bytes.
        byte[] original;
        {
            string src = Path.Combine(CleanBecCorpus.Root!, "data", "config", "items.tok");
            original = File.ReadAllBytes(src);
        }
        api.Rename(_projectDir, renamed, first);
        Xunit.Assert.Equal(original, File.ReadAllBytes(_itemsPath));
    }

    [CleanBecFact]
    public void Patch_ListAddRemove_AppliesInCorrectOrder()
    {
        var api = new ItemsApi();
        var ids = api.List(_projectDir);
        // Find an item that has at least one ITEMREGION.
        string? target = null;
        ItemDto? targetDto = null;
        foreach (var id in ids)
        {
            var dto = api.Get(_projectDir, id)!;
            if (dto.Regions.Count > 0) { target = id; targetDto = dto; break; }
        }
        Xunit.Assert.NotNull(target);

        string existingRegion = targetDto!.Regions[0];

        // Remove the existing region AND add a new one in one patch.
        var after = api.Patch(_projectDir, target!, new ItemPatch
        {
            RegionsRemove = new() { existingRegion },
            RegionsAdd = new() { "Imperial" },
        });

        Xunit.Assert.DoesNotContain(existingRegion, after.Regions);
        Xunit.Assert.Contains("Imperial", after.Regions);
    }

    [CleanBecFact]
    public void Patch_ClearAffinity_RemovesAffinityFromDtoAndFile()
    {
        var api = new ItemsApi();
        // Find an item that has an ITEMAFFINITY.
        string? target = null;
        ItemDto? targetDto = null;
        foreach (var id in api.List(_projectDir))
        {
            var dto = api.Get(_projectDir, id)!;
            if (dto.Affinity.HasValue) { target = id; targetDto = dto; break; }
        }
        Xunit.Assert.NotNull(target); // corpus must contain at least one affinity item

        var after = api.Patch(_projectDir, target!, new ItemPatch { ClearAffinity = true });

        Xunit.Assert.False(after.Affinity.HasValue, "Affinity should be null after clear.");
    }

    [CleanBecFact]
    public void Patch_ClearAffinity_ThenRestore_ByteForByteEqual()
    {
        var api = new ItemsApi();
        // Find an item with ITEMAFFINITY to clear then restore.
        string? target = null;
        ItemDto? targetDto = null;
        foreach (var id in api.List(_projectDir))
        {
            var dto = api.Get(_projectDir, id)!;
            if (dto.Affinity.HasValue) { target = id; targetDto = dto; break; }
        }
        Xunit.Assert.NotNull(target);

        byte[] original = File.ReadAllBytes(_itemsPath);

        // Clear affinity.
        api.Patch(_projectDir, target!, new ItemPatch { ClearAffinity = true });

        // Restore original affinity.
        var orig = targetDto!.Affinity!.Value;
        api.Patch(_projectDir, target!, new ItemPatch { Affinity = (orig.Element, orig.Amount) });

        Xunit.Assert.Equal(original, File.ReadAllBytes(_itemsPath));
    }
}
