## 0. Architecture (locked)

- **Three projects:**
  - `ModdingGUI.Core` — parsing, schemas, Apis, history, locking. No UI, no shell deps.
  - `ModdingGUI` — WinForms shell. Treated as a **parameter builder** over Core Apis.
  - `ModdingGUI.Mcp` — stdio MCP server. Thin tool surface over Core Apis.
- **API discipline:** stateless `open → mutate → write`. Per-project `ProjectLock`. Timestamped backup to `<project>/.mod-history/<rel>/<yyyyMMdd-HHmmss-fff>.bak` on every write.
- **Round-trip fidelity:** every `.tok`/`.enc` re-serializes byte-for-byte on no-op. Unknown tags + comments preserved.
- **JSON:** `System.Text.Json` only.
- **Tok I/O:** ISO-8859-1, for `.tok` *and* every `data/**/*.txt` table. The pack tools read them as ANSI (`Update_Strings_Bin.py` uses `encoding="ansi"`), so UTF-8 is never correct: reading a Latin-1 table as UTF-8 replaces high bytes with U+FFFD and writing re-encodes them as three bytes (this corrupted 456 bytes of `lookuptext_eng.txt` before it was fixed).
- **Test bar:** 557 passed / 0 failed / 0 skipped with real-data fixtures present (a `vanilla.iso` plus a `REALDATA_*` project folder in the build output — see `AGENTS.md`); the 16 `RealGameDataTests` skip without them (541 passed / 0 failed / 16 skipped). Every phase ends with green tests.

## 1. Format coverage (final scope)

Realistic schemas/Apis to build:

| Group | Files | Schema/API needed |
|---|---|---|
| Items | `data/config/items.tok` | ✅ done |
| Skills | `data/config/skills.tok` | freeform `ISkillsApi` + catalog (Phase 2.7) |
| Lookup text | `data/config/lookuptext_<eng\|fra\|ger\|ita\|spa>.txt` (no `.tok` exists) | `ILookupTextApi` — ✅ done (Phase 3). The `.bin` is **compiled at pack time**, English only by `Update_Strings_Bin.py`, all five by `tools_v008.exe strings`, so the `.txt` is the editing source and `.bin` is never hand-edited |
| Class defs | `data/config/classdefs.tok` | `IClassesApi` — ✅ done (Phase 3) |
| Skill meter | `data/config/swingmeter.tok` (`skillmeter.tok` never existed; accepted as a legacy alias) | read-only helper inside `SkillsContext` (Phase 2.7) |
| Encounters | `data/encounters/*.enc` | `IEncountersApi` (Phase 4) |
| Leagues | `data/towns/leagues/*.tok` | `ILeaguesApi` (Phase 4) |
| Shops | `data/towns/shops/*.tok` (59 files) | **No `ITownsApi`**: `data/towns` has no
  `towns/*.tok` at all — it holds `shops/`, `leagues/` and `schools/*.tnt`. Shop files are covered
  today by `IConfigTableApi` (repeated keys + raw values, so the header keys and the `ITEM` rows are
  readable and writable). A typed `IShopsApi` waits for a consumer (shop editor or randomizer stock)
  and, before naming anything, the same DOL pass `UNITDB` got: `Shop`/`GetShopName` are script
  bindings, not the reader, so the 12 `ITEM` values are still unverified |
| Schools | `data/school/*.tok` | `ISchoolsApi` (Phase 4) |
| Units (txt) | `data/units/*.txt` (gladiators, statsets, skillsets, itemsets, tints, schools) | **done (Phase 5):** `IGladiatorsApi`, `IStatSetsApi`, `ISkillSetsApi`, `IItemSetsApi`, `ITintSetsApi`, `ISchoolNamesApi`, over `UnitTextFile` + `SectionedFile` |
| Worldmap / fonts / hotkeys / options | `data/config/*.tok` | **Not built, deliberately.** No consumer
  needs them yet; Phase 6 (randomizer) will name the ones it actually reads rather than growing four
  speculative Apis |
| Scripts `.scp` | compiled | **out of scope** |
| Character `.pan` / `.bin` | binary | **read-only helpers only**, used by Skills animation resolver |
| Grids `.grd` | binary | `ModdingGUI.Core.Grids.GridFile`; two layouts decoded from the DOL, both read/write, 430/430 round-trip byte for byte |

This is the universe. ~12 schemas + units `.txt` family.

## 2. Phase plan (locked sequence)

### Phase 2.6 — Items GUI on `IItemsApi` (done)
Close the last GUI-bypasses-Core path for items.

- Rewrite `frmMain_partials/ItemEditor.cs` to call `IItemsApi.List/Get/Patch/Add/Remove/Rename`.
- Remove or quarantine the legacy `Item.ToTokFormatString()` write path so it's unreachable from the UI.
- Item editor controls become a parameter builder: one `ItemDto` in, one `ItemPatch` out.
- Use `RecordSchema<Item>` decode for typed field display; raw preservation for unknown fields.
- All saves go through `ChangeHistoryService` (real `.bak` files).
- **Exit criteria:** legacy item write path unreachable from UI; baseline tests + Items Core tests green; manual smoke test on `clean-bec-unpacked` items.tok.

### Phase 2.7 — Skills: catalog-driven, freeform, no rules engine
Bring Skills up to the same architectural bar as Items, without a typed schema.

> **STATUS: implemented (code-complete; manual smoke pending).**
> Delivered: catalog format v2 with structural rules folded in from
> `skills_field_reference.txt`; `ModdingGUI.Core.Skills` (`SkillsCatalog`,
> `SkillsValidator`, `SkillsContext`); `ISkillsApi.Validate` plus `Warn`/`Reject`
> overloads of `Add`/`Update`; GUI validation surfacing + a `Validate` button;
> 35 new Core tests. Suite: 251 passed / 0 failed / 0 skipped (was 216).
> Repeat/cardinality rules now come from the **DOL** (`dol/skills_fields.json`),
> not from corpus frequency — see "Delivered — deviations and decisions".
> Deviations from the text below are listed at the end of this section.

**Catalog model**
- Catalog ships at `<exe>/skills_catalog.json` (root next to exe, not under `skills/`).
- `skills_catalog_overrides.json` is Python-input only; never shipped.
- `build_catalog.py` is the only producer. C# side only consumes. No GUI regeneration button.
  - **OPEN QUESTION (decide before this phase):** a modder with a *modded* `skills.tok` (new field values) needs the catalog regenerated against their files, but they won't use OpenCode/Python. Decide how a non-developer regenerates a catalog: GUI button shelling out to bundled `build_catalog.py` (like unpack/pack via BATGeneration), a double-clickable `regenerate-catalog.bat`, or keep dev-only and accept stale dropdowns for modded values.
- No version field. C# tolerates unknown JSON fields silently.

**Core changes**
- New `ModdingGUI.Core.Skills.SkillsCatalog` (typed model + `Load(path)`).
- New `ModdingGUI.Core.Skills.SkillsContext` exposing project-relative dynamic resolvers:
  - `MeterIds(projectPath)` — from `skillmeter.tok`
  - `AnimationsForMeshes(projectPath, classNames)` — classdefs → mesh → `.pan`
  - Future: `LookupTextIds(projectPath)`, `ItemNames(projectPath)` (no-ops until Phase 3/2.6 wired).
- `ISkillsApi` gains additive method `Validate(SkillDto, SkillsCatalog) → IReadOnlyList<SkillValidationIssue>`. Warnings only. Never blocks.
- `Add` / `Update` accept optional `SkillValidationMode { None, WarnInLog, RejectOnError }`. GUI uses `WarnInLog`. MCP defaults `None`, opt-in `RejectOnError` via `strict: true`.
- `SkillsApi` stays raw-line-based. **Permanent.** No `RecordSchema<Skill>` ever.

**GUI changes**
- `SkillEditorView` keeps current UX (skill picker, identity panel, Skill Box source-of-truth, Add Field helper, dynamic dropdowns, quoting rules).
- Inner catalog/resolver types move out of the view into Core. View becomes a thinner consumer.
- Status bar shows "Validation: N warnings" with click-through detail. Save never blocked.

**Tests**
- Focused round-trip test for `skills.tok` (separate from corpus gate).
- ≥8 new Core tests: catalog load, validation warnings on representative bad input, validation silence on vanilla corpus, dynamic-resolver outputs against `clean-bec-unpacked`.

**Exit criteria**
1. `skills.tok` byte-equal round-trip green (focused test).
2. Catalog model + dynamic resolvers in Core.
3. `Validate()` produces zero warnings on the vanilla corpus.
4. `SkillEditorView.Save` writes through `ChangeHistoryService`.
5. Baseline + Items + Skills tests green.

**Delivered — deviations and decisions**
- **Catalog location:** ships at `<exe>/skills/skills_catalog.json` (the csproj link
  already used `skills/`). `skills/` now lives *inside* the repo at
  `ModdingGUI/skills/`; the copyrighted `.tok` inputs are `.gitignore`d
  (`skills/*.tok`) while the generator, field reference, overrides and generated
  catalog are versioned — so a clean clone can build and run the test suite.
- **OPEN QUESTION resolved: dev-only regeneration.** Matches the "No GUI
  regeneration button" line above. Consequence designed in: values the shipped
  catalog has never seen are reported as `Info`, never `Warning`, so modded data
  does not produce false failures. Revisit after Phase 3.
- **Catalog format v2.** Every field carries `format`, `multi`, `links`,
  `requires`, `note`; enum values documented in the reference file are unioned into
  the observed values. Parsing stays tolerant (unknown keys ignored, `_meta` version
  reported, malformed entries skipped).
- **Repeat semantics come from the DOL, not the corpus.** `dol/skills_fields.json`
  is extracted from the GameCube boot `bootfile.dol`: the 99-entry field table used by
  `SkillLoadTokenFile` plus the battle-data parser `FUN_80026ec0`, with per-field id,
  argument shape, slot kind and repeat behaviour. Consequences:
  - **Accumulating fields** — bit-sets (`SKILLATTRIBUTE`, `SKILLPROJECTILEATTR`,
    `SKILLPROJECTILESEQUENCE`), class sets (`SKILLUSECLASS`, every `class:` condition)
    and conditional modifiers (`SKILL*CONDITION`, `SKILLTARGETCONDITION`,
    `SKILLSPLITEFFECTCONDITION`) — combine on repeat; `UnitConditionalModify` ORs a flag
    word and appends to a class bitmask (`FUN_800f9608` does `*a |= *b`).
  - **Paired status slots** (`SKILLSTATUS`/`SKILLSTATUS2`, duration, interval, target,
    uselimit, chance) give exactly two slots; each spelling owns one, so a third line
    overwrites. Scalar fields overwrite on the second line.
  - **Verified in game** by two ISO experiments: Light Beam's 3rd `class:` condition
    still fired although the last line was `DarkLegionnaire` (⇒ conditions accumulate,
    nothing is discarded), and Beguile could target male enemies but not the female
    (`unitattr req MALE` + `enemy only` both hold). This **replaced** an earlier
    corpus-frequency heuristic that wrongly concluded the data was lossy.
  - New validator rules: `field-not-in-engine` (the engine ignores the line) and
    `exceeds-engine-slots` (extra line overwrites). 16 engine-legal fields that never
    occur in vanilla data are now documented (`engine_only`), and modded-only names the
    engine does not know (e.g. `SKILLSTATUSSITUATION`) are flagged.
- **`LINK`/`REQUIRES` remain advisory** — the DOL shows the loaders never enforce
  cross-field rules, so contradicted rules stay `Info` with a violation count.
- **`SkillValidationMode`** is `{ None, Warn, Reject }` rather than `WarnInLog`:
  Core has no logging sink, so `Warn` returns findings to the caller (the GUI writes
  the status-bar summary and a Validate dialog) and `Reject` throws
  `SkillValidationException` without writing.
- **`SkillsContext`** delivers `MeterIds` (from `swingmeter.tok` + values observed in
  the project's own `skills.tok`) and `LookupText` (dangling lookuptext-id checks).
  `AnimationsForMeshes` is deferred to Phase 3, where `classdefs.tok` becomes
  `IClassesApi`; the shell keeps its existing resolver until then.
- **Fixed in passing:** the Skill Editor read `data/config/skillmeter.tok`, which
  does not exist — the real file is `swingmeter.tok`, so the `SKILLMETER` dropdown
  was always empty. `ProjectPaths.SkillMeterTokFile` now points at the real file and
  Core accepts `skillmeter.tok` as a legacy alias.
- **Fixed in passing:** `SkillsApi` dropped the file's trailing blank line on write,
  so a no-op save changed bytes. It now preserves the exact trailing-newline count;
  `SkillsRoundTripTests` pins byte-for-byte no-op saves.

### Phase 3 — Lookup + Classes (high leverage)
Unlocks display-name resolution everywhere and makes Team Builder real.

**Status: done** (exit criteria met). Core has `IClassesApi` and the language-aware
`ILookupTextApi`; `LookupHelper` is deleted; Team Builder reads classes through the API.

- **Done — `IClassesApi`** over `classdefs.tok`: raw, comment-preserving blocks
  (`List/GetAll/Get/SetField/AddField/RemoveField/Rename/Add/Remove/Validate`) with the
  project lock and `.mod-history` backups. Set/add/remove round-trips are byte-identical.
- **Done — `classdefs/classdefs_catalog.json`** generated by `classdefs/build_catalog.py`
  from the corpus plus the engine field table in `dol/classdefs_fields.json`. 128 classes,
  22 keys in use, 3 engine-legal keys vanilla never uses (`BASECLASSNAME`, `ITEMINDIV`,
  `SHADOWSET`). `ClassDefsValidator` reports unknown keys, keys the engine ignores,
  unknown enum values, wrong argument counts and repeated single-valued keys; a gate
  asserts zero warnings across all 128 vanilla classes.
- **Done — language-aware lookup API.** `LookupLanguage` covers eng/fra/ger/ita/spa and
  `ILookupTextApi` gained language-aware `Load/Update/Add` + `AvailableLanguages`.
  **Encoding bug fixed:** these files are Latin-1 (`Update_Strings_Bin.py` reads them as
  `ansi`), but the API used UTF-8 — 456 bytes in the English table decoded to U+FFFD and
  a write would have re-encoded them, corrupting every accented string on the way into
  the packed `.bin`.
- **Done — shell migration.** `LookupHelper` deleted (`SkillParser` takes a pre-loaded
  table, `LookupTextResolver` in Core owns the line→text rule); the item editor already
  used `ILookupTextApi`. `SkillAnimationResolver` moved the SKILLUSECLASS → SKILLUSENAME →
  MESH → `*.pan` chain into Core and the editor's private classdefs parser is gone. Team
  Builder's `ClassParser` now calls `IClassesApi.GetAll` instead of reading the file.
- **Deviation — no `SKILLDISPLAYNAMEID` dropdown.** The plan assumed one exists; the field
  is a bare numeric id and the editor shows it as a plain text box. Making it a
  lookup-backed picker is a UX addition, not a migration, so it is not part of this phase.
  (The item editor already resolves display names through `ILookupTextApi`.)

**Exit:** Team Builder reads class metadata via `IClassesApi`. `LookupHelper` either delegates or is deleted.

### Phase 4 — Encounters, Leagues, Towns, Schools, plus config tail
Finishes the main `data/config` + `data/towns` + `data/encounters` triangle.

**Delivered.** `IEncountersApi` (834 `.enc` files, `UNITDB` arity 21), `ISchoolsApi`
(74 rosters, 924 units), `ILeaguesApi` (44 files, 220 leagues, 839 bouts) and
`IConfigTableApi` for flat `KEY value` tables — which is what the town data actually is:
`data/towns` holds `shops/` (59 files), `leagues/` (44) and `schools/*.tnt` tints, with no
`towns/*.tok` to model, so the planned `ITownsApi` folded into the generic table API.
Every API is raw-line based, byte-identical on a no-op write, locked and backed up.
Corpus findings folded into the validators: `PRIZETIER` is colon-less, `CORESTATSCOMP`
takes 7 values where `CORESTATSCOMP2` takes 6, `ITEMSCOMP` may omit trailing slots, and
7 vanilla league bouts reference `.enc` files that do not exist (pinned in the gate).
**`UNITDB` is modelled, not opaque.** `EncounterSetup` (DOL `0x80106ddc`, case `0x1D`) reads
exactly 21 values — `string, int, string, int×5, string×4, int×9` (the earlier `int×12` note was a
miscount; the nine trailing values are indices 12-20). `EncounterUnit` names what evidence supports
and leaves the rest numeric with a comment (see the evidence table below). The engine's 20-key
encounter field table is merged into the catalog (`dol/encounters_fields.json`), so the three keys
vanilla never uses (`BLOCK`, `FORCEPLACEMENT`, `VOICEINTRO`) are known and typos are flagged.
Corpus detail: spawn chance uses `(rand % 100) + 1 <= value`, so ≥100 always spawns — the
corpus contains `1000` — and only negatives are flagged.
**Grids — both layouts decoded from the DOL.** The engine reader is `FUN_800b0b4c` (called by
`BattleProcessGridFile`): a little-endian `u32 version`, then for version 2+ an inline `u32 count` +
30 x 32-byte tags + 1024 `u32` cells (bits 0-29; bits 30/31 masked off). Version < 2 branches to
`FUN_800b0e84`, a different format: a `u32` name count, that many raw 32-byte names (no `F`/`E`
prefix), then 1024 records of `(u16 count, u16 pad, u32 offset)` whose offsets point at arrays of
`u32` slot indices. Legacy grids therefore store a slot list per cell, which is why they are
~3.2 kB larger. **Both layouts are read and written.** An unchanged version-1 grid is written back
from the bytes we loaded, which keeps vanilla's shared index arrays and makes no-op saves
byte-identical; once a cell changes, the payload is rebuilt with one array per non-empty cell and
absolute offsets. All 430 vanilla grids round-trip byte for byte and the 27 legacy maps are
editable in the Grid Builder. Grid writes go through `IGridsApi`, which takes the project lock
and writes a `.mod-history` backup first — the shell no longer calls `GridFile.Save` directly, so
a grid edit is as recoverable as a text edit.

**`UNITDB` slots** are named from evidence, not guesses. The engine (`EncounterSetup` case
`0x1D`) confirms slots 1-3 (name, spawn chance, start marker), 4-6 (x, y, jitter — the jitter
flag adds `rand()` and clamps to ≥1) and 20-21 (spawn flags `0x100`/`0x200`). A modder's notes
(`devnotes.txt`) supply 7-8 (attribute/affinity requirement), 14-16 (the three "prohibited"
flips), 18 (mercenary required) and 19 (item drop chance % — vanilla values are 5-step
percentages up to 90, which fits). Still unknown and left as numbers with a comment: slot 13,
slot 17 (vanilla 0-3). The modder notes guessed slots 4-6 were level bounds; the DOL disproves
that, so the notes are not treated as authoritative where the code disagrees.

Also recorded: `EncounterSetup` forces `Summoner`/`UndeadCaster` on the second and later unit of
a team, which is the summon behaviour behind Hall of Titans and Summoner's Endurance — those
extra units on one slot are engine-generated, not placements.

**Two more unknowns closed with data rather than guesses.**

- **`Header (…): 205` in `schools.txt` and `tints.txt` is a real byte.** `Gladius_Units_IDX_Unpack.py` writes
  it into the text from byte 0 of `unitschools.idx` / `unittints.idx`, and `Gladius_Units_IDX_Repack.py`
  writes it back into that byte (`f.write(int(schoolsHeader).to_bytes(1, "big"))`). Its meaning is still
  unknown, but it is data the game reads, and the repack parses it from the first line with a regex — so a
  file without it cannot be packed at all. `UnitHeader` and both validators now require a numeric 0-255
  header.
- **`Outfit` indexes `data/characters/<mesh>/<mesh>_varNN.ptx`.** 83 classes ship variants (68 with four, 7
  with five, 8 with six; Amazon has `var01`-`var04`); 0 is the plain model and 1..N the variants. Every one
  of the 2,170 vanilla gladiators stays inside its class's variant count, which is why an invalid outfit is
  a crash risk rather than a cosmetic oddity (research log: "Outfit 1 is not valid … default to 0, hope it
  prevents crashes"). `GladiatorsApi.Validate` now resolves class → `MESH` → variant count
  (`OutfitVariants`) and reports an outfit the class does not have, falling back to the corpus range 0-6 when
  the mesh folder cannot be read. `CUSTOMIZE` in a school file carries the same value.

A slot may paint many tiles — that is how the player chooses where to stand — so one cell per slot
was wrong. Validation enforces the real rule: `start-slot-overflow` warns when named units outnumber
the cells painted for their slot (the case that silently dropped a unit in QA). Generated
(empty-name) units are auto-placed and neutral team `-1` is engine-managed, so neither counts — with
that, all 834 vanilla encounters validate clean.

Manual QA plan: `docs/testing/MANUAL_TESTING_PHASE_4.md`.

**Manual QA, confirmed in the app.** Grid Builder: legacy (version-1) maps list their real tags and
paint regions; a save overwrites the file, writes a `.mod-history` backup and reports its path; a
second save without edits leaves the bytes untouched; a version-2 map round-trips byte for byte.
Skill Editor: field help shows a one-line summary with the full derivation behind "Full details".
Item Editor: an item edit saved and was confirmed in game. Two defects surfaced during this QA and are
fixed: the Grid Builder save resolved the project from the edited file's own path (producing
"Cannot create ... because a file or directory with the same name already exists"), and the form's
`.resx` had lost the window icon and a help string while the designer kept regenerating it with the
ResX template (see the build notes in `AGENTS.md`).

**Status: done.** All four families landed; `ITownsApi` folded into `IConfigTableApi`.

- **Done — `IEncountersApi`** over `.enc`: an encounter is a header section plus `TEAM:`
  sections holding `UNITDB` rows. `ListFiles/List/Get/SetField/AddField/RemoveField/`
  `SetTeamField/RemoveTeamField/SetTeamUnits/Validate`, raw-line editing so comments,
  indentation and unknown keys survive. A generated `encounters/encounters_catalog.json`
  (834 files, 2051 teams, 6702 units, `UNITDB` arity 21) feeds `EncounterValidator`:
  unknown header/team keys, wrong unit argument counts, teams without a grid.
  No-op writes are byte-identical, verified by a sweep across the corpus.
  **Format finding:** tok keys are not always `KEY: value` — `PRIZETIER` uses a tab
  (`PRIZETIER\t"DrakeDefaultCash" 0`), so both the parser and the catalog generator
  accept a colon-less upper-case key.
- **Done — `ILeaguesApi`** (44 files, 220 leagues, 839 bouts) and **`ISchoolsApi`** (74 rosters,
  924 units), same raw-line + lock + backup + round-trip discipline.
- **Folded — `ITownsApi`** into `IConfigTableApi`: there is no `data/towns/*.tok`. Shops, league files
  and flat config all ride the generic table API today; a typed `IShopsApi` is deferred with the
  format table above rather than invented.
- **Deferred — the small `worldmap`/`fonts`/`hotkeys`/`options` Apis.** No consumer yet; Phase 6 will
  surface which are actually read instead of shipping four speculative wrappers.
- **Added, not originally planned — `IGridsApi`** (`List`, `Get`, `Describe`, `Save`). Grids turned out
  to be battle-facing, so they needed a real Api: it takes the project lock and writes a `.mod-history`
  backup before every grid write. The Grid Builder now calls it instead of `GridFile.Save`, closing the
  last "shell writes a game file directly" path.

**Exit:** all `.tok`/`.enc` formats with mod relevance have an Api. Round-trip gate stays green.

### Phase 5 — Units `.txt` family

**Status: APIs delivered, in-game QA pending.** Six APIs over the family, all with lock + `.mod-history`
backup + byte-identical no-op saves + advisory validation:

| File | Records | API |
|---|---|---|
| `gladiators.txt` | 2,170 × 9 labelled lines | `IGladiatorsApi` |
| `statsets.txt` | 169 sets × 30 level rows | `IStatSetsApi` |
| `skillsets.txt` | 230 sets of `min max SkillName` | `ISkillSetsApi` |
| `itemsets.txt` | 404 sets of `min max ItemName [note]` | `IItemSetsApi` |
| `tints.txt` | 196 sets × 6 RGB channels | `ITintSetsApi` |
| `schools.txt` | 144-entry name table | `ISchoolNamesApi` |

**The abstraction is two types, not the planned `DelimitedRecordSchema<T>`.** These files are not
delimited records: `gladiators.txt` is labelled blocks and the rest are numbered sections. `UnitTextFile`
carries the line/terminator fidelity (gladiators.txt is LF-only while the other five are CRLF, so a
CRLF-normalising writer would rewrite 21,700 line endings) and `SectionedFile` locates sections and rows
for the four sectioned files. `DelimitedRecordSchema<T>` remains the right tool for a genuinely
delimiter-based format; it was not the right tool here.

**Corpus findings folded into the validators** (each one corrected a rule that would otherwise have
produced false warnings): vanilla leaves `schools.txt` entry 0 blank; `tints.txt` set 0 is a paragraph of
prose rather than colours; skillset windows reach `0 232` and `0 30` (level 0 means "from the start");
skillset 144 lists `Summoner SummonAffinityPower` twice, so duplicates are Info; `gladiators.txt` contains
one malformed record (`Name: Muduk` followed by a stray duplicate line) that must round-trip untouched;
and `NUMENTRIES` is bookkeeping — the pack chain reads it but never enforces or rewrites it, so the APIs
recompute it only when the section count changes.

**Cross-file validation is real, not decorative:** class names are checked against `classdefs.tok`, skill
names against `skills.tok`, item names against `items.tok`, and every gladiator's five set indexes are
bounded by counts read out of the other units files (169/230/404/144/196), so a mod that adds a set
widens the range by itself.

**QA artifact — passed in game.** Bjolf's tint set 0 → 155 (all six channels 196,0,0) and stat set 71
level 1 CON/POW 3/3 → 150/120, applied through `ITintSetsApi`/`IGladiatorsApi`/`IStatSetsApi` and packed.
In game he is **fully red** with **905 HP** at level 1, so the tint path and the stat path are both proven
end to end.

**What the QA taught us about the fields** (all now in code comments and validators):

- **`Affinity` is the slot-eligibility affinity** — the one unknown that stayed open for several rounds,
  now closed. It is packed into `unitunits.idx` (low three bits of a byte shared with `Outfit`:
  `5 bits outfit | 3 bits affinity`) and remapped by `FUN_800dfac4` onto the engine ids
  (**1 Earth, 2 Water, 3 Dark, 4 Air, 5 Fire, 6 Light**; the file order is None, Air, Dark, Earth, Fire,
  Water, so Fire is file index 4 but engine id 5). The matcher `FUN_8010ad40` then compares that value with
  a `UNITDB` slot's affinity requirement (index 7) — or, with the prohibit flag (index 16), rejects a
  match — which is what the field is *for*. It is not the affinity a unit fights with: offense affinity is
  the weapon's and defence comes from armour/accessories, which is why changing it looks inert in the unit
  panel. `EncounterValidation` now rejects a requirement outside -1..6 (`affinity-requirement`), since
  such a slot can never be filled. Vanilla: -1 on 6,302 rows, 1-5 on 400 rows, prohibit set on 34.
  The enum's order is documented as load-bearing (`5` is a deliberate hole in the repack table, and
  "Light" has no name there even though the engine knows it).
- **`Outfit` is zero-indexed per class** and nothing lists the valid set; an invalid value has been
  reported to crash the game (research log: "Outfit 1 is not valid … default to 0, hope it prevents
  crashes"). `ISchoolsApi` now validates the matching `CUSTOMIZE` outfit for starting units, which was
  previously unvalidated.
- **Tints are gladiator-only** (nothing else in the game uses them), and they are forgiving: six RGB slots,
  a blank set defaults to zeros — which is why set 0 is a note rather than colours.
- **Skillsets/itemsets are what actually make a unit do things** — the skill list it can cast and the
  equipment it wears by level — confirming that the five set indexes, not the affinity label, are the
  meaningful cross-file references.
- **The engine never parses the units `.txt` labels.** `FUN_800dffb8` loads the seven compiled `.idx`
  files and `FUN_800dfac4`/`FUN_800dfe1c` read those, so the pack chain is the only consumer of the text
  files — which is why its own tables are the authority for names like the affinity list.
Gladiators, statsets, itemsets, skillsets. Tab/space-delimited, NOT tok.

- New `DelimitedRecordSchema<T>` peer abstraction (sibling of `RecordSchema<T>`).
- `IGladiatorsApi`, `IStatSetsApi`, `IItemSetsApi`, `ISkillSetsApi`.
- `IShopsApi` **if** a consumer lands with it (shop editor, randomizer stock). The rows are
  `ITEM "name", 2, -1, 1, 100, 0, 1, 12, 0, 0, 1, 0` — a repeated key with a positional tuple, the same
  shape as `UNITDB`, so extract the positional-row helper `EncounterUnit` already proves out instead of
  writing a second parser. Name the 12 values only after the DOL reader is identified (`Shop` and
  `GetShopName` are script bindings, not the parser).
- Same lock + backup + round-trip discipline.
- Must ship before Phase 6 (randomizer rewrite depends on these).

### Phase 6 — Randomizer rewrite
Move all `Randomizer.cs` logic (1,914 lines) into `ModdingGUI.Core.Randomizer`.

- Pure pipeline: `RandomizerOptions` + project path → patches via the Apis → applied via the same Apis.
- GUI settings panel becomes a parameter builder over `RandomizerOptions`.
- Eliminates the last "shell edits files directly" code.

**Scope decisions (2026-11-05):**

- **Script compilation and packing are in scope.** The randomizer edits `.scp` files and recompiles before
  packing, so `ProjectBuildService` (script compile + the pack chain) lands in Core as part of this phase.
  Without it the pipeline could randomise but not produce a playable ISO, and Phase 8's MCP could not run
  a complete randomization.
- **No value parity with the old implementation.** Nobody depends on today's exact picks, so the rewrite
  defines its own choice logic. What must survive is *feature* parity — the same options and the same
  things randomised:

  | Option | Effect |
  |---|---|
  | `RandomizeHeroes` | gladiator class / statset / skillset / itemset / tint assignment |
  | `RandomizeTeam` | the starting roster in the school file |
  | `RemoveRecruits` | strips recruitable units from encounters |
  | `RandomizeStatsets` / `RandomizeItemsets` | stat and equipment tables |
  | `NoRecruits` / `PermaDeath` / `InGameRandomization` / `FortyGladiators` | the existing toggles |
  | `Cash` | fixed or random starting gold |
  | `WeightedClasses` | class diversity weighting |
  | `Hero` (Ursula/Valens) and `GameVersion` (Vanilla/Ragnaroks/Leonarths) | which campaign files are touched |

**Why the seeding has to be rebuilt, not reused.** The old code seeds only two steps
(`RandomizeHeroes`, `RandomizeTeam` call `InitializeRandom(ctx.SeedText)`); `RandomizeStatsets`,
`RandomizeItemsets`, `RemoveAllRecruits` and `EditEncounterFiles` never do — they run on whatever `random`
instance the previous step left behind, so a displayed seed covers part of the run and the rest depends on
call order. The new pipeline takes one `RandomizerRandom` per run and every step draws from it.

**Acceptance for the rewrite** (replaces "matches the old output"): the same seed produces an identical
project across runs; a different seed differs; every validator still passes on the result; the randomised
project packs and plays.

**Heroes are gladiators, and the options apply to them.** The six story heroes are ordinary
`gladiators.txt` records (`Ursula` is class `Ursula`, stat set 163, skill set 225 …), so the rule is: whatever
an option does to any other gladiator, it does to the heroes too.

**Hero classes are fair game.** Any unit may end up wearing one of the six bespoke hero classes; it does not
break the game as long as the scripts are recompiled, which the pack step does. So the loadout pool is every
record — hero records included — and nothing filters them out.

**Stat sets: one random set per gladiator.** This copies the old `RandomizeStatsets` exactly (it assigned
`statSets[random.Next(statSets.Count)]` per gladiator): only the index in `gladiators.txt` changes, the tables
themselves are untouched, and heroes are restatted like everyone else.

**Item sets: affinity-matched, as the old code did** — implemented. `RandomizeItemsets` was not a uniform
pick: it read which affinity each itemset's last weapon grants from `items.tok`, pooled the sets by affinity,
and gave each gladiator a set from its own affinity's pool plus the universal **Light** pool (a unit with no
affinity may take anything). That matters because the equipped weapon is what lets a unit use affinity skills,
which is why changing the `Affinity` field alone looks inert in game. `ItemSetAffinityMap` reproduces the
mapping: vanilla has 404 sets, and a Fire unit may only draw Fire or Light sets.

**Loadouts are swapped as bundles, not field by field.** A gladiator's class, outfit, affinity, tint, skill,
stat and item sets are taken together from a **real record**, and that is the cross-reference the files
support: `classdefs.tok` maps a class only to `SKILLUSENAME` (animations), never to its stat/skill/item sets,
so choosing those independently would be invention. A bundle copied from another gladiator is a loadout the
designers actually shipped. Name and school are never part of the bundle — the hero keeps its identity and
the school that puts it in a roster. Vanilla offers 1,743 distinct bundles across 2,170 records.

**Team step: append a roster to both campaign files.** Copied from the old `RandomizeTeam`: 14 members (34 with
`FortyGladiators`), named `<gladiator>_random`, appended to `data/school/valensimperia.tok` and
`data/school/ursulanordagh.tok`. Each block carries the class's level-1 stat row, the level-1 row of each of the
five equipment slots, and the class's granted skills — a set lists level windows per slot, so writing every row
would malform `ITEMSCOMP`. `WeightedClasses` takes one of each base class before repeating one.

**Recruit removal** deletes every `RECRUIT` line from every league file (`ILeaguesApi.RemoveRecruits`), one write
per changed file with its own backup — vanilla has 21 league files carrying recruit entries.

**Build lives in Core.** `BatchContentBuilder` and `PathHelper` moved from the shell into
`ModdingGUI.Core.Tools`, so `ProjectBuildService` can run the same chain the app runs: script compile through
`DOGCodeCompiler.exe`, then pack through `bec-tool-all.py` and `ngciso-tool.py`. Output streams into `IAppLog`
as it arrives, because a pack takes minutes and silence looks like a hang. `RandomizerRunner` composes the two
(randomize → compile → pack), which is the single entry point the GUI, the CLI and the MCP server all use.

**A CLI verb makes it QA-able.** `ModdingGUI --randomize <project> --seed <text> [steps] [--pack]` runs the
pipeline headlessly and prints the seed, the steps and the record count. Adding it exposed a real API
inconsistency: `ItemsApi` was the only API that did not resolve a project root to its `_BEC` folder, so the
item-set step failed with "items.tok not found" on a project path. It now resolves like the rest.

**Reproducibility is verified, not assumed.** Running the full pipeline (heroes, statsets, itemsets, team,
recruits) twice with seed `alpha` on a fresh project produces byte-identical `gladiators.txt` and both campaign
rosters; seed `beta` changes them. The league files hash the same across seeds, as they should: recruit removal
is seed-independent.

**Slices:**

1. `RandomizerOptions` + seeded `RandomizerRandom` (one RNG, explicit string seed hashed stably so it
   survives processes) + result types.
2. **Batch mutation on the APIs** — the current per-record calls reload and back up the whole file each
   time; randomising 2,170 gladiators that way would write ~560 MB of backups. `ApplyMany` opens once,
   mutates many, writes once (one `.bak` per run).
3. `RandomizerService.Randomize(options, IAppLog)` applying through the APIs, emitting progress.
4. `ProjectBuildService`: script compile + pack in Core.
5. GUI becomes a parameter builder; the shell's direct `File.Write*` calls in `Randomizer.cs` are deleted.

**Exit:** randomization is reproducible (seeded), reviewable (one `.bak` per run through the Apis), packs,
and works through the same code path the MCP server will use.

### Phase 7 — Team Builder real implementation
Replace mockup with real implementation backed by `IClassesApi` + `ISkillsApi` + `IItemsApi` + `ISchoolsApi` + `IGladiatorsApi`.

### Phase 7.5 — Restore from backup
Expose the `.mod-history` archive as a first-class feature.

- `ChangeHistoryService.ListBackups(projectPath, relativeFilePath)` — returns `IReadOnlyList<(DateTime timestamp, string backupPath)>` sorted newest-first.
- `ChangeHistoryService.RestoreBackup(projectPath, relativeFilePath, backupPath)` — copies the chosen `.bak` over the live file, first writing a backup of the live file so the restore itself is undoable.
- Each `IXxxApi` gains `IReadOnlyList<(DateTime, string)> ListBackups(projectPath)` and `void RestoreBackup(projectPath, backupPath)` (or delegates directly to `ChangeHistoryService`).
- GUI: history panel per editor tab listing available backups with timestamp; "Restore" button triggers the Api method.
- **Includes binary formats**: `IGridsApi` backups are ordinary `.mod-history` entries, so grid restores
  come free once the panel exists — which is why grid writes were moved onto the Api in Phase 4.

**Exit:** user can browse and restore any prior backup from within the GUI; restoring is itself undoable (creates a new `.bak` of the pre-restore state).

### Phase 7.8 — Output and log sink (prerequisite for Phase 8)

Today every user-visible message goes into a WinForms control: **267 `AppendLog(` call sites** across
12 shell files funnel into `rtbUnpackOutput` / `rtbPackOutput` / `rtbItemEditor` /
`rtbPatchingApplicationOutput`, and the Grid Builder has a second private logger writing to its own
box. `ModdingGUI.Core` has no logging abstraction at all. Consequences:

- an MCP client (headless, Phase 8) can see **none** of it — not validation notes, not pack/unpack
  output, not why a run failed;
- pack/unpack is a shell-owned batch runner whose child-process stdout only ever reaches a RichTextBox,
  so nothing outside the GUI can report a packing failure;
- nothing to assert on in tests (log content is invisible to the suite).

**Status: the sink itself is done** (`ModdingGUI.Core.Logging`: `LogLevel`, `LogEntry`, `IAppLog`,
`AppLog` — ring buffer, per-subscriber-isolated `Written` event, `Scope`, file sink at
`%LOCALAPPDATA%\GladiusModding\last-run.log`, 11 tests). The shell's `AppendLog` is now a thin
adapter that records first and renders second, so all ~270 call sites already feed the stream without
being touched, and the Grid Builder shares the same instance via `GridBuilderView.Log`. Still open from
this slice: moving pack/unpack/script-compile behind Core services (`PackService`/`UnpackService`) so
child-process output is emitted as `LogEntry`s rather than only rendered — needed before Phase 8
exposes it, and it is the natural home for the Phase 6 progress reporting too.

**Deliverable: `IAppLog` in Core.**

- `LogLevel` (Debug/Info/Success/Warning/Error), `LogEntry(At, Level, Source, Message, Detail)`.
- `IAppLog.Write`, `Tail(count)`, `Clear`, `Scope(source)` (an idisposable tag so a view or tool owns
  its lines), and a `Written` event the GUI renders and MCP streams.
- Ring buffer in memory plus an optional file sink (`%LOCALAPPDATA%/GladiusModding/last-run.log`) so a
  headless run leaves evidence.
- The shell's existing `AppendLog(message, color, targetRtb)` becomes a **thin adapter**: it forwards
  to `IAppLog` and then renders. All 267 call sites keep working unchanged — the migration is one
  method, not a sweep. The Grid Builder's private logger forwards the same way.
- Pack/unpack/script-compile move behind Core services (`PackService`/`UnpackService`) that emit
  `LogEntry`s from the child processes, so the GUI and MCP see identical output.

**Why it sits here.** Phase 6 (randomizer) needs progress reporting that works without a form, and
Phase 8 cannot expose output that does not exist as data. One adapter now, versus a 267-site sweep
plus a second bridge later. No behaviour changes: the GUI looks the same; it just stops being the only
place the text exists.

**MCP surface (Phase 8).** `logs_tail(count, level?)`, `logs_clear`, `logs_subscribe` (streams lines
over stdio); every write tool's result envelope carries `warnings[]` and the log range it produced, so
an AI reads validation output structurally instead of scraping text.

### Phase 8 — `ModdingGUI.Mcp` stdio server
Thin tools-over-stdio bridge. One tool per Core method.

- Tool families: `items_*`, `skills_*`, `lookup_*`, `classes_*`, `encounters_*`, `leagues_*`, `schools_*`, `grids_*`, `gladiators_*`, `statsets_*`, `itemsets_*`, `skillsets_*`, `shops_*` (when built), `randomizer_*`, `project_*`, and `logs_*` over `IAppLog` (Phase 7.8).
- All write tools accept `strict: bool = false`. `false` = `WarnInLog`/`None` (matches GUI). `true` = `RejectOnError`.
- `skills_catalog_describe_field(fieldName)` returns slot types, enums, notes — so AIs can construct valid `SkillDto`s without guessing.
- Trust model: stdio only, no network, no auth beyond OS process trust.
- Settings checkbox in GUI: "Run as MCP server" relaunches process with `--mcp` flag (no WinForms init).
- Tool docs auto-generated from XML doc comments on each `IXxxApi` method.

**Exit:** Claude Desktop / OpenCode can drive every Api end-to-end through MCP, edits land in `.mod-history`.

### Phase 9 — Polish
- Undo via `.mod-history` (already on disk; needs UI).
- Per-backup diff viewer.
- Project-wide search / find-references across schemas.
- "Validate project" button — runs every Api's validator and aggregates.
- README + per-format docs.

## 3. In-game validation harness (anything battle-facing)

Some questions can only be answered by the game itself. The standing recipe gets a test
build straight into a fight instead of replaying the tutorial:

1. Unpack once: `ModdingGUI --unpack <vanilla.iso> Scratch` (no GUI needed).
2. Edit what the phase is about (skills, class definitions, encounters, units, …).
3. **Override the tutorial fights** so the campaign drops into the test encounter:
   `IEncountersApi.ReplaceWith(arenaPath, tutorialPath, "Scene04U", stripCutscene: true)` — the
   source fight is copied in, the destination keeps its own `ENCOUNTERNAME`, and the source's
   `CUTSCENE` is dropped so the wrong cinematic cannot fire.
   **Do not repoint the league's `ENCFILE` for these.** Story battles are started by the compiled
   game-flow script (`gameflow*.scp`) by encounter *name*; the engine resolves that name by scanning
   `data/encounters` for a matching `ENCOUNTERNAME`, so `towns/leagues/*.tok` only drives the league
   menu. A league `ENCFILE` edit on a story bout is silently inert — confirmed in game.

4. Give the hero room to experiment: `CORESTATSCOMP2` in the school file is
   **`CON, PWR, ACC, DEF, INI, MOVE`** (the comment in the file says `INT`; the value is
   initiative). A large CON/INI lets turns be passed while arranging units.
5. Grant test skills from the school file (`SKILL: "<name>"`) — two traps learned the hard
   way:
   - `NUMENTRIES` must be regenerated after adding records or the loader truncates the
     tail (the pack chain does this; a hand-rolled chain does not);
   - the in-game skill list keys off the **display name**, so two skills sharing a
     `SKILLDISPLAYNAMEID` show as one entry.
6. Pack: `ModdingGUI --pack <project> --python "py -3"` — one command, full chain.

Results recorded so far (details in `skills/README.md` and `dol/README.md`): conditional
fields accumulate rather than overwrite; there are exactly two status slots and a third
`SKILLSTATUS` line crashes the game; `SKILLEFFECTCONDITION` filters targets without
`SKILLEFFECTRANGE`.

7. **Start slots are regions, not tiles.** A slot may paint many cells — that is how the player
   chooses where each unit stands. On `arenacertification4.grd` each slot paints exactly one cell,
   which is why two *named* units on `Start6` cost one of them (**observed in game**). Encounter
   validation therefore warns (`start-slot-overflow`) only when named units outnumber painted cells;
   generated units are auto-placed and the neutral team `-1` is engine-managed, so neither counts.
8. **Summon fights are engine-driven.** `EncounterSetup` forces `Summoner`/`UndeadCaster` on the
   second and later unit of a team, which is what Hall of Titans and Summoner's Endurance are doing —
   extra units sharing one slot there are summoned, not mis-placed. Do not "fix" them.
9. **Legacy grids are a different format.** 27 of 430 vanilla grids are layout version 1 (raw tag
   names, per-cell slot lists, ~3.2 kB larger). Both layouts read and write; a no-op save is
   byte-identical for all 430. Verify a grid edit in game by painting a `NoMove` cell or an extra
   start tile on a reachable map — no version-1 grid is used by the Ursula campaign (they are
   Imperia/Belfort arenas).

## 4. Cross-cutting rules (apply to every phase)

1. **No GUI bypass.** Every persistent edit goes through a Core Api. The shell never writes game files directly.
2. **Backups always.** `ChangeHistoryService.Backup` is non-optional on every write.
3. **Locks always.** `ProjectLock.Acquire` wraps every multi-step write.
4. **Round-trip first.** New format = round-trip test green before any mutation lands.
5. **Reuse helpers.** `ProjectPaths`, `PathHelper`, `BatchContentBuilder`, `LookupHelper` (until deleted), validators. Never reimplement.
6. **Catalogs and static assets** ship beside the exe in their own folder (`<exe>/skills/skills_catalog.json`, `<exe>/classdefs/classdefs_catalog.json`, `<exe>/dol/*.json`), copied by the csproj so a clone can run the suite.
7. **Validation is advisory** in the GUI; opt-in strict in MCP.
8. **No new dependencies** without explicit approval. `System.Text.Json` only for JSON.
9. **`dotnet test -c Release` green** at the end of every phase. No phase merges with failing tests.
10. **One phase at a time.** Don't expand scope mid-phase.
11. **Never hand-roll the pack chain.** `ModdingGUI --pack <project>` replays the app's own batch (`BatchContentBuilder`), including `Tok_Num_Update.py`. A hand-copied subset once skipped that step, left `NUMENTRIES` stale, and the game silently dropped the tail of the skill list.
12. **Names need evidence, and the engine outranks notes.** Before a field gets a name, check what the
    engine does with it, then what a modder documented, then what the corpus values look like. Where
    they disagree the engine wins (`UNITDB` slots 4-6 are coordinates, not level bounds — the code adds
    `rand()` and clamps to ≥ 1). Anything still unexplained stays numeric with a comment naming the
    evidence, never an invented label.
13. **Binary formats preserve what we do not understand.** Keep unknown trailing bytes verbatim, make a
    no-op save byte-identical, and never truncate: the original Java grid tool wrote 27 legacy maps as
    version 2 and silently dropped 3.2 kB. If a payload has to be rebuilt, rebuild it only after an edit.
14. **Every write goes through an `IXxxApi`, binary included.** Lock, then `.mod-history` backup, then
    write. `IGridsApi` is the precedent — the shell must not call a serializer directly, even when the
    file is a grid.
15. **Field help is a summary plus a toggle.** One line in the form (first sentence, ≤ 140 chars) with
    the derivation behind "Full details". The methodology of how a value is resolved is developer
    knowledge; a modder choosing a value needs the gist.
16. **Output is data, not UI.** Anything the user needs to see (progress, validation, child-process
    output, failures) is written through `IAppLog` in Core and rendered by the shell; a RichTextBox is a
    consumer, never the store. This is what lets the same run be observed by the GUI, by tests, and by
    an MCP client, and it is why pack/unpack must move behind Core services rather than staying a
    shell-owned batch runner.
17. **Never loop a file-backed API.** Every `Get` re-reads and re-parses the whole file, so a per-record loop
    turns a two-second job into minutes: reading the gladiator table record by record took **210 s** for six
    hero lookups, and the same read through `GetAll` took **1.3 s**. Use the one-pass accessor (`GetAll`) or a
    batch write (`ApplyMany`); if one does not exist, add it before writing the loop.
18. **A rule that vanilla violates is suspect.** `start-slot-overflow` was first written as "duplicate
    start slot = warning" and flagged 4 working encounters; investigating those is what produced the
    region semantics and the summon exception. Before pinning a rule, check the corpus for false
    positives and explain every one.

## 5. Final goals (definition of done for the project)

- Every mod-relevant game file is editable through a Core Api with locking, backup, and round-trip fidelity.
- The WinForms shell is a parameter builder over those Apis. Every editor tab maps to one Api.
- `ModdingGUI.Mcp` exposes the full Api surface to AI clients with optional strict validation.
- Randomizer is a pure pipeline over the same Apis.
- Skills remains freeform with catalog-driven guidance; the catalog and project sources of truth grow over time without C# schema churn.
- Test suite is the safety net: corpus round-trip gate + per-Api unit tests + per-format focused round-trip tests, all green.
- Users get the exe + `skills_catalog.json` + bundled tooling. Modders get a GUI; AI agents get MCP. Same Apis underneath.