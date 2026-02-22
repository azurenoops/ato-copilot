using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Ato.Copilot.Core.Interfaces.Kanban;

namespace Ato.Copilot.Agents.Compliance.Services;

/// <summary>
/// Notification dispatch service using bounded Channel for async, fire-and-forget delivery.
/// Registered as Singleton — maintains channel and HTTP clients for webhook delivery.
/// </summary>
public class NotificationService : INotificationService, IDisposable
{
    private readonly Channel<NotificationMessage> _channel;
    private readonly ILogger<NotificationService> _logger;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _dispatchTask;

    /// <summary>
    /// Initializes a new instance of <see cref="NotificationService"/>.
    /// </summary>
    public NotificationService(ILogger<NotificationService> logger)
    {
        _logger = logger;
        _channel = Channel.CreateBounded<NotificationMessage>(new BoundedChannelOptions(500)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
        });

        _dispatchTask = Task.Run(DispatchLoopAsync);
    }

    /// <inheritdoc />
    public async Task EnqueueAsync(NotificationMessage message)
    {
        await _channel.Writer.WriteAsync(message);
        _logger.LogDebug("Notification enqueued: {EventType} for task {TaskNumber} to user {TargetUserId}",
            message.EventType, message.TaskNumber, message.TargetUserId);
    }

    private async Task DispatchLoopAsync()
    {
        try
        {
            await foreach (var message in _channel.Reader.ReadAllAsync(_cts.Token))
            {
                try
                {
                    // For now, log the notification. Channel-specific dispatch (Email/Teams/Slack)
                    // will be implemented in Phase 14 (US12 - T077).
                    _logger.LogInformation("Dispatching notification: {EventType} for {TaskNumber} to {TargetUserId}: {Title}",
                        message.EventType, message.TaskNumber, message.TargetUserId, message.Title);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to dispatch notification for task {TaskNumber}", message.TaskNumber);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _channel.Writer.TryComplete();
        _cts.Cancel();
        _cts.Dispose();
        GC.SuppressFinalize(this);
    }
}
