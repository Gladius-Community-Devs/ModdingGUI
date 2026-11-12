namespace ModdingGUI.SeriesEditor;

using System.Drawing;
using System.Windows.Forms;

/// <summary>A one-field prompt, for naming a new league.</summary>
/// <remarks>
/// The app has no input box of its own and <c>Microsoft.VisualBasic.Interaction.InputBox</c> would drag in a dependency for one
/// string; this is the same thing in twenty lines, on the app's palette.
/// </remarks>
public sealed class TextPromptDialog : Form
{
    private readonly TextBox _value;

    public TextPromptDialog(string title, string label, string initial)
    {
        Text = title;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(440, 150);
        Font = SystemFonts.MessageBoxFont;
        BackColor = UiTheme.Surface;
        ForeColor = UiTheme.Ink;

        var caption = new Label
        {
            Text = label,
            Dock = DockStyle.Top,
            Height = 34,
            Padding = new Padding(12, 10, 12, 0),
            TextAlign = ContentAlignment.MiddleLeft,
        };

        _value = new TextBox
        {
            Text = initial,
            Dock = DockStyle.Top,
            Margin = new Padding(12),
            BackColor = UiTheme.SurfaceAlt,
            ForeColor = UiTheme.Ink,
            BorderStyle = BorderStyle.FixedSingle,
        };

        var host = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12), BackColor = UiTheme.Surface };
        host.Controls.Add(_value);
        host.Controls.Add(caption);

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

        Controls.Add(host);
        Controls.Add(buttons);
        AcceptButton = ok;
        CancelButton = cancel;
        Shown += (_, _) => _value.Focus();
    }

    /// <summary>What the modder typed.</summary>
    public string Value => _value.Text.Trim();

    /// <summary>Ask for a value, or null when cancelled.</summary>
    public static string? Ask(IWin32Window owner, string title, string label, string initial)
    {
        using var dialog = new TextPromptDialog(title, label, initial);
        return dialog.ShowDialog(owner) == DialogResult.OK && dialog.Value.Length > 0 ? dialog.Value : null;
    }
}
