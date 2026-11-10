# Handoff — Gladius Modding Tools GUI

Written 2026-11-09 at commit `f6b8060` (branch `rewrite/full-app`). Read this first, then `PLAN.md` (the locked
roadmap), then `docs/AGENTS.md` (subsystem map) and `CLAUDE.md` in the repo root (build/test detail).

Everything below was verified against the working tree, not from memory.

---

## 1. What the app is

A Windows .NET 8 WinForms tool for modding *Gladius* (GameCube/PS2). It unpacks an ISO into a project folder, edits
game data (`.tok`, `.txt`, `.grd`, `.enc`), and packs a playable ISO back. It is mid-rewrite into three projects so
the same edits can be driven by a GUI, a CLI, and (next) an MCP server.

```
E:\code_stuff\GladiusModdingToolsGUI\        workspace (not a git repo)
├── clean-bec-unpacked/                      vanilla corpus — READ ONLY, never a write target
├── tools/                                   Python toolbelt + DOGCodeCompiler + xdelta3
├── qa-pack/qa-pack.iso                      last QA ISO built
└── ModdingGUI/                              ← the git repo and the solution
    ├── ModdingGUI.sln
    ├── ModdingGUI.csproj                    WinForms shell (net8.0-windows)
    ├── ModdingGUI.Core/                     net8.0, no UI: Tok, Schemas, APIs, Randomizer, History, Tools, Logging
    ├── ModdingGUI.Tests/                    xUnit (net8.0-windows), 804 tests
    └── ModdingGUI.Mcp/                      Phase 8 stub: builds, Program.cs is inert (no stdio server yet)
```

**State of the tree:** clean, `dotnet build` reports **0 warnings** in Debug and Release, suite is
**804 passed / 0 failed / 0 skipped**.

---

## 2. Rules that are not negotiable

From `PLAN.md` §0 and `docs/AGENTS.md` — violating any of these has already caused rework once:

1. **No GUI bypass.** Every persistent edit goes through a Core API. The shell never writes a game file. (The only
   remaining shell writes are `settings.json`, the project-settings file, the menu file, a downloaded patch and the
   opt-in command-chain dump — none is game data.)
2. **Backups always.** `ChangeHistoryService.Backup` runs before every write →
   `<project>/.mod-history/<relative-path>/<yyyyMMdd-HHmmss-fff>.bak`.
3. **Locks always.** `ProjectLock.Acquire(projectPath)` around read-modify-write.
4. **Byte-identical no-op round-trip.** `TokRoundTripTests` parses and re-serialises every `.tok`/`.enc` in the corpus
   (977 files) and fails on a single byte of drift. Never break it. tok I/O is **ISO-8859-1**, not UTF-8.
5. **Validation is advisory in the GUI** (`WarnInLog`), opt-in strict for MCP. A save is never blocked on a warning.
6. **`System.Text.Json` only.** No new dependencies without approval.
7. **Every API accepts the project root or its `<name>_BEC` folder**, and either separator. `ProjectRoots.ResolveBecFolder`
   is the single resolver — route new APIs through it or you reintroduce the `classdefs.tok not found` class of bug.
8. **Zero warnings.** Core has `Nullable=enable` with CS86xx promoted to errors; the shell is annotations-only. If a
   warning appears, fix it.
9. **One phase at a time**, and no phase closes without the suite green.
10. **No shell for the toolchain.** Pack, unpack, script compile and xdelta are started with
    `ProcessStartInfo.ArgumentList` through `ModdingGUI.Core.Tools.ToolRunner`. No `cmd.exe`, no generated `.bat`
    on any run path. If a new external tool appears, write a `ToolStep` — do not reach for a batch file, and do not
    pass `PathHelper.QuotePath` output to a directly-started process (a caret is literal there).
11. **A GUI change is verified in the GUI.** Start the app with `--ui-server`, drive it with
    `scripts/ui-drive.ps1`, and look at the screenshot before calling panel work done (`docs/UI_AUTOMATION.md`).
    The driver clicks real controls, so it can never be used to bypass an API.

Evidence discipline: `dol/` + `docs/DOL_FINDINGS.md` hold facts extracted from the game executable and its data. They
are the authority. Do not re-derive a limit from a hunch when a measured number is recorded there.

---

## 3. Build, test, run

All commands from `ModdingGUI/`:

```powershell
dotnet build ModdingGUI.sln -c Release          # 0 warnings expected
dotnet build ModdingGUI.sln -c Debug            # the app the user runs lives in Debug
.\run-tests.ps1                                 # full suite, TRX + summary
.\run-tests.ps1 -Filter RealGameDataTests       # real-data subset (needs setup-realdata.ps1)
dotnet test ModdingGUI.Tests\ModdingGUI.Tests.csproj -c Release --no-build --filter "ProcessContextTests"
```

The app: `ModdingGUI\bin\Debug\net8.0-windows\win-x64\ModdingGUI.exe`. It scans **its own folder** for
`<name>/<name>_BEC` project pairs, so test projects live next to the exe (`gold_test`, `REALDATA_vanilla`, …) or in
the folder configured by *Options → Settings → Projects Folder*.

CLI (same assembly, no window): `--unpack`, `--pack`, `--randomize` with `--seed/--heroes/--enemies/--team/--cash/…`,
plus `--python`, `--tools`, `--help`, `--version`. See `Helpers/Cli.cs`. It calls the same Core services the GUI
calls, so its log is the GUI's log.

---

## 4. What is done

### Core (the source of truth)

| Area | State |
|---|---|
| Tok framework | done — byte-identical round-trip over the whole corpus |
| `IItemsApi`, `ISkillsApi` (freeform + catalog validation) | done, GUI-wired |
| `ILookupTextApi` | done, GUI-wired (5 languages, ISO-8859-1) |
| `IClassesApi` | done, **headless + Team Builder** |
| `IEncountersApi`, `ILeaguesApi`, `IConfigTableApi` | done, **headless only** |
| `ISchoolsApi` (rosters) | done, GUI-wired through the Team Builder |
| Phase 5 units family: `IGladiatorsApi`, `IStatSetsApi`, `ISkillSetsApi`, `IItemSetsApi`, `ITintSetsApi`, `ISchoolNamesApi` | APIs done; Team Builder reads gladiators/stat sets; in-game QA pending |
| `IGridsApi` | done, Grid Builder saves through it |
| `IHistoryApi` + `ChangeHistoryService` | done — list backups/backed-up files, restore (backs up the live file first) |
| `Logging` (`IAppLog`, `AppLog`) | done — ring buffer, `Written` event, file sink |
| `Jobs` (`BuildJob`, `JobRegistry`) | done — unpack/pack/compile are jobs with a state, a `BuildResult` and a completion task; `Started`/`Finished` events, cancellation, bounded history. The GUI and the driver wait on them; MCP will forward them as progress |
| Build layer (`ToolStep`, `ToolRunner`, `PackService`, `UnpackService`, `ProjectBuildService`) | done (7.8 + 7.9) — no shell, no batch file |
| `Randomizer` pipeline + `ProjectBuildService` | done (Phase 6), GUI-wired, CLI-wired |
| `Units`: `ItemSetGear`, `ItemCategoryRules`, `JobPoints`, `LearnableSkills`, `InputRules` | done — engine rules as code |
| `Tools`: `ProjectFileOperations` (measure/copy/delete with progress), `BatchContentBuilder` | done |

### Shell

| Feature | State |
|---|---|
| Patch apply, unpack, pack | **done and Core-owned**: `BuildRunner.cs` calls `PackService`/`UnpackService`, mirrors the log into the panel, and reports a real failure instead of unconditional success |
| Randomizer panel | parameter builder over `RandomizerOptions`; progress + log mirroring through `AppLog` |
| Team Builder | Phase 7 done (incl. the UX pass): reads through `IClassesApi`/`IGladiatorsApi`/`IStatSetsApi`/`IItemsApi`/`IItemSetsApi`/`ISkillsApi`, exports through `ISchoolsApi`; searchable gear list, five labelled slots, item details, JP offer, level-aware preview |
| UI driver | `Helpers/UiDriver.cs` + `frmMain_partials/UiAutomation.cs`: opt-in loopback JSON control of the window (snapshot/click/set/screenshot), used to verify the GUI and to let an agent operate it — see `docs/UI_AUTOMATION.md` |
| Item Editor | on `IItemsApi`, completeness-gated, stale-load guards |
| Skill Editor | catalog-driven, advisory validation |
| Grid Builder | on `IGridsApi` |
| History browser (`Forms/HistoryForm`) | browse `.mod-history`, restore any backup (Phase 7.5) |
| Project tree | delete/copy with progress, `(randomized)`/`(incomplete)` labels, restore-from-backup, Open Project Folder |
| Robustness | global exception handlers, `RunGuarded` wrappers, `InputRules` validation on every numeric/name field |

### Domain facts already nailed down — reuse, do not re-measure

- **Level cap 30**: every one of the 169 stat sets provides rows exactly 1–30 (5,070 rows, none higher).
- **Gold ceiling 999,999**: measured in game — a roster written with 2,000,000,000 loads and displays, but the first
  gold gained or spent snaps to 999,999. Vanilla's own 999,980,999 snaps too. Item prices are capped at the same number
  because a price must be payable.
- **Affinity 0–200**; a unit holds at most 100 points of offensive affinity.
- **Equipment gate**: `FUN_800fd694` checks school tier (1 Amateur ≤ 5, 2 Semi-Pro ≤ 10, 3 Pro unlimited) against
  `ITEMMINLEVEL`, then `FUN_800f96f4` matches item Type/SubType/Style against `ITEMCAT` (`<Any>` = byte 1). Tiers come
  from championships beaten at runtime, not file data. `ITEMSCOMP` slots are weapon/armor/shield/helmet/accessory with no
  validation.
- **The item-set `[Tag]` is stripped by the packer**, so `unititems.idx` holds only `(min:u8, max:u8, itemId:u16)`.
- **Underscore display rule**: a unit name containing `_` displays only the part before it (`ludo_random` shows as
  "Ludo") without sharing data. `InputRules.DisplayNameOf` encodes it.
- **Skill availability**: `CharacterCanLearnSkillEventually` builds a 64-bit class mask by repeating `SKILLUSECLASS`
  (empty mask = any class); `SKILLLEVEL` gates nothing; price is `SKILLJOBPOINTCOST`.
- **Skills have three layers the .tok makes easy to confuse** (measured 2026-11-09 against 1,811 records):
  - *Combo hits are records too.* 185 skills are named by another record's `SKILLSUBSKILL` (`Amazon Combo I` →
    `Amazon ComboChain I` → `…II` → `…III` → `…IV`). They cost 0 JP because they are bought with the root, and many
    have no `SKILLUSECLASS` at all, which is how they appeared in other classes' pickers. `SKILLMETER`'s third value
    bounds how many shared hits a given root uses (`Combo I` = 2, `Combo IV` = 5), the same rule UsusBot's combo
    reader applies. `LearnableSkills.ChainOf` follows it and never offers a hit on its own.
  - *Category is `SKILLCREATE`'s second argument*: `Attack` (1,090), `Innate` (316), `Support` (241),
    `Secondary` (162), `Command` (2). `Innate` (e.g. `Gwazi OffBalance`) and `Command` (`Pass`, `Continue`) are
    placed by the game, not the designer, so the picker hides them.
  - *Level 0 means "the class's basic"*: `Amazon Attack`, `Amazon Daze`, `Amazon Evade` are 0 JP / level 0 and
    **are** written into rosters (vanilla grants `SKILL: "Valens Attack"`), so they stay in the list. The 8
    unrestricted records left over (`Crowd Status I–IV`, `Pass`, `Continue`, `Gwazi OffBalance`, `Other HellGate`)
    are behind an opt-in "any class" toggle, marked `· any class`.
- **Job points**: `JobPoints.Awarded` continues from the last listed `LEVELUPJPGIVEN`; `Compute` deducts skill costs.
- **A pack is `bec-tool-all.py`, and that step is the whole variance.** Measured on one project, one machine:
  cold (packed straight after unpacking it) `bec-tool-all.py` = **129.9 s** of a 136 s pack; warm (packed again)
  **8.1 s** of a 14.3 s pack. Every other step is 0.1–4.9 s either way (`ngciso-tool.py` ~4.5 s, the four
  config/unit steps ~1 s in total). That step walks all 27,298 files under `_BEC/Data`, `zlib.compress(…, 1)`s what
  it can and writes a ~1.4 GB `gladius.bec`, so its cost is file I/O over the whole tree plus one huge write. That is
  why "sometimes 2 minutes, sometimes 15 seconds" had no obvious trigger: it is cache and antivirus state, not the
  project. Reproduce the slow case by packing straight after an unpack (or after a reboot); reproduce the fast case
  by packing twice in a row. The real lever is a Defender exclusion for the project and ISO folders.

---

## 5. What is next, in order

### 5.1 Phase 8 — `ModdingGUI.Mcp`

`ModdingGUI.Mcp/ModdingGUI.Mcp.csproj` is in the solution and builds, but `Program.cs` is a **46-line inert
placeholder**: it validates a `<projectPath>` argument and exits, with no stdio server, no tool registry, no JSON-RPC.
Phase 8 is a greenfield implementation behind that stub. Plan is in `PLAN.md` §8: one tool per Core method,
`strict: bool = false` on writes, `logs_*` over `IAppLog` (now real, because pack/unpack emit `LogEntry`s),
auto-generated tool docs. Note the headless-only APIs above — MCP can expose encounters/leagues/units/config long
before the GUI does.

### 5.2 Phase 9 — polish

Undo UI over `.mod-history`, per-backup diff viewer, project-wide search, "validate project" aggregate, README + per
format docs.

### 5.3 Team Builder refinement backlog — **closed**

All four items the user asked for are done (Phase 7 UX pass):

1. **Loadout layout** — the Gear page is search + slot filter over the class's legal items on the left, and on the
   right: the selected unit with a level box, five labelled slots, the highlighted item's numbers, and the item/stat
   set row. The Skills page is two labelled lists with a JP line and a details box. Every control has a `Name`.
2. **Gear search/filter** — case-insensitive across name/type/subtype/style/rarity, plus a slot filter, with an
   `N of M legal items` counter.
3. **JP re-balance** — adding a skill that pushes the build over its award offers to raise `JOBPOINTS` to the spend
   (once, on the crossing add), logs the decision, and the JP line shows `(overridden)` / `(over budget)` in red.
4. **Preview refresh** — a level change re-reads the stat set at the new level and repaints; every gear/skill/set
   mutation refreshes both the loadout and the stats panel.

Also fixed on the way: the level box validates through `InputRules` (1–30) instead of clamping silently, and the
missing-file check now goes through `ProjectRoots.ResolveBecFolder` so a project whose BEC folder is not literally
`<name>_BEC` (the `REALDATA_*` fixtures) opens.

### 5.3b Second UX round, from the owner's screen-by-screen review

1. **Rules page rebuilt** — four labelled groups instead of loose controls: *Team type* (campaign only; PvP is not
   built, so the radio pair is gone and the level box is no longer locked to 1), *Team size and starting level*,
   *Job points for new units* (the derived number with "auto — what the class awards by this level", and an explicit
   "Give new units a custom amount instead" checkbox), *Rosters to save into*, *Edit an existing team*.
2. **Only the two campaign rosters are offered** — `valensimperia.tok` and `ursulanordagh.tok`, in the export ticks and
   in the load drop-down, with a line saying why: the campaign reads only those, the rest of `data/school` are test
   rosters. Picking one used to write a team the game never loads.
3. **Gear and skills are blank until there is a unit to edit** — the page is replaced by "No units yet…" (or "select a
   unit") instead of an empty list with live buttons, and both pages name the selected unit.
4. **The export message counts units, not writes** — "Team of 1 unit(s) written to 2 roster(s)" plus a per-roster
   breakdown. The old line summed the per-roster counts and read as a phantom second unit.
5. **Skills list is the class's real, teachable set** — see the domain facts below. `Amazon` went from 59 entries
   (combo hits and other classes' abilities included) to 39, tagged `· combo ×N` where a skill starts a chain, with a
   details panel that shows the chain and says why its hits cost nothing. An opt-in checkbox lists the unrestricted
   records for anyone who wants them.
6. **The 40-gladiator confirmation and the export summary go through `UiConfirm`/`UiAlert`**, so an automated run does
   not deadlock on a modal.
7. **A loaded unit round-trips.** Loading a roster carried LEVEL, stats and SKILL but not `ITEMSCOMP`, so a loaded unit
   opened with an empty Gear page — and the next save wrote five empty slots over the roster's equipment. `Unit` now
   carries `Experience` and `Customize` too (a save used to reset `EXPERIENCE` to 0 and `CUSTOMIZE` to `0`), and the
   class item-set fallback only fills gear for a unit *made in the tool*, never a roster unit's empty slots.
8. **Saving is idempotent.** The exporter compares each field and the whole skill list before writing, so loading a
   roster and saving it untouched reports `1 added, 0 updated, 2 unchanged` and leaves the file byte-identical apart
   from the new block. Before this every unit was rewritten whether or not anything had changed.

### 5.3c The pack validation pass, rebuilt on Core's validators

The Pack tab's "skip validation" checkbox used to guard three regex checks written in the shell. They only looked at
skillsets that `gladiators.txt` pointed at, so a bad skill name in a set nobody referenced passed validation and then
killed the pack inside `Gladius_Units_IDX_Repack.py` with `ValueError: 'Wolf Attack' is not in list`. That is how the
gap was found: a project that had "validated" could still not be packed.

1. **The pass now runs Core's own validators** — `ProjectValidator.Validate(projectRoot)` calls
   `GladiatorsApi.Validate`, `SkillSetsApi.Validate`, `ItemSetsApi.Validate`, `StatSetsApi.Validate`,
   `TintSetsApi.Validate`, `SchoolNamesApi.Validate` and `SchoolsApi.Validate` over every roster, and reports one
   list of findings from a single place. Nothing is re-implemented in the shell, and the CLI (`--validate <project>`)
   prints the same report, so MCP strict mode can reuse it.
2. **No check compares against a vanilla count.** A project unpacked from a modded ISO legitimately has different
   numbers of skills, items, sets and units; the checks verify that references *inside* the project resolve. A test
   asserts that removing every `SKILLCREATE` record produces findings about the dangling names and never a complaint
   about the missing vanilla total.
3. **Nothing blocks packing.** Findings are Warnings and Info notes; the summary says so out loud
   (`Validation: 62 finding(s): 0 error(s), 0 warning(s). These are advisory`). Info notes are counted, not listed,
   because vanilla produces 62 of them by itself (blank entry 0, tints that skip a channel, duplicate grants).
4. **It is on by default again.** The setting had shipped off, and a saved `settings.json` keeps it off, so
   `AppSettings.ValidationPassVersion` migrates version 0 users once: `ValidateBeforePacking = true`. The checkbox is
   now the escape hatch ("Skip validation (pack without checking)").
5. **Cost**: 0.8–1.7 s on a vanilla project (62 findings across 7 areas), so it does not need to be optional for
   speed.
6. The one shell-side check that remains is item-set weapon coverage (levels 1–99), which no Core validator makes;
   it reports at most ten gaps and no longer gates the pack.

### 5.3d App-level QA pass

An automated pass over the shipping UI found ten defects and fixed them all; the full log is
[`testing/QA_PASS.md`](testing/QA_PASS.md). The ones worth remembering:

- **A modal on a tab switch.** Selecting the Editors tab popped a "Select a Project" picker whenever the sidebar had
  no selection - the state the app starts in - so looking at the tab blocked the window. It now selects the first
  project and says so in the log.
- **A hidden unit.** Adding a unit left nothing selected, so Gear still read "No units yet" and Remove Unit stayed
  disabled. The new unit is selected now.
- **A silent input.** The level box dropped text it could not parse, then Add refused with "got 0". It clamps and
  logs instead.
- **The retired Rules page** was the first thing a fresh installer saw in the Team Builder, because the rebuilt page
  was only created on project selection. It is created on tab entry.
- **Patching at 1080p** clipped its own "Create xDelta" panel with no scrollbar; the split panels scroll now.
- **Text**: 55 labels shortened and ~70 tooltips added (`UiHelp.cs`). The rule is that the label says what, the
  tooltip says the one thing a label cannot fit - see `UiHelp`'s remarks. Nothing that a user needs is only in a
  tooltip; depth lives there, meaning does not.

The pass also hardened the driver, because a driver that lies poisons every result after it: `set` on a TabControl
reported success without switching, `screenshot` could paint the previous page, and any `MessageBox` in a handler
hung the run. `capture`, `closeDialog` and the automation dialog guard are the answers; `scripts/ui-batch.ps1` runs a
whole scenario over one connection with per-command timings.

### 5.4 The UI driver (how GUI work gets verified)

`--ui-server` / `--ui-script` let a script or an agent drive the window over loopback JSON lines — snapshot, click,
set, select, screenshot, log. It is opt-in, loopback-only, and drives the real controls, so it cannot reach a Core API
except by pressing the button that does. Documented in `docs/UI_AUTOMATION.md`; the replayable scenario for this phase
is `scripts/team-builder-smoke.txt`, and the checklist is `docs/testing/MANUAL_TESTING_TEAM_BUILDER.md`.

Unpack, pack and compile are **jobs** (`Core.Jobs`), and a `click` that starts one answers with its id, so a script
writes `waitForJob` and gets the result when the work ends. Never sleep for a guessed duration and never poll the log
for "Unpacking completed successfully." — that was the first version of this, and it timed out at six minutes on the
day the machine was busy.

---

## 6. Pending in-game QA (needs the user)

| Item | What to check |
|---|---|
| Phase 6 randomizer | one playthrough of `qa-pack/qa-pack.iso` (seed `phase6-qa4`) — nothing has been verified in game yet |
| Phase 5 units | a unit built from an edited `statsets`/`skillsets`/`itemsets` loads correctly |
| `gold_test` project | rosters hold 999,999 gold; one item costs 999,999 with affinity 200 — confirm a purchase leaves 0 and the affinity display caps sanely |
| Team Builder export | a roster exported through the panel boots and the edited unit matches the build |
| Phase 7.8/7.9 GUI pass | pack/unpack from the buttons: output appears in the panel, a deliberately broken tool path reports failure instead of "completed successfully", and a project under a path containing `(` works |
| Team Builder UX | `docs/testing/MANUAL_TESTING_TEAM_BUILDER.md`, or replay `scripts/team-builder-smoke.txt` against the running app |

---

## 7. Traps inside this codebase (learned the hard way)

1. **Two path dialects.** Fields are normalised to forward slashes for the Python toolchain; Windows APIs and `cmd`
   need backslashes. Use `ShellFolderOpener.NormaliseForShell` at any boundary that leaves .NET. Three separate bugs
   came from this (class/skill loading, folder opening, ShellExecute).
2. **Never `Process.Start` a folder** with `UseShellExecute = false` — Windows answers `Access is denied`, which reads
   like a permissions problem and is not.
3. **Never spawn `explorer.exe` yourself.** Folder opening goes through `Helpers/ShellFolderOpener`
   (`SHOpenFolderAndSelectItems`), which asks the running shell and creates no process.
4. **Log re-entrancy**: `AppendLog` writes to `IAppLog`, whose `Written` event used to call back into `AppendLog` and pin
   the UI. Rendering uses `AppendLogText`; subscriptions queue and drain. `BuildRunner.LogMirror` is the same pattern.
5. **Stale async loads**: the item/skill editors bump a version counter and discard results from an older load; a
   deleted project must not throw from a background task (`ClearEditorState`, task-existence guards, `_ = task?.Exception`).
6. **Incomplete projects**: `IsCompleteProject`/`MissingProjectFiles` gate every editor; an incomplete project reports
   what is missing instead of throwing.
7. **`ProjectRoots.ResolveBecFolder`** guards `EnumerateDirectories` — it used to throw `DirectoryNotFoundException` for
   a folder that did not exist yet.
8. **`RawLineFile.GetPath` and `SkillsApi.GetPath` resolve the BEC folder**; anything new must too.
9. **The shell writes cp1252 in places** (`frmMain_partials/Settings.cs`) — read it as cp1252, not UTF-8, if you script
   edits over it. `TeamBuilder.cs` is UTF-8; a scripted rewrite there must not change its line endings.
10. **`PathHelper.QuotePath` is batch-text only.** It escapes `(`, `&`, `%` with carets, which is right for a `.bat` and
    wrong for `ProcessStartInfo.Arguments` — a caret reaches the tool literally. The xdelta3 call site had that bug,
    which is why 7.9 was worth doing.
11. **MSBuild on this machine:** a `Build FAILED` with `0 Errors` and no project errors is a restore/node failure, not
    a code error. Builds also fail with `MSB3491 … Access is denied` writing under `obj\` when the workspace file
    policy is read-only. Check the policy before debugging code.
12. **A blank line ends a section in `data/units/*.txt`.** `SectionedFile` treats a blank line as the end of a
    `Skillset N:` / `Statset N:` block, so a row added *after* the blank line that follows a set belongs to no set at
    all: the editors and the validators both ignore it, silently. When hand-editing one of those files, put the row
    directly under its header line.
13. **A pack can be 130 s or 8 s and neither is a bug.** `bec-tool-all.py` dominates (see §4); the number depends on
    how much of the 1.8 GB tree is in the OS cache and whether the antivirus is scanning it. Do not "optimise" the
    step away without measuring it first — the per-step log line (`bec-tool-all.py: exit 0 in 8.1s, 50 line(s)`) and
    `BuildStepResult.Duration` exist for exactly this.
14. **The driver owns the UI thread**, so a modal dialog hangs it: `UiConfirm` is the safe way to ask (it logs and
    answers yes under automation), while a legacy `MessageBox.Show` in a path the driver reaches will stall the run
    until the app is killed. Also kill the app before rebuilding — a running exe locks `bin\…\ModdingGUI.exe`.
15. **A roster unit is a snapshot of the file.** Loading reads the fields the model understands; anything it does not
    carry must be written back verbatim or left alone. `ITEMSCOMP`, `EXPERIENCE` and `CUSTOMIZE` were each lost this
    way, and the item-set fallback invented gear into deliberately empty slots. If you add a field to `Unit`, add it to
    `LoadSelectedRoster` *and* `BuildSchoolUnit`, and make the writer compare before writing.
16. **`DrawToBitmap` screenshots are the window at its logical size**, not what a scaled desktop shows; read the
    snapshot's bounds when cropping, and do not trust a downscaled preview for pixel-level colour questions.

---

## 8. Where to look

| Need | File |
|---|---|
| Roadmap, phase scope, exit criteria | `PLAN.md` |
| Subsystem map + test bar | `docs/AGENTS.md` |
| Build/test detail, partial-class map | `CLAUDE.md` |
| Engine facts (equipment, skills) | `docs/DOL_FINDINGS.md`, `dol/*.json` |
| Measured input limits | `ModdingGUI.Core/Units/InputRules.cs`, `dol/equipment_rules.json` |
| Environment traps (shell/launch) | `docs/WINDOWS_GOTCHAS.md` |
| API surface | `ModdingGUI.Core/Api/*.cs` |
| Toolchain execution (no shell) | `ModdingGUI.Core/Tools/{ToolStep,ToolRunner,PackService,UnpackService}.cs` |
| Shell features | `frmMain_partials/*.cs`, `Forms/`, `GridBuilder/`, `SkillEditor/` |
| Manual test checklists | `docs/testing/*.md` |
| Driving the GUI (tests, agents) | `docs/UI_AUTOMATION.md`, `scripts/ui-drive.ps1`, `scripts/team-builder-smoke.txt` |
| Last QA ISO | `qa-pack/qa-pack.iso` (seed `phase6-qa4`) |

---

## 9. Working agreements with the user (project owner)

- **Ask instead of guessing.** Ambiguity in a data rule is a question, not an assumption.
- **No drift.** Use the proper channel (a Core API), not a shortcut, even when the shortcut is faster.
- **Least restrictive.** Do not impose vanilla maxima on a modder; enforce only what the engine enforces, and put the
  measured evidence in the code comment.
- **Validate against the DOL and with the user**, both, for anything battle-facing.
- **Explain the provenance** of any limit or number that lands in the code.
- Both builds matter: the user runs the **Debug** exe.

## 8. The interface layer (added after Phase 7)

The shell now has a design system instead of per-control values:

- **`frmMain_partials/UiTheme.cs`** - two palettes (Dark default, Light), fonts, DPI-scaled spacing, log colours, and
  `Apply(root)` which repaints an existing control tree. Colours are properties over the current palette, so switching
  scheme needs no restart; `AppSettings.Theme` remembers the choice and Options > "Dark theme" toggles it.
- **`frmMain_partials/UiKit.cs`** - the primitives pages are made of: `Card(title, caption)`, `Form`, `Row`,
  `RowWithButton`, `Primary`/`Secondary`/`Browse`, `Actions`, `AttachTabStrip`, plus `FieldHeight`/`FieldWidth` and
  `CardHeight`/`HeaderHeight`, so sizes come from fonts and metrics rather than guessed numbers.
- **Rebuilt pages**: Patching (`PatchingPage.cs`), Unpacking and Packing (`BuildPages.cs`), Randomizer
  (`RandomizerPage.cs`). Each is cards plus grids; the existing controls are reparented so names, handlers and the
  driver scripts keep working. **Still on the designer canvas:** Team Builder's four pages, the two Editor pages and
  Grid Builder.
- **Chrome**: `Chrome.cs` themes the menu (own renderer, own check mark) and replaces the native tab strip with a row of
  themed buttons - Windows paints a tab control's strip itself, so the run right of the last tab could not be themed any
  other way. The strip follows pages added or removed at run time, which is what finally retired "Ingame Random".
- **Settings dialog** (`Forms/SettingsForm.Themed.cs`) is built on the kit as well; modal dialogs block the UI driver,
  so `ModdingGUI.exe --render-settings <png>` draws it off-screen for screenshots.

Verification tooling lives with the driver: `audit`/`auditAll` report contrast and system-colour use per page, and `hit`
names the control at a point (see `docs/UI_AUTOMATION.md`). `docs/testing/QA_PASS.md` �10-�15 record the findings and
the two traps that cost the most time: a docked `Panel` has no automatic height, and a single-line `TextBox` (or a
disabled control) is painted by the framework whatever colour you assign.

Remaining UI work: the five pages above, a themed progress bar (the Randomizer's is hidden for now because a
`ProgressBar` is system-painted), and the sidebar/tree chrome.