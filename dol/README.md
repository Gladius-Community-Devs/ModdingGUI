# `dol/` — engine truth extracted from the game executable

These JSON files describe the game's **own** field tables, read out of the GameCube
`bootfile.dol`. They are the authority on what the engine accepts, which documentation
and observed corpus data cannot be: a modded file happily repeats a field the engine
merely overwrites, and a typo'd field name parses fine but is silently ignored in game.

| File | Contents |
|---|---|
| `skills_fields.json` | 99 `skills.tok` field names with engine id, parser, argument shape, slot kind, repeat behaviour and a note |
| `classdefs_fields.json` | 27 `classdefs.tok` field names with engine id |

## How they were extracted

The engine resolves tok keys by scanning a **NULL-terminated array of
`{ const char* name; u32 fieldId; }`** pairs, then switching on the id:

| File format | Field table | Parser |
|---|---|---|
| `skills.tok` | `0x802EB0B8` (99 entries) | `SkillLoadTokenFile` (`0x8002b678`) and the battle/effect parser `FUN_80026ec0` |
| `classdefs.tok` | `0x803038B0` (27 entries) | `ClassDefinitionsLoad` |

The extraction walks each table until the null terminator, decodes the name pointer
through the DOL section map and reads the id. Some names point into a second string
pool (`0x8048xxxx`) — `MESH:` and `FX:` live there, which is why an early pass listed
them as undecoded.

Token behaviour worth knowing (all read from the parsers):

- `FUN_800c3ab4` is the tokeniser: it `strcmp`s the key against the file's table,
  falls back to a global table, and returns id `0` when nothing matches — the caller's
  `switch` then hits `default:` and **skips the line**. Unknown fields are never errors.
- Typed reads (`FUN_800c3fb4` int, `FUN_800c40dc` float, `FUN_800c41d8` string) return
  0 on failure and the record load aborts, so a wrong *value* type is fatal where an
  unknown *field name* is not.
- `UnitConditionalModify` ORs a flag word and appends to a class bitmask
  (`FUN_800f9608` = `*a |= *b`), so conditional fields accumulate.
- The paired status fields each own one slot; the engine has exactly two.

## Verification

Every rule these tables feed was checked in game, not only in the disassembly
(see `skills/README.md` for the four experiments):

- a skill's 3rd `class:` condition still fired → conditional fields accumulate;
- `unitattr req MALE` held alongside `enemy only` → both target conditions apply;
- 1 status applied, 2 applied, a **3rd froze the game** → two status slots, hard limit;
- `SKILLEFFECTCONDITION` filtered targets with **and without** `SKILLEFFECTRANGE`.

## Regenerating

The tables are small and stable; they were produced with a short Python script over
`bootfile.dol` (parse the 7 text / 11 data section descriptors, map VA→file offset,
walk the `{name, id}` array). If a different game build is ever targeted, re-run that
walk against its DOL and diff — `classdefs_fields.json` in particular should be checked,
since its table lives at a different address.
