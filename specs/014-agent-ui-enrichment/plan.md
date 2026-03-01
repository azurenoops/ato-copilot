# Implementation Plan: Agent-to-UI Response Enrichment

**Branch**: `014-agent-ui-enrichment` | **Date**: 2026-02-26 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/014-agent-ui-enrichment/spec.md`

## Summary

Enrich the MCP server → client response pipeline so all chat responses carry intent classification, typed structured data, tool execution details, follow-up prompts, and dynamic suggestions for Compliance, KnowledgeBase, and Configuration agents. Fix `agentName`→`agentUsed` serialization mismatch. Evolve error model to structured `ErrorDetail`. Remove dead M365 card builders (cost, deployment, infrastructure, resource) and add 10 compliance-domain Adaptive Cards with drill-down navigation and action buttons. Enrich the VS Code analysis panel with 5-level severity, control family grouping, framework reference, resource context, auto-remediation with CAC+PIM security, and finding status lifecycle. Add SSE streaming progress UI. Expose an IaC scanning MCP tool. All infrastructure-modifying actions require CAC authentication and PIM role elevation via existing MCP tools.

## Technical Context

**Language/Version**: C# 13 / .NET 9.0 (server), TypeScript 5.3 (VS Code & M365 extensions)  
**Primary Dependencies**: Microsoft.Extensions.AI 9.4.0-preview, Azure.Identity 1.13.2, Azure.ResourceManager.* 1.x, Microsoft.EntityFrameworkCore 9.0, Microsoft.Graph 5.70.0, Serilog 4.2.0, System.Text.Json 9.0.5 (C#); axios 1.6.5, adaptivecards 3.0.1, express 4.18.2 (TS)  
**Storage**: EF Core 9.0 dual-provider (SQLite dev / SQL Server prod); two DbContexts (`AtoCopilotContext` for compliance, `ChatDbContext` for chat); in-memory caching via `Microsoft.Extensions.Caching.Memory`  
**Testing**: xUnit 2.9.3, FluentAssertions 7.0.0, Moq 4.20.72, EF Core InMemory 9.0, AspNetCore.Mvc.Testing 9.0 (C#); mocha 10.2.0, chai 4.3.10, sinon 17.0.1 (TS)  
**Target Platform**: Azure Government (AzureUSGovernment primary, AzureCloud secondary); Docker multi-stage (sdk:9.0 → aspnet:9.0), port 3001, non-root user  
**Project Type**: Multi-project web service (MCP server) + VS Code extension + M365 Teams extension + React Chat SPA  
**Performance Goals**: Simple MCP queries <5s, complex assessments <30s, health endpoints <200ms p95, startup <10s (Constitution VIII)  
**Constraints**: <512MB steady-state memory, <1GB bulk ops; NIST 800-53 / FedRAMP High compliance; US Gov data residency; CAC+PIM for infra changes  
**Scale/Scope**: 3 agents (Compliance, KnowledgeBase, Configuration), ~48 FRs, 3 TS codebases, 8 existing PIM MCP tools, 2462 .NET tests + 93 TS tests baseline

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Documentation as Source of Truth | ✅ PASS | All changes follow spec.md; no invented guidance. New IaC tool and streaming endpoint follow existing patterns in `/docs/`. |
| II. BaseAgent/BaseTool Architecture | ✅ PASS | AgentResponse extended with optional fields (no new agents). IaC scanning tool extends BaseTool. PIM tools already extend BaseTool. No agent architecture violations. |
| III. Testing Standards | ✅ PASS | Spec requires zero regressions on 2462+93 baseline. 80%+ coverage mandate. Each FR maps to positive + negative test cases. Edge cases explicitly enumerated in spec. |
| IV. Azure Government & Compliance | ✅ PASS | CAC+PIM security model for infrastructure changes. NIST 800-53 control family display. FedRAMP compliance context in all outputs. Existing `DefaultAzureCredential` chain preserved. |
| V. Observability & Structured Logging | ✅ PASS | FR-027/028/029 mandate TS extension logging. AuditLoggingMiddleware used for remediation audit. Tool executions logged per Constitution V. |
| VI. Code Quality & Maintainability | ✅ PASS | DI injection for all new services. `[JsonPropertyName]` for serialization. ErrorDetail model replaces magic strings. Optional properties avoid breaking changes. |
| VII. User Experience Consistency | ✅ PASS | Core motivation — enriched response envelope (intentType, agentUsed, toolsExecuted, structured data, ErrorDetail with errorCode+suggestion). Mode parity between stdio/HTTP. Progress feedback via SSE (FR-029a-e). |
| VIII. Performance Requirements | ✅ PASS | SSE streaming for long ops. CancellationToken on all async paths. No unbounded queries. IaC scan scoped to single file. |

**Gate Result**: ✅ ALL PASS — No violations. Proceed to Phase 0.

## Project Structure

### Documentation (this feature)

```text
specs/014-agent-ui-enrichment/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   ├── mcp-chat-response.json    # Enriched response schema
│   ├── mcp-chat-request.json     # Extended request schema (action/actionContext)
│   ├── sse-events.json           # SSE event type schemas
│   └── adaptive-cards.json       # Card template contracts
└── tasks.md             # Phase 2 output (NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
src/
├── Ato.Copilot.Core/
│   ├── Models/Compliance/         # ComplianceFinding enrichment (controlFamily, riskLevel, etc.)
│   └── Interfaces/Auth/           # IPimService (existing, unchanged)
├── Ato.Copilot.Agents/
│   ├── Common/BaseAgent.cs        # AgentResponse extension (Suggestions, ResponseData, etc.)
│   └── Compliance/Tools/          # IaC scanning tool (new BaseTool)
├── Ato.Copilot.Mcp/
│   ├── Models/McpProtocol.cs      # McpChatResponse enrichment, ErrorDetail, action fields
│   ├── Server/McpServer.cs        # ProcessChatRequestAsync enrichment, action routing
│   └── Tools/ComplianceMcpTools.cs # IaC tool registration (PIM tools already registered)
├── Ato.Copilot.State/             # No changes expected
├── Ato.Copilot.Channels/          # No changes expected
└── Ato.Copilot.Chat/              # No changes expected (ClientApp React SPA not in scope)

extensions/
├── vscode/
│   └── src/
│       ├── commands/              # ComplianceFinding TS interface (analyzeFile.ts)
│       ├── services/              # McpChatResponse TS interface, SSE streaming client, PIM check flow
│       ├── webview/               # Analysis panel enrichment (analysisPanel.ts — 5-level severity, control family)
│       └── participant.ts         # Chat participant enrichment (attribution, tools, suggestions)
└── m365/
    └── src/
        ├── cards/                 # Remove 4 dead cards, add 10 compliance cards (incl. kanban board)
        └── services/              # McpResponse TS interface, SSE streaming client, PIM check flow

tests/
├── Ato.Copilot.Tests.Unit/
│   ├── Agents/                    # AgentResponse extension tests, IaC tool tests
│   ├── Mcp/                       # McpServer enrichment tests, ErrorDetail tests, action routing
│   └── Models/                    # McpProtocol model tests
└── Ato.Copilot.Tests.Integration/
    └── Mcp/                       # End-to-end enriched response tests
```

**Structure Decision**: Multi-project structure matching existing repo layout. Changes span 3 C# projects (Core models, Agents, Mcp server) and 2 TypeScript extensions (VS Code, M365). No new projects created. Tests added to existing test projects.

## Complexity Tracking

> No Constitution Check violations — this section is intentionally empty.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|

## Constitution Check — Post-Design Re-evaluation

*Re-evaluated after Phase 1 design artifacts (data-model.md, contracts/, quickstart.md).*

| Principle | Status | Post-Design Notes |
|-----------|--------|-------------------|
| I. Documentation as Source of Truth | ✅ PASS | All design decisions documented in research.md with rationale. Contract schemas in contracts/ serve as source of truth for response formats. |
| II. BaseAgent/BaseTool Architecture | ✅ PASS | `AgentResponse` extended with optional properties (no subclassing). `IacComplianceScanTool` extends `BaseTool` with `Name`, `Description`, `Parameters`, `ExecuteAsync()`. PIM tier property inherited. |
| III. Testing Standards | ✅ PASS | Quickstart mandates ≥2462 .NET + ≥93 TS baseline. Each model change (ErrorDetail, AgentResponse extension) requires positive + negative tests. Edge cases from spec map to boundary tests. |
| IV. Azure Government & Compliance | ✅ PASS | PIM flow uses existing `IPimService` + MCP tools. CAC validation via `ComplianceAuthorizationMiddleware`. Audit logging via `AuditLoggingMiddleware`. All Azure SDK calls use `DefaultAzureCredential`. US Gov data residency maintained. |
| V. Observability & Structured Logging | ✅ PASS | FR-027/028/029 in contracts. SSE events provide real-time observability. Audit log fields defined in data model (user identity, PIM role, script hash, result). |
| VI. Code Quality & Maintainability | ✅ PASS | All new types use DI. `ErrorDetail` eliminates magic strings. `[JsonPropertyName]` for clean serialization. Optional properties with defaults avoid null-check proliferation. Card builders follow single-responsibility (one card per file). |
| VII. User Experience Consistency | ✅ PASS | Response schema in `mcp-chat-response.json` enforces uniform envelope. `ErrorDetail` has `errorCode` + `message` + `suggestion`. Card contracts define consistent attribution footer and suggestion buttons. SSE provides progress feedback for >2s operations. |
| VIII. Performance Requirements | ✅ PASS | IaC scan is single-file scoped. SSE streaming avoids timeout on long assessments. Conversation history capped at 20 exchanges. No unbounded collections in contracts. `CancellationToken` on all async tool paths. |

**Post-Design Gate Result**: ✅ ALL PASS — Design is constitution-compliant. Ready for Phase 2 (tasks).
