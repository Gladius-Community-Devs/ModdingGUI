# v008 tools as a drop-in for the current ModdingGUI — validation report

**Date:** validation run performed against `vanilla_gamecube.iso` (1,459,978,240 B, SHA256 `7A14F16AC7D0EC5F…`)
**Target:** the tools contract used by the shipping ModdingGUI (`ProgramInfo.Version = 4.2.0`; batch generation identical in tag `4.1.3` and current `HEAD`)
**Tool under test:** `E:\code_stuff\GladiusTools\Tools\original-tools\*` (the 7 drop-in wrappers) + `E:\code_stuff\GladiusTools\Tools\tools_v008\` (package) + `hashes.txt`
**Harness + raw logs:** `E:\code_stuff\GladiusModdingToolsGUI\_v008_val\`

---

## Verdict

**No, it had not been validated — and until today it had never been run through the ModdingGUI path at all.**

What existed before:

| Artefact | What it actually validated |
|---|---|
| `v008_moddinggui_test\HOW_TO_TEST_V008_IN_MODDINGGUI.txt` | A *procedure* for swapping the 7 scripts into the GUI. No recorded result — never executed. |
| `Recomp\_builds\modwork_user_v008*\FINDINGS.txt`, `USER_FINDINGS.md` | The standalone **`tools_v008.exe` + `Unpack_Vanilla_GC.bat` / `Pack_Modded_GC.bat`** flow. Valuable, but it never touches the drop-in wrappers or the GUI's generated batch. |
| `CHANGELOG.md` "Drop-in replacement for v007" + `README.txt` | Claims, not evidence. |

I have now run the GUI's exact command sequence end-to-end. **The drop-in works.** All 9 tool invocations (3 unpack, 6 pack) succeed, the migration path from a project unpacked by the *current* tools is byte-clean, and every one of the 27,292 real game files in a v008-packed BEC is byte-identical to a current-tools-packed BEC. Three things still need a human decision or an in-game check (see **§4**), and two *silent* traps should be closed before telling users to swap.

---

## 1. What the current GUI actually invokes

`frmMain_partials/BATGeneration.cs` → `Helpers/BatchContentBuilder.cs` (identical in `4.1.3` and `HEAD`) writes a `GeneratedBatch_*.bat` run via `cmd /c` with the app directory as cwd:

```
python tools/ngciso-tool.py            -unpack <iso> <proj>_ISO/ <proj>_FileList.txt
python tools/bec-tool-all.py           --platform GC -unpack <proj>_ISO/gladius.bec <proj>_BEC/
python tools/Gladius_Units_IDX_Unpack.py <proj>_BEC/Data/
--- edit, then Pack ---
python tools/Tok_Num_Update.py         <proj>_BEC/Data/Config/
python tools/tok-tool.py               -c <skills.tok> <skills_strings.bin> <skills_lines.bin> <skills.tok.brf>
python tools/Update_Strings_Bin.py     <proj>_BEC/Data/Config/
python tools/Gladius_Units_IDX_Repack.py <proj>_BEC/Data/
python tools/bec-tool-all.py           -pack <proj>_BEC/ <proj>_ISO/gladius.bec <proj>_BEC/filelist.txt --platform GC
python tools/ngciso-tool.py            -pack <proj>_ISO/ <proj>_ISO/fst.bin <proj>_ISO/<proj>_FileList.txt <proj>.iso
```

Notes that matter:
- The batch has **no error handling** (`||`, `if errorlevel`, `goto :failed`) and `RunBatchFileAsync` **never inspects the exit code** — it only streams stdout/stderr to the log.
- The batch is written **into the project folder and executed with that folder as cwd** (`Form1.cs:92` unpack, `:175` pack), not the app directory. All tool paths in the batch are absolute, so this is harmless — except that it means `hashes.txt` can only be found via the package-relative lookup (next to `tools_v008\`), never via cwd. This is exactly why the documented placement matters (see §4C).
- The exe-generated batch uses `python`; the hand-runnable template bats shipped next to the exe (`GUITest\Pack_Modded_GC_ISO.bat`) use `py`. Both work (on this machine `python` = Store Python 3.11.9, `py` = 3.12.10).
- Only GameCube is wired up — PS2/Xbox limitations in the v008 changelog are irrelevant to the GUI.
- Recompiling `.scp` scripts is a separate GUI feature and does not go through these 7 scripts.

## 2. Deployment: both layouts work

`tools_v008\` + `hashes.txt` were placed (A) inside `tools\` as the HOW_TO_TEST doc says, and (B) in the app root (parent of `tools\`). All **7 wrappers import and parse their arguments correctly in both layouts** — Python puts the script's own folder on `sys.path`, so the parent-only `sys.path` insert in `ngciso-tool.py` / `tok-tool.py` is harmless. Use Layout A; it matches the documentation.

## 3. What was verified (evidence)

| # | Check | Result |
|---|---|---|
| 1 | Unpack pipeline (ngciso → bec → idx), GUI command lines | exit 0; `_ISO` 1,395 MB / 47 files, `_BEC` 27,305 files, 6 unit `.txt` + `filelist.txt` produced, `NUMENTRIES` headers correct (skillsets 230/230 sections). |
| 2 | 47 unresolved files | Named by content (`unknown-script-0x00c5203b.scp`, `unknown-pttp-…`, `unknown-pak-…`, `unknown-mesh-…`, `unknown-bat-…`, `unknown-text-…`) — the v008.3 feature works, `hashes.txt` was found. |
| 3 | Pack pipeline (6 steps) on an untouched project | all exit 0; `toknum`, `tok compress`, `strings`, `idx repack` leave every input **byte-identical** (no spurious rewrites). |
| 4 | **Migration**: project unpacked by the *current* tools, packed with swapped-in v008 | BEC SHA256 `137511056DDA0D4A…` — **identical** to the v008-native unpack→pack path. Old v007-era projects migrate cleanly. |
| 5 | **BEC equivalence**: unpack both packed BECs with the same tool | v007-packed tree 27,305 files, v008-packed 27,299. **Every one of the 27,292 real game files is byte-identical.** Only differences: the 6 generated `data/units/*.txt` (v007-only, see §4A), `lookuptext_eng.bin` (§4B) and `filelist.txt` (metadata). |
| 6 | Repeat pack (exercises the new `repack-filelist.txt` manifest) | 2nd pack **byte-identical**, 0 warnings/changes — manifest is stable, no file loss. |
| 7 | ISO round trip | `gladius.bec` inside the packed ISO is byte-identical to the packed one. |
| 8 | **Injected IDX error** (`TestGladiator` with `Skill set: 9999`, from `v008_moddinggui_test\gladiators.txt`) | `idx repack` exits **1** with the exact documented message naming file/unit/field/value/range; **no `.idx` file was written**. After `Skill set: 226`, repack exits 0 and `TestGladiator` is in `unitnames.idx`. |
| 9 | Cross-check: `lookuptext_eng.bin` | v008 output = **byte-identical to vanilla** (541,932 B, `e46f269ca1ebb7b8…`). The current tool's output is not (542,112 B, `eeb8065e5ed3069c…`) — 21 entries contain a literal `\r\n` instead of a real newline. v008 is a **fix** here. |
| 10 | Hash-table coverage | v007 leaves **241** files unnamed (`unknown-1234.bin`); v008 leaves **47** — **194 more** get real paths (e.g. `data/levels/**/*_pms.pax`, `data/font/*.tga`, `data/buildpaks/*.pak`, `data/batch/*.bat`). |
| 11 | Threading (where, exactly) | `bec.py` only: `unpackBecArchive2` (line 371) and `createBecArchive` (line 519) use `ThreadPoolExecutor(max_workers=max(4, os.cpu_count()))`, overridable with `bec-tool-all.py --threads N`; the GUI passes nothing so it uses that default. IDX / tok / strings / ISO are still single-threaded. v007's `threading` + `queue` imports are **unused** and it called `flush()` per file, so the speed-up is threads *and* the removed flush. Same input: BEC pack 106.9s → 66.2s, BEC unpack 11.0s → 7.8s. |

## 4. Differences and gaps that still need attention

**A. The 6 generated `data/units/*.txt` are no longer packed into the modded BEC.** This is intentional (`bec.py` `GENERATED_TXT_FILES`) — in the current flow they were added as junk: re-unpacking a v007-packed BEC shows them as 6 extra `unknown-text-0x…txt` entries, i.e. the current tools store them under hashes the game cannot look up. Vanilla does not contain them. **Still needs one boot test** (they are what the game's own build tooling produced for the dev tree).

**B. `lookuptext_eng.bin` changes on every pack** (back to vanilla-exact). Almost certainly a fix, but it is the file the game reads for item/skill/lore text — needs an in-game spot check.

**C. Missing `hashes.txt` is a silent, total failure.** Verified: with no `hashes.txt` anywhere, `bec unpack` **exits 0 with no warning** and writes all **27,298 / 27,298** files as `unknown-*` (no `data/config/skills.tok` at all — the whole project is unusable). This is the single most likely mistake when a user swaps files by hand, because `hashes.txt` is a *new* file that the current tools folder does not have.

**D. The GUI ignores tool exit codes, so v008's new fail-fast is half-wired.** Verified: with the bad `gladiators.txt`, the GUI's generated batch (unchanged) printed the `ERROR:` line and **still built `err.iso` (1,459,980,992 B) containing stale unit data** — `TestGladiator` absent — and exited **0**. The user gets a red line in a wall of log text and an ISO that looks fine. `HOW_TO_TEST_...txt` claims "the ModdingGUI / batch can detect the failure instead of silently continuing with stale data" — that is **false for every shipped version**. (Not a regression: the old tools traceback'd and continued the same way.) The GUI's own pre-pack validation does not cover it either: `Form1.cs:141-161` runs `ValidateItemsets` / `ValidateGladiatorsFile` / `ValidateGladiatorsAndSkills` and *errors* block the pack, but this case only produces a non-blocking **Warning** ("Skill set 9999 … is empty or not found"), so validation passes and packing continues.

**E. Error surface is partial.** Only `idx.IDXDataError` is converted to a friendly message. A missing/unreadable file still produces a raw Python traceback from the wrappers (e.g. `FileNotFoundError: … unitunits.idx`), despite the v008.1 changelog claim — that only applies to the `tools_v008.exe` CLI.

**F. Update-safety.** The app auto-updates from `Gladius-Community-Devs/ModdingGUI` releases; a new release that ships `tools/` will silently revert the swap. Document "re-apply after updating".

**G. Not a risk.** PS2/Xbox (GC-only GUI); `data/debug/skills.txt|classes.txt|items.txt` (the current unpack writes them, v008 does not) — `git grep` over **every** revision shows no code ever reads them.

## 5. What still needs to be tested

The one artifact waiting for a human is **`_v008_val\v008_gui_pipeline.iso`** (built by the exact GUI command sequence from a pristine vanilla project).

**Tier 1 — must pass before telling users to swap**
- [ ] **Dolphin boot test** of `v008_gui_pipeline.iso`: boots to the main menu, a battle starts, no crash. Then a *modded* v008 ISO (rename one item and one gladiator) shows the new name in-game. This covers §4A and §4B.
- [ ] **Lore-text spot check** in that same ISO — entry 4291 ("Why?… my home…") is one of the 21 entries whose `\r\n` handling changed. It should render as two line breaks, not as literal `\r\n`.
- [ ] **Real-install swap dry-run** on a 4.2.0 install: back up `tools\`, drop in the 7 scripts + `tools_v008\` + `hashes.txt`, then run **Unpack!** and **Pack!** from the GUI on vanilla. Expect no red lines and a new `.iso`.
- [ ] **Decide the `hashes.txt` trap (§4C)**: either make a missing table a hard error ("hashes.txt not found next to tools_v008/ — unpack would name every file unknown-*"), or lean on the swap instructions + a startup check in the GUI.
- [ ] **Decide the exit-code gap (§4D)**: add `|| goto :failed` / exit-code checking to `BatchContentBuilder.GenerateUnpackContent`/`GeneratePackContent` (and the shipped template bats), so a failed `idx repack` cannot silently produce a stale ISO. Cheap, and it unlocks the whole point of the v008 error work.

**Tier 2 — should do (regression surface the GUI drives)**
- [ ] A genuinely modded, *existing* user project (v007-era unpack) → Pack with v008 → Dolphin. I proved the vanilla case is byte-clean; confirm with real edits.
- [ ] Randomizer / Team Builder / Item Editor outputs → Pack. These rewrite `gladiators.txt`, `statsets.txt`, `itemsets.txt`, `skillsets.txt` and `items.tok`; v008 now hard-fails on a stale `NUMENTRIES:` header and on out-of-range set references, where the old tools packed silently.
- [ ] CRLF `gladiators.txt` (Notepad re-save) through the GUI → Pack (v008.1 CRLF handling).
- [ ] Add/drop a file in `_BEC` (e.g. drop in a replacement `.ptx`, delete a file) → Pack twice; confirm the `repack-filelist.txt` manifest warning/skip behavior is understandable in the GUI log and no file is silently lost.
- [ ] `Update_Strings_Bin.py` with an edited `lookuptext_eng.txt` (including one of the 21 multi-line entries) → Pack → re-unpack; confirm the `.bin` matches the `.txt`.
- [ ] Broken-input messages visible through the GUI log: out-of-range `Outfit:`/`Stat set:`/`Item set:`/`School:`, misspelled skill/class, stale `NUMENTRIES:`.

**Tier 3 — nice to have**
- [ ] Re-run this harness after any `tools_v008` change: `_v008_val\run_unpack.ps1`, `run_pack_p1.ps1`, `run_errtest.ps1`, `run_extra.ps1`, `run_migrate.ps1`, `run_compare.ps1` (they need ~18 GB free; they rebuild everything from `vanilla_gamecube.iso`).
- [ ] PS2/Xbox — out of scope for this GUI.

## 6. Recommendation

The wrappers are mechanically sound: same names, same arguments, both documented layouts, clean migration, byte-clean output. **Ship it as a documented manual swap — but only after (a) the Dolphin boot/mod-text check and (b) fixing the two silent traps.** The `hashes.txt` trap and the ignored exit code are the two ways a user can "successfully" pack a broken mod with v008, and both are cheap to close.

Suggested user-facing swap text (6 files + 1 folder + 1 table, unchanged names):

```
<ModdingGUI>\tools\
    bec-tool-all.py                 (replace)
    Gladius_Units_IDX_Unpack.py     (replace)
    Gladius_Units_IDX_Repack.py     (replace)
    Tok_Num_Update.py               (replace)
    tok-tool.py                     (replace)
    Update_Strings_Bin.py           (replace)
    ngciso-tool.py                  (replace)
    tools_v008\                     (NEW folder: __init__.py bec.py idx.py tok.py strings.py iso.py hashes.py)
    hashes.txt                      (NEW - REQUIRED, ~1.7 MB; unpack is useless without it)
```

Everything else in `tools\` (`gladiushashes.py`, `gcnamehashes.py`, the `.exe`s, `filelist.txt`) stays as-is and is simply unused by the new scripts.

---

## 7. Release prep: 4.1.4

**Published 2026-10-11** — tag `4.1.4`, `origin/main` and the branch all at `8e4304f`.
Build artefacts live in `_release414\`.

| Item | Value |
|---|---|
| Base | official `ModdingGUI-4.1.3.zip` asset (sha256 `d4adfdf3097d55c4…`, matches GitHub's digest) |
| Source | tag `4.1.3` (`a6f9f95`) + 4 files changed |
| Build | `dotnet publish -c Release -r win-x64 --self-contained true -p:PublishProfile=FolderProfile` |
| Exe | 164,590,203 B, launches and creates first-run settings |
| Zip | `_release414\ModdingGUI-4.1.4.zip`, 76,682,760 B, sha256 `9A5C0F37B12EE486…`, 64 entries |
| Draft release | `gh release view 4.1.4 -R Gladius-Community-Devs/ModdingGUI` — `isDraft: true`, asset digest `9a5c0f37…`, **no git tag created**, 4.1.3 still `Latest` |
| Source changes | committed **locally only** on branch `release/4.1.4` (`6deff58` + `8e4304f`), not pushed |
| Toolchain change | `E:\code_stuff\GladiusTools\Tools\tools_v008\strings.py` edited in place (that tree is not a git repo) |
| RC install | `_release414\rc-install\` — exactly the current zip contents, ready to run |

Changes in the 4.1.4 exe (4 files — `ProgramInfo.cs`, `BATGeneration.cs`,
`Form1.cs`, `Utilities.cs`; 134 insertions / 21 deletions):

1. `ProgramInfo.Version` → `"4.1.4"`.
2. `BATGeneration.cs` — every generated tool step is `|| goto :pack_failed` /
   `:unpack_failed`; unpack preflights `tools\hashes.txt` **only when
   `tools\tools_v008\` is installed** (so an exe-only partial update is not
   blocked); `RunBatchFileAsync` now returns the exit code and logs a red
   summary on failure; `Form1.cs` no longer reports success (or writes project
   paths / shows the success dialog) when the exit code is non-zero.
3. `Utilities.cs` — `WARNING:` lines render in the warning colour and `ERROR:`
   lines in red, whichever stream they arrive on (previously all of stderr was
   red, so a benign warning looked like a hard failure).

Changes in the toolchain (`tools_v008\strings.py`):

4. The `lookuptext_*.bin` "N id(s) have no entry" warning now fires **only** for
   entries that had text in the `.bin` being replaced and are empty in the new
   one (deleted, renumbered or cleared line), instead of on every one of the
   vanilla table's ~64 always-empty holes. Written bytes are unchanged.


E2E results against the staged payload (`_release414\e2e.log`):

| Scenario | Exit | Effect |
|---|---|---|
| Unpack, normal | 0 | `_ISO` + `_BEC` + file list, all 6 unit `.txt`, `NUMENTRIES` correct |
| Unpack, `hashes.txt` missing (v008 installed) | **2** | clear message, **nothing written** |
| Unpack, old tools + new exe (partial update) | 0 | unchanged behaviour, project complete |
| Pack, unmodified | 0 | ISO written; BEC sha256 `137511056DDA0D4A…` (same as the pre-change run) |
| Pack, `Skill set: 9999` injected | **1** | `.idx` untouched, **no ISO written**, v008 message + new ERROR block |
| Pack, after fixing to `226` | 0 | ISO written |
| `strings`, untouched vanilla `.txt` | 0 | **silent**; `.bin` sha256 `E46F269CA1EBB7B8…` = vanilla |
| `strings`, entry 457's line deleted | 0 | warns, naming entry 457 |
| `strings`, entry 457's text cleared | 0 | warns, naming entry 457 |
| `strings`, no existing `.bin` (no baseline) | 0 | silent |

### Environment caveat — do not launch agent-created copies

The DSH sandbox applies a **Low** mandatory integrity label to everything under
this workspace, and it cannot be raised from inside the sandbox. A
`ModdingGUI.exe` extracted by the agent therefore **refuses to launch for a
normal user**: no window, no crash log, no WER entry, no Defender event, and the
single-file cache under `%LOCALAPPDATA%\Temp\.net\ModdingGUI` never advances (which
is how it was diagnosed — the cache timestamp proved the process never started).

Verified 2026-10-11: the *same zip*, extracted by the user with Explorer outside
`E:\code_stuff`, launches 4.1.3 **and** 4.1.4 normally, with only the expected
first-run SmartScreen prompt. So this is an artefact of agent staging, not of the
release.

**Rule: every GUI check must be run from a folder the user extracted themselves**
(Explorer "Extract All…"), never from `_release414\rc-install\`, `stage\`,
`updtest\` or `e2e\`. A copy made by a normal process, or extraction by Explorer,
produces correctly-labelled files; `Copy-Item` from the agent does not help
because the creating process's label is what sticks.

### Sign-off status

Verified in the real GUI (user, `_release414\rc-install`, `vanilla_gamecube.iso`):

| # | Step | Status |
|---|---|---|
| 1 | Launch; File → Info says `4.1.4` | **pass** |
| 2 | Unpack (`414_update`) | **pass** |
| 3 | Pack, unmodified → ISO | **pass** |
| 4 | Pack with `TestGladiator` / `Skill set: 9999` → red `ERROR:` line, guard block, `Packing FAILED.`, no ISO | **pass** |
| 5 | Fix (`226`) → Pack → success | **pass** |
| 6 | Boot the packed ISO in Dolphin; play the world map, fight | **pass** |
| 7 | Edit `lookuptext_eng.txt` (entry 457 → `MacheraValidation`), pack, see it in the in-game equipment list | **pass** — the string-table path is game-readable end to end |

Outstanding before publishing:

1. **Re-check the two log changes on the current build** (2026-10-11 repackage; the
   build you tested predates them). In `_release414\rc-install`: pack
   `414_update` once. The `64 id(s) have no entry` line should be **gone
   entirely**. To see the new warning, temporarily delete one line from
   `lookuptext_eng.txt` (e.g. the `457^` entry) and pack: it should appear in
   **orange**, naming the entry you removed. Restore the line afterwards.
2. **In-place update over an existing 4.1.3 install** — the mechanical half is
   **verified** (`_release414\updtest`): 4.1.3 extracted, a `settings.json` and a
   project folder added, then `ModdingGUI-4.1.4.zip` extracted over the top. Result:
   `settings.json` byte-identical, project `.txt` and ISO files byte-identical, exe
   swapped for the 4.1.4 build, all 7 scripts replaced, `hashes.txt` +
   `tools_v008\` + the how-to present, no stray files. **Left to eyeball:** launch
   `_release414\updtest\ModdingGUI.exe` once and confirm File → Info says `4.1.4`
   and the project still appears in the tree.
3. **Missing `hashes.txt` in the GUI** (old step 6) is verified at batch level
   (exit 2, nothing written) but has not been clicked through in the app; covered
   by the re-check in item 1 if you rename the file away.


### Maintainer follow-ups

- **Sign the exe when you can.** It is unsigned, so every user gets the SmartScreen
  "Windows protected your PC" box on first launch and the binary carries no
  reputation. That is the only known first-run friction in 4.1.4.
- **Done after the release:** `main` was fast-forwarded to the release commit
  (`a6f9f95..8e4304f`), and `Tools\tools_v008\CHANGELOG.md` now carries a `v008.4`
  entry for the conditional `lookuptext` warning. That tree is not under version
  control, so the changelog edit exists only on disk. Follow-up check: all 7
  modules inside the published `ModdingGUI-4.1.4.zip` hash-match that folder.
- The GitHub release body is deliberately short and user-facing (house style, and it
  is what the Discord webhook posts). **This document is the technical record** —
  the removed detail lives here, not in the release notes.

### Publishing (yours to run when the checklist is green)

```bash
# 1. push the source changes (they are NOT pushed yet)
git -C ModdingGUI push origin release/4.1.4     # or merge to main and push

# 2. the draft's tag does not exist yet; point it at the pushed commit and publish.
#    Publishing the draft is what creates the tag and fires the Discord webhook.
gh release edit 4.1.4 -R Gladius-Community-Devs/ModdingGUI --target <pushed-commit>
gh release edit 4.1.4 -R Gladius-Community-Devs/ModdingGUI --draft=false --latest

# to back out instead:
gh release delete 4.1.4 -R Gladius-Community-Devs/ModdingGUI --yes
```
