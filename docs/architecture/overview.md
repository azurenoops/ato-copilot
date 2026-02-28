# Architecture Overview

> System architecture, component diagram, data flow, and deployment topology for ATO Copilot.

---

## Table of Contents

- [System Overview](#system-overview)
- [Component Architecture](#component-architecture)
- [Data Flow](#data-flow)
- [Deployment Topology](#deployment-topology)
- [Technology Stack](#technology-stack)
- [Cross-Cutting Concerns](#cross-cutting-concerns)

---

## System Overview

ATO Copilot is a compliance-focused MCP (Model Context Protocol) agent server built on .NET 9.0. It provides end-to-end RMF lifecycle management — from system registration through continuous monitoring — accessible via natural language through AI coding assistants, Teams bots, or REST APIs.

### Design Principles

1. **Chat-first** — All operations accessible through natural language
2. **Compliance-native** — Built for NIST 800-53 / FedRAMP / DoD IL authorization workflows
3. **Auditable** — Every action logged with immutable audit trails
4. **Persona-driven** — Four RMF personas (ISSM, SCA, Engineer, AO) with tailored workflows
5. **Dual-transport** — HTTP REST and MCP stdio in a single binary

---

## Component Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│                         MCP Clients                                 │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐           │
│  │ GitHub   │  │ Claude   │  │ VS Code  │  │ Teams    │           │
│  │ Copilot  │  │ Desktop  │  │ @ato     │  │ Bot      │           │
│  └────┬─────┘  └────┬─────┘  └────┬─────┘  └────┬─────┘           │
│       │ stdio        │ stdio       │ HTTP        │ HTTP            │
└───────┴──────────────┴─────────────┴─────────────┴─────────────────┘
                           │
┌──────────────────────────┴──────────────────────────────────────────┐
│  Ato.Copilot.Mcp (Entry Point — ASP.NET Core 9.0)                  │
│                                                                     │
│  ┌─────────────┐  ┌──────────────┐  ┌────────────────────┐         │
│  │ McpStdioSvc │  │ McpHttpBridge│  │ ComplianceMcpTools │         │
│  │ Background  │  │ Minimal APIs │  │ Facade (100+ tools)│         │
│  └──────┬──────┘  └──────┬───────┘  └─────────┬──────────┘         │
│         └────────┬───────┘                     │                    │
│           ┌──────┴──────┐                      │                    │
│           │  McpServer  │◄─────────────────────┘                    │
│           └──────┬──────┘                                           │
│  ┌───────────────┴─────────────────────────────────────┐            │
│  │  Middleware Pipeline                                │            │
│  │  CorrelationId → Serilog → CORS → CacAuth →        │            │
│  │  ComplianceAuth → AuditLogging                      │            │
│  └─────────────────────────────────────────────────────┘            │
└──────────────────────────┬──────────────────────────────────────────┘
                           │
┌──────────────────────────┴──────────────────────────────────────────┐
│  Ato.Copilot.Agents (Agent Framework)                               │
│                                                                     │
│  ┌────────────────────┐  ┌────────────────────┐                     │
│  │  ComplianceAgent   │  │ ConfigurationAgent │                     │
│  │  100+ tools        │  │ 2 tools            │                     │
│  │  RMF step routing  │  └────────────────────┘                     │
│  │  AI + deterministic│                                             │
│  └────────┬───────────┘                                             │
│           │                                                         │
│  ┌────────┴──────────────────────────────────────────────┐          │
│  │  Tool Categories                                      │          │
│  │  ┌─────────┐ ┌────────┐ ┌──────┐ ┌─────┐ ┌────────┐ │          │
│  │  │ RMF     │ │ Kanban │ │ CAC  │ │ PIM │ │ Watch  │ │          │
│  │  │ (56)    │ │ (18)   │ │ (4)  │ │ (15)│ │ (23)   │ │          │
│  │  └─────────┘ └────────┘ └──────┘ └─────┘ └────────┘ │          │
│  └───────────────────────────────────────────────────────┘          │
│                                                                     │
│  ┌───────────────────────────────────────────────────────┐          │
│  │  Services Layer                                       │          │
│  │  RmfLifecycle │ Categorization │ Baseline │ Ssp      │          │
│  │  Assessment │ Authorization │ ConMon │ eMASS        │          │
│  │  AtoCompliance │ Remediation │ KanbanService        │          │
│  └───────────────────────────────────────────────────────┘          │
│                                                                     │
│  ┌───────────────────────────────────────────────────────┐          │
│  │  Hosted Services                                      │          │
│  │  ComplianceWatch │ Escalation │ OverdueScan │          │          │
│  │  SessionCleanup │ RetentionCleanup │ CacheWarmup      │          │
│  └───────────────────────────────────────────────────────┘          │
└──────────────────────────┬──────────────────────────────────────────┘
                           │
         ┌─────────────────┼─────────────────┐
         ▼                 ▼                 ▼
┌──────────────┐  ┌──────────────┐  ┌──────────────┐
│ Ato.Copilot  │  │ Ato.Copilot  │  │ Azure SDKs   │
│ .Core        │  │ .State       │  │              │
│ ├─ DbContext │  │ ├─ Agent     │  │ ├─ ARM       │
│ │  (45+ sets)│  │ │  State     │  │ ├─ Resource  │
│ ├─ Models    │  │ ├─ Conver-   │  │ │  Graph     │
│ ├─ Config    │  │ │  sation    │  │ ├─ Policy    │
│ ├─ Constants │  │ └─ State     │  │ ├─ Defender  │
│ └─ Interfaces│  └──────────────┘  │ ├─ Graph     │
└──────┬───────┘                    │ └─ Entra ID  │
       │                            └──────────────┘
       ▼
┌──────────────┐  ┌──────────────┐
│  SQLite      │  │  SQL Server  │
│  (dev)       │  │  (prod)      │
└──────────────┘  └──────────────┘
```

---

## Data Flow

### Chat Request Flow

```
1. Client sends natural language message
   ↓
2. McpServer receives via HTTP POST /mcp/chat or stdio JSON-RPC
   ↓
3. Middleware pipeline:
   CorrelationId → Serilog → CORS → CacAuth → ComplianceAuth → AuditLog
   ↓
4. McpServer.ClassifyAndRouteAgent() → routes to ComplianceAgent
   ↓
5. ComplianceAgent.ProcessAsync():
   a. CheckAuthGateAsync() — RBAC + PIM tier enforcement
   b. TryProcessWithAiAsync() — LLM tool-calling (if AI enabled)
   c. RouteToToolAsync() — deterministic keyword-based fallback
   d. AppendDeactivationOfferAsync() — PIM session management
   ↓
6. Tool.ExecuteAsync() wraps ExecuteCoreAsync():
   - Stopwatch timing
   - ToolMetrics recording
   - IServiceScopeFactory for scoped DB access
   ↓
7. AgentResponse returned with structured data + Adaptive Card type
   ↓
8. Client renders response (text, Adaptive Card, webview panel)
```

### RMF Lifecycle Data Flow

```
Register    Categorize    Select       Implement    Assess       Authorize    Monitor
   │            │            │             │            │             │           │
   ▼            ▼            ▼             ▼            ▼             ▼           ▼
Registered   Security    Control      Control     Assessment   Authorization  ConMon
System       Categori-   Baseline     Implemen-   Record       Decision       Plan
   │         zation         │         tation         │             │           │
   │            │            │             │            │             │           │
   ├─ Boundary  ├─ Info     ├─ Tailoring  │         ├─ Control    ├─ Risk     ├─ Report
   │  Resources │  Types    ├─ Inheritance│         │  Effective- │  Accept-  ├─ Signif-
   │            │           │             │         │  ness       │  ances    │  icant
   ├─ RMF Role  │           │             │         │             │           │  Changes
   │  Assign-   │           │             │         ├─ Snapshot   ├─ POA&M    │
   │  ments     │           │             │         │  Data       │  Items    │
   │            │           │             │         │             │           │
   └────────────┴───────────┴─────────────┴─────────┴─────────────┴───────────┘
                              ↓
                    AtoCopilotContext (EF Core)
                              ↓
                    SQLite / SQL Server
```

### Monitoring & Alert Pipeline (Phase 17)

```
ComplianceWatchService                AlertManager             AlertNotificationService
 │ DetectDriftAsync()                    │                            │
 │  ├─ Compare baselines                │                            │
 │  ├─ EnrichAlertWithSystemAsync()     │                            │
 │  │   └─ SystemSubscriptionResolver   │                            │
 │  │       .ResolveAsync()             │                            │
 │  │       (sub → RegisteredSystemId)  │                            │
 │  └─ CreateAlertAsync(alert) ────────►│                            │
 │                                       │ Persist + correlate       │
 │  ┌─ Threshold check ───────┐         │ SendNotificationAsync() ──►│
 │  │  driftCount >= threshold │         │                            │ Channels:
 │  │  → IConMonService        │         │                            │  ├─ Chat
 │  │    .ReportChangeAsync()  │         │                            │  ├─ Email
 │  └──────────────────────────┘         │                            │  └─ Webhook
 │                                       │                            │
ConMonService                            │                            │
 │ CheckExpirationAsync()                │                            │
 │  ├─ Graduated alerts (90/60/30/exp)   │                            │
 │  └─ CreateExpirationAlertAsync() ────►│                            │
 │ ReportChangeAsync()                   │                            │
 │  └─ CreateSignificantChangeAlert() ──►│                            │
 │ GenerateReportAsync()                 │                            │
 │  └─ EnrichReportWithWatchData()       │                            │
 │      ├─ MonitoringEnabled             │                            │
 │      ├─ DriftAlertCount               │                            │
 │      ├─ AutoRemediationRuleCount      │                            │
 │      └─ LastMonitoringCheck           │                            │
```

---

## Deployment Topology

### Development (Docker Compose)

```yaml
# docker-compose.mcp.yml
services:
  sqlserver:    # SQL Server 2022 — port 1433
  mcp:          # MCP Server — port 3001 (HTTP mode)
  chat:         # Chat App — port 5001

# Bridge network: ato-net
# Persistent volumes: sqlserver-data, mcp-data
```

### Production (Azure)

```
┌─────────────────────────────────────────────────────────┐
│  Azure Container Apps / App Service                      │
│                                                          │
│  ┌────────────────┐  ┌────────────────┐                  │
│  │ MCP Server     │  │ Chat Web App   │                  │
│  │ (Container)    │  │ (Container)    │                  │
│  │ Port 3001      │  │ Port 5001      │                  │
│  └───────┬────────┘  └───────┬────────┘                  │
│          │                   │                            │
│  ┌───────┴───────────────────┴─────────┐                 │
│  │  Azure SQL Database                 │                 │
│  │  (Managed, auto-failover)           │                 │
│  └─────────────────────────────────────┘                 │
│                                                          │
│  ┌──────────────────────────┐                            │
│  │  Azure Entra ID          │ ← CAC/PIV, Managed Identity│
│  └──────────────────────────┘                            │
│                                                          │
│  ┌──────────────────────────┐                            │
│  │  Azure Key Vault         │ ← Secrets, certificates   │
│  └──────────────────────────┘                            │
└──────────────────────────────────────────────────────────┘
```

### Docker Image

- **Base**: `mcr.microsoft.com/dotnet/aspnet:9.0`
- **Build**: Multi-stage with `mcr.microsoft.com/dotnet/sdk:9.0`
- **User**: Non-root `atocopilot` (UID 1000)
- **Port**: 3001 (configurable via `ASPNETCORE_URLS`)
- **Health**: `/health` endpoint

---

## Technology Stack

| Layer | Technology | Version |
|-------|-----------|---------|
| **Runtime** | .NET | 9.0 |
| **Language** | C# | 13.0 |
| **Web Framework** | ASP.NET Core Minimal APIs | 9.0 |
| **ORM** | Entity Framework Core | 9.0 |
| **Database** | SQLite (dev) / SQL Server 2022 (prod) | — |
| **AI** | Azure OpenAI (GPT-4o) | via Microsoft.Extensions.AI |
| **Identity** | Microsoft Identity Web / Entra ID | 3.5.0 |
| **Azure SDKs** | ARM, Resource Graph, Policy, Defender | 1.13.x |
| **PDF** | QuestPDF | 2024.12.3 |
| **Excel** | ClosedXML | 0.104.2 |
| **Graph API** | Microsoft.Graph | 5.70.0 |
| **Logging** | Serilog | 4.2.0 |
| **Email** | MailKit | 4.10.0 |
| **Testing** | xUnit, FluentAssertions, Moq | latest |
| **VS Code Extension** | TypeScript, Mocha, Chai | — |
| **Teams Extension** | TypeScript, Adaptive Cards v1.5 | — |

---

## Cross-Cutting Concerns

| Concern | Implementation |
|---------|---------------|
| **Authentication** | CAC/PIV certificates, Azure Entra ID, JWT bearer tokens |
| **Authorization** | 7 RBAC roles + PIM tiers (None/Read/Write) per tool |
| **Audit Logging** | Immutable `AuditLogEntry` entities, 7-year retention |
| **Structured Logging** | Serilog with console + rolling file sinks |
| **Request Correlation** | `CorrelationIdMiddleware` on every request |
| **Optimistic Concurrency** | `ConcurrentEntity` base with auto-regenerated `RowVersion` |
| **Health Monitoring** | `/health` with EF Core + agent status checks |
| **Sensitive Data** | `SensitiveDataDestructuringPolicy` redacts PII from logs |
| **Rate Limiting** | Configurable per-API limits (Resource Graph, Policy, Remediation) |
| **Data Retention** | Assessments 3 years, audit logs 7 years (configurable) |

---

## Related Documentation

- [Data Model](data-model.md) — Entity relationships and ER diagram
- [Agent & Tool Catalog](agent-tool-catalog.md) — Complete tool inventory
- [RMF Step Map](rmf-step-map.md) — RMF phase × tool × persona matrix
- [Security Model](security.md) — RBAC, PIM, CAC, audit details
- [MCP Server API](../api/mcp-server.md) — MCP tool API reference
- [Deployment Guide](../deployment.md) — Production deployment instructions
