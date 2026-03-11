using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Core.Interfaces.Compliance;

/// <summary>
/// Service for narrative governance — version history, diffing, rollback,
/// submission, review, batch operations, and approval progress tracking.
/// </summary>
/// <remarks>Feature 024 – Narrative Governance.</remarks>
public interface INarrativeGovernanceService
{
    // ─── US1: Version History ────────────────────────────────────────────────

    /// <summary>
    /// Retrieve the version history of a control narrative, ordered newest-first.
    /// </summary>
    Task<(List<NarrativeVersion> Versions, int TotalCount)> GetNarrativeHistoryAsync(
        string systemId,
        string controlId,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Produce a line-level unified diff between two narrative versions.
    /// </summary>
    Task<NarrativeDiff> GetNarrativeDiffAsync(
        string systemId,
        string controlId,
        int fromVersion,
        int toVersion,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Copy the content of a prior version into a new version (copy-forward rollback).
    /// Resets status to Draft. No versions are deleted.
    /// </summary>
    Task<NarrativeVersion> RollbackNarrativeAsync(
        string systemId,
        string controlId,
        int targetVersion,
        string authoredBy = "mcp-user",
        string? changeReason = null,
        CancellationToken cancellationToken = default);

    // ─── US2: Approval Workflow ──────────────────────────────────────────────

    /// <summary>
    /// Submit the latest Draft narrative version for ISSM review (Draft → UnderReview).
    /// </summary>
    Task<NarrativeVersion> SubmitNarrativeAsync(
        string systemId,
        string controlId,
        string submittedBy = "mcp-user",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Approve or request revision of a narrative in UnderReview status.
    /// </summary>
    Task<NarrativeReview> ReviewNarrativeAsync(
        string systemId,
        string controlId,
        ReviewDecision decision,
        string reviewedBy = "mcp-user",
        string? comments = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Batch approve or request revision of narratives by family or control IDs.
    /// </summary>
    Task<(List<string> ReviewedControlIds, List<string> SkippedReasons)> BatchReviewNarrativesAsync(
        string systemId,
        ReviewDecision decision,
        string reviewedBy = "mcp-user",
        string? comments = null,
        string? familyFilter = null,
        IEnumerable<string>? controlIds = null,
        CancellationToken cancellationToken = default);

    // ─── US3: Approval Progress Dashboard ────────────────────────────────────

    /// <summary>
    /// Aggregate approval status counts, approval percentage, per-family breakdown,
    /// review queue, and staleness warnings.
    /// </summary>
    Task<GovernanceProgressReport> GetNarrativeApprovalProgressAsync(
        string systemId,
        string? familyFilter = null,
        CancellationToken cancellationToken = default);

    // ─── US4: Batch Submit ───────────────────────────────────────────────────

    /// <summary>
    /// Submit all Draft narratives for a control family (or all families) for ISSM review.
    /// </summary>
    Task<BatchSubmitResult> BatchSubmitNarrativesAsync(
        string systemId,
        string? familyFilter = null,
        string submittedBy = "mcp-user",
        CancellationToken cancellationToken = default);
}
