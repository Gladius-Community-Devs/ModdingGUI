# PRD: {{Title}}

**Date:** {{YYYY-MM-DD}} \
**Status:** Draft / Review / Final \
**Version:** v0.1 \
**Author:** {{Name}}
**Phase(s):** {{PLAN.md phase(s) this belongs to}}

---

## Problem Statement

(2-3 sentences describing the user problem or feature gap. Focus on what the user currently can't do or what pain point this solves. No more than 25 words.)

---

## Proposed Solution

(A concise description of what the feature does from the user's perspective. What changes in the UI or workflow? 2-4 sentences.)

---

## User Impact

(Who benefits and how. Quantify where possible: "Removes 3 manual steps from item editing," "Covers all 1,852 records in items.tok," etc.)

---

## Scope

### In Scope

(List 3-7 specific deliverables. Be concrete.)

- {{deliverable}}

### Out of Scope

(Explicitly list exclusions to prevent scope creep. Especially note any format/schema that is explicitly out of the current PLAN.md phase.)

- {{exclusion}}

---

## User Flow

(Describe the end-to-end interaction in 3-6 steps. Focus on what the user sees and clicks, not implementation details.)

1. {{step}}

---

## Requirements

| ID | Requirement | Priority |
|---|---|---|
| FR-1 | {{What the system must do}} | Must / Should / Could |

---

## UX Notes

(Specific UI guidance: where in the window, what controls, default states, keyboard shortcuts, error messages. Link to mockups if applicable.)

---

## Data Considerations

(New or modified data structures, `.tok`/`.enc`/`.txt`/`.grd` format changes, catalog changes, config keys, or save file parsing. This project REQUIRES: lock + backup + byte-for-byte round-trip. State explicitly how each is satisfied.)

---

## Architecture Compliance

(How this feature upholds the locked rules from PLAN.md. Check each one that applies:)
- [ ] No GUI bypass — all edits through a Core API
- [ ] Backups always — via `ChangeHistoryService`
- [ ] Locks always — via `ProjectLock`
- [ ] Round-trip first — new format has a round-trip test green before mutations
- [ ] `System.Text.Json` only; ISO-8859-1 tok I/O
- [ ] No new dependencies (or approval attached)
- [ ] Reuses existing helpers (`ProjectPaths`, `PathHelper`, etc.)
- [ ] `dotnet test -c Release` green at phase end

---

## Dependencies

(List blocking dependencies only. Reference PLAN.md phases: e.g., "Requires ILookupApi (Phase 3) to be done first".)

- {{dependency}}

---

## Open Questions

(Anything undecided that needs input before implementation begins.)

- {{question}}

---

## Appendix — Implementation Notes

(Optional. Technical approaches considered, risk areas, known constraints discovered during scoping. For new `.tok` formats: reference the `RecordSchema<T>`/`DelimitedRecordSchema<T>` approach and the GridBuilder pattern as the template.)
