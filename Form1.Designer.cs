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
            mnuMain = new MenuStrip();
            optionsToolStripMenuItem = new ToolStripMenuItem();
            saveBatMenuItem = new ToolStripMenuItem();
            tabContainer.SuspendLayout();
            tabUnpacking.SuspendLayout();
            tabPacking.SuspendLayout();
            mnuMain.SuspendLayout();
            SuspendLayout();
            // 
            // tabContainer
            // 
            tabContainer.Controls.Add(tabUnpacking);
            tabContainer.Controls.Add(tabPacking);
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
            txtPackPath.Location = new Point(99, 7);
            txtPackPath.Name = "txtPackPath";
            txtPackPath.Size = new Size(685, 23);
            txtPackPath.TabIndex = 0;
            txtPackPath.Text = "Project filepath will show up here!";
            // 
            // mnuMain
            // 
            mnuMain.Items.AddRange(new ToolStripItem[] { optionsToolStripMenuItem });
            mnuMain.Location = new Point(0, 0);
            mnuMain.Name = "mnuMain";
            mnuMain.Size = new Size(800, 24);
            mnuMain.TabIndex = 1;
            mnuMain.Text = "menuStrip1";
            // 
            // optionsToolStripMenuItem
            // 
            optionsToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { saveBatMenuItem });
            optionsToolStripMenuItem.Name = "optionsToolStripMenuItem";
            optionsToolStripMenuItem.Size = new Size(61, 20);
            optionsToolStripMenuItem.Text = "Options";
            // 
            // saveBatMenuItem
            // 
            saveBatMenuItem.CheckOnClick = true;
            saveBatMenuItem.Name = "saveBatMenuItem";
            saveBatMenuItem.Size = new Size(180, 22);
            saveBatMenuItem.Text = "Save BAT file";
            // 
            // frmMain
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 450);
            Controls.Add(tabContainer);
            Controls.Add(mnuMain);
            MainMenuStrip = mnuMain;
            Name = "frmMain";
            Text = "Modding Tools GUI";
            tabContainer.ResumeLayout(false);
            tabUnpacking.ResumeLayout(false);
            tabUnpacking.PerformLayout();
            tabPacking.ResumeLayout(false);
            tabPacking.PerformLayout();
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
    }
}
