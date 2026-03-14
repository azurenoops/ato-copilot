# Data Model: Azure AI Foundry Agent Integration

**Feature**: 028-foundry-agents  
**Date**: 2026-03-13 (updated post-migration)

## Overview

This feature adds **no new database entities** — all state is either configuration, in-memory runtime state, or managed server-side by the Azure AI Foundry service. The data model covers the unified `AzureAiOptions` configuration, runtime state, and the relationships between existing entities and Foundry types.

## Configuration Entity

### AzureAiOptions

Unified AI configuration model bound from the top-level `AzureAi` section in appsettings.json. Replaces the former `AzureOpenAIGatewayOptions`, `AzureFoundryOptions`, and `AiBackend` enum.

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| Enabled | bool | `false` | Master AI feature flag — when false, all agents use deterministic tool routing |
| Provider | AiProvider | `OpenAi` | AI provider selection: `OpenAi` (direct) or `Foundry` (server-side agents) |
| Endpoint | string | `""` | Azure OpenAI service endpoint URI |
| DeploymentName | string | `"gpt-4o"` | Model deployment name (used by both paths) |
| ApiKey | string? | `null` | API key for Azure OpenAI (when `UseManagedIdentity` is false) |
| UseManagedIdentity | bool | `true` | Whether to use `DefaultAzureCredential` for IChatClient path |
| CloudEnvironment | string | `"AzurePublicCloud"` | Azure cloud: `AzureGovernment` or `AzurePublicCloud` |
| MaxCompletionTokens | int | `4096` | Maximum completion tokens per AI response |
| MaxToolIterations | int | `10` | Maximum LLM ↔ tool-call round-trips before terminating |
| ConversationWindowSize | int | `20` | Number of recent messages to include as conversation context |
| Temperature | double | `0.3` | LLM sampling temperature (0.0–1.0) |
| FoundryProjectEndpoint | string? | `null` | Azure AI Foundry project endpoint (required when Provider is Foundry) |
| RunTimeoutSeconds | int | `60` | Maximum seconds to poll a Foundry run before cancelling |
| SystemPromptTemplate | string? | `null` | Optional template for system prompts |

**Computed Properties**:
- `IsFoundry` → `Provider == AiProvider.Foundry && !string.IsNullOrWhiteSpace(FoundryProjectEndpoint)`
- `IsConfigured` → `!string.IsNullOrWhiteSpace(Endpoint)`

**Notes**:
- Foundry path always uses `DefaultAzureCredential` (SDK does not support API keys)
- IChatClient path uses `DefaultAzureCredential` when `UseManagedIdentity=true`, else `AzureKeyCredential`
- Authority host for Gov/Commercial determined by `CloudEnvironment` value

### AiProvider Enum

Controls which AI processing path agents use. Bound from `AzureAi:Provider`.

| Value | Int | Behavior |
|-------|-----|----------|
| `OpenAi` | 0 | Use existing `TryProcessWithAiAsync` (IChatClient path) — **default** |
| `Foundry` | 1 | Use new `TryProcessWithFoundryAsync` (server-side agent path) |

When `AzureAi:Enabled` is `false`, agents skip both AI paths regardless of `Provider`.

## Runtime State (In-Memory, Not Persisted)

### Thread-to-Conversation Mapping

| Key | Type | Storage | Lifetime |
|-----|------|---------|----------|
| ConversationId → ThreadId | `ConcurrentDictionary<string, string>` | In-memory on BaseAgent | Process lifetime |

- Created when the first Foundry-processed message arrives for a conversation
- Lost on MCP server restart — a new thread is created for subsequent messages
- Orphaned Foundry threads expire naturally server-side

### Provisioned Agent IDs

| Key | Type | Storage | Lifetime |
|-----|------|---------|----------|
| _foundryAgentId | `string?` field on each BaseAgent instance | In-memory | Process lifetime |

- Set during startup provisioning via `ProvisionFoundryAgentAsync`
- Each concrete agent (Compliance, Configuration, KnowledgeBase) stores its own Foundry agent ID
- Updated on restart if the agent's instructions or tools change

## Entity Relationships

```text
┌─────────────────────────┐
│     AzureAiOptions      │  (config — AzureAi section in appsettings.json)
│  - Enabled              │
│  - Provider (AiProvider) │
│  - Endpoint             │
│  - DeploymentName       │
│  - FoundryProjectEndpoint│
│  - RunTimeoutSeconds    │
│  - MaxToolIterations    │
│  - Temperature          │
└──────────┬──────────────┘
           │ DI: IOptions<AzureAiOptions>
           ▼
┌──────────────────────────┐
│  PersistentAgentsClient  │  (singleton — Azure.AI.Agents.Persistent)
│  - .Administration       │  registered when FoundryProjectEndpoint set
│  - .Threads              │
│  - .Messages             │
│  - .Runs                 │
└──────────┬───────────────┘
           │ injected into
           ▼
┌──────────────────────────┐          ┌──────────────┐
│       BaseAgent          │          │  IChatClient  │ (singleton — Azure.AI.OpenAI)
│  - _foundryClient?       │◄────────►│  (optional)   │  registered when Endpoint set
│  - _foundryAgentId?      │          └──────────────┘
│  - _azureAiOptions?      │
│  - _threadMap (ConvId→   │          ┌──────────────┐
│     ThreadId)            │◄────────►│  AiProvider   │ (enum on AzureAiOptions)
│  - TryProcessWithFoundry │          │  OpenAi | Foundry
│    Async()               │          └──────────────┘
│  - TryProcessWithAi      │
│    Async() (existing)    │
│  - ProvisionFoundryAgent │
│    Async()               │
└──────────┬───────────────┘
           │ extends
           ▼
┌──────────────────────────┐
│   ComplianceAgent        │
│   ConfigurationAgent     │
│   KnowledgeBaseAgent     │
│  - Each stores its own   │
│    provisioned agent ID  │
└──────────────────────────┘
```

## Existing Entities (Unchanged)

| Entity | Impact |
|--------|--------|
| AgentConversationContext | No changes — existing `ConversationId` and `MessageHistory` used as-is |
| BaseTool / ToolParameter | No changes — `Parameters` metadata reused for `FunctionToolDefinition` schema generation |
| AgentResponse | No changes — `Response`, `ToolsExecuted`, `ProcessingTimeMs` populated by Foundry path |

## Database Impact

**None.** No migrations, no new tables, no schema changes. All Foundry state is either configuration (appsettings) or runtime (in-memory + Foundry server-side).
