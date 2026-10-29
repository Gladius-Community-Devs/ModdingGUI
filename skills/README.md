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

## Rule support: data beats documentation

`skills_field_reference.txt` is hand-written intent; `skills.tok` is what the game
actually ships. The generator measures every documented `MULTI`, `LINK` and
`REQUIRES` rule against all observed records. When the corpus contradicts a rule the
rule is **kept but marked advisory** (`links_strict: false` / `strict: false` plus a
`violations` count) instead of being silently deleted.

Core reports advisory findings as `Info` and strict ones as `Warning`, so
`Validate()` never flags a valid vanilla record. Fix the reference file, regenerate,
and the rule becomes strict again automatically.

Current advisory set (vanilla + modded corpus):

| Rule | Why it is advisory |
|---|---|
| `MULTI: no` on `SKILLTARGETCONDITION`, `SKILLPROJECTILEATTR`, `SKILLUSABILITYCONDITION`, `SKILLEFFECTSTATUSCOND`, `SKILLEFFECTSKILLCOND`, `SKILLSPLITEFFECTCONDITION` | These fields repeat in real records — `MULTI` is forced to `true` from observation |
| `LINK SKILLAFFINITY ↔ SKILLAFFCOST` | Affinity attacks with no cost exist (`SKILLAFFINITY: "Air"`), and cost-only records exist too |
| `SKILLEFFECTCONDITION REQUIRES SKILLEFFECTRANGE` | 183 observed records violate it |
| `SKILLSTATUSTARGET REQUIRES SKILLSTATUS` | 8 records (e.g. `Dervish SurpriseAttack`) have a status target with no status |
| `SKILLMOVERANGE REQUIRES SKILLATTRIBUTE = teleport` | `translateanim` is also valid (`Galverg Uppercut`) |

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
