using Microsoft.Extensions.Logging;
using Ato.Copilot.Core.Constants;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Agents.Compliance.EvidenceCollectors;

/// <summary>
/// Evidence collector for the Configuration Management (CM) family.
/// Collects resource lock, tagging, naming convention, policy compliance,
/// and configuration access control evidence.
/// </summary>
public class ConfigMgmtEvidenceCollector : BaseEvidenceCollector
{
    private readonly IAzureResourceService _azureResourceService;
    private readonly IAzurePolicyComplianceService _policyService;

    /// <inheritdoc />
    public override string FamilyCode => ControlFamilies.ConfigurationManagement;

    public ConfigMgmtEvidenceCollector(
        IAzureResourceService azureResourceService,
        IAzurePolicyComplianceService policyService,
        ILogger<ConfigMgmtEvidenceCollector> logger) : base(logger)
    {
        _azureResourceService = azureResourceService;
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

        // 1. Configuration — Resource lock inventory
        try
        {
            var locks = await _azureResourceService.GetResourceLocksAsync(
                subscriptionId, resourceGroup, cancellationToken);
            items.Add(CreateEvidenceItem(
                EvidenceType.Configuration,
                "Resource Lock Inventory",
                "Resource locks for CM-5 (Access Restrictions for Change).",
                $"Resource locks found: {locks.Count}",
                $"/subscriptions/{subscriptionId}"));
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to collect lock evidence for CM");
        }

        // 2. Log — Resource tagging compliance
        try
        {
            var resources = await _azureResourceService.GetResourcesAsync(
                subscriptionId, resourceGroup, null, cancellationToken);
            var taggedCount = resources.Count(r => r?.Data?.Tags?.Count > 0);
            items.Add(CreateEvidenceItem(
                EvidenceType.Log,
                "Resource Tagging Compliance",
                "Resource tagging status for CM-8 (System Component Inventory).",
                $"Total resources: {resources.Count}, Tagged: {taggedCount}",
                $"/subscriptions/{subscriptionId}"));
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to collect tagging evidence for CM");
        }

        // 3. Policy — Configuration management policy compliance
        try
        {
            var policyState = await _policyService.GetPolicyStatesAsync(
                subscriptionId, cancellationToken: cancellationToken);
            items.Add(CreateEvidenceItem(
                EvidenceType.Policy,
                "Configuration Management Policy Compliance",
                "Azure Policy compliance for configuration management policies (CM-2).",
                policyState ?? "No policy state available",
                $"/subscriptions/{subscriptionId}"));
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to collect policy evidence for CM");
        }

        // 4. Metric — Configuration compliance metrics
        try
        {
            var summary = await _policyService.GetComplianceSummaryAsync(subscriptionId, cancellationToken);
            items.Add(CreateEvidenceItem(
                EvidenceType.Metric,
                "Configuration Compliance Metrics",
                "Policy compliance summary metrics for CM-3 (Configuration Change Control).",
                summary ?? "No compliance summary available",
                $"/subscriptions/{subscriptionId}"));
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to collect metric evidence for CM");
        }

        // 5. AccessControl — Configuration change access
        try
        {
            var roleAssignments = await _azureResourceService.GetRoleAssignmentsAsync(
                subscriptionId, cancellationToken);
            items.Add(CreateEvidenceItem(
                EvidenceType.AccessControl,
                "Configuration Change Access Control",
                "Role assignments for configuration management (CM-5).",
                $"Role assignments for configuration control: {roleAssignments.Count}",
                $"/subscriptions/{subscriptionId}"));
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to collect access control evidence for CM");
        }

        return items;
    }
}
