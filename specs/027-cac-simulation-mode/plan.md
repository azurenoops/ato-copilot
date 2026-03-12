# Implementation Plan: CAC Simulation Mode

**Branch**: `027-cac-simulation-mode` | **Date**: 2026-03-12 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/027-cac-simulation-mode/spec.md`

## Summary

Implement a middleware-based CAC simulation mode that synthesizes a configurable `ClaimsPrincipal` from `appsettings.json` / environment variables when `CacAuth:SimulationMode = true`. Simulation mode is guarded against non-Development environments and integrates transparently with existing CAC session management. The middleware reads `SimulatedIdentityOptions` from `CacAuthOptions`, builds claims, and sets `context.User` — no new service interfaces or abstractions are needed.

## Technical Context

**Language/Version**: C# 13 / .NET 9.0  
**Primary Dependencies**: ASP.NET Core, Entity Framework Core, Serilog, Azure.Identity, Microsoft.Graph  
**Storage**: SQL Server via EF Core (`IDbContextFactory<AtoCopilotContext>` pattern)  
**Testing**: xUnit + FluentAssertions + Moq (unit), WebApplication.CreateBuilder + UseTestServer (integration)  
**Target Platform**: Azure Government cloud (Linux/Windows server), dual-mode MCP server (stdio + HTTP)  
**Project Type**: Web service (compliance MCP server)  
**Performance Goals**: <100ms additional startup latency for simulation mode check (SC-006), <5s MCP tool response (Constitution VIII)  
**Constraints**: NIST 800-53, FedRAMP High, Azure Government data residency, no credentials in code  
**Scale/Scope**: Multi-agent compliance platform; this feature adds configuration extensions, a middleware simulation branch, and a new `ClientType` enum value

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| # | Principle | Verdict | Notes |
|---|-----------|---------|-------|
| I | Documentation as Source of Truth | **PASS** | Feature 003 spec exists; no conflicting `/docs/` guidance. New docs will be added post-implementation. |
| II | BaseAgent/BaseTool Architecture | **N/A** | This feature modifies middleware and configuration, not agents or tools. No BaseAgent/BaseTool changes. |
| III | Testing Standards | **PASS** | Plan includes unit tests (positive + negative per public method), integration tests (WebApplicationFactory), boundary/edge-case tests (null/empty config, role arrays). Target 80%+ coverage. |
| IV | Azure Government & Compliance First | **PASS** | Simulated sessions marked with `ClientType.Simulated` for audit exclusion (FR-013). Production safety guard prevents activation in non-Development (FR-008). Evidence generator excludes simulated sessions (FR-014). |
| V | Observability & Structured Logging | **PASS** | FR-017 requires same structured telemetry as real service + debug logs. FR-009 requires startup log with simulated UPN. |
| VI | Code Quality & Maintainability | **PASS** | Interface in Core, implementation in Agents (DI, SRP). XML docs on all public types. Constants for config keys. PascalCase naming. |
| VII | User Experience Consistency | **N/A** | No new MCP tools or user-facing endpoints. Internal service consumed by existing middleware. |
| VIII | Performance Requirements | **PASS** | SC-006: <100ms startup overhead. All async methods accept `CancellationToken`. No unbounded queries. |

**Gate result: PASS** — No violations. Proceed to Phase 0.

## Project Structure

### Documentation (this feature)

```text
specs/027-cac-simulation-mode/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
└── tasks.md             # Phase 2 output (NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
src/
├── Ato.Copilot.Core/
│   ├── Configuration/
│   │   └── GatewayOptions.cs              # Extend CacAuthOptions with SimulationMode + SimulatedIdentityOptions
│   └── Models/
│       └── Auth/
│           └── AuthEnums.cs                # Add ClientType.Simulated value
├── Ato.Copilot.Mcp/
│   ├── Middleware/
│   │   └── CacAuthenticationMiddleware.cs  # Modify: add simulation branch to synthesize ClaimsPrincipal
│   ├── appsettings.Development.json        # NEW: simulation config for dev
│   └── Program.cs                          # Modify: startup validation of SimulatedIdentity when SimulationMode=true

tests/
├── Ato.Copilot.Tests.Unit/
│   └── Middleware/
│       └── CacAuthSimulationTests.cs       # NEW: unit tests for simulation branch
└── Ato.Copilot.Tests.Integration/
    └── SimulationModeIntegrationTests.cs   # NEW
```

**Structure Decision**: Follows the existing multi-project layout. Configuration extends `CacAuthOptions` in Core. Simulation logic is a branch within the existing `CacAuthenticationMiddleware` in Mcp — no new interfaces or services needed. The middleware already serves as the abstraction layer between identity sources and downstream services.

## Phase 0 Output

- [research.md](research.md) — 6 research topics resolved:
  - R1: *(Superseded)* Interface placement — no new interface needed; middleware is the abstraction
  - R2: Config extension → extend `CacAuthOptions` with nested `SimulatedIdentityOptions`
  - R3: Middleware injection → synthesize `ClaimsPrincipal` with `"Simulated"` auth scheme
  - R4: Evidence exclusion → forward-looking guard via `ClientType.Simulated`
  - R5: DI override for tests → existing `WebApplication.CreateBuilder` + `UseTestServer()` pattern
  - R6: *(Superseded)* Real SmartCardService — no service abstraction needed

## Phase 1 Output

- [data-model.md](data-model.md) — 3 entities defined:
  - `SimulatedIdentityOptions` (new config POCO)
  - `CacAuthOptions` (extended with 2 properties)
  - `ClientType` enum (extended with `Simulated` value)
  - No database schema changes required
- [quickstart.md](quickstart.md) — developer setup guide:
  - Configuration examples (JSON + env vars)
  - Identity switching instructions
  - Integration test fixture pattern
  - Common error troubleshooting table

## Constitution Re-check (Post-Phase 1)

| # | Principle | Verdict | Notes |
|---|-----------|---------|-------|
| I | Documentation as Source of Truth | **PASS** | All design artifacts created; no conflicts with existing docs |
| II | BaseAgent/BaseTool Architecture | **N/A** | Middleware modification, not agent/tool |
| III | Testing Standards | **PASS** | Unit + integration test patterns documented; boundary cases identified |
| IV | Azure Government & Compliance First | **PASS** | ClientType.Simulated tagging, evidence exclusion, production safety guard designed |
| V | Observability & Structured Logging | **PASS** | Telemetry contract defined (spans, counters, structured logs) |
| VI | Code Quality & Maintainability | **PASS** | Config POCO in Core, middleware branch in Mcp. XML docs on new types. Constants for config keys. PascalCase naming. |
| VII | User Experience Consistency | **N/A** | No user-facing changes |
| VIII | Performance Requirements | **PASS** | In-memory config read; all async with CancellationToken; <100ms overhead |

**Gate result: PASS** — No violations. Ready for Phase 2 task generation.

## Complexity Tracking

> No Constitution Check violations — this section is empty.
