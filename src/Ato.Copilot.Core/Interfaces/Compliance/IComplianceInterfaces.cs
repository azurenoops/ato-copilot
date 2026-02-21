using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Core.Interfaces.Compliance;

/// <summary>
/// Core compliance scanning engine for NIST 800-53 assessments
/// </summary>
public interface IAtoComplianceEngine
{
    Task<ComplianceAssessment> RunAssessmentAsync(
        string subscriptionId,
        string? framework = null,
        string? controlFamilies = null,
        string? resourceTypes = null,
        string? scanType = null,
        bool includePassed = false,
        CancellationToken cancellationToken = default);

    Task<List<ComplianceAssessment>> GetAssessmentHistoryAsync(
        string subscriptionId,
        int days = 30,
        CancellationToken cancellationToken = default);

    Task<ComplianceFinding?> GetFindingAsync(
        string findingId,
        CancellationToken cancellationToken = default);

    Task SaveAssessmentAsync(
        ComplianceAssessment assessment,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Remediation engine for compliance findings
/// </summary>
public interface IRemediationEngine
{
    Task<RemediationPlan> GeneratePlanAsync(
        string subscriptionId,
        string? resourceGroupName = null,
        CancellationToken cancellationToken = default);

    Task<string> ExecuteRemediationAsync(
        string findingId,
        bool applyRemediation = false,
        bool dryRun = true,
        CancellationToken cancellationToken = default);

    Task<string> ValidateRemediationAsync(
        string findingId,
        string? executionId = null,
        string? subscriptionId = null,
        CancellationToken cancellationToken = default);

    Task<string> BatchRemediateAsync(
        string? subscriptionId = null,
        string? severity = null,
        string? family = null,
        bool dryRun = true,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// NIST 800-53 controls catalog service
/// </summary>
public interface INistControlsService
{
    Task<NistControl?> GetControlAsync(
        string controlId,
        CancellationToken cancellationToken = default);

    Task<List<NistControl>> GetControlFamilyAsync(
        string familyId,
        bool includeControls = true,
        CancellationToken cancellationToken = default);

    Task<List<NistControl>> SearchControlsAsync(
        string query,
        string? controlFamily = null,
        string? impactLevel = null,
        int maxResults = 10,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Azure Policy compliance integration 
/// </summary>
public interface IAzurePolicyComplianceService
{
    Task<string> GetComplianceSummaryAsync(
        string subscriptionId,
        CancellationToken cancellationToken = default);

    Task<string> GetPolicyStatesAsync(
        string subscriptionId,
        string? policyDefinitionId = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Microsoft Defender for Cloud integration
/// </summary>
public interface IDefenderForCloudService
{
    Task<string> GetSecureScoreAsync(
        string subscriptionId,
        CancellationToken cancellationToken = default);

    Task<string> GetAssessmentsAsync(
        string subscriptionId,
        CancellationToken cancellationToken = default);

    Task<string> GetRecommendationsAsync(
        string subscriptionId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Evidence storage and collection service
/// </summary>
public interface IEvidenceStorageService
{
    Task<ComplianceEvidence> CollectEvidenceAsync(
        string controlId,
        string subscriptionId,
        string? resourceGroup = null,
        CancellationToken cancellationToken = default);

    Task<List<ComplianceEvidence>> GetEvidenceAsync(
        string controlId,
        string? subscriptionId = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Compliance monitoring for continuous compliance posture tracking
/// </summary>
public interface IComplianceMonitoringService
{
    Task<string> GetStatusAsync(
        string? subscriptionId = null,
        CancellationToken cancellationToken = default);

    Task<string> TriggerScanAsync(
        string? subscriptionId = null,
        CancellationToken cancellationToken = default);

    Task<string> GetAlertsAsync(
        string? subscriptionId = null,
        int days = 30,
        CancellationToken cancellationToken = default);

    Task<string> GetTrendAsync(
        string? subscriptionId = null,
        int days = 30,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Compliance document generation service
/// </summary>
public interface IDocumentGenerationService
{
    Task<ComplianceDocument> GenerateDocumentAsync(
        string documentType,
        string? subscriptionId = null,
        string? framework = null,
        string? systemName = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Assessment audit trail service
/// </summary>
public interface IAssessmentAuditService
{
    Task<string> GetAuditLogAsync(
        string? subscriptionId = null,
        int days = 7,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Compliance history and trending service
/// </summary>
public interface IComplianceHistoryService
{
    Task<string> GetHistoryAsync(
        string? subscriptionId = null,
        int days = 30,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Real-time compliance status summary service
/// </summary>
public interface IComplianceStatusService
{
    Task<string> GetStatusAsync(
        string? subscriptionId = null,
        string? framework = null,
        CancellationToken cancellationToken = default);
}
