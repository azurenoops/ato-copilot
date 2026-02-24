using Microsoft.Extensions.Logging;
using Ato.Copilot.Core.Constants;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Agents.Compliance.EvidenceCollectors;

/// <summary>
/// Evidence collector for the Incident Response (IR) family.
/// Collects action group, alert rule, playbook, policy compliance,
/// and incident response access control evidence.
/// </summary>
public class IncidentResponseEvidenceCollector : BaseEvidenceCollector
{
    private readonly IAzureResourceService _azureResourceService;
    private readonly IAzurePolicyComplianceService _policyService;

    /// <inheritdoc />
    public override string FamilyCode => ControlFamilies.IncidentResponse;

    public IncidentResponseEvidenceCollector(
        IAzureResourceService azureResourceService,
        IAzurePolicyComplianceService policyService,
        ILogger<IncidentResponseEvidenceCollector> logger) : base(logger)
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

        // 1. Configuration — Action group inventory
        try
        {
            var actionGroups = await _azureResourceService.GetResourcesAsync(
                subscriptionId, resourceGroup, "Microsoft.Insights/actionGroups", cancellationToken);
            items.Add(CreateEvidenceItem(
                EvidenceType.Configuration,
                "Action Group Inventory",
                "Notification action groups for IR-6 (Incident Reporting).",
                $"Action Groups found: {actionGroups.Count}",
                $"/subscriptions/{subscriptionId}"));
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to collect action group evidence for IR");
        }

        // 2. Log — Alert rule inventory
        try
        {
            var alertRules = await _azureResourceService.GetResourcesAsync(
                subscriptionId, resourceGroup, "Microsoft.Insights/metricAlerts", cancellationToken);
            items.Add(CreateEvidenceItem(
                EvidenceType.Log,
                "Alert Rule Inventory",
                "Metric alert rules for IR-4 (Incident Handling).",
                $"Alert Rules found: {alertRules.Count}",
                $"/subscriptions/{subscriptionId}"));
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to collect alert rule evidence for IR");
        }

        // 3. Policy — Incident response policy compliance
        try
        {
            var policyState = await _policyService.GetPolicyStatesAsync(
                subscriptionId, cancellationToken: cancellationToken);
            items.Add(CreateEvidenceItem(
                EvidenceType.Policy,
                "Incident Response Policy Compliance",
                "Azure Policy compliance for incident response policies (IR-2).",
                policyState ?? "No policy state available",
                $"/subscriptions/{subscriptionId}"));
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to collect policy evidence for IR");
        }

        // 4. Metric — Playbook inventory
        try
        {
            var playbooks = await _azureResourceService.GetResourcesAsync(
                subscriptionId, resourceGroup, "Microsoft.Logic/workflows", cancellationToken);
            items.Add(CreateEvidenceItem(
                EvidenceType.Metric,
                "Incident Response Playbook Metrics",
                "Logic App playbooks for IR-5 (Incident Monitoring).",
                $"Logic App playbooks found: {playbooks.Count}",
                $"/subscriptions/{subscriptionId}"));
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to collect playbook evidence for IR");
        }

        // 5. AccessControl — Incident response access
        try
        {
            var roleAssignments = await _azureResourceService.GetRoleAssignmentsAsync(
                subscriptionId, cancellationToken);
            items.Add(CreateEvidenceItem(
                EvidenceType.AccessControl,
                "Incident Response Access Control",
                "Role assignments for incident response operations (IR-3).",
                $"Role assignments for incident response: {roleAssignments.Count}",
                $"/subscriptions/{subscriptionId}"));
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to collect access control evidence for IR");
        }

        return items;
    }
}
