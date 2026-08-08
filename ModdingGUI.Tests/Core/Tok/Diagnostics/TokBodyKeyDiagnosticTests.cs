using ModdingGUI.Core.Tok;

namespace ModdingGUI.Tests.Core.Tok.Diagnostics;

/// <summary>
/// One-off diagnostics that enumerate the distinct body keys found in record-based tok files
/// (<c>items.tok</c>, <c>skills.tok</c>) and report their cardinality (single-per-record vs.
/// repeated-per-record). The output of these tests drives the schema field-list design in
/// <see cref="ModdingGUI.Core.Schemas"/>.
/// </summary>
/// <remarks>
/// <para>
/// The diagnostic tests always pass; they exist to dump a catalog to a predictable file path
/// (<c>&lt;bin&gt;/schema-diagnostics/&lt;name&gt;.txt</c>) that the developer can inspect when
/// building or updating a <see cref="ModdingGUI.Core.Schemas.Schema.RecordSchema{TRecord}"/>.
/// </para>
/// <para>
/// Record boundaries are identified by the configured record-start key (<c>ITEMCREATE</c> for
/// items, <c>SKILLCREATE</c> for skills). Body lines are the <see cref="TokKeyValueLine"/>s
/// between one record start and the next. For each body key, the diagnostic records the maximum
/// count within any single record; cardinality is <c>Single</c> if the max is 1, <c>Multiple</c>
/// otherwise.
/// </para>
/// </remarks>
public class TokBodyKeyDiagnosticTests
{
    [CleanBecFact]
    public void ItemsTok_BodyKeyCardinality()
    {
        RunDiagnostic(
            relativePath: "data/config/items.tok",
            recordStartKey: "ITEMCREATE",
            outputFileName: "items-body-keys.txt");
    }

    [CleanBecFact]
    public void SkillsTok_BodyKeyCardinality()
    {
        RunDiagnostic(
            relativePath: "data/config/skills.tok",
            recordStartKey: "SKILLCREATE",
            outputFileName: "skills-body-keys.txt");
    }

    private static void RunDiagnostic(string relativePath, string recordStartKey, string outputFileName)
    {
        Xunit.Assert.True(CleanBecCorpus.IsAvailable);
        string path = System.IO.Path.Combine(CleanBecCorpus.Root!, relativePath.Replace('/', System.IO.Path.DirectorySeparatorChar));
        Xunit.Assert.True(System.IO.File.Exists(path), $"Missing corpus file: {relativePath}");

        TokDocument doc = TokReader.ReadFile(path);

        // For each body key: max count within a single record, total count across all records.
        var maxPerRecord = new System.Collections.Generic.Dictionary<string, int>(System.StringComparer.Ordinal);
        var totalCount = new System.Collections.Generic.Dictionary<string, int>(System.StringComparer.Ordinal);
        var firstSeenLine = new System.Collections.Generic.Dictionary<string, int>(System.StringComparer.Ordinal);

        int recordCount = 0;
        System.Collections.Generic.Dictionary<string, int>? current = null;

        void CloseRecord()
        {
            if (current is null) return;
            foreach (var kv in current)
            {
                if (!maxPerRecord.TryGetValue(kv.Key, out int existing) || kv.Value > existing)
                    maxPerRecord[kv.Key] = kv.Value;
                totalCount[kv.Key] = totalCount.TryGetValue(kv.Key, out int t) ? t + kv.Value : kv.Value;
            }
            current = null;
        }

        foreach (var line in doc.Lines)
        {
            if (line is TokKeyValueLine kv)
            {
                if (kv.Key == recordStartKey)
                {
                    CloseRecord();
                    current = new System.Collections.Generic.Dictionary<string, int>(System.StringComparer.Ordinal);
                    recordCount++;
                    continue;
                }
                if (current is null) continue; // preamble lines (e.g. NUMENTRIES)
                current[kv.Key] = current.TryGetValue(kv.Key, out int c) ? c + 1 : 1;
                if (!firstSeenLine.ContainsKey(kv.Key))
                    firstSeenLine[kv.Key] = kv.LineNumber + 1; // 1-based for humans
            }
        }
        CloseRecord();

        // Sort by key name.
        var keys = new System.Collections.Generic.List<string>(maxPerRecord.Keys);
        keys.Sort(System.StringComparer.Ordinal);

        // Write the catalog to a predictable path under the test binary output.
        string outDir = System.IO.Path.Combine(System.AppContext.BaseDirectory, "schema-diagnostics");
        System.IO.Directory.CreateDirectory(outDir);
        string outPath = System.IO.Path.Combine(outDir, outputFileName);

        using var writer = new System.IO.StreamWriter(outPath);
        writer.WriteLine($"# Body-key catalog for {relativePath}");
        writer.WriteLine($"# Record-start key: {recordStartKey}");
        writer.WriteLine($"# Total records: {recordCount}");
        writer.WriteLine($"# Distinct body keys: {keys.Count}");
        writer.WriteLine();
        writer.WriteLine($"{"Key",-32} {"Cardinality",-12} {"MaxPerRecord",-12} {"TotalCount",-10} {"FirstLine"}");
        writer.WriteLine(new string('-', 85));
        foreach (var key in keys)
        {
            string card = maxPerRecord[key] == 1 ? "Single" : "Multiple";
            writer.WriteLine($"{key,-32} {card,-12} {maxPerRecord[key],-12} {totalCount[key],-10} {firstSeenLine.GetValueOrDefault(key, -1)}");
        }

        Xunit.Assert.True(recordCount > 0, $"Parsed zero records from {relativePath}.");
    }
}
