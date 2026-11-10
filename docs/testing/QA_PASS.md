# App-level QA pass

Automated pass over the shipping UI, driven through the app's own window (`--ui-server`) rather than through Core.
Everything below was executed on a Debug build of `rewrite/full-app` against a project unpacked from the retail ISO
(`demo_hero`) plus the older `gold_test`, with the window placed on a secondary 1920×1080 monitor at 1920×1040.

Related: [`UI_AUTOMATION.md`](../UI_AUTOMATION.md) for the driver commands, [`MANUAL_TESTING_TEAM_BUILDER.md`](MANUAL_TESTING_TEAM_BUILDER.md)
for the parts only a human can judge (in-game behaviour).

---

## 1. How it was driven

| Tool | Purpose |
|---|---|
| `scripts/ui-drive.ps1` | One command per connection; used for one-off probes. |
| `scripts/ui-batch.ps1` | One connection for a whole scenario, per-command timings to `<out>.timing.csv`. 12 commands in 0.5 s instead of 6 s. |
| `scripts/qa/restart-app.ps1` | Stops only the instance it started (by recorded PID), builds, relaunches with `--ui-server`, places the window on a given monitor. |
| `scripts/qa/sweep-1-usage.txt` | The usage scenario in §3. |

Driver commands added during this pass:

| Command | Why |
|---|---|
| `window {monitor\|x,y,width,height}` | Put the window on a screen the user is not working on; repeat a layout check at several sizes. |
| `capture {path}` | True screen grab (`CopyFromScreen`). `screenshot` renders the control tree with `DrawToBitmap` and **can paint the wrong page** after a programmatic tab switch — `capture` is the ground truth. |
| `closeDialog` | Close a blocking dialog and report its title/body. |
| automation dialog guard | With `--ui-server`, native message boxes are dismissed as they appear and logged as `[automation] dialog dismissed: <title>: <text>`. Without it, the first `MessageBox.Show` inside a handler hangs the run forever, because the call that raised it is still on the stack. |

---

## 2. First-run state (nothing selected)

| # | Step | Expected | Result |
|---|---|---|---|
| 1 | Launch with no project selected | Window usable, no dialog | Pass |
| 2 | Click **Pack** with nothing selected | Refuses, says why | Pass — the button is disabled, and a click reports `'btnPack' is disabled` |
| 3 | Open **Team Builder** | Rule page usable, no project dialog | **Fail → fixed**: it used to show the retired PvP-era page until a project was chosen |
| 4 | Open **Editors** with no project selected | No dialog | **Fail → fixed**: it popped a modal "Select a Project" picker on *tab selection*, blocking the window. Now it selects the first project and logs `Editing demo_hero…`, or says `No projects yet — unpack an ISO…` |
| 5 | Patching tab | See all three panels | **Fail → fixed at 1080p**: content was clipped, "Create xDelta" unreachable, no scrollbar. Split panels now scroll |

## 3. The main loop: unpack → edit → pack

| # | Step | Result |
|---|---|---|
| 6 | Unpack a retail ISO | Pass — 21.5 s, 1.81 GB, job reported `Succeeded` with per-step timings |
| 7 | Select the project in the sidebar | Pass — 262 ms; sets pack path, randomizer path, patch paths and the editors in one go |
| 8 | Pack with validation on | Pass — validation 0.8 s across 7 areas, then the pack job |
| 9 | Pack, cold cache | 136 s (`bec-tool-all.py` 129.9 s) — see `HANDOFF.md` §4 |
| 10 | Pack, warm cache | 14–16 s (`bec-tool-all.py` 8.1–10.5 s, `ngciso-tool.py` 4.3–4.9 s) |
| 11 | Pack with a bad ISO path (Unpack tab) | Pass — `Invalid File: Please select a valid ISO file.` |
| 12 | Team Builder → Load roster | Pass — `Loaded 3 unit(s) from 'valensimperia.tok'` |
| 13 | Add a unit, then open Gear | **Fail → fixed**: the new unit was not selected, so Gear still showed "No units yet" and **Remove Unit** stayed disabled. Now the unit is selected and Gear reports `83 of 83 legal items` |
| 14 | Type an out-of-range level (99) and click Add | **Fail → fixed**: the level box silently discarded the text, then Add refused with `Invalid Level: Level must be between 1 and 30; got 0`. Now it clamps and logs `Level 99 is outside 1–30; using 30.` |
| 15 | Add a unit with an empty name | `Unit Name: A unit name is required.` — clear message, but a modal for a one-field error. Acceptable; inline validation would be better |
| 16 | Save team to the campaign roster | Pass — `1 added, 0 updated, 2 unchanged`, diff limited to the new block |

## 4. Editors

| # | Step | Result |
|---|---|---|
| 17 | Items editor first open | Pass — `Loaded 1852 items, 55 item skills` in well under a second |
| 18 | Skills editor first open | Pass — 758 ms (catalog load), subsequent tabs fast |
| 19 | Switch item/skill/grid sub-tabs repeatedly | Pass — no stale content, no exceptions in the log |
| 20 | Grid Builder | Exists as its own top-level tab, not a child of the editors tab (my sweep addressed it wrongly; the app is right) |

## 5. Text audit

Rule applied: the label says *what*; the tooltip says the one thing a label cannot fit. 55 strings shortened, ~70
controls given hover help (`frmMain_partials/UiHelp.cs`).

| Was | Now |
|---|---|
| `Project filepath will show up here! (SELECT ONE TO CONTINUE)` | placeholder `No project selected` |
| `Get Vanilla ISO Path` / `Get Modded ISO Path` / `Get xDelta Path` ×4 | `Browse…` |
| `Select project` | `Choose project…` |
| `Unpack!` / `Randomize!` / `Apply Patch!` / `Create xDelta!` | `Unpack` / `Randomize` / `Apply patch` / `Create xDelta` |
| `For applying xDeltas to a vanilla ISO` | `Apply a patch to a clean ISO` |
| `Weighted vs Full Random` | `Weighted picks` (tooltip: weights random picks by rarity) |
| `40 random units` | `40-unit roster` (tooltip: needs the gecko code) |
| `Custom Start Cash` | `Custom starting gold` |
| `Randomize enemy items` vs `Random enemy statsets` | `Random enemy items` / `Random enemy stat sets` |
| `Item Cost:` / `Item Level:` / `Item Mesh:` / `Item Affinity:` / `Item Skills:` / `Item Material:` | `Cost:` / `Level:` / `Mesh:` / `Affinity:` / `Skills:` / `Material:` |
| `First, let's start with some rules:` | `Rules` |
| `Input a team level. (All units added will have this level):` | `Level for new units:` |
| `Enter unit name:` / `Select a class:` / `Base Stats:` / `Attributes:` | `Name:` / `Class:` / `Stats:` / `Traits:` |
| `Project File Tree:` | `Projects` |
| three-paragraph randomizer explanation on screen | `Each run restores the vanilla baseline first.` (full text in the tooltip) |

Tooltips that carry the depth instead: what a validation skip means, what "recompile scripts" actually does, why
`bec-tool-all.py` is the slow step, 40-gladiator requirements, seed reproducibility, what an item's mesh/affinity
affects, what the campaign rosters are.

## 6. Defects found

| ID | Severity | Area | Status |
|---|---|---|---|
| D1 | High | Driver: `set` on a TabControl reported `ok` without switching tabs; `screenshot` then painted the *previous* page | Fixed — `capture` added, `screenshot` repaints first |
| D2 | High | Driver: snapshot reported the previous tab's page as visible | Fixed — visibility walks the selected-tab chain |
| D3 | High | UX: selecting the Editors tab popped a blocking "Select a Project" modal | Fixed — picks the first project or says there are none |
| D4 | High | Automation: any `MessageBox` inside a handler hangs a driven run forever | Fixed — automation dialog guard logs and dismisses; `closeDialog` |
| D5 | Medium | Layout: Patching tab clipped at 1080p, "Create xDelta" unreachable | Fixed — split panels scroll |
| D6 | Medium | Team Builder: level box silently discards unparseable text, then reports `got 0` | Fixed — clamp + log |
| D7 | Medium | Team Builder: rebuilt pages had unnamed controls (invisible to snapshot, accessibility, tooltips) | Fixed — naming pass |
| D8 | Medium | Team Builder: retired PvP-era Rules page shown until a project was selected | Fixed — built on tab entry |
| D9 | Medium | Team Builder: new unit not selected, so Gear/Skills/Remove did not apply to it | Fixed — selects the new node |
| D10 | Low | 55 labels verbose or jargon; vague placeholders | Fixed — see §5 |
| D11 | Open | Randomizer tab layout: option list cramped in the left third, "Reset to Vanilla" floats far from the options, no visible inputs for the two custom-amount options | Needs a layout pass |
| D12 | Open | Patching log prints `Adding mod node: …` / `Successfully received file list JSON.` | Log copy pass |
| D13 | Open | 134 `MessageBox.Show` call sites; only automation mode covers them | Route through one helper over time |
| D14 | Open | Unit Selection page is still the old absolute layout | Cosmetic |
| D15 | Open | Driver `set` writes to hidden/disabled controls without complaint (it drove a retired field during this pass) | Should fail loudly |
| D16 | Open | Randomizer and Patching flows not exercised (destructive / needs real patch files) | Add to a later sweep |
| D18 | High | Packing tab: "Make a patch" was drawn inside the log pane after the page started resizing (page-level `AutoScroll` scrolled an absolutely placed button into the anchored log) | Fixed - the page only fits itself to the viewport now, and the button lives in a docked footer. Verified at 1920x1040 and 1024x700 |
| D17 | Low | Team Builder: "Load into the editor" was clickable with no rosters, producing only a dialog; explanatory paragraphs on the Rules page duplicated their tooltips | Fixed - the button is disabled until a project with rosters is selected, and the paragraphs are tooltips only |

## 7. Performance (this machine, Debug build)

| Operation | Time |
|---|---|
| Tab switch (any main tab) | 90–110 ms |
| Team Builder tab entry (builds rules page first time) | 110 ms |
| Select a project in the tree | 262 ms |
| Snapshot of the whole form (162–400 controls) | ~110 ms |
| Items editor first open | <1 s (1,852 items) |
| Skills editor first open | 758 ms |
| Pre-pack validation (7 areas, vanilla) | 0.8–1.7 s |
| Add unit (rebuilds preview + JP budget) | 190–230 ms |
| Pack, warm | 14–16 s |
| Pack, cold | 136 s |
| Unpack 1.4 GB ISO | 21.5 s |

## 8. What this pass did not cover

- In-game behaviour (does the packed ISO boot, does the team appear) — needs Dolphin; see `HANDOFF.md` §6.
- Randomizer end-to-end and xDelta apply/create — destructive or needs real patch artefacts.
- Modal-heavy paths by hand: resolution is 1080p only, no DPI scaling checks (125 %/150 %), no keyboard-only navigation,
  no dark theme.

## 9. Suggested next steps

1. Randomizer tab reflow (D11) and the two missing custom-amount inputs.
2. Drive every `MessageBox.Show` through one helper (D13) so validation feedback is inline and testable.
3. Layout checks at 125 %/150 % DPI and at 1366×768 (the smallest plausible laptop screen).
4. Make `set` refuse invisible targets (D15) and add a `waitFor` predicate, so scenarios stop needing `closeDialog`.
