# Data Model: AI-Powered Task Enrichment

**Feature**: 012-task-enrichment  
**Date**: 2026-02-25  
**Status**: Complete

---

## Entity Changes

### Modified Entity: `RemediationTask`

**Location**: `src/Ato.Copilot.Core/Models/Kanban/KanbanModels.cs`

| Field | Type | Status | Description |
|-------|------|--------|-------------|
| `Id` | `string` | Existing | GUID primary key |
| `TaskNumber` | `string` | Existing | Human-readable ID (e.g., REM-001) |
| `BoardId` | `string` | Existing | FK to RemediationBoard |
| `Title` | `string` | Existing | Task title from finding |
| `Description` | `string` | Existing | Finding description |
| `ControlId` | `string` | Existing | NIST control reference |
| `ControlFamily` | `string` | Existing | Two-letter family code |
| `Severity` | `FindingSeverity` | Existing | Finding severity level |
| `Status` | `TaskStatus` | Existing | Kanban column |
| `AssigneeId` | `string?` | Existing | Assigned user ID |
| `AssigneeName` | `string?` | Existing | Assigned user display name |
| `DueDate` | `DateTime` | Existing | SLA-derived due date |
| `AffectedResources` | `List<string>` | Existing | Azure resource IDs |
| `RemediationScript` | `string?` | Existing | **NOW POPULATED** — AI or NIST-template script content |
| `ValidationCriteria` | `string?` | Existing | **NOW POPULATED** — AI or template verification steps |
| **`RemediationScriptType`** | **`string?`** | **NEW** | Script language identifier: "AzureCli", "PowerShell", "Bicep", "Terraform". Used for syntax highlighting in UI. |
| `FindingId` | `string?` | Existing | FK to ComplianceFinding for traceability |
| `LinkedAlertId` | `string?` | Existing | FK to ComplianceAlert |
| `CreatedBy` | `string` | Existing | Task creator |
| `Comments` | `List<TaskComment>` | Existing | Child comments |
| `History` | `List<TaskHistoryEntry>` | Existing | Audit trail |

**EF Configuration** (addition to `AtoCopilotContext.OnModelCreating`):
```csharp
entity.Property(e => e.RemediationScriptType).HasMaxLength(20);
```

**Validation Rules**:
- `RemediationScriptType` must be one of: `"AzureCli"`, `"PowerShell"`, `"Bicep"`, `"Terraform"`, or null
- `RemediationScript` max length 8000 (existing constraint — unchanged)
- `ValidationCriteria` max length 2000 (existing constraint — unchanged)

---

### New Model: `TaskEnrichmentResult`

**Location**: `src/Ato.Copilot.Core/Models/Kanban/KanbanModels.cs` (append to file)

```csharp
/// <summary>
/// Result of enriching a single task with remediation script and validation criteria.
/// </summary>
public class TaskEnrichmentResult
{
    /// <summary>Task GUID.</summary>
    public string TaskId { get; set; } = "";

    /// <summary>Human-readable task number (e.g., REM-001).</summary>
    public string TaskNumber { get; set; } = "";

    /// <summary>Whether a remediation script was generated or updated.</summary>
    public bool ScriptGenerated { get; set; }

    /// <summary>Whether validation criteria was generated or updated.</summary>
    public bool ValidationCriteriaGenerated { get; set; }

    /// <summary>How the content was generated: "AI" or "Template".</summary>
    public string GenerationMethod { get; set; } = "";

    /// <summary>Script type used (e.g., "AzureCli").</summary>
    public string? ScriptType { get; set; }

    /// <summary>Error message if enrichment failed for this task.</summary>
    public string? Error { get; set; }

    /// <summary>Whether enrichment was skipped (task already had script and force=false).</summary>
    public bool Skipped { get; set; }
}
```

**Fields**: 7 properties. No relationships. Not persisted to database — used as a return value.

---

### New Model: `BoardEnrichmentResult`

**Location**: `src/Ato.Copilot.Core/Models/Kanban/KanbanModels.cs` (append to file)

```csharp
/// <summary>
/// Aggregate result of enriching all tasks on a board.
/// </summary>
public class BoardEnrichmentResult
{
    /// <summary>Board GUID.</summary>
    public string BoardId { get; set; } = "";

    /// <summary>Number of tasks that received new enrichment content.</summary>
    public int TasksEnriched { get; set; }

    /// <summary>Number of tasks skipped (already had content).</summary>
    public int TasksSkipped { get; set; }

    /// <summary>Number of tasks where enrichment failed.</summary>
    public int TasksFailed { get; set; }

    /// <summary>Total tasks on the board.</summary>
    public int TotalTasks { get; set; }

    /// <summary>Wall-clock duration of the enrichment operation.</summary>
    public TimeSpan Duration { get; set; }

    /// <summary>Per-task enrichment results.</summary>
    public List<TaskEnrichmentResult> Results { get; set; } = new();
}
```

**Fields**: 7 properties. Not persisted to database — used as a return value.

---

## New Interface: `ITaskEnrichmentService`

**Location**: `src/Ato.Copilot.Core/Interfaces/Kanban/ITaskEnrichmentService.cs`

```csharp
using Ato.Copilot.Core.Models.Compliance;
using Ato.Copilot.Core.Models.Kanban;

namespace Ato.Copilot.Core.Interfaces.Kanban;

/// <summary>
/// FeatureSpec: 012-task-enrichment
/// Enriches remediation tasks with AI-generated remediation scripts and validation criteria.
/// Uses IRemediationEngine (AI-first → NIST-template fallback) for script generation.
/// </summary>
public interface ITaskEnrichmentService
{
    /// <summary>
    /// Enriches a single task with remediation script and validation criteria.
    /// </summary>
    /// <param name="task">The task to enrich (modified in-place).</param>
    /// <param name="finding">Linked compliance finding (null for manually created tasks).</param>
    /// <param name="scriptType">Target script language (default: AzureCli).</param>
    /// <param name="force">If true, regenerate even if content already exists.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Enrichment result with generation method and status.</returns>
    Task<TaskEnrichmentResult> EnrichTaskAsync(
        RemediationTask task,
        ComplianceFinding? finding,
        ScriptType scriptType = ScriptType.AzureCli,
        bool force = false,
        CancellationToken ct = default);

    /// <summary>
    /// Enriches all tasks on a board with bounded concurrency.
    /// </summary>
    /// <param name="board">Board containing tasks to enrich.</param>
    /// <param name="findings">Assessment findings for context lookup.</param>
    /// <param name="progress">Optional progress reporter for streaming updates.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Aggregate enrichment result.</returns>
    Task<BoardEnrichmentResult> EnrichBoardTasksAsync(
        RemediationBoard board,
        IReadOnlyList<ComplianceFinding> findings,
        IProgress<string>? progress = null,
        CancellationToken ct = default);

    /// <summary>
    /// Generates validation criteria for a finding using AI or templates.
    /// </summary>
    /// <param name="finding">Finding to generate criteria for.</param>
    /// <param name="scriptContent">Optional: the remediation script content for context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Validation criteria text.</returns>
    Task<string> GenerateValidationCriteriaAsync(
        ComplianceFinding finding,
        string? scriptContent = null,
        CancellationToken ct = default);
}
```

---

## Relationship Diagram

```
ComplianceAssessment (1)
    └── ComplianceFinding (N)
            └── RemediationTask (1)  ← FindingId FK
                    ├── RemediationScript (string, enriched)
                    ├── RemediationScriptType (string, NEW)
                    ├── ValidationCriteria (string, enriched)
                    └── via ITaskEnrichmentService:
                          ├── IRemediationEngine.GenerateRemediationScriptAsync()
                          │     ├── AiRemediationPlanGenerator.GenerateScriptAsync() [AI]
                          │     └── NistRemediationStepsService.GetRemediationSteps() [fallback]
                          └── TaskEnrichmentService.GenerateValidationCriteriaAsync()
                                ├── IChatClient prompt [AI]
                                └── Template string [fallback]
```

---

## State Transitions

### Task Enrichment States

```
┌──────────────┐
│  Unenriched  │ ← Initial state (RemediationScript = null)
└──────┬───────┘
       │ EnrichTaskAsync() called (board creation, lazy view, on-demand tool)
       ▼
┌──────────────┐
│  Enriched    │ ← RemediationScript != null, ValidationCriteria != null
└──────┬───────┘
       │ kanban_generate_script(force=true) or kanban_generate_validation(force=true)
       ▼
┌──────────────┐
│ Re-enriched  │ ← Content regenerated (new AI response or updated template)
└──────────────┘
```

### Enrichment Decision Flow

```
EnrichTaskAsync(task, finding, scriptType, force):
  1. if task.RemediationScript != null AND force == false → SKIP (return Skipped=true)
  2. if finding == null → SKIP (no context for generation)
  3. if finding.Severity == Informational:
       task.RemediationScript = "Informational finding — no remediation required"
       task.RemediationScriptType = null
       task.ValidationCriteria = "STIG reference — no validation required"
       return (GenerationMethod = "Template")
  4. script = IRemediationEngine.GenerateRemediationScriptAsync(finding, scriptType)
     task.RemediationScript = script.Content
     task.RemediationScriptType = scriptType.ToString()
  5. if task.ValidationCriteria == null OR force:
       task.ValidationCriteria = GenerateValidationCriteriaAsync(finding, script.Content)
  6. return (GenerationMethod = "AI" or "Template" based on script.IsSanitized check)
```
