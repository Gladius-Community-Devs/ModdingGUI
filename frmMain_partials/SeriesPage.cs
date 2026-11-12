namespace ModdingGUI;

using System;
using System.IO;
using System.Windows.Forms;
using ModdingGUI.SeriesEditor;

/// <summary>
/// The Series tab: the league runs a player progresses through, with its stages in order.
/// </summary>
public partial class frmMain
{
    private SeriesEditorView? _seriesEditor;
    private string? _seriesProject;

    /// <summary>Create the tab and attach the editor.</summary>
    private void BuildSeriesPage()
    {
        _seriesEditor = new SeriesEditorView { Log = _log };
        _seriesEditor.EncounterRequested += (_, path) =>
        {
            // "Edit its encounter" hands the stage's file to the Encounters tab
            if (tabContainer.TabPages.ContainsKey("tabEncounterEditor"))
            {
                foreach (TabPage page in tabContainer.TabPages)
                {
                    if (page.Name != "tabEncounterEditor") continue;
                    tabContainer.SelectedTab = page;
                    _encounterEditor?.AutomationOpen(path);
                    break;
                }
            }
        };

        var tab = new TabPage { Name = "tabSeriesEditor", Text = "Series", UseVisualStyleBackColor = true };
        tab.Controls.Add(_seriesEditor);

        int gridIndex = tabContainer.TabPages.IndexOfKey("tabGridBuilder");
        int encounterIndex = tabContainer.TabPages.IndexOfKey("tabEncounterEditor");
        int at = Math.Max(gridIndex, encounterIndex);
        if (at >= 0) tabContainer.TabPages.Insert(at + (encounterIndex >= 0 ? 1 : 1), tab);
        else tabContainer.TabPages.Add(tab);

        tab.Enter += (_, _) => SyncSeriesPage();
    }

    /// <summary>Point the series editor at the current project.</summary>
    private void SyncSeriesPage()
    {
        if (_seriesEditor is null) return;

        string? project = txtPackPath?.Text;
        if (string.IsNullOrWhiteSpace(project) || !Directory.Exists(project)) return;
        if (string.Equals(project, _seriesProject, StringComparison.OrdinalIgnoreCase)) return;

        _seriesProject = project;
        _seriesEditor.SetProject(project);
    }
}