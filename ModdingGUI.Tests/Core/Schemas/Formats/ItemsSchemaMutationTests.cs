using ModdingGUI.Core.Schemas;
using ModdingGUI.Core.Tok;

namespace ModdingGUI.Tests.Core.Schemas.Formats;

using Item = ModdingGUI.Core.Formats.Items.Item;
using ItemsSchema = ModdingGUI.Core.Formats.Items.ItemsSchema;

/// <summary>
/// Mutation-layer tests for <see cref="ItemsSchema"/>.
/// </summary>
/// <remarks>
/// <para>
/// These tests drive <see cref="RecordView{TRecord}"/> and <see cref="RecordSet{TRecord}"/>
/// mutation operations (SetField / ClearField / AddListItem / RemoveListItem /
/// SetHeaderPositional / Rename / RemoveRecord / AddRecord) against synthetic
/// documents constructed from <see cref="TokReader"/>, and verify:
/// </para>
/// <list type="bullet">
///   <item>A no-op (read + write) round-trips byte-for-byte.</item>
///   <item>Each mutation produces the expected raw line text.</item>
///   <item>Unknown body keys are preserved at their original relative position.</item>
///   <item>Line indices on adjacent records are kept correctly in sync.</item>
/// </list>
/// </remarks>
public class ItemsSchemaMutationTests
{
    private const string SampleDoc =
        "ITEMCREATE: \"Alpha\", \"Accessory\", \"Anklet\", \"plain\", 0\r\n" +
        "\tITEMDESCRIPTIONID: 100\r\n" +
        "\tITEMCOST: 200\r\n" +
        "\tITEMRARITY: \"Common\"\r\n" +
        "\tITEMREGION: \"Expanse\"\r\n" +
        "\tITEMSTATMOD: defense, 5\r\n" +
        "\r\n" +
        "\r\n" +
        "ITEMCREATE: \"Beta\", \"Weapon\", \"Sword\", \"plain\", 1\r\n" +
        "\tITEMDESCRIPTIONID: 101\r\n" +
        "\tITEMCOST: 300\r\n" +
        "\tITEMUNKNOWNKEY: \"preserve me\"\r\n" +
        "\tITEMSKILL: \"Slash\"\r\n" +
        "\tITEMSKILL: \"Cleave\"\r\n";

    private static (RecordSet<Item> set, TokDocument doc) Load(string text = SampleDoc)
    {
        var bytes = TokDocument.CanonicalEncoding.GetBytes(text);
        var doc = TokReader.Read(bytes, "synthetic.tok");
        var schema = new ItemsSchema();
        return (schema.Read(doc), doc);
    }

    private static string Dump(TokDocument doc) =>
        TokDocument.CanonicalEncoding.GetString(TokWriter.Write(doc));

    [Xunit.Fact]
    public void NoOpRoundTrip_ProducesByteIdenticalOutput()
    {
        var (_, doc) = Load();
        var rebuilt = TokWriter.Write(doc);
        var original = TokDocument.CanonicalEncoding.GetBytes(SampleDoc);
        Xunit.Assert.Equal(original, rebuilt);
    }

    [Xunit.Fact]
    public void SetField_RewritesExistingLine()
    {
        var (set, doc) = Load();
        var alpha = set.FindByStableId("Alpha")!;
        alpha.SetField("ITEMCOST", 999L);
        var s = Dump(doc);
        Xunit.Assert.Contains("\tITEMCOST: 999\r\n", s);
        Xunit.Assert.DoesNotContain("\tITEMCOST: 200\r\n", s);
    }

    [Xunit.Fact]
    public void SetField_InsertsNewLine_WhenFieldAbsent()
    {
        var (set, doc) = Load();
        var alpha = set.FindByStableId("Alpha")!;
        alpha.SetField("ITEMMINLEVEL", 7L);
        var s = Dump(doc);
        Xunit.Assert.Contains("\tITEMMINLEVEL: 7\r\n", s);

        // Second record's stable id must still resolve correctly after insertion.
        var beta = set.FindByStableId("Beta");
        Xunit.Assert.NotNull(beta);
        Xunit.Assert.Equal("Beta", beta!.StableId);
    }

    [Xunit.Fact]
    public void AddListItem_AppendsAfterExistingSameKey()
    {
        var (set, doc) = Load();
        var beta = set.FindByStableId("Beta")!;
        beta.AddListItem("ITEMSKILL", "Thrust");
        var s = Dump(doc);
        int idxSlash = s.IndexOf("ITEMSKILL: \"Slash\"");
        int idxCleave = s.IndexOf("ITEMSKILL: \"Cleave\"");
        int idxThrust = s.IndexOf("ITEMSKILL: \"Thrust\"");
        Xunit.Assert.True(idxSlash < idxCleave);
        Xunit.Assert.True(idxCleave < idxThrust);
    }

    [Xunit.Fact]
    public void RemoveListItem_RemovesAllMatchingLines()
    {
        var (set, doc) = Load();
        var beta = set.FindByStableId("Beta")!;
        // Add a duplicate to prove all-matching semantics.
        beta.AddListItem("ITEMSKILL", "Slash");
        int removed = beta.RemoveListItem("ITEMSKILL", "Slash");
        Xunit.Assert.Equal(2, removed);

        var s = Dump(doc);
        Xunit.Assert.DoesNotContain("ITEMSKILL: \"Slash\"", s);
        Xunit.Assert.Contains("ITEMSKILL: \"Cleave\"", s);
    }

    [Xunit.Fact]
    public void UnknownBodyKey_IsPreservedThroughMutation()
    {
        var (set, doc) = Load();
        var beta = set.FindByStableId("Beta")!;
        beta.SetField("ITEMCOST", 12345L);
        var s = Dump(doc);
        Xunit.Assert.Contains("ITEMUNKNOWNKEY: \"preserve me\"", s);
    }

    [Xunit.Fact]
    public void SetHeaderPositional_UpdatesNonStableField()
    {
        var (set, doc) = Load();
        var alpha = set.FindByStableId("Alpha")!;
        alpha.SetHeaderPositional("Type", "Gear");
        var s = Dump(doc);
        Xunit.Assert.Contains("ITEMCREATE: \"Alpha\", \"Gear\", \"Anklet\", \"plain\", 0\r\n", s);
    }

    [Xunit.Fact]
    public void SetHeaderPositional_StableIdThrows()
    {
        var (set, _) = Load();
        var alpha = set.FindByStableId("Alpha")!;
        Xunit.Assert.Throws<System.InvalidOperationException>(() =>
            alpha.SetHeaderPositional("Name", "Gamma"));
    }

    [Xunit.Fact]
    public void Rename_ChangesStableIdAndEnforcesUniqueness()
    {
        var (set, doc) = Load();
        var renamed = set.Rename("Alpha", "AlphaPrime");
        Xunit.Assert.Equal("AlphaPrime", renamed.StableId);
        Xunit.Assert.Contains("ITEMCREATE: \"AlphaPrime\",", Dump(doc));

        Xunit.Assert.Throws<System.InvalidOperationException>(() => set.Rename("Beta", "AlphaPrime"));
    }

    [Xunit.Fact]
    public void RemoveRecord_DeletesInclusiveRangeAndUpdatesOthers()
    {
        var (set, doc) = Load();
        bool ok = set.RemoveRecord("Alpha");
        Xunit.Assert.True(ok);

        var s = Dump(doc);
        Xunit.Assert.DoesNotContain("\"Alpha\"", s);
        Xunit.Assert.Contains("\"Beta\"", s);

        // Beta is now the only record and should still be resolvable and mutable.
        var beta = set.FindByStableId("Beta")!;
        beta.SetField("ITEMCOST", 555L);
        Xunit.Assert.Contains("ITEMCOST: 555", Dump(doc));
    }

    [Xunit.Fact]
    public void AddRecord_AtEnd_AppendsWithLeadingBlank()
    {
        var (set, doc) = Load();
        var item = new Item();
        item.Name.Set("Gamma");
        item.Type.Set("Armor");
        item.SubType.Set("Plate");
        item.Style.Set("plain");
        item.StarRating.Set(2L);
        item.Cost.Set(777L);
        item.Regions.Add("Imperial");
        item.Regions.Add("Expanse");

        var view = set.AddRecord(item);
        Xunit.Assert.Equal("Gamma", view.StableId);

        var s = Dump(doc);
        Xunit.Assert.Contains("ITEMCREATE: \"Gamma\", \"Armor\", \"Plate\", \"plain\", 2", s);
        Xunit.Assert.Contains("\tITEMCOST: 777", s);
        Xunit.Assert.Contains("\tITEMREGION: \"Imperial\"", s);
        Xunit.Assert.Contains("\tITEMREGION: \"Expanse\"", s);

        // Should be resolvable via the set.
        Xunit.Assert.Same(view, set.FindByStableId("Gamma"));
    }

    [Xunit.Fact]
    public void AddRecord_Before_PlacesBeforeAnchor()
    {
        var (set, doc) = Load();
        var item = new Item();
        item.Name.Set("Gamma");
        item.Type.Set("Armor");
        item.SubType.Set("Plate");
        item.Style.Set("plain");
        item.StarRating.Set(0L);

        set.AddRecord(item, InsertPosition.Before("Beta"));

        int idxAlpha = doc.Lines.FindIndex(l => l is TokKeyValueLine kv && kv.Key == "ITEMCREATE" && kv.Raw.Contains("\"Alpha\""));
        int idxGamma = doc.Lines.FindIndex(l => l is TokKeyValueLine kv && kv.Key == "ITEMCREATE" && kv.Raw.Contains("\"Gamma\""));
        int idxBeta = doc.Lines.FindIndex(l => l is TokKeyValueLine kv && kv.Key == "ITEMCREATE" && kv.Raw.Contains("\"Beta\""));
        Xunit.Assert.True(idxAlpha < idxGamma);
        Xunit.Assert.True(idxGamma < idxBeta);
    }

    [Xunit.Fact]
    public void ClearField_RemovesExistingLineAndUnsetsSlot()
    {
        var (set, doc) = Load();
        var alpha = set.FindByStableId("Alpha")!;
        alpha.ClearField("ITEMCOST");
        Xunit.Assert.False(alpha.Record.Cost.HasValue);
        // Alpha's cost line is gone but Beta still has ITEMCOST: 300.
        Xunit.Assert.DoesNotContain("ITEMCOST: 200", Dump(doc));
        Xunit.Assert.Contains("ITEMCOST: 300", Dump(doc));
    }

    [Xunit.Fact]
    public void Affinity_SetAndEncode_UsesIdentifierIntPair()
    {
        var (set, doc) = Load();
        var alpha = set.FindByStableId("Alpha")!;
        alpha.SetField<(string, long)>("ITEMAFFINITY", ("Fire", 25L));
        var s = Dump(doc);
        // ITEMAFFINITY uses space-comma-space separator to match the vanilla corpus format ("Water , 10").
        Xunit.Assert.Contains("\tITEMAFFINITY: Fire , 25\r\n", s);
    }

    // ---- Real corpus: mutate all 1,852 items then revert -------------------

    [CleanBecFact]
    public void RealItems_SetAndRevertEveryCost_RoundTripsByteForByte()
    {
        string path = System.IO.Path.Combine(CleanBecCorpus.Root!, "data", "config", "items.tok");
        byte[] original = System.IO.File.ReadAllBytes(path);
        var doc = TokReader.Read(original, path);
        var schema = new ItemsSchema();
        var setup = schema.Read(doc);

        // Snapshot original costs per stable id.
        var original_costs = new System.Collections.Generic.Dictionary<string, long?>(System.StringComparer.Ordinal);
        foreach (var v in setup.Records)
            original_costs[v.StableId] = v.Record.Cost.HasValue ? v.Record.Cost.Value : (long?)null;

        // Apply a uniform mutation.
        foreach (var v in setup.Records)
            v.SetField("ITEMCOST", 999_999L);

        // Revert each to its original value (or clear if it had none).
        foreach (var v in setup.Records)
        {
            var orig = original_costs[v.StableId];
            if (orig.HasValue) v.SetField("ITEMCOST", orig.Value);
            else v.ClearField("ITEMCOST");
        }

        byte[] rebuilt = TokWriter.Write(doc);
        Xunit.Assert.Equal(original.Length, rebuilt.Length);
        for (int i = 0; i < original.Length; i++)
        {
            if (original[i] != rebuilt[i])
                Xunit.Assert.Fail($"Byte mismatch at offset {i}: 0x{original[i]:X2} vs 0x{rebuilt[i]:X2}");
        }
    }
}
