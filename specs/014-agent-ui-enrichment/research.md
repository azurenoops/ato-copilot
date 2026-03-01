# Research: Agent-to-UI Response Enrichment

**Feature**: 014-agent-ui-enrichment  
**Date**: 2026-02-26  
**Status**: Complete

---

## R-001: AgentResponse Extension Pattern

**Decision**: Extend `AgentResponse` with optional properties using default values.

**Rationale**: The existing `AgentResponse` class in `BaseAgent.cs` has 5 properties (`Success`, `Response`, `AgentName`, `ToolsExecuted`, `ProcessingTimeMs`). Adding optional properties with defaults (`Suggestions = new()`, `RequiresFollowUp = false`, `FollowUpPrompt = null`, `MissingFields = new()`, `ResponseData = null`) preserves backward compatibility — existing agents compile and behave identically without changes.

**Alternatives considered**:
- **Subclass per intent** (e.g., `ComplianceAgentResponse`): Rejected — forces client code to cast, breaks existing `ProcessAsync` return type.
- **Response wrapper/decorator**: Rejected — unnecessary indirection for simple optional fields.
- **Separate enrichment service**: Rejected — enrichment data originates from agents; separating it adds coupling.

---

## R-002: Intent Type Mapping Strategy

**Decision**: Server-side mapping from `targetAgent.AgentId` to intent string in `ProcessChatRequestAsync`.

**Rationale**: The `targetAgent` variable is already in scope after `AgentOrchestrator.SelectAgent(message)`. A simple dictionary maps `"compliance-agent"` → `"compliance"`, `"knowledgebase-agent"` → `"knowledgebase"`, `"configuration-agent"` → `"configuration"`. Unknown IDs default to `"general"`. No agent changes required.

**Alternatives considered**:
- **Agent self-declaration** (agents return their own intent type): Rejected — agents shouldn't know about presentation-layer concepts; violates separation of concerns.
- **LLM-based classification**: Rejected — adds latency, non-deterministic, unnecessary when agent selection is already deterministic via confidence scoring.

---

## R-003: JSON Field Name Migration (`agentName` → `agentUsed`)

**Decision**: Add `[JsonPropertyName("agentUsed")]` to the existing C# `AgentName` property on `McpChatResponse`.

**Rationale**: This changes only JSON serialization output. C# code continues to use `.AgentName`. TypeScript clients already expect `agentUsed`. No breaking change for .NET consumers since they use the property name, not the JSON key.

**Alternatives considered**:
- **Rename the C# property**: Rejected — breaks all existing C# references, unnecessary churn.
- **Add a new property + deprecate old**: Rejected — duplicates data in the response; confusing.

---

## R-004: Error Model Evolution (`List<string>` → `List<ErrorDetail>`)

**Decision**: Replace `McpChatResponse.Errors` from `List<string>` to `List<ErrorDetail>` with `ErrorCode`, `Message`, and `Suggestion` fields.

**Rationale**: Constitution VII mandates machine-readable error codes and corrective suggestions. Current usage is limited to one catch block in `McpServer.ProcessChatRequestAsync`, making migration straightforward. The `ErrorDetail` type aligns with the existing `McpError` JSON-RPC error type but is tailored for chat responses.

**Alternatives considered**:
- **Backward-compatible `Errors` + new `ErrorDetails`**: Rejected — duplicates error data and clients would need to check both.
- **Union type (string | ErrorDetail)**: Rejected — C# doesn't support discriminated unions cleanly in System.Text.Json serialization.

---

## R-005: SSE Streaming Architecture for TypeScript Extensions

**Decision**: Implement SSE client using native `fetch` with `ReadableStream` in both TS extensions. Extend existing `/mcp/chat/stream` event types from 3 (progress/result/error) to 8 (agentRouted/thinking/toolStart/toolProgress/toolComplete/partial/validating/complete).

**Rationale**:
- The `/mcp/chat/stream` SSE endpoint **already exists** in `McpHttpBridge.cs` with proper SSE headers and event framing.
- The `IProgress<string>` pipeline flows through the **entire stack**: McpServer → BaseAgent → concrete agents. This already carries progress strings.
- The `ChatService` in `Ato.Copilot.Chat` already has a working SSE consumer with automatic fallback to sync — this pattern should be replicated in TypeScript.
- Both TypeScript extensions currently only use synchronous `/mcp/chat` via axios.
- No SSE client library exists in either extension — implement with native `fetch` + `ReadableStream` API to avoid new dependencies.

**Server-side changes needed**:
1. Extend `IProgress<string>` reports to emit typed JSON events instead of plain strings.
2. Add new event types: `agentRouted`, `thinking`, `toolStart`, `toolProgress`, `toolComplete`, `partial`, `validating`, `complete`.
3. Map `AssessmentProgress` (already typed in `IComplianceInterfaces.cs`) to `toolProgress` percentage events.

**Client-side changes needed**:
1. New `SseClient` class in each extension using `fetch` with `ReadableStream`.
2. Line-based SSE parser (same approach as `ChatService.cs`).
3. Event type dispatch to UI update callbacks.
4. Retry once with exponential backoff on connection failure, then fall back to sync.

**Alternatives considered**:
- **`eventsource` npm package**: Rejected — adds dependency; native fetch/ReadableStream is sufficient and tree-shakes better.
- **WebSocket**: Rejected — SSE is simpler for server→client streaming; endpoint already exists.
- **Polling**: Rejected — worse UX, higher server load, more complex.

---

## R-006: Action Routing Architecture

**Decision**: Extend the `ChatRequest` model with optional `action` (string) and `actionContext` (Dictionary<string, string>) fields. When `action` is present, `ProcessChatRequestAsync` routes to the appropriate MCP tool directly instead of normal chat flow.

**Rationale**:
- Current `ChatRequest` has 4 fields: `Message`, `ConversationId`, `Context`, `ConversationHistory`.
- Adding `action` + `actionContext` is non-breaking — existing requests without these fields flow through normal chat.
- Action routing maps action strings to existing registered tools:
  - `"remediate"` → remediation tool
  - `"collectEvidence"` → evidence collection tool
  - `"acknowledgeAlert"` → alert management tool
  - `"drillDown"` → lookup tool
  - `"updateFindingStatus"` → status update tool
  - `"checkPimStatus"` → `PimListActiveTool`
  - `"activatePim"` → `PimActivateRoleTool`
  - `"listEligiblePimRoles"` → `PimListEligibleTool`
- PIM tools are already registered in `ComplianceMcpTools.cs` — no new endpoints needed.

**Alternatives considered**:
- **Separate `/mcp/actions` endpoint**: Rejected — doubles API surface; action requests are semantically chat operations.
- **Encode actions in message text**: Rejected — fragile, not machine-parseable, no type safety.
- **Use existing `/mcp` JSON-RPC tool endpoint**: Rejected — different auth/middleware pipeline; chat endpoint has conversation context.

---

## R-007: M365 Adaptive Card Architecture

**Decision**: Remove 4 dead card builders (cost, deployment, infrastructure, resource), add 8 new compliance-domain cards + update 2 existing cards = 10 card types total in routing.

**Cards to remove** (no supporting agent):
1. `costCard.ts` — `CostData` interface, no agent produces cost data.
2. `deploymentCard.ts` — `DeploymentData` interface, no agent produces deployment data.
3. `infrastructureCard.ts` — `InfrastructureData` interface, no agent produces this.
4. `resourceCard.ts` — `ResourceData` interface, no agent produces this.

**Cards to add**:
1. Finding detail card — severity badge (5-level), resource context, remediation, "Apply Fix" action.
2. Remediation plan card — prioritized list, timeline, risk reduction.
3. Alert lifecycle card — severity, affected resources, SLA, acknowledge/dismiss/escalate actions.
4. Compliance trend card — sparkline visualization, trend direction indicator.
5. Evidence collection card — completeness meter, evidence items, "Collect More" action.
6. NIST control card — control statement, implementation guidance, STIGs, FedRAMP baseline.
7. Multi-turn clarification card — missing fields with input dropdowns.
8. Remediation kanban board card — board columns (To Do, In Progress, Done/Verified), task cards with severity badges, "Move" / "Mark Complete" actions. Uses existing `KanbanBoardShowTool` / `KanbanTaskListTool` data.

**Cards to update**:
1. Knowledge base card (new) — answer, sources, "Learn More".
2. Configuration card (new) — settings display, "Update Setting" actions.

**Card routing**: Sub-routing within compliance intent based on `data.type` field:
- `data.type === "assessment"` → compliance card (updated)
- `data.type === "finding"` → finding detail card
- `data.type === "remediationPlan"` → remediation plan card
- `data.type === "alert"` → alert lifecycle card
- `data.type === "trend"` → compliance trend card
- `data.type === "evidence"` → evidence collection card
- `data.type === "control"` → NIST control card
- `data.type === "clarification"` → clarification card
- `data.type === "kanban"` → kanban board card

**Alternatives considered**:
- **Single generic card with conditional sections**: Rejected — too complex to maintain; each domain has distinct layout needs.
- **Card templates from server**: Rejected — Adaptive Card JSON from server violates separation of concerns; clients own presentation.

---

## R-008: VS Code Analysis Panel Enrichment

**Decision**: Extend the existing `analysisPanel.ts` webview with all 26 ComplianceFinding fields, 5-level severity, control family grouping, framework badge, and interactive actions via `postMessage` to the extension host.

**Rationale**:
- Current panel renders only 5 of 26 available ComplianceFinding fields.
- TypeScript `ComplianceFinding` interface has 5 fields vs 26 in C#.
- Panel uses static HTML (`enableScripts: false`) — must enable scripts for interactive actions (drill-down, remediation confirmation, PIM prompts).
- Control family grouping is derived from the first segment of `controlId` (e.g., "AC-2" → "AC — Access Control").

**Severity mapping expansion** (3 → 5 levels):
| Level | Color | Use |
|-------|-------|-----|
| Critical | Purple (`#7B2D8E`) | Immediate risk to ATO |
| High | Red (`#E74C3C`) | Significant compliance gap |
| Medium | Orange (`#F39C12`) | Moderate risk |
| Low | Yellow (`#F1C40F`) | Minor risk |
| Informational | Blue (`#3498DB`) | Awareness only |

**Alternatives considered**:
- **Webview UI Toolkit (vscode-webview-ui-toolkit)**: Rejected — adds dependency for a webview that's fundamentally HTML template output; not worth the bundle size.
- **Tree view instead of webview**: Rejected — can't render rich formatting (color badges, collapsible sections, action buttons).

---

## R-009: IaC Scanning Tool Design

**Decision**: Create a new `IacComplianceScanTool` extending `BaseTool` that accepts file path or content, detects IaC type (Bicep/Terraform/ARM), and returns structured `ComplianceFinding[]` using AI-assisted analysis.

**Rationale**:
- No dedicated IaC file-scanning tool exists. The current `analyzeCurrentFile` VS Code command sends file content as a chat prompt, relying on unstructured LLM output.
- The `ComplianceAssessmentTool` runs live Azure assessments against subscriptions — fundamentally different from static file analysis.
- The `AiRemediationPlanGenerator` already generates Bicep/Terraform code, proving the AI can reason about IaC.
- The `ScriptSanitizationService` validates generated scripts, which can be reused for IaC scan output.

**Tool parameters**:
- `filePath` (string, optional) — path to file for scanning.
- `fileContent` (string, optional) — raw content when path unavailable (e.g., unsaved editor buffer).
- `fileType` (string, optional) — `"bicep"`, `"terraform"`, `"arm"`. Auto-detected if omitted.
- `framework` (string, optional) — target compliance framework, defaults to `"NIST 800-53"`.

**Output**: `List<ComplianceFinding>` with all enriched fields, serialized to JSON.

**Alternatives considered**:
- **Shell out to external linters** (tflint, bicep linter): Rejected — requires external tool installation; compliance rules are NIST-specific, not syntax.
- **Rule-engine based scanning**: Rejected — brittle for the variety of IaC patterns; AI-assisted analysis with compliance prompt is more flexible and maintainable.

---

## R-010: CAC + PIM Security Model for Infrastructure Actions

**Decision**: Reuse existing PIM MCP tools via `/mcp/chat` action routing. Both extensions perform a pre-flight PIM check (`action: "checkPimStatus"`) before infrastructure-modifying actions, prompt for activation via `action: "activatePim"` if needed, and pass CAC-derived identity in request headers.

**Rationale**:
- **8 PIM MCP tools already exist** in `ComplianceMcpTools.cs`: `PimListActiveTool`, `PimActivateRoleTool`, `PimListEligibleTool`, `PimExtendRoleTool`, `PimDeactivateRoleTool`, `PimApproveRequestTool`, `PimDenyRequestTool`, `PimHistoryTool`.
- **`IPimService` interface** in `Ato.Copilot.Core.Interfaces.Auth` provides full PIM lifecycle with 9 methods.
- **`AuthTierClassification.GetRequiredPimTier(string toolName)`** already classifies tools by PIM tier.
- **`BaseTool`** has a PIM tier property per existing R-010.
- **`AuditLoggingMiddleware`** already exists for audit trail.
- **`ComplianceAuthorizationMiddleware`** already validates CAC-derived identity.

**Flow for infrastructure-modifying actions**:
1. Extension sends `action: "checkPimStatus"`, `actionContext: { "scope": "<subscriptionId>" }`.
2. Server routes to `PimListActiveTool`, returns active roles.
3. If no matching role: extension prompts user, sends `action: "activatePim"` with justification.
4. If `PendingApproval`: extension shows "Approval pending" status.
5. Once PIM is active: extension sends the actual action (e.g., `action: "remediate"`).
6. Server validates PIM + CAC in `ComplianceAuthorizationMiddleware` before executing.
7. `AuditLoggingMiddleware` logs: user identity, PIM role, action, result, timestamp.

**Alternatives considered**:
- **New REST API for PIM**: Rejected — tools already exist as MCP tools; adding a REST API would duplicate functionality.
- **PIM check on server only**: Rejected — poor UX; user should be prompted client-side before sending the action.

---

## R-011: M365 Conversation History Management

**Decision**: Implement client-side conversation history accumulation in `ATOApiClient` with a `Map<string, ChatMessage[]>` keyed by `conversationId`, truncating at 20 exchanges.

**Rationale**:
- Currently hardcoded: `conversationHistory: []` with comment "No multi-turn for v1".
- `ConversationHistory` field exists on the C# `ChatRequest` model but M365 never populates it.
- Teams `conversation.id` provides a natural session key.
- 20-exchange limit (40 messages: 20 user + 20 assistant) prevents unbounded memory growth.
- Oldest entries dropped when limit exceeded (FIFO).

**Alternatives considered**:
- **Server-side history management**: Rejected — server already supports conversation history via `AgentConversationContext.MessageHistory`; client just needs to pass it.
- **Database-backed history**: Rejected — over-engineered for an extension; in-memory with session lifetime is sufficient.
- **Unlimited history**: Rejected — risks context window overflow and memory issues per Constitution VIII.

---

## R-012: Structured Data Pass-Through Design

**Decision**: `AgentResponse.ResponseData` is `Dictionary<string, object>`, passed through as the `data` field in `McpChatResponse` without transformation. Agents populate intent-specific keys.

**Rationale**:
- Avoids server-side text parsing (fragile, slow).
- Agents already know what structured data they produce.
- Dictionary allows each agent to populate domain-specific keys without server changes.
- TypeScript clients type-check the `data` field based on `intentType` (discriminated union pattern).

**Data schemas by intent**:
- `compliance`: `{ type: "assessment", complianceScore, passedControls, warningControls, failedControls, findings, framework, assessmentScope }`
- `compliance` (finding): `{ type: "finding", finding: ComplianceFinding }`
- `compliance` (plan): `{ type: "remediationPlan", phases, findings, riskReduction }`
- `compliance` (alert): `{ type: "alert", alertId, severity, affectedResources, slaDeadline?, status }`
- `compliance` (trend): `{ type: "trend", dataPoints, direction }`
- `compliance` (evidence): `{ type: "evidence", completeness, items }`
- `compliance` (kanban): `{ type: "kanban", board: KanbanBoard }`
- `knowledgebase`: `{ type: "answer", answer, sources, controlId, controlFamily }`
- `knowledgebase` (control): `{ type: "control", controlId, controlFamily, title, statement }`
- `configuration`: `{ type: "configuration", subscriptionId, framework, baseline, cloudEnvironment }`

**Alternatives considered**:
- **Typed response classes per intent**: Rejected — requires serialization polymorphism; Dictionary is simpler and extensible.
- **Server-side response text parsing**: Rejected — fragile regex/JSON extraction from Markdown text; error-prone.
