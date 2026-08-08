using ModdingGUI.Core.Formats.Items;
using ModdingGUI.Core.Tok;

namespace ModdingGUI.Tests.Core.Schemas.Formats;

/// <summary>
/// Read-only validation of <see cref="ItemsSchema"/> over the entire vanilla
/// <c>data/config/items.tok</c>. Confirms:
/// </summary>
/// <list type="bullet">
///   <item>Exactly <c>NUMENTRIES</c> (1852) records are discovered.</item>
///   <item>Every record has a non-empty <see cref="Item.Name"/> (stable id).</item>
///   <item>All scalar body fields decode without errors where they appear.</item>
///   <item>Re-serialising the underlying <see cref="TokDocument"/> without mutation
///         produces byte-identical output to the original file (the Phase 1 gate
///         reaffirmed — ensures the schema does not perturb the document).</item>
/// </list>
public class ItemsSchemaReadTests
{
    [CleanBecFact]
    public void ItemsTok_DecodesEveryRecord_AndRoundTripsByteForByte()
    {
        string path = System.IO.Path.Combine(
            CleanBecCorpus.Root!, "data", "config", "items.tok");
        byte[] originalBytes = System.IO.File.ReadAllBytes(path);

        var doc = TokReader.Read(originalBytes, path);
        var schema = new ItemsSchema();
        var set = schema.Read(doc);

        Xunit.Assert.Equal(1852, set.Records.Count);

        foreach (var view in set.Records)
        {
            Xunit.Assert.True(view.Record.Name.HasValue,
                $"Item record at line {view.StartLineIndex + 1} has no Name.");
            Xunit.Assert.False(string.IsNullOrEmpty(view.Record.Name.Value),
                $"Item record at line {view.StartLineIndex + 1} has empty Name.");
        }

        // Structural guarantee: unchanged reading leaves the TokDocument intact.
        byte[] rebuilt = TokWriter.Write(doc);

        Xunit.Assert.Equal(originalBytes.Length, rebuilt.Length);
        // Byte-for-byte equality: rely on Phase 1 gate already passing globally,
        // but verify locally too so a regression here is caught fast.
        for (int i = 0; i < originalBytes.Length; i++)
        {
            if (originalBytes[i] != rebuilt[i])
                Xunit.Assert.Fail($"Byte mismatch at offset {i}: original=0x{originalBytes[i]:X2}, rebuilt=0x{rebuilt[i]:X2}");
        }
    }

    [CleanBecFact]
    public void ItemsTok_FirstRecord_DecodesExpectedFields()
    {
        string path = System.IO.Path.Combine(
            CleanBecCorpus.Root!, "data", "config", "items.tok");
        var doc = TokReader.ReadFile(path);
        var schema = new ItemsSchema();
        var set = schema.Read(doc);

        var first = set.Records[0].Record;
        Xunit.Assert.Equal("Water Anklet of Oasis", first.Name.Value);
        Xunit.Assert.Equal("Accessory", first.Type.Value);
        Xunit.Assert.Equal("Anklet", first.SubType.Value);
        Xunit.Assert.Equal("plain", first.Style.Value);
        Xunit.Assert.Equal(0L, first.StarRating.Value);
        Xunit.Assert.Equal(1146L, first.DescriptionId.Value);
        Xunit.Assert.Equal(1683L, first.DisplayNameId.Value);
        Xunit.Assert.Equal(1188L, first.Cost.Value);
        Xunit.Assert.Equal(1L, first.MinLevel.Value);
        Xunit.Assert.Equal("Rare", first.Rarity.Value);
        Xunit.Assert.Single(first.Regions);
        Xunit.Assert.Equal("Expanse", first.Regions[0]);
        Xunit.Assert.Equal("accessories\\anklets\\anklet_oasis", first.Mesh.Value);
        Xunit.Assert.Equal("Plain", first.Material.Value);
        Xunit.Assert.True(first.Affinity.HasValue);
        Xunit.Assert.Equal(("Water", 10L), first.Affinity.Value);
    }

    [CleanBecFact]
    public void ItemsTok_EveryDistinctBodyKeyIsHandled()
    {
        string path = System.IO.Path.Combine(
            CleanBecCorpus.Root!, "data", "config", "items.tok");
        var doc = TokReader.ReadFile(path);
        var schema = new ItemsSchema();

        // Collect every distinct body key that appears inside any record.
        // Every one of them MUST be a registered body field on the schema; if not,
        // the schema is missing a field and records silently drop data on mutate.
        var allKeys = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
        bool inRecord = false;
        foreach (var line in doc.Lines)
        {
            if (line is TokKeyValueLine kv)
            {
                if (kv.Key == schema.StartKey) { inRecord = true; continue; }
                if (inRecord) allKeys.Add(kv.Key);
            }
        }

        var missing = new System.Collections.Generic.List<string>();
        foreach (var k in allKeys)
            if (!schema.BodyFields.ContainsKey(k)) missing.Add(k);

        Xunit.Assert.True(missing.Count == 0, "ItemsSchema is missing body fields for: " + string.Join(", ", missing));
    }
}
