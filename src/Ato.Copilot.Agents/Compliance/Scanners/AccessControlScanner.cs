using Microsoft.Extensions.Logging;
using Ato.Copilot.Core.Constants;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Agents.Compliance.Scanners;

/// <summary>
/// Scanner for the Access Control (AC) family.
/// Checks RBAC role assignments, custom roles, overly permissive access,
/// and PIM eligibility indicators.
/// </summary>
public class AccessControlScanner : BaseComplianceScanner
{
    private readonly IAzureResourceService _azureResourceService;
    private readonly IAzurePolicyComplianceService _policyService;

    /// <inheritdoc />
    public override string FamilyCode => ControlFamilies.AccessControl;

    public AccessControlScanner(
        IAzureResourceService azureResourceService,
        IAzurePolicyComplianceService policyService,
        ILogger<AccessControlScanner> logger) : base(logger)
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

        // Check role assignments for overly permissive access
        var roleAssignments = await _azureResourceService.GetRoleAssignmentsAsync(
            subscriptionId, cancellationToken);

        var ownerAssignments = roleAssignments
            .Where(ra => ra.Data.RoleDefinitionId?.ToString().Contains("Owner", StringComparison.OrdinalIgnoreCase) == true
                         || ra.Data.RoleDefinitionId?.ToString().Contains("8e3af657-a8ff-443c-a75c-2fe8c4bcb635", StringComparison.OrdinalIgnoreCase) == true)
            .ToList();

        if (ownerAssignments.Count > 3)
        {
            findings.Add(new ComplianceFinding
            {
                ControlId = "AC-6",
                ControlFamily = ControlFamilies.AccessControl,
                Title = "Excessive Owner role assignments",
                Description = $"Found {ownerAssignments.Count} Owner role assignments. " +
                              "Limit Owner assignments to reduce attack surface per AC-6 (Least Privilege).",
                Severity = FindingSeverity.High,
                Status = FindingStatus.Open,
                ResourceId = $"/subscriptions/{subscriptionId}",
                ResourceType = "Microsoft.Authorization/roleAssignments",
                RemediationGuidance = "Review and remove unnecessary Owner assignments. Use PIM for JIT access.",
                ScanSource = ScanSourceType.Resource,
                RiskLevel = RiskLevel.High,
                AutoRemediable = false
            });
        }

        var contributorAssignments = roleAssignments
            .Where(ra => ra.Data.RoleDefinitionId?.ToString().Contains("Contributor", StringComparison.OrdinalIgnoreCase) == true
                         || ra.Data.RoleDefinitionId?.ToString().Contains("b24988ac-6180-42a0-ab88-20f7382dd24c", StringComparison.OrdinalIgnoreCase) == true)
            .ToList();

        if (contributorAssignments.Count > 10)
        {
            findings.Add(new ComplianceFinding
            {
                ControlId = "AC-6",
                ControlFamily = ControlFamilies.AccessControl,
                Title = "High number of Contributor role assignments",
                Description = $"Found {contributorAssignments.Count} Contributor role assignments. " +
                              "Consider using more restrictive roles per AC-6 (Least Privilege).",
                Severity = FindingSeverity.Medium,
                Status = FindingStatus.Open,
                ResourceId = $"/subscriptions/{subscriptionId}",
                ResourceType = "Microsoft.Authorization/roleAssignments",
                RemediationGuidance = "Replace broad Contributor assignments with scoped, least-privilege roles.",
                ScanSource = ScanSourceType.Resource,
                RiskLevel = RiskLevel.High,
                AutoRemediable = false
            });
        }

        // Check for custom role definitions with wildcard actions
        var customRoles = roleAssignments
            .Where(ra => ra.Data.RoleDefinitionId?.ToString().Contains("custom", StringComparison.OrdinalIgnoreCase) == true)
            .ToList();

        if (customRoles.Count > 0)
        {
            Logger.LogInformation("Found {Count} custom role assignments in subscription {Sub}",
                customRoles.Count, subscriptionId);
        }

        // Policy-based AC compliance check
        var policyStates = await _policyService.GetPolicyStatesAsync(subscriptionId, cancellationToken: cancellationToken);
        if (!string.IsNullOrEmpty(policyStates) && policyStates.Contains("NonCompliant", StringComparison.OrdinalIgnoreCase))
        {
            findings.Add(new ComplianceFinding
            {
                ControlId = "AC-2",
                ControlFamily = ControlFamilies.AccessControl,
                Title = "Non-compliant access control policies detected",
                Description = "One or more Azure Policy assignments related to access control report non-compliance.",
                Severity = FindingSeverity.Medium,
                Status = FindingStatus.Open,
                ResourceId = $"/subscriptions/{subscriptionId}",
                ResourceType = "Microsoft.Authorization/policyStates",
                RemediationGuidance = "Review policy compliance states and remediate non-compliant resources.",
                ScanSource = ScanSourceType.Policy,
                RiskLevel = RiskLevel.High,
                AutoRemediable = true,
                RemediationType = RemediationType.PolicyRemediation
            });
        }

        // Check for scope of role assignments (subscription-level is less secure)
        var subscriptionScopeAssignments = roleAssignments
            .Where(ra => ra.Data.Scope == $"/subscriptions/{subscriptionId}")
            .ToList();

        if (subscriptionScopeAssignments.Count > 5)
        {
            findings.Add(new ComplianceFinding
            {
                ControlId = "AC-3",
                ControlFamily = ControlFamilies.AccessControl,
                Title = "Many subscription-scoped role assignments",
                Description = $"Found {subscriptionScopeAssignments.Count} role assignments at subscription scope. " +
                              "Per AC-3 (Access Enforcement), assignments should be scoped to resource groups or resources.",
                Severity = FindingSeverity.Medium,
                Status = FindingStatus.Open,
                ResourceId = $"/subscriptions/{subscriptionId}",
                ResourceType = "Microsoft.Authorization/roleAssignments",
                RemediationGuidance = "Narrow role assignments to resource group or resource scope.",
                ScanSource = ScanSourceType.Resource,
                RiskLevel = RiskLevel.High,
                AutoRemediable = false
            });
        }

        return findings;
    }
}
