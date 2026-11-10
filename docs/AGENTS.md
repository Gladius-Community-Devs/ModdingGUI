# AGENTS.md — Gladius Modding Tools Context & Planning

## What This Repo Is

Gladius Modding Tools is a **Windows .NET 8 (WinForms)** GUI for modding the GameCube/PS2 game *Gladius*. It unpacks `.iso`/`.bec` archives, edits game data files (`.tok`, `.enc`, `.txt`, `.grd`), and repacks them. It is mid-way through a **ground-up rewrite** into a three-project architecture:

- **`ModdingGUI`** — WinForms shell. Treated as a **parameter builder** over Core APIs. Never edits game files directly.

- **Logging is Core-owned (Phase 7.8, done).** Every user-visible line goes through
  `ModdingGUI.Core.Logging.IAppLog` / `AppLog` (ring buffer, `Written` event, file sink at
  `%LOCALAPPDATA%\GladiusModding\last-run.log`). The shell's `AppendLog` records first and renders
  second; the Grid Builder shares the same instance. **New output goes the same way**: take an
  `IAppLog`, never write to a control directly, and when adding a Core service return data (results,
  warnings, DTOs) rather than printing. An MCP client can only see what exists as data.
  Pack, unpack, script compile and xdelta now own their child processes in Core
  (`PackService` / `UnpackService` / `ProjectBuildService` + `ToolRunner`), so the GUI and the CLI
  print the same log.
- **`ModdingGUI.Core`** (`net8.0`) — parsing, schemas, Tok framework, APIs, history, locking, logging, toolchain. No UI.
- **`ModdingGUI.Mcp`** (`net8.0`) — stdio MCP server exposing the Core APIs as tools (Phase 8, scaffolding today).

The actual git repository lives at **`ModdingGUI/`** (origin `git@github.com:Gladius-Community-Devs/ModdingGUI.git`). The workspace root is a staging area that is **not** a git repo.

## Read First

- **`PLAN.md`** is the authoritative roadmap. Architecture is **locked** (section 0), and the phase sequence is **locked** (section 2). Read it before planning or implementing anything.
- **`ModdingGUI/CLAUDE.md`** has the detailed per-subsystem agent guide (build commands, test infra, partial-class map, migration targets).
- **`docs/UI_AUTOMATION.md`** is how to drive the window from a script or an agent (`--ui-server`, `scripts/ui-drive.ps1`). Use it to verify any GUI change.
- **`docs/prd-template.md`** is the PRD template used by `/feature`.
- `docs/testing/MANUAL_TESTING_PHASE_2_6.md` is the manual test checklist for the Item Editor migration.
- `docs/testing/MANUAL_TESTING_PHASE_2_7.md` is the manual test checklist for Skill Editor validation.
- `docs/testing/MANUAL_TESTING_TEAM_BUILDER.md` is the Team Builder checklist; `scripts/team-builder-smoke.txt` replays it.

## Layout

```
GladiusModdingToolsGUI/
  PLAN.md                       Master roadmap — phase-locked, architecture locked
  clean-bec-unpacked/           Vanilla game corpus (27k files) — READ-ONLY ground truth.
                                Sits OUTSIDE the git repo (sibling of ModdingGUI/) on
                                purpose: never version-controlled, never touched.
                                Reference only. Source of truth for round-trip tests.
                                Any test/edit that writes must operate on a staged COPY.
  GridBuilder.jar_Decompiler.com/  Decompiled original Java app (reference only)
  tools/                        Legacy Python modding toolbelt (started directly, no .bat)
  docs/                         AGENTS.md, prd-template.md, test-standards.md
  MANUAL_TESTING_PHASE_2_6.md   Manual test checklist (Item Editor)
  MANUAL_TESTING_PHASE_2_7.md   Manual test checklist (Skill Editor validation)
  bugs.txt                      Bug-tracking / regression notes
  .opencode/                    OpenCode commands (opencode.jsonc)

  ModdingGUI/                   ← THE GIT REPO + .NET solution
    ModdingGUI.sln
    ModdingGUI.csproj           WinForms shell (net8.0-windows, WinExe)
    ModdingGUI.Core/            net8.0 no-UI core (Tok, Schemas, Api, Skills, History, Lock, Logging, Tools)
    ModdingGUI.Mcp/             net8.0 MCP stdio server (scaffolded, Phase 8)
    ModdingGUI.Tests/           xUnit, net8.0-windows
    skills/                     Skills catalog pipeline (versioned)
classdefs/                  Class-definition catalog pipeline (versioned)
encounters/                 Encounter catalog pipeline (versioned)
tables/                     School + league catalogs (versioned)
dol/                        Engine field tables extracted from the DOL (authority)
      build_catalog.py          The ONLY producer of skills_catalog.json
      skills_field_reference.txt  Hand-written field docs + MULTI/LINK/REQUIRES rules
      skills_catalog.json       Generated catalog (consumed by Core, never hand-edited)
      skills_catalog_overrides.json  Python-input only additions
      skills.tok, modded-skills.tok  Catalog inputs — GITIGNORED (copyrighted game data)
    frmMain_partials/           Legacy main-form partials (migration targets)
    GridBuilder/                Self-contained .grd editor UI — the model now lives in Core
                            (`ModdingGUI.Core.Grids.GridFile`): v2 grids round-trip byte for
                            byte, and v1 (27 of 430) reads/writes its legacy per-cell slot lists:
                            a no-op save is byte-identical, an edit rebuilds the offset payload.
                            Writes go through `IGridsApi` (lock + `.mod-history` backup), never
                            through `GridFile.Save` from the shell
    SkillEditor/                Catalog-driven skills editor (Core-backed UI)
    scripts/                    ui-drive.ps1 (UI driver client) + replayable GUI scenarios
    Helpers/ Parsers/ Models/ Validators/  Migration targets into Core
```

## Build & Test

```
dotnet build ModdingGUI.sln          # whole solution (shell + Core + Mcp + Tests)
dotnet test ModdingGUI.sln           # xUnit suite: 775 pass / 0 fail / 0 skip
.\run-tests.ps1                      # colored runner w/ TRX, filters, -ProjectFolder
.\run-tests.ps1 -Filter RealGameDataTests
.\run-tests.ps1 -ProjectFolder REALDATA_v2
dotnet publish ModdingGUI.sln        # self-contained single-file exe (win-x64)
```

All commands run from inside `ModdingGUI/` (that is where the `.sln` and the git repo live).

### Test bar (mandatory, from PLAN.md)

- `dotnet test -c Release` green at the end of every phase. No phase merges with failing tests.
- Baseline is **775 passed / 0 failed / 0 skipped**. Every phase ends green.
- `ModdingGUI.Tests/Core/Tok/TokRoundTripTests.cs` is the **corpus gate**: it parses and re-serializes every `.tok`/`.enc` in `clean-bec-unpacked/` (977 files) and fails on any byte diff. Never break this.
- The `RealGameDataTests` are gated behind a `REALDATA_*` project folder + `vanilla.iso`. To enable them locally, run **`ModdingGUI/setup-realdata.ps1`** — it breaks the vanilla ISO down from scratch using the app's own unpack toolchain (`ngciso-tool.py` → `bec-tool-all.py` → `Gladius_Units_IDX_Unpack.py`, the same commands `BatchContentBuilder` emits), byte-verifies the result against `clean-bec-unpacked` (0 missing / 0 extra / 0 diffs), and installs it into `bin/{Debug|Release}/net8.0-windows/win-x64/REALDATA_<name>/` plus a `vanilla.iso` hardlink. The vanilla ISO + `clean-bec-unpacked` are outside the repo; `bin/` is gitignored.
- Integration fixture: `ModdingGUI.Tests/Integration/GameDataFixture.cs` points at `clean-bec-unpacked/`. Do not check large new fixtures into the repo.
- **Nullability policy (build must stay warning-free).** `ModdingGUI.Core` sets `Nullable=enable`
  **and promotes CS8600-CS8629 to errors** — Core is the source of truth, so new logic cannot land
  with a nullability warning. `ModdingGUI.Tests` is `enable` as well. The WinForms shell is
  `annotations`: type declarations stay annotated for callers, but flow analysis is off because
  designer fields and event-handler parameters are not nullable-aware (that accounted for the
  original 107 CS86xx warnings). A shell file flips to `#nullable enable` when it is ported onto a
  Core API; logic that needs checking belongs in Core, not in the shell. Consequences:
  `dotnet build` is expected to report **0 warnings** in both Debug and Release — if a new warning
  appears, fix it rather than widening the suppression.

## Versioning / Release

- Version is **hard-coded** as `4.2.0` in `ModdingGUI/frmMain_partials/ProgramInfo.cs` (`ProgramInfo.Version`).
- GitHub Releases are tag-based; the in-app updater fetches `https://api.github.com/repos/Gladius-Community-Devs/ModdingGUI/releases/latest` and downloads `ModdingGUI-{tag}.zip`.
- Publish profile: `ModdingGUI/Properties/PublishProfiles/FolderProfile.pubxml` → `bin\Release\net8.0-windows\publish\win-x64\`, self-contained, single-file.
- The only CI workflow (`ModdingGUI/.github/workflows/github-releases-to-discord.yml`) posts published releases to Discord. There is **no build/test CI**.
- Users need the `tools/` folder (Python scripts + executables) alongside the exe.

## Architecture Rules (locked — from PLAN.md)

1. **No GUI bypass.** Every persistent edit goes through a Core API. The shell never writes game files directly.
2. **Backups always.** `ChangeHistoryService.Backup` is non-optional on every write → `<project>/.mod-history/<rel>/<yyyyMMdd-HHmmss-fff>.bak`.
3. **Locks always.** `ProjectLock.Acquire` wraps every multi-step write.
4. **Round-trip first.** New format = round-trip test green before any mutation lands. Every `.tok`/`.enc` re-serializes byte-for-byte on no-op; unknown tags + comments preserved.
5. **Reuse helpers.** `ProjectPaths`, `PathHelper`, `BatchContentBuilder`, `LookupHelper` (until deleted), validators. Never reimplement.
6. **Catalogs and static assets** ship at `<exe>/<filename>.json` at root unless there's a reason otherwise.
7. **Validation is advisory** in the GUI (`WarnInLog`); opt-in strict in MCP (`RejectOnError` via `strict: true`).
8. **No new dependencies** without explicit approval. **`System.Text.Json` only** for JSON (no Newtonsoft). tok I/O is **ISO-8859-1**.
9. **`dotnet test -c Release` green** at end of every phase.
10. **One phase at a time.** Don't expand scope mid-phase.
11. **GUI work is verified in the GUI.** Start the app with `--ui-server` and drive it with `scripts/ui-drive.ps1`; the driver clicks real controls, so it can never be used to bypass an API.

## API discipline (Core)

Stateless `open → mutate → write`. Per-project `ProjectLock`. Timestamped backup on every write. Current API surface in `ModdingGUI.Core/Api/`:

- `IItemsApi` — `List/Get/GetAll/Patch/Add/Remove/Rename` on `data/config/items.tok`
- `ISkillsApi` — raw-line, freeform skills editing (no typed schema — permanent). Since
  Phase 2.7 also `Validate(projectPath, SkillDto, catalog?, context?, options?)` and
  `Warn`/`Reject`-mode overloads of `Add`/`Update` returning `SkillMutationResult`.
  Rules/catalog live in `ModdingGUI.Core.Skills` (`SkillsCatalog`, `SkillsValidator`,
  `SkillsContext`).
- Skill *picking* lives in `ModdingGUI.Core.Units.LearnableSkills`: the engine's class-mask rule (no
  `SKILLUSECLASS` = any class) **plus** the table's structure — records named by another's `SKILLSUBSKILL` are combo
  hits and are never offered, `Innate`/`Command` categories are game-granted, and unrestricted records are opt-in.
  `SKILLMETER`'s third value bounds a chain. Do not re-derive this per screen; `docs/HANDOFF.md` §4 has the counts.
- `ILookupTextApi` — language-aware `Load/Update/Add` over `lookuptext_<eng|fra|ger|ita|spa>.txt`
  (ISO-8859-1: the corpus is Latin-1, **not** UTF-8 — the pack tool reads it as ANSI)
- `IClassesApi` — raw, comment-preserving `classdefs.tok` blocks: `List/GetAll/Get/SetField/
  AddField/RemoveField/Rename/Add/Remove/Validate`, plus `ClassDefsCatalog` +
  `ClassDefsValidator` fed by `classdefs/classdefs_catalog.json`
- `IEncountersApi` — `.enc` files as header fields plus `TEAM:` sections: `ListFiles/List/Get/
  SetField/AddField/RemoveField/SetTeamField/RemoveTeamField/SetTeamUnits/SetTeamUnit/
  AddTeamUnit/RemoveTeamUnit/Validate`. `EncounterUnit` models the 21-value `UNITDB` record
  from the DOL (`EncounterSetup` case `0x1D`: string, int, string, int×5, string×4, int×12);
  slots 7/8/17/18/19 are exposed by index because the engine passes them through untested.
  `EncountersCatalog` merges the engine's 20-key field table (`dol/encounters_fields.json`,
  including the 3 keys vanilla never uses: `BLOCK`, `FORCEPLACEMENT`, `VOICEINTRO`)
- `ISchoolsApi` — `data/school/*.tok` rosters: `NAME`/`HERO`/`GOLD` plus `CREATEUNIT` blocks
  (level, job points, `CORESTATSCOMP2`, `ITEMSCOMP`, repeating `SKILL`). `SetUnitSkills`
  replaces the grant list; `SchoolsCatalog` + `SchoolValidator` from `tables/schools_catalog.json`
  (74 rosters, 924 units)
- `ILeaguesApi` — `data/towns/leagues/*.tok`: office header, `LEAGUE` sections and their
  `ENCOUNTER` bouts. `SetEncounterFile` is the in-game harness in one call;
  `LeaguesCatalog` + `LeagueValidator` from `tables/leagues_catalog.json`
  (44 files, 220 leagues, 839 bouts)
- `IConfigTableApi` — any flat `KEY value` tok table (shops, `data/config/*.tok`, `.tnt`):
  `ListFiles/Get/Set/Add/Remove/SetValues`. Deliberately generic: those formats have no
  cross-field rules to type-check

Skills validation is **advisory by construction**: catalog-derived findings (unknown
field, unknown enum value) are `Info`; structural findings are `Warning`; and a
documented `MULTI`/`LINK`/`REQUIRES` rule that the corpus contradicts is downgraded
to `Info` by the generator (`strict: false` + violation count) so `Validate()` reports
zero warnings across all 1,811 vanilla records.

## Active Phase

| Phase | Status |
|---|---|
| Phase 2.6 — Items GUI on `IItemsApi` | **done** |
| Phase 2.7 — Skills catalog-driven, freeform | **done** (manual smoke pending) |
| Phase 3 — Lookup + Classes | **done** |
| Phase 4 — Encounters, Leagues, Towns, Schools | **done**: encounters, schools, leagues and the flat config-table API; `ITownsApi` folded into `IConfigTableApi` (towns hold shops + `.tnt` tints, no `towns/*.tok`) |
| Phase 5 — Units `.txt` family | **APIs done**, in-game QA pending |
| Phase 6 — Randomizer rewrite | **done**, in-game QA pending |
| Phase 7 — Team Builder (incl. 7.5, 7.8, 7.9) | **done** — Team Builder reads/writes only through Core APIs, UX pass landed (searchable gear, labelled slots, item details, JP offer, level-aware preview); pack/unpack/compile/xdelta run from Core with no batch file. GUI work is verified through the UI driver (`docs/UI_AUTOMATION.md`) |
| Phase 8 — `ModdingGUI.Mcp` stdio server | **next** — `Program.cs` is a 46-line inert stub |
| Phase 9 — Polish | not started |

## Known Tech Debt / Gotchas

- **Everything is committed.** Keep the working tree clean; `dotnet clean` only affects gitignored `bin/`.
- **0 build warnings** are expected in Debug and Release. If one appears, fix it (see the nullability policy above).
- `CLAUDE.md` is in `.gitignore` (deliberately not committed) — this AGENTS.md is the committed equivalent.
- No bypasses remain: the randomizer panel builds `RandomizerOptions` for `RandomizerRunner`; Team Builder reads and writes through Core APIs; pack/unpack/script-compile are Core services.
- **`(` and `)` in paths are ordinary.** The toolchain is started with `ProcessStartInfo.ArgumentList`, so there is no batch parser to break. Do **not** reintroduce a parenthesis check, and do **not** pass `PathHelper.QuotePath` output to a directly-started process — a caret escape is literal there. `QuotePath` exists only to render the inspectable batch text ("Save tool commands (.bat)").
- **`clean-bec-unpacked/` is reference-only, never a write target.** It is not part of the git repo (deliberately), so it cannot be restored from version control. Never read-modify it in place. Any test, script, or manual session that mutates data **must copy the file(s) to a temp/staging location first** and operate on the copy (the test suite does this via per-test temp project dirs; see `docs/test-standards.md`).
- `ModdingGUI/skills/skills_catalog.json` is generated by `py -3 skills/build_catalog.py`
- `ModdingGUI/classdefs/classdefs_catalog.json` is generated by `py -3 classdefs/build_catalog.py`
  (needs `clean-bec-unpacked` for the .tok input; the generated JSON is what ships)
  (run from `ModdingGUI/`); never hand-edit the output — add values via
  `skills_catalog_overrides.json` and rules via `skills_field_reference.txt`.
- The catalog's `.tok` inputs are **copyrighted game data and are gitignored**
  (`skills/*.tok`). A fresh clone regenerates from the reference file + overrides but
  enumerates no values until the `.tok` files are dropped in.
- Documented `MULTI`/`LINK`/`REQUIRES` rules that the corpus contradicts are emitted as
  **advisory** (`strict: false`) by the generator. Don't "fix" a red validation test by
  deleting such a rule: fix the reference file, regenerate, and the rule becomes strict
  again automatically.
