# Data Model: Copilot Everywhere — Multi-Channel Extensions & Channels Library

**Feature**: 013-copilot-everywhere  
**Date**: 2026-02-26  
**Status**: Complete

---

## Entity Overview

```text
ChannelOptions ──────────────────────────────────────────────────┐
     │                                                            │ configures
     ├── StreamingOptions                                         │
     └── DefaultHandlerBehavior                                   │
                                                                  │
IChannelManager                                                   │
     ├── ConnectionInfo ──< ConversationGroup >── ChannelMessage  │
     │                                                  │         │
     │                                                  │         │
     └── IChannel (InMemoryChannel)                     │         │
                                                        │         │
IMessageHandler (DefaultMessageHandler)                 │         │
     ├── IncomingMessage ──> MessageAttachment           │         │
     └── connects to IConversationStateManager          │         │
                                                        │         │
IStreamingHandler                                       │         │
     └── IStreamContext ──> StreamChunk                  │         │
                                                        │         │
                                              ◄─────────┘─────────┘
```

---

## Entities

### 1. ChannelMessage (NEW)

**Location**: `src/Ato.Copilot.Channels/Models/ChannelMessage.cs`

The universal message payload sent through any channel.

| Field | Type | Description | Default |
|-------|------|-------------|---------|
| `MessageId` | `string` | Unique message identifier | `Guid.NewGuid().ToString()` |
| `ConversationId` | `string` | Conversation this message belongs to | `""` |
| `Type` | `MessageType` | Message type classification | `MessageType.UserMessage` |
| `Content` | `string` | Message text content | `""` |
| `AgentType` | `string?` | Agent that produced this response | `null` |
| `Timestamp` | `DateTimeOffset` | When the message was created (UTC) | `DateTimeOffset.UtcNow` |
| `Metadata` | `Dictionary<string, object>` | Extensible key-value metadata | `new()` |
| `IsStreaming` | `bool` | Whether this is part of a stream | `false` |
| `IsComplete` | `bool` | Whether the message/stream is complete | `true` |
| `SequenceNumber` | `long?` | Sequence number for streaming chunks | `null` |

**Validation rules**:
- `MessageId` MUST be non-empty
- `ConversationId` MUST be non-empty for routed messages
- `Content` MAY be empty (e.g., for `AgentThinking` notifications)

```csharp
/// <summary>
/// Universal message payload sent through channels.
/// </summary>
public class ChannelMessage
{
    public string MessageId { get; set; } = Guid.NewGuid().ToString();
    public string ConversationId { get; set; } = string.Empty;
    public MessageType Type { get; set; } = MessageType.UserMessage;
    public string Content { get; set; } = string.Empty;
    public string? AgentType { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public Dictionary<string, object> Metadata { get; set; } = new();
    public bool IsStreaming { get; set; }
    public bool IsComplete { get; set; } = true;
    public long? SequenceNumber { get; set; }
}
```

---

### 2. ConnectionInfo (NEW)

**Location**: `src/Ato.Copilot.Channels/Models/ConnectionInfo.cs`

Tracks a client connection's state and conversation memberships.

| Field | Type | Description | Default |
|-------|------|-------------|---------|
| `ConnectionId` | `string` | Unique connection identifier | `Guid.NewGuid().ToString()` |
| `UserId` | `string` | Authenticated user identifier | `""` |
| `ConnectedAt` | `DateTimeOffset` | When the connection was established | `DateTimeOffset.UtcNow` |
| `LastActivityAt` | `DateTimeOffset` | Last message send/receive time | `DateTimeOffset.UtcNow` |
| `Conversations` | `IReadOnlySet<string>` | Conversation IDs this connection is in | `new HashSet<string>()` |
| `Metadata` | `Dictionary<string, object>` | Extensible connection metadata | `new()` |
| `IsActive` | `bool` | Whether the connection is currently active | `true` |

**Validation rules**:
- `ConnectionId` MUST be non-empty
- `UserId` MUST be non-empty
- `ConnectedAt` MUST be ≤ `LastActivityAt`

```csharp
/// <summary>
/// Tracks a client connection's state and conversation memberships.
/// </summary>
public class ConnectionInfo
{
    public string ConnectionId { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = string.Empty;
    public DateTimeOffset ConnectedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastActivityAt { get; set; } = DateTimeOffset.UtcNow;
    public HashSet<string> Conversations { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
    public bool IsActive { get; set; } = true;
}
```

---

### 3. IncomingMessage (NEW)

**Location**: `src/Ato.Copilot.Channels/Models/IncomingMessage.cs`

An inbound message from any client channel.

| Field | Type | Description | Default |
|-------|------|-------------|---------|
| `ConnectionId` | `string` | Originating connection ID | `""` |
| `ConversationId` | `string` | Target conversation ID | `""` |
| `Content` | `string` | Message text | `""` |
| `TargetAgentType` | `string?` | Explicit agent routing hint | `null` |
| `Attachments` | `List<MessageAttachment>` | File attachments | `new()` |
| `Metadata` | `Dictionary<string, object>` | Request metadata (e.g., source platform) | `new()` |
| `Timestamp` | `DateTimeOffset` | When the message was sent | `DateTimeOffset.UtcNow` |

**Validation rules**:
- `ConnectionId` MUST be non-empty
- `ConversationId` MUST be non-empty
- `Content` MUST be non-empty (no blank messages)

```csharp
/// <summary>
/// An inbound message from any client channel.
/// </summary>
public class IncomingMessage
{
    public string ConnectionId { get; set; } = string.Empty;
    public string ConversationId { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? TargetAgentType { get; set; }
    public List<MessageAttachment> Attachments { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}
```

---

### 4. StreamChunk (NEW)

**Location**: `src/Ato.Copilot.Channels/Models/StreamChunk.cs`

A typed chunk in a streaming response.

| Field | Type | Description | Default |
|-------|------|-------------|---------|
| `SequenceNumber` | `long` | Incrementing sequence number | `0` |
| `Content` | `string` | Chunk text content | `""` |
| `ContentType` | `StreamContentType` | Content type classification | `StreamContentType.Text` |

```csharp
/// <summary>
/// A typed chunk in a streaming response.
/// </summary>
public class StreamChunk
{
    public long SequenceNumber { get; set; }
    public string Content { get; set; } = string.Empty;
    public StreamContentType ContentType { get; set; } = StreamContentType.Text;
}
```

---

### 5. MessageAttachment (NEW)

**Location**: `src/Ato.Copilot.Channels/Models/MessageAttachment.cs`

File attachment on an incoming message.

| Field | Type | Description | Default |
|-------|------|-------------|---------|
| `Name` | `string` | File name | `""` |
| `ContentType` | `string` | MIME type | `"application/octet-stream"` |
| `Url` | `string?` | URL to retrieve attachment | `null` |
| `Data` | `byte[]?` | Inline binary content | `null` |
| `Size` | `long` | File size in bytes | `0` |

**Validation rules**:
- `Name` MUST be non-empty
- At least one of `Url` or `Data` MUST be non-null

```csharp
/// <summary>
/// File attachment on an incoming message.
/// </summary>
public class MessageAttachment
{
    public string Name { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public string? Url { get; set; }
    public byte[]? Data { get; set; }
    public long Size { get; set; }
}
```

---

### 6. ChannelOptions (NEW)

**Location**: `src/Ato.Copilot.Channels/Configuration/ChannelOptions.cs`

Configuration for channel behavior.

| Field | Type | Description | Default |
|-------|------|-------------|---------|
| `EnableConnectionLogging` | `bool` | Log connection/disconnect events | `true` |
| `IdleConnectionTimeout` | `TimeSpan` | Auto-cleanup threshold | `TimeSpan.FromMinutes(30)` |
| `IdleCleanupInterval` | `TimeSpan` | How often to check for idle connections | `TimeSpan.FromMinutes(5)` |
| `MaxConnectionsPerUser` | `int` | Maximum concurrent connections per user | `10` |
| `EnableMessageAcknowledgment` | `bool` | Whether to ACK messages | `false` |
| `DefaultHandlerBehavior` | `DefaultHandlerBehavior` | Echo or Error when no AgentInvoker | `DefaultHandlerBehavior.Echo` |
| `Streaming` | `StreamingOptions` | Nested streaming configuration | `new()` |

```csharp
/// <summary>
/// Configuration for channel behavior.
/// </summary>
public class ChannelOptions
{
    public const string SectionName = "Channels";

    public bool EnableConnectionLogging { get; set; } = true;
    public TimeSpan IdleConnectionTimeout { get; set; } = TimeSpan.FromMinutes(30);
    public TimeSpan IdleCleanupInterval { get; set; } = TimeSpan.FromMinutes(5);
    public int MaxConnectionsPerUser { get; set; } = 10;
    public bool EnableMessageAcknowledgment { get; set; }
    public DefaultHandlerBehavior DefaultHandlerBehavior { get; set; } = DefaultHandlerBehavior.Echo;
    public StreamingOptions Streaming { get; set; } = new();
}

/// <summary>
/// Configuration for streaming behavior.
/// </summary>
public class StreamingOptions
{
    public TimeSpan StreamTimeout { get; set; } = TimeSpan.FromMinutes(5);
    public int MaxChunkSize { get; set; } = 4096;
    public bool EnableAutoComplete { get; set; } = true;
}
```

---

## Enums

**Location**: `src/Ato.Copilot.Channels/Models/ChannelEnums.cs`

```csharp
/// <summary>
/// Transport mechanism for the channel.
/// </summary>
public enum ChannelType
{
    SignalR,
    WebSocket,
    LongPolling,
    ServerSentEvents
}

/// <summary>
/// Classification of a channel message.
/// </summary>
public enum MessageType
{
    UserMessage,
    AgentResponse,
    AgentThinking,
    ToolExecution,
    ToolResult,
    Error,
    SystemNotification,
    ProgressUpdate,
    ConfirmationRequest,
    StreamChunk
}

/// <summary>
/// Content type classification for streaming chunks.
/// </summary>
public enum StreamContentType
{
    Text,
    Code,
    Markdown,
    Json,
    Tool,
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
```

---

## Interfaces

### IChannel

**Location**: `src/Ato.Copilot.Channels/Abstractions/IChannel.cs`

```csharp
/// <summary>
/// Transport abstraction for sending messages through a specific channel mechanism.
/// </summary>
public interface IChannel
{
    /// <summary>Send a message to a specific connection.</summary>
    Task SendAsync(string connectionId, ChannelMessage message, CancellationToken ct = default);

    /// <summary>Send a message to all connections in a conversation group.</summary>
    Task SendToConversationAsync(string conversationId, ChannelMessage message, CancellationToken ct = default);

    /// <summary>Send a message to all connected clients.</summary>
    Task BroadcastAsync(ChannelMessage message, CancellationToken ct = default);

    /// <summary>Check if a connection is currently active.</summary>
    Task<bool> IsConnectedAsync(string connectionId, CancellationToken ct = default);
}
```

### IChannelManager

**Location**: `src/Ato.Copilot.Channels/Abstractions/IChannelManager.cs`

```csharp
/// <summary>
/// Manages connections, conversation groups, and message routing.
/// </summary>
public interface IChannelManager
{
    /// <summary>Register a new connection.</summary>
    Task<ConnectionInfo> RegisterConnectionAsync(string userId, Dictionary<string, object>? metadata = null, CancellationToken ct = default);

    /// <summary>Unregister a connection and remove from all conversation groups.</summary>
    Task UnregisterConnectionAsync(string connectionId, CancellationToken ct = default);

    /// <summary>Add a connection to a conversation group.</summary>
    Task JoinConversationAsync(string connectionId, string conversationId, CancellationToken ct = default);

    /// <summary>Remove a connection from a conversation group.</summary>
    Task LeaveConversationAsync(string connectionId, string conversationId, CancellationToken ct = default);

    /// <summary>Send a message to all connections in a conversation.</summary>
    Task SendToConversationAsync(string conversationId, ChannelMessage message, CancellationToken ct = default);

    /// <summary>Check if a connection is registered and active.</summary>
    Task<bool> IsConnectedAsync(string connectionId, CancellationToken ct = default);

    /// <summary>Get info for a specific connection.</summary>
    Task<ConnectionInfo?> GetConnectionInfoAsync(string connectionId, CancellationToken ct = default);

    /// <summary>Get all active connections (for idle cleanup enumeration).</summary>
    IEnumerable<ConnectionInfo> GetAllConnections();
}
```

### IMessageHandler

**Location**: `src/Ato.Copilot.Channels/Abstractions/IMessageHandler.cs`

```csharp
/// <summary>
/// Processes incoming messages and produces channel responses.
/// </summary>
public interface IMessageHandler
{
    /// <summary>Handle an incoming message and return a response.</summary>
    Task<ChannelMessage> HandleMessageAsync(IncomingMessage message, CancellationToken ct = default);
}
```

### IStreamingHandler and IStreamContext

**Location**: `src/Ato.Copilot.Channels/Abstractions/IStreamingHandler.cs`

```csharp
/// <summary>
/// Initiates streaming responses through a channel.
/// </summary>
public interface IStreamingHandler
{
    /// <summary>Begin a new streaming response for a conversation.</summary>
    Task<IStreamContext> BeginStreamAsync(string conversationId, string? agentType = null, CancellationToken ct = default);
}

/// <summary>
/// Context for an active streaming response. Must be disposed to finalize the stream.
/// </summary>
public interface IStreamContext : IAsyncDisposable
{
    /// <summary>Current stream ID.</summary>
    string StreamId { get; }

    /// <summary>Write a text chunk to the stream.</summary>
    Task WriteAsync(string content, CancellationToken ct = default);

    /// <summary>Write a typed chunk to the stream.</summary>
    Task WriteAsync(StreamChunk chunk, CancellationToken ct = default);

    /// <summary>Complete the stream and deliver aggregated content.</summary>
    Task CompleteAsync(CancellationToken ct = default);

    /// <summary>Abort the stream and deliver an error message.</summary>
    Task AbortAsync(string error, CancellationToken ct = default);
}
```

---

## Implementation Notes

### InMemoryChannel Internal State

```csharp
// Connection registry (R1)
private readonly ConcurrentDictionary<string, ConnectionInfo> _connections = new();

// Conversation groups — ImmutableHashSet for lock-free reads (R1)
private readonly ConcurrentDictionary<string, ImmutableHashSet<string>> _conversationGroups = new();
```

### DefaultMessageHandler AgentInvoker

The `DefaultMessageHandler` accepts an optional `Func<IncomingMessage, CancellationToken, Task<ChannelMessage>>` delegate called `AgentInvoker`. This is configured at the DI composition root — the consumer of the Channels library provides the invoker that bridges to the MCP Server or agent layer.

```csharp
public class DefaultMessageHandler : IMessageHandler
{
    private readonly IChannelManager _channelManager;
    private readonly IConversationStateManager _conversationState;
    private readonly Func<IncomingMessage, CancellationToken, Task<ChannelMessage>>? _agentInvoker;
    private readonly IOptions<ChannelOptions> _options;
    private readonly ILogger<DefaultMessageHandler> _logger;
}
```

### DI Extension Methods

**Note**: `StreamingHandler` (implements `IStreamingHandler`) is a factory class that creates `StreamContext` instances. It lives in `StreamingHandler.cs`, while `StreamContext` (implements `IStreamContext`) lives in `StreamContext.cs`.

```csharp
public static class ChannelServiceExtensions
{
    /// <summary>Full registration with configuration binding.</summary>
    public static IServiceCollection AddChannels(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ChannelOptions>(configuration.GetSection(ChannelOptions.SectionName));
        services.AddSingleton<IChannel, InMemoryChannel>();
        services.AddSingleton<IChannelManager, ChannelManager>();
        services.AddSingleton<IStreamingHandler, StreamingHandler>();
        services.AddScoped<IMessageHandler, DefaultMessageHandler>();
        services.AddHostedService<IdleConnectionCleanupService>();
        return services;
    }

    /// <summary>Simplified registration for testing (defaults only, no config binding).</summary>
    public static IServiceCollection AddInMemoryChannels(this IServiceCollection services)
    {
        services.AddSingleton(Options.Create(new ChannelOptions()));
        services.AddSingleton<IChannel, InMemoryChannel>();
        services.AddSingleton<IChannelManager, ChannelManager>();
        services.AddSingleton<IStreamingHandler, StreamingHandler>();
        services.AddScoped<IMessageHandler, DefaultMessageHandler>();
        return services;
    }
}
```

---

## Relationships Summary

| Parent | Child | Cardinality | Mechanism |
|--------|-------|-------------|-----------|
| ChannelManager | ConnectionInfo | 1:N | `ConcurrentDictionary<string, ConnectionInfo>` |
| ChannelManager | ConversationGroup | 1:N | `ConcurrentDictionary<string, ImmutableHashSet<string>>` |
| ConversationGroup | ConnectionInfo | N:M | Connection IDs in `ImmutableHashSet` |
| IncomingMessage | MessageAttachment | 1:N | `List<MessageAttachment>` |
| ChannelOptions | StreamingOptions | 1:1 | Nested object |
| DefaultMessageHandler | IConversationStateManager | dependency | Constructor injection |
| IdleConnectionCleanupService | IChannelManager | dependency | Constructor injection |

---

## No EF Core / Database Changes

The Channels library is entirely in-memory. No database entities, migrations, or `DbContext` changes are required. Message persistence is delegated to `IConversationStateManager` (from `Ato.Copilot.State`), which is injected into `DefaultMessageHandler` — the Channels library does not own the persistence layer.
