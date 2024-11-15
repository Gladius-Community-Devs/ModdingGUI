namespace ModdingGUI
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
            ttpInform = new ToolTip(components);
            splitContainer1 = new SplitContainer();
            tabContainer = new TabControl();
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
            chbRandomPermaDeath = new CheckBox();
            chbRandomNoRecruits = new CheckBox();
            chbRandomVanillaOrNah = new CheckBox();
            btnRandomize = new Button();
            chbRandomTeam = new CheckBox();
            chbRandomHeroes = new CheckBox();
            btnRandomizerPath = new Button();
            txtRandomizerPath = new TextBox();
            tabIngameRandom = new TabPage();
            chbIngameRandom = new CheckBox();
            txtFileHeader = new TextBox();
            tvwProjects = new TreeView();
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
            SuspendLayout();
            // 
            // mnuMain
            // 
            mnuMain.Items.AddRange(new ToolStripItem[] { optionsToolStripMenuItem, funOptionsToolStripMenuItem });
            mnuMain.Location = new Point(0, 0);
            mnuMain.Name = "mnuMain";
            mnuMain.Size = new Size(1042, 24);
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
            funOptionsToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { randomizerMenuItem });
            funOptionsToolStripMenuItem.Name = "funOptionsToolStripMenuItem";
            funOptionsToolStripMenuItem.Size = new Size(84, 20);
            funOptionsToolStripMenuItem.Text = "Fun Options";
            // 
            // randomizerMenuItem
            // 
            randomizerMenuItem.CheckOnClick = true;
            randomizerMenuItem.Name = "randomizerMenuItem";
            randomizerMenuItem.Size = new Size(137, 22);
            randomizerMenuItem.Text = "Randomizer";
            randomizerMenuItem.Click += randomizerMenuItem_Click;
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
            splitContainer1.Size = new Size(1042, 423);
            splitContainer1.SplitterDistance = 785;
            splitContainer1.TabIndex = 3;
            // 
            // tabContainer
            // 
            tabContainer.Controls.Add(tabUnpacking);
            tabContainer.Controls.Add(tabPacking);
            tabContainer.Controls.Add(tabRandomizer);
            tabContainer.Controls.Add(tabIngameRandom);
            tabContainer.Dock = DockStyle.Fill;
            tabContainer.Location = new Point(0, 0);
            tabContainer.Name = "tabContainer";
            tabContainer.SelectedIndex = 0;
            tabContainer.Size = new Size(785, 423);
            tabContainer.TabIndex = 1;
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
            tabUnpacking.Size = new Size(777, 395);
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
            rtbUnpackOutput.Size = new Size(756, 204);
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
            tabPacking.Size = new Size(777, 395);
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
            rtbPackOutput.Size = new Size(762, 237);
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
            txtPackPath.Enabled = false;
            txtPackPath.Location = new Point(99, 6);
            txtPackPath.Name = "txtPackPath";
            txtPackPath.Size = new Size(685, 23);
            txtPackPath.TabIndex = 0;
            txtPackPath.Text = "Project filepath will show up here!";
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
            tabRandomizer.Size = new Size(777, 395);
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
            grpBaseOptions.Controls.Add(chbRandomPermaDeath);
            grpBaseOptions.Controls.Add(chbRandomNoRecruits);
            grpBaseOptions.Controls.Add(chbRandomVanillaOrNah);
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
            // chbRandomPermaDeath
            // 
            chbRandomPermaDeath.AutoSize = true;
            chbRandomPermaDeath.Location = new Point(6, 97);
            chbRandomPermaDeath.Name = "chbRandomPermaDeath";
            chbRandomPermaDeath.Size = new Size(130, 19);
            chbRandomPermaDeath.TabIndex = 6;
            chbRandomPermaDeath.Text = "All fights are deadly";
            chbRandomPermaDeath.UseVisualStyleBackColor = true;
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
            // 
            // chbRandomVanillaOrNah
            // 
            chbRandomVanillaOrNah.AutoSize = true;
            chbRandomVanillaOrNah.Location = new Point(2, 300);
            chbRandomVanillaOrNah.Name = "chbRandomVanillaOrNah";
            chbRandomVanillaOrNah.Size = new Size(168, 19);
            chbRandomVanillaOrNah.TabIndex = 6;
            chbRandomVanillaOrNah.Text = "Check if using a Vanilla ISO";
            chbRandomVanillaOrNah.UseVisualStyleBackColor = true;
            chbRandomVanillaOrNah.Visible = false;
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
            tabIngameRandom.Size = new Size(777, 395);
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
            // txtFileHeader
            // 
            txtFileHeader.Enabled = false;
            txtFileHeader.Location = new Point(5, 0);
            txtFileHeader.Name = "txtFileHeader";
            txtFileHeader.Size = new Size(243, 23);
            txtFileHeader.TabIndex = 4;
            txtFileHeader.Text = "Project File Tree:";
            // 
            // tvwProjects
            // 
            tvwProjects.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            tvwProjects.Location = new Point(5, 24);
            tvwProjects.Name = "tvwProjects";
            tvwProjects.Size = new Size(243, 394);
            tvwProjects.TabIndex = 3;
            tvwProjects.BeforeExpand += tvwProjects_BeforeExpand;
            tvwProjects.NodeMouseHover += tvwProjects_NodeMouseHover;
            tvwProjects.AfterSelect += tvwProjects_AfterSelect;
            tvwProjects.NodeMouseDoubleClick += tvwProjects_NodeMouseDoubleClick;
            // 
            // frmMain
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1042, 450);
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
        private CheckBox chbRandomVanillaOrNah;
        private Button btnRandomize;
        private CheckBox chbRandomTeam;
        private CheckBox chbRandomHeroes;
        private Button btnRandomizerPath;
        private TextBox txtRandomizerPath;
        private TabPage tabIngameRandom;
        private CheckBox chbIngameRandom;
        private TreeView tvwProjects;
        private TextBox txtFileHeader;
    }
}
