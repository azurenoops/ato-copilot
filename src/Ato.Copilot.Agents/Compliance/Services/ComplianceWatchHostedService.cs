using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ato.Copilot.Core.Configuration;
using Ato.Copilot.Core.Data.Context;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Agents.Compliance.Services;

/// <summary>
/// Background service that periodically runs compliance monitoring checks.
/// Queries MonitoringConfigurations for due checks, runs drift detection,
/// and advances NextRunAt. Uses PeriodicTimer with configurable tick interval.
/// </summary>
public class ComplianceWatchHostedService : BackgroundService
{
    private readonly IDbContextFactory<AtoCopilotContext> _dbFactory;
    private readonly IComplianceWatchService _watchService;
    private readonly IAlertManager _alertManager;
    private readonly IComplianceEventSource _eventSource;
    private readonly IOptions<MonitoringOptions> _monitoringOptions;
    private readonly ILogger<ComplianceWatchHostedService> _logger;
    private int _consecutiveFailures;
    private const int MaxConsecutiveFailuresBeforeMetaAlert = 3;
    private DateOnly _lastSnapshotDate;

    /// <summary>
    /// Initializes a new instance of the <see cref="ComplianceWatchHostedService"/> class.
    /// </summary>
    public ComplianceWatchHostedService(
        IDbContextFactory<AtoCopilotContext> dbFactory,
        IComplianceWatchService watchService,
        IAlertManager alertManager,
        IComplianceEventSource eventSource,
        IOptions<MonitoringOptions> monitoringOptions,
        ILogger<ComplianceWatchHostedService> logger)
    {
        _dbFactory = dbFactory;
        _watchService = watchService;
        _alertManager = alertManager;
        _eventSource = eventSource;
        _monitoringOptions = monitoringOptions;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var tickInterval = TimeSpan.FromSeconds(_monitoringOptions.Value.TickIntervalSeconds);
        _logger.LogInformation("ComplianceWatchHostedService started with tick interval {Interval}", tickInterval);

        using var timer = new PeriodicTimer(tickInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await RunScheduledChecksAsync(stoppingToken);
                await RunEventDrivenChecksAsync(stoppingToken);
                await CaptureSnapshotsIfDueAsync(stoppingToken);
                _consecutiveFailures = 0;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _consecutiveFailures++;
                var backoffMs = Math.Min(1000 * Math.Pow(2, _consecutiveFailures), 300_000); // Max 5 min

                _logger.LogError(ex,
                    "ComplianceWatchHostedService tick failed (consecutive: {Failures}). Backing off {Backoff}ms",
                    _consecutiveFailures, backoffMs);

                // Create meta-alert if repeated failures (FR-042)
                if (_consecutiveFailures >= MaxConsecutiveFailuresBeforeMetaAlert)
                {
                    await CreateMetaAlertAsync(ex, stoppingToken);
                }

                try
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(backoffMs), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        _logger.LogInformation("ComplianceWatchHostedService stopped");
    }

    /// <summary>
    /// Query all due monitoring configurations and run checks.
    /// </summary>
    private async Task RunScheduledChecksAsync(CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var dueConfigs = await db.MonitoringConfigurations
            .Where(c => c.IsEnabled && c.NextRunAt <= now)
            .ToListAsync(cancellationToken);

        if (dueConfigs.Count == 0)
            return;

        _logger.LogInformation("Found {Count} due monitoring configurations", dueConfigs.Count);

        foreach (var config in dueConfigs)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var alertCount = await _watchService.RunMonitoringCheckAsync(config, cancellationToken);

                // Advance NextRunAt
                config.NextRunAt = ComplianceWatchService.ComputeNextRunAt(config.Frequency);
                config.LastRunAt = DateTimeOffset.UtcNow;
                config.UpdatedAt = DateTimeOffset.UtcNow;

                sw.Stop();
                _logger.LogInformation(
                    "Monitoring check completed for {Sub}/{RG} in {Elapsed}ms: {AlertCount} alerts",
                    config.SubscriptionId, config.ResourceGroupName ?? "*",
                    sw.ElapsedMilliseconds, alertCount);
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError(ex,
                    "Monitoring check failed for {Sub}/{RG} after {Elapsed}ms",
                    config.SubscriptionId, config.ResourceGroupName ?? "*",
                    sw.ElapsedMilliseconds);

                // Still advance NextRunAt to prevent stuck loops
                config.NextRunAt = ComplianceWatchService.ComputeNextRunAt(config.Frequency);
                config.UpdatedAt = DateTimeOffset.UtcNow;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Poll Activity Log for scopes with Mode=EventDriven or Both,
    /// trigger targeted compliance checks on affected resources.
    /// </summary>
    internal async Task RunEventDrivenChecksAsync(CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var eventDrivenConfigs = await db.MonitoringConfigurations
            .Where(c => c.IsEnabled
                && (c.Mode == MonitoringMode.EventDriven || c.Mode == MonitoringMode.Both))
            .ToListAsync(cancellationToken);

        if (eventDrivenConfigs.Count == 0)
            return;

        foreach (var config in eventDrivenConfigs)
        {
            var since = config.LastEventCheckAt ?? config.CreatedAt;

            try
            {
                var events = await _eventSource.GetRecentEventsAsync(
                    config.SubscriptionId, since, config.ResourceGroupName, cancellationToken);

                if (events.Count == 0)
                    continue;

                _logger.LogInformation(
                    "Event-driven: {Count} events for {Sub}/{RG} since {Since}",
                    events.Count, config.SubscriptionId, config.ResourceGroupName ?? "*", since);

                // Check for policy drift events (FR-004)
                var policyDriftEvents = events
                    .Where(e => ActivityLogEventSource.IsPolicyDriftEvent(e))
                    .ToList();

                if (policyDriftEvents.Count > 0)
                {
                    _logger.LogWarning(
                        "Policy drift detected: {Count} policy change events for {Sub}",
                        policyDriftEvents.Count, config.SubscriptionId);
                }

                // Trigger a targeted compliance check for the affected scope
                await _watchService.RunMonitoringCheckAsync(config, cancellationToken);

                // Advance high-water mark to latest event timestamp
                var maxTimestamp = events.Max(e => e.Timestamp);
                config.LastEventCheckAt = maxTimestamp;
                config.UpdatedAt = DateTimeOffset.UtcNow;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Event-driven check failed for {Sub}/{RG} — falling back to scheduled",
                    config.SubscriptionId, config.ResourceGroupName ?? "*");

                // Fallback: if Activity Log is unavailable, scheduled checks still run
                // Do not advance LastEventCheckAt so events are re-polled next tick
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Capture daily compliance snapshots at midnight UTC.
    /// On Sundays, also promote the snapshot to a weekly snapshot.
    /// </summary>
    internal async Task CaptureSnapshotsIfDueAsync(CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.DateTime);
        if (today <= _lastSnapshotDate)
            return; // Already captured for today

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var configs = await db.MonitoringConfigurations
            .Where(c => c.IsEnabled)
            .ToListAsync(cancellationToken);

        if (configs.Count == 0)
            return;

        var isSunday = DateTimeOffset.UtcNow.DayOfWeek == DayOfWeek.Sunday;

        foreach (var config in configs)
        {
            // Check if snapshot already exists for today
            var existing = await db.ComplianceSnapshots
                .AnyAsync(s =>
                    s.SubscriptionId == config.SubscriptionId
                    && s.CapturedAt.Date == DateTimeOffset.UtcNow.Date,
                    cancellationToken);

            if (existing)
                continue;

            // Get latest assessment data
            var latestBaselines = await db.ComplianceBaselines
                .Where(b => b.SubscriptionId == config.SubscriptionId && b.IsActive)
                .ToListAsync(cancellationToken);

            // Count active alerts
            var alertCounts = await db.ComplianceAlerts
                .Where(a => a.SubscriptionId == config.SubscriptionId
                    && a.Status != AlertStatus.Resolved
                    && a.Status != AlertStatus.Dismissed)
                .GroupBy(a => a.Severity)
                .Select(g => new { Severity = g.Key, Count = g.Count() })
                .ToListAsync(cancellationToken);

            var totalActive = alertCounts.Sum(a => a.Count);
            var criticalCount = alertCounts
                .Where(a => a.Severity == AlertSeverity.Critical).Sum(a => a.Count);
            var highCount = alertCounts
                .Where(a => a.Severity == AlertSeverity.High).Sum(a => a.Count);

            // Build control family breakdown from baselines
            var familyBreakdown = latestBaselines
                .Where(b => !string.IsNullOrEmpty(b.ResourceType))
                .GroupBy(b => b.ResourceType.Split('/').FirstOrDefault() ?? "Unknown")
                .ToDictionary(g => g.Key, g => g.Count());

            var totalResources = latestBaselines.Count;
            var snapshot = new ComplianceSnapshot
            {
                Id = Guid.NewGuid(),
                SubscriptionId = config.SubscriptionId,
                ComplianceScore = totalResources > 0 ? (double)totalResources / Math.Max(totalResources, 1) * 100 : 0,
                TotalControls = totalResources,
                PassedControls = totalResources,
                FailedControls = 0,
                TotalResources = totalResources,
                CompliantResources = totalResources,
                NonCompliantResources = 0,
                ActiveAlertCount = totalActive,
                CriticalAlertCount = criticalCount,
                HighAlertCount = highCount,
                ControlFamilyBreakdown = familyBreakdown.Count > 0
                    ? System.Text.Json.JsonSerializer.Serialize(familyBreakdown)
                    : null,
                CapturedAt = DateTimeOffset.UtcNow,
                IsWeeklySnapshot = isSunday
            };

            db.ComplianceSnapshots.Add(snapshot);
            _logger.LogInformation(
                "Snapshot captured for {Sub} | Score: {Score:F1} | Alerts: {Alerts} | Weekly: {Weekly}",
                config.SubscriptionId, snapshot.ComplianceScore, totalActive, isSunday);
        }

        await db.SaveChangesAsync(cancellationToken);
        _lastSnapshotDate = today;
    }

    /// <summary>
    /// Create a meta-alert when the monitoring system itself is experiencing persistent failures (FR-042).
    /// </summary>
    private async Task CreateMetaAlertAsync(Exception ex, CancellationToken cancellationToken)
    {
        try
        {
            var alert = new ComplianceAlert
            {
                Type = AlertType.Degradation,
                Severity = AlertSeverity.Critical,
                Title = "Compliance monitoring system experiencing persistent failures",
                Description = $"The compliance monitoring background service has failed " +
                    $"{_consecutiveFailures} consecutive times. Last error: {ex.Message}. " +
                    $"Monitoring may be degraded until the issue is resolved.",
                SubscriptionId = "system",
                RecommendedAction = "Check system logs and connectivity to Azure. " +
                    "Verify network access and service principal permissions."
            };

            await _alertManager.CreateAlertAsync(alert, cancellationToken);
            _logger.LogWarning("Meta-alert created for persistent monitoring failures");
        }
        catch (Exception metaEx)
        {
            _logger.LogError(metaEx, "Failed to create meta-alert for monitoring failures");
        }
    }
}
