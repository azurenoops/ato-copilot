# Interface Contract: IRemediationEngine

**Feature**: 009-remediation-engine | **Date**: 2026-02-24
**File**: `src/Ato.Copilot.Core/Interfaces/Compliance/IComplianceInterfaces.cs`

## Overview

`IRemediationEngine` is the primary interface for all remediation operations. It is extended from 4 methods to 18 unique method names (21 total signatures including overloads) organized into three tiers. All existing method signatures are **preserved** for backward compatibility.

## Consumers

| Consumer | Methods Used | Location |
|----------|-------------|----------|
| KanbanService | `ExecuteRemediationAsync(string, bool, bool, CancellationToken)` | L700-780 |
| ComplianceWatchService | `BatchRemediateAsync(string?, string?, string?, bool, CancellationToken)` | L960-1005 |
| RemediationExecuteTool | `ExecuteRemediationAsync`, `BatchRemediateAsync` | ComplianceTools.cs L398 |
| ValidateRemediationTool | `ValidateRemediationAsync` | ComplianceTools.cs L455 |
| RemediationPlanTool | `GeneratePlanAsync` | ComplianceTools.cs L494 |

## Interface Definition

```csharp
public interface IRemediationEngine
{
    // ═══════════════════════════════════════════════════════
    // TIER 1: EXISTING METHODS (backward compatible)
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Generates a remediation plan for a subscription based on latest assessment.
    /// </summary>
    /// <param name="subscriptionId">Azure subscription ID</param>
    /// <param name="resourceGroupName">Optional resource group filter</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>A RemediationPlan with steps and timeline</returns>
    Task<RemediationPlan> GeneratePlanAsync(
        string subscriptionId,
        string? resourceGroupName = null,
        CancellationToken ct = default);

    /// <summary>
    /// Executes remediation for a single finding (existing signature).
    /// Returns JSON string result for backward compatibility.
    /// </summary>
    /// <param name="findingId">Finding to remediate</param>
    /// <param name="applyRemediation">Whether to apply (true) or dry-run (false)</param>
    /// <param name="dryRun">Explicit dry-run flag</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>JSON string with execution result</returns>
    Task<string> ExecuteRemediationAsync(
        string findingId,
        bool applyRemediation = false,
        bool dryRun = true,
        CancellationToken ct = default);

    /// <summary>
    /// Validates a remediation execution (existing signature).
    /// Returns JSON string result for backward compatibility.
    /// </summary>
    /// <param name="findingId">Finding to validate</param>
    /// <param name="executionId">Optional execution ID</param>
    /// <param name="subscriptionId">Optional subscription scope</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>JSON string with validation result</returns>
    Task<string> ValidateRemediationAsync(
        string findingId,
        string? executionId = null,
        string? subscriptionId = null,
        CancellationToken ct = default);

    /// <summary>
    /// Batch remediates findings by filter criteria (existing signature).
    /// Returns JSON string result for backward compatibility.
    /// </summary>
    /// <param name="subscriptionId">Optional subscription filter</param>
    /// <param name="severity">Optional severity filter</param>
    /// <param name="controlFamily">Optional control family filter</param>
    /// <param name="dryRun">Dry-run flag</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>JSON string with batch result</returns>
    Task<string> BatchRemediateAsync(
        string? subscriptionId = null,
        string? severity = null,
        string? controlFamily = null,
        bool dryRun = true,
        CancellationToken ct = default);

    // ═══════════════════════════════════════════════════════
    // TIER 2: ENHANCED CORE OPERATIONS (new methods)
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Generates an enhanced remediation plan from a collection of findings
    /// with filtering, prioritization, timeline, and risk scoring.
    /// </summary>
    /// <param name="findings">Findings to plan for</param>
    /// <param name="options">Plan generation options (filters)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Enhanced RemediationPlan with items, timeline, and risk metrics</returns>
    Task<RemediationPlan> GenerateRemediationPlanAsync(
        IEnumerable<ComplianceFinding> findings,
        RemediationPlanOptions? options = null,
        CancellationToken ct = default);

    /// <summary>
    /// Generates a remediation plan for a single finding using 3-tier fallback.
    /// </summary>
    Task<RemediationPlan> GenerateRemediationPlanAsync(
        ComplianceFinding finding,
        CancellationToken ct = default);

    /// <summary>
    /// Generates a remediation plan for a subscription with enhanced options.
    /// </summary>
    Task<RemediationPlan> GenerateRemediationPlanAsync(
        string subscriptionId,
        RemediationPlanOptions? options = null,
        CancellationToken ct = default);

    /// <summary>
    /// Executes remediation for a single finding with typed options and result.
    /// </summary>
    /// <param name="findingId">Finding to remediate</param>
    /// <param name="options">Execution options</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Typed execution result</returns>
    Task<RemediationExecution> ExecuteRemediationAsync(
        string findingId,
        RemediationExecutionOptions options,
        CancellationToken ct = default);

    /// <summary>
    /// Executes batch remediation with concurrency control and typed results.
    /// </summary>
    /// <param name="findingIds">Findings to remediate</param>
    /// <param name="options">Batch options</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Typed batch result</returns>
    Task<BatchRemediationResult> ExecuteBatchRemediationAsync(
        IEnumerable<string> findingIds,
        BatchRemediationOptions? options = null,
        CancellationToken ct = default);

    /// <summary>
    /// Rolls back a previously executed remediation using the before-snapshot.
    /// </summary>
    /// <param name="executionId">Execution to roll back</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Rollback result</returns>
    Task<RemediationRollbackResult> RollbackRemediationAsync(
        string executionId,
        CancellationToken ct = default);

    /// <summary>
    /// Validates a remediation execution with typed result.
    /// Checks execution status, steps completed, and changes applied.
    /// </summary>
    /// <param name="executionId">Execution to validate</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Typed validation result with per-check details</returns>
    Task<RemediationValidationResult> ValidateRemediationAsync(
        string executionId,
        CancellationToken ct = default);

    // ═══════════════════════════════════════════════════════
    // TIER 3: WORKFLOW & TRACKING (new methods)
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Gets progress snapshot for a subscription (last 30 days).
    /// </summary>
    Task<RemediationProgress> GetRemediationProgressAsync(
        string? subscriptionId = null,
        CancellationToken ct = default);

    /// <summary>
    /// Gets execution history for a date range with optional pagination.
    /// </summary>
    Task<RemediationHistory> GetRemediationHistoryAsync(
        DateTime startDate,
        DateTime endDate,
        string? subscriptionId = null,
        int skip = 0,
        int take = 50,
        CancellationToken ct = default);

    /// <summary>
    /// Analyzes remediation impact before execution.
    /// </summary>
    Task<RemediationImpactAnalysis> AnalyzeRemediationImpactAsync(
        IEnumerable<ComplianceFinding> findings,
        CancellationToken ct = default);

    /// <summary>
    /// Generates a manual remediation guide for a non-automatable finding.
    /// </summary>
    Task<ManualRemediationGuide> GenerateManualRemediationGuideAsync(
        ComplianceFinding finding,
        CancellationToken ct = default);

    /// <summary>
    /// Gets all active remediation workflows (pending, in-progress, recent).
    /// </summary>
    Task<RemediationWorkflowStatus> GetActiveRemediationWorkflowsAsync(
        string? subscriptionId = null,
        CancellationToken ct = default);

    /// <summary>
    /// Processes an approval or rejection for a pending remediation.
    /// </summary>
    Task<RemediationApprovalResult> ProcessRemediationApprovalAsync(
        string executionId,
        bool approve,
        string approverName,
        string? comments = null,
        CancellationToken ct = default);

    /// <summary>
    /// Schedules a remediation for future execution.
    /// </summary>
    Task<RemediationScheduleResult> ScheduleRemediationAsync(
        IEnumerable<string> findingIds,
        DateTime scheduledTime,
        BatchRemediationOptions? options = null,
        CancellationToken ct = default);

    /// <summary>
    /// Generates a remediation script using AI or deterministic fallback.
    /// </summary>
    Task<RemediationScript> GenerateRemediationScriptAsync(
        ComplianceFinding finding,
        ScriptType scriptType = ScriptType.AzureCli,
        CancellationToken ct = default);

    /// <summary>
    /// Gets AI-enhanced remediation guidance for a finding.
    /// </summary>
    Task<RemediationGuidance> GetRemediationGuidanceAsync(
        ComplianceFinding finding,
        CancellationToken ct = default);

    /// <summary>
    /// Prioritizes findings using AI with business context.
    /// </summary>
    Task<List<PrioritizedFinding>> PrioritizeFindingsWithAiAsync(
        IEnumerable<ComplianceFinding> findings,
        string? businessContext = null,
        CancellationToken ct = default);
}
```

## Supporting Service Interfaces

### IAiRemediationPlanGenerator

```csharp
public interface IAiRemediationPlanGenerator
{
    bool IsAvailable { get; }
    Task<RemediationScript?> GenerateScriptAsync(ComplianceFinding finding, ScriptType scriptType, CancellationToken ct = default);
    Task<RemediationGuidance?> GetGuidanceAsync(ComplianceFinding finding, CancellationToken ct = default);
    Task<List<PrioritizedFinding>> PrioritizeAsync(IEnumerable<ComplianceFinding> findings, string? businessContext, CancellationToken ct = default);
    Task<RemediationPlan?> GenerateEnhancedPlanAsync(ComplianceFinding finding, CancellationToken ct = default);
}
```

### IRemediationScriptExecutor

```csharp
public interface IRemediationScriptExecutor
{
    Task<RemediationExecution> ExecuteScriptAsync(RemediationScript script, string findingId, RemediationExecutionOptions options, CancellationToken ct = default);
}
```

### INistRemediationStepsService

```csharp
public interface INistRemediationStepsService
{
    List<string> GetRemediationSteps(string controlFamily, string controlId);
    List<string> ParseStepsFromGuidance(string guidanceText);
    string GetSkillLevel(string controlFamily);
}
```

### IAzureArmRemediationService

```csharp
public interface IAzureArmRemediationService
{
    Task<string?> CaptureResourceSnapshotAsync(string resourceId, CancellationToken ct = default);
    Task<RemediationExecution> ExecuteArmRemediationAsync(ComplianceFinding finding, RemediationExecutionOptions options, CancellationToken ct = default);
    Task<RemediationRollbackResult> RestoreFromSnapshotAsync(string resourceId, string snapshotJson, CancellationToken ct = default);
}
```

### IComplianceRemediationService

```csharp
public interface IComplianceRemediationService
{
    Task<RemediationExecution> ExecuteStructuredRemediationAsync(ComplianceFinding finding, RemediationExecutionOptions options, CancellationToken ct = default);
    bool CanHandle(ComplianceFinding finding);
}
```

### IScriptSanitizationService

```csharp
public interface IScriptSanitizationService
{
    bool IsSafe(string scriptContent);
    List<string> GetViolations(string scriptContent);
}
```

## Backward Compatibility

The 4 existing methods on `IRemediationEngine` are **preserved without signature changes**:
- `GeneratePlanAsync` — continues to return `RemediationPlan` (with new optional properties populated)
- `ExecuteRemediationAsync(string, bool, bool, CancellationToken)` — continues to return JSON string
- `ValidateRemediationAsync` — continues to return JSON string
- `BatchRemediateAsync` — continues to return JSON string

New consumers should prefer the typed overloads (Tier 2/3 methods) for structured data:
- `ExecuteRemediationAsync(string, RemediationExecutionOptions, CancellationToken)` — returns `RemediationExecution`
- `ValidateRemediationAsync(string, CancellationToken)` — returns `RemediationValidationResult`
- `ExecuteBatchRemediationAsync` — returns `BatchRemediationResult`
- `GenerateRemediationPlanAsync` (3 overloads) — returns enhanced `RemediationPlan`
