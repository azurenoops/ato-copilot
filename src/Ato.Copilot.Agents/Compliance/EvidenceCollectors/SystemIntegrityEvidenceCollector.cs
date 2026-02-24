using Microsoft.Extensions.Logging;
using Ato.Copilot.Core.Constants;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Agents.Compliance.EvidenceCollectors;

/// <summary>
/// Evidence collector for the System and Information Integrity (SI) family.
/// Collects VM patching status, antimalware extensions, Defender assessments,
/// policy compliance, and update management access control evidence.
/// </summary>
public class SystemIntegrityEvidenceCollector : BaseEvidenceCollector
{
    private readonly IAzureResourceService _azureResourceService;
    private readonly IDefenderForCloudService _defenderService;
    private readonly IAzurePolicyComplianceService _policyService;

    /// <inheritdoc />
    public override string FamilyCode => ControlFamilies.SystemInformationIntegrity;

    public SystemIntegrityEvidenceCollector(
        IAzureResourceService azureResourceService,
        IDefenderForCloudService defenderService,
        IAzurePolicyComplianceService policyService,
        ILogger<SystemIntegrityEvidenceCollector> logger) : base(logger)
    {
        _azureResourceService = azureResourceService;
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

        // 1. Configuration — VM inventory for patching assessment
        try
        {
            var vms = await _azureResourceService.GetResourcesAsync(
                subscriptionId, resourceGroup, "Microsoft.Compute/virtualMachines", cancellationToken);
            items.Add(CreateEvidenceItem(
                EvidenceType.Configuration,
                "Virtual Machine Inventory",
                "VM inventory for SI-2 (Flaw Remediation) patching assessment.",
                $"Virtual Machines found: {vms.Count}",
                $"/subscriptions/{subscriptionId}"));
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to collect VM evidence for SI");
        }

        // 2. Log — Defender for Cloud assessments
        try
        {
            var assessments = await _defenderService.GetAssessmentsAsync(subscriptionId, cancellationToken);
            items.Add(CreateEvidenceItem(
                EvidenceType.Log,
                "Defender Security Assessments",
                "Defender for Cloud assessment results for SI-4 (System Monitoring).",
                assessments ?? "No Defender assessments available",
                $"/subscriptions/{subscriptionId}"));
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to collect Defender evidence for SI");
        }

        // 3. Policy — Integrity policy compliance
        try
        {
            var policyState = await _policyService.GetPolicyStatesAsync(
                subscriptionId, cancellationToken: cancellationToken);
            items.Add(CreateEvidenceItem(
                EvidenceType.Policy,
                "System Integrity Policy Compliance",
                "Azure Policy compliance for integrity-related policies (SI-1).",
                policyState ?? "No policy state available",
                $"/subscriptions/{subscriptionId}"));
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to collect policy evidence for SI");
        }

        // 4. Metric — Defender secure score as integrity metric
        try
        {
            var secureScore = await _defenderService.GetSecureScoreAsync(subscriptionId, cancellationToken);
            items.Add(CreateEvidenceItem(
                EvidenceType.Metric,
                "Security Posture Metrics",
                "Defender secure score as a system integrity metric (SI-5).",
                secureScore ?? "Secure score not available",
                $"/subscriptions/{subscriptionId}"));
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to collect metric evidence for SI");
        }

        // 5. AccessControl — Update management access
        try
        {
            var roleAssignments = await _azureResourceService.GetRoleAssignmentsAsync(
                subscriptionId, cancellationToken);
            items.Add(CreateEvidenceItem(
                EvidenceType.AccessControl,
                "Update Management Access Control",
                "Role assignments for update/patch management (SI-2).",
                $"Total role assignments governing update access: {roleAssignments.Count}",
                $"/subscriptions/{subscriptionId}"));
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to collect access control evidence for SI");
        }

        return items;
    }
}
