namespace ModdingGUI
{
    using System.Text.Json;

    /// <summary>
    /// Driver commands for the Grid Builder, so an agent can read and edit a grid without a mouse.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The view exposes the operations (<see cref="GridBuilderView.AutomationState"/> and friends); these commands are the
    /// transport. One command with an <c>action</c> keeps the driver surface small:
    /// </para>
    /// <code>
    /// {"cmd":"grid","action":"state"}
    /// {"cmd":"grid","action":"cell","row":12,"col":7}
    /// {"cmd":"grid","action":"setTag","row":12,"col":7,"tag":"Start1","on":true}
    /// {"cmd":"grid","action":"clear","row":12,"col":7}
    /// {"cmd":"grid","action":"slot","tag":"Start1"}
    /// {"cmd":"grid","action":"arena","name":"expanses"}
    /// {"cmd":"grid","action":"save"}
    /// {"cmd":"grid","action":"map","visible":true,"x":0,"y":0,"scale":2.5,"rotate":0}
    /// </code>
    /// <para>
    /// <c>on</c> defaults to true, so "put Start1 on this cell" needs no extra field.
    /// </para>
    /// </remarks>
    public partial class frmMain
    {
        private static readonly string[] GridActions =
        {
            "state", "cell", "setTag", "clear", "slot", "arena", "save", "map",
        };

        /// <summary>Dispatch a <c>grid</c> command to the Grid Builder view.</summary>
        private string GridCommand(JsonElement request)
        {
            if (Controls.Find("GridBuilderView", searchAllChildren: true).FirstOrDefault() is not GridBuilderView view)
            {
                return JsonSerializer.Serialize(new { ok = false, reason = "the Grid Builder view is not in the form" });
            }

            if (!request.TryGetProperty("action", out JsonElement actionElement) ||
                actionElement.GetString() is not { Length: > 0 } action)
            {
                return JsonSerializer.Serialize(new { ok = false, reason = "action is required", actions = GridActions });
            }

            switch (action.ToLowerInvariant())
            {
                case "state":
                    return view.AutomationState();

                case "cell":
                    return view.AutomationCell(GridInt(request, "row"), GridInt(request, "col"));

                case "settag":
                    return view.AutomationSetTag(
                        GridInt(request, "row"),
                        GridInt(request, "col"),
                        GridText(request, "tag"),
                        !request.TryGetProperty("on", out JsonElement on) || on.GetBoolean());

                case "clear":
                    return view.AutomationClearCell(GridInt(request, "row"), GridInt(request, "col"));

                case "slot":
                    return view.AutomationSelectSlot(GridText(request, "tag"));

                case "arena":
                    return view.AutomationLoadArena(GridText(request, "name"));

                case "save":
                    return view.AutomationSave();

                case "map":
                    return view.AutomationSetMap(
                        request.TryGetProperty("visible", out JsonElement visible) && visible.GetBoolean(),
                        request.TryGetProperty("x", out JsonElement x) ? x.GetInt32() : null,
                        request.TryGetProperty("y", out JsonElement y) ? y.GetInt32() : null,
                        request.TryGetProperty("scale", out JsonElement scale) ? scale.GetDecimal() : null,
                        request.TryGetProperty("rotate", out JsonElement rotate) ? rotate.GetInt32() : null);

                default:
                    return JsonSerializer.Serialize(new
                    {
                        ok = false,
                        reason = $"unknown grid action '{action}'",
                        actions = GridActions,
                    });
            }
        }

        private static int GridInt(JsonElement request, string name) =>
            request.TryGetProperty(name, out JsonElement value) ? value.GetInt32() : -1;

        private static string GridText(JsonElement request, string name) =>
            request.TryGetProperty(name, out JsonElement value) ? value.GetString() ?? string.Empty : string.Empty;
    }
}
