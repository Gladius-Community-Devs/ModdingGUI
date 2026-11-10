namespace ModdingGUI
{
    using ModdingGUI.Core.Logging;

    /// <summary>Which palette the interface is drawn with.</summary>
    internal enum UiColorScheme
    {
        /// <summary>Dark surface, light ink. The default.</summary>
        Dark,

        /// <summary>Light surface, dark ink.</summary>
        Light,
    }

    /// <summary>Every colour the interface uses, in one place, for one scheme.</summary>
    internal sealed record UiPalette(
        Color Surface,
        Color SurfaceAlt,
        Color SurfaceSunken,
        Color Border,
        Color Ink,
        Color InkMuted,
        Color Accent,
        Color AccentHover,
        Color AccentInk,
        Color Success,
        Color Warning,
        Color Error,
        Color Info);

    /// <summary>
    /// The one place the interface gets its colours, fonts and spacing from, and the switch that changes them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Before this, every value was decided at the point of use: 161 absolute positions and 173 fixed sizes in the
    /// designer (on a 2127x1295 canvas for a window that opens at 1505x816), eight hand-picked fonts including three
    /// 26 pt TextBoxes standing in for headings, and <c>Color.Red</c>/<c>Color.Blue</c> typed wherever a log line was
    /// written.
    /// </para>
    /// <para>
    /// Colours are properties over <see cref="Current"/> rather than constants, because the scheme can change while the
    /// app runs: <see cref="Use"/> swaps the palette and <see cref="Apply"/> repaints an existing window in place.
    /// Spacing is in device pixels scaled from a 4 px base; fonts are in points, which the framework already scales, so
    /// they are not scaled here.
    /// </para>
    /// </remarks>
    internal static class UiTheme
    {
        private static readonly UiPalette DarkPalette = new(
            Surface: Color.FromArgb(30, 34, 40),
            SurfaceAlt: Color.FromArgb(37, 42, 49),
            SurfaceSunken: Color.FromArgb(22, 25, 30),
            Border: Color.FromArgb(51, 58, 67),
            Ink: Color.FromArgb(230, 233, 238),
            InkMuted: Color.FromArgb(174, 184, 198),
            Accent: Color.FromArgb(52, 104, 181),      // white text clears AA (5.4:1); the previous #3D7DD8 did not (4.1:1)
            AccentHover: Color.FromArgb(61, 125, 216),
            AccentInk: Color.FromArgb(255, 255, 255),
            Success: Color.FromArgb(76, 175, 125),
            Warning: Color.FromArgb(217, 164, 65),
            Error: Color.FromArgb(224, 108, 96),
            Info: Color.FromArgb(111, 168, 232));

        private static readonly UiPalette LightPalette = new(
            Surface: Color.FromArgb(255, 255, 255),
            SurfaceAlt: Color.FromArgb(247, 248, 250),
            SurfaceSunken: Color.FromArgb(251, 252, 253),
            Border: Color.FromArgb(216, 221, 228),
            Ink: Color.FromArgb(31, 35, 40),
            InkMuted: Color.FromArgb(74, 85, 99),
            Accent: Color.FromArgb(47, 111, 191),
            AccentHover: Color.FromArgb(38, 91, 158),
            AccentInk: Color.FromArgb(255, 255, 255),
            Success: Color.FromArgb(31, 122, 61),
            Warning: Color.FromArgb(154, 103, 0),
            Error: Color.FromArgb(180, 35, 24),
            Info: Color.FromArgb(31, 94, 158));

        private static UiColorScheme _scheme = UiColorScheme.Dark;

        /// <summary>Buttons registered as the page's primary action, so a repaint can keep them accent-coloured.</summary>
        private static readonly HashSet<Control> PrimaryButtons = new();

        /// <summary>The scheme in use.</summary>
        public static UiColorScheme Scheme => _scheme;

        /// <summary>The palette in use.</summary>
        public static UiPalette Current => _scheme == UiColorScheme.Dark ? DarkPalette : LightPalette;

        public static Color Surface => Current.Surface;
        public static Color SurfaceAlt => Current.SurfaceAlt;
        public static Color SurfaceSunken => Current.SurfaceSunken;
        public static Color Border => Current.Border;
        public static Color Ink => Current.Ink;
        public static Color InkMuted => Current.InkMuted;
        public static Color Accent => Current.Accent;
        public static Color AccentHover => Current.AccentHover;
        public static Color AccentInk => Current.AccentInk;
        public static Color Success => Current.Success;
        public static Color Warning => Current.Warning;
        public static Color Error => Current.Error;
        public static Color Info => Current.Info;

        // ---- Type: points, deliberately few sizes ----
        public const float BodyPoints = 9.75F;
        public const float CaptionPoints = 8.75F;
        public const float HeadingPoints = 12F;
        public const float TitlePoints = 15.75F;

        private static Font? _body;
        private static Font? _bodyBold;
        private static Font? _caption;
        private static Font? _heading;
        private static Font? _title;

        public static Font Body => _body ??= new Font("Segoe UI", BodyPoints);
        public static Font BodyBold => _bodyBold ??= new Font("Segoe UI", BodyPoints, FontStyle.Bold);
        public static Font Caption => _caption ??= new Font("Segoe UI", CaptionPoints);
        public static Font Heading => _heading ??= new Font("Segoe UI", HeadingPoints, FontStyle.Bold);
        public static Font Title => _title ??= new Font("Segoe UI", TitlePoints, FontStyle.Bold);

        /// <summary>Raised after the palette changes, so pages can re-tint anything they draw themselves.</summary>
        public static event EventHandler? Changed;

        /// <summary>Register a button as a primary action so a repaint keeps it accent-coloured.</summary>
        public static void RegisterPrimary(Control button) => PrimaryButtons.Add(button);

        /// <summary>Forget a button's primary status (used when a control is restyled as secondary).</summary>
        public static void ForgetPrimary(Control button) => PrimaryButtons.Remove(button);

        /// <summary>Register a button as a primary action so a repaint keeps it accent-coloured.</summary>

        /// <summary>Switch scheme without restarting.</summary>
        public static void Use(UiColorScheme scheme)
        {
            if (_scheme == scheme) return;

            _scheme = scheme;
            Changed?.Invoke(null, EventArgs.Empty);
        }

        /// <summary>Parse a stored setting; anything unrecognised is the default (dark).</summary>
        public static UiColorScheme Parse(string? stored) =>
            stored is not null && stored.Equals("light", StringComparison.OrdinalIgnoreCase)
                ? UiColorScheme.Light
                : UiColorScheme.Dark;

        /// <summary>The setting name for the current scheme.</summary>
        public static string ToSetting() => _scheme == UiColorScheme.Dark ? "dark" : "light";

        /// <summary>Scale a 96-dpi pixel value for the display this control is on.</summary>
        public static int Scale(Control control, int pixels)
        {
            int dpi = control.DeviceDpi <= 0 ? 96 : control.DeviceDpi;
            return (int)Math.Round(pixels * dpi / 96.0);
        }

        /// <summary>Spacing on a 4 px base: <c>Space(c, 2)</c> is 8 px at 96 dpi.</summary>
        public static int Space(Control control, int units) => Scale(control, units * 4);

        /// <summary>Colour for a log level, so severity reads the same everywhere.</summary>
        public static Color ForLog(LogLevel level) => level switch
        {
            LogLevel.Error => Error,
            LogLevel.Warning => Warning,
            LogLevel.Success => Success,
            LogLevel.Debug => InkMuted,
            _ => Info,
        };

        /// <summary>
        /// Paint an existing control tree in the current palette.
        /// </summary>
        /// <remarks>
        /// The designer's controls are all still default-coloured, so switching scheme means walking them rather than
        /// rebuilding the window. Structures keep their shape; only colours, borders and flatness change.
        /// </remarks>
        public static void Apply(Control root)
        {
            foreach (Control child in root.Controls) Apply(child);

            switch (root)
            {
                case Form form:
                    form.BackColor = Surface;
                    form.ForeColor = Ink;
                    break;

                case MenuStrip menu:
                    menu.BackColor = SurfaceAlt;
                    menu.ForeColor = Ink;
                    foreach (ToolStripItem item in menu.Items) TintMenuItem(item);
                    break;

                case TabControl tabs:
                    tabs.BackColor = SurfaceAlt;
                    tabs.ForeColor = Ink;
                    break;

                case TabPage page:
                    page.BackColor = Surface;
                    page.ForeColor = Ink;
                    break;

                case GroupBox group:
                    group.BackColor = Surface;
                    group.ForeColor = InkMuted;
                    break;

                case SplitContainer split:
                    split.BackColor = Border;
                    split.Panel1.BackColor = Surface;
                    split.Panel2.BackColor = Surface;
                    break;

                case StatusStrip status:
                    status.BackColor = SurfaceAlt;
                    status.ForeColor = Ink;
                    break;

                // LinkLabel derives from Label, so it has to be matched first.
                case LinkLabel link:
                    link.LinkColor = Accent;
                    link.ActiveLinkColor = AccentHover;
                    break;

                case Label label:
                    // Muted ink for the small print, full ink for anything given a heading-sized font.
                    label.ForeColor = label.Font.SizeInPoints >= HeadingPoints - 1 ? Ink : InkMuted;
                    break;
                case Button button when PrimaryButtons.Contains(button):
                    button.FlatStyle = FlatStyle.Flat;
                    button.BackColor = Accent;
                    button.ForeColor = AccentInk;
                    button.FlatAppearance.BorderSize = 0;
                    button.FlatAppearance.MouseOverBackColor = AccentHover;
                    button.UseVisualStyleBackColor = false;
                    break;

                case Button button:
                    button.FlatStyle = FlatStyle.Flat;
                    button.BackColor = SurfaceAlt;
                    button.ForeColor = Ink;
                    button.FlatAppearance.BorderColor = Border;
                    button.FlatAppearance.BorderSize = 1;
                    button.FlatAppearance.MouseOverBackColor = Border;
                    button.UseVisualStyleBackColor = false;
                    break;

                case RichTextBox log:
                    log.BackColor = SurfaceSunken;
                    log.ForeColor = Ink;
                    log.BorderStyle = BorderStyle.FixedSingle;
                    break;

                case TextBox text when text.ReadOnly:
                    text.BackColor = SurfaceAlt;
                    text.ForeColor = InkMuted;
                    text.BorderStyle = BorderStyle.FixedSingle;
                    break;

                case TextBox text:
                    text.BackColor = SurfaceSunken;
                    text.ForeColor = Ink;
                    text.BorderStyle = BorderStyle.FixedSingle;
                    break;

                case NumericUpDown spin:
                    // The spinner's edit box is a child control, so it needs the colour too or it stays white in a dark
                    // window (the audit reports it as an unnamed TextBox with a system background).
                    spin.BackColor = SurfaceSunken;
                    spin.ForeColor = Ink;
                    spin.BorderStyle = BorderStyle.FixedSingle;
                    break;

                case ComboBox combo:
                    combo.BackColor = SurfaceSunken;
                    combo.ForeColor = Ink;
                    combo.FlatStyle = FlatStyle.Flat;
                    break;

                case ListBox list:
                    list.BackColor = SurfaceSunken;
                    list.ForeColor = Ink;
                    list.BorderStyle = BorderStyle.FixedSingle;
                    break;

                case ListView view:
                    view.BackColor = SurfaceSunken;
                    view.ForeColor = Ink;
                    break;

                case TreeView tree:
                    tree.BackColor = Surface;
                    tree.ForeColor = Ink;
                    tree.LineColor = Border;
                    tree.BorderStyle = BorderStyle.None;
                    tree.HideSelection = false;
                    break;

                case CheckBox check:
                    // Kept a normal checkbox here: the toggle look is opt-in per page (see the Randomizer options), so a
                    // list that suits switches is not forced on every checkbox in the app.
                    check.BackColor = Surface;
                    check.ForeColor = Ink;
                    check.FlatStyle = FlatStyle.Flat;
                    break;

                case RadioButton radio:
                    radio.BackColor = Surface;
                    radio.ForeColor = Ink;
                    radio.FlatStyle = FlatStyle.Flat;
                    break;

                case Panel:
                    root.BackColor = root.BackColor == SurfaceAlt ? SurfaceAlt : Surface;
                    root.ForeColor = Ink;
                    break;
            }
        }

        /// <summary>
        /// Paint controls as they are added from now on, for anything built after the initial <see cref="Apply"/>.
        /// </summary>
        /// <remarks>
        /// Editors create rows lazily - a stat row's text box did not exist when the palette was applied, so it kept the
        /// system colours and the audit reported it as an unnamed white TextBox with black text. Watching additions is
        /// the only way to cover that without every call site remembering to style what it creates.
        /// </remarks>
        public static void Watch(Control root)
        {
            foreach (Control child in root.Controls)
            {
                child.ControlAdded += (_, e) =>
                {
                    if (e.Control is not Control added) return;
                    Apply(added);
                    Watch(added);
                };

                Watch(child);
            }
        }

        private static void OnToggleChanged(object? sender, EventArgs e)
        {
            if (sender is CheckBox check) TintToggle(check);
        }

        /// <summary>Colour a checkbox-as-toggle by its state: accent when on, quiet surface when off.</summary>
        internal static void TintToggle(CheckBox check)
        {
            bool on = check.Checked;
            check.BackColor = on ? Accent : SurfaceAlt;
            check.ForeColor = on ? AccentInk : Ink;
            check.FlatAppearance.BorderColor = on ? Accent : Border;
            check.FlatAppearance.BorderSize = 1;
            check.FlatAppearance.MouseOverBackColor = on ? AccentHover : Border;
        }

        private static void TintMenuItem(ToolStripItem item)
        {
            item.ForeColor = Ink;
            item.BackColor = SurfaceAlt;

            if (item is not ToolStripMenuItem menuItem) return;
            foreach (ToolStripItem child in menuItem.DropDownItems) TintMenuItem(child);
        }
    }
}
