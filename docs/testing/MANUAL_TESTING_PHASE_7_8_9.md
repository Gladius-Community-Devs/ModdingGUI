# Manual QA — Phase 7.8 and 7.9 (build output, and no batch files)

Phase 7.8 moved pack/unpack/compile out of the shell, and 7.9 stopped generating `.bat` files entirely. Unit and
integration tests cover the command chain (`PackUnpackServiceTests`, `ToolRunnerTests`), but the GUI behaviour —
what appears in the panels, what a failure looks like, and whether a parenthesis in a path is really ordinary — needs
eyes on the window. Do this once per release that touches `BuildRunner.cs` or the toolchain.

## Prerequisites

- The app built **Debug** (that is the exe the user runs) with `tools/` beside it: the Python toolbelt,
  `DOGCodeCompiler.exe`, `xdelta3.exe`.
- Python on `PATH` (`python --version` answers in the same shell you launch the app from).
- A vanilla ISO, and about 4 GB of free space for one unpack + one pack.
- Close Dolphin and any previous app instance.

## 1. Unpack, and watch the log

1. Unpack tab → pick the ISO → project name `qa_789` → **Unpack**.
   - [ ] Lines appear in the unpack panel *while it runs* (ngciso, then bec, then the units index) — not in one
         burst at the end.
   - [ ] A `[debug]` line shows the exact command for each step; it reads `python …/ngciso-tool.py -unpack "…"`,
         with no caret escapes.
   - [ ] The run ends with "Unpacking completed successfully." and the tree shows `qa_789 (incomplete)` until the
         pack fills in the rest.
   - [ ] No `GeneratedBatch_*.bat` exists in the project folder afterwards, and none in `%TEMP%`.

## 2. Pack, and watch the log

2. Packing tab → the project is selected → leave validation on → **Pack**.
   - [ ] Each of the six steps' output streams into the pack panel as it happens.
   - [ ] Progress is visible during the minutes-long bec step (the panel keeps scrolling rather than freezing).
   - [ ] "Packing completed successfully." appears once, at the end.
   - [ ] The ISO exists and is roughly the same size as the vanilla ISO.
   - [ ] No `.bat` was created anywhere.

## 3. A failure must be reported as a failure

3. Rename `tools/ngciso-tool.py` to `ngciso-tool.py.off`, then **Unpack** a different name (`qa_789_fail`).
   - [ ] The panel shows Python's own error ("can't open file …") and then
         "Unpacking failed at 'ngciso-tool.py' (exit 2)" — **not** "completed successfully".
   - [ ] The Unpack button is enabled again, so the run can be retried.
4. Rename it back.

## 4. Parentheses are ordinary characters (Phase 7.9)

5. Copy the unpacked project to `qa (test) 789` — or unpack straight into a name containing `(` and `)`.
   - [ ] Selecting an ISO whose path contains `(` no longer shows "The ISO path cannot contain parentheses."
   - [ ] Selecting a pack folder containing `(` is accepted with no dialog and no "rename a copy" offer.
   - [ ] Packing that project succeeds and produces `qa (test) 789.iso`.
   - [ ] The app does not complain at startup if its own folder contains parentheses.
   - [ ] The project tree lists the project and its `(randomized)`/`(incomplete)` labels work.

## 5. Script recompile path

6. Edit any `.scp` under `<project>_BEC/data/script`, then **Pack** with "Recompile scripts before packing" ticked.
   - [ ] "Compiling 1 changed file(s)" style lines appear, one per file, each routed through the same log.
   - [ ] A deliberately broken `.scp` (delete a `}`) reports `failed to compile (exit N)` and the pack still runs
         or stops as it did before — the count is honest either way (it used to count failures as successes).

## 6. Command chain, on request only

7. Options → tick **Save tool commands (.bat)** → pack once.
   - [ ] `ModdingGUI-pack-commands.bat` appears in the project folder, and the log says where.
   - [ ] The file is the chain that just ran (same six lines), and nothing executed it.
   - [ ] Untick, pack again: no new file.

## 7. CLI parity

8. From a shell: `ModdingGUI.exe --pack "<project>" --tools "<app>\tools"`.
   - [ ] It prints the same lines the GUI panel showed (same wording, same order) plus `[pack] ok`.
   - [ ] Exit code is 0 (`echo $LASTEXITCODE` / `%ERRORLEVEL%`). A broken toolbelt path gives 1 and a readable
         error, not a silent success.

## 8. Patch apply (xdelta3)

9. Patching tab → apply a known `.xdelta` to a vanilla ISO whose path contains a space and a parenthesis.
   - [ ] No "create a copy with parentheses replaced" dialog appears.
   - [ ] Patching succeeds and `Gladius_Patched.ISO` is produced next to the source.
   - [ ] Work from a shortcut / different working directory too: the exe is found in `tools/` beside the app, so
         it no longer depends on the current directory.

## Notes for the tester

- `%LOCALAPPDATA%\GladiusModding\last-run.log` holds every line of the last run, including the commands. Attach it
  to a bug report instead of retyping the panel.
- A cancelled step (closing the app mid-pack) is reported as exit `-1`; the log says `cancelled: <step>`.
