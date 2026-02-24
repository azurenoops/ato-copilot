using Microsoft.Extensions.Logging;
using Ato.Copilot.Core.Constants;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Agents.Compliance.EvidenceCollectors;

/// <summary>
/// Evidence collector for the Risk Assessment (RA) family.
/// Collects Defender assessments, secure score, vulnerability recommendations,
/// policy compliance, and risk management access control evidence.
/// </summary>
public class RiskAssessmentEvidenceCollector : BaseEvidenceCollector
{
    private readonly IDefenderForCloudService _defenderService;
    private readonly IAzurePolicyComplianceService _policyService;

    /// <inheritdoc />
    public override string FamilyCode => ControlFamilies.RiskAssessment;

    public RiskAssessmentEvidenceCollector(
        IDefenderForCloudService defenderService,
        IAzurePolicyComplianceService policyService,
        ILogger<RiskAssessmentEvidenceCollector> logger) : base(logger)
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

        // 1. Configuration — Defender assessment results
        try
        {
            var assessments = await _defenderService.GetAssessmentsAsync(subscriptionId, cancellationToken);
            items.Add(CreateEvidenceItem(
                EvidenceType.Configuration,
                "Defender Security Assessments",
                "Defender assessment results for RA-5 (Vulnerability Monitoring and Scanning).",
                assessments ?? "No assessments available",
                $"/subscriptions/{subscriptionId}"));
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to collect assessment evidence for RA");
        }

        // 2. Log — Vulnerability recommendations
        try
        {
            var recommendations = await _defenderService.GetRecommendationsAsync(subscriptionId, cancellationToken);
            items.Add(CreateEvidenceItem(
                EvidenceType.Log,
                "Vulnerability Recommendations",
                "Defender vulnerability recommendations for RA-5.",
                recommendations ?? "No recommendations available",
                $"/subscriptions/{subscriptionId}"));
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to collect vulnerability evidence for RA");
        }

        // 3. Policy — Risk assessment policy compliance
        try
        {
            var policyState = await _policyService.GetPolicyStatesAsync(
                subscriptionId, cancellationToken: cancellationToken);
            items.Add(CreateEvidenceItem(
                EvidenceType.Policy,
                "Risk Assessment Policy Compliance",
                "Azure Policy compliance for risk assessment policies (RA-1).",
                policyState ?? "No policy state available",
                $"/subscriptions/{subscriptionId}"));
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to collect policy evidence for RA");
        }

        // 4. Metric — Secure score
        try
        {
            var secureScore = await _defenderService.GetSecureScoreAsync(subscriptionId, cancellationToken);
            items.Add(CreateEvidenceItem(
                EvidenceType.Metric,
                "Risk Posture Score",
                "Defender secure score as a risk posture metric (RA-3).",
                secureScore ?? "Secure score not available",
                $"/subscriptions/{subscriptionId}"));
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to collect metric evidence for RA");
        }

        // 5. AccessControl — Risk assessment process access
        try
        {
            var summary = await _policyService.GetComplianceSummaryAsync(subscriptionId, cancellationToken);
            items.Add(CreateEvidenceItem(
                EvidenceType.AccessControl,
                "Risk Assessment Process Access",
                "Policy compliance summary governing risk assessment process (RA-2).",
                summary ?? "No compliance summary available",
                $"/subscriptions/{subscriptionId}"));
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to collect access control evidence for RA");
        }

        return items;
    }
}
