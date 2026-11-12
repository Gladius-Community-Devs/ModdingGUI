namespace ModdingGUI
{
    using System.Text.Json;
    using ModdingGUI.Core;
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
            "list", "get", "fields", "setHeader", "removeField", "setTeamField", "addTeamField", "removeTeamField", "setUnits", "setUnit", "setUnitFields", "validate",
        };

        private readonly EncountersApi _encountersApi = new();
        private readonly ClassDefsApi _classesApi = new();

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
                                        levelMinOffset = unit?.Slot4LevelMinOffset,
                                        levelMaxOffset = unit?.Slot5LevelMaxOffset,
                                        levelJitter = unit?.Slot6LevelJitter,
                                    };
                                }).ToArray(),
                            }).ToArray(),
                        });
                    }

                    case "fields":
                    {
                        // The dropdown contents for an encounter: enemy schools for a team, gladiators filtered to the
                        // school, class tags, and the start markers of this arena's own grid.
                        string path = RequirePath(request);
                        EncounterDto? encounter = _encountersApi.Get(project, path);
                        if (encounter is null) return UiFail($"no encounter at '{path}'");

                        var choices = new EncounterChoices(_classesApi);
                        IReadOnlyList<string> schools = choices.EnemySchools(project);
                        IReadOnlyDictionary<int, IReadOnlyList<string>> bySchool = choices.GladiatorsBySchool(project, out IReadOnlyDictionary<string, GladiatorInfo> gladiators);
                        IReadOnlyList<string> classTags = choices.ClassTags(project);

                        // The arena grid the units spawn on: the team's own GRIDFILE, else the encounter's.
                        // GRIDFILE values are quoted and use the game's path aliases: $d is the data root, $l the levels
                        // root. Resolve both, and skip quietly when this arena's grid is not in the project.
                        string? gridField = encounter.Teams.Select(t2 => t2.Value("GRIDFILE")).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))
                                            ?? encounter.Value("GRIDFILE");
                        IReadOnlyList<string> startMarkers = Array.Empty<string>();
                        if (!string.IsNullOrWhiteSpace(gridField))
                        {
                            string relative = Unquote(gridField!)
                                .Replace("$l\\", "data/levels/", StringComparison.OrdinalIgnoreCase)
                                .Replace("$d\\", "data/", StringComparison.OrdinalIgnoreCase)
                                .Replace('\\', '/');
                            string candidate = Path.Combine(ProjectRoots.ResolveBecFolder(project), relative);
                            if (File.Exists(candidate)) startMarkers = EncounterChoices.StartMarkers(candidate);
                        }


                        // The two runtime tokens are resolvable from data: the encounter lives in
                        // data/encounters/<region>/<arena>/, the arena's league file lists the town's schools, and the region
                        // is the union of its arenas' lists. HOMETOWNSCHOOL/REGIONSCHOOL then have something to show.
                        string[] segments = path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
                        string regionGuess = segments.Length >= 4 ? segments[2] : string.Empty;
                        string arenaGuess = segments.Length >= 5 ? segments[3] : string.Empty;

                        IReadOnlyList<string> homeTownSchools = WorldRegions.ArenaSchools(project, arenaGuess);
                        IReadOnlyList<string> regionSchools = WorldRegions.RegionSchools(project, regionGuess);

                        var teams = new List<object>();
                        foreach (EncounterTeamDto team in encounter.Teams)
                        {
                            string? schoolName = Unquote(team.Value("SCHOOL"));
                            int schoolIndex = -1;
                            for (int i = 0; i < schools.Count && schoolName is not null; i++)
                            {
                                if (string.Equals(schools[i], schoolName, StringComparison.OrdinalIgnoreCase)) { schoolIndex = i; break; }
                            }

                            IReadOnlyList<string> unitNames = schoolIndex >= 0 && bySchool.TryGetValue(schoolIndex, out IReadOnlyList<string>? names)
                                ? names
                                : Array.Empty<string>();

                            // A team naming a runtime token gets the schools it will actually draw from.
                            IReadOnlyList<string> resolved = schoolName switch
                            {
                                var s when string.Equals(s, "HOMETOWNSCHOOL", StringComparison.OrdinalIgnoreCase) => homeTownSchools,
                                var s when string.Equals(s, "REGIONSCHOOL", StringComparison.OrdinalIgnoreCase) => regionSchools,
                                _ => Array.Empty<string>(),
                            };

                            teams.Add(new
                            {
                                index = team.Index,
                                school = schoolName,
                                resolvedSchools = resolved,
                                unitsInSchool = unitNames.Count,
                                unitNames,
                            });
                        }

                        return UiOk(new
                        {
                            file = path,
                            region = regionGuess,
                            arena = arenaGuess,
                            resolvedSchools = new { homeTown = homeTownSchools, region = regionSchools },
                            unitFields = EncounterUnitFields.All.Select(f => new
                            {
                                slot = f.Slot,
                                key = f.Key,
                                label = f.Label,
                                kind = f.Kind.ToString(),
                                description = f.Description,
                                minimum = f.Minimum,
                                maximum = f.Maximum,
                                choices = f.Choices,
                                observed = f.ObservedValues,
                            }).ToArray(),
                            choices = new
                            {
                                enemySchools = schools,
                                classTags,
                                startMarkers,
                                gladiatorCount = gladiators.Count,
                            },
                            teams,
                        });
                    }

                    case "setunitfields":
                    {
                        // Named values in, one valid UNITDB row out. Everything is checked before the write, so a rejected
                        // value cannot leave the team partly edited.
                        string path = RequirePath(request);
                        int team = EncounterInt(request, "team") ?? throw new ArgumentException("team is required");
                        int index = EncounterInt(request, "index") ?? throw new ArgumentException("index is required");

                        EncounterDto? encounter = _encountersApi.Get(project, path);
                        EncounterTeamDto? target = encounter?.Team(team);
                        if (target is null) return UiFail($"no team {team} in '{path}'");
                        if (index < 0 || index >= target.Units.Count) return UiFail($"team {team} has no unit {index} (0..{target.Units.Count - 1})");

                        EncounterUnit? unit = EncounterUnit.TryParse(target.Units[index]);
                        if (unit is null) return UiFail($"unit {index} of team {team} does not parse");

                        var applied = new List<object>();
                        if (request.TryGetProperty("values", out JsonElement values))
                        {
                            foreach (JsonProperty property in values.EnumerateObject())
                            {
                                EncounterUnitField? field = EncounterUnitFields.ForKey(property.Name);
                                if (field is null) return UiFail($"unknown unit field '{property.Name}'");

                                string text = property.Value.ValueKind == JsonValueKind.String
                                    ? property.Value.GetString() ?? string.Empty
                                    : property.Value.ToString();

                                if (!EncounterUnitEdit.TryApply(unit, field.Slot, text, out EncounterUnit? edited, out string? error) || edited is null)
                                {
                                    return UiFail(error ?? $"'{text}' is not valid for {field.Key}");
                                }

                                unit = edited;
                                applied.Add(new { field = field.Key, slot = field.Slot, value = text });
                            }
                        }

                        _encountersApi.SetTeamUnit(project, path, team, index, unit);
                        return UiOk(new { file = path, team, index, applied, raw = unit.RebuildRaw() });
                    }

                    case "setheader":
                    {
                        string path = RequirePath(request);
                        string key = Require(request, "key");
                        string value = Require(request, "value");
                        _encountersApi.SetField(project, path, key, value);
                        return UiOk(new { file = path, key, value, changed = true });
                    }

                    case "removefield":
                    {
                        string path = RequirePath(request);
                        string key = Require(request, "key");
                        _encountersApi.RemoveField(project, path, key);
                        return UiOk(new { file = path, key, removed = true });
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

                    case "removeteamfield":
                    {
                        string path = RequirePath(request);
                        int team = EncounterInt(request, "team") ?? throw new ArgumentException("team is required");
                        string key = Require(request, "key");
                        _encountersApi.RemoveTeamField(project, path, team, key);
                        return UiOk(new { file = path, team, key, removed = true });
                    }

                    case "addteamfield":
                    {
                        string path = RequirePath(request);
                        int team = EncounterInt(request, "team") ?? throw new ArgumentException("team is required");
                        string key = Require(request, "key");
                        string value = Require(request, "value");
                        _encountersApi.AddTeamField(project, path, team, key, value);
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

        /// <summary>Field values come back exactly as written, i.e. quoted for string slots.</summary>
        private static string? Unquote(string? value)
        {
            if (value is null) return null;
            string trimmed = value.Trim();
            return trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[^1] == '"' ? trimmed[1..^1] : trimmed;
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
