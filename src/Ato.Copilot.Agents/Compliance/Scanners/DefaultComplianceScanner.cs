using Microsoft.Extensions.Logging;
using Ato.Copilot.Core.Constants;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Agents.Compliance.Scanners;

/// <summary>
/// Default fallback scanner for families without a specialized scanner.
/// Handles: AT, MA, MP, PE, PL, PM, PS, PT, SA, SR via policy-based scanning.
/// Uses <see cref="IAzurePolicyComplianceService"/> to check policy states
/// mapped to the family's NIST controls.
/// </summary>
public class DefaultComplianceScanner : BaseComplianceScanner
{
    private readonly IAzurePolicyComplianceService _policyService;
    private readonly IDefenderForCloudService _defenderService;

    /// <summary>
    /// Family code "DEFAULT" signals the <see cref="ScannerRegistry"/>
    /// to use this scanner as the fallback.
    /// </summary>
    public override string FamilyCode => "DEFAULT";

    public DefaultComplianceScanner(
        IAzurePolicyComplianceService policyService,
        IDefenderForCloudService defenderService,
        ILogger<DefaultComplianceScanner> logger) : base(logger)
    {
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

        // Determine the family code from the controls
        var familyCode = controls.FirstOrDefault()?.Family?.ToUpperInvariant() ?? "UNKNOWN";

        Logger.LogInformation("Default scanner handling family {Family} with {Count} controls via policy-based scanning",
            familyCode, controls.Count);

        // Check policy compliance for mapped policy definitions
        foreach (var control in controls)
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var policyDefId in control.AzurePolicyDefinitionIds)
            {
                if (string.IsNullOrEmpty(policyDefId)) continue;

                try
                {
                    var policyState = await _policyService.GetPolicyStatesAsync(
                        subscriptionId, policyDefId, cancellationToken);

                    if (!string.IsNullOrEmpty(policyState) &&
                        policyState.Contains("NonCompliant", StringComparison.OrdinalIgnoreCase))
                    {
                        findings.Add(new ComplianceFinding
                        {
                            ControlId = control.Id.ToUpperInvariant(),
                            ControlFamily = familyCode,
                            ControlTitle = control.Title,
                            ControlDescription = control.Description,
                            Title = $"Policy non-compliance for {control.Id.ToUpperInvariant()} ({control.Title})",
                            Description = $"Azure Policy definition {policyDefId} reports non-compliant resources " +
                                          $"for control {control.Id.ToUpperInvariant()} — {control.Title}.",
                            Severity = FindingSeverity.Medium,
                            Status = FindingStatus.Open,
                            ResourceId = $"/subscriptions/{subscriptionId}",
                            ResourceType = "Microsoft.Authorization/policyStates",
                            RemediationGuidance = $"Remediate non-compliant resources for policy definition: {policyDefId}",
                            ScanSource = ScanSourceType.Policy,
                            PolicyDefinitionId = policyDefId,
                            AutoRemediable = true,
                            RemediationType = RemediationType.PolicyRemediation
                        });
                    }
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    Logger.LogDebug(ex, "Failed to check policy state for {PolicyDef} (control {Control})",
                        policyDefId, control.Id);
                }
            }
        }

        // If no policy mappings produced findings, check Defender general recommendations
        if (findings.Count == 0)
        {
            var defenderAssessments = await _defenderService.GetAssessmentsAsync(subscriptionId, cancellationToken);
            if (!string.IsNullOrEmpty(defenderAssessments) &&
                defenderAssessments.Contains("Unhealthy", StringComparison.OrdinalIgnoreCase))
            {
                findings.Add(new ComplianceFinding
                {
                    ControlId = controls.FirstOrDefault()?.Id.ToUpperInvariant() ?? familyCode,
                    ControlFamily = familyCode,
                    Title = $"Defender assessments pending for {familyCode} family",
                    Description = $"Microsoft Defender reports unhealthy assessments relevant to the " +
                                  $"{ControlFamilies.FamilyNames.GetValueOrDefault(familyCode, familyCode)} family.",
                    Severity = FindingSeverity.Medium,
                    Status = FindingStatus.Open,
                    ResourceId = $"/subscriptions/{subscriptionId}",
                    ResourceType = "Microsoft.Security/assessments",
                    RemediationGuidance = "Review Defender for Cloud assessments.",
                    ScanSource = ScanSourceType.Defender,
                    AutoRemediable = false
                });
            }
        }

        return findings;
    }
}
