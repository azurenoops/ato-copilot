using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ato.Copilot.Core.Configuration;
using Ato.Copilot.Core.Data.Context;

namespace Ato.Copilot.Agents.Compliance.Services;

/// <summary>
/// Background service that periodically cleans up expired assessment records
/// based on <see cref="RetentionPolicyOptions"/> configuration.
/// Assessment data is deleted after <see cref="RetentionPolicyOptions.AssessmentRetentionDays"/> days (default 1095 = 3 years per FR-042).
/// Audit log entries are NEVER deleted — they are immutable and retained for 7+ years per FR-043.
/// Creates a scoped DI per tick to avoid DbContext lifetime issues.
/// </summary>
public class RetentionCleanupHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RetentionCleanupHostedService> _logger;
    private readonly RetentionPolicyOptions _options;
    private readonly TimeSpan _interval;

    /// <summary>
    /// Initializes a new instance of <see cref="RetentionCleanupHostedService"/>.
    /// </summary>
    /// <param name="scopeFactory">Factory for creating DI scopes per cleanup tick.</param>
    /// <param name="options">Retention policy configuration.</param>
    /// <param name="logger">Logger instance.</param>
    public RetentionCleanupHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<RetentionPolicyOptions> options,
        ILogger<RetentionCleanupHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;
        _interval = TimeSpan.FromHours(Math.Max(1, _options.CleanupIntervalHours));
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "RetentionCleanupHostedService started — interval: {IntervalHours}h, assessment retention: {RetentionDays} days, automatic cleanup: {Enabled}",
            _options.CleanupIntervalHours,
            _options.AssessmentRetentionDays,
            _options.EnableAutomaticCleanup);

        if (!_options.EnableAutomaticCleanup)
        {
            _logger.LogInformation("Automatic cleanup is disabled — RetentionCleanupHostedService will not run");
            return;
        }

        using var timer = new PeriodicTimer(_interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await CleanupExpiredAssessmentsAsync(stoppingToken);
                await CleanupExpiredSnapshotsAsync(stoppingToken);
                await CleanupExpiredAlertsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during retention cleanup");
            }
        }

        _logger.LogInformation("RetentionCleanupHostedService stopped");
    }

    /// <summary>
    /// Deletes assessment records that have exceeded the configured retention period.
    /// Audit log entries are NEVER deleted (immutable per FR-043).
    /// </summary>
    private async Task CleanupExpiredAssessmentsAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AtoCopilotContext>();
        var cutoffDate = DateTime.UtcNow.AddDays(-_options.AssessmentRetentionDays);

        var expiredAssessments = await db.Assessments
            .Where(a => a.AssessedAt < cutoffDate)
            .ToListAsync(cancellationToken);

        if (expiredAssessments.Count == 0)
        {
            _logger.LogDebug("Retention cleanup: no expired assessments found (cutoff: {CutoffDate:yyyy-MM-dd})", cutoffDate);
            return;
        }

        db.Assessments.RemoveRange(expiredAssessments);
        await db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Retention cleanup completed — deleted {Count} assessment records older than {CutoffDate:yyyy-MM-dd} ({RetentionDays} days). Audit logs: untouched (immutable).",
            expiredAssessments.Count,
            cutoffDate,
            _options.AssessmentRetentionDays);
    }

    /// <summary>
    /// Deletes daily snapshots older than <see cref="RetentionPolicyOptions.DailySnapshotRetentionDays"/> (default 90 days)
    /// and weekly snapshots older than <see cref="RetentionPolicyOptions.WeeklySnapshotRetentionDays"/> (default 730 days / 2 years).
    /// </summary>
    private async Task CleanupExpiredSnapshotsAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AtoCopilotContext>();

        // Delete daily snapshots older than configured retention
        var dailyCutoff = DateTimeOffset.UtcNow.AddDays(-_options.DailySnapshotRetentionDays);
        var expiredDaily = await db.ComplianceSnapshots
            .Where(s => !s.IsWeeklySnapshot && s.CapturedAt < dailyCutoff)
            .ToListAsync(cancellationToken);

        // Delete weekly snapshots older than configured retention
        var weeklyCutoff = DateTimeOffset.UtcNow.AddDays(-_options.WeeklySnapshotRetentionDays);
        var expiredWeekly = await db.ComplianceSnapshots
            .Where(s => s.IsWeeklySnapshot && s.CapturedAt < weeklyCutoff)
            .ToListAsync(cancellationToken);

        var totalExpired = expiredDaily.Count + expiredWeekly.Count;
        if (totalExpired == 0)
        {
            _logger.LogDebug("Retention cleanup: no expired snapshots found");
            return;
        }

        db.ComplianceSnapshots.RemoveRange(expiredDaily);
        db.ComplianceSnapshots.RemoveRange(expiredWeekly);
        await db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Snapshot retention cleanup — deleted {DailyCount} daily snapshots (>{DailyDays}d) and {WeeklyCount} weekly snapshots (>{WeeklyDays}d)",
            expiredDaily.Count, _options.DailySnapshotRetentionDays,
            expiredWeekly.Count, _options.WeeklySnapshotRetentionDays);
    }

    /// <summary>
    /// Deletes compliance alerts older than <see cref="RetentionPolicyOptions.AlertRetentionDays"/> (default 730 days / 2 years).
    /// Only deletes alerts in terminal states (Resolved or Dismissed).
    /// </summary>
    private async Task CleanupExpiredAlertsAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AtoCopilotContext>();
        var cutoff = DateTimeOffset.UtcNow.AddDays(-_options.AlertRetentionDays);

        var expiredAlerts = await db.ComplianceAlerts
            .Where(a => a.CreatedAt < cutoff
                && (a.Status == Core.Models.Compliance.AlertStatus.Resolved
                    || a.Status == Core.Models.Compliance.AlertStatus.Dismissed))
            .ToListAsync(cancellationToken);

        if (expiredAlerts.Count == 0)
        {
            _logger.LogDebug("Retention cleanup: no expired alerts found (cutoff: {CutoffDate:yyyy-MM-dd})", cutoff);
            return;
        }

        db.ComplianceAlerts.RemoveRange(expiredAlerts);
        await db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Alert retention cleanup — deleted {Count} resolved/dismissed alerts older than {RetentionDays} days",
            expiredAlerts.Count, _options.AlertRetentionDays);
    }
}
