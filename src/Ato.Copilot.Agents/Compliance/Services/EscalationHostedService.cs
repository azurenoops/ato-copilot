using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Ato.Copilot.Core.Data.Context;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Agents.Compliance.Services;

/// <summary>
/// Background service that periodically checks for alerts exceeding their SLA and
/// triggers escalation notifications via configured escalation paths.
/// Also implements IEscalationService for managing escalation path configuration.
/// </summary>
public class EscalationHostedService : BackgroundService, IEscalationService
{
    private readonly IDbContextFactory<AtoCopilotContext> _dbFactory;
    private readonly IAlertNotificationService _notificationService;
    private readonly IAlertManager _alertManager;
    private readonly ILogger<EscalationHostedService> _logger;
    private static readonly TimeSpan TickInterval = TimeSpan.FromMinutes(1);

    public EscalationHostedService(
        IDbContextFactory<AtoCopilotContext> dbFactory,
        IAlertNotificationService notificationService,
        IAlertManager alertManager,
        ILogger<EscalationHostedService> logger)
    {
        _dbFactory = dbFactory;
        _notificationService = notificationService;
        _alertManager = alertManager;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("EscalationHostedService started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckEscalationsAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error during escalation check");
            }

            await Task.Delay(TickInterval, stoppingToken);
        }
    }

    /// <inheritdoc />
    public async Task CheckEscalationsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        // Find alerts past SLA that are still New or Acknowledged
        var now = DateTimeOffset.UtcNow;
        var overdueAlerts = await db.ComplianceAlerts
            .Where(a => a.SlaDeadline <= now
                && (a.Status == AlertStatus.New || a.Status == AlertStatus.Acknowledged))
            .ToListAsync(cancellationToken);

        if (overdueAlerts.Count == 0) return;

        // Get enabled escalation paths
        var paths = await db.EscalationPaths
            .Where(p => p.IsEnabled)
            .ToListAsync(cancellationToken);

        if (paths.Count == 0) return;

        foreach (var alert in overdueAlerts)
        {
            // Match escalation paths by severity
            var matchingPaths = paths
                .Where(p => p.TriggerSeverity == alert.Severity)
                .ToList();

            if (matchingPaths.Count == 0) continue;

            // Check how many escalation notifications already sent for this alert
            var existingEscalations = await db.AlertNotifications
                .CountAsync(n => n.AlertId == alert.Id
                    && n.Subject != null && n.Subject.Contains("[ESCALATION]"), cancellationToken);

            foreach (var path in matchingPaths)
            {
                // Check MaxEscalations cap
                if (existingEscalations >= path.MaxEscalations)
                    continue;

                // Check RepeatIntervalMinutes
                var lastEscalation = await db.AlertNotifications
                    .Where(n => n.AlertId == alert.Id
                        && n.Subject != null && n.Subject.Contains("[ESCALATION]"))
                    .OrderByDescending(n => n.SentAt)
                    .FirstOrDefaultAsync(cancellationToken);

                if (lastEscalation != null
                    && lastEscalation.SentAt.AddMinutes(path.RepeatIntervalMinutes) > now)
                    continue;

                // Check escalation delay
                if (alert.SlaDeadline.AddMinutes(path.EscalationDelayMinutes) > now)
                    continue;

                // Transition alert to Escalated if not already
                if (alert.Status != AlertStatus.Escalated)
                {
                    try
                    {
                        await _alertManager.TransitionAlertAsync(
                            alert.Id, AlertStatus.Escalated, "system", "EscalationService", cancellationToken: cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to transition alert {AlertId} to Escalated", alert.AlertId);
                    }
                }

                // Send escalation notification to all recipients
                await _notificationService.SendNotificationAsync(alert, cancellationToken);

                _logger.LogWarning("Escalated alert {AlertId} — SLA exceeded by {Minutes}m",
                    alert.AlertId, (int)(now - alert.SlaDeadline).TotalMinutes);
            }
        }
    }

    /// <inheritdoc />
    public async Task<EscalationPath> ConfigureEscalationPathAsync(
        EscalationPath path, CancellationToken cancellationToken = default)
    {
        if (path.Recipients.Count == 0)
            throw new InvalidOperationException("VALIDATION_ERROR: Recipients must have at least one entry.");
        if (path.EscalationDelayMinutes <= 0)
            throw new InvalidOperationException("VALIDATION_ERROR: EscalationDelayMinutes must be > 0.");
        if (path.RepeatIntervalMinutes < 5)
            throw new InvalidOperationException("VALIDATION_ERROR: RepeatIntervalMinutes must be >= 5.");

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        if (path.Id == Guid.Empty)
        {
            path.Id = Guid.NewGuid();
            path.CreatedAt = DateTimeOffset.UtcNow;
            path.UpdatedAt = DateTimeOffset.UtcNow;
            db.EscalationPaths.Add(path);
        }
        else
        {
            var existing = await db.EscalationPaths.FindAsync(new object[] { path.Id }, cancellationToken)
                ?? throw new KeyNotFoundException($"ESCALATION_NOT_FOUND: Path '{path.Id}' not found.");

            existing.Name = path.Name;
            existing.TriggerSeverity = path.TriggerSeverity;
            existing.EscalationDelayMinutes = path.EscalationDelayMinutes;
            existing.Recipients = path.Recipients;
            existing.Channel = path.Channel;
            existing.RepeatIntervalMinutes = path.RepeatIntervalMinutes;
            existing.MaxEscalations = path.MaxEscalations;
            existing.WebhookUrl = path.WebhookUrl;
            existing.IsEnabled = path.IsEnabled;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
            path = existing;
        }

        await db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Escalation path '{Name}' configured for {Severity}",
            path.Name, path.TriggerSeverity);
        return path;
    }

    /// <inheritdoc />
    public async Task<List<EscalationPath>> GetEscalationPathsAsync(
        AlertSeverity? severity = null, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var query = db.EscalationPaths.AsQueryable();
        if (severity.HasValue)
            query = query.Where(p => p.TriggerSeverity == severity.Value);

        return await query.OrderBy(p => p.TriggerSeverity).ThenBy(p => p.Name).ToListAsync(cancellationToken);
    }
}
