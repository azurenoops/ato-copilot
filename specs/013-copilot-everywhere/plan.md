# Implementation Plan: Copilot Everywhere — Multi-Channel Extensions & Channels Library

**Branch**: `013-copilot-everywhere` | **Date**: 2026-02-26 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/013-copilot-everywhere/spec.md`

## Summary

Build a three-component multi-channel extension suite: (1) a .NET Channels class library (`Ato.Copilot.Channels`) providing `IChannel`, `IChannelManager`, `IMessageHandler`, and `IStreamingHandler` abstractions with an `InMemoryChannel` implementation for message routing, connection tracking, and streaming responses; (2) a VS Code extension registering `@ato` as a GitHub Copilot Chat participant with `/compliance`, `/knowledge`, `/config` slash commands, compliance analysis commands, export services, and webview panels; (3) an M365 Copilot declarative agent (Express.js webhook) returning Adaptive Card responses for Teams integration. All three connect to the existing MCP Server's `/mcp/chat` endpoint.

## Technical Context

**Language/Version**: C# 13 / .NET 9.0 (Channels library), TypeScript 5.x (VS Code extension), TypeScript 5.x / Node.js 20 LTS (M365 extension)
**Primary Dependencies**: `Microsoft.Extensions.DependencyInjection.Abstractions`, `Microsoft.Extensions.Logging.Abstractions`, `System.Text.Json` (Channels); `@vscode/chat` API, `axios` (VS Code); `express`, `adaptivecards`, `axios` (M365)
**Storage**: In-memory `ConcurrentDictionary` collections (Channels library); `IConversationStateManager` from `Ato.Copilot.State` (message persistence)
**Testing**: xUnit 2.9 + FluentAssertions + Moq (Channels unit tests); Mocha + Chai (VS Code/M365 tests) | Baseline: ~2364 unit tests passing
**Target Platform**: .NET 9.0 class library (any), VS Code 1.90+ (desktop/web), Node.js 20 LTS (M365 webhook server), Azure Government
**Project Type**: Multi-project: .NET class library + 2 TypeScript extension projects
**Performance Goals**: 100 concurrent connections with reliable delivery (SC-002); streaming chunk delivery with sequence ordering (SC-003); <30s response time excluding MCP processing (SC-001); <60s M365 end-to-end (SC-009)
**Constraints**: Single-instance deployment (InMemoryChannel only — no distributed state); MCP Server must be running for VS Code/M365 extensions; no OAuth implementation (externally configured); idle connection timeout 30 min default
**Scale/Scope**: 100 concurrent connections, 3 slash commands, 4 VS Code commands, 8 Adaptive Card builders, 6 key entities in Channels library

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| # | Principle | Status | Evidence |
|---|-----------|--------|----------|
| I | Documentation as Source of Truth | **PASS** | Feature spec at `specs/013-copilot-everywhere/spec.md` with 5 user stories, 51 FRs (including FR-017a), 10 SCs, 5 clarifications resolved. Plan generates `research.md`, `data-model.md`, `quickstart.md`, `contracts/`. |
| II | BaseAgent/BaseTool Architecture | **N/A** | No new agents or tools. The Channels library provides transport abstractions consumed by existing agents. VS Code extension calls `/mcp/chat` which routes to existing `ComplianceAgent`/`KnowledgeBaseAgent`/`ConfigurationAgent`. |
| III | Testing Standards | **PASS** | Spec requires 90%+ coverage for channel abstractions (SC-010). Channel library fully testable via `InMemoryChannel` without external dependencies. VS Code/M365 extensions have Mocha test suites. Boundary tests: empty messages, null connections, idle timeout, mid-stream disconnect, concurrent sends. |
| IV | Azure Government & Compliance First | **PASS** | M365 Adaptive Cards link to `portal.azure.us` (FR-044). No new Azure service clients created. VS Code extension calls existing MCP Server which already handles Azure Gov credentials. No hardcoded credentials — API key/URL via settings. |
| V | Observability & Structured Logging | **PASS** | `ILogger<T>` injected into `ChannelManager`, `DefaultMessageHandler`, `StreamContext`, `InMemoryChannel`. Connection register/unregister, message routing, stream lifecycle, idle cleanup all logged. FR-018 requires warning-level log for inactive connection skips. |
| VI | Code Quality & Maintainability | **PASS** | Single-responsibility interfaces: `IChannel` (transport), `IChannelManager` (connections/routing), `IMessageHandler` (message processing), `IStreamingHandler` (streaming). All dependencies via constructor injection. XML docs on all public types. `ChannelType`, `MessageType` enums replace magic strings. |
| VII | User Experience Consistency | **PASS** | VS Code extension renders streamed Markdown with agent attribution (FR-028). M365 returns Adaptive Card v1.5 with color-coded scores, action buttons (FR-042–044). Error responses include actionable messages with configuration buttons (FR-033). |
| VIII | Performance Requirements | **PASS** | InMemoryChannel uses `ConcurrentDictionary` for O(1) connection lookups. Streaming with sequence-numbered chunks. Idle cleanup via periodic timer (not per-request). VS Code health check is background/non-blocking (FR-034). `CancellationToken` support on all async methods. |

**Gate Result**: PASS — All applicable principles satisfied. Principle II is N/A (no new agents/tools).

## Project Structure

### Documentation (this feature)

```text
specs/013-copilot-everywhere/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
│   ├── channel-interfaces.md   # .NET Channels library public API contract
│   ├── vscode-extension.md     # VS Code extension API, commands, settings
│   └── m365-extension.md       # M365 webhook endpoints, Adaptive Card schemas
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
src/
├── Ato.Copilot.Channels/                  # EXISTING project — rebuild contents
│   ├── Ato.Copilot.Channels.csproj        # MODIFIED — add NuGet deps
│   ├── Abstractions/
│   │   ├── IChannel.cs                    # NEW — transport interface
│   │   ├── IChannelManager.cs             # NEW — connection/routing interface
│   │   ├── IMessageHandler.cs             # NEW — message processing interface
│   │   └── IStreamingHandler.cs           # NEW — streaming interface + IStreamContext
│   ├── Models/
│   │   ├── ChannelMessage.cs              # NEW — universal message payload
│   │   ├── ConnectionInfo.cs              # NEW — connection tracking model
│   │   ├── IncomingMessage.cs             # NEW — inbound message model
│   │   ├── StreamChunk.cs                 # NEW — typed streaming chunk
│   │   ├── MessageAttachment.cs           # NEW — file attachment model
│   │   └── ChannelEnums.cs                # NEW — ChannelType, MessageType, etc.
│   ├── Configuration/
│   │   └── ChannelOptions.cs              # NEW — options with StreamingOptions
│   ├── Implementations/
│   │   ├── InMemoryChannel.cs             # NEW — in-memory channel impl
│   │   ├── ChannelManager.cs              # NEW — connection/routing impl
│   │   ├── DefaultMessageHandler.cs       # NEW — message handler impl
│   │   ├── StreamContext.cs               # NEW — streaming context impl (IStreamContext)
│   │   ├── StreamingHandler.cs            # NEW — streaming handler factory (IStreamingHandler)
│   │   └── IdleConnectionCleanupService.cs # NEW — BackgroundService for idle cleanup
│   └── Extensions/
│       └── ChannelServiceExtensions.cs    # NEW — AddChannels(), AddInMemoryChannels()
│
├── Ato.Copilot.Channels/GitHub/           # REMOVE — entire directory (placeholder stubs)
│   ├── PlatformParticipant.cs             # DELETE
│   ├── InlineComplianceChecker.cs         # DELETE
│   ├── README.md                          # DELETE
│   └── manifest.json                      # DELETE
├── Ato.Copilot.Channels/M365/            # REMOVE — entire directory (placeholder stubs)
│   ├── PlatformBot.cs                     # DELETE
│   ├── AdaptiveCards/                     # DELETE — directory
│   ├── README.md                          # DELETE
│   └── manifest.json                      # DELETE
├── Ato.Copilot.Channels/placeholder.md    # DELETE

extensions/                                # NEW top-level directory
├── vscode/                                # NEW — VS Code extension
│   ├── package.json                       # Extension manifest
│   ├── tsconfig.json
│   ├── src/
│   │   ├── extension.ts                   # Activation, participant registration
│   │   ├── participant.ts                 # @ato chat participant handler
│   │   ├── commands/
│   │   │   ├── health.ts                  # ato.checkHealth
│   │   │   ├── configure.ts               # ato.configure
│   │   │   ├── analyzeFile.ts             # ato.analyzeCurrentFile
│   │   │   └── analyzeWorkspace.ts        # ato.analyzeWorkspace
│   │   ├── services/
│   │   │   ├── mcpClient.ts               # HTTP client for MCP Server
│   │   │   ├── exportService.ts           # Markdown/JSON/HTML export
│   │   │   └── workspaceService.ts        # Template file management
│   │   └── webview/
│   │       └── analysisPanel.ts           # Webview for compliance results
│   └── test/
│       └── suite/
│           ├── participant.test.ts
│           ├── mcpClient.test.ts
│           └── exportService.test.ts
│
└── m365/                                  # NEW — M365 Copilot extension
    ├── package.json
    ├── tsconfig.json
    ├── src/
    │   ├── index.ts                       # Express server, routes, shutdown
    │   ├── services/
    │   │   └── atoApiClient.ts            # HTTP client for MCP Server
    │   ├── cards/
    │   │   ├── complianceCard.ts           # Compliance assessment card
    │   │   ├── infrastructureCard.ts       # Infrastructure result card
    │   │   ├── costCard.ts                # Cost estimate card
    │   │   ├── deploymentCard.ts          # Deployment result card
    │   │   ├── resourceCard.ts            # Resource list card
    │   │   ├── genericCard.ts             # Generic response card
    │   │   ├── errorCard.ts               # Error card
    │   │   └── followUpCard.ts            # Follow-up card
    │   └── manifest/
    │       ├── manifest.json              # Teams app manifest
    │       ├── ai-plugin.json             # M365 plugin descriptor
    │       └── openapi.json               # OpenAPI spec
    └── test/
        ├── cards.test.ts
        └── atoApiClient.test.ts

tests/
└── Ato.Copilot.Tests.Unit/
    └── Channels/
        ├── InMemoryChannelTests.cs        # NEW — channel transport tests
        ├── ChannelManagerTests.cs         # NEW — connection/routing tests
        ├── DefaultMessageHandlerTests.cs  # NEW — message handler tests
        └── StreamContextTests.cs          # NEW — streaming tests
```

**Structure Decision**: Three-component architecture following the existing multi-project pattern. The Channels .NET library lives in the existing `src/Ato.Copilot.Channels/` project (rebuilt from scaffolds). TypeScript extensions live in a new `extensions/` top-level directory with `vscode/` and `m365/` subdirectories, keeping them separate from the .NET solution. Unit tests for Channels go in `tests/Ato.Copilot.Tests.Unit/Channels/`.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| Two TypeScript projects outside .NET solution | VS Code and M365 extensions are inherently TypeScript/Node.js — cannot be built as .NET class libraries. Different runtime, different package managers, different build tooling. | Embedding in .NET project would require Node.js shelling, complicate the build, and violate platform idioms. |
| `extensions/` top-level directory | Standard convention for VS Code extension repos. Keeps TypeScript projects out of `src/` which is .NET-only. | Putting in `src/` would confuse `dotnet build Ato.Copilot.sln` and mix concerns. |

## Post-Design Constitution Re-Check

*Re-evaluated after Phase 1 design artifacts (data-model.md, contracts/channel-interfaces.md, contracts/vscode-extension.md, contracts/m365-extension.md, quickstart.md) finalized.*

| # | Principle | Status | Post-Design Notes |
|---|-----------|--------|-------------------|
| I | Documentation | **PASS** | All artifacts generated: research.md (9 decisions), data-model.md (6 entities, 4 enums, 4 interfaces), 3 contract files (channel-interfaces.md, vscode-extension.md, m365-extension.md), quickstart.md (4 verification scenarios). |
| II | BaseTool Architecture | **N/A** | No new agents or tools defined. Channels library is a transport abstraction consumed by existing agents via `IChannelManager`. VS Code slash commands map to existing agents. |
| III | Testing Standards | **PASS** | Data model documents validation rules for all entities. Contracts document error responses with specific error codes and messages. Test file inventory: 4 new .NET test files (~30+ tests), 2 TypeScript test suites. Quickstart defines behavior matrix for DefaultMessageHandler. |
| IV | Azure Government | **PASS** | No new Azure service clients. M365 Adaptive Cards link to `portal.azure.us` per contract. VS Code extension connects to configurable MCP Server URL (defaults to localhost). API keys via VS Code settings, never hardcoded. |
| V | Observability | **PASS** | Research R5 confirms `ILogger<T>` injected into all implementations. Connection lifecycle, message routing, stream events, idle cleanup all logged. Warning-level log for inactive connection skips documented in channel-interfaces.md contract. |
| VI | Code Quality | **PASS** | 4 focused interfaces (IChannel, IChannelManager, IMessageHandler, IStreamingHandler) each with ≤4 methods. Models are simple POCOs with XML docs. DI extension methods follow existing `AddChannels(configuration)` pattern per contracts. |
| VII | UX Consistency | **PASS** | VS Code contract defines streamed Markdown with agent attribution. M365 contract defines 8 Adaptive Card builders with consistent color-coded compliance scores, action buttons, and follow-up suggestions. Error Adaptive Card includes actionable retry guidance. |
| VIII | Performance | **PASS** | Research R1–R5 confirm: ConcurrentDictionary + ImmutableHashSet for O(1) lookups, PeriodicTimer for idle cleanup (no per-request overhead), Interlocked.Increment for lock-free sequence numbers, CompareExchange guard for one-time disposal. |

**Post-Design Gate Result**: PASS — Design artifacts align with all 8 constitution principles.

## Generated Artifacts

| Artifact | Path | Status |
|----------|------|--------|
| Implementation Plan | `specs/013-copilot-everywhere/plan.md` | ✅ Complete |
| Research | `specs/013-copilot-everywhere/research.md` | ✅ Complete (9 decisions) |
| Data Model | `specs/013-copilot-everywhere/data-model.md` | ✅ Complete |
| Contracts — Channel Interfaces | `specs/013-copilot-everywhere/contracts/channel-interfaces.md` | ✅ Complete |
| Contracts — VS Code Extension | `specs/013-copilot-everywhere/contracts/vscode-extension.md` | ✅ Complete |
| Contracts — M365 Extension | `specs/013-copilot-everywhere/contracts/m365-extension.md` | ✅ Complete |
| Quickstart | `specs/013-copilot-everywhere/quickstart.md` | ✅ Complete |
| Agent Context | `.github/agents/copilot-instructions.md` | ✅ Updated |

**Next Step**: Run `/speckit.tasks` to generate the Phase 2 task breakdown.
