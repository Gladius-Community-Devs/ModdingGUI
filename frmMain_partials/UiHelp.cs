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
                    page.Dock = DockStyle.Fill;
                    page.AutoScroll = true;
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
            ["Load into the editor"] = "Add the roster's units to the team list so they can be edited.",
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
    }
}
