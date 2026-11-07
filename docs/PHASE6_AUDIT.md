# Phase 6 audit — every old method against its replacement

The old randomizer is `frmMain_partials/Randomizer.cs` at `6ff0709^` (1,915 lines). This is a method-by-method
audit of that file against `ModdingGUI.Core.Randomizer`, written after QA found three behavioural gaps, to make sure
there is not a fourth waiting.

## Method map

| Old method (line) | New code | Verdict |
|---|---|---|
| `InitializeRandom` (231) | `RandomizerRandom` | **exceeded** — one RNG for every step; the old one seeded only heroes and team, so four steps ran on leftover state |
| `ReadRandomizerUIContext` (249) | `Form1.ReadRandomizerOptions` | matched — same controls, same defaults |
| `GetDeterministicHashCode` (288) | `RandomizerRandom.Hash` | equivalent (SHA-256 → FNV-1a; seeds are labels, not values) |
| `AppendRandomizerLog` (300) | `IAppLog` | matched |
| `RandomizeHeroes` (313) | `RandomizeHeroes` + `WriteRosters` | matched, plus distinct classes, preserved progression, six-value stats |
| `RandomizeTeam` (407) | `RandomizeTeam` | matched; base-class collapse now identical (see below) |
| `RandomizeStatsets` (535) | `RandomizeStatSetAssignments` | matched |
| `RandomizeItemsets` (592) | `RandomizeItemSetAssignments` + `ItemSetAffinityMap` | matched, plus the ranged-weapon rule and empty-set exclusion |
| `ItemsetContainsBow` (709) | `ItemSetAffinityMap.IsRanged` + `DeriveRequiredRangedWeapons` | **old dead code, now live** |
| `ParseItemsForAffinityAndType` (739) | map's weapon-affinity table | matched |
| `DetermineItemsetAffinity` (761) | map build (last weapon row) | matched |
| `HasFullWeaponCoverage` (815) | level-1 weapon filter | equivalent in vanilla: all 331 usable sets already cover 1-17, so the filters select the same sets |
| `FilterValidItemsets` (854) | candidate filter | matched |
| `CreateAffinityPools` (882) | map pools | matched |
| `SaveGladiators` (914) | `IGladiatorsApi.ApplyMany` | **exceeded** — the old writer rebuilt the file from its own parser (LF only, no terminator); the API preserves the file and writes one backup |
| `RemoveAllRecruits` (938) | `ILeaguesApi.RemoveRecruits` | matched |
| `GetEligibleClasses` (988) | `ClassEligibility` | matched (same three lists, generated from the old source) |
| `UpdateWorldmapWithCharacter` (1012) | — | **dead code in the old file too**: defined, never called |
| `WriteUnitsToFile` (1032) | `WriteRosters` | matched (NAME/HERO/GOLD, both campaigns, all six heroes) |
| `AppendUnitsToFile` (1067) | `RandomizeTeam` + `AddUnits` | matched |
| `AppendUnitBlocks` (1086) | `BuildTeamUnit` | matched, plus the fixes below |
| `ParseGladiators` (1135) | `GladiatorsApi.GetAll` | **exceeded** — the old parser split on `": "`, so a name containing that would mangle the record |
| `ParseStatSets` (1191) | `IStatSetsApi` | matched — both take the `1:` row; all 169 sets have one |
| `ParseItemSets` (1219) | `BuildItemLine` | matched — both take one item per slot (old: first row seen; new: the row covering level 1) |
| `ParseItemSetsFull` (1292) | `IItemSetsApi.GetAll` | matched |
| `ParseSkillSets` (1330) | `SkillNames(level)` | **exceeded** — the old code accepted only `0 30` windows; the new one is level-aware |
| `EditEncounterFiles` (1370) | `EncounterRandomizer` | matched, plus the prop/count-0/idempotency fixes |
| `EditScpFilesAndCompileAsync` (1600) | `ScriptPatcher` + `CompileScripts` | matched, plus `.scb` output and change-aware compilation |
| `ApplyIngameRandomAsync` (1926) | — | **removed** — the abandoned `wmglobal.scp` experiment |

## Gaps this audit found and fixed

1. **The encounter step only ran when an encounter option was ticked.** The old flow ran it on *every* pass, and
   its team-0 blanking is what lets the randomized school fight at all — so randomizing heroes and team without
   ticking "randomize enemies" would have produced a school that never appeared in battle. Encounters now always
   run; enemy rows and `CANDIE:` stay behind their own options.
2. **The script step only ran when heroes were randomized.** The old flow patched and compiled the six campaign
   scripts every pass. They now always run (and are always compiled before packing).
3. **Weighted mode grouped variants differently.** The old `GetBaseClass` collapsed every undead melee variant to
   `UndeadMelee`, then stripped `Imp|Nor|Ste|Exp|[AB]` with an optional `F`, then a lone `F`. Mine only stripped
   `F` and the regional word, so `UndeadMeleeImpA`, `BanditB` and `BanditAF` all counted as distinct bases and the
   "one of each class" pass degenerated. `BaseClass` now uses the old cascade verbatim.

## Checked and found equivalent (no action)

- **Stat sets**: all 169 carry a `1:` row, and the old code only accepted sets that did — so the new "random index
  from 0..168" cannot reach a set the old one would have skipped.
- **Item sets**: every one of the 331 usable sets has weapon rows covering levels 1-17, which is exactly what the
  old `HasFullWeaponCoverage` demanded. The two filters therefore pick the same 331 sets; the 73 the new code
  excludes are the ones with no level-1 weapon at all, which the old coverage test also rejected.
- **`CUSTOMIZE`**: the old writer always emitted `0, ""`, which is why the old randomizer never hit the variant
  lockup. The new code matched that only after the roster rewrite initially preserved the old outfit.
- **World map**: `UpdateWorldmapWithCharacter` exists in the old file but is never called, so no feature is lost.
- **Skills**: the old `0 30` window filter and the new level filter both include the always-available skills; the
  new one additionally allows a unit's level-appropriate ones.
- **Seeding**: blank seed → generated value, reported in the log, in both.

## What the new code does that the old could not

- One deterministic RNG per run (the old seeded two of six steps).
- One backup per run instead of one per record.
- Hero progression, outfits and levels preserved instead of reset to level 1.
- Ranged classes keep a usable weapon; units never receive an empty item set.
- Prop teams, count-0 rows and the prop team's school are left alone.
- Every write goes through an API, so the CLI, the GUI and (later) the MCP server share one path.
