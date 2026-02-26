using Ato.Copilot.Channels.Abstractions;
using Ato.Copilot.Channels.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ato.Copilot.Channels.Implementations;

/// <summary>
/// Background service that periodically scans for idle connections and cleans them up.
/// Uses PeriodicTimer (R2) — the same pattern used in 5+ existing codebase services.
/// Runs at IdleCleanupInterval (default 5 min) and removes connections idle longer
/// than IdleConnectionTimeout (default 30 min).
/// </summary>
public class IdleConnectionCleanupService : BackgroundService
{
    private readonly IChannelManager _channelManager;
    private readonly IOptions<ChannelOptions> _options;
    private readonly ILogger<IdleConnectionCleanupService> _logger;

    /// <summary>
    /// Initializes a new instance of IdleConnectionCleanupService.
    /// </summary>
    public IdleConnectionCleanupService(
        IChannelManager channelManager,
        IOptions<ChannelOptions> options,
        ILogger<IdleConnectionCleanupService> logger)
    {
        _channelManager = channelManager;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Idle connection cleanup service started. Interval: {Interval}, Timeout: {Timeout}",
            _options.Value.IdleCleanupInterval,
            _options.Value.IdleConnectionTimeout);

        using var timer = new PeriodicTimer(_options.Value.IdleCleanupInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await CleanupIdleConnectionsAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error during idle connection cleanup");
            }
        }
    }

    private async Task CleanupIdleConnectionsAsync(CancellationToken ct)
    {
        var timeout = _options.Value.IdleConnectionTimeout;
        var now = DateTimeOffset.UtcNow;
        var cleanedUp = 0;

        foreach (var connection in _channelManager.GetAllConnections())
        {
            if (!connection.IsActive) continue;

            var idleDuration = now - connection.LastActivityAt;
            if (idleDuration > timeout)
            {
                _logger.LogInformation(
                    "Cleaning up idle connection {ConnectionId} for user {UserId} (idle {IdleDuration})",
                    connection.ConnectionId, connection.UserId, idleDuration);

                await _channelManager.UnregisterConnectionAsync(connection.ConnectionId, ct);
                cleanedUp++;
            }
        }

        if (cleanedUp > 0)
        {
            _logger.LogInformation("Idle connection cleanup completed: {Count} connections removed", cleanedUp);
        }
    }
}
