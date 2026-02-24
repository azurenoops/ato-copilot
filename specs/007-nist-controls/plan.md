# Implementation Plan: NIST Controls Knowledge Foundation

**Feature Branch**: `007-nist-controls`
**Spec**: `specs/007-nist-controls/spec.md`
**Created**: 2026-02-23

## Summary

Enhance the existing `NistControlsService` (482 lines, 3-method interface, 10 tests) into a production-grade NIST SP 800-53 Rev 5 knowledge foundation. This adds 4 new interface methods, typed OSCAL deserialization models, `IMemoryCache` with configurable TTL, Polly resilience via `Microsoft.Extensions.Http.Resilience`, a `BackgroundService` cache warmup, `IHealthCheck` integration, compliance validation, `System.Diagnostics.Metrics` observability, and two new knowledge-base tools (`NistControlSearchTool`, `NistControlExplainerTool`). All changes stay within existing projects — no new assemblies.

## Technical Context

| Dimension | Value |
|-----------|-------|
| Language | C# 13 / .NET 9.0 |
| Dependencies | `IMemoryCache` (already in State project), `Microsoft.Extensions.Http.Resilience` 9.0.0 / Polly 8 (already in Core), `System.Text.Json` 9.0.5, `System.Diagnostics.Metrics`, `Microsoft.Extensions.Diagnostics.HealthChecks` |
| Storage | In-memory only (`IMemoryCache`); existing `NistControl` EF entity preserved for DB |
| Testing | xUnit + FluentAssertions + Moq; target 80%+ coverage |
| Performance Goals | Cache hit < 100ms, health check < 5s, search < 2s, warmup < 15s |
| Constraints | 255K-line embedded OSCAL catalog (compiled into assembly), Singleton DI lifetime |
| Scale | 20 control families, ~1,000 controls, 3 existing consumers |

## Constitution Check

| # | Principle | Status | Notes |
|---|-----------|--------|-------|
| I | Documentation as Source of Truth | PASS | Spec, data-model, contracts, quickstart all produced |
| II | BaseAgent/BaseTool Architecture | PASS | Two new tools extend `BaseTool`; `RegisterTool()` in agent constructor |
| III | Testing Standards | PASS | 20+ new unit tests planned; existing 10 preserved (FR-049) |
| IV | Azure Government & Compliance First | PASS | NIST 800-53 Rev 5 catalog is the core subject matter |
| V | Observability & Structured Logging | PASS | `ComplianceMetricsService` + `Activity` spans + Serilog structured logging |
| VI | Code Quality & Maintainability | PASS | Typed records, `[JsonPropertyName]`, XML docs, no magic values |
| VII | User Experience Consistency | PASS | MCP tools follow standard envelope; actionable error messages |
| VIII | Performance Requirements | PASS | Cache warmup eliminates cold start; cache hit < 100ms target |

## Project Structure

```
src/Ato.Copilot.Core/
  Interfaces/Compliance/
    IComplianceInterfaces.cs              ← MODIFIED (add 4 methods to INistControlsService)
  Models/Compliance/
    ComplianceModels.cs                   ← PRESERVED (NistControl EF entity unchanged)

src/Ato.Copilot.Agents/
  Compliance/
    Agents/
      ComplianceAgent.cs                  ← MODIFIED (register 2 new tools via RegisterTool)
    Configuration/
      ComplianceAgentOptions.cs           ← MODIFIED (add NistControlsOptions nested class or binding)
    Models/
      OscalModels.cs                      ← NEW (typed OSCAL deserialization records)
    Services/
      NistControlsService.cs             ← MODIFIED (IMemoryCache, IOptions, 4 new methods, typed deserialization)
      NistControlsCacheWarmupService.cs   ← NEW (BackgroundService for cache pre-warming)
      ComplianceValidationService.cs      ← NEW (validate 11 system control IDs against catalog)
    Tools/
      ComplianceTools.cs                  ← PRESERVED (existing ControlFamilyTool unchanged)
      NistControlSearchTool.cs            ← NEW (BaseTool — search_nist_controls)
      NistControlExplainerTool.cs         ← NEW (BaseTool — explain_nist_control)
    Resources/
      NIST_SP-800-53_rev5_catalog.json    ← PRESERVED (embedded resource, 255K lines)
  Extensions/
    ServiceCollectionExtensions.cs        ← MODIFIED (register new services, tools, health check, IMemoryCache)
  Observability/
    ComplianceMetricsService.cs           ← NEW (static Meter, counters, histograms — follows ToolMetrics pattern)
    NistControlsHealthCheck.cs            ← NEW (IHealthCheck — follows AgentHealthCheck pattern)

src/Ato.Copilot.Mcp/
  appsettings.json                        ← MODIFIED (migrate NistCatalog:* → Agents:Compliance:NistControls)
  Tools/
    ComplianceMcpTools.cs                 ← MODIFIED (wire 2 new tools)
  Program.cs                             ← MODIFIED (register health check endpoint)

tests/Ato.Copilot.Tests.Unit/
  Services/
    NistControlsServiceTests.cs           ← MODIFIED (expand from 10 to 30+ tests)
  Tools/
    NistControlToolTests.cs               ← NEW (unit tests for search + explainer tools)
  Observability/
    NistControlsHealthCheckTests.cs       ← NEW (unit tests for health check)
    ComplianceMetricsServiceTests.cs      ← NEW (unit tests for metrics service)
  Services/
    ComplianceValidationServiceTests.cs   ← NEW (unit tests for validation service)
    NistControlsCacheWarmupServiceTests.cs ← NEW (unit tests for warmup background service)
```

### Structure Decision

No new projects. All changes within existing `Ato.Copilot.Agents`, `Ato.Copilot.Core`, `Ato.Copilot.Mcp`, and `Ato.Copilot.Tests.Unit` projects. This preserves the current solution topology and avoids unnecessary assembly proliferation.

## Complexity Tracking

No violations detected — table not required. All changes follow established patterns (`BaseTool`, `BackgroundService`, `IHealthCheck`, `ToolMetrics`). No new project references needed. One new NuGet package: `Microsoft.Extensions.Caching.Memory` is added to `Ato.Copilot.Agents.csproj` (T001) for `IMemoryCache` support — this package is already used transitively but requires an explicit reference for direct API usage.

## Phase 0 Artifacts

| Artifact | Path | Status |
|----------|------|--------|
| Research | `specs/007-nist-controls/research.md` | Complete (9 decisions) |

## Phase 1 Artifacts

| Artifact | Path | Status |
|----------|------|--------|
| Data Model | `specs/007-nist-controls/data-model.md` | Complete |
| Interface Contract | `specs/007-nist-controls/contracts/interface-contract.md` | Complete |
| MCP Tools Contract | `specs/007-nist-controls/contracts/mcp-tools-contract.md` | Complete |
| Quickstart | `specs/007-nist-controls/quickstart.md` | Complete |
