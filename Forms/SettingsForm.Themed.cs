namespace ModdingGUI.Forms
{

    /// <summary>
    /// Themed layout for the Settings dialog: same controls, rebuilt as label/field rows on the kit.
    /// </summary>
    /// <remarks>
    /// The designer laid this dialog out with absolute positions on a canvas slightly smaller than the rows people read,
    /// so it looked cramped and stayed in system colours. Each page is now a grid of caption/field rows with the
    /// checkboxes beneath, the two dialogs buttons sit in a docked action strip, and the tab headers are the same themed
    /// strip the main window uses.
    /// </remarks>
    public partial class SettingsForm
    {
        /// <summary>Rebuild the dialog in the current palette. Called at the end of the constructor.</summary>
        internal void BuildThemedLayout()
        {
            SuspendLayout();

            BackColor = UiTheme.Surface;
            ForeColor = UiTheme.Ink;
            Font = UiTheme.Body;
            Padding = new Padding(UiTheme.Space(this, 2));

            tabControl1.Appearance = TabAppearance.FlatButtons;
            tabControl1.ItemSize = new Size(1, 1);
            tabControl1.SizeMode = TabSizeMode.Fixed;
            tabControl1.Multiline = false;
            tabControl1.BackColor = UiTheme.SurfaceAlt;
            tabControl1.Dock = DockStyle.Fill;

            var pages = new Panel { Dock = DockStyle.Fill, BackColor = UiTheme.Surface };
            Controls.Remove(tabControl1);
            pages.Controls.Add(tabControl1);
            Controls.Add(pages);

            UiKit.AttachTabStrip(tabControl1);

            BuildGeneralPage();
            BuildRowPage(tabRandomizer, "Default Randomizer Iterations", numDefaultRandomIterations, new Control[] { chkEnableRandomizerLogs });
            BuildRowPage(tabPatching, "Last Used Patch Server", txtLastUsedPatchServer, new Control[] { chkAutoCheckUpdates });
            BuildCheckboxPage(tabUI, new Control[] { chkShowAdvancedOptions });

            StyleActions();

            // Colours last: the grid lays things out, then the palette paints every field, label and check.
            UiTheme.Apply(this);

            ResumeLayout(true);
        }

        private void BuildGeneralPage()
        {
            // The old absolutely-positioned hint label lands on top of the grid once the page reflows; the caption says
            // the same thing, so it goes.
            foreach (Label label in AllLabels(tabGeneral).ToArray())
            {
                if (label.Text.StartsWith("Leave blank", StringComparison.OrdinalIgnoreCase)) label.Visible = false;
            }

            var rows = new List<(string Caption, Control Field, Control? Button)>
            {
                ("Dolphin executable", txtDolphinPath, btnBrowseDolphinPath),
                ("Projects folder", txtDefaultProjectDir, btnBrowseProjectDir),
            };

            BuildGrid(tabGeneral, rows, new Control[] { chkAutoOpenFolder, chkValidateBeforePacking });
        }

        private void BuildRowPage(TabPage page, string caption, Control field, Control[] checks) =>
            BuildGrid(page, new List<(string, Control, Control?)> { (caption, field, null) }, checks);

        private void BuildCheckboxPage(TabPage page, Control[] checks) =>
            BuildGrid(page, new List<(string, Control, Control?)>(), checks);

        /// <summary>Lay a page out as caption/field rows followed by full-width checks.</summary>
        private void BuildGrid(TabPage page, List<(string Caption, Control Field, Control? Button)> rows, Control[] checks)
        {
            page.SuspendLayout();
            page.BackColor = UiTheme.Surface;
            page.Padding = new Padding(0);

            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                BackColor = UiTheme.Surface,
                Padding = new Padding(UiTheme.Space(this, 4), UiTheme.Space(this, 3), UiTheme.Space(this, 4), 0),
                Margin = new Padding(0),
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, UiTheme.Scale(this, 148)));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            foreach (var (caption, field, button) in rows)
            {
                Hide(caption);

                if (button is not null)
                {
                    UiKit.Look((Button)button, this, primary: false);
                    UiKit.RowWithButton(grid, this, caption, field, (Button)button);
                }
                else
                {
                    UiKit.Row(grid, this, caption, field);
                }
            }

            int row = grid.RowCount;
            foreach (Control check in checks)
            {
                Hide(null);
                grid.RowCount = row + 1;
                grid.RowStyles.Add(new RowStyle(SizeType.Absolute, UiTheme.Scale(this, 30)));
                check.Font = UiTheme.Body;
                check.ForeColor = UiTheme.Ink;
                check.BackColor = UiTheme.Surface;
                check.Margin = new Padding(UiTheme.Scale(this, 2), UiTheme.Scale(this, 1), 0, 0);
                grid.Controls.Add(check, 0, row);
                grid.SetColumnSpan(check, 2);
                row++;
            }

            page.Controls.Add(grid);
            page.ResumeLayout();
        }

        /// <summary>Hide a designer label we replaced with a caption in the grid.</summary>
        private void Hide(string? caption)
        {
            if (caption is null) return;

            // The designer captions are looked up by their text so the grid can own the caption column.
            foreach (Label label in AllLabels(this))
            {
                if (!label.Text.StartsWith(caption, StringComparison.OrdinalIgnoreCase)) continue;
                label.Visible = false;
                return;
            }
        }

        private static IEnumerable<Label> AllLabels(Control root)
        {
            foreach (Control child in root.Controls)
            {
                if (child is Label label) yield return label;

                foreach (Label deeper in AllLabels(child)) yield return deeper;
            }
        }

        /// <summary>Dock the OK/Cancel pair into a themed action strip at the bottom.</summary>
        private void StyleActions()
        {
            foreach (Button button in new[] { btnSave, btnCancel })
            {
                UiKit.Look(button, this, primary: button == btnSave);
                button.Height = UiKit.FieldHeight(this);
            }

            var actions = UiKit.Actions(this, "pnlSettingsActions", btnSave, btnCancel);
            actions.FlowDirection = FlowDirection.RightToLeft;
            actions.Dock = DockStyle.Bottom;
            Controls.Add(actions);
        }
    }
}
