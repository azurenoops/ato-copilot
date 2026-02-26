# Research: AI-Powered Task Enrichment

**Feature**: 012-task-enrichment  
**Date**: 2026-02-25  
**Status**: Complete — all unknowns resolved

---

## Research Tasks

### R1: How does `AtoRemediationEngine.GenerateRemediationScriptAsync` behave?

**Decision**: Use `IRemediationEngine.GenerateRemediationScriptAsync(finding, scriptType, ct)` as the primary entry point for script generation in `TaskEnrichmentService`. It already implements the full AI → NIST fallback pipeline.

**Rationale**: The method (at `AtoRemediationEngine.cs` L1595–L1647) follows this flow:
1. If `_aiGenerator.IsAvailable` (`IChatClient != null`):
   - Calls `AiRemediationPlanGenerator.GenerateScriptAsync(finding, scriptType, ct)`
   - If result is non-null and `IScriptSanitizationService.IsSafe(script.Content)` → returns AI script with `IsSanitized = true`
   - If unsafe → logs violations, falls through to NIST fallback
   - On any exception → catches, logs warning, falls through
2. NIST fallback (always reached on failure):
   - `INistRemediationStepsService.GetRemediationSteps(finding.ControlFamily, finding.ControlId)` → joins steps as `# Step {i+1}: {step}`
   - Returns `RemediationScript` with `IsSanitized = true`

**Key finding**: The method **never returns null** — it always returns a `RemediationScript` with a `Content` string. This means `TaskEnrichmentService.EnrichTaskAsync` can safely assign `task.RemediationScript = script.Content` without null checks.

**Alternatives considered**:
- Calling `AiRemediationPlanGenerator.GenerateScriptAsync` directly → Rejected because it bypasses sanitization and NIST fallback, requiring us to reimplement that logic
- Creating a new AI prompt for enrichment → Rejected because the existing prompt (in `AiRemediationPlanGenerator`) already handles control ID, severity, resource context

---

### R2: What does the `RemediationScript` model contain?

**Decision**: Map `RemediationScript.Content` → `task.RemediationScript`, `RemediationScript.ScriptType` → `task.RemediationScriptType` (new property), and optionally expose `Description`, `EstimatedDuration` in the enrichment result.

**Rationale**: `RemediationScript` (at `RemediationScript.cs`) has:
```csharp
Content: string           // The actual script text
ScriptType: ScriptType    // Enum: AzureCli, PowerShell, Bicep, Terraform
Description: string       // What the script does
Parameters: Dictionary     // e.g., resourceId, subscriptionId
EstimatedDuration: TimeSpan // How long to run
IsSanitized: bool          // Whether it passed sanitization
```

The `Content` field maps directly to `RemediationTask.RemediationScript` (existing varchar(8000) column). The `ScriptType` maps to the new `RemediationScriptType` property (string, for syntax highlighting in the UI).

---

### R3: Can we reuse `GetGuidanceAsync` for validation criteria?

**Decision**: Do NOT reuse `GetGuidanceAsync` for validation criteria. Create a dedicated AI prompt within `TaskEnrichmentService.GenerateValidationCriteriaAsync`.

**Rationale**: `AiRemediationPlanGenerator.GetGuidanceAsync` (L102–L165) returns `RemediationGuidance` with `Explanation` and `TechnicalPlan` fields, but its prompt is framed around *remediation guidance* ("explain how to remediate"), not *validation criteria* ("how to verify the fix was applied"). The `TechnicalPlan` contains implementation steps, not verification steps.

A dedicated validation prompt will:
1. Accept the finding context + the script content (if available)
2. Ask the AI: "Generate 2-3 validation steps to verify this remediation was applied correctly"
3. Include resource-specific verification (e.g., "Run `az policy state list --filter ...`")

**Fallback template**: When AI is unavailable:
```
1. Re-scan {ResourceId} for control {ControlId}
2. Verify finding status changed to Remediated
3. Confirm no new non-compliance alerts for {ControlFamily} family
```

**Alternatives considered**:
- Reusing `GetGuidanceAsync.TechnicalPlan` as validation → Rejected because the content describes *implementation*, not *verification*
- Calling `GetGuidanceAsync` then transforming the output → Rejected as wasteful (extra AI call) and the prompt isn't optimized for validation output

---

### R4: How to look up the finding for a given task?

**Decision**: Use `task.FindingId` FK to query `AtoCopilotContext.Findings.FirstOrDefaultAsync(f => f.Id == task.FindingId)`.

**Rationale**: 
- `RemediationTask.FindingId` (string?, nullable) stores the FK to `ComplianceFinding.Id`
- `AtoCopilotContext.Findings` (`DbSet<ComplianceFinding>`) provides the query surface
- For board-level enrichment, the findings list is already available (passed from `CreateBoardFromAssessmentAsync` which loads `assessment.Findings`)
- For on-demand tools and lazy enrichment, perform a DB lookup by `FindingId`
- If `FindingId` is null (manually created tasks), skip enrichment or use a minimal finding stub

---

### R5: DI lifetime for `TaskEnrichmentService`

**Decision**: Register as **scoped** (`AddScoped<ITaskEnrichmentService, TaskEnrichmentService>()`).

**Rationale**:
- `TaskEnrichmentService` depends on `AtoCopilotContext` which is scoped (EF Core DbContext)
- `IRemediationEngine` and `IAiRemediationPlanGenerator` are singletons — safe to inject into scoped services
- `IOptions<AzureOpenAIGatewayOptions>` is singleton — safe
- `ILogger<T>` is singleton — safe
- The scoped lifetime matches `KanbanService` (also `AddScoped<IKanbanService, KanbanService>()`)

**Alternatives considered**:
- Singleton → Rejected because it would require `IServiceScopeFactory` to create scoped DbContext instances, adding complexity
- Transient → Unnecessary — scoped is the standard for EF-dependent services

---

### R6: EF migration for `RemediationScriptType` column

**Decision**: Add `RemediationScriptType` as a string property to `RemediationTask` with EF Fluent API configuration. Use code-first approach with `EnsureCreated()` (project doesn't use EF migrations — it uses `EnsureCreated` at startup).

**Rationale**:
- The project uses `_context.Database.EnsureCreated()` in the startup path (no `Migrations/` folder exists)
- Adding a new nullable string property will be automatically included when `EnsureCreated()` runs
- For existing Docker volumes, the column won't auto-appear — container rebuild with fresh volume required (acceptable for dev-only deployment)
- Column config: `entity.Property(e => e.RemediationScriptType).HasMaxLength(20);`

**Alternatives considered**:
- EF Migrations (`dotnet ef migrations add`) → Project doesn't use migrations; `EnsureCreated()` is the pattern
- Raw SQL migration script → Over-engineered for dev deployment

---

### R7: Bounded concurrency pattern for board enrichment

**Decision**: Use `SemaphoreSlim(5)` with `Task.WhenAll` for parallel enrichment of board tasks.

**Rationale**:
- Azure OpenAI has TPM/RPM rate limits — unbounded parallelism would trigger 429 errors
- 5 concurrent requests is a safe default for government-tier Azure OpenAI deployments
- Pattern: create `Task[]` for all tasks, each waits on `semaphore.WaitAsync()` before calling `IRemediationEngine.GenerateRemediationScriptAsync`
- Per-task timeout: wrap each call in `CancellationTokenSource.CreateLinkedTokenSource(ct)` with 30s timeout

**Alternatives considered**:
- `Parallel.ForEachAsync(maxDegreeOfParallelism: 5)` → .NET 6+ API but less control over per-task error handling
- Sequential processing → Too slow for 30+ tasks (30 × 5s = 150s >> 60s target)
- `Channel<T>` producer-consumer → Over-engineered for this use case

---

### R8: How KanbanTools access scoped services

**Decision**: Tools are singletons but receive scoped services via `IServiceProvider` in `ExecuteAsync`. Follow the existing pattern used by all Kanban tools.

**Rationale**: Examining existing tools (e.g., `KanbanGetTaskTool`), they use:
```csharp
protected override async Task<string> ExecuteAsync(
    IDictionary<string, object?> parameters,
    IServiceProvider serviceProvider,
    CancellationToken cancellationToken)
{
    var svc = serviceProvider.GetRequiredService<IKanbanService>();
    // ... use scoped service
}
```

The `IServiceProvider` parameter is scoped — it's created per-request by the MCP server infrastructure. New tools (`KanbanGenerateScriptTool`, `KanbanGenerateValidationTool`) and the modified `KanbanGetTaskTool` will resolve `ITaskEnrichmentService` from this scoped provider.

---

### R9: Informational severity handling

**Decision**: Skip AI calls for `FindingSeverity.Informational` tasks. Set `RemediationScript = "Informational finding — no remediation required"` and `ValidationCriteria = "STIG reference — no validation required"`.

**Rationale**: Per spec clarification (2026-02-25), Informational findings are observations with no actionable remediation. Calling AI for these is wasteful and would produce generic "no action needed" responses. The fixed strings are clear, deterministic, and distinguishable from AI-generated content.

---

## Summary

All 9 research items resolved. No NEEDS CLARIFICATION items remain. Key architectural decisions:

1. **Use `IRemediationEngine` as-is** — it already handles AI → NIST fallback transparently
2. **Dedicated validation criteria prompt** — don't reuse `GetGuidanceAsync`
3. **Scoped DI lifetime** — matches `KanbanService` pattern
4. **`SemaphoreSlim(5)` concurrency** — safe for Azure Gov OpenAI rate limits
5. **No EF migrations** — project uses `EnsureCreated()`
6. **Singleton tools resolve scoped services** via `IServiceProvider` parameter in `ExecuteAsync`
