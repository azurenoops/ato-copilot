using System.Collections.Concurrent;
using System.Collections.Immutable;
using Ato.Copilot.Channels.Abstractions;
using Ato.Copilot.Channels.Models;
using Microsoft.Extensions.Logging;

namespace Ato.Copilot.Channels.Implementations;

/// <summary>
/// In-memory channel implementation using concurrent collections for single-instance deployments and testing.
/// Uses ConcurrentDictionary for O(1) connection lookups and ImmutableHashSet for lock-free conversation group reads.
/// </summary>
public class InMemoryChannel : IChannel
{
    private readonly ConcurrentDictionary<string, ConnectionInfo> _connections = new();
    private readonly ConcurrentDictionary<string, ImmutableHashSet<string>> _conversationGroups = new();
    private readonly ConcurrentDictionary<string, Func<ChannelMessage, Task>> _handlers = new();
    private readonly ILogger<InMemoryChannel> _logger;

    /// <summary>
    /// Initializes a new instance of InMemoryChannel.
    /// </summary>
    public InMemoryChannel(ILogger<InMemoryChannel> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Register a connection with a message delivery handler.
    /// </summary>
    public void RegisterConnection(ConnectionInfo connection, Func<ChannelMessage, Task> handler)
    {
        _connections[connection.ConnectionId] = connection;
        _handlers[connection.ConnectionId] = handler;
        _logger.LogInformation("Connection {ConnectionId} registered for user {UserId}", connection.ConnectionId, connection.UserId);
    }

    /// <summary>
    /// Remove a connection from the channel.
    /// </summary>
    public void RemoveConnection(string connectionId)
    {
        _connections.TryRemove(connectionId, out _);
        _handlers.TryRemove(connectionId, out _);
        _logger.LogInformation("Connection {ConnectionId} removed from channel", connectionId);
    }

    /// <summary>
    /// Add a connection to a conversation group.
    /// </summary>
    public void AddToGroup(string connectionId, string conversationId)
    {
        _conversationGroups.AddOrUpdate(
            conversationId,
            _ => ImmutableHashSet.Create(connectionId),
            (_, existing) => existing.Add(connectionId));
    }

    /// <summary>
    /// Remove a connection from a conversation group.
    /// </summary>
    public void RemoveFromGroup(string connectionId, string conversationId)
    {
        _conversationGroups.AddOrUpdate(
            conversationId,
            _ => ImmutableHashSet<string>.Empty,
            (_, existing) => existing.Remove(connectionId));
    }

    /// <summary>
    /// Remove a connection from all conversation groups.
    /// </summary>
    public void RemoveFromAllGroups(string connectionId)
    {
        foreach (var kvp in _conversationGroups)
        {
            _conversationGroups.AddOrUpdate(
                kvp.Key,
                _ => ImmutableHashSet<string>.Empty,
                (_, existing) => existing.Remove(connectionId));
        }
    }

    /// <summary>
    /// Remove empty conversation group entries.
    /// </summary>
    public void CleanupEmptyGroups()
    {
        foreach (var kvp in _conversationGroups)
        {
            if (kvp.Value.IsEmpty)
            {
                _conversationGroups.TryRemove(kvp.Key, out _);
            }
        }
    }

    /// <inheritdoc />
    public async Task SendAsync(string connectionId, ChannelMessage message, CancellationToken ct = default)
    {
        // Pre-filter check (R5)
        if (!await IsConnectedAsync(connectionId, ct))
        {
            _logger.LogWarning("Skipping send to inactive connection {ConnectionId}", connectionId);
            return;
        }

        // Safety-net try-catch for race condition (R5)
        try
        {
            if (_handlers.TryGetValue(connectionId, out var handler))
            {
                await handler(message);

                // Update LastActivityAt (FR-009)
                if (_connections.TryGetValue(connectionId, out var connection))
                {
                    connection.LastActivityAt = DateTimeOffset.UtcNow;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send message to connection {ConnectionId}, skipping", connectionId);
        }
    }

    /// <inheritdoc />
    public async Task SendToConversationAsync(string conversationId, ChannelMessage message, CancellationToken ct = default)
    {
        if (!_conversationGroups.TryGetValue(conversationId, out var members))
        {
            return; // No members — silently drop
        }

        // Snapshot iteration via ImmutableHashSet (R1)
        foreach (var connectionId in members)
        {
            await SendAsync(connectionId, message, ct);
        }
    }

    /// <inheritdoc />
    public async Task BroadcastAsync(ChannelMessage message, CancellationToken ct = default)
    {
        foreach (var connectionId in _connections.Keys)
        {
            await SendAsync(connectionId, message, ct);
        }
    }

    /// <inheritdoc />
    public Task<bool> IsConnectedAsync(string connectionId, CancellationToken ct = default)
    {
        var isConnected = _connections.TryGetValue(connectionId, out var connection) && connection.IsActive;
        return Task.FromResult(isConnected);
    }

    /// <summary>
    /// Get connection info for a specific connection.
    /// </summary>
    public ConnectionInfo? GetConnectionInfo(string connectionId)
    {
        return _connections.TryGetValue(connectionId, out var connection) ? connection : null;
    }

    /// <summary>
    /// Get all registered connections.
    /// </summary>
    public IEnumerable<ConnectionInfo> GetAllConnections()
    {
        return _connections.Values;
    }
}
