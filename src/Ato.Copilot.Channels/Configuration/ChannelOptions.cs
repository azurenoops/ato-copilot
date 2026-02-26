namespace Ato.Copilot.Channels.Configuration;

using Ato.Copilot.Channels.Models;

/// <summary>
/// Configuration for channel behavior.
/// </summary>
public class ChannelOptions
{
    /// <summary>Configuration section name for binding.</summary>
    public const string SectionName = "Channels";

    /// <summary>Log connection/disconnect events.</summary>
    public bool EnableConnectionLogging { get; set; } = true;

    /// <summary>Auto-cleanup threshold for idle connections.</summary>
    public TimeSpan IdleConnectionTimeout { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>How often to check for idle connections.</summary>
    public TimeSpan IdleCleanupInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Maximum concurrent connections per user.</summary>
    public int MaxConnectionsPerUser { get; set; } = 10;

    /// <summary>Whether to ACK messages.</summary>
    public bool EnableMessageAcknowledgment { get; set; }

    /// <summary>Behavior when no AgentInvoker is configured.</summary>
    public DefaultHandlerBehavior DefaultHandlerBehavior { get; set; } = DefaultHandlerBehavior.Echo;

    /// <summary>Nested streaming configuration.</summary>
    public StreamingOptions Streaming { get; set; } = new();
}

/// <summary>
/// Configuration for streaming behavior.
/// </summary>
public class StreamingOptions
{
    /// <summary>Maximum time a stream can remain open.</summary>
    public TimeSpan StreamTimeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Maximum size of a single chunk in bytes.</summary>
    public int MaxChunkSize { get; set; } = 4096;

    /// <summary>Whether to auto-complete streams on disposal.</summary>
    public bool EnableAutoComplete { get; set; } = true;
}
