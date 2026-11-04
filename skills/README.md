# Skills Catalog

This folder holds the skill-field catalog used by Core (`SkillsCatalog`) and the
Skill Editor UI to populate field lists, dropdowns and word-banks — and, since
Phase 2.7, the structural rules that `ISkillsApi.Validate()` reports on.

## Files

| File | Purpose | Versioned? |
|---|---|---|
| `skills.tok` | Vanilla skills source (read-only input). | **No** — copyrighted game data |
| `modded-skills.tok` | Largest stable community-overhaul skills source (read-only input). | **No** — same reason |
| `skills_field_reference.txt` | Hand-written field documentation and rules. The structural source of truth. | Yes |
| `Gladius Effects and Statuses.txt` | Modder reference notes describing each field. | Yes |
| `build_catalog.py` | Generator script. Re-run whenever inputs or the reference file change. | Yes |
| `skills_catalog.json` | **Generated** catalog consumed by Core and the GUI. | Yes |
| `skills_catalog_overrides.json` | User/community additions. Survives regeneration. | Yes |

The two `.tok` inputs are `.gitignore`d (`skills/*.tok`). Without them the
generator warns and skips enumeration input, but the field reference, overrides and
structural rules still produce a usable catalog. Drop the `.tok` files in this
folder to restore full value enumeration.

## Regenerating

```
py -3 skills/build_catalog.py
```

The script merges values from both `.tok` files, folds the structural rules from
`skills_field_reference.txt`, applies `skills_catalog_overrides.json` additions
(set-union; nothing is ever removed), then measures every documented rule against
the observed corpus. Output is deterministic — identical inputs produce a
byte-identical `skills_catalog.json`.

Do not hand-edit `skills_catalog.json`: add values via the overrides file and rules
via the reference file, so regeneration does not clobber them.

## Catalog shape (format version 2)

```jsonc
{
  "_meta": {
    "format_version": 2,
    "generated_from": ["skills.tok", "modded-skills.tok"],
    "reference": { "file": "skills_field_reference.txt", "fields_documented": 68, ... },
    "rule_support": { "records_scanned": 3638, "multi_flipped_from_data": 9, ... }
  },
  "fields": {
    "<FIELDNAME>": {
      "enumerate": true | false,
      "note":      "human-readable note (catalog policy or reference NOTES)",
      "format":    "\"name\", chance, value",          // from the reference file
      "multi":     true | false,
      "multi_source": "observed",                      // only when data overrode MULTI: no
      "mirrors":   ["SKILLSTATUS", "SKILLSTATUS2", ...],
      "links":     ["SKILLAFFCOST"],                   // must appear together (LINK)
      "links_strict": false,                           // false => advisory (data disagrees)
      "link_violations": 59,
      "requires":  [ { "field": "SKILLATTRIBUTE", "equals": "teleport",
                       "strict": false, "violations": 1 } ],
      "numeric":   { "type": "int", "min": 1, "max": 30 },   // when applicable
      "reference_only": true,                          // documented but never observed
      "slots": [
        {
          "types":  ["string"] | ["int"] | ["float"] | ["int","float"],
          "values": ["sorted", "unique", "observed", "string", "values"],
          "enumerate": false,          // optional, slot-level override
          "note":      "..."           // optional, slot-level note
        }
      ]
    }
  }
}
```

Notes:

- **Mirror collapsing.** Numbered duplicate fields (`SKILLSTATUS2`,
  `SKILLSTATUSCONDITION3`, ...) are merged into their unsuffixed base
  field. The original observed names are kept in `mirrors`.
- **Slots are positional.** Slot 0 is the first comma-separated argument
  after `FIELD:`, slot 1 the second, etc.
- **String slots** hold the union of observed values and values documented in the
  reference file. Placeholders (`<Any>`, `class: <name>`, `skill: <skill name>`,
  `(same list as …)`) are dropped.
- **Numeric slots** record only their type(s); no enumeration.
- **`SKILLCREATE` slot 0** is the internal skill name (free-form) and
  is intentionally not enumerated. Slots 1 (skill category) and 2
  (skill subcategory) are enumerated normally.

## Fields sourced externally (not enumerated here)

Resolved at runtime from the project, not from the catalog:

- `SKILLUSECLASS` → `data/config/classdefs.tok`
- `SKILLDISPLAYNAMEID`, `SKILLDESCRIPTIONID` → `data/config/lookuptext_eng.txt`
- `SKILLMETER` → `data/config/swingmeter.tok` + values observed in the project's own
  `skills.tok` (the previous `skillmeter.tok` filename never existed; it is still
  accepted as a legacy alias)
- `SKILLANIM`, `SKILLANIMTIME`, `SKILLFXSWING`, `SKILLSOUND` → animation / FX / sound files
- `SKILLCOSTS`, `SKILLAFFCOST`, `SKILLCOMBATMODS` → free-form numeric input

## Numeric-only fields

- `SKILLLEVEL` — integer (1–30; vanilla also uses 0 for innates)
- `SKILLJOBPOINTCOST` — integer; -1 hidden, 0 free, ≥1 costs that many JP

## Engine truth: `dol_skill_fields.json` (authority on legality)

`dol_skill_fields.json` is extracted from the GameCube boot DOL (`SkillLoadTokenFile`
and its battle-data sibling `FUN_80026ec0`): every field name the engine's tokenizer
recognises, its internal id, its argument shape, and how repeated lines behave. The
generator folds all of it into the catalog, so nothing about repetition is guessed
from the corpus:

| Engine behaviour | Catalog | Validation |
|---|---|---|
| Accumulating field — bit-sets (`SKILLATTRIBUTE`, `SKILLPROJECTILEATTR`, `SKILLPROJECTILESEQUENCE`), class sets (`SKILLUSECLASS`, all `class:` conditions) and conditional modifiers (`SKILL*CONDITION`, `SKILLTARGETCONDITION`, `SKILLSPLITEFFECTCONDITION`) | `accumulates: true`, `max_occurrences: null` | repeats are **legal** |
| Paired status slots (`SKILLSTATUS`/`SKILLSTATUS2`, duration/interval/target/uselimit/chance) | one slot each, `max_occurrences: 1` per spelling — two statuses total | a **third status line freezes the game** → `exceeds-engine-slots` (other paired fields just overwrite) |
| Scalar fields (ids, names, floats) | `max_occurrences: 1` | a second line overwrites → `exceeds-engine-slots` |
| Field names absent from the engine table | `engine: false` | `field-not-in-engine` (the game ignores the line) |
| Engine fields with no vanilla data (`SKILLWEAPONEXC`, `SKILLEFFECTVALUE2`, …) | `engine_only: true` | documented, usable |

Verified in game (three ISO tests, Oct 2026):

- **Light Beam**: a skill's 3rd `class:` condition still fired (the *last* line was
  `DarkLegionnaire`, yet `DarkCat` took the 10% split damage) → conditions accumulate,
  nothing is discarded. My earlier "last wins" reading was wrong.
- **Beguile**: `unitattr req MALE` + `enemy only` both hold — the Amazon could target
  male enemies but **not** the female one.
- **Status slots**: one `SKILLSTATUS` applied (confusion), two applied (confusion +
  blind), and a **third froze the game**. So the status pair is exactly two slots and
  overflowing it is a hard crash, not a silent overwrite — the validator says so.

Engine-only aliases are also recorded (`SKILLEXPCOST` ≡ `SKILLJOBPOINTCOST`; five
`SKILLSTATUSSITUATION*CONDITION` spellings share one id), and field names that appear
only in modded data but not in the engine table are flagged (e.g.
`SKILLSTATUSSITUATION`, a typo in `modded-skills.tok`).

## Documented rules that the corpus contradicts

`skills_field_reference.txt` also holds `LINK` / `REQUIRES` intent, which the engine
itself never enforces (the loaders only write per-field slots). Where the corpus
contradicts such a rule the rule is **kept but marked advisory** (`links_strict: false`
/ `strict: false` plus a `violations` count); Core reports those as `Info` rather than
`Warning`. Fix the reference file, regenerate, and the rule becomes strict again.

| Rule | Why it is advisory |
|---|---|
| `LINK SKILLAFFINITY ↔ SKILLAFFCOST` | Affinity attacks with no cost exist (`SKILLAFFINITY: "Air"`), and cost-only records exist too — the two slots are independent |
| `SKILLEFFECTCONDITION REQUIRES SKILLEFFECTRANGE` | 183 observed records violate it; the reverse also fails, so they are independent |
| `SKILLSTATUSTARGET REQUIRES SKILLSTATUS` | 8 records (e.g. `Dervish SurpriseAttack`) carry a status target with no status |
| `SKILLMOVERANGE REQUIRES SKILLATTRIBUTE = teleport` | `translateanim` is also valid (`Galverg Uppercut`) — the rule should be "requires `SKILLATTRIBUTE` present" |

## Adding a newly-discovered value

Edit `skills_catalog_overrides.json`:

```json
{
  "fields": {
    "SKILLEFFECT": {
      "slots": [
        { "values": ["My Newly Found Effect Name"] }
      ]
    }
  }
}
```

Then re-run `py -3 skills/build_catalog.py`. Done.
