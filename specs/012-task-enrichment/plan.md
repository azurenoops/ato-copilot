# Implementation Plan: AI-Powered Task Enrichment — Remediation Scripts & Validation Criteria

**Branch**: `012-task-enrichment` | **Date**: 2026-02-25 | **Spec**: [spec.md](spec.md)  
**Input**: Feature specification from `/specs/012-task-enrichment/spec.md`

## Summary

Bridge the gap between fully implemented AI remediation services (`IAiRemediationPlanGenerator`, `AtoRemediationEngine`) and the Kanban task pipeline. Currently, no scanner populates `ComplianceFinding.RemediationScript`, so `CreateTaskFromFinding()` copies null to every task. This feature introduces `ITaskEnrichmentService` — a synchronous enrichment layer that calls `IRemediationEngine.GenerateRemediationScriptAsync()` (AI-first with NIST-template fallback) during board creation, board updates, on-demand tool invocation, and lazy task-detail views. Two new MCP tools (`kanban_generate_script`, `kanban_generate_validation`) expose this capability to users.

## Technical Context

**Language/Version**: C# 13 / .NET 9.0  
**Primary Dependencies**: `IRemediationEngine` (AI → NIST fallback), `IAiRemediationPlanGenerator` (Azure OpenAI via `IChatClient`), `IScriptSanitizationService`, `INistRemediationStepsService`, `IKanbanService`, EF Core 9  
**Storage**: SQL Server 2022 via EF Core (existing `RemediationTasks` table; `RemediationScript` varchar(8000) and `ValidationCriteria` varchar(2000) columns already exist; new `RemediationScriptType` varchar(20) column added)  
**Testing**: xUnit 2.9 + FluentAssertions + Moq | Baseline: 2311 unit tests passing  
**Target Platform**: Linux containers (Docker Compose), Azure Government  
**Project Type**: Multi-project .NET solution (Core lib / Agents service lib / MCP server / State lib)  
**Performance Goals**: Board enrichment for 30 tasks within 60 seconds; individual task enrichment <5s; lazy enrichment on `kanban_get_task` <5s first-time, zero overhead thereafter  
**Constraints**: Azure OpenAI rate limits (TPM/RPM) — bounded concurrency via `SemaphoreSlim(5)`. Synchronous enrichment blocks board creation. Immediate fallback to NIST templates on any AI failure (no retry). `AgentAIEnabled` as global kill switch.  
**Scale/Scope**: ~30–100 tasks per board, ~5–10 boards per subscription, single-subscription scope per assessment

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| # | Principle | Status | Evidence |
|---|-----------|--------|----------|
| I | Documentation as Source of Truth | **PASS** | Feature spec at `specs/012-task-enrichment/spec.md` with full technical design, 5 user stories, 5 clarifications. Plan generates `research.md`, `data-model.md`, `quickstart.md`, `contracts/`. System prompt updated for new tools. |
| II | BaseAgent/BaseTool Architecture | **PASS** | Two new tools (`KanbanGenerateScriptTool`, `KanbanGenerateValidationTool`) extend `BaseTool` with `Name`, `Description`, `Parameters`, `ExecuteAsync()`. Registered via `RegisterTool()` in `ComplianceAgent` constructor. System prompt externalized in `ComplianceAgent.prompt.txt`. |
| III | Testing Standards | **PASS** | Spec defines 17+ test cases across 3 test files. Each public method has positive + negative tests. Boundary tests: empty/null scripts, force flag, Informational severity skip, AI failure fallback. xUnit + FluentAssertions + Moq. Estimated 40+ new unit tests. |
| IV | Azure Government & Compliance First | **PASS** | Uses existing `IChatClient` singleton targeting `https://mcp-ai.openai.azure.us/` (Azure Gov endpoint). No new Azure client creation. Credentials via environment variables (existing `.env` pattern). `DefaultAzureCredential` chain unchanged. |
| V | Observability & Structured Logging | **PASS** | `TaskEnrichmentService` logs: enrichment start/complete, AI vs. template method used, per-task duration, failure details. Board-level enrichment logs aggregate metrics (enriched/skipped/failed counts). ILogger<T> injected. |
| VI | Code Quality & Maintainability | **PASS** | Single-responsibility: `TaskEnrichmentService` handles enrichment only; delegates script generation to `IRemediationEngine`, validation criteria to `IAiRemediationPlanGenerator`. All dependencies via constructor injection. XML docs on all public types. No magic values — constants for fallback messages. |
| VII | User Experience Consistency | **PASS** | New tool responses follow existing MCP envelope: `status`, `data`, `metadata` (execution time, tool name). Error responses include `message`, `errorCode`, `suggestion`. Progress feedback via `IProgress<string>` for board enrichment (>2s operations). |
| VIII | Performance Requirements | **PASS** | Individual enrichment <5s (tool response time limit). Board enrichment 30 tasks at `SemaphoreSlim(5)` concurrency = ~6 batches × 5s = 30s (within 60s target). Per-task timeout (30s) prevents single-call blocking. All async methods accept `CancellationToken`. Memory: no bulk loading — tasks enriched one at a time. |

**Gate Result**: PASS — All 8 principles satisfied. No violations to justify.

## Project Structure

### Documentation (this feature)

```text
specs/012-task-enrichment/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
│   └── mcp-tools.md     # New tool schemas (kanban_generate_script, kanban_generate_validation)
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
src/
├── Ato.Copilot.Core/
│   ├── Interfaces/
│   │   └── Kanban/
│   │       └── ITaskEnrichmentService.cs        # NEW — enrichment interface
│   ├── Models/
│   │   └── Kanban/
│   │       └── KanbanModels.cs                  # MODIFIED — add TaskEnrichmentResult,
│   │                                            #   BoardEnrichmentResult, RemediationScriptType property
│   └── Data/
│       └── Context/
│           └── AtoCopilotContext.cs              # MODIFIED — add RemediationScriptType column config
│
├── Ato.Copilot.Agents/
│   ├── Compliance/
│   │   ├── Agents/
│   │   │   └── ComplianceAgent.cs               # MODIFIED — register 2 new tools via RegisterTool()
│   │   ├── Services/
│   │   │   ├── KanbanService.cs                 # MODIFIED — inject ITaskEnrichmentService?,
│   │   │   │                                    #   call EnrichBoardTasksAsync after board creation
│   │   │   └── TaskEnrichmentService.cs         # NEW — enrichment implementation
│   │   ├── Tools/
│   │   │   └── KanbanTools.cs                   # MODIFIED — add KanbanGenerateScriptTool,
│   │   │                                        #   KanbanGenerateValidationTool, lazy enrichment in GetTask
│   │   └── Prompts/
│   │       └── ComplianceAgent.prompt.txt       # MODIFIED — add 2 new tool descriptions
│   └── Extensions/
│       └── ServiceCollectionExtensions.cs        # MODIFIED — register ITaskEnrichmentService + 2 tools

tests/
└── Ato.Copilot.Tests.Unit/
    ├── Services/
    │   └── TaskEnrichmentServiceTests.cs         # NEW — 12+ enrichment service tests
    └── Tools/
        ├── KanbanGenerateScriptToolTests.cs      # NEW — on-demand script generation tests
        └── KanbanGenerateValidationToolTests.cs  # NEW — on-demand validation generation tests
```

**Structure Decision**: Follows the established multi-project pattern — interface in `Core`, implementation in `Agents`, tools alongside existing Kanban tools, tests in `Tests.Unit`. No new projects needed.

## Complexity Tracking

> No constitution violations. No complexity justifications required.

## Post-Design Constitution Re-Check

*Re-evaluated after Phase 1 design artifacts (data-model.md, contracts/mcp-tools.md, quickstart.md) finalized.*

| # | Principle | Status | Post-Design Notes |
|---|-----------|--------|-------------------|
| I | Documentation | **PASS** | All artifacts generated: research.md (9 decisions), data-model.md (entities + state transitions), contracts/mcp-tools.md (2 tool schemas + lazy enrichment behavior), quickstart.md (verification guide). |
| II | BaseTool Architecture | **PASS** | Tool schemas defined in contracts. Both tools follow `BaseTool.ExecuteAsync(params, serviceProvider, ct)` pattern. Scoped `ITaskEnrichmentService` resolved from `serviceProvider` parameter. |
| III | Testing Standards | **PASS** | Data model documents validation rules. Contracts document error envelopes with error codes. Test file inventory: 3 new test files, ~40+ tests targeting all acceptance scenarios. |
| IV | Azure Government | **PASS** | No new Azure service clients. Existing `IChatClient` singleton unchanged. Environment variable configuration unchanged. |
| V | Observability | **PASS** | Research R5 confirms `ILogger<TaskEnrichmentService>` injected. Board enrichment logs aggregate metrics. Per-task generation method (AI/Template) logged. |
| VI | Code Quality | **PASS** | `ITaskEnrichmentService` interface has 3 methods, each <50 lines. Delegation model: enrichment → `IRemediationEngine`, validation → `IChatClient` or template. XML docs on all public types per data-model.md. |
| VII | UX Consistency | **PASS** | Tool contracts define response envelopes with `status`/`data`/`metadata`. Error responses include `errorCode` + `suggestion`. New `remediationScriptType` field added to `kanban_get_task` response. |
| VIII | Performance | **PASS** | Research R7 confirms `SemaphoreSlim(5)` concurrency. Per-task 30s timeout documented in research. Board enrichment worst-case 30s for 30 tasks (within 60s target). |

**Post-Design Gate Result**: PASS — Design artifacts align with all 8 constitution principles.

## Generated Artifacts

| Artifact | Path | Status |
|----------|------|--------|
| Implementation Plan | `specs/012-task-enrichment/plan.md` | ✅ Complete |
| Research | `specs/012-task-enrichment/research.md` | ✅ Complete (9 decisions) |
| Data Model | `specs/012-task-enrichment/data-model.md` | ✅ Complete |
| Contracts | `specs/012-task-enrichment/contracts/mcp-tools.md` | ✅ Complete |
| Quickstart | `specs/012-task-enrichment/quickstart.md` | ✅ Complete |
| Agent Context | `.github/agents/copilot-instructions.md` | ✅ Updated |

**Next Step**: Run `/speckit.tasks` to generate the Phase 2 task breakdown.
