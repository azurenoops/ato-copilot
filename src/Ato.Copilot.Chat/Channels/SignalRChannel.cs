using Ato.Copilot.Channels.Abstractions;
using Ato.Copilot.Channels.Models;
using Ato.Copilot.Chat.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Ato.Copilot.Chat.Channels;

/// <summary>
/// <see cref="IChannel"/> implementation that sends messages via SignalR
/// using <see cref="IHubContext{ChatHub}"/>.
/// Maps <see cref="ChannelMessage"/> payloads to the existing ChatHub event shapes
/// for full backward compatibility with connected clients.
/// </summary>
public sealed class SignalRChannel : IChannel
{
    private readonly IHubContext<ChatHub> _hubContext;
    private readonly SignalRConnectionTracker _tracker;
    private readonly ILogger<SignalRChannel> _logger;

    public SignalRChannel(
        IHubContext<ChatHub> hubContext,
        SignalRConnectionTracker tracker,
        ILogger<SignalRChannel> logger)
    {
        _hubContext = hubContext;
        _tracker = tracker;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task SendAsync(string connectionId, ChannelMessage message, CancellationToken ct = default)
    {
        var eventName = ResolveEventName(message);
        var payload = ResolvePayload(message);

        _logger.LogDebug("Sending {Event} to connection {ConnectionId}", eventName, connectionId);
        await _hubContext.Clients.Client(connectionId).SendAsync(eventName, payload, ct);
    }

    /// <inheritdoc />
    public async Task SendToConversationAsync(string conversationId, ChannelMessage message, CancellationToken ct = default)
    {
        var eventName = ResolveEventName(message);
        var payload = ResolvePayload(message);

        _logger.LogDebug("Sending {Event} to conversation {ConversationId}", eventName, conversationId);
        await _hubContext.Clients.Group(conversationId).SendAsync(eventName, payload, ct);
    }

    /// <inheritdoc />
    public async Task BroadcastAsync(ChannelMessage message, CancellationToken ct = default)
    {
        var eventName = ResolveEventName(message);
        var payload = ResolvePayload(message);

        _logger.LogDebug("Broadcasting {Event} to all clients", eventName);
        await _hubContext.Clients.All.SendAsync(eventName, payload, ct);
    }

    /// <inheritdoc />
    public Task<bool> IsConnectedAsync(string connectionId, CancellationToken ct = default)
    {
        return Task.FromResult(_tracker.IsConnected(connectionId));
    }

    /// <summary>
    /// Resolve the SignalR event name based on the message type,
    /// matching existing ChatHub event names for backward compat.
    /// </summary>
    private static string ResolveEventName(ChannelMessage message) => message.Type switch
    {
        MessageType.Error => "MessageError",
        MessageType.ProgressUpdate => "MessageProgress",
        MessageType.AgentThinking => "MessageProcessing",
        _ => "MessageReceived"
    };

    /// <summary>
    /// Resolve the payload shape based on the message type,
    /// matching existing ChatHub anonymous object format.
    /// </summary>
    private static object ResolvePayload(ChannelMessage message) => message.Type switch
    {
        MessageType.Error => ChatMessageMapper.ToSignalRErrorPayload(message, message.MessageId),
        _ => ChatMessageMapper.ToSignalRPayload(message)
    };
}
