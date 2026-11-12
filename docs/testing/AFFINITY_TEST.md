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

## Revision 2: the tutorial script was blocking the battle

First attempt reached the battle but never handed over a turn, so the enemy units could not be hovered. Cause:
`scene06va.enc` had `BATTLESCRIPT: "battle_scene06v.scp"` - the Valens tutorial script, which looks units up by name
(`GetUnitByName("Ludo")`, `"Usus"`, `"Valens"`) and runs a scripted dialogue/turn sequence. The enemy team no longer
contains Ludo, so that script cannot run, and the battle waits on it.

Both script hooks are now removed from the encounter (`CUTSCENE` played `Scene05va.cut` / `Scene06vg.cut`):

```
SCENE:      "imperia\valensschool\Valensschool_noon_cloudLight.scn"
GRIDFILE:   "$l\imperia\valensschool\valensschoolnomove.grd"
CAMERATRACK: "intro","$l\imperia\valensschool\valensschoolIntroAnim.pan"
```

Nothing else in `data/script` references `scene06va` or `battle_scene06v`, so the battle now starts as an ordinary
encounter: normal turn order, and the enemy team is the six affinity rows.

Build: `demo_hero.iso`, 1,392.4 MB, md5 `aa5d52a037c594b240fbe7282352d4d7`.

If it still does not hand over a turn, the next step is to replace the encounter wholesale with a league arena battle
(no `CAMERATRACK`, no script) that carries the same six rows - the campaign only needs the battle to start.

## Revision 3: what the first in-game run proved, and the one-enemy build

The first run reached the battle (after "Ignore for this session" on a Dolphin warning) and spawned **five** enemies, not six:

```
Ageliasa (Air)   Odystus (Dark)   Chenzira (Fire)   Kaethe (Earth)   Ihpicus (Water)
```

Affinities from `data/units/gladiators.txt`. Five distinct affinities, **Light absent** - which is the missing spawn: the
corpus has no Light-affinity gladiator, so the Light row has nothing to draw and silently produces nothing. That is why the
six rows became five units; no spawn was "swallowed" by Valens.

The five-row set is consistent with two different id bases, so it cannot settle them on its own:

| hypothesis | ids 1-5 are | what spawned |
|---|---|---|
| 1-based (Air=1) | Air, Earth, Fire, Water, Light | the four non-Light ids + the -1 control drew Dark |
| 0-based (Air=0) | Earth, Fire, Water, Light, Dark | Earth, Fire, Water, Dark + the control drew Air |

Both explain the same five names. One row on its own settles it: with **only id 1** on the field, the single spawned unit's
affinity is id 1's affinity - Air means 1-based, Earth means 0-based.

### The crash at load

Dolphin reported `Invalid read from 0x0000010C, PC = 0x800ef0ac`. That PC is inside `FUN_800ef09c`, which walks a tagged
script-argument list and dereferences every entry as a node; the previous build called

```
tactics.AddUnitPriorityTarget(hero, hero)
```

with two object arguments, and the argument marshaller followed a null pointer to offset 0x10C. So the crash was the test
script's unverified engine call, not the compiler and not the encounter: the method exists (it is registered), but its
argument shape is not two objects. Revision 3 removes every engine call with an unverified shape and keeps only the language
constructs the corpus proves work (a `for ... to ... step` loop, a nested function, a talk box).

### Revision 3 build

`demo_hero.iso`, 1,392.4 MB, md5 `b8f049f84a0f3d4935304c808d7a9fab`.

- Team 1 is a **single** row: id `1`, chance 100, `Start1`, nothing else. One unit tells us id 1's affinity, hence the base.
- `battle_apitest.scp` (revision 3) compiles to a 1,016-byte `.scb` and reports `APITEST lang-ok` in a talk box.
- The grid has four single-cell start markers (`Start1..Start4`); this grid has no fifth slot, which is why the six-row
  positional idea was abandoned - the props sit on `Start3`/`Start4` anyway.

## Revision 4: the crash is the talk box during init

Revision 3 crashed again (same PC `0x800ef0ac`, address `0x000000D0` instead of `0x0000010C`) and the battle then completed
instantly: the aborted setup left the enemy team empty, which the game reads as an immediate win. So the crash aborts
encounter setup, and the "no unit spawned" symptom is a consequence, not a separate problem.

What is left in revision 3 that could marshal an argument badly? One thing: the talk box.

```
for / nested function / gb.CreateTalkBox(40.0, 10.0, "Usus", "Usus.tga")   <- in OnInitBattle
```

Every `CreateTalkBox` in the entire corpus is inside **`OnExecBattle`** - 695 scripts, not one call from `OnInitBattle` (the
only talk-box call init ever makes is `LoadTalkBoxSoundTable`, which just registers samples). The battle UI does not exist
when init runs, so creating a box there hands the marshaller a null interface and it dereferences offset `0xD0`, exactly the
address Dolphin printed.

Revision 4 therefore makes **no engine call at all**: a `for ... to ... step` loop with arithmetic and a nested function,
storing the results in `shared` variables. Nothing else.

### Revision 4 build

`demo_hero.iso`, 1,392.4 MB, md5 `a9bc3ae571b1986b79d0ed08eb2cfd61`.

- `battle_apitest.scp` revision 4 compiles to a 708-byte `.scb`; the disassembly contains only `FOR`/`NEXT_SYM`/`SKIP`,
  arithmetic, `FUNCLABEL` and symbol pushes - no `CALLFUNC` to any interface.
- Team 1 is one row: id `1`, chance 100, `Start1`.
- No script index file exists (`data/script` has 695 `.scp` and 695 `.scb`, no `.idx`), so the engine resolves the script by
  its path hash inside the archive - which the packer wrote.

### What each outcome means

| Result | Meaning |
|---|---|
| loads clean, one enemy spawns | the crash was the talk box; the enemy's name gives id 1's affinity, hence the id base |
| loads clean, no enemy spawns | the script is fine and the id-1 pool is empty, which would point at a different id base than expected |
| crashes again | not the talk box: next suspect is the script mechanism itself, and the test becomes "recompile a vanilla battle script and ship that" to separate compiler output from new language features |

## Revision 5: the crash predates the script - it is the missing CUTSCENE hook

Revision 4 made no engine call at all and still crashed at `0x800ef0ac` (offset `0x94`). That rules out the script contents,
and the build history says why: the very first build that crashed was **1b**, the one where I *removed* `BATTLESCRIPT` and
`CUTSCENE` to get past the tutorial script.

| build | rows | script hooks | result |
|---|---|---|---|
| 1 `bf0627..` | 6 affinity rows | vanilla `BATTLESCRIPT` + both `CUTSCENE` | reached the battle, never got a turn |
| 1b `25544f..` | 6 affinity rows | **removed** | memory error at `0x800ef0ac`, 5 units |
| 2 `ec95d2b..` | 6 rows | ours + `AddUnitPriorityTarget(hero, hero)` | memory error, 5 units |
| 3 `40c8833..` | 1 row | ours + talk box in `OnInitBattle` | memory error, instant victory |
| 4 `a9bc3ae..` | 1 row | ours, no engine calls at all | memory error, instant victory |
| 5 `787203c..` | 1 row | ours, no engine calls, **cutscene hooks restored** | this build |

So the fault is the removed `CUTSCENE` hook: the school flow plays `Scene05va.cut` before the battle, and with the record gone
something dereferences null. Every revision since has changed *where* the null read lands (0x10C, 0xD0, 0x94) because the
script contents changed, which is why it looked like the script was at fault.

Revision 5 restores both vanilla cutscene lines - and keeps them: the encounters API's `AddField` collapsed the duplicate
`CUTSCENE` key, so `AppendField` was added to `IEncountersApi` for repeatable header keys (`CUTSCENE`, `PRIZETIER`,
`CAMERATRACK`). The header now matches the vanilla one except for `BATTLESCRIPT` (ours) and team 1.

`demo_hero.iso`, 1,392.4 MB, md5 `787203c2ede5610b0f26e6402352fd53`.