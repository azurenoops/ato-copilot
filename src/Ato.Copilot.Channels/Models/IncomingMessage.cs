namespace Ato.Copilot.Channels.Models;

/// <summary>
/// An inbound message from any client channel.
/// </summary>
public class IncomingMessage
{
    /// <summary>Originating connection ID.</summary>
    public string ConnectionId { get; set; } = string.Empty;

    /// <summary>Target conversation ID.</summary>
    public string ConversationId { get; set; } = string.Empty;

    /// <summary>Message text content.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>Explicit agent routing hint (null for intent-based routing).</summary>
    public string? TargetAgentType { get; set; }

    /// <summary>File attachments.</summary>
    public List<MessageAttachment> Attachments { get; set; } = new();

    /// <summary>Request metadata (e.g., source platform).</summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>When the message was sent.</summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}
