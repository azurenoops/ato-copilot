# Channel Interfaces Contract: Copilot Everywhere — .NET Channels Library

**Feature**: 013-copilot-everywhere  
**Date**: 2026-02-26  
**Status**: Complete

---

## Overview

The Channels library (`Ato.Copilot.Channels`) exposes four public interfaces: `IChannel`, `IChannelManager`, `IMessageHandler`, and `IStreamingHandler`/`IStreamContext`. These are consumed by the MCP Server, Chat App, or any host application to route messages between clients and the agent layer.

---

## Interface: `IChannel`

Transport abstraction. The `InMemoryChannel` implementation stores connections and groups in `ConcurrentDictionary` collections.

### `SendAsync(connectionId, message, ct)`

Send a message to a specific connection.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `connectionId` | `string` | yes | Target connection ID |
| `message` | `ChannelMessage` | yes | Message payload |
| `ct` | `CancellationToken` | no | Cancellation token |

**Behavior**:
- If `connectionId` is not registered → log warning, no exception (FR-018)
- Updates `LastActivityAt` on the target connection (FR-009)

### `SendToConversationAsync(conversationId, message, ct)`

Send to all connections in a conversation group.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `conversationId` | `string` | yes | Conversation group ID |
| `message` | `ChannelMessage` | yes | Message payload |
| `ct` | `CancellationToken` | no | Cancellation token |

**Behavior**:
- If group has no connections → silently drop (edge case)
- Pre-filters inactive connections; try-catches per send (R5)
- Updates `LastActivityAt` on each successfully sent connection

### `BroadcastAsync(message, ct)`

Send to all connected clients.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `message` | `ChannelMessage` | yes | Message payload |
| `ct` | `CancellationToken` | no | Cancellation token |

### `IsConnectedAsync(connectionId, ct)`

Check if a connection is registered and active.

**Returns**: `Task<bool>`

---

## Interface: `IChannelManager`

Connection lifecycle and message routing.

### `RegisterConnectionAsync(userId, metadata, ct)`

Register a new client connection.

**Returns**: `Task<ConnectionInfo>` — new connection with unique ID, timestamps, empty conversation list.

**Behavior**:
- Generates `ConnectionId` via `Guid.NewGuid().ToString()`
- Sets `ConnectedAt` and `LastActivityAt` to `DateTimeOffset.UtcNow`
- Logs connection event if `ChannelOptions.EnableConnectionLogging` is true

### `UnregisterConnectionAsync(connectionId, ct)`

Remove a connection from all conversation groups and mark inactive.

**Behavior**:
- Removes connection from **all** conversation groups (FR-007)
- Removes empty group entries when last connection leaves (FR-008)
- Sets `IsActive = false` on `ConnectionInfo`
- Logs disconnection event

### `JoinConversationAsync(connectionId, conversationId, ct)`

Add a connection to a conversation group.

**Behavior**:
- Creates group if first member
- Adds `conversationId` to `connection.Conversations`
- Uses `ImmutableHashSet.Add` — idempotent if already joined

### `LeaveConversationAsync(connectionId, conversationId, ct)`

Remove connection from a conversation group.

**Behavior**:
- Removes `conversationId` from `connection.Conversations`
- Cleans up empty group entry if last member leaves (FR-008)

### `SendToConversationAsync(conversationId, message, ct)`

Delegates to `IChannel.SendToConversationAsync`.

### `IsConnectedAsync(connectionId, ct)`

**Returns**: `Task<bool>` — true if connection exists and `IsActive` is true.

### `GetConnectionInfoAsync(connectionId, ct)`

**Returns**: `Task<ConnectionInfo?>` — null if not found.

### `GetAllConnections()`

**Returns**: `IEnumerable<ConnectionInfo>` — all registered connections (for idle cleanup enumeration).

---

## Interface: `IMessageHandler`

### `HandleMessageAsync(message, ct)`

Process an incoming message and return a response.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `message` | `IncomingMessage` | yes | Inbound message |
| `ct` | `CancellationToken` | no | Cancellation token |

**Returns**: `Task<ChannelMessage>`

**DefaultMessageHandler behavior** (FR-011, FR-012):

1. **With AgentInvoker configured**:
   - Store user message via `IConversationStateManager.SaveConversationAsync`
   - Send `AgentThinking` notification to conversation
   - Call `agentInvoker(message, ct)` to get agent response
   - Store response as assistant message
   - Return response `ChannelMessage`

2. **Without AgentInvoker, `DefaultHandlerBehavior.Echo`** (default):
   - Return `ChannelMessage { Type = AgentResponse, Content = message.Content }`

3. **Without AgentInvoker, `DefaultHandlerBehavior.Error`**:
   - Return `ChannelMessage { Type = Error, Content = "No agent invoker configured" }`

4. **On exception** (regardless of setting):
   - Return `ChannelMessage { Type = Error, Content = ex.Message }`

---

## Interface: `IStreamingHandler` / `IStreamContext`

### `BeginStreamAsync(conversationId, agentType, ct)`

Create a new streaming context.

**Returns**: `Task<IStreamContext>` — disposable stream context.

### `IStreamContext.WriteAsync(content, ct)` / `WriteAsync(chunk, ct)`

Write a text or typed chunk to the stream.

**Behavior**:
- Auto-increments sequence number via `Interlocked.Increment` (R3)
- Appends content to internal `StringBuilder` buffer
- Sends `StreamChunk` message to conversation with sequence number and `IsStreaming = true`

### `IStreamContext.CompleteAsync(ct)`

Finalize stream and send aggregated content.

**Behavior**:
- Uses `Interlocked.CompareExchange` for exactly-once semantics (R4)
- Sends final `ChannelMessage { Content = buffer, IsStreaming = false, IsComplete = true }`
- Idempotent — second call is no-op

### `IStreamContext.AbortAsync(error, ct)`

Abort stream and send error.

**Behavior**:
- Uses `Interlocked.CompareExchange` for exactly-once semantics
- Sends `ChannelMessage { Type = Error, Content = error, IsComplete = true }`

### `IStreamContext.DisposeAsync()`

FR-016 safety net — calls `CompleteAsync` if not already completed or aborted.

---

## DI Registration

### `AddChannels(IConfiguration)`

Full registration with configuration binding from `Channels` section.

```csharp
services.AddChannels(configuration);
// Registers: IChannel, IChannelManager, IStreamingHandler, IMessageHandler (scoped),
//            IdleConnectionCleanupService (hosted), ChannelOptions (from config)
```

### `AddInMemoryChannels()`

Simplified registration for unit tests — default options, no hosted service.

```csharp
services.AddInMemoryChannels();
// Registers: IChannel, IChannelManager, IStreamingHandler, IMessageHandler (scoped),
//            ChannelOptions (defaults)
```

---

## Configuration Schema

```json
{
  "Channels": {
    "EnableConnectionLogging": true,
    "IdleConnectionTimeout": "00:30:00",
    "IdleCleanupInterval": "00:05:00",
    "MaxConnectionsPerUser": 10,
    "EnableMessageAcknowledgment": false,
    "DefaultHandlerBehavior": "Echo",
    "Streaming": {
      "StreamTimeout": "00:05:00",
      "MaxChunkSize": 4096,
      "EnableAutoComplete": true
    }
  }
}
```
