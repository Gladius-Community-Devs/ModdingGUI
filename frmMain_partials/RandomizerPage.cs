namespace ModdingGUI
{
    /// <summary>
    /// The Randomizer page, rebuilt on the kit: three cards instead of five absolutely-positioned group boxes.
    /// </summary>
    /// <remarks>
    /// The designer page put "Options", "Starting hero" and "Game version" in group boxes at fixed coordinates, with the
    /// status row and the two actions floating underneath and a path box across the top. At the launch size that left
    /// dead space on the right and rows that did not line up. Here the page is: what to randomize (the checkbox list),
    /// the starting setup (hero, school, version), and the run (status, actions, baseline note) - each a card, so the
    /// page reflows and reads top to bottom.
    /// </remarks>
    internal static class RandomizerPage
    {
        public static void Build(Control root)
        {
            if (root.Controls.Find("tabRandomizer", true).FirstOrDefault() is not TabPage page) return;

            Control? Find(string name) => root.Controls.Find(name, true).FirstOrDefault();

            // The designer group boxes held the controls that now live in the cards. Their children moved out, so what is
            // left is three empty frames and two captions - visible evidence that an old layout is still on the page.
            foreach (string retired in new[]
                     {
                         "grpBaseOptions", "grpHeroSelection", "grpGameVersion", "label16", "label15",
                     })
            {
                if (Find(retired) is Control stale) stale.Visible = false;
            }

            int pad = UiTheme.Space(page, 3);
            page.BackColor = UiTheme.Surface;
            page.Padding = new Padding(pad);

            var columns = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                BackColor = UiTheme.Surface,
                Margin = new Padding(0),
            };
            columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44F));
            columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28F));
            columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28F));

            // ---- Options -----------------------------------------------------------------------------------
            var optionsCard = UiKit.Card("cardRandomOptions", "What to randomize",
                "Everything ticked is rebuilt; leave a box clear to keep the original.");

            string[] optionNames =
            {
                "chbRandomHeroes", "chbRandomTeam", "chbRandomNoRecruits", "chbRandomizedEnemies",
                "chbRandomPermaDeath", "chbRandomStatsets", "chbRandomItemsets", "chbRandom40Glads",
                "chbRandomCustomCash", "chbRandomWeighted",
            };

            var optionsBody = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = UiTheme.Surface,
                Padding = new Padding(pad, 0, pad, UiTheme.Space(page, 2)),
            };

            var optionsFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                BackColor = UiTheme.Surface,
            };

            foreach (string name in optionNames)
            {
                if (Find(name) is not CheckBox check) continue;

                // A switch per option rather than the framework box glyph: that glyph is drawn from the system theme and
                // shows as a pale square on a dark surface. Same control, same handlers, same Checked state.
                check.Appearance = Appearance.Button;
                check.FlatStyle = FlatStyle.Flat;
                check.AutoSize = false;
                check.TextAlign = ContentAlignment.MiddleLeft;
                check.Height = UiTheme.Scale(page, 30);
                check.Font = UiTheme.Body;
                check.Padding = new Padding(UiTheme.Space(page, 2), 0, UiTheme.Space(page, 1), 0);
                check.Margin = new Padding(0, 0, 0, UiTheme.Scale(page, 1));
                check.UseVisualStyleBackColor = false;
                UiTheme.TintToggle(check);
                check.CheckedChanged += (sender, _) => { if (sender is CheckBox self) UiTheme.TintToggle(self); };

                optionsFlow.Controls.Add(check);
            }

            // Full-width rows, like a list of switches: on their own the toggles shrink to the text.
            void FitOptions()
            {
                int width = Math.Max(UiTheme.Scale(page, 180), optionsFlow.ClientSize.Width - UiTheme.Scale(page, 2));
                foreach (Control control in optionsFlow.Controls) control.Width = width;
            }

            optionsFlow.SizeChanged += (_, _) => FitOptions();
            FitOptions();

            optionsBody.Controls.Add(optionsFlow);
            UiKit.Body(optionsCard, optionsBody);
            columns.Controls.Add(optionsCard, 0, 0);

            // ---- Starting setup ----------------------------------------------------------------------------
            var setupCard = UiKit.Card("cardRandomSetup", "Starting setup", "Who starts, and which game rules apply.");
            var setupBody = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = UiTheme.Surface,
                Padding = new Padding(pad, 0, pad, UiTheme.Space(page, 2)),
            };

            var setupFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = UiTheme.Surface,
            };

            AddHeading(setupFlow, page, "Starting hero");
            foreach (string name in new[] { "rbnValens", "rbnUrsula" })
            {
                if (Find(name) is RadioButton radio) AddChoice(setupFlow, page, radio);
            }

            AddHeading(setupFlow, page, "Game version");
            foreach (string name in new[] { "rbnVanilla", "rbnRagnaroks", "rbnLeonarths" })
            {
                if (Find(name) is RadioButton radio) AddChoice(setupFlow, page, radio);
            }

            AddHeading(setupFlow, page, "School name");
            if (Find("txtRandomCustomSchoolName") is TextBox school)
            {
                school.Font = UiTheme.Body;
                school.Width = UiKit.FieldWidth(page);
                school.MaximumSize = new Size(UiKit.FieldWidth(page), 0);
                school.Margin = new Padding(0, 0, 0, UiTheme.Space(page, 1));
                setupFlow.Controls.Add(school);
            }

            setupBody.Controls.Add(setupFlow);
            UiKit.Body(setupCard, setupBody);
            columns.Controls.Add(setupCard, 1, 0);

            // ---- Run --------------------------------------------------------------------------------------
            var runCard = UiKit.Card("cardRandomRun", "Run", "Pick the project folder, then randomize.");
            var runBody = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = UiTheme.Surface,
                Padding = new Padding(pad, 0, pad, UiTheme.Space(page, 2)),
            };

            // Laid out as heading/field pairs rather than a caption grid: the grid put the Seed caption a row away
            // from its box once the progress bar joined it.
            var runFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = UiTheme.Surface,
            };

            if (Find("txtRandomizerPath") is TextBox path && Find("btnRandomizerPath") is Button choose)
            {
                choose.Text = "Choose";
                UiKit.Look(choose, page, primary: false);
                AddHeading(runFlow, page, "Project folder");
                runFlow.Controls.Add(RowOf(page, path, choose));
            }

            if (Find("pgbRandomizeStatus") is ProgressBar progress)
            {
                // A ProgressBar is painted by Windows and stays white in a dark window; the status line carries progress
                // until there is a themed bar. Kept visible=false rather than removed so the code that drives it is intact.
                progress.Visible = false;   // no heading either, or the card shows a label with nothing under it
                progress.Width = UiKit.FieldWidth(page);
                progress.Height = UiTheme.Scale(page, 12);
                progress.Margin = new Padding(0, UiTheme.Scale(page, 1), 0, UiTheme.Scale(page, 2));
                if (progress.Visible) AddHeading(runFlow, page, "Progress");
                if (progress.Visible) runFlow.Controls.Add(progress);
            }

            if (Find("lblRandomizeStatus") is Label status)
            {
                // Driven by code ("Ready", "Randomizing..."), so it has to stay visible - it just belongs in the card.
                status.Font = UiTheme.Body;
                status.ForeColor = UiTheme.InkMuted;
                status.BackColor = UiTheme.Surface;
                status.AutoSize = true;
                if (status.Text is "Status:" or "") status.Text = "Ready";   // designer placeholder, not a state
                AddHeading(runFlow, page, "Status");
                runFlow.Controls.Add(status);
            }

            if (Find("txtSeed") is TextBox seed)
            {
                seed.ReadOnly = true;   // themed, rather than a disabled box painted in system colours
                seed.Width = UiKit.FieldWidth(page);
                seed.MaximumSize = new Size(UiKit.FieldWidth(page), 0);
                AddHeading(runFlow, page, "Seed");
                runFlow.Controls.Add(seed);
            }

            runBody.Controls.Add(runFlow);

            // The column is ~300 px wide but the page is 1200, so the first pass sized these to the page and pushed the
            // Choose button past the card. Fit them to the card whenever it lays out.
            void FitRun()
            {
                int width = Math.Max(UiTheme.Scale(page, 150), Math.Min(UiKit.FieldWidth(page), runFlow.ClientSize.Width - UiTheme.Scale(page, 4)));
                if (Find("txtRandomizerPath") is TextBox path && Find("btnRandomizerPath") is Button choose)
                {
                    path.Width = Math.Max(UiTheme.Scale(page, 100), width - choose.Width - UiTheme.Scale(page, 12));
                }

            if (Find("txtSeed") is TextBox seed) seed.Width = width;
            }

            runFlow.SizeChanged += (_, _) => FitRun();
            FitRun();

            var actions = new List<Control>();
            foreach (string name in new[] { "btnRandomize", "btnRandomizerReset" })
            {
                if (Find(name) is not Button button) continue;
                UiKit.Look(button, page, primary: name == "btnRandomize");
                actions.Add(button);
            }

            if (actions.Count > 0) runBody.Controls.Add(UiKit.Actions(page, "pnlRandomActions", actions.ToArray()));

            if (Find("lblRandomizerBaseline") is Label baseline)
            {
                baseline.Font = UiTheme.Caption;
                baseline.ForeColor = UiTheme.InkMuted;
                baseline.BackColor = UiTheme.Surface;
                baseline.TextAlign = ContentAlignment.MiddleLeft;
                baseline.Dock = DockStyle.Bottom;
                baseline.Height = UiTheme.Scale(page, 22);
                runBody.Controls.Add(baseline);
            }

            UiKit.Body(runCard, runBody);
            columns.Controls.Add(runCard, 2, 0);

            page.Controls.Add(columns);
        }

        /// <summary>A field and its button on one line, sized to the shared metric.</summary>
        private static FlowLayoutPanel RowOf(Control page, Control field, Button button)
        {
            var line = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = UiTheme.Surface,
                Margin = new Padding(0, 0, 0, UiTheme.Space(page, 1)),
            };

            int available = page.Width > 0 ? page.Width : UiKit.FieldWidth(page);
            field.Width = Math.Max(UiTheme.Scale(page, 120), Math.Min(UiKit.FieldWidth(page), available) - button.Width - UiTheme.Scale(page, 12));
            field.Height = UiKit.FieldHeight(page);
            field.Margin = new Padding(0);
            button.Height = UiKit.FieldHeight(page);
            button.Margin = new Padding(UiTheme.Scale(page, 2), 0, 0, 0);
            line.Controls.Add(field);
            line.Controls.Add(button);
            return line;
        }

        private static void AddHeading(FlowLayoutPanel flow, Control page, string text)
        {
            flow.Controls.Add(new Label
            {
                Text = text,
                AutoSize = true,
                Font = UiTheme.BodyBold,
                ForeColor = UiTheme.Ink,
                BackColor = UiTheme.Surface,
                Margin = new Padding(0, UiTheme.Space(page, 1), 0, UiTheme.Scale(page, 2)),
            });
        }

        private static void AddChoice(FlowLayoutPanel flow, Control page, RadioButton radio)
        {
            radio.Font = UiTheme.Body;
            radio.ForeColor = UiTheme.Ink;
            radio.BackColor = UiTheme.Surface;
            radio.Margin = new Padding(0, 0, 0, UiTheme.Scale(page, 1));
            flow.Controls.Add(radio);
        }
    }
}
