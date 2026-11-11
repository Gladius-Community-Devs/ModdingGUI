# In-game test: which affinity id is which affinity?

`UNITDB` slot 8 is an affinity id. The binary's affinity name block reads `Air, Earth, Fire, Water, Light, Dark`, and
`devnotes.txt` claims "5 = fire", but the two disagree, so the mapping is being settled in game.

## What was changed

`demo_hero/demo_hero_BEC/data/encounters/imperia/pirgosarena/PirgosClassicBattle.enc` - the enemy team (team 1) now has
`SCHOOL: "BelfortMilitary"` (its gladiators cover Air, Earth, Fire and Water) and five `UNITDB` rows, one per affinity id:

```
UNITDB: "", 100, "Start1", 0, 0, 0, 0, 1, "", "", "", "", 0, 0, 0, 0, 0, 0, 0, 0, 0   <- affinity id 1
UNITDB: "", 100, "Start1", 0, 0, 0, 0, 2, ...                                        <- id 2
UNITDB: "", 100, "Start1", 0, 0, 0, 0, 3, ...                                        <- id 3
UNITDB: "", 100, "Start1", 0, 0, 0, 0, 4, ...                                        <- id 4
UNITDB: "", 100, "Start1", 0, 0, 0, 0, 5, ...                                        <- id 5
```

Every row spawns with probability 100 at `Start1` with no class tag and no attribute mask, so the only filter is the
affinity requirement: each row can only draw gladiators of one affinity from that school.

## What to do

1. Pack `demo_hero` and start it (Packing tab -> Pack, then launch in Dolphin).
2. Play Pirgos -> **Apprentice Classic League** -> the match that loads `PirgosClassicBattle.enc`.
3. Note the enemy unit names that appear. Each one's affinity is known from `gladiators.txt`, so the names identify which id
   maps to which affinity; if a row draws nothing, that affinity is the one BelfortMilitary lacks (Light or Dark).

## Reporting

Give me the names in order of the rows if you can, or just the list; `gladiators.txt` resolves each name to its affinity.
If two rows draw from the same affinity, or a row draws nothing unexpectedly, that is also informative - it would mean the
slot is not a plain id.

## Undo

The edit is backed up under `demo_hero_BEC/.mod-history/data/encounters/imperia/pirgosarena/PirgosClassicBattle.enc/`; the
same rows can be restored through the `encounters` driver command (`setUnits` with the original three rows, `setTeamField`
`SCHOOL: "ImperiaOrusOrcusDomini"`).