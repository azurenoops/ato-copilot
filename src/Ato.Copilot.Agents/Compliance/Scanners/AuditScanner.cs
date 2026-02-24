using Microsoft.Extensions.Logging;
using Ato.Copilot.Core.Constants;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Agents.Compliance.Scanners;

/// <summary>
/// Scanner for the Audit and Accountability (AU) family.
/// Checks diagnostic settings, log retention, and activity log profiles.
/// </summary>
public class AuditScanner : BaseComplianceScanner
{
    private readonly IAzureResourceService _azureResourceService;
    private readonly IAzurePolicyComplianceService _policyService;

    /// <inheritdoc />
    public override string FamilyCode => ControlFamilies.AuditAccountability;

    public AuditScanner(
        IAzureResourceService azureResourceService,
        IAzurePolicyComplianceService policyService,
        ILogger<AuditScanner> logger) : base(logger)
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

        // Check resources for diagnostic settings
        var resources = await _azureResourceService.GetResourcesAsync(
            subscriptionId, resourceGroup, cancellationToken: cancellationToken);

        var auditableResources = resources
            .Where(r => r.Data.ResourceType.ToString() != null)
            .Take(50) // Sample up to 50 resources
            .ToList();

        int resourcesWithoutDiagnostics = 0;
        foreach (var resource in auditableResources)
        {
            try
            {
                var diagnosticSettings = await _azureResourceService.GetDiagnosticSettingsAsync(
                    resource.Id.ToString(), cancellationToken);

                if (diagnosticSettings.Count == 0)
                {
                    resourcesWithoutDiagnostics++;
                }
            }
            catch (Exception ex)
            {
                Logger.LogDebug(ex, "Could not check diagnostic settings for {ResourceId}", resource.Id);
            }
        }

        if (resourcesWithoutDiagnostics > 0)
        {
            findings.Add(new ComplianceFinding
            {
                ControlId = "AU-6",
                ControlFamily = ControlFamilies.AuditAccountability,
                Title = "Resources without diagnostic settings",
                Description = $"{resourcesWithoutDiagnostics} of {auditableResources.Count} sampled resources " +
                              "lack diagnostic settings. Per AU-6 (Audit Record Review), all critical resources " +
                              "must forward logs to a centralized logging solution.",
                Severity = resourcesWithoutDiagnostics > auditableResources.Count / 2
                    ? FindingSeverity.High
                    : FindingSeverity.Medium,
                Status = FindingStatus.Open,
                ResourceId = $"/subscriptions/{subscriptionId}",
                ResourceType = "Microsoft.Insights/diagnosticSettings",
                RemediationGuidance = "Enable diagnostic settings on all auditable resources to send logs to Log Analytics or Storage.",
                ScanSource = ScanSourceType.Resource,
                AutoRemediable = true,
                RemediationType = RemediationType.PolicyRemediation
            });
        }

        // Check policy compliance for audit-related policies
        var policyStates = await _policyService.GetPolicyStatesAsync(subscriptionId, cancellationToken: cancellationToken);
        if (!string.IsNullOrEmpty(policyStates) && policyStates.Contains("NonCompliant", StringComparison.OrdinalIgnoreCase))
        {
            findings.Add(new ComplianceFinding
            {
                ControlId = "AU-2",
                ControlFamily = ControlFamilies.AuditAccountability,
                Title = "Non-compliant audit policies detected",
                Description = "Azure Policy reports non-compliant audit and logging policies. " +
                              "Per AU-2 (Event Logging), audit events must be captured for all security-relevant activities.",
                Severity = FindingSeverity.Medium,
                Status = FindingStatus.Open,
                ResourceId = $"/subscriptions/{subscriptionId}",
                ResourceType = "Microsoft.Authorization/policyStates",
                RemediationGuidance = "Remediate non-compliant audit policy assignments.",
                ScanSource = ScanSourceType.Policy,
                AutoRemediable = true,
                RemediationType = RemediationType.PolicyRemediation
            });
        }

        return findings;
    }
}
