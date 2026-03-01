namespace Ato.Copilot.Channels.Models;

/// <summary>
/// Universal message payload sent through channels.
/// </summary>
public class ChannelMessage
{
    /// <summary>Unique message identifier.</summary>
    public string MessageId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Conversation this message belongs to.</summary>
    public string ConversationId { get; set; } = string.Empty;

    /// <summary>Message type classification.</summary>
    public MessageType Type { get; set; } = MessageType.UserMessage;

    /// <summary>Message text content.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>Agent that produced this response (null for user messages).</summary>
    public string? AgentType { get; set; }

    /// <summary>When the message was created (UTC).</summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Extensible key-value metadata.</summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>Whether this is part of a stream.</summary>
    public bool IsStreaming { get; set; }

    /// <summary>Whether the message/stream is complete.</summary>
    public bool IsComplete { get; set; } = true;

    /// <summary>Sequence number for streaming chunks (null for non-streaming).</summary>
    public long? SequenceNumber { get; set; }
}
