# Implementation Plan: ATO Remediation Engine

**Branch**: `009-remediation-engine` | **Date**: 2026-02-24 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/009-remediation-engine/spec.md`

## Summary

Enhance the existing `RemediationEngine` (452 lines, partially simulated) into a production-grade `AtoRemediationEngine` with a 3-tier execution pipeline (AI script → Compliance Remediation Service → Legacy ARM), real Azure ARM operations, snapshot-based rollback, approval workflows, batch concurrency control via SemaphoreSlim, and bidirectional integration with the Remediation Kanban board (Feature 002). The engine exposes 18 unique method names (21 total signatures including overloads) across three tiers — core operations, workflow management, and AI-enhanced capabilities — consumed by 3 existing MCP tools and the KanbanService.

## Technical Context

**Language/Version**: C# 13 / .NET 9.0
**Primary Dependencies**: Azure.ResourceManager (1.13.2), Azure.Identity (1.13.2), Microsoft.Extensions.AI (9.4.0-preview), Microsoft.EntityFrameworkCore (9.0.x), Moq, xUnit, FluentAssertions
**Storage**: EF Core InMemory (unit tests), SQL Server via AtoCopilotContext (runtime), in-memory Dictionary + List for remediation tracking
**Testing**: xUnit + FluentAssertions + Moq, InMemoryDbContextFactory pattern (established in RemediationEngineTests.cs)
**Target Platform**: Azure Government (AzureUSGovernment primary, AzureCloud secondary)
**Project Type**: Library (Ato.Copilot.Agents) + Service host (Ato.Copilot.Mcp)
**Performance Goals**: Plan generation <5s for 50+ findings, single remediation <2min, batch of 10 <5min
**Constraints**: <512MB memory steady-state, CancellationToken on all async methods, ARM API rate limiting (3-5 concurrent operations)
**Scale/Scope**: 1,000+ findings per plan, 10 concurrent batch remediations, 8 legacy ARM operations

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Documentation as Source of Truth | PASS | Spec documents all behavior; plan references existing codebase patterns |
| II. BaseAgent/BaseTool Architecture | PASS | Existing 3 MCP tools already extend BaseTool; new tools (if any) will follow same pattern |
| III. Testing Standards | PASS | Will use established xUnit + FluentAssertions + Moq pattern; positive + negative tests per method; InMemoryDbContextFactory |
| IV. Azure Government & Compliance | PASS | ArmClient already registered with dual-cloud support via DefaultAzureCredential; all ARM operations will use existing ArmClient singleton |
| V. Observability & Structured Logging | PASS | ILogger<AtoRemediationEngine> injected; each tier logs success/failure; execution tracking captures duration |
| VI. Code Quality & Maintainability | PASS | Decomposing monolithic RemediationEngine into single-responsibility services; XML docs on all public types; no magic values |
| VII. User Experience Consistency | PASS | Existing MCP tools maintain response envelope patterns; manual guidance uses plain language |
| VIII. Performance Requirements | PASS | Plan generation <5s, CancellationToken on all async ops, SemaphoreSlim for bounded concurrency |

## Project Structure

### Documentation (this feature)

```text
specs/009-remediation-engine/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   └── IRemediationEngine.md
├── checklists/
│   └── requirements.md
└── tasks.md             # Phase 2 output (via /speckit.tasks)
```

### Source Code (repository root)

```text
src/Ato.Copilot.Core/
├── Interfaces/Compliance/
│   ├── IComplianceInterfaces.cs           # MODIFY: Extend IRemediationEngine (4→17 methods)
│   ├── IAiRemediationPlanGenerator.cs     # NEW: AI plan generator interface
│   ├── IRemediationScriptExecutor.cs      # NEW: Script executor interface
│   ├── INistRemediationStepsService.cs    # NEW: NIST steps service interface
│   ├── IAzureArmRemediationService.cs     # NEW: ARM remediation interface
│   ├── IComplianceRemediationService.cs   # NEW: Tier 2 remediation interface
│   └── IScriptSanitizationService.cs      # NEW: Script safety validation interface
├── Models/Compliance/
│   ├── ComplianceModels.cs                # MODIFY: Add 15+ new model classes
│   ├── RemediationScript.cs               # NEW: Script + execution result models
│   ├── RemediationGuidance.cs             # NEW: AI guidance model
│   └── PrioritizedFinding.cs              # NEW: AI prioritization model
└── Configuration/
    └── GatewayOptions.cs                  # EXISTING: No changes needed

src/Ato.Copilot.Agents/
├── Compliance/
│   ├── Configuration/
│   │   └── ComplianceAgentOptions.cs      # MODIFY: Add RemediationOptions sub-class
│   ├── Services/
│   │   ├── RemediationEngine.cs           # REPLACE: AtoRemediationEngine.cs (452→~2200 lines)
│   │   └── Engines/Remediation/           # NEW directory
│   │       ├── AtoRemediationEngine.cs    # NEW: Production engine
│   │       ├── AiRemediationPlanGenerator.cs       # NEW
│   │       ├── RemediationScriptExecutor.cs        # NEW
│   │       ├── NistRemediationStepsService.cs      # NEW
│   │       ├── AzureArmRemediationService.cs       # NEW
│   │       ├── ComplianceRemediationService.cs     # NEW
│   │       └── ScriptSanitizationService.cs        # NEW
│   └── Tools/
│       └── ComplianceTools.cs             # MODIFY: Update tool implementations for new engine API
├── Extensions/
│   └── ServiceCollectionExtensions.cs     # MODIFY: Register new services

tests/Ato.Copilot.Tests.Unit/
├── Services/
│   ├── RemediationEngineTests.cs          # MODIFY: Update for new AtoRemediationEngine
│   └── Engines/Remediation/              # NEW directory
│       ├── AtoRemediationEngineTests.cs   # NEW: Core engine tests
│       ├── AiRemediationPlanGeneratorTests.cs      # NEW
│       ├── RemediationScriptExecutorTests.cs       # NEW
│       ├── NistRemediationStepsServiceTests.cs     # NEW
│       ├── AzureArmRemediationServiceTests.cs      # NEW
│       ├── ComplianceRemediationServiceTests.cs    # NEW
│       └── ScriptSanitizationServiceTests.cs       # NEW
```

**Structure Decision**: Follows established project layout. New remediation services placed under `Engines/Remediation/` subdirectory matching the pattern used for compliance engine services. Interfaces in Core project, implementations in Agents project, tests mirror source structure.

## Complexity Tracking

| Metric | Value | Notes |
|--------|-------|-------|
| Existing source files modified | 5 | IComplianceInterfaces.cs, ComplianceModels.cs, ComplianceAgentOptions.cs, ComplianceTools.cs, ServiceCollectionExtensions.cs |
| New source files created | 12 | AtoRemediationEngine.cs + 6 services + 3 model files + RemediationOptions.cs + 1 test infrastructure |
| New test files created | 7 | 1 per service + AtoRemediationEngineTests.cs |
| Existing test files modified | 1 | RemediationEngineTests.cs (rename references) |
| New interfaces | 6 | IAiRemediationPlanGenerator, IRemediationScriptExecutor, INistRemediationStepsService, IAzureArmRemediationService, IComplianceRemediationService, IScriptSanitizationService |
| Extended interfaces | 1 | IRemediationEngine (4→18 unique method names, 21 total signatures) |
| New model types | ~30 | See data-model.md |
| Estimated LOC (source) | ~3,500 | Engine: ~2,200 + services: ~1,300 |
| Estimated LOC (tests) | ~4,000 | ~150 tests across 7 test files |
| Estimated total tests | ~150 | Positive + negative + edge cases |

## Research Findings Summary

Full research documented in [research.md](research.md). Key decisions:

1. **Enhance, don't replace**: Extend existing `RemediationEngine` into `AtoRemediationEngine` preserving backward compatibility
2. **ComplianceFinding, not AtoFinding**: Spec references `AtoFinding` but codebase uses `ComplianceFinding` throughout — use `ComplianceFinding`
3. **Singleton lifetime**: Matches existing registration pattern; `IDbContextFactory` already creates short-lived DbContext instances
4. **ARM write operations**: `GenericResource.UpdateAsync` for property mods, REST PUT for provider-specific operations (diagnostics, alerts, policies)
5. **IChatClient optional**: Nullable constructor parameter with `IsAvailable` property gate; all AI methods return fallback results when unavailable
6. **3-tier pipeline**: AI Script → Compliance Remediation Service → Legacy ARM — progressive fallback with per-tier logging
7. **Kanban integration**: Optional `IKanbanService?` dependency; post-execution task status updates and evidence collection

## Implementation Phases

### Phase 1: Models, Interfaces & Configuration (Foundation)

**Goal**: Define all types, interfaces, and configuration options needed by the engine and services.

**Covers**: FR-001, FR-010, FR-016, FR-017, FR-018 (type definitions)

**Files created/modified**:
- `src/Ato.Copilot.Core/Models/Compliance/ComplianceModels.cs` — Add ~30 new types including models, enums, and options (RemediationItem, RemediationExecution, BatchRemediationResult, RemediationValidationResult, RemediationRollbackResult, RemediationWorkflowStatus, RemediationApprovalResult, RemediationScheduleResult, RemediationProgress, RemediationHistory, RemediationMetric, RemediationActivity, RemediationImpactAnalysis, ResourceImpact, ManualRemediationGuide, ImplementationTimeline, TimelinePhase, RemediationExecutionOptions, BatchRemediationOptions, RemediationPlanOptions, BatchRemediationSummary, ValidationCheck, RollbackPlan, RemediationExecutiveSummary, RiskMetrics, RemediationPriority, RemediationExecutionStatus, ScriptType)
- `src/Ato.Copilot.Core/Models/Compliance/RemediationScript.cs` — NEW: Script model with Content, ScriptType enum, Description, Parameters, EstimatedDuration
- `src/Ato.Copilot.Core/Models/Compliance/RemediationGuidance.cs` — NEW: AI guidance model with Explanation, TechnicalPlan, ConfidenceScore, References
- `src/Ato.Copilot.Core/Models/Compliance/PrioritizedFinding.cs` — NEW: AI prioritization model with Finding, AiPriority, Justification, BusinessImpact
- `src/Ato.Copilot.Core/Interfaces/Compliance/IComplianceInterfaces.cs` — Extend IRemediationEngine from 4→18 unique method names (21 total signatures)
- `src/Ato.Copilot.Core/Interfaces/Compliance/IAiRemediationPlanGenerator.cs` — NEW interface
- `src/Ato.Copilot.Core/Interfaces/Compliance/IRemediationScriptExecutor.cs` — NEW interface
- `src/Ato.Copilot.Core/Interfaces/Compliance/INistRemediationStepsService.cs` — NEW interface
- `src/Ato.Copilot.Core/Interfaces/Compliance/IAzureArmRemediationService.cs` — NEW interface
- `src/Ato.Copilot.Core/Interfaces/Compliance/IComplianceRemediationService.cs` — NEW interface
- `src/Ato.Copilot.Core/Interfaces/Compliance/IScriptSanitizationService.cs` — NEW interface
- `src/Ato.Copilot.Agents/Compliance/Configuration/ComplianceAgentOptions.cs` — Add RemediationOptions sub-class with MaxConcurrentRemediations, ScriptTimeout, MaxRetries, RequireApproval, AutoValidate, AutoRollbackOnFailure, UseAiScript

**Estimated tests**: None — types and interfaces have no logic
**Dependencies**: None

### Phase 2: Supporting Services (Service Layer)

**Goal**: Implement the 6 decomposed services that the engine delegates to.

**Covers**: FR-002 (pipeline services), FR-013 (AI services), FR-015 (service decomposition), FR-019 (legacy ARM), FR-020 (step parsing)

**Files created/modified**:
- `src/Ato.Copilot.Agents/Compliance/Services/Engines/Remediation/NistRemediationStepsService.cs` — Curated NIST steps by control family, regex step parsing from guidance text, FR-020 action verb patterns
- `src/Ato.Copilot.Agents/Compliance/Services/Engines/Remediation/ScriptSanitizationService.cs` — Validate scripts against safe command patterns, reject destructive commands (resource deletion, subscription-wide changes)
- `src/Ato.Copilot.Agents/Compliance/Services/Engines/Remediation/AiRemediationPlanGenerator.cs` — IChatClient-based script generation, guidance, prioritization; graceful null fallback
- `src/Ato.Copilot.Agents/Compliance/Services/Engines/Remediation/RemediationScriptExecutor.cs` — Script execution with timeout (5min), retry (3x), sanitization gate
- `src/Ato.Copilot.Agents/Compliance/Services/Engines/Remediation/AzureArmRemediationService.cs` — 8 legacy ARM operations (TLS, diagnostics, alerts, log retention, encryption, NSG, policy, HTTPS), before/after snapshot capture, GenericResource.UpdateAsync
- `src/Ato.Copilot.Agents/Compliance/Services/Engines/Remediation/ComplianceRemediationService.cs` — Structured Tier 2 remediation orchestration between AI and legacy ARM

**Tests created**:
- `tests/Ato.Copilot.Tests.Unit/Services/Engines/Remediation/NistRemediationStepsServiceTests.cs`
- `tests/Ato.Copilot.Tests.Unit/Services/Engines/Remediation/ScriptSanitizationServiceTests.cs`
- `tests/Ato.Copilot.Tests.Unit/Services/Engines/Remediation/AiRemediationPlanGeneratorTests.cs`
- `tests/Ato.Copilot.Tests.Unit/Services/Engines/Remediation/RemediationScriptExecutorTests.cs`
- `tests/Ato.Copilot.Tests.Unit/Services/Engines/Remediation/AzureArmRemediationServiceTests.cs`
- `tests/Ato.Copilot.Tests.Unit/Services/Engines/Remediation/ComplianceRemediationServiceTests.cs`

**Estimated tests**: ~60 tests (10 per service)
**Dependencies**: Phase 1 (interfaces + models)

### Phase 3: Core Engine — Plan Generation (P1)

**Goal**: Implement enhanced plan generation with filtering, prioritization, timeline, and risk scoring.

**Covers**: FR-001 (methods 5-7), FR-010, FR-016, FR-017, FR-018, US-1

**Files created/modified**:
- `src/Ato.Copilot.Agents/Compliance/Services/Engines/Remediation/AtoRemediationEngine.cs` — NEW: Constructor, DI, plan generation methods: `GenerateRemediationPlanAsync` (3 overloads), `AnalyzeRemediationImpactAsync`, `PrioritizeFindingsWithAiAsync`, `GenerateManualRemediationGuideAsync`
- Delete or archive `src/Ato.Copilot.Agents/Compliance/Services/RemediationEngine.cs`

**Key implementation details**:
- Severity→Priority mapping: Critical→P0, High→P1, Medium→P2, Low→P3, Other→P4
- Risk scoring: Critical=10, High=7.5, Medium=5, Low=2.5, Other=1
- Duration estimation: Auto 10-30min by severity, Manual 30min-4hr
- Timeline: 5 phases — Immediate, 24 Hours, Week 1, Month 1, Backlog
- Plan sorting: Severity desc → AutoRemediable first → Duration asc
- Filter support: MinSeverity, IncludeFamilies, ExcludeFamilies, AutomatableOnly, GroupByResource
- 3-tier single-finding plan: AI plan → NIST steps → manual parsing

**Tests created**:
- `tests/Ato.Copilot.Tests.Unit/Services/Engines/Remediation/AtoRemediationEngineTests.cs` — Plan generation section (~30 tests)

**Estimated tests**: ~30 tests
**Dependencies**: Phase 1 (models), Phase 2 (AI service, NIST service)

### Phase 4: Core Engine — Execution & Validation (P1-P2)

**Goal**: Implement single-finding execution, validation, rollback, and dry-run support.

**Covers**: FR-002, FR-003, FR-004, FR-005, FR-008, FR-009, US-2, US-5

**Files modified**:
- `src/Ato.Copilot.Agents/Compliance/Services/Engines/Remediation/AtoRemediationEngine.cs` — Add execution methods: `ExecuteRemediationAsync` (new overload with options), `ValidateRemediationAsync` (new overload returning typed result), `RollbackRemediationAsync`, plus snapshot capture helpers

**Key implementation details**:
- 3-tier execution pipeline with explicit fallback triggers:
  - **Tier 1** (AI Script): Attempted when `IChatClient` is available and `UseAiScript = true`; falls back when `IChatClient` is null, AI returns null, or throws
  - **Tier 2** (ComplianceRemediationService): Applies predefined remediation templates for known compliance patterns; falls back when `CanHandle()` returns false or throws
  - **Tier 3** (AzureArmRemediationService): Executes one of 8 hardcoded ARM operations as final fallback
- Before/after snapshot via `GenericResource.GetAsync()` → JSON serialization
- Dry-run: Walk pipeline without applying changes
- `EnableAutomatedRemediation` gate check before any execution
- Execution tracking in `ConcurrentDictionary<string, RemediationExecution>`
- Status lifecycle: Pending → Approved → InProgress → Completed/Failed/RolledBack
- Auto-validate: Post-execution validation checks status + steps count + changes
- Auto-rollback: On validation failure, restore from before-snapshot

**Tests added to**: `AtoRemediationEngineTests.cs` — Execution & validation section (~25 tests)

**Estimated tests**: ~25 tests
**Dependencies**: Phase 3 (engine class exists), Phase 2 (ARM service, script executor)

### Phase 5: Batch, Concurrency & Kanban Integration (P2)

**Goal**: Implement batch remediation with SemaphoreSlim, kanban task sync, and evidence collection.

**Covers**: FR-007, FR-011, US-3, US-4

**Files modified**:
- `src/Ato.Copilot.Agents/Compliance/Services/Engines/Remediation/AtoRemediationEngine.cs` — Add: `ExecuteBatchRemediationAsync` with SemaphoreSlim concurrency, post-execution kanban task updates, bulk task advancement

**Key implementation details**:
- `SemaphoreSlim(_options.MaxConcurrentRemediations)` for bounded concurrency
- `FailFast` mode: CancellationTokenSource linked to batch, cancel on first failure
- `ContinueOnError` mode: Catch per-finding exceptions, aggregate results
- Kanban integration (optional `IKanbanService?`):
  - After individual success: Find linked task by FindingId → MoveTask to InReview → Add history entry
  - After batch: Bulk-advance all linked tasks
  - On failure: Add system comment with error details
  - Post-remediation: Trigger `CollectTaskEvidenceAsync`
- Batch summary: SuccessRate, severity counts, risk reduction, duration

**Tests added to**: `AtoRemediationEngineTests.cs` — Batch & kanban section (~20 tests)

**Estimated tests**: ~20 tests  
**Dependencies**: Phase 4 (single-execution works), Phase 2 (services)

### Phase 6: Workflow, Approval & Scheduling (P3-P4)

**Goal**: Implement approval workflows, progress tracking, history, scheduling, and impact analysis.

**Covers**: FR-006, FR-014, US-6, US-7, US-10, US-11

**Files modified**:
- `src/Ato.Copilot.Agents/Compliance/Services/Engines/Remediation/AtoRemediationEngine.cs` — Add: `ProcessRemediationApprovalAsync`, `GetActiveRemediationWorkflowsAsync`, `GetRemediationProgressAsync`, `GetRemediationHistoryAsync`, `ScheduleRemediationAsync`, `AnalyzeRemediationImpactAsync`

**Key implementation details**:
- Approval: Pending → Approved (triggers execute) or Rejected (records approver + comments)
- Progress: Query `_activeRemediations` + `_remediationHistory` for last 30 days, calculate completion rate + average time
- History: Date range filter on `_remediationHistory`, aggregate metrics (total, successful, failed, rolled back)
- Scheduling: Create `RemediationScheduleResult` with scheduled time + finding IDs + status=Scheduled; execution deferred to caller — the calling layer (e.g., `ComplianceWatchService` or MCP host) is responsible for polling due schedules and triggering `ExecuteBatchRemediationAsync` at the scheduled time (no background timer in this phase)
- Impact analysis: Severity-weighted risk calculation, per-resource impact grouping, recommendations

**Tests added to**: `AtoRemediationEngineTests.cs` — Workflow section (~15 tests)

**Estimated tests**: ~15 tests
**Dependencies**: Phase 4 (execution tracking exists)

### Phase 7: AI-Enhanced Capabilities (P3)

**Goal**: Implement AI script generation, guidance, and prioritization with graceful fallback.

**Covers**: FR-013, US-8, US-9

**Files modified**:
- `src/Ato.Copilot.Agents/Compliance/Services/Engines/Remediation/AtoRemediationEngine.cs` — Add: `GenerateRemediationScriptAsync`, `GetRemediationGuidanceAsync`, `PrioritizeFindingsWithAiAsync`, `GenerateManualRemediationGuideAsync`

**Key implementation details**:
- Script generation: Delegate to `IAiRemediationPlanGenerator.GenerateScriptAsync` with script type (AzureCli, PowerShell, Bicep, Terraform)
- Guidance: Delegate to `IAiRemediationPlanGenerator.GetGuidanceAsync` returning explanation, plan, confidence, references
- Prioritization: Delegate to `IAiRemediationPlanGenerator.PrioritizeAsync` with business context string
- Manual guide: Parse finding remediation guidance text, extract steps with regex, assess skill level by control family (AC→Intermediate, SC→Advanced, CP→Intermediate)
- All AI methods: Check `_aiPlanGenerator?.IsAvailable == true`, fallback to deterministic results if unavailable

**Tests added to**: `AtoRemediationEngineTests.cs` — AI section (~15 tests)

**Estimated tests**: ~15 tests
**Dependencies**: Phase 2 (AI service), Phase 3 (engine class)

### Phase 8: DI Registration & MCP Tool Updates

**Goal**: Wire everything together — register services, update tools, update existing tests.

**Covers**: FR-001 (backward compatibility), FR-015 (service registration)

**Files modified**:
- `src/Ato.Copilot.Agents/Extensions/ServiceCollectionExtensions.cs` — Register `AtoRemediationEngine` as `IRemediationEngine`, register 6 supporting services as singletons, add `RemediationOptions` binding
- `src/Ato.Copilot.Agents/Compliance/Tools/ComplianceTools.cs` — Update `RemediationExecuteTool`, `ValidateRemediationTool`, `RemediationPlanTool` to use enhanced typed return values from new engine overloads
- `tests/Ato.Copilot.Tests.Unit/Services/RemediationEngineTests.cs` — Update class references from `RemediationEngine` to `AtoRemediationEngine`, update mock setup for new constructor dependencies

**Key implementation details**:
- DI registrations: All Singleton lifetime
  ```csharp
  services.AddSingleton<IAiRemediationPlanGenerator, AiRemediationPlanGenerator>();
  services.AddSingleton<IRemediationScriptExecutor, RemediationScriptExecutor>();
  services.AddSingleton<INistRemediationStepsService, NistRemediationStepsService>();
  services.AddSingleton<IAzureArmRemediationService, AzureArmRemediationService>();
  services.AddSingleton<IComplianceRemediationService, ComplianceRemediationService>();
  services.AddSingleton<IScriptSanitizationService, ScriptSanitizationService>();
  services.AddSingleton<IRemediationEngine, AtoRemediationEngine>();
  ```
- MCP tools: Preserve existing parameter schemas; enhance response payloads with typed models
- Backward compatibility: Existing 4 methods still work; tools can use enhanced overloads

**Tests**: Update existing 13 tests + integration verification
**Dependencies**: All previous phases

### Phase 9: Comprehensive Testing & Validation

**Goal**: Full test suite validation, edge case coverage, performance verification.

**Covers**: All SCs (SC-001 through SC-012)

**Activities**:
- Run full test suite — all existing 1,758 tests + ~150 new tests must pass
- Verify edge cases from spec (ARM throttling, deleted resources, failed snapshots, same-resource batch, already-remediated findings, destructive script rejection)
- Build verification — `dotnet build` clean with zero warnings
- SC verification matrix:
  | SC | Verified By |
  |----|-------------|
  | SC-001 | Plan generation test with 50+ findings, assert <5s |
  | SC-002 | Single remediation test with mock ARM, assert <2min |
  | SC-003 | Batch test with 10 findings + semaphore assert |
  | SC-004 | 3-tier fallthrough test with T1+T2 unavailable |
  | SC-005 | Rollback test with snapshot restore |
  | SC-006 | Filter accuracy tests (severity, family, auto-only) |
  | SC-007 | Risk calculation test with known inputs |
  | SC-008 | Kanban integration test with task status assertion |
  | SC-009 | 8 ARM operation tests with mock ArmClient |
  | SC-010 | Approval workflow test (approve + reject paths) |
  | SC-011 | Performance test with 1,000 findings |
  | SC-012 | Manual guide completeness assertion |

**Estimated tests**: Edge case tests ~15 (included in ~150 total)
**Dependencies**: All previous phases

## Key Design Decisions

### D1: Enhance vs. Replace RemediationEngine

**Decision**: Enhance (rename + extend). **Rationale**: Preserves backward compatibility with 5 existing consumers. The existing `GeneratePlanAsync` logic (~200 lines of real plan generation) is valuable and can be integrated. See [research.md](research.md) §1.

### D2: ComplianceFinding vs. AtoFinding

**Decision**: Use `ComplianceFinding` everywhere. **Rationale**: `AtoFinding` does not exist in the codebase. `ComplianceFinding` (27 properties) already has all remediation-related fields. See [research.md](research.md) §9.

### D3: Singleton DI with IDbContextFactory

**Decision**: All services registered as Singleton. **Rationale**: Matches existing pattern. `IDbContextFactory<AtoCopilotContext>` creates short-lived DbContext instances per operation. In-memory tracking (`ConcurrentDictionary`, `List`) persists across requests as required by spec. See [research.md](research.md) §5.

### D4: Optional AI via Nullable Constructor Parameter

**Decision**: `IChatClient? chatClient = null` in constructor. **Rationale**: Package is already referenced but unused. Nullable injection avoids DI registration requirements when AI is not configured. `IsAvailable` property gate ensures clean fallback path. See [research.md](research.md) §6.

### D5: Kanban Service as Optional Dependency

**Decision**: `IKanbanService? kanbanService = null` in constructor. **Rationale**: Engine works without kanban board. When available, post-execution updates are best-effort (log warnings on failure, don't block remediation result). See [research.md](research.md) §7.

### D6: Scheduling Without Background Timer

**Decision**: `ScheduleRemediationAsync` creates a record but does not execute automatically. **Rationale**: Background execution requires a hosted service pattern that adds complexity. For the current phase, scheduling is a record-keeping feature; the calling layer (MCP server or watchdog) is responsible for polling and triggering execution at the scheduled time.

## Risk Assessment

| Risk | Impact | Mitigation |
|------|--------|------------|
| ARM API rate limiting during batch | Medium | SemaphoreSlim limits concurrency; exponential backoff on 429 responses |
| Snapshot capture failure | Low | Execution continues; rollback marked unavailable; warning logged |
| IChatClient preview package instability | Low | All AI paths have deterministic fallbacks; AI is additive |
| Large finding sets (1,000+) causing memory pressure | Medium | Stream processing for plan generation; limit in-memory history to 30 days |
| Existing test breakage from RemediationEngine rename | Low | Phased approach — update tests in Phase 8 after engine is complete |
| Kanban service unavailable at runtime | Low | Optional dependency; null-check guards; best-effort updates |

## References

- [spec.md](spec.md) — Feature specification
- [research.md](research.md) — Phase 0 research findings
- [data-model.md](data-model.md) — Entity model definitions
- [contracts/IRemediationEngine.md](contracts/IRemediationEngine.md) — Interface contract
- [quickstart.md](quickstart.md) — Developer quickstart guide