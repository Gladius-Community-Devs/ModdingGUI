# In-game test: which affinity id is which affinity?

`UNITDB` slot 8 is an affinity id. The binary's affinity name block reads `Air, Earth, Fire, Water, Light, Dark` and
`devnotes.txt` claims "5 = fire"; they disagree, so this is settled in game.

## The build

`demo_hero.iso` (1,430.7 MB, md5 `bf06273a05d057c53209c675d65b00d1`, built 2026-11-11 14:35) in
`ModdingGUI/bin/Debug/net8.0-windows/win-x64/demo_hero/`.

## What was changed (all in the Valens campaign's first battle)

`data/encounters/imperia/valensschool/scene06va.enc` is the **tutorial battle of the Valens campaign**
(`BATTLESCRIPT "battle_scene06v.scp"`, arena `valensschool`). Its enemy team (team 1) was the single named gladiator
"Ludo"; it is now six random draws:

```
TEAM:       1, 0
GRIDFILE:   "$d\encounters\Imperia\valensschool\valensschoolstorystart.grd"
SCHOOL:     "REGIONSCHOOL"
UNITDB: "", 100, "Start1", 0, 0, 0, 0,  1, "", "", "", "", 0, 0, 0, 0, 0, 0, 0, 0, 0
UNITDB: "", 100, "Start1", 0, 0, 0, 0,  2, "", "", "", "", 0, 0, 0, 0, 0, 0, 0, 0, 0
UNITDB: "", 100, "Start1", 0, 0, 0, 0,  3, "", "", "", "", 0, 0, 0, 0, 0, 0, 0, 0, 0
UNITDB: "", 100, "Start1", 0, 0, 0, 0,  4, "", "", "", "", 0, 0, 0, 0, 0, 0, 0, 0, 0
UNITDB: "", 100, "Start1", 0, 0, 0, 0,  5, "", "", "", "", 0, 0, 0, 0, 0, 0, 0, 0, 0
UNITDB: "", 100, "Start1", 0, 0, 0, 0, -1, "", "", "", "", 0, 0, 0, 0, 0, 0, 0, 0, 0
```

- No unit names, no class tags, no attribute mask, no prohibitions, no level offsets: the only thing that differs between
  the six rows is the affinity id, and the last row (`-1`) is an unrestricted control.
- `REGIONSCHOOL` rather than a named school, because `valensschool_leagues.tok` has no `SCHOOL` lines of its own; the
  region token resolves to Imperia's 30 schools (see `WorldRegions`), which is the random pool.
- `START1` gained room: `data/encounters/imperia/valensschool/valensschoolstorystart.grd` had **one cell** for each of
  Start1-Start4, so Start1 was widened to a 3x2 block at rows 24-25, columns 9-11 (six cells) before packing. The rest of
  the grid is empty, so nothing was disturbed.

## What to do

1. Open `demo_hero.iso` in Dolphin.
2. Start the **Valens campaign** and play to the first battle (the school tutorial). It loads `scene06va.enc`.
3. Note the six enemy units that spawn.

## Reporting

Give me the six names (or as many as spawn) and `gladiators.txt` resolves each to its affinity, which gives id -> affinity.
If fewer than six spawn, the missing ids are the affinities Imperia's schools do not offer (Light in particular: the corpus
has no Light-affinity gladiators).

## Undo

Backups are in `demo_hero_BEC/.mod-history/` for both the encounter and the grid. The encounter can also be restored with
the `encounters` driver command (`setUnits` with `"Ludo", 100, "Start2", 0, 0, 1, 0, -1, ...`, and `removeTeamField`
`SCHOOL`).