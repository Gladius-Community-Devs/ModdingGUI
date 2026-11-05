# Phase 2.7 Manual Testing — Skills validation, catalog v2, meter fix

Manual checks for the Phase 2.7 work: Core-owned catalog, advisory validation,
`SKILLMETER` dropdown fix, and no-op save fidelity.

> **Run order:** full automated suite first (`.\run-tests.ps1 -Configuration Release`,
> expect **246 passed / 0 failed / 0 skipped** with `REALDATA_vanilla` present), then
> these manual checks.

## Safety setup

1. **Never test against `clean-bec-unpacked` directly** — reference only.
2. Use a disposable project. Fastest: `bin\Release\net8.0-windows\win-x64\REALDATA_vanilla`
   (regenerate with `ModdingGUI\setup-realdata.ps1` if it has been dirtied), or unpack
   `vanilla_gamecube.iso` into a throwaway project via the Unpacking tab.
3. Delete `<project>/<project>_BEC/.mod-history` first so backup counts start clean.
4. Copy `data/config/skills.tok` somewhere outside the project for byte comparison.

## 1. Load

1. Launch the exe directly from the build output, select the project, open **Editors → Skill Editor**.
2. Expect: skill dropdown populated with **1,811** entries; status shows "Loaded 1811 skills."
3. The **Add Field** field dropdown lists **71** catalog fields, alphabetically, starting with `SKILLAFFCOST`.
4. Select a skill → SKILL Box fills; the **Validate** button is enabled.

## 2. `SKILLMETER` dropdown (regression fix)

Previously the meter dropdown was always empty because the editor looked for a
non-existent `skillmeter.tok`.

1. Pick a skill that uses a meter, e.g. one whose SKILL Box contains `SKILLMETER: "Chain", …`.
2. In **Add Field**, choose `SKILLMETER`.
3. Expect a dropdown whose first entry is `None`, followed by the 15 `swingmeter.tok` ids
   (`pound1`…`pound3`, `FastButton1-3`, `Precision1-3`, `loop1-3`, `bounce1-3`) and the
   observed extras (`Basic`, `Breakout`, `Chain`, `ChainSlow`).
4. The combo is editable — typing a new id is allowed. No red "No meter IDs found" label.

## 3. Validation button

1. Select a **vanilla, unmodified** skill → click **Validate**.
2. Expect: "No validation issues found in '<skill>'." (Vanilla is guaranteed warning-free
   by the pinned test `Validate_EveryVanillaSkill_HasNoWarnings`.)
3. Now edit the SKILL Box of a scratch copy: duplicate the `SKILLRANGE` line.
4. Click **Validate** → expect a **Warning**: `duplicate-single-cardinality` for `SKILLRANGE`.
5. Delete the second `SKILLRANGE` line, and instead change `SKILLDISPLAYNAMEID` to `999999`.
6. **Validate** → expect a **Warning**: `dangling-lookup-id` (999999 is not in `lookuptext_eng.txt`).
7. Clear the second positional in `SKILLCREATE` (the type, e.g. `"Attack"` → `""`).
8. **Validate** → expect a **Warning**: `missing-positional`.
9. Add an invented field, e.g. `SKILLMADEUP: "x"`.
10. **Validate** → expect an **Info** note (`unknown-field`), *not* a warning — modded data
    must never be treated as an error.

## 4. Save is advisory, never blocked

1. With the record still carrying a warning (step 3 or 7 above), click **Save to Project**.
2. Expect: the save **succeeds**; status reads "saved — validation: N warning(s). Click Validate for details."
3. Confirm `<project>/<project>_BEC/.mod-history/data/config/skills.tok/` gained a new `.bak`.
4. Reopen the skill → the edited content persisted.

## 5. No-op save preserves bytes

1. Re-stage a pristine `skills.tok` copy (or use a fresh skill that was never edited).
2. Select one skill, change nothing, click **Save to Project**.
3. Compare `skills.tok` with the copy taken in setup:
   `Get-FileHash data\config\skills.tok` before and after → **identical**.
4. (The file must still end with the blank separator line — the writer preserves the
   exact trailing-newline count now.)

## 6. Add / Delete still work

1. **New Skill** → name `ZZTestSkill`, type `Attack`, sub-type `Standard`, add
   `SKILLUSECLASS: "Barbarian"` → **Save**.
2. Reload → the skill is in the dropdown and validates with no warnings.
3. **Delete Skill** → confirm; `.mod-history` gains a backup; the skill disappears.

## 7. Catalog regeneration (dev path)

1. From `ModdingGUI/`, run `py -3 skills/build_catalog.py`.
2. Expect the summary: `fields: 71`, `enumerated: 53`, plus `rule support:` line with
   `multi flipped from data 9, links advisory 2, requires advisory 3`.
3. `git status skills/skills_catalog.json` → **no diff** (generation is deterministic and
   the committed catalog matches its inputs).
4. Restart the app → identical behaviour (nothing depended on a stale catalog).

## Final sanity

- Close and reopen the app; project selection still loads without errors.
- No unexpected red lines in the log.
- `git status` in `ModdingGUI/` is clean except intentional edits.
