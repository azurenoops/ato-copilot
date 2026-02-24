using Microsoft.Extensions.Logging;
using Ato.Copilot.Core.Constants;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Agents.Compliance.EvidenceCollectors;

/// <summary>
/// Evidence collector for the Identification and Authentication (IA) family.
/// Collects managed identity, MFA policy, authentication logs,
/// identity metrics, and identity access control evidence.
/// </summary>
public class IdentificationAuthEvidenceCollector : BaseEvidenceCollector
{
    private readonly IAzureResourceService _azureResourceService;
    private readonly IAzurePolicyComplianceService _policyService;
    private readonly IDefenderForCloudService _defenderService;

    /// <inheritdoc />
    public override string FamilyCode => ControlFamilies.IdentificationAuthentication;

    public IdentificationAuthEvidenceCollector(
        IAzureResourceService azureResourceService,
        IAzurePolicyComplianceService policyService,
        IDefenderForCloudService defenderService,
        ILogger<IdentificationAuthEvidenceCollector> logger) : base(logger)
    {
        _azureResourceService = azureResourceService;
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

        // 1. Configuration — Managed identity inventory
        try
        {
            var identities = await _azureResourceService.GetResourcesAsync(
                subscriptionId, resourceGroup, "Microsoft.ManagedIdentity/userAssignedIdentities", cancellationToken);
            items.Add(CreateEvidenceItem(
                EvidenceType.Configuration,
                "Managed Identity Inventory",
                "User-assigned managed identities for IA-2 (Identification and Authentication).",
                $"User-assigned managed identities found: {identities.Count}",
                $"/subscriptions/{subscriptionId}"));
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to collect managed identity evidence for IA");
        }

        // 2. Log — Defender MFA recommendations
        try
        {
            var recommendations = await _defenderService.GetRecommendationsAsync(subscriptionId, cancellationToken);
            items.Add(CreateEvidenceItem(
                EvidenceType.Log,
                "MFA and Authentication Recommendations",
                "Defender recommendations for MFA enforcement (IA-2(1)).",
                recommendations ?? "No MFA recommendations available",
                $"/subscriptions/{subscriptionId}"));
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to collect MFA evidence for IA");
        }

        // 3. Policy — Authentication policy compliance
        try
        {
            var policyState = await _policyService.GetPolicyStatesAsync(
                subscriptionId, cancellationToken: cancellationToken);
            items.Add(CreateEvidenceItem(
                EvidenceType.Policy,
                "Authentication Policy Compliance",
                "Azure Policy compliance for authentication policies (IA-5).",
                policyState ?? "No policy state available",
                $"/subscriptions/{subscriptionId}"));
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to collect policy evidence for IA");
        }

        // 4. Metric — Identity posture metrics
        try
        {
            var secureScore = await _defenderService.GetSecureScoreAsync(subscriptionId, cancellationToken);
            items.Add(CreateEvidenceItem(
                EvidenceType.Metric,
                "Identity Security Posture Metrics",
                "Defender secure score as an identity posture metric (IA-2).",
                secureScore ?? "Secure score not available",
                $"/subscriptions/{subscriptionId}"));
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to collect metric evidence for IA");
        }

        // 5. AccessControl — Privileged identity management
        try
        {
            var roleAssignments = await _azureResourceService.GetRoleAssignmentsAsync(
                subscriptionId, cancellationToken);
            items.Add(CreateEvidenceItem(
                EvidenceType.AccessControl,
                "Privileged Identity Access Control",
                "Privileged role assignments for IA-4 (Identifier Management).",
                $"Total privileged role assignments: {roleAssignments.Count}",
                $"/subscriptions/{subscriptionId}"));
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to collect access control evidence for IA");
        }

        return items;
    }
}
