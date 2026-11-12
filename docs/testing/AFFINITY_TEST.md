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

## Revision 6 and the vanilla control: the talk-box sound table

Restoring the cutscene hooks changed nothing, so the crash is not the hook either. What separates the builds that worked from
the ones that crash is now precise:

| build | battle script | crash |
|---|---|---|
| 1 | vanilla `battle_scene06v.scp` | **no** |
| 1b | none | **yes** |
| 2-5 | ours (various) | **yes** |

The vanilla tutorial's `OnInitBattle` opens with five interface lookups and then, on line 24, `gb.LoadTalkBoxSoundTable("06V")`.
Our revisions 2-5 never did that, and neither does "no script at all" - so both crash, which is exactly the observed pattern.

The crash happens while the **loading screen** is up, and that screen shows the "Ask Usus" tutorial tooltips. Those tooltips are
talk boxes; talk boxes need the sound table; without it the tooltip path dereferences null, at `0x800ef0ac` with a small offset
(`0x94`, `0xD0`, `0x10C`) depending on what is being marshalled.

Revision 6 performs the same setup as the tutorial (five interface lookups plus `gb.LoadTalkBoxSoundTable("06V")`), then the
language test. Its `.scb` now contains `CALLFUNC InterfaceGlobals`, `InterfaceInput`, `InterfaceBattle`, `InterfaceMapCursor`,
`GetSchool`, `LoadTalkBoxSoundTable` - matching the vanilla init's shape.

Also moved the single enemy row to `Start2` so it no longer stacks on Valens (`Start1`).

### Two ISOs for one round

| file | contents | purpose |
|---|---|---|
| `demo_hero.iso` / `apitest_r6.iso`, md5 `39dcdd02.` | revision 6: our script with the talk-box setup, one enemy on `Start2` | if the crash is gone, the sound table was the fault and the single enemy's name settles the affinity base |
| `vanilla_control.iso`, md5 `b75f6539.` | the battle put back to vanilla (vanilla `BATTLESCRIPT`, Ludo on `Start2`, vanilla grid) | if *this* also crashes, the fault is not the encounter at all but something about how this ISO is packed |

## Revision 7: the missing OnExecBattle entry point

The owner reported the crash lands exactly when the battle finishes loading and "Press A to Continue" appears - i.e. when the
battle starts *executing*, not during init. That reframes it away from the loading-screen tooltips.

Every battle script in the corpus defines `OnExecBattle`, the callback the engine drives once the battle is running:

| script | entry points |
|---|---|
| `battle_scene06v.scp` | OnInitBattle, OnStartBattle, OnExecBattle, OnSkirmishEnd, OnKillBattle, OnExecComboMeter |
| `battle_practice.scp` | OnInitBattle, OnStartBattle, OnExecBattle, OnSkirmishEnd, OnKillBattle |
| `battle_anteupbyo10000.scp` | OnInitBattle, OnExecBattle |

`OnStartBattle` is optional; `OnExecBattle` is not. Our script defined only `OnInitBattle`, so the engine was left holding a
null entry point, and the failure surfaced inside the script value dereference helper (`FUN_800ef09c`, PC `0x800ef0ac`) the
moment the battle began executing. That also explains both controls: the vanilla tutorial defines `OnExecBattle` (no crash),
and "no script at all" defines nothing (crash).

Revision 7 declares the same set, empty except init - and init keeps the vanilla-style setup plus the language test. The
compiled `.scb` exports `OnInitBattle`, `OnStartBattle`, `OnExecBattle`, `OnSkirmishEnd`, `OnKillBattle`, `OnExecComboMeter`.

`demo_hero.iso` / `apitest_r7.iso`, 1,392.4 MB, md5 `ad8ef389a74f3bf3c1e2a15f4560c2b8`.

The `vanilla_control.iso` from the previous round (md5 `b75f6539.`) is still on disk and still useful: if revision 7 fails,
that control says whether a fully vanilla battle inside this pack loads at all.

## The id table is 0-based - settled in game

Revision 7 put a **single** enemy row (id `1`, no control row) on the field and it spawned **Juventas**, which
`data/units/gladiators.txt` lists as `Affinity: Earth`.

With no unrestricted row in play there is nothing else the Earth unit can have come from, so:

| id | affinity |
|---|---|
| 0 | Air |
| 1 | **Earth** (observed: Juventas) |
| 2 | Fire |
| 3 | Water |
| 4 | Light (no gladiator has it - never spawns) |
| 5 | Dark |

That is a straight 0-based index into the DOL's contiguous name block at `0x80489768` (`Air, Earth, Fire, Water, Light,
Dark`), which is what the earlier fuzz run implied but could not prove: with the six-row build the observed set
`{Air, Earth, Fire, Water, Dark}` fitted both a 0-based and a 1-based reading, because the unrestricted `-1` row supplied the
sixth value. One row without a control removed that ambiguity.

`devnotes.txt`'s "5 = fire" is wrong on both readings; there is no interpretation in which 5 is Fire.

`docs/DOL_FINDINGS.md` line 121 said the base was still unproven; it is now proven from the game, not inferred from the DOL.

Revision 8 (`29f593b7.`) puts a single row with id `0` on the field, the only id not yet observed, to confirm Air.

## The load-end crash is still unexplained, and no longer blocks the data

Revision 7 declared every entry point the corpus uses (`OnInitBattle`, `OnStartBattle`, `OnExecBattle`, `OnSkirmishEnd`,
`OnKillBattle`, `OnExecComboMeter`) and still faulted at load end - so the missing `OnExecBattle` was not it either.

What is now established, with the timing the owner supplied ("right as Press A to Continue appears" = battle starts executing):

- vanilla tutorial script: no crash
- our script (six different shapes, including one with no engine calls): crash
- no script at all: crash

The tutorial tooltips come from `data/config/scriptedscenes.tok`, which holds the whole "Ask Usus" sequence under the scene id
`06V` (with moods `USS06U001`... and locations `06VA_G`, `06VH`). The sequence is not the battle script, but the battle is what
plays it, and the sequence reaches into the battle at the "press to continue" step - which is where the null dereference
happens (`FUN_800ef09c`, PC `0x800ef0ac`).

The next probe is therefore to keep the vanilla `BATTLESCRIPT` name (`battle_scene06v.scp`) and change only its contents -
name-based lookups stay intact while the tutorial dialogue is replaced by a script that defines every entry point and does
nothing else. If that loads clean, the crash is a name-keyed lookup, and the fix is to overwrite the vanilla script rather
than point at a new one.

## Retraction: the in-game readings do not establish an id table

Revision 7 (single row, id `1`) spawned **Juventas**, which `gladiators.txt` lists as Earth. Revision 8 (single row, id `0`)
spawned **Meadhbh**, listed as **None**.

Neither a 0-based nor a 1-based index into the DOL block explains both:

| model | predicts id 0 | predicts id 1 | observed |
|---|---|---|---|
| 0-based `Air, Earth, ...` | Air | Earth | None, Earth |
| 1-based `Air, Earth, ...` | (out of range = any) | Air | None, Earth |

The 0-based claim recorded earlier in this file is **withdrawn**. The most likely explanation is not the enum but the pool:
this story battle's team uses `SCHOOL: "REGIONSCHOOL"`, which probably resolves only in a league arena. With no valid pool the
affinity requirement cannot be satisfied and the game draws an arbitrary unit - which also explains why the six-row run
produced five units with five different affinities (Air, Earth, Fire, Water, Dark): that is what "draw anything" looks like.

Consequence for the test design: the affinity experiment needs a battle whose school resolves (a league arena with
`HOMETOWNSCHOOL`, or an explicit school name), and one marker per id so each unit's origin is known. The story tutorial is the
wrong vehicle for it, and it is also the battle that keeps faulting at load end - a null school pool would explain that too,
since the spawn code would dereference a null school object inside the script VM, which is exactly where the fault PCs sit
(`0x800ef0ac`, `0x800ed4a4`, `0x800ed4dc`, `0x800ed530`, `0x800ed538`, `0x800ef0c0`, `0x800ed0f0`, `0x800eff38` - all in the
script value/allocator helpers).

Build 1 keeps its own explanation: it used the **vanilla** tutorial script, which spawns Ludo by name and never consults a
school pool, so no null pool was ever dereferenced.