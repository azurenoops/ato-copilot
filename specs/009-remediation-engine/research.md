# Research: ATO Remediation Engine

**Feature**: 009-remediation-engine | **Date**: 2026-02-24

## Research Areas

### 1. Existing RemediationEngine Architecture

**Decision**: Enhance the existing `RemediationEngine` rather than replacing it from scratch.

**Rationale**: The current engine (452 lines) has working plan generation, database persistence, batch orchestration, and 3 MCP tools that depend on `IRemediationEngine`. The simulated portions (apply-mode execution, validation) are clearly marked and isolated. Preserving the existing interface prevents breaking `KanbanService`, `ComplianceWatchService`, and the MCP tools.

**Alternatives Considered**:
- **Full replacement**: Discard `RemediationEngine`, create `AtoRemediationEngine` from scratch. Rejected because it would break all existing consumers without providing additional value.
- **Parallel engine**: Create `AtoRemediationEngine` alongside `RemediationEngine`. Rejected because it introduces confusion about which engine to use and complicates DI registration.

**Approach**: Rename `RemediationEngine` → `AtoRemediationEngine`, move to `Engines/Remediation/` directory, extend `IRemediationEngine` with 13 new methods (4→17 total), decompose internal logic into supporting services. Existing method signatures are preserved for backward compatibility.

### 2. IRemediationEngine Interface Extension Strategy

**Decision**: Extend `IRemediationEngine` in-place with new methods while preserving existing 4-method signatures.

**Rationale**: The existing interface is consumed by `KanbanService.ExecuteTaskRemediationAsync` (calls `ExecuteRemediationAsync`), `ComplianceWatchService.TryAutoRemediateAsync` (calls `BatchRemediateAsync`), and 3 MCP tools. These consumers use the current method signatures. Adding new methods is backward-compatible; changing existing signatures would require updating all consumers simultaneously.

**Current signatures to preserve**:
- `GeneratePlanAsync(subscriptionId, resourceGroupName?, ct)` → `RemediationPlan`
- `ExecuteRemediationAsync(findingId, applyRemediation, dryRun, ct)` → `string`
- `ValidateRemediationAsync(findingId, executionId?, subscriptionId?, ct)` → `string`
- `BatchRemediateAsync(subscriptionId?, severity?, family?, dryRun, ct)` → `string`

**New methods added** (13): `GenerateRemediationPlanAsync` (3 overloads), `ExecuteRemediationAsync` (new overload with options), `ExecuteBatchRemediationAsync`, `RollbackRemediationAsync`, `GetRemediationProgressAsync`, `GetRemediationHistoryAsync`, `AnalyzeRemediationImpactAsync`, `GenerateManualRemediationGuideAsync`, `GetActiveRemediationWorkflowsAsync`, `ProcessRemediationApprovalAsync`, `ScheduleRemediationAsync`, `GenerateRemediationScriptAsync`, `GetRemediationGuidanceAsync`, `PrioritizeFindingsWithAiAsync`.

### 3. 3-Tier Execution Pipeline Design

**Decision**: Progressive fallback: Tier 1 (AI Script) → Tier 2 (Compliance Remediation Service) → Tier 3 (Legacy ARM).

**Rationale**: Supports organizations at different maturity levels. AI-first orgs get LLM-generated scripts; structured orgs get deterministic ARM operations; all orgs get legacy fallback for foundational operations. The pipeline always produces a result.

**Alternatives Considered**:
- **Single-tier (ARM-only)**: Simpler but loses AI capability and structured remediation service benefits. Rejected.
- **Two-tier (AI + ARM)**: Loses the intermediate structured remediation layer. Rejected because the Compliance Remediation Service provides tested, deterministic operations that are more reliable than AI scripts for common scenarios.

### 4. ARM SDK Write Operations

**Decision**: Use `GenericResource.UpdateAsync` for property modifications and REST PUT/PATCH for provider-specific operations (diagnostic settings, alerts, policies).

**Rationale**: The existing `AzureResourceService` establishes read-only ARM patterns with caching. Write operations cannot be cached and require different error handling (optimistic concurrency, conflict detection). A separate `IAzureArmRemediationService` encapsulates ARM write operations with retry logic and snapshot capture.

**Existing patterns reused**:
- `ArmClient` singleton registered via `CoreServiceExtensions.RegisterArmClient()`
- `DefaultAzureCredential` with dual-cloud support
- Resource identification via `ResourceIdentifier`
- `_armClient.GetSubscriptionResource()` for subscription-scoped operations

**New patterns introduced**:
- `GenericResource.UpdateAsync(BinaryData)` for property updates
- REST PUT to `/providers/microsoft.insights/diagnosticSettings/` for diagnostic settings
- REST PUT to `/providers/Microsoft.Insights/scheduledQueryRules/` for alert rules
- REST PUT to `/providers/Microsoft.Authorization/policyAssignments/` for policy assignment
- Before/after snapshot capture via `GenericResource.GetAsync()` → serialize to JSON

### 5. DI Lifetime Strategy

**Decision**: Register `AtoRemediationEngine` and all supporting services as **Singleton** (matching existing pattern).

**Rationale**: The existing `RemediationEngine` is registered as Singleton and uses `IDbContextFactory` for short-lived DbContext instances. In-memory tracking dictionaries (`_activeRemediations`, `_remediationHistory`) are scoped to the engine instance, which is appropriate for Singleton lifetime where the tracking data persists across requests. The spec explicitly states "in-memory tracking is acceptable for the current phase."

**Alternatives Considered**:
- **Scoped**: Would reset tracking data per request, losing active remediation state. Rejected per spec requirement for persistent tracking.
- **Transient**: Same problem as Scoped plus additional overhead. Rejected.

### 6. IChatClient Integration

**Decision**: Accept `IChatClient?` as an optional nullable constructor parameter.

**Rationale**: The `Microsoft.Extensions.AI` package (9.4.0-preview) is already referenced in `Ato.Copilot.Agents.csproj` but has zero source-code usage. The `AiRemediationPlanGenerator` will use `IChatClient` when available and return null/fallback results when not. This matches the spec requirement for graceful degradation.

**Pattern**: Constructor injection with `IChatClient? chatClient = null`. The `IsAvailable` property checks `_chatClient != null`. If null, all AI methods return fallback results without logging errors.

### 7. Kanban Integration Architecture

**Decision**: Engine optionally depends on `IKanbanService?` and updates kanban tasks post-remediation when the service is available.

**Rationale**: The kanban integration is bidirectional:
- **Kanban → Engine**: Already works — `KanbanService.ExecuteTaskRemediationAsync` calls `IRemediationEngine.ExecuteRemediationAsync`. The enhanced engine provides real execution instead of simulation.
- **Engine → Kanban**: New — after successful remediation, the engine looks up kanban tasks by `FindingId` via `IKanbanService.GetTaskByLinkedAlertIdAsync` or by scanning board tasks mathing the finding's control ID / affected resources. On match, it calls `MoveTaskAsync` (→ InReview) and adds history entries.

**Integration points implemented**:
1. Post-execution kanban task status update (individual and bulk)
2. System comment on remediation success/failure
3. Evidence collection trigger via `CollectTaskEvidenceAsync`

### 8. Existing Test Pattern Alignment

**Decision**: Follow established InMemoryDbContextFactory + Mock + FluentAssertions pattern from RemediationEngineTests.cs.

**Rationale**: The existing test file (329 lines, 13 tests) uses `InMemoryDbContextFactory`, `Mock<IAtoComplianceEngine>`, and JSON parsing with `JsonDocument`. New tests will follow the same infrastructure pattern, adding mocks for the new service dependencies.

**Test infrastructure reused**:
- `InMemoryDbContextFactory` implementing `IDbContextFactory<AtoCopilotContext>`
- `CreateFinding()` helper for building test findings
- `SeedFindings()` / `SeedAssessment()` for database setup
- JSON result parsing via `JsonDocument.Parse(result)` for existing method tests
- FluentAssertions for typed model assertion on new methods

### 9. ComplianceFinding vs AtoFinding

**Decision**: Use `ComplianceFinding` throughout — `AtoFinding` does not exist in the codebase.

**Rationale**: The spec references `AtoFinding` but this class does not exist. `ComplianceFinding` (line 546 of ComplianceModels.cs, 27 properties) is the only finding class and already has all remediation-related properties (`RemediationGuidance`, `RemediationScript`, `AutoRemediable`, `RemediationType`, `RemediationTrackingStatus`, `RemediatedAt`, `RemediatedBy`). The new methods will accept `ComplianceFinding` where the spec says `AtoFinding`.

### 10. Model Extension Strategy

**Decision**: Add 15+ new model classes to ComplianceModels.cs and 3 new model files.

**Rationale**: The spec defines new entity types that don't exist in the codebase. These belong in `Ato.Copilot.Core/Models/Compliance/` following existing patterns. Smaller focused types (`RemediationScript`, `RemediationGuidance`, `PrioritizedFinding`) get their own files to keep ComplianceModels.cs manageable. Larger execution/workflow types stay in ComplianceModels.cs alongside existing `RemediationPlan` and `RemediationStep`.

**New types to add to ComplianceModels.cs**: `RemediationItem`, `RemediationExecution`, `RemediationExecutionStatus` (enum), `BatchRemediationResult`, `BatchRemediationSummary`, `RemediationValidationResult`, `ValidationCheck`, `RemediationRollbackResult`, `RollbackPlan`, `RemediationWorkflowStatus`, `RemediationApprovalResult`, `RemediationScheduleResult`, `RemediationProgress`, `RemediationHistory`, `RemediationMetric`, `RemediationActivity`, `RemediationImpactAnalysis`, `ResourceImpact`, `ManualRemediationGuide`, `ImplementationTimeline`, `TimelinePhase`, `RemediationExecutionOptions`, `BatchRemediationOptions`, `RemediationPlanOptions`.

**New model files**: `RemediationScript.cs`, `RemediationGuidance.cs`, `PrioritizedFinding.cs`.
