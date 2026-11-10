namespace ModdingGUI
{
    using ModdingGUI.Core.Logging;

    /// <summary>
    /// The one place the interface gets its colours, fonts and spacing from.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Before this, every value was decided at the point of use: 161 absolute positions and 173 fixed sizes in the
    /// designer (on a 2127x1295 canvas for a window that opens at 1505x816), eight hand-picked fonts including three
    /// 26 pt TextBoxes standing in for headings, and <c>Color.Red</c>/<c>Color.Blue</c> typed wherever a log line was
    /// written. That is why a layout change is a fight: there is no shared vocabulary to change.
    /// </para>
    /// <para>
    /// Spacing is in device pixels scaled from a 4 px base, so 125 % and 150 % displays stay proportional. Fonts are in
    /// points, which the framework already scales, so they are <em>not</em> scaled here — doing both would double-scale.
    /// </para>
    /// </remarks>
    internal static class UiTheme
    {
        // ---- Colour: one light palette, low chroma, borders instead of boxes everywhere ----
        public static readonly Color Surface = Color.FromArgb(255, 255, 255);
        public static readonly Color SurfaceAlt = Color.FromArgb(247, 248, 250);
        public static readonly Color SurfaceSunken = Color.FromArgb(251, 252, 253);
        public static readonly Color Border = Color.FromArgb(216, 221, 228);
        public static readonly Color Ink = Color.FromArgb(31, 35, 40);
        public static readonly Color InkMuted = Color.FromArgb(90, 100, 114);
        public static readonly Color Accent = Color.FromArgb(47, 111, 191);
        public static readonly Color AccentDark = Color.FromArgb(38, 91, 158);
        public static readonly Color AccentInk = Color.White;

        public static readonly Color Success = Color.FromArgb(31, 122, 61);
        public static readonly Color Warning = Color.FromArgb(154, 103, 0);
        public static readonly Color Error = Color.FromArgb(180, 35, 24);
        public static readonly Color Info = Color.FromArgb(31, 94, 158);

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
        /// Walk a control tree and give anything that has not been styled explicitly the body font and surface ink.
        /// </summary>
        /// <remarks>
        /// Deliberately conservative: it sets the font only where none was chosen, so the deliberately large and bold
        /// pieces keep their size while everything else stops being whatever the designer default happened to be.
        /// </remarks>
        public static void Apply(Control root)
        {
            foreach (Control child in root.Controls)
            {
                Apply(child);
            }

            switch (root)
            {
                case Label label when label.Font.SizeInPoints is < 8F or > 40F:
                    label.Font = Body;
                    break;

                case LinkLabel link:
                    link.LinkColor = Accent;
                    link.ActiveLinkColor = AccentDark;
                    break;
            }

            if (root.BackColor == SystemColors.Control || root.BackColor == Color.Transparent)
            {
                // Leave the host form's own background alone; only the pages are painted.
            }
        }
    }
}
