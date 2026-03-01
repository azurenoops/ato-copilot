using Ato.Copilot.Channels.Abstractions;
using Ato.Copilot.Channels.Models;
using Microsoft.Extensions.Logging;

namespace Ato.Copilot.Channels.Implementations;

/// <summary>
/// Manages connections, conversation groups, and message routing via the underlying IChannel.
/// Provides connection registration with Guid IDs, conversation group management,
/// automatic unregister cleanup, and LastActivityAt tracking.
/// </summary>
public class ChannelManager : IChannelManager
{
    private readonly InMemoryChannel _channel;
    private readonly ILogger<ChannelManager> _logger;

    /// <summary>
    /// Initializes a new instance of ChannelManager.
    /// </summary>
    public ChannelManager(IChannel channel, ILogger<ChannelManager> logger)
    {
        _channel = (InMemoryChannel)channel;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<ConnectionInfo> RegisterConnectionAsync(string userId, Dictionary<string, object>? metadata = null, CancellationToken ct = default)
    {
        var connection = new ConnectionInfo
        {
            ConnectionId = Guid.NewGuid().ToString(),
            UserId = userId,
            ConnectedAt = DateTimeOffset.UtcNow,
            LastActivityAt = DateTimeOffset.UtcNow,
            Metadata = metadata ?? new Dictionary<string, object>(),
            IsActive = true
        };

        // Register with a no-op handler by default; callers can use InMemoryChannel directly for custom handlers
        _channel.RegisterConnection(connection, _ => Task.CompletedTask);

        _logger.LogInformation("Connection {ConnectionId} registered for user {UserId}", connection.ConnectionId, userId);

        return Task.FromResult(connection);
    }

    /// <inheritdoc />
    public Task UnregisterConnectionAsync(string connectionId, CancellationToken ct = default)
    {
        // Remove from all conversation groups (FR-007)
        _channel.RemoveFromAllGroups(connectionId);

        // Clean up empty groups (FR-008)
        _channel.CleanupEmptyGroups();

        // Remove connection from the channel
        _channel.RemoveConnection(connectionId);

        _logger.LogInformation("Connection {ConnectionId} unregistered and removed from all groups", connectionId);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task JoinConversationAsync(string connectionId, string conversationId, CancellationToken ct = default)
    {
        _channel.AddToGroup(connectionId, conversationId);
        _logger.LogDebug("Connection {ConnectionId} joined conversation {ConversationId}", connectionId, conversationId);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task LeaveConversationAsync(string connectionId, string conversationId, CancellationToken ct = default)
    {
        _channel.RemoveFromGroup(connectionId, conversationId);

        // Clean up empty groups (FR-008)
        _channel.CleanupEmptyGroups();

        _logger.LogDebug("Connection {ConnectionId} left conversation {ConversationId}", connectionId, conversationId);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task SendToConversationAsync(string conversationId, ChannelMessage message, CancellationToken ct = default)
    {
        await _channel.SendToConversationAsync(conversationId, message, ct);
    }

    /// <inheritdoc />
    public async Task<bool> IsConnectedAsync(string connectionId, CancellationToken ct = default)
    {
        return await _channel.IsConnectedAsync(connectionId, ct);
    }

    /// <inheritdoc />
    public Task<ConnectionInfo?> GetConnectionInfoAsync(string connectionId, CancellationToken ct = default)
    {
        return Task.FromResult(_channel.GetConnectionInfo(connectionId));
    }

    /// <inheritdoc />
    public IEnumerable<ConnectionInfo> GetAllConnections()
    {
        return _channel.GetAllConnections();
    }
}
