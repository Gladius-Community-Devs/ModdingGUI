using System.Text;
using ModdingGUI.Core.Tok;
using Xunit;
using Xunit.Abstractions;

namespace ModdingGUI.Tests.Core.Tok;

/// <summary>
/// Phase 1 round-trip fidelity gate.
/// </summary>
/// <remarks>
/// <para>
/// Parses and re-serializes every <c>.tok</c> and <c>.enc</c> file in the
/// vanilla <c>clean-bec-unpacked/</c> corpus and asserts that the bytes emitted
/// by <see cref="TokWriter"/> exactly match the bytes read from disk.
/// </para>
/// <para>
/// This test is the architectural contract of the tok framework: if it passes,
/// every higher layer (schemas, APIs, editors, randomizer, MCP) can operate on
/// tok documents and know that unmodified content will be preserved bit-exactly.
/// It also catches regressions in the reader/writer themselves and provides
/// early warning of any vanilla file with a quirk (mixed line endings, trailing
/// whitespace, embedded BOM, etc.) that the framework doesn't yet handle.
/// </para>
/// <para>
/// The test reports up to <see cref="MaxReportedMismatches"/> diffs with
/// per-file details (relative path, first differing byte offset, context windows,
/// and a length delta) to make regressions fast to diagnose. A passing run
/// covers the full corpus (~977 files, ~4.6 MB) in roughly one second.
/// </para>
/// </remarks>
public class TokRoundTripTests
{
    private const int MaxReportedMismatches = 25;
    private readonly ITestOutputHelper _out;

    public TokRoundTripTests(ITestOutputHelper @out)
    {
        _out = @out;
    }

    [CleanBecFact]
    public void EveryTokAndEncFile_RoundTripsByteForByte()
    {
        Assert.True(CleanBecCorpus.IsAvailable);

        var mismatches = new List<string>();
        int fileCount = 0;
        long totalBytes = 0;

        foreach (var path in CleanBecCorpus.EnumerateTokAndEnc())
        {
            fileCount++;
            byte[] original = File.ReadAllBytes(path);
            totalBytes += original.Length;

            TokDocument doc;
            try
            {
                doc = TokReader.Read(original, path);
            }
            catch (Exception ex)
            {
                mismatches.Add(
                    $"{CleanBecCorpus.Relative(path)}: READ THREW {ex.GetType().Name}: {ex.Message}");
                if (mismatches.Count >= MaxReportedMismatches) break;
                continue;
            }

            byte[] roundTripped;
            try
            {
                roundTripped = TokWriter.Write(doc);
            }
            catch (Exception ex)
            {
                mismatches.Add(
                    $"{CleanBecCorpus.Relative(path)}: WRITE THREW {ex.GetType().Name}: {ex.Message}");
                if (mismatches.Count >= MaxReportedMismatches) break;
                continue;
            }

            if (!ByteSequenceEquals(original, roundTripped, out int firstDiff))
            {
                mismatches.Add(FormatMismatch(path, original, roundTripped, firstDiff));
                if (mismatches.Count >= MaxReportedMismatches) break;
            }
        }

        _out.WriteLine(
            $"Round-trip scan complete: {fileCount} files, {totalBytes:N0} bytes, " +
            $"{mismatches.Count} mismatch(es).");

        if (mismatches.Count > 0)
        {
            var msg = new StringBuilder();
            msg.AppendLine($"Tok round-trip failed on {mismatches.Count} file(s) " +
                           $"(showing up to {MaxReportedMismatches}):");
            foreach (var m in mismatches)
            {
                msg.AppendLine();
                msg.AppendLine(m);
            }
            Assert.Fail(msg.ToString());
        }
    }

    private static bool ByteSequenceEquals(byte[] a, byte[] b, out int firstDiff)
    {
        int min = Math.Min(a.Length, b.Length);
        for (int i = 0; i < min; i++)
        {
            if (a[i] != b[i]) { firstDiff = i; return false; }
        }
        if (a.Length != b.Length) { firstDiff = min; return false; }
        firstDiff = -1;
        return true;
    }

    private static string FormatMismatch(string path, byte[] original, byte[] roundTripped, int firstDiff)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{CleanBecCorpus.Relative(path)}:");
        sb.AppendLine($"  original length = {original.Length}, round-tripped length = {roundTripped.Length}");
        sb.AppendLine($"  first differing byte offset = {firstDiff}");

        int ctxStart = Math.Max(0, firstDiff - 16);
        int origCtxEnd = Math.Min(original.Length, firstDiff + 16);
        int rtCtxEnd = Math.Min(roundTripped.Length, firstDiff + 16);

        sb.AppendLine($"  original  [offset {ctxStart}..{origCtxEnd}]: {HexDump(original, ctxStart, origCtxEnd)}");
        sb.AppendLine($"  roundTrip [offset {ctxStart}..{rtCtxEnd  }]: {HexDump(roundTripped, ctxStart, rtCtxEnd)}");
        return sb.ToString();
    }

    private static string HexDump(byte[] bytes, int start, int end)
    {
        var sb = new StringBuilder();
        for (int i = start; i < end; i++)
        {
            sb.Append(bytes[i].ToString("x2"));
            sb.Append(' ');
        }
        sb.Append("| ");
        for (int i = start; i < end; i++)
        {
            byte b = bytes[i];
            sb.Append(b >= 0x20 && b < 0x7F ? (char)b : '.');
        }
        return sb.ToString();
    }
}
