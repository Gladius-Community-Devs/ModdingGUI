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

    /// <summary>Menu renderer that uses <see cref="UiColorTable"/> and drops the rounded edges.</summary>
    internal sealed class UiMenuRenderer : ToolStripProfessionalRenderer
    {
        public UiMenuRenderer() : base(new UiColorTable()) => RoundedEdges = false;
    }

    public partial class frmMain
    {
        /// <summary>Paint the menu, the tab strips and the status bar in the current palette.</summary>
        /// <remarks>
        /// These three are drawn by the framework rather than by us, so they need more than a BackColor: a menu
        /// renderer, owner-drawn tab headers, and a status strip that matches the surface.
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
                tabs.DrawMode = TabDrawMode.OwnerDrawFixed;
                tabs.SizeMode = TabSizeMode.Fixed;
                tabs.ItemSize = new Size(UiTheme.Scale(tabs, 104), UiTheme.Scale(tabs, 26));
                tabs.BackColor = UiTheme.SurfaceAlt;
                tabs.DrawItem -= DrawTabHeader;
                tabs.DrawItem += DrawTabHeader;
                tabs.Invalidate();
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

        /// <summary>Owner-draw one tab header in the palette, with an accent underline on the selected one.</summary>
        private void DrawTabHeader(object? sender, DrawItemEventArgs e)
        {
            if (sender is not TabControl tabs || e.Index < 0 || e.Index >= tabs.TabPages.Count) return;

            bool selected = tabs.SelectedIndex == e.Index;
            Rectangle bounds = e.Bounds;

            using (var fill = new SolidBrush(selected ? UiTheme.Surface : UiTheme.SurfaceAlt))
            {
                e.Graphics.FillRectangle(fill, bounds);
            }

            TextRenderer.DrawText(
                e.Graphics,
                tabs.TabPages[e.Index].Text,
                selected ? UiTheme.BodyBold : UiTheme.Body,
                bounds,
                selected ? UiTheme.Ink : UiTheme.InkMuted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            if (!selected) return;

            int thickness = Math.Max(2, UiTheme.Scale(tabs, 2));
            using var pen = new Pen(UiTheme.Accent, thickness);
            e.Graphics.DrawLine(pen, bounds.Left + 4, bounds.Bottom - thickness, bounds.Right - 4, bounds.Bottom - thickness);
        }
    }
}
