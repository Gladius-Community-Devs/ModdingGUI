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
        private void BuildTabStrip(TabControl tabs)
        {
            tabs.Appearance = TabAppearance.FlatButtons;
            tabs.ItemSize = new Size(1, 1);
            tabs.Multiline = false;
            tabs.SizeMode = TabSizeMode.Fixed;
            tabs.BackColor = UiTheme.SurfaceAlt;

            if (tabs.Parent is not Control parent) return;

            string hostName = "pnlTabStrip_" + tabs.Name;
            if (parent.Controls.Find(hostName, false).FirstOrDefault() is not FlowLayoutPanel host)
            {
                host = new FlowLayoutPanel
                {
                    Name = hostName,
                    Dock = DockStyle.Top,
                    Height = UiTheme.Scale(tabs, 34),
                    FlowDirection = FlowDirection.LeftToRight,
                    WrapContents = false,
                    BackColor = UiTheme.SurfaceAlt,
                    Padding = new Padding(UiTheme.Space(tabs, 1), UiTheme.Space(tabs, 1), 0, 0),
                };

                // Added after the filling tab control, so it docks first and takes the top band.
                parent.Controls.Add(host);
                tabs.SelectedIndexChanged += (_, _) => RefreshTabStrip(tabs);
            }

            host.SuspendLayout();
            host.Controls.Clear();

            foreach (TabPage page in tabs.TabPages)
            {
                var button = new Button
                {
                    Text = page.Text,
                    Tag = page,
                    AutoSize = false,
                    FlatStyle = FlatStyle.Flat,
                    Font = UiTheme.Body,
                    Height = UiTheme.Scale(tabs, 26),
                    Margin = new Padding(0, 0, UiTheme.Scale(tabs, 2), 0),
                    UseVisualStyleBackColor = false,
                };

                button.FlatAppearance.BorderSize = 0;
                button.Width = TextRenderer.MeasureText(page.Text, UiTheme.Body).Width + UiTheme.Scale(tabs, 26);

                button.Click += (sender, _) =>
                {
                    if (sender is Button clicked && clicked.Tag is TabPage target) tabs.SelectedTab = target;
                };

                host.Controls.Add(button);
            }

            host.ResumeLayout();
            RefreshTabStrip(tabs);
        }

        /// <summary>Tint the strip's buttons so the selected page reads as selected.</summary>
        private void RefreshTabStrip(TabControl tabs)
        {
            if (tabs.Parent?.Controls.Find("pnlTabStrip_" + tabs.Name, false).FirstOrDefault() is not FlowLayoutPanel host) return;

            foreach (Control control in host.Controls)
            {
                if (control is not Button button || button.Tag is not TabPage page) continue;

                bool selected = tabs.SelectedTab == page;
                button.BackColor = selected ? UiTheme.Surface : UiTheme.SurfaceAlt;
                button.ForeColor = selected ? UiTheme.Ink : UiTheme.InkMuted;
                button.Font = selected ? UiTheme.BodyBold : UiTheme.Body;
                button.FlatAppearance.MouseOverBackColor = selected ? UiTheme.Surface : UiTheme.Border;
            }

            host.Invalidate();
        }
    }
}
