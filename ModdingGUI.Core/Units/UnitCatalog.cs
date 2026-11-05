using System;
using System.IO;
using System.Text.RegularExpressions;

namespace ModdingGUI.Core.Units;

/// <summary>How many entries each cross-referenced units file holds.</summary>
/// <remarks>
/// A gladiator's set fields are zero-based indexes into these files, so the counts are what a validator
/// needs to say "Stat set 400 does not exist". The numbers are read from the files themselves rather
/// than hard-coded, so a mod that adds a set widens the valid range automatically.
/// </remarks>
public sealed record UnitCatalog(int StatSets, int SkillSets, int ItemSets, int Schools, int Tints);

/// <summary>Reads the <see cref="UnitCatalog"/> from a project.</summary>
public static class UnitCatalogLoader
{
    private static readonly Regex StatSetSection = new(@"^Statset (\d+):", RegexOptions.Multiline);
    private static readonly Regex SkillSetSection = new(@"^Skillset (\d+):", RegexOptions.Multiline);
    private static readonly Regex ItemSetSection = new(@"^Itemset (\d+):", RegexOptions.Multiline);
    // schools.txt is a name table ("N: <school name>", entry 0 blank) rather than "Section N:" headers,
    // so its entries carry text after the colon.
    private static readonly Regex SchoolSection = new(@"^(\d+):", RegexOptions.Multiline);
    private static readonly Regex TintSection = new(@"^Tint set (\d+):", RegexOptions.Multiline);

    /// <summary>Read the counts; a missing file counts as zero rather than throwing.</summary>
    public static UnitCatalog Load(string projectRootOrBec)
    {
        string bec = ProjectRoots.ResolveBecFolder(projectRootOrBec);

        return new UnitCatalog(
            CountSections(bec, "statsets.txt", StatSetSection),
            CountSections(bec, "skillsets.txt", SkillSetSection),
            CountSections(bec, "itemsets.txt", ItemSetSection),
            CountSections(bec, "schools.txt", SchoolSection),
            CountSections(bec, "tints.txt", TintSection));
    }

    private static int CountSections(string bec, string fileName, Regex section)
    {
        string path = Path.Combine(bec, "data", "units", fileName);
        if (!File.Exists(path)) return 0;

        string text = File.ReadAllText(path, Tok.TokDocument.CanonicalEncoding);
        return section.Matches(text).Count;
    }
}
