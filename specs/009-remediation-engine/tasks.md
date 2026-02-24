# Tasks: ATO Remediation Engine

**Input**: Design documents from `/specs/009-remediation-engine/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

**Tests**: Included — the spec requires comprehensive testing with xUnit + FluentAssertions + Moq, following the established InMemoryDbContextFactory pattern.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story. 11 user stories (P1–P4) mapped from spec.md.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization — create directories, enums, and options types that all phases depend on

- [X] T001 Create Engines/Remediation/ directory under src/Ato.Copilot.Agents/Compliance/Services/
- [X] T002 Create Engines/Remediation/ directory under tests/Ato.Copilot.Tests.Unit/Services/
- [X] T003 [P] Add RemediationPriority enum (P0–P4) to src/Ato.Copilot.Core/Models/Compliance/ComplianceModels.cs
- [X] T004 [P] Add RemediationExecutionStatus enum (Pending, Approved, InProgress, Completed, Failed, RolledBack, Rejected, Cancelled, Scheduled) to src/Ato.Copilot.Core/Models/Compliance/ComplianceModels.cs
- [X] T005 [P] Add ScriptType enum (AzureCli, PowerShell, Bicep, Terraform) to src/Ato.Copilot.Core/Models/Compliance/ComplianceModels.cs
- [X] T006 [P] Add RemediationExecutionOptions class (DryRun, RequireApproval, AutoValidate, AutoRollbackOnFailure, UseAiScript) to src/Ato.Copilot.Core/Models/Compliance/ComplianceModels.cs
- [X] T007 [P] Add BatchRemediationOptions class (MaxConcurrentRemediations, FailFast, ContinueOnError, DryRun) to src/Ato.Copilot.Core/Models/Compliance/ComplianceModels.cs
- [X] T008 [P] Add RemediationPlanOptions class (MinSeverity, IncludeFamilies, ExcludeFamilies, AutomatableOnly, GroupByResource) to src/Ato.Copilot.Core/Models/Compliance/ComplianceModels.cs
- [X] T009 Add RemediationOptions configuration sub-class (MaxConcurrentRemediations, ScriptTimeoutSeconds, MaxRetries, RequireApproval, AutoValidate, AutoRollbackOnFailure, UseAiScript) and add Remediation property to ComplianceAgentOptions in src/Ato.Copilot.Agents/Compliance/Configuration/ComplianceAgentOptions.cs

**Checkpoint**: Directory structure, enums, and option types ready for all subsequent phases

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core interfaces, models, and supporting services that MUST be complete before any user story engine work can begin

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

### Interfaces

- [X] T010 [P] Add 16 new method signatures (14 unique new method names, including 3 GenerateRemediationPlanAsync overloads) to IRemediationEngine interface — GenerateRemediationPlanAsync (3 overloads), ExecuteRemediationAsync typed overload, ValidateRemediationAsync typed overload, ExecuteBatchRemediationAsync, RollbackRemediationAsync, GetRemediationProgressAsync, GetRemediationHistoryAsync, AnalyzeRemediationImpactAsync, GenerateManualRemediationGuideAsync, GetActiveRemediationWorkflowsAsync, ProcessRemediationApprovalAsync, ScheduleRemediationAsync, GenerateRemediationScriptAsync, GetRemediationGuidanceAsync, PrioritizeFindingsWithAiAsync — preserving existing 4 methods (18 unique method names, 21 total signatures including overloads) in src/Ato.Copilot.Core/Interfaces/Compliance/IComplianceInterfaces.cs
- [X] T011 [P] Create IAiRemediationPlanGenerator interface (IsAvailable, GenerateScriptAsync, GetGuidanceAsync, PrioritizeAsync, GenerateEnhancedPlanAsync) in src/Ato.Copilot.Core/Interfaces/Compliance/IAiRemediationPlanGenerator.cs
- [X] T012 [P] Create IRemediationScriptExecutor interface (ExecuteScriptAsync) in src/Ato.Copilot.Core/Interfaces/Compliance/IRemediationScriptExecutor.cs
- [X] T013 [P] Create INistRemediationStepsService interface (GetRemediationSteps, ParseStepsFromGuidance, GetSkillLevel) in src/Ato.Copilot.Core/Interfaces/Compliance/INistRemediationStepsService.cs
- [X] T014 [P] Create IAzureArmRemediationService interface (CaptureResourceSnapshotAsync, ExecuteArmRemediationAsync, RestoreFromSnapshotAsync) in src/Ato.Copilot.Core/Interfaces/Compliance/IAzureArmRemediationService.cs
- [X] T015 [P] Create IComplianceRemediationService interface (ExecuteStructuredRemediationAsync, CanHandle) in src/Ato.Copilot.Core/Interfaces/Compliance/IComplianceRemediationService.cs
- [X] T016 [P] Create IScriptSanitizationService interface (IsSafe, GetViolations) in src/Ato.Copilot.Core/Interfaces/Compliance/IScriptSanitizationService.cs

### Shared Models

- [X] T017 [P] Add RemediationExecution model (Id, FindingId, SubscriptionId, Status, BeforeSnapshot, AfterSnapshot, BackupId, StartedAt, CompletedAt, Duration, StepsExecuted, ChangesApplied, TierUsed, Error, ApprovedBy, ApprovedAt, RejectedBy, RejectedAt, RejectionReason, DryRun, Options) to src/Ato.Copilot.Core/Models/Compliance/ComplianceModels.cs
- [X] T018 [P] Add RemediationActivity model (Id, ExecutionId, Action, Details, Timestamp) to src/Ato.Copilot.Core/Models/Compliance/ComplianceModels.cs
- [X] T019 [P] Add RiskMetrics model (CurrentRiskScore, ProjectedRiskScore, RiskReductionPercentage) to src/Ato.Copilot.Core/Models/Compliance/ComplianceModels.cs

### Supporting Services

- [X] T020 [P] Implement NistRemediationStepsService — inject ILogger<NistRemediationStepsService>, curated NIST steps by control family (AC, AU, CM, CP, IA, IR, MA, MP, PE, PL, PS, RA, SA, SC, SI), regex step parsing from guidance text using action verb patterns (Enable, Configure, Implement, Review, Update, Deploy, Monitor, Verify, Create, Remove, Restrict, Set, Add, Apply, Disable), skill level mapping by family, log step lookups and parse operations in src/Ato.Copilot.Agents/Compliance/Services/Engines/Remediation/NistRemediationStepsService.cs
- [X] T021 [P] Implement ScriptSanitizationService — inject ILogger<ScriptSanitizationService>, validate scripts against safe command patterns, reject destructive commands (resource deletion, subscription-wide changes, az group delete, Remove-AzResourceGroup, bicep destroy patterns, terraform destroy patterns), log sanitization checks and rejections in src/Ato.Copilot.Agents/Compliance/Services/Engines/Remediation/ScriptSanitizationService.cs
- [X] T022 [P] Implement AiRemediationPlanGenerator — IChatClient-based script generation in AzureCli/PowerShell/Bicep/Terraform, natural-language guidance with confidence score, AI prioritization with business context, graceful null fallback when IChatClient unavailable in src/Ato.Copilot.Agents/Compliance/Services/Engines/Remediation/AiRemediationPlanGenerator.cs
- [X] T023 [P] Implement RemediationScriptExecutor — inject ILogger<RemediationScriptExecutor>, script execution with 5-minute timeout, 3-retry limit, sanitization gate via IScriptSanitizationService, execution status tracking, log execution attempts and retries in src/Ato.Copilot.Agents/Compliance/Services/Engines/Remediation/RemediationScriptExecutor.cs
- [X] T024 [P] Implement AzureArmRemediationService — inject ILogger<AzureArmRemediationService>, 8 legacy ARM operations (TLS version update, diagnostic settings, alert rules, log retention, encryption, NSG configuration, policy assignment, HTTPS enforcement), before/after snapshot capture via GenericResource.GetAsync → JSON, GenericResource.UpdateAsync for property changes, REST PUT for provider-specific operations, log each ARM operation and snapshot capture in src/Ato.Copilot.Agents/Compliance/Services/Engines/Remediation/AzureArmRemediationService.cs
- [X] T025 [P] Implement ComplianceRemediationService — inject ILogger<ComplianceRemediationService>, Tier 2 structured remediation orchestration handling findings with predefined remediation templates (property-correction logic for known compliance patterns), CanHandle returns true when finding.RemediationType matches a supported template, delegate to IRemediationScriptExecutor for script-based fixing, log orchestration decisions and tier selection in src/Ato.Copilot.Agents/Compliance/Services/Engines/Remediation/ComplianceRemediationService.cs

### Supporting Service Tests

- [X] T026 [P] Write NistRemediationStepsService tests — curated steps lookup for AC/AU/CM/CP/IA/SC families, regex parsing for numbered steps and bulleted lists, action verb extraction, skill level mapping (AC→Intermediate, SC→Advanced, CP→Intermediate), empty input handling in tests/Ato.Copilot.Tests.Unit/Services/Engines/Remediation/NistRemediationStepsServiceTests.cs
- [X] T027 [P] Write ScriptSanitizationService tests — safe script passes validation, destructive commands rejected (az group delete, Remove-AzResourceGroup, bicep destroy, terraform destroy), multi-line scripts checked, empty script handling, GetViolations returns specific violations in tests/Ato.Copilot.Tests.Unit/Services/Engines/Remediation/ScriptSanitizationServiceTests.cs
- [X] T028 [P] Write AiRemediationPlanGenerator tests — script generation with mock IChatClient, guidance generation, AI prioritization with business context, null IChatClient returns fallback results, IsAvailable returns false when null in tests/Ato.Copilot.Tests.Unit/Services/Engines/Remediation/AiRemediationPlanGeneratorTests.cs
- [X] T029 [P] Write RemediationScriptExecutor tests — successful execution, timeout handling, retry on failure up to 3x, sanitization rejection blocks execution, execution tracking in tests/Ato.Copilot.Tests.Unit/Services/Engines/Remediation/RemediationScriptExecutorTests.cs
- [X] T030 [P] Write AzureArmRemediationService tests — snapshot capture, TLS update operation, diagnostic settings operation, HTTPS enforcement, restore from snapshot, resource not found handling, all 8 ARM operations in tests/Ato.Copilot.Tests.Unit/Services/Engines/Remediation/AzureArmRemediationServiceTests.cs
- [X] T031 [P] Write ComplianceRemediationService tests — CanHandle returns true for supported types, structured execution delegates to script executor, unsupported finding type returns failure, execution status tracking in tests/Ato.Copilot.Tests.Unit/Services/Engines/Remediation/ComplianceRemediationServiceTests.cs

**Checkpoint**: All interfaces defined, 6 supporting services implemented and tested, foundation ready for engine implementation

---

## Phase 3: User Story 1 — Generate Remediation Plan from Assessment (Priority: P1) 🎯 MVP

**Goal**: Security engineers can generate a prioritized remediation plan from compliance findings with filtering, timeline, risk scoring, and executive summary

**Independent Test**: Generate a plan from 50+ findings and verify prioritized items, duration estimates, timeline phases, and risk reduction calculations

### Models for User Story 1

- [X] T032 [P] [US1] Add RemediationItem model (Id, Finding, Priority, PriorityLabel, EstimatedDuration, Steps, ValidationSteps, RollbackPlan, Dependencies, IsAutoRemediable, RemediationType, AffectedResourceId) to src/Ato.Copilot.Core/Models/Compliance/ComplianceModels.cs
- [X] T033 [P] [US1] Add ImplementationTimeline model (Phases, TotalEstimatedDuration, StartDate, EndDate) and TimelinePhase model (Name, Priority, Items, StartDate, EndDate, EstimatedDuration) to src/Ato.Copilot.Core/Models/Compliance/ComplianceModels.cs
- [X] T034 [P] [US1] Add RemediationExecutiveSummary model (TotalFindings, CriticalCount, HighCount, MediumCount, LowCount, AutoRemediableCount, ManualCount, TotalEstimatedEffort, ProjectedRiskReduction) to src/Ato.Copilot.Core/Models/Compliance/ComplianceModels.cs
- [X] T035 [US1] Add new properties (Items, Timeline, ExecutiveSummary, RiskMetrics, GroupByResource, Filters) to existing RemediationPlan class in src/Ato.Copilot.Core/Models/Compliance/ComplianceModels.cs

### Engine — Plan Generation

- [X] T036 [US1] Create AtoRemediationEngine class with constructor accepting IAtoComplianceEngine, IDbContextFactory<AtoCopilotContext>, IAzureArmRemediationService, IAiRemediationPlanGenerator, IComplianceRemediationService, IRemediationScriptExecutor, INistRemediationStepsService, IScriptSanitizationService, ComplianceAgentOptions, IKanbanService?, ILogger<AtoRemediationEngine> — initialize ConcurrentDictionary for active remediations, List for history, SemaphoreSlim for concurrency — delete or archive old RemediationEngine.cs in src/Ato.Copilot.Agents/Compliance/Services/Engines/Remediation/AtoRemediationEngine.cs
- [X] T037 [US1] Implement GeneratePlanAsync (existing signature — subscriptionId, resourceGroupName, ct) preserving backward compatibility — query latest assessment, build plan with steps — in src/Ato.Copilot.Agents/Compliance/Services/Engines/Remediation/AtoRemediationEngine.cs
- [X] T038 [US1] Implement GenerateRemediationPlanAsync (IEnumerable<ComplianceFinding>, RemediationPlanOptions, ct) — severity-to-priority mapping (Critical→P0, High→P1, Medium→P2, Low→P3, Other→P4), risk scoring (Critical=10, High=7.5, Medium=5, Low=2.5, Other=1), duration estimation (Auto 10-30min, Manual 30min-4hr), plan sorting (Severity desc → AutoRemediable first → Duration asc), filter support (MinSeverity, IncludeFamilies, ExcludeFamilies, AutomatableOnly, GroupByResource), build Items/Timeline/ExecutiveSummary/RiskMetrics in src/Ato.Copilot.Agents/Compliance/Services/Engines/Remediation/AtoRemediationEngine.cs
- [X] T039 [US1] Implement GenerateRemediationPlanAsync (ComplianceFinding, ct) — single-finding plan with 3-tier fallback: AI plan via IAiRemediationPlanGenerator, then NIST steps via INistRemediationStepsService, then manual parsing of finding guidance text in src/Ato.Copilot.Agents/Compliance/Services/Engines/Remediation/AtoRemediationEngine.cs
- [X] T040 [US1] Implement GenerateRemediationPlanAsync (subscriptionId, RemediationPlanOptions, ct) — query findings from compliance engine then delegate to IEnumerable overload in src/Ato.Copilot.Agents/Compliance/Services/Engines/Remediation/AtoRemediationEngine.cs
- [X] T041 [US1] Implement private helpers: MapSeverityToPriority, CalculateRiskScore, EstimateDuration, BuildTimeline (5 phases: Immediate/24Hours/Week1/Month1/Backlog), BuildExecutiveSummary, GroupFindingsByResource in src/Ato.Copilot.Agents/Compliance/Services/Engines/Remediation/AtoRemediationEngine.cs

### Tests for User Story 1

- [X] T042 [P] [US1] Write plan generation tests — plan from 50+ findings sorted by priority, severity filter produces correct subset, control family filter produces correct subset, AutomatableOnly filter, GroupByResource grouping, executive summary counts, risk reduction calculation accuracy to within 1%, timeline has 5 phases, duration estimates within expected ranges, empty findings produces empty plan, single-finding 3-tier fallback in tests/Ato.Copilot.Tests.Unit/Services/Engines/Remediation/AtoRemediationEngineTests.cs

**Checkpoint**: Plan generation fully functional — can generate prioritized, filtered, risk-scored plans from compliance findings

---

## Phase 4: User Story 2 — Execute Single-Finding Remediation (Priority: P1)

**Goal**: Security engineers can execute a single-finding remediation through the 3-tier pipeline with before/after snapshots, dry-run support, and execution tracking

**Independent Test**: Execute remediation against a mock Azure resource, verify snapshots captured, 3-tier pipeline attempted in order, execution tracked with status transitions, backup ID returned

### Implementation for User Story 2

- [X] T043 [US2] Implement ExecuteRemediationAsync (existing signature — findingId, applyRemediation, dryRun, ct) preserving backward compatibility returning JSON string — check EnableAutomatedRemediation gate, lookup finding, execute through 3-tier pipeline, track in ConcurrentDictionary in src/Ato.Copilot.Agents/Compliance/Services/Engines/Remediation/AtoRemediationEngine.cs
- [X] T044 [US2] Implement ExecuteRemediationAsync (findingId, RemediationExecutionOptions, ct) typed overload — capture before-snapshot via IAzureArmRemediationService.CaptureResourceSnapshotAsync, attempt Tier 1 (AI script via IAiRemediationPlanGenerator + IRemediationScriptExecutor), fallback Tier 2 (IComplianceRemediationService), fallback Tier 3 (IAzureArmRemediationService), capture after-snapshot, track execution with RemediationExecution, return typed result in src/Ato.Copilot.Agents/Compliance/Services/Engines/Remediation/AtoRemediationEngine.cs
- [X] T045 [US2] Implement dry-run mode — walk full 3-tier pipeline without applying changes, return preview of what would change with DryRun=true on execution record in src/Ato.Copilot.Agents/Compliance/Services/Engines/Remediation/AtoRemediationEngine.cs
- [X] T046 [US2] Implement EnableAutomatedRemediation gate check — block all automated execution when disabled, return failure message suggesting manual remediation, continue to allow plan generation and script generation in src/Ato.Copilot.Agents/Compliance/Services/Engines/Remediation/AtoRemediationEngine.cs
- [X] T047 [US2] Implement RequireApproval flow — when options.RequireApproval=true, create execution with status Pending, return immediately without executing in src/Ato.Copilot.Agents/Compliance/Services/Engines/Remediation/AtoRemediationEngine.cs
- [X] T048 [US2] Implement non-auto-remediable finding handling — when finding.AutoRemediable=false, return error and automatically offer manual guidance via GenerateManualRemediationGuideAsync in src/Ato.Copilot.Agents/Compliance/Services/Engines/Remediation/AtoRemediationEngine.cs

### Tests for User Story 2

- [X] T049 [P] [US2] Write execution tests — successful 3-tier execution with snapshots (assert completes within 2 minutes), dry-run returns preview without changes, EnableAutomatedRemediation=false blocks execution, RequireApproval creates Pending execution, non-auto-remediable returns error with manual guidance, Tier 1 falls to Tier 2 when IChatClient is null or AI returns null/throws, Tier 2 falls to Tier 3 when CanHandle() returns false or throws, all tiers fail returns failure, execution tracking records status transitions, backup ID returned on success in tests/Ato.Copilot.Tests.Unit/Services/Engines/Remediation/AtoRemediationEngineTests.cs

**Checkpoint**: Single-finding remediation fully functional with 3-tier pipeline, snapshots, dry-run, and execution tracking

---

## Phase 5: User Story 5 — Validation and Rollback (Priority: P2)

**Goal**: After execution, validate that the fix worked and rollback if it caused problems

**Independent Test**: Execute a remediation, validate it (check status, steps, changes), rollback and verify resource restored from snapshot

### Models for User Story 5

- [X] T050 [P] [US5] Add RemediationValidationResult model (ExecutionId, IsValid, Checks, FailureReason, ValidatedAt) and ValidationCheck model (Name, Passed, ExpectedValue, ActualValue, Details) to src/Ato.Copilot.Core/Models/Compliance/ComplianceModels.cs
- [X] T051 [P] [US5] Add RemediationRollbackResult model (ExecutionId, Success, RollbackSteps, RestoredSnapshot, Error, RolledBackAt) and RollbackPlan model (Description, Steps, RequiresSnapshot, EstimatedDuration) to src/Ato.Copilot.Core/Models/Compliance/ComplianceModels.cs

### Implementation for User Story 5

- [X] T052 [US5] Implement ValidateRemediationAsync (existing signature — findingId, executionId, subscriptionId, ct) preserving backward compatibility returning JSON string in src/Ato.Copilot.Agents/Compliance/Services/Engines/Remediation/AtoRemediationEngine.cs
- [X] T053 [US5] Implement ValidateRemediationAsync typed overload (executionId, ct) returning RemediationValidationResult — check execution status equals Completed, steps count > 0, changes were applied, return RemediationValidationResult with per-check details. The legacy string-returning overload (T052) delegates to this method and serializes the result in src/Ato.Copilot.Agents/Compliance/Services/Engines/Remediation/AtoRemediationEngine.cs
- [X] T054 [US5] Implement RollbackRemediationAsync — lookup execution by ID, verify BeforeSnapshot exists, restore resource via IAzureArmRemediationService.RestoreFromSnapshotAsync, record rollback steps, update execution status to RolledBack, return RemediationRollbackResult in src/Ato.Copilot.Agents/Compliance/Services/Engines/Remediation/AtoRemediationEngine.cs
- [X] T055 [US5] Implement auto-validate and auto-rollback flow — when options.AutoValidate=true post-execution, run validation; when validation fails and options.AutoRollbackOnFailure=true, automatically rollback in src/Ato.Copilot.Agents/Compliance/Services/Engines/Remediation/AtoRemediationEngine.cs

### Tests for User Story 5

- [X] T056 [P] [US5] Write validation and rollback tests — completed execution validates successfully, failed execution validates as invalid, rollback restores from before-snapshot (assert completes within 1 minute), rollback without snapshot returns failure, auto-validate triggers post-execution, auto-rollback triggers on validation failure, execution status transitions to RolledBack in tests/Ato.Copilot.Tests.Unit/Services/Engines/Remediation/AtoRemediationEngineTests.cs

**Checkpoint**: Validation and rollback fully functional with auto-validate and auto-rollback support

---

## Phase 6: User Story 3 — Batch Remediation with Concurrency Control (Priority: P2)

**Goal**: Execute multiple findings in parallel with SemaphoreSlim concurrency control, FailFast, and aggregate reporting

**Independent Test**: Submit batch of 10 findings, verify semaphore limits concurrency to 3, FailFast cancels remaining, batch summary has correct counts and risk reduction

### Models for User Story 3

- [X] T057 [P] [US3] Add BatchRemediationResult model (BatchId, Executions, SuccessCount, FailureCount, CancelledCount, SkippedCount, Summary, StartedAt, CompletedAt, Duration, Options) to src/Ato.Copilot.Core/Models/Compliance/ComplianceModels.cs
- [X] T058 [P] [US3] Add BatchRemediationSummary model (SuccessRate, CriticalFindingsRemediated, HighFindingsRemediated, MediumFindingsRemediated, LowFindingsRemediated, EstimatedRiskReduction, ControlFamiliesAffected, TotalDuration) to src/Ato.Copilot.Core/Models/Compliance/ComplianceModels.cs

### Implementation for User Story 3

- [X] T059 [US3] Implement BatchRemediateAsync (existing signature — subscriptionId, severity, controlFamily, dryRun, ct) preserving backward compatibility returning JSON string in src/Ato.Copilot.Agents/Compliance/Services/Engines/Remediation/AtoRemediationEngine.cs
- [X] T060 [US3] Implement ExecuteBatchRemediationAsync (IEnumerable<string> findingIds, BatchRemediationOptions, ct) — SemaphoreSlim with MaxConcurrentRemediations, FailFast mode with linked CancellationTokenSource that cancels remaining on first failure, ContinueOnError mode catching per-finding exceptions, skip non-auto-remediable findings, aggregate BatchRemediationResult with counts and summary in src/Ato.Copilot.Agents/Compliance/Services/Engines/Remediation/AtoRemediationEngine.cs
- [X] T061 [US3] Implement batch summary calculation — SuccessRate, severity counts from completed executions, risk reduction using severity weights, unique ControlFamiliesAffected, total duration in src/Ato.Copilot.Agents/Compliance/Services/Engines/Remediation/AtoRemediationEngine.cs

### Tests for User Story 3

- [X] T062 [P] [US3] Write batch remediation tests — 10 findings with MaxConcurrent=3 limits concurrency (assert batch of 10 completes within 5 minutes), FailFast=true cancels remaining on first failure, ContinueOnError=true aggregates all results, non-auto-remediable skipped, batch summary has correct counts and SuccessRate, severity counts accurate, risk reduction calculated, empty batch returns empty result in tests/Ato.Copilot.Tests.Unit/Services/Engines/Remediation/AtoRemediationEngineTests.cs

**Checkpoint**: Batch remediation with concurrency control, FailFast, and aggregate reporting fully functional

---

## Phase 7: User Story 4 — Kanban Task Remediation Integration (Priority: P2)

**Goal**: Post-remediation, automatically update linked kanban tasks — advance to InReview on success, add system comments on failure, trigger evidence collection

**Independent Test**: Execute remediation for a finding with a linked kanban task, verify task advances to InReview, history entry recorded, evidence collection triggered

### Implementation for User Story 4

- [X] T063 [US4] Implement post-execution kanban task sync — after successful individual remediation, lookup linked kanban task by FindingId via IKanbanService?, advance task to InReview, add RemediationAttempt history entry with execution details in src/Ato.Copilot.Agents/Compliance/Services/Engines/Remediation/AtoRemediationEngine.cs
- [X] T064 [US4] Implement post-batch kanban bulk advancement — after batch completion, bulk-advance all linked kanban tasks for successful executions, add history entries per task in src/Ato.Copilot.Agents/Compliance/Services/Engines/Remediation/AtoRemediationEngine.cs
- [X] T065 [US4] Implement kanban failure handling — on remediation failure, add system comment to linked kanban task with error details, keep task in current status in src/Ato.Copilot.Agents/Compliance/Services/Engines/Remediation/AtoRemediationEngine.cs
- [X] T066 [US4] Implement evidence collection trigger — after successful remediation, call CollectTaskEvidenceAsync on linked kanban task via IKanbanService in src/Ato.Copilot.Agents/Compliance/Services/Engines/Remediation/AtoRemediationEngine.cs
- [X] T067 [US4] Implement null IKanbanService guard — all kanban integration is best-effort; when IKanbanService is null, skip all kanban operations with no errors; when kanban operations fail, log warning but don't block remediation result in src/Ato.Copilot.Agents/Compliance/Services/Engines/Remediation/AtoRemediationEngine.cs

### Tests for User Story 4

- [X] T068 [P] [US4] Write kanban integration tests — successful remediation advances task to InReview (assert kanban update within 5 seconds of remediation completion), batch success bulk-advances tasks, failure adds system comment, evidence collection triggered, null IKanbanService skips silently, kanban operation failure logs warning but returns success, task without RemediationScript returns error in tests/Ato.Copilot.Tests.Unit/Services/Engines/Remediation/AtoRemediationEngineTests.cs

**Checkpoint**: Kanban integration fully functional with bidirectional sync, failure handling, and evidence collection

---

## Phase 8: User Story 6 — Approval Workflow (Priority: P3)

**Goal**: High-risk remediations enter Pending state, await explicit approval/rejection, approved remediations proceed to execution

**Independent Test**: Request remediation with RequireApproval=true, verify Pending state, approve and verify execution triggers, reject and verify comments recorded

### Models for User Story 6

- [X] T069 [P] [US6] Add RemediationApprovalResult model (ExecutionId, Approved, ApproverName, Comments, ProcessedAt, ExecutionTriggered) to src/Ato.Copilot.Core/Models/Compliance/ComplianceModels.cs
- [X] T070 [P] [US6] Add RemediationWorkflowStatus model (SubscriptionId, PendingApprovals, InProgressExecutions, RecentlyCompleted, RetrievedAt) to src/Ato.Copilot.Core/Models/Compliance/ComplianceModels.cs

### Implementation for User Story 6

- [X] T071 [US6] Implement ProcessRemediationApprovalAsync — lookup pending execution by ID, if approve=true set status to Approved and trigger ExecuteRemediationAsync, if approve=false set status to Rejected recording approverName and comments, return RemediationApprovalResult in src/Ato.Copilot.Agents/Compliance/Services/Engines/Remediation/AtoRemediationEngine.cs
- [X] T072 [US6] Implement GetActiveRemediationWorkflowsAsync — query _activeRemediations for pending, in-progress, and recently completed (last 24h) executions, return RemediationWorkflowStatus in src/Ato.Copilot.Agents/Compliance/Services/Engines/Remediation/AtoRemediationEngine.cs

### Tests for User Story 6

- [X] T073 [P] [US6] Write approval workflow tests — RequireApproval creates Pending execution, approval triggers execution, rejection records approver and comments without executing, GetActiveWorkflows returns pending/in-progress/recent, approval of non-pending execution returns error in tests/Ato.Copilot.Tests.Unit/Services/Engines/Remediation/AtoRemediationEngineTests.cs

**Checkpoint**: Approval workflow functional — remediations can be gated behind explicit approval

---

## Phase 9: User Story 7 — Impact Analysis (Priority: P3)

**Goal**: Pre-execution risk analysis with severity-weighted scores, per-resource impacts, and actionable recommendations

**Independent Test**: Submit mixed-severity findings, verify risk scores, per-resource impacts, and recommendations

### Models for User Story 7

- [X] T074 [P] [US7] Add RemediationImpactAnalysis model (RiskMetrics, TotalFindingsAnalyzed, AutoRemediableCount, ManualCount, ResourceImpacts, Recommendations, AnalyzedAt) to src/Ato.Copilot.Core/Models/Compliance/ComplianceModels.cs
- [X] T075 [P] [US7] Add ResourceImpact model (ResourceId, ResourceType, FindingsCount, ProposedChanges, RiskLevel) to src/Ato.Copilot.Core/Models/Compliance/ComplianceModels.cs

### Implementation for User Story 7

- [X] T076 [US7] Implement AnalyzeRemediationImpactAsync — calculate current risk score (severity-weighted sum: Critical=10, High=7.5, Medium=5, Low=2.5, Other=1), projected risk (non-remediable findings only), risk reduction percentage, group findings by Azure resource with ResourceImpact details, generate recommendations distinguishing automatable vs manual in src/Ato.Copilot.Agents/Compliance/Services/Engines/Remediation/AtoRemediationEngine.cs

### Tests for User Story 7

- [X] T077 [P] [US7] Write impact analysis tests — risk score calculation accuracy to within 1%, per-resource impact grouping for 5 unique resources, recommendations distinguish auto vs manual, empty findings returns zero scores, all Critical findings produce maximum risk score in tests/Ato.Copilot.Tests.Unit/Services/Engines/Remediation/AtoRemediationEngineTests.cs

**Checkpoint**: Impact analysis functional — operators can assess risk before executing remediation

---

## Phase 10: User Story 8 — AI-Enhanced Remediation (Priority: P3)

**Goal**: AI script generation in AzureCli/PowerShell/Bicep/Terraform, natural-language guidance, and business-context prioritization with graceful fallback

**Independent Test**: Inject mock IChatClient, generate script, verify sanitized; confirm fallback when IChatClient is null

### Models for User Story 8

- [X] T078 [P] [US8] Create RemediationScript model (Content, ScriptType, Description, Parameters, EstimatedDuration, IsSanitized) in src/Ato.Copilot.Core/Models/Compliance/RemediationScript.cs
- [X] T079 [P] [US8] Create RemediationGuidance model (FindingId, Explanation, TechnicalPlan, ConfidenceScore, References, GeneratedAt) in src/Ato.Copilot.Core/Models/Compliance/RemediationGuidance.cs
- [X] T080 [P] [US8] Create PrioritizedFinding model (Finding, AiPriority, Justification, BusinessImpact, OriginalPriority) in src/Ato.Copilot.Core/Models/Compliance/PrioritizedFinding.cs

### Implementation for User Story 8

- [X] T081 [US8] Implement GenerateRemediationScriptAsync — delegate to IAiRemediationPlanGenerator.GenerateScriptAsync with ScriptType, validate via IScriptSanitizationService, fallback to NIST steps or manual parsing when AI unavailable in src/Ato.Copilot.Agents/Compliance/Services/Engines/Remediation/AtoRemediationEngine.cs
- [X] T082 [US8] Implement GetRemediationGuidanceAsync — delegate to IAiRemediationPlanGenerator.GetGuidanceAsync, return explanation/plan/confidence/references, fallback to deterministic guidance from finding text when AI unavailable in src/Ato.Copilot.Agents/Compliance/Services/Engines/Remediation/AtoRemediationEngine.cs
- [X] T083 [US8] Implement PrioritizeFindingsWithAiAsync — delegate to IAiRemediationPlanGenerator.PrioritizeAsync with business context, return PrioritizedFinding list with AI priority/justification/impact, fallback to severity-based priority when AI unavailable in src/Ato.Copilot.Agents/Compliance/Services/Engines/Remediation/AtoRemediationEngine.cs

### Tests for User Story 8

- [X] T084 [P] [US8] Write AI-enhanced tests — script generation with mock IChatClient produces valid script, sanitization rejects unsafe scripts, null IChatClient falls back to NIST steps, guidance returns explanation/plan/confidence, prioritization with business context returns justified priorities, fallback priority matches severity-based priority in tests/Ato.Copilot.Tests.Unit/Services/Engines/Remediation/AtoRemediationEngineTests.cs

**Checkpoint**: AI-enhanced capabilities functional with clean fallback when AI is unavailable

---

## Phase 11: User Story 9 — Manual Remediation Guidance (Priority: P3)

**Goal**: Generate comprehensive manual guides for non-automatable findings with steps, prerequisites, skill level, permissions, validation, and rollback

**Independent Test**: Request manual guide for non-automatable finding, verify all sections populated with contextually relevant content

### Models for User Story 9

- [X] T085 [P] [US9] Add ManualRemediationGuide model (FindingId, ControlId, Title, Steps, Prerequisites, SkillLevel, RequiredPermissions, ValidationSteps, RollbackPlan, EstimatedDuration, References) to src/Ato.Copilot.Core/Models/Compliance/ComplianceModels.cs

### Implementation for User Story 9

- [X] T086 [US9] Implement GenerateManualRemediationGuideAsync — parse finding remediation guidance text to extract steps using INistRemediationStepsService.ParseStepsFromGuidance, assess skill level by control family via INistRemediationStepsService.GetSkillLevel (AC→Intermediate, SC→Advanced, CP→Intermediate), generate prerequisites based on finding type, determine required permissions, add validation steps, generate rollback plan, estimate duration in src/Ato.Copilot.Agents/Compliance/Services/Engines/Remediation/AtoRemediationEngine.cs

### Tests for User Story 9

- [X] T087 [P] [US9] Write manual guidance tests — guide for non-automatable finding has all sections, skill level matches control family (AC→Intermediate, SC→Advanced, CP→Intermediate), steps parsed from guidance text using regex, empty guidance returns generic steps, prerequisites populated based on finding type in tests/Ato.Copilot.Tests.Unit/Services/Engines/Remediation/AtoRemediationEngineTests.cs

**Checkpoint**: Manual remediation guidance functional for non-automatable findings

---

## Phase 12: User Story 10 — Progress Tracking and History (Priority: P4)

**Goal**: Track active remediations and maintain execution history with metrics — progress snapshots, date-range history, lifecycle tracking

**Independent Test**: Execute multiple remediations, query progress (verify counts, completion rate), query history (verify date-range filtering, metrics)

### Models for User Story 10

- [X] T088 [P] [US10] Add RemediationProgress model (SubscriptionId, CompletedCount, InProgressCount, FailedCount, PendingCount, TotalCount, CompletionRate, AverageRemediationTime, Period, AsOf) to src/Ato.Copilot.Core/Models/Compliance/ComplianceModels.cs
- [X] T089 [P] [US10] Add RemediationHistory model (SubscriptionId, StartDate, EndDate, Executions, Metrics) and RemediationMetric model (TotalExecutions, SuccessfulExecutions, FailedExecutions, RolledBackExecutions, AverageExecutionTime, MostRemediatedFamily) to src/Ato.Copilot.Core/Models/Compliance/ComplianceModels.cs

### Implementation for User Story 10

- [X] T090 [US10] Implement GetRemediationProgressAsync — query _activeRemediations + _remediationHistory for last 30 days, calculate CompletedCount/InProgressCount/FailedCount/PendingCount, calculate CompletionRate and AverageRemediationTime, return RemediationProgress in src/Ato.Copilot.Agents/Compliance/Services/Engines/Remediation/AtoRemediationEngine.cs
- [X] T091 [US10] Implement GetRemediationHistoryAsync — date-range filter on _remediationHistory with pagination (skip, take, default 50), aggregate metrics (total, successful, failed, rolled back, average time, most remediated family), return RemediationHistory with TotalCount for pagination in src/Ato.Copilot.Agents/Compliance/Services/Engines/Remediation/AtoRemediationEngine.cs

### Tests for User Story 10

- [X] T092 [P] [US10] Write progress and history tests — progress with 23 completed/3 in-progress/2 failed/19 pending shows accurate counts and 48.9% completion rate, history date-range filter returns correct subset, aggregate metrics accurate, empty history returns zero counts, average time calculation correct in tests/Ato.Copilot.Tests.Unit/Services/Engines/Remediation/AtoRemediationEngineTests.cs

**Checkpoint**: Progress tracking and history fully functional with counts, rates, and date-range filtering

---

## Phase 13: User Story 11 — Scheduled Remediation (Priority: P4)

**Goal**: Schedule batch remediation for a future change window

**Independent Test**: Schedule remediation for future time, verify schedule record created with correct time, findings, and Scheduled status

### Models for User Story 11

- [X] T093 [P] [US11] Add RemediationScheduleResult model (ScheduleId, ScheduledTime, FindingIds, FindingCount, Status, Options, CreatedAt) to src/Ato.Copilot.Core/Models/Compliance/ComplianceModels.cs

### Implementation for User Story 11

- [X] T094 [US11] Implement ScheduleRemediationAsync — create RemediationScheduleResult with scheduled time, finding IDs, status=Scheduled, BatchRemediationOptions; no background timer — execution deferred to caller (e.g., ComplianceWatchService polls due schedules and triggers ExecuteBatchRemediationAsync at the scheduled time) in src/Ato.Copilot.Agents/Compliance/Services/Engines/Remediation/AtoRemediationEngine.cs

### Tests for User Story 11

- [X] T095 [P] [US11] Write scheduling tests — schedule creates result with correct time and finding count, status is Scheduled, options preserved, scheduled time in the past returns error in tests/Ato.Copilot.Tests.Unit/Services/Engines/Remediation/AtoRemediationEngineTests.cs

**Checkpoint**: Scheduled remediation functional — operators can queue remediations for change windows

---

## Phase 14: Polish & Cross-Cutting Concerns

**Purpose**: Wire DI, update MCP tools, update existing tests, build verification, edge case coverage

### DI Registration

- [X] T096 Register AtoRemediationEngine as IRemediationEngine (Singleton), register 6 supporting services (IAiRemediationPlanGenerator, IRemediationScriptExecutor, INistRemediationStepsService, IAzureArmRemediationService, IComplianceRemediationService, IScriptSanitizationService) as Singletons, bind RemediationOptions configuration in src/Ato.Copilot.Agents/Extensions/ServiceCollectionExtensions.cs

### MCP Tool Updates

- [X] T097 [P] Update RemediationExecuteTool to use enhanced typed return values from new engine overloads while preserving existing parameter schema in src/Ato.Copilot.Agents/Compliance/Tools/ComplianceTools.cs
- [X] T098 [P] Update ValidateRemediationTool to use enhanced typed validation result while preserving existing parameter schema in src/Ato.Copilot.Agents/Compliance/Tools/ComplianceTools.cs
- [X] T099 [P] Update RemediationPlanTool to use enhanced plan generation with filtering support while preserving existing parameter schema in src/Ato.Copilot.Agents/Compliance/Tools/ComplianceTools.cs

### Existing Test Updates

- [X] T100 Update existing 13 RemediationEngine tests — change class references from RemediationEngine to AtoRemediationEngine, update mock setup for new constructor dependencies, verify all 13 tests still pass in tests/Ato.Copilot.Tests.Unit/Services/RemediationEngineTests.cs

### Edge Case Tests

- [X] T101 [P] Write edge case tests — ARM API throttled during execution (retry with backoff), resource deleted between snapshot and execution (Failed with descriptive message), snapshot capture fails but execution succeeds (warning, rollback unavailable), multiple findings on same resource in batch (serialized access), already-remediated finding skipped (kanban task advanced to Done), destructive AI script rejected by sanitization in tests/Ato.Copilot.Tests.Unit/Services/Engines/Remediation/AtoRemediationEngineTests.cs

### Build & Validation

- [X] T102 Run dotnet build Ato.Copilot.sln — verify zero warnings
- [X] T103 Run full test suite — all existing 1,758 tests + ~150 new tests must pass
- [X] T104 Remove old RemediationEngine.cs from src/Ato.Copilot.Agents/Compliance/Services/ (archived by T036)
- [X] T105 [P] Write SC-011 performance test — generate a remediation plan from 1,000+ mock ComplianceFindings, assert completion within 5 seconds without memory degradation in tests/Ato.Copilot.Tests.Unit/Services/Engines/Remediation/AtoRemediationEngineTests.cs
- [X] T106 [P] Add XML documentation comments (summary, param, returns) to all new public types (~32 model types, 6 service interfaces, 18 engine interface methods) across src/Ato.Copilot.Core/Models/Compliance/ and src/Ato.Copilot.Core/Interfaces/Compliance/

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Phase 1 — BLOCKS all user stories
- **US1 Plan Generation (Phase 3)**: Depends on Phase 2 — creates AtoRemediationEngine class
- **US2 Single Execution (Phase 4)**: Depends on Phase 3 (engine class exists)
- **US5 Validation & Rollback (Phase 5)**: Depends on Phase 4 (execution exists)
- **US3 Batch (Phase 6)**: Depends on Phase 4 (single execution works)
- **US4 Kanban Integration (Phase 7)**: Depends on Phase 4 (execution exists)
- **US6 Approval (Phase 8)**: Depends on Phase 4 (execution tracking exists)
- **US7 Impact Analysis (Phase 9)**: Depends on Phase 3 (risk calculation helpers)
- **US8 AI-Enhanced (Phase 10)**: Depends on Phase 3 (engine class exists)
- **US9 Manual Guidance (Phase 11)**: Depends on Phase 3 (engine class exists)
- **US10 Progress & History (Phase 12)**: Depends on Phase 4 (execution tracking)
- **US11 Scheduling (Phase 13)**: Depends on Phase 6 (batch exists)
- **Polish (Phase 14)**: Depends on all previous phases

### User Story Dependencies

- **US1 (P1)**: After Foundational — No dependency on other stories
- **US2 (P1)**: After US1 — Needs engine class created in US1
- **US5 (P2)**: After US2 — Needs execution to validate/rollback
- **US3 (P2)**: After US2 — Builds on single execution
- **US4 (P2)**: After US2 — Needs execution result to sync kanban
- **US6 (P3)**: After US2 — Extends execution with approval gate
- **US7 (P3)**: After US1 — Uses risk calculation helpers
- **US8 (P3)**: After US1 — Uses engine class, AI services
- **US9 (P3)**: After US1 — Uses engine class, NIST service
- **US10 (P4)**: After US2 — Queries execution tracking data
- **US11 (P4)**: After US3 — Extends batch with scheduling

### Within Each User Story

1. Models first (can be parallel within story)
2. Implementation (sequential within story)
3. Tests (can be parallel with implementation when writing tests first)

### Parallel Opportunities

- All Phase 1 tasks marked [P] can run in parallel
- All Phase 2 interfaces (T010-T016) can run in parallel
- All Phase 2 shared models (T017-T019) can run in parallel
- All Phase 2 service implementations (T020-T025) can run in parallel
- All Phase 2 service tests (T026-T031) can run in parallel
- After Phase 3 (US1), the following can run in parallel:
  - US7 (Impact Analysis) — different methods, no execution dependency
  - US8 (AI-Enhanced) — different methods, AI delegation
  - US9 (Manual Guidance) — different methods, NIST delegation
- After Phase 4 (US2), the following can run in parallel:
  - US3 (Batch) — adds batch method
  - US4 (Kanban) — adds post-execution hooks
  - US5 (Validation) — adds validation/rollback methods
  - US6 (Approval) — adds approval method
  - US10 (Progress) — adds query methods

---

## Parallel Example: Foundational Phase

```bash
# Launch all interfaces in parallel:
T010: Extend IRemediationEngine in IComplianceInterfaces.cs
T011: Create IAiRemediationPlanGenerator.cs
T012: Create IRemediationScriptExecutor.cs
T013: Create INistRemediationStepsService.cs
T014: Create IAzureArmRemediationService.cs
T015: Create IComplianceRemediationService.cs
T016: Create IScriptSanitizationService.cs

# Then launch all service implementations in parallel:
T020: NistRemediationStepsService.cs
T021: ScriptSanitizationService.cs
T022: AiRemediationPlanGenerator.cs
T023: RemediationScriptExecutor.cs
T024: AzureArmRemediationService.cs
T025: ComplianceRemediationService.cs

# Then launch all service tests in parallel:
T026-T031: One test file per service
```

---

## Parallel Example: After Phase 4 (US2 Complete)

```bash
# These user stories can proceed in parallel since they add independent methods:
Phase 5 (US5): Validation & Rollback — adds ValidateRemediationAsync, RollbackRemediationAsync
Phase 6 (US3): Batch — adds ExecuteBatchRemediationAsync
Phase 7 (US4): Kanban — adds post-execution hooks
Phase 8 (US6): Approval — adds ProcessRemediationApprovalAsync
Phase 12 (US10): Progress — adds GetRemediationProgressAsync, GetRemediationHistoryAsync
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001–T009)
2. Complete Phase 2: Foundational (T010–T031)
3. Complete Phase 3: User Story 1 — Plan Generation (T032–T042)
4. **STOP and VALIDATE**: Test plan generation independently
5. Deploy/demo if ready — security engineers can generate prioritized plans

### Incremental Delivery

1. Setup + Foundational → Foundation ready
2. Add US1 (Plan Generation) → Test independently → **MVP!**
3. Add US2 (Single Execution) → Test independently → Core execution working
4. Add US5 (Validation/Rollback) → Test independently → Safety net in place
5. Add US3 (Batch) + US4 (Kanban) → Test independently → Full workflow
6. Add US6-US9 (P3 stories) → Test independently → Advanced features
7. Add US10-US11 (P4 stories) → Test independently → Operational maturity
8. Polish (Phase 14) → Full build + test validation

### Parallel Team Strategy

With multiple developers:

1. Team completes Setup + Foundational together
2. Single developer: US1 → US2 (sequential, creates engine class)
3. Once US2 complete:
   - Developer A: US3 (Batch) + US4 (Kanban)
   - Developer B: US5 (Validation/Rollback) + US6 (Approval)
   - Developer C: US7 (Impact) + US8 (AI-Enhanced) + US9 (Manual Guidance)
4. Team: US10 + US11 + Polish

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- Each user story should be independently completable and testable after its phase completes
- All engine methods are added to the same AtoRemediationEngine.cs file — ordering within the file follows the phase sequence
- Tests use established InMemoryDbContextFactory + Mock + FluentAssertions pattern
- Commit after each phase to maintain clean git history
- Stop at any checkpoint to validate story independently
- Avoid: cross-story dependencies that break independence
