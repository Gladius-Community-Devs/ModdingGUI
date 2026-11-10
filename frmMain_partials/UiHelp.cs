namespace ModdingGUI
{
    /// <summary>
    /// Hover help, and the rules for what belongs on screen versus in a tooltip.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The label says what the control is; the tooltip says the one thing a label cannot fit without becoming a
    /// paragraph. Anything that needs more than a sentence, or that explains *why* rather than *what*, goes here —
    /// the visible text stays short so the window scans quickly.
    /// </para>
    /// <para>
    /// Tips are keyed by control name and applied once, after the designer has built the form and again after the Team
    /// Builder builds its pages (those controls exist only at runtime). Adding a tip for a control that is not there is
    /// a no-op, so the table can stay in one place.
    /// </para>
    /// </remarks>
    internal sealed class UiHelp
    {
        private readonly ToolTip _tips = new()
        {
            AutoPopDelay = 20000,
            InitialDelay = 350,
            ReshowDelay = 150,
            ShowAlways = true,
        };

        /// <summary>Short help per control name.</summary>
        private static readonly Dictionary<string, string> Texts = new(StringComparer.Ordinal)
        {
            // ---- Unpacking ----
            ["txtISOPath"] = "The disc image to unpack: the retail GameCube or PS2 ISO.",
            ["btnSelectISO"] = "Pick the ISO to unpack.",
            ["txtUnpackPath"] = "The folder the unpack creates. Use a new name for each variant of the game.",
            ["btnUnpack"] = "Unpack the ISO into a project folder. Takes a minute or two; the log shows each step.",
            ["btnOpenUnpackLocation"] = "Open the projects folder in Explorer.",

            // ---- Packing ----
            ["txtPackPath"] = "The project that will be packed. Choose it in the project list on the right.",
            ["btnPackPath"] = "Choose a project folder to pack.",
            ["chbRecompileScripts"] = "Recompile changed .scp scripts before packing. Needed after editing a script or "
                                    + "class definition; leave off for data-only changes to save time.",
            ["chbValidationSkip"] = "Skip the pre-pack check. Use only when the check flags something you intend to ship.",
            ["btnPack"] = "Build the ISO from the project. The slow step is re-archiving the data, not the ISO write.",
            ["btnOpenPackLocation"] = "Open the project folder that holds the packed ISO.",
            ["btnToPatching"] = "The Patching tab creates an xDelta from a vanilla and a modded ISO, so a release can "
                                + "ship as a patch instead of a whole ISO.",

            // ---- Patching ----
            ["btnPatchingApplicationISOPath"] = "The untouched retail ISO that the patch applies to.",
            ["btnPatchingApplicationXdeltaPath"] = "The .xdelta patch file to apply.",
            ["btnPatchingApplicationApply"] = "Apply the patch and write a new ISO beside the original.",
            ["btnPatchingUpload"] = "Publish a patch to the community list.",
            ["btnPatchingCreationVanISOPath"] = "The clean ISO, used as the 'before' side of the comparison.",
            ["btnPatchingCreationModISOPath"] = "The modded ISO, used as the 'after' side.",
            ["btnPatchingCreationOutputPath"] = "Where the .xdelta file is written.",
            ["btnPatchingCreation"] = "Compare the two ISOs and write a patch that turns one into the other.",

            // ---- Randomizer ----
            ["btnRandomizerPath"] = "The project to randomize.",
            ["txtRandomizerPath"] = "Randomizing edits this project in place; pack it afterwards.",
            ["grpBaseOptions"] = "Each option changes how the run builds the game. Hover one to see what it does.",
            ["chbRandomHeroes"] = "Randomize the stats and skills of Valens, Ursula and the story heroes.",
            ["chbRandomTeam"] = "Replace the starting team with a full random roster.",
            ["chbRandomNoRecruits"] = "Leave schools empty so the roster comes from your team alone.",
            ["chbRandomizedEnemies"] = "Give every encounter random enemies instead of its scripted ones.",
            ["chbRandomPermaDeath"] = "A downed gladiator stays down.",
            ["chbRandomStatsets"] = "Give random stat growth sets to enemies.",
            ["chbRandomItemsets"] = "Give random equipment sets to enemies.",
            ["chbRandom40Glads"] = "Raise the roster cap to 40. Needs the 40-gladiator gecko code enabled in Dolphin.",
            ["chbRandomCustomCash"] = "Start with an amount of gold you choose instead of the school's.",
            ["chbRandomWeighted"] = "Weight random picks by rarity instead of picking uniformly.",
            ["txtSeed"] = "The same seed and options always produce the same run. Leave blank to get a new seed.",
            ["lblRandomizeStatus"] = "Where the last run's seed and baseline are reported.",
            ["btnRandomize"] = "Randomize the chosen project. The first run saves a vanilla baseline to restore from.",
            ["label15"] = "Every run restores the vanilla baseline first, so runs never stack and a seed always "
                        + "reproduces its result. Use Reset to Vanilla to discard the randomization.",
            ["grpHeroSelection"] = "Which campaign hero the run starts you with.",
            ["grpGameVersion"] = "Which version's data layout the run assumes. Vanilla unless the project came from a mod.",
            ["label16"] = "Optional: rename the starting school so randomized runs are easy to spot.",

            // ---- Team Builder ----
            ["chbTeam40Glads"] = "Allow 34 units and the 40-gladiator mode. The game needs its gecko code enabled.",
            ["chbTeamEquipRestrict"] = "Let any class equip any gear type.",
            ["lblEquipmentRules"] = "Relax the class restrictions on equipment.",
            ["txtTeamHeader"] = "How many units the team holds and the campaign's limit of 14.",
            ["txtTeamUnitName"] = "The name shown in game. Keep it unique per roster.",
            ["ddlTeamClasses"] = "The class decides the unit's stats, skills and allowed gear.",
            ["btnTeamAddUnit"] = "Add the named unit to the team with the chosen class and level.",
            ["btnTeamRemoveUnit"] = "Remove the selected unit from the team.",
            ["txtTeamLevel"] = "Level 1 to 30. An existing unit's level is set on the Gear tab instead.",
            ["chbTeamAllow34"] = "The campaign roster holds 14 units; 34 requires the 40-gladiator gecko code.",
            ["btnTeamAddToFile"] = "Write the team into the ticked roster files. Existing units are updated in place.",
            ["btnLoadRoster"] = "Load a campaign roster into the editor so its units can be changed and saved back.",

            // ---- Editors ----
            ["ddlItemEditorAllItems"] = "Pick the item to edit. Changes are written on Save.",
            ["btnItemEditorNewSave"] = "Write the item to items.tok, keeping a backup of the previous file.",
            ["btnItemEditorMoreStats"] = "Add another stat modifier to this item.",
            ["label17"] = "Item cost in gold.",
            ["label18"] = "The level a gladiator must reach to use the item.",
            ["label19"] = "The 3D model used by the item.",
            ["label20"] = "Which school of magic the item's stats belong to.",
            ["label21"] = "A skill the item grants while equipped.",
            ["label23"] = "The material, which affects how the item looks and sounds.",

            // ---- window chrome ----
            ["tvwProjects"] = "Every project folder. Selecting one sets it as the active project everywhere.",
            ["txtFileHeader"] = "Projects are the unpacked game folders. Packing writes an ISO into the selected one.",
        };

        /// <summary>
        /// Fit every tab page to the window and let it scroll, so content taller than the window stays reachable.
        /// </summary>
        /// <remarks>
        /// The pages carry an absolute 1747x1212 size from the designer, larger than a 1080p window, and a TabPage that
        /// is bigger than its TabControl is clipped rather than scrolled - Patching's "Create xDelta" panel was simply
        /// unreachable. Docking the page to the viewport makes the children overflow the page instead, which is the case
        /// AutoScroll handles.
        /// </remarks>
        public static void EnableTabScrolling(Control root)
        {
            foreach (Control child in root.Controls)
            {
                if (child is TabPage page)
                {
                    // Fit the page to the viewport, but do NOT set AutoScroll on it: that scrolls absolutely positioned
                    // children too, and the Packing tab's "Make a patch" button then drifted down into the middle of the
                    // log pane (the log is anchored to the bottom, the button sits at a fixed Y). Controls that must not
                    // move live in docked footers; see FixPackingFooter.
                    page.Dock = DockStyle.Fill;
                }

                // The content usually sits inside a split container, which docks its panels to itself: a docked panel
                // cannot make its tab page overflow, so the panel is the thing that has to scroll.
                if (child is SplitContainer split)
                {
                    split.Panel1.AutoScroll = true;
                    split.Panel2.AutoScroll = true;
                }

                EnableTabScrolling(child);
            }
        }

        /// <summary>
        /// Tips for controls whose name is generated at runtime (the Team Builder's rebuilt pages): matched on the
        /// visible text, which is stable, instead of a name nobody can predict.
        /// </summary>
        private static readonly Dictionary<string, string> TextTips = new(StringComparer.Ordinal)
        {
            ["Team type"] = "A campaign team is written into the campaign roster. PvP teams are not supported yet.",
            ["Team size and starting level"] = "The campaign roster holds 14 units. 34 needs the 40-gladiator gecko code.",
            ["Job points for new units"] = "Job points are what a class awards by the unit's level; skills are bought "
                                         + "with them on the Skills tab.",
            ["Rosters to save into"] = "The campaign loads only these two school files. Existing units are updated in "
                                     + "place; new units are appended.",
            ["Edit an existing team"] = "Load a roster so its units can be changed and written back.",
            ["Allow 34 units"] = "Raise the cap to 34. Requires the 40-gladiator gecko code enabled in Dolphin.",
            ["Save team to rosters"] = "Write the team into the ticked roster files, with a backup of each file first.",
            ["Load into the editor"] = "Add the roster's units to the team list so they can be edited, then saving rewrites them in place.",
            ["New units start at level"] = "1-30. A unit's level can also be changed later on the Gear tab.",
            ["Campaign team"] = "This build writes the campaign rosters the game loads; PvP teams are not implemented yet.",
            ["Save team to rosters"] = "Writes the team list into the ticked rosters: existing units are updated in place, new ones are appended.",
            ["Rosters to save into"] = "The campaign loads only valensimperia.tok and ursulanordagh.tok; the rest of data/school are test rosters a normal playthrough never loads.",
            ["Job points for new units"] = "Job points are what the class's LEVELUPJPGIVEN awards by that level. The selected unit's own budget and spending are on the Skills tab.",
        };

        /// <summary>Apply every tip whose control exists. Safe to call again after building new controls.</summary>
        public void Install(Control root)
        {
            foreach (var (name, text) in Texts)
            {
                Control[] found = root.Controls.Find(name, searchAllChildren: true);
                foreach (Control control in found)
                {
                    _tips.SetToolTip(control, text);
                }
            }

            foreach (var (visible, text) in TextTips)
            {
                foreach (Control control in Find(root, visible))
                {
                    _tips.SetToolTip(control, text);
                }
            }
        }

        private static IEnumerable<Control> Find(Control root, string text)
        {
            foreach (Control child in root.Controls)
            {
                if (string.Equals(child.Text, text, StringComparison.Ordinal)) yield return child;

                foreach (Control deeper in Find(child, text)) yield return deeper;
            }
        }

        /// <summary>
        /// Make the project sidebar follow the window instead of the designer's 1199 px tree.
        /// </summary>
        /// <remarks>
        /// <c>tvwProjects</c> is anchored on all four sides inside the split panel, but it keeps its design-time height
        /// when the form shrinks, so at 1366x768 the tree runs 400 px past the bottom of the window and the lower half
        /// of the project list is unreachable. Docking the header to the top and the tree to the rest is what the panel
        /// actually means; the header is brought to the front so it docks last and keeps its strip.
        /// </remarks>
        internal static void FixSidebarLayout(Control root)
        {
            if (root.Controls.Find("tvwProjects", true).FirstOrDefault() is not TreeView tree) return;
            if (root.Controls.Find("txtFileHeader", true).FirstOrDefault() is not TextBox header) return;
            if (tree.Parent is not Control panel) return;

            header.Dock = DockStyle.Top;
            tree.Dock = DockStyle.Fill;
            tree.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;

            // Docking runs from the end of the child collection, so the header must come first in z-order.
            header.BringToFront();
        }

        /// <summary>
        /// Reflow the Unit Selection page: a compact "Add a unit" panel, and a preview that fills the rest.
        /// </summary>
        /// <remarks>
        /// The page was laid out by hand at fixed coordinates: the name/class boxes and both buttons hugged the far left,
        /// then 130 px of nothing, then the preview group offset to x=365 with its own inset boxes. Nothing resized with
        /// the window and there were two competing left margins. The controls are reparented, not recreated, so every
        /// handler (and every control name the driver and tests use) still applies.
        /// </remarks>
        /// <summary>A caption for a two-column grid.</summary>
        private static Label Caption(string text) => new()
        {
            Text = text,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 6, 8, 0),
        };

        internal static void FixUnitSelectionLayout(Control root)
        {
            if (root.Controls.Find("tabTeamUnitSelection", true).FirstOrDefault() is not TabPage page) return;
            if (root.Controls.Find("grpPreview", true).FirstOrDefault() is not GroupBox preview) return;

            Control? Find(string name) => root.Controls.Find(name, true).FirstOrDefault();

            if (Find("txtTeamUnitName") is not TextBox nameBox ||
                Find("ddlTeamClasses") is not ComboBox classBox ||
                Find("btnTeamAddUnit") is not Button addButton ||
                Find("btnTeamRemoveUnit") is not Button removeButton)
            {
                return;
            }

            // ---- "Add a unit": label/field pairs in a two-column grid, buttons underneath --------------------
            var addGroup = new GroupBox
            {
                Name = "grpAddUnit",
                Text = "Add a unit",
                Location = new Point(11, 10),
                Size = new Size(420, 132),
            };

            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 3,
                Padding = new Padding(9, 6, 9, 6),
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 62F));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));

            nameBox.Dock = DockStyle.Fill;
            classBox.Dock = DockStyle.Fill;
            grid.Controls.Add(Caption("Name"), 0, 0);
            grid.Controls.Add(nameBox, 1, 0);
            grid.Controls.Add(Caption("Class"), 0, 1);
            grid.Controls.Add(classBox, 1, 1);

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0, 4, 0, 0),
            };
            addButton.Width = 110;
            removeButton.Width = 118;
            removeButton.Margin = new Padding(8, 0, 0, 0);
            buttons.Controls.Add(addButton);
            buttons.Controls.Add(removeButton);
            grid.Controls.Add(buttons, 0, 2);
            grid.SetColumnSpan(buttons, 2);

            addGroup.Controls.Add(grid);

            // Remove the old label placeholders; the grid has its own.
            foreach (string stale in new[] { "label1", "label2" })
            {
                if (Find(stale) is Label label) label.Visible = false;
            }

            page.Controls.Add(addGroup);

            // ---- Preview: same four rows, but stretching with the page ---------------------------------------
            var previewGrid = new TableLayoutPanel
            {
                Name = "tblUnitPreview",
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 4,
                Padding = new Padding(9, 6, 9, 6),
            };
            previewGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 84F));
            previewGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            previewGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
            previewGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 33F));
            previewGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 34F));
            previewGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 33F));

            var rows = new (string Label, string Field, string Caption, int Row)[]
            {
                ("label3", "txtPreviewUnitName", "Name", 0),
                ("label4", "txtPreviewStats", "Stats", 1),
                ("label7", "txtPreviewAttributes", "Traits", 2),
                ("label5", "txtPreviewAllowedGear", "Gear", 3),
            };

            foreach (var (labelName, fieldName, caption, row) in rows)
            {
                if (Find(fieldName) is not TextBox field) continue;

                field.Dock = DockStyle.Fill;
                field.Margin = new Padding(0, 3, 0, 3);
                previewGrid.Controls.Add(Caption(caption), 0, row);
                previewGrid.Controls.Add(field, 1, row);

                if (Find(labelName) is Label stale) stale.Visible = false;
            }

            preview.Controls.Clear();
            preview.Controls.Add(previewGrid);

            // Fill the page to the right of the add panel, and down to the bottom with a margin for the team header.
            preview.Location = new Point(addGroup.Right + 12, 10);
            preview.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            preview.Size = new Size(
                Math.Max(320, page.ClientSize.Width - preview.Left - 12),
                Math.Max(240, page.ClientSize.Height - 30));
        }

        /// <summary>
        /// Give the Randomizer page a footer row instead of controls floating in the empty half of the options box.
        /// </summary>
        /// <remarks>
        /// The options group is sized for about ten checkboxes of content but declared 590 px tall, so Randomize,
        /// Reset to Vanilla and the baseline status sat in the dead space below the ticks with no visible relationship
        /// to anything. The group is shrunk to its content and those three get a row beneath it.
        /// </remarks>
        internal static void FixRandomizerLayout(Control root)
        {
            if (root.Controls.Find("grpBaseOptions", true).FirstOrDefault() is not GroupBox options) return;
            if (options.Parent is not Control page) return;

            int contentBottom = options.Controls.Cast<Control>()
                .Select(child => child.Bottom + child.Margin.Bottom)
                .DefaultIfEmpty(160)
                .Max();

            options.Height = Math.Max(180, contentBottom + 18);

            int row = options.Top + options.Height + 14;
            int left = options.Left + 9;

            // Randomize is a child of the options group, so moving it would only move it inside the box that just
            // shrank. Pull it out onto the page first, then the row below the group is its own.
            if (options.Controls.Find("btnRandomize", true).FirstOrDefault() is Button randomize)
            {
                options.Controls.Remove(randomize);
                page.Controls.Add(randomize);
                randomize.Location = new Point(left, row);
                left = randomize.Right + 12;
            }

            if (root.Controls.Find("btnRandomizerReset", true).FirstOrDefault() is Button reset)
            {
                reset.Location = new Point(left, row + 2);
                left = reset.Right + 16;
            }

            if (root.Controls.Find("lblRandomizerBaseline", true).FirstOrDefault() is Label baseline)
            {
                baseline.Location = new Point(left, row + 9);
                baseline.AutoEllipsis = true;
            }

            // The sentence "each run restores the vanilla baseline first" is now carried by the baseline status label
            // beside it and by the tooltip; leaving it here only collided with the row above.
            if (root.Controls.Find("label15", true).FirstOrDefault() is Label paragraph) paragraph.Visible = false;
        }

        /// <summary>
        /// Give the Packing tab a footer bar for "Make a patch", instead of a button at a hard-coded Y.
        /// </summary>
        /// <remarks>
        /// The log pane is anchored to the bottom of the page, so it grows and shrinks with the window while the button
        /// stayed at Y=1093 - on a 1080p screen the button ended up drawn inside the log, which is what "the make a
        /// patch button is stuck" looked like. A bottom-docked panel and a filling log cannot overlap, whatever the
        /// window size.
        /// </remarks>
        internal static void FixPackingFooter(Control root)
        {
            if (root.Controls.Find("btnToPatching", true).FirstOrDefault() is not Button button) return;
            if (button.Parent is not Control page) return;
            if (root.Controls.Find("rtbPackOutput", true).FirstOrDefault() is not RichTextBox log) return;

            var footer = new Panel
            {
                Name = "pnlPackingFooter",
                Dock = DockStyle.Bottom,
                Height = 44,
                Padding = new Padding(9, 6, 9, 6),
            };

            page.Controls.Remove(button);
            button.Location = new Point(9, 6);
            button.Size = new Size(196, 30);
            button.Anchor = AnchorStyles.Left | AnchorStyles.Top;
            footer.Controls.Add(button);

            // The log fills whatever the footer leaves. Added after the footer so docking gives the footer the bottom
            // strip first, then hands the remainder to the log.
            log.Location = new Point(9, 143);
            log.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            page.Controls.Add(footer);
        }
    }
}
