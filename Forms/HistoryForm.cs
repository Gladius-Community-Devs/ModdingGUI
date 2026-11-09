namespace ModdingGUI.Forms;

using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using ModdingGUI.Core.Api;
using ModdingGUI.Core.History;

/// <summary>
/// Browse and restore <c>.mod-history</c> for a project: pick a file on the left, pick a timestamp on the right,
/// restore. Restoring backs up the current file first, so it is itself undoable.
/// </summary>
public sealed class HistoryForm : Form
{
    private readonly string _projectRoot;
    private readonly IHistoryApi _history = new HistoryApi();

    private readonly ListView _files = new()
    {
        View = View.Details, FullRowSelect = true, MultiSelect = false, Dock = DockStyle.Fill,
    };

    private readonly ListView _backups = new()
    {
        View = View.Details, FullRowSelect = true, MultiSelect = false, Dock = DockStyle.Fill,
    };

    private readonly Label _status = new()
    {
        Dock = DockStyle.Bottom, Height = 44, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(6, 0, 6, 0),
    };

    private readonly Button _restore = new() { Text = "Restore Selected", Width = 140, Height = 32, Enabled = false };

    /// <summary>True when at least one restore happened, so the caller can refresh its editors.</summary>
    public bool RestoredAnything { get; private set; }

    /// <summary>Create the dialog for a project.</summary>
    /// <param name="projectRoot">Project root (the folder holding <c>_BEC</c>).</param>
    public HistoryForm(string projectRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        _projectRoot = projectRoot;

        Text = "Restore from Backup";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(820, 480);
        Size = new Size(980, 560);
        ShowInTaskbar = false;
        MinimizeBox = false;

        _files.Columns.Add("File", 420);
        _files.Columns.Add("Backups", 70, HorizontalAlignment.Right);
        _files.Columns.Add("Latest", 150);

        _backups.Columns.Add("Taken", 160);
        _backups.Columns.Add("Size", 90, HorizontalAlignment.Right);

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 520,
        };
        split.Panel1.Controls.Add(_files);
        split.Panel2.Controls.Add(_backups);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Height = 48, Padding = new Padding(6),
        };

        var close = new Button { Text = "Close", Width = 90, Height = 32 };
        close.Click += (_, _) => Close();

        _restore.Click += (_, _) => RestoreSelected();

        buttons.Controls.Add(close);
        buttons.Controls.Add(_restore);

        Controls.Add(split);
        Controls.Add(buttons);
        Controls.Add(_status);

        _files.SelectedIndexChanged += (_, _) => LoadBackupsForSelection();
        _backups.SelectedIndexChanged += (_, _) => UpdateRestoreState();

        Load += (_, _) => LoadFiles();
    }

    private void LoadFiles()
    {
        try
        {
            _files.Items.Clear();

            var files = _history.ListFiles(_projectRoot);
            foreach (var file in files)
            {
                var item = new ListViewItem(file.RelativePath) { Tag = file.RelativePath };
                item.SubItems.Add(file.Count.ToString());
                item.SubItems.Add(file.Newest.ToString("yyyy-MM-dd HH:mm:ss"));
                _files.Items.Add(item);
            }

            if (_files.Items.Count > 0) _files.Items[0].Selected = true;

            _status.Text = files.Count == 0
                ? "No backups yet for this project. Every edit writes one before it changes a file."
                : $"{files.Count} file(s) with backups. Pick one, then pick the version to restore.";
        }
        catch (Exception ex)
        {
            _status.Text = $"Could not read history: {ex.Message}";
        }

        LoadBackupsForSelection();
    }

    private string? SelectedRelativePath =>
        _files.SelectedItems.Count > 0 ? _files.SelectedItems[0].Tag as string : null;

    private void LoadBackupsForSelection()
    {
        _backups.Items.Clear();
        string? relative = SelectedRelativePath;
        if (relative is null)
        {
            UpdateRestoreState();
            return;
        }

        try
        {
            foreach (BackupEntry entry in _history.ListFor(_projectRoot, relative))
            {
                var item = new ListViewItem(entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss")) { Tag = entry };
                item.SubItems.Add($"{entry.Size:N0}");
                _backups.Items.Add(item);
            }

            if (_backups.Items.Count > 0) _backups.Items[0].Selected = true;
        }
        catch (Exception ex)
        {
            _status.Text = $"Could not read backups: {ex.Message}";
        }

        UpdateRestoreState();
    }

    private void UpdateRestoreState()
    {
        _restore.Enabled = _backups.SelectedItems.Count > 0;
        if (_backups.SelectedItems.Count > 0 && _backups.SelectedItems[0].Tag is BackupEntry entry)
        {
            _status.Text = $"Restoring puts {entry.RelativePath} back to {entry.Timestamp:yyyy-MM-dd HH:mm:ss}. " +
                           "The current file is backed up first, so this is undoable.";
        }
    }

    private void RestoreSelected()
    {
        if (_backups.SelectedItems.Count == 0 || _backups.SelectedItems[0].Tag is not BackupEntry entry) return;

        var confirm = MessageBox.Show(
            $"Restore '{entry.RelativePath}' to its {entry.Timestamp:yyyy-MM-dd HH:mm:ss} version?\n\n" +
            "The current file is backed up first, so you can undo this by restoring the newer backup.",
            "Restore from Backup", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

        if (confirm != DialogResult.Yes) return;

        try
        {
            _history.Restore(_projectRoot, entry);
            RestoredAnything = true;
            _status.Text = $"Restored {entry.RelativePath} to {entry.Timestamp:yyyy-MM-dd HH:mm:ss}.";
            LoadFiles();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Restore failed:\n\n{ex.Message}", "Restore from Backup",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
