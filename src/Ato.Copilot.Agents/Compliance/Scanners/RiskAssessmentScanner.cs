using Microsoft.Extensions.Logging;
using Ato.Copilot.Core.Constants;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Agents.Compliance.Scanners;

/// <summary>
/// Scanner for the Risk Assessment (RA) family.
/// Delegates to Defender for Cloud assessments, vulnerability scans, and secure score.
/// </summary>
public class RiskAssessmentScanner : BaseComplianceScanner
{
    private readonly IDefenderForCloudService _defenderService;
    private readonly IAzurePolicyComplianceService _policyService;

    /// <inheritdoc />
    public override string FamilyCode => ControlFamilies.RiskAssessment;

    public RiskAssessmentScanner(
        IDefenderForCloudService defenderService,
        IAzurePolicyComplianceService policyService,
        ILogger<RiskAssessmentScanner> logger) : base(logger)
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

        // Get Defender assessments for vulnerability and risk
        var assessments = await _defenderService.GetAssessmentsAsync(subscriptionId, cancellationToken);
        if (!string.IsNullOrEmpty(assessments) && assessments.Contains("Unhealthy", StringComparison.OrdinalIgnoreCase))
        {
            findings.Add(new ComplianceFinding
            {
                ControlId = "RA-5",
                ControlFamily = ControlFamilies.RiskAssessment,
                Title = "Unhealthy vulnerability assessments detected",
                Description = "Defender for Cloud reports unhealthy resource assessments. " +
                              "Per RA-5 (Vulnerability Monitoring and Scanning), all resources must be scanned.",
                Severity = FindingSeverity.High,
                Status = FindingStatus.Open,
                ResourceId = $"/subscriptions/{subscriptionId}",
                ResourceType = "Microsoft.Security/assessments",
                RemediationGuidance = "Review and remediate Defender for Cloud vulnerability assessments.",
                ScanSource = ScanSourceType.Defender,
                AutoRemediable = false
            });
        }

        // Check secure score
        var secureScore = await _defenderService.GetSecureScoreAsync(subscriptionId, cancellationToken);
        if (!string.IsNullOrEmpty(secureScore))
        {
            // Try to parse score from response
            if (TryExtractScore(secureScore, out var scoreValue) && scoreValue < 70)
            {
                findings.Add(new ComplianceFinding
                {
                    ControlId = "RA-3",
                    ControlFamily = ControlFamilies.RiskAssessment,
                    Title = "Low Defender secure score",
                    Description = $"Secure score is {scoreValue:F0}% (below 70% threshold). " +
                                  "Per RA-3 (Risk Assessment), the organization must maintain an acceptable risk posture.",
                    Severity = scoreValue < 50 ? FindingSeverity.Critical : FindingSeverity.High,
                    Status = FindingStatus.Open,
                    ResourceId = $"/subscriptions/{subscriptionId}",
                    ResourceType = "Microsoft.Security/secureScores",
                    RemediationGuidance = "Address Defender for Cloud recommendations to improve secure score.",
                    ScanSource = ScanSourceType.Defender,
                    AutoRemediable = false
                });
            }
        }

        // Check recommendations
        var recommendations = await _defenderService.GetRecommendationsAsync(subscriptionId, cancellationToken);
        if (!string.IsNullOrEmpty(recommendations) && recommendations.Contains("vulnerability", StringComparison.OrdinalIgnoreCase))
        {
            findings.Add(new ComplianceFinding
            {
                ControlId = "RA-5",
                ControlFamily = ControlFamilies.RiskAssessment,
                Title = "Pending vulnerability recommendations",
                Description = "Defender for Cloud has pending vulnerability-related recommendations.",
                Severity = FindingSeverity.Medium,
                Status = FindingStatus.Open,
                ResourceId = $"/subscriptions/{subscriptionId}",
                ResourceType = "Microsoft.Security/assessments",
                RemediationGuidance = "Review and address Defender for Cloud vulnerability recommendations.",
                ScanSource = ScanSourceType.Defender,
                AutoRemediable = false
            });
        }

        return findings;
    }

    /// <summary>
    /// Attempts to extract a numeric score from a Defender secure score response.
    /// </summary>
    private static bool TryExtractScore(string response, out double score)
    {
        score = 0;
        // Look for common patterns: "score": 75, "percentage": 75, etc.
        var patterns = new[] { "\"score\":", "\"percentage\":", "\"current\":" };
        foreach (var pattern in patterns)
        {
            var idx = response.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                var valueStart = idx + pattern.Length;
                while (valueStart < response.Length && char.IsWhiteSpace(response[valueStart]))
                    valueStart++;
                var valueEnd = response.IndexOfAny([',', '}', ' '], valueStart);
                if (valueEnd < 0) valueEnd = response.Length;
                var valueStr = response[valueStart..valueEnd].Trim().Trim('"');
                if (double.TryParse(valueStr, out score))
                    return true;
            }
        }
        return false;
    }
}
