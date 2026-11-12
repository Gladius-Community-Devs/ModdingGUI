namespace ModdingGUI.EncounterEditor;

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using ModdingGUI.Core;
using ModdingGUI.Core.Api;
using ModdingGUI.Core.Logging;

/// <summary>
/// Hand-crafting editor for one <c>.enc</c> encounter: pick it, pick a team, pick a row, then edit the row's 21 slots with
/// controls whose values are validated before they are accepted.
/// </summary>
/// <remarks>
/// <para>
/// The design goal is that a typo cannot reach the file. Every edit goes through
/// <see cref="EncounterUnitEdit.TryApply"/>, which is the same validation the driver and (later) the MCP server use: bounds,
/// enumerations, the 0/1 switches and the class-tag/bitmask shapes. A rejected value is shown next to the control and the
/// draft keeps the previous value, so a save always writes something that passed.
/// </para>
/// <para>
/// Controls are chosen from the slot's <see cref="EncounterFieldKind"/> in <see cref="EncounterUnitFields"/> rather than
/// hard-coded per slot, so when the catalog learns something new (a bound, an enum, the affinity names) the page follows
/// without changes here.
/// </para>
/// </remarks>
public sealed class EncounterEditorView : UserControl
{
    private const string SchoolKey = "SCHOOL";
    private const string GridKey = "GRIDFILE";

    private readonly IEncountersApi _api = new EncountersApi();
    private readonly EncounterChoices _choices = new();

    private string _project = string.Empty;
    private EncounterDto? _encounter;
    private int _teamIndex = 1;
    private int _rowIndex = -1;

    /// <summary>Rows as edited so far, per team index. Cleared whenever the encounter is (re)loaded.</summary>
    private readonly Dictionary<int, List<EncounterUnit>> _draft = new();

    private readonly Dictionary<int, Control> _fieldControls = new();

    private ComboBox _region = null!;
    private ComboBox _town = null!;
    private ComboBox _league = null!;
    private IReadOnlyList<ArenaCatalog.ArenaEntry> _arenas = Array.Empty<ArenaCatalog.ArenaEntry>();
    private TextBox _search = null!;
    private ListBox _files = null!;
    private ComboBox _teams = null!;
    private ComboBox _school = null!;
    private Label _gridLabel = null!;
    private ListBox _rows = null!;
    private Panel _fieldsHost = null!;
    private Label _status = null!;
    private Label? _schoolHint;
    private Label? _unitsCaption;
    private Label? _unitWarning;
    private Label? _requirementsSummary;
    private CheckBox? _showUnclear;
    private TableLayoutPanel? _unclearGrid;
    private readonly List<Button> _rowButtons = new();

    /// <summary>Where messages go; null keeps them in the status line only.</summary>
    public IAppLog? Log { get; init; }

    public EncounterEditorView()
    {
        Dock = DockStyle.Fill;

        // A table, not a SplitContainer: SplitterDistance is ignored until the handle exists, which left the picker a
        // hundred pixels wide on first show.
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, UiTheme.Scale(this, 320)));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        layout.Controls.Add(BuildPicker(), 0, 0);
        layout.Controls.Add(BuildEditor(), 1, 0);
        Controls.Add(layout);
    }

    /// <summary>Point the page at a project and load its encounter list.</summary>
    public void SetProject(string projectPath)
    {
        _project = projectPath ?? string.Empty;
        LoadNavigation();
    }

    // ---------------------------------------------------------------- picker

    private Control BuildPicker()
    {
        var host = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };

        // Region -> town -> league. An arena belongs to exactly one town in world.tok (44 towns, 44 arenas, all
        // distinct), so asking for it separately is a control with one possible answer; the arena is derived from
        // the town instead. Only 8 of the 44 arena names resemble the town name, which is why the mapping is needed.
        var nav = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = UiTheme.Scale(this, 92),
            ColumnCount = 2,
            RowCount = 3,
        };
        nav.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, UiTheme.Scale(this, 56)));
        nav.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < 3; i++) nav.RowStyles.Add(new RowStyle(SizeType.Percent, 33));

        _region = NavCombo("cboEncRegion", nav, 0, "Region", OnRegionChanged);
        _town = NavCombo("cboEncTown", nav, 1, "Town", OnTownChanged);
        _league = NavCombo("cboEncLeague", nav, 2, "League", ReloadFiles);

        _search = new TextBox { Dock = DockStyle.Top, Name = "txtEncSearch", PlaceholderText = "filter these encounters." };
        _search.TextChanged += (_, _) => ReloadFiles();

        _files = new ListBox
        {
            Dock = DockStyle.Fill,
            Name = "lstEncFiles",
            IntegralHeight = false,
            BorderStyle = BorderStyle.None,
            HorizontalScrollbar = true,
        };
        _files.SelectedIndexChanged += (_, _) => OpenSelectedFile();

        var caption = new Label
        {
            Dock = DockStyle.Top,
            Height = UiTheme.Scale(this, 22),
            Text = "Encounters",
            Font = new Font(Font, FontStyle.Bold),
        };

        host.Controls.Add(_files);
        host.Controls.Add(_search);
        host.Controls.Add(nav);
        host.Controls.Add(caption);
        return host;
    }

    /// <summary>One league in the picker: what the game calls it, and what the file calls it.</summary>
    private sealed record LeagueItem(string Display, string Name)
    {
        public override string ToString() => Display;
    }

    /// <summary>One encounter in the picker: what it is called, and the file behind it.</summary>
    private sealed record EncounterItem(string Display, string Path)
    {
        public override string ToString() => Display;
    }

    private ComboBox NavCombo(string name, TableLayoutPanel grid, int row, string label, Action changed)
    {
        var combo = new ComboBox { Name = name, Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(0, 2, 0, 2) };
        combo.SelectedIndexChanged += (_, _) => changed();
        grid.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, row);
        grid.Controls.Add(combo, 1, row);
        return combo;
    }

    private void LoadNavigation()
    {
        if (string.IsNullOrWhiteSpace(_project) || !Directory.Exists(_project)) return;

        try
        {
            _arenas = ArenaCatalog.Arenas(_project);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        {
            _arenas = Array.Empty<ArenaCatalog.ArenaEntry>();
            SetStatus(ex.Message, error: true);
        }

        RefreshClassTags();

        _region.Items.Clear();
        foreach (string region in _arenas.Select(a => a.Region).Distinct(StringComparer.OrdinalIgnoreCase)) _region.Items.Add(region);
        if (_region.Items.Count > 0) _region.SelectedIndex = 0;
    }

    private void OnRegionChanged()
    {
        _town.Items.Clear();
        string? region = _region.SelectedItem?.ToString();
        foreach (string town in _arenas.Where(a => a.Region == region).Select(a => a.Town).Distinct(StringComparer.OrdinalIgnoreCase)) _town.Items.Add(town);
        if (_town.Items.Count > 0) _town.SelectedIndex = 0;
        OnTownChanged();
    }

    private void OnTownChanged()
    {
        _league.Items.Clear();
        _league.Items.Add(new LeagueItem(AllLeagues, AllLeagues));

        ArenaCatalog.ArenaEntry? arena = SelectedArena();
        if (arena is not null)
        {
            try
            {
                foreach (ArenaCatalog.LeagueEntry league in ArenaCatalog.Leagues(_project, arena.Arena))
                {
                    // "Civilis League of Pirgos" is what the game shows; "Apprentice Open League" is what the file calls it
                    string display = string.IsNullOrWhiteSpace(league.DisplayName) || league.DisplayName == league.Name
                        ? league.Name
                        : $"{league.DisplayName}  ({league.Name})";
                    _league.Items.Add(new LeagueItem(display, league.Name));
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
            {
                SetStatus(ex.Message, error: true);
            }
        }

        if (_league.Items.Count > 0) _league.SelectedIndex = 0;
        ReloadFiles();
    }

    private const string AllLeagues = "(all in arena)";

    /// <summary>The arena for the selected town; world.tok gives every town exactly one arena.</summary>
    private ArenaCatalog.ArenaEntry? SelectedArena() =>
        _arenas.FirstOrDefault(a => a.Region == _region.SelectedItem?.ToString() && a.Town == _town.SelectedItem?.ToString());

    private void ReloadFiles()
    {
        _files.BeginUpdate();
        try
        {
            _files.Items.Clear();
            if (string.IsNullOrWhiteSpace(_project) || !Directory.Exists(_project)) return;

            string filter = _search?.Text?.Trim() ?? string.Empty;
            ArenaCatalog.ArenaEntry? arena = SelectedArena();
            IEnumerable<string> paths = arena is null ? _api.ListFiles(_project) : LeagueEncounters();

            foreach (string path in paths)
            {
                string display = EncounterDisplayName(path);
                if (filter.Length > 0 && display.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0
                    && path.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                _files.Items.Add(new EncounterItem(display, path));
            }

            SetStatus($"{_files.Items.Count} encounter(s) - {arena?.Town ?? "whole project"}", error: false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        {
            SetStatus(ex.Message, error: true);
        }
        finally
        {
            _files.EndUpdate();
        }
    }

    /// <summary>
    /// What to show for an encounter: its <c>ENCOUNTERNAME</c>, because the folder, town and arena are already chosen -
    /// every list entry sharing the same path prefix was noise. Falls back to the file name when the file has no name.
    /// </summary>
    private string EncounterDisplayName(string relativePath)
    {
        string stem = Path.GetFileNameWithoutExtension(relativePath);

        // The game shows the league's name for the encounter ("Open Battle I"), not the file's own name ("Battle1"),
        // so prefer that and keep the stem beside it - it is what the file is called on disk.
        if (DisplayNames().TryGetValue(stem, out string? inGame) && !string.IsNullOrWhiteSpace(inGame))
        {
            return string.Equals(inGame, stem, StringComparison.OrdinalIgnoreCase) ? inGame! : $"{inGame}  ({stem})";
        }

        try
        {
            string full = Path.Combine(ProjectRoots.ResolveBecFolder(_project), relativePath.Replace('/', Path.DirectorySeparatorChar));
            foreach (string line in File.ReadLines(full).Take(40))
            {
                if (!line.TrimStart().StartsWith("ENCOUNTERNAME", StringComparison.OrdinalIgnoreCase)) continue;

                int first = line.IndexOf('"');
                int second = first < 0 ? -1 : line.IndexOf('"', first + 1);
                if (second > first) return line[(first + 1)..second];
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Log?.Write(LogLevel.Warning, "encounters", $"cannot read {relativePath}: {ex.Message}");
        }

        return stem;
    }

    private IReadOnlyDictionary<string, string>? _displayNames;
    private string? _displayNamesProject;

    /// <summary>In-game encounter names for this project, read once (see <see cref="ArenaCatalog.EncounterDisplayNames"/>).</summary>
    private IReadOnlyDictionary<string, string> DisplayNames()
    {
        if (_displayNames is not null && string.Equals(_displayNamesProject, _project, StringComparison.Ordinal)) return _displayNames;

        try
        {
            _displayNames = ArenaCatalog.EncounterDisplayNames(_project);
            _displayNamesProject = _project;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        {
            Log?.Write(LogLevel.Warning, "encounters", $"cannot read in-game encounter names: {ex.Message}");
            _displayNames = new Dictionary<string, string>();
            _displayNamesProject = _project;
        }

        return _displayNames;
    }

    /// <summary>Encounters in the selected arena, narrowed to one league when one is chosen.</summary>
    private IReadOnlyList<string> LeagueEncounters()
    {
        ArenaCatalog.ArenaEntry? arena = SelectedArena();
        if (arena is null) return Array.Empty<string>();

        string? leagueName = (_league.SelectedItem as LeagueItem)?.Name ?? _league.SelectedItem?.ToString();
        if (string.IsNullOrEmpty(leagueName) || leagueName == AllLeagues) return arena.Encounters;

        ArenaCatalog.LeagueEntry? league = ArenaCatalog.Leagues(_project, arena.Arena)
            .FirstOrDefault(l => string.Equals(l.Name, leagueName, StringComparison.OrdinalIgnoreCase));
        if (league is null) return arena.Encounters;

        var known = new HashSet<string>(arena.Encounters, StringComparer.OrdinalIgnoreCase);
        return league.EncounterPaths.Where(known.Contains).ToList();
    }

    private void OpenSelectedFile()
    {
        if (_files.SelectedItem is not EncounterItem item) return;

        try
        {
            _encounter = _api.Get(_project, item.Path);
            _draft.Clear();
            if (_encounter is null) return;

            // team order in the file: 0, 1, then the props team (-1)
            _teams.Items.Clear();
            foreach (EncounterTeamDto team in _encounter.Teams) _teams.Items.Add($"{TeamDisplay(team.Index)}");
            if (_teams.Items.Count > 0)
            {
                int preferred = Math.Max(0, _encounter.Teams.ToList().FindIndex(t => t.Index == 1));
                _teams.SelectedIndex = preferred < _teams.Items.Count ? preferred : 0;
            }

            SetStatus($"{item.Display} - {_encounter.Teams.Sum(t => t.Units.Count)} rows", error: false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        {
            SetStatus(ex.Message, error: true);
        }
    }

    // ---------------------------------------------------------------- editor

    // ---------------------------------------------------------------- editor

    /// <summary>Row height for a slot editor; two columns of these fit without scrolling.</summary>
    private int SlotRowHeight => UiTheme.Scale(this, 26);

    private Control BuildEditor()
    {
        var host = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };

        var top = new TableLayoutPanel { Dock = DockStyle.Top, Height = UiTheme.Scale(this, 30), ColumnCount = 4, RowCount = 1 };
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, UiTheme.Scale(this, 44)));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, UiTheme.Scale(this, 210)));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, UiTheme.Scale(this, 52)));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        _teams = new ComboBox { Name = "cboEncTeam", DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        _teams.SelectedIndexChanged += (_, _) => SelectTeam();

        _school = new ComboBox { Name = "cboEncSchool", DropDownStyle = ComboBoxStyle.DropDown, Dock = DockStyle.Fill };
        _school.Validated += (_, _) => ApplyTeamField(SchoolKey, _school.Text);
        _school.SelectedIndexChanged += (_, _) => UpdateSchoolHint();

        top.Controls.Add(Label("Team"), 0, 0);
        top.Controls.Add(_teams, 1, 0);
        top.Controls.Add(Label("School"), 2, 0);
        top.Controls.Add(_school, 3, 0);

        // A school token means nothing on its own (HOMETOWNSCHOOL is not a school), so the page explains what it resolves to.
        _schoolHint = new Label
        {
            Name = "lblEncSchoolHint",
            Dock = DockStyle.Top,
            Height = UiTheme.Scale(this, 34),
            ForeColor = UiTheme.Info,
            TextAlign = ContentAlignment.MiddleLeft,
        };

        _gridLabel = new Label { Name = "lblEncGrid", Dock = DockStyle.Top, Height = UiTheme.Scale(this, 20), TextAlign = ContentAlignment.MiddleLeft };

        _unitsCaption = new Label
        {
            Name = "lblEncUnits",
            Dock = DockStyle.Top,
            Height = UiTheme.Scale(this, 24),
            Font = new Font(Font, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
        };

        var rowBar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = UiTheme.Scale(this, 32),
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
        };
        _rowButtons.Add(RowButton("btnEncAddRow", "Add row", (_, _) => AddRow()));
        _rowButtons.Add(RowButton("btnEncDupRow", "Duplicate", (_, _) => DuplicateRow()));
        _rowButtons.Add(RowButton("btnEncDelRow", "Remove", (_, _) => RemoveRow()));
        _rowButtons.Add(RowButton("btnEncSaveRows", "Save rows", (_, _) => SaveRows()));
        _rowButtons.Add(RowButton("btnEncValidate", "Validate", (_, _) => ValidateAll()));
        foreach (Button button in _rowButtons) rowBar.Controls.Add(button);

        _rows = new ListBox { Dock = DockStyle.Top, Height = UiTheme.Scale(this, 92), Name = "lstEncRows", IntegralHeight = false };
        _rows.SelectedIndexChanged += (_, _) => SelectRow();

        _fieldsHost = new Panel { Dock = DockStyle.Fill, AutoScroll = false, Name = "pnlEncFields" };

        _status = new Label
        {
            Dock = DockStyle.Bottom,
            Height = UiTheme.Scale(this, 24),
            Name = "lblEncStatus",
            TextAlign = ContentAlignment.MiddleLeft,
        };

        host.Controls.Add(_fieldsHost);
        host.Controls.Add(_status);
        host.Controls.Add(_rows);
        host.Controls.Add(rowBar);
        host.Controls.Add(_unitsCaption);
        host.Controls.Add(_gridLabel);
        host.Controls.Add(_schoolHint);
        host.Controls.Add(top);

        BuildFieldEditors();
        return host;
    }

    private Label Label(string text) => new()
    {
        Text = text,
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleLeft,
        AutoSize = false,
    };

    private static Button RowButton(string name, string text, EventHandler onClick)
    {
        var button = new Button { Name = name, Text = text, AutoSize = true, Margin = new Padding(0, 0, 6, 0) };
        button.Click += onClick;
        return button;
    }

    /// <summary>Team names as the game presents them, so a bare "1" is not something the modder has to decode.</summary>
    private static string TeamDisplay(int index) => index switch
    {
        0 => "Team 0 - your gladiators",
        1 => "Team 1 - the enemy",
        -1 => "Props and neutrals",
        _ => $"Team {index}",
    };

    private void SelectTeam()
    {
        if (_encounter is null || _teams.SelectedIndex < 0) return;
        _teamIndex = _encounter.Teams[_teams.SelectedIndex].Index;
        EnsureDraft(_teamIndex);
        _school.Items.Clear();
        foreach (string name in SafeSchools()) _school.Items.Add(name);
        _school.Text = Unquote(_encounter.Team(_teamIndex)?.Value(SchoolKey) ?? string.Empty);
        UpdateSchoolHint();
        RefreshRows();
    }

    /// <summary>Explain a school token that is not a school name, by listing what it resolves to for this arena.</summary>
    private void UpdateSchoolHint()
    {
        if (_schoolHint is null) return;

        string token = _school.Text.Trim().ToUpperInvariant();
        if (token is not ("HOMETOWNSCHOOL" or "REGIONSCHOOL"))
        {
            _schoolHint.Text = string.Empty;
            return;
        }

        ArenaCatalog.ArenaEntry? arena = SelectedArena();
        IReadOnlyList<string> names = arena is null
            ? Array.Empty<string>()
            : token == "REGIONSCHOOL"
                ? WorldRegions.RegionSchools(_project, arena.Region)
                : WorldRegions.ArenaSchools(_project, arena.Arena);

        _schoolHint.Text = names.Count == 0
            ? $"{token} - no schools resolved for this arena."
            : $"{token} means this arena's own schools: {string.Join(", ", names.Take(6))}"
              + (names.Count > 6 ? $", +{names.Count - 6} more" : string.Empty);
    }

    private IReadOnlyList<string> SafeSchools()
    {
        if (string.IsNullOrWhiteSpace(_project)) return Array.Empty<string>();
        try { return _choices.EnemySchools(_project); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        {
            return Array.Empty<string>();
        }
    }

    private List<EncounterUnit> EnsureDraft(int teamIndex)
    {
        if (_draft.TryGetValue(teamIndex, out List<EncounterUnit>? rows)) return rows;

        rows = new List<EncounterUnit>();
        EncounterTeamDto? team = _encounter?.Team(teamIndex);
        if (team is not null)
        {
            foreach (string raw in team.Units)
            {
                // A row the parser cannot read is kept verbatim so the editor never silently drops it.
                EncounterUnit? unit = EncounterUnit.TryParse(raw);
                rows.Add(unit ?? new EncounterUnit { Raw = raw });
            }
        }

        _draft[teamIndex] = rows;
        return rows;
    }

    /// <summary>Split a raw row on commas, honouring quoted strings (a unit name may contain one).</summary>
    private static string[] Slots(string raw)
    {
        var parts = new List<string>();
        var current = new System.Text.StringBuilder();
        bool quoted = false;
        foreach (char c in raw)
        {
            if (c == '"') { quoted = !quoted; current.Append(c); continue; }
            if (c == ',' && !quoted) { parts.Add(current.ToString().Trim()); current.Clear(); continue; }
            current.Append(c);
        }

        parts.Add(current.ToString().Trim());
        return parts.ToArray();
    }
    private void RefreshRows()
    {
        if (_encounter is null) return;
        List<EncounterUnit> rows = EnsureDraft(_teamIndex);

        _rows.BeginUpdate();
        try
        {
            _rows.Items.Clear();
            for (int i = 0; i < rows.Count; i++) _rows.Items.Add(Describe(i, rows[i]));
        }
        finally
        {
            _rows.EndUpdate();
        }

        _gridLabel.Text = "Start grid: " + (_encounter.Team(_teamIndex)?.Value(GridKey) ?? "(none)");
        _unitsCaption.Text = $"Units in {TeamDisplay(_teamIndex)}  ({rows.Count})";

        _rowIndex = rows.Count > 0 ? 0 : -1;
        if (_rows.Items.Count > 0) _rows.SelectedIndex = 0;
        SelectRow();
    }

    private static string Describe(int index, EncounterUnit unit)
    {
        string[] slots = Slots(unit.Raw);
        if (slots.Length < EncounterUnit.SlotCount) return $"row {index + 1}  (unreadable row)";

        string name = slots[0].Trim('"', ' ');
        if (name.Length == 0) name = "(any unit)";
        string start = slots[2].Trim('"', ' ');
        string affinity = EncounterUnitFields.AffinityName(slots[7].Trim()) ?? "any affinity";
        return $"row {index + 1}  {name}  at {start}  {affinity}  {slots[1].Trim()}% chance";
    }

    private void SelectRow()
    {
        List<EncounterUnit> rows = EnsureDraft(_teamIndex);
        _rowIndex = _rows.SelectedIndex;
        bool has = _rowIndex >= 0 && _rowIndex < rows.Count;

        foreach (Button button in _rowButtons) button.Enabled = has || button.Name is "btnEncAddRow" or "btnEncValidate";

        string[] slots = has ? Slots(rows[_rowIndex].Raw) : Array.Empty<string>();
        foreach ((int slot, Control control) in _fieldControls)
        {
            bool unclear = UnclearSlots.Contains(slot);
            control.Enabled = has && (!unclear || _showUnclear?.Checked == true);
            if (has) SetControlValue(control, slot >= 1 && slot <= slots.Length ? slots[slot - 1] : string.Empty);
        }

        UpdateUnitWarning();
        UpdateRequirementsSummary();
    }

    // ---------------------------------------------------------------- fields

    /// <summary>Slots whose meaning is still unknown; hidden unless the modder asks for them.</summary>
    private static readonly int[] UnclearSlots = { 17, 20, 21 };

    /// <summary>Short, human labels. The long explanation lives in the tooltip so no line spills.</summary>
    private static string SlotLabel(EncounterUnitField field) => field.Slot switch
    {
        1 => "Unit",
        2 => "Chance %",
        3 => "Start cell",
        4 => "Level under",
        5 => "Level over",
        6 => "Level random",
        7 => "Requirements",
        8 => "Affinity",
        13 => "Not: class tags",
        14 => "Not: class",
        15 => "Not: attribute",
        16 => "Not: affinity",
        17 => "Unknown 17",
        18 => "Mercenary only",
        19 => "Chest drop %",
        20 => "Unknown flag A",
        21 => "Unknown flag B",
        _ => field.Label,
    };

    /// <summary>Plain-language help, shown on hover over the label and the control.</summary>
    private static string SlotHelp(EncounterUnitField field) => field.Slot switch
    {
        1 => "Exact unit name. A name that matches no unit makes the game spawn its debug mongrel (Bagordis), so use Find to "
             + "pick a real one. Leave it empty to let the school choose any unit that matches the requirements.",
        2 => "Chance out of 100 that this row appears. 100 always spawns it; below 100 the game treats the unit as optional "
             + "and reports it missing when it does not show up.",
        3 => "Which start marker the unit is placed on. Units that share a marker are spread over its cells at random.",
        4 => "How far below the team's level this unit may roll.",
        5 => "How far above the team's level this unit may roll.",
        6 => "Extra random levels on top of the offsets.",
        7 => "What the unit must be: body type, arcane or support, beast, gender, region. Tick the boxes with Choose; "
             + "bits the table does not name stay editable as a number.",
        8 => "Affinity the unit must have. Any affinity lets the school decide.",
        9 or 10 or 11 or 12 => "Extra class tag the unit must carry, on top of its school's own classes.",
        13 => "Inverts the class-tag test: the unit must NOT carry these tags.",
        14 => "Inverts the class requirement: the unit must NOT be this class.",
        15 => "Inverts the attribute test: the unit must NOT match these attributes.",
        16 => "Inverts the affinity test: the unit must NOT have this affinity.",
        17 => "Unknown. The engine passes it into unit creation and vanilla only ever uses 0-3.",
        18 => "Requires a mercenary for this slot. Every vanilla row is 0.",
        19 => "Chance, per item, that a chest drops.",
        20 => "Unknown spawn flag the engine sets. Vanilla never uses it.",
        21 => "Unknown spawn flag the engine sets. Vanilla never uses it.",
        _ => field.Description,
    };

    private void BuildFieldEditors()
    {
        var normal = EncounterUnitFields.All.Where(f => !UnclearSlots.Contains(f.Slot)).ToList();
        var unclear = EncounterUnitFields.All.Where(f => UnclearSlots.Contains(f.Slot)).ToList();

        var host = new Panel { Dock = DockStyle.Top, AutoSize = true };

        int rows = Math.Max(1, (normal.Count + 1) / 2);
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 4,
            RowCount = rows,
            Name = "tblEncFields",
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, UiTheme.Scale(this, 106)));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, UiTheme.Scale(this, 232)));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, UiTheme.Scale(this, 106)));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, UiTheme.Scale(this, 232)));
        for (int i = 0; i < rows; i++) grid.RowStyles.Add(new RowStyle(SizeType.Absolute, SlotRowHeight));

        for (int i = 0; i < normal.Count; i++) AddSlotRow(grid, normal[i], i / 2, i % 2 == 0 ? 0 : 2);

        host.Controls.Add(grid);

        // Fields nobody has explained yet are real, just not worth showing by default.
        _showUnclear = new CheckBox
        {
            Name = "chkEncUnclear",
            Text = "Show fields whose meaning is unknown (17, 20, 21)",
            Dock = DockStyle.Top,
            Height = SlotRowHeight,
        };
        _showUnclear.CheckedChanged += (_, _) => SetUnclearVisible(_showUnclear.Checked);
        host.Controls.Add(_showUnclear);

        var unclearGrid = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 4,
            RowCount = unclear.Count,
            Visible = false,
            Name = "tblEncUnclear",
        };
        unclearGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, UiTheme.Scale(this, 106)));
        unclearGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, UiTheme.Scale(this, 232)));
        unclearGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, UiTheme.Scale(this, 106)));
        unclearGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, UiTheme.Scale(this, 232)));
        for (int i = 0; i < unclear.Count; i++) unclearGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, SlotRowHeight));
        for (int i = 0; i < unclear.Count; i++) AddSlotRow(unclearGrid, unclear[i], i, 0);

        host.Controls.Add(unclearGrid);
        _unclearGrid = unclearGrid;

        _unitWarning = new Label
        {
            Name = "lblEncUnitWarn",
            Dock = DockStyle.Top,
            Height = UiTheme.Scale(this, 20),
            ForeColor = UiTheme.Warning,
        };
        host.Controls.Add(_unitWarning);

        _requirementsSummary = new Label
        {
            Name = "lblEncRequirements",
            Dock = DockStyle.Top,
            Height = UiTheme.Scale(this, 20),
            ForeColor = UiTheme.Info,
        };
        host.Controls.Add(_requirementsSummary);

        // Dock.Top stacks in reverse add order, so re-add in the order the modder should read them:
        // the fields first, the notes about them underneath.
        host.Controls.Clear();
        host.Controls.Add(_requirementsSummary);
        host.Controls.Add(_unitWarning);
        host.Controls.Add(unclearGrid);
        host.Controls.Add(_showUnclear);
        host.Controls.Add(grid);

        _fieldsHost.Controls.Add(host);
    }

    private void SetUnclearVisible(bool visible)
    {
        if (_unclearGrid is null) return;
        _unclearGrid.Visible = visible;
        foreach (int slot in UnclearSlots)
        {
            if (_fieldControls.TryGetValue(slot, out Control? control)) control.Enabled = visible && _rowIndex >= 0;
        }
    }

    private void AddSlotRow(TableLayoutPanel grid, EncounterUnitField field, int row, int column)
    {
        // Alternating bands are what shows which label belongs to which field; the slot numbers meant nothing to a modder.
        Color band = row % 2 == 1 ? UiTheme.SurfaceAlt : Color.Transparent;

        Control control = ControlFor(field);
        control.Name = $"encSlot{field.Slot}";
        control.Tag = field.Kind;
        control.Margin = new Padding(0, 2, 4, 2);
        control.Enabled = false;
        if (band != Color.Transparent) control.BackColor = band;

        int slot = field.Slot;
        Hook(control, () => CommitField(slot));
        _fieldControls[slot] = control;

        var label = new Label
        {
            Text = SlotLabel(field),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoSize = false,
            Margin = new Padding(6, 0, 4, 0),
            BackColor = band,
        };

        var tip = new ToolTip { AutoPopDelay = 20000, InitialDelay = 300, ReshowDelay = 100 };
        tip.SetToolTip(label, SlotHelp(field));
        tip.SetToolTip(control, SlotHelp(field));

        grid.Controls.Add(label, column, row);
        grid.Controls.Add(control, column + 1, row);

        if (slot == 1) AddUnitTools(control, grid, row, column);
        if (slot == 7) AddRequirementTools(control, grid, row, column);
    }

    /// <summary>Slot 1 gets a finder, because a name that matches nothing silently becomes Bagordis.</summary>
    private void AddUnitTools(Control control, TableLayoutPanel grid, int row, int column)
    {
        var find = new Button
        {
            Name = "btnEncFindUnit",
            Text = "Find",
            AutoSize = false,
            Width = UiTheme.Scale(this, 48),
            Height = SlotRowHeight - 6,
            Margin = new Padding(0, 2, 4, 2),
        };
        find.Click += (_, _) => PickUnit();

        var stack = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = new Padding(0) };
        grid.Controls.Remove(control);
        control.Dock = DockStyle.Fill;
        control.Width = UiTheme.Scale(this, 176);
        stack.Controls.Add(find);
        stack.Controls.Add(control);
        grid.Controls.Add(stack, column + 1, row);
    }

    /// <summary>Slot 7 gets a chooser and a readable summary, instead of a number nobody can decode.</summary>
    private void AddRequirementTools(Control control, TableLayoutPanel grid, int row, int column)
    {
        var choose = new Button
        {
            Name = "btnEncRequirements",
            Text = "Choose",
            AutoSize = false,
            Width = UiTheme.Scale(this, 64),
            Height = SlotRowHeight - 6,
            Margin = new Padding(0, 2, 4, 2),
        };
        choose.Click += (_, _) => ChooseRequirements();

        var stack = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = new Padding(0) };
        grid.Controls.Remove(control);
        control.Dock = DockStyle.Fill;
        control.Width = UiTheme.Scale(this, 160);
        stack.Controls.Add(choose);
        stack.Controls.Add(control);
        grid.Controls.Add(stack, column + 1, row);
    }

    private void PickUnit()
    {
        if (_rowIndex < 0 || !_fieldControls.TryGetValue(1, out Control? control)) return;

        using var dialog = new UnitPickerDialog(UnitRows(), control.Text.Trim());
        if (dialog.ShowDialog(this) != DialogResult.OK || string.IsNullOrWhiteSpace(dialog.SelectedUnit)) return;

        control.Text = dialog.SelectedUnit;
        CommitField(1);
    }

    private void ChooseRequirements()
    {
        if (_rowIndex < 0 || !_fieldControls.TryGetValue(7, out Control? control)) return;
        if (!int.TryParse(control.Text.Trim(), out int mask)) mask = 0;

        using var dialog = new AttributeMaskDialog(mask);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        control.Text = dialog.Mask.ToString(CultureInfo.InvariantCulture);
        CommitField(7);
    }

    private void UpdateUnitWarning()
    {
        if (_unitWarning is null) return;

        string name = _rowIndex >= 0 && _fieldControls.TryGetValue(1, out Control? control) ? control.Text.Trim() : string.Empty;
        if (name.Length == 0)
        {
            _unitWarning.Text = "No unit name: the school picks any unit that matches the requirements.";
            _unitWarning.ForeColor = UiTheme.Info;
            return;
        }

        bool known = UnitRows().Any(u => string.Equals(u.Name, name, StringComparison.OrdinalIgnoreCase));
        _unitWarning.Text = known
            ? string.Empty
            : $"'{name}' is not a unit the game knows - it spawns the debug mongrel (Bagordis).";
        _unitWarning.ForeColor = UiTheme.Warning;
    }

    private void UpdateRequirementsSummary()
    {
        if (_requirementsSummary is null) return;

        if (_rowIndex < 0 || !_fieldControls.TryGetValue(7, out Control? control)
            || !int.TryParse(control.Text.Trim(), out int mask))
        {
            _requirementsSummary.Text = string.Empty;
            return;
        }

        IReadOnlyList<string> names = EncounterUnitFields.AttributeNamesFor(mask);
        _requirementsSummary.Text = mask == 0
            ? "No attribute requirement."
            : "Requires: " + (names.Count > 0 ? string.Join(", ", names) : "(unnamed bits only)");
    }

    /// <summary>How many class-tag choices the dropdowns hold (driver use).</summary>
    public int ClassTagChoiceCount => _classTagCombos.Count == 0 ? 0 : _classTagCombos[0].Items.Count;

    /// <summary>Unit rows for the driver smoke test.</summary>
    public IReadOnlyList<UnitPickerDialog.UnitRow> UnitRowsForAutomation() => UnitRows();

    private IReadOnlyList<UnitPickerDialog.UnitRow> UnitRows()
    {
        if (string.IsNullOrWhiteSpace(_project)) return Array.Empty<UnitPickerDialog.UnitRow>();

        try
        {
            IReadOnlyDictionary<int, IReadOnlyList<string>> bySchool =
                _choices.GladiatorsBySchool(_project, out IReadOnlyDictionary<string, GladiatorInfo> details);

            return bySchool.SelectMany(pair => pair.Value.Select(name =>
            {
                GladiatorInfo info = details.TryGetValue(name, out GladiatorInfo? found)
                    ? found
                    : new GladiatorInfo(name, string.Empty, string.Empty, pair.Key);
                return new UnitPickerDialog.UnitRow(name, info.ClassName, info.Affinity, info.SchoolIndex);
            })).ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        {
            Log?.Write(LogLevel.Warning, "encounters", $"cannot read the unit list: {ex.Message}");
            return Array.Empty<UnitPickerDialog.UnitRow>();
        }
    }

    private const string NoneClassTag = "(none)";
    private readonly List<ComboBox> _classTagCombos = new();

    private Control ClassTagControl()
    {
        var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        combo.Items.Add(NoneClassTag);
        foreach (string tag in ClassTags()) combo.Items.Add(tag);
        combo.SelectedItem = NoneClassTag;
        _classTagCombos.Add(combo);
        return combo;
    }

    private IReadOnlyList<string> ClassTags()
    {
        if (string.IsNullOrWhiteSpace(_project)) return Array.Empty<string>();
        try { return _choices.ClassTags(_project); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        {
            return Array.Empty<string>();
        }
    }

    /// <summary>Refill the class-tag lists once a project is known; they are empty when the page is constructed.</summary>
    private void RefreshClassTags()
    {
        IReadOnlyList<string> tags = ClassTags();
        foreach (ComboBox combo in _classTagCombos)
        {
            string? keep = combo.SelectedItem?.ToString();
            combo.Items.Clear();
            combo.Items.Add(NoneClassTag);
            foreach (string tag in tags) combo.Items.Add(tag);
            combo.SelectedItem = keep is not null && combo.Items.Contains(keep) ? keep : NoneClassTag;
        }
    }

    private Control ControlFor(EncounterUnitField field) => field.Kind switch
    {
        EncounterFieldKind.Switch => new CheckBox { Text = string.Empty, CheckAlign = ContentAlignment.MiddleLeft },
        EncounterFieldKind.Choice => ChoiceControl(field),
        EncounterFieldKind.ClassTag => ClassTagControl(),
        EncounterFieldKind.Bitmask => new TextBox(),
        EncounterFieldKind.Integer => new NumericUpDown
        {
            Minimum = field.Minimum ?? -1_000_000,
            Maximum = field.Maximum ?? 1_000_000,
            ThousandsSeparator = false,
        },
        _ => new TextBox(),
    };

    private Control ChoiceControl(EncounterUnitField field)
    {
        var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };

        switch (field.Slot)
        {
            case 1:
                combo.DropDownStyle = ComboBoxStyle.DropDown;
                combo.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
                combo.AutoCompleteSource = AutoCompleteSource.ListItems;
                break;
            case 3:
                foreach (string marker in StartMarkers()) combo.Items.Add(marker);
                break;
            case 8:
                combo.Items.Add("-1 (any)");
                foreach ((string id, string name) in EncounterUnitFields.AffinityNames) combo.Items.Add($"{id} {name}");
                break;
            default:
                if (field.Choices is not null) foreach (string choice in field.Choices) combo.Items.Add(choice);
                break;
        }

        return combo;
    }

    private IReadOnlyList<string> StartMarkers()
    {
        try
        {
            string? grid = _encounter?.Team(_teamIndex)?.Value(GridKey);
            if (string.IsNullOrWhiteSpace(grid)) return new[] { "Start1", "Start2", "Start3", "Start4" };

            string relative = grid.Replace("$d\\", "data\\", StringComparison.OrdinalIgnoreCase)
                                  .Replace("$l\\", "levels\\", StringComparison.OrdinalIgnoreCase)
                                  .Replace('/', '\\');
            string full = Path.Combine(ProjectRoots.ResolveBecFolder(_project), relative);
            IReadOnlyList<string> markers = EncounterChoices.StartMarkers(full);
            return markers.Count > 0 ? markers : new[] { "Start1", "Start2", "Start3", "Start4" };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        {
            return new[] { "Start1", "Start2", "Start3", "Start4" };
        }
    }

    private static void Hook(Control control, Action commit)
    {
        switch (control)
        {
            case TextBox text: text.Validated += (_, _) => commit(); break;
            case ComboBox combo: combo.SelectedIndexChanged += (_, _) => commit(); combo.Validated += (_, _) => commit(); break;
            case NumericUpDown number: number.Validated += (_, _) => commit(); break;
            case CheckBox check: check.CheckedChanged += (_, _) => commit(); break;
        }
    }

    private static void SetControlValue(Control control, string text)
    {
        string value = text.Trim();
        switch (control)
        {
            case TextBox box: box.Text = value.Trim('"'); break;
            case NumericUpDown number:
                number.Value = decimal.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out decimal parsed)
                    ? Math.Clamp(parsed, number.Minimum, number.Maximum)
                    : 0;
                break;
            case CheckBox check: check.Checked = value is "1"; break;
            case ComboBox combo when combo.Tag is EncounterFieldKind.ClassTag:
                combo.SelectedItem = value.Trim('"').Length == 0 ? NoneClassTag : value.Trim('"').Trim();
                break;
            case ComboBox combo when combo.DropDownStyle == ComboBoxStyle.DropDownList:
                string match = combo.Items.Cast<object>().Select(o => o.ToString() ?? string.Empty)
                    .FirstOrDefault(item => item == value
                        || item.StartsWith(value + " ", StringComparison.Ordinal)
                        || item.Trim('"') == value.Trim('"')) ?? value;
                combo.SelectedItem = match;
                if (combo.SelectedIndex < 0 && combo.Items.Count > 0) combo.SelectedIndex = 0;
                break;
            case ComboBox combo:
                combo.Text = value.Trim('"');
                break;
        }
    }

    private string? ControlText(Control control) => control switch
    {
        TextBox box => box.Text,
        NumericUpDown number => number.Value.ToString(CultureInfo.InvariantCulture),
        CheckBox check => check.Checked ? "1" : "0",
        ComboBox combo when combo.Tag is EncounterFieldKind.ClassTag =>
            (combo.SelectedItem?.ToString() ?? NoneClassTag) == NoneClassTag ? string.Empty : (combo.SelectedItem?.ToString() ?? string.Empty),
        ComboBox combo when combo.DropDownStyle == ComboBoxStyle.DropDownList =>
            (combo.SelectedItem?.ToString() ?? string.Empty).Split(' ')[0],
        ComboBox combo => combo.Text,
        _ => null,
    };

    private void CommitField(int slot)
    {
        if (_rowIndex < 0 || _encounter is null) return;
        List<EncounterUnit> rows = EnsureDraft(_teamIndex);
        if (_rowIndex >= rows.Count) return;
        if (!_fieldControls.TryGetValue(slot, out Control? control)) return;

        string? text = ControlText(control);
        if (text is null) return;

        // slot 1 and the class tags are strings in the file; the editor holds them without quotes and the API adds them
        if (slot is 1 or 9 or 10 or 11 or 12) text = text.Trim();

        if (!EncounterUnitEdit.TryApply(rows[_rowIndex], slot, text, out EncounterUnit? updated, out string? error))
        {
            SetStatus($"slot {slot}: {error}", error: true);
            SelectRow();     // put the control back to the accepted value
            return;
        }

        rows[_rowIndex] = updated!;
        SetStatus($"slot {slot} = {text}  (unsaved)", error: false);
        _rows.Items[_rowIndex] = Describe(_rowIndex, updated!);
    }

    // ---------------------------------------------------------------- row and team actions

    private void AddRow()
    {
        if (_encounter is null) return;
        List<EncounterUnit> rows = EnsureDraft(_teamIndex);

        // a new row starts from a safe default: a random unit at Start1 with a 100% chance
        EncounterUnit? unit = EncounterUnit.TryParse("\"\", 100, \"Start1\", 0, 0, 0, 0, -1, \"\", \"\", \"\", \"\", 0, 0, 0, 0, 0, 0, 0, 0, 0");
        if (unit is null) return;

        rows.Add(unit);
        RefreshRows();
        _rows.SelectedIndex = rows.Count - 1;
        SetStatus("row added (unsaved)", error: false);
    }

    private void DuplicateRow()
    {
        List<EncounterUnit> rows = EnsureDraft(_teamIndex);
        if (_rowIndex < 0 || _rowIndex >= rows.Count) return;

        EncounterUnit? copy = EncounterUnit.TryParse(rows[_rowIndex].Raw);
        if (copy is null) return;

        rows.Insert(_rowIndex + 1, copy);
        RefreshRows();
        _rows.SelectedIndex = Math.Min(_rowIndex + 1, rows.Count - 1);
        SetStatus("row duplicated (unsaved)", error: false);
    }

    private void RemoveRow()
    {
        List<EncounterUnit> rows = EnsureDraft(_teamIndex);
        if (_rowIndex < 0 || _rowIndex >= rows.Count) return;

        rows.RemoveAt(_rowIndex);
        RefreshRows();
        SetStatus("row removed (unsaved)", error: false);
    }

    private void SaveRows()
    {
        if (_encounter is null) return;
        List<EncounterUnit> rows = EnsureDraft(_teamIndex);

        try
        {
            _api.SetTeamUnits(_project, _encounter.FilePath, _teamIndex,
                rows.Select(r => r.Raw).ToList());
            _encounter = _api.Get(_project, _encounter.FilePath);
            _draft.Clear();
            SelectTeam();
            SetStatus($"team {_teamIndex}: saved {rows.Count} row(s)", error: false);
            Log?.Write(LogLevel.Success, "encounters", $"Saved team {_teamIndex} of {_encounter?.Name}.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        {
            SetStatus(ex.Message, error: true);
        }
    }

    private void ApplyTeamField(string key, string value)
    {
        if (_encounter is null) return;
        string trimmed = value.Trim();
        if (trimmed.Length == 0) return;

        string raw = key is SchoolKey ? $"\"{trimmed}\"" : trimmed;
        if (Unquote(_encounter.Team(_teamIndex)?.Value(key) ?? string.Empty) == trimmed) return;

        try
        {
            _api.SetTeamField(_project, _encounter.FilePath, _teamIndex, key, raw);
            _encounter = _api.Get(_project, _encounter.FilePath);
            SetStatus($"{key} = {trimmed}", error: false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        {
            SetStatus(ex.Message, error: true);
        }
    }

    private void ValidateAll()
    {
        if (_encounter is null) return;

        try
        {
            IReadOnlyList<EncounterValidationIssue> issues = _api.Validate(_project, _encounter.FilePath);
            if (issues.Count == 0)
            {
                SetStatus("no issues found", error: false);
                Log?.Write(LogLevel.Success, "encounters", $"{_encounter.Name}: validation clean.");
                return;
            }

            SetStatus($"{issues.Count} issue(s): " + string.Join("; ", issues.Take(3).Select(i => i.Message)), error: true);
            foreach (EncounterValidationIssue issue in issues)
                Log?.Write(LogLevel.Warning, "encounters", $"{Path.GetFileName(_encounter.FilePath)}: {issue.Message}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        {
            SetStatus(ex.Message, error: true);
        }
    }

    private static string Unquote(string value)
    {
        string trimmed = value.Trim();
        return trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[^1] == '"' ? trimmed[1..^1] : trimmed;
    }

    private void SetStatus(string message, bool error)
    {
        if (_status is null) return;
        _status.Text = message;
        _status.ForeColor = error ? UiTheme.Error : UiTheme.Info;
    }

    // ---------------------------------------------------------------- driver surface

    /// <summary>Names of the controls the UI driver addresses, for tests and screenshots.</summary>
    public IEnumerable<string> ControlNames() =>
        new[] { "cboEncRegion", "cboEncTown", "cboEncLeague", "txtEncSearch", "lstEncFiles", "cboEncTeam", "cboEncSchool", "lstEncRows", "lblEncStatus", "lblEncGrid" }
            .Concat(_rowButtons.Select(b => b.Name))
            .Concat(_fieldControls.Values.Where(c => !string.IsNullOrEmpty(c.Name)).Select(c => c.Name));

    /// <summary>Load one encounter by its project-relative path (driver use).</summary>
    public void AutomationOpen(string relativePath)
    {
        SelectArenaFor(relativePath);
        int index = FindFile(relativePath);
        if (index < 0)
        {
            // not in the filtered view: clear the filter, then look again
            _search.Text = string.Empty;
            index = FindFile(relativePath);
        }
        if (index < 0) throw new InvalidOperationException($"no encounter '{relativePath}'");

        _files.SelectedIndex = index;
    }

    /// <summary>Point the navigators at whichever arena holds this path, so the picker and the file agree.</summary>
    /// <summary>Point the navigators at whichever town holds this path, so the picker and the file agree.</summary>
    private void SelectArenaFor(string relativePath)
    {
        string normalized = relativePath.Replace('\\', '/');
        ArenaCatalog.ArenaEntry? entry = _arenas.FirstOrDefault(a =>
            a.Encounters.Any(e => string.Equals(e, normalized, StringComparison.OrdinalIgnoreCase)));

        if (entry is null) return;
        if (entry.Region != _region.SelectedItem?.ToString()) _region.SelectedItem = entry.Region;
        if (entry.Town != _town.SelectedItem?.ToString()) _town.SelectedItem = entry.Town;
    }

    /// <summary>What the navigation currently offers (driver use).</summary>
    public object AutomationNavigation() => new
    {
        regions = _region.Items.Cast<object>().Select(o => o.ToString()).ToArray(),
        towns = _town.Items.Cast<object>().Select(o => o.ToString()).ToArray(),
        leagues = _league.Items.Cast<object>().Select(o => o.ToString()).ToArray(),
        selected = new
        {
            region = _region.SelectedItem?.ToString(),
            town = _town.SelectedItem?.ToString(),
            arena = SelectedArena()?.Arena,
            league = _league.SelectedItem?.ToString(),
        },
        files = _files.Items.Cast<EncounterItem>().Take(40).Select(i => i.Display).ToArray(),
        paths = _files.Items.Cast<EncounterItem>().Take(40).Select(i => i.Path).ToArray(),
        fileCount = _files.Items.Count,
    };

    /// <summary>Set the navigators (driver use). The arena follows from the town, so it is not settable.</summary>
    public void AutomationNavigate(string? region, string? town, string? arena, string? league)
    {
        if (region is not null && _region.Items.Contains(region)) _region.SelectedItem = region;
        if (town is not null && _town.Items.Contains(town)) _town.SelectedItem = town;
        if (league is not null)
        {
            foreach (object item in _league.Items)
            {
                if (item is LeagueItem candidate
                    && (candidate.Name == league || candidate.Display == league))
                {
                    _league.SelectedItem = item;
                    break;
                }
            }
        }
    }

    private int FindFile(string relativePath)
    {
        for (int i = 0; i < _files.Items.Count; i++)
        {
            if (_files.Items[i] is EncounterItem item
                && string.Equals(item.Path, relativePath, StringComparison.OrdinalIgnoreCase)) return i;
        }

        return -1;
    }

    /// <summary>What the picker is holding (driver use, for diagnosing the list).</summary>
    public object AutomationFiles(string? probe) => new
    {
        project = _project,
        count = _files.Items.Count,
        first = _files.Items.Cast<object>().Take(3).Select(o => o.ToString()).ToArray(),
        probeFound = probe is not null && FindFile(probe) >= 0,
        search = _search?.Text,
    };

    /// <summary>What each slot control is currently displaying (driver use, verifies the panel against the draft).</summary>
    public object AutomationControls() => _fieldControls
        .OrderBy(kv => kv.Key)
        .Select(kv => new { slot = kv.Key, text = ControlText(kv.Value), enabled = kv.Value.Enabled })
        .ToArray();

    /// <summary>State snapshot for the driver.</summary>
    public object AutomationState()
    {
        List<EncounterUnit> rows = EnsureDraft(_teamIndex);
        return new
        {
            project = _project,
            file = _encounter?.FilePath,
            name = _encounter?.Name,
            teams = _encounter?.Teams.Select(t => t.Index).ToArray() ?? Array.Empty<int>(),
            team = _teamIndex,
            school = _school?.Text,
            grid = _gridLabel?.Text,
            rowCount = rows.Count,
            row = _rowIndex,
            rows = rows.Select((r, i) => new { index = i, raw = r.Raw }).ToArray(),
            status = _status?.Text,
        };
    }

    /// <summary>Set one slot of the selected row through the same validation the UI uses (driver use).</summary>
    public object AutomationSetSlot(int slot, string text)
    {
        List<EncounterUnit> rows = EnsureDraft(_teamIndex);
        if (_rowIndex < 0 || _rowIndex >= rows.Count) throw new InvalidOperationException("no row selected");

        if (!EncounterUnitEdit.TryApply(rows[_rowIndex], slot, text, out EncounterUnit? updated, out string? error))
            return new { ok = false, error };

        rows[_rowIndex] = updated!;
        SelectRow();
        _rows.Items[_rowIndex] = Describe(_rowIndex, updated!);
        return new { ok = true, raw = updated!.Raw };
    }

    /// <summary>Read one slot of the selected row (driver use).</summary>
    public object AutomationReadRow()
    {
        List<EncounterUnit> rows = EnsureDraft(_teamIndex);
        if (_rowIndex < 0 || _rowIndex >= rows.Count) return new { ok = false };
        return new { ok = true, raw = rows[_rowIndex].Raw, slots = Slots(rows[_rowIndex].Raw), row = _rowIndex };
    }

    /// <summary>Select a row by index (driver use).</summary>
    public void AutomationSelectRow(int index)
    {
        if (index < 0 || index >= _rows.Items.Count) throw new InvalidOperationException($"no row {index}");
        _rows.SelectedIndex = index;
    }

    /// <summary>Save the selected team's rows through the API (driver use).</summary>
    public void AutomationSaveRows() => SaveRows();

    /// <summary>Append a default row (driver use).</summary>
    public void AutomationAddRow() => AddRow();

    /// <summary>Remove the selected row (driver use).</summary>
    public void AutomationRemoveRow() => RemoveRow();

    /// <summary>Run the encounter validator and put the result in the status line (driver use).</summary>
    public void AutomationValidate() => ValidateAll();

    /// <summary>Select a team by its index in the file (0, 1 or -1) (driver use).</summary>
    public void AutomationSelectTeam(int teamIndex)
    {
        for (int i = 0; i < _teams.Items.Count; i++)
        {
            if ((_encounter?.Teams[i].Index ?? int.MinValue) == teamIndex)
            {
                _teams.SelectedIndex = i;
                return;
            }
        }
        throw new InvalidOperationException($"no team {teamIndex}");
    }
}
