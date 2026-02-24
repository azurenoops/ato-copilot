using Microsoft.Extensions.Logging;
using Ato.Copilot.Core.Constants;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Agents.Compliance.EvidenceCollectors;

/// <summary>
/// Evidence collector for the Access Control (AC) family.
/// Collects RBAC assignments, policy state, access logs, role definitions,
/// and conditional-access–related evidence (5 evidence types).
/// </summary>
public class AccessControlEvidenceCollector : BaseEvidenceCollector
{
    private readonly IAzureResourceService _azureResourceService;
    private readonly IAzurePolicyComplianceService _policyService;

    /// <inheritdoc />
    public override string FamilyCode => ControlFamilies.AccessControl;

    public AccessControlEvidenceCollector(
        IAzureResourceService azureResourceService,
        IAzurePolicyComplianceService policyService,
        ILogger<AccessControlEvidenceCollector> logger) : base(logger)
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

        // 1. Configuration — RBAC role assignments snapshot
        try
        {
            var roleAssignments = await _azureResourceService.GetRoleAssignmentsAsync(
                subscriptionId, cancellationToken);
            items.Add(CreateEvidenceItem(
                EvidenceType.Configuration,
                "RBAC Role Assignments",
                "Snapshot of all role assignments in the subscription for AC-2 (Account Management).",
                $"Total role assignments: {roleAssignments.Count}",
                $"/subscriptions/{subscriptionId}"));
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to collect RBAC evidence for AC");
        }

        // 2. Policy — Access control policy compliance state
        try
        {
            var policyState = await _policyService.GetPolicyStatesAsync(
                subscriptionId, cancellationToken: cancellationToken);
            items.Add(CreateEvidenceItem(
                EvidenceType.Policy,
                "Access Control Policy Compliance",
                "Azure Policy compliance state for access-control–related policies (AC-1).",
                policyState ?? "No policy state available",
                $"/subscriptions/{subscriptionId}"));
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to collect policy evidence for AC");
        }

        // 3. Log — Role assignment activity logs
        try
        {
            var diagnostics = await _azureResourceService.GetDiagnosticSettingsAsync(
                $"/subscriptions/{subscriptionId}", cancellationToken);
            items.Add(CreateEvidenceItem(
                EvidenceType.Log,
                "Access Activity Diagnostic Settings",
                "Diagnostic settings audit for role assignment tracking (AC-6).",
                $"Diagnostic settings found: {diagnostics.Count}",
                $"/subscriptions/{subscriptionId}"));
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to collect log evidence for AC");
        }

        // 4. Metric — Resource count summary as an access scope metric
        try
        {
            var resources = await _azureResourceService.GetResourcesAsync(
                subscriptionId, resourceGroup, null, cancellationToken);
            items.Add(CreateEvidenceItem(
                EvidenceType.Metric,
                "Resource Scope Metrics",
                "Count of resources under access control scope (AC-3).",
                $"Total resources in scope: {resources.Count}",
                $"/subscriptions/{subscriptionId}"));
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to collect metric evidence for AC");
        }

        // 5. AccessControl — Role definition inventory
        try
        {
            var roleAssignments = await _azureResourceService.GetRoleAssignmentsAsync(
                subscriptionId, cancellationToken);
            var distinctRoles = roleAssignments
                .Select(r => r.Data.RoleDefinitionId?.ToString() ?? "unknown")
                .Distinct()
                .Count();
            items.Add(CreateEvidenceItem(
                EvidenceType.AccessControl,
                "Role Definition Inventory",
                "Distinct role definitions in use for AC-5 (Separation of Duties).",
                $"Distinct role definitions in use: {distinctRoles}",
                $"/subscriptions/{subscriptionId}"));
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to collect access control evidence for AC");
        }

        return items;
    }
}
