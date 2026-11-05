# Gladius Modding Tools — Baseline Manual Test

Verifies the **underlying app shell** works before it is relied upon for phase-specific testing. Run this after a fresh build, after toolchain changes (Python / `tools/` updates), and before any release.

Phase-specific checklists (e.g. `MANUAL_TESTING_PHASE_2_6.md` in this folder) assume this baseline passes. This document is **not** about the Item Editor's new Core API path — that has its own checklist.

## Prerequisites

- A **built app**: `ModdingGUI\bin\Debug\net8.0-windows\win-x64\ModdingGUI.exe` (or Release). Launch the exe **directly** so the app directory = build output (launching via `dotnet run` puts projects under the source folder instead).
- **Python on PATH** and the **`tools/`** folder present next to the exe (unpack/pack shell out to Python).
- **Vanilla ISO**: `E:\code_stuff\GladiusModdingToolsGUI\vanilla_gamecube.iso`.
- A **disposable project** to run destructive tests on (created by Test 4 below, or the staged `REALDATA_vanilla` copy in the build output).
- `clean-bec-unpacked` is **reference-only** — never edit it; compare against it, don't write to it.

## App layout quick reference

| Path (relative to exe dir) | Meaning |
|---|---|
| `tools/` | Python toolbelt invoked by unpack/pack batches |
| `<Name>/<Name>_ISO/` | Filesystem dump of the ISO (contains `gladius.bec`, `fst.bin`) |
| `<Name>/<Name>_BEC/` | Unpacked BEC: `Data/`, `filelist.txt`, `unknown-*.bin` |
| `<Name>/<Name>_FileList.txt` | ngciso file list from unpack |
| `<Name>/<Name>.iso` | Packed output from the Packing tab |
| `<Name>/<Name>_BEC/.mod-history/` | Core API backups: `data/config/items.tok/<timestamp>.bak` etc. |
| `settings.json` | App settings in the exe dir |

Main UI: tabs **Unpacking**, **Packing**, **Patching**, **Randomizer**, **Ingame Random**, **Team Builder**, **Editors** (→ **Item Editor**, **Skill Editor (WIP)**), **Grid Builder**, plus the **project tree** and the **File / Debug Options / Fun Options** menus.

---

## Test 1 — Launch & app directory

1. Launch `ModdingGUI.exe` from the build output.
2. Watch the main log as it starts.

Expected:
- Log reports `App Directory: <build output path>` and the number of directories found (including `tools`).
- Window opens without an unhandled exception; project tree is populated.
- **File → Info** shows the app version (`4.2.0`).
- **File → Check for Updates...** either reports "up to date" or errors cleanly (network-dependent — a clean error is acceptable, a hang is not).

## Test 2 — Project tree detection

1. With the tree loaded, check which folders are listed.

Expected:
- `REALDATA_vanilla` (has `REALDATA_vanilla_ISO` + `REALDATA_vanilla_BEC`) appears as a project; expanding shows **BEC** and **ISO** child nodes.
- A folder missing either `_ISO` or `_BEC` (e.g. the leftover `test1` with only `test1_ISO`) is **skipped** and logged as "not a valid project".
- Selecting a project populates the editors (Item Editor dropdown loads, Skill Editor list loads, Grid Builder arena list loads).

## Test 3 — Settings persistence

1. **File → Settings**, change a setting, OK.
2. Close and relaunch the app; reopen Settings.

Expected:
- `settings.json` is written in the app dir.
- The change is still applied after restart.
- Corrupting `settings.json` (rename it) does **not** crash the app on startup — it should fall back to defaults.

## Test 4 — Unpack workflow

1. Go to the **Unpacking** tab.
2. **Select ISO** → `vanilla_gamecube.iso`.
3. Enter a throwaway project name, e.g. `BASELINE`.
4. **Unpack!** and wait for the batch to finish.

Expected:
- Log streams the Python toolchain output; the batch file is deleted afterward (unless **Debug Options → Save BAT file** is checked).
- A `BASELINE/` folder appears in the app dir containing:
  - `BASELINE_ISO/` with the disc layout (`gladius.bec`, `fst.bin`, `appldr.bin`, `boot.bin`, `code/`, ...)
  - `BASELINE_BEC/` with `Data/`, `filelist.txt`, `unknown-*.bin`
  - `BASELINE_FileList.txt`
- No red error lines; the project appears in the tree on refresh.
- Spot-check a couple of files against `clean-bec-unpacked` (e.g. `Data/config/items.tok` exists and parses in the Item Editor).

## Test 5 — Pack workflow + validation

1. Go to the **Packing** tab.
2. **Select project** → `BASELINE`.
3. Leave **Recompile Scripts** and **Auto launch into Dolphin** unchecked.
4. **Pack** and wait.

Expected:
- Pack validation runs first (see Pack Validation below); if it reports issues they are shown and the pack is blocked unless **Skip Validation (force packing)** is checked.
- On success the toolchain runs (`Tok_Num_Update`, `tok-tool` compile, units repack, `bec-tool-all` pack, `ngciso` pack) and produces `BASELINE/BASELINE.iso`.
- Log ends clean; no hang; the output ISO is non-trivial in size (> 1 GB).

**Pack validation sub-test:** point Pack at a project you have intentionally broken (e.g. delete a `.tok`/units file) and confirm validation reports a failure rather than silently packing. Then use **Skip Validation** to confirm the force path still completes (this is the documented escape hatch).

## Test 6 — Core discipline smoke (`.mod-history` backups)

1. Select the test project.
2. **Editors → Item Editor**: change one item's cost, **Save/Update Item**.
3. **Editors → Skill Editor (WIP)**: open a skill, change one value, **Save to Project**.
4. Inspect the project's `_BEC/.mod-history/` folder.

Expected:
- `.mod-history/data/config/items.tok/<yyyyMMdd-HHmmss-fff>.bak` exists after the item save.
- `.mod-history/data/config/skills.tok/<...>.bak` exists after the skill save.
- Each `.bak` is the file content **before** that write; the live file reflects the change; reopening the editor shows the change.

## Test 7 — Skill Editor (WIP)

1. **Editors → Skill Editor (WIP)**; pick a skill from the dropdown.
2. Edit a field's value in the **Skill Box** (the box is the source of truth).
3. **Save to Project**.

Expected:
- Status bar shows `Skill '<name>' saved.`
- Reloading the skill shows the change; unrelated lines are preserved.
- A `skills.tok` backup is created (Test 6).
- The editor is freeform — no schema validation errors block saving.

## Test 8 — Grid Builder

1. Select the test project in the tree.
2. Open the **Grid Builder** tab; choose an arena from the arena dropdown (e.g. `data/encounters/expanse/expansedunes/dunesstartdefault.grd`).
3. Paint a cell with a slot, then **Save**.
4. Reopen the same arena.

Expected:
- The grid renders (32×32 cells, color-coded per slot tag).
- The painted cell persists after reopen (binary `.grd` round-trip).
- Save failures (read-only file, bad path) show an error, not a silent no-op.
- Note: Grid Builder writes the `.grd` directly and does **not** create `.mod-history` backups — see Known behaviors.

## Test 9 — Randomizer (seeded, reproducible)

1. **Randomizer** tab; **Select project** → the disposable project.
2. Enter a seed (e.g. `42`), pick a few options (e.g. **Randomize enemy items**), **Randomize!**.
3. Re-unpack the project fresh, run again with the same seed + options.

Expected:
- Status + log update as each stage runs; log colors follow conventions (green=success, red=error, orange=warning, blue=info).
- With the same seed on a fresh unpack, the output is **identical** (reproducibility).
- Output files change as expected (e.g. `data/units/gladiators.txt`, `data/units/itemsets.txt`).
- Note: the Randomizer writes files directly and does **not** create `.mod-history` backups — see Known behaviors.

## Test 10 — Team Builder

1. **Team Builder** tab; complete **Rules Selection** (Campaign/PvP, team level).
2. In **Unit Selection** add a unit; configure **Gear Selection** and **Skills Selection**.
3. **Add all to team**.

Expected:
- The team is written into the project's unit files without crashing.
- The project tree / editors still load the project afterward.
- Any validation warning (e.g. branded ISO / PvP note) is surfaced but does not crash.

## Test 11 — Patching tab (optional; needs two ISOs)

1. On the **Patching** tab, provide a vanilla ISO and a modded ISO and **Create xDelta!**.
2. On a fresh vanilla copy, load the xDelta and **Apply Patch!**.

Expected:
- The `.xdelta` is created at the chosen output path.
- Applying it produces a patched ISO that matches the modded ISO (spot-check file size/compare).
- xDelta path + ISO pickers validate existence and report clean errors for missing files.

## Test 12 — Error / edge cases

1. **Missing Python**: rename `python` out of PATH (or point the toolchain at a broken interpreter) and attempt an unpack → red error lines, app does **not** hang or crash.
2. **Paths with parentheses**: try a project or ISO path containing `(` / `)` → the app warns/fixes via `PathHelper` (Python batches break on those characters).
3. **Missing files**: rename `lookuptext_eng.txt` in the project and edit an item display name → the Item Editor surfaces the lookup-text save error rather than claiming success.
4. **Cancel / stale state**: switch projects while an editor is loading → no cross-project bleed (dropdowns and logs belong to the newly selected project).

---

## Known behaviors (do not file these as bugs)

- **Randomizer** and **Grid Builder** write files directly and bypass `.mod-history`. This is tracked legacy debt (PLAN Phase 6 / migration) — the Core APIs and phase 2.6 checklist cover the migrated paths.
- **Auto launch into Dolphin** can fire even when unchecked if a Dolphin path is configured (`bugs.txt` item 1).
- Python batch scripts break on `(` / `)` in paths; the app warns/fixes at several entry points.
- ~113 build warnings (nullable analysis in legacy `frmMain_partials`) are expected; there is no analyzer gate.

## Final checklist

- [ ] Test 1 — launch, app dir, Info version, update check
- [ ] Test 2 — project detection (+ `_ISO`-only folder skipped)
- [ ] Test 3 — settings persist across restart; corrupt settings don't crash
- [ ] Test 4 — unpack produces `_ISO` + `_BEC` + file list, no red errors
- [ ] Test 5 — pack produces `.iso`; validation blocks then force-packs
- [ ] Test 6 — `.mod-history` backups created on Core-backed saves
- [ ] Test 7 — Skill Editor load/edit/save round-trips
- [ ] Test 8 — Grid Builder load/edit/save round-trips
- [ ] Test 9 — randomizer runs, seeded reproducible
- [ ] Test 10 — team builder writes without crashing
- [ ] Test 11 — xDelta create/apply (optional)
- [ ] Test 12 — error/edge cases behave cleanly
