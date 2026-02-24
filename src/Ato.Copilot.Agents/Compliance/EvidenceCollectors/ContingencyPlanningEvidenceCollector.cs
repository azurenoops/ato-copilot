using Microsoft.Extensions.Logging;
using Ato.Copilot.Core.Constants;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Agents.Compliance.EvidenceCollectors;

/// <summary>
/// Evidence collector for the Contingency Planning (CP) family.
/// Collects Recovery Services vault, geo-replication, backup policy,
/// disaster recovery metrics, and backup access control evidence.
/// </summary>
public class ContingencyPlanningEvidenceCollector : BaseEvidenceCollector
{
    private readonly IAzureResourceService _azureResourceService;
    private readonly IAzurePolicyComplianceService _policyService;

    /// <inheritdoc />
    public override string FamilyCode => ControlFamilies.ContingencyPlanning;

    public ContingencyPlanningEvidenceCollector(
        IAzureResourceService azureResourceService,
        IAzurePolicyComplianceService policyService,
        ILogger<ContingencyPlanningEvidenceCollector> logger) : base(logger)
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

        // 1. Configuration — Recovery Services vault inventory
        try
        {
            var vaults = await _azureResourceService.GetResourcesAsync(
                subscriptionId, resourceGroup, "Microsoft.RecoveryServices/vaults", cancellationToken);
            items.Add(CreateEvidenceItem(
                EvidenceType.Configuration,
                "Recovery Services Vault Inventory",
                "Backup infrastructure inventory for CP-9 (System Backup).",
                $"Recovery Services Vaults found: {vaults.Count}",
                $"/subscriptions/{subscriptionId}"));
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to collect vault evidence for CP");
        }

        // 2. Log — SQL failover group evidence
        try
        {
            var failoverGroups = await _azureResourceService.GetResourcesAsync(
                subscriptionId, resourceGroup, "Microsoft.Sql/servers/failoverGroups", cancellationToken);
            items.Add(CreateEvidenceItem(
                EvidenceType.Log,
                "SQL Failover Group Evidence",
                "Geo-replication and failover configuration for CP-6 (Alternate Storage Site).",
                $"SQL Failover Groups found: {failoverGroups.Count}",
                $"/subscriptions/{subscriptionId}"));
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to collect failover evidence for CP");
        }

        // 3. Policy — Contingency planning policy compliance
        try
        {
            var policyState = await _policyService.GetPolicyStatesAsync(
                subscriptionId, cancellationToken: cancellationToken);
            items.Add(CreateEvidenceItem(
                EvidenceType.Policy,
                "Contingency Planning Policy Compliance",
                "Azure Policy compliance for backup and DR policies (CP-2).",
                policyState ?? "No policy state available",
                $"/subscriptions/{subscriptionId}"));
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to collect policy evidence for CP");
        }

        // 4. Metric — Backup resource metrics
        try
        {
            var vaults = await _azureResourceService.GetResourcesAsync(
                subscriptionId, resourceGroup, "Microsoft.RecoveryServices/vaults", cancellationToken);
            var databases = await _azureResourceService.GetResourcesAsync(
                subscriptionId, resourceGroup, "Microsoft.Sql/servers/databases", cancellationToken);
            items.Add(CreateEvidenceItem(
                EvidenceType.Metric,
                "Disaster Recovery Metrics",
                "Backup and replication coverage metrics (CP-10).",
                $"Recovery vaults: {vaults.Count}, SQL databases: {databases.Count}",
                $"/subscriptions/{subscriptionId}"));
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to collect DR metric evidence for CP");
        }

        // 5. AccessControl — Backup operator access
        try
        {
            var roleAssignments = await _azureResourceService.GetRoleAssignmentsAsync(
                subscriptionId, cancellationToken);
            items.Add(CreateEvidenceItem(
                EvidenceType.AccessControl,
                "Backup Operator Access Control",
                "Role assignments for backup management (CP-9).",
                $"Role assignments governing backup access: {roleAssignments.Count}",
                $"/subscriptions/{subscriptionId}"));
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to collect access control evidence for CP");
        }

        return items;
    }
}
