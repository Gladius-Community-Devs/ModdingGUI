using ModdingGUI.Core.Tok;
using ModdingGUI.Core.Tok.Values;
using Xunit;
using Xunit.Abstractions;

namespace ModdingGUI.Tests.Core.Tok.Values;

/// <summary>
/// Guarantees that parsing and re-emitting the value portion of every <c>KEY: value</c> line in the
/// full <c>clean-bec-unpacked/</c> corpus round-trips byte-for-byte. This is the Phase 2 value-grammar
/// analogue of the Phase 1 full-file round-trip gate.
/// </summary>
public sealed class TokValueRoundTripTests
{
    private readonly ITestOutputHelper _output;

    public TokValueRoundTripTests(ITestOutputHelper output) => _output = output;

    [CleanBecFact]
    public void EveryKeyValueLine_ValueRebuildsRawExactly()
    {
        var mismatches = new List<string>();
        int linesExamined = 0;

        foreach (string path in CleanBecCorpus.EnumerateTokAndEnc())
        {
            TokDocument doc = TokReader.ReadFile(path);
            string rel = CleanBecCorpus.Relative(path);

            for (int idx = 0; idx < doc.Lines.Count; idx++)
            {
                if (doc.Lines[idx] is not TokKeyValueLine kv) continue;
                linesExamined++;

                TokValueList list = TokValueReader.Parse(kv.ValueRaw);
                string rebuilt = list.Rebuild();
                if (rebuilt != kv.ValueRaw)
                {
                    if (mismatches.Count < 25)
                    {
                        mismatches.Add($"{rel}:{idx + 1} key={kv.Key}\n  raw     : [{kv.ValueRaw}]\n  rebuilt : [{rebuilt}]");
                    }
                }
            }
        }

        _output.WriteLine($"Value-grammar round-trip: {linesExamined} KV lines examined, {mismatches.Count} mismatch(es).");

        if (mismatches.Count > 0)
        {
            Assert.Fail("Value-grammar round-trip failed:\n" + string.Join("\n---\n", mismatches));
        }
    }
}
