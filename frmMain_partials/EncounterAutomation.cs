namespace ModdingGUI
{
    using System.Text.Json;
    using ModdingGUI.Core.Api;

    /// <summary>
    /// The <c>encounters</c> driver command: the encounter editor's agent surface, and the first half of the encounter UI.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A <c>.enc</c> file is a header (ENCOUNTERNAME, SCENE, GRIDFILE, PRIZETIER rows, ...) followed by <c>TEAM:</c> blocks.
    /// Each team has its own fields and a list of <c>UNITDB:</c> rows, and a row is 21 positional slots - which is exactly
    /// the sort of thing an agent should not be asked to count in its head. So this command speaks in file/team/unit
    /// indices and raw rows, and <see cref="EncounterUnit.TryParse"/> validates a row before anything is written.
    /// </para>
    /// <para>
    /// Deliberately no file dialogs and no dialogs of any kind: it has to work while the app is unattended.
    /// </para>
    /// </remarks>
    public partial class frmMain
    {
        private static readonly string[] EncounterActions =
        {
            "list", "get", "setHeader", "setTeamField", "setUnits", "setUnit", "validate",
        };

        private readonly EncountersApi _encountersApi = new();

        private string EncountersCommand(JsonElement request)
        {
            string action = UiString(request, "action")?.ToLowerInvariant() ?? "list";

            string? project = UiString(request, "project");
            if (string.IsNullOrWhiteSpace(project)) project = txtPackPath?.Text;
            if (string.IsNullOrWhiteSpace(project) || !Directory.Exists(project))
            {
                return UiFail("no project selected; pass 'project' or select a project in the tree");
            }

            try
            {
                switch (action)
                {
                    case "list":
                    {
                        IReadOnlyList<string> files = _encountersApi.ListFiles(project);
                        string? filter = UiString(request, "filter");
                        IEnumerable<string> filtered = files;
                        if (!string.IsNullOrWhiteSpace(filter))
                        {
                            filtered = files.Where(f => f.Contains(filter, StringComparison.OrdinalIgnoreCase));
                        }

                        string[] result = filtered.OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToArray();
                        return UiOk(new { total = files.Count, count = result.Length, files = result });
                    }

                    case "get":
                    {
                        string path = RequirePath(request);
                        EncounterDto? encounter = _encountersApi.Get(project, path);
                        if (encounter is null) return UiFail($"no encounter at '{path}'");

                        return UiOk(new
                        {
                            file = encounter.FilePath,
                            name = encounter.Name,
                            unitCount = encounter.UnitCount,
                            fields = encounter.Fields.Select(f => new { key = f.Key, rawValue = f.RawValue }).ToArray(),
                            teams = encounter.Teams.Select(team => new
                            {
                                index = team.Index,
                                rawValue = team.RawValue,
                                fields = team.Fields.Select(f => new { key = f.Key, rawValue = f.RawValue }).ToArray(),
                                units = team.Units.Select((raw, unitIndex) =>
                                {
                                    EncounterUnit? unit = EncounterUnit.TryParse(raw);
                                    return new
                                    {
                                        index = unitIndex,
                                        raw,
                                        valid = unit is not null,
                                        name = unit?.UnitName,
                                        chance = unit?.SpawnChancePercent,
                                        startSlot = unit?.StartSlot,
                                        @class = unit?.ClassTag1,
                                        subType = unit?.ClassTag2,
                                        level = unit?.PositionX,
                                    };
                                }).ToArray(),
                            }).ToArray(),
                        });
                    }

                    case "setheader":
                    {
                        string path = RequirePath(request);
                        string key = Require(request, "key");
                        string value = Require(request, "value");
                        _encountersApi.SetField(project, path, key, value);
                        return UiOk(new { file = path, key, value, changed = true });
                    }

                    case "setteamfield":
                    {
                        string path = RequirePath(request);
                        int team = EncounterInt(request, "team") ?? throw new ArgumentException("team is required");
                        string key = Require(request, "key");
                        string value = Require(request, "value");
                        _encountersApi.SetTeamField(project, path, team, key, value);
                        return UiOk(new { file = path, team, key, value, changed = true });
                    }

                    case "setunits":
                    {
                        // Replaces a team's whole unit list. Every row is validated first, so a bad row cannot leave the team
                        // half-written.
                        string path = RequirePath(request);
                        int team = EncounterInt(request, "team") ?? throw new ArgumentException("team is required");
                        string[] units = ReadStringArray(request, "units");
                        string[] invalid = units.Where(u => EncounterUnit.TryParse(u) is null).ToArray();
                        if (invalid.Length > 0)
                        {
                            return UiFail($"{invalid.Length} unit row(s) do not parse as a 21-slot UNITDB value: {string.Join(" | ", invalid.Take(3))}");
                        }

                        _encountersApi.SetTeamUnits(project, path, team, units);
                        return UiOk(new { file = path, team, count = units.Length, changed = true });
                    }

                    case "setunit":
                    {
                        string path = RequirePath(request);
                        int team = EncounterInt(request, "team") ?? throw new ArgumentException("team is required");
                        int index = EncounterInt(request, "index") ?? throw new ArgumentException("index is required");
                        string raw = Require(request, "raw");
                        EncounterUnit? unit = EncounterUnit.TryParse(raw);
                        if (unit is null) return UiFail("'raw' does not parse as a 21-slot UNITDB value");

                        _encountersApi.SetTeamUnit(project, path, team, index, unit);
                        return UiOk(new { file = path, team, index, name = unit.UnitName, changed = true });
                    }

                    case "validate":
                    {
                        string path = RequirePath(request);
                        EncounterDto? encounter = _encountersApi.Get(project, path);
                        if (encounter is null) return UiFail($"no encounter at '{path}'");

                        var problems = new List<object>();
                        foreach (EncounterTeamDto team in encounter.Teams)
                        {
                            for (int unit = 0; unit < team.Units.Count; unit++)
                            {
                                if (EncounterUnit.TryParse(team.Units[unit]) is null)
                                {
                                    problems.Add(new { team = team.Index, unit, issue = "row-does-not-parse", raw = team.Units[unit] });
                                }
                            }
                        }

                        return UiOk(new { file = path, teams = encounter.Teams.Count, units = encounter.UnitCount, problems });
                    }

                    default:
                        return UiFail($"unknown action '{action}' (actions: {string.Join(", ", EncounterActions)})");
                }
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException or ArgumentException)
            {
                return UiFail(ex.Message);
            }
        }

        private static int? EncounterInt(JsonElement request, string property) =>
            request.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.Number
                ? value.GetInt32()
                : null;

        private static string RequirePath(JsonElement request) => Require(request, "path");

        private static string Require(JsonElement request, string property)
        {
            string? value = UiString(request, property);
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException($"{property} is required");
            return value;
        }

        private static string[] ReadStringArray(JsonElement request, string property)
        {
            if (!request.TryGetProperty(property, out JsonElement array) || array.ValueKind != JsonValueKind.Array)
            {
                throw new ArgumentException($"{property} must be an array of UNITDB strings");
            }

            return array.EnumerateArray().Select(e => e.GetString() ?? string.Empty).ToArray();
        }
    }
}
