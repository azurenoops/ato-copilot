using Ato.Copilot.Channels.Models;

namespace Ato.Copilot.Channels.Abstractions;

/// <summary>
/// Manages connections, conversation groups, and message routing.
/// </summary>
public interface IChannelManager
{
    /// <summary>Register a new connection.</summary>
    /// <param name="userId">Authenticated user ID.</param>
    /// <param name="metadata">Optional connection metadata.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>ConnectionInfo with generated connection ID.</returns>
    Task<ConnectionInfo> RegisterConnectionAsync(string userId, Dictionary<string, object>? metadata = null, CancellationToken ct = default);

    /// <summary>Unregister a connection and remove from all conversation groups.</summary>
    /// <param name="connectionId">Connection to unregister.</param>
    /// <param name="ct">Cancellation token.</param>
    Task UnregisterConnectionAsync(string connectionId, CancellationToken ct = default);

    /// <summary>Add a connection to a conversation group.</summary>
    /// <param name="connectionId">Connection to add.</param>
    /// <param name="conversationId">Conversation to join.</param>
    /// <param name="ct">Cancellation token.</param>
    Task JoinConversationAsync(string connectionId, string conversationId, CancellationToken ct = default);

    /// <summary>Remove a connection from a conversation group.</summary>
    /// <param name="connectionId">Connection to remove.</param>
    /// <param name="conversationId">Conversation to leave.</param>
    /// <param name="ct">Cancellation token.</param>
    Task LeaveConversationAsync(string connectionId, string conversationId, CancellationToken ct = default);

    /// <summary>Send a message to all connections in a conversation.</summary>
    /// <param name="conversationId">Target conversation.</param>
    /// <param name="message">Message to send.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SendToConversationAsync(string conversationId, ChannelMessage message, CancellationToken ct = default);

    /// <summary>Check if a connection is registered and active.</summary>
    /// <param name="connectionId">Connection ID to check.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if connected and active.</returns>
    Task<bool> IsConnectedAsync(string connectionId, CancellationToken ct = default);

    /// <summary>Get info for a specific connection.</summary>
    /// <param name="connectionId">Connection ID to look up.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>ConnectionInfo or null if not found.</returns>
    Task<ConnectionInfo?> GetConnectionInfoAsync(string connectionId, CancellationToken ct = default);

    /// <summary>Get all active connections (for idle cleanup enumeration).</summary>
    /// <returns>Enumerable of all connection infos.</returns>
    IEnumerable<ConnectionInfo> GetAllConnections();
}
