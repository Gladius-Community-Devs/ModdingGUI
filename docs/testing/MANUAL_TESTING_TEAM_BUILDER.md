# Manual QA — Team Builder (Phase 7 UX)

The data path is covered by tests (`TeamExportShapeTests`, `ItemSetGearTests`, `JobPointsTests`, `LearnableSkillsTests`).
What needs eyes is the panel: does a designer find the gear, does the number they see match the number written, and
does the JP offer behave. `scripts/team-builder-smoke.txt` replays most of this through the [UI driver](../UI_AUTOMATION.md)
and leaves a screenshot behind; the list below is what to look at in it, plus the parts a script cannot judge.

## Prerequisites

- Debug build (the exe the user runs) with `tools/` beside it.
- A complete project — `gold_test` next to the exe, or any `<name>_BEC` + `<name>_ISO` pair.

## 1. Open it

1. Set a project (Pack tab path, or the tree), then **Debug Options → Team Builder**.
   - [ ] Tab opens, no "classdefs.tok not found" dialog. A project whose `_BEC` folder is named differently (the
         `REALDATA_*` fixtures use `vanilla_BEC`) also opens, because the check goes through `ProjectRoots`.
   - [ ] Rules Selection page shows four labelled groups (team type, size and starting level, job points for new
         units, rosters) and a fifth for loading a team. No bare "Override JP" checkbox floating on the page.
   - [ ] The level box is editable and reads 1; the roster drop-down lists only `valensimperia.tok` and
         `ursulanordagh.tok`, and the page says why the other school files are not offered.
2. Add a unit: name `QA Hero`, class `Murmillo`, level `5`, **Add Unit**.
   - [ ] It appears in the tree as `QA Hero (Murmillo)` and in the preview with 5's stats.
   - [ ] `Current Team: (1/14)`.

## 2. Gear page

3. Select the unit in the tree, open **Gear**.
   - [ ] The right panel shows `QA Hero — Murmillo`, a level box showing `5`, and five labelled slots, all `(empty)`.
   - [ ] The list shows only items this class may use, each labelled `Name [Type / SubType / Style] lvl N`.
   - [ ] `N of M legal items` is shown; a Murmillo sees a few hundred, not all 1,852.
4. Type `ring` in **Search**.
   - [ ] The list narrows to the rings, the count updates, and no item is offered that is not legal for the class.
5. Choose **Slot → Armor**, then clear the search.
   - [ ] Only armour is listed; switching back to `All slots` restores everything.
6. Select an item.
   - [ ] **Selected item** shows its name, type/subtype/style, min level, stars, cost, rarity, affinity, stat mods and
         skills — the numbers a modder needs before committing.
7. **Assign to slot**.
   - [ ] The matching slot fills in on the right (an `Accessory` item fills `Accessory`, never `Weapon`).
   - [ ] The list keeps the item highlighted, so the details stay readable.
   - [ ] The stats panel (Unit Selection page) still shows class stats; weapon damage is added to `DAM`.
8. **Clear all gear**, then pick an **Item set** and press **Apply both**.
   - [ ] All five slots fill from the set's level-5 row, and the item set never writes a slot the set does not cover.
   - [ ] The stat set's level-5 row becomes the unit's stats.

## 3. Level + preview (the old stale-preview bug)

9. Change the level box to `20` and press **Set level**.
   - [ ] The stats panel repaints immediately: `Level: 20`, and the values are the stat set's level-20 row.
   - [ ] The JP line moves to level 20's award.
   - [ ] Gear stays as it was (a level change must not silently re-gear the unit).
10. Type `99` and press **Set level**.
    - [ ] It is refused with a log line, and the box reverts to 20. Nothing is clamped behind the user's back.

## 4. Skills page

11. **Skills** tab.
    - [ ] Left: `Available for this class` with `(cost JP, lvl N)` per skill. Right: `Granted`, empty.
    - [ ] The JP line reads `Level 20: <awarded> JP awarded, 0 spent, <left> left`.
    - [ ] The list is the class's own skills only: no `…ComboChain …` records, no `Innate`/`Command` entries
          (`Gwazi OffBalance`, `Pass`, `Continue`), and no other class's abilities. An Amazon sees ~39 entries.
    - [ ] A skill that starts a chain is tagged `· combo ×N`; its cost is the only JP charged (the hits are free).
    - [ ] Tick **Also list skills any class can learn**: the list grows by the 8 records with no `SKILLUSECLASS`
          (`Crowd Status I–IV`, `Pass`, `Continue`, `Gwazi OffBalance`, `Other HellGate`), each tagged `· any class`.
          Untick and they go away again.
    - [ ] Select a combo root (`Amazon Combo I`): the details box lists the chain, marks which hit is bought, and says
          the later hits cost nothing because they come with it.
12. Add skills until the total cost passes the award.
    - [ ] The first additions are silent.
    - [ ] The one that crosses the budget asks: *"'QA Hero' now spends X JP on granted skills but is awarded Y.
          Raise this unit's JOBPOINTS to X so the roster stays legal?"*
    - [ ] **No** leaves the unit over budget, logs it, and shows `(over budget)` in red on the JP line.
    - [ ] **Yes** raises `JOBPOINTS`, logs it, and the line reads `<X> JP (overridden), <X> spent, 0 left`.
    - [ ] A granted skill cannot be added twice.
13. **← Remove** a skill.
    - [ ] It leaves `Granted`, the JP line drops the cost, and an over-budget build comes back under.
14. Select a learnable skill, then a granted one.
    - [ ] The details box describes the selection (cost, level, type, first fields).

## 5. Empty state (before any unit exists)

17. With no unit in the tree, open **Gear**, then **Skills**.
    - [ ] Both pages read "No units yet. Create one on the Unit Selection tab …" instead of an empty list with live
          buttons. Nothing is clickable behind the message.
18. Add one unit, remove it, and open the pages again.
    - [ ] The message changes to "Select a unit in the team list on the right…" (units exist, none selected).
19. Select a unit.
    - [ ] The editors come back, and the right-hand panel names the unit (`QA Hero — Murmillo`).

## 6. Roster round trip

20. Rules Selection → **Load into the editor** (`valensimperia.tok` in a vanilla project).
    - [ ] Units appear in the tree; each is editable, and the ticked rosters decide what **Save team to rosters** writes.
21. Before saving, note `EXPERIENCE`/`CUSTOMIZE`/`ITEMSCOMP` of each loaded unit in the roster file. Press
    **Save team to rosters** without editing them.
    - [ ] The summary says `0 updated, N unchanged` for those units.
    - [ ] The file diff contains only the new block — no reset experience, no changed `CUSTOMIZE`, no invented gear in
          slots that were empty. (This was broken: loading skipped `ITEMSCOMP`, so the Gear page was empty and the save
          wrote five empty slots over the equipment.)
22. Add a unit and press **Save team to rosters**.
    - [ ] The summary counts **units**, not writes: "Team of 1 unit(s) written to 2 roster(s):" followed by one line per
          roster. It must not claim two units because two rosters were ticked.
    - [ ] The project gains `.mod-history/data/school/<roster>.tok/<timestamp>.bak`, and the roster's edited unit boots
          with the class, stats, `ITEMSCOMP` and `SKILL` lines the panel showed (this half needs the game — see
          `HANDOFF.md` §6).
23. Press **Save team to rosters** again without changing anything.
    - [ ] It reports the units as `N added, 0 updated, M unchanged` rather than appending duplicates.

## Automated half

```powershell
# From ModdingGUI/, with the Debug exe built:
Start-Process .\bin\Debug\net8.0-windows\win-x64\ModdingGUI.exe -ArgumentList '--ui-server'
.\scripts\ui-drive.ps1 -File .\scripts\team-builder-smoke.txt
```

Every command should answer `{"ok":true,…}`; the last one is a log tail that should contain the JP decision and the
screenshot path. This is the regression net for the panel: run it after touching `TeamBuilder.cs`, `UiAutomation.cs` or
`UiDriver.cs`. It does not export, so it cannot touch your rosters.

## Notes

- A unit added at level 5 keeps level-5 stats until **Set level** or **Apply both** re-reads the set — that is
  deliberate, so a level change never destroys a hand-tuned stat row without the designer asking.
- The custom-JP checkbox on the Rules page and the JP offer on the Skills page write the same field (`JOBPOINTS`); the
  offer only ever raises it. Lowering is a deliberate edit, so it stays manual.
- A skill's `JOBPOINTS` price is the record's `SKILLJOBPOINTCOST`; a combo's later hits are separate records with no
  price, because the root is what the designer places.
- Anything the panel cannot do yet is listed in `HANDOFF.md` §5.3 (currently: nothing — the UX backlog is closed).

## 7. Pack validation pass (Pack tab)

The checkbox is **off** by default now, which means validation *runs*; tick it only to skip.

24. Pack a project with the checkbox unticked.
    - [ ] The log opens with `Validating project...`, one line per area (gladiators, skillsets, itemsets, statsets,
          tints, school names, rosters) and a summary such as
          `Validation: 62 finding(s): 0 error(s), 0 warning(s). These are advisory - packing continues unless you stop it.`
    - [ ] Informational notes are counted, not listed; packing starts either way.
25. Break a reference on purpose: change a skill name inside any `data/units/skillsets.txt` row to something that does
    not exist (keep the level numbers), then pack.
    - [ ] A warning appears naming the set and the name: `set 209: '...' is not a skill in skills.tok`.
    - [ ] Note the difference from the old pass: the row does **not** have to belong to a gladiator to be reported.
26. `ModdingGUI.exe --validate <projectFolder>` (add `--tools <dir>` outside an install).
    - [ ] Prints the same findings and `[validate] N finding(s): ...`; exit code 0 while there are no errors.