using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Core.Interfaces.Compliance;

/// <summary>
/// Service for Privacy Threshold Analysis (PTA) and Privacy Impact Assessment (PIA) lifecycle management.
/// Implements E-Government Act §208 / OMB M-03-22 / NIST SP 800-122 privacy requirements.
/// </summary>
public interface IPrivacyService
{
    /// <summary>
    /// Conduct a Privacy Threshold Analysis for a registered system.
    /// Auto-detects PII from SecurityCategorization info types or accepts manual PII flags.
    /// </summary>
    /// <param name="systemId">RegisteredSystem ID (GUID string).</param>
    /// <param name="analyzedBy">Identity of the user performing the PTA.</param>
    /// <param name="manualMode">If true, uses explicit PII flags instead of auto-detection.</param>
    /// <param name="collectsPii">Manual mode: whether system collects PII.</param>
    /// <param name="maintainsPii">Manual mode: whether system maintains PII.</param>
    /// <param name="disseminatesPii">Manual mode: whether system disseminates PII.</param>
    /// <param name="piiCategories">Manual mode: PII categories identified.</param>
    /// <param name="estimatedRecordCount">Manual mode: estimated PII records (≥10 triggers PIA per E-Gov Act).</param>
    /// <param name="exemptionRationale">Exemption justification (required if exempt).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>PTA analysis result with determination.</returns>
    Task<PtaResult> CreatePtaAsync(
        string systemId,
        string analyzedBy,
        bool manualMode = false,
        bool collectsPii = false,
        bool maintainsPii = false,
        bool disseminatesPii = false,
        List<string>? piiCategories = null,
        int? estimatedRecordCount = null,
        string? exemptionRationale = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate a Privacy Impact Assessment with 8 OMB M-03-22 sections.
    /// Pre-populates sections from system data and drafts narratives via AI.
    /// </summary>
    /// <param name="systemId">RegisteredSystem ID (GUID string).</param>
    /// <param name="createdBy">Identity of the user generating the PIA.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>PIA generation result with document content.</returns>
    Task<PiaResult> GeneratePiaAsync(
        string systemId,
        string createdBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Review a PIA — approve or request revision with deficiency notes.
    /// Approval sets ExpirationDate to now + 1 year. Revision resets status to Draft.
    /// </summary>
    /// <param name="systemId">RegisteredSystem ID (GUID string).</param>
    /// <param name="decision">Approve or RequestRevision.</param>
    /// <param name="reviewerComments">Reviewer notes.</param>
    /// <param name="reviewedBy">Identity of the reviewer.</param>
    /// <param name="deficiencies">Specific deficiencies if requesting revision.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>PIA review result.</returns>
    Task<PiaReviewResult> ReviewPiaAsync(
        string systemId,
        PiaReviewDecision decision,
        string reviewerComments,
        string reviewedBy,
        List<string>? deficiencies = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidate a PTA (e.g., after information type changes).
    /// Deletes existing PTA and sets any Approved PIA to UnderReview.
    /// </summary>
    /// <param name="systemId">RegisteredSystem ID (GUID string).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task InvalidatePtaAsync(
        string systemId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get privacy compliance dashboard for a system.
    /// Aggregates PTA, PIA, interconnection, and agreement status.
    /// </summary>
    /// <param name="systemId">RegisteredSystem ID (GUID string).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Privacy compliance status summary.</returns>
    Task<PrivacyComplianceResult> GetPrivacyComplianceAsync(
        string systemId,
        CancellationToken cancellationToken = default);
}
