namespace Ato.Copilot.Channels.Models;

/// <summary>
/// Tracks a client connection's state and conversation memberships.
/// </summary>
public class ConnectionInfo
{
    /// <summary>Unique connection identifier.</summary>
    public string ConnectionId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Authenticated user identifier.</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>When the connection was established.</summary>
    public DateTimeOffset ConnectedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Last message send/receive time.</summary>
    public DateTimeOffset LastActivityAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Conversation IDs this connection is in.</summary>
    public HashSet<string> Conversations { get; set; } = new();

    /// <summary>Extensible connection metadata.</summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>Whether the connection is currently active.</summary>
    public bool IsActive { get; set; } = true;
}
