namespace ModdingGUI
{
    using System.Text.Json;

    /// <summary>
    /// A contrast and colour audit of whatever page is on screen, so "this text is hard to read" becomes a list.
    /// </summary>
    /// <remarks>
    /// Two things it catches: real contrast failures (WCAG ratio below the threshold), and controls still painted with a
    /// system colour while the dark palette is active — the latter is what leaves a pale band or a white box in a dark
    /// window, and it cannot be seen by reading one control's colour alone.
    /// </remarks>
    public partial class frmMain
    {
        /// <summary>Audit the controls on the selected page. Driver command: <c>{"cmd":"audit"}</c>.</summary>
        private string AuditPage(JsonElement command)
        {
            double threshold = 3.0;
            if (command.ValueKind == JsonValueKind.Object &&
                command.TryGetProperty("contrast", out JsonElement wanted) &&
                wanted.TryGetDouble(out double value))
            {
                threshold = value;
            }

            var findings = new List<object>();

            foreach (Control control in Descendants(this))
            {
                if (!IsOnScreen(control)) continue;
                if (control is not (Label or Button or CheckBox or RadioButton or GroupBox or TextBox
                    or RichTextBox or TreeView or ComboBox or LinkLabel)) continue;

                string? text = control.Text is { Length: > 0 } own ? own : control is ComboBox combo ? combo.Text : null;
                if (string.IsNullOrWhiteSpace(text)) continue;

                Color back = EffectiveBackground(control);
                double ratio = ContrastRatio(control.ForeColor, back);
                bool systemColour = UiTheme.Scheme == UiColorScheme.Dark && IsSystemColour(back);

                if (ratio >= threshold && !systemColour) continue;

                findings.Add(new
                {
                    name = control.Name,
                    type = control.GetType().Name,
                    text = text.Length > 48 ? text[..48] : text,
                    fore = Hex(control.ForeColor),
                    back = Hex(back),
                    contrast = Math.Round(ratio, 2),
                    issue = systemColour ? "system-colour" : "low-contrast",
                });
            }

            return JsonSerializer.Serialize(new { scheme = UiTheme.ToSetting(), threshold, count = findings.Count, findings });
        }

        /// <summary>Name the deepest control at a point and the colours it is painted with. Command: <c>{"cmd":"hit","x":1,"y":2}</c>.</summary>
        private string HitTest(JsonElement command)
        {
            int x = command.TryGetProperty("x", out JsonElement px) ? px.GetInt32() : 0;
            int y = command.TryGetProperty("y", out JsonElement py) ? py.GetInt32() : 0;

            // GetChildAtPoint stops at the first child, so descend to the deepest one ourselves.
            Control? found = this;
            Point local = new(x, y);
            while (true)
            {
                Control? child = found.GetChildAtPoint(local, GetChildAtPointSkip.Invisible);
                if (child is null || child == found) break;

                local = new Point(local.X - child.Left, local.Y - child.Top);
                found = child;
            }
            var chain = new List<object>();

            for (Control? current = found; current is not null; current = current.Parent)
            {
                chain.Add(new
                {
                    type = current.GetType().Name,
                    name = current.Name,
                    back = Hex(current.BackColor),
                    fore = Hex(current.ForeColor),
                    bounds = $"{current.Left},{current.Top} {current.Width}x{current.Height}",
                });
            }

            return JsonSerializer.Serialize(new { point = $"{x},{y}", deepest = chain.FirstOrDefault(), chain });
        }

        /// <summary>Audit every tab page in turn and return the union. Driver command: <c>{"cmd":"auditAll"}</c>.</summary>
        private string AuditAllPages(JsonElement command)
        {
            var report = new List<object>();
            if (tabContainer is null) return JsonSerializer.Serialize(new { pages = report });

            int original = tabContainer.SelectedIndex;
            try
            {
                foreach (TabPage page in tabContainer.TabPages)
                {
                    tabContainer.SelectedTab = page;
                    Application.DoEvents();

                    report.Add(new { page = page.Text, audit = AuditPage(command) });
                }
            }
            finally
            {
                tabContainer.SelectedIndex = original;
            }

            return JsonSerializer.Serialize(new { pages = report });
        }

        private static IEnumerable<Control> Descendants(Control root)
        {
            foreach (Control child in root.Controls)
            {
                yield return child;

                foreach (Control deeper in Descendants(child)) yield return deeper;
            }
        }

        /// <summary>The colour actually behind a control: walk up while the control is transparent.</summary>
        private static Color EffectiveBackground(Control control)
        {
            Control? current = control;
            while (current is not null)
            {
                if (current.BackColor != Color.Transparent) return current.BackColor;
                current = current.Parent;
            }

            return UiTheme.Surface;
        }

        /// <summary>True for the light system colours the designer defaults to.</summary>
        private static bool IsSystemColour(Color colour) =>
            colour == SystemColors.Control
            || colour == SystemColors.Window
            || colour == SystemColors.ControlLight
            || colour == SystemColors.ControlLightLight
            || colour == SystemColors.Info
            || colour == Color.White;

        private static string Hex(Color colour) => $"#{colour.R:X2}{colour.G:X2}{colour.B:X2}";

        /// <summary>WCAG 2.1 contrast ratio between two opaque colours.</summary>
        private static double ContrastRatio(Color first, Color second)
        {
            double lighter = Math.Max(Luminance(first), Luminance(second));
            double darker = Math.Min(Luminance(first), Luminance(second));
            return (lighter + 0.05) / (darker + 0.05);
        }

        private static double Luminance(Color colour)
        {
            static double Channel(byte value)
            {
                double channel = value / 255.0;
                return channel <= 0.03928 ? channel / 12.92 : Math.Pow((channel + 0.055) / 1.055, 2.4);
            }

            return 0.2126 * Channel(colour.R) + 0.7152 * Channel(colour.G) + 0.0722 * Channel(colour.B);
        }
    }
}
