# Implementation Plan: Feature 018 — SAP Generation

**Branch**: `018-sap-generation` | **Date**: 2026-03-04 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/018-sap-generation/spec.md`

## Summary

Implement Security Assessment Plan (SAP) generation — the mandatory RMF Step 4 deliverable — as a document assembly pipeline that combines existing system data (control baselines, OSCAL assessment objectives, inheritance designations, STIG benchmarks, evidence records) with SCA-provided inputs (schedule, team, scope notes, method overrides) to produce a structured SAP in Markdown, DOCX, and PDF formats. The feature adds 3 new entities, 1 new service (`ISapService`/`SapService`), and 5 new MCP tools following established `BaseTool` patterns.

## Technical Context

**Language/Version**: C# 13 / .NET 9.0 (`net9.0`)
**Primary Dependencies**: EF Core 9.0, QuestPDF 2024.12.3, System.Text.Json 9.0.5, Serilog 4.2.0, ClosedXML 0.104.2
**Storage**: EF Core InMemory (dev/test), SQLite/SQL Server (prod); `EnsureCreatedAsync()` — no migrations
**Testing**: xUnit 2.9.3, FluentAssertions 7.0.0, Moq 4.20.72, EF Core InMemory for data tests
**Target Platform**: .NET 9.0 on Linux/macOS/Windows; Azure Government deployment
**Project Type**: MCP server (stdio + HTTP) with multi-agent compliance tooling
**Performance Goals**: SAP generation < 15s for Moderate baseline (~325 controls), < 30s for High baseline (~421 controls)
**Constraints**: Memory < 512MB steady-state; all async methods accept `CancellationToken`; MCP tool responses follow envelope schema `{ status, data, metadata }`
**Scale/Scope**: ~130+ existing MCP tools, 40+ EF Core entities, 3,356 existing unit tests; this feature adds 5 tools, 3 entities, ~135+ tests

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| # | Principle | Status | Evidence |
|---|-----------|--------|----------|
| I | Documentation as Source of Truth | PASS | Spec follows `/docs/` conventions; guidance documents maintained alongside implementation |
| II | BaseAgent/BaseTool Architecture | PASS | All 5 tools extend `BaseTool` with `Name`/`Description`/`Parameters`/`ExecuteCoreAsync`. No new agents needed — tools register on existing `ComplianceAgent` |
| III | Testing Standards | PASS | ~135+ tests planned across 6 phases; every public method has positive + negative cases; boundary tests for empty baselines, null inputs, max-length strings |
| IV | Azure Government & Compliance First | PASS | No new Azure interactions; SAP is a compliance document (NIST 800-37 RMF Step 4); data stays in existing EF Core context |
| V | Observability & Structured Logging | PASS | Structured logging in Phase 6 (T050): generation started/completed, update, finalization, validation |
| VI | Code Quality & Maintainability | PASS | SRP via assembly pipeline stages; XML docs on all public types; no magic values (methods enumerated as constants) |
| VII | User Experience Consistency | PASS | Tool responses follow `{ status, data, metadata }` envelope; actionable errors with `errorCode` + `suggestion` |
| VIII | Performance Requirements | PASS | Benchmarks in T052: Moderate < 15s, High < 30s; pagination on `ListSapsAsync`; `CancellationToken` on all async |

**Gate Result**: ALL PASS — no violations. Proceed to Phase 0.

## Project Structure

### Documentation (this feature)

```text
specs/018-sap-generation/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Already exists (created during spec)
├── quickstart.md        # Phase 1 output
├── contracts/
│   └── sap-tools.md     # MCP tool contracts for 5 SAP tools
└── tasks.md             # Already exists (created during spec, to be reconciled)
```

### Source Code (repository root)

```text
src/
├── Ato.Copilot.Core/
│   ├── Interfaces/Compliance/
│   │   └── ISapService.cs                    # NEW: SAP service interface
│   └── Models/Compliance/
│       └── SapModels.cs                      # NEW: Entities, enum, DTOs
├── Ato.Copilot.Agents/
│   ├── Compliance/
│   │   ├── Services/
│   │   │   └── SapService.cs                 # NEW: SAP generation/update/finalize/retrieve
│   │   └── Tools/
│   │       └── SapTools.cs                   # NEW: 5 MCP tools
│   └── Extensions/
│       └── ServiceCollectionExtensions.cs    # MODIFIED: DI registrations
├── Ato.Copilot.Mcp/
│   └── Tools/
│       └── ComplianceMcpTools.cs             # MODIFIED: Register 5 new tools

tests/
└── Ato.Copilot.Tests.Unit/
    ├── Models/
    │   └── SapModelTests.cs                  # NEW: Entity construction tests
    ├── Services/
    │   └── SapServiceTests.cs                # NEW: Service logic tests
    ├── Tools/
    │   └── SapToolTests.cs                   # NEW: Tool invocation tests
    └── Performance/
        └── SapPerformanceTests.cs            # NEW: Benchmark tests
```

**Structure Decision**: Follows existing single-solution layout. New files placed in established directories matching the `SspService`/`AssessmentArtifactService` patterns. No new projects needed.

## Constitution Re-Check (Post-Design)

*Re-evaluated after Phase 1 design completion.*

| # | Principle | Status | Post-Design Evidence |
|---|-----------|--------|---------------------|
| I | Documentation as Source of Truth | PASS | research.md cites 10 concrete file paths; contracts/sap-tools.md defines all 5 tool schemas; data-model.md has full EF Core config |
| II | BaseAgent/BaseTool Architecture | PASS | 5 tools extend `BaseTool` with `Name`/`Description`/`Parameters`/`ExecuteCoreAsync`. DI follows two-step singleton pattern |
| III | Testing Standards | PASS | ~135+ tests covering positive/negative/boundary. Entity, service, tool, and performance layers all covered |
| IV | Azure Government & Compliance First | PASS | SAP is RMF Step 4 deliverable. No new Azure service interactions |
| V | Observability & Structured Logging | PASS | Structured Serilog logging: generation, update, finalization, validation events |
| VI | Code Quality & Maintainability | PASS | Pipeline stages enforce SRP. XML docs on all public types. Method name constants |
| VII | User Experience Consistency | PASS | All 5 tools return `{ status, data, metadata }` envelope. 8 error codes with `message` + `suggestion` |
| VIII | Performance Requirements | PASS | Benchmarks: Moderate <15s, High <30s. `CancellationToken` on all async. Bounded list results |

**Post-Design Gate Result**: ALL PASS — no violations.

## Complexity Tracking

> No constitution violations — this section is intentionally empty.
