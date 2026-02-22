# Implementation Plan: CAC Authentication & Privileged Identity Management (Update)

**Branch**: `003-cac-auth-pim` | **Date**: 2026-02-22 | **Spec**: [spec.md](spec.md)
**Input**: Updated feature specification with 13 new FRs (FR-036 through FR-048) and 4 new SCs (SC-013 through SC-016), plus modifications to FR-001, FR-004, FR-010, FR-013, FR-019, FR-034, FR-035.

## Summary

This plan update integrates requirements from an external project's FR set into the existing Feature 003 specification. The original 100 tasks (T001–T100) are complete with 796 passing tests. This update addresses:

1. **Tier 2a/2b sub-tier model** (FR-001, FR-013, FR-019): Tools declare read-eligible vs. write-eligible PIM tier. Read operations (assessments) require Reader PIM; write operations (remediations) require Contributor+.
2. **PIM timeout alignment** (FR-010): Default activation 4 hours (was 8), max 8 hours (was 24) per IL5/IL6 policy.
3. **Development bypass mode** (FR-036): Formal FR for existing `RequireCac`/`RequirePim` config flags.
4. **Sensitive data protection** (FR-037): Token/credential scrubbing from logs, errors, and responses.
5. **Secret management** (FR-038): Azure Key Vault with managed identity for production; .env fallback for dev.
6. **Role-based access control** (FR-039–041): Four-role model with union permissions and PIM tier constraints.
7. **Data retention** (FR-042–044): 3-year assessment retention, 7-year immutable audit logs.
8. **Observability** (FR-045–048): Health endpoint, structured metrics, correlation ID propagation, structured log enrichment.

## Technical Context

**Language/Version**: C# 13 / .NET 9.0
**Primary Dependencies**: Microsoft.SemanticKernel 1.34, Microsoft.Identity.Web 3.5, Microsoft.Graph 5.70, Azure.ResourceManager.SecurityCenter, EF Core 9.0, Serilog, xUnit 2.9.3, FluentAssertions 7.0.0, Moq 4.20.72
**Storage**: SQLite (dev) / SQL Server (prod) via EF Core; Azure Key Vault (prod secrets)
**Testing**: xUnit + FluentAssertions + Moq (unit), WebApplicationFactory (integration)
**Target Platform**: .NET 9.0 MCP server (stdio + HTTP), Azure Government
**Project Type**: Multi-agent MCP server (web-service + CLI)
**Performance Goals**: Simple queries <5s, health endpoint <200ms p95, startup <10s
**Constraints**: <512MB steady-state memory, FIPS 140-2 Level 2 Key Vault, Azure Government endpoints only
**Scale/Scope**: Single-tenant, ~50 concurrent users, 37+ MCP tools

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Documentation as Source of Truth | PASS | Spec updated with all new FRs; plan references spec sections |
| II. BaseAgent/BaseTool Architecture | PASS | Tools extend BaseTool; new `RequiredPimTier` property added to tool metadata |
| III. Testing Standards | PASS | Each new FR requires corresponding unit + integration tests |
| IV. Azure Government & Compliance First | PASS | FR-038 (Key Vault), FR-042–044 (retention), FR-045–048 (observability) all align |
| V. Observability & Structured Logging | PASS | FR-047–048 directly implement this principle |
| VI. Code Quality & Maintainability | PASS | XML docs, SRP, DI patterns maintained |
| VII. User Experience Consistency | PASS | Uniform envelope schema; FR-034 enhanced with PIM tier info |
| VIII. Performance Requirements | PASS | SC-015 (/health <2s), structured metrics per FR-046 |

**Gate result**: PASS — no violations.

## Project Structure

### Documentation (this feature)

```text
specs/003-cac-auth-pim/
├── plan.md              # This file (updated)
├── research.md          # Phase 0 output (updated with R-010 through R-013)
├── data-model.md        # Phase 1 output (updated with PIM tier, retention fields)
├── quickstart.md        # Phase 1 output (updated with new defaults and verification steps)
├── contracts/
│   └── auth-pim-tools.md # Phase 1 output (updated with requiredPimTier, new error codes)
└── tasks.md             # Phase 2 output (existing 100 tasks + new delta tasks)
```

### Source Code (repository root)

```text
src/
├── Ato.Copilot.Core/
│   ├── Configuration/GatewayOptions.cs
│   ├── Constants/ComplianceRoles.cs
│   ├── Data/Context/AtoCopilotContext.cs        # DbSets: CacSessions, JitRequests, CertificateRoleMappings
│   ├── Interfaces/Compliance/                   # ICacSessionService, IPimService, IJitVmAccessService
│   ├── Models/Auth/                              # AuthEnums.cs (PimTier, SessionStatus, ClientType, JitRequestType, JitRequestStatus)
│   ├── Models/Compliance/                       # Entities, enums, result types
│   └── Observability/                           # NEW: CorrelationIdMiddleware, HealthCheckService, MetricsService
├── Ato.Copilot.Agents/
│   ├── Common/
│   │   ├── BaseAgent.cs
│   │   └── BaseTool.cs                          # UPDATE: Add RequiredPimTier property
│   ├── Compliance/
│   │   ├── Agents/ComplianceAgent.cs
│   │   ├── Services/
│   │   │   ├── CacSessionService.cs
│   │   │   ├── PimService.cs                    # UPDATE: Default 4h, max 8h
│   │   │   ├── JitVmAccessService.cs
│   │   │   ├── CertificateRoleResolver.cs
│   │   │   ├── OverdueScanHostedService.cs
│   │   │   └── SessionCleanupHostedService.cs
│   │   └── Tools/AuthPimTools.cs                # UPDATE: RequiredPimTier on each tool
│   └── Extensions/ServiceCollectionExtensions.cs
├── Ato.Copilot.Mcp/
│   ├── Program.cs                               # UPDATE: Health checks, correlation ID, Key Vault
│   ├── Middleware/
│   │   ├── CacAuthenticationMiddleware.cs
│   │   ├── ComplianceAuthorizationMiddleware.cs # UPDATE: Tier 2a/2b enforcement
│   │   ├── AuditLoggingMiddleware.cs            # UPDATE: Correlation ID enrichment
│   │   └── CorrelationIdMiddleware.cs           # NEW: (in pipeline before all others)
│   ├── Server/McpServer.cs, McpHttpBridge.cs, McpStdioService.cs
│   └── Tools/ComplianceMcpTools.cs
├── Ato.Copilot.State/
│   ├── Abstractions/IStateManagers.cs
│   └── Implementations/InMemoryStateManagers.cs

tests/
├── Ato.Copilot.Tests.Unit/                      # 745 existing tests + new delta tests
└── Ato.Copilot.Tests.Integration/               # 51 existing tests + new delta tests
```

**Structure Decision**: Existing 6-project structure retained. Observability code in `Ato.Copilot.Core/Observability/`. No new projects needed.

## Complexity Tracking

No constitution violations requiring justification.

## Delta Summary: New FRs vs. Existing Implementation

| New FR | Gap Status | Implementation Needed |
|--------|------------|----------------------|
| FR-036 (dev bypass) | **Already implemented** | Document-only: `RequireCac`/`RequirePim` config flags exist |
| FR-037 (sensitive data) | **Partially implemented** | Audit Serilog destructuring policy, scrub tool responses |
| FR-038 (Key Vault) | **Not implemented** | Add `Azure.Extensions.AspNetCore.Configuration.Secrets` provider |
| FR-039–041 (RBAC roles) | **Already implemented** | No code changes — ComplianceRoles + CertificateRoleResolver cover this |
| FR-042 (3yr retention) | **Not implemented** | Add retention metadata, configurable cleanup policy |
| FR-043 (7yr audit immutable) | **Partially implemented** | Add immutability constraint on audit tables |
| FR-044 (configurable retention) | **Not implemented** | Add `RetentionPolicyOptions` configuration |
| FR-045 (health endpoint) | **Partially implemented** | Enhance `/health` with per-agent status |
| FR-046 (structured metrics) | **Not implemented** | Add metrics instrumentation (latency, error rate, throughput) |
| FR-047 (correlation ID) | **In progress** | Complete `CorrelationIdMiddleware` + propagation |
| FR-048 (log enrichment) | **Partially implemented** | Add correlation ID + agent/tool name to Serilog context |
| FR-001 (Tier 2a/2b) | **Requires changes** | Add `PimTier` enum, update `AuthTierClassification`, update middleware |
| FR-010 (PIM 4h/8h) | **Requires changes** | Update `PimServiceOptions` defaults and validation bounds |
| FR-013 (read/write PIM) | **Requires changes** | Link tier classification to PIM role requirements |
| FR-034 (PIM tier in errors) | **Requires changes** | Update error response messages with required tier info |
