namespace ModdingGUI.Core.Api;

using ModdingGUI.Core.Grids;

/// <summary>
/// The dropdown contents for an encounter editor: enemy schools, gladiators per school, class tags and start markers.
/// </summary>
/// <remarks>
/// <para>
/// Every list here is read out of the project rather than hard-coded, because that is what the user edits:
/// </para>
/// <list type="bullet">
/// <item><description>
/// Enemy schools: <c>data/units/schools.txt</c> (the indexed enemy-school list) cross-checked with
/// <c>data/config/enemyschools.tok</c> (the same names with a display-text id and a cloth colour).
/// </description></item>
/// <item><description>
/// Gladiators: <c>data/units/gladiators.txt</c>, where each entry names its class, affinity and <c>School:</c> index -
/// so the unit list can be filtered to the school a team uses.
/// </description></item>
/// <item><description>
/// Class tags: <c>CREATECLASS</c> names from <c>data/config/classdefs.tok</c>, via <see cref="IClassesApi"/>.
/// </description></item>
/// <item><description>
/// Start markers: the tag names of the arena's own <c>.grd</c>, via <see cref="GridFile"/>.
/// </description></item>
/// </list>
/// </remarks>
public sealed class EncounterChoices
{
    private static readonly string[] SchoolNamesFile = { "data/units/schools.txt" };
    private const string EnemySchoolsFile = "data/config/enemyschools.tok";
    private const string GladiatorsFile = "data/units/gladiators.txt";

    private readonly IClassesApi _classes;

    /// <summary>Create the provider. <paramref name="classes"/> supplies class-tag names.</summary>
    public EncounterChoices(IClassesApi? classes = null) => _classes = classes ?? new ClassDefsApi();

    /// <summary>Enemy school names, in <c>schools.txt</c> index order (index 0 is the empty name and is skipped).</summary>
    public IReadOnlyList<string> EnemySchools(string projectPath)
    {
        string bec = ProjectRoots.ResolveBecFolder(projectPath);
        var names = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        string indexFile = Path.Combine(bec, "data", "units", "schools.txt");
        if (File.Exists(indexFile))
        {
            foreach (string line in File.ReadAllLines(indexFile))
            {
                // "12: BelfortMilitary" - the number is the School: index gladiators.txt refers to.
                int colon = line.IndexOf(':');
                if (colon <= 0) continue;

                string name = line[(colon + 1)..].Trim();
                if (name.Length == 0) continue;
                if (seen.Add(name)) names.Add(name);
            }
        }

        // enemyschools.tok holds the same roster with display ids; include anything it has that schools.txt lacks.
        string enemyFile = Path.Combine(bec, "data", "config", "enemyschools.tok");
        if (File.Exists(enemyFile))
        {
            foreach (string line in File.ReadAllLines(enemyFile))
            {
                string trimmed = line.TrimStart();
                if (!trimmed.StartsWith("SCHOOL", StringComparison.OrdinalIgnoreCase)) continue;

                int firstQuote = trimmed.IndexOf('"');
                int lastQuote = trimmed.LastIndexOf('"');
                if (firstQuote < 0 || lastQuote <= firstQuote) continue;

                string name = trimmed[(firstQuote + 1)..lastQuote];
                if (name.Length > 0 && seen.Add(name)) names.Add(name);
            }
        }

        return names;
    }

    /// <summary>
    /// Gladiators, keyed by the enemy school they belong to (<c>School:</c> in gladiators.txt).
    /// </summary>
    /// <returns>School index -> gladiator names, plus each name's class and affinity in <paramref name="details"/>.</returns>
    public IReadOnlyDictionary<int, IReadOnlyList<string>> GladiatorsBySchool(
        string projectPath,
        out IReadOnlyDictionary<string, GladiatorInfo> details)
    {
        string bec = ProjectRoots.ResolveBecFolder(projectPath);
        string path = Path.Combine(bec, "data", "units", "gladiators.txt");

        var bySchool = new Dictionary<int, List<string>>();
        var info = new Dictionary<string, GladiatorInfo>(StringComparer.OrdinalIgnoreCase);

        if (!File.Exists(path))
        {
            details = info;
            return bySchool.ToDictionary(p => p.Key, p => (IReadOnlyList<string>)p.Value);
        }

        string? name = null;
        string? className = null;
        string? affinity = null;
        int? school = null;

        foreach (string raw in File.ReadAllLines(path))
        {
            string line = raw.Trim();
            if (line.Length == 0) continue;

            if (line.StartsWith("Name:", StringComparison.OrdinalIgnoreCase))
            {
                Flush();
                name = line[5..].Trim();
                className = null;
                affinity = null;
                school = null;
                continue;
            }

            if (line.StartsWith("Class:", StringComparison.OrdinalIgnoreCase)) { className = line[6..].Trim(); continue; }
            if (line.StartsWith("Affinity:", StringComparison.OrdinalIgnoreCase)) { affinity = line[9..].Trim(); continue; }

            if (line.StartsWith("School:", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(line[7..].Trim(), out int index))
            {
                school = index;
                Flush();
            }
        }

        Flush();
        details = info;
        return bySchool.ToDictionary(p => p.Key, p => (IReadOnlyList<string>)p.Value);

        void Flush()
        {
            if (string.IsNullOrWhiteSpace(name) || school is not int schoolIndex) return;

            if (!bySchool.TryGetValue(schoolIndex, out List<string>? list))
            {
                list = new List<string>();
                bySchool[schoolIndex] = list;
            }

            list.Add(name);
            info[name] = new GladiatorInfo(name, className ?? string.Empty, affinity ?? string.Empty, schoolIndex);
        }
    }

    /// <summary>Class names usable as class tags (the <c>CREATECLASS</c> names).</summary>
    public IReadOnlyList<string> ClassTags(string projectPath) => _classes.List(projectPath);

    /// <summary>Start markers available in an arena's grid file, in slot order (e.g. Start1..Start8).</summary>
    public static IReadOnlyList<string> StartMarkers(string gridFilePath)
    {
        if (!File.Exists(gridFilePath)) return Array.Empty<string>();

        GridFile grid = GridFile.Load(gridFilePath);
        var tags = new List<string>();

        for (int slot = 0; slot < GridFile.NumSlots; slot++)
        {
            string tag = grid.GetTagNameForSlot(slot);
            if (string.IsNullOrWhiteSpace(tag) || tag == GridFile.NO_VALUE) continue;
            if (!grid.IsSlotActive(slot)) continue;
            if (!tag.StartsWith("Start", StringComparison.OrdinalIgnoreCase)) continue;
            tags.Add(tag);
        }

        return tags;
    }
}

/// <summary>One gladiator's fixed data from <c>gladiators.txt</c>.</summary>
/// <param name="Name">Gladiator name.</param>
/// <param name="ClassName">Class name (a <c>classdefs.tok</c> entry).</param>
/// <param name="Affinity">Affinity as written in the file, e.g. <c>Earth</c>.</param>
/// <param name="SchoolIndex">The <c>School:</c> index, which points into <c>data/units/schools.txt</c>.</param>
public sealed record GladiatorInfo(string Name, string ClassName, string Affinity, int SchoolIndex);
