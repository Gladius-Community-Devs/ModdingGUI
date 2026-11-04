## 0. Architecture (locked)

- **Three projects:**
  - `ModdingGUI.Core` — parsing, schemas, Apis, history, locking. No UI, no shell deps.
  - `ModdingGUI` — WinForms shell. Treated as a **parameter builder** over Core Apis.
  - `ModdingGUI.Mcp` — stdio MCP server. Thin tool surface over Core Apis.
- **API discipline:** stateless `open → mutate → write`. Per-project `ProjectLock`. Timestamped backup to `<project>/.mod-history/<rel>/<yyyyMMdd-HHmmss-fff>.bak` on every write.
- **Round-trip fidelity:** every `.tok`/`.enc` re-serializes byte-for-byte on no-op. Unknown tags + comments preserved.
- **JSON:** `System.Text.Json` only.
- **Tok I/O:** ISO-8859-1, for `.tok` *and* every `data/**/*.txt` table. The pack tools read them as ANSI (`Update_Strings_Bin.py` uses `encoding="ansi"`), so UTF-8 is never correct: reading a Latin-1 table as UTF-8 replaces high bytes with U+FFFD and writing re-encodes them as three bytes (this corrupted 456 bytes of `lookuptext_eng.txt` before it was fixed).
- **Test bar:** 348 passed / 0 failed / 0 skipped with real-data fixtures present (a `vanilla.iso` plus a `REALDATA_*` project folder in the build output — see `AGENTS.md`); the 16 `RealGameDataTests` skip without them (332 passed / 0 failed / 16 skipped). Every phase ends with green tests.

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
| Towns / shops | `data/towns/*.tok` | `ITownsApi` (Phase 4) |
| Schools | `data/school/*.tok` | `ISchoolsApi` (Phase 4) |
| Units (txt) | `data/units/*.txt` (gladiators, statsets, itemsets, skillsets) | `IGladiatorsApi`, `IStatSetsApi`, `IItemSetsApi`, `ISkillSetsApi` — separate delimited backend (Phase 5) |
| Worldmap / fonts / hotkeys / options | `data/config/*.tok` | small Apis as needed (Phase 4 tail) |
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
**`UNITDB` is modelled, not opaque.** `EncounterSetup` (DOL `0x80106ddc`, case `0x1D`)
reads exactly 21 values — `string, int, string, int×5, string×4, int×12` — so `EncounterUnit`
exposes unit name, spawn chance, start slot, placement X/Y, position jitter, four class tags,
four boolean flags and two spawn flags by name, and slots 7/8/17/18/19 by index (the engine
passes them into unit creation without a visible test). The engine's 20-key encounter field
table is merged into the catalog (`dol/encounters_fields.json`), so the three keys vanilla
never uses (`BLOCK`, `FORCEPLACEMENT`, `VOICEINTRO`) are known and typos are flagged.
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
editable in the Grid Builder.

A slot may paint many tiles — that is how the player chooses where to stand — so one cell per slot
was wrong. Validation enforces the real rule: `start-slot-overflow` warns when named units outnumber
the cells painted for their slot (the case that silently dropped a unit in QA). Generated
(empty-name) units are auto-placed and neutral team `-1` is engine-managed, so neither counts — with
that, all 834 vanilla encounters validate clean.

Manual QA plan: `MANUAL_TESTING_PHASE_4.md` at the workspace root.

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
- `ILeaguesApi` — recruits, store inventories, league progression.
- `ITownsApi` — town defs, shop refs.
- `ISchoolsApi` — school defs.
- Small Apis for `worldmap.tok`, `fonts.tok`, `hotkeys.tok`, `options.tok` as the randomizer needs them.

**Exit:** all `.tok`/`.enc` formats with mod relevance have an Api. Round-trip gate stays green.

### Phase 5 — Units `.txt` family
Gladiators, statsets, itemsets, skillsets. Tab/space-delimited, NOT tok.

- New `DelimitedRecordSchema<T>` peer abstraction (sibling of `RecordSchema<T>`).
- `IGladiatorsApi`, `IStatSetsApi`, `IItemSetsApi`, `ISkillSetsApi`.
- Same lock + backup + round-trip discipline.
- Must ship before Phase 6 (randomizer rewrite depends on these).

### Phase 6 — Randomizer rewrite
Move all `Randomizer.cs` logic into `ModdingGUI.Core.Randomizer`.

- Pure pipeline: `RandomizerOptions` + project path → patches via the Apis above → applied via the same Apis.
- GUI settings panel becomes a parameter builder over `RandomizerOptions`.
- Eliminates the last "shell edits files directly" code.

**Exit:** randomization is reproducible (seeded), reviewable (every change is a `.bak`), and works through the same code path the MCP server will use.

### Phase 7 — Team Builder real implementation
Replace mockup with real implementation backed by `IClassesApi` + `ISkillsApi` + `IItemsApi` + `ISchoolsApi` + `IGladiatorsApi`.

### Phase 7.5 — Restore from backup
Expose the `.mod-history` archive as a first-class feature.

- `ChangeHistoryService.ListBackups(projectPath, relativeFilePath)` — returns `IReadOnlyList<(DateTime timestamp, string backupPath)>` sorted newest-first.
- `ChangeHistoryService.RestoreBackup(projectPath, relativeFilePath, backupPath)` — copies the chosen `.bak` over the live file, first writing a backup of the live file so the restore itself is undoable.
- Each `IXxxApi` gains `IReadOnlyList<(DateTime, string)> ListBackups(projectPath)` and `void RestoreBackup(projectPath, backupPath)` (or delegates directly to `ChangeHistoryService`).
- GUI: history panel per editor tab listing available backups with timestamp; "Restore" button triggers the Api method.

**Exit:** user can browse and restore any prior backup from within the GUI; restoring is itself undoable (creates a new `.bak` of the pre-restore state).

### Phase 8 — `ModdingGUI.Mcp` stdio server
Thin tools-over-stdio bridge. One tool per Core method.

- Tool families: `items_*`, `skills_*`, `lookup_*`, `classes_*`, `encounters_*`, `leagues_*`, `towns_*`, `schools_*`, `gladiators_*`, `statsets_*`, `itemsets_*`, `skillsets_*`, `randomizer_*`, `project_*`.
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

## 5. Final goals (definition of done for the project)

- Every mod-relevant game file is editable through a Core Api with locking, backup, and round-trip fidelity.
- The WinForms shell is a parameter builder over those Apis. Every editor tab maps to one Api.
- `ModdingGUI.Mcp` exposes the full Api surface to AI clients with optional strict validation.
- Randomizer is a pure pipeline over the same Apis.
- Skills remains freeform with catalog-driven guidance; the catalog and project sources of truth grow over time without C# schema churn.
- Test suite is the safety net: corpus round-trip gate + per-Api unit tests + per-format focused round-trip tests, all green.
- Users get the exe + `skills_catalog.json` + bundled tooling. Modders get a GUI; AI agents get MCP. Same Apis underneath.