# Implementation Plan: KnowledgeBase Agent — "Compliance Library"

**Branch**: `010-knowledgebase-agent` | **Date**: 2026-02-24 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/010-knowledgebase-agent/spec.md`

## Summary

Implement a standalone KnowledgeBase Agent extending `BaseAgent` that provides always-available compliance education and reference capabilities. The agent registers 7 tools spanning NIST 800-53 controls, STIG controls, RMF process, DoD impact levels, and FedRAMP templates. It replaces 5 existing stub services with full implementations backed by 9 manually curated JSON data files. A new multi-agent orchestrator with confidence-scored `CanHandle` routing replaces the current if/else `ClassifyAndRouteAgent()` method. All data is resolved from local files for offline IL6 operation. Comprehensive unit + integration tests (150+ new tests) cover tools, services, orchestrator, and MCP layer.

## Technical Context

**Language/Version**: C# 13 / .NET 9.0  
**Primary Dependencies**: Microsoft.Extensions.AI, Microsoft.Extensions.Caching.Memory, Microsoft.Extensions.DependencyInjection, System.Text.Json, xUnit 2.9.3, Moq 4.20.72, FluentAssertions 7.0.0  
**Storage**: JSON data files on disk (9 files loaded into `IMemoryCache`); `IAgentStateManager` (in-memory) for agent state  
**Testing**: xUnit + Moq + FluentAssertions (unit); Microsoft.AspNetCore.Mvc.Testing (integration)  
**Target Platform**: .NET 9.0 console/web — Azure Government (primary), Azure Cloud (secondary)  
**Project Type**: MCP server (stdio + HTTP modes) with multi-agent architecture  
**Performance Goals**: <2s cached / <5s cold for simple lookups (SC-001); <10s startup (Constitution VIII)  
**Constraints**: <512MB steady-state memory; fully offline-capable (IL6); zero external HTTP calls for data retrieval  
**Scale/Scope**: 7 new tools, 7 service implementations (5 replacements + 2 new), 9 JSON data files, 1 new agent, 1 orchestrator, 28 functional requirements, 150+ new tests

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Documentation as Source of Truth | PASS | New feature will add `/docs/knowledgebase.md` documenting agent capabilities, tools, and data file format |
| II. BaseAgent/BaseTool Architecture | PASS | `KnowledgeBaseAgent` extends `BaseAgent`; all 7 tools extend `BaseTool`; system prompt in `KnowledgeBaseAgent.prompt.txt`; tools registered via `RegisterTool()` |
| III. Testing Standards | PASS | FR-028 requires unit tests for all 7 tools + 6 services + orchestrator + query classification; integration tests for MCP routing; target 150+ new tests (SC-013); 80%+ coverage |
| IV. Azure Government & Compliance | PASS | No direct Azure API calls in KB agent (informational-only boundary); Azure implementation guidance is hardcoded reference data, not live interactions |
| V. Observability & Structured Logging | PASS | FR-019 tracks query metadata (type, duration, success); FR-025 logs errors; `BaseTool.ExecuteAsync()` already instruments tool executions |
| VI. Code Quality & Maintainability | PASS | DI via constructor injection; XML docs on all public types; named constants for magic values; methods scoped to single responsibility |
| VII. User Experience Consistency | PASS | FR-025 uses `success: false` + `suggestion` error envelope; all tools return structured JSON; informational disclaimer on educational responses |
| VIII. Performance Requirements | PASS | SC-001 defines <2s cached / <5s cold; 24h cache TTL for service data + 60min for tool results; `CancellationToken` propagated through all async paths |

**Gate Result**: PASS — No violations. Proceed to Phase 0.

## Project Structure

### Documentation (this feature)

```text
specs/010-knowledgebase-agent/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output (MCP tool contracts)
└── tasks.md             # Phase 2 output (/speckit.tasks command)
```

### Source Code (repository root)

```text
src/
├── Ato.Copilot.Agents/
│   ├── Common/
│   │   ├── BaseAgent.cs                        # MODIFY: add abstract CanHandle(string)
│   │   └── BaseTool.cs                         # EXISTING (no changes)
│   ├── Compliance/
│   │   ├── Agents/
│   │   │   └── ComplianceAgent.cs              # MODIFY: add CanHandle() implementation
│   │   └── Services/
│   │       └── KnowledgeBase/                  # REPLACE: 5 stubs → full implementations
│   │           ├── RmfKnowledgeService.cs
│   │           ├── StigKnowledgeService.cs
│   │           ├── StigValidationService.cs
│   │           ├── DoDInstructionService.cs
│   │           └── DoDWorkflowService.cs
│   ├── Configuration/
│   │   └── Agents/
│   │       └── ConfigurationAgent.cs           # MODIFY: add CanHandle() implementation
│   ├── KnowledgeBase/                          # NEW: entire directory
│   │   ├── Agents/
│   │   │   └── KnowledgeBaseAgent.cs
│   │   ├── Configuration/
│   │   │   └── KnowledgeBaseAgentOptions.cs
│   │   ├── Data/                               # 9 JSON data files (content files)
│   │   │   ├── nist-800-53-controls.json
│   │   │   ├── stig-controls.json
│   │   │   ├── windows-server-stig-azure.json
│   │   │   ├── rmf-process.json
│   │   │   ├── rmf-process-enhanced.json
│   │   │   ├── impact-levels.json
│   │   │   ├── fedramp-templates.json
│   │   │   ├── dod-instructions.json
│   │   │   └── navy-workflows.json
│   │   ├── Prompts/
│   │   │   └── KnowledgeBaseAgent.prompt.txt
│   │   ├── Services/
│   │   │   ├── ImpactLevelService.cs
│   │   │   └── FedRampTemplateService.cs
│   │   └── Tools/
│   │       ├── ExplainNistControlTool.cs
│   │       ├── SearchNistControlsTool.cs
│   │       ├── ExplainStigTool.cs
│   │       ├── SearchStigsTool.cs
│   │       ├── ExplainRmfTool.cs
│   │       ├── ExplainImpactLevelTool.cs
│   │       └── GetFedRampTemplateGuidanceTool.cs
│   └── Extensions/
│       └── ServiceCollectionExtensions.cs      # MODIFY: add AddKnowledgeBaseAgent()
├── Ato.Copilot.Core/
│   ├── Interfaces/
│   │   └── Compliance/
│   │       └── IComplianceInterfaces.cs        # MODIFY: expand KB interfaces + add new ones
│   └── Models/
│       └── Compliance/                         # MODIFY: add KB entity models
├── Ato.Copilot.Mcp/
│   ├── Server/
│   │   ├── McpServer.cs                        # MODIFY: replace ClassifyAndRouteAgent → orchestrator
│   │   └── AgentOrchestrator.cs                # NEW: multi-agent orchestrator
│   ├── Tools/
│   │   └── KnowledgeBaseMcpTools.cs            # NEW: MCP tool wrappers
│   └── Extensions/
│       └── McpServiceExtensions.cs             # MODIFY: register KB MCP tools

tests/
├── Ato.Copilot.Tests.Unit/
│   ├── Agents/
│   │   └── KnowledgeBaseAgentTests.cs          # NEW
│   ├── Services/
│   │   ├── ImpactLevelServiceTests.cs          # NEW
│   │   ├── FedRampTemplateServiceTests.cs      # NEW
│   │   ├── RmfKnowledgeServiceTests.cs         # NEW
│   │   ├── StigKnowledgeServiceTests.cs        # NEW
│   │   ├── DoDInstructionServiceTests.cs       # NEW
│   │   └── DoDWorkflowServiceTests.cs          # NEW
│   ├── Tools/
│   │   ├── ExplainNistControlToolTests.cs      # NEW
│   │   ├── SearchNistControlsToolTests.cs      # NEW
│   │   ├── ExplainStigToolTests.cs             # NEW
│   │   ├── SearchStigsToolTests.cs             # NEW
│   │   ├── ExplainRmfToolTests.cs              # NEW
│   │   ├── ExplainImpactLevelToolTests.cs      # NEW
│   │   └── GetFedRampTemplateGuidanceToolTests.cs # NEW
│   └── Orchestrator/
│       └── AgentOrchestratorTests.cs           # NEW
└── Ato.Copilot.Tests.Integration/
    ├── KnowledgeBaseMcpToolEndpointTests.cs    # NEW
    └── OrchestratorRoutingIntegrationTests.cs  # NEW
```

**Structure Decision**: Follows the existing multi-project solution layout. New KB agent code goes into `src/Ato.Copilot.Agents/KnowledgeBase/` (parallel to `Compliance/` and `Configuration/`). New services that replace stubs stay in their existing `Compliance/Services/KnowledgeBase/` directory. Orchestrator lives in `Ato.Copilot.Mcp/Server/` alongside the existing `McpServer`. JSON data files are content files in `KnowledgeBase/Data/` with `<Content CopyToOutputDirectory="PreserveNewest" />` in the csproj.

## Post-Design Constitution Re-Check

*Re-evaluated after Phase 1 design artifacts (data-model.md, contracts/mcp-tools.md, quickstart.md) are complete.*

| Principle | Status | Post-Design Notes |
|-----------|--------|-------------------|
| I. Documentation as Source of Truth | PASS | quickstart.md provides developer onboarding; data-model.md documents all entities/interfaces; contracts/mcp-tools.md documents all MCP tool schemas |
| II. BaseAgent/BaseTool Architecture | PASS | data-model.md confirms `KnowledgeBaseAgent : BaseAgent` with all required abstracts; 7 tools extend `BaseTool`; `CanHandle()` added as abstract on `BaseAgent` — existing agents get concrete implementations |
| III. Testing Standards | PASS | research.md defines 168 estimated tests across 14+ files; contracts define expected inputs/outputs enabling precise assertions; boundary cases documented (empty collections, null inputs, invalid IDs) |
| IV. Azure Government & Compliance | PASS | No live Azure API calls in design; all data is local JSON; Azure Gov guidance is reference content only; IL6 offline-capable confirmed |
| V. Observability & Structured Logging | PASS | `BaseTool.ExecuteAsync()` already instruments all tool calls; orchestrator will log selected agent + confidence score + fallback decisions |
| VI. Code Quality & Maintainability | PASS | data-model.md uses C# records for immutable entities; all interfaces documented with XML summary; no magic values — thresholds in `KnowledgeBaseAgentOptions`; DI via constructor injection throughout |
| VII. User Experience Consistency | PASS | contracts/mcp-tools.md defines uniform response envelopes with `success`, `data`, `metadata` fields; error responses include `errorCode` + `suggestion`; informational disclaimer on all educational responses |
| VIII. Performance Requirements | PASS | `IMemoryCache` with 24h TTL for data files; <2s cached / <5s cold targets documented; `CancellationToken` on all async signatures in data-model.md; 9 JSON files well under 512MB memory budget |

**Post-Design Gate Result**: PASS — No new violations introduced by design artifacts.

## Complexity Tracking

> No violations to justify — Constitution Check passed cleanly (both pre-research and post-design).
