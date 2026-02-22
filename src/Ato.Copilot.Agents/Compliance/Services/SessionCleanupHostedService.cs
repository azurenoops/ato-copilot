using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Ato.Copilot.Core.Data.Context;
using Ato.Copilot.Core.Models.Auth;

namespace Ato.Copilot.Agents.Compliance.Services;

/// <summary>
/// Background service that periodically cleans up stale CAC sessions and expired JIT requests.
/// Runs every 5 minutes by default (configurable via Agents:Auth:SessionCleanup:IntervalMinutes).
/// Creates a scoped DI per tick to avoid DbContext lifetime issues.
/// </summary>
public class SessionCleanupHostedService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SessionCleanupHostedService> _logger;
    private readonly TimeSpan _interval;

    /// <summary>
    /// Initializes a new instance of <see cref="SessionCleanupHostedService"/>.
    /// </summary>
    public SessionCleanupHostedService(
        IServiceProvider serviceProvider,
        ILogger<SessionCleanupHostedService> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;

        var intervalMinutes = configuration.GetValue<int>("Agents:Auth:SessionCleanup:IntervalMinutes", 5);
        _interval = TimeSpan.FromMinutes(intervalMinutes);
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SessionCleanupHostedService started with interval {Interval}", _interval);

        using var timer = new PeriodicTimer(_interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await CleanupAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during session cleanup");
            }
        }

        _logger.LogInformation("SessionCleanupHostedService stopped");
    }

    private async Task CleanupAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AtoCopilotContext>();
        var now = DateTimeOffset.UtcNow;

        // ── Expire stale CAC sessions ────────────────────────────────────
        var staleSessions = await db.CacSessions
            .Where(s => s.Status == SessionStatus.Active && s.ExpiresAt <= now)
            .ToListAsync(cancellationToken);

        foreach (var session in staleSessions)
        {
            session.Status = SessionStatus.Expired;
            session.UpdatedAt = now;
        }

        // ── Expire stale JIT requests ────────────────────────────────────
        var staleRequests = await db.JitRequests
            .Where(r => r.Status == JitRequestStatus.Active
                && r.ExpiresAt != null
                && r.ExpiresAt <= now)
            .ToListAsync(cancellationToken);

        foreach (var request in staleRequests)
        {
            request.Status = JitRequestStatus.Expired;
        }

        if (staleSessions.Count > 0 || staleRequests.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation(
                "Session cleanup: expired {SessionCount} sessions and {RequestCount} JIT requests",
                staleSessions.Count, staleRequests.Count);
        }
    }
}
