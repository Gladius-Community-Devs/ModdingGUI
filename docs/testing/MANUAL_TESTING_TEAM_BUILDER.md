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
   - [ ] Rules Selection page: campaign selected, level 1, roster drop-down lists `data/school/*.tok`.
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

## 5. Roster round trip

15. Rules Selection → **Load roster** (`valensimperia.tok` in a vanilla project).
    - [ ] Units appear in the tree; each is editable, and the checkbox set (Valens/Ursula) decides which rosters the
          export writes.
16. Add to file (**Add all to team file** / the export button).
    - [ ] The summary lists written, skipped and duplicate units; duplicates are refused rather than silently renamed.
    - [ ] The project gains `.mod-history/data/school/<roster>.tok/<timestamp>.bak`, and the roster's edited unit boots
          with the class, stats, `ITEMSCOMP` and `SKILL` lines the panel showed (this half needs the game — see
          `HANDOFF.md` §6).

## Automated half

```powershell
# From ModdingGUI/, with the Debug exe built:
Start-Process .\bin\Debug\net8.0-windows\win-x64\ModdingGUI.exe -ArgumentList '--ui-server'
.\scripts\ui-drive.ps1 -File .\scripts\team-builder-smoke.txt
```

All 24 commands should answer `{"ok":true,…}`; the last one is a log tail that should contain the JP decision and the
screenshot path. This is the regression net for the panel: run it after touching `TeamBuilder.cs`, `UiAutomation.cs` or
`UiDriver.cs`.

## Notes

- A unit added at level 5 keeps level-5 stats until **Set level** or **Apply both** re-reads the set — that is
  deliberate, so a level change never destroys a hand-tuned stat row without the designer asking.
- `Override JP` on the Rules page and the JP offer write the same field (`JOBPOINTS`); the offer only ever raises it.
  Lowering is a deliberate edit, so it stays manual.
- Anything the panel cannot do yet is listed in `HANDOFF.md` §5.3 (currently: nothing — the UX backlog is closed).
