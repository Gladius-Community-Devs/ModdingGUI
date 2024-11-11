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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(frmMain));
            tabContainer = new TabControl();
            tabUnpacking = new TabPage();
            btnOpenUnpackLocation = new Button();
            rtbUnpackOutput = new RichTextBox();
            btnUnpack = new Button();
            txtUnpackPath = new TextBox();
            btnSelectISO = new Button();
            txtISOPath = new TextBox();
            tabPacking = new TabPage();
            btnOpenPackLocation = new Button();
            rtbPackOutput = new RichTextBox();
            btnPack = new Button();
            btnPackPath = new Button();
            txtPackPath = new TextBox();
            tabRandomizer = new TabPage();
            txtSeed = new TextBox();
            label1 = new Label();
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
            mnuMain = new MenuStrip();
            optionsToolStripMenuItem = new ToolStripMenuItem();
            saveBatMenuItem = new ToolStripMenuItem();
            randomizerLogsMenuItem = new ToolStripMenuItem();
            funOptionsToolStripMenuItem = new ToolStripMenuItem();
            randomizerMenuItem = new ToolStripMenuItem();
            tabContainer.SuspendLayout();
            tabUnpacking.SuspendLayout();
            tabPacking.SuspendLayout();
            tabRandomizer.SuspendLayout();
            grpBaseOptions.SuspendLayout();
            mnuMain.SuspendLayout();
            SuspendLayout();
            // 
            // tabContainer
            // 
            tabContainer.Controls.Add(tabUnpacking);
            tabContainer.Controls.Add(tabPacking);
            tabContainer.Controls.Add(tabRandomizer);
            tabContainer.Dock = DockStyle.Fill;
            tabContainer.Location = new Point(0, 24);
            tabContainer.Name = "tabContainer";
            tabContainer.SelectedIndex = 0;
            tabContainer.Size = new Size(800, 426);
            tabContainer.TabIndex = 0;
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
            tabUnpacking.Size = new Size(792, 398);
            tabUnpacking.TabIndex = 0;
            tabUnpacking.Text = "Unpacking";
            tabUnpacking.UseVisualStyleBackColor = true;
            // 
            // btnOpenUnpackLocation
            // 
            btnOpenUnpackLocation.Location = new Point(8, 285);
            btnOpenUnpackLocation.Name = "btnOpenUnpackLocation";
            btnOpenUnpackLocation.Size = new Size(179, 23);
            btnOpenUnpackLocation.TabIndex = 5;
            btnOpenUnpackLocation.Text = "Open containing folder";
            btnOpenUnpackLocation.UseVisualStyleBackColor = true;
            btnOpenUnpackLocation.Click += btnOpenUnpackLocation_Click;
            // 
            // rtbUnpackOutput
            // 
            rtbUnpackOutput.Location = new Point(6, 117);
            rtbUnpackOutput.Name = "rtbUnpackOutput";
            rtbUnpackOutput.Size = new Size(778, 162);
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
            txtUnpackPath.Click += txtUnpackPath_Click;
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
            tabPacking.Controls.Add(btnOpenPackLocation);
            tabPacking.Controls.Add(rtbPackOutput);
            tabPacking.Controls.Add(btnPack);
            tabPacking.Controls.Add(btnPackPath);
            tabPacking.Controls.Add(txtPackPath);
            tabPacking.Location = new Point(4, 24);
            tabPacking.Name = "tabPacking";
            tabPacking.Padding = new Padding(3);
            tabPacking.Size = new Size(792, 398);
            tabPacking.TabIndex = 1;
            tabPacking.Text = "Packing";
            tabPacking.UseVisualStyleBackColor = true;
            // 
            // btnOpenPackLocation
            // 
            btnOpenPackLocation.Location = new Point(8, 232);
            btnOpenPackLocation.Name = "btnOpenPackLocation";
            btnOpenPackLocation.Size = new Size(159, 23);
            btnOpenPackLocation.TabIndex = 5;
            btnOpenPackLocation.Text = "Open containing folder";
            btnOpenPackLocation.UseVisualStyleBackColor = true;
            btnOpenPackLocation.Click += btnOpenPackLocation_Click;
            // 
            // rtbPackOutput
            // 
            rtbPackOutput.Location = new Point(6, 83);
            rtbPackOutput.Name = "rtbPackOutput";
            rtbPackOutput.Size = new Size(778, 143);
            rtbPackOutput.TabIndex = 4;
            rtbPackOutput.Text = "";
            // 
            // btnPack
            // 
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
            tabRandomizer.Controls.Add(txtSeed);
            tabRandomizer.Controls.Add(label1);
            tabRandomizer.Controls.Add(pgbRandomizeStatus);
            tabRandomizer.Controls.Add(grpBaseOptions);
            tabRandomizer.Controls.Add(btnRandomizerPath);
            tabRandomizer.Controls.Add(txtRandomizerPath);
            tabRandomizer.Location = new Point(4, 24);
            tabRandomizer.Name = "tabRandomizer";
            tabRandomizer.Padding = new Padding(3);
            tabRandomizer.Size = new Size(792, 398);
            tabRandomizer.TabIndex = 2;
            tabRandomizer.Text = "Randomizer";
            tabRandomizer.UseVisualStyleBackColor = true;
            // 
            // txtSeed
            // 
            txtSeed.Location = new Point(190, 54);
            txtSeed.Name = "txtSeed";
            txtSeed.Size = new Size(219, 23);
            txtSeed.TabIndex = 9;
            txtSeed.Text = "Enter a seed here!";
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(190, 369);
            label1.Name = "label1";
            label1.Size = new Size(42, 15);
            label1.TabIndex = 8;
            label1.Text = "Status:";
            // 
            // pgbRandomizeStatus
            // 
            pgbRandomizeStatus.Location = new Point(238, 367);
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
            chbRandomNoRecruits.Visible = false;
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
            // mnuMain
            // 
            mnuMain.Items.AddRange(new ToolStripItem[] { optionsToolStripMenuItem, funOptionsToolStripMenuItem });
            mnuMain.Location = new Point(0, 0);
            mnuMain.Name = "mnuMain";
            mnuMain.Size = new Size(800, 24);
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
            // frmMain
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 450);
            Controls.Add(tabContainer);
            Controls.Add(mnuMain);
            Icon = (Icon)resources.GetObject("$this.Icon");
            MainMenuStrip = mnuMain;
            Name = "frmMain";
            Text = "Modding Tools GUI";
            Load += frmMain_Load;
            tabContainer.ResumeLayout(false);
            tabUnpacking.ResumeLayout(false);
            tabUnpacking.PerformLayout();
            tabPacking.ResumeLayout(false);
            tabPacking.PerformLayout();
            tabRandomizer.ResumeLayout(false);
            tabRandomizer.PerformLayout();
            grpBaseOptions.ResumeLayout(false);
            grpBaseOptions.PerformLayout();
            mnuMain.ResumeLayout(false);
            mnuMain.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private TabControl tabContainer;
        private TabPage tabUnpacking;
        private TabPage tabPacking;
        private TextBox txtUnpackPath;
        private Button btnSelectISO;
        private TextBox txtISOPath;
        private Button btnUnpack;
        private Button btnPack;
        private Button btnPackPath;
        private TextBox txtPackPath;
        private MenuStrip mnuMain;
        private ToolStripMenuItem optionsToolStripMenuItem;
        private ToolStripMenuItem saveBatMenuItem;
        private RichTextBox rtbUnpackOutput;
        private RichTextBox rtbPackOutput;
        private Button btnOpenUnpackLocation;
        private Button btnOpenPackLocation;
        private ToolStripMenuItem funOptionsToolStripMenuItem;
        private ToolStripMenuItem randomizerMenuItem;
        private TabPage tabRandomizer;
        private Button btnRandomizerPath;
        private TextBox txtRandomizerPath;
        private GroupBox grpBaseOptions;
        private CheckBox chbRandomHeroes;
        private CheckBox chbRandomTeam;
        private Button btnRandomize;
        private CheckBox chbRandomVanillaOrNah;
        private CheckBox chbRandomPermaDeath;
        private CheckBox chbRandomNoRecruits;
        private ProgressBar pgbRandomizeStatus;
        private Label label1;
        private TextBox txtSeed;
        private ToolStripMenuItem randomizerLogsMenuItem;
    }
}
