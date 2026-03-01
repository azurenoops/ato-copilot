using Ato.Copilot.Channels.Models;

namespace Ato.Copilot.Channels.Abstractions;

/// <summary>
/// Transport abstraction for sending messages through a specific channel mechanism.
/// </summary>
public interface IChannel
{
    /// <summary>Send a message to a specific connection.</summary>
    /// <param name="connectionId">Target connection ID.</param>
    /// <param name="message">Message to send.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SendAsync(string connectionId, ChannelMessage message, CancellationToken ct = default);

    /// <summary>Send a message to all connections in a conversation group.</summary>
    /// <param name="conversationId">Target conversation ID.</param>
    /// <param name="message">Message to send.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SendToConversationAsync(string conversationId, ChannelMessage message, CancellationToken ct = default);

    /// <summary>Send a message to all connected clients.</summary>
    /// <param name="message">Message to broadcast.</param>
    /// <param name="ct">Cancellation token.</param>
    Task BroadcastAsync(ChannelMessage message, CancellationToken ct = default);

    /// <summary>Check if a connection is currently active.</summary>
    /// <param name="connectionId">Connection ID to check.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if the connection is active.</returns>
    Task<bool> IsConnectedAsync(string connectionId, CancellationToken ct = default);
}
