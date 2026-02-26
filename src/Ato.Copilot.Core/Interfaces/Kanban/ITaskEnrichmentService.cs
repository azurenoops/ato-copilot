using Ato.Copilot.Core.Models.Compliance;
using Ato.Copilot.Core.Models.Kanban;

namespace Ato.Copilot.Core.Interfaces.Kanban;

/// <summary>
/// FeatureSpec: 012-task-enrichment
/// Enriches remediation tasks with AI-generated remediation scripts and validation criteria.
/// Uses IRemediationEngine (AI-first → NIST-template fallback) for script generation
/// and a dedicated AI prompt (via IChatClient) for validation criteria.
/// </summary>
public interface ITaskEnrichmentService
{
    /// <summary>
    /// Enriches a single task with remediation script and validation criteria.
    /// Modifies the task entity in-place. Caller is responsible for SaveChangesAsync.
    /// </summary>
    /// <param name="task">The task to enrich (modified in-place).</param>
    /// <param name="finding">Linked compliance finding (null for manually created tasks — will skip).</param>
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
    /// Enriches all tasks on a board with bounded concurrency (SemaphoreSlim(5)).
    /// Reports progress via IProgress for streaming updates.
    /// Caller is responsible for SaveChangesAsync after this completes.
    /// </summary>
    /// <param name="board">Board containing tasks to enrich.</param>
    /// <param name="findings">Assessment findings for context lookup.</param>
    /// <param name="progress">Optional progress reporter for streaming updates.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Aggregate enrichment result with per-task details.</returns>
    Task<BoardEnrichmentResult> EnrichBoardTasksAsync(
        RemediationBoard board,
        IReadOnlyList<ComplianceFinding> findings,
        IProgress<string>? progress = null,
        CancellationToken ct = default);

    /// <summary>
    /// Generates validation criteria for a finding using AI or templates.
    /// AI path uses a dedicated prompt via IChatClient (per research R3).
    /// Template fallback: "1. Re-scan {ResourceId}... 2. Verify... 3. Confirm..."
    /// </summary>
    /// <param name="finding">Finding to generate criteria for.</param>
    /// <param name="scriptContent">Optional: the remediation script content for AI context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Validation criteria text.</returns>
    Task<string> GenerateValidationCriteriaAsync(
        ComplianceFinding finding,
        string? scriptContent = null,
        CancellationToken ct = default);
}
