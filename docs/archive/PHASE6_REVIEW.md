# Phase 6 review — the old randomizer against the new pipeline

Comparing `frmMain_partials/Randomizer.cs` as it shipped (2,166 lines, deleted in `6ff0709`) with
`ModdingGUI.Core.Randomizer` (1,384 lines across 8 files).

## Feature parity

| Capability | Old | New | Verdict |
|---|---|---|---|
| Six story heroes re-classed | `RandomizeHeroes`, classes dealt from a shuffled list | `RandomizeHeroes` + `WriteRosters` | matched — distinct classes, all six in **both** rosters |
| Hero keeps progression | not preserved (blocks rebuilt at level 1) | `LEVEL`/`EXPERIENCE`/`JOBPOINTS`/`CUSTOMIZE` carried over | **exceeded** |
| Team roster, 14 or 34 | `RandomizeTeam` | `RandomizeTeam` | matched |
| Weighted class diversity | `RandomWeighted` | `WeightedClasses` | matched |
| Version class blacklist | `GetEligibleClasses` (57/42/38) | `ClassEligibility` (same lists, generated from the old source) | matched |
| Stat set reassignment | `RandomizeStatsets`: random index per gladiator | `RandomizeStatSetAssignments` | matched |
| Item set affinity matching | `RandomizeItemsets`: own affinity ∪ Light | `ItemSetAffinityMap` | matched |
| Recruit removal | `RemoveAllRecruits` (RECRUIT lines in leagues) | `ILeaguesApi.RemoveRecruits` | matched |
| Encounter rewrite | `EditEncounterFiles` | `EncounterRandomizer` | matched, **idempotent** |
| Randomized enemies | `UNITDB: "", 99, …` with Boss/Prop/DarkGod/AffinityBeastGreater prohibited | same row, same list | matched |
| Perma-death `CANDIE:` | inserted after `TEAM: 0` | same, only when missing | matched |
| Campaign script patches | `EditScpFilesAndCompileAsync`: 6 flow scripts + Fjord league for Valens | `ScriptPatcher` | matched |
| Script compilation | `DOGCodeCompiler.exe` via batch | `ProjectBuildService.CompileScripts` (`.scp` → `bin/*.scb`) | matched |
| Cash | fixed / RANDOM / 15000 / 75000 | `CashMode` + same defaults | matched |
| School name | text box, empty ⇒ "Random's School" | `RandomizerOptions.SchoolName`, same default | matched |
| Seed | `InitializeRandom` — **only 2 of 6 steps used it** | one `RandomizerRandom` for every step | **exceeded** |
| Settings persistence | `randomizer_settings.json` | unchanged (shell) | matched |
| GUI | checkboxes drive file edits directly | checkboxes build `RandomizerOptions`; shell edits nothing | **exceeded** |

## Deliberately not carried over

| Item | Why |
|---|---|
| In-game randomization (`wmglobal.scp` `OnKill` class change) | a proof of concept that never worked; the option is removed rather than ported |
| Randomizer Testing menu (N-run harness) | it drove the deleted shell pipeline; the menu item is hidden |
| `RandomizeSchools` | never existed in the old randomizer; my own addition, removed as drift |

## Bugs the new code fixes (all verified in the DOL)

| Bug | Old behaviour | Consequence | Now |
|---|---|---|---|
| `CORESTATSCOMP2` had 5 values | `AppendUnitBlocks` wrote five | `SchoolLoad` stops at the missing sixth and never parses `ITEMSCOMP`/`SKILL` — units arrive with **no gear and no skills** | six values |
| `ITEMSCOMP` from every set row | wrote all rows | nine names in a five-slot line, malformed block | one level-1 row per slot |
| Skills not level-filtered | granted the whole set | level-1 hero with `Combo IV` and every shapeshift tier | filtered to the unit's level |
| `CUSTOMIZE` carried over on a class change | preserved the old outfit | `varNN.ptx` that the new class does not ship ⇒ **lockup** (14 reachable classes ship none) | always `0` |
| Team read stats/items/skills before they were randomized | order was heroes → team → … → statsets | teams always got vanilla curves | deliberate change: teams now reflect the randomized tables |
| One backup per record | every `Patch` wrote a `.bak` | 2,170 patches ≈ 550 MB of history | one batch write, one backup |
| Prop teams randomized | — (the old code guarded on `team > 0`) | props replaced by gladiators that sit on a team which never gets a turn | fixed after QA: only `team > 0` is touched, count-0 rows refused |
| `TEAM: -1` misparsed | — | the prop team's header was not recognised, so its `SCHOOL: "PropSchool"` was dropped | fixed after QA: team index is `int?`, -1 and "not a team line" are distinct |
| Empty item sets assigned | — | 28 sets have no rows and 45 more have no level-1 row; a unit on one of them has no gear at all | fixed after QA: the map offers only the 331 sets with a level-1 row |
| Slow reads | `Get` per record | 2,170 parses for one hero step (210 s measured) | `GetAll`, 1.3 s |

## Structural gains

- **One code path**: GUI panel, `--randomize` CLI and (later) the MCP server all call `RandomizerRunner`.
- **Reproducible**: same seed twice produced byte-identical rosters and gladiator table; a different seed differs.
- **Reviewable**: every change goes through an API, so every run leaves one backup per file.
- **Validated**: the roster write is followed by school validation, which is what would have caught the short
  `CORESTATSCOMP2` line before it reached a build.

## Known differences worth stating plainly

1. **Team units now use randomized tables.** The old order randomized the team first, so teams always carried
   vanilla stats/gear even when "randomize statsets" was on. The new order runs the team last, so a team member's
   stats, gear and skills match the randomized tables. This is intentional and contradicts the old output, not the
   old intent.
2. **School name defaults to "Random's School"** exactly as before; a project that never sets it keeps that name.
3. **`CUSTOMIZE` resets to 0**, so a hero's custom outfit/tint name is dropped. Vanilla blocks carry `""` anyway.
