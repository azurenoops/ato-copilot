# Implementation Plan: Kanban User Context & Comment Permission Enhancement

**Branch**: `004-kanban-user-context` | **Date**: 2026-02-22 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/004-kanban-user-context/spec.md`

## Summary

Propagate authenticated user identity from the HTTP request context through the MCP tool pipeline into all Kanban service operations and MCP server session initialization, replacing 18 hardcoded `"system"` user-ID strings, 9 `"System"` display-name strings, and 8 `"Compliance.Officer"` role strings across 12 tool methods. Add an `isAssignedToCurrentUser` computed flag to all task responses. Grant the SecurityLead role the `CanDeleteAnyComment` permission.

## Technical Context

**Language/Version**: C# 13 / .NET 9.0  
**Primary Dependencies**: Microsoft.Extensions.AI 9.4.0-preview, Microsoft.Identity.Web 3.5.0, Serilog, Entity Framework Core 9.0.0  
**Storage**: SQLite (development), SQL Server (production) via EF Core  
**Testing**: xUnit 2.9.3, FluentAssertions 7.0.0, Moq 4.20.72 — 950 tests (877 unit + 73 integration)  
**Target Platform**: ASP.NET Core 9.0 (Azure Government, Azure Cloud)  
**Project Type**: MCP server (web-service) with agent architecture  
**Performance Goals**: MCP tool response < 5s for simple queries, < 30s for complex operations per Constitution VIII  
**Constraints**: < 512MB steady-state memory, CancellationToken on all async ops  
**Scale/Scope**: 6-project solution, 18 Kanban tools with 12 requiring identity propagation changes and 3 requiring `isAssignedToCurrentUser` flag addition

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| # | Principle | Requirement | Status | Notes |
|---|-----------|-------------|--------|-------|
| I | Documentation as Source of Truth | Follow `/docs/` guidance | PASS | No conflicting guidance exists; changes align with documented architecture |
| II | BaseAgent/BaseTool Architecture | All tools extend BaseTool | PASS | No new tools created; existing tools modified in-place. BaseTool pattern preserved |
| III | Testing Standards | 80%+ coverage, positive + negative tests | PASS | New tests required for: user context extraction, `isAssignedToCurrentUser` flag, SecurityLead permission, fallback identity |
| IV | Azure Government & Compliance | FedRAMP/NIST compliance | PASS | No new Azure interactions. Identity extraction uses existing claims from CAC/PIM auth middleware |
| V | Observability & Structured Logging | Serilog structured logging | PASS | User identity will be logged via existing AuditLoggingMiddleware enrichment |
| VI | Code Quality & Maintainability | SRP, DI, XML docs, no magic values | PASS | New `IUserContext` interface follows DI pattern; hardcoded strings replaced with injected values. Accepted deviation: Singleton tools resolve `IUserContext` via `scope.ServiceProvider.GetRequiredService<IUserContext>()` — this follows the pre-existing BaseTool scope-resolution pattern (see Research Task 1, Alternative 3). |
| VII | UX Consistency | Envelope schema, actionable errors | PASS | `isAssignedToCurrentUser` added to existing task response schema; no breaking changes |
| VIII | Performance Requirements | < 5s simple, CancellationToken | PASS | No additional latency — user context extraction is O(1) claim lookup |

**Gate Result**: PASS — all 8 principles satisfied. No violations to justify.

## Project Structure

### Documentation (this feature)

```text
specs/004-kanban-user-context/
├── plan.md              # This file
├── research.md          # Phase 0: Research findings
├── data-model.md        # Phase 1: Entity and interface definitions
├── quickstart.md        # Phase 1: Developer quickstart guide
├── contracts/           # Phase 1: MCP tool response contract changes
│   └── tool-responses.md
└── tasks.md             # Phase 2 output (/speckit.tasks — NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
src/
├── Ato.Copilot.Core/
│   └── Interfaces/
│       └── Auth/
│           └── IUserContext.cs          # NEW — user context abstraction
├── Ato.Copilot.Mcp/
│   ├── Server/
│   │   └── McpServer.cs                 # MODIFIED — replace hardcoded "mcp-user" with IUserContext.UserId
│   └── Extensions/
│       └── McpServiceExtensions.cs      # MODIFIED — register IUserContext in DI
├── Ato.Copilot.Agents/
│   └── Compliance/
│       ├── Tools/
│       │   └── KanbanTools.cs           # MODIFIED — resolve IUserContext, replace hardcoded identity strings in 12 tool methods
│       └── Services/
│           └── KanbanPermissionsHelper.cs # MODIFIED — add CanDeleteAnyComment to SecurityLead
└── Ato.Copilot.State/
    └── (no changes — ConversationState.UserId already exists)

tests/
├── Ato.Copilot.Tests.Unit/
│   ├── Services/
│   │   └── KanbanPermissionsHelperTests.cs  # MODIFIED — SecurityLead delete-any tests
│   ├── Tools/
│   │   └── KanbanToolTests.cs               # MODIFIED — user context propagation tests
│   └── Middleware/
│       └── UserContextTests.cs              # NEW — IUserContext extraction tests
└── Ato.Copilot.Tests.Integration/
    └── KanbanIntegrationTests.cs            # MODIFIED — user identity propagation tests
```

**Structure Decision**: Existing 6-project structure preserved. The `IUserContext` interface lives in `Ato.Copilot.Core` (which already has `<FrameworkReference Include="Microsoft.AspNetCore.App" />`) so it can be referenced by both `Ato.Copilot.Agents` and `Ato.Copilot.Mcp`. No new projects needed.

## Complexity Tracking

> No constitution violations — this section intentionally left empty.
