# Engine facts (from `bootfile.dol`)

> Machine-readable form: `dol/equipment_rules.json` (tier caps, category check, itemset storage, `ITEMSCOMP`
> positions). That file is the authority; this page is the reasoning behind it.

Evidence for design decisions that depend on how the retail engine actually behaves. Every claim names the function
it came from so it can be re-checked. Source of truth: `bootfile.dol` via Ghidra — **not** `protius.exe`, which is a
development artifact, and not the community docs.

## Item category is `items.tok`, never the itemset tag

`Gladius_Units_IDX_Repack.py` line 322-325 parses an itemset row as:

```python
re.search(r"^(\d*?) (\d*?) (.*?)(\W*?\[.*?\]|$)", line)
tempItemset.append([group(1), group(2), items.index(group(3))])   # min level, max level, item id
```

The bracketed tag is matched and **discarded**. `unititems.idx` is written as `(min:1 byte, max:1 byte, itemId:2
bytes)` per row, `0x0000` terminated — there is no slot or tag field. The engine therefore cannot validate a tag.

What it validates instead, in `FUN_800f96f4` (called from the equip path):

- `item[5]` (**Type** from `items.tok`: Weapon / Armor / Shield / Helmet / Accessory) must equal the category's type
  byte,
- `item[6]` (**SubType**, e.g. `Bow`) must equal the category's subtype unless the category is the wildcard `1`
  (`<Any>`),
- `item[7]` (**Style**, e.g. `Plain`, `Light`, `Archer`) must equal the category's style.

Vanilla agrees with itself: all 9,912 itemset rows carry a tag matching their `items.tok` Type. The tag is a useful
annotation, but a tool that trusts it over `items.tok` will disagree with the engine on modded data.

**Consequence for tooling:** classify gear by `items.tok` Type. Warn when a tag disagrees, because the packer drops it
silently and the engine will follow the item, not the tag.

## Item level caps are tiered, and only at the equip menu

`FUN_800fd694(unit, item)`:

```c
tier = FUN_800f7bd8(school, 0);                    // school grade: 1, 2, 3
if (tier == 1 && item[9] > 5)  return 0;           // Amateur  - items above level 5 cannot be equipped
if (tier == 2 && item[9] > 10) return 0;           // Semi-Pro - items above level 10 cannot be equipped
                                                   // Pro      - no cap
if (!FUN_800f96f4(unit[0x20], item)) return 0;     // item category vs class ITEMCAT
```

`item[9]` is `ITEMMINLEVEL` from `items.tok`.

The tier itself is **not stored in any data file**: `FUN_800f7bd8` derives it from championships completed. Amateur
until the first championship is won, Semi-Pro until the second, Pro afterwards. A starting team is always Amateur;
PvP/tournament teams are Pro. Consequences for tooling: the campaign rosters the Team Builder writes will be Amateur in
normal play, so items above level 5 would not be equippable **by hand** there — but explicit `ITEMSCOMP` gear ignores
the cap, so this stays an advisory note rather than a validation rule.

So the rule is **not** "an item's level must not exceed the unit's level". It is the *school's tier* against the
*item's* minimum level: Amateur ≤ 5, Semi-Pro ≤ 10, Pro unlimited. A level-3 unit may legitimately carry a level-5
weapon.

The cap lives in the equip menu (`UpdateEquipItemMenu` → `FUN_800fd694`), so it governs what the player may equip by
hand. Gear written into a school roster's `ITEMSCOMP` is applied directly — the same handler that reads `ITEMSCOMP`
(`CharacterChangeItem`, slots 1,2,4,3,5 = weapon, armor, shield, helmet, accessory) does **no** category or level
validation. An itemset or a roster can therefore grant any item at any level, which is why team and school content
must be validated by the tooling rather than by the engine.

**Consequence for tooling:** do not refuse gear because its level exceeds the unit's level. Do warn when gear would
be unequippable by hand in the current tier, and do enforce the weapon requirement, because nothing else will.

## Weapon slots are positional

`ITEMSCOMP` is five item names, applied to slots in order. The engine does not check that the item in the weapon slot
is a weapon. A helmet written in position 1 sits in the weapon slot, and a unit with no usable weapon does not attack
— the same failure that a bow class with a sword shows.

**Consequence for tooling:** when writing `ITEMSCOMP`, place each item in the position its `items.tok` Type belongs
to, and never leave a class that needs a weapon without one.

## `UNITDB` rows and the requirement test

Two functions settle what a `UNITDB` row means, and they are not the same struct.

`EncounterSetup` (`0x80106ddc`), case `0x1d`, reads exactly 21 values in this order and type:

| Slot | Reader | Meaning seen in the code |
|---|---|---|
| 1 | `FUN_800c41d8` (string) | unit name; empty means "generate from the school" |
| 2 | `FUN_800c3fb4` (int) | `(rand % 100 + 1) <= value` gates the spawn; `100` therefore always spawns |
| 3 | string | start marker, handed to unit creation |
| 4, 5 | ints | **level bounds as offsets**: `EncounterSetup` adds `FUN_800f7c50`'s result to each, then `FUN_80108828` stores them on the created unit at `+0x10` / `+0x14` |
| 6 | int | level jitter: non-zero adds the school average a second time, independently for each bound, clamped to `>= 1` |
| 7, 8 | ints | requirement values (see below) |
| 9-12 | four strings | class tags accumulated on the unit |
| 13-16 | ints | normalised to 0/1 and used as the four prohibition flags |
| 17 | int | passed to unit creation |
| 18 | int | passed to unit creation |
| 19 | int | passed to unit creation |
| 20, 21 | ints | non-zero OR `0x100` / `0x200` into the spawn flags |

**Correction (second pass).** Slots 4-6 *are* level bounds, and the earlier reading of this file was wrong to call them
coordinates: `FUN_800f7c50(seed, school)` walks the school's roster (levels at `+0x90`), computes its average and clamps it
by difficulty (`DAT_803c44ac` ? 5, 10, 13, 15, 18, 30), and `EncounterSetup` adds that average to slot 4 and slot 5. When
slot 6 is non-zero it adds the average a second time, once per bound, and clamps both to `>= 1`, which is exactly the
"enemies spawn a couple of levels either side of the school average" behaviour the randomizer depends on. Placement comes
from the start marker on the arena grid, not from these slots. `devnotes.txt` was right about the levels and wrong about
their slot numbers.

`FUN_8010ad40(requirement, unit)` tests a requirement record:

- `+0x00` / `+0x04` - minimum / maximum level, compared against the unit's level at `+0x90`.
- `+0x08` - one integer holding three masks: `0x103f` attributes, `0xf00` class tags, `0xc0` unclaimed bits. The attribute
  and class-tag groups are each inverted by their own prohibit flag.
- `+0x0c` - affinity id, compared against the unit's affinity byte at `+0x82` (`-1` skips the test).
- `+0x11`, `+0x12`, `+0x13` - prohibit-classes, prohibit-attributes, prohibit-affinity flags.
- `+0x14` - up to four class names, matched with `FUN_800f95a0` / `FUN_800f95d0`.
- `+0x1c` - tri-state checked against bit 1 of the unit's `+0x9c` byte: `2` requires the bit, `1` requires it clear.

The level fields in that struct do **not** come from a `UNITDB` row (a row has coordinates at those positions), so the
requirement record is a different, recruit/roster-shaped structure. Slot 7 is nonetheless the same three-mask integer, which
is why `EncounterUnitFields.SplitMask` exists.

**Affinity names** sit as one contiguous block of strings at `0x80489768`: `Air`, `Earth`, `Fire`, `Water`, `Light`, `Dark`
(each preceded by `None`, `Basic`, `Chain`, `Special`, `Attack` for other enums). The ids are therefore index-based in that
order. `devnotes.txt` claims "5 would be fire", which does not fit that order, so the note is unverified; the base offset
(`None` = 0 or 1 = `Air`) is **still open**, and the first in-game attempt to pin it did not work:

- a single row with id `1` spawned Juventas, an **Earth** gladiator;
- a single row with id `0` spawned Meadhbh, a **None** gladiator.

No monotone index into this block produces both (0 = Air predicts Air for the first; 1 = Air predicts Air for the second).
The likely reason is the *school pool*, not the enum: the test put those rows in a story battle whose team uses
`SCHOOL: "REGIONSCHOOL"` - a token that probably does not resolve outside a league arena - so the affinity filter cannot be
satisfied and the game draws an arbitrary unit. A story battle is the wrong vehicle for this test.

Three tables are now in play and must not be conflated:

| table | values | where |
|---|---|---|
| unit data field (3 bits) | `None, Air, Dark, Earth, Fire, (5 = invalid), Water` | `gladiators.txt` / the tools' `AFFINITY` list |
| DOL name block | `Air, Earth, Fire, Water, Light, Dark` | `0x80489768`, used by some affinity enum |
| encounter requirement | ids `-1` (6298 rows), `0` (1), `1` (49), `2` (95), `3` (15), `4` (107), `5` (139) | `UNITDB` slot 8 |

The corpus uses `1`-`5` heavily and the unit data has **no Light-affinity unit at all** (0 of 2170), so Light cannot be one of
the five legal requirement ids: the requirement enum is almost certainly the five affinities units actually have
(`Air, Earth, Fire, Water, Dark`) in some order, with `-1` for "any". The pairing within those five still needs a per-id
observation in a battle whose school pool resolves - a league arena, one marker per id.

## The two runtime school tokens are data-resolvable - no DOL edit needed

`HOMETOWNSCHOOL` and `REGIONSCHOOL` appear only inside `.enc` files, and an earlier pass concluded they were engine-resolved
with nothing in the data to read. That was wrong: `data/config/world.tok` is the region ? town ? arena table
(4 `REGION` blocks, 44 towns, 44 arenas), and every arena owns a league file whose `SCHOOL` lines list its town's schools.

- `HOMETOWNSCHOOL` = the arena's own league-file list. `pirgosarena_leagues.tok` yields 8 schools.
- `REGIONSCHOOL` = the union of the league-file lists of every arena in the region. Imperia yields 30 schools.
- The encounter path already names both (`data/encounters/<region>/<arena>/<file>.enc`), so the editor resolves the tokens
  with no configuration at all.

`WorldRegions` implements this (`Read`, `ForArena`, `ArenaSchools`, `RegionSchools`) with six tests against the corpus, and
the `encounters` driver's `fields` action now returns `region`, `arena`, `resolvedSchools.homeTown`, `resolvedSchools.region`
and a per-team `resolvedSchools` for teams that use either token.

## Attribute bits, from the engine

`UnitConditionalLookUpAttr` (`0x800f1444`) matches the tokens `unitattr req <NAME>` / `unitattr ign <NAME>` and ORs a bit
into the required / ignored mask. It is the authority for the attribute bit numbers, and it confirms `devnotes.txt`'s
"Heavy = 4":

| Attribute | Bit | Attribute | Bit |
|---|---|---|---|
| Light | `0x1` | Beast | `0x20` |
| Medium | `0x2` | Male | `0x40` |
| **Heavy** | `0x4` | Female | `0x80` |
| Arcane | `0x8` | Nordagh | `0x100` |
| Support | `0x10` | Imperia | `0x200` |
|  |  | Expanse | `0x400` |
|  |  | Steppes | `0x800` |

`EncounterUnitFields.AttributeTable` holds this, with `AttributeBitFor(name)` and `AttributeNamesFor(mask)`; three tests
pin it, including that `1056 = Beast|Expanse` and that `4224 = Female` plus `0x1000`, for which **no attribute claims a
name** - the requirement test masks `0x103f`, so `0x1000` is tested as an attribute bit but is not one of the twelve named
tokens.

The requirement test groups these differently from the attribute list: `0x103f` covers Light..Beast and `0x1000`; `0xc0`
(Male|Female) is a separate presence test; `0xf00` (Nordagh|Imperia|Expanse|Steppes) is tested against the unit's class
tags and inverted by the prohibit flag.