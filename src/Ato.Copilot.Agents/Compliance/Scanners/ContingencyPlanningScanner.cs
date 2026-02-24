using Microsoft.Extensions.Logging;
using Ato.Copilot.Core.Constants;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Agents.Compliance.Scanners;

/// <summary>
/// Scanner for the Contingency Planning (CP) family.
/// Checks Recovery Services vaults, geo-replication, and availability zones.
/// </summary>
public class ContingencyPlanningScanner : BaseComplianceScanner
{
    private readonly IAzureResourceService _azureResourceService;
    private readonly IAzurePolicyComplianceService _policyService;

    /// <inheritdoc />
    public override string FamilyCode => ControlFamilies.ContingencyPlanning;

    public ContingencyPlanningScanner(
        IAzureResourceService azureResourceService,
        IAzurePolicyComplianceService policyService,
        ILogger<ContingencyPlanningScanner> logger) : base(logger)
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

        // Check for Recovery Services vaults (backup infrastructure)
        var vaults = await _azureResourceService.GetResourcesAsync(
            subscriptionId, resourceGroup, "Microsoft.RecoveryServices/vaults", cancellationToken);

        if (vaults.Count == 0)
        {
            findings.Add(new ComplianceFinding
            {
                ControlId = "CP-9",
                ControlFamily = ControlFamilies.ContingencyPlanning,
                Title = "No backup infrastructure detected",
                Description = "No Recovery Services vaults found. Per CP-9 (Information System Backup), " +
                              "critical data must be backed up with tested recovery procedures.",
                Severity = FindingSeverity.Critical,
                Status = FindingStatus.Open,
                ResourceId = $"/subscriptions/{subscriptionId}",
                ResourceType = "Microsoft.RecoveryServices/vaults",
                RemediationGuidance = "Deploy Azure Recovery Services Vault and configure backup policies.",
                ScanSource = ScanSourceType.Resource,
                AutoRemediable = false
            });
        }

        // Check for geo-replicated SQL databases
        var sqlServers = await _azureResourceService.GetResourcesAsync(
            subscriptionId, resourceGroup, "Microsoft.Sql/servers", cancellationToken);

        if (sqlServers.Count > 0)
        {
            var databases = await _azureResourceService.GetResourcesAsync(
                subscriptionId, resourceGroup, "Microsoft.Sql/servers/databases", cancellationToken);

            // Check for failover groups or geo-replication
            var failoverGroups = await _azureResourceService.GetResourcesAsync(
                subscriptionId, resourceGroup, "Microsoft.Sql/servers/failoverGroups", cancellationToken);

            if (databases.Count > 0 && failoverGroups.Count == 0)
            {
                findings.Add(new ComplianceFinding
                {
                    ControlId = "CP-6",
                    ControlFamily = ControlFamilies.ContingencyPlanning,
                    Title = "SQL databases without geo-replication",
                    Description = $"Found {databases.Count} SQL databases but no failover groups. " +
                                  "Per CP-6 (Alternate Storage Site), data must be replicated geographically.",
                    Severity = FindingSeverity.High,
                    Status = FindingStatus.Open,
                    ResourceId = $"/subscriptions/{subscriptionId}",
                    ResourceType = "Microsoft.Sql/servers/databases",
                    RemediationGuidance = "Configure auto-failover groups for critical databases.",
                    ScanSource = ScanSourceType.Resource,
                    AutoRemediable = false
                });
            }
        }

        // Check policy compliance for contingency planning
        var policyStates = await _policyService.GetPolicyStatesAsync(subscriptionId, cancellationToken: cancellationToken);
        if (!string.IsNullOrEmpty(policyStates) && policyStates.Contains("NonCompliant", StringComparison.OrdinalIgnoreCase))
        {
            findings.Add(new ComplianceFinding
            {
                ControlId = "CP-2",
                ControlFamily = ControlFamilies.ContingencyPlanning,
                Title = "Non-compliant contingency planning policies",
                Description = "Azure Policy reports non-compliant backup or DR policies.",
                Severity = FindingSeverity.Medium,
                Status = FindingStatus.Open,
                ResourceId = $"/subscriptions/{subscriptionId}",
                ResourceType = "Microsoft.Authorization/policyStates",
                RemediationGuidance = "Review and remediate non-compliant contingency planning policy assignments.",
                ScanSource = ScanSourceType.Policy,
                AutoRemediable = true,
                RemediationType = RemediationType.PolicyRemediation
            });
        }

        return findings;
    }
}
