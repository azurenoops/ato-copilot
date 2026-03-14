# Research: Azure AI Foundry Agent Integration

**Feature**: 028-foundry-agents  
**Date**: 2026-03-13  
**Status**: Complete — all unknowns resolved

## R-001: Azure AI Foundry SDK Package & API Surface

**Decision**: Use `Azure.AI.Agents.Persistent` NuGet package (v1.1.0 stable) with `PersistentAgentsClient` as the primary client. Optionally add `Azure.AI.Projects` for the `AIProjectClient` entry point.

**Rationale**: The spec originally referenced `AgentsClient` and "connection string" — both are incorrect for the current SDK version. The actual client is `PersistentAgentsClient`, and connection string support was discontinued in v1.1.0 in favor of project endpoint URIs.

**Alternatives considered**:
- `Azure.AI.Projects` `AIProjectClient` as entry point → More overhead, provides Deployments/Connections sub-clients we don't need. Use `PersistentAgentsClient` directly.
- Sticking with connection string API → Discontinued in v1.1.0; the SDK now requires `string endpoint` + credential.

**Key corrections to spec FR-001** (applied during clarify session):
- Replace `AgentsClient` → `PersistentAgentsClient` throughout
- Config path is now `AzureAi:FoundryProjectEndpoint` (unified config)
- `AzureAiOptions` has `FoundryProjectEndpoint` property (not separate `AzureFoundryOptions`)

## R-002: Sub-Client Architecture

**Decision**: The `PersistentAgentsClient` uses a sub-client pattern. Methods are accessed via `.Administration`, `.Threads`, `.Messages`, `.Runs`.

**Rationale**: This is a non-obvious API design that differs from the flat `AzureOpenAIClient` surface. Code must call `client.Administration.CreateAgentAsync()`, not `client.CreateAgentAsync()`.

**Key API mappings**:

| Operation | Method |
|-----------|--------|
| Create/get agent | `client.Administration.CreateAgentAsync(model, name, instructions, tools)` |
| Create thread | `client.Threads.CreateThreadAsync()` |
| Add message | `client.Messages.CreateMessageAsync(threadId, role, content)` |
| Create run | `client.Runs.CreateRunAsync(threadId, agentId)` |
| Poll run | `client.Runs.GetRunAsync(threadId, runId)` |
| Submit tool outputs | `client.Runs.SubmitToolOutputsToRunAsync(run, toolOutputs)` |
| Read response | `client.Messages.GetMessagesAsync(threadId, order)` |

## R-003: FunctionToolDefinition Schema Format

**Decision**: Use `FunctionToolDefinition` with `BinaryData.FromObjectAsJson()` for parameter schemas.

**Rationale**: The Foundry SDK uses `BinaryData` for JSON Schema parameters, not the `JsonElement`-based `AITool` schema from `Microsoft.Extensions.AI`. The existing `ToolAIFunction.BuildSchema()` generates a `JsonElement` — this needs conversion to `BinaryData` for Foundry.

**Pattern**:
```csharp
new FunctionToolDefinition(
    name: tool.Name,
    description: tool.Description,
    parameters: BinaryData.FromString(tool.JsonSchema.GetRawText()) // reuse existing schema
);
```

**Alternatives considered**:
- Rebuild schemas from scratch using anonymous objects → Unnecessary duplication; the existing `BuildSchema()` JSON is already OpenAI-compatible and Foundry uses the same schema format.

## R-004: Authentication & Azure Government

**Decision**: Use `DefaultAzureCredential` with Azure Government authority host, matching the existing Feature 011 pattern. API key authentication is not supported by the `PersistentAgentsClient` constructor.

**Rationale**: The `PersistentAgentsClient` constructor takes `(string endpoint, TokenCredential credential)`. There is no API key overload. The `UseManagedIdentity` flag from the spec becomes the only auth method — `DefaultAzureCredential` handles both managed identity (prod) and Azure CLI (dev).

**Key corrections to spec** (applied during clarify session):
- Foundry auth always uses `DefaultAzureCredential` — `AzureAiOptions.UseManagedIdentity` controls only the IChatClient path
- Azure Government is handled by setting the correct `AuthorityHost` via `AzureAi:CloudEnvironment`

## R-005: RequiresAction Tool Dispatch Pattern

**Decision**: When a run reaches `RequiresAction`, cast `run.RequiredAction` to `SubmitToolOutputsAction`, iterate `.ToolCalls`, cast each to `RequiredFunctionToolCall`, execute the local `BaseTool`, and submit results via `SubmitToolOutputsToRunAsync`.

**Rationale**: The SDK uses a type hierarchy: `RequiredToolCall` → `RequiredFunctionToolCall` (with `.Name`, `.Arguments`, `.Id`). Results are submitted as `List<ToolOutput>` where each `ToolOutput` pairs a call with its string result.

**Key types**:
| Type | Purpose |
|------|---------|
| `SubmitToolOutputsAction` | `RequiredAction` subclass with `.ToolCalls` |
| `RequiredFunctionToolCall` | Has `.Name` (tool name), `.Arguments` (JSON string), `.Id` (correlation) |
| `ToolOutput` | `new ToolOutput(toolCall, resultString)` |

## R-006: Thread Lifecycle & Reading Responses

**Decision**: Threads are persistent server-side. After a run completes, read the assistant's response via `client.Messages.GetMessagesAsync(threadId, ListSortOrder.Ascending)` and extract the last assistant message.

**Rationale**: Foundry threads accumulate all messages (user + assistant + tool results). After a completed run, the new assistant message is the last one in the thread. Use `ListSortOrder.Ascending` and take the last assistant-role message.

**Alternatives considered**:
- Streaming via `CreateRunStreamingAsync` → Deferred to future enhancement per spec assumptions.

## R-007: Idempotent Agent Provisioning

**Decision**: On startup, list existing agents via `client.Administration.GetAgentsAsync()`, match by name, and update if found or create if not.

**Rationale**: The Foundry API does not have a "create or update" operation. To ensure idempotency (FR-018), the system must list agents, find by name match, and either update the existing agent's instructions/tools or create a new one.

**Key pattern**:
```csharp
// List agents, find by name
var existing = await FindAgentByNameAsync(agentName);
if (existing != null)
{
    // Update existing agent
    await client.Administration.UpdateAgentAsync(existing.Id, ...);
}
else
{
    // Create new agent
    await client.Administration.CreateAgentAsync(...);
}
```
