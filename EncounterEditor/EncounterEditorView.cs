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
        _league.Items.Add(AllLeagues);

        ArenaCatalog.ArenaEntry? arena = SelectedArena();
        if (arena is not null)
        {
            try
            {
                foreach (ArenaCatalog.LeagueEntry league in ArenaCatalog.Leagues(_project, arena.Arena)) _league.Items.Add(league.Name);
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

        string? leagueName = _league.SelectedItem?.ToString();
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
            foreach (EncounterTeamDto team in _encounter.Teams) _teams.Items.Add($"Team {team.Index}");
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

    private Control BuildEditor()
    {
        var host = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };

        var top = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = UiTheme.Scale(this, 34),
            ColumnCount = 4,
            RowCount = 1,
        };
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, UiTheme.Scale(this, 56)));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, UiTheme.Scale(this, 150)));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, UiTheme.Scale(this, 64)));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        _teams = new ComboBox { Name = "cboEncTeam", DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        _teams.SelectedIndexChanged += (_, _) => SelectTeam();

        _school = new ComboBox { Name = "cboEncSchool", DropDownStyle = ComboBoxStyle.DropDown, Dock = DockStyle.Fill };
        _school.Validated += (_, _) => ApplyTeamField(SchoolKey, _school.Text);

        _gridLabel = new Label { Name = "lblEncGrid", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };

        top.Controls.Add(Label("Team"), 0, 0);
        top.Controls.Add(_teams, 1, 0);
        top.Controls.Add(Label("School"), 2, 0);
        top.Controls.Add(_school, 3, 0);

        var gridRow = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = UiTheme.Scale(this, 28),
            ColumnCount = 2,
            RowCount = 1,
        };
        gridRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, UiTheme.Scale(this, 56)));
        gridRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        gridRow.Controls.Add(Label("Grid"), 0, 0);
        gridRow.Controls.Add(_gridLabel, 1, 0);

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
        foreach (Button b in _rowButtons) rowBar.Controls.Add(b);

        _rows = new ListBox
        {
            Dock = DockStyle.Top,
            Height = UiTheme.Scale(this, 132),
            Name = "lstEncRows",
            IntegralHeight = false,
        };
        _rows.SelectedIndexChanged += (_, _) => SelectRow();

        _fieldsHost = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Name = "pnlEncFields" };

        _status = new Label
        {
            Dock = DockStyle.Bottom,
            Height = UiTheme.Scale(this, 26),
            Name = "lblEncStatus",
            TextAlign = ContentAlignment.MiddleLeft,
        };

        host.Controls.Add(_fieldsHost);
        host.Controls.Add(_status);
        host.Controls.Add(_rows);
        host.Controls.Add(rowBar);
        host.Controls.Add(gridRow);
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

    private Button RowButton(string name, string text, EventHandler onClick)
    {
        var button = new Button
        {
            Name = name,
            Text = text,
            AutoSize = true,
            Margin = new Padding(0, 0, 6, 0),
        };
        button.Click += onClick;
        return button;
    }

    private void SelectTeam()
    {
        if (_encounter is null || _teams.SelectedIndex < 0) return;
        _teamIndex = _encounter.Teams[_teams.SelectedIndex].Index;
        EnsureDraft(_teamIndex);
        _school.Items.Clear();
        foreach (string name in SafeSchools()) _school.Items.Add(name);
        _school.Text = Unquote(_encounter.Team(_teamIndex)?.Value(SchoolKey) ?? string.Empty);
        RefreshRows();
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
                EncounterUnit? unit = EncounterUnit.TryParse(raw);
                if (unit is null)
                {
                    // A row the parser cannot read is kept verbatim so the editor never silently drops it; its slots are
                    // unavailable until the text is corrected outside, and the row shows as unreadable.
                    unit = new EncounterUnit { Raw = raw };
                }
                rows.Add(unit);
            }
        }

        _draft[teamIndex] = rows;
        return rows;
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

        _gridLabel.Text = _encounter.Team(_teamIndex)?.Value(GridKey) ?? string.Empty;
        _rowIndex = rows.Count > 0 ? 0 : -1;
        if (_rows.Items.Count > 0) _rows.SelectedIndex = 0;
        SelectRow();
    }

    private static string Describe(int index, EncounterUnit unit)
    {
        string[] slots = Slots(unit.Raw);
        if (slots.Length < EncounterUnit.SlotCount) return $"#{index}  (unreadable row)";

        string name = slots[0].Trim('"', ' ');
        if (name.Length == 0) name = "(random)";
        string start = slots[2].Trim('"', ' ');
        string affinity = EncounterUnitFields.AffinityName(slots[7].Trim()) ?? slots[7].Trim();
        string level = $"{slots[3].Trim()}/{slots[4].Trim()}/{slots[5].Trim()}";
        return $"#{index}  {name}  {start}  aff={affinity}  lvl={level}  x{slots[1].Trim()}%";
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

    private void SelectRow()
    {
        List<EncounterUnit> rows = EnsureDraft(_teamIndex);
        _rowIndex = _rows.SelectedIndex;
        bool has = _rowIndex >= 0 && _rowIndex < rows.Count;

        foreach (Button b in _rowButtons) b.Enabled = has || b.Name is "btnEncAddRow" or "btnEncValidate";

        string[] slots = has ? Slots(rows[_rowIndex].Raw) : Array.Empty<string>();
        foreach ((int slot, Control control) in _fieldControls)
        {
            control.Enabled = has;
            if (!has) continue;
            SetControlValue(control, slot >= 1 && slot <= slots.Length ? slots[slot - 1] : string.Empty);
        }
    }

    // ---------------------------------------------------------------- fields

    private void BuildFieldEditors()
    {
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 3,
            RowCount = EncounterUnitFields.All.Count,
            Name = "tblEncFields",
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, UiTheme.Scale(this, 34)));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, UiTheme.Scale(this, 128)));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        foreach (EncounterUnitField field in EncounterUnitFields.All)
        {
            Control control = ControlFor(field);
            control.Name = $"encSlot{field.Slot}";
            control.Tag = field.Kind;
            control.Dock = DockStyle.Fill;
            control.Margin = new Padding(0, 2, 0, 2);
            control.Enabled = false;
            int slot = field.Slot;
            Hook(control, () => CommitField(slot));
            _fieldControls[slot] = control;

            var tip = new ToolTip();
            tip.SetToolTip(control, field.Description);

            int row = grid.RowCount - EncounterUnitFields.All.Count + field.Slot - 1;
            grid.Controls.Add(new Label
            {
                Text = field.Slot.ToString(CultureInfo.InvariantCulture),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight,
            }, 0, row);

            grid.Controls.Add(new Label
            {
                Text = field.Label,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoSize = false,
            }, 1, row);

            grid.Controls.Add(control, 2, row);
        }

        _fieldsHost.Controls.Add(grid);
    }

    private Control ClassTagControl()
    {
        var combo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Name = "encClassTag",
        };

        combo.Items.Add(NoneClassTag);
        foreach (string tag in ClassTags()) combo.Items.Add(tag);
        return combo;
    }

    private const string NoneClassTag = "(none)";

    private IReadOnlyList<string> ClassTags()
    {
        if (string.IsNullOrWhiteSpace(_project)) return Array.Empty<string>();
        try { return _choices.ClassTags(_project); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        {
            return Array.Empty<string>();
        }
    }

    private Control ControlFor(EncounterUnitField field) => field.Kind switch
    {
        EncounterFieldKind.Switch => new CheckBox { Text = string.Empty, CheckAlign = ContentAlignment.MiddleLeft },

        EncounterFieldKind.Choice => ChoiceControl(field),

        EncounterFieldKind.Integer => new NumericUpDown
        {
            Minimum = field.Minimum ?? -1_000_000,
            Maximum = field.Maximum ?? 1_000_000,
            ThousandsSeparator = false,
        },

        // A closed list on purpose: a mistyped class tag means the unit never spawns, and the class list is right there.
        EncounterFieldKind.ClassTag => ClassTagControl(),

        _ => new TextBox(),
    };

    private Control ChoiceControl(EncounterUnitField field)
    {
        var combo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Name = $"encChoice{field.Slot}",
        };

        if (field.Slot == 8)
        {
            combo.Items.Add("-1 (any)");
            foreach ((string id, string name) in EncounterUnitFields.AffinityNames) combo.Items.Add($"{id} {name}");
        }
        else if (field.Slot == 3)
        {
            foreach (string marker in StartMarkers()) combo.Items.Add(marker);
        }
        else if (field.Choices is not null)
        {
            foreach (string choice in field.Choices) combo.Items.Add(choice);
        }

        if (field.Slot == 1)
        {
            combo.DropDownStyle = ComboBoxStyle.DropDown;
            combo.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            combo.AutoCompleteSource = AutoCompleteSource.ListItems;
            foreach (string name in GladiatorNames()) combo.Items.Add(name);
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

    private IReadOnlyList<string> GladiatorNames()
    {
        if (string.IsNullOrWhiteSpace(_project)) return Array.Empty<string>();
        try
        {
            IReadOnlyDictionary<int, IReadOnlyList<string>> bySchool =
                _choices.GladiatorsBySchool(_project, out IReadOnlyDictionary<string, GladiatorInfo> _);
            return bySchool.Values.SelectMany(v => v).Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(v => v, StringComparer.OrdinalIgnoreCase).ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        {
            return Array.Empty<string>();
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
            case ComboBox combo:
                if (combo.DropDownStyle == ComboBoxStyle.DropDownList)
                {
                    // choices are stored as ids or names, but displayed with a label (e.g. "1 Earth")
                    string match = combo.Items.Cast<object>().Select(o => o.ToString() ?? string.Empty)
                        .FirstOrDefault(item => item == value
                            || item.StartsWith(value + " ", StringComparison.Ordinal)
                            || item.Trim('"') == value.Trim('"'))
                        ?? value;
                    combo.SelectedItem = match;
                    if (combo.SelectedIndex < 0 && combo.Items.Count > 0) combo.SelectedIndex = 0;
                }
                else
                {
                    combo.Text = value.Trim('"');
                }
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
        if (league is not null && _league.Items.Contains(league)) _league.SelectedItem = league;
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
