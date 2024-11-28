﻿namespace ModdingGUI
{
    partial class frmMain
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(frmMain));
            mnuMain = new MenuStrip();
            optionsToolStripMenuItem = new ToolStripMenuItem();
            saveBatMenuItem = new ToolStripMenuItem();
            randomizerLogsMenuItem = new ToolStripMenuItem();
            funOptionsToolStripMenuItem = new ToolStripMenuItem();
            randomizerMenuItem = new ToolStripMenuItem();
            teamBuilderToolStripMenuItem = new ToolStripMenuItem();
            ttpInform = new ToolTip(components);
            splitContainer1 = new SplitContainer();
            tabContainer = new TabControl();
            tabPatching = new TabPage();
            tabUnpacking = new TabPage();
            btnOpenUnpackLocation = new Button();
            rtbUnpackOutput = new RichTextBox();
            btnUnpack = new Button();
            txtUnpackPath = new TextBox();
            btnSelectISO = new Button();
            txtISOPath = new TextBox();
            tabPacking = new TabPage();
            pgbValidation = new ProgressBar();
            chbValidationSkip = new CheckBox();
            btnOpenPackLocation = new Button();
            rtbPackOutput = new RichTextBox();
            btnPack = new Button();
            btnPackPath = new Button();
            txtPackPath = new TextBox();
            tabRandomizer = new TabPage();
            grpGameVersion = new GroupBox();
            rbnLeonarths = new RadioButton();
            rbnRagnaroks = new RadioButton();
            rbnVanilla = new RadioButton();
            grpHeroSelection = new GroupBox();
            rbnUrsula = new RadioButton();
            rbnValens = new RadioButton();
            txtSeed = new TextBox();
            lblRandomizeStatus = new Label();
            pgbRandomizeStatus = new ProgressBar();
            grpBaseOptions = new GroupBox();
            chbRandomizedEnemies = new CheckBox();
            chbRandomPermaDeath = new CheckBox();
            chbRandomNoRecruits = new CheckBox();
            btnRandomize = new Button();
            chbRandomTeam = new CheckBox();
            chbRandomHeroes = new CheckBox();
            btnRandomizerPath = new Button();
            txtRandomizerPath = new TextBox();
            tabIngameRandom = new TabPage();
            chbIngameRandom = new CheckBox();
            tabTeamBuilder = new TabPage();
            splitContainer2 = new SplitContainer();
            tabTeamBuilderMaster = new TabControl();
            tabTeamRulesSelection = new TabPage();
            grpRules = new GroupBox();
            chbTeamEquipRestrict = new CheckBox();
            label6 = new Label();
            lblEquipmentRules = new Label();
            grpTeamType = new GroupBox();
            rbnTeamCampaign = new RadioButton();
            rbnTeamPVP = new RadioButton();
            lblTeamLevel = new Label();
            txtTeamLevel = new TextBox();
            lblTeamUnitCreator = new Label();
            tabTeamUnitSelection = new TabPage();
            btnTeamRemoveUnit = new Button();
            grpPreview = new GroupBox();
            tableLayoutPanel1 = new TableLayoutPanel();
            label3 = new Label();
            txtPreviewUnitName = new TextBox();
            label4 = new Label();
            txtPreviewStats = new TextBox();
            label7 = new Label();
            txtPreviewAttributes = new TextBox();
            label2 = new Label();
            label1 = new Label();
            ddlTeamClasses = new ComboBox();
            txtTeamUnitName = new TextBox();
            btnTeamAddUnit = new Button();
            tabTeamGearSelection = new TabPage();
            richTextBox1 = new RichTextBox();
            textBox1 = new TextBox();
            groupBox1 = new GroupBox();
            radioButton5 = new RadioButton();
            radioButton4 = new RadioButton();
            radioButton3 = new RadioButton();
            radioButton2 = new RadioButton();
            radioButton1 = new RadioButton();
            tabTeamSkillSelection = new TabPage();
            tvwTeam = new TreeView();
            txtTeamHeader = new TextBox();
            txtFileHeader = new TextBox();
            tvwProjects = new TreeView();
            dgvGear = new DataGridView();
            mnuMain.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)splitContainer1).BeginInit();
            splitContainer1.Panel1.SuspendLayout();
            splitContainer1.Panel2.SuspendLayout();
            splitContainer1.SuspendLayout();
            tabContainer.SuspendLayout();
            tabUnpacking.SuspendLayout();
            tabPacking.SuspendLayout();
            tabRandomizer.SuspendLayout();
            grpGameVersion.SuspendLayout();
            grpHeroSelection.SuspendLayout();
            grpBaseOptions.SuspendLayout();
            tabIngameRandom.SuspendLayout();
            tabTeamBuilder.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)splitContainer2).BeginInit();
            splitContainer2.Panel1.SuspendLayout();
            splitContainer2.Panel2.SuspendLayout();
            splitContainer2.SuspendLayout();
            tabTeamBuilderMaster.SuspendLayout();
            tabTeamRulesSelection.SuspendLayout();
            grpRules.SuspendLayout();
            grpTeamType.SuspendLayout();
            tabTeamUnitSelection.SuspendLayout();
            grpPreview.SuspendLayout();
            tableLayoutPanel1.SuspendLayout();
            tabTeamGearSelection.SuspendLayout();
            groupBox1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dgvGear).BeginInit();
            SuspendLayout();
            // 
            // mnuMain
            // 
            mnuMain.Items.AddRange(new ToolStripItem[] { optionsToolStripMenuItem, funOptionsToolStripMenuItem });
            mnuMain.Location = new Point(0, 0);
            mnuMain.Name = "mnuMain";
            mnuMain.Size = new Size(1489, 24);
            mnuMain.TabIndex = 1;
            mnuMain.Text = "menuStrip1";
            // 
            // optionsToolStripMenuItem
            // 
            optionsToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { saveBatMenuItem, randomizerLogsMenuItem });
            optionsToolStripMenuItem.Name = "optionsToolStripMenuItem";
            optionsToolStripMenuItem.Size = new Size(99, 20);
            optionsToolStripMenuItem.Text = "Debug Options";
            // 
            // saveBatMenuItem
            // 
            saveBatMenuItem.CheckOnClick = true;
            saveBatMenuItem.Name = "saveBatMenuItem";
            saveBatMenuItem.Size = new Size(165, 22);
            saveBatMenuItem.Text = "Save BAT file";
            // 
            // randomizerLogsMenuItem
            // 
            randomizerLogsMenuItem.CheckOnClick = true;
            randomizerLogsMenuItem.Name = "randomizerLogsMenuItem";
            randomizerLogsMenuItem.Size = new Size(165, 22);
            randomizerLogsMenuItem.Text = "Randomizer Logs";
            // 
            // funOptionsToolStripMenuItem
            // 
            funOptionsToolStripMenuItem.CheckOnClick = true;
            funOptionsToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { randomizerMenuItem, teamBuilderToolStripMenuItem });
            funOptionsToolStripMenuItem.Name = "funOptionsToolStripMenuItem";
            funOptionsToolStripMenuItem.Size = new Size(84, 20);
            funOptionsToolStripMenuItem.Text = "Fun Options";
            // 
            // randomizerMenuItem
            // 
            randomizerMenuItem.CheckOnClick = true;
            randomizerMenuItem.Name = "randomizerMenuItem";
            randomizerMenuItem.Size = new Size(142, 22);
            randomizerMenuItem.Text = "Randomizer";
            randomizerMenuItem.Click += randomizerMenuItem_Click;
            // 
            // teamBuilderToolStripMenuItem
            // 
            teamBuilderToolStripMenuItem.CheckOnClick = true;
            teamBuilderToolStripMenuItem.Name = "teamBuilderToolStripMenuItem";
            teamBuilderToolStripMenuItem.Size = new Size(142, 22);
            teamBuilderToolStripMenuItem.Text = "Team Builder";
            teamBuilderToolStripMenuItem.Click += teamBuilderToolStripMenuItem_Click;
            // 
            // splitContainer1
            // 
            splitContainer1.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            splitContainer1.Location = new Point(0, 27);
            splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            splitContainer1.Panel1.Controls.Add(tabContainer);
            // 
            // splitContainer1.Panel2
            // 
            splitContainer1.Panel2.Controls.Add(txtFileHeader);
            splitContainer1.Panel2.Controls.Add(tvwProjects);
            splitContainer1.Panel2.Margin = new Padding(5);
            splitContainer1.Panel2.Padding = new Padding(5);
            splitContainer1.Size = new Size(1489, 750);
            splitContainer1.SplitterDistance = 1229;
            splitContainer1.TabIndex = 3;
            // 
            // tabContainer
            // 
            tabContainer.Controls.Add(tabPatching);
            tabContainer.Controls.Add(tabUnpacking);
            tabContainer.Controls.Add(tabPacking);
            tabContainer.Controls.Add(tabRandomizer);
            tabContainer.Controls.Add(tabIngameRandom);
            tabContainer.Controls.Add(tabTeamBuilder);
            tabContainer.Dock = DockStyle.Fill;
            tabContainer.Location = new Point(0, 0);
            tabContainer.Name = "tabContainer";
            tabContainer.SelectedIndex = 0;
            tabContainer.Size = new Size(1229, 750);
            tabContainer.TabIndex = 1;
            // 
            // tabPatching
            // 
            tabPatching.Location = new Point(4, 24);
            tabPatching.Name = "tabPatching";
            tabPatching.Size = new Size(1221, 722);
            tabPatching.TabIndex = 5;
            tabPatching.Text = "Patching";
            tabPatching.UseVisualStyleBackColor = true;
            // 
            // tabUnpacking
            // 
            tabUnpacking.Controls.Add(btnOpenUnpackLocation);
            tabUnpacking.Controls.Add(rtbUnpackOutput);
            tabUnpacking.Controls.Add(btnUnpack);
            tabUnpacking.Controls.Add(txtUnpackPath);
            tabUnpacking.Controls.Add(btnSelectISO);
            tabUnpacking.Controls.Add(txtISOPath);
            tabUnpacking.Location = new Point(4, 24);
            tabUnpacking.Name = "tabUnpacking";
            tabUnpacking.Padding = new Padding(3);
            tabUnpacking.Size = new Size(1221, 722);
            tabUnpacking.TabIndex = 0;
            tabUnpacking.Text = "Unpacking";
            tabUnpacking.UseVisualStyleBackColor = true;
            // 
            // btnOpenUnpackLocation
            // 
            btnOpenUnpackLocation.Location = new Point(87, 88);
            btnOpenUnpackLocation.Name = "btnOpenUnpackLocation";
            btnOpenUnpackLocation.Size = new Size(179, 23);
            btnOpenUnpackLocation.TabIndex = 5;
            btnOpenUnpackLocation.Text = "Open containing folder";
            btnOpenUnpackLocation.UseVisualStyleBackColor = true;
            btnOpenUnpackLocation.Click += btnOpenUnpackLocation_Click;
            // 
            // rtbUnpackOutput
            // 
            rtbUnpackOutput.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            rtbUnpackOutput.Location = new Point(9, 120);
            rtbUnpackOutput.Name = "rtbUnpackOutput";
            rtbUnpackOutput.Size = new Size(1200, 531);
            rtbUnpackOutput.TabIndex = 4;
            rtbUnpackOutput.Text = "";
            // 
            // btnUnpack
            // 
            btnUnpack.Location = new Point(6, 88);
            btnUnpack.Name = "btnUnpack";
            btnUnpack.Size = new Size(75, 23);
            btnUnpack.TabIndex = 3;
            btnUnpack.Text = "Unpack!";
            btnUnpack.UseVisualStyleBackColor = true;
            btnUnpack.Click += btnUnpack_Click;
            // 
            // txtUnpackPath
            // 
            txtUnpackPath.Location = new Point(6, 59);
            txtUnpackPath.Name = "txtUnpackPath";
            txtUnpackPath.Size = new Size(181, 23);
            txtUnpackPath.TabIndex = 2;
            txtUnpackPath.Text = "Enter project name here";
            // 
            // btnSelectISO
            // 
            btnSelectISO.Location = new Point(6, 6);
            btnSelectISO.Name = "btnSelectISO";
            btnSelectISO.Size = new Size(87, 23);
            btnSelectISO.TabIndex = 1;
            btnSelectISO.Text = "Select ISO";
            btnSelectISO.UseVisualStyleBackColor = true;
            btnSelectISO.Click += btnSelectISO_Click;
            // 
            // txtISOPath
            // 
            txtISOPath.Enabled = false;
            txtISOPath.Location = new Point(99, 7);
            txtISOPath.Name = "txtISOPath";
            txtISOPath.Size = new Size(685, 23);
            txtISOPath.TabIndex = 0;
            txtISOPath.Text = "ISO Path will appear here!";
            // 
            // tabPacking
            // 
            tabPacking.Controls.Add(pgbValidation);
            tabPacking.Controls.Add(chbValidationSkip);
            tabPacking.Controls.Add(btnOpenPackLocation);
            tabPacking.Controls.Add(rtbPackOutput);
            tabPacking.Controls.Add(btnPack);
            tabPacking.Controls.Add(btnPackPath);
            tabPacking.Controls.Add(txtPackPath);
            tabPacking.Location = new Point(4, 24);
            tabPacking.Name = "tabPacking";
            tabPacking.Padding = new Padding(3);
            tabPacking.Size = new Size(1221, 722);
            tabPacking.TabIndex = 1;
            tabPacking.Text = "Packing";
            tabPacking.UseVisualStyleBackColor = true;
            // 
            // pgbValidation
            // 
            pgbValidation.Location = new Point(264, 35);
            pgbValidation.Name = "pgbValidation";
            pgbValidation.Size = new Size(186, 19);
            pgbValidation.TabIndex = 7;
            pgbValidation.Visible = false;
            // 
            // chbValidationSkip
            // 
            chbValidationSkip.AutoSize = true;
            chbValidationSkip.Checked = true;
            chbValidationSkip.CheckState = CheckState.Checked;
            chbValidationSkip.Location = new Point(267, 61);
            chbValidationSkip.Name = "chbValidationSkip";
            chbValidationSkip.Size = new Size(186, 19);
            chbValidationSkip.TabIndex = 6;
            chbValidationSkip.Text = "Skip Validation (force packing)";
            chbValidationSkip.UseVisualStyleBackColor = true;
            chbValidationSkip.CheckedChanged += chbValidationSkip_CheckedChanged;
            // 
            // btnOpenPackLocation
            // 
            btnOpenPackLocation.Location = new Point(99, 54);
            btnOpenPackLocation.Name = "btnOpenPackLocation";
            btnOpenPackLocation.Size = new Size(159, 23);
            btnOpenPackLocation.TabIndex = 5;
            btnOpenPackLocation.Text = "Open containing folder";
            btnOpenPackLocation.UseVisualStyleBackColor = true;
            btnOpenPackLocation.Click += btnOpenPackLocation_Click;
            // 
            // rtbPackOutput
            // 
            rtbPackOutput.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            rtbPackOutput.Location = new Point(9, 86);
            rtbPackOutput.Name = "rtbPackOutput";
            rtbPackOutput.Size = new Size(1206, 564);
            rtbPackOutput.TabIndex = 4;
            rtbPackOutput.Text = "";
            // 
            // btnPack
            // 
            btnPack.Enabled = false;
            btnPack.Location = new Point(6, 54);
            btnPack.Name = "btnPack";
            btnPack.Size = new Size(87, 23);
            btnPack.TabIndex = 3;
            btnPack.Text = "Pack";
            btnPack.UseVisualStyleBackColor = true;
            btnPack.Click += btnPack_Click;
            // 
            // btnPackPath
            // 
            btnPackPath.Location = new Point(6, 6);
            btnPackPath.Name = "btnPackPath";
            btnPackPath.Size = new Size(87, 23);
            btnPackPath.TabIndex = 2;
            btnPackPath.Text = "Select project";
            btnPackPath.UseVisualStyleBackColor = true;
            btnPackPath.Click += btnPackPath_Click;
            // 
            // txtPackPath
            // 
            txtPackPath.ForeColor = Color.Black;
            txtPackPath.Location = new Point(99, 6);
            txtPackPath.Name = "txtPackPath";
            txtPackPath.ReadOnly = true;
            txtPackPath.Size = new Size(685, 23);
            txtPackPath.TabIndex = 0;
            txtPackPath.Text = "Project filepath will show up here! (SELECT ONE TO CONTINUE)";
            // 
            // tabRandomizer
            // 
            tabRandomizer.Controls.Add(grpGameVersion);
            tabRandomizer.Controls.Add(grpHeroSelection);
            tabRandomizer.Controls.Add(txtSeed);
            tabRandomizer.Controls.Add(lblRandomizeStatus);
            tabRandomizer.Controls.Add(pgbRandomizeStatus);
            tabRandomizer.Controls.Add(grpBaseOptions);
            tabRandomizer.Controls.Add(btnRandomizerPath);
            tabRandomizer.Controls.Add(txtRandomizerPath);
            tabRandomizer.Location = new Point(4, 24);
            tabRandomizer.Name = "tabRandomizer";
            tabRandomizer.Padding = new Padding(3);
            tabRandomizer.Size = new Size(1221, 722);
            tabRandomizer.TabIndex = 2;
            tabRandomizer.Text = "Randomizer";
            tabRandomizer.UseVisualStyleBackColor = true;
            // 
            // grpGameVersion
            // 
            grpGameVersion.Controls.Add(rbnLeonarths);
            grpGameVersion.Controls.Add(rbnRagnaroks);
            grpGameVersion.Controls.Add(rbnVanilla);
            grpGameVersion.Location = new Point(190, 108);
            grpGameVersion.Name = "grpGameVersion";
            grpGameVersion.Size = new Size(141, 136);
            grpGameVersion.TabIndex = 11;
            grpGameVersion.TabStop = false;
            grpGameVersion.Text = "Choose Game Version";
            // 
            // rbnLeonarths
            // 
            rbnLeonarths.AutoSize = true;
            rbnLeonarths.Location = new Point(6, 72);
            rbnLeonarths.Name = "rbnLeonarths";
            rbnLeonarths.Size = new Size(108, 19);
            rbnLeonarths.TabIndex = 2;
            rbnLeonarths.Text = "Leonarth's Mod";
            rbnLeonarths.UseVisualStyleBackColor = true;
            // 
            // rbnRagnaroks
            // 
            rbnRagnaroks.AutoSize = true;
            rbnRagnaroks.Location = new Point(6, 47);
            rbnRagnaroks.Name = "rbnRagnaroks";
            rbnRagnaroks.Size = new Size(111, 19);
            rbnRagnaroks.TabIndex = 1;
            rbnRagnaroks.Text = "Ragnarok's Mod";
            rbnRagnaroks.UseVisualStyleBackColor = true;
            // 
            // rbnVanilla
            // 
            rbnVanilla.AutoSize = true;
            rbnVanilla.Checked = true;
            rbnVanilla.Location = new Point(6, 22);
            rbnVanilla.Name = "rbnVanilla";
            rbnVanilla.Size = new Size(59, 19);
            rbnVanilla.TabIndex = 0;
            rbnVanilla.TabStop = true;
            rbnVanilla.Text = "Vanilla";
            rbnVanilla.UseVisualStyleBackColor = true;
            // 
            // grpHeroSelection
            // 
            grpHeroSelection.Controls.Add(rbnUrsula);
            grpHeroSelection.Controls.Add(rbnValens);
            grpHeroSelection.Location = new Point(190, 36);
            grpHeroSelection.Name = "grpHeroSelection";
            grpHeroSelection.Size = new Size(141, 66);
            grpHeroSelection.TabIndex = 10;
            grpHeroSelection.TabStop = false;
            grpHeroSelection.Text = "Choose Starting Hero";
            // 
            // rbnUrsula
            // 
            rbnUrsula.AutoSize = true;
            rbnUrsula.Location = new Point(6, 43);
            rbnUrsula.Name = "rbnUrsula";
            rbnUrsula.Size = new Size(58, 19);
            rbnUrsula.TabIndex = 1;
            rbnUrsula.Text = "Ursula";
            rbnUrsula.UseVisualStyleBackColor = true;
            // 
            // rbnValens
            // 
            rbnValens.AutoSize = true;
            rbnValens.Checked = true;
            rbnValens.Location = new Point(6, 18);
            rbnValens.Name = "rbnValens";
            rbnValens.Size = new Size(58, 19);
            rbnValens.TabIndex = 0;
            rbnValens.TabStop = true;
            rbnValens.Text = "Valens";
            rbnValens.UseVisualStyleBackColor = true;
            // 
            // txtSeed
            // 
            txtSeed.Location = new Point(367, 361);
            txtSeed.Name = "txtSeed";
            txtSeed.Size = new Size(219, 23);
            txtSeed.TabIndex = 9;
            txtSeed.Text = "Enter a seed here!";
            txtSeed.MouseHover += txtSeed_MouseHover;
            // 
            // lblRandomizeStatus
            // 
            lblRandomizeStatus.AutoSize = true;
            lblRandomizeStatus.Location = new Point(193, 343);
            lblRandomizeStatus.Name = "lblRandomizeStatus";
            lblRandomizeStatus.Size = new Size(42, 15);
            lblRandomizeStatus.TabIndex = 8;
            lblRandomizeStatus.Text = "Status:";
            // 
            // pgbRandomizeStatus
            // 
            pgbRandomizeStatus.Location = new Point(190, 361);
            pgbRandomizeStatus.Name = "pgbRandomizeStatus";
            pgbRandomizeStatus.Size = new Size(171, 23);
            pgbRandomizeStatus.TabIndex = 7;
            // 
            // grpBaseOptions
            // 
            grpBaseOptions.Controls.Add(chbRandomizedEnemies);
            grpBaseOptions.Controls.Add(chbRandomPermaDeath);
            grpBaseOptions.Controls.Add(chbRandomNoRecruits);
            grpBaseOptions.Controls.Add(btnRandomize);
            grpBaseOptions.Controls.Add(chbRandomTeam);
            grpBaseOptions.Controls.Add(chbRandomHeroes);
            grpBaseOptions.Location = new Point(8, 36);
            grpBaseOptions.Name = "grpBaseOptions";
            grpBaseOptions.Size = new Size(176, 354);
            grpBaseOptions.TabIndex = 5;
            grpBaseOptions.TabStop = false;
            grpBaseOptions.Text = "Randomized Options";
            // 
            // chbRandomizedEnemies
            // 
            chbRandomizedEnemies.AutoSize = true;
            chbRandomizedEnemies.Location = new Point(6, 97);
            chbRandomizedEnemies.Name = "chbRandomizedEnemies";
            chbRandomizedEnemies.Size = new Size(143, 19);
            chbRandomizedEnemies.TabIndex = 7;
            chbRandomizedEnemies.Text = "Fully random enemies";
            chbRandomizedEnemies.UseVisualStyleBackColor = true;
            chbRandomizedEnemies.MouseHover += chbRandomizedEnemies_MouseHover;
            // 
            // chbRandomPermaDeath
            // 
            chbRandomPermaDeath.AutoSize = true;
            chbRandomPermaDeath.Location = new Point(6, 122);
            chbRandomPermaDeath.Name = "chbRandomPermaDeath";
            chbRandomPermaDeath.Size = new Size(130, 19);
            chbRandomPermaDeath.TabIndex = 6;
            chbRandomPermaDeath.Text = "All fights are deadly";
            chbRandomPermaDeath.UseVisualStyleBackColor = true;
            chbRandomPermaDeath.MouseHover += chbRandomPermaDeath_MouseHover;
            // 
            // chbRandomNoRecruits
            // 
            chbRandomNoRecruits.AutoSize = true;
            chbRandomNoRecruits.Location = new Point(6, 72);
            chbRandomNoRecruits.Name = "chbRandomNoRecruits";
            chbRandomNoRecruits.Size = new Size(84, 19);
            chbRandomNoRecruits.TabIndex = 6;
            chbRandomNoRecruits.Text = "No recruits";
            chbRandomNoRecruits.UseVisualStyleBackColor = true;
            chbRandomNoRecruits.MouseHover += chbRandomNoRecruits_MouseHover;
            // 
            // btnRandomize
            // 
            btnRandomize.Enabled = false;
            btnRandomize.Location = new Point(39, 325);
            btnRandomize.Name = "btnRandomize";
            btnRandomize.Size = new Size(87, 23);
            btnRandomize.TabIndex = 6;
            btnRandomize.Text = "Randomize!";
            btnRandomize.UseVisualStyleBackColor = true;
            btnRandomize.Click += btnRandomize_Click;
            // 
            // chbRandomTeam
            // 
            chbRandomTeam.AutoSize = true;
            chbRandomTeam.Checked = true;
            chbRandomTeam.CheckState = CheckState.Checked;
            chbRandomTeam.Location = new Point(6, 47);
            chbRandomTeam.Name = "chbRandomTeam";
            chbRandomTeam.Size = new Size(150, 19);
            chbRandomTeam.TabIndex = 6;
            chbRandomTeam.Text = "Give Full Random Team";
            chbRandomTeam.UseVisualStyleBackColor = true;
            chbRandomTeam.MouseHover += chbRandomTeam_MouseHover;
            // 
            // chbRandomHeroes
            // 
            chbRandomHeroes.AutoSize = true;
            chbRandomHeroes.Checked = true;
            chbRandomHeroes.CheckState = CheckState.Checked;
            chbRandomHeroes.Location = new Point(6, 22);
            chbRandomHeroes.Name = "chbRandomHeroes";
            chbRandomHeroes.Size = new Size(111, 19);
            chbRandomHeroes.TabIndex = 0;
            chbRandomHeroes.Text = "Random Heroes";
            chbRandomHeroes.UseVisualStyleBackColor = true;
            chbRandomHeroes.MouseHover += chbRandomHeroes_MouseHover;
            // 
            // btnRandomizerPath
            // 
            btnRandomizerPath.Location = new Point(6, 6);
            btnRandomizerPath.Name = "btnRandomizerPath";
            btnRandomizerPath.Size = new Size(87, 23);
            btnRandomizerPath.TabIndex = 4;
            btnRandomizerPath.Text = "Select project";
            btnRandomizerPath.UseVisualStyleBackColor = true;
            btnRandomizerPath.Click += btnRandomizerPath_Click;
            // 
            // txtRandomizerPath
            // 
            txtRandomizerPath.Enabled = false;
            txtRandomizerPath.Location = new Point(99, 7);
            txtRandomizerPath.Name = "txtRandomizerPath";
            txtRandomizerPath.Size = new Size(685, 23);
            txtRandomizerPath.TabIndex = 3;
            txtRandomizerPath.Text = "Project filepath will show up here!";
            // 
            // tabIngameRandom
            // 
            tabIngameRandom.Controls.Add(chbIngameRandom);
            tabIngameRandom.Location = new Point(4, 24);
            tabIngameRandom.Name = "tabIngameRandom";
            tabIngameRandom.Padding = new Padding(3);
            tabIngameRandom.Size = new Size(1221, 722);
            tabIngameRandom.TabIndex = 3;
            tabIngameRandom.Text = "Ingame Random";
            tabIngameRandom.UseVisualStyleBackColor = true;
            // 
            // chbIngameRandom
            // 
            chbIngameRandom.AutoSize = true;
            chbIngameRandom.Location = new Point(11, 9);
            chbIngameRandom.Name = "chbIngameRandom";
            chbIngameRandom.Size = new Size(162, 19);
            chbIngameRandom.TabIndex = 8;
            chbIngameRandom.Text = "Randomize on town leave";
            chbIngameRandom.UseVisualStyleBackColor = true;
            chbIngameRandom.Visible = false;
            chbIngameRandom.CheckedChanged += chbIngameRandom_CheckedChanged;
            chbIngameRandom.MouseHover += chbIngameRandom_MouseHover;
            // 
            // tabTeamBuilder
            // 
            tabTeamBuilder.Controls.Add(splitContainer2);
            tabTeamBuilder.Location = new Point(4, 24);
            tabTeamBuilder.Name = "tabTeamBuilder";
            tabTeamBuilder.Padding = new Padding(3);
            tabTeamBuilder.Size = new Size(1221, 722);
            tabTeamBuilder.TabIndex = 4;
            tabTeamBuilder.Text = "Team Builder";
            tabTeamBuilder.UseVisualStyleBackColor = true;
            // 
            // splitContainer2
            // 
            splitContainer2.Dock = DockStyle.Fill;
            splitContainer2.Location = new Point(3, 3);
            splitContainer2.Name = "splitContainer2";
            // 
            // splitContainer2.Panel1
            // 
            splitContainer2.Panel1.Controls.Add(tabTeamBuilderMaster);
            // 
            // splitContainer2.Panel2
            // 
            splitContainer2.Panel2.Controls.Add(tvwTeam);
            splitContainer2.Panel2.Controls.Add(txtTeamHeader);
            splitContainer2.Size = new Size(1215, 716);
            splitContainer2.SplitterDistance = 955;
            splitContainer2.TabIndex = 0;
            // 
            // tabTeamBuilderMaster
            // 
            tabTeamBuilderMaster.Controls.Add(tabTeamRulesSelection);
            tabTeamBuilderMaster.Controls.Add(tabTeamUnitSelection);
            tabTeamBuilderMaster.Controls.Add(tabTeamGearSelection);
            tabTeamBuilderMaster.Controls.Add(tabTeamSkillSelection);
            tabTeamBuilderMaster.Dock = DockStyle.Fill;
            tabTeamBuilderMaster.Location = new Point(0, 0);
            tabTeamBuilderMaster.Name = "tabTeamBuilderMaster";
            tabTeamBuilderMaster.SelectedIndex = 0;
            tabTeamBuilderMaster.Size = new Size(955, 716);
            tabTeamBuilderMaster.TabIndex = 1;
            // 
            // tabTeamRulesSelection
            // 
            tabTeamRulesSelection.Controls.Add(grpRules);
            tabTeamRulesSelection.Controls.Add(lblTeamUnitCreator);
            tabTeamRulesSelection.Location = new Point(4, 24);
            tabTeamRulesSelection.Name = "tabTeamRulesSelection";
            tabTeamRulesSelection.Padding = new Padding(3);
            tabTeamRulesSelection.Size = new Size(947, 688);
            tabTeamRulesSelection.TabIndex = 3;
            tabTeamRulesSelection.Text = "Rules Selection";
            tabTeamRulesSelection.UseVisualStyleBackColor = true;
            // 
            // grpRules
            // 
            grpRules.Controls.Add(chbTeamEquipRestrict);
            grpRules.Controls.Add(label6);
            grpRules.Controls.Add(lblEquipmentRules);
            grpRules.Controls.Add(grpTeamType);
            grpRules.Controls.Add(lblTeamLevel);
            grpRules.Controls.Add(txtTeamLevel);
            grpRules.Location = new Point(6, 41);
            grpRules.Name = "grpRules";
            grpRules.Size = new Size(666, 314);
            grpRules.TabIndex = 7;
            grpRules.TabStop = false;
            grpRules.Text = "First, let's start with some rules:";
            // 
            // chbTeamEquipRestrict
            // 
            chbTeamEquipRestrict.AutoSize = true;
            chbTeamEquipRestrict.Location = new Point(190, 114);
            chbTeamEquipRestrict.Name = "chbTeamEquipRestrict";
            chbTeamEquipRestrict.Size = new Size(133, 19);
            chbTeamEquipRestrict.TabIndex = 2;
            chbTeamEquipRestrict.Text = "Remove Restrictions";
            chbTeamEquipRestrict.UseVisualStyleBackColor = true;
            chbTeamEquipRestrict.CheckedChanged += chbTeamEquipRestrict_CheckedChanged;
            // 
            // label6
            // 
            label6.AutoSize = true;
            label6.Font = new Font("Segoe UI", 8.25F, FontStyle.Regular, GraphicsUnit.Point, 0);
            label6.ForeColor = Color.Red;
            label6.Location = new Point(6, 130);
            label6.Name = "label6";
            label6.Size = new Size(409, 13);
            label6.TabIndex = 10;
            label6.Text = "(This will brand your ISO. You cannot use this team in PvP unless agreed upon)";
            // 
            // lblEquipmentRules
            // 
            lblEquipmentRules.AutoSize = true;
            lblEquipmentRules.Location = new Point(6, 115);
            lblEquipmentRules.Name = "lblEquipmentRules";
            lblEquipmentRules.Size = new Size(178, 15);
            lblEquipmentRules.TabIndex = 9;
            lblEquipmentRules.Text = "Remove equipment restrictions: ";
            // 
            // grpTeamType
            // 
            grpTeamType.Controls.Add(rbnTeamCampaign);
            grpTeamType.Controls.Add(rbnTeamPVP);
            grpTeamType.Location = new Point(6, 22);
            grpTeamType.Name = "grpTeamType";
            grpTeamType.Size = new Size(347, 44);
            grpTeamType.TabIndex = 8;
            grpTeamType.TabStop = false;
            grpTeamType.Text = "Is this for PvP or a campaign?";
            // 
            // rbnTeamCampaign
            // 
            rbnTeamCampaign.AutoSize = true;
            rbnTeamCampaign.Location = new Point(57, 19);
            rbnTeamCampaign.Name = "rbnTeamCampaign";
            rbnTeamCampaign.Size = new Size(80, 19);
            rbnTeamCampaign.TabIndex = 1;
            rbnTeamCampaign.TabStop = true;
            rbnTeamCampaign.Text = "Campaign";
            rbnTeamCampaign.UseVisualStyleBackColor = true;
            rbnTeamCampaign.CheckedChanged += rbnTeamCampaign_CheckedChanged;
            // 
            // rbnTeamPVP
            // 
            rbnTeamPVP.AutoSize = true;
            rbnTeamPVP.Checked = true;
            rbnTeamPVP.Location = new Point(6, 19);
            rbnTeamPVP.Name = "rbnTeamPVP";
            rbnTeamPVP.Size = new Size(45, 19);
            rbnTeamPVP.TabIndex = 0;
            rbnTeamPVP.TabStop = true;
            rbnTeamPVP.Text = "PvP";
            rbnTeamPVP.UseVisualStyleBackColor = true;
            // 
            // lblTeamLevel
            // 
            lblTeamLevel.AutoSize = true;
            lblTeamLevel.Location = new Point(6, 85);
            lblTeamLevel.Name = "lblTeamLevel";
            lblTeamLevel.Size = new Size(295, 15);
            lblTeamLevel.TabIndex = 7;
            lblTeamLevel.Text = "Input a team level. (All units added will have this level):";
            // 
            // txtTeamLevel
            // 
            txtTeamLevel.Location = new Point(307, 82);
            txtTeamLevel.Name = "txtTeamLevel";
            txtTeamLevel.Size = new Size(100, 23);
            txtTeamLevel.TabIndex = 6;
            txtTeamLevel.TextChanged += txtTeamLevel_TextChanged;
            txtTeamLevel.Leave += txtTeamLevel_Leave;
            // 
            // lblTeamUnitCreator
            // 
            lblTeamUnitCreator.AutoSize = true;
            lblTeamUnitCreator.BackColor = Color.LightGray;
            lblTeamUnitCreator.BorderStyle = BorderStyle.Fixed3D;
            lblTeamUnitCreator.Font = new Font("Segoe UI", 14.25F, FontStyle.Regular, GraphicsUnit.Point, 0);
            lblTeamUnitCreator.Location = new Point(166, 7);
            lblTeamUnitCreator.Margin = new Padding(4);
            lblTeamUnitCreator.Name = "lblTeamUnitCreator";
            lblTeamUnitCreator.Size = new Size(254, 27);
            lblTeamUnitCreator.TabIndex = 4;
            lblTeamUnitCreator.Text = "Welcome to the unit creator!";
            // 
            // tabTeamUnitSelection
            // 
            tabTeamUnitSelection.Controls.Add(btnTeamRemoveUnit);
            tabTeamUnitSelection.Controls.Add(grpPreview);
            tabTeamUnitSelection.Controls.Add(label2);
            tabTeamUnitSelection.Controls.Add(label1);
            tabTeamUnitSelection.Controls.Add(ddlTeamClasses);
            tabTeamUnitSelection.Controls.Add(txtTeamUnitName);
            tabTeamUnitSelection.Controls.Add(btnTeamAddUnit);
            tabTeamUnitSelection.Location = new Point(4, 24);
            tabTeamUnitSelection.Name = "tabTeamUnitSelection";
            tabTeamUnitSelection.Padding = new Padding(3);
            tabTeamUnitSelection.Size = new Size(947, 688);
            tabTeamUnitSelection.TabIndex = 0;
            tabTeamUnitSelection.Text = "Unit Selection";
            tabTeamUnitSelection.UseVisualStyleBackColor = true;
            // 
            // btnTeamRemoveUnit
            // 
            btnTeamRemoveUnit.Enabled = false;
            btnTeamRemoveUnit.Location = new Point(9, 120);
            btnTeamRemoveUnit.Name = "btnTeamRemoveUnit";
            btnTeamRemoveUnit.Size = new Size(94, 23);
            btnTeamRemoveUnit.TabIndex = 7;
            btnTeamRemoveUnit.Text = "Remove Unit";
            btnTeamRemoveUnit.UseVisualStyleBackColor = true;
            btnTeamRemoveUnit.Click += btnTeamRemoveUnit_Click;
            // 
            // grpPreview
            // 
            grpPreview.Controls.Add(tableLayoutPanel1);
            grpPreview.Location = new Point(354, 37);
            grpPreview.Name = "grpPreview";
            grpPreview.Size = new Size(318, 379);
            grpPreview.TabIndex = 6;
            grpPreview.TabStop = false;
            grpPreview.Text = "Unit Preview";
            // 
            // tableLayoutPanel1
            // 
            tableLayoutPanel1.ColumnCount = 2;
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 31.75966F));
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 68.24034F));
            tableLayoutPanel1.Controls.Add(label3, 0, 0);
            tableLayoutPanel1.Controls.Add(txtPreviewUnitName, 1, 0);
            tableLayoutPanel1.Controls.Add(label4, 0, 1);
            tableLayoutPanel1.Controls.Add(txtPreviewStats, 1, 1);
            tableLayoutPanel1.Controls.Add(label7, 0, 2);
            tableLayoutPanel1.Controls.Add(txtPreviewAttributes, 1, 2);
            tableLayoutPanel1.Dock = DockStyle.Fill;
            tableLayoutPanel1.Location = new Point(3, 19);
            tableLayoutPanel1.Name = "tableLayoutPanel1";
            tableLayoutPanel1.RowCount = 3;
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 11.9378872F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 51.00235F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 37.0597572F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Absolute, 20F));
            tableLayoutPanel1.Size = new Size(312, 357);
            tableLayoutPanel1.TabIndex = 0;
            // 
            // label3
            // 
            label3.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            label3.AutoSize = true;
            label3.Location = new Point(3, 0);
            label3.Name = "label3";
            label3.Size = new Size(93, 42);
            label3.TabIndex = 1;
            label3.Text = "Unit Name:";
            label3.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // txtPreviewUnitName
            // 
            txtPreviewUnitName.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            txtPreviewUnitName.Location = new Point(102, 3);
            txtPreviewUnitName.Multiline = true;
            txtPreviewUnitName.Name = "txtPreviewUnitName";
            txtPreviewUnitName.ReadOnly = true;
            txtPreviewUnitName.Size = new Size(207, 36);
            txtPreviewUnitName.TabIndex = 0;
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Dock = DockStyle.Fill;
            label4.Location = new Point(3, 42);
            label4.Name = "label4";
            label4.Size = new Size(93, 182);
            label4.TabIndex = 2;
            label4.Text = "Base Stats:";
            label4.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // txtPreviewStats
            // 
            txtPreviewStats.AcceptsReturn = true;
            txtPreviewStats.Dock = DockStyle.Fill;
            txtPreviewStats.Location = new Point(102, 45);
            txtPreviewStats.Multiline = true;
            txtPreviewStats.Name = "txtPreviewStats";
            txtPreviewStats.ReadOnly = true;
            txtPreviewStats.ScrollBars = ScrollBars.Vertical;
            txtPreviewStats.Size = new Size(207, 176);
            txtPreviewStats.TabIndex = 3;
            // 
            // label7
            // 
            label7.AutoSize = true;
            label7.Dock = DockStyle.Fill;
            label7.Location = new Point(3, 224);
            label7.Name = "label7";
            label7.Size = new Size(93, 133);
            label7.TabIndex = 6;
            label7.Text = "Attributes:";
            label7.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // txtPreviewAttributes
            // 
            txtPreviewAttributes.Dock = DockStyle.Fill;
            txtPreviewAttributes.Location = new Point(102, 227);
            txtPreviewAttributes.Multiline = true;
            txtPreviewAttributes.Name = "txtPreviewAttributes";
            txtPreviewAttributes.ReadOnly = true;
            txtPreviewAttributes.ScrollBars = ScrollBars.Vertical;
            txtPreviewAttributes.Size = new Size(207, 127);
            txtPreviewAttributes.TabIndex = 7;
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Font = new Font("Segoe UI", 9F, FontStyle.Underline);
            label2.Location = new Point(6, 69);
            label2.Name = "label2";
            label2.Size = new Size(78, 15);
            label2.TabIndex = 5;
            label2.Text = "Select a class:";
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Font = new Font("Segoe UI", 9F, FontStyle.Underline);
            label1.Location = new Point(6, 40);
            label1.Name = "label1";
            label1.Size = new Size(97, 15);
            label1.TabIndex = 4;
            label1.Text = "Enter unit name: ";
            // 
            // ddlTeamClasses
            // 
            ddlTeamClasses.FormattingEnabled = true;
            ddlTeamClasses.Location = new Point(109, 66);
            ddlTeamClasses.Name = "ddlTeamClasses";
            ddlTeamClasses.Size = new Size(239, 23);
            ddlTeamClasses.TabIndex = 2;
            ddlTeamClasses.SelectedIndexChanged += ddlTeamClasses_SelectedIndexChanged;
            // 
            // txtTeamUnitName
            // 
            txtTeamUnitName.Location = new Point(109, 37);
            txtTeamUnitName.Name = "txtTeamUnitName";
            txtTeamUnitName.Size = new Size(239, 23);
            txtTeamUnitName.TabIndex = 1;
            txtTeamUnitName.TextChanged += txtTeamUnitName_TextChanged;
            // 
            // btnTeamAddUnit
            // 
            btnTeamAddUnit.Location = new Point(9, 91);
            btnTeamAddUnit.Name = "btnTeamAddUnit";
            btnTeamAddUnit.Size = new Size(94, 23);
            btnTeamAddUnit.TabIndex = 0;
            btnTeamAddUnit.Text = "Add Unit";
            btnTeamAddUnit.UseVisualStyleBackColor = true;
            btnTeamAddUnit.Click += btnTeamAddUnit_Click;
            // 
            // tabTeamGearSelection
            // 
            tabTeamGearSelection.Controls.Add(dgvGear);
            tabTeamGearSelection.Controls.Add(richTextBox1);
            tabTeamGearSelection.Controls.Add(textBox1);
            tabTeamGearSelection.Controls.Add(groupBox1);
            tabTeamGearSelection.Location = new Point(4, 24);
            tabTeamGearSelection.Name = "tabTeamGearSelection";
            tabTeamGearSelection.Padding = new Padding(3);
            tabTeamGearSelection.Size = new Size(947, 688);
            tabTeamGearSelection.TabIndex = 1;
            tabTeamGearSelection.Text = "Gear Selection";
            tabTeamGearSelection.UseVisualStyleBackColor = true;
            // 
            // richTextBox1
            // 
            richTextBox1.Location = new Point(6, 94);
            richTextBox1.Name = "richTextBox1";
            richTextBox1.Size = new Size(270, 588);
            richTextBox1.TabIndex = 3;
            richTextBox1.Text = resources.GetString("richTextBox1.Text");
            // 
            // textBox1
            // 
            textBox1.Location = new Point(6, 65);
            textBox1.Name = "textBox1";
            textBox1.Size = new Size(270, 23);
            textBox1.TabIndex = 2;
            textBox1.Text = "UnitNameHere (Stat Preview)";
            // 
            // groupBox1
            // 
            groupBox1.Controls.Add(radioButton5);
            groupBox1.Controls.Add(radioButton4);
            groupBox1.Controls.Add(radioButton3);
            groupBox1.Controls.Add(radioButton2);
            groupBox1.Controls.Add(radioButton1);
            groupBox1.Location = new Point(3, 6);
            groupBox1.Name = "groupBox1";
            groupBox1.Size = new Size(359, 53);
            groupBox1.TabIndex = 0;
            groupBox1.TabStop = false;
            groupBox1.Text = "Gear Type";
            // 
            // radioButton5
            // 
            radioButton5.AutoSize = true;
            radioButton5.Location = new Point(279, 22);
            radioButton5.Name = "radioButton5";
            radioButton5.Size = new Size(78, 19);
            radioButton5.TabIndex = 4;
            radioButton5.TabStop = true;
            radioButton5.Text = "Accessory";
            radioButton5.UseVisualStyleBackColor = true;
            // 
            // radioButton4
            // 
            radioButton4.AutoSize = true;
            radioButton4.Location = new Point(216, 22);
            radioButton4.Name = "radioButton4";
            radioButton4.Size = new Size(57, 19);
            radioButton4.TabIndex = 3;
            radioButton4.TabStop = true;
            radioButton4.Text = "Shield";
            radioButton4.UseVisualStyleBackColor = true;
            // 
            // radioButton3
            // 
            radioButton3.AutoSize = true;
            radioButton3.Location = new Point(141, 22);
            radioButton3.Name = "radioButton3";
            radioButton3.Size = new Size(69, 19);
            radioButton3.TabIndex = 2;
            radioButton3.TabStop = true;
            radioButton3.Text = "Weapon";
            radioButton3.UseVisualStyleBackColor = true;
            // 
            // radioButton2
            // 
            radioButton2.AutoSize = true;
            radioButton2.Location = new Point(76, 22);
            radioButton2.Name = "radioButton2";
            radioButton2.Size = new Size(59, 19);
            radioButton2.TabIndex = 1;
            radioButton2.TabStop = true;
            radioButton2.Text = "Armor";
            radioButton2.UseVisualStyleBackColor = true;
            // 
            // radioButton1
            // 
            radioButton1.AutoSize = true;
            radioButton1.Location = new Point(6, 22);
            radioButton1.Name = "radioButton1";
            radioButton1.Size = new Size(64, 19);
            radioButton1.TabIndex = 0;
            radioButton1.TabStop = true;
            radioButton1.Text = "Helmet";
            radioButton1.UseVisualStyleBackColor = true;
            // 
            // tabTeamSkillSelection
            // 
            tabTeamSkillSelection.Location = new Point(4, 24);
            tabTeamSkillSelection.Name = "tabTeamSkillSelection";
            tabTeamSkillSelection.Padding = new Padding(3);
            tabTeamSkillSelection.Size = new Size(947, 688);
            tabTeamSkillSelection.TabIndex = 2;
            tabTeamSkillSelection.Text = "Skills Selection";
            tabTeamSkillSelection.UseVisualStyleBackColor = true;
            // 
            // tvwTeam
            // 
            tvwTeam.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            tvwTeam.Location = new Point(3, 27);
            tvwTeam.Name = "tvwTeam";
            tvwTeam.Size = new Size(250, 689);
            tvwTeam.TabIndex = 1;
            tvwTeam.AfterSelect += tvwTeam_AfterSelect;
            // 
            // txtTeamHeader
            // 
            txtTeamHeader.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            txtTeamHeader.Location = new Point(3, 3);
            txtTeamHeader.Name = "txtTeamHeader";
            txtTeamHeader.ReadOnly = true;
            txtTeamHeader.Size = new Size(250, 23);
            txtTeamHeader.TabIndex = 0;
            txtTeamHeader.Text = "Current Team:";
            // 
            // txtFileHeader
            // 
            txtFileHeader.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            txtFileHeader.Location = new Point(5, 0);
            txtFileHeader.Name = "txtFileHeader";
            txtFileHeader.ReadOnly = true;
            txtFileHeader.Size = new Size(246, 23);
            txtFileHeader.TabIndex = 4;
            txtFileHeader.Text = "Project File Tree:";
            // 
            // tvwProjects
            // 
            tvwProjects.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            tvwProjects.Location = new Point(5, 24);
            tvwProjects.Name = "tvwProjects";
            tvwProjects.Size = new Size(246, 721);
            tvwProjects.TabIndex = 3;
            tvwProjects.BeforeExpand += tvwProjects_BeforeExpand;
            tvwProjects.NodeMouseHover += tvwProjects_NodeMouseHover;
            tvwProjects.AfterSelect += tvwProjects_AfterSelect;
            tvwProjects.NodeMouseDoubleClick += tvwProjects_NodeMouseDoubleClick;
            // 
            // dgvGear
            // 
            dgvGear.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgvGear.Location = new Point(282, 65);
            dgvGear.Name = "dgvGear";
            dgvGear.Size = new Size(662, 617);
            dgvGear.TabIndex = 4;
            // 
            // frmMain
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1489, 777);
            Controls.Add(mnuMain);
            Controls.Add(splitContainer1);
            Icon = (Icon)resources.GetObject("$this.Icon");
            MainMenuStrip = mnuMain;
            Name = "frmMain";
            Text = "Modding Tools GUI";
            Load += frmMain_Load;
            mnuMain.ResumeLayout(false);
            mnuMain.PerformLayout();
            splitContainer1.Panel1.ResumeLayout(false);
            splitContainer1.Panel2.ResumeLayout(false);
            splitContainer1.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)splitContainer1).EndInit();
            splitContainer1.ResumeLayout(false);
            tabContainer.ResumeLayout(false);
            tabUnpacking.ResumeLayout(false);
            tabUnpacking.PerformLayout();
            tabPacking.ResumeLayout(false);
            tabPacking.PerformLayout();
            tabRandomizer.ResumeLayout(false);
            tabRandomizer.PerformLayout();
            grpGameVersion.ResumeLayout(false);
            grpGameVersion.PerformLayout();
            grpHeroSelection.ResumeLayout(false);
            grpHeroSelection.PerformLayout();
            grpBaseOptions.ResumeLayout(false);
            grpBaseOptions.PerformLayout();
            tabIngameRandom.ResumeLayout(false);
            tabIngameRandom.PerformLayout();
            tabTeamBuilder.ResumeLayout(false);
            splitContainer2.Panel1.ResumeLayout(false);
            splitContainer2.Panel2.ResumeLayout(false);
            splitContainer2.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)splitContainer2).EndInit();
            splitContainer2.ResumeLayout(false);
            tabTeamBuilderMaster.ResumeLayout(false);
            tabTeamRulesSelection.ResumeLayout(false);
            tabTeamRulesSelection.PerformLayout();
            grpRules.ResumeLayout(false);
            grpRules.PerformLayout();
            grpTeamType.ResumeLayout(false);
            grpTeamType.PerformLayout();
            tabTeamUnitSelection.ResumeLayout(false);
            tabTeamUnitSelection.PerformLayout();
            grpPreview.ResumeLayout(false);
            tableLayoutPanel1.ResumeLayout(false);
            tableLayoutPanel1.PerformLayout();
            tabTeamGearSelection.ResumeLayout(false);
            tabTeamGearSelection.PerformLayout();
            groupBox1.ResumeLayout(false);
            groupBox1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)dgvGear).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion
        private MenuStrip mnuMain;
        private ToolStripMenuItem optionsToolStripMenuItem;
        private ToolStripMenuItem saveBatMenuItem;
        private ToolStripMenuItem funOptionsToolStripMenuItem;
        private ToolStripMenuItem randomizerMenuItem;
        private ToolStripMenuItem randomizerLogsMenuItem;
        private ToolTip ttpInform;
        private SplitContainer splitContainer1;
        private TabControl tabContainer;
        private TabPage tabUnpacking;
        private Button btnOpenUnpackLocation;
        private RichTextBox rtbUnpackOutput;
        private Button btnUnpack;
        private TextBox txtUnpackPath;
        private Button btnSelectISO;
        private TextBox txtISOPath;
        private TabPage tabPacking;
        private ProgressBar pgbValidation;
        private CheckBox chbValidationSkip;
        private Button btnOpenPackLocation;
        private RichTextBox rtbPackOutput;
        private Button btnPack;
        private Button btnPackPath;
        private TextBox txtPackPath;
        private TabPage tabRandomizer;
        private GroupBox grpGameVersion;
        private RadioButton rbnLeonarths;
        private RadioButton rbnRagnaroks;
        private RadioButton rbnVanilla;
        private GroupBox grpHeroSelection;
        private RadioButton rbnUrsula;
        private RadioButton rbnValens;
        private TextBox txtSeed;
        private Label lblRandomizeStatus;
        private ProgressBar pgbRandomizeStatus;
        private GroupBox grpBaseOptions;
        private CheckBox chbRandomPermaDeath;
        private CheckBox chbRandomNoRecruits;
        private Button btnRandomize;
        private CheckBox chbRandomTeam;
        private CheckBox chbRandomHeroes;
        private Button btnRandomizerPath;
        private TextBox txtRandomizerPath;
        private TabPage tabIngameRandom;
        private CheckBox chbIngameRandom;
        private TreeView tvwProjects;
        private TextBox txtFileHeader;
        private ToolStripMenuItem teamBuilderToolStripMenuItem;
        private TabPage tabTeamBuilder;
        private SplitContainer splitContainer2;
        private TabControl tabTeamBuilderMaster;
        private TabPage tabTeamUnitSelection;
        private ComboBox ddlTeamClasses;
        private TextBox txtTeamUnitName;
        private Button btnTeamAddUnit;
        private TabPage tabTeamGearSelection;
        private TabPage tabTeamSkillSelection;
        private Label label2;
        private Label label1;
        private TextBox txtTeamHeader;
        private TreeView tvwTeam;
        private GroupBox grpPreview;
        private TableLayoutPanel tableLayoutPanel1;
        private Label label3;
        private TextBox txtPreviewUnitName;
        private Label label4;
        private TextBox txtPreviewStats;
        private TabPage tabTeamRulesSelection;
        private Label lblTeamUnitCreator;
        private GroupBox grpRules;
        private Label lblTeamLevel;
        private TextBox txtTeamLevel;
        private GroupBox grpTeamType;
        private RadioButton rbnTeamCampaign;
        private RadioButton rbnTeamPVP;
        private Label label6;
        private Label lblEquipmentRules;
        private CheckBox chbTeamEquipRestrict;
        private Label label7;
        private TextBox txtPreviewAttributes;
        private Button btnTeamRemoveUnit;
        private CheckBox chbRandomizedEnemies;
        private TextBox textBox1;
        private GroupBox groupBox1;
        private RadioButton radioButton5;
        private RadioButton radioButton4;
        private RadioButton radioButton3;
        private RadioButton radioButton2;
        private RadioButton radioButton1;
        private RichTextBox richTextBox1;
        private TabPage tabPatching;
        private DataGridView dgvGear;
    }
}
