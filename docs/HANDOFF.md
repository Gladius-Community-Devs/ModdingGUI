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
    ├── ModdingGUI.Tests/                    xUnit (net8.0-windows), 763 tests
    └── ModdingGUI.Mcp/                      Phase 8 stub: builds, Program.cs is inert (no stdio server yet)
```

**State of the tree:** clean, `dotnet build` reports **0 warnings** in Debug and Release, suite is
**763 passed / 0 failed / 0 skipped**.

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
| Build layer (`ToolStep`, `ToolRunner`, `PackService`, `UnpackService`, `ProjectBuildService`) | done (7.8 + 7.9) — no shell, no batch file |
| `Randomizer` pipeline + `ProjectBuildService` | done (Phase 6), GUI-wired, CLI-wired |
| `Units`: `ItemSetGear`, `ItemCategoryRules`, `JobPoints`, `LearnableSkills`, `InputRules` | done — engine rules as code |
| `Tools`: `ProjectFileOperations` (measure/copy/delete with progress), `BatchContentBuilder` | done |

### Shell

| Feature | State |
|---|---|
| Patch apply, unpack, pack | **done and Core-owned**: `BuildRunner.cs` calls `PackService`/`UnpackService`, mirrors the log into the panel, and reports a real failure instead of unconditional success |
| Randomizer panel | parameter builder over `RandomizerOptions`; progress + log mirroring through `AppLog` |
| Team Builder | Phase 7 done: reads through `IClassesApi`/`IGladiatorsApi`/`IStatSetsApi`/`IItemsApi`/`IItemSetsApi`/`ISkillsApi`, exports through `ISchoolsApi`; no shell-side parsing left |
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
- **Job points**: `JobPoints.Awarded` continues from the last listed `LEVELUPJPGIVEN`; `Compute` deducts skill costs.

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

### 5.3 Team Builder refinement backlog (agreed with the user, not started; the data path is complete)

1. Loadout editor layout — currently dense; needs grouping (weapon/armor/shield/helmet/accessory) and clearer labels.
2. Gear list search/filter (404 item sets, 1,852 items — the list is long).
3. JP re-balance after skill edits: adding a skill should offer to raise `JOBPOINTS` so the unit stays legal.
4. Preview refresh: the stat preview does not always repaint after a level or statset change.

---

## 6. Pending in-game QA (needs the user)

| Item | What to check |
|---|---|
| Phase 6 randomizer | one playthrough of `qa-pack/qa-pack.iso` (seed `phase6-qa4`) — nothing has been verified in game yet |
| Phase 5 units | a unit built from an edited `statsets`/`skillsets`/`itemsets` loads correctly |
| `gold_test` project | rosters hold 999,999 gold; one item costs 999,999 with affinity 200 — confirm a purchase leaves 0 and the affinity display caps sanely |
| Team Builder export | a roster exported through the panel boots and the edited unit matches the build |
| Phase 7.8/7.9 GUI pass | pack/unpack from the buttons: output appears in the panel, a deliberately broken tool path reports failure instead of "completed successfully", and a project under a path containing `(` works |

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
