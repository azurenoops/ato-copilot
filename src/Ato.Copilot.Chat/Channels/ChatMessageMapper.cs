using Ato.Copilot.Channels.Models;
using Ato.Copilot.Chat.Models;

namespace Ato.Copilot.Chat.Channels;

/// <summary>
/// Maps between Chat app DTOs and Channels library models.
/// </summary>
public static class ChatMessageMapper
{
    /// <summary>
    /// Map a <see cref="SendMessageRequest"/> to a <see cref="IncomingMessage"/>.
    /// </summary>
    /// <param name="request">Chat request.</param>
    /// <param name="connectionId">SignalR connection ID.</param>
    /// <returns>Channels IncomingMessage.</returns>
    public static IncomingMessage ToIncomingMessage(SendMessageRequest request, string connectionId)
    {
        var incoming = new IncomingMessage
        {
            ConnectionId = connectionId,
            ConversationId = request.ConversationId,
            Content = request.GetContent(),
            Timestamp = DateTimeOffset.UtcNow,
            Metadata = new Dictionary<string, object>()
        };

        // Carry over request context as metadata
        if (request.Context is not null)
        {
            foreach (var kvp in request.Context)
            {
                incoming.Metadata[kvp.Key] = kvp.Value;
            }
        }

        // Extract agent routing hint from context if provided
        if (request.Context?.TryGetValue("targetAgentType", out var agentType) == true)
        {
            incoming.TargetAgentType = agentType.ToString();
        }

        // Map attachment IDs to metadata (actual attachments are resolved downstream)
        if (request.AttachmentIds is { Count: > 0 })
        {
            incoming.Metadata["attachmentIds"] = request.AttachmentIds;
        }

        return incoming;
    }

    /// <summary>
    /// Map a <see cref="ChatResponse"/> to a <see cref="ChannelMessage"/>.
    /// </summary>
    /// <param name="response">Chat response.</param>
    /// <param name="conversationId">Target conversation ID.</param>
    /// <returns>Channels ChannelMessage.</returns>
    public static ChannelMessage ToChannelMessage(ChatResponse response, string conversationId)
    {
        var metadata = response.Metadata ?? new Dictionary<string, object>();

        // Inject suggestedActions into metadata so they flow through SignalR to the frontend
        if (response.SuggestedActions is { Count: > 0 })
        {
            metadata["suggestedActions"] = response.SuggestedActions;
        }

        return new ChannelMessage
        {
            MessageId = response.MessageId,
            ConversationId = conversationId,
            Type = response.Success ? MessageType.AgentResponse : MessageType.Error,
            Content = response.Success ? response.Content : response.Error ?? "The request could not be processed",
            Timestamp = DateTimeOffset.UtcNow,
            IsComplete = true,
            Metadata = metadata
        };
    }

    /// <summary>
    /// Map a <see cref="ChannelMessage"/> to a <see cref="ChatResponse"/>.
    /// </summary>
    /// <param name="message">Channel message.</param>
    /// <returns>Chat response.</returns>
    public static ChatResponse ToChatResponse(ChannelMessage message)
    {
        return new ChatResponse
        {
            MessageId = message.MessageId,
            Content = message.Type != MessageType.Error ? message.Content : string.Empty,
            Success = message.Type != MessageType.Error,
            Error = message.Type == MessageType.Error ? message.Content : null,
            Metadata = message.Metadata
        };
    }

    /// <summary>
    /// Map a <see cref="ChannelMessage"/> to the anonymous SignalR payload shape
    /// matching the existing ChatHub event format for backward compatibility.
    /// </summary>
    /// <param name="message">Channel message.</param>
    /// <returns>Anonymous object matching existing ChatHub event shape.</returns>
    public static object ToSignalRPayload(ChannelMessage message)
    {
        return new
        {
            conversationId = message.ConversationId,
            message = new
            {
                id = message.MessageId,
                conversationId = message.ConversationId,
                role = message.Type == MessageType.Error ? "System" : "Assistant",
                content = message.Content,
                status = message.IsComplete ? "Completed" : "Processing",
                timestamp = message.Timestamp.UtcDateTime,
                metadata = message.Metadata,
                attachments = Array.Empty<object>(),
                toolResults = Array.Empty<object>()
            }
        };
    }

    /// <summary>
    /// Map a <see cref="ChannelMessage"/> to the error payload shape
    /// matching the existing ChatHub MessageError event format.
    /// </summary>
    /// <param name="message">Error channel message.</param>
    /// <param name="messageId">Original message ID for correlation.</param>
    /// <returns>Anonymous object matching existing ChatHub error event shape.</returns>
    public static object ToSignalRErrorPayload(ChannelMessage message, string messageId)
    {
        var category = message.Metadata.TryGetValue("errorCategory", out var cat)
            ? cat.ToString()
            : "ProcessingError";

        return new
        {
            conversationId = message.ConversationId,
            messageId,
            error = message.Content,
            category
        };
    }
}
