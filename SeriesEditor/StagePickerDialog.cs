namespace ModdingGUI.SeriesEditor;

using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using ModdingGUI.Core.Api;

/// <summary>
/// Picks an existing league registration to copy into the current league.
/// </summary>
/// <remarks>
/// A series is built by recycling bouts that are already registered somewhere in the arena, because whether the engine accepts
/// a newly invented encounter name is untested. Copying the registration keeps the text id, so the in-game name still matches
/// the file the stage loads.
/// </remarks>
public sealed class StagePickerDialog : Form
{
    private readonly ListView _list = new()
    {
        Dock = DockStyle.Fill,
        View = View.Details,
        FullRowSelect = true,
        MultiSelect = false,
        HideSelection = false,
        BorderStyle = BorderStyle.FixedSingle,
    };

    private readonly TextBox _search = new()
    {
        Dock = DockStyle.Top,
        PlaceholderText = "search encounter name…",
        BackColor = UiTheme.SurfaceAlt,
        ForeColor = UiTheme.Ink,
        BorderStyle = BorderStyle.FixedSingle,
    };

    private readonly IReadOnlyList<LeagueEncounterDto> _all;

    public StagePickerDialog(IEnumerable<LeagueEncounterDto> registrations)
    {
        _all = registrations
            .GroupBy(r => r.Name, System.StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(r => r.Name, System.StringComparer.OrdinalIgnoreCase)
            .ToList();

        Text = "Add a stage - recycle an existing bout";
        FormBorderStyle = FormBorderStyle.SizableToolWindow;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(700, 460);
        Font = SystemFonts.MessageBoxFont;
        BackColor = UiTheme.Surface;
        ForeColor = UiTheme.Ink;

        _list.Columns.Add("Encounter", 240);
        _list.Columns.Add("Type", 100);
        _list.Columns.Add("Teams", 120);
        _list.Columns.Add("File", 220);
        _list.BackColor = UiTheme.SurfaceSunken;
        _list.ForeColor = UiTheme.Ink;

        _search.TextChanged += (_, _) => Refill();
        _list.DoubleClick += (_, _) => Accept();

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 48,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(12),
            BackColor = UiTheme.Surface,
        };
        var ok = new Button { Text = "Add this bout", Width = 130, DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "Cancel", Width = 92, DialogResult = DialogResult.Cancel };
        buttons.Controls.Add(ok);
        buttons.Controls.Add(cancel);

        var host = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12), BackColor = UiTheme.Surface };
        host.Controls.Add(_list);
        host.Controls.Add(_search);

        Controls.Add(host);
        Controls.Add(buttons);
        AcceptButton = ok;
        CancelButton = cancel;

        Refill();
        if (_list.Items.Count > 0) _list.Items[0].Selected = true;
    }

    /// <summary>The registration to copy, or null.</summary>
    public LeagueEncounterDto? Selected { get; private set; }

    private void Refill()
    {
        string filter = _search.Text.Trim();

        _list.BeginUpdate();
        try
        {
            _list.Items.Clear();
            foreach (LeagueEncounterDto registration in _all)
            {
                if (filter.Length > 0 && registration.Name.IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) < 0) continue;

                var item = new ListViewItem(registration.Name);
                item.SubItems.Add(registration.Value("TYPE")?.Trim().Trim('"') ?? string.Empty);
                item.SubItems.Add(registration.Teams?.Trim() ?? string.Empty);
                item.SubItems.Add(registration.EncounterFile?.Trim().Trim('"') ?? string.Empty);
                item.Tag = registration;
                _list.Items.Add(item);
            }
        }
        finally
        {
            _list.EndUpdate();
        }
    }

    private void Accept()
    {
        if (_list.SelectedItems.Count == 0) return;
        Selected = (LeagueEncounterDto?)_list.SelectedItems[0].Tag;
        if (Selected is null) return;

        DialogResult = DialogResult.OK;
        Close();
    }
}
