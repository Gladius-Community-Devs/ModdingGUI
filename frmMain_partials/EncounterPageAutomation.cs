namespace ModdingGUI;

using System;
using System.Text.Json;

/// <summary>
/// The <c>encpage</c> driver command: the Encounters tab's agent surface, so the page can be exercised and screenshotted
/// without a human at the mouse.
/// </summary>
/// <remarks>
/// Separate from <c>encounters</c> on purpose: that command edits files directly through the API, while this one drives the
/// <em>page</em> - which is what verifies that the page's controls, validation and draft state behave. Reading a row here
/// returns what the page is holding, including unsaved edits.
/// </remarks>
public partial class frmMain
{
    private string EncounterPageCommand(JsonElement request)
    {
        if (_encounterEditor is null) return UiFail("the encounter editor is not built");

        string action = UiString(request, "action")?.ToLowerInvariant() ?? "state";

        try
        {
            switch (action)
            {
                case "files":
                    SyncEncounterPage();
                    return UiOk(_encounterEditor.AutomationFiles(UiString(request, "path")));

                case "dialogs":
                {
                    // construct both modals without showing them: proves the layout code cannot throw
                    using (var attributes = new ModdingGUI.EncounterEditor.AttributeMaskDialog(1056))
                    {
                        using var units = new ModdingGUI.EncounterEditor.UnitPickerDialog(
                            _encounterEditor.UnitRowsForAutomation(), "Ludo");
                        return UiOk(new
                        {
                            attributeMask = attributes.Mask,
                            attributeControls = attributes.Controls.Count,
                            unitCount = _encounterEditor.UnitRowsForAutomation().Count,
                            unitSelected = units.SelectedUnit,
                            classTagChoices = _encounterEditor.ClassTagChoiceCount,
                        });
                    }
                }

                case "displaynames":
                {
                    string project = txtPackPath?.Text ?? string.Empty;
                    var names = ModdingGUI.Core.Api.ArenaCatalog.EncounterDisplayNames(project);
                    return UiOk(new { count = names.Count, battle1 = names.TryGetValue("battle1", out string? n) ? n : null });
                }

                case "leagues":
                {
                    string project = txtPackPath?.Text ?? string.Empty;
                    string arena = UiString(request, "arena") ?? throw new ArgumentException("arena is required");
                    return UiOk(ModdingGUI.Core.Api.ArenaCatalog.Leagues(project, arena)
                        .Select(l => new { l.Name, count = l.EncounterPaths.Count, first = l.EncounterPaths.FirstOrDefault() })
                        .ToArray());
                }

                case "nav":
                    SyncEncounterPage();
                    return UiOk(_encounterEditor.AutomationNavigation());

                case "navigate":
                    _encounterEditor.AutomationNavigate(UiString(request, "region"), UiString(request, "town"), UiString(request, "arena"), UiString(request, "league"));
                    return UiOk(_encounterEditor.AutomationNavigation());

                case "state":
                    SyncEncounterPage();
                    return UiOk(_encounterEditor.AutomationState());

                case "open":
                {
                    SyncEncounterPage();
                    string path = UiString(request, "path") ?? throw new ArgumentException("path is required");
                    _encounterEditor.AutomationOpen(path);
                    return UiOk(_encounterEditor.AutomationState());
                }

                case "team":
                {
                    int team = EncounterInt(request, "team") ?? throw new ArgumentException("team is required");
                    _encounterEditor.AutomationSelectTeam(team);
                    return UiOk(_encounterEditor.AutomationState());
                }

                case "row":
                {
                    int row = EncounterInt(request, "row") ?? throw new ArgumentException("row is required");
                    _encounterEditor.AutomationSelectRow(row);
                    return UiOk(_encounterEditor.AutomationReadRow());
                }

                case "readcontrols":
                    return UiOk(_encounterEditor.AutomationControls());

                case "readrow":
                    return UiOk(_encounterEditor.AutomationReadRow());

                case "setslot":
                {
                    int slot = EncounterInt(request, "slot") ?? throw new ArgumentException("slot is required");
                    string text = UiString(request, "text") ?? throw new ArgumentException("text is required");
                    return UiOk(_encounterEditor.AutomationSetSlot(slot, text));
                }

                case "saverows":
                {
                    _encounterEditor.AutomationSaveRows();
                    return UiOk(_encounterEditor.AutomationState());
                }

                case "addrow":
                {
                    _encounterEditor.AutomationAddRow();
                    return UiOk(_encounterEditor.AutomationState());
                }

                case "removerow":
                {
                    _encounterEditor.AutomationRemoveRow();
                    return UiOk(_encounterEditor.AutomationState());
                }

                case "validate":
                {
                    _encounterEditor.AutomationValidate();
                    return UiOk(_encounterEditor.AutomationState());
                }

                case "controls":
                    return UiOk(new { names = System.Linq.Enumerable.ToArray(_encounterEditor.ControlNames()) });

                default:
                    return UiFail($"unknown action '{action}'");
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or System.IO.IOException
                                       or UnauthorizedAccessException)
        {
            return UiFail(ex.Message);
        }
    }
}
