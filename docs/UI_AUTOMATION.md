# Driving the GUI (for tests and for AI agents)

The app can be operated from a script or an agent over a loopback JSON line protocol. It is **off unless asked for**,
and it drives the real window: every command presses an actual control, so an automated run exercises the same code
path a user does and every edit still lands in `.mod-history` through the Core APIs.

This exists for two reasons. A GUI-only feature is otherwise untestable without a person clicking it, and an assistant
helping a user should be able to press the buttons instead of describing where they are.

## Starting it

```powershell
# From the folder holding ModdingGUI.exe (the Debug exe is what the user runs):
.\ModdingGUI.exe --ui-server            # listens on 127.0.0.1:4571
.\ModdingGUI.exe --ui-server 5000       # explicit port
.\ModdingGUI.exe --ui-script smoke.txt  # run a command file once the window is open
.\ModdingGUI.exe --ui-script smoke.txt --ui-exit   # ...then close
```

While it listens it writes `%TEMP%\gladius-ui-driver.json` with the port and pid, and prints
`[ui] listening on 127.0.0.1:<port>`. The listener is bound to loopback only and is stopped when the window closes.

## Talking to it

One JSON request per line, one JSON response per line. `scripts/ui-drive.ps1` wraps that:

```powershell
cd ModdingGUI
.\scripts\ui-drive.ps1 '{"cmd":"ping"}'
.\scripts\ui-drive.ps1 '{"cmd":"snapshot"}' | ConvertFrom-Json
.\scripts\ui-drive.ps1 -File .\scripts\team-builder-smoke.txt
```

`snapshot` is how you find a control's name; `items` is how you read a list past snapshot's 40-item cut.

## Commands

| Command | Fields | What it does |
|---|---|---|
| `ping` | — | Liveness, plus the selected tab |
| `snapshot` | — | Every named control (type, text, bounds, enabled/visible, list items, checked, tab pages), the selected tab, and the last 40 log lines |
| `items` | `name`, `offset?`, `count?` | Every item of a list or combo box with its index |
| `click` | `name` | Presses a button, toggles a checkbox, selects a radio button |
| `set` | `name`, `text?` / `checked?` / `index?` / `item?` | Sets text, a checkbox, a list or combo index, a combo item by substring, or a tab page of a `TabControl` |
| `selectTab` | `name` | Switches the main tab strip |
| `selectNode` | `name`, `text` | Selects a tree node by (partial) text |
| `clickMenuItem` | `name` | Presses a menu item, checking it first when it is a toggle |
| `screenshot` | `path?` | Renders the window to a PNG (`DrawToBitmap`); `%TEMP%` is expanded |
| `log` | `count?` | The app log tail, with level and source |
| `jobs` | — | Running and recently finished jobs: id, kind, target, state, elapsed, exit code |
| `waitForJob` | `id?` / `kind?`, `timeoutMs?` | Blocks until that job finishes (or the latest of a kind) and answers with its result |
| `cancelJob` | `id?` / `kind?` | Asks a running job to stop; the tool runner kills the child tree |
| `waitForLog` | `text`, `timeoutMs?` | Blocks until a log line contains `text` — for flows that have no job |
| `window` | `monitor?` / `x`,`y`,`width`,`height` | Move and size the window; `monitor` takes a device name like `\\.\DISPLAY2` |
| `capture` | `path?` | Screen grab of the window - the truth when `screenshot` shows a stale page |
| `closeDialog` | - | Close a blocking dialog and report what it said |
| `openTeamBuilder` | `project` | Selects the project (relative paths resolve against the app) and opens the Team Builder tab |
| `quit` | — | Closes the window |

`wait` is not a server command: it is handled by `ui-drive.ps1` (client-side sleep, so the app's UI thread stays free)
and by `--ui-script`.

Every response is `{"ok":true,"result":{…}}` or `{"ok":false,"error":"…"}`.

## Waiting for work to finish (don't poll, don't guess a timeout)

Unpack, pack and script compile are **jobs** (`ModdingGUI.Core.Jobs`): each has an id, a state, a `BuildResult` and a
completion task. A `click` that starts one returns its id:

```json
> {"cmd":"click","name":"btnUnpack"}
< {"ok":true,"result":{"clicked":"btnUnpack","job":{"id":"99133c796645","kind":"unpack","state":"Running"}}}

> {"cmd":"waitForJob","id":"99133c796645"}
< {"ok":true,"result":{"job":{"id":"99133c796645","kind":"unpack","state":"Succeeded","elapsedSeconds":21.5,
                             "exitCode":0,"steps":3},"succeeded":true,"firstFailure":null}}
```

The response arrives when the work ends, however long it takes — the client's default is to wait for a response, not
to impose a deadline (`-TimeoutMs` still exists for callers that want one). Before jobs existed the choices were
sleeping for a guessed duration and grepping the log for `Unpacking completed successfully.`; both are bets, and a
bet made in a test script is a flake waiting to happen.

Use `waitForLog` only for a flow with no job object (a UI-only action such as loading a roster, or asserting on what a
run printed).

## Behaviour under automation

- **Dialogs do not appear.** A modal would deadlock the driver, which owns the UI thread. `UiConfirm` logs the
  question and answers **yes**, so the transcript shows the decision:

  ```
  [automation] Job points: 'Agent Hero' now spends 58 JP on granted skills but is awarded 55. … → yes
  Success: Raised 'Agent Hero' JOBPOINTS to 58 so its granted skills stay legal.
  ```

  Legacy `MessageBox.Show` calls (settings, missing files) are untouched: avoid commands that trigger them, or they
  will hang the run.
- **Everything runs on the UI thread**, so `snapshot` and `screenshot` are consistent with what the user would see.
- **`screenshot` uses `DrawToBitmap`**, which needs no foreground window and works on a locked desktop. WebView2 and
  some themed controls may render imperfectly; the layout is reliable.

## Adding a command

Two files:

1. `frmMain_partials/UiAutomation.cs` — add a `case` to the `ExecuteUiCommand` switch and a method. Get controls by
   name through `FindControl`, and give anything you add to the UI a `Name` so it is addressable.
2. `scripts/ui-drive.ps1` needs no change: it forwards whatever line it is given.

Keep commands to *driving* the window. A command that reaches into Core directly would be a second code path into the
game data, which `PLAN.md` §0 forbids; press the button instead.

## Relationship to the MCP server (Phase 8)

This is not the MCP server. It is the same idea applied to the shell: a small, opt-in, loopback-only surface used to
test the GUI and to let an agent operate it for a user. Phase 8 exposes the Core APIs over stdio for clients such as
Claude Desktop; the driver exposes the *window*. The two are complementary and share nothing but `IAppLog`.

What they will share is `JobRegistry`: an MCP `project_pack` tool can run a job and forward its `Started`/`Finished`
events as `notifications/progress`, answer with the same `BuildResult`, and honour `notifications/cancelled` through
`BuildJob.Cancel`. That is why the jobs live in Core rather than in the driver — and why an MCP client will not need a
timeout either, just a progress token.

## Visual and contrast auditing

Three commands exist so UI review is measured rather than guessed:

| Command | What it returns |
|---|---|
| `{"cmd":"audit","contrast":4.5}` | Every visible control whose text/background contrast is below the threshold, plus any control still painted with a *system* colour while the dark palette is active. Reports `scheme`, `threshold`, `count`, and a `findings` array of `{name, type, text, fore, back, contrast, issue}`. |
| `{"cmd":"auditAll","contrast":4.5}` | Runs the audit on every tab page in turn, grouped by page. |
| `{"cmd":"hit","x":N,"y":N}` | The deepest control at a point, with its type, name, colours and bounds, plus the parent chain. Use it to identify what a patch of colour actually belongs to. |

`audit` at 4.5:1 is the release bar; the app currently clears AAA (~7.4:1) for muted text. Two gotchas found by using
it: a docked `Panel` has no automatic height, and disabled `TextBox`/`Button` controls are painted by Windows in system
colours whatever the palette says - prefer `ReadOnly` for fields that should stay on theme.

## Screenshotting a modal dialog

Modal dialogs run their own message loop, so they block this driver: a `clickMenuItem` that opens one never returns.
Dialogs get screenshots from a separate process instead:

```powershell
ModdingGUI.exe --render-settings shots\settings.png
```

That draws the Settings dialog off-screen and exits. The same switch pattern is the way to capture any other dialog.

## Grid Builder (agent-driveable grid editing)

The Grid Builder is a `UserControl` that paints a 32x32 arena itself, so the generic commands (click a button, set a
text box) cannot express "put Start1 on cell 12,7". One command with an `action` covers it:

| Command | Returns |
|---|---|
| `{"cmd":"grid","action":"state"}` | project, arena, all arenas, layout version, writable, dirty, size, painted cell count, draw mask, every slot (tag + active + is-draw-slot), the paint slot list, and whether a map image is loaded |
| `{"cmd":"grid","action":"cell","row":12,"col":7}` | the cell's raw `value` and the `tags` whose bits are set |
| `{"cmd":"grid","action":"setTag","row":12,"col":7,"tag":"Start1"}` | sets the bit; `"on":false` clears it (`on` defaults to true) |
| `{"cmd":"grid","action":"clear","row":12,"col":7}` | value 0, reports `before` |
| `{"cmd":"grid","action":"slot","tag":"Start1"}` | chooses which tag mouse painting applies and reports the mask |
| `{"cmd":"grid","action":"arena","name":"expanse\\expansedunes\\dunesstartdefault.grd"}` | loads that grid from the selected project |
| `{"cmd":"grid","action":"save"}` | writes through `GridsApi` (project lock + `.mod-history` backup) |
| `{"cmd":"grid","action":"map","visible":true,"x":0,"y":0,"scale":2.5,"rotate":0}
{"cmd":"grid","action":"placement"}                      current + saved placement for the open arena
{"cmd":"grid","action":"placement","remember":true}    save the current transform for this arena
{"cmd":"grid","action":"placement","remember":false}   forget it` | shows/hides the arena map and sets its transform |

Three behaviours matter when scripting against it:

1. **Tags are resolved against the loaded file, not a fixed table.** In one arena `Start1` is bit 1, in a new grid it is
   bit 0: the same tag has different bit numbers per file. `GridFile.IndexOfTag` does that lookup, ignores inactive
   slots, and is covered by four tests in `GridFileTests`.
2. **Failures say what to try instead.** An unknown tag returns `availableTags`, a missing map image says so, and a
   missing project says to select one - a script can recover without guessing.
3. **`save` is a real save**: it goes through `GridsApi`, so the project lock and the timestamped backup apply exactly as
   they do for a click on Save.

An agent session looks like: select the project in `tvwProjects`, `{"cmd":"grid","action":"state"}` to see arenas and
slots, `arena` to load one, `setTag` per cell, `cell` to verify, `save`. That is the whole loop with no mouse.

### `convert` - the Convert page

Drives the Convert tab: turn an RVZ/WIA/GCZ/CISO image into a plain ISO (or compress an ISO back into a Dolphin
container), and rebuild NKit images. No file dialogs, so it works while the app is running headless.

| Action | Fields | Returns |
|---|---|---|
| `state` | - | the page's source/detected/destination/target, `availableTargets`, `hasDolphinTool`, `hasNkitTool`, `lastOutput` |
| `load` | `source`, optional `target` | fills the page exactly as Browse would (source, detected label, destination) so a screenshot matches what a user sees |
| `detect` | `source` | `format` + a description; no page change |
| `run` | `source`, optional `destination`, optional `target` (`iso`/`rvz`/`wia`/`gcz`) | `output` and `message`; runs the conversion and waits for it |

```json
{"cmd":"convert","action":"state"}
{"cmd":"convert","action":"detect","source":"C:\\roms\\Gladius (USA).rvz"}
{"cmd":"convert","action":"load","source":"C:\\roms\\Gladius (USA).rvz","target":"iso"}
{"cmd":"convert","action":"run","source":"C:\\roms\\Gladius (USA).rvz","destination":"C:\\roms\\Gladius (USA)-from-rvz.iso"}
```

Notes that matter when scripting it:

- `run` blocks for as long as the conversion takes (a Gladius RVZ takes ~2 s, an NKit rebuild ~8 s, ISO->RVZ ~2 s) and it
  invokes the Core converter on a background thread. It does **not** go through the page's async handler, because that
  handler touches controls in its continuations and blocking on it from the driver would deadlock the UI thread.
- The destination must not already exist: `run` refuses rather than silently overwriting (`dolphin-tool` overwrites
  without asking, which is exactly why the check is there).
- Without a `destination` it derives one beside the source: same stem plus the target extension, and `-converted` when
  that would collide with the source file, so `rvz -> rvz` does not target the input.
- `target` other than `iso` needs Dolphin's CLI; `availableTargets` in `state` reports what is actually possible before
  you try.

### `encounters` - encounters before they have a page

A `.enc` file is a header plus `TEAM:` blocks of `UNITDB:` rows, each row 21 positional slots. The command speaks
file/team/unit indices and raw rows, and validates rows with `EncounterUnit.TryParse` before writing.

| Action | Fields | Returns |
|---|---|---|
| `list` | optional `filter`, optional `project` | `total`, `count`, `files` (relative paths) |
| `get` | `path` | header `fields`, `teams` with their fields and `units` (raw + parsed name/chance/startSlot/class/subType) |
| `setHeader` | `path`, `key`, `value` | `changed: true` |
| `setTeamField` | `path`, `team`, `key`, `value` | `changed: true` (the field must already exist) |
| `addTeamField` | `path`, `team`, `key`, `value` | adds the field when the team has none, else sets it |
| `removeTeamField` | `path`, `team`, `key` | removes a team field |
| `setUnits` | `path`, `team`, `units` (array of raw rows) | `count`; every row is validated first, so a bad row writes nothing |
| `setUnit` | `path`, `team`, `index`, `raw` | `name` |
| `fields` | `path` | all 21 unit fields (kind, bounds, choices, observed values) + dropdown contents: `enemySchools` (146), `classTags` (128), `startMarkers` (from the arena grid), `gladiators` (2,170, grouped by school), and each team's `school`/`unitsInSchool`/`unitNames` |
| `setUnitFields` | `path`, `team`, `index`, `values` {key: value} | applies named values through `EncounterUnitEdit` (validated; a rejected value writes nothing) and returns the new raw row |
| `validate` | `path` | `teams`, `units`, `problems` (`row-does-not-parse`) |

`project` defaults to the project selected in the tree, so a scenario rarely needs to pass it.

## Toolchain (bundled exe vs Python scripts)

Every build runs through `ModdingGUI.Core.Tools.Toolchain`:

- **Bundled (default when present):** `tools/v009/tools_v009.exe`, subcommands `bec/idx/tok/toknum/strings/iso/maps`. The\n  `maps png <texture dir> <out dir>` form replaces the Python `pttp_tga.py` for the Grid Builder map overlay. Carries its
  own Python, so nothing has to be installed.
- **Scripts (fallback):** the v007 `tools/*.py` chain, needs `python` on PATH.

`Toolchain.Choose(applicationDirectory, scriptsFolder)` picks, `RequiresPython` drives the startup gate, and
`scripts/validate-toolchain.ps1` proves both produce a byte-identical ISO from a vanilla unpack. When the source text of the
derived binaries is untouched, `PackService` skips `toknum`/`tok compress`/`strings` (`DerivedToolState`) so an unedited
project repacks byte-identically.