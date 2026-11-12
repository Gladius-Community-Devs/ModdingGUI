# The MCP server (`ModdingGUI.Mcp`)

`ModdingGUI.Mcp` exposes the Core API layer — the same APIs the WinForms shell is a parameter builder over — as
[Model Context Protocol](https://modelcontextprotocol.io) tools over stdio. An AI client (Claude Desktop, OpenCode,
anything that speaks MCP) can therefore read and edit a Gladius project through exactly the code path a user clicks
through: project lock, timestamped `.mod-history` backup, byte-faithful writer.

It is **not** the UI driver (`--ui-server`, see `UI_AUTOMATION.md`). That one drives the *window* and exists to test
and operate the shell; this one exposes the *data model*. They share nothing but `IAppLog` and `JobRegistry`.

- 149 tools across 18 families, every public method of the 16 Core API interfaces reachable, plus `logs_*`,
  `project_*` and two documented extras (`skills_catalog_describe_field`, `encounters_fields`, `grids_set_tag`,
  `grids_set_value`).
- One project per process, fixed at startup.
- Writes take `strict: bool = false`. Advisory by default (log + report + write, like the GUI); with `strict: true` an
  edit that introduces a validation warning is reverted and refused.
- Long operations are Core jobs: progress notifications, and the client's cancellation stops the child process tree.

---

## Running it

```
ModdingGUI.Mcp [--project <path>] [--tools <dir>] [--python <command>] [--log <file>] [--help]
```

| Option | Default | Meaning |
|---|---|---|
| `--project <path>` | the current directory | Project root **or** its `_BEC` folder. Validated at startup. |
| `--tools <dir>` | `<app>/tools` | Toolbelt folder for `project_pack` / `project_unpack`. |
| `--python <cmd>` | `python` | Interpreter for the script toolchain (`"py -3"` works). Unused when the bundled toolchain is present. |
| `--log <file>` | `%LOCALAPPDATA%\GladiusModding\last-run.log` | App-log sink. Appended, never truncated, so a GUI session's log is not wiped. |
| `--help` | — | Usage on stderr, exit 0. |

A bare path (`ModdingGUI.Mcp C:\mods\MyMod`) still works — that was the pre-Phase-8 contract.

**Startup is validated.** The path must exist and resolve to a folder with `data/config`. Anything else is one clear
line on stderr and exit code 2, never a half-working server:

```
ModdingGUI.Mcp: 'C:\src\repo' is not a Gladius project: no data/config folder under 'C:\src\repo'.
                  Pass --project <folder> with the project root or its _BEC folder.
```

A Claude Desktop entry looks like this:

```json
{
  "mcpServers": {
    "gladius": {
      "command": "C:\\Program Files\\Gladius Modding Tools\\ModdingGUI.Mcp.exe",
      "args": ["--project", "C:\\mods\\MyMod"]
    }
  }
}
```

A Release publish puts it there: `dotnet publish ModdingGUI.sln -c Release` writes both `ModdingGUI.exe` and
`ModdingGUI.Mcp.exe` into `bin/Release/net8.0-windows/win-x64/publish/` (the MCP project publishes into the
shell's folder on purpose, so a release zip carries both exes in one folder.
The generated catalogs (`skills/`, `classdefs/`, `encounters/`, `tables/`, `dol/`) are copied beside the MCP exe as
well, so validation and `skills_catalog_describe_field` work even when it is installed on its own. Publishing with the
explicit `FolderProfile` puts the shell under `bin/Release/net8.0-windows/publish/win-x64/` instead; pass
`-p:PublishDir=<that folder>` when publishing `ModdingGUI.Mcp.csproj` to keep them together.

**One gap found while verifying this**: the shell's publish settings copy `tools/` (and the catalogs) to the *build*
output but not to the *publish* output, so a plain `dotnet publish` leaves the toolbelt behind and `project_pack` from a
published install fails with "toolbelt folder not found". Copy `tools/` into the publish folder or pass
`--tools <repo>\tools`. That is a pre-existing shell packaging issue, recorded in `OPEN_ITEMS.md`; the MCP project does
ship its own catalogs, so validation and `skills_catalog_describe_field` work either way.

## The transport contract

**stdout belongs to the transport.** Everything on stdout is parsed as JSON-RPC; one stray line breaks the session.
So:

- the SDK writes the protocol frames and nothing else;
- the host's console logger is redirected to stderr (`LogToStandardErrorThreshold = Trace`);
- `IAppLog` lines are mirrored to stderr by the server, so a terminal client shows the same record the GUI does;
- the server never prints usage or banners to stdout, not even for `--help`.

`scripts/mcp-smoke.ps1` exists to catch a violation of that rule: it asserts every stdout line is a JSON-RPC frame.

**Responses may arrive in any order.** Legal JSON-RPC, and the SDK does reorder (a `tools/list` result can precede the
`initialize` result). Match by `id`, never by position — the smoke script does, and so should any client you write.

## Trust model

- **stdio only, no network, no listener.** The process is started by the client and talks over its own pipes.
- **No authentication.** Authority is OS process trust: whoever can spawn the exe gets the project it names. That is
  the same authority needed to edit the files by hand, so MCP adds no new trust boundary.
- **No project switching.** The project is resolved once; there is no tool that repoints it, and no `projectPath`
  argument on any tool. A client that wants another project starts another server.
- **Everything is undoable.** Every write goes through an `IXxxApi`, which takes the project lock and copies the file
  into `<project>/.mod-history/` first. `history_*` lists and restores backups, including the binary grids.
- **Nothing outside the project is written.** Tools only touch the project folder (and the `.mod-history` inside it),
  the app-log sink, and — for `project_pack`/`project_unpack` — the project's own folder and parent.
- Settings are **read-only** over MCP and no tool exposes them; `ModdingGUI.Tests/Architecture/McpBoundaryTests.cs`
  is the guard.

## The project is fixed per process

Tools never take a project path. That is deliberate: it matches the GUI (one selected project, every button acts on
it), it makes `strict` unambiguous, and it means a relative path in a tool argument can only mean one thing. Call
`project_info` first to see what the server is bound to.

---

## Strict mode

Every tool that edits project data takes `strict` (default `false`). The two that do not are the jobs (`project_pack`, `project_unpack`: their outcome is the `BuildResult`) and `logs_clear` (it clears the in-memory log, not a file).

| | `strict: false` (default) | `strict: true` |
|---|---|---|
| Behaviour | validate, log the findings, write anyway | validate, write, then **revert** if the edit introduced a warning |
| Matches | the GUI (`WarnInLog`) | the MCP-only `RejectOnError` |
| Result | `{ result, warnings[], strict }` | either the same, or an **error result** naming the findings |

Semantics, precisely:

1. The affected scope is validated **before** the write (one record, one file, one set — whatever the family's own
   `Validate` covers). That snapshot is the baseline.
2. The write runs.
3. The same scope is validated again. Findings that were not in the baseline are *the edit's* findings.
4. `strict: false` → findings are logged (`IAppLog` + stderr) and returned in `warnings[]`; the file keeps the edit.
5. `strict: true` and any new finding is a **warning** → the backup the write just made is restored, so the file is
   byte-identical to before the call, and the tool answers with an error result listing the findings.

Why a delta: vanilla content has legitimate oddities (leagues with no bouts, unknown header keys in shipped encounter
files, …). Refusing on any finding at all would make strict mode permanently unusable on real projects, so strict
refuses only what *this edit* broke. Info findings are reported and never refuse.

Consequences worth knowing:

- A rejected strict write is visible in `.mod-history`: the attempt is backed up, then the pre-call bytes are restored
  (and that restore is backed up too, as every restore is). The file content is unchanged; the audit trail shows the
  attempt.
- Repeating a write that is *already* invalid changes nothing, so it introduces no new warning and strict accepts it.
  That is the delta at work, not a hole: the file was already in that state before the call.
- Families with no validator accept the flag and say so in the tool description: **items**, **lookup**, **config**
  (and the `_add_set`/`_remove_set` operations whose scope does not exist yet or any more). `items.tok` values are
  type-checked by the tok layer as they are written — a malformed value is an error there, regardless of `strict`.
- `skills_add_validated` / `skills_update_validated` use the API's own `SkillValidationMode` instead: `strict: false`
  is `Warn`, `strict: true` is `Reject`. That is the same mechanism, implemented in Core before MCP existed.

## Return shapes

Read tools return the Core DTO as-is, so field names match property names (camel-cased by the SDK). Writes return:

```json
{ "result": <the API return value, or null for a void write>, "warnings": [ … ], "strict": false }
```

A finding is uniform across families, mapped from each validator's own issue record:

```json
{ "severity": "warning", "code": "stat-out-of-range", "target": "0", "field": null, "message": "level 1 has an implausible stat value (9999); expected 0-255" }
```

Five placeholders that are not the raw Core type, each because System.Text.Json cannot represent it *or* because a
client reads it better flattened. All are documented in the tool descriptions too:

| Core type | Wire shape | Why |
|---|---|---|
| `ItemDto.Affinity` — `(string Element, long Amount)?` | `{"element": "Fire", "amount": 100}` | a `ValueTuple`'s members are fields, so STJ round-trips it as `{}`: the affinity would silently vanish |
| `ItemDto.StatMods`, `ItemPatch.StatModsAdd/Remove` — `List<(string Stat, long Value)>` | `[{"stat": "PWR", "value": 2}]` | same |
| `EncounterUnit` | raw `UNITDB` row text | the row is 21 positional slots the engine reads; the text is what the file stores, so it round-trips exactly. `encounters_get` returns both raw and parsed forms, and `encounters_fields` documents the slots |
| `GridFile` | `{ layoutVersion, isWritable, slots[], cells[32][32] }` | the model carries `System.Drawing.Color` and a private cell array; a client wants the numbers. `grids_set_tag` resolves the per-file bit number so a client never has to |
| `IEncountersApi.ReadLines` — `(lines, trailing)` | `{ "lines": […], "trailing": 0 }` | same tuple problem |
| `(string RelativePath, int Count, DateTime Newest)` from `IHistoryApi` | `{"relativePath", "count", "newest"}` | same |
| `BuildResult` / `BuildStepResult` | `{ kind, jobId, target, state, succeeded, exitCode, seconds, result, steps[{name, exitCode, succeeded, seconds, output[]}] }` | `TimeSpan` serialises as `"00:00:01.234"`; seconds and a job id are what a caller acts on |

## Logs, jobs, progress and cancellation

- `logs_tail(count, level?)` and `logs_clear` read the `IAppLog` ring buffer — the same stream the GUI renders, and the
  same file sink. Every write appends one `Success` line (`items_patch 'Iron Sword': written`) and one line per finding,
  so `logs_tail` after a call is the audit trail of what the Core APIs said.
- `project_pack` and `project_unpack` run through `ModdingGUI.Core.Jobs.JobRegistry`, the same object the GUI and the UI
  driver use. The tool awaits the job's `Completion` and answers with its `BuildResult`, so no client needs a timeout.
- Progress: one notification when the job starts, one per log line (capped at 200, with a note saying so), and one when
  it finishes with the state, exit code and duration. Only sent when the request carries a progress token.
- Cancellation: `notifications/cancelled` cancels the request's token, which is registered straight to
  `BuildJob.Cancel()`. The tool runner kills the child process tree, the job settles as `Cancelled`, and the tool still
  answers with the report.
- The job result is data, not text: `state`, `exitCode`, every step with its exit code, output lines and duration.

---

## Tool catalogue

Generated from a live `tools/list` (149 tools). One class per family under `ModdingGUI.Mcp/Tools/`. The descriptions
below are trimmed to their first sentence; the full text — what a model actually reads — is in the `[Description]`
attributes, and every parameter carries its own description.

### classes (10)

| Tool | What it does |
|---|---|
| `classes_list` | List every class name in data/config/classdefs.tok, in file order. |
| `classes_get_all` | Read every class block in one pass. |
| `classes_get` | Read one class block by name. |
| `classes_set_field` | Replace a class field's value, keeping the line's layout. |
| `classes_add_field` | Append a key line at the end of a class block, for repeatable keys. |
| `classes_remove_field` | Remove every line for a key from a class block. |
| `classes_rename` | Rename a class by rewriting its CREATECLASS header. |
| `classes_add` | Add a new class block at the end of classdefs.tok. |
| `classes_remove` | Remove a class block, including the blank separator lines after it. |
| `classes_validate` | Validate one class block against the generated class catalog. |

### config (6)

| Tool | What it does |
|---|---|
| `config_list_files` | List files under a project-relative directory. |
| `config_get` | Read one flat tok table as its entries, in file order. |
| `config_set` | Replace the first value of a key. |
| `config_add` | Append a new line for a key (repeating keys such as a shop's ITEM lines). |
| `config_remove` | Remove every line for a key. |
| `config_set_values` | Replace all lines for a key with the supplied values, in order. |

### encounters (18)

| Tool | What it does |
|---|---|
| `encounters_list_files` | List the project-relative paths of every .enc file. |
| `encounters_list` | List every encounter's ENCOUNTERNAME. |
| `encounters_get` | Read one encounter: header fields, TEAM sections, raw UNITDB rows and their parsed form. |
| `encounters_set_field` | Replace a header field's value. |
| `encounters_add_field` | Append a header field. |
| `encounters_remove_field` | Remove every header line for a key. |
| `encounters_set_team_field` | Replace a TEAM section's field value. |
| `encounters_add_team_field` | Add a field line to a TEAM block. |
| `encounters_remove_team_field` | Remove every line for a key from a TEAM section. |
| `encounters_set_team_units` | Replace all UNITDB rows of a team. |
| `encounters_set_team_unit` | Replace one UNITDB row of a team. |
| `encounters_add_team_unit` | Append a UNITDB row to a team. |
| `encounters_remove_team_unit` | Remove one UNITDB row from a team. |
| `encounters_replace_with` | Overwrite one encounter file with another's content. |
| `encounters_fields` | Describe the 21 UNITDB slots (kinds, bounds, choices, observed values). |
| `encounters_read_lines` | Read an encounter as raw lines with its trailing byte count. |
| `encounters_write_lines` | Replace an encounter with the given lines. |
| `encounters_validate` | Validate one .enc file without touching disk. |

### gladiators (9)

| Tool | What it does |
|---|---|
| `gladiators_list` | List every gladiator name (2,170 in vanilla). |
| `gladiators_get` | Read one gladiator. |
| `gladiators_get_all` | Read every gladiator in one pass. |
| `gladiators_patch` | Change any subset of the eight mutable values. |
| `gladiators_apply_many` | Patch many records in one read/write/backup. |
| `gladiators_rename` | Rename a gladiator, refusing a taken name. |
| `gladiators_add` | Append a record. |
| `gladiators_remove` | Remove a record. |
| `gladiators_validate` | Validate one record or the whole table. |

### grids (6)

| Tool | What it does |
|---|---|
| `grids_list` | List every .grd in the project. |
| `grids_get` | Read one grid: layout version, tag slots and the 32×32 cell values. |
| `grids_describe` | Summarise one grid without its cells. |
| `grids_save` | Write a grid's 32×32 cell values back. |
| `grids_set_tag` | Set or clear one named tag on one cell (bit number resolved from the file). |
| `grids_set_value` | Set one cell to a raw bitmask. |

### history (5)

| Tool | What it does |
|---|---|
| `history_list_all` | List every backup in the project, newest first. |
| `history_list_files` | List the files that have backups, with counts. |
| `history_list_for` | List one file's backups. |
| `history_restore` | Restore a backup by path. |
| `history_restore_entry` | Restore a backup by its full entry. |

### items (7)

| Tool | What it does |
|---|---|
| `items_list` | List every item id. |
| `items_get_all` | Read every item in one pass. |
| `items_get` | Read one item by stable id. |
| `items_patch` | Set selected fields of one item (add/remove verb pairs for lists). |
| `items_add` | Append a new item record. |
| `items_remove` | Delete the item with this id. |
| `items_rename` | Change an item's stable id. |

### itemsets (10)

| Tool | What it does |
|---|---|
| `itemsets_list` | List every item-sets index. |
| `itemsets_get` | Read one item set. |
| `itemsets_get_all` | Read every item set in one pass (404 sets). |
| `itemsets_set_row` | Replace one row by position. |
| `itemsets_apply_many` | Replace many rows in one pass. |
| `itemsets_add_row` | Add a row, inserted in order. |
| `itemsets_remove_row` | Remove a row by position. |
| `itemsets_add_set` | Append a new, empty set. |
| `itemsets_remove_set` | Remove a whole set. |
| `itemsets_validate` | Validate one set or all of them. |

### leagues (11)

| Tool | What it does |
|---|---|
| `leagues_list_files` | List the project-relative paths of every league file. |
| `leagues_list` | List the LEAGUE names in one file. |
| `leagues_get` | Read one league file: header, leagues and their bouts. |
| `leagues_set_header_field` | Replace a file-level field. |
| `leagues_remove_recruits` | Remove every RECRUIT line from every league file. |
| `leagues_set_league_field` | Replace a league-level field. |
| `leagues_set_encounter_field` | Replace a field of one bout. |
| `leagues_set_encounter_file` | Point a bout at a different .enc — the in-game harness in one call. |
| `leagues_add_encounter` | Append a bout to a league. |
| `leagues_remove_encounter` | Remove a bout. |
| `leagues_validate` | Validate one league file. |

### logs (2)

| Tool | What it does |
|---|---|
| `logs_tail` | Read the tail of the application log, oldest first. |
| `logs_clear` | Drop the in-memory log buffer, returning how many lines went. |

### lookup (7)

| Tool | What it does |
|---|---|
| `lookup_load` | Read the English lookup-text table as `{id, text}` entries. |
| `lookup_load_language` | Read one language's table (eng/fra/ger/ita/spa). |
| `lookup_update` | Replace one English text id. |
| `lookup_update_language` | Replace one id in one language. |
| `lookup_add` | Append an English entry, returning its allocated id. |
| `lookup_add_language` | Append an entry to one language, returning its id. |
| `lookup_available_languages` | List the language tables the project has. |

### project (4)

| Tool | What it does |
|---|---|
| `project_info` | Report the bound project, app directory, toolbelt, toolchain and log sink. |
| `project_validate` | Run every pack-relevant validator and return the findings together. |
| `project_pack` | Pack the project into an ISO as a Core job; returns its BuildResult. |
| `project_unpack` | Unpack an ISO into a new project folder as a Core job. |

### schoolnames (6)

| Tool | What it does |
|---|---|
| `schoolnames_list` | List the school-name table (index, name). |
| `schoolnames_get` | Read the name at an index. |
| `schoolnames_set_name` | Rename an entry in place. |
| `schoolnames_add` | Append a name, returning its index. |
| `schoolnames_remove_last` | Remove the highest-numbered entry. |
| `schoolnames_validate` | Validate the table. |

### schools (14)

| Tool | What it does |
|---|---|
| `schools_list_files` | List the project-relative paths of every roster. |
| `schools_list` | List every roster's NAME. |
| `schools_get` | Read one roster: header and its CREATEUNIT blocks. |
| `schools_set_header_field` | Replace a header field (NAME/HERO/GOLD). |
| `schools_set_unit_class` | Replace a unit's class on its CREATEUNIT line. |
| `schools_set_unit_field` | Replace a single-valued field of a unit block. |
| `schools_add_unit_field` | Append a field to a unit block. |
| `schools_remove_unit_field` | Remove every line for a key from a unit block. |
| `schools_set_unit_skills` | Replace a unit's whole granted-skill list. |
| `schools_add_unit_skill` | Grant one skill. |
| `schools_remove_unit` | Delete a unit block. |
| `schools_add_units` | Append new unit blocks in one write. |
| `schools_replace_roster` | Replace a whole roster. |
| `schools_validate` | Validate one roster. |

### skills (10)

| Tool | What it does |
|---|---|
| `skills_list` | List every skill name (1,811 in vanilla). |
| `skills_get_all` | Read every skill in one pass. |
| `skills_get` | Read one skill by name. |
| `skills_update` | Replace a skill's body, validating first when strict. |
| `skills_update_validated` | Replace a skill through `SkillValidationMode` (Warn/Reject). |
| `skills_add` | Append a skill, validating first when strict. |
| `skills_add_validated` | Append a skill through `SkillValidationMode` (Warn/Reject). |
| `skills_remove` | Delete a skill. |
| `skills_validate` | Validate a record without writing it. |
| `skills_catalog_describe_field` | Describe one catalog field: slots, enum values, bounds, links, requirements. |

### skillsets (8)

| Tool | What it does |
|---|---|
| `skillsets_list` | List every skill-sets index. |
| `skillsets_get` | Read one skill set. |
| `skillsets_set_row` | Replace one row by position. |
| `skillsets_add_row` | Add a row, inserted in order. |
| `skillsets_remove_row` | Remove a row by position. |
| `skillsets_add_set` | Append a new, empty set. |
| `skillsets_remove_set` | Remove a whole set. |
| `skillsets_validate` | Validate one set or all of them. |

### statsets (10)

| Tool | What it does |
|---|---|
| `statsets_list` | List every stat-sets index. |
| `statsets_get` | Read one stat set. |
| `statsets_get_all` | Read every stat set in one pass (169 sets). |
| `statsets_set_row` | Replace one level row. |
| `statsets_apply_many` | Replace many rows in one pass. |
| `statsets_add_row` | Append a level row. |
| `statsets_remove_row` | Remove a level row. |
| `statsets_add_set` | Append a new, empty set. |
| `statsets_remove_set` | Remove a whole set. |
| `statsets_validate` | Validate one set or all of them. |

### tintsets (6)

| Tool | What it does |
|---|---|
| `tintsets_list` | List every tint-sets index. |
| `tintsets_get` | Read one tint set (channel → `{r,g,b}`). |
| `tintsets_set_color` | Set one channel's colour. |
| `tintsets_add_set` | Append a new, empty set. |
| `tintsets_remove_set` | Remove a set. |
| `tintsets_validate` | Validate one set or all of them. |

---

## Worked examples

The calls below are the JSON arguments and result payloads; the surrounding envelope is MCP's
(`{"jsonrpc":"2.0","id":N,"method":"tools/call","params":{"name":…,"arguments":{…}}}` and the result's
`content[0].text` holding the JSON shown). Results are shown formatted for reading.

### 1. Read an item, change its price, see the change and its backup

```jsonc
// items_list → ["Amber Anklet of Focus", …]
{"name": "items_list", "arguments": {}}

// items_get
{"name": "items_get", "arguments": {"name": "Amber Anklet of Focus"}}
→ {"name":"Amber Anklet of Focus","type":"Accessory","subType":"Anklet","style":"plain",
   "cost":1188,"minLevel":1,"rarity":"Rare","affinity":{"element":"Fire","amount":10},
   "regions":["Expanse"],"skills":[],"attributes":[],"hideSets":[],"statMods":[]}

// items_patch — only the fields named change
{"name": "items_patch",
 "arguments": {"name": "Amber Anklet of Focus", "patch": {"cost": 900, "skillsAdd": ["Fire Ward"]}}}
→ {"result": { … "cost": 900, "skills": ["Fire Ward"] … }, "warnings": [], "strict": false}
```

The file is written once, the previous bytes are in `.mod-history/data/config/items.tok/<stamp>.bak`, and `logs_tail`
shows `items_patch 'Amber Anklet of Focus': written`. `history_list_for` + `history_restore` undo it.

### 2. Build a valid skill, and see strict mode refuse an invalid one

```jsonc
// What is SKILLEFFECTRANGE? — the catalog answers without guessing
{"name": "skills_catalog_describe_field", "arguments": {"fieldName": "SKILLEFFECTRANGE"}}
→ {"field":"SKILLEFFECTRANGE","summary":"…","multi":false,"numericType":"int",
   "slots":[{"types":["int"],"values":[]}],"links":["SKILLEFFECTAREATYPE"],"note":"…"}

// Validate before writing: the same check strict mode runs
{"name": "skills_validate", "arguments": {"skill": {"name":"Flame Test","skillType":"Attack",
   "skillSubType":"Standard","fields":[{"fieldName":"SKILLEFFECTRANGE","rawValue":"not-a-number"}]}}}
→ [{"severity":"warning","code":"numeric-shape","field":"SKILLEFFECTRANGE", …}]

// strict: true refuses, and nothing is written
{"name": "skills_add", "arguments": {"skill": { … same … }, "strict": true}}
→ isError: "skills_add 'Flame Test': refused by strict mode — nothing was written. 1 warning(s): …"

// strict: false writes it and returns what it thinks (the GUI's behaviour)
{"name": "skills_add", "arguments": {"skill": { … same … }, "strict": false}}
→ {"result": {"name":"Flame Test", …}, "warnings": [{"severity":"warning","code":"numeric-shape", …}], "strict": false}
```

Note the difference from the generic gate: skills has a payload validator, so `strict` refuses *before* writing.

### 3. Repoint a league bout at a test encounter, then pack it

```jsonc
// 1. Which encounters exist, and which league file do I want?
{"name": "encounters_list_files", "arguments": {}}
{"name": "leagues_get", "arguments": {"path": "data/towns/leagues/altahrunruins_leagues.tok"}}
→ {"leagues":[{"name":"…","encounters":[{"name":"Battle1","fields":[{"key":"ENCFILE","rawValue":"…"}]}]}]}

// 2. Point the bout at the fight to test — one call, no hand-editing ENCFILE and TEAMS
{"name": "leagues_set_encounter_file",
 "arguments": {"path":"data/towns/leagues/altahrunruins_leagues.tok",
               "league":"…","encounter":"Battle1",
               "encounterFile":"data/encounters/expanse/expansedunes/expansedunesencounter01.enc",
               "strict": true}}
→ {"result": true, "warnings": [], "strict": true}

// 3. Would it pack? (never blocks, but says what it thinks)
{"name": "project_validate", "arguments": {}}
→ {"findings":[…],"total":3,"warnings":0,"errors":0,"byArea":{"gladiators":3}}

// 4. Pack it as a job — pass a progress token to follow it, cancel the request to stop it
{"name": "project_pack", "arguments": {}}
→ {"kind":"pack","jobId":"c0ffee123456","target":"C:\\mods\\MyMod","state":"Succeeded",
   "succeeded":true,"exitCode":0,"seconds":41.2,"result":"C:\\mods\\MyMod\\MyMod.iso",
   "steps":[{"name":"pack","exitCode":0,"succeeded":true,"seconds":38.9,"output":["…"]}]}
```

A story battle (`Scene04U`, `Scene06U`, …) ignores `ENCFILE`: the game-flow script starts it by encounter *name*, so
use `encounters_replace_with` for those.

---

## Testing

Three layers, each catching what the others cannot:

| Test | What it proves |
|---|---|
| `ModdingGUI.Tests/Mcp/McpServerTests.cs` (xUnit) | The real exe, spawned through `McpClient` + `StdioClientTransport`: handshake, `tools/list`, a read, a write that lands on disk with a `.mod-history` backup, strict mode in both directions (advisory write, then refusal with the file byte-identical), `logs_tail` showing the write, `project_unpack` running as a job with the `BuildResult` and progress notifications. |
| `EveryCoreApiMethodHasATool` (same file) | Reflects the 16 `I*Api` interfaces in Core and asserts each public method has a registered tool, by name (`IEncountersApi.SetTeamUnits` → `encounters_set_team_units`, overloads by suffix). This is what stops the server drifting behind the APIs — it is how `encounters_set_unit` was caught and renamed. |
| `scripts/mcp-smoke.ps1` (PowerShell) | The raw wire, with no client library: spawns the exe, writes JSON lines, reads frames. Asserts every stdout line is a frame (no pollution), matches responses by id (order-independent), checks `initialize`/`ping`/`tools/list`/`tools/call`/`notifications/cancelled`, and that the server exits 0 when stdin closes. Run it after any SDK bump. |

```
dotnet test ModdingGUI.sln                                     # everything, including the MCP integration tests
dotnet test ModdingGUI.Tests --filter "FullyQualifiedName~Mcp"  # just the MCP ones
.\scripts\mcp-smoke.ps1                                         # raw wire, staging its own project
.\scripts\mcp-smoke.ps1 -Exe .\ModdingGUI.Mcp\bin\Release\net8.0\ModdingGUI.Mcp.exe -KeepProject
```

The integration tests stage their own project from the read-only `clean-bec-unpacked/` corpus into a temp folder and
delete it afterwards, and they skip (like the round-trip gate) when the corpus is absent. Set `GLADIUS_MCP_EXE` to test
a specific build.

## Pinned versions

Exact versions, no floating ranges. A bump changes the wire protocol and the transitive `System.Text.Json` version, so
it is a deliberate change that must re-run `scripts/mcp-smoke.ps1`.

| Package | Version | Where |
|---|---|---|
| `ModelContextProtocol` | 2.4.0 | `ModdingGUI.Mcp` |
| `Microsoft.Extensions.Hosting` | 8.0.1 | `ModdingGUI.Mcp` (the SDK brings only the abstractions; `Host.CreateApplicationBuilder` needs this explicitly) |
| `ModelContextProtocol.Core` | 2.4.0 | `ModdingGUI.Tests` (client half only) |

Transitively resolved today: `System.Text.Json` 10.0.13 and `Microsoft.Extensions.Hosting.Abstractions` 10.0.14 inside
a `net8.0` exe. That combination builds and runs; `ModdingGUI.Mcp` stays a **leaf** (nothing references it, not even
the tests), so those versions cannot leak into the shell or its test run.

## Limits and deliberate omissions

- No `logs_subscribe` (streaming log lines as notifications): progress notifications cover the long jobs, and
  `logs_tail` covers the rest without a second delivery path to reason about.
- No resources or prompts: this server is a tool surface over a data model.
- `randomizer_*` and `shops_*` from PLAN §8 are not here. The randomizer is a pipeline with its own options object and
  the CLI already drives it (`--randomize`); shops are `config_*` over `data/towns/shops`. Adding them later is a tool
  class each, and the coverage test will demand them the moment a `ITownsApi`-style interface appears.
- The optional "Run as MCP server" checkbox in the shell's settings is separate: it relaunches the shell with
  `--mcp`, which spawns this exe against the selected project.
