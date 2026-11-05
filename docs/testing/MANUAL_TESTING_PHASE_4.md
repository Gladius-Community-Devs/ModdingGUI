# Phase 4 QA — round 6 (deviations closed)

## Round 5 result

| Your check | Result |
|---|---|
| Tests on your side | **validated** — re-ran the suite: 348 → now **358 passed, 0 failed, 0 skipped** |
| Grid Builder (1.1–1.5) | **pass** — legacy tags visible, region painting, save, reopen |
| 3.3 "A Jingle for Me" | **pass** — Item Editor path confirmed |
| 2. Bjolf far right, Aera far left | **pass** — three enemies, all on far-side cells. Closes typed `UNITDB`, story-encounter resolution and the slot rules in one run |

## What "no deviations" changed

**1. Grid writes go through Core.** New `IGridsApi` (`List`, `Get`, `Describe`, `Save`); `Save` takes
the project lock and writes a timestamped `.mod-history` backup before overwriting. The Grid Builder
calls it instead of `GridFile.Save`. Tests assert the backup holds the **previous** bytes, that a
no-op save leaves the file untouched, and that `../../` paths are rejected.

**2. `UNITDB` slots named from evidence.** Three sources, graded:

| Evidence | Slots |
|---|---|
| Engine (`EncounterSetup` case `0x1D`) | name, spawn chance, start marker, x, y, jitter flag (adds `rand()`, clamps to ≥ 1), spawn flags `0x100` / `0x200` |
| Modder notes (`devnotes.txt`) | 7–8 attribute/affinity requirement, 14–16 the three "prohibited" flips, 18 mercenary required, 19 item drop chance % (vanilla: 5-step values up to 90) |
| Unknown — left numeric with a comment | slot 13, slot 17 (vanilla 0–3) |

Where notes disagree with the engine, the engine wins: the notes guessed slots 4–6 were level bounds,
but the code adds `rand()` to them and clamps to ≥ 1 — they are coordinates.

Same function, new find: the engine forces **`Summoner`/`UndeadCaster`** on the second and later unit
of a team. That is the summon behaviour behind Hall of Titans and Summoner's Endurance — those extra
units are engine-generated, not badly placed.

**3. Skill Editor descriptions.** Each field shows a one-line summary (first sentence, capped at 140
characters) with the full engine-lookup derivation behind a **"Full details"** toggle. A catalog-wide
test asserts every documented field has a summary that fits and keeps its note.

## What still needs your eyes

1. **Grid Builder save** — open a legacy map, paint a cell, **Save**, then confirm a timestamped
   `.bak` appeared under `<project>_BEC\.mod-history\data\encounters\...`. That is the only new
   behaviour this round (plus the log line naming the backup).
2. **Skill Editor** — pick `SKILLUSECLASS`, then `SKILLANIM`: one line + "Full details", not a paragraph.
3. No in-game change: the ISO from 22:34 is untouched.

## Remaining item, deliberately not forced

`IShopsApi` / shop inventories: the data is reachable through `IConfigTableApi`, so nothing is
unreachable or hand-edited — there is just no typed model yet. Inventing a schema before the shop
fields are verified would be the same guess this round removed from `UNITDB`, so it belongs with the
next slice (`gladiators/statsets/itemsets/skillsets`) rather than a rush.
