# Manual QA — Phase 6 through the app (end to end)

This is the GUI pass. It checks that the randomizer panel drives the Core pipeline correctly, that the files on
disk are right, and that the packed ISO boots. The in-game checks live in `MANUAL_TESTING_PHASE_6.md`; run those on
the ISO this produces.

## Prerequisites

- The app built `Release` (or the published exe), with the `tools/` folder beside the executable
  (`DOGCodeCompiler.exe`, the Python toolbelt).
- A fresh unpacked project: `<name>_BEC` + `<name>_ISO`. **Start from a clean unpack**, not a project that has
  already been randomized — several steps are idempotent but the seed sweep is only meaningful on a clean tree.
- Close any previous instance; the pack step holds files open.

## 1. Launch

1. Start the app.
2. First run may show **"Settings File Created"** — dismiss it. (Seen in the smoke test; expected.)
   - [ ] Main window appears, Randomizer tab is reachable.
   - [ ] Options menu no longer shows **Randomizer Testing** (retired with the old pipeline).

## 2. Point the panel at a project

3. Randomizer tab → browse / type the project folder → the path is accepted and Pack gets enabled.
   - [ ] `txtRandomizerPath` shows the project, no error dialog.

## 3. Configure a full run

4. Set:
   - Seed: `phase6-gui`
   - Campaign: **Valens**
   - Version: **Vanilla**
   - School name: leave empty (should become "Random's School")
   - Cash: tick **Custom**, enter `25000`
5. Tick: **Heroes**, **Team**, **No Recruits**, **Statsets**, **Itemsets**, **Randomize Enemies**, **40 glads OFF**,
   **Weighted OFF**, **Perma-death OFF**.
   - [ ] Every checkbox stays ticked after clicking elsewhere (settings round-trip on selection change).

## 3b. Baseline behaviour

- [ ] Before any run, the status line beside the button reads Baseline: not pinned yet (the first run pins it).
- [ ] After the first run it reads Baseline: pinned (N files) - every run starts from it.
- [ ] Running a second time logs Restored the vanilla baseline (N file(s)) and the result is byte-identical to
      the first run (same seed).
- [ ] **Reset to Vanilla** asks for confirmation, restores, and logs Reset to vanilla: N file(s) restored.
- [ ] After a reset, the rosters and gladiators table match a fresh unpack, and the reset itself left a .bak per
      restored file.

## 4. Run it

6. Press **Randomize**. Watch the pack output.
   - [ ] Log shows `Randomizing with seed 'phase6-gui'.`
   - [ ] Core lines appear while it works: `Randomizer run started…`, one `… changed` per step.
   - [ ] Final lines: `Seed: phase6-gui`, `Steps: heroes, scripts, statsets, itemsets, team, encounters, recruits`,
       `Records changed: <n>`, `Compiled 6 campaign script(s).`
   - [ ] The UI stays responsive; the status label returns to `Ready`.
   - [ ] A dialog offers to go to the Pack tab.
   - [ ] The menu entry **"Game is Randomized!"** appears in the game list.
7. Close the app **without** packing, then check the disk:

| Check | Expected |
|---|---|
| `<proj>/.mod-history/data/units/gladiators.txt/` | exactly **1** new `.bak` (not one per record) |
| `.mod-history/data/school/valensimperia.tok/` | 1 `.bak` |
| `.mod-history/data/encounters/…` | one `.bak` per changed encounter |
| `.mod-history/data/script/gameflowvalenssteppes.scp/` | 1 `.bak` |
| `<proj>_BEC/data/school/valensimperia.tok` | `NAME: "Random's School"`, `HERO: "Valens"`, `GOLD: 25000`, 20 `CREATEUNIT` blocks |
| Every block in both rosters | `CUSTOMIZE: 0, ""`, six values in `CORESTATSCOMP2` |
| `<proj>_BEC/data/script/bin/*.scb` | the six flow scripts have fresh timestamps |
| `<proj>_BEC/data/encounters/imperia/pirgosarena/battle1.enc` | the `TEAM: -1` block is unchanged from vanilla, `SCHOOL: "PropSchool"` still there |

8. Re-run with the **same seed** on a fresh unpack and diff `gladiators.txt` + the two rosters.
   - [ ] Byte-identical. (This is the reproducibility guarantee; a different seed must differ.)

## 5. Pack and boot

9. Pack tab → Pack. Optionally tick **Recompile Scripts** (belt and braces; the randomizer already compiled them).
   - [ ] Pack finishes with exit 0 and an ISO is written.
10. Launch the ISO (Dolphin), New Game, **Valens**.
    - [ ] Reaches playable state, no hang.
    - [ ] School lists all six heroes + 14 `<name>_random` units.
    - [ ] Every unit has gear; archers have bows.
    - [ ] First battle fields your school; enemies are random with no props/bosses.

## 6. Selective runs (the options must be independent)

11. Fresh unpack, tick **only Statsets**, run.
    - [ ] Log steps: `scripts, statsets, encounters` (no heroes/team/itemsets/recruits).
    - [ ] Team-0 encounter rows are still opened (the encounter step always runs).
12. Fresh unpack, tick **only Heroes**, run.
    - [ ] Six distinct hero classes in both rosters; Ludo keeps 450 experience.
13. Fresh unpack, tick **only Randomize Enemies**, run.
    - [ ] Enemy rows become randomizable slots; player-team slots opened; no CANDIE lines added.

## 6b. Project context menu

- [ ] Right-click a project → it becomes the selected node (highlighted).
- [ ] **Delete Project** asks for confirmation, shows the progress dialog, and the project disappears from the tree.
- [ ] Delete a **second** project immediately after, without clicking anything in between.
- [ ] Delete a third while a fourth is selected — the delete acts on the one right-clicked, not the selected one.
- [ ] Cancel during a delete: the dialog closes, the project stays.
- [ ] Right-click **empty space** below the tree → Delete/Load/Copy say "Right-click a project first" instead of
      doing nothing.
- [ ] **Launch with Dolphin** and **Copy Project** act on the right-clicked project too.

## 7. Failure table

| Symptom | Most likely cause |
|---|---|
| `Invalid project folder selected` | `_BEC`/`_ISO` missing, or a path with parentheses the toolbelt dislikes |
| `Nothing selected` | every option is off — by design |
| `Script compilation failed` | `tools/DOGCodeCompiler.exe` missing beside the exe |
| Hang at new game | a roster block asking for a variant that does not exist — should be impossible now (`CUSTOMIZE 0`) |
| Unit with no gear | an item set with no level-1 gear was assigned — should be impossible now |
| Two backups for one file | a step ran twice in one press; report it |
| Steps list missing `scripts` | the run aborted before the script step; check the log for the exception |
