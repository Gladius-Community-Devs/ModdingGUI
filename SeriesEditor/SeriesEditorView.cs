namespace ModdingGUI.SeriesEditor;

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
/// The series editor: the staged runs a league offers, in order, with the fields that decide how they behave.
/// </summary>
/// <remarks>
/// <para>
/// A "series" is not a file type. The <c>*_series*.enc</c> files are ordinary encounters; what makes a run is the league
/// entry, and specifically its <c>TYPE</c>, which the corpus uses with five values:
/// </para>
/// <list type="table">
/// <item><term>Staged</term><description>260 entries - the common multi-stage run.</description></item>
/// <item><term>Endurance</term><description>94 - described in the data as "the player must endure a wave of ... battles".</description></item>
/// <item><term>Series</term><description>69 - a named sequence of bouts.</description></item>
/// <item><term>Open</term><description>89 - a single bout, not a run.</description></item>
/// <item><term>Circus</term><description>8 - special-setup bouts with props.</description></item>
/// </list>
/// <para>
/// Which of those keep a gladiator's health and affinity between stages and which reset is <b>not</b> recorded in the data -
/// the encounter files of a carry-over run and a reset run have identical key sets - so the editor names the behaviour the
/// owner described for the two families and leaves the choice explicit rather than pretending to know.
/// </para>
/// <para>
/// Editing recycles existing bouts: a stage is added by copying another registration from the same arena (name, text id,
/// description, file, teams, type), because whether the engine accepts a newly invented encounter name is untested. The
/// order is the progression, so moving a stage rewrites the affected registrations through the league API rather than
/// shuffling raw text.
/// </para>
/// </remarks>
public sealed class SeriesEditorView : UserControl
{
    private const string LeagueRoot = "data/towns/leagues";

    private readonly LeaguesApi _api = new();
    private readonly EncounterChoices _choices = new();

    private string _project = string.Empty;
    private string _arena = string.Empty;
    private string _leagueName = string.Empty;
    private LeagueDto? _league;
    private int _stageIndex = -1;
    private bool _loading;

    private readonly List<ArenaCatalog.ArenaEntry> _arenas = new();
    private readonly List<(string League, LeagueDto Dto)> _leagueCache = new();

    private ComboBox _region = null!;
    private ComboBox _town = null!;
    private ComboBox _leagueBox = null!;
    private ListBox _stages = null!;
    private Label _status = null!;

    private ComboBox _encounterFile = null!;
    private ComboBox _type = null!;
    private NumericUpDown[] _teams = Array.Empty<NumericUpDown>();
    private NumericUpDown _points = null!;
    private NumericUpDown _frequency = null!;
    private NumericUpDown _entryFee = null!;
    private Label _teamsHint = null!;
    private Label _typeHint = null!;
    private Label? _typeConsistency;

    /// <summary>Where messages go.</summary>
    public IAppLog? Log { get; init; }

    private const string NoTypeLabel = "(no type - the file has no TYPE line)";

    /// <summary>The <c>TYPE</c> values the corpus uses, with the behaviour each is believed to have.</summary>
    public static IReadOnlyList<(string Value, string Label)> StageTypes { get; } = new[]
    {
        ("Series", "Series - returns to squad selection after each win; you cannot leave until the run is done"),
        ("Endurance", "Endurance - the only TYPE the engine special-cases (sets flag 0x10); carry-over candidate"),
        ("Staged", "Staged - engine default, like Series: squad selection after each win, team not carried"),
        ("Open", "Open - a single bout, not a run"),
        ("Circus", "Circus - special bout with props on the field"),
    };

    public SeriesEditorView()
    {
        Dock = DockStyle.Fill;

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, UiTheme.Scale(this, 400)));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.Controls.Add(BuildPicker(), 0, 0);
        layout.Controls.Add(BuildEditor(), 1, 0);
        Controls.Add(layout);
    }

    /// <summary>Point the page at a project.</summary>
    public void SetProject(string projectPath)
    {
        _project = projectPath ?? string.Empty;
        LoadNavigation();
    }

    // ---------------------------------------------------------------- picker

    private Control BuildPicker()
    {
        var host = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };

        var nav = new TableLayoutPanel { Dock = DockStyle.Top, Height = UiTheme.Scale(this, 92), ColumnCount = 2, RowCount = 3 };
        nav.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, UiTheme.Scale(this, 58)));
        nav.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < 3; i++) nav.RowStyles.Add(new RowStyle(SizeType.Percent, 33));

        _region = NavCombo("cboSeriesRegion", nav, 0, "Region", OnRegionChanged);
        _town = NavCombo("cboSeriesTown", nav, 1, "Town", OnTownChanged);
        _leagueBox = NavCombo("cboSeriesLeague", nav, 2, "League", OnLeagueChanged);

        _stages = new ListBox
        {
            Dock = DockStyle.Fill,
            Name = "lstSeriesStages",
            IntegralHeight = false,
            HorizontalScrollbar = true,
        };
        _stages.SelectedIndexChanged += (_, _) => SelectStage();

        var caption = new Label
        {
            Dock = DockStyle.Top,
            Height = UiTheme.Scale(this, 22),
            Text = "Stages, in play order",
            Font = new Font(Font, FontStyle.Bold),
        };

        host.Controls.Add(_stages);
        host.Controls.Add(caption);
        host.Controls.Add(nav);
        return host;
    }

    private ComboBox NavCombo(string name, TableLayoutPanel grid, int row, string label, Action changed)
    {
        var combo = new ComboBox
        {
            Name = name,
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Margin = new Padding(0, 2, 0, 2),
        };
        combo.SelectedIndexChanged += (_, _) => { if (!_loading) changed(); };
        grid.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, row);
        grid.Controls.Add(combo, 1, row);
        return combo;
    }

    private void LoadNavigation()
    {
        if (string.IsNullOrWhiteSpace(_project) || !Directory.Exists(_project)) return;

        _loading = true;
        try
        {
            _arenas.Clear();
            _arenas.AddRange(ArenaCatalog.Arenas(_project));

            _region.Items.Clear();
            foreach (string region in _arenas.Select(a => a.Region).Distinct(StringComparer.OrdinalIgnoreCase)) _region.Items.Add(region);
            if (_region.Items.Count > 0) _region.SelectedIndex = 0;
        }
        finally
        {
            _loading = false;
        }

        OnRegionChanged();
    }

    private void OnRegionChanged()
    {
        _loading = true;
        try
        {
            _town.Items.Clear();
            string? region = _region.SelectedItem?.ToString();
            foreach (string town in _arenas.Where(a => a.Region == region).Select(a => a.Town).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                _town.Items.Add(town);
            }

            if (_town.Items.Count > 0) _town.SelectedIndex = 0;
        }
        finally
        {
            _loading = false;
        }

        OnTownChanged();
    }

    private void OnTownChanged()
    {
        _loading = true;
        try
        {
            _leagueBox.Items.Clear();
            _leagueCache.Clear();

            ArenaCatalog.ArenaEntry? arena = SelectedArena();
            _arena = arena?.Arena ?? string.Empty;

            if (arena is not null)
            {
                string relative = $"{LeagueRoot}/{_arena.ToLowerInvariant()}_leagues.tok";
                string full = Path.Combine(ProjectRoots.ResolveBecFolder(_project), relative.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(full))
                {
                    LeagueFileDto file = _api.Get(_project, relative);

                    // display names come from ArenaCatalog, which already resolves the league lookup ids
                    var displays = ArenaCatalog.Leagues(_project, _arena)
                        .ToDictionary(l => l.Name, l => l.DisplayName ?? l.Name, StringComparer.OrdinalIgnoreCase);

                    foreach (LeagueDto league in file.Leagues)
                    {
                        // only leagues that stage more than one bout are runs
                        if (league.Encounters.Count < 2) continue;

                        string display = displays.TryGetValue(league.Name, out string? name) ? name : league.Name;
                        if (display.Length > 48) display = string.Concat(display.AsSpan(0, 46), ".");
                        _leagueCache.Add((league.Name, league));
                        _leagueBox.Items.Add($"{display}  ({league.Name})");
                    }
                }
            }
            if (_leagueBox.Items.Count > 0) _leagueBox.SelectedIndex = 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        {
            SetStatus(ex.Message, error: true);
        }
        finally
        {
            _loading = false;
        }

        OnLeagueChanged();
    }

    private void OnLeagueChanged()
    {
        if (_leagueBox.SelectedIndex < 0 || _leagueBox.SelectedIndex >= _leagueCache.Count)
        {
            _league = null;
            RefreshStages();
            return;
        }

        _leagueName = _leagueCache[_leagueBox.SelectedIndex].League;
        _league = _leagueCache[_leagueBox.SelectedIndex].Dto;
        RefreshStages();
    }

    private ArenaCatalog.ArenaEntry? SelectedArena() =>
        _arenas.FirstOrDefault(a => a.Region == _region.SelectedItem?.ToString() && a.Town == _town.SelectedItem?.ToString());

    private string LeagueRelativePath => $"{LeagueRoot}/{_arena.ToLowerInvariant()}_leagues.tok";

    /// <summary>
    /// Warn when the stages of a run disagree about their type.
    /// </summary>
    /// <remarks>
    /// Tested in game: a four-stage Series run with two stages flipped to Endurance sent the player back to the League Office
    /// after the first battle instead of continuing. Vanilla never mixes types inside one league, so the editor says so rather
    /// than letting a modder discover it by losing the run.
    /// </remarks>
    private void UpdateTypeConsistency()
    {
        if (_typeConsistency is null) return;

        if (_league is null || _league.Encounters.Count < 2)
        {
            _typeConsistency.Text = string.Empty;
            return;
        }

        var types = _league.Encounters
            .Select(stage => stage.Value("TYPE")?.Trim().Trim('"') ?? string.Empty)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (types.Count <= 1)
        {
            _typeConsistency.Text = "All stages share one type, which is how every vanilla run is built.";
            _typeConsistency.ForeColor = UiTheme.Info;
            return;
        }

        _typeConsistency.Text = $"This run mixes types ({string.Join(", ", types.Select(t => t.Length == 0 ? "(none)" : t))}). "
            + "A mixed Series/Endurance run was tested in game and returned the player to the League Office after the first "
            + "battle, so keep a run to one type.";
        _typeConsistency.ForeColor = UiTheme.Warning;
    }
    private void RefreshStages()
    {
        _stages.BeginUpdate();
        try
        {
            _stages.Items.Clear();
            if (_league is null) return;

            for (int i = 0; i < _league.Encounters.Count; i++)
            {
                LeagueEncounterDto encounter = _league.Encounters[i];
                string type = encounter.Value("TYPE")?.Trim().Trim('"') ?? "(no type)";
                _ = Path.GetFileNameWithoutExtension(encounter.EncounterFile?.Trim('"') ?? string.Empty);
                string teams = encounter.Teams?.Trim() ?? string.Empty;
                string compact = teams.Replace(" ", "v").TrimEnd('v');
                _stages.Items.Add($"{i + 1}.  {type,-10}  {compact,-12}  {encounter.Name}");
            }
        }
        finally
        {
            _stages.EndUpdate();
        }

        UpdateTypeConsistency();

        _stageIndex = _league?.Encounters.Count > 0 ? 0 : -1;
        if (_stages.Items.Count > 0) _stages.SelectedIndex = 0;
        SelectStage();
    }

    private void SelectStage()
    {
        _stageIndex = _stages.SelectedIndex;
        bool has = _league is not null && _stageIndex >= 0 && _stageIndex < _league.Encounters.Count;
        SetEditorEnabled(has);
        if (!has) return;

        _loading = true;
        try
        {
            LeagueEncounterDto stage = _league!.Encounters[_stageIndex];

            string type = stage.Value("TYPE")?.Trim().Trim('"') ?? string.Empty;
            _type.SelectedIndex = Math.Max(0, StageTypes.ToList().FindIndex(t => string.Equals(t.Value, type, StringComparison.OrdinalIgnoreCase)));

            FillEncounterFiles();
            string file = stage.EncounterFile?.Trim().Trim('"') ?? string.Empty;
            int index = _encounterFile.Items.Cast<object>()
                .Select(o => o.ToString() ?? string.Empty)
                .ToList()
                .FindIndex(item => string.Equals(item, file, StringComparison.OrdinalIgnoreCase));
            if (index < 0 && file.Length > 0) _encounterFile.Items.Add(file);
            _encounterFile.SelectedItem = index >= 0 ? _encounterFile.Items[index] : file;

            string[] teams = (stage.Teams ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < _teams.Length; i++)
            {
                _teams[i].Value = i < teams.Length
                    && int.TryParse(teams[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
                    ? Math.Clamp(value, 0, 12)
                    : 0;
            }

            _points.Value = ReadNumber(stage, "ENCPTS", 0, 0, 99);
            _frequency.Value = ReadNumber(stage, "FREQUENCY", 100, 0, 100);
            _entryFee.Value = ReadNumber(stage, "ENTRYFEE", 0, 0, 1_000_000);

            UpdateHints(stage);
            SetStatus($"{_leagueName}: stage {_stageIndex + 1} of {_league.Encounters.Count}", error: false);
        }
        finally
        {
            _loading = false;
        }
    }

    private static decimal ReadNumber(LeagueEncounterDto stage, string key, decimal fallback, decimal min, decimal max)
    {
        string? raw = stage.Value(key)?.Trim();
        return decimal.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out decimal value)
            ? Math.Clamp(value, min, max)
            : fallback;
    }

    /// <summary>Explain the type, and warn when the declared team sizes disagree with the encounter file.</summary>
    private void UpdateHints(LeagueEncounterDto stage)
    {
        int typeIndex = _type.SelectedIndex;
        _typeHint.Text = typeIndex <= 0
            ? "No TYPE line: the engine falls back to its own default - set one deliberately."
            : typeIndex - 1 < StageTypes.Count ? StageTypes[typeIndex - 1].Label : string.Empty;

        string? file = stage.EncounterFile?.Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(file))
        {
            _teamsHint.Text = "No ENCFILE: the engine has nothing to load for this stage.";
            _teamsHint.ForeColor = UiTheme.Warning;
            return;
        }

        string full = Path.Combine(ProjectRoots.ResolveBecFolder(_project),
            "data", "encounters", file.Replace('\\', Path.DirectorySeparatorChar));
        if (!File.Exists(full))
        {
            _teamsHint.Text = $"'{file}' does not exist in this project.";
            _teamsHint.ForeColor = UiTheme.Warning;
            return;
        }

        // The player's team comes from the roster, not from a TEAM block, and team -1 is the props team, so the
        // comparison is: declared non-zero team sizes minus the player, against enemy TEAM blocks in the file.
        int declared = _teams.Count(t => t.Value > 0);
        int enemies = Math.Max(0, declared - 1);
        int enemyBlocks = File.ReadLines(full).Count(line =>
        {
            string text = line.TrimStart();
            if (!text.StartsWith("TEAM:", StringComparison.OrdinalIgnoreCase)) return false;
            string index = text[5..].Trim();
            return int.TryParse(index.Split(',')[0], out int value) && value >= 0;
        });

        _teamsHint.Text = enemyBlocks == enemies
            ? $"{declared} team(s) declared, {enemyBlocks} enemy TEAM block(s) in the file."
            : $"{declared} team(s) declared but {enemyBlocks} enemy TEAM block(s) in the file - check TEAMS.";
        _teamsHint.ForeColor = enemyBlocks == enemies ? UiTheme.Info : UiTheme.Warning;
    }

    // ---------------------------------------------------------------- editor

    private Control BuildEditor()
    {
        var host = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };

        var bar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = UiTheme.Scale(this, 32),
            WrapContents = false,
        };
        bar.Controls.Add(BarButton("btnSeriesSave", "Save stage", (_, _) => SaveStage()));
        bar.Controls.Add(BarButton("btnSeriesUp", "Move up", (_, _) => MoveStage(-1)));
        bar.Controls.Add(BarButton("btnSeriesDown", "Move down", (_, _) => MoveStage(1)));
        bar.Controls.Add(BarButton("btnSeriesAdd", "Add stage", (_, _) => AddStage()));
        bar.Controls.Add(BarButton("btnSeriesRemove", "Remove stage", (_, _) => RemoveStage()));
        bar.Controls.Add(BarButton("btnSeriesCloneLeague", "Copy this league.", (_, _) => CloneLeague()));
        bar.Controls.Add(BarButton("btnSeriesOpenEncounter", "Edit its encounter", (_, _) => OpenEncounter()));
        bar.Controls.Add(BarButton("btnSeriesValidate", "Validate this league file", (_, _) => ValidateLeague()));

        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 8,
            Name = "tblSeriesFields",
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, UiTheme.Scale(this, 120)));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, UiTheme.Scale(this, 420)));
        for (int i = 0; i < 8; i++) grid.RowStyles.Add(new RowStyle(SizeType.Absolute, UiTheme.Scale(this, 28)));

        _encounterFile = new ComboBox { Name = "cboSeriesFile", Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        _type = new ComboBox { Name = "cboSeriesType", Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        _type.Items.Add(NoTypeLabel);
        foreach ((string _, string label) in StageTypes) _type.Items.Add(label);
        _type.SelectedIndexChanged += (_, _) => { if (!_loading) UpdateHints(CurrentStage()); };

        var teams = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Margin = new Padding(0) };
        _teams = new NumericUpDown[5];
        for (int i = 0; i < _teams.Length; i++)
        {
            _teams[i] = new NumericUpDown
            {
                Name = $"numSeriesTeam{i}",
                Width = UiTheme.Scale(this, 52),
                Minimum = 0,
                Maximum = 12,
                Margin = new Padding(0, 2, 6, 2),
            };
            teams.Controls.Add(_teams[i]);
        }

        _points = new NumericUpDown { Name = "numSeriesPoints", Width = UiTheme.Scale(this, 90), Minimum = 0, Maximum = 99 };
        _frequency = new NumericUpDown { Name = "numSeriesFrequency", Width = UiTheme.Scale(this, 90), Minimum = 0, Maximum = 100 };
        _entryFee = new NumericUpDown { Name = "numSeriesEntryFee", Width = UiTheme.Scale(this, 110), Minimum = 0, Maximum = 1_000_000 };

        AddRow(grid, 0, "Encounter file", _encounterFile,
            "The .enc this stage loads. Reusing a file that already exists is the safe route: whether the engine accepts a newly invented encounter is untested.");
        AddRow(grid, 1, "Type", _type,
            "How the run behaves. The data does not record which types keep health between stages, so pick the behaviour you want and verify it in game.");
        AddRow(grid, 2, "Teams", teams,
            "Team sizes in order; the first is the player's team. A zero means the team is absent.");
        AddRow(grid, 3, "Points", _points, "ENCPTS: progress this stage awards in the league.");
        AddRow(grid, 4, "Frequency", _frequency, "FREQUENCY: how often the office offers this stage, out of 100.");
        AddRow(grid, 5, "Entry fee", _entryFee, "ENTRYFEE: gold charged to enter.");

        _typeHint = new Label
        {
            Name = "lblSeriesTypeHint",
            Dock = DockStyle.Top,
            Height = UiTheme.Scale(this, 22),
            ForeColor = UiTheme.Info,
        };

        _teamsHint = new Label
        {
            Name = "lblSeriesTeamsHint",
            Dock = DockStyle.Top,
            Height = UiTheme.Scale(this, 22),
            ForeColor = UiTheme.Info,
        };

        _typeConsistency = new Label
        {
            Name = "lblSeriesTypes",
            Dock = DockStyle.Top,
            Height = UiTheme.Scale(this, 34),
            ForeColor = UiTheme.Info,
        };

        _status = new Label
        {
            Name = "lblSeriesStatus",
            Dock = DockStyle.Bottom,
            Height = UiTheme.Scale(this, 24),
            TextAlign = ContentAlignment.MiddleLeft,
        };

        host.Controls.Add(grid);
        host.Controls.Add(_teamsHint);
        host.Controls.Add(_typeHint);
        host.Controls.Add(bar);
        host.Controls.Add(_typeConsistency);
        host.Controls.Add(_typeConsistency);
        host.Controls.Add(_status);
        return host;
    }

    private Button BarButton(string name, string text, EventHandler onClick)
    {
        var button = new Button { Name = name, Text = text, AutoSize = true, Margin = new Padding(0, 0, 6, 0) };
        button.Click += onClick;
        return button;
    }

    private void AddRow(TableLayoutPanel grid, int row, string label, Control field, string help)
    {
        grid.Controls.Add(new Label
        {
            Text = label,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            BackColor = row % 2 == 1 ? UiTheme.SurfaceAlt : Color.Transparent,
        }, 0, row);

        var tip = new ToolTip { AutoPopDelay = 20000 };
        tip.SetToolTip(field, help);
        grid.Controls.Add(field, 1, row);
    }

    private void SetEditorEnabled(bool enabled)
    {
        foreach (Control control in new Control[]
                 {
                     _encounterFile, _type, _points, _frequency, _entryFee,
                 })
        {
            control.Enabled = enabled;
        }

        foreach (NumericUpDown team in _teams) team.Enabled = enabled;
    }

    private LeagueEncounterDto? CurrentStage() =>
        _league is not null && _stageIndex >= 0 && _stageIndex < _league.Encounters.Count
            ? _league.Encounters[_stageIndex]
            : null;

    private void FillEncounterFiles()
    {
        string? keep = _encounterFile.SelectedItem?.ToString();
        _encounterFile.Items.Clear();

        ArenaCatalog.ArenaEntry? arena = SelectedArena();
        if (arena is null) return;

        foreach (string path in arena.Encounters) _encounterFile.Items.Add(ToGamePath(path));
        if (keep is not null && keep.Length > 0 && !_encounterFile.Items.Contains(keep)) _encounterFile.Items.Add(keep);
    }

    /// <summary>Project path (<c>data/encounters/...</c>) to the game's own <c>region\arena\file.enc</c> form.</summary>
    private static string ToGamePath(string projectPath)
    {
        const string prefix = "data/encounters/";
        string relative = projectPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? projectPath[prefix.Length..]
            : projectPath;
        return relative.Replace('/', '\\');
    }

    // ---------------------------------------------------------------- actions

    private void SaveStage()
    {
        LeagueEncounterDto? stage = CurrentStage();
        if (stage is null || _league is null) return;

        try
        {
            string relative = LeagueRelativePath;

            if (_type.SelectedIndex >= 0)
            {
                string type = StageTypes[_type.SelectedIndex].Value;
                if (!string.Equals(stage.Value("TYPE")?.Trim().Trim('"'), type, StringComparison.OrdinalIgnoreCase))
                {
                    _api.SetEncounterField(_project, relative, _leagueName, stage.Name, "TYPE", $"\"{type}\"");
                }
            }

            string teams = string.Join(" ", _teams.Select(t => ((int)t.Value).ToString(CultureInfo.InvariantCulture)));
            if (!string.Equals((stage.Teams ?? string.Empty).Trim(), teams, StringComparison.Ordinal))
            {
                _api.SetEncounterField(_project, relative, _leagueName, stage.Name, "TEAMS", teams);
            }

            SetNumber(stage, "ENCPTS", _points.Value, relative);
            SetNumber(stage, "FREQUENCY", _frequency.Value, relative);
            SetNumber(stage, "ENTRYFEE", _entryFee.Value, relative);

            if (_encounterFile.SelectedItem is string file && !string.IsNullOrWhiteSpace(file))
            {
                string current = stage.EncounterFile?.Trim().Trim('"') ?? string.Empty;
                if (!string.Equals(current, file, StringComparison.OrdinalIgnoreCase))
                {
                    _api.SetEncounterFile(_project, relative, _leagueName, stage.Name, "data/encounters/" + file.Replace('\\', '/'));
                }
            }

            ReloadLeague(_stageIndex);
            SetStatus($"saved stage {_stageIndex + 1}", error: false);
            Log?.Write(LogLevel.Success, "series", $"Saved '{stage.Name}' in {_leagueName}.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        {
            SetStatus(ex.Message, error: true);
            Log?.Write(LogLevel.Error, "series", ex.Message);
        }
    }

    private void SetNumber(LeagueEncounterDto stage, string key, decimal value, string relative)
    {
        string text = ((int)value).ToString(CultureInfo.InvariantCulture);
        if (string.Equals((stage.Value(key) ?? string.Empty).Trim(), text, StringComparison.Ordinal)) return;

        if (stage.Value(key) is null)
        {
            // a stage without the key: the league API can only replace, so add it by rewriting the entry with the field
            Log?.Write(LogLevel.Warning, "series", $"'{stage.Name}' has no {key} line; left unchanged.");
            return;
        }

        _api.SetEncounterField(_project, relative, _leagueName, stage.Name, key, text);
    }

    private void MoveStage(int direction)
    {
        if (_league is null || _stageIndex < 0) return;

        int target = _stageIndex + direction;
        if (target < 0 || target >= _league.Encounters.Count) return;

        // The order is the progression, and the league API can only append, so the affected registrations are removed and
        // re-added in the new order. Comments inside those blocks do not survive that, which the status line says.
        Reorder(_stageIndex, target);
    }

    private void Reorder(int from, int to)
    {
        if (_league is null) return;

        try
        {
            string relative = LeagueRelativePath;
            List<LeagueEncounterDto> order = _league.Encounters.ToList();
            LeagueEncounterDto moved = order[from];
            order.RemoveAt(from);
            order.Insert(to, moved);

            foreach (LeagueEncounterDto stage in _league.Encounters) _api.RemoveEncounter(_project, relative, _leagueName, stage.Name);

            foreach (LeagueEncounterDto stage in order)
            {
                _api.AddEncounter(_project, relative, _leagueName, stage.Name, stage.RawValue, stage.Fields);
            }

            ReloadLeague(to);
            SetStatus($"moved '{moved.Name}' to position {to + 1} (comments inside the re-written blocks are lost)", error: false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        {
            SetStatus(ex.Message, error: true);
        }
    }

    private void AddStage()
    {
        if (_league is null) return;

        using var dialog = new StagePickerDialog(AllRegistrations());
        if (dialog.ShowDialog(this) != DialogResult.OK || dialog.Selected is null) return;

        try
        {
            string relative = LeagueRelativePath;
            LeagueEncounterDto source = dialog.Selected;

            // the copy keeps the source's text id and description, so the in-game name still matches the file it loads
            _api.AddEncounter(_project, relative, _leagueName, UniqueName(source.Name), source.RawValue, source.Fields);

            ReloadLeague(_league.Encounters.Count);
            SetStatus($"added '{source.Name}' to {_leagueName}", error: false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        {
            SetStatus(ex.Message, error: true);
        }
    }

    private string UniqueName(string baseName)
    {
        if (_league is null) return baseName;

        var taken = new HashSet<string>(_league.Encounters.Select(e => e.Name), StringComparer.OrdinalIgnoreCase);
        if (!taken.Contains(baseName)) return baseName;

        for (int i = 2; ; i++)
        {
            string candidate = $"{baseName}_{i}";
            if (!taken.Contains(candidate)) return candidate;
        }
    }

    /// <summary>Every registration in this arena's leagues, so a stage can be recycled rather than invented.</summary>
    private IReadOnlyList<LeagueEncounterDto> AllRegistrations()
    {
        var all = new List<LeagueEncounterDto>();
        foreach ((string _, LeagueDto dto) in _leagueCache) all.AddRange(dto.Encounters);
        return all;
    }

    private void RemoveStage()
    {
        LeagueEncounterDto? stage = CurrentStage();
        if (stage is null || _league is null) return;

        try
        {
            _api.RemoveEncounter(_project, LeagueRelativePath, _leagueName, stage.Name);
            ReloadLeague(Math.Max(0, _stageIndex - 1));
            SetStatus($"removed '{stage.Name}'", error: false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        {
            SetStatus(ex.Message, error: true);
        }
    }

    /// <summary>
    /// Copy the whole run into a new league, so a modder can build one from scratch out of bouts that already exist.
    /// </summary>
    /// <remarks>
    /// Whether the engine accepts a newly invented league is untested; the clone reuses the arena's encounter files (which are
    /// known to load) and gives the new league its own display text, so what is being tested is the league block itself.
    /// </remarks>
    private void CloneLeague()
    {
        if (_league is null || _leagueName.Length == 0) return;

        string? name = TextPromptDialog.Ask(this, "New league",
            "Internal name for the copied league (letters, digits, underscore):", $"{_leagueName}_Copy");
        if (name is null) return;

        string? display = TextPromptDialog.Ask(this, "New league",
            "In-game name to show on the arena screen:", $"{name} (test)");
        if (display is null) return;

        try
        {
            int id = new LookupTextApi().Add(ProjectRoots.ResolveBecFolder(_project), display);
            _api.CloneLeague(_project, LeagueRelativePath, _leagueName, name, id.ToString(CultureInfo.InvariantCulture));

            _leagueName = name;
            OnTownChanged();
            SetStatus($"copied '{name}' into {Path.GetFileName(LeagueRelativePath)} as a new league", error: false);
            Log?.Write(LogLevel.Success, "series", $"Cloned a league as '{name}' with display text '{display}' (lookup id {id}).");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        {
            SetStatus(ex.Message, error: true);
        }
    }

    /// <summary>Clone a league without the dialogs (driver use).</summary>
    public object AutomationCloneLeague(string name, string display)
    {
        if (_league is null || _leagueName.Length == 0) return new { ok = false, error = "no league selected" };

        try
        {
            int id = new LookupTextApi().Add(ProjectRoots.ResolveBecFolder(_project), display);
            _api.CloneLeague(_project, LeagueRelativePath, _leagueName, name, id.ToString(CultureInfo.InvariantCulture));
            _leagueName = name;
            OnTownChanged();
            return new { ok = true, id, league = name };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        {
            return new { ok = false, error = ex.Message };
        }
    }
    private void ValidateLeague()
    {
        if (string.IsNullOrWhiteSpace(_arena)) return;

        try
        {
            IReadOnlyList<LeagueValidationIssue> issues = _api.Validate(_project, LeagueRelativePath);
            if (issues.Count == 0)
            {
                SetStatus("league validation clean", error: false);
                return;
            }

            SetStatus($"{issues.Count} issue(s): " + string.Join("; ", issues.Take(3).Select(i => i.Message)), error: true);
            foreach (LeagueValidationIssue issue in issues)
            {
                Log?.Write(LogLevel.Warning, "series", $"{Path.GetFileName(LeagueRelativePath)}: {issue.Message}");
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        {
            SetStatus(ex.Message, error: true);
        }
    }

    private void OpenEncounter()
    {
        LeagueEncounterDto? stage = CurrentStage();
        if (stage?.EncounterFile is null) return;

        string file = stage.EncounterFile.Trim().Trim('"').Replace('\\', '/');
        EncounterRequested?.Invoke(this, "data/encounters/" + file);
    }

    /// <summary>Raised with a project-relative encounter path when the modder asks to edit the stage's encounter.</summary>
    public event EventHandler<string>? EncounterRequested;

    private void ReloadLeague(int selectIndex)
    {
        try
        {
            LeagueFileDto file = _api.Get(_project, LeagueRelativePath);
            LeagueDto? reloaded = file.Leagues.FirstOrDefault(l => string.Equals(l.Name, _leagueName, StringComparison.OrdinalIgnoreCase));
            if (reloaded is null) return;

            _league = reloaded;
            RefreshStages();

            if (_stages.Items.Count > 0)
            {
                _stages.SelectedIndex = Math.Clamp(selectIndex, 0, _stages.Items.Count - 1);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        {
            SetStatus(ex.Message, error: true);
        }
    }

    private void SetStatus(string message, bool error)
    {
        if (_status is null) return;
        _status.Text = message;
        _status.ForeColor = error ? UiTheme.Error : UiTheme.Info;
    }

    // ---------------------------------------------------------------- driver

    /// <summary>State snapshot for the UI driver.</summary>
    public object AutomationState()
    {
        LeagueEncounterDto? stage = CurrentStage();
        return new
        {
            project = _project,
            arena = _arena,
            league = _leagueName,
            leagues = _leagueBox.Items.Cast<object>().Select(o => o.ToString()).ToArray(),
            stages = _stages.Items.Cast<object>().Select(o => o.ToString()).ToArray(),
            index = _stageIndex,
            stage = stage?.Name,
            type = stage?.Value("TYPE"),
            teams = stage?.Teams,
            file = stage?.EncounterFile,
            points = stage?.Value("ENCPTS"),
            frequency = stage?.Value("FREQUENCY"),
            entryFee = stage?.Value("ENTRYFEE"),
            status = _status?.Text,
        };
    }

    /// <summary>Select a league by its internal name (driver use).</summary>
    public void AutomationSelectLeague(string leagueName)
    {
        for (int i = 0; i < _leagueCache.Count; i++)
        {
            if (!string.Equals(_leagueCache[i].League, leagueName, StringComparison.OrdinalIgnoreCase)) continue;
            _leagueBox.SelectedIndex = i;
            return;
        }

        throw new InvalidOperationException($"no league '{leagueName}' in {_arena}");
    }

    /// <summary>Select a stage by index (driver use).</summary>
    public void AutomationSelectStage(int index)
    {
        if (index < 0 || index >= _stages.Items.Count) throw new InvalidOperationException($"no stage {index}");
        _stages.SelectedIndex = index;
        // the index can already be the same after a league switch, so the event would not fire and the editors
        // would keep the previous league's stage while the list shows the new one
        SelectStage();
    }

    /// <summary>Set the type through the same validation the UI uses (driver use).</summary>
    public object AutomationSetType(string type)
    {
        int index = StageTypes.ToList().FindIndex(t => string.Equals(t.Value, type, StringComparison.OrdinalIgnoreCase));
        if (index < 0) return new { ok = false, error = $"unknown type '{type}'" };

        _type.SelectedIndex = index;
        SaveStage();
        return new { ok = true, type = StageTypes[index].Value };
    }

    /// <summary>Set the declared team sizes (driver use).</summary>
    public object AutomationSetTeams(params int[] sizes)
    {
        for (int i = 0; i < _teams.Length; i++) _teams[i].Value = i < sizes.Length ? Math.Clamp(sizes[i], 0, 12) : 0;
        SaveStage();
        return new { ok = true };
    }

    /// <summary>Set the encounter file of the selected stage (driver use).</summary>
    public object AutomationSetFile(string gamePath)
    {
        string item = gamePath.Replace('/', '\\');
        int index = _encounterFile.Items.Cast<object>().Select(o => o.ToString()).ToList().IndexOf(item);
        if (index < 0) return new { ok = false, error = $"'{item}' is not an encounter in this arena" };

        _encounterFile.SelectedIndex = index;
        SaveStage();
        return new { ok = true, file = item };
    }

    /// <summary>Run the league's own validator (driver use).</summary>
    public void AutomationValidate() => ValidateLeague();

    /// <summary>Move the selected stage (driver use).</summary>
    public void AutomationMove(int direction) => MoveStage(direction);
}
