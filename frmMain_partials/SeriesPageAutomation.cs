namespace ModdingGUI;

using System;
using System.Text.Json;

/// <summary>
/// The <c>seriespage</c> driver command: the Series tab's agent surface.
/// </summary>
public partial class frmMain
{
    private string SeriesPageCommand(JsonElement request)
    {
        if (_seriesEditor is null) return UiFail("the series editor is not built");

        string action = UiString(request, "action")?.ToLowerInvariant() ?? "state";

        try
        {
            switch (action)
            {
                case "state":
                    SyncSeriesPage();
                    return UiOk(_seriesEditor.AutomationState());

                case "league":
                {
                    SyncSeriesPage();
                    string league = UiString(request, "league") ?? throw new ArgumentException("league is required");
                    _seriesEditor.AutomationSelectLeague(league);
                    return UiOk(_seriesEditor.AutomationState());
                }

                case "stage":
                {
                    int index = EncounterInt(request, "index") ?? throw new ArgumentException("index is required");
                    _seriesEditor.AutomationSelectStage(index);
                    return UiOk(_seriesEditor.AutomationState());
                }

                case "type":
                {
                    string type = UiString(request, "type") ?? throw new ArgumentException("type is required");
                    return UiOk(_seriesEditor.AutomationSetType(type));
                }

                case "teams":
                {
                    int[] sizes = request.TryGetProperty("sizes", out JsonElement array) && array.ValueKind == JsonValueKind.Array
                        ? System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Select(array.EnumerateArray(), e => e.GetInt32()))
                        : Array.Empty<int>();
                    return UiOk(_seriesEditor.AutomationSetTeams(sizes));
                }

                case "file":
                {
                    string file = UiString(request, "file") ?? throw new ArgumentException("file is required");
                    return UiOk(_seriesEditor.AutomationSetFile(file));
                }

                case "move":
                {
                    int direction = EncounterInt(request, "direction") ?? throw new ArgumentException("direction is required");
                    _seriesEditor.AutomationMove(direction);
                    return UiOk(_seriesEditor.AutomationState());
                }

                case "validate":
                    _seriesEditor.AutomationValidate();
                    return UiOk(_seriesEditor.AutomationState());

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