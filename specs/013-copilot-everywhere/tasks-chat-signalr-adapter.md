# Tasks: Chat App — SignalR Channel Adapter Integration

**Input**: Feature 013-copilot-everywhere Channels library + existing Chat app (`Ato.Copilot.Chat`)
**Purpose**: Bridge the Chat app's SignalR-based messaging with the Channels library abstractions so both the Chat app and external extensions (VS Code, M365) share the same `IChannel`/`IChannelManager`/`IMessageHandler` contracts.

**Scope**: Internal adapter wiring — no new user-facing functionality. The Chat app continues to work identically; the Channels abstractions sit underneath.

## Current State

| Concern | Chat App (today) | Channels Library |
|---------|-------------------|-----------------|
| Transport | `Clients.Group().SendAsync()` inline in ChatHub | `IChannel.SendToConversationAsync(ChannelMessage)` |
| Connections | SignalR `Context.ConnectionId`, logging only | `IChannelManager.Register/Unregister` with `ConnectionInfo` |
| Groups | `Groups.AddToGroupAsync` directly | `IChannelManager.Join/LeaveConversationAsync` |
| Message handling | `IChatService.SendMessageAsync` → HTTP to MCP | `IMessageHandler.HandleMessageAsync(IncomingMessage)` |
| Models | `SendMessageRequest` / `ChatResponse` | `IncomingMessage` / `ChannelMessage` |
| DI | No Channels reference | `services.AddChannels(config)` available |

## Phase 1: Adapter Components

- [x] T1 Add `ProjectReference` to `Ato.Copilot.Channels` in `src/Ato.Copilot.Chat/Ato.Copilot.Chat.csproj`

- [x] T2 Create `SignalRChannel : IChannel` in `src/Ato.Copilot.Chat/Channels/SignalRChannel.cs`
  - Constructor: `IHubContext<ChatHub>`, `ILogger<SignalRChannel>`
  - `SendAsync(connectionId, ChannelMessage)` → `Clients.Client(connectionId).SendAsync("MessageReceived", mapped)`
  - `SendToConversationAsync(conversationId, ChannelMessage)` → `Clients.Group(conversationId).SendAsync("MessageReceived", mapped)`
  - `BroadcastAsync(ChannelMessage)` → `Clients.All.SendAsync("MessageReceived", mapped)`
  - `IsConnectedAsync(connectionId)` → delegate to connection tracker (T3)
  - Map `ChannelMessage` → anonymous object matching existing SignalR event shape (`id`, `conversationId`, `content`, `role`, `timestamp`, `metadata`)

- [x] T3 Create `SignalRConnectionTracker` in `src/Ato.Copilot.Chat/Channels/SignalRConnectionTracker.cs`
  - `ConcurrentDictionary<string, ConnectionInfo>` keyed by SignalR `Context.ConnectionId`
  - `RegisterConnection(string connectionId, string userId)` → creates `ConnectionInfo`
  - `UnregisterConnection(string connectionId)` → removes + sets `IsActive = false`
  - `GetConnectionInfo(string connectionId)` → lookup
  - `GetAllConnections()` → snapshot enumeration
  - `IsConnected(string connectionId)` → exists + `IsActive`
  - Used by both `SignalRChannel.IsConnectedAsync` and `SignalRChannelManager`

- [x] T4 Create `SignalRChannelManager : IChannelManager` in `src/Ato.Copilot.Chat/Channels/SignalRChannelManager.cs`
  - Constructor: `SignalRConnectionTracker`, `IHubContext<ChatHub>`, `ILogger<SignalRChannelManager>`
  - `RegisterConnectionAsync` → delegates to `SignalRConnectionTracker.RegisterConnection`
  - `UnregisterConnectionAsync` → delegates to tracker + removes from all SignalR groups (via stored conversation set)
  - `JoinConversationAsync` → `HubContext.Groups.AddToGroupAsync` + track in `ConnectionInfo.Conversations`
  - `LeaveConversationAsync` → `HubContext.Groups.RemoveFromGroupAsync` + remove from set
  - `SendToConversationAsync` → delegates to `SignalRChannel.SendToConversationAsync`
  - `IsConnectedAsync`, `GetConnectionInfoAsync`, `GetAllConnections` → delegates to tracker

- [x] T5 Create `ChatMessageMapper` in `src/Ato.Copilot.Chat/Channels/ChatMessageMapper.cs`
  - `ToIncomingMessage(SendMessageRequest, string connectionId)` → `IncomingMessage`
    - Maps: `ConnectionId`, `ConversationId`, `Content` (via `GetContent()`), `TargetAgentType` (from context), `Metadata`, `Timestamp`
  - `ToChatResponse(ChannelMessage)` → `ChatResponse`
    - Maps: `MessageId`, `Content`, `Success` (Type != Error), `Error` (if Error type), `Metadata`
  - `ToChannelMessage(ChatResponse, string conversationId)` → `ChannelMessage`
    - Maps: `MessageId`, `ConversationId`, `Type` (AgentResponse or Error), `Content`, `Timestamp`, `Metadata`
  - `ToSignalRPayload(ChannelMessage)` → anonymous object matching existing hub event shape

- [x] T6 Create `ChatServiceMessageHandler : IMessageHandler` in `src/Ato.Copilot.Chat/Channels/ChatServiceMessageHandler.cs`
  - Constructor: `IChatService`, `ILogger<ChatServiceMessageHandler>`
  - `HandleMessageAsync(IncomingMessage)`:
    1. Map `IncomingMessage` → `SendMessageRequest` via `ChatMessageMapper`
    2. Call `IChatService.SendMessageAsync(request)`
    3. Map `ChatResponse` → `ChannelMessage` via `ChatMessageMapper`
    4. Return `ChannelMessage`
  - This preserves the existing MCP HTTP call pipeline — the handler just wraps it behind the Channels interface

## Phase 2: Integration Wiring

- [x] T7 Update `ChatHub` to use `SignalRChannelManager` in `src/Ato.Copilot.Chat/Hubs/ChatHub.cs`
  - Inject `IChannelManager` (resolved as `SignalRChannelManager`)
  - `OnConnectedAsync` → call `IChannelManager.RegisterConnectionAsync(userId)` (extract userId from `Context.User` or `Context.ConnectionId`)
  - `OnDisconnectedAsync` → call `IChannelManager.UnregisterConnectionAsync(Context.ConnectionId)`
  - `JoinConversation` → delegate to `IChannelManager.JoinConversationAsync`
  - `LeaveConversation` → delegate to `IChannelManager.LeaveConversationAsync`
  - `SendMessage` → map to `IncomingMessage`, call `IMessageHandler.HandleMessageAsync`, send result via `IChannel.SendToConversationAsync`
  - Keep `NotifyTyping` as-is (pass-through, no channel abstraction needed)
  - Keep existing SignalR event names (`MessageProcessing`, `MessageReceived`, `MessageError`) for backward compat

- [x] T8 Register adapter services in `src/Ato.Copilot.Chat/Program.cs`
  - Add `services.AddSingleton<SignalRConnectionTracker>()`
  - Add `services.AddSingleton<IChannel, SignalRChannel>()` (replaces InMemoryChannel)
  - Add `services.AddSingleton<IChannelManager, SignalRChannelManager>()`
  - Add `services.AddScoped<IMessageHandler, ChatServiceMessageHandler>()`
  - Do NOT call `services.AddChannels()` (that registers InMemoryChannel — we want SignalRChannel)
  - Optionally register `IConversationStateManager` adapter if needed for the Channels `DefaultMessageHandler` (may not be needed since we're using `ChatServiceMessageHandler` instead)

## Phase 3: Tests

- [x] T9 [P] Create `SignalRChannelTests` in `tests/Ato.Copilot.Tests.Unit/Chat/SignalRChannelTests.cs`
  - `SendAsync` calls `Clients.Client().SendAsync` with correct event name and mapped payload
  - `SendToConversationAsync` calls `Clients.Group().SendAsync`
  - `BroadcastAsync` calls `Clients.All.SendAsync`
  - `IsConnectedAsync` returns true for registered, false for unknown
  - Mock `IHubContext<ChatHub>` and verify method calls

- [x] T10 [P] Create `SignalRChannelManagerTests` in `tests/Ato.Copilot.Tests.Unit/Chat/SignalRChannelManagerTests.cs`
  - `RegisterConnectionAsync` creates `ConnectionInfo` with generated ID and tracks in connection tracker
  - `UnregisterConnectionAsync` removes connection and sets `IsActive = false`
  - `JoinConversationAsync` adds to SignalR group + tracks in `ConnectionInfo.Conversations`
  - `LeaveConversationAsync` removes from SignalR group + removes from conversation set
  - `GetConnectionInfoAsync` returns null for unknown connection

- [x] T11 [P] Create `ChatMessageMapperTests` in `tests/Ato.Copilot.Tests.Unit/Chat/ChatMessageMapperTests.cs`
  - `SendMessageRequest` → `IncomingMessage` maps all fields correctly
  - `ChatResponse` (success) → `ChannelMessage` with `MessageType.AgentResponse`
  - `ChatResponse` (error) → `ChannelMessage` with `MessageType.Error`
  - `ChannelMessage` → SignalR payload matches existing event shape
  - Round-trip: request → incoming → handler → channel message → response preserves data

- [x] T12 [P] Create `ChatServiceMessageHandlerTests` in `tests/Ato.Copilot.Tests.Unit/Chat/ChatServiceMessageHandlerTests.cs`
  - Delegates to `IChatService.SendMessageAsync` with mapped request
  - Returns mapped `ChannelMessage` on success
  - Returns `ChannelMessage` with `MessageType.Error` on exception
  - Mock `IChatService` to verify call delegation

## Phase 4: Validation

- [x] T13 Verify full solution builds: `dotnet build Ato.Copilot.sln` — 0 errors
- [x] T14 Run all tests: `dotnet test` — no regressions in existing Chat or Channels tests
- [x] T15 Smoke test: ChatHub still works identically via SignalR (same event names, same payloads) — backward compatible

---

## Dependencies

- T1 → T2, T3, T4, T5, T6 (project reference needed first)
- T2, T3 → T4 (channel manager wraps both)
- T5 → T6 (mapper used by handler)
- T2, T3, T4, T5, T6 → T7, T8 (all adapters before wiring)
- T7, T8 → T9, T10, T11, T12 (wiring before tests)
- T9–T12 → T13, T14, T15 (tests before validation)

## Task Count

| Phase | Tasks |
|-------|-------|
| Phase 1: Adapters | 6 |
| Phase 2: Wiring | 2 |
| Phase 3: Tests | 4 |
| Phase 4: Validation | 3 |
| **Total** | **15** |

## Notes

- `SignalRChannel` replaces `InMemoryChannel` for the Chat app — the `InMemoryChannel` remains the default for extensions (VS Code, M365) and unit tests
- The existing `IChatService` → MCP HTTP pipeline is preserved; `ChatServiceMessageHandler` just wraps it behind `IMessageHandler`
- Backward compatibility is critical: SignalR event names (`MessageReceived`, `MessageError`, `MessageProcessing`, `MessageProgress`, `UserTyping`) must not change
- `NotifyTyping` stays outside the channel abstraction (simple passthrough, not worth abstracting)
