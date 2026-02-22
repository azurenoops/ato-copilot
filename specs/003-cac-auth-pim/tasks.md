# Tasks: CAC Authentication & Privileged Identity Management

**Input**: Design documents from `/specs/003-cac-auth-pim/`
**Prerequisites**: plan.md (updated), spec.md (48 FRs, 16 SCs), research.md (13 decisions), data-model.md (3 entities + 5 enums + 1 config entity), contracts/auth-pim-tools.md (15 tools + PimTier classification)

## Format: `[ID] [P?] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- Exact file paths included in all descriptions

---

## Completed Phases (T001–T100) ✅

The original 100 tasks across 15 phases are complete with 796 passing tests (745 unit + 51 integration). See git history for implementation details:

- **Phase 1**: Setup (T001–T003) — NuGet packages, config, enums
- **Phase 2**: Foundational (T004–T016, T080–T081) — Entities, interfaces, DB schema, middleware
- **Phase 3**: US1 CAC Gate (T017–T025, T082–T084)
- **Phase 4**: US2 Two-Tier (T026–T028, T085)
- **Phase 5**: US3 PIM Activation (T029–T037, T086–T087)
- **Phase 6**: US4 PIM Session Mgmt (T038–T042, T088–T089)
- **Phase 7**: US5 Approval Workflow (T043–T047, T090–T091)
- **Phase 8**: US6 Integrated Workflows (T048–T050, T092)
- **Phase 9**: US7 JIT VM Access (T051–T056, T093–T094)
- **Phase 10**: US8 Session Config (T057–T060, T095)
- **Phase 11**: US9 Cert Mapping (T061–T064, T096)
- **Phase 12**: US10 Audit Trail (T065–T068, T097)
- **Phase 13**: US11 Notifications (T069–T070)
- **Phase 14**: US12 Multi-Client (T071–T073)
- **Phase 15**: Polish (T074–T079, T098–T100)

---

## Delta: New FRs Requiring Implementation

**Scope**: 13 new FRs (FR-036–048) plus modifications to FR-001, FR-010, FR-013, FR-019, FR-034. Per Delta Summary in plan.md:

| Status | FRs | Action |
|--------|-----|--------|
| Already implemented | FR-036, FR-039–041 | No tasks needed |
| Requires code changes | FR-001, FR-010, FR-013, FR-034, FR-037, FR-038, FR-042–048 | Delta tasks below |

---

## Phase 16: Delta Setup

**Purpose**: Add NuGet packages and configuration for new FRs

- [X] T101 Add NuGet package Azure.Extensions.AspNetCore.Configuration.Secrets to src/Ato.Copilot.Mcp/Ato.Copilot.Mcp.csproj for Key Vault configuration provider (FR-038)
- [X] T102 [P] Add Retention and KeyVault configuration sections to src/Ato.Copilot.Mcp/appsettings.json per quickstart.md — Retention: { AssessmentRetentionDays: 1095, AuditLogRetentionDays: 2555, CleanupIntervalHours: 24, EnableAutomaticCleanup: true }; KeyVault: { VaultUri: "" }

---

## Phase 17: Delta Foundational

**Purpose**: Core types and configuration updates that MUST be complete before delta feature phases

**⚠️ CRITICAL**: No delta feature work (Phases 18–21) can begin until this phase is complete

- [X] T103 Create PimTier enum (None=0, Read=1, Write=2) with XML doc comments in src/Ato.Copilot.Core/Models/Auth/AuthEnums.cs per data-model.md — None: Tier 1 (no auth/PIM), Read: Tier 2a (CAC + Reader PIM), Write: Tier 2b (CAC + Contributor+ PIM)
- [X] T104 [P] Create RetentionPolicyOptions configuration class in src/Ato.Copilot.Core/Configuration/GatewayOptions.cs per data-model.md — AssessmentRetentionDays (int, default 1095, min 365), AuditLogRetentionDays (int, default 2555, min 2555), CleanupIntervalHours (int, default 24, min 1), EnableAutomaticCleanup (bool, default true)
- [X] T105 [P] Update PimServiceOptions defaults in src/Ato.Copilot.Core/Configuration/GatewayOptions.cs — DefaultActivationDuration from 8h to 4h, MaxActivationDuration from 24h to 8h per FR-010; update appsettings.json Pim section defaults to match; update any hardcoded duration validation bounds in PimService
- [X] T106 Add virtual RequiredPimTier property (default PimTier.None) to BaseTool in src/Ato.Copilot.Agents/Common/BaseTool.cs per R-010; add using directive for PimTier enum
- [X] T107 Update AuthTierClassification in src/Ato.Copilot.Core/Configuration/AuthTierClassification.cs — add Tier2aTools HashSet (pim_list_eligible, pim_list_active, pim_history, jit_list_sessions, run_assessment, collect_evidence, discover_resources, compliance_assess, compliance_collect_evidence, compliance_monitoring) and Tier2bTools HashSet (pim_activate_role, pim_deactivate_role, pim_extend_role, pim_approve_request, pim_deny_request, jit_request_access, jit_revoke_access, cac_sign_out, cac_set_timeout, cac_map_certificate, execute_remediation, validate_remediation, deploy_template, compliance_remediate, compliance_validate_remediation, kanban_remediate_task, kanban_validate_task, kanban_collect_evidence) per R-010 and contracts PIM Tier Classification table; add IsTier2a() and IsTier2b() methods
- [X] T108 [P] Register RetentionPolicyOptions from IConfiguration("Retention") in src/Ato.Copilot.Core/Extensions/CoreServiceExtensions.cs
- [X] T109 [P] Unit tests for PimTier and RetentionPolicyOptions in tests/Ato.Copilot.Tests.Unit/Configuration/DeltaConfigTests.cs — PimTier enum values (None=0, Read=1, Write=2), RetentionPolicyOptions default values and validation bounds, PimServiceOptions updated defaults (4h/8h)

**Checkpoint**: Delta foundation ready — PimTier enum, BaseTool.RequiredPimTier property, AuthTierClassification Tier2a/2b sets, RetentionPolicyOptions, and PIM 4h/8h defaults in place.

---

## Phase 18: Tier 2a/2b Sub-Tier Enforcement (FR-001, FR-013, FR-034)

**Goal**: Tools declare read-eligible vs. write-eligible PIM tier. Middleware enforces PIM tier check against user's active PIM roles. Error messages include required tier and current elevation status.

**Independent Test**: With Reader PIM active, run a compliance assessment (Tier 2a) → succeeds. Attempt a remediation (Tier 2b) → returns INSUFFICIENT_PIM_TIER with "Requires write-eligible PIM role (Contributor or higher). Your current elevation: Reader."

- [X] T110 Override RequiredPimTier on all existing tool classes in src/Ato.Copilot.Agents/Compliance/Tools/AuthPimTools.cs — set PimTier.Read on: PimListEligibleTool, PimListActiveTool, PimHistoryTool, JitListSessionsTool; set PimTier.Write on: PimActivateRoleTool, PimDeactivateRoleTool, PimExtendRoleTool, PimApproveRequestTool, PimDenyRequestTool, JitRequestAccessTool, JitRevokeAccessTool, CacSignOutTool, CacSetTimeoutTool, CacMapCertificateTool; leave PimTier.None (inherited default) on: CacStatusTool — per contracts PIM Tier Classification table
- [X] T111 Update ComplianceAuthorizationMiddleware in src/Ato.Copilot.Mcp/Middleware/ComplianceAuthorizationMiddleware.cs — after existing CAC session check and before role check, add PIM tier enforcement: lookup tool's RequiredPimTier (from BaseTool metadata or Tier2aTools/Tier2bTools sets), query user's active PIM roles from IPimService.ListActiveRolesAsync, compare PIM tier level (Reader=Read, Contributor+=Write); return PIM_ELEVATION_REQUIRED (403) when tool requires PimTier.Read or .Write but user has no active PIM role; return INSUFFICIENT_PIM_TIER (403) when user has Reader PIM but tool requires PimTier.Write; skip check when RequirePim=false (dev mode per FR-036) — per contracts error codes
- [X] T112 Enhance error responses in ComplianceAuthorizationMiddleware — all auth/PIM error envelopes MUST include: requiredPimTier field (None/Read/Write), user's current PIM roles and elevation status, and actionable suggestion per FR-034. Example: "This operation requires a write-eligible PIM role (Contributor or higher). Your current elevation: Reader. Use 'pim_activate_role' with a Contributor-eligible role to proceed."
- [X] T113 [P] Unit tests for PimTier enforcement in tests/Ato.Copilot.Tests.Unit/Middleware/PimTierEnforcementTests.cs — Tier 2a tool with Reader PIM passes, Tier 2a tool without PIM returns PIM_ELEVATION_REQUIRED, Tier 2b tool with Reader PIM returns INSUFFICIENT_PIM_TIER, Tier 2b tool with Contributor PIM passes, Tier 1 tool (PimTier.None) passes without any PIM, error response includes requiredPimTier and current elevation in envelope, RequirePim=false skips all PIM tier checks
- [X] T114 [P] Unit tests for RequiredPimTier declarations in tests/Ato.Copilot.Tests.Unit/Tools/PimTierDeclarationTests.cs — verify every tool class declares correct PimTier per contracts classification table: CacStatusTool=None, PimListEligibleTool=Read, PimListActiveTool=Read, PimHistoryTool=Read, JitListSessionsTool=Read, PimActivateRoleTool=Write, PimDeactivateRoleTool=Write, PimExtendRoleTool=Write, PimApproveRequestTool=Write, PimDenyRequestTool=Write, JitRequestAccessTool=Write, JitRevokeAccessTool=Write, CacSignOutTool=Write, CacSetTimeoutTool=Write, CacMapCertificateTool=Write

**Checkpoint**: Tier 2a/2b enforcement active. Read operations (assessments, evidence, PIM queries) require Reader PIM; write operations (remediations, PIM activations, JIT) require Contributor+. Error messages are descriptive with tier requirements.

---

## Phase 19: Key Vault & Sensitive Data Protection (FR-038, FR-037)

**Goal**: Production secrets loaded from Azure Key Vault via managed identity. Sensitive data (tokens, credentials, connection strings) scrubbed from logs and tool responses.

**Independent Test**: In Production config, verify secrets load from Key Vault (not appsettings). Inject a Bearer token value into a log statement → verify it is scrubbed in output. Inspect tool responses → no plaintext tokens or credentials.

- [X] T115 Add Azure Key Vault configuration provider in src/Ato.Copilot.Mcp/Program.cs — for non-Development environments, read VaultUri from config "KeyVault:VaultUri", add builder.Configuration.AddAzureKeyVault(new Uri(vaultUri), new DefaultAzureCredential(new DefaultAzureCredentialOptions { AuthorityHost = AzureAuthorityHosts.AzureGovernment })) per R-013; Key Vault secret names use "--" as section delimiter (e.g., AzureAd--ClientSecret)
- [X] T116 [P] Create SensitiveDataDestructuringPolicy in src/Ato.Copilot.Core/Observability/SensitiveDataDestructuringPolicy.cs — implement Serilog IDestructuringPolicy; scrub properties matching patterns: Bearer tokens (Authorization headers), ClientSecret, ConnectionString, AccessToken, RefreshToken; replace values with "[REDACTED]" per FR-037
- [X] T117 Register SensitiveDataDestructuringPolicy in Serilog configuration in src/Ato.Copilot.Mcp/Program.cs (.Destructure.With<SensitiveDataDestructuringPolicy>()); audit all tool response models in src/Ato.Copilot.Agents/Compliance/Tools/AuthPimTools.cs — ensure CacStatusTool returns tokenHash (not plaintext token), PIM tools return pimRequestId (not auth tokens), JIT tools return connection commands (not credentials) per FR-037
- [X] T118 [P] Unit tests for SensitiveDataDestructuringPolicy in tests/Ato.Copilot.Tests.Unit/Observability/SensitiveDataPolicyTests.cs — verify Bearer token values scrubbed to [REDACTED], ClientSecret masked, ConnectionString redacted, AccessToken/RefreshToken redacted, safe property values (roleName, scope, justification) pass through unchanged

**Checkpoint**: Key Vault integration ready for production deployment. All sensitive data scrubbed from structured log output. No credential leakage in tool responses.

---

## Phase 20: Data Retention (FR-042, FR-043, FR-044)

**Goal**: Assessment data retained minimum 3 years, audit logs retained minimum 7 years (immutable, append-only). Automated daily cleanup service removes only expired assessment data. Configurable via RetentionPolicyOptions.

**Independent Test**: Verify RetentionPolicyOptions loads from config with correct defaults. Attempt to update/delete an audit log entry → rejected. Insert assessment records with old CreatedAt dates → run cleanup → only records past retention period deleted; audit logs untouched.

- [X] T119 Create RetentionCleanupHostedService in src/Ato.Copilot.Agents/Compliance/Services/RetentionCleanupHostedService.cs — extend BackgroundService with PeriodicTimer(TimeSpan.FromHours(CleanupIntervalHours)); on each tick: create IServiceScope, resolve AtoCopilotContext, delete assessment records where CreatedAt < DateTime.UtcNow.AddDays(-AssessmentRetentionDays), NEVER delete audit log entries (immutable per FR-043), log cleanup summary (records deleted, execution time) at Information level; inject IServiceScopeFactory, IOptions<RetentionPolicyOptions>, ILogger per R-011
- [X] T120 [P] Enforce audit log immutability — ensure the audit log service interface (IAuditLogService or equivalent in existing audit infrastructure) exposes only AddAsync/CreateAsync methods; remove or guard any Update/Delete methods on audit entities; add EF Core model configuration in AtoCopilotContext.OnModelCreating to prevent cascading deletes on audit tables; add XML doc comments noting immutability constraint per FR-043
- [X] T121 Add EF Core migration AddRetentionIndexes in src/Ato.Copilot.Core — create index IX_Assessment_CreatedAt on assessment results table (CreatedAt column) and IX_AuditLog_CreatedAt on audit log table (CreatedAt column) for efficient retention period queries per R-011 implementation notes
- [X] T122 Register RetentionCleanupHostedService as IHostedService in src/Ato.Copilot.Agents/Extensions/ServiceCollectionExtensions.cs — conditionally register only when RetentionPolicyOptions.EnableAutomaticCleanup is true; log registration status at startup
- [X] T123 [P] Unit tests for RetentionCleanupHostedService in tests/Ato.Copilot.Tests.Unit/Services/RetentionCleanupServiceTests.cs — deletes assessment records past AssessmentRetentionDays, does NOT delete audit log records regardless of age, respects EnableAutomaticCleanup=false (service does not register), handles empty dataset gracefully (no errors), runs at configured CleanupIntervalHours interval, uses IServiceScope per request to avoid DbContext concurrency issues

**Checkpoint**: Data retention enforced. Assessment cleanup runs daily. Audit logs immutable for 7+ years per federal requirements. All configurable via RetentionPolicyOptions.

---

## Phase 21: Observability (FR-045, FR-046, FR-047, FR-048)

**Goal**: Health endpoint with per-agent status. Structured metrics (latency, errors, throughput, active sessions). Correlation ID propagation across all requests. Structured log enrichment with correlation ID, agent name, tool name, and user identity.

**Independent Test**: GET /health → returns JSON with per-agent status within 2 seconds. Send request with X-Correlation-ID: test-123 → same ID in response headers and all log entries. Invoke a tool → ToolInvocations counter incremented, ToolDurationMs histogram recorded. All structured logs include CorrelationId, AgentName, ToolName, UserId.

- [X] T124 Complete CorrelationIdMiddleware in src/Ato.Copilot.Core/Observability/CorrelationIdMiddleware.cs — read X-Correlation-ID from request headers; if missing, generate new Guid.NewGuid().ToString(); store in HttpContext.Items["CorrelationId"]; push to Serilog LogContext via LogContext.PushProperty("CorrelationId", correlationId); add X-Correlation-ID to response headers; must run as first middleware in pipeline (before UseSerilogRequestLogging) per R-012
- [X] T125 [P] Create ToolMetrics static class in src/Ato.Copilot.Core/Observability/ToolMetrics.cs — create static Meter named "Ato.Copilot" using System.Diagnostics.Metrics; define instruments: Counter<long> ToolInvocations (tags: tool, agent, status), Histogram<double> ToolDurationMs (tags: tool, agent), Counter<long> ToolErrors (tags: tool, agent, errorCode), UpDownCounter<long> ActiveSessions; provide static Record methods for each instrument per R-012
- [X] T126 [P] Create AgentHealthCheck in src/Ato.Copilot.Core/Observability/AgentHealthCheck.cs — implement IHealthCheck for ComplianceAgent; check that agent is registered, core services (ICacSessionService, IPimService) are resolvable from DI, database is accessible; return HealthCheckResult.Healthy/Degraded/Unhealthy with description string; inject IServiceProvider per R-012 and FR-045
- [X] T127 Configure health check endpoint in src/Ato.Copilot.Mcp/Program.cs — add builder.Services.AddHealthChecks().AddCheck<AgentHealthCheck>("compliance-agent"); add app.MapHealthChecks("/health", new HealthCheckOptions { ResponseWriter = custom JSON writer }); JSON output format: { "status": "Healthy|Degraded|Unhealthy", "agents": [{ "name": "compliance-agent", "status": "Healthy", "description": "..." }], "totalDurationMs": 45 } per FR-045 and SC-015
- [X] T128 Instrument tool invocations in BaseTool in src/Ato.Copilot.Agents/Common/BaseTool.cs — wrap ExecuteAsync with Stopwatch timing; on entry: increment ToolMetrics.ToolInvocations(Name, AgentName, "started"); on success: record ToolMetrics.ToolDurationMs(elapsed, Name, AgentName); on exception: increment ToolMetrics.ToolErrors(Name, AgentName, errorCode) per FR-046
- [X] T129 Add structured log enrichment in src/Ato.Copilot.Mcp/Middleware/AuditLoggingMiddleware.cs — at start of InvokeAsync, push to Serilog LogContext: CorrelationId (from HttpContext.Items["CorrelationId"]), AgentName (from request route/tool metadata), ToolName (from MCP request method), UserId (redacted — first 8 chars of Azure AD object ID + "***") per FR-048; all downstream log statements automatically include these properties
- [X] T130 Wire CorrelationIdMiddleware and health checks in src/Ato.Copilot.Mcp/Program.cs — add app.UseMiddleware<CorrelationIdMiddleware>() as first middleware (before app.UseSerilogRequestLogging()); register health check services in ConfigureServices; ensure pipeline order: CorrelationId → SerilogRequestLogging → CORS → CacAuthentication → ComplianceAuthorization → AuditLogging per R-012 and plan.md middleware pipeline
- [X] T131 [P] Unit tests for CorrelationIdMiddleware in tests/Ato.Copilot.Tests.Unit/Middleware/CorrelationIdMiddlewareTests.cs — request with X-Correlation-ID header uses provided value, request without header generates new GUID (format validated), correlation ID appears in response X-Correlation-ID header, correlation ID stored in HttpContext.Items["CorrelationId"], Serilog LogContext contains CorrelationId property
- [X] T132 [P] Unit tests for ToolMetrics and health checks in tests/Ato.Copilot.Tests.Unit/Observability/ObservabilityTests.cs — ToolMetrics instruments created, ToolInvocations incremented on call, ToolDurationMs recorded, ToolErrors incremented on exception with errorCode tag; AgentHealthCheck returns Healthy when all services available, Unhealthy when core service missing from DI, returns Degraded when database unreachable

**Checkpoint**: Full observability stack active. Health checks, metrics, correlation IDs, and structured log enrichment operational.

---

## Phase 22: Delta Polish & Verification

**Purpose**: Cross-cutting quality improvements, integration tests, and final validation for all delta features

- [X] T133 [P] Add XML documentation comments on all new delta types and methods — PimTier enum, RetentionPolicyOptions, SensitiveDataDestructuringPolicy, RetentionCleanupHostedService, CorrelationIdMiddleware, ToolMetrics, AgentHealthCheck, BaseTool.RequiredPimTier; verify all public members have /// summary per Constitution VI
- [X] T134 [P] Ensure all new async methods honor CancellationToken parameter per Constitution VIII — verify RetentionCleanupHostedService.ExecuteAsync passes token to DB queries and PeriodicTimer.WaitForNextTickAsync, AgentHealthCheck.CheckHealthAsync passes token to service resolution, CorrelationIdMiddleware.InvokeAsync passes token to next delegate
- [X] T135 [P] Integration tests for Tier 2a/2b enforcement in tests/Ato.Copilot.Tests.Integration/PimTierIntegrationTests.cs — Reader PIM active + assessment tool succeeds (Tier 2a), Reader PIM active + remediation tool returns INSUFFICIENT_PIM_TIER (Tier 2b), Contributor PIM active + remediation tool succeeds, no PIM active + Tier 2a tool returns PIM_ELEVATION_REQUIRED, error response envelope contains requiredPimTier field per SC-013/SC-014
- [X] T136 [P] Integration tests for observability in tests/Ato.Copilot.Tests.Integration/ObservabilityIntegrationTests.cs — GET /health returns JSON with agent status within 2 seconds (SC-015), request with X-Correlation-ID header propagated to response and log output, request without correlation ID header generates new GUID in response, tool invocation emits structured metrics with correlation ID per SC-015
- [X] T137 [P] Integration tests for data retention in tests/Ato.Copilot.Tests.Integration/RetentionIntegrationTests.cs — RetentionCleanupHostedService deletes assessments past retention period, audit log entries remain after cleanup (immutable), RetentionPolicyOptions loaded correctly from appsettings.json per SC-016
- [X] T138 [P] Integration test for Key Vault configuration provider in tests/Ato.Copilot.Tests.Integration/KeyVaultIntegrationTests.cs — verify AddAzureKeyVault is registered for non-Development environments, verify Key Vault provider is skipped in Development environment, verify VaultUri is read from configuration "KeyVault:VaultUri", verify DefaultAzureCredential uses AzureGovernment authority host per FR-038/SC-013
- [X] T139 Run updated quickstart.md verification checklist steps 24-32 to validate all delta features end-to-end — Tier 2a/2b sub-tier verification (steps 24-26), observability verification (steps 27-30), data retention verification (steps 31-32)

**Checkpoint**: All delta features validated with integration tests and manual verification. No regressions on original 796 tests.

---

## Dependencies & Execution Order

### Delta Phase Dependencies

- **Delta Setup (Phase 16)**: No dependencies — start immediately
- **Delta Foundational (Phase 17)**: Depends on Phase 16 — BLOCKS all delta feature phases
- **Tier 2a/2b (Phase 18)**: Depends on Phase 17 (PimTier enum, BaseTool.RequiredPimTier, AuthTierClassification)
- **Key Vault & Sensitive Data (Phase 19)**: Depends on Phase 16 (NuGet package); Key Vault (T115) independent of Phase 17; SensitiveDataPolicy (T116-T118) independent of Phase 17
- **Data Retention (Phase 20)**: Depends on Phase 17 (RetentionPolicyOptions)
- **Observability (Phase 21)**: Depends on Phase 17 — independent of Phases 18-20
- **Delta Polish (Phase 22)**: Depends on ALL delta feature phases (18-21) being complete

### Dependency Graph

```
Phase 16 (Delta Setup)
  ├─> Phase 17 (Delta Foundational) ──── BLOCKS delta features
  │     ├─> Phase 18 (Tier 2a/2b) ───── Core security enhancement
  │     ├─> Phase 20 (Data Retention) ── Independent of 18, 19
  │     └─> Phase 21 (Observability) ─── Independent of 18, 19, 20
  │
  └─> Phase 19 (Key Vault / Sensitive Data) ── Can start after Phase 16
        │
        └──── All ──> Phase 22 (Delta Polish & Verification)
```

### Within Each Delta Phase

- Enum/config types before service classes
- BaseTool property before tool class overrides
- AuthTierClassification before middleware enforcement
- Middleware enforcement before error response enhancement
- Core implementation before tests (tests marked [P])

### Parallel Opportunities

- **Phase 16**: T101 and T102 parallel (different files)
- **Phase 17**: T103 sequential (PimTier needed by T106); T104/T105/T108 parallel (independent config classes); T109 parallel (tests)
- **Phase 18**: T110-T112 sequential (tool declarations → middleware → errors); T113/T114 parallel (tests)
- **Phase 19**: T115/T116 parallel (Key Vault and Serilog policy are independent); T117 after T116; T118 parallel (tests)
- **Phase 20**: T119/T120 parallel (cleanup service and immutability enforcement are independent); T121 after T120; T122 after T119; T123 parallel (tests)
- **Phase 21**: T124/T125/T126 parallel (middleware, metrics, health check are different files); T127 after T126; T128 after T125; T129 after T124; T130 after T124/T126/T127; T131/T132 parallel (tests)
- **Phase 22**: All tasks parallel (different files, no interdependencies)

---

## Parallel Example: After Delta Foundational (Phase 17)

```bash
# After Phase 17 completes, these can run in parallel:
Worker A: Phase 18 (Tier 2a/2b) — T110-T114 (5 tasks)
Worker B: Phase 20 (Data Retention) — T119-T123 (5 tasks)
Worker C: Phase 21 (Observability) — T124-T132 (9 tasks)

# Phase 19 can start even earlier (after Phase 16, needs only NuGet package):
Worker D: Phase 19 (Key Vault & Sensitive Data) — T115-T118 (4 tasks)
```

---

## Parallel Example: Within Phase 21 (Observability)

```bash
# These three core components can run in parallel (different files):
Task A: T124 CorrelationIdMiddleware (Core/Observability/)
Task B: T125 ToolMetrics (Core/Observability/)
Task C: T126 AgentHealthCheck (Core/Observability/)

# Then wire everything together:
T127 (needs T126), T128 (needs T125), T129 (needs T124), T130 (needs T124+T126+T127)

# Tests run in parallel after all implementation:
Task D: T131 CorrelationId tests
Task E: T132 Metrics + health tests
```

---

## Implementation Strategy

### Delta MVP (Tier 2a/2b = Phases 16-18)

1. Complete Phase 16: Delta Setup (2 tasks)
2. Complete Phase 17: Delta Foundational (7 tasks)
3. Complete Phase 18: Tier 2a/2b Enforcement (5 tasks)
4. **VALIDATE**: Reader PIM allows assessments but blocks remediations; Contributor PIM allows both; error messages include required tier and current elevation

### Core Security (Phases 16-19)

5. Complete Phase 19: Key Vault & Sensitive Data (4 tasks)
6. **VALIDATE**: Secrets load from Key Vault in non-Dev environments; no credential leakage in logs or tool responses

### Compliance (Phase 20)

7. Complete Phase 20: Data Retention (5 tasks)
8. **VALIDATE**: Assessments auto-cleaned after 3 years; audit logs immutable and retained 7+ years; configurable via RetentionPolicyOptions

### Full Observability (Phase 21)

9. Complete Phase 21: Observability (9 tasks)
10. **VALIDATE**: /health returns per-agent status under 2s; metrics emit on tool invocations; correlation ID propagates end-to-end; structured logs enriched

### Final Validation (Phase 22)

11. Complete Phase 22: Delta Polish (6 tasks)
12. **VALIDATE**: All integration tests pass; quickstart steps 24-32 verified; no regressions on existing 796 tests

---

## Notes

- Delta tasks start at T101 (T001–T100 completed in prior implementation)
- [P] tasks = different files, no dependencies on each other
- No new user stories — all delta tasks enhance existing US1–US12 or add cross-cutting infrastructure
- All tool classes continue to extend BaseTool per Constitution II (NON-NEGOTIABLE)
- All responses continue to use status/data/metadata envelope per Constitution VII
- Commit after each task or logical group
- Stop at any checkpoint to validate independently
- Total: **39 delta tasks** across 7 phases (including 11 test tasks)
- Grand total: **139 tasks** (100 complete + 39 delta)
