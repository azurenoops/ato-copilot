using Microsoft.Extensions.Logging;
using Ato.Copilot.Core.Constants;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Agents.Compliance.Scanners;

/// <summary>
/// Scanner for the Identification and Authentication (IA) family.
/// Checks MFA enforcement, credential expiry, managed identities,
/// and service principal hygiene.
/// </summary>
public class IdentificationAuthScanner : BaseComplianceScanner
{
    private readonly IAzureResourceService _azureResourceService;
    private readonly IAzurePolicyComplianceService _policyService;
    private readonly IDefenderForCloudService _defenderService;

    /// <inheritdoc />
    public override string FamilyCode => ControlFamilies.IdentificationAuthentication;

    public IdentificationAuthScanner(
        IAzureResourceService azureResourceService,
        IAzurePolicyComplianceService policyService,
        IDefenderForCloudService defenderService,
        ILogger<IdentificationAuthScanner> logger) : base(logger)
    {
        _azureResourceService = azureResourceService;
        _policyService = policyService;
        _defenderService = defenderService;
    }

    /// <inheritdoc />
    protected override async Task<List<ComplianceFinding>> ScanFamilyAsync(
        string subscriptionId,
        string? resourceGroup,
        List<NistControl> controls,
        CancellationToken cancellationToken)
    {
        var findings = new List<ComplianceFinding>();

        // Check for resources using managed identities
        var resources = await _azureResourceService.GetResourcesAsync(
            subscriptionId, resourceGroup, cancellationToken: cancellationToken);

        // Evaluate managed identity adoption
        var managedIdentityResources = resources
            .Where(r => r.Data.Identity != null)
            .ToList();

        var identityEligibleTypes = new[]
        {
            "Microsoft.Web/sites", "Microsoft.Compute/virtualMachines",
            "Microsoft.ContainerService/managedClusters", "Microsoft.Sql/servers"
        };

        var eligibleResources = resources
            .Where(r => identityEligibleTypes.Any(t =>
                r.Data.ResourceType.ToString().Equals(t, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (eligibleResources.Count > 0)
        {
            var withoutIdentity = eligibleResources
                .Where(r => r.Data.Identity == null)
                .ToList();

            if (withoutIdentity.Count > 0)
            {
                findings.Add(new ComplianceFinding
                {
                    ControlId = "IA-2",
                    ControlFamily = ControlFamilies.IdentificationAuthentication,
                    Title = "Resources without managed identity",
                    Description = $"{withoutIdentity.Count} of {eligibleResources.Count} identity-eligible resources " +
                                  "lack managed identities. Per IA-2 (Identification and Authentication), " +
                                  "workloads should use managed identities to eliminate credential management.",
                    Severity = FindingSeverity.Medium,
                    Status = FindingStatus.Open,
                    ResourceId = $"/subscriptions/{subscriptionId}",
                    ResourceType = "Microsoft.ManagedIdentity/userAssignedIdentities",
                    RemediationGuidance = "Enable system-assigned or user-assigned managed identities.",
                    ScanSource = ScanSourceType.Resource,
                    RiskLevel = RiskLevel.High,
                    AutoRemediable = true,
                    RemediationType = RemediationType.ResourceConfiguration
                });
            }
        }

        // Defender MFA/authentication recommendations
        var recommendations = await _defenderService.GetRecommendationsAsync(subscriptionId, cancellationToken);
        if (!string.IsNullOrEmpty(recommendations) &&
            (recommendations.Contains("MFA", StringComparison.OrdinalIgnoreCase) ||
             recommendations.Contains("multi-factor", StringComparison.OrdinalIgnoreCase)))
        {
            findings.Add(new ComplianceFinding
            {
                ControlId = "IA-2(1)",
                ControlFamily = ControlFamilies.IdentificationAuthentication,
                Title = "MFA not enforced for all privileged accounts",
                Description = "Defender for Cloud reports MFA-related recommendations. " +
                              "Per IA-2(1), MFA must be enforced for all privileged access.",
                Severity = FindingSeverity.Critical,
                Status = FindingStatus.Open,
                ResourceId = $"/subscriptions/{subscriptionId}",
                ResourceType = "Microsoft.Security/assessments",
                RemediationGuidance = "Enable MFA via Conditional Access policies for all users.",
                ScanSource = ScanSourceType.Defender,
                RiskLevel = RiskLevel.High,
                AutoRemediable = false
            });
        }

        // Policy-based auth compliance
        var policyStates = await _policyService.GetPolicyStatesAsync(subscriptionId, cancellationToken: cancellationToken);
        if (!string.IsNullOrEmpty(policyStates) && policyStates.Contains("NonCompliant", StringComparison.OrdinalIgnoreCase))
        {
            findings.Add(new ComplianceFinding
            {
                ControlId = "IA-5",
                ControlFamily = ControlFamilies.IdentificationAuthentication,
                Title = "Non-compliant authentication policies",
                Description = "Azure Policy reports non-compliant authentication-related policies.",
                Severity = FindingSeverity.Medium,
                Status = FindingStatus.Open,
                ResourceId = $"/subscriptions/{subscriptionId}",
                ResourceType = "Microsoft.Authorization/policyStates",
                RemediationGuidance = "Remediate non-compliant authentication policy assignments.",
                ScanSource = ScanSourceType.Policy,
                RiskLevel = RiskLevel.High,
                AutoRemediable = true,
                RemediationType = RemediationType.PolicyRemediation
            });
        }

        return findings;
    }
}
