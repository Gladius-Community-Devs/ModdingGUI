namespace ModdingGUI
{
    /// <summary>
    /// The building blocks every page is made of, so no page has to invent a coordinate.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A page is a column of <see cref="Card"/>s. A card is a title, an optional caption, a body and an optional action
    /// row. The body is a <see cref="Form"/> grid (caption column + field column) or a filling control such as a log or
    /// a tree. Everything uses Dock/TableLayout, so a page behaves the same at 1280x720 and maximised, and the only
    /// numbers in the code are the theme's spacing steps.
    /// </para>
    /// <para>
    /// Replacing the old 26 pt TextBoxes-used-as-headings matters beyond looks: a TextBox sets its own height from its
    /// font, which is how a header ended up painting over the caption beneath it.
    /// </para>
    /// </remarks>
    internal static class UiKit
    {
        /// <summary>A titled surface with a hairline border and an optional caption.</summary>
        public static Panel Card(string name, string title, string? caption = null)
        {
            var card = new Panel
            {
                Name = name,
                Dock = DockStyle.Fill,
                BackColor = UiTheme.Surface,
                Margin = new Padding(0),
                Padding = new Padding(0),
            };

            var header = new Panel
            {
                Dock = DockStyle.Top,
                BackColor = UiTheme.Surface,
                Padding = new Padding(UiTheme.Space(card, 3), UiTheme.Space(card, 1),
                                      UiTheme.Space(card, 3), 0),
            };

            var heading = new Label
            {
                Text = title,
                Dock = DockStyle.Top,
                AutoSize = false,
                Height = (int)Math.Ceiling(UiTheme.Heading.GetHeight() * 1.35),
                Font = UiTheme.Heading,
                ForeColor = UiTheme.Ink,
                TextAlign = ContentAlignment.MiddleLeft,
            };
            header.Controls.Add(heading);

            if (!string.IsNullOrWhiteSpace(caption))
            {
                var sub = new Label
                {
                    Text = caption,
                    Dock = DockStyle.Top,
                    AutoSize = false,
                    Height = (int)Math.Ceiling(UiTheme.Caption.GetHeight() * 1.5),
                    Font = UiTheme.Caption,
                    ForeColor = UiTheme.InkMuted,
                    TextAlign = ContentAlignment.MiddleLeft,
                };
                header.Controls.Add(sub);
                sub.BringToFront();
            }

            // A docked Panel has no auto height: without this the header keeps its default 100 px and squeezes the body,
            // which is how a two-row form ended up showing one row behind its button.
            header.Height = HeaderHeight(card, hasCaption: !string.IsNullOrWhiteSpace(caption));
            card.Controls.Add(header);
            card.Paint += (_, e) =>
            {
                ControlPaint.DrawBorder(e.Graphics, card.ClientRectangle,
                    UiTheme.Border, ButtonBorderStyle.Solid);
            };

            return card;
        }

        /// <summary>
        /// How tall a card must be to hold a form of <paramref name="formRows"/> rows, its header and its actions.
        /// </summary>
        /// <remarks>
        /// Guessing this number is what put a button on top of a field: the header is two labels whose height comes from
        /// <summary>The height of a card's header, from the fonts it draws with.</summary>
        public static int HeaderHeight(Control owner, bool hasCaption)
        {
            int heading = (int)Math.Ceiling(UiTheme.Heading.GetHeight() * 1.35);
            int caption = hasCaption ? (int)Math.Ceiling(UiTheme.Caption.GetHeight() * 1.5) : 0;
            return heading + caption + UiTheme.Space(owner, 1);
        }

        /// their fonts, so the total is computed here rather than typed into a row style.
        /// </remarks>
        public static int CardHeight(Control owner, int formRows, bool withActions = true)
        {
            int header = HeaderHeight(owner, hasCaption: true);

            int form = UiTheme.Space(owner, 1) + UiTheme.Space(owner, 2) + formRows * UiTheme.Scale(owner, 32);
            int actions = withActions ? UiTheme.Scale(owner, 44) : 0;
            return header + form + actions + UiTheme.Scale(owner, 6);
        }

        /// <summary>The body of a card: a grid of caption/field rows, inset by the theme''s padding.</summary>
        public static TableLayoutPanel Form(Control owner, int labelWidth = 116)
        {
            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                AutoSize = false,
                Padding = new Padding(UiTheme.Space(owner, 3), UiTheme.Space(owner, 1),
                                      UiTheme.Space(owner, 3), UiTheme.Space(owner, 2)),
                BackColor = UiTheme.Surface,
                Margin = new Padding(0),
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, UiTheme.Scale(owner, labelWidth)));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            return grid;
        }

        /// <summary>The height every single-line field and its button share, so they line up pixel for pixel.</summary>
        public static int FieldHeight(Control owner) => UiTheme.Scale(owner, 30);

        /// <summary>
        /// Cap on a field's width. A path box that spans a 1000 px card is unreadable and pushes its Browse button miles
        /// away; fields stay at a sane length and the row keeps the rest of the space empty.
        /// </summary>
        public static int FieldWidth(Control owner) => UiTheme.Scale(owner, 420);

        /// <summary>Add a caption + field row, growing the grid by one 30 px row.</summary>
        public static void Row(TableLayoutPanel grid, Control owner, string caption, Control field, string? hint = null)
        {
            int row = grid.RowCount;
            grid.RowCount = row + 1;
            // The row must be tall enough for a 30 px field plus its 4 px margins, or the field is clipped by its host.
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, UiTheme.Scale(owner, 38)));

            var label = new Label
            {
                Text = caption,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = UiTheme.Ink,
                Font = UiTheme.Body,
                AutoSize = false,
                Margin = new Padding(0),
            };
            grid.Controls.Add(label, 0, row);

            field.Font = UiTheme.Body;
            field.Margin = new Padding(0);

            // Fields keep their own width instead of stretching across the card: a 1000 px path box reads badly and
            // drags its button to the far edge. The host fills the cell and the field sits at its left.
            var host = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                Margin = new Padding(0, UiTheme.Scale(owner, 4), 0, UiTheme.Scale(owner, 4)),
            };

            // Fill with a maximum instead of a fixed width: the field takes the cell when the card is narrow (the
            // Settings dialog is only ~250 px wide) and stops growing at the cap on a wide one, which keeps its button
            // on screen instead of pushing it past the edge.
            field.Dock = DockStyle.Fill;
            field.MaximumSize = new Size(FieldWidth(owner), 0);
            host.Controls.Add(field);
            field.Height = FieldHeight(owner);
            grid.Controls.Add(host, 1, row);

            if (!string.IsNullOrWhiteSpace(hint)) label.Tag = hint;   // surfaced by the tooltip table
        }

        /// <summary>A label + field row where the field is followed by a button, e.g. a path and its Browse.</summary>
        public static FlowLayoutPanel RowWithButton(
            TableLayoutPanel grid, Control owner, string caption, Control field, Button button, string? hint = null)
        {
            var line = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0),
            };

            // The button and the field share one height and one baseline; they used to differ by a few pixels, which is
            // exactly what "Browse does not match the text box" looks like.
            button.Width = UiTheme.Scale(owner, 88);
            button.Height = FieldHeight(owner);
            button.Margin = new Padding(UiTheme.Scale(owner, 2), 0, 0, 0);
            field.Margin = new Padding(0);
            field.Height = FieldHeight(owner);
            line.Controls.Add(field);
            line.Controls.Add(button);

            void Fit()
            {
                int taken = button.Width + button.Margin.Horizontal + UiTheme.Scale(owner, 2);
                field.Width = Math.Max(UiTheme.Scale(owner, 120), line.ClientSize.Width - taken);
            }

            Fit();
            line.SizeChanged += (_, _) => Fit();
            line.ControlAdded += (_, _) => Fit();
            Row(grid, owner, caption, line, hint);
            return line;
        }

        /// <summary>The action button of a card: the thing the page exists for.</summary>
        public static Button Primary(Control owner, string name, string text, EventHandler onClick)
        {
            var button = new Button
            {
                Name = name,
                Text = text,
                AutoSize = false,
                FlatStyle = FlatStyle.Flat,
                BackColor = UiTheme.Accent,
                ForeColor = UiTheme.AccentInk,
                Font = UiTheme.Body,
                Height = UiTheme.Scale(owner, 30),
                Width = UiTheme.Scale(owner, 132),
                Margin = new Padding(0, 0, UiTheme.Space(owner, 2), 0),
                UseVisualStyleBackColor = false,
            };

            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = UiTheme.AccentHover;
            button.FlatAppearance.MouseDownBackColor = UiTheme.AccentHover;
            button.Click += onClick;
            UiTheme.RegisterPrimary(button);
            return button;
        }

        /// <summary>A secondary action: same shape, quieter.</summary>
        public static Button Secondary(Control owner, string name, string text, EventHandler onClick)
        {
            var button = new Button
            {
                Name = name,
                Text = text,
                AutoSize = false,
                FlatStyle = FlatStyle.Flat,
                BackColor = UiTheme.SurfaceAlt,
                ForeColor = UiTheme.Ink,
                Font = UiTheme.Body,
                Height = UiTheme.Scale(owner, 30),
                Width = UiTheme.Scale(owner, 132),
                Margin = new Padding(0, 0, UiTheme.Space(owner, 2), 0),
                UseVisualStyleBackColor = false,
            };

            button.FlatAppearance.BorderColor = UiTheme.Border;
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.MouseOverBackColor = UiTheme.Border;
            button.Click += onClick;
            return button;
        }

        /// <summary>A small "Browse…" style button that sits beside a field.</summary>
        public static Button Browse(Control owner, string name, EventHandler onClick)
        {
            var button = new Button
            {
                Name = name,
                Text = "Browse",
                AutoSize = false,
                FlatStyle = FlatStyle.Flat,
                BackColor = UiTheme.SurfaceAlt,
                ForeColor = UiTheme.Ink,
                Font = UiTheme.Body,
                Height = UiTheme.Scale(owner, 26),
                UseVisualStyleBackColor = false,
            };

            button.FlatAppearance.BorderColor = UiTheme.Border;
            button.FlatAppearance.BorderSize = 1;
            button.Click += onClick;
            return button;
        }

        /// <summary>A row of actions docked to the bottom of a card.</summary>
        public static FlowLayoutPanel Actions(Control owner, string name, params Control[] controls)
        {
            var flow = new FlowLayoutPanel
            {
                Name = name,
                Dock = DockStyle.Bottom,
                Height = UiTheme.Scale(owner, 44),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(UiTheme.Space(owner, 3), UiTheme.Space(owner, 1), 0, 0),
                BackColor = UiTheme.Surface,
            };

            flow.Controls.AddRange(controls);
            return flow;
        }

        /// <summary>Insert a card's body below its header, whatever order the caller built things in.</summary>
        /// <remarks>
        /// WinForms docks from the end of the child collection, so a filling body added after the header would cover it.
        /// Inserting at index 0 keeps the header last, which makes the header dock first and the body take the rest.
        /// </remarks>
        public static void Body(Panel card, Control content)
        {
            card.Controls.Add(content);
            card.Controls.SetChildIndex(content, 0);
        }

        /// <summary>Give an existing button the theme's primary or secondary look, keeping its handler.</summary>
        public static void Look(Button button, Control owner, bool primary)
        {
            button.AutoSize = false;
            button.FlatStyle = FlatStyle.Flat;
            button.Font = UiTheme.Body;
            button.Height = UiTheme.Scale(owner, 30);
            button.Width = Math.Max(button.Width, UiTheme.Scale(owner, 128));
            button.UseVisualStyleBackColor = false;

            // A repaint (theme switch) needs to know which buttons are the page's primary action.
            if (primary) UiTheme.RegisterPrimary(button); else UiTheme.ForgetPrimary(button);

            if (primary)
            {
                button.BackColor = UiTheme.Accent;
                button.ForeColor = UiTheme.AccentInk;
                button.FlatAppearance.BorderSize = 0;
                button.FlatAppearance.MouseOverBackColor = UiTheme.AccentHover;
            }
            else
            {
                button.BackColor = UiTheme.SurfaceAlt;
                button.ForeColor = UiTheme.Ink;
                button.FlatAppearance.BorderColor = UiTheme.Border;
                button.FlatAppearance.BorderSize = 1;
                button.FlatAppearance.MouseOverBackColor = UiTheme.Border;
            }
        }

        /// <summary>
        /// Collapse a TabControl's native row and put a themed strip of buttons above it.
        /// </summary>
        /// <remarks>
        /// Windows paints a tab strip itself, so the run right of the last tab cannot be themed - the app shipped a pale
        /// band across a dark window because of it. This replaces that row with our own buttons and keeps them in step
        /// with the pages, including pages added or removed at run time.
        /// </remarks>
        public static void AttachTabStrip(TabControl tabs)
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

                parent.Controls.Add(host);
                tabs.SelectedIndexChanged += (_, _) => TintTabStrip(tabs);

                // Deferred: the removal notification arrives while the page is still in the collection.
                tabs.ControlAdded += (_, _) => tabs.BeginInvoke((Action)(() => AttachTabStrip(tabs)));
                tabs.ControlRemoved += (_, _) => tabs.BeginInvoke((Action)(() => AttachTabStrip(tabs)));
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
            TintTabStrip(tabs);
        }

        /// <summary>Tint a strip's buttons so the selected page reads as selected.</summary>
        public static void TintTabStrip(TabControl tabs)
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

        /// <summary>Style an existing control (a log, a tree, a text box) to match the theme.</summary>
        public static void Style(Control control)
        {
            control.Font = UiTheme.Body;

            switch (control)
            {
                case RichTextBox log:
                    log.BackColor = UiTheme.SurfaceSunken;
                    log.ForeColor = UiTheme.Ink;
                    log.BorderStyle = BorderStyle.FixedSingle;
                    break;

                case TreeView tree:
                    tree.BackColor = UiTheme.Surface;
                    tree.ForeColor = UiTheme.Ink;
                    tree.BorderStyle = BorderStyle.None;
                    tree.HideSelection = false;
                    break;

                case TextBox text:
                    text.BackColor = UiTheme.Surface;
                    text.ForeColor = UiTheme.Ink;
                    text.BorderStyle = BorderStyle.FixedSingle;
                    break;
            }
        }
    }
}
