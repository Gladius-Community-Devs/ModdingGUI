using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ModdingGUI.Core.Api;
using ModdingGUI.Core.Skills;
using ModdingGUI.Core.Tok;
using Xunit;

namespace ModdingGUI.Tests.Core.Skills;

/// <summary>
/// Round-trip fidelity for <c>skills.tok</c>. PLAN rule 4 requires a focused
/// round-trip test per format before any mutation lands; Phase 2.7 is the first
/// phase that mutates skills through anything other than the raw-line editor, so
/// the no-op re-serialization guarantee is pinned here explicitly (in addition to
/// the whole-corpus tok gate in <c>TokRoundTripTests</c>).
/// </summary>
public sealed class SkillsRoundTripTests : IDisposable
{
    private const int VanillaSkillCount = 1811;

    private readonly string _projectDir;
    private readonly string _skillsPath;

    public SkillsRoundTripTests()
    {
        _projectDir = Path.Combine(Path.GetTempPath(),
            "ModdingGUI.SkillsRoundTrip-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_projectDir, "data", "config"));
        _skillsPath = Path.Combine(_projectDir, "data", "config", "skills.tok");

        if (CleanBecCorpus.IsAvailable)
        {
            File.Copy(
                Path.Combine(CleanBecCorpus.Root!, "data", "config", "skills.tok"),
                _skillsPath,
                overwrite: true);
        }
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_projectDir)) Directory.Delete(_projectDir, recursive: true);
        }
        catch
        {
            // best effort temp cleanup
        }
    }

    [CleanBecFact]
    public void EveryVanillaSkillBlock_RebuildsByteForByte()
    {
        string text = File.ReadAllText(_skillsPath, TokDocument.CanonicalEncoding);
        string normalized = text.Replace("\r\n", "\n").Replace("\r", "\n");
        var lines = normalized.TrimEnd('\n').Split('\n').ToList();

        var mismatched = new List<string>();
        int blocks = 0;

        for (int i = 0; i < lines.Count; i++)
        {
            if (!lines[i].StartsWith("SKILLCREATE:", StringComparison.Ordinal)) continue;

            var (start, end) = SkillsApi.BlockEndFrom(lines, i);
            var dto = SkillsApi.ParseBlock(lines, start, end);
            var rebuilt = SkillsApi.BuildBlock(dto);
            var original = lines.GetRange(start, end - start + 1);

            if (!rebuilt.SequenceEqual(original, StringComparer.Ordinal))
                mismatched.Add(dto.Name);

            blocks++;
            i = end;
        }

        Assert.Equal(VanillaSkillCount, blocks);
        Assert.True(mismatched.Count == 0,
            $"{mismatched.Count} block(s) do not re-serialize identically: " +
            string.Join(", ", mismatched.Take(10)));
    }

    [CleanBecFact]
    public void Update_WithUnchangedRecords_PreservesFileBytesExactly()
    {
        var api = new SkillsApi();
        byte[] before = File.ReadAllBytes(_skillsPath);

        // A spread of records across the file, including ones with many body fields.
        var names = api.List(_projectDir);
        Assert.Equal(VanillaSkillCount, names.Count);

        var sample = new[] { 0, 1, names.Count / 4, names.Count / 2, names.Count - 2, names.Count - 1 }
            .Select(i => names[i])
            .Concat(names.Where((_, i) => i % 250 == 0))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        foreach (string name in sample)
        {
            var dto = api.Get(_projectDir, name);
            Assert.NotNull(dto);
            api.Update(_projectDir, name, dto!);
        }

        Assert.Equal(before, File.ReadAllBytes(_skillsPath));

        string backupDir = Path.Combine(_projectDir, ".mod-history", "data", "config", "skills.tok");
        Assert.True(Directory.Exists(backupDir));
        Assert.Equal(sample.Count, Directory.GetFiles(backupDir, "*.bak").Length);
    }

    [CleanBecFact]
    public void GetAll_ReadsEveryRecord_AndRoundTripsThroughTheDto()
    {
        var api = new SkillsApi();

        var all = api.GetAll(_projectDir);

        Assert.Equal(VanillaSkillCount, all.Count);
        Assert.All(all, s =>
        {
            Assert.False(string.IsNullOrWhiteSpace(s.Name));
            Assert.False(string.IsNullOrWhiteSpace(s.SkillType));
            Assert.False(string.IsNullOrWhiteSpace(s.SkillSubType));
        });
        Assert.Equal(all.Count, all.Select(s => s.Name).Distinct(StringComparer.Ordinal).Count());
    }
}
