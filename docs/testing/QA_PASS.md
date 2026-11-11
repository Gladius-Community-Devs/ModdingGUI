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

### 12b. Dark by default, light on request

The palette became a *setting* rather than constants:

- **`UiTheme`** holds two palettes (`Dark`, `Light`) as records and exposes colours as properties over the current
  one, so `UiKit`'s cards and the log colours follow the scheme instead of being baked in. `UiTheme.Use(scheme)` swaps,
  `UiTheme.Apply(root)` repaints an existing window in place, and `AppSettings.Theme` (`"dark"` default) remembers it.
- **Options ? "Dark theme"** toggles it (`CheckOnClick` off: the checked state mirrors the scheme, so a scripted click
  cannot leave the menu lying about which palette is in use). It is created in code, next to the theme, so a third
  scheme is a palette plus a radio item rather than a designer edit.
- **Chrome** needed more than a BackColor: `UiColorTable`/`UiMenuRenderer` theme the menu, and the tab strips are
  owner-drawn (selected tab bold, accent underline) because the system paints them otherwise.
- **Log panes are re-rendered** on a switch. A RichTextBox keeps the colour of spans it has already drawn, so after
  toggling, old lines stayed in the old palette - dark-theme blue on a white page. The panes are rebuilt from
  `IAppLog.Tail`, which has the level for every entry. Side effect worth knowing: a pane shows the whole recent stream
  after a switch, not only the lines it received while it was visible.
- **Primary buttons** are registered with the theme (`UiKit.Primary` / `UiKit.Look(..., primary: true)`) so a repaint
  keeps them accent-coloured instead of demoting them to secondary grey.

Verified by driving the toggle twice and reading the log: `Theme: light.` then `Theme: dark.`, with both screenshots at
the launch size.

## 13. Page-by-page UX pass (dark default)

Two driver commands were added so this is repeatable instead of an opinion:

- **`{"cmd":"audit","contrast":4.5}`** - walks the visible control tree and reports every control whose text/background
  contrast is below the threshold, plus any control still painted with a *system* colour while the dark palette is
  active (the latter is what leaves a white box or a pale band in a dark window).
- **`{"cmd":"hit","x":N,"y":N}`** - names the deepest control at a point with its colours and bounds; this is what
  identified the pale tab strip as the TabControl's own client area rather than mine.

Result at 4.5:1 across all thirteen reachable pages (7 top tabs, 4 Team Builder pages, 2 editor pages, Grid Builder):
**no failures**. The dimmest text was muted ink at **5.7:1**, so the palette was raised to AAA instead of just passing -
`InkMuted` is now `#AEB8C6` on dark (`#4A5563` on light), which measures ~7.4:1 on the card surface.

**Tab strip.** The pale run right of the last tab was Windows painting the tab control's own strip. It survived
`SetWindowTheme` (visual styles off), painting over it in `WM_PAINT`, and `TabSizeMode.FillToRight` (verified with a
magenta diagnostic fill that never appeared). The native row is now collapsed to 1x1 and **the strip is ours**: a
`FlowLayoutPanel` of flat buttons above the pages, selected one bold on the surface colour. No band, and the strip can
hold controls later (the theme toggle is the obvious candidate).

**Colour literals.** 25 direct assignments (`Color.White`, `SystemColors.Window`, `SystemColors.GrayText`, `Color.Black`
on team-level labels, `Color.FromArgb(240,240,240)` in Grid Builder) were replaced with palette references, so the
scheme reaches them too.

**Pages rebuilt on the kit:** Patching (section 12), Unpacking and Packing - one card each: what to work on, the options
that change the outcome, the action, and the activity log filling the rest. Before, Packing was a path box, three
loose checkboxes and a log, with most of the page empty at the launch size.

**Known gaps this pass found but did not fix** (both are behaviour, not colour):

1. **Disabled inputs ignore BackColor.** A disabled `TextBox` and a disabled `Button` are drawn by Windows in system
   colours, so they read pale in a dark window whatever the palette says. The Randomizer page's path/school-name boxes
   and its Status row are the visible cases. Fixing it means either `ReadOnly = true` instead of `Enabled = false`, or
   owner-drawing those controls.
2. **Randomizer, Team Builder, Editors and Grid Builder still use the designer canvas** - they are themed but not
   reflowed, so their spacing does not follow the window the way Patching/Packing/Unpacking now do.

## 14. Feedback round: sizing, retirement, dialogs

**Button and field sizing.** Browse buttons were a few pixels shorter than their field and sat on a different
baseline; a path box also stretched to whatever the card was wide (1060 px in Packing). The kit now has one metric -
`FieldHeight` (30 px, DPI-scaled) - used by both the field and its button, and `FieldWidth` (420 px) applied as
`MaximumSize` so a field fills a narrow cell (the Settings dialog is ~250 px) and stops at the cap in a wide one.

**"Ingame Random" tab.** It was removed by `Form_Load`, which runs *after* the strip is built, so the strip kept a
button for a page that no longer existed. The strip now rebuilds when pages are added or removed, deferred to
`BeginInvoke` because the removal notification arrives while the page is still in the collection.

**Unpacking.** "Choose ISO" is now the button of the ISO row instead of a separate row with an empty caption, and
Unpack/Open folder share one height. **Packing:** "Make a patch" is always enabled - switching tabs never needed a
project - so it is no longer a dim, unreadable disabled button.

**Settings dialog.** Rebuilt on the kit: each page is a grid of caption/field rows with the checks beneath, the
OK/Cancel pair is a docked action strip, and the tab headers are the same themed strip as the main window. The form
now calls `UiTheme.Apply`, so fields are dark too. The absolutely-positioned "leave blank" hint label that overlapped
the reflowed grid is gone; the caption carries the note.

Because a modal blocks the UI driver (`ShowDialog` runs its own message loop), the dialog gets a screenshot through a
new switch: `ModdingGUI.exe --render-settings <png>` draws it off-screen and exits. `--render-settings` is the pattern
for any dialog that cannot be captured while the driver is attached.

**Menu.** The system check glyph and selection plate are replaced by our own renderer: flat hover plate, accent
underline on an open top-level item, a hand-drawn check mark, themed separators, and text that stays readable in every
state. Popup menus are separate windows, so this has no automated screenshot - it needs a human look.

## 15. Alignment, the Randomizer page, and a docs sweep

**Browse vs field.** The buttons were taller than their boxes and a pixel lower. Cause: a single-line `TextBox` ignores
an assigned `Height` (its height comes from the font), so a button forced to the kit's 30 px metric could not match. The
metric is now measured from a probe text box in the body font (`FieldHeight`), and both the field and its button are set
to it. Verified by snapshot rather than by eye: `txtPatchingApplicationISOPath` and `btnPatchingApplicationISOPath`
both report `y=143 h=25`, and likewise on every other path row.

**Randomizer page.** Rebuilt on the kit as three cards: *What to randomize* (the ten option checkboxes), *Starting
setup* (hero, game version, school name) and *Run* (project folder + Choose, seed, actions, baseline note). The two
floating status labels and the five fixed-position group boxes are gone, the Seed box is `ReadOnly` rather than
disabled so it stays on theme, and `UiHelp.FixRandomizerLayout` - the old patch that positioned those floats - is
deleted rather than left to fight the new layout.

One thing is deliberately hidden: the `ProgressBar`. A WinForms `ProgressBar` is painted by the system and stays white
in a dark window (same class of problem as disabled inputs). The control is still driven and still in the tree, with
`Visible = false` and no caption, until there is a themed bar.

## 16. Documentation sweep (same round)

- `CLAUDE.md` / `docs/AGENTS.md` / `docs/HANDOFF.md`: test count 793 -> 800; the `frmMain_partials` table now lists
  `UiTheme`, `UiKit`, `Chrome`/`Theme`, the rebuilt pages and `UiAudit`; the logging note says severity colours come
  from the palette and that the panes re-render on a scheme change.
- `docs/HANDOFF.md` gained section 8, "The interface layer", covering the tokens, the kit, which pages are rebuilt and
  which are not, the tab-strip reason, the Settings dialog and `--render-settings`.
- `docs/UI_AUTOMATION.md` gained the `audit` / `auditAll` / `hit` commands with their response shapes, and the
  `--render-settings` pattern for modal dialogs (they block the driver, which is why the section exists).

## 17. How styling is validated (and what slipped through)

The Randomizer shipped with three empty designer group boxes, two stale captions and a repeated "Status" heading, all
themed and all passing the audit. Two real gaps, now closed:

**Gap 1 - the audit only looked at text colour.** Empty frames have no text of their own; "Options" as a group caption
has perfectly good contrast. `audit` now also returns `layout` findings for the pages rebuilt on the kit (Patching,
Unpacking, Packing, Randomizer):

- `empty-container` - a visible `GroupBox`/`Panel` with a caption and no visible children, i.e. its contents moved out
  and the frame was left behind;
- `outside-card` - a visible label/button/field on a migrated page whose ancestors contain no card, i.e. a leftover
  rather than part of the new layout.

Run across all thirteen pages it now reports **contrast 0 / layout 0** on every one.

**Gap 2 - a local layout function created controls.** The "Status" heading appeared five times because the block that
adds it had been pasted inside `FitRun()`, which runs on every layout pass: each pass added another heading and
re-parented the status label. Rule for the kit: **a layout callback may only size things, never create them**. The
duplicate was found by reading the file, not by the audit - worth remembering that an audit only finds the classes of
problem it knows about.

Two more defects came out of the same sweep:

- the primary button failed AA: white on `#3D7DD8` measures **4.1:1**. Accent darkened to `#3468B5` (**5.4:1**);
  `AccentHover` keeps the old brighter value so hover still reads as a change;
- the Editors page had an **unnamed white `TextBox` with black text** in every audit run. It was created lazily by a
  row builder long after `UiTheme.Apply` had walked the tree. `UiTheme.Watch(root)` now themes controls as they are
  added, which covers every editor that builds rows on demand.

The verification loop for a page is therefore: build clean -> `snapshot` geometry against the client area ->
`audit` (contrast **and** layout) -> `screenshot` -> toggle the theme and check the same page -> suite green.

## 18. Option switches, the strip artefact, and Team Builder starts

**Checkboxes on dark.** A `CheckBox` glyph is drawn from the system theme; no colour property reaches it, so on a dark
surface it reads as a pale square. `Appearance = Appearance.Button` renders the same control as a flat toggle - same
instance, same handlers, same `Checked` - tinted accent when on. Applied to the Randomizer's ten options only, as
full-width rows; forcing it app-wide would restyle every checkbox in Team Builder and the editors before those pages
have had their own pass. `UiTheme.TintToggle` is the shared tint; pages opt in.

**The artefact under the tab strip.** Two separate things in the same band: the bright hairline was the TabControl's
own themed frame (Windows draws it in the system colour), removed by turning visual styles off for that control alone;
the pale text fragment was a stale designer caption from the old layout, retired with the other containers. Confirmed
by cropping the band at 6x rather than guessing from a full screenshot.

**Team Builder, first pass.** The Rules page is now cards: `TeamBuilderPages` replaces each `GroupBox` in place - the
card takes the group's table cell, the group's children move into the card body, names and handlers untouched - so five
pages convert without rewriting their logic. Two traps, both caught by looking:

- the conversion has to run where the pages are *built* (`InitializeTeamBuilderUI`), not in the constructor: Team
  Builder creates its pages lazily, so at construction there was nothing to convert;
- the card height has to be measured **before** the children are detached. `PreferredSize` is derived from children, so
  reading it afterwards reports the empty group and every card comes out short, clipping its rows.

Unit Selection, Gear and Skills show an empty state until a unit exists, so their cards need a unit in the editor before
they can be checked. Editors and Grid Builder are untouched.

## 19. Randomizer polish, remnants, and what "(randomized)" means

**Row alignment.** `Row`/`RowWithButton` put the caption in one grid column and the field in the other, so the two only
lined up while both rows agreed - which is why the Unpacking page showed "Project" under its own box. Each row is now a
single `FlowLayoutPanel` holding caption, field and button, sized from `FieldHeight`, so a caption cannot drift from its
field.

**Option switches.** `Appearance.Button` draws the checked face from `FlatAppearance.CheckedBackColor`, not `BackColor`;
leaving that at the system default is why the list showed a pale blue row, a grey row and a light blue row while moving
through it. Every state is set in `UiTheme.TintToggle` and the rows no longer take focus rectangles.

**The gold amount.** It was a designer text box at an absolute position, so it drew beside whichever row happened to be
at those coordinates. It now lives in a host panel under its own option, with the host collapsing when the amount is
hidden - the amount box is only shown while the option is on (the shell already did that; the heading and the spacing
now follow the same flag).

**The remnant under the tabs, finally.** `tabTeamBuilderMaster` is created lazily by the Team Builder, *after* the
chrome pass, so its native tab row and frame were still drawn: that was the pale text fragment plus hairline. The strip
is now attached to nested tab controls after the Team Builder builds its pages, and the frame fix moved into
`UiKit.AttachTabStrip` so any control it attaches to loses the system frame.

**"(randomized)" was derived, and slightly wrong.** It tested whether `.mod-history/_randomizer-baseline` exists. Reset
to Vanilla restores *from* that folder and keeps it (`RandomizerBaseline.Restore` copies files back, backs up the current
ones, deletes nothing), so a project that had been reset still showed "(randomized)". It now compares file contents
against the pinned baseline, so the marker means what it says: this project differs from its baseline.

**New driver command: `children`.** `{"cmd":"children","name":"pnlRandomGold"}` lists a container's children in order
with type, name, text and visibility. It settled a duplicated-heading argument I had been having with a screenshot, and
proved the amount box was `visible=false` by design rather than missing.

**A note on tooling.** Twice in this round a `string.Replace` insertion landed in a *second* copy of the same line - once
inside the `FitRun` local function, once in the options loop - producing duplicated headings. Line-based edits or the
`edit` tool with unique context are the safe way to change code in this file; `Replace` is only safe for a string known
to occur once.

## 20. The tab-row remnant: root cause at last

The pale text row under the tabs was the **native `TabControl` row**, and the reason it never went away is
`ItemSize` clamping: WinForms clamps `ItemSize` to the font height while the control is still in its normal fixed
mode, so `new Size(1, 1)` became roughly `(48, 24)` and a second row of tab buttons was drawn *underneath* our strip -
same labels, so it read as "bits of something" rather than as a duplicate strip.

Three things were tried and measured before the fix that worked:

- owner-draw with an empty `DrawItem` (kept, and correct) - did not shrink the row, because the row was already 24 px;
- setting `ItemSize` after `DrawMode = OwnerDrawFixed` (kept) - still clamped by this control;
- painting diagnostic colours to find the owner (no effect, which is what ruled out our strip).

What works: **clip it**. `UiKit.AttachTabStrip` slides the `TabControl` up by its own `DisplayRectangle.Top` and grows it
by the same amount, so the native row sits above the strip and the page area begins exactly at the strip's bottom edge.
Anchors keep that true across resizes. Verified on the Team Builder sub-tabs (`tabTeamBuilderMaster`) and the main
`tabContainer`: `tabContainer` reports y=56 with its page at y=61, so nothing is clipped from the page itself.

Lesson worth keeping: a screenshot that looks "clean" at 100 % can hide pixel-level artifacts; cropping the band at 4-5x
was the only way to see whether a change had any effect - and two of my earlier "fixes" looked done in the full image
while the cropped band was byte-identical.

## 21. Tab row, final: exclusive Region

Shifting the `TabControl` up to clip its native row made the bar *worse* rather than better: the control sits in front of
the docked strip in z-order, so the native tab labels were drawn **over** our strip instead of being hidden above it.

The complete fix is a `Region`: exclude the control's own tab-row band (`DisplayRectangle.Top`) from the region it paints,
so Windows never draws those labels, and the page area below keeps its full size. The region is rebuilt on
`Resize`/`DisplayRectangle` changes. No overlap, no z-order juggling, no shifting - and it applies to every attached tab
control, nested ones included, which is why "all tabs" now show exactly one row.

Checked the way this should have been checked all along: crop the band at 4x and compare hashes between builds. The
Patching band shows the labels and then the page edge - no second row.

## 22. Grid Builder: themed, and driveable by an agent

**Styling.** The panel painted itself in system colours (`Color.White` background, `Pens.Black` grid lines, `LightGray`
empty state, `Color.DarkBlue` drag rectangle) and its tool strip was the one light band in a dark window. Now:

- tool strip uses `UiMenuRenderer` with the palette, and its combo boxes and check box are themed;
- the grid clears to `SurfaceSunken`, draws grid lines in `Border`, shows an empty state in `InkMuted`, errors on
  `SurfaceAlt` with `Error` ink, and drag selection in `Accent`;
- the cell under the pointer gets a 2 px `Accent` outline - on a dark surface the painted colours alone do not say where
  the cursor is;
- the commands panel was 90 px wide (one word per line) because `SplitterDistance` was being read as a height in a
  vertical split; it is 300 px with a 220 px minimum, and both text panes use the palette fonts.

**Automation.** `GridBuilderView` now exposes `AutomationState`/`AutomationCell`/`AutomationSetTag`/`AutomationClearCell`/
`AutomationSelectSlot`/`AutomationLoadArena`/`AutomationSave`/`AutomationSetMap`, and the driver exposes them as the
`grid` command (see `docs/UI_AUTOMATION.md`). Verified end to end on the demo project: read cell 12,7 (0) -> `setTag`
Start1 -> read back (value 2, tags `["Start1"]`) -> `clear` -> read back (0), plus arena load, slot selection and save.
Failures are actionable: an unknown tag returns the tags that exist, and asking for the map when the arena has no image
says exactly that.

The tag-to-bit mapping is per file (`Start1` was bit 1 in that arena, bit 0 in a new grid), so the lookup lives in
`GridFile.IndexOfTag` with four tests rather than in the UI.

## 23. Arena maps: per project, and how the overlay actually composites

**Per-project lookup.** The Grid Builder looked for `map_<arena>.png` only in `<app>/maps`, so a project could never
carry its own maps - which is exactly what an unpack is meant to produce from the disc's images. The probe now resolves,
in order: `<project>/maps/`, `<project>/<name>_BEC/maps/`, then `<app>/maps/` as the bundled fallback. Arena key is the
parent folder of the `.grd` (`expanse/expansedunes/dunesstartdefault.grd` -> `expansedunes`), unchanged.

Proved by copying a reference PNG into a scratch project's `maps/` folder, switching arena away and back, and reading
`{"cmd":"grid","action":"state"}` -> `hasMapImage: true`; then `{"action":"map","visible":true,"scale":2.5}` draws it.

**The overlay is drawn as-is, deliberately.** The reference maps are 256x256 with ~47% alpha baked in (average alpha
119/255). Two attempts to "fix" it were wrong and are recorded so they are not repeated:

- drawing it at 55% and 85% opacity (ColorMatrix `Matrix33`) made the map *disappear* - dimming already-transparent dark
  art against a dark surface;
- the map is painted **before** the cells, so it only shows through cell gaps and empty cells; it never washes out the
  cell colours. At full opacity it reads as a background texture with the cells crisp on top, which is the intended look.

256 x 256 at the default transform scale of 2.5 is exactly 640 px = 32 cells x 20 px, which is why the built-in transform
defaults in the view are what they are.

## 24. Arena maps come from the project itself; disc images are no longer ISO-only

### Arena maps at unpack

The map art is **not TGA**: all 49 files under `<project>_BEC/data/texture/gui/maps/` are PTTP texture pages
(magic `PTTP`, 256x256, `PFHD` format byte `0x22` = GameCube CMPR colour plus an alpha mask in a second plane - 65,536
bytes = 256x256 x 4bpp x 2 planes). `Converter-Arena-Map.py` in GladiusTools is not a map converter at all (it is a
PAO/PAX geometry tool needing numpy), and the sibling `convert_pttp_to_tga.py` mis-decodes `0x22` as DXT5 and produces
noise. The correct converter is `GladiusTools/_tga_view/pttp_tga.py`: stdlib only, `--png <dir> <outdir>`.

- Vendored into the repo as `TextureConversion/pttp_tga.py` and copied to `tools/pttp_tga.py` on build, because the
  shipped toolbelt has no texture converter.
- A fourth unpack step runs it after the BEC unpack: `<project>_BEC/data/texture/gui/maps` -> `<project>/maps`, and it is
  only added when the script and the source folder exist. Measured on the demo project: **49 PNGs from 49 pages in
  4.4 s, 3.82 MB total, all 256x256**.
- Output goes to `<project>/maps`, deliberately **not** `_BEC/maps`: `bec-tool-all.py -pack` walks the whole `_BEC` tree
  and packs anything not listed in `filelist.txt`, so PNGs there would be shipped inside the ISO.
- Grid Builder resolves, in order: `<project>/maps`, `<project>/<name>_BEC/maps`, `<app>/maps`; with fallbacks for the
  arenas whose art is numbered (`darkgod` -> `darkgod1..4`, `nordaghforest` -> `nordaghforest1..3`) and the one
  misspelled directory (`channelerstemple` holds `map_channellerstemple`). Verified: all four previously map-less arenas
  now resolve. Also fixed the transform-defaults typo `caltharena` -> `calthaarena`, which meant those defaults never
  applied.
- The overlay draws the art **as-is**: the reference pages carry ~47 % alpha already, and two attempts to dim them
  (55 %, 85 %) made the map vanish on a dark surface. The cells are painted afterwards, so the map only shows through
  cell gaps and empty cells.

### Disc images: RVZ, WIA, GCZ, CISO, NKit

The app's tools seek raw ISO offsets (`ngciso-tool.py` even opens the file `rb+`), so they can only read a plain ISO.
Detection is by bytes, not extension: `ModdingGUI.Core/Discs/DiscImageProbe` reads 0x208 bytes and checks, in order,
RVZ (`52 56 5A 01`), WIA (`57 49 41 01`), GCZ (`01 C0 0B B1`), CISO (`CISO`), the NKit marker `"NKIT v01"` at 0x200,
then the GameCube magic word `C2 33 9F 3D` at 0x1C. Every pattern was verified against a real file produced by
Dolphin 2606a on this machine - the Dolphin source's own comment for the GCZ magic is stale, so it was not trusted.

`DiscImagePreparer` converts anything else to a temporary plain ISO and the caller deletes it:

- RVZ/WIA/GCZ/CISO -> `dolphin-tool convert -i <in> -o <tmp>.iso -f iso` (no block-size/compression flags needed for a
  plain-ISO output);
- NKit -> NKit's `ConvertToISO.exe`, which always writes into its own `utils/Processed/GameCube` and appends `_1` on a
  collision, so the file is discovered rather than assumed. Its exit code is 2 on success *and* failure, so the output
  file is the only signal;
- **nothing is bundled**: Dolphin is GPL-2.0-or-later and NKit is closed-source freeware with no licence grant, so the
  converter is a setting (`Disc image converter`) that is auto-derived from `DolphinTool.exe` sitting beside the
  configured Dolphin executable.

Verified end to end: `g.rvz` -> detected RVZ -> converted in **2.1 s** -> 1,459,978,240-byte ISO with MD5
`a78d6e1c9de69886ce827ad2a7507339`, i.e. exactly the vanilla hash. Through the UI (with the Dolphin path configured and
the converter auto-discovered) the patching tab logged detection, conversion, **"Verified against the vanilla ISO
hash"**, then ran xdelta against the converted ISO - failing only because the test patch was a dummy.

Consequences wired in: the MD5 gate now runs on a **plain ISO** (a container hash would always mismatch) - at selection
for plain images, and after conversion for everything else. NKit is allowed for **unpack** with a warning (it is a valid
disc with padding stripped and its file table still resolves; 182 of 185 extracted files are byte-identical to a
rebuilt ISO) but patching requires conversion, since a scrubbed image can never match a vanilla patch. The five
file pickers now offer `iso/gcm/nkit.iso/rvz/wia/gcz/ciso`, and the old "go find Swift's tool in Discord" message is
gone. `DiscImagePreparerTests` covers the decisions without an external process; `DiscImageProbeTests` covers the magics.

## 25. Convert page, and the shipped NKit converter

The project owner confirmed the NKit distribution is free to bundle, so it now ships: `NkitConvert/` (ConvertToISO.exe,
NKit.dll, SharpCompress, Redump/GameTDB dats, ~1.8 MB) is copied to `tools/nkit` on build, and a dedicated **Convert**
tab uses it.

**What the page does.** Source image (any of iso/gcm/nkit.iso/rvz/wia/gcz/ciso) -> detected format label -> target
format -> destination, with an activity pane. Targets are tinted `CheckBox`-as-button toggles, not a ComboBox: a
`DropDownList` keeps its own system-coloured closed box no matter what the palette says (drawn by the OS), and
owner-drawing it needed the `e.Index == -1` closed-state case which is easy to miss. The Randomizer's toggle pattern was
already the house style.

**Engine.** `ModdingGUI.Core/Discs/DiscImageConverter`:

- ISO/GCM/RVZ/WIA/GCZ/CISO -> `dolphin-tool convert`, with the exact per-format flags those containers demand
  (`-b` for gcz/wia/rvz; `-c zstd -l 1` for RVZ; `-c none -b 2097152` for WIA). 13 unit tests pin the argument mapping,
  because getting a block size wrong is a hard failure, not a slow path.
- NKit -> the bundled `ConvertToISO.exe`, run on a **copy of the tool folder in temp**: it always writes to
  `<tool>/utils/Processed/GameCube` and appends `_1` on a name collision, and the install directory may be read-only.
- Output is verified, never trusted: the converted file must exist, be non-empty, and - for an ISO - be re-read through
  `dolphin-tool header -j` for a game id. Both tools lie about exit codes (Dolphin exits 0 on garbage and writes a file;
  NKit's converter exits 2 on success *and* failure).
- Refuses to overwrite an existing destination, and refuses source == destination.

**Measured end to end through the app** (driver `convert run`, and again through the page's own button):

| Conversion | Time | Result |
|---|---|---|
| RVZ -> ISO | 2.3 s | 1,392 MB, game id GLSE64 |
| NKit -> ISO | 8.5 s | 1,392 MB, game id GM4E01 |
| ISO -> RVZ | 1.7 s | 1,060 MB, magic `52 56 5A 01` |

**Also fixed while wiring it:** `AppSettings`' deep-copy constructor dropped `Theme` (saving Settings reset a light
theme to dark) and the new `DiscImageConverterPath`; the Packing page's `chbAutoLaunchDolphin` had been left off the card
by the old designer layout - invisible and unclickable - and now has an "After packing" row. All eight pages audit at
contrast 0 / layout 0.

## 26. Map decode validated against the 2015 reference set

The app's converted pages were compared pixel-for-pixel with the reference PNGs the project owner supplied
(`E:\Gladius-Workshop\ModdingGUI\maps`), not just eyeballed.

| Check | Result |
|---|---|
| File names | 49 of 49 match; no extras, none missing |
| Dimensions | 49 of 49 are 256x256 |
| Colour difference | **mean 0.73/255**; 3,059 px of 3,211,264 (0.095%) differ by more than 8; worst single pixel 42 |
| Alpha difference | mean 0.47/255; 15,190 px (0.473%) differ by more than 8; worst 22 |

The alpha difference is **identical for every one of the 49 images** (310 px over 8, worst 22 in each), which says it is a
systematic difference in the mask plane rather than per-image noise: the 2015 tool interpolated the mask, the current
decode takes the CMPM block value as stored. At the ~47% opacity these pages are drawn with, it is invisible - the
difference panel of the comparison sheet is black.

Detailed arenas were used deliberately: `map_pirgosarena.png` (a colosseum with tiered seating) and
`map_arenasuren.png` (a settlement with structures, roads and vegetation), because a flat arena proves nothing about the
decode. `qa-maps-compare.png` shows ours | reference | difference x8 for both.

**Fit-to-board fallback.** Only four arena keys have hand-tuned transforms, so every other arena opened with the page at
scale 1.0 - a 256 px thumbnail in the corner of a 640 px board. When the map key has no tuned transform the page is now
scaled to fill the board (`FitScaleFor`, 32 cells x 20 px / image width, so 2.5 for the standard page size) with zero
offsets, and auto-shown. Five tests pin the arithmetic; the four tuned arenas still use their tuned values.

## 27. Map placement: what could not be derived, and what replaced it

**Do not trust the machine's guesses about map orientation.** Two attempts were made to derive the page alignment from the
`.grd` data, both failed, and both are recorded here so nobody repeats them:

1. *Luminance under wall cells*: score each of 8 orientations by "walls darker than floor". It favoured a different
   orientation for almost every arena and contradicted the hand-tuned values (it wanted flipX for `belfortarena` and
   rot90 for `calthaarena`, both hand-tuned to rot180). Walls are not consistently darker than floor, so the metric is
   measuring nothing.
2. *Registration of wall boundaries onto map edges*: exhaustive search over 8 orientations and a 33x33 offset grid. It ran
   to the edges of the search box and matched none of the ten hand-tuned arenas. Sparse boundary samples against an edge map
   with strong axes give a degenerate optimum.

Conclusion: the alignment is a property of the art, not of the grid data. The only trustworthy source is the hand-tuned
table in the original `GridBuilderView` (ten arenas) plus a human saying "that is where it goes".

**What changed instead.**

- The rotation list gains **Flip H** and **Flip V** (indices 4 and 5). Indices 0-3 keep their original meaning, so every
  value in the hand-tuned table still means what it did. A mirrored page cannot be aligned by rotation alone, and a flipped
  page was previously unexpressible.
- **Remember placement / Clear placement** buttons on the Grid Builder toolbar save the current offset, scale and rotation
  for the open arena (key = the `.grd`'s parent folder) to `<app>/map_placements.json`, and **saved placements win** over
  the built-in table and over fit-to-board. The file is camelCase, indented and keyed by arena so it can be hand-edited or
  shipped with a project.
- Driver: `{"cmd":"grid","action":"placement"}` reads current + saved; `"remember":true` saves; `"remember":false` forgets.
- Verified end to end: set Flip V on `arenasuren`, `remember:true` wrote the JSON, an arena round-trip came back with
  `hasSavedPlacement: true` and rotation still 5, and `remember:false` emptied it. 8 store tests cover round-trip,
  case-insensitive keys, corruption, precedence-independent removal and the hand-readability of the file.
- Precedence on load: saved placement -> built-in table -> fit-to-board.

## 28. What the original map implementation did (and why a flip was impossible)

`_release414/src/GridBuilder/GridPanel.cs` lines 58-90 are the whole of it, reproduced here because "look at the old code"
is the right instinct:

```csharp
Image? originalMapImage = _ctx.CurrentMapArenaImage;
Point offset = _ctx.MapDisplayOffset;
float scale  = _ctx.MapDisplayScale;
RotateFlipType rotation = _ctx.MapDisplayRotation;

imageToProcess = new Bitmap(originalMapImage.Width, originalMapImage.Height, Format32bppArgb);
tempGraphics.DrawImage(originalMapImage, 0, 0, w, h);      // copy, so the source is not modified
if (rotation != RotateFlipType.RotateNoneFlipNone)
    imageToProcess.RotateFlip(rotation);
g.DrawImage(imageToProcess, new RectangleF(offset.X, offset.Y, w * scale, h * scale));
```

So the original applied **offset + scale + RotateFlipType**, driven by a seven-item toolbar (X, Y, Scale, Rot) whose
rotation list was **`None / 90 / 180 / 270` only**, plus a ten-entry `_mapDefaultTransforms` table. Consequences:

- **A flip was not expressible.** `RotateFlipType` supports all eight orientations, but the UI only offered the four
  rotations, so a page whose art is mirrored could not be corrected in the old app either - only approximated with 180.
- **No global transform.** Five tuned arenas use 180 and five use None, which a single global rotation or flip cannot
  reproduce; and the 2015 reference PNGs in the shared `maps` folder are in the *same* orientation as the current decode
  (mean difference 0.73/255, no mirroring), so the assets were never pre-flipped either. The difference between arenas is
  a property of the art per arena, not of the pipeline.
- `arenasuren` is simply absent from that table, so the old app showed it at scale 1 with no rotation too.

**Therefore:** the complete fix is to offer all eight orientations, which the rotation list now does -
`None, 90, 180, 270, Flip H, Flip V, 90 + Flip H, 270 + Flip H` (indices 0-3 unchanged so the hand-tuned values still
mean what they meant), and to let any placement be remembered per arena (section 27). `qa-suren-orientations.png` renders
one arena's page under all eight, labelled with the index, so the right one can be identified by looking rather than
described.

## 29. Arena Suren confirmed: 180�, and how the rest of the alignment gets fixed

`arenasuren` is now in the built-in table at **180� / scale 2.5 / offset 0,0**, confirmed against that arena's own `.grd`
by the project owner. It had been missing from the original table entirely, so every version of the app showed it at
scale 1 with no rotation. Verified in the running app: opening any `steppes/arenasuren/*.grd` now lands at
`rotationIndex 2, offset 0,0, scale 2.5` with the overlay shown.

The remainder is a workflow rather than a table, for the arenas nobody has tuned yet:

1. Open the arena; the page shows at fit-to-board with no rotation.
2. Pick one of the **eight** orientations (None, 90, 180, 270, Flip H, Flip V, 90+Flip H, 270+Flip H).
3. **Nudge** with the X/Y boxes, or with the arrow keys while the overlay is showing - one board pixel per press, ten with
   Shift. Nothing else in the view used the keyboard, and the numeric boxes alone are too coarse for fine alignment.
4. **Remember placement** writes it to `map_placements.json`; **Clear placement** returns the arena to the table or the fit.

Saved placements win over the built-in table, so a user can correct an arena the table gets wrong without waiting for a
code change, and a value confirmed here can be promoted into the table for everyone (as `arenasuren` just was).

## 30. Encounters, Leagues, Towns: what exists, and the first surface for encounters

Surveyed before building anything, because the four domains the owner asked for are in very different states:

| Domain | Corpus | Core | GUI | Agent surface |
|---|---|---|---|---|
| Encounters | `data/encounters/**/*.enc` - **834 files** | `IEncountersApi` complete: header fields, teams, per-team fields, `SetTeamUnits`/`SetTeamUnit`, `EncounterUnit.TryParse` (21 slots), validation | **none** | `encounters` command (this pass) |
| Leagues | `data/towns/leagues/*.tok` - **44 files** (OFFICENAME/OFFICER/RECRUIT rows/LEAGUEPTSNEEDED) | `ILeaguesApi` + `LeagueValidator` + `LeaguesCatalog` complete | **none** | none |
| Towns | no town table: `towns/` holds only `leagues/` (44), `shops/` (**59** `.tok`), `schools/` (**27** `.tnt` tints). Town identity lives in scripts/menus (`data/script/cnv*town.scp`) | **none** - shops and tints have no API | none | none |
| Schools | `data/school/*.tok` - **~76 files**; 144 names in `data/units/schools.txt`; tints in `towns/schools/*.tnt` | `ISchoolsApi` lists **every** school file | Team Builder opens only the two campaign rosters | `openTeamBuilder` only |

So the "schools" question settles as: the **enemy** school files are the ones not editable today. `data/school/*.tok` are roster-shaped
(`NAME:`, `HERO:`, `GOLD:`, `CREATEUNIT` blocks) and encounters reference them by name - `HOMETOWNSCHOOL` 248 times,
`Dynamic Statues` 56, `NoSchool` 46, `Story Characters` 38, `Suyhron` 24, `RollOutTheBarrels` 23, `BelfortMilitary` 21,
`Eyes of the Beast` 19, `Riot Gyhrls` 17, `REGIONSCHOOL` 17, and so on. The API already lists all 76; only the Team
Builder's roster picker is restricted to the two campaign files.

**Built this pass - the `encounters` driver command** (`list`, `get`, `setHeader`, `setTeamField`, `setUnits`, `setUnit`,
`validate`), so encounters have an agent surface even before they have a page. It speaks in file/team/unit indices and raw
`UNITDB` rows rather than 21 positional slots, and validates every row with `EncounterUnit.TryParse` before writing.

Verified on `demo_hero`: `list` returns 834 files (52 for `pirgosarena`); `get` on `battle1.enc` returns 15 header fields,
3 teams and 7 units with class/sub-type/start-slot/chance parsed; `setHeader ENCOUNTERNAME` took effect and reverting it
left the file **byte-identical** (SHA-256 compared before/after) with a `.mod-history` backup written per write.

One defect found by that test and fixed: the command lowercased the action name but compared camelCase literals, so every
write action reported "unknown action" while the read actions worked.