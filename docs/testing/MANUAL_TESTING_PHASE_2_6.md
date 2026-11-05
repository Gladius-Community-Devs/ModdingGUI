# Phase 2.6 Manual Testing — Item Editor on `IItemsApi`

Manual tests for the Item Editor migration to `IItemsApi` + Core-backed `lookuptext_eng.txt` writes.

> **Run order matters:** do the full automated suite first (`dotnet test ModdingGUI.sln`), then these manual tests, then restart the app for the Final Sanity section.
>
> **What this verifies:** every Item Editor save goes through the Core API, lands in `.mod-history` as a timestamped `.bak`, preserves unknown fields/comments, and resolves display names/descriptions via `lookuptext_eng.txt` without the app writing that file directly except through `ILookupTextApi`.

## Safety Setup

1. **Never test against `clean-bec-unpacked` directly.** It is reference-only and not in version control.
2. Create a disposable GUI project. Two options:

   **Option A — unpack the vanilla ISO with the app (fullest workflow test):**
   1. Launch the app.
   2. Go to the **Unpacking** tab.
   3. Click **Select ISO** and pick `E:\code_stuff\GladiusModdingToolsGUI\vanilla_gamecube.iso`.
   4. Enter a throwaway project name (e.g. `TEST26`) in the project-name box.
   5. Click **Unpack!** and wait for the batch to finish (log goes green/red; requires Python + the `tools/` folder next to the exe).
   6. Confirm `TEST26/TEST26_BEC/data/config/items.tok` and `.../lookuptext_eng.txt` exist.

   **Option B — reuse the staged REALDATA project (fast, no Python needed):**
   1. `bin\Debug\net8.0-windows\win-x64\REALDATA_vanilla\vanilla_BEC\` (generated from the vanilla ISO by `setup-realdata.ps1`) already looks like a normal project to the app. If it has been dirtied, regenerate it from scratch: `ModdingGUI\setup-realdata.ps1` (re-breaks the ISO down with the app's own toolchain and byte-verifies against `clean-bec-unpacked`).
   2. It is a *copy* — safe to edit, but remember it also feeds the `RealGameDataTests`, so re-run `setup-realdata.ps1` after you finish if you want a pristine test-data set.

3. Before testing, copy these files somewhere outside the project for comparison:
   - `<project>/<project>_BEC/data/config/items.tok`
   - `<project>/<project>_BEC/data/config/lookuptext_eng.txt`
4. Delete or move any existing `<project>/<project>_BEC/.mod-history` so backup counts start clean.
5. Launch the app and select the test project in the project tree.

> **Pick items that cover both cases:** sort the dropdown by display name and pick (a) a weapon with an affinity (e.g. a Fire/Earth weapon) and (b) an item with no affinity (many accessories/armor). You need both for the affinity tests.

## Load Smoke Test

1. Open the **Editors** tab, then the **Item Editor** sub-tab.
2. Confirm the item dropdown is populated (1,852 items on vanilla data).
3. Select several items, including an item **with** affinity and one **without**.
4. Confirm the **Original Item Details** preview updates and no error appears in the log.
5. Confirm **Item Mesh**, Mesh2, and Material are read-only/informational.

Expected:
- Item selection is responsive; no re-read of `items.tok` on every selection (DTO cache).
- Display names resolve from `lookuptext_eng.txt`; items without a display-name id fall back to the internal name.
- No direct file-write errors appear.

## Patch Existing Item

1. Select an item.
2. Change **Item Cost** to a distinct value.
3. Change **Item Level** to a distinct value.
4. Change affinity to a non-`NONE` value and set an amount (or change an existing affinity amount).
5. Change one or both **Item Skills**.
6. Add or change one stat mod in **Item Stat Mods** (use **Add More Stats** to add a row).
7. Click **Save/Update Item**.
8. Reselect the same item (or restart/reload the project) and verify the values persisted.

Expected:
- `items.tok` changed through Core (log shows `Item '<name>' saved.`).
- Exactly **one** new `.bak` in `<project>/<project>_BEC/.mod-history/data/config/items.tok/`.
- The `.bak` is byte-identical to the pre-save `items.tok` copy you saved in Setup.
- Existing comments/unknown item fields outside the edited fields are preserved (diff the new file against your pre-test copy — only expected lines differ).

## Clear Affinity

1. Select an item that **has** an affinity.
2. Set the affinity dropdown to `NONE`.
3. Click **Save/Update Item**.
4. Reselect the item.

Expected:
- Affinity shows as `NONE` and the amount box is disabled/empty.
- The item no longer has an `ITEMAFFINITY` line in `items.tok`.
- One new `items.tok` backup is created.

## Display Name Lookup Edit

1. Select an item whose **New Name** box is enabled (i.e. it has a `ITEMDISPLAYNAMEID`).
2. Change the display name text.
3. Click **Save/Update Item**.
4. Reselect the item and confirm the dropdown label updates to the new text.
5. Inspect `lookuptext_eng.txt` for the changed text.

Expected:
- `lookuptext_eng.txt` is edited via `ILookupTextApi`.
- A backup appears under `<project>/<project>_BEC/.mod-history/data/config/lookuptext_eng.txt/`.
- `items.tok` should **not** change unless another item field was changed at the same time.

## Description Lookup Edit

1. Select an item with an existing description.
2. Change the description text in **New Description**.
3. Click **Save/Update Item**.
4. Reselect the item and confirm the description persists.

Expected:
- The existing lookup row is updated (not duplicated).
- A `lookuptext_eng.txt` backup is created.
- No `items.tok` change unless another field was edited.

## Add Missing Description

1. Find an item with an empty description (leave the description box empty to start).
2. Enter a new description.
3. Click **Save/Update Item**.
4. Reselect the item.

Expected:
- A new lookup row is **appended** to `lookuptext_eng.txt` with a fresh unused id.
- The item gains an `ITEMDESCRIPTIONID` in `items.tok`.
- **Both** `lookuptext_eng.txt` and `items.tok` backups are created.

## Add Item

1. Click **Add** (top of the Item Editor, next to the dropdown).
2. Fill required fields: **Internal Name**, **Type**, **SubType**, **Style**.
3. Optionally fill display name, description, cost, min level, rarity, mesh/material, affinity, skills, regions, attributes, hide sets, show set, and stat mods.
4. For stat mods use one `stat=value` pair per line, e.g. `accuracy=3`.
5. Click **Add Item**.
6. Confirm the new item appears in the dropdown and can be selected.

Expected:
- New item appended through `IItemsApi.Add`.
- One `items.tok` backup; plus a `lookuptext_eng.txt` backup if a display name or description was provided.
- The new record is written at the end of `items.tok` and re-parses cleanly.

Negative checks:
- Add an item with a **duplicate internal name** → it must fail without changing any files.
- Enter invalid stat mod text such as `badline` → the dialog must stay open and show an error.

## Rename Item

1. Select the test item you added.
2. Click **Rename**.
3. Enter a new unique internal name.
4. Confirm the dropdown updates and the old internal name is gone.

Expected:
- Rename goes through `IItemsApi.Rename`.
- One `items.tok` backup.
- Renaming to an existing name is rejected (error shown, no file change).

## Remove Item

1. Select the test item you added or renamed.
2. Click **Remove**.
3. Confirm the warning dialog.
4. Verify the item disappears from the dropdown.

Expected:
- Remove goes through `IItemsApi.Remove`.
- One `items.tok` backup.
- Other items still load correctly and the file still round-trips (no leftover blank lines/records).

## Backup Verification

After the edit tests, verify these folders as applicable:

```text
<project>/<project>_BEC/.mod-history/data/config/items.tok/
<project>/<project>_BEC/.mod-history/data/config/lookuptext_eng.txt/
```

Expected:
- **Each mutating save has exactly one timestamped `.bak`** — count them against the number of saves you performed.
- Each `.bak` contains the file state immediately before that individual write (spot-check by opening one).

## Failure Cases

1. Temporarily rename `lookuptext_eng.txt` (e.g. to `lookuptext_eng.txt.bak`) inside the project.
2. Try changing a display name or description and saving.
3. Restore the file afterward.

Expected:
- The UI shows a lookup-text save error and does **not** silently claim success.
- The error is also written to the Item Editor log in red.

## Round-Trip Preservation Check

1. Compare the final `items.tok` against the pre-test copy you saved in Setup.
2. The only diffs should be the fields you intentionally edited (cost, level, affinity, skills, stat mods, added/removed items, `ITEMDESCRIPTIONID` additions).
3. Confirm header lines, comments, blank-line structure, and **all other items** are byte-identical.

Expected:
- No stray reordering of records, no dropped comments, no reformatting of untouched items.

## Final Sanity

1. Restart the app.
2. Reopen the same test project.
3. Confirm the Item Editor still loads all items with no log errors.
4. Optionally run a pack (or **Pack Validation**) on the project if it must stay valid — then remove throwaway test items first.
5. If you used Option B, re-run `setup-realdata.ps1` afterwards so the real-data test fixture is pristine again.

## Completion Checklist

- [ ] Load smoke test passed (dropdown populated, no log errors, mesh/material read-only)
- [ ] Patch existing item persisted + 1 backup created
- [ ] Clear affinity removed the `ITEMAFFINITY` line
- [ ] Display name edit updated the dropdown and `lookuptext_eng.txt`
- [ ] Description edit updated the existing lookup row
- [ ] Add-missing-description appended a row and added `ITEMDESCRIPTIONID`
- [ ] Add item (incl. duplicate-name and bad-statmod negative checks)
- [ ] Rename item (incl. duplicate-name rejection)
- [ ] Remove item
- [ ] Backup counts match the number of saves; `.bak` contents correct
- [ ] Lookup-text failure case reports an error (no silent success)
- [ ] Round-trip diff shows only intended changes
- [ ] Restart sanity passed; test data reset if needed
