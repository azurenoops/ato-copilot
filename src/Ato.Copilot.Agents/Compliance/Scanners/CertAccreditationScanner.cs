using Microsoft.Extensions.Logging;
using Ato.Copilot.Core.Constants;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Agents.Compliance.Scanners;

/// <summary>
/// Scanner for the Certification, Accreditation, and Security Assessments (CA) family.
/// Delegates to Defender for Cloud regulatory compliance and recommendations.
/// </summary>
public class CertAccreditationScanner : BaseComplianceScanner
{
    private readonly IDefenderForCloudService _defenderService;
    private readonly IAzurePolicyComplianceService _policyService;

    /// <inheritdoc />
    public override string FamilyCode => ControlFamilies.AssessmentAuthorization;

    public CertAccreditationScanner(
        IDefenderForCloudService defenderService,
        IAzurePolicyComplianceService policyService,
        ILogger<CertAccreditationScanner> logger) : base(logger)
    {
        _defenderService = defenderService;
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

        // Check Defender regulatory compliance
        var assessments = await _defenderService.GetAssessmentsAsync(subscriptionId, cancellationToken);
        if (!string.IsNullOrEmpty(assessments) && assessments.Contains("Unhealthy", StringComparison.OrdinalIgnoreCase))
        {
            findings.Add(new ComplianceFinding
            {
                ControlId = "CA-2",
                ControlFamily = ControlFamilies.AssessmentAuthorization,
                Title = "Unhealthy security assessments detected",
                Description = "Defender for Cloud reports unhealthy security assessments. " +
                              "Per CA-2 (Control Assessments), controls must be periodically assessed.",
                Severity = FindingSeverity.High,
                Status = FindingStatus.Open,
                ResourceId = $"/subscriptions/{subscriptionId}",
                ResourceType = "Microsoft.Security/assessments",
                RemediationGuidance = "Review and remediate Defender for Cloud assessment findings.",
                ScanSource = ScanSourceType.Defender,
                AutoRemediable = false
            });
        }

        // Check recommendations for security assessment gaps
        var recommendations = await _defenderService.GetRecommendationsAsync(subscriptionId, cancellationToken);
        if (!string.IsNullOrEmpty(recommendations) &&
            (recommendations.Contains("security", StringComparison.OrdinalIgnoreCase) ||
             recommendations.Contains("assessment", StringComparison.OrdinalIgnoreCase)))
        {
            findings.Add(new ComplianceFinding
            {
                ControlId = "CA-7",
                ControlFamily = ControlFamilies.AssessmentAuthorization,
                Title = "Pending security recommendations",
                Description = "Defender for Cloud has unresolved security recommendations. " +
                              "Per CA-7 (Continuous Monitoring), security controls must be continuously monitored.",
                Severity = FindingSeverity.Medium,
                Status = FindingStatus.Open,
                ResourceId = $"/subscriptions/{subscriptionId}",
                ResourceType = "Microsoft.Security/assessments",
                RemediationGuidance = "Address Defender for Cloud security recommendations.",
                ScanSource = ScanSourceType.Defender,
                AutoRemediable = false
            });
        }

        // Policy compliance for CA controls
        var policyCompliance = await _policyService.GetComplianceSummaryAsync(subscriptionId, cancellationToken);
        if (!string.IsNullOrEmpty(policyCompliance) && policyCompliance.Contains("NonCompliant", StringComparison.OrdinalIgnoreCase))
        {
            findings.Add(new ComplianceFinding
            {
                ControlId = "CA-5",
                ControlFamily = ControlFamilies.AssessmentAuthorization,
                Title = "Non-compliant security policies detected",
                Description = "Azure Policy compliance summary reports non-compliant policies. " +
                              "Per CA-5 (Plan of Action and Milestones), policy violations must be tracked.",
                Severity = FindingSeverity.Medium,
                Status = FindingStatus.Open,
                ResourceId = $"/subscriptions/{subscriptionId}",
                ResourceType = "Microsoft.Authorization/policyStates",
                RemediationGuidance = "Create POA&M entries for non-compliant policy findings.",
                ScanSource = ScanSourceType.Policy,
                AutoRemediable = true,
                RemediationType = RemediationType.PolicyRemediation
            });
        }

        return findings;
    }
}
