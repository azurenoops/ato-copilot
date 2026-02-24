using Microsoft.Extensions.Logging;
using Ato.Copilot.Core.Constants;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Agents.Compliance.Scanners;

/// <summary>
/// Scanner for the Incident Response (IR) family.
/// Checks action groups, alert rules, activity log alerts, and playbook readiness.
/// </summary>
public class IncidentResponseScanner : BaseComplianceScanner
{
    private readonly IAzureResourceService _azureResourceService;
    private readonly IAzurePolicyComplianceService _policyService;

    /// <inheritdoc />
    public override string FamilyCode => ControlFamilies.IncidentResponse;

    public IncidentResponseScanner(
        IAzureResourceService azureResourceService,
        IAzurePolicyComplianceService policyService,
        ILogger<IncidentResponseScanner> logger) : base(logger)
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

        // Check for action groups (notification targets)
        var actionGroups = await _azureResourceService.GetResourcesAsync(
            subscriptionId, resourceGroup, "Microsoft.Insights/actionGroups", cancellationToken);

        if (actionGroups.Count == 0)
        {
            findings.Add(new ComplianceFinding
            {
                ControlId = "IR-6",
                ControlFamily = ControlFamilies.IncidentResponse,
                Title = "No alert action groups configured",
                Description = "No Azure Monitor action groups found. Per IR-6 (Incident Reporting), " +
                              "security events must trigger notifications to designated personnel.",
                Severity = FindingSeverity.High,
                Status = FindingStatus.Open,
                ResourceId = $"/subscriptions/{subscriptionId}",
                ResourceType = "Microsoft.Insights/actionGroups",
                RemediationGuidance = "Create action groups with email/SMS/webhook receivers for incident notification.",
                ScanSource = ScanSourceType.Resource,
                AutoRemediable = false
            });
        }

        // Check for alert rules
        var metricAlerts = await _azureResourceService.GetResourcesAsync(
            subscriptionId, resourceGroup, "Microsoft.Insights/metricAlerts", cancellationToken);
        var activityLogAlerts = await _azureResourceService.GetResourcesAsync(
            subscriptionId, resourceGroup, "Microsoft.Insights/activityLogAlerts", cancellationToken);

        int totalAlerts = metricAlerts.Count + activityLogAlerts.Count;
        if (totalAlerts == 0)
        {
            findings.Add(new ComplianceFinding
            {
                ControlId = "IR-4",
                ControlFamily = ControlFamilies.IncidentResponse,
                Title = "No alert rules configured",
                Description = "No metric or activity log alerts found. Per IR-4 (Incident Handling), " +
                              "automated alerting must detect and report security incidents.",
                Severity = FindingSeverity.High,
                Status = FindingStatus.Open,
                ResourceId = $"/subscriptions/{subscriptionId}",
                ResourceType = "Microsoft.Insights/metricAlerts",
                RemediationGuidance = "Configure activity log alerts for security events and metric alerts for resource health.",
                ScanSource = ScanSourceType.Resource,
                AutoRemediable = false
            });
        }

        // Check for Logic App playbooks (automated response)
        var logicApps = await _azureResourceService.GetResourcesAsync(
            subscriptionId, resourceGroup, "Microsoft.Logic/workflows", cancellationToken);

        if (logicApps.Count == 0 && actionGroups.Count > 0)
        {
            findings.Add(new ComplianceFinding
            {
                ControlId = "IR-5",
                ControlFamily = ControlFamilies.IncidentResponse,
                Title = "No automated incident response playbooks",
                Description = "Action groups exist but no Logic App playbooks for automated response. " +
                              "Per IR-5 (Incident Monitoring), consider automated response procedures.",
                Severity = FindingSeverity.Low,
                Status = FindingStatus.Open,
                ResourceId = $"/subscriptions/{subscriptionId}",
                ResourceType = "Microsoft.Logic/workflows",
                RemediationGuidance = "Create Logic App playbooks for automated incident response.",
                ScanSource = ScanSourceType.Resource,
                AutoRemediable = false
            });
        }

        // Policy compliance for IR
        var policyStates = await _policyService.GetPolicyStatesAsync(subscriptionId, cancellationToken: cancellationToken);
        if (!string.IsNullOrEmpty(policyStates) && policyStates.Contains("NonCompliant", StringComparison.OrdinalIgnoreCase))
        {
            findings.Add(new ComplianceFinding
            {
                ControlId = "IR-2",
                ControlFamily = ControlFamilies.IncidentResponse,
                Title = "Non-compliant incident response policies",
                Description = "Azure Policy reports non-compliance with incident response policies.",
                Severity = FindingSeverity.Medium,
                Status = FindingStatus.Open,
                ResourceId = $"/subscriptions/{subscriptionId}",
                ResourceType = "Microsoft.Authorization/policyStates",
                RemediationGuidance = "Remediate non-compliant incident response policy assignments.",
                ScanSource = ScanSourceType.Policy,
                AutoRemediable = true,
                RemediationType = RemediationType.PolicyRemediation
            });
        }

        return findings;
    }
}
