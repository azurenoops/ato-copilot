using Microsoft.Extensions.Logging;
using Ato.Copilot.Core.Constants;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Agents.Compliance.EvidenceCollectors;

/// <summary>
/// Evidence collector for the Audit and Accountability (AU) family.
/// Collects diagnostic settings, log retention, audit policy states,
/// activity log metrics, and audit access control evidence.
/// </summary>
public class AuditEvidenceCollector : BaseEvidenceCollector
{
    private readonly IAzureResourceService _azureResourceService;
    private readonly IAzurePolicyComplianceService _policyService;

    /// <inheritdoc />
    public override string FamilyCode => ControlFamilies.AuditAccountability;

    public AuditEvidenceCollector(
        IAzureResourceService azureResourceService,
        IAzurePolicyComplianceService policyService,
        ILogger<AuditEvidenceCollector> logger) : base(logger)
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

        // 1. Configuration — Diagnostic settings snapshot
        try
        {
            var diagnostics = await _azureResourceService.GetDiagnosticSettingsAsync(
                $"/subscriptions/{subscriptionId}", cancellationToken);
            items.Add(CreateEvidenceItem(
                EvidenceType.Configuration,
                "Diagnostic Settings Configuration",
                "Audit logging diagnostic settings for AU-2 (Audit Events).",
                $"Diagnostic settings found: {diagnostics.Count}",
                $"/subscriptions/{subscriptionId}"));
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to collect diagnostic settings evidence for AU");
        }

        // 2. Log — Activity log sample
        try
        {
            var resources = await _azureResourceService.GetResourcesAsync(
                subscriptionId, resourceGroup, "Microsoft.Insights/diagnosticSettings", cancellationToken);
            items.Add(CreateEvidenceItem(
                EvidenceType.Log,
                "Audit Log Configuration Evidence",
                "Diagnostic setting resources for AU-6 (Audit Review, Analysis, and Reporting).",
                $"Diagnostic setting resources found: {resources.Count}",
                $"/subscriptions/{subscriptionId}"));
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to collect audit log evidence for AU");
        }

        // 3. Policy — Audit policy compliance state
        try
        {
            var policyState = await _policyService.GetPolicyStatesAsync(
                subscriptionId, cancellationToken: cancellationToken);
            items.Add(CreateEvidenceItem(
                EvidenceType.Policy,
                "Audit Policy Compliance State",
                "Azure Policy compliance for audit-related policies (AU-1).",
                policyState ?? "No policy state available",
                $"/subscriptions/{subscriptionId}"));
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to collect policy evidence for AU");
        }

        // 4. Metric — Log retention metric
        try
        {
            var diagnostics = await _azureResourceService.GetDiagnosticSettingsAsync(
                $"/subscriptions/{subscriptionId}", cancellationToken);
            var hasRetention = diagnostics.Count > 0;
            items.Add(CreateEvidenceItem(
                EvidenceType.Metric,
                "Log Retention Metrics",
                "Log retention configuration status for AU-11 (Audit Record Retention).",
                $"Retention configured: {hasRetention}, Diagnostic settings: {diagnostics.Count}",
                $"/subscriptions/{subscriptionId}"));
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to collect metric evidence for AU");
        }

        // 5. AccessControl — Log access permissions
        try
        {
            var roleAssignments = await _azureResourceService.GetRoleAssignmentsAsync(
                subscriptionId, cancellationToken);
            var monitorRoles = roleAssignments
                .Where(r => r.Data.RoleDefinitionId?.ToString()?.Contains("monitor", StringComparison.OrdinalIgnoreCase) == true)
                .ToList();
            items.Add(CreateEvidenceItem(
                EvidenceType.AccessControl,
                "Audit Log Access Control",
                "Role assignments with monitoring/log access for AU-9 (Protection of Audit Information).",
                $"Monitor-related role assignments: {monitorRoles.Count}",
                $"/subscriptions/{subscriptionId}"));
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to collect access control evidence for AU");
        }

        return items;
    }
}
