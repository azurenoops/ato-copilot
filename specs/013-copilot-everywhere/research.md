# Research: Copilot Everywhere — Multi-Channel Extensions & Channels Library

**Feature**: 013-copilot-everywhere  
**Date**: 2026-02-26  
**Status**: Complete — all unknowns resolved

---

## Research Tasks

### R1: Concurrent Data Structures for InMemoryChannel Connection/Group Management

**Decision**: Use `ConcurrentDictionary<string, ConnectionInfo>` for connections and `ConcurrentDictionary<string, ImmutableHashSet<string>>` for conversation groups.

**Rationale**: `ImmutableHashSet<string>` provides lock-free reads and snapshot iteration for broadcast operations. `ConcurrentDictionary.AddOrUpdate` handles the compare-exchange loop internally. When broadcasting to a conversation group, the current `ImmutableHashSet` reference is grabbed and iterated without any lock — adds/removes create new set instances via structural sharing (O(log n)), negligible at 100 connections.

**Alternatives considered**:
- `ConcurrentBag<string>` → Rejected: no `Remove` method, allows duplicates — fatal for set membership
- `lock` + `HashSet<string>` → Rejected: coarser locking, requires per-group lock objects, risk of contention during broadcast while members join/leave

---

### R2: Idle Connection Cleanup Strategy

**Decision**: `PeriodicTimer` inside a `BackgroundService.ExecuteAsync`, exactly matching the pattern used in 5+ existing hosted services in the codebase (`RetentionCleanupHostedService`, `OverdueScanHostedService`, `SessionCleanupHostedService`, etc.).

**Rationale**: `PeriodicTimer` (introduced .NET 6) is async-native: `await timer.WaitForNextTickAsync(stoppingToken)` naturally integrates with `CancellationToken` and `BackgroundService`. No callback marshaling, no timer disposal races, no risk of overlapping ticks (the next tick doesn't start until the previous one completes). The cleanup interval should be a fraction of the idle timeout (e.g., every 5 minutes when timeout is 30 minutes) so connections are cleaned up promptly.

**Alternatives considered**:
- `System.Threading.Timer` → Rejected: fires callbacks on thread-pool threads, requires `SemaphoreSlim` for re-entrancy protection, harder `CancellationToken` integration — more code, more edge cases, zero benefit

---

### R3: Thread-Safe Streaming Sequence Numbers

**Decision**: `Interlocked.Increment(ref long _sequenceNumber)` per `StreamContext` instance.

**Rationale**: Each `StreamContext` is scoped to a single streaming operation. A per-instance `long` field with `Interlocked.Increment` is the simplest correct approach — full memory barrier, returns incremented value atomically. Using `long` avoids theoretical `int` overflow at zero additional cost.

**Alternatives considered**:
- `lock` around an int counter → Rejected: unnecessarily heavy for a single increment
- Atomic wrapper class → Rejected: extra allocation with no benefit over `Interlocked`

---

### R4: StreamContext `IAsyncDisposable` Pattern

**Decision**: Implement `IAsyncDisposable` with `Interlocked.CompareExchange` guard on a `_completed` flag. `DisposeAsync` calls `CompleteAsync` if not already completed/aborted (FR-016 safety net). No `IDisposable` dual implementation (no unmanaged resources).

**Rationale**: 
- `Interlocked.CompareExchange(ref _completed, 1, 0)` ensures exactly-once completion even under concurrent dispose + explicit complete
- `DisposeAsync` wraps `CompleteAsync` in try-catch because async disposal must not throw
- Callers use `await using var stream = await handler.BeginStreamAsync(...)`
- No `GC.SuppressFinalize` needed — no finalizer, no unmanaged resources

**Alternatives considered**:
- Manual `bool _disposed` flag with `lock` → Rejected: `Interlocked.CompareExchange` is simpler, lock-free, and correct for the single-transition case

---

### R5: Inactive Connection Send Behavior

**Decision**: Pre-filter with `IsConnectedAsync` check **plus** try-catch per send as a safety net (FR-018: "log warning, no exception").

**Rationale**: Pre-filter alone is racey — a connection can disconnect between the `IsConnectedAsync` check and the `SendAsync` call. Try-catch alone is wasteful — why attempt sends to known-dead connections? Both together: pre-filter avoids most dead sends (O(1) dictionary lookup); try-catch catches the race window. The `ImmutableHashSet` snapshot guarantees iteration stability even as other threads modify group membership.

**Alternatives considered**:
- Pre-filter only → Rejected: race condition between check and send
- Try-catch only → Rejected: unnecessary exception overhead for known-dead connections

---

### R6: Relationship Between Channels Library and Existing Chat App SignalR Hub

**Decision**: The Channels library is a **parallel abstraction** — not a replacement for the existing `ChatHub`. The existing Chat app (`Ato.Copilot.Chat`) will continue using its direct `ChatHub → ChatService → MCP Server` pipeline unchanged. The Channels library provides the foundation for the VS Code and M365 extensions to communicate via a unified interface. A future feature may migrate the Chat app to use Channels.

**Rationale**: The Chat app's `ChatHub` at `src/Ato.Copilot.Chat/Hubs/ChatHub.cs` uses SignalR groups keyed by `conversationId`, with `SendMessage` dispatching to `IChatService` which POSTs to `/mcp/chat/stream`. It has its own message models (`SendMessageRequest`, `ChatMessage`). Refactoring the Chat app to use the Channels library would risk regressions in a working system with no immediate benefit — the Chat app already works. Instead, the Channels library introduces `IChannel`/`IChannelManager` abstractions that *could* wrap the Chat Hub in the future but are initially consumed only by `InMemoryChannel` for testing and the new extensions.

**Alternatives considered**:
- Refactor Chat app to use Channels immediately → Rejected: risk of regressions, scope creep, no immediate user benefit

---

### R7: VS Code GitHub Copilot Chat Participant API Pattern

**Decision**: Use the `@vscode/chat` API (stable in VS Code 1.90+) to register a `ChatParticipant` with `id: "ato"` and `isSticky: true`. The participant handler receives `ChatRequest` objects with `prompt`, `command` (slash command), and `ChatContext` (history). Responses are streamed via `ChatResponseStream.markdown()`.

**Rationale**: The VS Code Chat Participant API has been stable since VS Code 1.90 (mid-2024). Key API surface:
- `vscode.chat.createChatParticipant("ato", handler)` — registers the participant
- Handler signature: `(request: ChatRequest, context: ChatContext, stream: ChatResponseStream, token: CancellationToken) => Promise<ChatResult>`
- `request.command` contains the slash command name (e.g., `"compliance"`)
- `request.prompt` contains the user's message text
- `context.history` provides `ChatRequestTurn[]` and `ChatResponseTurn[]` for multi-turn context
- `stream.markdown(text)` writes Markdown chunks to the chat panel
- `participant.iconPath` sets the participant icon
- Slash commands registered via `participant.subCommands` array

**Alternatives considered**:
- Language Model API (`vscode.lm`) → Rejected: that's for calling LLMs directly, not for creating chat participants
- Chat Extension API (deprecated) → Rejected: replaced by Chat Participant API in VS Code 1.90+

---

### R8: M365 Copilot Declarative Agent Architecture

**Decision**: Build as an Express.js webhook server accepting `POST /api/messages` from the Teams Bot Framework. The server translates incoming messages to MCP Server requests, translates responses to Adaptive Card v1.5 JSON, and returns them. Plugin discovery via `GET /ai-plugin.json` and `GET /openapi.json`.

**Rationale**: The M365 Copilot declarative agent pattern exposes a webhook endpoint that the Teams platform calls. The server does not use the Bot Framework SDK directly (which would add ~20 MB of dependencies) — instead, it handles the simplified webhook payload format directly. The `manifest.json` in the Teams app package declares the webhook URL and plugin metadata.

Key design decisions:
- `POST /api/messages` receives `{ text, conversation: { id }, from: { id } }` and returns Adaptive Card JSON
- Conversation IDs generated in format `m365-{timestamp}-{random9}` for tracking
- Intent-based card routing: parse `intentType` from MCP response, dispatch to appropriate card builder
- Adaptive Card v1.5 for Teams compatibility (v1.6 has limited Teams support)
- `ATOApiClient` wraps `axios` with 300s timeout and `User-Agent` header

**Alternatives considered**:
- Bot Framework SDK for Node.js → Rejected: heavy dependency (~20 MB), complex setup, not needed for simple webhook → MCP proxy pattern
- Azure Bot Service → Rejected: adds Azure resource dependency for what is essentially an HTTP proxy

---

### R9: Channels Library NuGet Dependencies

**Decision**: Add `Microsoft.Extensions.DependencyInjection.Abstractions`, `Microsoft.Extensions.Logging.Abstractions`, `Microsoft.Extensions.Options`, `Microsoft.Extensions.Hosting.Abstractions`, `System.Text.Json`, and `System.Collections.Immutable` to the Channels project.

**Rationale**:
- `DependencyInjection.Abstractions` — for `IServiceCollection` extension methods (`AddChannels`, `AddInMemoryChannels`)
- `Logging.Abstractions` — for `ILogger<T>` in `ChannelManager`, `InMemoryChannel`, `DefaultMessageHandler`, `StreamContext`
- `Options` — for `IOptions<ChannelOptions>` injection
- `Hosting.Abstractions` — for `BackgroundService` (idle cleanup hosted service)
- `System.Text.Json` — for `Dictionary<string, object>` metadata serialization
- `System.Collections.Immutable` — for `ImmutableHashSet<string>` in conversation groups

No project reference to `Ato.Copilot.State` — instead, `IConversationStateManager` is injected into `DefaultMessageHandler` as an interface dependency. The Channels project defines its own models and only depends on the State project through interface injection at the DI composition root.

**Alternatives considered**:
- Adding a project reference to `Ato.Copilot.State` → Rejected: creates a circular dependency risk and couples the generic Channels library to a specific state implementation. Better to inject `IConversationStateManager` at the composition root.

---

## Summary

| # | Research Task | Decision | Key Driver |
|---|---|---|---|
| R1 | Concurrent collections | `ConcurrentDictionary` + `ImmutableHashSet<string>` | Lock-free reads, snapshot iteration |
| R2 | Idle cleanup | `PeriodicTimer` in `BackgroundService` | Matches 5+ existing codebase services |
| R3 | Sequence numbers | `Interlocked.Increment(ref long)` | Simplest correct atomic primitive |
| R4 | StreamContext disposal | `IAsyncDisposable` + `CompareExchange` guard | Exactly-once completion, FR-016 |
| R5 | Inactive sends | Pre-filter + try-catch per send | FR-018, race condition safety |
| R6 | Chat app relationship | Parallel abstraction, no refactor | Avoid regressions, scope control |
| R7 | VS Code Chat API | `ChatParticipant` via `@vscode/chat` | Stable API in VS Code 1.90+ |
| R8 | M365 architecture | Express.js webhook, no Bot Framework SDK | Lightweight proxy, simpler deps |
| R9 | NuGet dependencies | 6 packages, no State project ref | Clean dependency graph |
