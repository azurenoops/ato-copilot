# Feature Specification: AI-Powered Task Enrichment — Remediation Scripts & Validation Criteria

**Feature Branch**: `012-task-enrichment`  
**Created**: 2026-02-25  
**Status**: Draft  
**Depends On**: 011-azure-openai-agents (IChatClient + AiRemediationPlanGenerator wiring)  
**Input**: Gap analysis — existing `IAiRemediationPlanGenerator` and `AtoRemediationEngine.GenerateRemediationScriptAsync()` are fully implemented but never called during the assessment → board → task pipeline. Tasks are created with null `RemediationScript` and null `ValidationCriteria`.

---

## Problem Statement

When a compliance assessment generates findings and a Kanban board is created from those findings, each `RemediationTask` is created with:

- **`RemediationScript` = null** — No scanner sets `ComplianceFinding.RemediationScript`. The `CreateTaskFromFinding()` method copies `finding.RemediationScript` (always null) to the task.
- **`ValidationCriteria` = null** — Nothing in the codebase ever generates or assigns validation criteria to a task.
- **`AffectedResources` = subscription GUID** — STIG findings use bare subscription GUID instead of Azure resource paths. Other scanners use `/subscriptions/{id}` but never include specific resource IDs from Azure Resource Graph results.

Meanwhile, fully implemented AI services sit unused:

| Service | Method | Status |
|---------|--------|--------|
| `IAiRemediationPlanGenerator` | `GenerateScriptAsync(finding, scriptType)` | ✅ Implemented, uses Azure OpenAI |
| `IAiRemediationPlanGenerator` | `GetGuidanceAsync(finding)` | ✅ Implemented, uses Azure OpenAI |
| `IRemediationEngine` | `GenerateRemediationScriptAsync(finding, scriptType)` | ✅ Implemented, AI first → NIST fallback |
| `IRemediationEngine` | `GetRemediationGuidanceAsync(finding)` | ✅ Implemented, AI first → fallback |
| `IScriptSanitizationService` | `IsSafe(script)`, `GetViolations(script)` | ✅ Implemented |

The user sees "Not provided" for Remediation Script and Validation Criteria on every task detail view, and `kanban_remediate_task` fails with "no remediation script".

---

## User Scenarios & Testing

### User Story 1 — Auto-Enrich Tasks at Board Creation (Priority: P1)

As a compliance analyst, I want remediation tasks to be automatically populated with AI-generated remediation scripts and validation criteria when a board is created from an assessment, so that I can immediately see actionable remediation steps and know how to verify fixes.

**Why this priority**: This is the core gap. Without enrichment at creation time, every task starts empty and requires manual intervention or a separate tool call. Enrichment at creation time delivers immediate value with zero user action.

**Independent Test**: Create a board from a test assessment with mock findings. Verify each resulting task has a non-null `RemediationScript` and non-null `ValidationCriteria`.

**Acceptance Scenarios**:

1. **Given** an assessment with Open findings and `AgentAIEnabled=true` with a functioning `IChatClient`, **When** `CreateBoardFromAssessmentAsync` is called, **Then** each task is created with an AI-generated `RemediationScript` (Azure CLI format) populated from `IRemediationEngine.GenerateRemediationScriptAsync()`.
2. **Given** an assessment with Open findings and `AgentAIEnabled=true`, **When** the board is created, **Then** each task has a `ValidationCriteria` string describing how to verify the remediation was successful.
3. **Given** AI is unavailable (`IChatClient` is null or `AgentAIEnabled=false`), **When** the board is created, **Then** each task still receives a deterministic template-based `RemediationScript` from the NIST-steps fallback path, and a template-based `ValidationCriteria`.
4. **Given** AI script generation fails for a specific finding (timeout, rate limit, content filter), **When** that task is created, **Then** the task receives the deterministic fallback script and the error is logged. Other tasks are not affected.
5. **Given** an assessment with 30+ findings, **When** the board is created, **Then** enrichment completes within 60 seconds (parallel AI calls with bounded concurrency) and progress is reported via `IProgress<string>` if available.

---

### User Story 2 — On-Demand Script Generation Tool (Priority: P2)

As a compliance analyst viewing a task with no remediation script (or wanting an alternative script type), I want to ask the AI to generate a remediation script on demand so that I can get actionable remediation for any task at any time.

**Why this priority**: Covers the case where board-creation enrichment was skipped (AI was off), where the analyst wants a different script type (PowerShell vs. Azure CLI), or where the analyst wants to regenerate after the resource context has changed.

**Independent Test**: Call the new MCP tool with a task ID and script type. Verify it returns a generated script and persists it to the task.

**Acceptance Scenarios**:

1. **Given** a task with no `RemediationScript`, **When** the user invokes `kanban_generate_script` with the task ID, **Then** the tool generates an Azure CLI script via `IRemediationEngine.GenerateRemediationScriptAsync()` using the task's finding context, persists it to the task, and returns the script content.
2. **Given** a task with an existing `RemediationScript`, **When** the user invokes `kanban_generate_script` with `force=true`, **Then** the existing script is replaced with a newly generated one.
3. **Given** a task ID, **When** the user specifies `script_type=PowerShell`, **Then** the generated script uses PowerShell syntax instead of the default Azure CLI.
4. **Given** AI is unavailable, **When** the tool is called, **Then** it falls back to the deterministic NIST-steps-based script template and returns it.

---

### User Story 3 — On-Demand Validation Criteria Generation Tool (Priority: P2)

As a compliance analyst, I want to generate or update validation criteria for a task so that I know exactly how to verify a remediation was applied correctly.

**Why this priority**: Complements US2 — analysts need both the "how to fix" (script) and the "how to verify" (criteria). Also supports re-generation when task context changes.

**Acceptance Scenarios**:

1. **Given** a task with no `ValidationCriteria`, **When** the user invokes `kanban_generate_validation` with the task ID, **Then** the tool generates validation criteria using AI guidance and persists it to the task.
2. **Given** AI is unavailable, **When** the tool is called, **Then** it generates template-based validation criteria: "Re-scan resource {ResourceId} for control {ControlId} and verify finding status changed to Remediated."
3. **Given** a task with existing `ValidationCriteria`, **When** the user invokes `kanban_generate_validation` with `force=false` (default), **Then** the tool returns a `CRITERIA_EXISTS` error with a suggestion to use `force=true`.

---

### User Story 4 — Enrichment on Task Detail View (Priority: P3)

As a user viewing task details, I want empty `RemediationScript` and `ValidationCriteria` fields to be lazily populated via AI when I view the task, so that legacy tasks created before this feature also benefit.

**Why this priority**: Nice-to-have for backward compatibility. Enriches existing tasks without requiring a full re-assessment. Lower priority because US1 solves the problem for new boards and US2/US3 provide manual tools.

**Acceptance Scenarios**:

1. **Given** a task with null `RemediationScript` and `AgentAIEnabled=true`, **When** `kanban_get_task` is called, **Then** the tool lazily generates and persists a remediation script before returning the task details.
2. **Given** a task that already has a `RemediationScript`, **When** `kanban_get_task` is called, **Then** no regeneration occurs and the existing script is returned as-is.
3. **Given** AI is unavailable, **When** `kanban_get_task` is called for a task with null script, **Then** the deterministic fallback is used.

---

### User Story 5 — Board Update Enrichment (Priority: P2)

As a compliance analyst, I want new tasks added during a board update (from a re-assessment) to also receive AI-generated scripts and validation criteria, so that incremental updates maintain the same enrichment quality.

**Acceptance Scenarios**:

1. **Given** an existing board and a new assessment with additional findings, **When** `UpdateBoardFromAssessmentAsync` is called, **Then** newly created tasks are enriched with remediation scripts and validation criteria just like during initial board creation.
2. **Given** existing tasks on the board that already have scripts, **When** the board is updated, **Then** existing task scripts are not overwritten.

---

## Clarifications

### Session 2026-02-25

- Q: Should board-level enrichment be synchronous (blocking), asynchronous (background), or hybrid? → A: Synchronous — block until enrichment completes, stream progress via existing IProgress/SignalR.
- Q: Should we store only the script content string, or also persist the script type (AzureCli/PowerShell/Bicep/Terraform)? → A: Add a `RemediationScriptType` string property to `RemediationTask` for syntax highlighting and language identification.
- Q: Should enrichment generate scripts for Informational-severity tasks (e.g., STIG mapping findings)? → A: Skip — set RemediationScript to "Informational finding — no remediation required" and do not call AI.
- Q: On AI failure (429, timeout, transient error), should the enrichment service retry or immediately fall back to templates? → A: Immediate fallback — no retry logic in enrichment layer; use deterministic NIST-template script. Users can regenerate on-demand via kanban_generate_script (US2).
- Q: Should lazy enrichment on `kanban_get_task` (US4) be enabled by default or require opt-in? → A: Enabled by default — auto-fires on any task with null script when AI is available. One-time 3–5s cost per task; `AgentAIEnabled` serves as global kill switch.

### Checklist-Driven Clarifications (CHK Audit)

- **CHK005 — Partial Enrichment**: `EnrichTaskAsync(force=false)` treats the task atomically — if `RemediationScript` is already set, the entire method skips (including validation criteria generation). For tasks that have a script but no validation criteria, use `kanban_generate_validation` directly. This is by design: `EnrichTaskAsync` is the "enrich-everything" path; on-demand tools handle individual fields.

- **CHK009 — Structured Logging Fields**: All enrichment entry points MUST use structured logging with these properties: `{TaskId}`, `{TaskNumber}`, `{ControlId}`, `{GenerationMethod}` ("AI" | "Template" | "Skipped"), `{DurationMs}`. Board-level logging adds: `{BoardId}`, `{Enriched}`, `{Skipped}`, `{Failed}`, `{TotalTasks}`, `{TotalDurationMs}`.

- **CHK010 — Enrichment History**: Enrichment events are NOT recorded as `TaskHistoryEntry` entries. Rationale: enrichment is an automated background operation, not a user action. The `GenerationMethod` field on `TaskEnrichmentResult` and structured logs provide audit trail. On-demand tool invocations (US2/US3) MAY add a history entry to record explicit user-initiated regeneration.

- **CHK013 — AI vs Template Detection**: `GenerationMethod` is determined by checking `_aiGenerator.IsAvailable` (which reflects `IChatClient != null && AgentAIEnabled == true`) BEFORE calling `IRemediationEngine`. If available → `"AI"` (even if engine internally fell back due to transient error). If unavailable → `"Template"`. This approximation is acceptable because transient fallbacks are rare and logged separately by `IRemediationEngine`.

- **CHK017 — "AI Unavailable" Definition**: All three phrasings — "`IChatClient` is null", "`AgentAIEnabled=false`", "AI is unavailable" — refer to the same single condition: `_aiGenerator.IsAvailable == false`. This is true when either (a) `AgentAIEnabled=false` in configuration, or (b) Feature 011 is not deployed (`IChatClient` not registered). There is no distinction between them.

- **CHK036/CHK037 — Content Length Limits**: AI-generated content exceeding `varchar(8000)` for `RemediationScript` or `varchar(2000)` for `ValidationCriteria` MUST be truncated to the column limit with a trailing `\n<!-- Truncated -->` marker. The AI prompt SHOULD include a max-length instruction to minimize truncation occurrence.

- **CHK039 — Empty Engine Content**: If `IRemediationEngine.GenerateRemediationScriptAsync` returns a `RemediationScript` with empty or whitespace-only `Content`, treat it as a failure: log a warning and fall back to the NIST template path. The never-null contract (R1) does not guarantee non-empty content.

- **CHK053 — Service Resolution in Tools**: New MCP tools resolve `ITaskEnrichmentService` via `serviceProvider.GetService<ITaskEnrichmentService>()` (optional, returns null if not registered), NOT `GetRequiredService`. This matches the `KanbanService` pattern where `ITaskEnrichmentService?` is injected as optional. If null, tools return an error message indicating enrichment is not available.

- **CHK054 — Error Envelope vs Silent Enrich**: On-demand tools (`kanban_generate_script`, `kanban_generate_validation`) return explicit error envelopes (e.g., `SCRIPT_EXISTS`, `CRITERIA_EXISTS`) because they are user-initiated actions requiring confirmation. Lazy enrichment in `kanban_get_task` is silent and transparent — it enriches without user confirmation because the user didn't explicitly request generation. These are intentionally different UX patterns.

---

## Technical Design

### Architecture Overview

```
Assessment Scan → Findings Created → Board Created → Tasks Created
                                                        ↓
                                              ┌─── Task Enrichment Service ───┐
                                              │                               │
                                              │  For each task:               │
                                              │  1. Resolve linked finding    │
                                              │  2. Generate RemediationScript│
                                              │     via IRemediationEngine    │
                                              │  3. Generate ValidationCriteria│
                                              │     via AI or template        │
                                              │  4. Persist to task           │
                                              └───────────────────────────────┘
```

### New Components

#### 1. `ITaskEnrichmentService` Interface

**Location**: `src/Ato.Copilot.Core/Interfaces/Kanban/ITaskEnrichmentService.cs`

```csharp
public interface ITaskEnrichmentService
{
    /// <summary>
    /// Enriches a single task with AI-generated remediation script and validation criteria.
    /// Uses AI when available, falls back to deterministic templates.
    /// </summary>
    Task<TaskEnrichmentResult> EnrichTaskAsync(
        RemediationTask task,
        ComplianceFinding? finding,
        ScriptType scriptType = ScriptType.AzureCli,
        bool force = false,
        CancellationToken ct = default);

    /// <summary>
    /// Enriches all tasks on a board in parallel with bounded concurrency.
    /// </summary>
    Task<BoardEnrichmentResult> EnrichBoardTasksAsync(
        RemediationBoard board,
        IReadOnlyList<ComplianceFinding> findings,
        IProgress<string>? progress = null,
        CancellationToken ct = default);

    /// <summary>
    /// Generates validation criteria for a finding using AI or templates.
    /// </summary>
    Task<string> GenerateValidationCriteriaAsync(
        ComplianceFinding finding,
        string? scriptContent = null,
        CancellationToken ct = default);
}
```

#### 2. `TaskEnrichmentService` Implementation

**Location**: `src/Ato.Copilot.Agents/Compliance/Services/TaskEnrichmentService.cs`

**Dependencies**:
- `IRemediationEngine` — for `GenerateRemediationScriptAsync()` (AI-first with NIST fallback)
- `IChatClient?` — for dedicated validation criteria AI prompt (per research R3; does NOT reuse `IAiRemediationPlanGenerator.GetGuidanceAsync()`)
- `IOptions<AzureOpenAIGatewayOptions>` — for `AgentAIEnabled` gate check
- `ILogger<TaskEnrichmentService>`

**Behavior**:
- `EnrichTaskAsync`: If `task.RemediationScript` is null (or `force=true`):
  1. If finding severity is `Informational`, set `task.RemediationScript = "Informational finding — no remediation required"` and `task.ValidationCriteria = "STIG reference — no validation required"` — skip AI call
  2. Call `IRemediationEngine.GenerateRemediationScriptAsync(finding, scriptType)` — this already implements AI-first → NIST-template fallback
  2. Set `task.RemediationScript = script.Content` and `task.RemediationScriptType = scriptType.ToString()`
  3. If `task.ValidationCriteria` is null (or `force=true`), call `GenerateValidationCriteriaAsync(finding)` and set it
  4. Return result with success/failure and generation method (AI vs. template)

- `EnrichBoardTasksAsync`: For all tasks on the board:
  1. Build `findingId → finding` lookup from the findings list
  2. Process tasks in parallel with `SemaphoreSlim(maxConcurrency: 5)` to avoid OpenAI rate limits
  3. Report progress: "Enriching task {n}/{total}: {taskNumber} ({controlId})..."
  4. Return aggregate result: tasks enriched, tasks skipped (already had script), tasks failed

- `GenerateValidationCriteriaAsync`:
  1. If AI available: prompt LLM with finding context → "Generate 2-3 validation steps to verify this remediation"
  2. Fallback template: "1. Re-scan {ResourceId} for control {ControlId}\n2. Verify finding status changed to Remediated\n3. Confirm no new non-compliance alerts for {ControlFamily} family"

#### 3. Result Models

**Location**: `src/Ato.Copilot.Core/Models/Kanban/KanbanModels.cs` (append)

```csharp
public class TaskEnrichmentResult
{
    public string TaskId { get; set; } = "";
    public string TaskNumber { get; set; } = "";
    public bool ScriptGenerated { get; set; }
    public bool ValidationCriteriaGenerated { get; set; }
    public string GenerationMethod { get; set; } = ""; // "AI" or "Template"
    public string? ScriptType { get; set; }
    public string? Error { get; set; }
    public bool Skipped { get; set; }
}

public class BoardEnrichmentResult
{
    public string BoardId { get; set; } = "";
    public int TasksEnriched { get; set; }
    public int TasksSkipped { get; set; }
    public int TasksFailed { get; set; }
    public int TotalTasks { get; set; }
    public TimeSpan Duration { get; set; }
    public List<TaskEnrichmentResult> Results { get; set; } = new();
}
```

### Modified Components

#### 4. `KanbanService.CreateBoardFromAssessmentAsync()` — Wire Enrichment

**File**: `src/Ato.Copilot.Agents/Compliance/Services/KanbanService.cs`

After creating all tasks and calling `SaveChangesAsync`, call:
```csharp
if (_taskEnrichmentService != null)
{
    var enrichResult = await _taskEnrichmentService.EnrichBoardTasksAsync(
        board, findings, progress: null, cancellationToken);
    await _context.SaveChangesAsync(cancellationToken);
    _logger.LogInformation("Board enrichment: {Enriched} enriched, {Skipped} skipped, {Failed} failed",
        enrichResult.TasksEnriched, enrichResult.TasksSkipped, enrichResult.TasksFailed);
}
```

`ITaskEnrichmentService` injected as optional (`ITaskEnrichmentService? _taskEnrichmentService`) to avoid breaking existing tests.

#### 5. `KanbanService.UpdateBoardFromAssessmentAsync()` — Wire Enrichment for New Tasks

**File**: `src/Ato.Copilot.Agents/Compliance/Services/KanbanService.cs`

After creating new tasks during a board update, enrich only the newly created tasks.

#### 6. `KanbanGetTaskTool` — Lazy Enrichment (US4)

**File**: `src/Ato.Copilot.Agents/Compliance/Tools/KanbanTools.cs`

Before returning task details, if `task.RemediationScript` is null and enrichment service is available:
```csharp
if (string.IsNullOrEmpty(task.RemediationScript) && _enrichmentService != null)
{
    var finding = await FindLinkedFinding(task, svc, cancellationToken);
    if (finding != null)
    {
        await _enrichmentService.EnrichTaskAsync(task, finding);
        await svc.SaveChangesAsync(cancellationToken);
    }
}
```

#### 7. New MCP Tool: `kanban_generate_script` (US2)

**File**: `src/Ato.Copilot.Agents/Compliance/Tools/KanbanTools.cs`

```
Name: kanban_generate_script
Description: Generate an AI-powered remediation script for a task.
Parameters:
  - task_id (string, required): Task ID or task number (e.g., 'REM-001')
  - script_type (string, optional): AzureCli (default), PowerShell, Bicep, Terraform
  - force (bool, optional): Regenerate even if a script already exists
Returns: Generated script content, script type, generation method
```

#### 8. New MCP Tool: `kanban_generate_validation` (US3)

**File**: `src/Ato.Copilot.Agents/Compliance/Tools/KanbanTools.cs`

```
Name: kanban_generate_validation
Description: Generate validation criteria for verifying a task's remediation.
Parameters:
  - task_id (string, required): Task ID or task number
  - force (bool, optional): Regenerate even if criteria already exists
Returns: Validation criteria text, generation method
```

### DI Registration

**File**: `src/Ato.Copilot.Agents/Extensions/ServiceCollectionExtensions.cs`

```csharp
services.AddScoped<ITaskEnrichmentService, TaskEnrichmentService>();
```

Register the two new tools alongside existing Kanban tools.

### System Prompt Update

**File**: `src/Ato.Copilot.Agents/Compliance/Prompts/ComplianceAgent.prompt.txt`

Add to the Kanban Board tools section:
```
19. **kanban_generate_script** — Generate an AI-powered remediation script for a task. Supports Azure CLI, PowerShell, Bicep, and Terraform output.
20. **kanban_generate_validation** — Generate validation criteria for verifying a task's remediation was applied correctly.
```

---

## Existing Infrastructure Leverage

This feature requires **no new AI plumbing**. All AI services are already implemented and registered:

| Existing Service | How This Feature Uses It |
|-----------------|--------------------------|
| `AiRemediationPlanGenerator.GenerateScriptAsync()` | Called by `AtoRemediationEngine.GenerateRemediationScriptAsync()` — AI-first with NIST-template fallback |
| `AiRemediationPlanGenerator.GetGuidanceAsync()` | Used for AI-generated validation criteria prompting |
| `AtoRemediationEngine.GenerateRemediationScriptAsync()` | Primary entry point for `TaskEnrichmentService` — handles AI → fallback transparently |
| `IScriptSanitizationService.IsSafe()` | Already called inside `AtoRemediationEngine` for AI-generated scripts |
| `INistRemediationStepsService.GetRemediationSteps()` | Already provides deterministic fallback steps when AI is unavailable |
| `IChatClient` singleton | Already registered in DI from Feature 011 |
| `AzureOpenAIGatewayOptions.AgentAIEnabled` | Feature flag gate — already wired |

---

## Performance Considerations

- **Enrichment mode**: Synchronous — blocks `CreateBoardFromAssessmentAsync` until all tasks are enriched. Progress streamed via existing `IProgress<string>` / SignalR pipeline ("Enriching task 5/30: REM-005 (AC-2)..."). NIST-template fallback is near-instant when AI is unavailable or slow.
- **Concurrency**: Board-level enrichment uses `SemaphoreSlim(5)` to limit parallel AI calls. Azure OpenAI rate limits (TPM/RPM) are the primary constraint.
- **Lazy enrichment (US4)**: Adds a single AI call per task on first view. Acceptable because it's a one-time cost per task.
- **Timeouts**: Individual `GenerateRemediationScriptAsync` calls inherit the existing `CancellationToken`. Add per-task timeout (30s) to prevent a single slow generation from blocking the board.
- **Total board creation time**: With 30 tasks at 5 concurrent AI calls, worst case ~6 batches × 5s per call = 30s. Within the 60s acceptance threshold.

---

## Testing Strategy

### Unit Tests (new file: `tests/Ato.Copilot.Tests.Unit/Services/TaskEnrichmentServiceTests.cs`)

1. `EnrichTask_GeneratesScript_WhenScriptIsNull` — mock `IRemediationEngine.GenerateRemediationScriptAsync`, verify task.RemediationScript set
2. `EnrichTask_GeneratesValidationCriteria_WhenCriteriaIsNull` — verify task.ValidationCriteria set
3. `EnrichTask_SkipsScript_WhenAlreadyPresent_AndForceIsFalse` — verify no call to IRemediationEngine
4. `EnrichTask_RegeneratesScript_WhenForceIsTrue` — verify call even when existing script present
5. `EnrichTask_UsesFallback_WhenAiUnavailable` — mock IAiRemediationPlanGenerator.IsAvailable=false, verify template-based script
6. `EnrichTask_HandlesAiFailure_Gracefully` — throw from GenerateScriptAsync, verify fallback used
7. `EnrichBoard_EnrichesAllTasks_InParallel` — verify all tasks enriched with bounded concurrency
8. `EnrichBoard_SkipsAlreadyEnrichedTasks` — pre-set some tasks with scripts, verify skipped count
9. `EnrichBoard_ReportsProgress` — verify IProgress calls
10. `EnrichBoard_ContinuesOnIndividualFailure` — one task throws, verify others still enriched
11. `GenerateValidationCriteria_UsesAi_WhenAvailable` — verify AI prompt content
12. `GenerateValidationCriteria_UsesTemplate_WhenAiUnavailable` — verify template format

### Unit Tests (existing file updates):

13. `KanbanServiceTests` — verify `CreateBoardFromAssessmentAsync` calls enrichment service
14. `KanbanServiceTests` — verify `UpdateBoardFromAssessmentAsync` enriches new tasks only

### Unit Tests (new tool test files):

15. `KanbanGetTaskToolTests` — verify lazy enrichment on detail view
16. `KanbanGenerateScriptToolTests` — new tool test file
17. `KanbanGenerateValidationToolTests` — new tool test file

### Integration Verification

- Deploy to Docker stack, run compliance assessment, create board, verify task details show scripts and validation criteria
- Verify `kanban_generate_script` and `kanban_generate_validation` tools appear in MCP tool registry
- Verify `kanban_remediate_task` no longer fails with "no remediation script"

---

## File Inventory

### New Files

| File | Purpose |
|------|---------|
| `src/Ato.Copilot.Core/Interfaces/Kanban/ITaskEnrichmentService.cs` | Interface for task enrichment |
| `src/Ato.Copilot.Agents/Compliance/Services/TaskEnrichmentService.cs` | AI-powered enrichment + template fallback |
| `tests/Ato.Copilot.Tests.Unit/Services/TaskEnrichmentServiceTests.cs` | Unit tests for enrichment service |
| `tests/Ato.Copilot.Tests.Unit/Tools/KanbanGenerateScriptToolTests.cs` | Unit tests for new script tool |
| `tests/Ato.Copilot.Tests.Unit/Tools/KanbanGenerateValidationToolTests.cs` | Unit tests for new validation tool |
| `tests/Ato.Copilot.Tests.Unit/Tools/KanbanGetTaskToolTests.cs` | Unit tests for lazy enrichment on task detail view |

### Modified Files

| File | Change |
|------|--------|
| `src/Ato.Copilot.Core/Models/Kanban/KanbanModels.cs` | Add `TaskEnrichmentResult`, `BoardEnrichmentResult` models; add `RemediationScriptType` property to `RemediationTask` |
| `src/Ato.Copilot.Agents/Compliance/Services/KanbanService.cs` | Inject optional `ITaskEnrichmentService?`, call after board/task creation |
| `src/Ato.Copilot.Agents/Compliance/Tools/KanbanTools.cs` | Add `KanbanGenerateScriptTool`, `KanbanGenerateValidationTool`; add lazy enrichment to `KanbanGetTaskTool` |
| `src/Ato.Copilot.Agents/Extensions/ServiceCollectionExtensions.cs` | Register `ITaskEnrichmentService` and new tools |
| `src/Ato.Copilot.Agents/Compliance/Prompts/ComplianceAgent.prompt.txt` | Add new tool descriptions |
| `src/Ato.Copilot.Agents/Compliance/Agents/ComplianceAgent.cs` | Register 2 new tools via `RegisterTool()` in constructor |
| `tests/Ato.Copilot.Tests.Unit/Services/KanbanServiceTests.cs` | Add enrichment integration tests |

### No Changes Required

| Component | Reason |
|-----------|--------|
| `AiRemediationPlanGenerator` | Already fully implemented — used as-is |
| `AtoRemediationEngine` | Already implements `GenerateRemediationScriptAsync` with AI → fallback — used as-is |
| `RemediationScriptExecutor` | Already works — `kanban_remediate_task` will succeed once scripts exist |
| `IScriptSanitizationService` | Already called inside `AtoRemediationEngine` — no changes needed |
| `GatewayOptions.cs` | `AgentAIEnabled` flag already exists from Feature 011 |
| `CoreServiceExtensions.cs` | `IChatClient` already registered from Feature 011 |
| Database schema | `RemediationScript` (string) and `ValidationCriteria` (string) columns already exist on `RemediationTasks` table. New `RemediationScriptType` column added via EF Fluent API configuration (`EnsureCreated()` pattern — no migrations). |
