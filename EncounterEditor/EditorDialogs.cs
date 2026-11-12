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
    private readonly List<(string Name, int Bit)> _checked = new();
    private readonly NumericUpDown _other;

    public AttributeMaskDialog(int mask)
    {
        Text = "Requirements - what the unit must be";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(520, 380);
        Font = SystemFonts.MessageBoxFont;

        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(12),
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        var left = CheckList(AttributeMaskTable.Take(6), mask, grid, 0);
        var right = CheckList(AttributeMaskTable.Skip(6), mask, grid, 1);
        _checked.AddRange(left);
        _checked.AddRange(right);

        var otherRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 40,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(12, 0, 12, 0),
        };
        otherRow.Controls.Add(new Label
        {
            Text = "Other bits (unnamed)",
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
        };
        otherRow.Controls.Add(_other);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 46,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(12),
        };
        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Width = 90 };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 90 };
        buttons.Controls.Add(ok);
        buttons.Controls.Add(cancel);

        Controls.Add(grid);
        Controls.Add(otherRow);
        Controls.Add(buttons);
        AcceptButton = ok;
        CancelButton = cancel;
    }

    /// <summary>The chosen mask, or 0 when the dialog was cancelled.</summary>
    public int Mask { get; private set; }

    private static IEnumerable<(string Name, int Bit)> AttributeMaskTable => EncounterUnitFields.AttributeTable
        .Select(entry => (entry.Name, entry.Bit));

    private static int OtherBits(int mask)
    {
        int known = AttributeMaskTable.Aggregate(0, (bits, entry) => bits | entry.Bit);
        return mask & ~known;
    }

    private List<(string, int)> CheckList(IEnumerable<(string Name, int Bit)> entries, int mask, TableLayoutPanel grid, int column)
    {
        var captured = new List<(string, int)>();
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
        };

        foreach ((string name, int bit) in entries)
        {
            var box = new CheckBox
            {
                Text = $"{name}  (0x{bit:X})",
                Checked = (mask & bit) != 0,
                AutoSize = true,
                Padding = new Padding(0, 2, 0, 2),
            };
            panel.Controls.Add(box);
            captured.Add((name, bit));

            // keep the checkbox and its bit together for the read-back
            box.Tag = bit;
        }

        grid.Controls.Add(panel, column, 0);
        return captured;
    }

    /// <summary>Read back the ticked bits from the form's checkboxes.</summary>
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);
        if (DialogResult != DialogResult.OK) return;

        int mask = 0;
        foreach (Control column in Controls.OfType<TableLayoutPanel>().SelectMany(t => t.Controls.OfType<FlowLayoutPanel>()))
        {
            foreach (CheckBox box in column.Controls.OfType<CheckBox>())
            {
                if (box.Checked && box.Tag is int bit) mask |= bit;
            }
        }

        Mask = mask | (int)_other.Value;
    }
}

/// <summary>The names the engine accepts for the requirement mask.</summary>
public static class AttributeMaskTable
{
    /// <summary>Named attribute bits, in engine order (see <c>UnitConditionalLookUpAttr</c>).</summary>
    public static IReadOnlyList<(string Name, int Bit)> All { get; } = new[]
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
}

/// <summary>
/// Finds a unit by name when the modder only half-remembers it: search by name, class, affinity or school, see the match,
/// pick it.
/// </summary>
/// <remarks>
/// Typing a name that does not match a unit makes the game spawn its debug placeholder (Bagordis), so the risk is silent
/// wrongness rather than an error. A picker that lists what actually exists removes the guess.
/// </remarks>
public sealed class UnitPickerDialog : Form
{
    private readonly ListBox _list = new() { Dock = DockStyle.Fill, IntegralHeight = false };
    private readonly TextBox _search = new() { Dock = DockStyle.Top, PlaceholderText = "search name, class, affinity or school…" };
    private readonly IReadOnlyList<UnitRow> _units;

    /// <summary>One unit as the picker shows it.</summary>
    public sealed record UnitRow(string Name, string Class, string Affinity, int School);

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
        ClientSize = new Size(620, 460);
        Font = SystemFonts.MessageBoxFont;

        _search.TextChanged += (_, _) => Refill();
        _list.DoubleClick += (_, _) => Accept();

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 46,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(12),
        };
        var ok = new Button { Text = "Use this unit", Width = 130, DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "Cancel", Width = 90, DialogResult = DialogResult.Cancel };
        buttons.Controls.Add(ok);
        buttons.Controls.Add(cancel);

        var host = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10) };
        host.Controls.Add(_list);
        host.Controls.Add(_search);

        Controls.Add(host);
        Controls.Add(buttons);
        AcceptButton = ok;
        CancelButton = cancel;

        Refill();

        int index = _list.Items.Cast<UnitRow>().ToList()
            .FindIndex(u => string.Equals(u.Name, current, StringComparison.OrdinalIgnoreCase));
        if (index >= 0) _list.SelectedIndex = index;
        else if (_list.Items.Count > 0) _list.SelectedIndex = 0;
    }

    /// <summary>The chosen unit name, or null.</summary>
    public string? SelectedUnit => (_list.SelectedItem as UnitRow)?.Name;

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

                _list.Items.Add(unit);
            }
        }
        finally
        {
            _list.EndUpdate();
        }
    }

    private void Accept()
    {
        if (_list.SelectedItem is null) return;
        DialogResult = DialogResult.OK;
        Close();
    }
}
