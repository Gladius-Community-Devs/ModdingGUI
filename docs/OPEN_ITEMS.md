# Open items

Everything that is unfinished, unverified or deliberately deferred, in one place so it can be picked up without re-reading a
session. Each line says what it is, why it matters, and what "done" means.

Ordering inside a section is priority, not sequence. The section order is the order I would work in.

---

## 1. Textures: the league-card encoder is not faithful

The pipeline is right - a card named `<league name, lowercased>.tga` in `data/texture/gui/leagues/`, added with `bec add`, packs
and shows up in game under that league. The **bytes it puts in the card are wrong**.

| item | detail |
|---|---|
| Calibration card | Encode a page whose 8x8 tiles are distinct solid colours, one clearly asymmetric marker tile. One screenshot shows the engine's real block order and whether the 8x8 tiling is offset. This is the next action; everything else here waits on it. |
| Encoder fidelity gate | Re-encoding a decoded vanilla card currently matches **0 of 1024** 32-byte blocks (36.2% of bytes). The encoder must reproduce a vanilla card block-for-block before it is trusted with a user's picture. Add that as a test, not a manual check. |
| Endpoint search | `images.py` picks endpoints from the darkest and brightest texel only. A real DXT1 encoder searches the block's min/max per channel (and usually tries a few candidates). Needed for quality once the layout is right. |
| Alpha plane | `fmt 0x22` carries alpha in a second CMPR plane's green channel. Unverified whether the engine also expects that plane to be *solid* (255) when the card is fully opaque; the cat card renders, so it is at least not crashing. |
| `maps.py` is not a reference for this format | Its CMPR decode is circular evidence: our encoder round-trips through it, and both may disagree with the engine. Never again treat "decodes correctly with maps.py" as proof for a card. |
| Import button in the app | The card is made today by decoding the picture in PowerShell and calling `images.card_from_rgba`. It belongs behind **Import image...** in the Series tab: pick a file, resize to the page geometry, encode, `bec add`, repack. |

---

## 2. Encounter editor - deferred from the owner's review

These are all from the review rounds and are small, independent, and each has a visible symptom.

| item | symptom today |
|---|---|
| Breadcrumb | Nothing says which region/town/arena the page is editing; the arena folder name is the only clue. Add `Imperia > Pirgos > pirgosarena - <league> (saves into <project BEC>)`. |
| `ENTRYFEE` absent shows as 0 | A stage whose file has no `ENTRYFEE` renders `0`, identical to a real zero. Saving does not create the key, so the display is simply lying. Show empty and say the line is absent. |
| Stage list has no headers | `2v4v3v0v0` needs a legend. Add a header row: stage / type / teams (you first) / encounter. |
| Which of the team numbers is mine | Same problem, needs saying in the list or the editor caption. |
| Team box count varies | The editor always renders five team boxes; a league with six values is silently truncated. Render based on the file's `TEAMS` length. |
| Region must be chosen first | The town list is filtered by region, and nothing says `Pirgos` is in `Imperia`. Add "(all regions)" or make the town list searchable across regions. |
| Duplicate display names | Two Pirgos leagues resolve to "Agony of Saturno" and two to "Civilis League of Pirgos". The internal name in parentheses is the only disambiguator; consider marking duplicates. |
| Validate covers the whole file | The button says "Validate this league file" now, but the results are not scoped and land in a 20px status strip. Show them in a list, scoped to the selected league. |
| "saved stage 1" reads like state | It is a last-action flash. Say "last action: saved stage 1". |
| Encounter headers are read-only | `SCENE`, `TOTALPOP`, `CROWDLEVEL`, `CUTSCENE`, `PRIZETIER` are editable through the API but not exposed in the page. |
| No new/duplicate/delete encounter | The page edits existing files only. Duplicating an existing `.enc` under a new name is the natural way to make new content, and it is the same "recycle" principle as stages. |
| `ENCDESC` on hover | Each league entry carries an `ENCDESC` lookup id next to the `ENCOUNTER` one; the flavour text could show as a tooltip. |

---

## 3. Series authoring - unverified pieces

| item | state |
|---|---|
| Add stage in game | Adding a stage copies another arena registration (keeping its text id). Untested in game: does a recycled registration under a new name play? |
| League-level editing | The page cannot change `LEAGUEPTSNEEDED`, `LEAGUEPTS`, `BADGE`, `HERO`, `TIER`, `LEAGUEDESC`, `ONHOVER`/`ONSELECT`/`ONWIN`. Cloning copies them; editing them is the next capability. |
| Reorder loses comments | Moving a stage rewrites the affected registrations through the API (remove + append), which drops comments inside those blocks. The status line warns. A real fix is a `MoveEncounter` in `LeaguesApi` that moves raw lines. |
| Drag to reorder | Not implemented; buttons only. |
| New `.enc` file | Whether the engine accepts a newly invented encounter file is **untested**. `bec add` can put one in the archive now, so the test is possible: copy an existing `.enc`, point one stage of a proven league at it, and play. |
| Mixed-type runs are broken | Confirmed in game: a Series run with two stages flipped to Endurance aborted to the League Office after the first battle. The page warns about mixed types; it does not prevent it. |
| A run always starts at stage 1 | Confirmed in game. The page should say so, so nobody expects to test stage 3 directly. |

---

## 4. Grid Builder - the region/town filter

Asked for twice, never landed. The builder lists every `.grd` in one flat combo and finding the right one means reading folder
names. The pattern is proven in the Series tab: `ArenaCatalog` already returns region / town / arena with the grids per arena.

Add Region and Town combos ahead of the grid combo, filter the `ComboItem` list by the first two path segments, keep
"(all regions)" as today's behaviour. The blocker last time was that `ComboItem` is not a member of the partial class - build the
filter in the same file, or promote the record.

---

## 5. Bigger items still queued

| item | notes |
|---|---|
| Phase 8 - MCP server | `ModdingGUI.Mcp/Program.cs` is still an inert 46-line stub. One tool per Core method, `logs_*` over `IAppLog`, auto-generated docs. Would have made several of this session's experiments unnecessary. |
| Phase 9 - polish | Undo UI over `.mod-history`, per-backup diff viewer, project-wide search, validate-project aggregate, README plus per-format docs. |
| Team Builder enemy schools | Only the two campaign rosters are offered. The real enemy schools are in `data/units` (`schools.txt`, 144 entries, and `gladiators.txt`'s `School:` index) - not the 76 `data/school/*.tok` files, which are dev artifacts. |
| `UNITDB` slots 17/20/21 | Stored at `unit+0x38`/`+0x39`; no reader traced. The editor hides them behind a checkbox and says the meaning is unknown. |
| Arena map placement | ~35 arenas have no confirmed map placement in the Grid Builder overlay. |
| Pending in-game QA | See `HANDOFF.md` §6: randomizer playthrough, edited units, `gold_test`, Team Builder export, GUI pass. |

---

## 6. Hard-won facts worth not rediscovering

These cost real time. They are also in `DOL_FINDINGS.md`, listed here because features depend on them.

- **A run cannot mix `TYPE` values.** Mixed runs abort to the League Office after the first battle.
- **`TYPE "Endurance"` is the carry-over flag.** `EncounterSetup` compares the type with `"Endurance"` and ORs `0x10` into the
  session flags; its absence is the reset case that returns to squad selection. Proven in game: straight into the next battle,
  wounded units, affinities kept.
- **A run always begins at its first battle.**
- **Every `.tga` under `data/texture/` is a PTTP page, not a Targa file.** Cards are `fmt 0x22`: CMPR plus an alpha plane.
- **League cards and arena portraits are named after the league / arena**, lowercased: `$t\gui\leagues\<name>.tga` with
  `league_default.tga` as fallback, and `$t\classes\large\leagueowner_<arena>.tga`.
- **Encounter display names are two hops away**: league file `ENCOUNTER "Battle1", 2228` then `lookuptext_eng.txt` id 2228.
  Leagues work the same way (`LEAGUE "...", 2179`). The lookuptext format is `id^speak^listen^text` in vanilla and `id^text` in
  modder files; both are supported.
- **Affinity ids for `UNITDB` slot 8**: `1 Earth, 2 Water, 3 Dark, 4 Air, 5 Fire`, `-1` = any. This is a *third* table, distinct
  from the DOL name block and the unit data's 3-bit field.
- **A new league works.** The engine enumerates leagues from `<arena>_leagues.tok` at runtime; a modded league needs nothing else.

---

## 7. Verification traps

- **`bec unpack` resolves names from a hash table, not from the archive.** An unregistered path unpacks as
  `unknown-<tag>-0x<hash>.<ext>`. Mod files now register into `<bec>/mod-hashes.txt`, which `unpack` reads; copy that file into
  the output folder when verifying by hand, or the new file will look missing.
- **Duplicate entries share a payload.** Two files with identical content are stored once (compressed size OR-ed with
  `0x02000000`, same offset). "Not in the unpacks" does not mean "not in the archive" - look the hash up instead.
- **Never trust a decoder we wrote to validate an encoder we wrote.** That is how the scrambled card shipped.
- **MSB3027/MSB3021 build errors** mean a running `ModdingGUI.exe` is holding the DLLs. Kill it before building.
- **PowerShell multi-line `-replace` silently misses** when the file uses CRLF. Splice by line index and *verify the file length
  after writing* - a bad splice once truncated a source file to zero.
- **`$Host`/`$host` is a reserved PowerShell variable.** Use another name for a file path.
