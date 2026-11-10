namespace ModdingGUI
{
    /// <summary>
    /// The Unpacking and Packing pages, built on the same kit as Patching: one card per job.
    /// </summary>
    /// <remarks>
    /// Both pages were a handful of controls placed on the designer canvas with a log underneath, which left a large
    /// empty area at the size the window opens. They are now a single card - "what to work on", the options that change
    /// what happens, the action, and the log - so the layout follows the window and the page has a readable shape.
    /// Existing controls are reused, so handler wiring and control names are unchanged.
    /// </remarks>
    internal static class BuildPages
    {
        public static void Build(Control root)
        {
            BuildUnpacking(root);
            BuildPacking(root);
        }

        private static void BuildUnpacking(Control root)
        {
            if (root.Controls.Find("tabUnpacking", true).FirstOrDefault() is not TabPage page) return;

            Control? Find(string name) => root.Controls.Find(name, true).FirstOrDefault();

            int pad = UiTheme.Space(page, 3);
            page.BackColor = UiTheme.Surface;
            page.Padding = new Padding(pad);

            var card = UiKit.Card("cardUnpack", "Unpack a modded ISO",
                "Pulls the disc image apart so the data files can be edited.");
            var body = new Panel { Dock = DockStyle.Fill, BackColor = UiTheme.Surface };
            var form = UiKit.Form(card, 112);

            // "Choose ISO" belongs beside the ISO it fills, not on a row of its own with an empty caption.
            if (Find("txtISOPath") is TextBox isoPath && Find("btnSelectISO") is Button chooseIso)
            {
                isoPath.ReadOnly = true;
                UiKit.Look(chooseIso, page, primary: false);
                UiKit.RowWithButton(form, page, "ISO", isoPath, chooseIso);
            }
            else if (Find("txtISOPath") is TextBox isoOnly)
            {
                isoOnly.ReadOnly = true;
                UiKit.Row(form, page, "ISO", isoOnly);
            }

            if (Find("txtUnpackPath") is TextBox projectName)
            {
                UiKit.Row(form, page, "Project", projectName);
            }

            if (Find("btnUnpack") is Button unpack)
            {
                UiKit.Look(unpack, page, primary: true);
                body.Controls.Add(form);
                body.Controls.Add(UiKit.Actions(page, "pnlUnpackActions", unpack,
                    Find("btnOpenUnpackLocation") as Button ?? new Button()));
            }
            else
            {
                body.Controls.Add(form);
            }

            UiKit.Body(card, body);

            var columns = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = UiTheme.Surface,
                Margin = new Padding(0),
            };
            columns.RowStyles.Add(new RowStyle(SizeType.Absolute, UiKit.CardHeight(page, formRows: 3)));
            columns.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            columns.Controls.Add(card, 0, 0);

            if (Find("rtbUnpackOutput") is RichTextBox log)
            {
                columns.Controls.Add(LogCard(page, "cardUnpackLog", "Activity", log, pad), 0, 1);
            }

            page.Controls.Add(columns);
        }

        private static void BuildPacking(Control root)
        {
            if (root.Controls.Find("tabPacking", true).FirstOrDefault() is not TabPage page) return;

            Control? Find(string name) => root.Controls.Find(name, true).FirstOrDefault();

            int pad = UiTheme.Space(page, 3);
            page.BackColor = UiTheme.Surface;
            page.Padding = new Padding(pad);

            var card = UiKit.Card("cardPack", "Pack the project back into an ISO",
                "Runs the tool chain over the project folder and writes a playable image.");
            var body = new Panel { Dock = DockStyle.Fill, BackColor = UiTheme.Surface };
            var form = UiKit.Form(card, 112);

            if (Find("txtPackPath") is TextBox packPath)
            {
                UiKit.Row(form, page, "Project", packPath);
            }

            if (Find("chbRecompileScripts") is CheckBox recompile)
            {
                UiKit.Row(form, page, "Scripts", Wrap(recompile), "Leave off to pack the scripts as they are.");
            }

            if (Find("chbValidationSkip") is CheckBox skipValidation)
            {
                UiKit.Row(form, page, "Validation", Wrap(skipValidation),
                    "Checks the project before packing; tick to pack without checking.");
            }

            // This one was left behind by the old designer layout: still wired to the auto-launch setting and the pack
            // completion handler, but positioned off the card, so it was invisible and unclickable. Give it a row.
            if (Find("chbAutoLaunchDolphin") is CheckBox autoLaunch)
            {
                autoLaunch.Visible = true;
                UiKit.Row(form, page, "After packing", Wrap(autoLaunch),
                    "Packs, then starts the game in Dolphin.");
            }

            var actions = new List<Control>();
            foreach (string name in new[] { "btnPack", "btnPackPath", "btnOpenPackLocation", "btnToPatching" })
            {
                if (Find(name) is not Button button) continue;
                UiKit.Look(button, page, primary: name == "btnPack");
                if (name == "btnToPatching") button.Enabled = true;   // switching tabs never needed a project
                actions.Add(button);
            }

            body.Controls.Add(form);
            if (actions.Count > 0) body.Controls.Add(UiKit.Actions(page, "pnlPackActions", actions.ToArray()));

            UiKit.Body(card, body);

            var columns = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = UiTheme.Surface,
                Margin = new Padding(0),
            };
            columns.RowStyles.Add(new RowStyle(SizeType.Absolute, UiKit.CardHeight(page, formRows: 3)));
            columns.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            columns.Controls.Add(card, 0, 0);

            if (Find("rtbPackOutput") is RichTextBox log)
            {
                columns.Controls.Add(LogCard(page, "cardPackLog", "Activity", log, pad), 0, 1);
            }

            page.Controls.Add(columns);
        }

        /// <summary>A log pane in a card, so every page shows its output the same way.</summary>
        private static Panel LogCard(Control page, string cardName, string title, RichTextBox log, int pad)
        {
            var card = UiKit.Card(cardName, title);
            UiKit.Style(log);
            log.Dock = DockStyle.Fill;
            log.Margin = new Padding(0);

            var body = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = UiTheme.Surface,
                Padding = new Padding(pad, 0, pad, UiTheme.Space(page, 2)),
            };
            body.Controls.Add(log);
            UiKit.Body(card, body);
            return card;
        }

        /// <summary>Wrap a single control so it can live in the form grid's field column.</summary>
        private static FlowLayoutPanel Wrap(Control control)
        {
            var panel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = false,
                Margin = new Padding(0),
            };

            control.Margin = new Padding(0);
            panel.Controls.Add(control);
            return panel;
        }
    }
}
