# Manual Test Results — Baseline + Phase 2.6

> Fill this in as you run. Keep one line per check, and record the *observed* log line or file path for anything that isn't an obvious PASS.

## Run Metadata

| Field | Value |
|---|---|
| Date | |
| Tester | |
| Build source commit / branch | |
| `dotnet build ModdingGUI.sln` run? (Y/N + timestamp) | |
| Exe under test (full path) | `bin\Debug\net8.0-windows\win-x64\ModdingGUI.exe` |
| App directory (= exe dir, the anchor) | |
| Vanilla ISO (full path) | `E:\code_stuff\GladiusModdingToolsGUI\vanilla_gamecube.iso` |
| `clean-bec-unpacked` present? | |
| `tools\` next to exe? | |
| Python on PATH? (`python --version`) | |
| 2.6 setup route used | Option A (unpack) / Option B (REALDATA) |

## Environment snapshot (before starting)

| Item | Value |
|---|---|
| Contents of app dir (project folders listed) | |
| `REALDATA_vanilla` present next to exe? | |
| `REALDATA_vanilla` has `.mod-history`? (should be No) | |
| Scratch dir for before-copies (outside `bin\`) | |

---

## Result legend

- **PASS** — observed matches expected exactly
- **FAIL** — observed differs from expected (attach the log line / diff)
- **WARN** — works but with a caveat worth discussing
- **BLOCKED** — couldn't run (note why)
- **SKIP** — not applicable / intentionally skipped

---

## Baseline Results

| # | Test | Status | Observed (log lines, paths, diffs) | Notes |
|---|---|---|---|---|
| 1 | Launch, app dir, Info version, update check | | | |
| 2 | Project detection (+ `_ISO`-only folder skipped) | | | |
| 3 | Settings persist across restart; corrupt settings don't crash | | | |
| 4 | Unpack produces `_ISO` + `_BEC` + file list, no red errors | | | |
| 5 | Pack produces `.iso`; validation blocks then force-packs | | | |
| 6 | `.mod-history` backups created on Core-backed saves | | | |
| 7 | Skill Editor load/edit/save round-trips | | | |
| 8 | Grid Builder load/edit/save round-trips | | | |
| 9 | Randomizer runs, seeded reproducible | | | |
| 10 | Team Builder writes without crashing | | | |
| 11 | xDelta create/apply (optional) | | | |
| 12 | Error/edge cases behave cleanly | | | |

### Baseline extra notes / discrepancies

_(Anything that didn't fit above, incl. the parens-path result and which fixture folders you actually had next to the exe.)_

---

## Phase 2.6 Results

> Pre-test before-copies captured? (items.tok, lookuptext_eng.txt) — where:
> `.mod-history` cleared before counting backups? Y/N

| # | Check | Status | Observed | Notes |
|---|---|---|---|---|
| 1 | Load smoke test — dropdown populated (1,852), no log errors, mesh/material read-only | | | |
| 2 | Patch existing item persisted + 1 backup created | | | |
| 3 | Clear affinity removed the `ITEMAFFINITY` line | | | |
| 4 | Display name edit updated dropdown + `lookuptext_eng.txt` | | | |
| 5 | Description edit updated existing lookup row | | | |
| 6 | Add-missing-description appended row + added `ITEMDESCRIPTIONID` | | | |
| 7 | Add item (incl. duplicate-name + bad-statmod negative checks) | | | |
| 8 | Rename item (incl. duplicate-name rejection) | | | |
| 9 | Remove item | | | |
| 10 | Backup counts match saves; `.bak` contents correct | | | |
| 11 | Lookup-text failure case reports an error (no silent success) | | | |
| 12 | Round-trip diff shows only intended changes | | | |
| 13 | Restart sanity passed; test data reset if needed | | | |

### Backup-count tally

| Action | Expected `.bak` count | Actual count | Path |
|---|---|---|---|
| Patch existing item | 1 (items.tok) | | |
| Clear affinity | 1 (items.tok) | | |
| Display name edit | 1 (lookuptext) | | |
| Description edit | 1 (lookuptext) | | |
| Add missing description | 2 (lookuptext + items.tok) | | |
| Add item | 1 (items.tok) [+1 lookuptext if name/desc] | | |
| Rename item | 1 (items.tok) | | |
| Remove item | 1 (items.tok) | | |

### Diff commands used (record for reproducibility)

```
# byte-identical check
fc /b <before> <after>
# or hash
(Get-FileHash -LiteralPath <before>).Hash -eq (Get-FileHash -LiteralPath <after>).Hash
```

### Phase 2.6 extra notes / discrepancies

_(Doc/UI mismatches — e.g. Add-item stat-mod input format, clear-description gap, anything unexpected.)_

---

## Final disposition

| Question | Answer |
|---|---|
| Baseline pass/fail | |
| Phase 2.6 pass/fail | |
| `setup-realdata.ps1` needed afterward? (if REALDATA was dirtied) | |
| Open follow-ups for us to analyze | |
