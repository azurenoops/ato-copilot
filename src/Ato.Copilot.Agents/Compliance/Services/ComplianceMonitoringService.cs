using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Ato.Copilot.Core.Data.Context;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Agents.Compliance.Services;

/// <summary>
/// Implements compliance monitoring, history, audit, and status services.
/// Queries assessment/finding history from the database, computes trends,
/// detects drift, and provides real-time status summaries.
/// </summary>
public class ComplianceMonitoringService
    : IComplianceMonitoringService,
      IComplianceHistoryService,
      IAssessmentAuditService,
      IComplianceStatusService
{
    private readonly IDbContextFactory<AtoCopilotContext> _dbFactory;
    private readonly IAtoComplianceEngine _complianceEngine;
    private readonly ILogger<ComplianceMonitoringService> _logger;
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="ComplianceMonitoringService"/> class.
    /// </summary>
    public ComplianceMonitoringService(
        IDbContextFactory<AtoCopilotContext> dbFactory,
        IAtoComplianceEngine complianceEngine,
        ILogger<ComplianceMonitoringService> logger)
    {
        _dbFactory = dbFactory;
        _complianceEngine = complianceEngine;
        _logger = logger;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // IComplianceMonitoringService
    // ═══════════════════════════════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<string> GetStatusAsync(
        string? subscriptionId = null,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var query = db.Assessments.AsQueryable();
        if (!string.IsNullOrEmpty(subscriptionId))
            query = query.Where(a => a.SubscriptionId == subscriptionId);

        var latest = await query
            .OrderByDescending(a => a.AssessedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (latest == null)
        {
            return JsonSerializer.Serialize(new
            {
                status = "no_data",
                message = "No assessments found. Run a compliance assessment first."
            }, JsonOpts);
        }

        var totalFindings = await db.Findings
            .Where(f => f.AssessmentId == latest.Id)
            .CountAsync(cancellationToken);

        var openFindings = await db.Findings
            .Where(f => f.AssessmentId == latest.Id && f.Status == FindingStatus.Open)
            .CountAsync(cancellationToken);

        return JsonSerializer.Serialize(new
        {
            status = latest.ComplianceScore >= 90 ? "healthy" :
                     latest.ComplianceScore >= 70 ? "warning" : "critical",
            complianceScore = latest.ComplianceScore,
            lastAssessment = latest.AssessedAt,
            totalControls = latest.TotalControls,
            passedControls = latest.PassedControls,
            failedControls = latest.FailedControls,
            totalFindings,
            openFindings,
            framework = latest.Framework,
            scanType = latest.ScanType
        }, JsonOpts);
    }

    /// <inheritdoc />
    public async Task<string> TriggerScanAsync(
        string? subscriptionId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(subscriptionId))
        {
            return JsonSerializer.Serialize(new
            {
                status = "error",
                message = "Subscription ID is required to trigger a scan."
            }, JsonOpts);
        }

        _logger.LogInformation("Triggering compliance scan for {SubId}", subscriptionId);

        try
        {
            var assessment = await _complianceEngine.RunAssessmentAsync(
                subscriptionId, null, null, null, "combined", false, cancellationToken);

            return JsonSerializer.Serialize(new
            {
                status = "completed",
                assessmentId = assessment.Id,
                complianceScore = assessment.ComplianceScore,
                totalControls = assessment.TotalControls,
                failedControls = assessment.FailedControls,
                message = "Compliance scan completed successfully."
            }, JsonOpts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Compliance scan failed for {SubId}", subscriptionId);
            return JsonSerializer.Serialize(new
            {
                status = "error",
                message = $"Scan failed: {ex.Message}"
            }, JsonOpts);
        }
    }

    /// <inheritdoc />
    public async Task<string> GetAlertsAsync(
        string? subscriptionId = null,
        int days = 30,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var since = DateTime.UtcNow.AddDays(-days);

        var query = db.Findings.Where(f => f.DiscoveredAt >= since);

        if (!string.IsNullOrEmpty(subscriptionId))
        {
            var assessmentIds = await db.Assessments
                .Where(a => a.SubscriptionId == subscriptionId && a.AssessedAt >= since)
                .Select(a => a.Id)
                .ToListAsync(cancellationToken);
            query = query.Where(f => assessmentIds.Contains(f.AssessmentId));
        }

        var criticalFindings = await query
            .Where(f => f.Severity == FindingSeverity.Critical || f.Severity == FindingSeverity.High)
            .OrderByDescending(f => f.Severity)
            .ThenByDescending(f => f.DiscoveredAt)
            .Take(50)
            .ToListAsync(cancellationToken);

        var alerts = criticalFindings.Select(f => new
        {
            id = f.Id,
            controlId = f.ControlId,
            title = f.Title,
            severity = f.Severity.ToString(),
            status = f.Status.ToString(),
            resourceId = f.ResourceId,
            discoveredAt = f.DiscoveredAt
        }).ToList();

        return JsonSerializer.Serialize(new
        {
            alertCount = alerts.Count,
            period = $"{days} days",
            alerts
        }, JsonOpts);
    }

    /// <inheritdoc />
    public async Task<string> GetTrendAsync(
        string? subscriptionId = null,
        int days = 30,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var since = DateTime.UtcNow.AddDays(-days);

        var query = db.Assessments.Where(a => a.AssessedAt >= since);
        if (!string.IsNullOrEmpty(subscriptionId))
            query = query.Where(a => a.SubscriptionId == subscriptionId);

        var assessments = await query
            .OrderBy(a => a.AssessedAt)
            .Select(a => new
            {
                a.Id,
                date = a.AssessedAt,
                score = a.ComplianceScore,
                a.TotalControls,
                a.PassedControls,
                a.FailedControls,
                a.ScanType
            })
            .ToListAsync(cancellationToken);

        if (assessments.Count == 0)
        {
            return JsonSerializer.Serialize(new
            {
                status = "no_data",
                message = $"No assessments found in the last {days} days."
            }, JsonOpts);
        }

        var first = assessments.First();
        var last = assessments.Last();
        var scoreDelta = last.score - first.score;

        // Drift detection between last two assessments
        string? driftSummary = null;
        if (assessments.Count >= 2)
        {
            var prev = assessments[^2];
            var cur = assessments[^1];
            var newFindings = cur.FailedControls - prev.FailedControls;
            driftSummary = newFindings > 0
                ? $"{newFindings} new failing controls since previous assessment"
                : newFindings < 0
                    ? $"{-newFindings} controls remediated since previous assessment"
                    : "No drift detected since previous assessment";
        }

        return JsonSerializer.Serialize(new
        {
            period = $"{days} days",
            dataPoints = assessments.Count,
            trend = scoreDelta > 0 ? "improving" : scoreDelta < 0 ? "declining" : "stable",
            scoreDelta = Math.Round(scoreDelta, 1),
            currentScore = last.score,
            assessments = assessments.Select(a => new
            {
                a.date,
                a.score,
                a.TotalControls,
                a.FailedControls
            }),
            driftSummary
        }, JsonOpts);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // IComplianceHistoryService
    // ═══════════════════════════════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<string> GetHistoryAsync(
        string? subscriptionId = null,
        int days = 30,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var since = DateTime.UtcNow.AddDays(-days);

        var query = db.Assessments.Where(a => a.AssessedAt >= since);
        if (!string.IsNullOrEmpty(subscriptionId))
            query = query.Where(a => a.SubscriptionId == subscriptionId);

        var assessments = await query
            .OrderByDescending(a => a.AssessedAt)
            .ToListAsync(cancellationToken);

        var history = assessments.Select(a => new
        {
            id = a.Id,
            date = a.AssessedAt,
            score = a.ComplianceScore,
            framework = a.Framework,
            status = a.Status.ToString(),
            totalControls = a.TotalControls,
            passedControls = a.PassedControls,
            failedControls = a.FailedControls,
            scanType = a.ScanType
        }).ToList();

        return JsonSerializer.Serialize(new
        {
            period = $"{days} days",
            count = history.Count,
            history
        }, JsonOpts);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // IAssessmentAuditService
    // ═══════════════════════════════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<string> GetAuditLogAsync(
        string? subscriptionId = null,
        int days = 7,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var since = DateTime.UtcNow.AddDays(-days);

        var query = db.AuditLogs.Where(l => l.Timestamp >= since);
        if (!string.IsNullOrEmpty(subscriptionId))
            query = query.Where(l => l.SubscriptionId == subscriptionId);

        var entries = await query
            .OrderByDescending(l => l.Timestamp)
            .Take(100)
            .ToListAsync(cancellationToken);

        var log = entries.Select(e => new
        {
            id = e.Id,
            userId = e.UserId,
            userRole = e.UserRole,
            action = e.Action,
            timestamp = e.Timestamp,
            outcome = e.Outcome.ToString(),
            details = e.Details,
            duration = e.Duration?.ToString()
        }).ToList();

        return JsonSerializer.Serialize(new
        {
            period = $"{days} days",
            count = log.Count,
            entries = log
        }, JsonOpts);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // IComplianceStatusService
    // ═══════════════════════════════════════════════════════════════════════

    /// <inheritdoc />
    async Task<string> IComplianceStatusService.GetStatusAsync(
        string? subscriptionId,
        string? framework,
        CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var query = db.Assessments.AsQueryable();
        if (!string.IsNullOrEmpty(subscriptionId))
            query = query.Where(a => a.SubscriptionId == subscriptionId);
        if (!string.IsNullOrEmpty(framework))
            query = query.Where(a => a.Framework == framework);

        var latest = await query
            .OrderByDescending(a => a.AssessedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (latest == null)
        {
            return JsonSerializer.Serialize(new
            {
                status = "unknown",
                message = "No assessment data found."
            }, JsonOpts);
        }

        // Get finding breakdown by family
        var findings = await db.Findings
            .Where(f => f.AssessmentId == latest.Id)
            .ToListAsync(cancellationToken);

        var familyBreakdown = findings
            .GroupBy(f => f.ControlFamily)
            .Select(g => new
            {
                family = g.Key,
                total = g.Count(),
                critical = g.Count(f => f.Severity == FindingSeverity.Critical),
                high = g.Count(f => f.Severity == FindingSeverity.High),
                medium = g.Count(f => f.Severity == FindingSeverity.Medium),
                low = g.Count(f => f.Severity == FindingSeverity.Low),
                open = g.Count(f => f.Status == FindingStatus.Open),
                remediated = g.Count(f => f.Status == FindingStatus.Remediated)
            })
            .OrderByDescending(x => x.critical)
            .ThenByDescending(x => x.high)
            .ToList();

        return JsonSerializer.Serialize(new
        {
            status = latest.ComplianceScore >= 90 ? "compliant" :
                     latest.ComplianceScore >= 70 ? "partially_compliant" : "non_compliant",
            complianceScore = latest.ComplianceScore,
            framework = latest.Framework,
            assessedAt = latest.AssessedAt,
            totalControls = latest.TotalControls,
            passedControls = latest.PassedControls,
            failedControls = latest.FailedControls,
            totalFindings = findings.Count,
            openFindings = findings.Count(f => f.Status == FindingStatus.Open),
            remediatedFindings = findings.Count(f => f.Status == FindingStatus.Remediated),
            familyBreakdown
        }, JsonOpts);
    }
}
