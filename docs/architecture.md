# Architecture

> System design, project structure, agent framework, database, and cross-cutting concerns.

## Table of Contents

- [Overview](#overview)
- [System Diagram](#system-diagram)
- [Project Structure](#project-structure)
- [Transport Layer — MCP Protocol](#transport-layer--mcp-protocol)
  - [Dual-Mode Server](#dual-mode-server)
  - [HTTP Endpoints](#http-endpoints)
  - [Stdio (JSON-RPC)](#stdio-json-rpc)
  - [Middleware Pipeline](#middleware-pipeline)
- [Agent Framework](#agent-framework)
  - [BaseAgent](#baseagent)
  - [BaseTool](#basetool)
  - [ComplianceAgent](#complianceagent)
  - [ConfigurationAgent](#configurationagent)
  - [Intent Routing](#intent-routing)
  - [System Prompts](#system-prompts)
- [Tool Inventory](#tool-inventory)
  - [Core Compliance (11 tools)](#core-compliance-11-tools)
  - [Remediation Kanban (18 tools)](#remediation-kanban-18-tools)
  - [CAC Authentication (4 tools)](#cac-authentication-4-tools)
  - [PIM — Privileged Identity Management (9 tools)](#pim--privileged-identity-management-9-tools)
  - [JIT VM Access (3 tools)](#jit-vm-access-3-tools)
  - [Compliance Watch (23 tools)](#compliance-watch-23-tools)
  - [Configuration (2 tools)](#configuration-2-tools)
- [Database Layer](#database-layer)
  - [Provider Strategy](#provider-strategy)
  - [DbContext](#dbcontext)
  - [Entity Model](#entity-model)
  - [Concurrency](#concurrency)
  - [Migrations](#migrations)
- [State Management](#state-management)
- [Security Architecture](#security-architecture)
  - [Authentication Tiers](#authentication-tiers)
  - [RBAC Roles](#rbac-roles)
  - [Audit Logging](#audit-logging)
- [Dependency Injection](#dependency-injection)
- [Observability](#observability)
- [Key Design Patterns](#key-design-patterns)

---

## Overview

ATO Copilot is a compliance-focused MCP (Model Context Protocol) agent server built on .NET 9.0. It provides NIST 800-53 / FedRAMP compliance assessment, remediation, evidence collection, document generation, and continuous monitoring — all through natural language via AI coding assistants or REST APIs.

The system is designed around three principles:
1. **Chat-first** — all operations are accessible through natural language
2. **Compliance-native** — built for FedRAMP / DoD IL authorization workflows
3. **Auditable** — every action is logged with immutable audit trails

---

## System Diagram

```
MCP Clients
┌──────────────────────────────────────────────────────────────┐
│  GitHub Copilot  │  Claude Desktop  │  HTTP REST  │  CLI    │
└────────┬─────────┴────────┬─────────┴──────┬──────┴────┬────┘
         │ stdio (JSON-RPC)  │ stdio           │ HTTP      │
         └──────────┬────────┘                 └─────┬─────┘
                    │                                │
┌───────────────────┴────────────────────────────────┴────────┐
│  Ato.Copilot.Mcp (Entry Point)                              │
│  ┌──────────────┐  ┌──────────────┐  ┌───────────────────┐  │
│  │ McpStdioSvc  │  │ McpHttpBridge│  │ ComplianceMcpTools│  │
│  │ (BackgroundSvc│  │ (MinimalAPIs)│  │ (Facade/Adapter) │  │
│  └──────┬───────┘  └──────┬───────┘  └─────────┬─────────┘  │
│         └──────────┬───────┘                    │            │
│              ┌─────┴──────┐                     │            │
│              │  McpServer  │◄────────────────────┘            │
│              └─────┬──────┘                                  │
│  ┌─────────────────┴──────────────────────────┐              │
│  │  Middleware Pipeline                       │              │
│  │  CorrelationId → Serilog → CORS →          │              │
│  │  CacAuth → ComplianceAuth → AuditLog       │              │
│  └────────────────────────────────────────────┘              │
└──────────────────────────┬───────────────────────────────────┘
                           │
┌──────────────────────────┴───────────────────────────────────┐
│  Ato.Copilot.Agents (Agent Framework)                        │
│  ┌──────────────────┐  ┌─────────────────────┐               │
│  │ ComplianceAgent  │  │ ConfigurationAgent  │               │
│  │ (65+ tools)      │  │ (2 tools)           │               │
│  └────────┬─────────┘  └─────────┬───────────┘               │
│           │ Tool Execution        │                           │
│  ┌────────┴───────────────────────┴──────────────────┐       │
│  │  Tools (Singleton, ScopeFactory pattern)          │       │
│  │  Core │ Kanban │ CAC │ PIM │ JIT │ Watch │ Config │       │
│  └───────────────────────┬───────────────────────────┘       │
│  ┌───────────────────────┴───────────────────────────┐       │
│  │  Services (Singleton + Scoped)                    │       │
│  │  AtoComplianceEngine │ RemediationEngine │         │       │
│  │  KanbanService │ ComplianceWatchService │          │       │
│  │  AlertManager │ AlertCorrelation │ etc.            │       │
│  └───────────────────────┬───────────────────────────┘       │
│  ┌───────────────────────┴───────────────────────────┐       │
│  │  Hosted Services                                  │       │
│  │  ComplianceWatch │ Escalation │ OverdueScan │      │       │
│  │  SessionCleanup │ RetentionCleanup                │       │
│  └───────────────────────────────────────────────────┘       │
└──────────────────────────┬───────────────────────────────────┘
                           │
         ┌─────────────────┼─────────────────┐
         ▼                 ▼                 ▼
┌──────────────┐  ┌──────────────┐  ┌──────────────┐
│ Ato.Copilot  │  │ Ato.Copilot  │  │ Azure SDKs   │
│ .Core        │  │ .State       │  │              │
│ ├─ DbContext │  │ ├─ Agent     │  │ ├─ ARM       │
│ ├─ Models    │  │ │  State     │  │ ├─ Resource  │
│ ├─ Config    │  │ ├─ Conver-   │  │ │  Graph     │
│ ├─ Constants │  │ │  sation    │  │ ├─ Policy    │
│ └─ Interfaces│  │ └─ State     │  │ ├─ Defender  │
└──────────────┘  └──────────────┘  │ └─ Entra ID  │
                                    └──────────────┘
```

---

## Project Structure

```
ato-copilot/
├── Ato.Copilot.sln
├── Dockerfile
├── docker-compose.mcp.yml
├── docs/                              # User-facing documentation
├── specs/                             # Specify toolkit feature specs
├── src/
│   ├── Ato.Copilot.Core/             # Domain layer
│   │   ├── Configuration/            # Options classes (9+)
│   │   ├── Constants/                 # Roles, frameworks, control families
│   │   ├── Data/Context/             # AtoCopilotContext (EF Core)
│   │   ├── Interfaces/               # Service contracts
│   │   ├── Models/                    # Entity models
│   │   │   ├── Compliance/           # Assessments, findings, alerts, etc.
│   │   │   ├── Kanban/               # Boards, tasks, comments, history
│   │   │   └── Auth/                 # CAC sessions, PIM, JIT
│   │   └── Extensions/               # CoreServiceExtensions (DI)
│   │
│   ├── Ato.Copilot.State/            # State management
│   │   ├── Abstractions/             # IAgentStateManager, IConversationStateManager
│   │   ├── Implementations/          # InMemory implementations
│   │   └── Extensions/               # StateServiceExtensions (DI)
│   │
│   ├── Ato.Copilot.Agents/           # Agent framework + implementations
│   │   ├── Common/                    # BaseAgent, BaseTool (abstract bases)
│   │   ├── Compliance/
│   │   │   ├── Agents/               # ComplianceAgent, ConfigurationAgent
│   │   │   ├── Tools/                # 65+ tool implementations
│   │   │   ├── Services/             # Business logic services
│   │   │   ├── Configuration/        # Agent-specific options
│   │   │   └── Prompts/              # Embedded LLM system prompts
│   │   └── Extensions/               # ServiceCollectionExtensions (DI)
│   │
│   └── Ato.Copilot.Mcp/              # MCP server host
│       ├── Program.cs                 # Entry point (dual-mode)
│       ├── Server/                    # McpServer, McpHttpBridge, McpStdioService
│       ├── Tools/                     # ComplianceMcpTools (facade)
│       ├── Middleware/                # Auth, audit logging
│       ├── Prompts/                   # MCP prompt registry
│       └── Extensions/               # McpServiceExtensions (DI)
│
└── tests/
    ├── Ato.Copilot.Tests.Unit/        # xUnit + FluentAssertions + Moq
    └── Ato.Copilot.Tests.Integration/ # TestServer integration tests
```

---

## Transport Layer — MCP Protocol

### Dual-Mode Server

The server supports two transport modes, selected at startup:

| Mode | Flag | Transport | Use Case |
|---|---|---|---|
| **HTTP** | `--http` | REST API (ASP.NET Core Minimal APIs) | Web apps, dashboards, direct API access |
| **Stdio** | `--stdio` | JSON-RPC over stdin/stdout | GitHub Copilot, Claude Desktop, CLI tools |

Mode is determined by (in priority order):
1. Command-line flag (`--stdio` / `--http`)
2. Environment variable (`ATO_RUN_MODE`)
3. Auto-detect: redirected stdin → stdio; otherwise HTTP

### HTTP Endpoints

Mapped by `McpHttpBridge` using ASP.NET Core minimal APIs:

| Route | Method | Purpose |
|---|---|---|
| `/` | GET | Server info (version, endpoints) |
| `/health` | GET | Health check with agent status |
| `/mcp/tools` | GET | List all MCP tool definitions |
| `/mcp/chat` | POST | Natural language chat (`ChatRequest` → `AgentResponse`) |
| `/mcp` | POST | MCP JSON-RPC (`tools/list`, `tools/call`, `initialize`, `ping`) |

### Stdio (JSON-RPC)

`McpStdioService` is a `BackgroundService` that delegates to `McpServer.StartAsync()`. The server reads newline-delimited JSON-RPC messages from stdin and writes responses to stdout. Protocol version: `2024-11-05`.

**Supported methods:**
- `initialize` — Returns server capabilities and protocol version
- `tools/list` — Returns all tool definitions with JSON Schema
- `tools/call` — Executes a tool by name with arguments
- `prompts/list` / `prompts/get` — MCP prompt catalog
- `ping` — Connectivity check

### Middleware Pipeline

HTTP requests pass through a six-layer middleware stack (ordering enforced per Constitution R-012):

```
Request → CorrelationId → Serilog Request Logging → CORS
       → CacAuthentication → ComplianceAuthorization → AuditLogging
       → Endpoint
```

1. **CorrelationId** — Attaches a unique ID to every request for distributed tracing
2. **Serilog** — Structured request/response logging
3. **CORS** — Configurable allowed origins
4. **CacAuthentication** — CAC/PIV certificate authentication (JWT `amr` claim)
5. **ComplianceAuthorization** — Multi-tier auth (CAC → PIM → RBAC), tool-level access control
6. **AuditLogging** — Records all requests with user, tool, duration, outcome

---

## Agent Framework

### BaseAgent

Abstract base class that all agents must extend (Constitution Principle II):

```csharp
public abstract class BaseAgent
{
    public abstract string AgentId { get; }
    public abstract string AgentName { get; }
    public abstract string Description { get; }

    public List<BaseTool> Tools { get; }

    public abstract string GetSystemPrompt();
    public abstract Task<AgentResponse> ProcessAsync(
        string message, AgentConversationContext context, CancellationToken ct);

    protected void RegisterTool(BaseTool tool);
}
```

**Supporting types:**
- `AgentResponse` — `Success`, `Response`, `AgentName`, `ToolsExecuted`, `ProcessingTimeMs`
- `AgentConversationContext` — `ConversationId`, `UserId`, `MessageHistory`, `WorkflowState`

### BaseTool

Abstract base with instrumented execution (Template Method pattern):

```csharp
public abstract class BaseTool
{
    public abstract string Name { get; }
    public abstract string Description { get; }
    public abstract IReadOnlyDictionary<string, ToolParameter> Parameters { get; }

    // Override this — your tool logic goes here
    public abstract Task<string> ExecuteCoreAsync(
        Dictionary<string, object?> arguments, CancellationToken ct);

    // Instrumented wrapper — do NOT override
    public async Task<string> ExecuteAsync(
        Dictionary<string, object?> arguments, CancellationToken ct);
}
```

`ExecuteAsync` wraps `ExecuteCoreAsync` with:
- `Stopwatch` timing
- `ToolMetrics.RecordStart()` / `RecordSuccess()` / `RecordError()`
- Exception handling with structured error responses

### ComplianceAgent

The primary agent (1300+ lines). Registers **65+ tools** across 7 domains via constructor injection.

**Processing flow:**
1. **Auth gate** — `CheckAuthGateAsync()` validates CAC/PIM requirements
2. **Intent classification** — `ClassifyIntent()` maps keywords to tool categories
3. **Tool routing** — `RouteToToolAsync()` selects and executes the appropriate tool
4. **Context-aware resolution** — Resolves ordinal references ("the first one") from stored task lists
5. **Post-operation offers** — `AppendDeactivationOfferAsync()` suggests PIM deactivation after privileged ops
6. **Audit logging** — Records the operation outcome

### ConfigurationAgent

Lightweight agent for managing Azure subscription, framework, and scan settings. Two tools: `configuration_manage` and `configuration_chat`.

### Intent Routing

`ComplianceAgent.ClassifyIntent()` uses keyword matching to route natural language to tools:

| Keywords | Routed To |
|---|---|
| `assess`, `scan`, `evaluate` | `ComplianceAssessmentTool` |
| `control family`, `control families` | `ControlFamilyTool` |
| `generate`, `ssp`, `poam`, `sar` | `DocumentGenerationTool` |
| `evidence`, `collect evidence` | `EvidenceCollectionTool` |
| `remediate`, `fix` | `RemediationExecuteTool` |
| `board`, `kanban` | Kanban tools |
| `watch`, `monitor`, `alert` | Compliance Watch tools |
| `pim`, `activate`, `elevate` | PIM tools |
| `configure`, `settings` | ConfigurationAgent handoff |

### System Prompts

Loaded from embedded resources at runtime via `Assembly.GetManifestResourceStream`:

| Prompt File | Purpose |
|---|---|
| `ComplianceAgent.prompt.txt` | Main agent persona — ISSO/ISSM assistant, all tool descriptions, workflows |
| `KanbanAgent.prompt.txt` | Kanban operations, column flow, transition rules, RBAC |
| `PimAgent.prompt.txt` | PIM activation flow, duration estimation, justification format |

---

## Tool Inventory

### Core Compliance (11 tools)

| Tool | Description |
|---|---|
| `compliance_assess` | Run NIST 800-53 compliance assessment |
| `compliance_get_control_family` | Get control family details and Azure mapping |
| `compliance_generate_document` | Generate SSP, POA&M, SAR documents |
| `compliance_collect_evidence` | Collect compliance evidence from Azure |
| `compliance_remediate` | Remediate compliance findings |
| `compliance_validate_remediation` | Validate applied remediations |
| `compliance_generate_plan` | Generate prioritized remediation plan |
| `compliance_audit_log` | Get assessment audit trail |
| `compliance_history` | Get compliance history and trends |
| `compliance_status` | Get current compliance posture |
| `compliance_monitoring` | Continuous compliance monitoring |

### Remediation Kanban (18 tools)

| Tool | Description |
|---|---|
| `kanban_create_board` | Create a board (from assessment or manual) |
| `kanban_board_show` | Show board overview with column breakdown |
| `kanban_get_task` | Get full task details |
| `kanban_create_task` | Create a task manually |
| `kanban_assign_task` | Assign or unassign a task |
| `kanban_move_task` | Status transition between columns |
| `kanban_task_list` | List and filter tasks |
| `kanban_task_history` | View task audit trail |
| `kanban_task_validate` | Run validation scan |
| `kanban_add_comment` | Add a comment |
| `kanban_task_comments` | List comments |
| `kanban_edit_comment` | Edit a comment (24h window) |
| `kanban_delete_comment` | Delete a comment (1h window) |
| `kanban_remediate_task` | Execute remediation script |
| `kanban_collect_evidence` | Collect task-scoped evidence |
| `kanban_bulk_update` | Bulk assign/move/setDueDate |
| `kanban_export` | Export to CSV or POA&M |
| `kanban_archive_board` | Archive a board |

### CAC Authentication (4 tools)

| Tool | Description |
|---|---|
| `cac_status` | Check CAC/PIV session status |
| `cac_sign_out` | End CAC session |
| `cac_set_timeout` | Configure session timeout |
| `cac_map_certificate` | Map certificate to role |

### PIM — Privileged Identity Management (9 tools)

| Tool | Description |
|---|---|
| `pim_list_eligible` | List eligible PIM roles |
| `pim_list_active` | List active PIM roles |
| `pim_activate_role` | Activate an eligible PIM role |
| `pim_deactivate_role` | Deactivate a PIM role |
| `pim_extend_role` | Extend an active PIM session |
| `pim_approve_request` | Approve a PIM request |
| `pim_deny_request` | Deny a PIM request |
| `pim_history` | View PIM activation history |
| `jit_request_access` | Request JIT VM access |

### JIT VM Access (3 tools)

| Tool | Description |
|---|---|
| `jit_request_access` | Request just-in-time VM access |
| `jit_list_sessions` | List active JIT sessions |
| `jit_revoke_access` | Revoke JIT access |

### Compliance Watch (23 tools)

| Tool | Description |
|---|---|
| `watch_enable_monitoring` | Enable continuous monitoring |
| `watch_disable_monitoring` | Disable monitoring |
| `watch_configure_monitoring` | Configure monitoring settings |
| `watch_monitoring_status` | Get monitoring status |
| `watch_show_alerts` | List/filter alerts |
| `watch_get_alert` | Get alert details |
| `watch_acknowledge_alert` | Acknowledge an alert |
| `watch_fix_alert` | Remediate an alert |
| `watch_dismiss_alert` | Dismiss an alert |
| `watch_create_rule` | Create custom alert rule |
| `watch_list_rules` | List alert rules |
| `watch_suppress_alerts` | Suppress alerts (control/severity) |
| `watch_configure_quiet_hours` | Set quiet hours |
| `watch_remove_suppression` | Remove suppression rule |
| `watch_configure_notifications` | Configure notification channels |
| `watch_configure_escalation` | Configure escalation paths |
| `watch_alert_history` | Natural language alert history |
| `watch_compliance_trend` | Compliance trend over time |
| `watch_alert_statistics` | Alert statistics dashboard |
| `watch_create_task_from_alert` | Create Kanban task from alert |
| `watch_collect_evidence_from_alert` | Collect evidence for alert |
| `watch_create_auto_remediation_rule` | Create auto-remediation rule |
| `watch_list_auto_remediation_rules` | List auto-remediation rules |

### Configuration (2 tools)

| Tool | Description |
|---|---|
| `configuration_manage` | Manage subscription/framework/scan settings |
| `configuration_chat` | Natural language configuration |

---

## Database Layer

### Provider Strategy

The system supports dual database providers, selected via configuration:

| Provider | Config Value | Use Case |
|---|---|---|
| **SQLite** | `"SQLite"` (default) | Development, single-instance deployment |
| **SQL Server** | `"SqlServer"` | Production, multi-instance deployment |

Provider selection happens at DI registration time in `CoreServiceExtensions`. SQL Server includes retry-on-failure (5 retries, 30s max delay).

### DbContext

`AtoCopilotContext` (640 lines) manages 24 DbSets:

| Domain | DbSets |
|---|---|
| **Core Compliance** | `Assessments`, `Findings`, `Evidence`, `Documents`, `NistControls`, `RemediationPlans` |
| **Kanban** | `RemediationBoards`, `RemediationTasks`, `TaskComments`, `TaskHistoryEntries` |
| **Authentication** | `CacSessions`, `JitRequests`, `CertificateRoleMappings` |
| **Compliance Watch** | `ComplianceAlerts`, `AlertIdCounters`, `AlertNotifications`, `MonitoringConfigurations`, `ComplianceBaselines`, `AlertRules`, `SuppressionRules`, `EscalationPaths`, `ComplianceSnapshots`, `AutoRemediationRules` |
| **Audit** | `AuditLogs` |

### Entity Model

**Core entities (from ComplianceModels.cs, 1265 lines):**

| Entity | Purpose | Key Relationships |
|---|---|---|
| `ComplianceAssessment` | Assessment run result | 1:N Findings |
| `ComplianceFinding` | Individual non-compliant finding | N:1 Assessment |
| `NistControl` | NIST 800-53 control catalog entry | Self-referential (enhancements) |
| `RemediationPlan` | Prioritized remediation plan | Owns RemediationSteps |
| `ComplianceEvidence` | Collected audit evidence | Standalone |
| `ComplianceDocument` | Generated SSP/POA&M/SAR | Owns DocumentMetadata |
| `AuditLogEntry` | Immutable audit record | Standalone |
| `ComplianceAlert` | Compliance watch alert | 1:N Notifications, self-referential (child alerts) |
| `MonitoringConfiguration` | Per-subscription monitoring config | Standalone |
| `ComplianceBaseline` | Captured compliance snapshot | Standalone |

**Kanban entities (from KanbanModels.cs):**

| Entity | Purpose | Key Relationships |
|---|---|---|
| `RemediationBoard` | Kanban board | 1:N Tasks, N:1 Assessment (optional) |
| `RemediationTask` | Remediation work item | N:1 Board, 1:N Comments, 1:N History |
| `TaskComment` | Comment on a task | N:1 Task |
| `TaskHistoryEntry` | Immutable change record | N:1 Task |

**Key enums (17 total):** `FindingSeverity`, `FindingStatus`, `AssessmentStatus`, `ScanSourceType`, `AlertStatus`, `AlertType`, `AlertSeverity`, `MonitoringFrequency`, `MonitoringMode`, `TaskStatus`, `HistoryEventType`, and more.

### Concurrency

The system uses **optimistic concurrency** via a `ConcurrentEntity` base class:

```csharp
public abstract class ConcurrentEntity
{
    public Guid RowVersion { get; set; }
}
```

`AtoCopilotContext.SaveChangesAsync` auto-regenerates `RowVersion` for all modified `ConcurrentEntity` instances before saving. On conflict, EF Core throws `DbUpdateConcurrencyException`, which tools catch and return as `CONCURRENCY_CONFLICT` errors.

### Migrations

EF Core migrations are applied **automatically at startup** via `MigrateDatabaseAsync` in `Program.cs`. The migration has a 30-second timeout — if it fails, the application exits with code 1 (fail-fast).

---

## State Management

Two in-memory state managers (registered as Singletons):

| Interface | Implementation | Storage | Purpose |
|---|---|---|---|
| `IAgentStateManager` | `InMemoryAgentStateManager` | `ConcurrentDictionary<string, object>` | Per-agent key/value state |
| `IConversationStateManager` | `InMemoryConversationStateManager` | `ConcurrentDictionary<string, ConversationState>` | Conversation lifecycle |

These are abstracted behind interfaces for potential future persistence (Redis, database-backed, etc.).

---

## Security Architecture

### Authentication Tiers

Tools are classified into three security tiers (`AuthTierClassification`):

| Tier | PIM Level | Examples | Requirements |
|---|---|---|---|
| **Tier 1** | `None` | `compliance_chat`, `compliance_status`, Kanban read ops | No special auth |
| **Tier 2a** | `Read` | `compliance_assess`, `compliance_collect_evidence`, `pim_list_eligible` | Active PIM role (any level) |
| **Tier 2b** | `Write` | `compliance_remediate`, `pim_activate_role`, `jit_request_access` | PIM Contributor+ role |

The `ComplianceAuthorizationMiddleware` enforces these tiers in order:
1. Health/development bypass
2. CAC session verification (if configured)
3. PIM tier enforcement
4. RBAC role check

### RBAC Roles

Six roles with hierarchical permissions:

| Role | Access Level |
|---|---|
| `Administrator` | Full access to all operations |
| `Auditor` | Read-only access to all data including audit trails |
| `Analyst` | Assessment and monitoring, no approval authority |
| `Viewer` | Read-only access to assessments and status |
| `SecurityLead` | Create tasks, assign to engineers, approve |
| `PlatformEngineer` | Work assigned tasks, comment, self-assign |

### Audit Logging

`AuditLoggingMiddleware` captures every request with:
- Correlation ID (distributed tracing)
- User ID (redacted — first 8 chars + `***`)
- Agent name, tool name
- HTTP method, path, status code
- Duration (milliseconds)

Logs are written via Serilog to both console and rolling file (`logs/ato-copilot-*.log`, 14-day retention).

---

## Dependency Injection

Service registration is organized into four extension methods called from `Program.cs`:

```csharp
void RegisterCoreServices(IServiceCollection services, IConfiguration configuration)
{
    services.AddAtoCopilotCore(configuration);      // Core (DB, config, Azure)
    services.AddInMemoryStateManagement();           // State
    services.AddComplianceAgent(configuration);      // Agent + 65+ tools + services
    services.AddConfigurationAgent();                // ConfigurationAgent
    services.AddMcpServer(configuration);            // MCP server + bridge
}
```

**Tool registration pattern:** Tools are registered as **Singletons** (they use `IServiceScopeFactory` internally to create scoped database access). Each tool is dual-registered — as its concrete type AND as `BaseTool` — enabling both direct injection and collection resolution.

**Service lifetimes:**
| Lifetime | Services |
|---|---|
| **Singleton** | All tools, agents, Azure services, monitoring services, state managers |
| **Scoped** | `IKanbanService`, `ICacSessionService`, `IPimService`, `IJitVmAccessService`, `IUserContext` |
| **Hosted** | `ComplianceWatchHostedService`, `EscalationHostedService`, `OverdueScanHostedService`, `SessionCleanupHostedService`, `RetentionCleanupHostedService` |

---

## Observability

| Concern | Implementation |
|---|---|
| **Structured logging** | Serilog (console + rolling file, 14-day retention) |
| **Request correlation** | `CorrelationIdMiddleware` attaches unique ID to every request |
| **Tool metrics** | `ToolMetrics.RecordStart/Success/Error` on every tool execution |
| **Health checks** | `/health` endpoint with `AgentHealthCheck` |
| **Sensitive data** | `SensitiveDataDestructuringPolicy` redacts PII from logs |
| **Audit trail** | `AuditLoggingMiddleware` + `AuditLogEntry` entity (immutable) |

---

## Key Design Patterns

| Pattern | Where | Purpose |
|---|---|---|
| **Template Method** | `BaseTool.ExecuteAsync` wraps `ExecuteCoreAsync` | Automatic instrumentation on all tools |
| **Facade** | `ComplianceMcpTools` wraps all tools | Adapt agent tools to MCP protocol |
| **Strategy** | DB provider selection (SQLite/SqlServer) | Environment-appropriate storage |
| **Observer** | `ActivityLogEventSource` → event-driven monitoring | Real-time compliance drift detection |
| **Singleton + ScopeFactory** | All tools | Thread-safe tools with scoped DB access |
| **Optimistic Concurrency** | `ConcurrentEntity` + `RowVersion` | Lock-free concurrent edits |
| **Standard Envelope** | `ToolResponse<T>` (Constitution Principle VII) | Consistent success/error response format |
| **Agent Handoff** | `McpServer.ClassifyAndRouteAgent` | Intent-based routing between agents |
| **Middleware Pipeline** | 6-layer HTTP middleware | Separation of cross-cutting concerns |
| **Dual Transport** | HTTP + stdio in same binary | Support MCP clients and REST API |
