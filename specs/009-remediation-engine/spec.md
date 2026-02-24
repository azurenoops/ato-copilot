# Feature Specification: ATO Remediation Engine

**Feature Branch**: `009-remediation-engine`
**Created**: 2026-02-24
**Status**: Draft
**Input**: User description: "ATO Remediation Engine - comprehensive remediation orchestrator that transforms compliance findings into executable fix plans, automated resource modifications, and validated security improvements across Azure subscriptions"

## Overview

The ATO Remediation Engine is the operational counterpart to the ATO Compliance Engine (Feature 008). While the Compliance Engine identifies what is wrong, the Remediation Engine determines how to fix it, executes the fix, validates success, and rolls back if something goes wrong.

This feature enhances the existing `RemediationEngine` (452 lines, partially implemented with simulated execution) into a production-grade `AtoRemediationEngine` with a 3-tier execution pipeline, real Azure ARM operations, AI-enhanced script generation, snapshot-based rollback, approval workflows, batch concurrency control, and deep integration with the Remediation Kanban board (Feature 002).

### Existing Code Assessment

The current codebase contains a foundational `RemediationEngine` with:

- **Real implementation**: Plan generation (`GeneratePlanAsync`) queries latest assessment and builds plans with steps
- **Simulated execution**: `ExecuteRemediationAsync` in apply mode comments "simplified — in production, this would call Azure SDK"
- **Simulated validation**: `ValidateRemediationAsync` comments "simulated validation — in production, re-scan the specific resource"
- **Real batch orchestration**: `BatchRemediateAsync` generates plan, filters, iterates with stop-on-failure
- **3 MCP tools**: `compliance_remediate`, `compliance_validate_remediation`, `compliance_generate_plan`
- **Kanban integration**: `KanbanService.ExecuteTaskRemediationAsync` delegates to `IRemediationEngine`

This feature transforms the simulated portions into production-ready implementations while preserving the existing interface contract that `KanbanService`, `ComplianceWatchService`, and the MCP tools already depend on.

### Kanban Integration Strategy

The Remediation Kanban (Feature 002) provides a workflow management layer with `RemediationBoard`, `RemediationTask`, status transitions, audit trails, and validation. The integration approach:

1. **Bidirectional task sync** — When the Remediation Engine executes a finding fix, it automatically updates any linked kanban task's status and adds history entries
2. **Kanban-initiated remediation** — The existing `KanbanRemediateTaskTool` already delegates to `IRemediationEngine`; the enhanced engine provides real execution instead of simulation
3. **Batch remediation to bulk task advancement** — After batch remediation, the engine bulk-advances linked kanban tasks through InReview and Done
4. **Evidence auto-population** — Post-remediation, the engine triggers `CollectTaskEvidenceAsync` on linked kanban tasks
5. **POA&M alignment** — Open kanban tasks feed into POA&M generation; completed remediations close corresponding tasks

---

## User Scenarios & Testing

### User Story 1 — Generate Remediation Plan from Assessment (Priority: P1)

A security engineer runs a compliance assessment and receives findings. They ask for a remediation plan to understand what needs fixing, in what order, and how long it will take. The engine analyzes all findings, prioritizes them by severity and automability, groups them by dependency, and produces a phased implementation timeline with risk reduction projections.

**Why this priority**: Plan generation is the foundation — every remediation workflow starts with understanding what to fix and in what order. This is the most frequently used capability and must work before any execution can proceed.

**Independent Test**: Can be fully tested by generating a plan from a set of compliance findings and verifying the plan contains correctly prioritized items, accurate duration estimates, a valid timeline, and risk reduction calculations.

**Acceptance Scenarios**:

1. **Given** an assessment with 47 findings across multiple severity levels, **When** the user requests a remediation plan, **Then** the engine produces a plan with items sorted by severity descending, automatable-first, duration ascending, grouped into 5 priority phases (P0–P4), with per-item steps, validation steps, rollback plans, and dependency links.
2. **Given** a plan request with filters (only Critical and High severities, only Access Control family), **When** the engine generates the plan, **Then** only findings matching the filters appear in the plan.
3. **Given** a set of findings with mixed automatable and manual items, **When** the plan is generated, **Then** the executive summary shows counts per severity, auto vs manual split, total estimated effort, and projected risk reduction percentage.
4. **Given** multiple findings affecting the same Azure resource, **When** the plan is generated, **Then** those findings are linked as dependencies and the engine optionally groups them together when `GroupByResource` is enabled.
5. **Given** a single finding input (not a batch), **When** a plan is requested, **Then** the engine uses a 3-tier fallback: AI-enhanced plan (if available), then NIST remediation steps, then manual parsing of finding guidance text.

---

### User Story 2 — Execute Single-Finding Remediation (Priority: P1)

A security engineer identifies a critical finding — for example, HTTPS not enforced on a storage account — and requests the engine to fix it. The engine validates the finding, captures a before-snapshot of the resource, executes the fix through a 3-tier pipeline (AI script, then Compliance Remediation Service, then Legacy ARM operations), captures an after-snapshot, optionally validates the fix, and optionally rolls back if validation fails.

**Why this priority**: Execution is the core value proposition. Without real infrastructure modification, the engine is only a planning tool. Single-finding execution must work before batch execution.

**Independent Test**: Can be tested by executing a remediation against a mock Azure resource and verifying the before/after snapshots are captured, the 3-tier pipeline is attempted in order, the execution record tracks status transitions, and the result includes backup ID for rollback.

**Acceptance Scenarios**:

1. **Given** an auto-remediable finding and `EnableAutomatedRemediation = true`, **When** the user executes remediation with `dry_run = false`, **Then** the engine captures a before-snapshot, executes through the 3-tier pipeline, captures an after-snapshot, records the execution with status Completed, and returns a backup ID.
2. **Given** `EnableAutomatedRemediation = false`, **When** the user attempts execution, **Then** the engine returns a failure message indicating automated remediation is disabled and suggesting manual remediation.
3. **Given** `dry_run = true`, **When** the user executes remediation, **Then** the engine walks through the full pipeline but applies no changes to Azure resources, returning a preview of what would change.
4. **Given** `RequireApproval = true`, **When** remediation is requested, **Then** the engine creates an execution with status Pending and returns without executing, awaiting explicit approval.
5. **Given** a finding with `IsAutoRemediable = false`, **When** the user attempts execution, **Then** the engine returns an error and automatically offers manual remediation guidance.
6. **Given** execution with `AutoValidate = true` and the validation fails, **When** `AutoRollbackOnFailure = true`, **Then** the engine automatically restores the resource from the before-snapshot and sets status to RolledBack.

---

### User Story 3 — Batch Remediation with Concurrency Control (Priority: P2)

A security engineer says "fix all high-priority compliance issues." The engine filters findings by severity, limits to auto-remediable items, and executes them in parallel with configurable concurrency control. Each parallel execution follows the full single-finding pipeline. The batch result aggregates success rates, severity counts, and risk reduction.

**Why this priority**: Batch remediation delivers the "fix everything" workflow that organizations need for rapid compliance improvement. It depends on single-finding execution (P1) being working.

**Independent Test**: Can be tested by submitting a batch of findings and verifying concurrency is limited by the semaphore, FailFast behavior cancels remaining on first error, the batch summary correctly counts successes and failures, and risk reduction is calculated.

**Acceptance Scenarios**:

1. **Given** 10 auto-remediable findings and `MaxConcurrentRemediations = 3`, **When** batch remediation executes, **Then** at most 3 remediations run concurrently (controlled by semaphore).
2. **Given** `FailFast = true` and the 3rd finding fails, **When** batch remediation executes, **Then** all remaining findings are cancelled and the result includes the 2 successes and 1 failure.
3. **Given** `FailFast = false` (default), **When** one finding fails mid-batch, **Then** all other findings continue executing and the batch result includes per-finding success or failure status.
4. **Given** batch completion, **When** the result is returned, **Then** it includes `BatchRemediationSummary` with SuccessRate, CriticalFindingsRemediated, HighFindingsRemediated, EstimatedRiskReduction, and ControlFamiliesAffected.

---

### User Story 4 — Kanban Task Remediation Integration (Priority: P2)

A security engineer views their remediation kanban board and sees task REM-005 with a remediation script. They say "fix REM-005." The kanban service delegates to the Remediation Engine, which executes the real fix. On success, the kanban task automatically advances to InReview and a history entry is recorded. On failure, a system comment is added explaining what went wrong.

**Why this priority**: This is the bridge between the workflow management layer (kanban) and the operational layer (remediation engine). The kanban board is where operators manage their day-to-day work, so seamless remediation from the board is essential.

**Independent Test**: Can be tested by creating a kanban task with a remediation script, invoking remediation through `KanbanRemediateTaskTool`, and verifying the task status transitions, history entries, and system comments are created correctly.

**Acceptance Scenarios**:

1. **Given** a kanban task REM-005 with `RemediationScript` populated and `FindingId` linked, **When** the user says "fix REM-005", **Then** the kanban service delegates to `IRemediationEngine.ExecuteRemediationAsync`, executes the fix, adds a `RemediationAttempt` history entry, and transitions the task to InReview.
2. **Given** a successful batch remediation affecting 5 findings, **When** those findings have linked kanban tasks, **Then** all 5 kanban tasks are bulk-advanced to InReview with corresponding history entries.
3. **Given** a kanban task without a `RemediationScript`, **When** remediation is attempted, **Then** the system returns an error suggesting manual remediation and does not change the task status.
4. **Given** remediation failure on a kanban task, **When** the exception is caught, **Then** a system comment with the error details is added to the task and the task remains in its current status.

---

### User Story 5 — Validation and Rollback (Priority: P2)

After executing a remediation, the security engineer wants to verify the fix took effect. They also need the ability to undo a remediation if it caused unintended consequences. The engine validates by checking execution metadata and step completion, and rolls back by restoring the before-snapshot captured during execution.

**Why this priority**: Validation confirms fixes worked; rollback provides a safety net. Both are essential for production trust but depend on execution (P1) being in place.

**Independent Test**: Can be tested by executing a remediation, then validating it (checking execution status, steps executed, changes applied), then rolling back and verifying the resource is restored from the before-snapshot.

**Acceptance Scenarios**:

1. **Given** a completed remediation execution with steps executed, **When** validation is requested, **Then** the engine checks execution status equals Completed, steps count is greater than 0, and changes were applied, returning a `RemediationValidationResult` with individual check results.
2. **Given** a failed execution where no steps completed, **When** validation is requested, **Then** the result shows `IsValid = false` with a failure reason.
3. **Given** a completed execution with a `BeforeSnapshot`, **When** rollback is requested, **Then** the engine restores the resource to its pre-remediation state, records rollback steps, and sets execution status to RolledBack.
4. **Given** an execution without a `BeforeSnapshot` (snapshot capture failed), **When** rollback is requested, **Then** the engine returns a failure indicating rollback data is unavailable.

---

### User Story 6 — Approval Workflow (Priority: P3)

A compliance officer requires that high-risk remediations be reviewed before execution. The engine supports an approval gate where remediation requests enter a Pending state and await explicit approval or rejection. Approved remediations proceed to execution; rejected ones are recorded with the approver's comments.

**Why this priority**: Approval workflows are important for governance but are not required for basic remediation functionality. Most organizations will start without approval gates and add them as they mature.

**Independent Test**: Can be tested by requesting a remediation with `RequireApproval = true`, verifying the execution enters Pending status, then approving or rejecting it and verifying the subsequent behavior.

**Acceptance Scenarios**:

1. **Given** `RequireApproval = true`, **When** a remediation is requested, **Then** the engine creates an execution with status Pending and returns without modifying any resources.
2. **Given** a pending execution, **When** the compliance officer approves it, **Then** the engine sets status to Approved and triggers the full execution pipeline.
3. **Given** a pending execution, **When** the compliance officer rejects it with comments "Risk too high for production", **Then** the engine sets status to Rejected, records the approver name and comments, and does not execute.
4. **Given** a subscription with multiple active remediation workflows, **When** the compliance officer queries active workflows, **Then** the engine returns all pending, in-progress, and recently completed executions with their status.

---

### User Story 7 — Impact Analysis (Priority: P3)

Before executing remediation, the security engineer wants to understand the risk. The engine analyzes each finding, calculates risk scores (current and projected), identifies impacted resources, and provides recommendations on whether to proceed. This helps operators make informed decisions before modifying production infrastructure.

**Why this priority**: Impact analysis adds significant value for risk-conscious organizations but is not required for basic remediation. It enhances the plan generation workflow.

**Independent Test**: Can be tested by submitting findings for impact analysis and verifying risk score calculations, per-resource impact details, and actionable recommendations.

**Acceptance Scenarios**:

1. **Given** a set of findings with mixed severities, **When** impact analysis is requested, **Then** the engine calculates current risk score (severity-weighted sum), projected risk score (non-remediable findings only), and risk reduction percentage.
2. **Given** findings affecting 5 unique Azure resources, **When** impact analysis runs, **Then** the result includes a `ResourceImpact` for each resource with findings count, proposed changes, and risk level.
3. **Given** findings where some require manual remediation, **When** the impact analysis generates recommendations, **Then** it distinguishes between automatable and manual findings and suggests execution order.

---

### User Story 8 — AI-Enhanced Remediation (Priority: P3)

When an AI model is available (via `IChatClient`), the engine can generate context-aware remediation scripts in Azure CLI, PowerShell, Bicep or Terraform. It can also provide natural-language remediation guidance and prioritize findings using business context. When AI is unavailable, the engine silently falls back to deterministic remediation strategies.

**Why this priority**: AI enhancement provides significant sophistication but is optional — the engine must function fully without AI. This is an additive capability.

**Independent Test**: Can be tested by injecting a mock `IChatClient`, requesting script generation for a finding, and verifying the script is generated, sanitized, and includes expected parameters. Also testable by confirming graceful fallback when AI is unavailable.

**Acceptance Scenarios**:

1. **Given** an available `IChatClient` and `UseAiScript = true`, **When** script generation is requested for a finding, **Then** the engine generates a remediation script in the requested language (Azure CLI, PowerShell, Bicep or Terraform) with description, parameters, and estimated duration.
2. **Given** the AI model is unavailable (IChatClient is null), **When** script generation is requested, **Then** the engine falls back to NIST remediation steps or manual parsing without returning an error.
3. **Given** an AI-generated script, **When** script sanitization is available, **Then** the script is validated against safe command patterns before execution.
4. **Given** a set of findings and a business context string, **When** AI prioritization is requested, **Then** the engine returns prioritized findings with AI-assigned priority, justification, and business impact assessment.
5. **Given** natural-language guidance is requested for a finding, **When** the AI engine processes it, **Then** the result includes an explanation, technical plan, confidence score, and reference links.

---

### User Story 9 — Manual Remediation Guidance (Priority: P3)

For findings that cannot be automatically remediated, the engine generates a comprehensive manual guide with step-by-step instructions, prerequisites, required permissions, skill level assessment, validation steps, and rollback plan.

**Why this priority**: Manual guidance ensures the engine provides value even for non-automatable findings. It covers the gap where automated execution is not possible.

**Independent Test**: Can be tested by requesting a manual guide for a finding with `IsAutoRemediable = false` and verifying all guide sections are populated with contextually relevant content.

**Acceptance Scenarios**:

1. **Given** a finding with `IsAutoRemediable = false`, **When** a manual guide is requested, **Then** the engine produces a `ManualRemediationGuide` with steps, prerequisites, skill level, required permissions, validation steps, and rollback plan.
2. **Given** a control family with known skill requirements (e.g., CP for Intermediate, SC for Advanced), **When** the guide is generated, **Then** the skill level reflects the control family's complexity.
3. **Given** a finding with remediation guidance text, **When** the engine parses it, **Then** the steps are extracted using regex patterns for numbered steps, bulleted lists, and action verbs.

---

### User Story 10 — Progress Tracking and History (Priority: P4)

The security engineer and compliance officer need visibility into remediation progress. The engine tracks all active remediations and maintains execution history with metrics. They can query progress snapshots (last 30 days), historical execution data for date ranges, and individual execution status.

**Why this priority**: Tracking and history are essential for compliance auditing but do not affect remediation functionality. They can be added after core operations are working.

**Independent Test**: Can be tested by executing multiple remediations and then querying progress (verifying counts, completion rate, average time) and history (verifying date-range filtering and metric calculation).

**Acceptance Scenarios**:

1. **Given** 23 completed, 3 in-progress, 2 failed, and 19 pending remediations in the last 30 days, **When** progress is queried, **Then** the result includes accurate counts, a 48.9% completion rate, and average remediation time.
2. **Given** remediation executions spanning a date range, **When** history is queried with start and end dates, **Then** only executions within that range are returned with aggregate metrics.
3. **Given** a remediation execution lifecycle from creation through completion, **When** the execution is tracked, **Then** it transitions through states (Pending, Approved, InProgress, Completed, Failed, RolledBack, Rejected) with timestamps at each transition.

---

### User Story 11 — Scheduled Remediation (Priority: P4)

For production environments with change windows, the engine supports scheduling remediation for a future time. The security engineer specifies when remediation should execute, and the engine queues the findings for execution at the scheduled time.

**Why this priority**: Scheduling is important for enterprises with change management processes but is not required for basic remediation. It extends the batch execution capability.

**Independent Test**: Can be tested by scheduling a remediation for a future time and verifying the schedule record is created with the correct time, findings, and status.

**Acceptance Scenarios**:

1. **Given** a set of findings and a scheduled time, **When** the user schedules remediation, **Then** the engine creates a `RemediationScheduleResult` with the scheduled time, finding count, and status Scheduled.
2. **Given** a scheduled remediation at 2:00 AM Saturday, **When** the scheduled time arrives, **Then** the engine executes the batch remediation with the configured options.

---

### Edge Cases

- **What happens when the Azure ARM API is throttled during execution?** The engine retries with exponential backoff and records the throttling event in the execution log. If retries are exhausted, the finding is marked as Failed and the next finding in the batch continues.
- **What happens when a resource is deleted between snapshot and execution?** The engine catches the "resource not found" error, marks the finding as Failed with a descriptive message, and continues batch processing.
- **What happens when snapshot capture fails but execution succeeds?** The remediation completes but rollback is unavailable for that execution. A warning is logged noting that rollback data was not captured.
- **What happens when multiple findings affect the same resource simultaneously in a batch?** The semaphore serializes concurrent access, and when `GroupByResource` is enabled, same-resource findings execute sequentially to avoid ARM conflicts.
- **What happens when the Kanban board has a task for a finding but the finding is already remediated?** The engine detects the finding is no longer in the assessment or is marked compliant and skips execution, advancing the kanban task directly to Done.
- **What happens when AI script generation produces a destructive command?** The script sanitization service rejects scripts containing dangerous patterns (resource deletion, subscription-wide changes) before execution.

---

## Requirements

### Functional Requirements

- **FR-001**: System MUST enhance the existing `IRemediationEngine` interface to support 18 unique method names (21 total signatures including overloads) across three tiers — core remediation operations, workflow management, and AI-enhanced capabilities — while preserving backward compatibility with existing consumers (`KanbanService`, `ComplianceWatchService`, MCP tools).
- **FR-002**: System MUST implement a 3-tier execution pipeline with explicit fallback triggers: **Tier 1** (AI script) — attempted when `IChatClient` is available and `UseAiScript = true`; falls back when `IChatClient` is null, AI returns null, or throws an exception. **Tier 2** (Compliance Remediation Service) — applies predefined remediation templates matching known compliance patterns (property corrections for supported RemediationType values); falls back when `CanHandle()` returns false or throws. **Tier 3** (Legacy ARM) — executes one of 8 hardcoded ARM operations as a final fallback. Each tier’s success or failure MUST be logged.
- **FR-003**: System MUST capture before-snapshot and after-snapshot of Azure resources during remediation execution for rollback capability, using `CaptureResourceSnapshotAsync` to serialize the resource's JSON state.
- **FR-004**: System MUST support five remediation modes — dry run (preview-only), single-finding live execution, batch execution with concurrency control, scheduled execution for change windows, and manual guidance generation.
- **FR-005**: System MUST enforce the `EnableAutomatedRemediation` configuration gate, blocking all automated execution when disabled while continuing to support plan generation, impact analysis, script generation, and manual guides.
- **FR-006**: System MUST support an approval workflow where remediation requests enter a Pending state, await explicit approval or rejection via `ProcessRemediationApprovalAsync`, and proceed or halt accordingly.
- **FR-007**: System MUST implement batch remediation with configurable concurrency via `SemaphoreSlim`, supporting `MaxConcurrentRemediations` (default 3), `FailFast` mode, and `ContinueOnError` mode with aggregate reporting.
- **FR-008**: System MUST validate post-execution remediation by checking execution status, steps executed count, and changes applied, returning a structured `RemediationValidationResult` with per-check details.
- **FR-009**: System MUST support snapshot-based rollback that restores a resource to its pre-remediation state from the captured before-snapshot, recording rollback steps and updating execution status to RolledBack.
- **FR-010**: System MUST calculate risk scores using severity weights (Critical=10, High=7.5, Medium=5, Low=2.5, Other=1) for current risk, projected risk after remediation, and risk reduction percentage.
- **FR-011**: System MUST integrate with the Remediation Kanban board (Feature 002) by updating linked kanban task statuses and history entries when remediations complete, supporting both individual task remediation and bulk task advancement after batch operations.
- **FR-012**: System MUST generate manual remediation guides for non-automatable findings with step-by-step instructions, prerequisites, skill level assessment, required permissions, validation steps, and rollback plans.
- **FR-013**: System MUST support AI-enhanced capabilities when `IChatClient` is available — script generation in Azure CLI, PowerShell, Bicep and Terraform; natural-language remediation guidance; and business-context-aware finding prioritization — with graceful fallback when AI is unavailable.
- **FR-014**: System MUST track all active remediations and maintain execution history in-memory, supporting progress queries (last 30 days), date-range history queries, and execution status lifecycle tracking.
- **FR-015**: System MUST decompose the monolithic `RemediationEngine` into single-responsibility services: `AiRemediationPlanGenerator`, `RemediationScriptExecutor`, `NistRemediationStepsService`, `AzureArmRemediationService`, `ComplianceRemediationService`, and `ScriptSanitizationService`.
- **FR-016**: System MUST map finding severity to remediation priority — P0 (Immediate) for Critical, P1 (24 hours) for High, P2 (7 days) for Medium, P3 (30 days) for Low, P4 (Best effort) for Other.
- **FR-017**: System MUST estimate remediation duration based on severity and automation status — auto-remediable findings range from 10 to 30 minutes, manual findings from 30 minutes to 4 hours, with resource-type-specific estimates for manual items.
- **FR-018**: System MUST generate an implementation timeline with 5 priority-based phases (Immediate, 24 Hours, Week 1, Month 1, Backlog) with start and end dates and assigned remediation items.
- **FR-019**: System MUST support 8 legacy ARM remediation operations — TLS version update, diagnostic settings, alert rules, log retention, encryption, NSG configuration, policy assignment, and HTTPS enforcement (deprecated) — as Tier 3 fallback.
- **FR-020**: System MUST parse remediation steps from finding text using regex patterns for numbered steps, bulleted substeps, and action verbs (Enable, Configure, Implement, Review, Update, Deploy, Monitor, Verify, Create, Remove, Restrict, Set, Add, Apply, Disable).

### Key Entities

- **RemediationPlan**: A prioritized collection of remediation items with timeline, risk metrics, and executive summary — generated from assessment findings
- **RemediationItem**: A single finding paired with remediation steps, priority (P0–P4), estimated duration, dependencies, validation steps, and rollback plan
- **RemediationExecution**: A tracking record for a remediation operation with execution status, before/after snapshots, backup ID, duration, steps executed, and changes applied
- **BatchRemediationResult**: Aggregate outcome of a batch remediation with per-execution results, success and failure counts, summary with risk reduction, and duration
- **RemediationValidationResult**: Post-execution validation outcome with individual pass/fail checks and failure reason
- **RemediationRollbackResult**: Rollback operation outcome with steps executed, success flag, and link to original execution
- **RemediationWorkflowStatus**: Active workflow state showing pending approvals, in-progress executions, and recently completed operations
- **RemediationApprovalResult**: Approval or rejection record with approver identity, comments, and timestamp
- **RemediationScheduleResult**: Scheduled execution record with target time, findings, and status
- **RemediationProgress**: Subscription-level progress snapshot with counts (completed, in-progress, failed, pending), completion rate, and average remediation time
- **RemediationHistory**: Date-range execution history with aggregate metrics (total, successful, failed, rolled back, average time)
- **RemediationImpactAnalysis**: Pre-execution risk analysis with current and projected risk scores, per-resource impacts, and recommendations
- **ManualRemediationGuide**: Comprehensive guide for non-automatable findings with steps, prerequisites, skill level, permissions, validation, and rollback
- **RemediationScript**: AI-generated script with content, script type, description, parameters, and estimated duration
- **RemediationGuidance**: AI-generated remediation explanation with technical plan, confidence score, and references
- **PrioritizedFinding**: AI-prioritized finding with assigned priority, justification, and business impact assessment
- **RemediationExecutionOptions**: Runtime options — dry run, require approval, auto-validate, auto-rollback, use AI script
- **BatchRemediationOptions**: Batch options — max concurrent remediations, fail fast, continue on error
- **RemediationPlanOptions**: Filters — minimum severity, include and exclude control families, automatable-only, group by resource

---

## Success Criteria

### Measurable Outcomes

- **SC-001**: Security engineers can generate a remediation plan from an assessment with 50+ findings in under 5 seconds, producing a correctly prioritized plan with duration estimates and risk reduction projection.
- **SC-002**: Single-finding remediation executes end-to-end (snapshot, fix, snapshot, validate) in under 2 minutes for common remediation types (TLS update, HTTPS enforcement, encryption enablement).
- **SC-003**: Batch remediation of 10 findings completes within 5 minutes with concurrency control correctly limiting parallel execution.
- **SC-004**: The engine correctly falls through all 3 execution tiers (AI, then Compliance Service, then Legacy ARM) when earlier tiers are unavailable, without returning errors to the user.
- **SC-005**: Rollback restores a resource to its pre-remediation state within 1 minute when a before-snapshot is available.
- **SC-006**: Plan generation filters findings accurately — severity filters, control family filters, and automatable-only filters produce correct subsets with zero false inclusions.
- **SC-007**: Risk reduction calculations are accurate to within 1% of the expected value based on severity weights and finding counts.
- **SC-008**: Kanban task status updates occur within 5 seconds of remediation completion, with history entries and system comments correctly recorded.
- **SC-009**: All 8 legacy ARM operations (TLS, diagnostics, alerts, log retention, encryption, NSG, policy, HTTPS) execute successfully against valid Azure resources.
- **SC-010**: Approval workflows correctly block execution until approved, and rejected workflows record the approver and comments without executing.
- **SC-011**: The engine handles 1,000+ findings in a single plan generation without performance degradation.
- **SC-012**: Manual remediation guides for non-automatable findings include all required sections (steps, prerequisites, skill level, permissions, validation, rollback).

---

## Assumptions

- Feature 008 (ATO Compliance Engine) is merged and functional, providing `IAtoComplianceEngine` with assessment data, finding retrieval, and compliance scoring
- Feature 002 (Remediation Kanban) is merged and functional, providing `IKanbanService` with task management, status transitions, and history tracking
- Feature 007 (NIST Controls) is merged and functional, providing `INistControlsService` for control family lookup and NIST remediation step knowledge
- Feature 005 (Compliance Watch) is merged and functional, providing `IComplianceWatchService` with auto-remediation rule matching
- The existing `IRemediationEngine` interface (4 methods) will be extended to 17 methods while maintaining backward compatibility — existing consumers will not break
- Azure ARM SDK (`ArmClient`, `GenericResource`) is available for resource operations
- `IChatClient` from `Microsoft.Extensions.AI` is an optional dependency — the engine must work entirely without AI
- In-memory tracking (Dictionary and List) is acceptable for the current phase; database persistence for remediation history is a future backlog item
- Script execution via `RemediationScriptExecutor` runs in a controlled environment with 5-minute timeout and 3-retry limit
- The existing `ComplianceAgentOptions.EnableAutomatedRemediation` boolean is the master gate for execution

---

## Dependencies

- **Feature 008** (ATO Compliance Engine): `IAtoComplianceEngine` for assessments, findings, scoring
- **Feature 002** (Remediation Kanban): `IKanbanService` for task status updates, history entries, evidence collection
- **Feature 007** (NIST Controls): `INistControlsService` for control definitions, `INistRemediationStepsService` for curated remediation steps
- **Feature 005** (Compliance Watch): `IComplianceWatchService` for auto-remediation rule integration
- **Azure ARM SDK**: `ArmClient`, `GenericResource` for resource operations
- **Microsoft.Extensions.AI**: `IChatClient` (optional) for AI-enhanced capabilities