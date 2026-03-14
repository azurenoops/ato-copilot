# Implementation Plan: Azure AI Foundry Agent Integration

**Branch**: `028-foundry-agents` | **Date**: 2026-03-13 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/028-foundry-agents/spec.md`

## Summary

Add Azure AI Foundry Agents as a parallel AI processing path alongside the existing IChatClient (direct Azure OpenAI) path. The feature registers a `PersistentAgentsClient` singleton from the unified `AzureAiOptions` configuration, provisions Foundry agents at startup with system prompts and tool definitions, processes user messages through server-side threads and runs with tool dispatch, and provides a graceful fallback chain (Foundry → IChatClient → deterministic). The `AiProvider` enum (`OpenAi` | `Foundry`) on `AzureAiOptions` controls which path agents use at runtime.

## Technical Context

**Language/Version**: C# 13 / .NET 9.0
**Primary Dependencies**: Azure.AI.Agents.Persistent 1.1.0, Azure.AI.OpenAI 2.1.0, Microsoft.Extensions.AI 9.4.x, Azure.Identity 1.13.2, EF Core 9.0, Serilog
**Storage**: SQLite (dev) / SQL Server (prod) via EF Core; Foundry threads are server-side (ephemeral, no local persistence)
**Testing**: xUnit + FluentAssertions + Moq; Tests.Unit (~4146 tests) + Tests.Integration (~220 tests)
**Target Platform**: Linux containers (Docker) on Azure Government / Azure Commercial
**Project Type**: Web service (ASP.NET Core MCP server + Chat API)
**Performance Goals**: MCP tools < 5s simple / < 30s complex; health < 200ms p95; Foundry run timeout 60s configurable
**Constraints**: IL5/IL6 compliance; Azure Government regions only for data residency; DefaultAzureCredential for Foundry (no API key auth); steady-state memory < 512MB
**Scale/Scope**: Multi-agent system with 3 concrete agents (Compliance, Configuration, KnowledgeBase), 70+ tools, multi-user conversations via thread mapping

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| # | Principle | Status | Notes |
|---|-----------|--------|-------|
| I | Documentation as Source of Truth | PASS | Feature documented under `/specs/028-foundry-agents/`; docs updates tracked |
| II | BaseAgent/BaseTool Architecture | PASS | All changes extend BaseAgent (new constructor param, `TryProcessWithFoundryAsync`); no new agent classes |
| III | Testing Standards | PASS | 10+ new unit tests (FoundryAgentTests, FoundryFallbackTests, AzureAiOptionsTests, CoreServiceExtensionsAiTests); integration tests for fallback chain |
| IV | Azure Government & Compliance First | PASS | DefaultAzureCredential with Gov authority host; CloudEnvironment toggle; IL5/IL6 region validation |
| V | Observability & Structured Logging | PASS | FR-021 requires structured logging for run lifecycle events; Serilog configured |
| VI | Code Quality & Maintainability | PASS | DI-based; XML doc on public types; no magic values; single-responsibility methods |
| VII | User Experience Consistency | PASS | Agent responses remain natural language; fallback chain preserves existing UX |
| VIII | Performance Requirements | PASS | 60s run timeout; CancellationToken throughout; bounded polling loop |

**Gate result: PASS** — No violations. Proceeding to Phase 0.

## Project Structure

### Documentation (this feature)

```text
specs/028-foundry-agents/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output (Foundry agent provisioning contract)
└── tasks.md             # Phase 2 output (/speckit.tasks command)
```

### Source Code (repository root)

```text
src/
├── Ato.Copilot.Core/
│   ├── Configuration/
│   │   └── GatewayOptions.cs          # AzureAiOptions, AiProvider enum (MODIFIED)
│   └── Extensions/
│       └── CoreServiceExtensions.cs   # RegisterChatClient, RegisterFoundryClient (MODIFIED)
├── Ato.Copilot.Agents/
│   └── Common/
│       └── BaseAgent.cs               # TryProcessWithFoundryAsync, ProvisionFoundryAgentAsync, thread mapping (MODIFIED)
│   └── Compliance/Agents/
│       └── ComplianceAgent.cs         # Constructor updated (MODIFIED)
│   └── Configuration/Agents/
│       └── ConfigurationAgent.cs      # Constructor updated (MODIFIED)
│   └── KnowledgeBase/Agents/
│       └── KnowledgeBaseAgent.cs      # Constructor updated (MODIFIED)
│   └── Extensions/
│       └── ServiceCollectionExtensions.cs  # Agent DI registrations updated (MODIFIED)
├── Ato.Copilot.Mcp/                   # MCP server entry point (appsettings.json MODIFIED)
└── Ato.Copilot.Chat/                  # Chat API (no changes)

tests/
├── Ato.Copilot.Tests.Unit/
│   ├── Agents/
│   │   └── FoundryAgentTests.cs       # Foundry dispatch, provisioning, thread mapping
│   ├── Common/
│   │   └── BaseAgentAiProcessingTests.cs
│   ├── Configuration/
│   │   └── AzureAiOptionsTests.cs     # Config binding tests
│   ├── Extensions/
│   │   └── CoreServiceExtensionsAiTests.cs
│   └── Server/
│       └── McpServerAiIntegrationTests.cs
└── Ato.Copilot.Tests.Integration/
    └── Agents/
        └── FoundryFallbackTests.cs    # Fallback chain integration tests
```

**Structure Decision**: Existing multi-project solution layout. Feature 028 modifies existing files only — no new projects or assemblies. All Foundry SDK types (`PersistentAgentsClient`) flow from Core → Agents via constructor injection.

## Complexity Tracking

No constitution violations. All gates pass. No complexity justifications needed.

## Post-Design Constitution Re-Check

*Re-evaluated after Phase 1 design completion.*

| # | Principle | Status | Post-Design Notes |
|---|-----------|--------|-------------------|
| I | Documentation as Source of Truth | PASS | spec.md, research.md, data-model.md, quickstart.md, contracts/ all updated |
| II | BaseAgent/BaseTool Architecture | PASS | Foundry integration extends BaseAgent; all tools remain BaseTool subclasses |
| III | Testing Standards | PASS | 4146 unit + 220 integration tests passing; 10+ Foundry-specific tests |
| IV | Azure Government & Compliance First | PASS | Gov-first default; DefaultAzureCredential; authorized regions only |
| V | Observability & Structured Logging | PASS | FR-021 structured logging at appropriate levels |
| VI | Code Quality & Maintainability | PASS | DI-based; XML docs; no magic values; single responsibility |
| VII | User Experience Consistency | PASS | Responses remain natural language; fallback preserves UX |
| VIII | Performance Requirements | PASS | 60s configurable timeout; CancellationToken; bounded polling |

**Post-design gate result: PASS** — No violations introduced by design artifacts.

## Generated Artifacts

| Artifact | Path | Status |
|----------|------|--------|
| Implementation Plan | [plan.md](plan.md) | Complete |
| Research | [research.md](research.md) | Complete (7 decisions documented) |
| Data Model | [data-model.md](data-model.md) | Complete (updated to unified AzureAiOptions) |
| Quickstart | [quickstart.md](quickstart.md) | Complete (updated config paths) |
| Contracts | [contracts/foundry-provisioning.md](contracts/foundry-provisioning.md) | Complete |
| Agent Context | `.github/agents/copilot-instructions.md` | Updated |
