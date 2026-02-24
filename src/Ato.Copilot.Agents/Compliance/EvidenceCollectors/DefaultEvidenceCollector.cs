using Microsoft.Extensions.Logging;
using Ato.Copilot.Core.Constants;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Agents.Compliance.EvidenceCollectors;

/// <summary>
/// Default fallback evidence collector for families without a specialized collector.
/// Handles: AT, MA, MP, PE, PL, PM, PS, PT, SA, SR via policy-based evidence collection.
/// Collects policy compliance, Defender assessments, and compliance summary evidence.
/// </summary>
public class DefaultEvidenceCollector : BaseEvidenceCollector
{
    private readonly IAzurePolicyComplianceService _policyService;
    private readonly IDefenderForCloudService _defenderService;

    /// <summary>
    /// Family code "DEFAULT" signals the <see cref="EvidenceCollectorRegistry"/>
    /// to use this collector as the fallback.
    /// </summary>
    public override string FamilyCode => "DEFAULT";

    public DefaultEvidenceCollector(
        IAzurePolicyComplianceService policyService,
        IDefenderForCloudService defenderService,
        ILogger<DefaultEvidenceCollector> logger) : base(logger)
    {
        _policyService = policyService;
        _defenderService = defenderService;
    }

    /// <inheritdoc />
    protected override async Task<List<EvidenceItem>> CollectFamilyEvidenceAsync(
        string subscriptionId,
        string? resourceGroup,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var items = new List<EvidenceItem>();

        // 1. Configuration — Policy compliance configuration
        try
        {
            var policyState = await _policyService.GetPolicyStatesAsync(
                subscriptionId, cancellationToken: cancellationToken);
            items.Add(CreateEvidenceItem(
                EvidenceType.Configuration,
                "Policy Compliance Configuration",
                "Azure Policy compliance state for the control family.",
                policyState ?? "No policy state available",
                $"/subscriptions/{subscriptionId}"));
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to collect policy configuration evidence");
        }

        // 2. Log — Defender assessments
        try
        {
            var assessments = await _defenderService.GetAssessmentsAsync(subscriptionId, cancellationToken);
            items.Add(CreateEvidenceItem(
                EvidenceType.Log,
                "Defender Security Assessments",
                "Defender for Cloud assessment results for the control family.",
                assessments ?? "No assessments available",
                $"/subscriptions/{subscriptionId}"));
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to collect Defender evidence");
        }

        // 3. Policy — Compliance summary
        try
        {
            var summary = await _policyService.GetComplianceSummaryAsync(subscriptionId, cancellationToken);
            items.Add(CreateEvidenceItem(
                EvidenceType.Policy,
                "Policy Compliance Summary",
                "Overall policy compliance summary for the control family.",
                summary ?? "No compliance summary available",
                $"/subscriptions/{subscriptionId}"));
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to collect compliance summary evidence");
        }

        // 4. Metric — Secure score metric
        try
        {
            var secureScore = await _defenderService.GetSecureScoreAsync(subscriptionId, cancellationToken);
            items.Add(CreateEvidenceItem(
                EvidenceType.Metric,
                "Security Posture Metric",
                "Defender secure score as a general security posture metric.",
                secureScore ?? "Secure score not available",
                $"/subscriptions/{subscriptionId}"));
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to collect secure score evidence");
        }

        // 5. AccessControl — Defender recommendations (access-related)
        try
        {
            var recommendations = await _defenderService.GetRecommendationsAsync(subscriptionId, cancellationToken);
            items.Add(CreateEvidenceItem(
                EvidenceType.AccessControl,
                "Security Recommendations",
                "Defender recommendations relevant to access control posture.",
                recommendations ?? "No recommendations available",
                $"/subscriptions/{subscriptionId}"));
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to collect recommendation evidence");
        }

        return items;
    }
}
