namespace ModdingGUI;

using System;
using System.IO;
using System.Windows.Forms;
using ModdingGUI.EncounterEditor;

/// <summary>
/// The Encounters tab: hosts <see cref="EncounterEditorView"/> and keeps it pointed at the selected project.
/// </summary>
public partial class frmMain
{
    private EncounterEditorView? _encounterEditor;
    private string? _encounterProject;

    /// <summary>Create the tab and attach the editor. Called once, after the other pages are built.</summary>
    private void BuildEncounterPage()
    {
        _encounterEditor = new EncounterEditorView { Log = _log };

        var page = new TabPage
        {
            Name = "tabEncounterEditor",
            Text = "Encounters",
            UseVisualStyleBackColor = true,
        };
        page.Controls.Add(_encounterEditor);

        // sit next to the Grid Builder: both are per-arena authoring tools
        int gridIndex = tabContainer.TabPages.IndexOfKey("tabGridBuilder");
        if (gridIndex >= 0) tabContainer.TabPages.Insert(gridIndex + 1, page);
        else tabContainer.TabPages.Add(page);

        // The project selection lives in the packing page's path box, which several places set. Syncing when the tab is
        // entered is the one hook that catches all of them without touching each caller.
        page.Enter += (_, _) => SyncEncounterPage();
    }

    /// <summary>Point the encounter editor at the current project, if it has been built.</summary>
    private void SyncEncounterPage()
    {
        if (_encounterEditor is null) return;

        string? project = txtPackPath?.Text;
        if (string.IsNullOrWhiteSpace(project) || !Directory.Exists(project)) return;
        if (string.Equals(project, _encounterProject, StringComparison.OrdinalIgnoreCase)) return;

        _encounterProject = project;
        _encounterEditor.SetProject(project);
    }
}
