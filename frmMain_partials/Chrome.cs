namespace ModdingGUI
{
    /// <summary>
    /// Dark-aware colours for menus, so the menu bar stops being the one light strip in a dark window.
    /// </summary>
    internal sealed class UiColorTable : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => UiTheme.SurfaceAlt;
        public override Color MenuStripGradientBegin => UiTheme.SurfaceAlt;
        public override Color MenuStripGradientEnd => UiTheme.SurfaceAlt;
        public override Color MenuItemSelected => UiTheme.Accent;
        public override Color MenuItemSelectedGradientBegin => UiTheme.Accent;
        public override Color MenuItemSelectedGradientEnd => UiTheme.Accent;
        public override Color MenuItemPressedGradientBegin => UiTheme.Surface;
        public override Color MenuItemPressedGradientEnd => UiTheme.Surface;
        public override Color MenuItemBorder => UiTheme.Border;
        public override Color MenuBorder => UiTheme.Border;
        public override Color ImageMarginGradientBegin => UiTheme.SurfaceAlt;
        public override Color ImageMarginGradientMiddle => UiTheme.SurfaceAlt;
        public override Color ImageMarginGradientEnd => UiTheme.SurfaceAlt;
        public override Color SeparatorDark => UiTheme.Border;
        public override Color SeparatorLight => UiTheme.Border;
        public override Color CheckBackground => UiTheme.Accent;
        public override Color CheckSelectedBackground => UiTheme.Accent;
        public override Color CheckPressedBackground => UiTheme.Accent;
    }

    /// <summary>
    /// Menu renderer for the current palette: flat hover instead of the system gradient, readable text in every state,
    /// and our own check mark instead of the small blue system box that looked out of place in a dark window.
    /// </summary>
    internal sealed class UiMenuRenderer : ToolStripProfessionalRenderer
    {
        public UiMenuRenderer() : base(new UiColorTable()) => RoundedEdges = false;

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            using var brush = new SolidBrush(UiTheme.SurfaceAlt);
            e.Graphics.FillRectangle(brush, e.AffectedBounds);
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            bool hot = e.Item.Selected || e.Item.Pressed;
            using var brush = new SolidBrush(hot ? UiTheme.Border : UiTheme.SurfaceAlt);

            // A menu strip keeps its own background; only the drop-down items get a hover plate.
            Rectangle plate = e.Item.Owner is MenuStrip
                ? Rectangle.Empty
                : new Rectangle(2, 0, e.Item.Width - 4, e.Item.Height);

            if (plate != Rectangle.Empty) e.Graphics.FillRectangle(brush, plate);

            if (e.Item.Owner is MenuStrip && hot)
            {
                using var accent = new SolidBrush(UiTheme.Accent);
                e.Graphics.FillRectangle(accent, 2, e.Item.Height - 3, e.Item.Width - 4, 2);
            }
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? UiTheme.Ink : UiTheme.InkMuted;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            Rectangle box = e.ImageRectangle;
            if (box.Width <= 0 || box.Height <= 0) return;

            using var pen = new Pen(UiTheme.Accent, 2f);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            e.Graphics.DrawLines(pen, new[]
            {
                new Point(box.Left + 2, box.Top + box.Height / 2),
                new Point(box.Left + box.Width / 2 - 1, box.Bottom - 3),
                new Point(box.Right - 2, box.Top + 3),
            });
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            using var pen = new Pen(UiTheme.Border);
            int y = e.Item.Height / 2;
            e.Graphics.DrawLine(pen, 6, y, e.Item.Width - 6, y);
        }
    }

    public partial class frmMain
    {
        /// <summary>Paint the menu and the page navigation in the current palette.</summary>
        /// <remarks>
        /// The menu needs a renderer; the tabs need replacing. Windows paints a tab strip itself, so the run right of the
        /// last tab stayed pale in a dark window whatever colour the control was given — including with visual styles
        /// turned off and after painting over it in WM_PAINT. The native row is therefore collapsed to a pixel and a row
        /// of themed buttons sits above the pages, which also removes the wasted space the user pointed at.
        /// </remarks>
        private void ThemeChrome()
        {
            if (mnuMain is not null)
            {
                mnuMain.Renderer ??= new UiMenuRenderer();
                mnuMain.BackColor = UiTheme.SurfaceAlt;
                mnuMain.ForeColor = UiTheme.Ink;
            }

            foreach (TabControl tabs in AllTabControls(this))
            {
                BuildTabStrip(tabs);
            }
        }

        private static IEnumerable<TabControl> AllTabControls(Control root)
        {
            foreach (Control child in root.Controls)
            {
                if (child is TabControl tabs) yield return tabs;

                foreach (TabControl deeper in AllTabControls(child)) yield return deeper;
            }
        }

        /// <summary>Collapse the native tab row and put a themed strip above the pages.</summary>
        /// <summary>Collapse the native tab row and put the themed strip above the pages.</summary>
        private static void BuildTabStrip(TabControl tabs) => UiKit.AttachTabStrip(tabs);
    }
}
