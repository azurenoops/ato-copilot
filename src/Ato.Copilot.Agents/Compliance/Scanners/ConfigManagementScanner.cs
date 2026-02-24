using Microsoft.Extensions.Logging;
using Ato.Copilot.Core.Constants;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Agents.Compliance.Scanners;

/// <summary>
/// Scanner for the Configuration Management (CM) family.
/// Checks resource locks, required tags, and naming conventions.
/// </summary>
public class ConfigManagementScanner : BaseComplianceScanner
{
    private readonly IAzureResourceService _azureResourceService;
    private readonly IAzurePolicyComplianceService _policyService;

    /// <inheritdoc />
    public override string FamilyCode => ControlFamilies.ConfigurationManagement;

    public ConfigManagementScanner(
        IAzureResourceService azureResourceService,
        IAzurePolicyComplianceService policyService,
        ILogger<ConfigManagementScanner> logger) : base(logger)
    {
        _azureResourceService = azureResourceService;
        _policyService = policyService;
    }

    /// <inheritdoc />
    protected override async Task<List<ComplianceFinding>> ScanFamilyAsync(
        string subscriptionId,
        string? resourceGroup,
        List<NistControl> controls,
        CancellationToken cancellationToken)
    {
        var findings = new List<ComplianceFinding>();

        // Check resource locks for production resources
        var locks = await _azureResourceService.GetResourceLocksAsync(
            subscriptionId, resourceGroup, cancellationToken);

        if (locks.Count == 0)
        {
            findings.Add(new ComplianceFinding
            {
                ControlId = "CM-5",
                ControlFamily = ControlFamilies.ConfigurationManagement,
                Title = "No resource locks configured",
                Description = "No management locks found. Per CM-5 (Access Restrictions for Change), " +
                              "critical resources must be protected with CanNotDelete or ReadOnly locks.",
                Severity = FindingSeverity.Medium,
                Status = FindingStatus.Open,
                ResourceId = $"/subscriptions/{subscriptionId}",
                ResourceType = "Microsoft.Authorization/locks",
                RemediationGuidance = "Apply CanNotDelete locks on critical production resources.",
                ScanSource = ScanSourceType.Resource,
                AutoRemediable = true,
                RemediationType = RemediationType.ResourceConfiguration
            });
        }

        // Check resource tagging compliance
        var resources = await _azureResourceService.GetResourcesAsync(
            subscriptionId, resourceGroup, cancellationToken: cancellationToken);

        var requiredTags = new[] { "Environment", "Owner", "CostCenter" };
        var untaggedResources = resources
            .Where(r => r.Data.Tags == null || !requiredTags.All(tag =>
                r.Data.Tags.ContainsKey(tag)))
            .ToList();

        if (untaggedResources.Count > 0 && resources.Count > 0)
        {
            var percentage = (double)untaggedResources.Count / resources.Count * 100;
            findings.Add(new ComplianceFinding
            {
                ControlId = "CM-8",
                ControlFamily = ControlFamilies.ConfigurationManagement,
                Title = "Resources missing required tags",
                Description = $"{untaggedResources.Count} of {resources.Count} resources ({percentage:F0}%) " +
                              "are missing required tags (Environment, Owner, CostCenter). " +
                              "Per CM-8 (System Component Inventory), all components must be catalogued.",
                Severity = percentage > 50 ? FindingSeverity.High : FindingSeverity.Medium,
                Status = FindingStatus.Open,
                ResourceId = $"/subscriptions/{subscriptionId}",
                ResourceType = "Microsoft.Resources/resources",
                RemediationGuidance = "Apply required tags via Azure Policy and manual tagging.",
                ScanSource = ScanSourceType.Resource,
                AutoRemediable = true,
                RemediationType = RemediationType.PolicyRemediation
            });
        }

        // Policy compliance for CM-related policies
        var policyStates = await _policyService.GetPolicyStatesAsync(subscriptionId, cancellationToken: cancellationToken);
        if (!string.IsNullOrEmpty(policyStates) && policyStates.Contains("NonCompliant", StringComparison.OrdinalIgnoreCase))
        {
            findings.Add(new ComplianceFinding
            {
                ControlId = "CM-2",
                ControlFamily = ControlFamilies.ConfigurationManagement,
                Title = "Non-compliant configuration management policies",
                Description = "Azure Policy reports non-compliant configuration management policies.",
                Severity = FindingSeverity.Medium,
                Status = FindingStatus.Open,
                ResourceId = $"/subscriptions/{subscriptionId}",
                ResourceType = "Microsoft.Authorization/policyStates",
                RemediationGuidance = "Remediate non-compliant configuration policy assignments.",
                ScanSource = ScanSourceType.Policy,
                AutoRemediable = true,
                RemediationType = RemediationType.PolicyRemediation
            });
        }

        return findings;
    }
}
