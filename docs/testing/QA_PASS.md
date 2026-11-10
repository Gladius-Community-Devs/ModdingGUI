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
| D11 | Fixed | Randomizer tab layout: the options box was 590 px tall for ~10 checkboxes, so Randomize, Reset to Vanilla and the baseline status floated in its dead space. The box is content-sized now, the two buttons and the status share a row beneath it, and the redundant sentence was dropped. Verified: a seeded run still completes (~8 s) and Reset restores 960 files |
| D12 | Fixed | Patching log printed `Adding mod node: ...` / `Successfully received file list JSON.` | Now "Loading the patch list.", "Patch list received.", "Available patch: X" |
| D13 | Fixed | 134 `MessageBox.Show` call sites; only automation mode covered them | The editors report validation in a non-blocking strip (`InlineNotice`) plus the log: item cost/level/affinity/stat/duplicate/nothing-changed and the Skills name check. Dialogs remain for failed writes, confirmations and the validation report, which need a decision rather than a glance; the other ~120 sites are outside the editors |
| D14 | Fixed | Unit Selection page was the old absolute layout: fields and buttons hugged the far left, 130 px of nothing, then the preview offset at x=365 with its own nested insets, and nothing resized with the window | Reparented (not recreated, so every handler and control name still applies) into an "Add a unit" panel and a preview grid that fills the page: 760x882 preview, 0 controls past the edge at 1366x768, and Add/Remove still work |
| D15 | Fixed (see D23) | Driver `set` writes to hidden/disabled controls without complaint (it drove a retired field during this pass) | `set`/`click` refuse with a reason |
| D16 | Fixed | Randomizer and Patching flows not exercised (destructive / needs real patch files) | Both exercised: randomizer is deterministic and resettable (�6b); xDelta created from vanilla ? packed ISO (6.9 MB, 12.6 s) and applied back to a copy of vanilla (5.9 s) producing an ISO **byte-identical** to the packed one |
| D19 | Fixed | Randomizer: clicking Randomize with no options ticked disabled the button for the rest of the session (the early return skipped the re-enable) | Fixed |
| D20 | Fixed | Automation guard reported an OK-only message as "answered yes" | Fixed - "acknowledged" |
| D21 | Fixed | Skills editor: every control was unnamed, so nothing there was reachable by the driver, a screen reader, or hover help | Fixed - 14 controls named |
| D22 | Fixed | Driver: combo/list items were read and matched by `ToString()`, which for a bound list is the type name - the Items editor's 1,852 items were all `ModdingGUI.frmMain+ItemRow` | Fixed - `GetItemText`, i.e. what the user sees |
| D23 | Fixed | Driver: `set`/`click` silently drove hidden or disabled controls (one sweep "passed" against a retired field) | Fixed - both refuse with a reason |
| D18 | High | Packing tab: "Make a patch" was drawn inside the log pane after the page started resizing (page-level `AutoScroll` scrolled an absolutely placed button into the anchored log) | Fixed - the page only fits itself to the viewport now, and the button lives in a docked footer. Verified at 1920x1040 and 1024x700 |
| D17 | Low | Team Builder: "Load into the editor" was clickable with no rosters, producing only a dialog; explanatory paragraphs on the Rules page duplicated their tooltips | Fixed - the button is disabled until a project with rosters is selected, and the paragraphs are tooltips only |

## 6b. Round trips proved in the app (not just in Core)

| Flow | Result |
|---|---|
| Items editor: change Machera's cost 1125 ? 1126 ? save | `ITEMCOST: 1126` written, `.mod-history/data/config/items.tok/*.bak` holds the 1125 version, no other line changed |
| Items editor: change it back and save | file returns to 1125, a second backup holds the 1126 version, hash equals the pre-test hash |
| Skills editor: `SKILLLEVEL: 0` ? `3` ? save | body field written, backup created |
| Skills editor: back to `0` and save | second backup, and `skills.tok` is **byte-identical to the pre-test file** (32,219 lines, CRLF preserved) |
| Randomizer, seed `qa-seed-B` | changes `gladiators.txt` and both campaign rosters, compiles 7 campaign scripts, ~12 s |
| Randomizer, same seed again | produces **identical files** - the seed really does reproduce the run |
| Reset to Vanilla | restores all **960** files byte-exactly, twice |
| xDelta create: vanilla ISO ? packed ISO | 6.9 MB patch in 12.6 s (`xdelta3 -e -s`), log says "Patch created successfully." |
| xDelta apply: vanilla copy + patch | 5.9 s, and `Gladius_Patched.ISO` is **byte-identical** to the packed ISO |

Two operational notes for anyone scripting this:
- **A save finishes after the click returns.** Both editors write on a background task, so hashing or counting backups immediately after `click` reads the old state. Wait for the log/status, or poll until two hashes agree.
- **`waitForLog` matches lines already in the log.** Repeating a run with identical output needs a unique marker (a different seed), otherwise the wait returns instantly and the test asserts nothing.

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

| D24 | Fixed | My own edits to the designer lost the ellipsis character in four "Choose..." labels (`Choose project.`) | Fixed - plain labels without punctuation, matching Unpack/Pack |
| D25 | Fixed | `capture` photographs the whole screen region, so it can capture the user's other windows; `screenshot` renders only the app | Documented - use `screenshot` for evidence, `capture` only to check what the compositor shows |

## 10. Layout at a small window (1366x768)

Checked by geometry rather than by eye: every visible control's bounds against the client area, per tab.

| Tab | Controls past the edge |
|---|---|
| Team Builder, Editors, Grid Builder | **0** — the rebuilt pages hold up |
| Packing, Unpacking, Randomizer | 1 (`txtFileHeader`) plus the project tree, which keeps the designer's 1173 px height and runs below the window |
| Patching | 8 — the lower "Create xDelta" panel is only reachable by scrolling (D5) |

New finding:

| D26 | Fixed | At 1366x768 the project sidebar kept its 1173 px height and ran past the bottom of the window, hiding the lower half of the project list | The header docks to the top and the tree fills the rest (`UiHelp.FixSidebarLayout`); measured at 1366x768 the tree is 222x673 inside a 702 px panel |

## 11. Designed for the launch size (1505x816)

The app opens at **1505x816** (a 1220x722 tab area) but the tabs were laid out on a 1747x1212 designer canvas, so
at the size people actually get, Patching clipped its own "Create xDelta" panel and showed horizontal scrollbars.
Fixed by reflowing the tab instead of expecting a bigger window:

- Patching: the apply form is a grid (header, subtitle, two path rows, Apply) with the log filling under it; the
  right side is two docked panels - downloads tree above, creation form below with its actions in a docked strip.
- Project sidebar and the Team Builder team list: both trees were anchored but kept their design-time height
  (1173 and 977 px), so at 1080p and at the launch size they ran below the window; both are docked now.
- Randomizer and Unit Selection were reflowed in the previous rounds; the editors and Grid Builder already scale.

Measured after the change, every visible control against the client area, all seven tabs at **1505x816: 0 past the
edge**. The wiring survived the reparenting: Apply patch still answers with "Please select valid ISO and xDelta
files." when the paths are empty.

Two traps worth remembering, both hit while doing this:

1. **A docked control and a filling sibling need the right order.** WinForms docks from the *end* of the child
   collection, so a `Fill` sibling added last takes the whole area and paints over a `Top`/`Bottom` strip.
   `BringToFront()` makes it worse - it moves the control to the front of the collection, i.e. docks it last.
2. **A big-font TextBox sets its own height.** The panel headers are TextBoxes at ~26 pt; they report a
   54 px `PreferredHeight` and ignore an assigned `Height`, so a 46 px row made them overflow and paint over
   the subtitle underneath ("Make a patch to share" was clipped to a sliver). The rows are now sized from
   `PreferredHeight`, not from a number I guessed.
3. **`Control.Visible` is not "what the user sees".** Geometry against the client area caught all of this; the
   screenshots confirmed it.

## 12. UI foundation: design tokens, a card kit, and the first page rebuilt

The audit behind this section: the designer holds **161 absolute positions and 173 fixed sizes** on a **2127x1295**
canvas for a window that opens at **1505x816**, eight hand-picked fonts (three of them 26 pt TextBoxes standing in for
headings), and `Color.Red`/`Color.Blue` typed wherever a log line is written. The layout fixes in �10-�11 were patches
over that, and patching it produced its own magic numbers (18 absolute row heights). What replaces it:

- **`UiTheme`** - one palette (surface, surface-alt, sunken, border, ink, muted ink, accent, success/warning/error/info),
  four type sizes in points, and a spacing scale in device pixels (4 px base, scaled by `DeviceDpi`). Fonts stay in
  points because the framework already scales them; scaling both would double-scale.
- **`UiKit`** - the primitives a page is made of: `Card(title, caption)`, `Form(labelWidth)`, `Row`, `RowWithButton`,
  `Primary`/`Secondary`/`Browse` buttons, `Actions` strip, `Style` for logs and trees, and `CardHeight`/`HeaderHeight`
  so a card is sized from its fonts instead of a guessed row height. Everything is Dock/TableLayout; the only numbers
  are the theme's spacing steps.
- **`PatchingPage`** - the first page rebuilt on the kit and the reference for the rest: two columns of cards (apply
  form + activity log; available patches + make-a-patch), the three 26 pt TextBox headings replaced by styled labels,
  a flat accent button for the primary action. Controls are reparented rather than recreated, so names, handlers and
  the driver's scripts keep working.

**Verified** at 1505x816, 1366x768, 1280x720 and 1920x1040: 0 controls past the client edge in every case, and
`Create xDelta` still answers with "Please select valid Vanilla and Modified ISO files." when the paths are empty.

Two things the kit fixed by construction, both of which had bitten this pass already:

- a docked `Panel` has **no automatic height** - the card header kept its default 100 px and squeezed a two-row form
  until only one row showed under the button; `HeaderHeight` now derives it from the fonts;
- big-font `TextBox` headings **set their own height**, so they overflowed their rows and painted over captions;
  section headings are `Label`s with a theme font, so the class of bug cannot recur.

Next: Packing, Unpacking and Randomizer adopt the same kit, then the Team Builder pages, then the global chrome
(menu, tab strip, status) - each is a page rebuild on the primitives above rather than another patch.