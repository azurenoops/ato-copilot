using Ato.Copilot.Channels.Abstractions;
using Ato.Copilot.Channels.Models;
using Ato.Copilot.Chat.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

using ConnectionInfo = Ato.Copilot.Channels.Models.ConnectionInfo;

namespace Ato.Copilot.Chat.Channels;

/// <summary>
/// <see cref="IChannelManager"/> implementation that delegates connection tracking
/// to <see cref="SignalRConnectionTracker"/> and group management to
/// <see cref="IHubContext{ChatHub}"/>.
/// </summary>
public sealed class SignalRChannelManager : IChannelManager
{
    private readonly SignalRConnectionTracker _tracker;
    private readonly IHubContext<ChatHub> _hubContext;
    private readonly IChannel _channel;
    private readonly ILogger<SignalRChannelManager> _logger;

    public SignalRChannelManager(
        SignalRConnectionTracker tracker,
        IHubContext<ChatHub> hubContext,
        IChannel channel,
        ILogger<SignalRChannelManager> logger)
    {
        _tracker = tracker;
        _hubContext = hubContext;
        _channel = channel;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<ConnectionInfo> RegisterConnectionAsync(string userId, Dictionary<string, object>? metadata = null, CancellationToken ct = default)
    {
        // For SignalR, the connectionId is pre-assigned by the framework.
        // When called from ChatHub.OnConnectedAsync, the actual SignalR connectionId
        // is passed via metadata["signalRConnectionId"]. Otherwise generate one.
        var connectionId = metadata?.TryGetValue("signalRConnectionId", out var id) == true
            ? id.ToString()!
            : Guid.NewGuid().ToString();

        var cleanMetadata = metadata is not null
            ? new Dictionary<string, object>(metadata.Where(kvp => kvp.Key != "signalRConnectionId"))
            : null;

        var info = _tracker.RegisterConnection(connectionId, userId, cleanMetadata);
        _logger.LogInformation("Registered connection {ConnectionId} for user {UserId}", connectionId, userId);

        return Task.FromResult(info);
    }

    /// <inheritdoc />
    public async Task UnregisterConnectionAsync(string connectionId, CancellationToken ct = default)
    {
        var info = _tracker.UnregisterConnection(connectionId);
        if (info is null)
        {
            _logger.LogDebug("Connection {ConnectionId} not found during unregister", connectionId);
            return;
        }

        // Remove from all SignalR groups the connection was tracking
        foreach (var conversationId in info.Conversations)
        {
            try
            {
                await _hubContext.Groups.RemoveFromGroupAsync(connectionId, conversationId, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to remove connection {ConnectionId} from group {ConversationId} during unregister",
                    connectionId, conversationId);
            }
        }

        _logger.LogInformation("Unregistered connection {ConnectionId}", connectionId);
    }

    /// <inheritdoc />
    public async Task JoinConversationAsync(string connectionId, string conversationId, CancellationToken ct = default)
    {
        await _hubContext.Groups.AddToGroupAsync(connectionId, conversationId, ct);

        var info = _tracker.GetConnectionInfo(connectionId);
        if (info is not null)
        {
            info.Conversations.Add(conversationId);
            _tracker.TouchActivity(connectionId);
        }

        _logger.LogInformation("Connection {ConnectionId} joined conversation {ConversationId}", connectionId, conversationId);
    }

    /// <inheritdoc />
    public async Task LeaveConversationAsync(string connectionId, string conversationId, CancellationToken ct = default)
    {
        await _hubContext.Groups.RemoveFromGroupAsync(connectionId, conversationId, ct);

        var info = _tracker.GetConnectionInfo(connectionId);
        if (info is not null)
        {
            info.Conversations.Remove(conversationId);
            _tracker.TouchActivity(connectionId);
        }

        _logger.LogInformation("Connection {ConnectionId} left conversation {ConversationId}", connectionId, conversationId);
    }

    /// <inheritdoc />
    public async Task SendToConversationAsync(string conversationId, ChannelMessage message, CancellationToken ct = default)
    {
        await _channel.SendToConversationAsync(conversationId, message, ct);
    }

    /// <inheritdoc />
    public Task<bool> IsConnectedAsync(string connectionId, CancellationToken ct = default)
    {
        return Task.FromResult(_tracker.IsConnected(connectionId));
    }

    /// <inheritdoc />
    public Task<ConnectionInfo?> GetConnectionInfoAsync(string connectionId, CancellationToken ct = default)
    {
        return Task.FromResult(_tracker.GetConnectionInfo(connectionId));
    }

    /// <inheritdoc />
    public IEnumerable<ConnectionInfo> GetAllConnections()
    {
        return _tracker.GetAllConnections();
    }
}
