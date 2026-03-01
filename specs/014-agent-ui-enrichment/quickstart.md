# Quickstart: Agent-to-UI Response Enrichment

**Feature**: 014-agent-ui-enrichment  
**Branch**: `014-agent-ui-enrichment`  
**Date**: 2026-02-26

---

## Prerequisites

- .NET 9.0 SDK installed
- Node.js 20+ and npm installed
- VS Code 1.90+ with Extension Host debugging capability
- Docker Desktop (for containerized testing)
- Active Azure Government subscription (for PIM/CAC testing)
- CAC reader + smart card (for security flow testing)

## Build & Test Baseline

```bash
# Verify .NET baseline (2462 tests must pass)
cd /Users/johnspinella/repos/ato-copilot
dotnet build Ato.Copilot.sln
dotnet test Ato.Copilot.sln

# Verify VS Code extension baseline (61 tests must pass)
cd extensions/vscode
npm install && npm test

# Verify M365 extension baseline (32 tests must pass)
cd ../m365
npm install && npm test
```

## Implementation Order

### Phase 1: Server-Side Response Enrichment (C#)

**Why first**: All client-side work depends on enriched data from the server.

1. **Extend AgentResponse** (`src/Ato.Copilot.Agents/Common/BaseAgent.cs`)
   - Add: `Suggestions`, `RequiresFollowUp`, `FollowUpPrompt`, `MissingFields`, `ResponseData`
   - All optional with defaults — zero impact on existing agents

2. **Enrich McpChatResponse** (`src/Ato.Copilot.Mcp/Models/McpProtocol.cs`)
   - Add `[JsonPropertyName("agentUsed")]` to `AgentName`
   - Add `IntentType`, `FollowUpPrompt`, `MissingFields`, `Data` properties
   - Replace `List<string> Errors` with `List<ErrorDetail> Errors`
   - Add `ErrorDetail` class

3. **Extend ChatRequest** (`src/Ato.Copilot.Mcp/Server/McpHttpBridge.cs`)
   - Add `Action` and `ActionContext` to the private `ChatRequest` class

4. **Enrich ProcessChatRequestAsync** (`src/Ato.Copilot.Mcp/Server/McpServer.cs`)
   - Map `intentType` from `targetAgent.AgentId`
   - Map all new fields from `AgentResponse` to `McpChatResponse`
   - Add action routing: when `Action` is present, route to appropriate MCP tool
   - Update error handling to use `ErrorDetail`

5. **Extend SSE streaming** (`src/Ato.Copilot.Mcp/Server/McpHttpBridge.cs`)
   - Add typed SSE event models
   - Update `HandleChatStreamRequestAsync` to emit 8 event types
   - Map `IProgress<string>` to typed events

6. **Add IaC scanning tool** (`src/Ato.Copilot.Agents/Compliance/Tools/`)
   - New `IacComplianceScanTool` extending `BaseTool`
   - Register in `ComplianceMcpTools.cs` and DI

```bash
# Verify after each step
dotnet build Ato.Copilot.sln
dotnet test Ato.Copilot.sln
```

### Phase 2: M365 Extension Cards (TypeScript)

**Why second**: Cards can be developed against the enriched response schema.

1. **Remove dead cards** — Delete `costCard.ts`, `deploymentCard.ts`, `infrastructureCard.ts`, `resourceCard.ts` and their tests/imports
2. **Update TypeScript interfaces** — Align `McpResponse` with enriched server schema
3. **Add new card builders** — 9 new cards per `contracts/adaptive-cards.json`
4. **Update card routing** — Intent + data.type sub-routing
5. **Add conversation history** — Accumulate in `ATOApiClient` per conversationId
6. **Add suggestion buttons** — Action.Submit on all cards
7. **Add SSE client** — `fetch` + `ReadableStream` for `/mcp/chat/stream`
8. **Add PIM check flow** — Pre-flight `checkPimStatus` before infra actions

```bash
cd extensions/m365 && npm test
```

### Phase 3: VS Code Extension Enrichment (TypeScript)

**Why third**: Parallel with M365 but depends on server enrichment.

1. **Update TypeScript interfaces** — Align `McpChatResponse` with enriched server schema
2. **Enrich analysis panel** — 5-level severity, control family grouping, framework badge, resource context, auto-remediation flow
3. **Enable scripts in webview** — Required for interactive actions
4. **Add drill-down navigation** — Clickable control IDs, inline expansion
5. **Add remediation confirmation** — Diff preview + "Confirm & Apply" + PIM check
6. **Enrich chat participant** — Attribution, tools summary, suggestions, follow-up prompts
7. **Add SSE client** — Streaming progress in chat and analysis panel
8. **Add PIM check flow** — Pre-flight `checkPimStatus` before infra actions

```bash
cd extensions/vscode && npm test
```

### Phase 4: Integration & Polish

1. **End-to-end testing** — Full flow from chat request through enriched response to rendered card/panel
2. **Cross-extension parity** — Verify M365 and VS Code show identical confirmation info for remediation
3. **Performance validation** — SSE streaming latency, tool execution timing
4. **Audit trail verification** — CAC identity + PIM role in audit logs for all infra actions

## Key File Map

| File | What Changes |
|------|-------------|
| `src/Ato.Copilot.Agents/Common/BaseAgent.cs` | AgentResponse extended |
| `src/Ato.Copilot.Mcp/Models/McpProtocol.cs` | McpChatResponse enriched, ErrorDetail added |
| `src/Ato.Copilot.Mcp/Server/McpServer.cs` | ProcessChatRequestAsync enriched, action routing |
| `src/Ato.Copilot.Mcp/Server/McpHttpBridge.cs` | ChatRequest extended, SSE event types |
| `src/Ato.Copilot.Agents/Compliance/Tools/*.cs` | IaC scanning tool added |
| `extensions/m365/src/cards/*.ts` | 4 removed, 9 added, routing updated |
| `extensions/m365/src/services/atoApiClient.ts` | Conversation history, SSE client |
| `extensions/vscode/src/webview/analysisPanel.ts` | Full panel enrichment |
| `extensions/vscode/src/services/mcpClient.ts` | Interface update, SSE client |
| `extensions/vscode/src/participant.ts` | Chat enrichment |

## Validation Checklist

- [ ] `dotnet build Ato.Copilot.sln` — zero warnings
- [ ] `dotnet test Ato.Copilot.sln` — ≥2462 tests pass
- [ ] `cd extensions/vscode && npm test` — ≥61 tests pass
- [ ] `cd extensions/m365 && npm test` — ≥32 tests pass
- [ ] POST `/mcp/chat` with compliance query → response has `intentType: "compliance"`, `agentUsed`, `toolsExecuted`
- [ ] POST `/mcp/chat` with action field → routes to correct tool
- [ ] SSE `/mcp/chat/stream` → emits all 8 event types in order
- [ ] M365 compliance card renders with color-coded score and action buttons
- [ ] VS Code panel shows 5 severity levels with correct colors
- [ ] Remediation attempt triggers PIM check → confirmation → audit log
- [ ] `agentUsed` (not `agentName`) in all JSON responses
- [ ] Error responses include `errorCode` and `suggestion`
