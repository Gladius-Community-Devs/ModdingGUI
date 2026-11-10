namespace ModdingGUI
{
    /// <summary>
    /// Theme switching: the app opens dark, and Options → "Dark theme" toggles the light palette.
    /// </summary>
    /// <remarks>
    /// The menu item is created here rather than in the designer so the theme entry lives next to the theme code, and
    /// so adding a third scheme later is a palette plus one radio item rather than a designer edit.
    /// </remarks>
    public partial class frmMain
    {
        private ToolStripMenuItem? darkThemeMenuItem;

        /// <summary>Add the theme entry to the Options menu, reflecting the scheme in use.</summary>
        private void BuildThemeMenu()
        {
            if (darkThemeMenuItem is not null) return;

            darkThemeMenuItem = new ToolStripMenuItem("Dark theme")
            {
                Name = "darkThemeMenuItem",
                // Not CheckOnClick: the checked state mirrors the scheme, so letting the click toggle it as well made the
                // two disagree after a scripted click and the menu item lied about which palette was in use.
                CheckOnClick = false,
                Checked = UiTheme.Scheme == UiColorScheme.Dark,
                ToolTipText = "The app opens dark. Untick for the light palette; the choice is remembered.",
            };

            darkThemeMenuItem.Click += (_, _) =>
                SetTheme(UiTheme.Scheme == UiColorScheme.Dark ? UiColorScheme.Light : UiColorScheme.Dark);

            optionsToolStripMenuItem.DropDownItems.Add(new ToolStripSeparator());
            optionsToolStripMenuItem.DropDownItems.Add(darkThemeMenuItem);
        }

        /// <summary>Switch palette, repaint, remember the choice.</summary>
        private void SetTheme(UiColorScheme scheme)
        {
            UiTheme.Use(scheme);
            UiTheme.Apply(this);
            ThemeChrome();

            // The kit draws its card borders in a Paint handler, so a repaint is what re-tints them.
            Invalidate(true);
            RefreshLogPanes();

            if (darkThemeMenuItem is not null) darkThemeMenuItem.Checked = scheme == UiColorScheme.Dark;

            appSettings.Theme = UiTheme.ToSetting();
            SaveAppSettings();

            AppendLog($"Theme: {UiTheme.ToSetting()}.", UiTheme.InkMuted);
        }

        /// <summary>
        /// Re-render the visible log panes in the current palette.
        /// </summary>
        /// <remarks>
        /// RichTextBox keeps the colour of every span it has already drawn, so after a scheme switch the old lines stay
        /// in the old palette - dark-theme blue on a white page. The entries are in <see cref="Output"/> with their
        /// level, so the panes are rebuilt from the log rather than left half-readable.
        /// </remarks>
        private void RefreshLogPanes()
        {
            var tail = Output.Tail(400);

            foreach (string name in new[] { "rtbUnpackOutput", "rtbPackOutput", "rtbPatchingApplicationOutput" })
            {
                if (Controls.Find(name, true).FirstOrDefault() is not RichTextBox pane) continue;

                pane.SuspendLayout();
                pane.Clear();
                foreach (var entry in tail)
                {
                    pane.SelectionColor = UiTheme.ForLog(entry.Level);
                    pane.AppendText(entry.Message + Environment.NewLine);
                }

                pane.SelectionColor = UiTheme.Ink;
                pane.ResumeLayout();
                pane.SelectionStart = pane.TextLength;
                pane.ScrollToCaret();
            }
        }

        /// <summary>Apply the remembered scheme to a freshly built window.</summary>
        private void ApplySavedTheme()
        {
            UiTheme.Use(UiTheme.Parse(appSettings.Theme));
            BuildThemeMenu();
            UiTheme.Apply(this);
            UiTheme.Watch(this);
            ThemeChrome();
        }
    }
}
