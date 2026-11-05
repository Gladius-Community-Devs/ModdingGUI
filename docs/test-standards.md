# Test Standards — Gladius Modding Tools

The scoring rubric and conventions for the xUnit suite in `ModdingGUI/ModdingGUI.Tests/`.
The `/test-add` opencode command reads this file when designing tests. If it is ever missing,
the command falls back to the rubric summary embedded in its template.

## Purpose

Tests exist to do three things, in priority order:
1. Protect byte-for-byte data integrity (round-trip fidelity).
2. Pin reverse-engineered game knowledge so it can never be silently lost.
3. Make every Core API's behavior executable documentation.

A test that does none of these is suspect.

## Scoring rubric (score every new test before writing it)

| Factor | Weight | Question |
|---|---|---|
| Project usefulness | 25 | Does it guard real product behavior or data integrity, or is it trivia? |
| Repeated usefulness | 20 | Needed going forward, or a one-off / test of code slated for deletion? |
| Expandability | 15 | Can new cases be added cheaply (data-driven, fixtures), or does scope change rewrite it? |
| Readability | 15 | Can a developer see what it protects in seconds? |
| Coverage depth | 15 | Happy path only, or edge cases, error paths, boundaries? |
| Maintainability | 10 | Isolated, fast, no brittle magic numbers, no silent skips. |
| **Total** | **100** | |

### What scores badly
- "Count > threshold" / "doesn't throw" as the *only* assertion on real data — fine as a smoke
  layer, not a substitute for value-level checks.
- Magic numbers with no provenance (e.g. `Assert.Equal(433, ...)` must say what 433 is and
  where it came from).
- Tests that write into the repo — stage in temp dirs instead.
- Tests gated behind `CleanBecFact`/`GameDataFact` that silently skip when their fixture is
  missing. If a gate matters, it should fail loudly, not go green by skipping.

## Established patterns (copy these; don't invent new ones)

| Situation | Pattern | Example |
|---|---|---|
| Pure logic, no I/O | `[Fact]`, in-memory inputs | `PathHelperTests`, `PackValidatorTests` |
| Depends on the vanilla corpus | `[CleanBecFact]` via `CleanBecCorpus.Root` (read-only) | `TokRoundTripTests`, `ItemsSchemaReadTests` |
| Needs a real built project + vanilla.iso | `[GameDataFact]` via `GameDataFixture` | `RealGameDataTests` |
| API / file-write behavior | Per-test temp project dir, stage the file, assert `.mod-history/*.bak` is created | `ItemsApiTests`, `LookupTextApiTests` |
| Format fidelity | Byte-equality round-trip: bytes → parse → mutate (or not) → write → byte-equal | `ItemsSchemaMutationTests` corpus revert |
| Many cases, one shape | `[Theory]` + `[InlineData]` | `PackValidatorTests` beast-class theories |

## Naming
- Class: `<Target>Tests`, namespace mirroring the code under test
  (`ModdingGUI.Tests.Core.Api.ItemsApiTests` ↔ `ModdingGUI.Core.Api.ItemsApi`).
- Method: `Method_Behavior_ExpectedResult` (e.g. `SetField_RewritesExistingLine`).

## Domain knowledge is a first-class test source
Reverse-engineered facts must be pinned as tests with the source documented in an XML doc
comment (or a brief comment where one is justified):
- `ModdingGUI/devnotes.txt` — UNITDB line rules (Discord-decoded), etc.
- `skills_field_reference.txt`, `skills/README.md`, `Gladius Effects and Statuses.txt` — skills domain knowledge
- `bugs.txt` — regression seeds
- `clean-bec-unpacked/` — ground-truth corpus (never edit, never write to). It sits outside the git
  repo deliberately and cannot be restored from version control. Read-only reference only: any test
  that mutates data must first copy the file(s) into a per-test temp project dir and operate there.

When modders report or discover an edge case, record the provenance in the test so the next
developer knows why the value is what it is.

## Phase discipline
Tests belong to phases (PLAN.md). A test whose target is outside the active phase should
normally wait until that phase starts. `/test-add` flags the mismatch prominently; the final
decision to add it early belongs to the developer. Do not pre-add coverage for formats the
code cannot yet mutate — the round-trip-first rule means the serializer gate lands *with* its
phase, not before it.

## Verification
- A new test must pass via `.\run-tests.ps1 -Filter <Class>` from `ModdingGUI/`.
- Where cheap, prove it FAILS when the behavior is removed (mutation check) — especially for
  data-integrity tests.
- Full suite must stay green at phase end: `.\run-tests.ps1 -Configuration Release`.
- Never commit test changes unless the user asks.
