namespace ModdingGUI.Forms
{
    partial class SettingsForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
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
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            tabControl1 = new TabControl();
            tabGeneral = new TabPage();
            btnBrowseDolphinPath = new Button();
            txtDolphinPath = new TextBox();
            label4 = new Label();
            btnBrowseProjectDir = new Button();
            txtDefaultProjectDir = new TextBox();
            label1 = new Label();
            chkAutoOpenFolder = new CheckBox();
            chkValidateBeforePacking = new CheckBox();
            tabRandomizer = new TabPage();
            numDefaultRandomIterations = new NumericUpDown();
            label2 = new Label();
            chkEnableRandomizerLogs = new CheckBox();
            tabPatching = new TabPage();
            txtLastUsedPatchServer = new TextBox();
            label3 = new Label();
            chkAutoCheckUpdates = new CheckBox();
            tabUI = new TabPage();
            chkShowAdvancedOptions = new CheckBox();
            btnSave = new Button();
            btnCancel = new Button();
            btnReset = new Button();
            tabControl1.SuspendLayout();
            tabGeneral.SuspendLayout();
            tabRandomizer.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)numDefaultRandomIterations).BeginInit();
            tabPatching.SuspendLayout();
            tabUI.SuspendLayout();
            SuspendLayout();
            // 
            // tabControl1
            // 
            tabControl1.Controls.Add(tabGeneral);
            tabControl1.Controls.Add(tabRandomizer);
            tabControl1.Controls.Add(tabPatching);
            tabControl1.Controls.Add(tabUI);
            tabControl1.Location = new Point(12, 12);
            tabControl1.Name = "tabControl1";
            tabControl1.SelectedIndex = 0;
            tabControl1.Size = new Size(460, 297);
            tabControl1.TabIndex = 0;
            // 
            // tabGeneral
            // 
            tabGeneral.Controls.Add(btnBrowseDolphinPath);
            tabGeneral.Controls.Add(txtDolphinPath);
            tabGeneral.Controls.Add(label4);
            tabGeneral.Controls.Add(btnBrowseProjectDir);
            tabGeneral.Controls.Add(txtDefaultProjectDir);
            tabGeneral.Controls.Add(label1);
            tabGeneral.Controls.Add(chkAutoOpenFolder);
            tabGeneral.Controls.Add(chkValidateBeforePacking);
            tabGeneral.Location = new Point(4, 24);
            tabGeneral.Name = "tabGeneral";
            tabGeneral.Padding = new Padding(3);
            tabGeneral.Size = new Size(452, 269);
            tabGeneral.TabIndex = 0;
            tabGeneral.Text = "General";
            tabGeneral.UseVisualStyleBackColor = true;
            // 
            // btnBrowseDolphinPath
            // 
            btnBrowseDolphinPath.Location = new Point(371, 120);
            btnBrowseDolphinPath.Name = "btnBrowseDolphinPath";
            btnBrowseDolphinPath.Size = new Size(75, 23);
            btnBrowseDolphinPath.TabIndex = 7;
            btnBrowseDolphinPath.Text = "Browse...";
            btnBrowseDolphinPath.UseVisualStyleBackColor = true;
            btnBrowseDolphinPath.Click += btnBrowseDolphinPath_Click;
            // 
            // txtDolphinPath
            // 
            txtDolphinPath.Location = new Point(6, 120);
            txtDolphinPath.Name = "txtDolphinPath";
            txtDolphinPath.Size = new Size(359, 23);
            txtDolphinPath.TabIndex = 6;
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Location = new Point(6, 102);
            label4.Name = "label4";
            label4.Size = new Size(139, 15);
            label4.TabIndex = 5;
            label4.Text = "Dolphin Executable Path:";
            // 
            // btnBrowseProjectDir
            // 
            btnBrowseProjectDir.Location = new Point(371, 70);
            btnBrowseProjectDir.Name = "btnBrowseProjectDir";
            btnBrowseProjectDir.Size = new Size(75, 23);
            btnBrowseProjectDir.TabIndex = 4;
            btnBrowseProjectDir.Text = "Browse...";
            btnBrowseProjectDir.UseVisualStyleBackColor = true;
            btnBrowseProjectDir.Click += btnBrowseProjectDir_Click;
            // 
            // txtDefaultProjectDir
            // 
            txtDefaultProjectDir.Location = new Point(6, 70);
            txtDefaultProjectDir.Name = "txtDefaultProjectDir";
            txtDefaultProjectDir.Size = new Size(359, 23);
            txtDefaultProjectDir.TabIndex = 3;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(6, 52);
            label1.Name = "label1";
            label1.Size = new Size(139, 15);
            label1.TabIndex = 2;
            label1.Text = "Default Project Directory:";
            // 
            // chkAutoOpenFolder
            // 
            chkAutoOpenFolder.AutoSize = true;
            chkAutoOpenFolder.Location = new Point(6, 30);
            chkAutoOpenFolder.Name = "chkAutoOpenFolder";
            chkAutoOpenFolder.Size = new Size(227, 19);
            chkAutoOpenFolder.TabIndex = 1;
            chkAutoOpenFolder.Text = "Automatically open folder after action";
            chkAutoOpenFolder.UseVisualStyleBackColor = true;
            // 
            // chkValidateBeforePacking
            // 
            chkValidateBeforePacking.AutoSize = true;
            chkValidateBeforePacking.Location = new Point(6, 6);
            chkValidateBeforePacking.Name = "chkValidateBeforePacking";
            chkValidateBeforePacking.Size = new Size(202, 19);
            chkValidateBeforePacking.TabIndex = 0;
            chkValidateBeforePacking.Text = "Always validate before packaging";
            chkValidateBeforePacking.UseVisualStyleBackColor = true;
            // 
            // tabRandomizer
            // 
            tabRandomizer.Controls.Add(numDefaultRandomIterations);
            tabRandomizer.Controls.Add(label2);
            tabRandomizer.Controls.Add(chkEnableRandomizerLogs);
            tabRandomizer.Location = new Point(4, 24);
            tabRandomizer.Name = "tabRandomizer";
            tabRandomizer.Padding = new Padding(3);
            tabRandomizer.Size = new Size(452, 269);
            tabRandomizer.TabIndex = 1;
            tabRandomizer.Text = "Randomizer";
            tabRandomizer.UseVisualStyleBackColor = true;
            // 
            // numDefaultRandomIterations
            // 
            numDefaultRandomIterations.Location = new Point(179, 32);
            numDefaultRandomIterations.Maximum = new decimal(new int[] { 1000, 0, 0, 0 });
            numDefaultRandomIterations.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            numDefaultRandomIterations.Name = "numDefaultRandomIterations";
            numDefaultRandomIterations.Size = new Size(120, 23);
            numDefaultRandomIterations.TabIndex = 2;
            numDefaultRandomIterations.Value = new decimal(new int[] { 10, 0, 0, 0 });
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(6, 34);
            label2.Name = "label2";
            label2.Size = new Size(166, 15);
            label2.TabIndex = 1;
            label2.Text = "Default Randomizer Iterations:";
            // 
            // chkEnableRandomizerLogs
            // 
            chkEnableRandomizerLogs.AutoSize = true;
            chkEnableRandomizerLogs.Location = new Point(6, 6);
            chkEnableRandomizerLogs.Name = "chkEnableRandomizerLogs";
            chkEnableRandomizerLogs.Size = new Size(155, 19);
            chkEnableRandomizerLogs.TabIndex = 0;
            chkEnableRandomizerLogs.Text = "Enable Randomizer Logs";
            chkEnableRandomizerLogs.UseVisualStyleBackColor = true;
            // 
            // tabPatching
            // 
            tabPatching.Controls.Add(txtLastUsedPatchServer);
            tabPatching.Controls.Add(label3);
            tabPatching.Controls.Add(chkAutoCheckUpdates);
            tabPatching.Location = new Point(4, 24);
            tabPatching.Name = "tabPatching";
            tabPatching.Size = new Size(452, 269);
            tabPatching.TabIndex = 2;
            tabPatching.Text = "Patching";
            tabPatching.UseVisualStyleBackColor = true;
            // 
            // txtLastUsedPatchServer
            // 
            txtLastUsedPatchServer.Enabled = false;
            txtLastUsedPatchServer.Location = new Point(6, 70);
            txtLastUsedPatchServer.Name = "txtLastUsedPatchServer";
            txtLastUsedPatchServer.Size = new Size(440, 23);
            txtLastUsedPatchServer.TabIndex = 2;
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new Point(6, 52);
            label3.Name = "label3";
            label3.Size = new Size(128, 15);
            label3.TabIndex = 1;
            label3.Text = "Last Used Patch Server:";
            // 
            // chkAutoCheckUpdates
            // 
            chkAutoCheckUpdates.AutoSize = true;
            chkAutoCheckUpdates.Enabled = false;
            chkAutoCheckUpdates.Location = new Point(6, 6);
            chkAutoCheckUpdates.Name = "chkAutoCheckUpdates";
            chkAutoCheckUpdates.Size = new Size(230, 19);
            chkAutoCheckUpdates.TabIndex = 0;
            chkAutoCheckUpdates.Text = "Automatically check for patch updates";
            chkAutoCheckUpdates.UseVisualStyleBackColor = true;
            // 
            // tabUI
            // 
            tabUI.Controls.Add(chkShowAdvancedOptions);
            tabUI.Location = new Point(4, 24);
            tabUI.Name = "tabUI";
            tabUI.Size = new Size(452, 269);
            tabUI.TabIndex = 3;
            tabUI.Text = "UI";
            tabUI.UseVisualStyleBackColor = true;
            // 
            // chkShowAdvancedOptions
            // 
            chkShowAdvancedOptions.AutoSize = true;
            chkShowAdvancedOptions.Enabled = false;
            chkShowAdvancedOptions.Location = new Point(6, 6);
            chkShowAdvancedOptions.Name = "chkShowAdvancedOptions";
            chkShowAdvancedOptions.Size = new Size(156, 19);
            chkShowAdvancedOptions.TabIndex = 0;
            chkShowAdvancedOptions.Text = "Show Advanced Options";
            chkShowAdvancedOptions.UseVisualStyleBackColor = true;
            // 
            // btnSave
            // 
            btnSave.Location = new Point(316, 315);
            btnSave.Name = "btnSave";
            btnSave.Size = new Size(75, 23);
            btnSave.TabIndex = 1;
            btnSave.Text = "Save";
            btnSave.UseVisualStyleBackColor = true;
            btnSave.Click += btnSave_Click;
            // 
            // btnCancel
            // 
            btnCancel.Location = new Point(397, 315);
            btnCancel.Name = "btnCancel";
            btnCancel.Size = new Size(75, 23);
            btnCancel.TabIndex = 2;
            btnCancel.Text = "Cancel";
            btnCancel.UseVisualStyleBackColor = true;
            btnCancel.Click += btnCancel_Click;
            // 
            // btnReset
            // 
            btnReset.Location = new Point(12, 315);
            btnReset.Name = "btnReset";
            btnReset.Size = new Size(118, 23);
            btnReset.TabIndex = 3;
            btnReset.Text = "Reset to Defaults";
            btnReset.UseVisualStyleBackColor = true;
            btnReset.Click += btnReset_Click;
            // 
            // SettingsForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(484, 350);
            Controls.Add(btnReset);
            Controls.Add(btnCancel);
            Controls.Add(btnSave);
            Controls.Add(tabControl1);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "SettingsForm";
            StartPosition = FormStartPosition.CenterParent;
            Text = "Application Settings";
            tabControl1.ResumeLayout(false);
            tabGeneral.ResumeLayout(false);
            tabGeneral.PerformLayout();
            tabRandomizer.ResumeLayout(false);
            tabRandomizer.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)numDefaultRandomIterations).EndInit();
            tabPatching.ResumeLayout(false);
            tabPatching.PerformLayout();
            tabUI.ResumeLayout(false);
            tabUI.PerformLayout();
            ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.TabPage tabGeneral;
        private System.Windows.Forms.TabPage tabRandomizer;
        private System.Windows.Forms.Button btnSave;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnReset;
        private System.Windows.Forms.CheckBox chkValidateBeforePacking;
        private System.Windows.Forms.CheckBox chkAutoOpenFolder;
        private System.Windows.Forms.Button btnBrowseProjectDir;
        private System.Windows.Forms.TextBox txtDefaultProjectDir;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.CheckBox chkEnableRandomizerLogs;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.NumericUpDown numDefaultRandomIterations;
        private System.Windows.Forms.TabPage tabPatching;
        private System.Windows.Forms.CheckBox chkAutoCheckUpdates;
        private System.Windows.Forms.TextBox txtLastUsedPatchServer;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TabPage tabUI;
        private System.Windows.Forms.CheckBox chkShowAdvancedOptions;
        private System.Windows.Forms.Button btnBrowseDolphinPath;
        private System.Windows.Forms.TextBox txtDolphinPath;
        private System.Windows.Forms.Label label4;
    }
}
