# Phase 2.6 Manual Testing

Manual tests for the Item Editor migration to `IItemsApi` and Core-backed lookup text writes.

## Safety Setup

1. Do not test against `clean-bec-unpacked` directly.
2. Create or use a disposable GUI project that has a normal `<ProjectName>_BEC` folder.
3. Before testing, copy these files somewhere outside the project for quick comparison:
   - `<project>/<project>_BEC/data/config/items.tok`
   - `<project>/<project>_BEC/data/config/lookuptext_eng.txt`
4. Delete or move any existing `<project>/<project>_BEC/.mod-history` if you want a clean backup-count check.
5. Launch the app and select the test project in the project tree.

## Load Smoke Test

1. Open `Editors > Item Editor`.
2. Confirm the item dropdown is populated.
3. Select several items, including an item with affinity and an item without affinity if possible.
4. Confirm the original details preview updates and no error appears in the log.
5. Confirm Mesh, Mesh2, and Material fields are informational/read-only.

Expected:
- Item selection is responsive.
- Display names resolve from `lookuptext_eng.txt`.
- No direct file-write errors appear.

## Patch Existing Item

1. Select an item.
2. Change `Item Cost` to a distinct value.
3. Change `Item Level` to a distinct value.
4. Change affinity to a non-`NONE` value and set an amount, or change an existing affinity amount.
5. Change one or both item skills.
6. Add or change one stat mod.
7. Click `Save/Update Item`.
8. Reselect the same item or restart/reload the project and verify the values persisted.

Expected:
- `items.tok` changes through Core.
- A backup appears under `<project>/<project>_BEC/.mod-history/data/config/items.tok/`.
- Existing comments/unknown item fields outside the edited fields remain preserved.

## Clear Affinity

1. Select an item with an affinity.
2. Set affinity dropdown to `NONE`.
3. Click `Save/Update Item`.
4. Reselect the item.

Expected:
- Affinity shows as `NONE`.
- The item no longer has an `ITEMAFFINITY` line in `items.tok`.
- A new `items.tok` backup is created.

## Display Name Lookup Edit

1. Select an item with a display name field enabled.
2. Change the display name text.
3. Click `Save/Update Item`.
4. Reselect the item and confirm the dropdown label updates.
5. Inspect `lookuptext_eng.txt` for the changed text.

Expected:
- `lookuptext_eng.txt` is edited.
- A backup appears under `<project>/<project>_BEC/.mod-history/data/config/lookuptext_eng.txt/`.
- `items.tok` should not change unless another item field was changed at the same time.

## Description Lookup Edit

1. Select an item with an existing description.
2. Change the description text.
3. Save.
4. Reselect the item and confirm the description persists.

Expected:
- Existing lookup row is updated.
- `lookuptext_eng.txt` backup is created.

## Add Missing Description

1. Find an item with an empty description if available.
2. Enter a new description.
3. Save.
4. Reselect the item.

Expected:
- A new lookup row is appended to `lookuptext_eng.txt`.
- The item gets an `ITEMDESCRIPTIONID` in `items.tok`.
- Both `lookuptext_eng.txt` and `items.tok` backups are created.

## Add Item

1. Click `Add`.
2. Fill required fields:
   - Internal Name
   - Type
   - SubType
   - Style
3. Optionally fill display name, description, cost, min level, rarity, mesh/material, affinity, skills, regions, attributes, hide sets, show set, and stat mods.
4. For stat mods, use one `stat=value` pair per line, such as `accuracy=3`.
5. Click `Add Item`.
6. Confirm the new item appears in the dropdown and can be selected.

Expected:
- New item is appended through `IItemsApi.Add`.
- `items.tok` backup is created.
- If display name or description was provided, `lookuptext_eng.txt` backup is also created.

Negative checks:
- Try adding an item with a duplicate internal name. It should fail without changing files.
- Try invalid stat mod text such as `badline`. The dialog should stay open and show an error.

## Rename Item

1. Select the test item you added.
2. Click `Rename`.
3. Enter a new unique internal name.
4. Confirm the dropdown updates and the old internal name is gone.

Expected:
- Rename goes through `IItemsApi.Rename`.
- `items.tok` backup is created.
- Duplicate names are rejected.

## Remove Item

1. Select the test item you added or renamed.
2. Click `Remove`.
3. Confirm the warning.
4. Verify the item disappears from the dropdown.

Expected:
- Remove goes through `IItemsApi.Remove`.
- `items.tok` backup is created.
- Other items still load correctly.

## Backup Verification

After the edit tests, verify these folders as applicable:

```text
<project>/<project>_BEC/.mod-history/data/config/items.tok/
<project>/<project>_BEC/.mod-history/data/config/lookuptext_eng.txt/
```

Expected:
- Each mutating save has a timestamped `.bak` file.
- A `.bak` should contain the file state before that individual write.

## Failure Cases

1. Temporarily rename `lookuptext_eng.txt` and try changing a display name or description.
2. Restore the file afterward.

Expected:
- The UI shows a lookup text save error.
- It should not silently claim success.

## Final Sanity

1. Restart the app.
2. Reopen the same test project.
3. Confirm the Item Editor still loads all items.
4. Run pack validation if this project is expected to stay valid.
5. If you only used throwaway test items, remove them before keeping the project.
