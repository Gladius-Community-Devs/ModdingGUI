// Forms/ProgramInfoForm.cs
using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using ModdingGUI.Models;

namespace ModdingGUI.Forms
{
    public class ProgramInfoForm : Form
    {
        private readonly ProgramInfo _programInfo;

        public ProgramInfoForm(ProgramInfo programInfo)
        {
            _programInfo = programInfo;
            InitializeComponents();
        }

        private void InitializeComponents()
        {
            // Form settings
            this.Text = "About Modding GUI";
            this.Size = new Size(400, 300);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = Color.WhiteSmoke;

            // Create a TableLayoutPanel to organize the content
            TableLayoutPanel tableLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5,
                Padding = new Padding(20),
                CellBorderStyle = TableLayoutPanelCellBorderStyle.None
            };

            // Add title label
            Label titleLabel = new Label
            {
                Text = "Gladius Modding GUI",
                Font = new Font("Segoe UI", 18, FontStyle.Bold),
                AutoSize = true,
                Anchor = AnchorStyles.None
            };
            tableLayout.Controls.Add(titleLabel);
            tableLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            // Add version label
            Label versionLabel = new Label
            {
                Text = $"Version: {_programInfo.Version}",
                Font = new Font("Segoe UI", 12),
                AutoSize = true,
                Anchor = AnchorStyles.None,
                Margin = new Padding(0, 10, 0, 0)
            };
            tableLayout.Controls.Add(versionLabel);
            tableLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            // Add author label
            Label authorLabel = new Label
            {
                Text = $"Created by: {_programInfo.Author}",
                Font = new Font("Segoe UI", 12),
                AutoSize = true,
                Anchor = AnchorStyles.None,
                Margin = new Padding(0, 10, 0, 0)
            };
            tableLayout.Controls.Add(authorLabel);
            tableLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            // Add GitHub link label
            LinkLabel githubLink = new LinkLabel
            {
                Text = "GitHub Releases",
                Font = new Font("Segoe UI", 12),
                AutoSize = true,
                Anchor = AnchorStyles.None,
                Margin = new Padding(0, 10, 0, 0)
            };
            githubLink.LinkClicked += (s, e) => Process.Start(new ProcessStartInfo(_programInfo.GitHubUrl) { UseShellExecute = true });
            tableLayout.Controls.Add(githubLink);
            tableLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            // Add close button
            Button closeButton = new Button
            {
                Text = "Close",
                Size = new Size(100, 30),
                Anchor = AnchorStyles.None,
                Margin = new Padding(0, 20, 0, 0)
            };
            closeButton.Click += (s, e) => this.Close();
            tableLayout.Controls.Add(closeButton);
            tableLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            this.Controls.Add(tableLayout);
        }
    }
}
