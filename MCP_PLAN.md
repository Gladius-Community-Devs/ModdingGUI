# MCP Server — Status & Plan

_Status snapshot taken this session, from the working tree at `rewrite/full-app` (2 commits ahead of origin, clean)._

## 1. Status: the MCP server is a 46-line stub

| Piece | State | Evidence |
|---|---|---|
| `ModdingGUI.Mcp` project | Exists, `net8.0`, references Core, builds | `ModdingGUI/ModdingGUI.Mcp/ModdingGUI.Mcp.csproj` |
| MCP entrypoint | Prints "scaffolding-only build… Stdio server not yet implemented (Phase 8)" and **returns 0** | `ModdingGUI/ModdingGUI.Mcp/Program.cs:28-45` |
| JSON-RPC / stdio transport | None | no other source files in the project |
| Tool registry / tools | None | — |
| MCP tests | None | `ModdingGUI.Tests/` has no MCP folder |
| GUI integration (`--mcp`, settings checkbox) | None; `AppSettings` has no MCP field | `ModdingGUI/Models/AppSettings.cs` |
| Client config docs / snippets | None | — |

**What does exist underneath it** — three Core APIs, all locked (`ProjectLock`), backup-before-write (`ChangeHistoryService`), and tested:

- `IItemsApi` — `List / GetAll / Get / Patch / Add / Remove / Rename` on `data/config/items.tok` (`ModdingGUI.Core/Api/IItemsApi.cs`)
- `ISkillsApi` — `List / GetAll / Get / Update / Add / Remove`, raw-line freeform, no typed schema by design (`ModdingGUI.Core/Api/ISkillsApi.cs`)
- `ILookupTextApi` — `Load / Update / Add` on `data/config/lookuptext_eng.txt` (`ModdingGUI.Core/Api/ILookupTextApi.cs`)

So the *shape* of the MCP layer is already determined — it is a thin adapter over these. What's missing is the protocol, the tool surface, and roughly six phases of APIs that Phase 8 is sequenced behind.

## 2. Roadmap drift (independent of MCP)

`PLAN.md` §2 and the Active Phase table in `docs/AGENTS.md` both say **Phase 2.6 is "next"**. The code says otherwise:

- Phase 2.6 ("Items GUI on `IItemsApi`") is **implemented**: `frmMain_partials/ItemEditor.cs` uses `IItemsApi` + `ILookupTextApi` for load/save/rename/delete, and the legacy `ToTokFormatString` write path is gone (only a comment mentions it).
- Phase 2.7 ("Skills catalog-driven") is **half done**: `SkillEditor/SkillEditorView.cs` is a catalog-driven editor over `ISkillsApi`, but the catalog model still lives *inside the view* (`private sealed class SkillsCatalogRoot`, line 46) instead of Core, there is no `ISkillsApi.Validate(...)`, no `SkillValidationMode`, and no `SkillsContext` dynamic resolvers.
- Phase 6 bypass is still live: `frmMain_partials/Randomizer.cs:934` and `:1551` write game files directly with `File.WriteAllText`. That is the last big rule-1 violation.

Two consequences: (a) the docs need re-syncing before we plan anything else, and (b) "the next thing" is *not* MCP — it is closing 2.6 and finishing 2.7.

## 3. Decisions locked in this session

1. **Protocol implementation:** official **`ModelContextProtocol` C# SDK, 2.3.0** (Apache-2.0, `net8.0` TFM). This is an explicit approval to add a NuGet dependency despite PLAN rule 8. It pulls `ModelContextProtocol.Core` and `Microsoft.Extensions.{Caching,Hosting}.Abstractions 10.0.13`; we will also add `Microsoft.Extensions.Hosting` for `Host.CreateApplicationBuilder`, per the SDK's own getting-started guidance.
2. **Sequence unchanged:** MCP stays **Phase 8**, after 2.6 → 2.7 → 3 → 4 → 5 → 6 → 7 → 7.5. No out-of-sequence start.
3. **Shipping:** a separate **`ModdingGUI.Mcp.exe`** next to the GUI exe (not a `--mcp` mode inside the WinForms binary). MCP clients launch their own subprocess, so this is the idiomatic shape — and it keeps WinForms out of the server process.
4. Therefore: **next unit of work = Phase 2.6 close-out**, then 2.7.

## 4. The road to Phase 8, and what each phase owes MCP

| Phase | Work | What Phase 8 gets from it | Rough effort |
|---|---|---|---|
| **2.6** | Close out: run full suite, walk exit criteria, re-sync PLAN.md/AGENTS.md status | Nothing new — but it validates the `open → mutate → write` API pattern that every tool will reuse | hours |
| **2.7** | Move catalog into Core (`SkillsCatalog`), `ISkillsApi.Validate` + `SkillValidationMode`, `SkillsContext` resolvers (meter ids, mesh→`.pan`) | `skills_validate` + `skills_field_reference` tools; makes `strict: true` mean something for skills | ✅ **done** (catalog v2, `Validate`, `Warn`/`Reject` overloads, `SkillsContext.MeterIds`/`LookupText`). Deferred: animation resolver (`AnimationsForMeshes`) → Phase 3 |
| **3** | `ILookupApi`, `IClassesApi`, delete/delegate `LookupHelper` | Real lookup tool family (today's is a narrow bridge), `classes_*` tools, and **human-readable names in tool output** everywhere | 2–3 days |
| **4** | `IEncountersApi`, `ILeaguesApi`, `ITownsApi`, `ISchoolsApi`, config tail | 4 new tool families | 4–6 days |
| **5** | `DelimitedRecordSchema<T>` + `IGladiatorsApi`, `IStatSetsApi`, `IItemSetsApi`, `ISkillSetsApi` | Units tool family — required for anything that resembles "make me a mod" | 3–4 days |
| **6** | Randomizer pipeline into `ModdingGUI.Core.Randomizer`, seeded + reviewable | `randomizer_apply` — the single highest-leverage AI tool, and the reason it must be pure/seeded first | 4–6 days |
| **7** | Team Builder on real APIs | No Phase 8 tools (GUI feature) | 3–5 days |
| **7.5** | `ChangeHistoryService.ListBackups / RestoreBackup` + per-Api wrappers | `history_list` / `history_restore` — **the undo story for AI edits; must land before any write tool ships** | ~1 day |
| **8** | The server itself | — | 3–5 days (with the SDK) |

Rough total to "an AI can edit my project": **3–5 weeks of focused sessions**, dominated by Phases 4–6.

## 5. Phase 8 design (decided now so implementation is mechanical)

### 5.1 Shape

```
ModdingGUI.Mcp/
  Program.cs              Host.CreateApplicationBuilder → AddMcpServer().WithStdioServerTransport()
  McpOptions.cs           --project, --read-only, --strict-default, --allow-outside-project
  Tools/ProjectTools.cs   project_list, project_info, project_resolve
  Tools/ItemsTools.cs     items_*
  Tools/SkillsTools.cs    skills_*
  Tools/LookupTools.cs    lookup_*
  Tools/ClassesTools.cs   classes_*      (Phase 3)
  Tools/HistoryTools.cs   history_*
  Wire/                   MCP-facing DTO records + converters (see 5.5)
  README.md               client config snippets
```

- `[McpServerToolType]` classes, `[McpServerTool]` methods. Parameter types drive the auto-generated JSON Schema, so **tool parameters are purpose-built records**, not reuse of `ItemDto` where that leaks `ValueTuple` (see 5.5).
- **All logging to stderr.** `builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace)`. stdout belongs to the protocol; a stray `Console.WriteLine` corrupts the session.
- Tools are grouped per Api and hold `IItemsApi` etc. via constructor injection; Core is stateless per call, so no per-tool state.
- Exit code 0 on clean shutdown, 2 on bad args. Cancellation token threaded into every call.

### 5.2 Project resolution (needs a Core addition first)

The APIs take a **BEC-root path** (they append `data/config/...`), while the GUI's notion of a project is the folder containing `<Name>_ISO` and `<Name>_BEC` (`ModdingGUI/Helpers/ProjectPaths.cs`, currently `internal` and shell-only).

- Add `ModdingGUI.Core.Projects.ProjectPaths` (public) + `ProjectResolver.TryResolve(projectRootOrBec, out becRoot)`; **the shell delegates to it** (rule 5, no reimplementation).
- Every tool accepts `projectPath` = project root; the adapter resolves to BEC root centrally, so no tool does path math.
- Optional server-level default: `--project <path>` or `MODDINGGUI_PROJECT` env var; per-call argument wins.
- Guard: reject paths that don't resolve to a directory containing a `_BEC` sibling (or `data/config`), and refuse to write outside it. This is the sandbox that keeps a hallucinating model honest.

### 5.3 Tool surface (v1 = ~26 tools; grows one family per later phase)

| Family | Tools |
|---|---|
| project | `project_list`, `project_info` (files present, record counts, last backup), `project_validate` (once per-Api validators exist) |
| items | `items_list`, `items_search`, `items_get`, `items_schema` (field reference: type, observed range, vanilla examples), `items_patch`, `items_add`, `items_remove`, `items_rename` |
| skills | `skills_list`, `skills_search`, `skills_get`, `skills_field_reference`, `skills_update`, `skills_add`, `skills_remove`, `skills_validate` (2.7+) |
| lookup | `lookup_get` (by id, batch), `lookup_search`, `lookup_update`, `lookup_add` |
| history | `history_list`, `history_restore` (7.5+) |

Deliberately **not** exposed: `GetAll` for items/skills (1,852 / 1,811 records — megabytes of JSON). Bulk reads go through `search` with paging.

Web-research / no-network stance is unchanged: stdio only, no HTTP, no auth beyond OS process trust.

### 5.4 Per-tool contract

- Read tools: `(projectPath, ...)`. Write tools: `(projectPath, ..., strict = false)` — `strict` maps to `RejectOnError` where validators exist, `WarnInLog`/`None` otherwise, per PLAN rule 7.
- Result content: one `text` block of compact JSON (`System.Text.Json`, camelCase, `WriteIndented=false`), plus human-readable summary line. Errors use `isError: true` with stable codes — `NotFound`, `InvalidArgument`, `ProjectNotFound`, `FileMissing`, `ValidationFailed`, `Locked`, `NotSupported` — a message and a `hint` field. **Never** a stack trace; full detail to stderr.
- Every successful write returns `{ changed: true, backupPath: "...", record: {...} }` so the model (and the user) can see exactly what happened and undo it.
- Optional `dryRun` on mutating tools: parse + apply in memory, report the diff, write nothing.

### 5.5 JSON gotcha to fix (real, found in this audit)

`ItemDto` exposes `(string Element, long Amount)? Affinity` and `List<(string,long)> StatMods` (`ModdingGUI.Core/Api/ItemDto.cs:30-45`). `System.Text.Json` does **not** serialize `ValueTuple` — fields aren't serialized, so those become `{}`. Two options; recommend the first:

1. `Wire/` records in the MCP project: `Affinity { element, amount }`, `StatMod { stat, value }`, mapped to/from the Core DTOs. Keeps Core clean, keeps schemas readable in `tools/list`.
2. Add `JsonConverter`s in Core. Avoid — it changes Core's wire contract for a consumer-specific reason.

`SkillDto` is fine (`Fields: [{fieldName, rawValue}]`).

### 5.6 Context budget rules (what makes this usable vs. unusable)

- `*_list` → names only (+ count); `*_search` → `{name, oneLineSummary}`, default `limit: 25`, max `200`, explicit `offset`; `*_get` → one full record.
- No tool output exceeds ~64 KB; truncate with `"truncated": true, "nextOffset": n`.
- `tools/list` stays under ~8 k tokens total (with ~26 tools that's fine; keep descriptions terse and put detail in `items_schema` / `skills_field_reference`).

### 5.7 Safety & concurrency

- `--read-only` mode registers only read tools (a "let the AI look, not touch" switch for new users).
- `--allow-outside-project` off by default; a project-root allowlist guards traversal.
- Backup on every write already comes free from the Core APIs — "backups always" (rule 2) is inherited, not re-implemented.
- **Known limitation to document, not fix:** `ProjectLock` is in-process only (`ModdingGUI.Core/ProjectLock.cs:22-26`). If the GUI is open on the same project while the MCP server writes, last-write-wins. Document "close the editor tab before letting the AI edit" and let `history_list` show the collision.

### 5.8 Tests (the bar for Phase 8)

In-process, no client needed — the SDK's server can be driven over a `Stream`/pipe:

1. `Tools/RegistryTests` — `tools/list` snapshot: names, descriptions, JSON Schema hash. Fails loudly when a tool signature drifts.
2. `Tools/*ToolsTests` — per-tool CRUD against temp project dirs staged from `clean-bec-unpacked` (same pattern as `ItemsApiTests`): read, patch, add, remove, rename, strict/reject, error codes, `dryRun` writes nothing, backup exists after every write.
3. `ProtocolTests` — `initialize` (version negotiation) → `notifications/initialized` → `tools/list` → `tools/call`, plus unknown-method and malformed-JSON handling.
4. `SmokeTests` — spawn the **built exe**, same 4-step transcript, assert clean stdout/stderr separation and exit code.
5. `ModdingGUI.Mcp/smoke.ps1` — the same transcript as a one-command manual check on a real project.
6. Corpus gate (`TokRoundTripTests`) stays green: MCP must never touch bytes directly.

Target: ~30–40 new tests, full suite green at 2xx/0/0 with `REALDATA_vanilla` present.

### 5.9 Packaging & client config

- Publish `ModdingGUI.Mcp.csproj` self-contained single-file win-x64 (the GUI already ships self-contained; users shouldn't need a .NET runtime).
- Release layout: `ModdingGUI.exe`, `ModdingGUI.Mcp.exe`, `skills/skills_catalog.json`, `tools/`, `mcp/README.md` + example configs.
- Ship real snippets for: Claude Desktop (`claude_desktop_config.json`), OpenCode (`opencode.jsonc` → `mcp` block), Claude Code (`claude mcp add`). Args shown as `["--project", "C:\\path\\to\\MyMod"]`.
- `mcp/README.md` also states the trust model (stdio, local process, writes are backed up) and the GUI-open caveat.

### 5.10 Staged delivery inside Phase 8

| Slice | Content | Exit |
|---|---|---|
| 8.0 | Project skeleton, SDK wired, stderr logging, `project_list`/`project_info`, in-process protocol tests, smoke script | A real MCP client lists tools and calls `project_info` |
| 8.1 | `items_*` family + `items_schema` + wire DTOs + tool tests | Items fully editable by an AI; every write leaves a `.bak` |
| 8.2 | `skills_*` + `lookup_*` families | Skills/lookup editable with catalog-guided fields |
| 8.3 | `history_list`/`history_restore`, `--read-only`, `dryRun`, error-code polish | AI edits fully reviewable and undoable |
| 8.4 | Packaging, client configs, README, release zip changes | Shipped in a release; one-command install documented |

Phases 3–7 later add `classes_*`, `encounters_*`, `leagues_*`, `towns_*`, `schools_*`, `gladiators_*`, `statsets_*`, `itemsets_*`, `skillsets_*`, `randomizer_*` — one tool class each, ~30–60 lines, no protocol work.

## 5.9 What MCP must not touch

Application settings are **not** exposed as tools, by design, and the boundary is enforced by tests
(`ModdingGUI.Tests/Architecture/McpBoundaryTests.cs`):

| Guard | Why |
|---|---|
| `ModdingGUI.Mcp` may reference **only** `ModdingGUI.Core` | `AppSettings` lives in the shell; without the reference no tool can reach it |
| No `AppSettings`, `settings.json`, Dolphin path, projects folder or auto-launch mentions in the MCP project | the intent is checked, not just the reference |
| `AppSettings` must not move into Core | a tripwire for the one refactor that would quietly open the door |

Reasons to keep it closed:

1. **Settings are machine-local, not part of the mod.** A Dolphin executable path or a projects folder says nothing
   about the game data a client is editing.
2. **They are outside the undo story.** Every write tool ships with `.mod-history` backups; settings changes have no
   history, so an AI edit there would be unrecoverable - against the rule that write tools wait for `history_restore`
   (Phase 7.5).
3. **They change where the tool looks and what it launches.** Moving the projects folder or pointing at a different
   emulator is a decision for the person at the keyboard.
4. **The MCP server is opt-in per process.** `EnableMcpServer` in `AppSettings` gates it; a client that can write
   settings could switch its own transport on.

Read-only context is fine, and is planned as `project_list` / `project_info` output (version, project layout, whether
a randomizer baseline is pinned). That is information about the project, not configuration of the application.

## 6. Verification status from this session

- **Could not run the build or tests here.** `dotnet` in this sandbox can't reach nuget.org (`NU1301`, TLS/"No credentials are available in the security package" — the machine's proxy needs Windows SSO, which a sandboxed child process doesn't get in the same way). `DOTNET_CLI_HOME` also had to be redirected to a writable folder.
- Side effect to be aware of: my restore attempt rewrote `ModdingGUI/obj/project.assets.json` (now ~6 KB, restore-failed). **Run `dotnet restore ModdingGUI.sln` once on your machine** before the next build; if it still complains, `git status` is clean so nothing tracked was touched.
- Last recorded test evidence is `ModdingGUI/TestResults/TestResults.trx` = **16/16 passed**, but that was a *filtered* `RealGameDataTests` run (dated 8/11), not the full suite.
- Real-data fixtures **are** present (`bin/{Debug,Release}/net8.0-windows/win-x64/REALDATA_vanilla` + `vanilla.iso`), so the full 216/0/0 bar is reachable: `.\run-tests.ps1 -Configuration Release`.
- git: branch `rewrite/full-app`, clean tree, **2 commits unpushed** (`0dffb0e`, `3da3d7d`).

## 7. Next sessions

1. **Now — Phase 2.6 close-out** (needs your machine for the build/test run): `dotnet restore` → `.\run-tests.ps1 -Configuration Release` → walk the 2.6 exit criteria → update `PLAN.md` §2 and the Active Phase table in `docs/AGENTS.md` (2.6 done, 2.7 next). Then the manual checklist in `MANUAL_TESTING_PHASE_2_6.md` if you want it certified, not just code-complete.
2. **Phase 2.7 — Core skills catalog**: `SkillsCatalog` + `SkillsContext` + `ISkillsApi.Validate`/`SkillValidationMode`, view reduced to a consumer, ≥8 new Core tests, `skills.tok` round-trip test.
3. **Phase 3 — lookup + classes.** This is the first phase whose output makes MCP tool results *readable* (names instead of raw ids), so it's the real inflection point for AI usability.

## 8. Open questions to settle before Phase 8 (none block starting 2.6)

1. **PLAN §2.7 open question (already flagged there):** how does a non-developer regenerate `skills_catalog.json` when their `skills.tok` has new values — GUI button shelling out to `build_catalog.py`, a double-clickable `.bat`, or dev-only with stale dropdowns? Needs an answer *before* 2.7 starts.
2. **Phase 6 `randomizer_apply` over MCP**: do you want a mandatory diff-preview step (`dryRun` first, explicit apply second) rather than a one-shot apply? I recommend mandatory preview for that tool only.
3. **Tool naming**: PLAN says `items_*`. Keep that (snake_case, one prefix per family) — confirm, since tool names are user-visible in every client.
4. **Optional out-of-sequence spike** (only if you want early confidence): a 2–3 hour throwaway `ModelContextProtocol` + one-tool proof-of-concept against a staged project, to prove the SDK/stdio handshake works with your client *before* six phases of API work. It violates PLAN rule 10 unless we call it approved exploration; my recommendation is to skip it — slice 8.0 is already small and the SDK is battle-tested.
