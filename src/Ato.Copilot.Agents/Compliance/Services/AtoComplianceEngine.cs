using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Ato.Copilot.Core.Constants;
using Ato.Copilot.Core.Data.Context;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Agents.Compliance.Services;

/// <summary>
/// Core compliance scanning engine that orchestrates resource (via Resource Graph/Policy),
/// policy (via Azure Policy Insights), and Defender for Cloud scans.
/// Merges and correlates findings, computes compliance scores, and persists
/// assessments via EF Core.
/// </summary>
public class AtoComplianceEngine : IAtoComplianceEngine
{
    private readonly INistControlsService _nistService;
    private readonly IAzurePolicyComplianceService _policyService;
    private readonly IDefenderForCloudService _defenderService;
    private readonly IDbContextFactory<AtoCopilotContext> _dbFactory;
    private readonly ILogger<AtoComplianceEngine> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="AtoComplianceEngine"/> class.
    /// </summary>
    public AtoComplianceEngine(
        INistControlsService nistService,
        IAzurePolicyComplianceService policyService,
        IDefenderForCloudService defenderService,
        IDbContextFactory<AtoCopilotContext> dbFactory,
        ILogger<AtoComplianceEngine> logger)
    {
        _nistService = nistService;
        _policyService = policyService;
        _defenderService = defenderService;
        _dbFactory = dbFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ComplianceAssessment> RunAssessmentAsync(
        string subscriptionId,
        string? framework = null,
        string? controlFamilies = null,
        string? resourceTypes = null,
        string? scanType = null,
        bool includePassed = false,
        CancellationToken cancellationToken = default)
    {
        var assessment = new ComplianceAssessment
        {
            SubscriptionId = subscriptionId,
            Framework = ComplianceFrameworks.Normalize(framework ?? "NIST800-53") ?? "NIST80053",
            ScanType = NormalizeScanType(scanType),
            Status = AssessmentStatus.InProgress,
            InitiatedBy = "system",
            AssessedAt = DateTime.UtcNow 
        };

        _logger.LogInformation(
            "Starting compliance assessment {Id} | Sub: {Sub} | Framework: {Fw} | Type: {Type}",
            assessment.Id, subscriptionId, assessment.Framework, assessment.ScanType);

        try
        {
            // Parse control family filter
            var familyFilter = ParseControlFamilies(controlFamilies);

            // Run scans based on scan type
            assessment.ProgressMessage = "Running scans...";

            var policySummary = new ScanSummary();
            var resourceSummary = new ScanSummary();

            if (assessment.ScanType is "policy" or "combined")
            {
                assessment.ProgressMessage = "Running policy compliance scan...";
                var policyFindings = await RunPolicyScanAsync(
                    subscriptionId, familyFilter, policySummary, cancellationToken);
                assessment.Findings.AddRange(policyFindings);
            }

            if (assessment.ScanType is "resource" or "combined")
            {
                assessment.ProgressMessage = "Running Defender for Cloud scan...";
                var defenderFindings = await RunDefenderScanAsync(
                    subscriptionId, familyFilter, resourceSummary, cancellationToken);
                assessment.Findings.AddRange(defenderFindings);
            }

            // Correlate findings from multiple sources
            if (assessment.ScanType == "combined")
            {
                assessment.ProgressMessage = "Correlating findings...";
                CorrelateFindings(assessment.Findings);
            }

            // Filter by resource types if specified
            if (!string.IsNullOrWhiteSpace(resourceTypes))
            {
                var types = resourceTypes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                assessment.Findings = assessment.Findings
                    .Where(f => string.IsNullOrEmpty(f.ResourceType) ||
                                types.Any(t => f.ResourceType.Contains(t, StringComparison.OrdinalIgnoreCase)))
                    .ToList();
            }

            // Remove passed findings unless requested
            if (!includePassed)
            {
                assessment.Findings = assessment.Findings
                    .Where(f => f.Status != FindingStatus.Remediated && f.Status != FindingStatus.FalsePositive)
                    .ToList();
            }

            // Set assessment ID on all findings
            foreach (var finding in assessment.Findings)
            {
                finding.AssessmentId = assessment.Id;
            }

            // Compute compliance score using NIST catalog
            await ComputeComplianceScoreAsync(assessment, familyFilter, cancellationToken);

            // Set scan summaries
            assessment.ResourceScanSummary = resourceSummary;
            assessment.PolicyScanSummary = policySummary;

            // Mark complete
            assessment.Status = AssessmentStatus.Completed;
            assessment.CompletedAt = DateTime.UtcNow;
            assessment.ProgressMessage = "Assessment completed";

            // Persist
            await SaveAssessmentAsync(assessment, cancellationToken);

            _logger.LogInformation(
                "Assessment {Id} completed | Score: {Score:F1}% | Findings: {Count}",
                assessment.Id, assessment.ComplianceScore, assessment.Findings.Count);
        }
        catch (OperationCanceledException)
        {
            assessment.Status = AssessmentStatus.Cancelled;
            assessment.ProgressMessage = "Assessment cancelled";
            _logger.LogWarning("Assessment {Id} was cancelled", assessment.Id);
            throw;
        }
        catch (Exception ex)
        {
            assessment.Status = AssessmentStatus.Failed;
            assessment.ProgressMessage = $"Assessment failed: {ex.Message}";
            _logger.LogError(ex, "Assessment {Id} failed", assessment.Id);

            // Still persist the failed assessment for audit trail
            try { await SaveAssessmentAsync(assessment, cancellationToken); }
            catch (Exception saveEx) { _logger.LogError(saveEx, "Failed to save failed assessment {Id}", assessment.Id); }

            throw;
        }

        return assessment;
    }

    /// <inheritdoc />
    public async Task<List<ComplianceAssessment>> GetAssessmentHistoryAsync(
        string subscriptionId,
        int days = 30,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var cutoff = DateTime.UtcNow.AddDays(-days);

        return await db.Assessments
            .Where(a => a.SubscriptionId == subscriptionId && a.AssessedAt >= cutoff)
            .OrderByDescending(a => a.AssessedAt)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ComplianceFinding?> GetFindingAsync(
        string findingId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Findings.FindAsync(new object[] { findingId }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task SaveAssessmentAsync(
        ComplianceAssessment assessment,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var existing = await db.Assessments
            .AsNoTracking()
            .AnyAsync(a => a.Id == assessment.Id, cancellationToken);

        if (existing)
        {
            db.Assessments.Update(assessment);
        }
        else
        {
            db.Assessments.Add(assessment);
        }

        await db.SaveChangesAsync(cancellationToken);
        _logger.LogDebug("Saved assessment {Id}", assessment.Id);
    }

    // ─── Private Methods ────────────────────────────────────────────────────────

    /// <summary>
    /// Runs a policy compliance scan and converts results to ComplianceFindings.
    /// </summary>
    private async Task<List<ComplianceFinding>> RunPolicyScanAsync(
        string subscriptionId,
        HashSet<string>? familyFilter,
        ScanSummary summary,
        CancellationToken cancellationToken)
    {
        var findings = new List<ComplianceFinding>();

        try
        {
            var policyJson = await _policyService.GetPolicyStatesAsync(subscriptionId, null, cancellationToken);
            using var doc = JsonDocument.Parse(policyJson);
            var root = doc.RootElement;

            // Check for error response
            if (root.TryGetProperty("error", out _))
            {
                _logger.LogWarning("Policy scan returned error for {Sub}", subscriptionId);
                return findings;
            }

            if (!root.TryGetProperty("states", out var statesArray))
                return findings;

            int compliant = 0, nonCompliant = 0;

            foreach (var state in statesArray.EnumerateArray())
            {
                cancellationToken.ThrowIfCancellationRequested();

                var complianceState = state.TryGetProperty("complianceState", out var csElem)
                    ? csElem.GetString() : null;

                // Map policy definition groups to NIST controls
                var groupNames = new List<string>();
                if (state.TryGetProperty("policyDefinitionGroupNames", out var groupsElem))
                {
                    foreach (var g in groupsElem.EnumerateArray())
                    {
                        var name = g.GetString();
                        if (!string.IsNullOrEmpty(name))
                            groupNames.Add(name);
                    }
                }

                var controlIds = AzurePolicyComplianceService.MapGroupsToNistControls(groupNames);

                if (string.Equals(complianceState, "compliant", StringComparison.OrdinalIgnoreCase))
                {
                    compliant++;
                    continue; // Skip compliant — we create findings for non-compliant only
                }

                nonCompliant++;

                // Create a finding for each mapped control
                foreach (var controlId in controlIds)
                {
                    var family = controlId.Split('-')[0].ToUpperInvariant();
                    if (familyFilter != null && !familyFilter.Contains(family))
                        continue;

                    var resourceId = state.TryGetProperty("resourceId", out var ridElem)
                        ? ridElem.GetString() ?? "" : "";
                    var resourceType = state.TryGetProperty("resourceType", out var rtElem)
                        ? rtElem.GetString() ?? "" : "";
                    var policyDefId = state.TryGetProperty("policyDefinitionId", out var pdElem)
                        ? pdElem.GetString() : null;
                    var policyAssignId = state.TryGetProperty("policyAssignmentId", out var paElem)
                        ? paElem.GetString() : null;

                    findings.Add(new ComplianceFinding
                    {
                        ControlId = controlId,
                        ControlFamily = family,
                        Title = $"Policy non-compliance: {controlId}",
                        Description = $"Azure Policy detected non-compliance for control {controlId}",
                        Severity = ClassifyPolicySeverity(controlId),
                        Status = FindingStatus.Open,
                        ResourceId = resourceId,
                        ResourceType = resourceType,
                        RemediationGuidance = $"Review Azure Policy assignment and remediate the non-compliant resource.",
                        Source = "PolicyInsights",
                        ScanSource = ScanSourceType.Policy,
                        PolicyDefinitionId = policyDefId,
                        PolicyAssignmentId = policyAssignId,
                        RemediationType = RemediationType.PolicyRemediation,
                        RiskLevel = IsHighRiskFamily(family) ? RiskLevel.High : RiskLevel.Standard
                    });
                }
            }

            summary.Compliant = compliant;
            summary.NonCompliant = nonCompliant;
            summary.PoliciesEvaluated = compliant + nonCompliant;
            summary.CompliancePercentage = summary.PoliciesEvaluated > 0
                ? Math.Round((double)compliant / summary.PoliciesEvaluated * 100, 2)
                : 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Policy scan failed for {Sub}", subscriptionId);
        }

        return findings;
    }

    /// <summary>
    /// Runs a Defender for Cloud scan and converts recommendations to ComplianceFindings.
    /// </summary>
    private async Task<List<ComplianceFinding>> RunDefenderScanAsync(
        string subscriptionId,
        HashSet<string>? familyFilter,
        ScanSummary summary,
        CancellationToken cancellationToken)
    {
        var findings = new List<ComplianceFinding>();

        try
        {
            var recommendationsJson = await _defenderService.GetRecommendationsAsync(subscriptionId, cancellationToken);
            using var doc = JsonDocument.Parse(recommendationsJson);
            var root = doc.RootElement;

            if (root.TryGetProperty("error", out _))
            {
                _logger.LogWarning("Defender scan returned error for {Sub}", subscriptionId);
                return findings;
            }

            if (!root.TryGetProperty("recommendations", out var recsArray))
                return findings;

            int scanned = 0, compliant = 0, nonCompliant = 0;

            foreach (var rec in recsArray.EnumerateArray())
            {
                cancellationToken.ThrowIfCancellationRequested();
                scanned++;

                var displayName = rec.TryGetProperty("displayName", out var dnElem)
                    ? dnElem.GetString() ?? "" : "";
                var status = rec.TryGetProperty("status", out var sElem)
                    ? sElem.GetString() : null;
                var recId = rec.TryGetProperty("id", out var idElem)
                    ? idElem.GetString() : null;

                if (string.Equals(status, "Healthy", StringComparison.OrdinalIgnoreCase))
                {
                    compliant++;
                    continue;
                }

                nonCompliant++;

                // Map recommendation to NIST controls
                var controlIds = DefenderForCloudService.MapRecommendationToNistControls(displayName);
                if (controlIds.Count == 0)
                    controlIds.Add("SI-4"); // Default to Information System Monitoring

                foreach (var controlId in controlIds)
                {
                    var family = controlId.Split('-')[0].ToUpperInvariant();
                    if (familyFilter != null && !familyFilter.Contains(family))
                        continue;

                    findings.Add(new ComplianceFinding
                    {
                        ControlId = controlId,
                        ControlFamily = family,
                        Title = displayName,
                        Description = $"Defender for Cloud recommends action: {displayName}",
                        Severity = ClassifyDefenderSeverity(status),
                        Status = FindingStatus.Open,
                        ResourceId = "",
                        ResourceType = "",
                        RemediationGuidance = $"Follow Defender for Cloud recommendation: {displayName}",
                        Source = "DefenderForCloud",
                        ScanSource = ScanSourceType.Defender,
                        DefenderRecommendationId = recId,
                        RemediationType = RemediationType.ResourceConfiguration,
                        RiskLevel = IsHighRiskFamily(family) ? RiskLevel.High : RiskLevel.Standard
                    });
                }
            }

            summary.ResourcesScanned = scanned;
            summary.Compliant = compliant;
            summary.NonCompliant = nonCompliant;
            summary.CompliancePercentage = scanned > 0
                ? Math.Round((double)compliant / scanned * 100, 2)
                : 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Defender scan failed for {Sub}", subscriptionId);
        }

        return findings;
    }

    /// <summary>
    /// Correlates findings from multiple scan sources.
    /// Deduplicates by (controlId + resourceId), keeps the higher-severity finding.
    /// </summary>
    private static void CorrelateFindings(List<ComplianceFinding> findings)
    {
        var grouped = findings
            .GroupBy(f => new { f.ControlId, f.ResourceId })
            .ToList();

        findings.Clear();

        foreach (var group in grouped)
        {
            if (group.Count() == 1)
            {
                findings.Add(group.First());
                continue;
            }

            // Keep the finding with highest severity; mark as Combined source
            var primary = group.OrderBy(f => f.Severity).First(); // Critical=0, High=1, ...
            primary.ScanSource = ScanSourceType.Combined;
            primary.Source = string.Join("+", group.Select(f => f.Source).Distinct());
            findings.Add(primary);
        }
    }

    /// <summary>
    /// Computes overall compliance score using NIST catalog as baseline.
    /// Score = (passed / total) * 100, where controls with no findings count as passed.
    /// </summary>
    private async Task ComputeComplianceScoreAsync(
        ComplianceAssessment assessment,
        HashSet<string>? familyFilter,
        CancellationToken cancellationToken)
    {
        // Get all control families to assess
        var families = familyFilter ?? ControlFamilies.AllFamilies;

        int totalControls = 0, passedControls = 0, failedControls = 0;
        var failedControlIds = assessment.Findings
            .Select(f => f.ControlId.ToUpperInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var family in families)
        {
            try
            {
                var controls = await _nistService.GetControlFamilyAsync(family, false, cancellationToken);
                foreach (var control in controls)
                {
                    totalControls++;
                    if (failedControlIds.Contains(control.Id.ToUpperInvariant()))
                        failedControls++;
                    else
                        passedControls++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get control family {Family} for scoring", family);
            }
        }

        assessment.TotalControls = totalControls;
        assessment.PassedControls = passedControls;
        assessment.FailedControls = failedControls;
        assessment.NotAssessedControls = totalControls - passedControls - failedControls;
        assessment.ComplianceScore = totalControls > 0
            ? Math.Round((double)passedControls / totalControls * 100, 1)
            : 0;
    }

    // ─── Helper Methods ──────────────────────────────────────────────────────────

    /// <summary>Parses a comma-separated list of control families into a normalized hash set.</summary>
    private static HashSet<string>? ParseControlFamilies(string? controlFamilies)
    {
        if (string.IsNullOrWhiteSpace(controlFamilies))
            return null;

        return controlFamilies
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(f => f.ToUpperInvariant())
            .Where(f => ControlFamilies.IsValidFamily(f))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Normalizes the scan type string to a known value (resource, policy, combined).</summary>
    private static string NormalizeScanType(string? scanType) =>
        scanType?.ToLowerInvariant() switch
        {
            "resource" => "resource",
            "policy" => "policy",
            "combined" => "combined",
            "quick" => "combined",
            "full" => "combined",
            _ => "combined"
        };

    /// <summary>Classifies the severity of a policy finding based on its control family.</summary>
    private static FindingSeverity ClassifyPolicySeverity(string controlId)
    {
        var family = controlId.Split('-')[0].ToUpperInvariant();
        return family switch
        {
            "AC" or "IA" or "SC" => FindingSeverity.High,
            "AU" or "SI" or "CM" => FindingSeverity.Medium,
            _ => FindingSeverity.Low
        };
    }

    /// <summary>Classifies the severity of a Defender finding based on its assessment status.</summary>
    private static FindingSeverity ClassifyDefenderSeverity(string? status) =>
        status?.ToLowerInvariant() switch
        {
            "unhealthy" => FindingSeverity.High,
            "notapplicable" => FindingSeverity.Low,
            _ => FindingSeverity.Medium
        };

    /// <summary>Returns true if the control family is high-risk (AC, IA, SC).</summary>
    private static bool IsHighRiskFamily(string family) =>
        family is "AC" or "IA" or "SC";
}
