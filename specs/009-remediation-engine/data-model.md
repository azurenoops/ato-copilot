# Data Model: ATO Remediation Engine

**Feature**: 009-remediation-engine | **Date**: 2026-02-24

## Existing Types (Modified)

### RemediationPlan (ComplianceModels.cs L685)

Already exists with 14 properties. **No structural changes** — this type continues to serve the legacy `GeneratePlanAsync` return. The new enhanced plan generation returns a new `RemediationPlan` with the additional `Items`, `Timeline`, `ExecutiveSummary`, and `RiskMetrics` properties added below.

**Added properties**:
| Property | Type | Description |
|----------|------|-------------|
| Items | `List<RemediationItem>` | Prioritized remediation items (replaces flat Steps for enhanced plans) |
| Timeline | `ImplementationTimeline` | 5-phase implementation timeline |
| ExecutiveSummary | `RemediationExecutiveSummary` | Plan-level summary with counts and risk projection |
| RiskMetrics | `RiskMetrics` | Current risk score, projected risk score, reduction percentage |
| GroupByResource | `bool` | Whether items are grouped by Azure resource |
| Filters | `RemediationPlanOptions?` | Filters applied when generating this plan |

### RemediationStep (ComplianceModels.cs L731)

Already exists with 16 properties. **No changes needed** — existing fields cover all use cases.

### ComplianceFinding (ComplianceModels.cs L546)

Already exists with 27 properties. **No changes needed** — existing remediation fields (`RemediationGuidance`, `RemediationScript`, `AutoRemediable`, `RemediationType`, `RemediationTrackingStatus`) are sufficient.

> **Note**: The spec references `AtoFinding` but this class does not exist. All references to `AtoFinding` in the spec map to `ComplianceFinding`.

### ComplianceAgentOptions (ComplianceAgentOptions.cs)

**Added property**:
| Property | Type | Description |
|----------|------|-------------|
| Remediation | `RemediationOptions` | Remediation-specific configuration sub-class |

---

## New Types

### RemediationItem

A single finding paired with its remediation plan — combines the finding data with execution metadata.

| Property | Type | Description |
|----------|------|-------------|
| Id | `string` | Unique item identifier (GUID) |
| Finding | `ComplianceFinding` | The compliance finding to remediate |
| Priority | `RemediationPriority` | P0–P4 priority level |
| PriorityLabel | `string` | Human-readable priority (e.g., "P0 - Immediate") |
| EstimatedDuration | `TimeSpan` | Estimated time to remediate |
| Steps | `List<RemediationStep>` | Detailed remediation steps |
| ValidationSteps | `List<string>` | Steps to verify remediation success |
| RollbackPlan | `string` | Description of how to undo this remediation |
| Dependencies | `List<string>` | IDs of other items this depends on |
| IsAutoRemediable | `bool` | Whether this can be auto-remediated |
| RemediationType | `RemediationType` | Configuration, Monitoring, Access, etc. |
| AffectedResourceId | `string?` | Azure resource ID if applicable |

### RemediationExecution

Tracks a single remediation operation through its lifecycle.

| Property | Type | Description |
|----------|------|-------------|
| Id | `string` | Unique execution ID (GUID) |
| FindingId | `string` | ID of the finding being remediated |
| SubscriptionId | `string?` | Target Azure subscription |
| Status | `RemediationExecutionStatus` | Current execution status |
| BeforeSnapshot | `string?` | JSON snapshot of resource before remediation |
| AfterSnapshot | `string?` | JSON snapshot of resource after remediation |
| BackupId | `string?` | Reference to snapshot for rollback |
| StartedAt | `DateTime?` | When execution started |
| CompletedAt | `DateTime?` | When execution completed |
| Duration | `TimeSpan?` | Total execution duration |
| StepsExecuted | `int` | Number of steps completed |
| ChangesApplied | `List<string>` | Description of changes made |
| TierUsed | `int` | Which pipeline tier executed (1=AI, 2=Service, 3=ARM) |
| Error | `string?` | Error message if failed |
| ApprovedBy | `string?` | Approver identity if approval workflow used |
| ApprovedAt | `DateTime?` | When approved |
| RejectedBy | `string?` | Rejector identity if rejected |
| RejectedAt | `DateTime?` | When rejected |
| RejectionReason | `string?` | Reason for rejection |
| DryRun | `bool` | Whether this was a dry-run execution |
| Options | `RemediationExecutionOptions?` | Execution options used |

### RemediationExecutionStatus (Enum)

| Value | Description |
|-------|-------------|
| Pending | Awaiting approval |
| Approved | Approved, awaiting execution |
| InProgress | Currently executing |
| Completed | Successfully completed |
| Failed | Execution failed |
| RolledBack | Rolled back after failure |
| Rejected | Approval rejected |
| Cancelled | Batch cancellation (FailFast) |
| Scheduled | Scheduled for future execution |

### BatchRemediationResult

Aggregate outcome of a batch remediation operation.

| Property | Type | Description |
|----------|------|-------------|
| BatchId | `string` | Unique batch ID (GUID) |
| Executions | `List<RemediationExecution>` | Individual execution results |
| SuccessCount | `int` | Number of successful remediations |
| FailureCount | `int` | Number of failed remediations |
| CancelledCount | `int` | Number cancelled (FailFast mode) |
| SkippedCount | `int` | Number skipped (not auto-remediable) |
| Summary | `BatchRemediationSummary` | Aggregate statistics |
| StartedAt | `DateTime` | Batch start time |
| CompletedAt | `DateTime?` | Batch completion time |
| Duration | `TimeSpan?` | Total batch duration |
| Options | `BatchRemediationOptions?` | Options used for batch |

### BatchRemediationSummary

Aggregate statistics for a batch remediation.

| Property | Type | Description |
|----------|------|-------------|
| SuccessRate | `double` | Percentage of successful remediations |
| CriticalFindingsRemediated | `int` | Count of Critical findings fixed |
| HighFindingsRemediated | `int` | Count of High findings fixed |
| MediumFindingsRemediated | `int` | Count of Medium findings fixed |
| LowFindingsRemediated | `int` | Count of Low findings fixed |
| EstimatedRiskReduction | `double` | Projected risk reduction percentage |
| ControlFamiliesAffected | `List<string>` | Unique control families touched |
| TotalDuration | `TimeSpan` | Sum of all execution durations |

### RemediationValidationResult

Post-execution validation outcome.

| Property | Type | Description |
|----------|------|-------------|
| ExecutionId | `string` | ID of the execution validated |
| IsValid | `bool` | Overall validation result |
| Checks | `List<ValidationCheck>` | Individual check results |
| FailureReason | `string?` | Overall failure reason if not valid |
| ValidatedAt | `DateTime` | When validation was performed |

### ValidationCheck

A single validation check within a validation result.

| Property | Type | Description |
|----------|------|-------------|
| Name | `string` | Check name (e.g., "ExecutionStatus", "StepsCompleted") |
| Passed | `bool` | Whether this check passed |
| ExpectedValue | `string?` | What was expected |
| ActualValue | `string?` | What was found |
| Details | `string?` | Additional details |

### RemediationRollbackResult

Outcome of a rollback operation.

| Property | Type | Description |
|----------|------|-------------|
| ExecutionId | `string` | ID of the original execution |
| Success | `bool` | Whether rollback succeeded |
| RollbackSteps | `List<string>` | Steps executed during rollback |
| RestoredSnapshot | `string?` | JSON of restored resource state |
| Error | `string?` | Error message if rollback failed |
| RolledBackAt | `DateTime` | When rollback was performed |

### RollbackPlan

Planned rollback approach for a remediation item.

| Property | Type | Description |
|----------|------|-------------|
| Description | `string` | What the rollback will do |
| Steps | `List<string>` | Ordered rollback steps |
| RequiresSnapshot | `bool` | Whether a before-snapshot is needed |
| EstimatedDuration | `TimeSpan` | Estimated rollback time |

### RemediationWorkflowStatus

Active workflow state for a subscription.

| Property | Type | Description |
|----------|------|-------------|
| SubscriptionId | `string?` | Target subscription |
| PendingApprovals | `List<RemediationExecution>` | Executions awaiting approval |
| InProgressExecutions | `List<RemediationExecution>` | Currently executing |
| RecentlyCompleted | `List<RemediationExecution>` | Completed in last 24 hours |
| RetrievedAt | `DateTime` | When this snapshot was taken |

### RemediationApprovalResult

Result of an approval or rejection decision.

| Property | Type | Description |
|----------|------|-------------|
| ExecutionId | `string` | Execution that was approved/rejected |
| Approved | `bool` | Whether approved (true) or rejected (false) |
| ApproverName | `string` | Identity of the approver |
| Comments | `string?` | Approver comments |
| ProcessedAt | `DateTime` | When the decision was made |
| ExecutionTriggered | `bool` | Whether execution was auto-triggered post-approval |

### RemediationScheduleResult

Result of scheduling a remediation for future execution.

> **Caller responsibility**: The engine creates the schedule record but does not execute automatically. The calling layer (e.g., `ComplianceWatchService` or MCP host) is responsible for polling due schedules and triggering `ExecuteBatchRemediationAsync` at the scheduled time.

| Property | Type | Description |
|----------|------|-------------|
| ScheduleId | `string` | Unique schedule ID (GUID) |
| ScheduledTime | `DateTime` | When remediation will execute |
| FindingIds | `List<string>` | Findings included |
| FindingCount | `int` | Number of findings |
| Status | `string` | Schedule status (Scheduled, Executed, Cancelled) |
| Options | `BatchRemediationOptions?` | Options to use at execution time |
| CreatedAt | `DateTime` | When schedule was created |

### RemediationProgress

Subscription-level progress snapshot.

| Property | Type | Description |
|----------|------|-------------|
| SubscriptionId | `string?` | Target subscription |
| CompletedCount | `int` | Successful remediations |
| InProgressCount | `int` | Currently executing |
| FailedCount | `int` | Failed remediations |
| PendingCount | `int` | Awaiting approval |
| TotalCount | `int` | Total remediations |
| CompletionRate | `double` | Percentage complete |
| AverageRemediationTime | `TimeSpan` | Average execution duration |
| Period | `string` | Time period covered (e.g., "Last 30 days") |
| AsOf | `DateTime` | When snapshot was calculated |

### RemediationHistory

Date-range execution history with aggregate metrics.

| Property | Type | Description |
|----------|------|-------------|
| SubscriptionId | `string?` | Target subscription |
| StartDate | `DateTime` | Range start |
| EndDate | `DateTime` | Range end |
| Executions | `List<RemediationExecution>` | Executions in range |
| Metrics | `RemediationMetric` | Aggregate metrics |
| Skip | `int` | Pagination offset (default 0) |
| Take | `int` | Pagination page size (default 50) |
| TotalCount | `int` | Total matching executions before pagination |

### RemediationMetric

Aggregate metrics for remediation history.

| Property | Type | Description |
|----------|------|-------------|
| TotalExecutions | `int` | Total count |
| SuccessfulExecutions | `int` | Completed successfully |
| FailedExecutions | `int` | Failed |
| RolledBackExecutions | `int` | Rolled back |
| AverageExecutionTime | `TimeSpan` | Average duration |
| MostRemediatedFamily | `string?` | Most frequently remediated control family |

### RemediationActivity

A single activity entry for tracking/audit purposes.

| Property | Type | Description |
|----------|------|-------------|
| Id | `string` | Activity ID |
| ExecutionId | `string` | Related execution |
| Action | `string` | What happened (e.g., "SnapshotCaptured", "TierFallback") |
| Details | `string?` | Additional details |
| Timestamp | `DateTime` | When it occurred |

### RemediationImpactAnalysis

Pre-execution risk analysis.

| Property | Type | Description |
|----------|------|-------------|
| RiskMetrics | `RiskMetrics` | Current/projected risk scores and reduction percentage |
| TotalFindingsAnalyzed | `int` | Total findings considered |
| AutoRemediableCount | `int` | Auto-remediable findings |
| ManualCount | `int` | Manual-only findings |
| ResourceImpacts | `List<ResourceImpact>` | Per-resource impact details |
| Recommendations | `List<string>` | Actionable recommendations |
| AnalyzedAt | `DateTime` | When analysis was performed |

### ResourceImpact

Per-resource impact detail within an impact analysis.

| Property | Type | Description |
|----------|------|-------------|
| ResourceId | `string` | Azure resource ID |
| ResourceType | `string?` | Resource type |
| FindingsCount | `int` | Number of findings for this resource |
| ProposedChanges | `List<string>` | What would change |
| RiskLevel | `RiskLevel` | Impact risk level |

### ManualRemediationGuide

Comprehensive guide for non-automatable findings.

| Property | Type | Description |
|----------|------|-------------|
| FindingId | `string` | Finding this guide is for |
| ControlId | `string?` | NIST control ID |
| Title | `string` | Guide title |
| Steps | `List<string>` | Step-by-step instructions |
| Prerequisites | `List<string>` | What must be in place before starting |
| SkillLevel | `string` | Required skill level (Beginner, Intermediate, Advanced) |
| RequiredPermissions | `List<string>` | Azure permissions needed |
| ValidationSteps | `List<string>` | How to verify the fix worked |
| RollbackPlan | `string` | How to undo if something goes wrong |
| EstimatedDuration | `TimeSpan` | How long it should take |
| References | `List<string>` | Microsoft Docs or NIST links |

### ImplementationTimeline

5-phase implementation timeline for a remediation plan.

| Property | Type | Description |
|----------|------|-------------|
| Phases | `List<TimelinePhase>` | Ordered phases |
| TotalEstimatedDuration | `TimeSpan` | Sum of all phase durations |
| StartDate | `DateTime` | Timeline start date |
| EndDate | `DateTime` | Timeline end date |

### TimelinePhase

A single phase within the implementation timeline.

| Property | Type | Description |
|----------|------|-------------|
| Name | `string` | Phase name (Immediate, 24 Hours, Week 1, Month 1, Backlog) |
| Priority | `RemediationPriority` | P0–P4 |
| Items | `List<RemediationItem>` | Items in this phase |
| StartDate | `DateTime` | Phase start |
| EndDate | `DateTime` | Phase end |
| EstimatedDuration | `TimeSpan` | Total effort for this phase |

### RemediationScript

AI-generated or curated remediation script.

| Property | Type | Description |
|----------|------|-------------|
| Content | `string` | Script content |
| ScriptType | `ScriptType` | AzureCli, PowerShell, Bicep, Terraform |
| Description | `string` | What the script does |
| Parameters | `Dictionary<string, string>` | Required parameters and descriptions |
| EstimatedDuration | `TimeSpan` | Estimated execution time |
| IsSanitized | `bool` | Whether script passed safety validation |

### ScriptType (Enum)

| Value | Description |
|-------|-------------|
| AzureCli | Azure CLI script |
| PowerShell | PowerShell script |
| Bicep | Bicep configuration |
| Terraform | Terraform configuration |

### RemediationGuidance

AI-generated remediation guidance.

| Property | Type | Description |
|----------|------|-------------|
| FindingId | `string` | Finding this guidance is for |
| Explanation | `string` | Natural-language explanation |
| TechnicalPlan | `string` | Technical implementation plan |
| ConfidenceScore | `double` | AI confidence (0.0–1.0) |
| References | `List<string>` | Reference links |
| GeneratedAt | `DateTime` | When guidance was generated |

### PrioritizedFinding

AI-prioritized finding with business context.

| Property | Type | Description |
|----------|------|-------------|
| Finding | `ComplianceFinding` | The finding |
| AiPriority | `RemediationPriority` | AI-assigned priority |
| Justification | `string` | Why this priority was assigned |
| BusinessImpact | `string` | Business impact assessment |
| OriginalPriority | `RemediationPriority` | Severity-based priority before AI adjustment |

### RemediationPriority (Enum)

| Value | Label | Description |
|-------|-------|-------------|
| P0 | Immediate | Critical findings — fix now |
| P1 | Within24Hours | High findings — fix within 24 hours |
| P2 | Week1 | Medium findings — fix within 7 days |
| P3 | Month1 | Low findings — fix within 30 days |
| P4 | Backlog | Other findings — best effort |

---

## Options Types

### RemediationExecutionOptions

Runtime options for a single remediation execution.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| DryRun | `bool` | `false` | Preview only, no changes applied |
| RequireApproval | `bool` | `false` | Enter Pending state before executing |
| AutoValidate | `bool` | `true` | Validate after execution |
| AutoRollbackOnFailure | `bool` | `false` | Auto-rollback if validation fails |
| UseAiScript | `bool` | `true` | Attempt AI script generation (Tier 1) |

### BatchRemediationOptions

Options for batch remediation operations.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| MaxConcurrentRemediations | `int` | `3` | SemaphoreSlim limit |
| FailFast | `bool` | `false` | Cancel remaining on first failure |
| ContinueOnError | `bool` | `true` | Continue batch on individual failure |
| DryRun | `bool` | `false` | Preview only for entire batch |

### RemediationPlanOptions

Filters for plan generation.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| MinSeverity | `string?` | `null` | Minimum severity to include |
| IncludeFamilies | `List<string>?` | `null` | Only include these control families |
| ExcludeFamilies | `List<string>?` | `null` | Exclude these control families |
| AutomatableOnly | `bool` | `false` | Only include auto-remediable findings |
| GroupByResource | `bool` | `false` | Group items by Azure resource |

### RemediationOptions (Configuration)

Configuration sub-class added to `ComplianceAgentOptions`.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| MaxConcurrentRemediations | `int` | `3` | Default concurrency limit |
| ScriptTimeoutSeconds | `int` | `300` | Script execution timeout (5 min) |
| MaxRetries | `int` | `3` | Max retry attempts |
| RequireApproval | `bool` | `false` | Default approval requirement |
| AutoValidate | `bool` | `true` | Default auto-validate |
| AutoRollbackOnFailure | `bool` | `false` | Default auto-rollback |
| UseAiScript | `bool` | `true` | Default AI script usage |

---

## Executive Summary Types

### RemediationExecutiveSummary

Plan-level executive summary.

| Property | Type | Description |
|----------|------|-------------|
| TotalFindings | `int` | Total findings in plan |
| CriticalCount | `int` | Critical severity count |
| HighCount | `int` | High severity count |
| MediumCount | `int` | Medium severity count |
| LowCount | `int` | Low severity count |
| AutoRemediableCount | `int` | Auto-remediable findings |
| ManualCount | `int` | Manual-only findings |
| TotalEstimatedEffort | `TimeSpan` | Sum of all item durations |
| ProjectedRiskReduction | `double` | Projected risk reduction percentage |

### RiskMetrics

Risk scoring for a plan or analysis.

| Property | Type | Description |
|----------|------|-------------|
| CurrentRiskScore | `double` | Severity-weighted current risk |
| ProjectedRiskScore | `double` | Risk after remediation |
| RiskReductionPercentage | `double` | Reduction percentage |

---

## Relationships

```text
RemediationPlan
  ├── Items: List<RemediationItem>
  │     ├── Finding: ComplianceFinding
  │     ├── Steps: List<RemediationStep>
  │     └── Dependencies: List<string> → other RemediationItem.Id
  ├── Timeline: ImplementationTimeline
  │     └── Phases: List<TimelinePhase>
  │           └── Items: List<RemediationItem>
  ├── ExecutiveSummary: RemediationExecutiveSummary
  └── RiskMetrics: RiskMetrics

RemediationExecution
  ├── FindingId → ComplianceFinding.Id
  ├── Options: RemediationExecutionOptions
  └── Status: RemediationExecutionStatus

BatchRemediationResult
  ├── Executions: List<RemediationExecution>
  ├── Summary: BatchRemediationSummary
  └── Options: BatchRemediationOptions

RemediationValidationResult
  ├── ExecutionId → RemediationExecution.Id
  └── Checks: List<ValidationCheck>

RemediationRollbackResult
  └── ExecutionId → RemediationExecution.Id

RemediationImpactAnalysis
  └── ResourceImpacts: List<ResourceImpact>

RemediationHistory
  ├── Executions: List<RemediationExecution>
  └── Metrics: RemediationMetric

ManualRemediationGuide
  └── FindingId → ComplianceFinding.Id

RemediationScript (standalone)
RemediationGuidance → FindingId → ComplianceFinding.Id
PrioritizedFinding → Finding: ComplianceFinding
```
