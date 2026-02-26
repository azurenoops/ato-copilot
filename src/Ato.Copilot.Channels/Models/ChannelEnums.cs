namespace Ato.Copilot.Channels.Models;

/// <summary>
/// Transport mechanism for the channel.
/// </summary>
public enum ChannelType
{
    /// <summary>SignalR-based transport.</summary>
    SignalR,

    /// <summary>WebSocket-based transport.</summary>
    WebSocket,

    /// <summary>Long-polling transport.</summary>
    LongPolling,

    /// <summary>Server-Sent Events transport.</summary>
    ServerSentEvents
}

/// <summary>
/// Classification of a channel message.
/// </summary>
public enum MessageType
{
    /// <summary>Message from a user.</summary>
    UserMessage,

    /// <summary>Response from an agent.</summary>
    AgentResponse,

    /// <summary>Agent is processing (thinking indicator).</summary>
    AgentThinking,

    /// <summary>A tool is being executed.</summary>
    ToolExecution,

    /// <summary>Result from a tool execution.</summary>
    ToolResult,

    /// <summary>Error message.</summary>
    Error,

    /// <summary>System notification.</summary>
    SystemNotification,

    /// <summary>Progress update during long operations.</summary>
    ProgressUpdate,

    /// <summary>Request for user confirmation.</summary>
    ConfirmationRequest,

    /// <summary>Chunk in a streaming response.</summary>
    StreamChunk
}

/// <summary>
/// Content type classification for streaming chunks.
/// </summary>
public enum StreamContentType
{
    /// <summary>Plain text content.</summary>
    Text,

    /// <summary>Source code content.</summary>
    Code,

    /// <summary>Markdown-formatted content.</summary>
    Markdown,

    /// <summary>JSON content.</summary>
    Json,

    /// <summary>Tool output content.</summary>
    Tool,

    /// <summary>Progress indicator content.</summary>
    Progress
}

/// <summary>
/// Behavior when DefaultMessageHandler has no AgentInvoker configured.
/// </summary>
public enum DefaultHandlerBehavior
{
    /// <summary>Return user message as an AgentResponse (default, useful for testing).</summary>
    Echo,

    /// <summary>Return an Error type message.</summary>
    Error
}
