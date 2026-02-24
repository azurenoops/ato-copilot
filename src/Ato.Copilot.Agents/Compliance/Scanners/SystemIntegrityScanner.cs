using Microsoft.Extensions.Logging;
using Ato.Copilot.Core.Constants;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Agents.Compliance.Scanners;

/// <summary>
/// Scanner for the System and Information Integrity (SI) family.
/// Checks VM extensions, update compliance, guest configuration, and antimalware.
/// </summary>
public class SystemIntegrityScanner : BaseComplianceScanner
{
    private readonly IAzureResourceService _azureResourceService;
    private readonly IAzurePolicyComplianceService _policyService;
    private readonly IDefenderForCloudService _defenderService;

    /// <inheritdoc />
    public override string FamilyCode => ControlFamilies.SystemInformationIntegrity;

    public SystemIntegrityScanner(
        IAzureResourceService azureResourceService,
        IAzurePolicyComplianceService policyService,
        IDefenderForCloudService defenderService,
        ILogger<SystemIntegrityScanner> logger) : base(logger)
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

        // Check VMs for update compliance
        var vms = await _azureResourceService.GetResourcesAsync(
            subscriptionId, resourceGroup, "Microsoft.Compute/virtualMachines", cancellationToken);

        if (vms.Count > 0)
        {
            // Check policy compliance for patch management
            var policyStates = await _policyService.GetPolicyStatesAsync(subscriptionId, cancellationToken: cancellationToken);
            if (!string.IsNullOrEmpty(policyStates) && policyStates.Contains("NonCompliant", StringComparison.OrdinalIgnoreCase))
            {
                findings.Add(new ComplianceFinding
                {
                    ControlId = "SI-2",
                    ControlFamily = ControlFamilies.SystemInformationIntegrity,
                    Title = "Virtual machines may have missing patches",
                    Description = $"Found {vms.Count} VMs with non-compliant policy states. " +
                                  "Per SI-2 (Flaw Remediation), systems must have timely security patches applied.",
                    Severity = FindingSeverity.High,
                    Status = FindingStatus.Open,
                    ResourceId = $"/subscriptions/{subscriptionId}",
                    ResourceType = "Microsoft.Compute/virtualMachines",
                    RemediationGuidance = "Enable Azure Update Manager to automate patch management.",
                    ScanSource = ScanSourceType.Policy,
                    AutoRemediable = true,
                    RemediationType = RemediationType.PolicyRemediation
                });
            }

            // Check for antimalware extensions
            var vmExtensions = await _azureResourceService.GetResourcesAsync(
                subscriptionId, resourceGroup, "Microsoft.Compute/virtualMachines/extensions", cancellationToken);

            var antimalwareExtensions = vmExtensions
                .Where(e => e.Data.Name?.Contains("Antimalware", StringComparison.OrdinalIgnoreCase) == true
                            || e.Data.Name?.Contains("EndpointProtection", StringComparison.OrdinalIgnoreCase) == true)
                .ToList();

            if (antimalwareExtensions.Count < vms.Count)
            {
                findings.Add(new ComplianceFinding
                {
                    ControlId = "SI-3",
                    ControlFamily = ControlFamilies.SystemInformationIntegrity,
                    Title = "Virtual machines without antimalware protection",
                    Description = $"Only {antimalwareExtensions.Count} of {vms.Count} VMs have antimalware extensions. " +
                                  "Per SI-3 (Malicious Code Protection), all systems must have malware protection.",
                    Severity = FindingSeverity.High,
                    Status = FindingStatus.Open,
                    ResourceId = $"/subscriptions/{subscriptionId}",
                    ResourceType = "Microsoft.Compute/virtualMachines",
                    RemediationGuidance = "Deploy Microsoft Antimalware or Defender for Endpoint on all VMs.",
                    ScanSource = ScanSourceType.Resource,
                    AutoRemediable = true,
                    RemediationType = RemediationType.PolicyRemediation
                });
            }
        }

        // Defender assessments for integrity
        var assessments = await _defenderService.GetAssessmentsAsync(subscriptionId, cancellationToken);
        if (!string.IsNullOrEmpty(assessments) && assessments.Contains("Unhealthy", StringComparison.OrdinalIgnoreCase))
        {
            findings.Add(new ComplianceFinding
            {
                ControlId = "SI-4",
                ControlFamily = ControlFamilies.SystemInformationIntegrity,
                Title = "Defender for Cloud reports unhealthy assessments",
                Description = "Microsoft Defender reports unhealthy resource assessments. " +
                              "Per SI-4 (System Monitoring), continuous monitoring must detect anomalies.",
                Severity = FindingSeverity.Medium,
                Status = FindingStatus.Open,
                ResourceId = $"/subscriptions/{subscriptionId}",
                ResourceType = "Microsoft.Security/assessments",
                RemediationGuidance = "Review and remediate Defender for Cloud assessments.",
                ScanSource = ScanSourceType.Defender,
                AutoRemediable = false
            });
        }

        return findings;
    }
}
