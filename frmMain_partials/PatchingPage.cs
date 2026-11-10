namespace ModdingGUI
{
    /// <summary>
    /// The Patching page, as the reference for how a page is built: cards and grids, no coordinates.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This used to be a nested pair of split containers holding controls placed on a 1747x1212 canvas, so at the size
    /// the window actually opens (1505x816) the "Create xDelta" panel fell below the fold. It is now two columns of
    /// cards: the apply form with its log, and the download list with the creation form. Both adapt to the window
    /// because every size comes from <see cref="UiTheme"/> and every position from docking.
    /// </para>
    /// <para>
    /// The existing controls are reused, not recreated, so their names (which the UI driver and the tests use) and their
    /// click handlers are untouched.
    /// </para>
    /// </remarks>
    internal static class PatchingPage
    {
        public static void Build(Control root)
        {
            if (root.Controls.Find("tabPatching", true).FirstOrDefault() is not TabPage page) return;

            Control? Find(string name) => root.Controls.Find(name, true).FirstOrDefault();

            // The old split and the TextBox headings it used stay in the tree (their children move out), hidden.
            if (Find("splitContainer3") is Control oldSplit) oldSplit.Visible = false;
            foreach (string retired in new[]
                     {
                         "txtPatchApplicationHeader", "txtPatchingDownloadHeader", "txtPatchingCreationHeader",
                         "label8", "label9", "label10", "label11", "label12", "label13", "label14",
                     })
            {
                if (Find(retired) is Control control) control.Visible = false;
            }

            int pad = UiTheme.Space(page, 3);
            page.BackColor = UiTheme.Surface;
            page.Padding = new Padding(pad);

            var columns = new TableLayoutPanel
            {
                Name = "tblPatchColumns",
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = UiTheme.Surface,
                Margin = new Padding(0),
            };
            columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46F));
            columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 54F));

            // ---- Left column: the apply form, then the log -------------------------------------------------
            var left = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = UiTheme.Surface,
                Margin = new Padding(0, 0, pad, 0),
            };
            left.RowStyles.Add(new RowStyle(SizeType.Absolute, UiKit.CardHeight(page, formRows: 2)));
            left.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            var applyCard = UiKit.Card("cardPatchApply", "Patch a clean ISO",
                "Applies an .xdelta patch to an untouched copy of the game.");
            var applyBody = new Panel { Dock = DockStyle.Fill, BackColor = UiTheme.Surface };
            var applyForm = UiKit.Form(applyCard, 112);

            if (Find("txtPatchingApplicationISOPath") is TextBox applyIso &&
                Find("btnPatchingApplicationISOPath") is Button applyIsoBrowse)
            {
                UiKit.RowWithButton(applyForm, page, "Vanilla ISO", applyIso, applyIsoBrowse);
            }

            if (Find("txtPatchingApplicationXdeltaPath") is TextBox applyPatch &&
                Find("btnPatchingApplicationXdeltaPath") is Button applyPatchBrowse)
            {
                UiKit.RowWithButton(applyForm, page, "xDelta patch", applyPatch, applyPatchBrowse);
            }

            if (Find("btnPatchingApplicationApply") is Button applyButton)
            {
                UiKit.Look(applyButton, page, primary: true);
                applyBody.Controls.Add(applyForm);
                applyBody.Controls.Add(UiKit.Actions(page, "pnlPatchApplyActions", applyButton));
            }
            else
            {
                applyBody.Controls.Add(applyForm);
            }

            UiKit.Body(applyCard, applyBody);
            left.Controls.Add(applyCard, 0, 0);

            var logCard = UiKit.Card("cardPatchLog", "Activity");
            if (Find("rtbPatchingApplicationOutput") is RichTextBox log)
            {
                UiKit.Style(log);
                log.Dock = DockStyle.Fill;
                log.Margin = new Padding(0);

                var logBody = new Panel
                {
                    Dock = DockStyle.Fill,
                    BackColor = UiTheme.Surface,
                    Padding = new Padding(pad, 0, pad, UiTheme.Space(page, 2)),
                };
                logBody.Controls.Add(log);
                UiKit.Body(logCard, logBody);
            }

            left.Controls.Add(logCard, 0, 1);

            // ---- Right column: downloads above, creation below ---------------------------------------------
            var right = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = UiTheme.Surface,
                Margin = new Padding(pad, 0, 0, 0),
            };
            right.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            right.RowStyles.Add(new RowStyle(SizeType.Absolute, UiKit.CardHeight(page, formRows: 3)));

            var downloadsCard = UiKit.Card("cardPatchDownloads", "Available patches",
                "Patch packs published by the community.");
            if (Find("tvwxdeltaFiles") is TreeView patches)
            {
                UiKit.Style(patches);
                patches.Dock = DockStyle.Fill;
                patches.Margin = new Padding(0);

                var treeBody = new Panel
                {
                    Dock = DockStyle.Fill,
                    BackColor = UiTheme.Surface,
                    Padding = new Padding(pad, 0, pad, UiTheme.Space(page, 2)),
                };
                treeBody.Controls.Add(patches);
                UiKit.Body(downloadsCard, treeBody);
            }

            right.Controls.Add(downloadsCard, 0, 0);

            var createCard = UiKit.Card("cardPatchCreate", "Make a patch",
                "Compares two ISOs and writes an .xdelta to hand out.");
            var createBody = new Panel { Dock = DockStyle.Fill, BackColor = UiTheme.Surface };
            var createForm = UiKit.Form(createCard, 112);

            if (Find("txtPatchingCreationVanISOPath") is TextBox vanilla &&
                Find("btnPatchingCreationVanISOPath") is Button vanillaBrowse)
            {
                UiKit.RowWithButton(createForm, page, "Vanilla ISO", vanilla, vanillaBrowse);
            }

            if (Find("txtPatchingCreationModISOPath") is TextBox modded &&
                Find("btnPatchingCreationModISOPath") is Button moddedBrowse)
            {
                UiKit.RowWithButton(createForm, page, "Modded ISO", modded, moddedBrowse);
            }

            if (Find("txtPatchingCreationOutputPath") is TextBox output &&
                Find("btnPatchingCreationOutputPath") is Button outputBrowse)
            {
                UiKit.RowWithButton(createForm, page, "xDelta output", output, outputBrowse);
            }

            createBody.Controls.Add(createForm);

            var actions = new List<Control>();
            foreach (string buttonName in new[] { "btnPatchingCreation", "btnPatchingUpload" })
            {
                if (Find(buttonName) is not Button button) continue;
                UiKit.Look(button, page, primary: buttonName == "btnPatchingCreation");
                actions.Add(button);
            }

            if (actions.Count > 0)
            {
                createBody.Controls.Add(UiKit.Actions(page, "pnlPatchCreateActions", actions.ToArray()));
            }

            UiKit.Body(createCard, createBody);
            right.Controls.Add(createCard, 0, 1);

            columns.Controls.Add(left, 0, 0);
            columns.Controls.Add(right, 1, 0);
            page.Controls.Add(columns);
        }
    }
}
