namespace ModdingGUI.EncounterEditor;

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using ModdingGUI.Core.Api;

/// <summary>
/// Chooses the requirement mask (slot 7) by ticking attributes, instead of typing an integer nobody can compute.
/// </summary>
/// <remarks>
/// The integer packs three different tests - attribute bits (<c>0x103f</c>), a male/female presence test (<c>0xc0</c>) and
/// class-tag region bits (<c>0xf00</c>) - so a raw number is not something a modder should have to derive. Every named bit the
/// engine knows is a checkbox here; anything the table does not name stays reachable through the "other bits" box, which is
/// the honest way to expose bits whose meaning is still unknown.
/// </remarks>
public sealed class AttributeMaskDialog : Form
{
    private readonly NumericUpDown _other;

    public AttributeMaskDialog(int mask)
    {
        Text = "Requirements - what the unit must be";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(520, 400);
        Font = SystemFonts.MessageBoxFont;
        BackColor = UiTheme.Surface;
        ForeColor = UiTheme.Ink;

        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(12),
            BackColor = UiTheme.Surface,
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        IReadOnlyList<(string Name, int Bit)> table = AttributeMaskTable;
        CheckList(table.Take(6), mask, grid, 0);
        CheckList(table.Skip(6), mask, grid, 1);

        var otherRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 42,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(12, 0, 12, 0),
            BackColor = UiTheme.Surface,
        };
        otherRow.Controls.Add(new Label
        {
            Text = "Other bits (no name known)",
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(0, 10, 8, 0),
        });

        _other = new NumericUpDown
        {
            Width = 110,
            Minimum = 0,
            Maximum = 1_000_000,
            Value = OtherBits(mask),
            BackColor = UiTheme.SurfaceAlt,
            ForeColor = UiTheme.Ink,
        };
        otherRow.Controls.Add(_other);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 48,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(12),
            BackColor = UiTheme.Surface,
        };
        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Width = 92 };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 92 };
        buttons.Controls.Add(ok);
        buttons.Controls.Add(cancel);

        Controls.Add(grid);
        Controls.Add(otherRow);
        Controls.Add(buttons);
        AcceptButton = ok;
        CancelButton = cancel;
    }

    /// <summary>The chosen mask, set when the dialog is accepted.</summary>
    public int Mask { get; private set; }

    /// <summary>Named attribute bits, in engine order (see <c>UnitConditionalLookUpAttr</c>).</summary>
    public static IReadOnlyList<(string Name, int Bit)> AttributeMaskTable { get; } = new[]
    {
        ("Light", 0x1),
        ("Medium", 0x2),
        ("Heavy", 0x4),
        ("Arcane", 0x8),
        ("Support", 0x10),
        ("Beast", 0x20),
        ("Male", 0x40),
        ("Female", 0x80),
        ("Nordagh", 0x100),
        ("Imperia", 0x200),
        ("Expanse", 0x400),
        ("Steppes", 0x800),
    };

    private static int OtherBits(int mask)
    {
        int known = AttributeMaskTable.Aggregate(0, (bits, entry) => bits | entry.Bit);
        return mask & ~known;
    }

    private void CheckList(IEnumerable<(string Name, int Bit)> entries, int mask, TableLayoutPanel grid, int column)
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            BackColor = UiTheme.Surface,
        };

        foreach ((string name, int bit) in entries)
        {
            panel.Controls.Add(new CheckBox
            {
                Text = $"{name}  (0x{bit:X})",
                Checked = (mask & bit) != 0,
                AutoSize = true,
                Padding = new Padding(0, 2, 0, 2),
                Tag = bit,
            });
        }

        grid.Controls.Add(panel, column, 0);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);
        if (DialogResult != DialogResult.OK) return;

        int mask = 0;
        foreach (CheckBox box in Controls.OfType<TableLayoutPanel>()
                     .SelectMany(t => t.Controls.OfType<FlowLayoutPanel>())
                     .SelectMany(p => p.Controls.OfType<CheckBox>()))
        {
            if (box.Checked && box.Tag is int bit) mask |= bit;
        }

        Mask = mask | (int)_other.Value;
    }
}

/// <summary>
/// Finds a unit by name when the modder only half-remembers it: search by name, class or affinity, then pick from a table.
/// </summary>
/// <remarks>
/// Typing a name that matches no unit makes the game spawn its debug placeholder (Bagordis), so the risk is silent wrongness
/// rather than an error. A picker that lists what actually exists removes the guess - and it has to look like the rest of the
/// app, which means columns and the theme palette, not a raw record dump.
/// </remarks>
public sealed class UnitPickerDialog : Form
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
        PlaceholderText = "search name, class or affinity…",
        BackColor = UiTheme.SurfaceAlt,
        ForeColor = UiTheme.Ink,
        BorderStyle = BorderStyle.FixedSingle,
    };

    private readonly Label _count = new()
    {
        Dock = DockStyle.Bottom,
        Height = 20,
        ForeColor = UiTheme.InkMuted,
        TextAlign = ContentAlignment.MiddleLeft,
    };

    private readonly IReadOnlyList<UnitRow> _units;

    /// <summary>One unit as the picker shows it.</summary>
    public sealed record UnitRow(string Name, string Class, string Affinity, int School)
    {
        public override string ToString() => $"{Name} - {Class} - {Affinity}";
    }

    public UnitPickerDialog(IEnumerable<UnitRow> units, string? current)
    {
        // A gladiator can belong to several schools, so the same name may arrive more than once.
        _units = units.GroupBy(u => u.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(u => u.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        Text = "Find a unit";
        FormBorderStyle = FormBorderStyle.SizableToolWindow;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(660, 480);
        Font = SystemFonts.MessageBoxFont;
        BackColor = UiTheme.Surface;
        ForeColor = UiTheme.Ink;

        _list.Columns.Add("Unit", 220);
        _list.Columns.Add("Class", 190);
        _list.Columns.Add("Affinity", 110);
        _list.BackColor = UiTheme.SurfaceSunken;
        _list.ForeColor = UiTheme.Ink;

        _search.TextChanged += (_, _) => Refill();
        _search.KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Down || _list.Items.Count == 0) return;
            _list.Focus();
            if (_list.SelectedIndices.Count == 0) _list.Items[0].Selected = true;
            e.Handled = true;
        };
        _list.DoubleClick += (_, _) => Accept();

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 48,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(12),
            BackColor = UiTheme.Surface,
        };
        var ok = new Button { Text = "Use this unit", Width = 130, DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "Cancel", Width = 92, DialogResult = DialogResult.Cancel };
        buttons.Controls.Add(ok);
        buttons.Controls.Add(cancel);

        var host = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12), BackColor = UiTheme.Surface };
        host.Controls.Add(_list);
        host.Controls.Add(_count);
        host.Controls.Add(_search);

        Controls.Add(host);
        Controls.Add(buttons);
        AcceptButton = ok;
        CancelButton = cancel;

        Refill();

        int index = _list.Items.Cast<ListViewItem>()
            .ToList()
            .FindIndex(item => string.Equals(item.Text, current, StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
        {
            _list.Items[index].Selected = true;
            _list.Items[index].EnsureVisible();
        }
        else if (_list.Items.Count > 0)
        {
            _list.Items[0].Selected = true;
        }
    }

    /// <summary>Column headers, for the driver smoke test.</summary>
    public IReadOnlyList<string> ColumnHeaders => _list.Columns.Cast<ColumnHeader>().Select(c => c.Text).ToList();

    /// <summary>First visible row, for the driver smoke test.</summary>
    public string? FirstRow => _list.Items.Count == 0 ? null : string.Join(" | ", _list.Items[0].SubItems.Cast<ListViewItem.ListViewSubItem>().Select(s => s.Text));

    /// <summary>The chosen unit name, or null.</summary>
    public string? SelectedUnit => _list.SelectedItems.Count == 0 ? null : _list.SelectedItems[0].Text;

    private void Refill()
    {
        string filter = _search.Text.Trim();

        _list.BeginUpdate();
        try
        {
            _list.Items.Clear();
            foreach (UnitRow unit in _units)
            {
                if (filter.Length > 0
                    && unit.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0
                    && unit.Class.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0
                    && unit.Affinity.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                var item = new ListViewItem(unit.Name);
                item.SubItems.Add(unit.Class);
                item.SubItems.Add(unit.Affinity);
                _list.Items.Add(item);
            }
        }
        finally
        {
            _list.EndUpdate();
        }

        _count.Text = $"{_list.Items.Count} unit(s) of {_units.Count}";
    }

    private void Accept()
    {
        if (_list.SelectedItems.Count == 0) return;
        DialogResult = DialogResult.OK;
        Close();
    }
}
