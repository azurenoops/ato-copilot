using Microsoft.Extensions.Logging;
using Ato.Copilot.Core.Constants;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Agents.Compliance.EvidenceCollectors;

/// <summary>
/// Evidence collector for the Assessment, Authorization, and Monitoring (CA) family.
/// Collects regulatory compliance, Defender recommendations, policy compliance,
/// assessment metrics, and authorization access control evidence.
/// </summary>
public class CertAccreditationEvidenceCollector : BaseEvidenceCollector
{
    private readonly IDefenderForCloudService _defenderService;
    private readonly IAzurePolicyComplianceService _policyService;

    /// <inheritdoc />
    public override string FamilyCode => ControlFamilies.AssessmentAuthorization;

    public CertAccreditationEvidenceCollector(
        IDefenderForCloudService defenderService,
        IAzurePolicyComplianceService policyService,
        ILogger<CertAccreditationEvidenceCollector> logger) : base(logger)
    {
        _defenderService = defenderService;
        _policyService = policyService;
    }

    /// <inheritdoc />
    protected override async Task<List<EvidenceItem>> CollectFamilyEvidenceAsync(
        string subscriptionId,
        string? resourceGroup,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var items = new List<EvidenceItem>();

        // 1. Configuration — Defender regulatory compliance assessments
        try
        {
            var assessments = await _defenderService.GetAssessmentsAsync(subscriptionId, cancellationToken);
            items.Add(CreateEvidenceItem(
                EvidenceType.Configuration,
                "Regulatory Compliance Assessments",
                "Defender regulatory compliance assessment results for CA-2 (Control Assessments).",
                assessments ?? "No assessments available",
                $"/subscriptions/{subscriptionId}"));
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to collect assessment evidence for CA");
        }

        // 2. Log — Defender recommendations
        try
        {
            var recommendations = await _defenderService.GetRecommendationsAsync(subscriptionId, cancellationToken);
            items.Add(CreateEvidenceItem(
                EvidenceType.Log,
                "Security Recommendations",
                "Defender security recommendations for CA-7 (Continuous Monitoring).",
                recommendations ?? "No recommendations available",
                $"/subscriptions/{subscriptionId}"));
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to collect recommendation evidence for CA");
        }

        // 3. Policy — Assessment and authorization policy compliance
        try
        {
            var policyState = await _policyService.GetPolicyStatesAsync(
                subscriptionId, cancellationToken: cancellationToken);
            items.Add(CreateEvidenceItem(
                EvidenceType.Policy,
                "Assessment Authorization Policy Compliance",
                "Azure Policy compliance for assessment and authorization policies (CA-5).",
                policyState ?? "No policy state available",
                $"/subscriptions/{subscriptionId}"));
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to collect policy evidence for CA");
        }

        // 4. Metric — Secure score as authorization metric
        try
        {
            var secureScore = await _defenderService.GetSecureScoreAsync(subscriptionId, cancellationToken);
            items.Add(CreateEvidenceItem(
                EvidenceType.Metric,
                "Authorization Posture Metrics",
                "Defender secure score as authorization posture metric (CA-6).",
                secureScore ?? "Secure score not available",
                $"/subscriptions/{subscriptionId}"));
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to collect metric evidence for CA");
        }

        // 5. AccessControl — Authorization process access
        try
        {
            var summary = await _policyService.GetComplianceSummaryAsync(subscriptionId, cancellationToken);
            items.Add(CreateEvidenceItem(
                EvidenceType.AccessControl,
                "Authorization Process Access Control",
                "Compliance summary for authorization process evidence (CA-3).",
                summary ?? "No compliance summary available",
                $"/subscriptions/{subscriptionId}"));
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to collect access control evidence for CA");
        }

        return items;
    }
}
