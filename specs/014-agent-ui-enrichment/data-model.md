# Data Model: Agent-to-UI Response Enrichment

**Feature**: 014-agent-ui-enrichment  
**Date**: 2026-02-26  
**Status**: Complete

---

## Entity Catalog

### 1. AgentResponse (Extended)

**Location**: `src/Ato.Copilot.Agents/Common/BaseAgent.cs`  
**Change type**: Extend existing class with optional properties  

| Field | Type | Default | New? | Description |
|-------|------|---------|------|-------------|
| `Success` | `bool` | — | No | Whether the agent processed successfully |
| `Response` | `string` | `""` | No | Text response content |
| `AgentName` | `string` | `""` | No | Agent display name |
| `ToolsExecuted` | `List<ToolExecutionResult>` | `new()` | No | Tools invoked during processing |
| `ProcessingTimeMs` | `double` | — | No | Total processing duration |
| `Suggestions` | `List<string>` | `new()` | **Yes** | Dynamic follow-up suggestions |
| `RequiresFollowUp` | `bool` | `false` | **Yes** | Whether agent needs more input |
| `FollowUpPrompt` | `string?` | `null` | **Yes** | Prompt text for missing info |
| `MissingFields` | `List<string>` | `new()` | **Yes** | List of fields needed |
| `ResponseData` | `Dictionary<string, object>?` | `null` | **Yes** | Structured data payload |

**Validation rules**:
- `Suggestions` items must be non-empty strings, max 10 items.
- `FollowUpPrompt` only meaningful when `RequiresFollowUp == true`.
- `MissingFields` only meaningful when `RequiresFollowUp == true`.
- `ResponseData` is opaque to the server — contents determined by agent type.

---

### 2. McpChatResponse (Enriched)

**Location**: `src/Ato.Copilot.Mcp/Models/McpProtocol.cs`  
**Change type**: Extend existing class, rename JSON field, change error type  

| Field | Type | JSON Name | New? | Description |
|-------|------|-----------|------|-------------|
| `Success` | `bool` | `success` | No | Request success indicator |
| `Response` | `string` | `response` | No | Text response content |
| `ConversationId` | `string` | `conversationId` | No | Session identifier |
| `AgentName` | `string?` | `agentUsed` | **Modified** | Agent name (JSON serialized as `agentUsed`) |
| `IntentType` | `string?` | `intentType` | **Yes** | Derived from agent ID |
| `ProcessingTimeMs` | `double` | `processingTimeMs` | No | Total processing time |
| `ToolsExecuted` | `List<ToolExecution>` | `toolsExecuted` | No (unmapped) | Now populated from agent response |
| `Errors` | `List<ErrorDetail>` | `errors` | **Modified** | Was `List<string>`, now structured |
| `Suggestions` | `List<string>` | `suggestions` | No (unmapped) | Now populated from agent response |
| `RequiresFollowUp` | `bool` | `requiresFollowUp` | No (unmapped) | Now populated from agent response |
| `FollowUpPrompt` | `string?` | `followUpPrompt` | **Yes** | Follow-up prompt from agent |
| `MissingFields` | `List<string>?` | `missingFields` | **Yes** | Fields needed for follow-up |
| `Data` | `Dictionary<string, object>?` | `data` | **Yes** | Structured intent-specific data |
| `Metadata` | `Dictionary<string, object>` | `metadata` | No | Kept for extensibility |

**JSON field name change**:  
`AgentName` C# property → `"agentUsed"` in JSON via `[JsonPropertyName("agentUsed")]`

---

### 3. ErrorDetail (New)

**Location**: `src/Ato.Copilot.Mcp/Models/McpProtocol.cs`  
**Change type**: New class  

| Field | Type | Description |
|-------|------|-------------|
| `ErrorCode` | `string` | Machine-readable code (e.g., `"AGENT_TIMEOUT"`, `"UNAUTHORIZED"`, `"TOOL_FAILURE"`, `"ACTION_UNAVAILABLE"`, `"CAC_AUTH_REQUIRED"`, `"PIM_EXPIRED"`) |
| `Message` | `string` | Human-readable error description |
| `Suggestion` | `string` | Corrective guidance for the user |

**Validation rules**:
- `ErrorCode` must be a non-empty uppercase string with underscores.
- `Message` must be non-empty.
- `Suggestion` must be non-empty (Constitution VII).

---

### 4. ChatRequest (Extended)

**Location**: `src/Ato.Copilot.Mcp/Server/McpHttpBridge.cs` (private class)  
**Change type**: Extend existing class  

| Field | Type | Default | New? | Description |
|-------|------|---------|------|-------------|
| `Message` | `string` | `""` | No | Chat message text |
| `ConversationId` | `string?` | `null` | No | Session identifier |
| `Context` | `Dictionary<string, object>?` | `null` | No | Freeform context |
| `ConversationHistory` | `List<ChatMessage>?` | `null` | No | Message history |
| `Action` | `string?` | `null` | **Yes** | Action type (e.g., `"remediate"`, `"drillDown"`, `"checkPimStatus"`) |
| `ActionContext` | `Dictionary<string, string>?` | `null` | **Yes** | Action parameters (e.g., `findingId`, `controlId`, `scope`) |

**Validation rules**:
- When `Action` is present, `ActionContext` should contain required parameters for the action type.
- When `Action` is absent, normal chat flow is used.
- `Action` values are case-insensitive.

---

### 5. SseEvent (New)

**Location**: `src/Ato.Copilot.Mcp/Models/McpProtocol.cs`  
**Change type**: New class hierarchy  

| Field | Type | Description |
|-------|------|-------------|
| `Type` | `string` | Event type discriminator |

**Event subtypes** (all serialize as `{ "type": "<name>", ...payload }`):

| Type | Payload Fields | Description |
|------|---------------|-------------|
| `agentRouted` | `agentId: string`, `agentName: string` | Agent selected |
| `thinking` | `status: "processing"` | Agent started processing |
| `toolStart` | `toolName: string`, `toolIndex: int` | Tool invocation began |
| `toolProgress` | `toolName: string`, `percentComplete: int (0-100)`, `statusMessage?: string` | Tool progress update |
| `toolComplete` | `toolName: string`, `success: bool`, `executionTimeMs: double`, `resultSummary?: string` | Tool finished |
| `partial` | `text: string` | Incremental response text |
| `validating` | `status: "validating"` | Post-processing validation |
| `complete` | Full `McpChatResponse` | Final response payload |

---

### 6. ComplianceFinding (Enriched — TypeScript)

**Location**: `extensions/vscode/src/commands/analyzeFile.ts` (and new shared types)  
**Change type**: Extend existing TypeScript interface  

| Field | Type | New? | Description |
|-------|------|------|-------------|
| `controlId` | `string` | No | NIST control ID (e.g., "AC-2") |
| `title` | `string` | No | Finding title |
| `severity` | `"critical" \| "high" \| "medium" \| "low" \| "informational"` | **Modified** | Expanded from 3 to 5 levels |
| `description` | `string` | No | Finding description |
| `recommendation` | `string` | No | Remediation guidance |
| `controlFamily` | `string` | **Yes** | Control family (e.g., "AC") |
| `resourceId` | `string?` | **Yes** | Azure resource identifier |
| `resourceType` | `string?` | **Yes** | Azure resource type |
| `autoRemediable` | `boolean` | **Yes** | Whether auto-fix is available |
| `remediationScript` | `string?` | **Yes** | Script for auto-remediation |
| `riskLevel` | `"critical" \| "high" \| "medium" \| "low"` | **Yes** | Risk assessment level |
| `frameworkReference` | `string` | **Yes** | e.g., "NIST 800-53 Rev 5" |
| `findingStatus` | `"open" \| "acknowledged" \| "remediated" \| "verified"` | **Yes** | Finding lifecycle status |
| `findingId` | `string?` | **Yes** | Unique finding identifier |

---

### 7. McpChatResponse (TypeScript — VS Code)

**Location**: `extensions/vscode/src/services/mcpClient.ts`  
**Change type**: Extend existing TypeScript interface  

| Field | Type | New? | Description |
|-------|------|------|-------------|
| `success` | `boolean` | No | Request success |
| `response` | `string` | No | Text response |
| `conversationId` | `string` | No | Session ID |
| `agentUsed` | `string?` | **Modified** | Was `agentName` |
| `intentType` | `string?` | **Yes** | Intent classification |
| `processingTimeMs` | `number` | No | Processing duration |
| `toolsExecuted` | `ToolExecution[]` | **Yes** | Tool execution records |
| `errors` | `ErrorDetail[]` | **Modified** | Was `string[]` |
| `suggestions` | `string[]` | **Yes** | Follow-up suggestions |
| `requiresFollowUp` | `boolean` | **Yes** | Needs more input |
| `followUpPrompt` | `string?` | **Yes** | Follow-up prompt text |
| `missingFields` | `string[]?` | **Yes** | Required fields |
| `data` | `Record<string, unknown>?` | **Yes** | Structured data payload |

---

### 8. McpResponse (TypeScript — M365)

**Location**: `extensions/m365/src/services/atoApiClient.ts` (inline interface)  
**Change type**: Update TypeScript interface  

> **Terminology note**: The M365 extension uses the name `McpResponse` while the VS Code extension uses `McpChatResponse`. Both interfaces MUST be identical in shape. The naming difference is an extension-local convention — each extension names its response type independently. A shared types package may unify this in a future feature.

---

### 9. IacComplianceScanTool (New)

**Location**: `src/Ato.Copilot.Agents/Compliance/Tools/`  
**Change type**: New `BaseTool` implementation  

**Tool Parameters**:

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `filePath` | `string` | No* | Path to IaC file |
| `fileContent` | `string` | No* | Raw file content |
| `fileType` | `string` | No | `"bicep"`, `"terraform"`, `"arm"` — auto-detected |
| `framework` | `string` | No | Compliance framework, default `"NIST 800-53"` |

*At least one of `filePath` or `fileContent` required.

**Tool Response**: `List<ComplianceFinding>` with all enriched fields.

---

## Relationship Map

```
AgentResponse (Agents)
    ├── ToolExecutionResult[] ─────→ mapped to ─→ ToolExecution[] (McpProtocol)
    ├── Suggestions ───────────────→ passed to ──→ McpChatResponse.Suggestions
    ├── RequiresFollowUp ──────────→ passed to ──→ McpChatResponse.RequiresFollowUp
    ├── FollowUpPrompt ────────────→ passed to ──→ McpChatResponse.FollowUpPrompt
    ├── MissingFields ─────────────→ passed to ──→ McpChatResponse.MissingFields
    └── ResponseData ──────────────→ passed to ──→ McpChatResponse.Data

McpChatResponse (MCP Server)
    ├── IntentType ──────→ derived from targetAgent.AgentId
    ├── AgentName ───────→ serialized as "agentUsed" in JSON
    ├── Errors ──────────→ List<ErrorDetail> (was List<string>)
    └── Data ────────────→ Dictionary<string, object> from ResponseData

ChatRequest (MCP Server)
    ├── Action ──────────→ routes to MCP tool (bypasses chat flow)
    └── ActionContext ───→ parameters for the action

SseEvent (MCP Server → Client)
    └── complete ────────→ payload is full McpChatResponse

ComplianceFinding (Core ↔ extensions)
    ├── C# model (26 fields) ───→ serialized in ResponseData
    └── TS interface (14 fields) → subset for UI rendering
```

## State Transitions

### Finding Status Lifecycle

```
Open ──→ Acknowledged ──→ Remediated ──→ Verified
 │            │                │
 │            └── (can revert to Open if remediation fails)
 │
 └── (can jump to Remediated if auto-fix applied directly)
```

- `Open → Acknowledged`: User explicitly acknowledges via action button.
- `Acknowledged → Remediated`: Auto-fix applied and confirmed, or manual fix reported.
- `Remediated → Verified`: Follow-up assessment confirms fix effective.
- `Acknowledged → Open`: Remediation attempt fails or is cancelled.

### PIM Elevation Flow

```
Not Active ──→ Activation Requested ──→ Active ──→ Expired
                     │                     │
                     └── Pending Approval   └── Extended
```

- CAC authentication is prerequisite for any PIM action.
- PIM check happens before every infrastructure-modifying action.
- If PIM expires mid-operation: `PIM_EXPIRED` error, no partial execution.
