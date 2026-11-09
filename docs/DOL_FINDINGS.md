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
