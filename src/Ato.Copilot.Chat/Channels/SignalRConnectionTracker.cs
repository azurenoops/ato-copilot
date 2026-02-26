using System.Collections.Concurrent;
using Ato.Copilot.Channels.Models;

using ConnectionInfo = Ato.Copilot.Channels.Models.ConnectionInfo;

namespace Ato.Copilot.Chat.Channels;

/// <summary>
/// Thread-safe tracker for SignalR connections, mapping SignalR Context.ConnectionId
/// to Channels <see cref="ConnectionInfo"/> instances.
/// </summary>
public sealed class SignalRConnectionTracker
{
    private readonly ConcurrentDictionary<string, ConnectionInfo> _connections = new();

    /// <summary>
    /// Register a new SignalR connection.
    /// </summary>
    /// <param name="connectionId">SignalR Context.ConnectionId.</param>
    /// <param name="userId">Authenticated user ID.</param>
    /// <param name="metadata">Optional connection metadata.</param>
    /// <returns>The created <see cref="ConnectionInfo"/>.</returns>
    public ConnectionInfo RegisterConnection(string connectionId, string userId, Dictionary<string, object>? metadata = null)
    {
        var info = new ConnectionInfo
        {
            ConnectionId = connectionId,
            UserId = userId,
            ConnectedAt = DateTimeOffset.UtcNow,
            LastActivityAt = DateTimeOffset.UtcNow,
            IsActive = true,
            Metadata = metadata ?? new Dictionary<string, object>()
        };

        _connections[connectionId] = info;
        return info;
    }

    /// <summary>
    /// Unregister a connection, marking it inactive and removing from tracking.
    /// </summary>
    /// <param name="connectionId">SignalR Context.ConnectionId.</param>
    /// <returns>The removed <see cref="ConnectionInfo"/> or null if not found.</returns>
    public ConnectionInfo? UnregisterConnection(string connectionId)
    {
        if (_connections.TryRemove(connectionId, out var info))
        {
            info.IsActive = false;
            return info;
        }

        return null;
    }

    /// <summary>
    /// Get connection info for a specific connection.
    /// </summary>
    /// <param name="connectionId">SignalR Context.ConnectionId.</param>
    /// <returns>ConnectionInfo or null if not found.</returns>
    public ConnectionInfo? GetConnectionInfo(string connectionId)
    {
        return _connections.TryGetValue(connectionId, out var info) ? info : null;
    }

    /// <summary>
    /// Get a snapshot of all active connections.
    /// </summary>
    /// <returns>Enumerable of all tracked connection infos.</returns>
    public IEnumerable<ConnectionInfo> GetAllConnections()
    {
        return _connections.Values.ToArray();
    }

    /// <summary>
    /// Check if a connection is registered and active.
    /// </summary>
    /// <param name="connectionId">SignalR Context.ConnectionId.</param>
    /// <returns>True if the connection exists and is active.</returns>
    public bool IsConnected(string connectionId)
    {
        return _connections.TryGetValue(connectionId, out var info) && info.IsActive;
    }

    /// <summary>
    /// Update the last activity timestamp for a connection.
    /// </summary>
    /// <param name="connectionId">SignalR Context.ConnectionId.</param>
    public void TouchActivity(string connectionId)
    {
        if (_connections.TryGetValue(connectionId, out var info))
        {
            info.LastActivityAt = DateTimeOffset.UtcNow;
        }
    }
}
