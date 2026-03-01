namespace Ato.Copilot.Channels.Abstractions;

/// <summary>
/// Manages conversation state across channel interactions.
/// This is a Channels-local interface (R9: no project reference to Ato.Copilot.State).
/// At the DI composition root, the consumer registers an implementation or adapter
/// that bridges to the actual state persistence layer.
/// </summary>
public interface IConversationStateManager
{
    /// <summary>
    /// Retrieves an existing conversation by ID, or null if not found.
    /// </summary>
    Task<ConversationState?> GetConversationAsync(string conversationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists the conversation state (creates or updates).
    /// </summary>
    Task SaveConversationAsync(ConversationState state, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new conversation and returns its unique ID.
    /// </summary>
    Task<string> CreateConversationAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Conversation state managed by the Channels library.
/// </summary>
public class ConversationState
{
    /// <summary>Unique conversation identifier.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>User who owns this conversation.</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>Ordered list of messages in the conversation.</summary>
    public List<ConversationMessage> Messages { get; set; } = new();

    /// <summary>Arbitrary key-value variables associated with the conversation.</summary>
    public Dictionary<string, object> Variables { get; set; } = new();

    /// <summary>When the conversation was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>When the conversation was last active.</summary>
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// A single message within a conversation.
/// </summary>
public class ConversationMessage
{
    /// <summary>Message role — "user", "assistant", "system".</summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>Message content text.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>When the message was created.</summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
