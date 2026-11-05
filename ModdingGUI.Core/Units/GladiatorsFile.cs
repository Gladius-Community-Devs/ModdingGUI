using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace ModdingGUI.Core.Units;

/// <summary>The labels <c>gladiators.txt</c> uses, in file order.</summary>
public enum GladiatorField
{
    Name,
    Class,
    Outfit,
    Affinity,
    TintSet,
    SkillSet,
    StatSet,
    ItemSet,
    School,
}

/// <summary>
/// Parser and editor for <c>data/units/gladiators.txt</c>: 2,170 records of nine labelled lines,
/// separated by blank lines.
/// </summary>
/// <remarks>
/// <para>
/// Every edit rewrites only the lines it touches, so unrelated records, spacing and the file's line
/// endings stay exactly as the corpus has them (see <see cref="UnitTextFile"/>). That matters more than
/// usual here: this file is LF-only, and vanilla contains at least one malformed record
/// (<c>Name: Muduk</c> followed by a stray duplicate <c>Muduk</c> line) that must survive a save.
/// </para>
/// <para>
/// A record starts at a <c>Name:</c> line and runs to the next one; blank lines and any unrecognised
/// lines are kept as-is and reported separately by the validator.
/// </para>
/// </remarks>
public sealed class GladiatorsFile
{
    /// <summary>Project-relative location of the file.</summary>
    public const string RelativePath = "data/units/gladiators.txt";

    private static readonly (GladiatorField Field, string Label)[] Labels =
    {
        (GladiatorField.Name, "Name"),
        (GladiatorField.Class, "Class"),
        (GladiatorField.Outfit, "Outfit"),
        (GladiatorField.Affinity, "Affinity"),
        (GladiatorField.TintSet, "Tint set"),
        (GladiatorField.SkillSet, "Skill set"),
        (GladiatorField.StatSet, "Stat set"),
        (GladiatorField.ItemSet, "Item set"),
        (GladiatorField.School, "School"),
    };

    private readonly UnitTextFile _file;
    private readonly List<Record> _records = new();

    private GladiatorsFile(UnitTextFile file)
    {
        _file = file;
    }

    /// <summary>Every record, in file order.</summary>
    public IReadOnlyList<GladiatorDto> Records => _records.Select(r => r.Dto).ToArray();

    /// <summary>True once a line has been changed, added or removed.</summary>
    public bool IsModified => _file.IsModified;

    /// <summary>Load and parse a file.</summary>
    public static GladiatorsFile Load(string path) => Parse(UnitTextFile.Load(path));

    /// <summary>Parse already-read bytes.</summary>
    public static GladiatorsFile Parse(byte[] bytes) => Parse(UnitTextFile.Parse(bytes));

    /// <summary>Load from a project folder (accepts a project root or a BEC folder).</summary>
    public static GladiatorsFile LoadProject(string projectRootOrBec)
    {
        string bec = ProjectRoots.ResolveBecFolder(projectRootOrBec);
        string path = Path.Combine(bec, RelativePath.Replace('/', Path.DirectorySeparatorChar));
        return Load(path);
    }

    private static GladiatorsFile Parse(UnitTextFile file)
    {
        var result = new GladiatorsFile(file);
        string[] lines = file.Lines.ToArray();

        Record? current = null;
        var values = new Dictionary<GladiatorField, string>();
        var fieldLines = new Dictionary<GladiatorField, int>();
        var extras = new List<int>();

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];

            if (TryReadLabel(line, out GladiatorField field, out string value))
            {
                if (field == GladiatorField.Name)
                {
                    Flush();
                    current = new Record { StartLine = i };
                    values.Clear();
                    fieldLines.Clear();
                    extras.Clear();
                }

                if (current is null) continue;   // stray label before the first record

                values[field] = value;
                fieldLines[field] = i;
                continue;
            }

            if (current is null) continue;       // header/preamble lines
            if (line.Trim().Length == 0) continue; // separator inside/after the record
            extras.Add(i);
        }

        Flush();
        return result;

        void Flush()
        {
            if (current is null) return;

            current.Values = new Dictionary<GladiatorField, string>(values);
            current.FieldLines = new Dictionary<GladiatorField, int>(fieldLines);
            current.ExtraLines = extras.ToList();
            current.Dto = BuildDto(current.Values);
            current.EndLine = current.ExtraLines.Count > 0
                ? current.ExtraLines[^1]
                : current.FieldLines.Count > 0 ? current.FieldLines.Values.Max() : current.StartLine;
            result._records.Add(current);
        }
    }

    /// <summary>Look up one gladiator by name (case-sensitive, as in the file).</summary>
    public GladiatorDto? Get(string name) => Find(name)?.Dto;

    /// <summary>Apply a partial update, rewriting only the changed lines.</summary>
    /// <summary>Apply a partial update; returns false when every value already matched.</summary>
    public bool Patch(string name, GladiatorPatch patch)
    {
        ArgumentNullException.ThrowIfNull(patch);
        if (patch.IsEmpty) return false;

        var record = Find(name) ?? throw new InvalidOperationException($"No gladiator named '{name}'.");
        bool changed = false;
        var values = new Dictionary<GladiatorField, string>(record.Values);

        if (patch.Class is not null) values[GladiatorField.Class] = patch.Class;
        if (patch.Outfit is not null) values[GladiatorField.Outfit] = Int(patch.Outfit.Value);
        if (patch.Affinity is not null) values[GladiatorField.Affinity] = patch.Affinity;
        if (patch.TintSet is not null) values[GladiatorField.TintSet] = Int(patch.TintSet.Value);
        if (patch.SkillSet is not null) values[GladiatorField.SkillSet] = Int(patch.SkillSet.Value);
        if (patch.StatSet is not null) values[GladiatorField.StatSet] = Int(patch.StatSet.Value);
        if (patch.ItemSet is not null) values[GladiatorField.ItemSet] = Int(patch.ItemSet.Value);
        if (patch.School is not null) values[GladiatorField.School] = Int(patch.School.Value);

        foreach (var (field, label) in Labels)
        {
            if (!values.TryGetValue(field, out string? newValue)) continue;
            if (!record.FieldLines.TryGetValue(field, out int line)) continue;
            if (string.Equals(record.Values[field], newValue, StringComparison.Ordinal)) continue;

            _file.ReplaceLine(line, $"{label}: {newValue}");
            record.Values[field] = newValue;
            changed = true;
        }

        record.Dto = BuildDto(record.Values);
        return changed;
    }

    /// <summary>Rename a gladiator, refusing a name that already exists.</summary>
    public void Rename(string name, string newName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newName);
        var record = Find(name) ?? throw new InvalidOperationException($"No gladiator named '{name}'.");

        if (Find(newName) is not null && !string.Equals(name, newName, StringComparison.Ordinal))
            throw new InvalidOperationException($"A gladiator named '{newName}' already exists.");

        if (record.FieldLines.TryGetValue(GladiatorField.Name, out int line))
        {
            _file.ReplaceLine(line, $"Name: {newName}");
            record.Values[GladiatorField.Name] = newName;
            record.Dto = BuildDto(record.Values);
        }
    }

    /// <summary>Append a record (nine lines plus the separating blank line).</summary>
    public void Add(GladiatorDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        ArgumentException.ThrowIfNullOrWhiteSpace(dto.Name);

        if (Find(dto.Name) is not null)
            throw new InvalidOperationException($"A gladiator named '{dto.Name}' already exists.");

        int index = _file.Count;
        while (index > 0 && _file.GetLine(index - 1).Trim().Length == 0 && index > _records.LastOrDefault()?.EndLine)
        {
            // Keep any trailing blank line at the end of the file rather than before the new record.
            index--;
            if (index == 0) break;
        }

        var record = new Record
        {
            StartLine = index,
            Values = new Dictionary<GladiatorField, string>(),
            FieldLines = new Dictionary<GladiatorField, int>(),
            ExtraLines = new List<int>(),
        };

        int cursor = index;
        foreach (var (field, label) in Labels)
        {
            string value = field switch
            {
                GladiatorField.Name => dto.Name,
                GladiatorField.Class => dto.Class,
                GladiatorField.Outfit => Int(dto.Outfit),
                GladiatorField.Affinity => dto.Affinity,
                GladiatorField.TintSet => Int(dto.TintSet),
                GladiatorField.SkillSet => Int(dto.SkillSet),
                GladiatorField.StatSet => Int(dto.StatSet),
                GladiatorField.ItemSet => Int(dto.ItemSet),
                GladiatorField.School => Int(dto.School),
                _ => string.Empty,
            };

            _file.InsertLine(cursor, $"{label}: {value}");
            record.Values[field] = value;
            record.FieldLines[field] = cursor;
            cursor++;
        }

        // Keep the blank-line separator that every record in the corpus has.
        _file.InsertLine(cursor, string.Empty);

        record.EndLine = cursor;
        record.Dto = BuildDto(record.Values);

        // Later records shifted down by the number of inserted lines.
        foreach (var existing in _records.Where(r => r.StartLine >= index))
        {
            existing.StartLine += Labels.Length + 1;
            existing.EndLine += Labels.Length + 1;
            foreach (var key in existing.FieldLines.Keys.ToList())
                existing.FieldLines[key] += Labels.Length + 1;
            for (int i = 0; i < existing.ExtraLines.Count; i++)
                existing.ExtraLines[i] += Labels.Length + 1;
        }

        _records.Add(record);
    }

    /// <summary>Remove a gladiator and its separator line.</summary>
    public bool Remove(string name)
    {
        var record = Find(name);
        if (record is null) return false;

        int last = record.EndLine;
        if (last + 1 < _file.Count && _file.GetLine(last + 1).Trim().Length == 0) last++;

        for (int line = last; line >= record.StartLine; line--) _file.RemoveLine(line);

        int removed = last - record.StartLine + 1;
        _records.Remove(record);

        foreach (var existing in _records.Where(r => r.StartLine > record.StartLine))
        {
            existing.StartLine -= removed;
            existing.EndLine -= removed;
            foreach (var key in existing.FieldLines.Keys.ToList())
                existing.FieldLines[key] -= removed;
            for (int i = 0; i < existing.ExtraLines.Count; i++)
                existing.ExtraLines[i] -= removed;
        }

        return true;
    }

    /// <summary>Unexpected lines found inside a record (vanilla has at least one).</summary>
    public IReadOnlyList<(string Name, int Line, string Text)> ExtraLines()
    {
        var result = new List<(string, int, string)>();
        foreach (var record in _records)
        {
            foreach (int line in record.ExtraLines)
                result.Add((record.Dto.Name, line, _file.GetLine(line)));
        }
        return result;
    }

    /// <summary>Records that are missing a field line.</summary>
    public IReadOnlyList<(string Name, GladiatorField Missing)> MissingFields()
    {
        var result = new List<(string, GladiatorField)>();
        foreach (var record in _records)
        {
            foreach (var (field, _) in Labels)
            {
                if (!record.FieldLines.ContainsKey(field)) result.Add((record.Dto.Name, field));
            }
        }
        return result;
    }

    /// <summary>Serialise; an unmodified file reproduces the input bytes.</summary>
    public byte[] ToBytes() => _file.ToBytes();

    /// <summary>Write through a backup.</summary>
    public void Save(string projectRootOrBec)
    {
        string bec = ProjectRoots.ResolveBecFolder(projectRootOrBec);
        _file.Save(bec, RelativePath);
    }

    private Record? Find(string name) =>
        _records.FirstOrDefault(r => string.Equals(r.Dto.Name, name, StringComparison.Ordinal));

    private static bool TryReadLabel(string line, out GladiatorField field, out string value)
    {
        foreach (var (candidate, label) in Labels)
        {
            if (!line.StartsWith(label + ":", StringComparison.Ordinal)) continue;

            field = candidate;
            value = line[(label.Length + 1)..].Trim();
            return true;
        }

        field = default;
        value = string.Empty;
        return false;
    }

    private static string Int(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static int ParseInt(string value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) ? parsed : 0;

    private static GladiatorDto BuildDto(IReadOnlyDictionary<GladiatorField, string> values) =>
        new(
            values.TryGetValue(GladiatorField.Name, out string? name) ? name : string.Empty,
            values.TryGetValue(GladiatorField.Class, out string? klass) ? klass : string.Empty,
            values.TryGetValue(GladiatorField.Outfit, out string? outfit) ? ParseInt(outfit) : 0,
            values.TryGetValue(GladiatorField.Affinity, out string? affinity) ? affinity : string.Empty,
            values.TryGetValue(GladiatorField.TintSet, out string? tint) ? ParseInt(tint) : 0,
            values.TryGetValue(GladiatorField.SkillSet, out string? skill) ? ParseInt(skill) : 0,
            values.TryGetValue(GladiatorField.StatSet, out string? stat) ? ParseInt(stat) : 0,
            values.TryGetValue(GladiatorField.ItemSet, out string? item) ? ParseInt(item) : 0,
            values.TryGetValue(GladiatorField.School, out string? school) ? ParseInt(school) : 0);

    private sealed class Record
    {
        public int StartLine { get; set; }
        public int EndLine { get; set; }
        public Dictionary<GladiatorField, string> Values { get; set; } = new();
        public Dictionary<GladiatorField, int> FieldLines { get; set; } = new();
        public List<int> ExtraLines { get; set; } = new();
        public GladiatorDto Dto { get; set; } = new(string.Empty, string.Empty, 0, string.Empty, 0, 0, 0, 0, 0);
    }
}
