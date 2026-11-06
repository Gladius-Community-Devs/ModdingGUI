# Manual QA — Phase 6 randomizer

Build under test: `qa-pack/qa-pack.iso` (packed 16:49), seed **`phase6-qa2`**, steps run:
`heroes, scripts, statsets, itemsets, team, encounters, recruits`, `--hero-name valens --cash 25000`.

That build changed 5,178 records. What it contains, verified from the packed project:

| Area | State |
|---|---|
| Rosters | both files carry **six heroes + 14 team units (20 blocks)**, six distinct classes, all `CUSTOMIZE: 0`, all `CORESTATSCOMP2` six values |
| Heroes | Valens=Murmillo, Ursula=ArcherF, Ludo=BanditB, Urlan=Eiji, Eiji=UndeadMeleeNorA, Gwazi=Legionnaire |
| Gold | 25,000 in both schools |
| Units table | 2,170 records, stat/item sets reassigned |
| Encounters | 834 files, **3,174** randomizable enemy rows, **0** `SCHOOL:` lines inside teams, 116 `CANDIE:` lines |
| Scripts | 6 flow scripts patched, Fjord league switched to the Valens branch, all `.scb` recompiled |
| Prop teams | `battle1.enc`'s `TEAM: -1` block is **byte-identical** to vanilla, `SCHOOL: "PropSchool"` kept |
| Item sets | only sets with a level-1 row are assigned (331 of 404); 0 gladiators on an unusable set |
| Team gear | 14 team units, none bare |

Run with Dolphin against that ISO. Report each step as **PASS / FAIL / NA**, and note anything that looks odd even
if it passes.

---

## 1. Boot and new game

1. Launch the ISO. Main menu → New Game.
2. **Choose the Valens campaign.**
   - [ ] Game reaches the first playable state without hanging.
   - [ ] No black screen, no freeze at the loading screen.

> A hang here would most likely be a `CUSTOMIZE`/variant mismatch. The build writes `0` everywhere, so it should
> not happen — if it does, that is a blocker and I need the exact moment.

## 2. School roster — heroes

Open the school and inspect each hero's panel.

3. **Valens** — expect class **Murmillo**, level 1, with gear and 4-6 skills.
   - [ ] Class shows Murmillo.
   - [ ] Data/stats panel shows CON/PWR/ACC/DEF values that are **not** vanilla Valens (vanilla 5/7/10/11).
   - [ ] Equipment panel shows a weapon + armour (not all "Empty").
   - [ ] Skills list is short (roughly 4-6), with no tier-IV or shape-shift entries.
4. **Ludo** — expect class **BanditB**, and **450 experience** (vanilla value preserved).
   - [ ] Class shows BanditB.
   - [ ] Experience is 450, not 0.
   - [ ] Gear present.
5. **Ursula, Urlan, Eiji, Gwazi** all appear in the roster immediately.
   - [ ] All four are present at new game, not arriving later.
   - [ ] Each shows a class from the list above.
   - [ ] No two heroes share a class.

> If a unit's equipment panel is empty while stats are right, that is the `CORESTATSCOMP2` arity bug returning —
> report it immediately.

## 3. Team units

6. Scroll the roster past the six heroes.
   - [ ] 14 additional units named `<name>_random` are present.
   - [ ] Each has a class, level 1, gear and a few skills.
   - [ ] None of them are `Boss`, `Prop`, `DarkGod` or `AffinityBeastGreater` classes.

## 4. First battles

7. Fight the first league/tutorial bout.
   - [ ] Your team fields your school units (the placeholder rows pulled from your roster).
   - [ ] The game does not stall when loading the battle.
8. Play two or three bouts in different leagues.
   - [ ] Opposing teams appear with **random** unit mixes.
   - [ ] No barrels, statues, tombstones or other props appear as fighters.
   - [ ] No boss-class enemies appear in ordinary bouts.
   - [ ] Enemy levels track your school level within a couple of levels (the row asks for −2..+2).

## 4b. The three bugs from the previous pass

- [ ] **Props stay props** — in `battle1.enc` (Pirgos arena) the two prop slots still hold props, not
      gladiators, and nothing appears where they stand (they are on the prop team with count 0).
- [ ] **No bare units** — every team member and hero has at least one item equipped; specifically no Ogre with
      an empty equipment panel.
- [ ] **Encounters with a prop team** load and play normally.

## 5. Recruits

9. Visit a league office / recruit screen.
   - [ ] No recruits are offered anywhere (the `RECRUIT` lines were stripped from all 21 league files).

## 6. Stats and equipment

10. Pick three units at random and compare their stat blocks.
    - [ ] Stats are genuinely scrambled between them (different curves, not all identical).
11. Check an affinity unit (one whose weapon grants Fire/Earth/Water/Air/Light).
    - [ ] It can use its affinity skills.
    - [ ] A unit with no affinity weapon still cannot use affinity skills.

## 7. Scripts and progression

12. Advance a story scene that would normally remove Ludo or Gwazi from the school.
    - [ ] They are **not** removed (the `SchoolRemoveCharacter` lines were deleted and the scripts recompiled).
13. If you reach a costume-change moment (Steppes/Expanse/Endgame).
    - [ ] It triggers correctly (the `CostumeChange != 1` tests were rewritten to `!= 0`).
14. Valens campaign, Fjord league.
    - [ ] The league behaves as the Valens branch (no Urlan-only dialogue branch).

## 8. Save and load

15. Save to a memory card slot, quit, reload.
    - [ ] Save writes without error.
    - [ ] Loading restores the randomized school exactly (same classes, same gear, same gold).
16. Load an **existing pre-randomizer save** if you have one.
    - [ ] It still loads (roster edits cannot affect a save block).

---

## What a failure means

| Symptom | Likely cause | Where I would look |
|---|---|---|
| Hang on new game | `CUSTOMIZE` referencing a missing `varNN.ptx` | `RandomizerService.BuildHeroUnit` |
| Unit has stats but no gear/skills | `CORESTATSCOMP2` not six values | `BuildStatLine` |
| Props/bosses as enemies | prohibition fields lost from the `UNITDB` row | `EncounterRandomizer.RandomizableRow` |
| Enemy levels wildly off | level fields in the row | same |
| Recruits still offered | `RemoveRecruits` did not run | `ILeaguesApi.RemoveRecruits` |
| Ludo/Gwazi vanish mid-story | script patch missing + not compiled | `ScriptPatcher`, `CompileScripts` |
| Same seed produced a different school | RNG not threaded through a step | `RandomizerRandom` |

## Rebuilding it yourself

```powershell
dotnet ModdingGUI\bin\Release\net8.0-windows\win-x64\ModdingGUI.dll `
  --randomize qa-pack --seed "phase6-qa" `
  --heroes --hero-name valens --statsets --itemsets --team --enemies --recruits --cash 25000 `
  --pack --tools "E:\code_stuff\GladiusModdingToolsGUI\tools"
```

Or from the GUI: Randomizer tab → pick the project → tick the boxes → Randomize (the panel builds the same
options and calls the same pipeline), then Pack.
