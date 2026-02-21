# ATO Copilot

Compliance-only MCP agent server for NIST 800-53 / FedRAMP authorization support on Azure Government.

Built from the [Platform Engineering Copilot](https://github.com/azurenoops/platform-engineering-copilot) architecture using the BaseAgent/BaseTool pattern.

## Features

- **NIST 800-53 Compliance Assessment** — Run quick, policy, or full scans against Azure subscriptions
- **Control Family Browser** — Get detailed info on AC, AU, IA, CM, SC, and all 20 control families
- **Document Generation** — Generate SSP, POA&M, SAR, and assessment reports
- **Evidence Collection** — Collect and store compliance evidence for audit
- **Automated Remediation** — Guided or automated fixes with dry-run support
- **Continuous Monitoring** — Real-time compliance posture tracking and alerting
- **Audit Trail** — Full history of compliance assessments and remediations
- **Configuration Agent** — Manage Azure subscription, framework, and scan settings via natural language
- **Agent Handoff** — Automatic intent-based routing between Configuration and Compliance agents
- **RBAC Enforcement** — Role-based access control (Viewer, Operator, Administrator, Auditor)
- **Dual-Mode MCP** — Stdio (GitHub Copilot, Claude Desktop) + HTTP REST API

## Quick Start

### Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- Azure subscription (Azure Government preferred)
- Azure CLI (`az login`)

### Build & Test

```bash
dotnet build Ato.Copilot.sln
dotnet test Ato.Copilot.sln
```

### Run (HTTP mode)

```bash
cd src/Ato.Copilot.Mcp
dotnet run -- --http
```

Server starts at `http://localhost:3001`. Try:
- `GET /health` — Health check with capabilities
- `GET /mcp/tools` — List available compliance tools
- `POST /mcp/chat` — Send a compliance or configuration question
- `POST /mcp` — MCP JSON-RPC endpoint (tools/list, tools/call)

### Run (stdio mode)

```bash
cd src/Ato.Copilot.Mcp
dotnet run -- --stdio
```

### Docker

```bash
cp .env.example .env
# Edit .env with your Azure credentials
docker compose -f docker-compose.mcp.yml up --build
```

## MCP Tools

### Compliance Tools

| Tool | Description |
|------|-------------|
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
| `compliance_chat` | Natural language compliance interaction |

### Configuration Tools

| Tool | Description |
|------|-------------|
| `configuration_manage` | Manage Azure subscription, framework, and scan settings |
| `configuration_chat` | Natural language configuration interaction |

## Project Structure

```
ato-copilot/
├── Ato.Copilot.sln
├── src/
│   ├── Ato.Copilot.Core/          # Models, interfaces, configuration, EF Core
│   ├── Ato.Copilot.State/         # In-memory state management
│   ├── Ato.Copilot.Agents/        # BaseAgent/BaseTool + agents & services
│   │   ├── Common/                # BaseAgent, BaseTool
│   │   └── Compliance/            # ComplianceAgent, ConfigurationAgent
│   │       ├── Agents/            # Agent implementations
│   │       ├── Tools/             # 14 tool implementations
│   │       └── Configuration/     # Agent options
│   └── Ato.Copilot.Mcp/           # MCP server (stdio + HTTP)
│       ├── Server/                # McpServer, McpHttpBridge, McpStdioService
│       ├── Middleware/            # Auth, audit logging
│       └── Tools/                 # ComplianceMcpTools (MCP tool definitions)
├── tests/
│   ├── Ato.Copilot.Tests.Unit/        # 170 unit tests (xUnit + FluentAssertions + Moq)
│   └── Ato.Copilot.Tests.Integration/ # 20 integration tests (TestServer)
├── specs/                          # Specify toolkit specs
├── Dockerfile
└── docker-compose.mcp.yml
```

## Architecture

```
MCP Client (GitHub Copilot / Claude Desktop / HTTP REST)
    │
    ▼
┌────────────────────────────────────────┐
│  Ato.Copilot.Mcp                       │
│  ├── McpServer (stdio JSON-RPC)        │
│  │   └── Agent Handoff (intent routing)│
│  ├── McpHttpBridge (REST API)          │
│  ├── ComplianceMcpTools (tool defs)    │
│  └── Middleware (Auth + Audit)         │
└────────────┬───────────────────────────┘
             │
     ┌───────┴───────┐
     ▼               ▼
┌────────────┐  ┌──────────────────┐
│ Config     │  │ Compliance Agent │
│ Agent      │  │ (12 tools)       │
│ (2 tools)  │  │  ├── Assessment  │
└────────────┘  │  ├── Remediation │
                │  ├── Evidence    │
                │  ├── Documents   │
                │  └── Monitoring  │
                └──────┬───────────┘
                       │
             ┌─────────┼──────────┐
             ▼         ▼          ▼
┌──────────────┐ ┌──────────┐ ┌────────────┐
│ Core Services│ │ State    │ │ Azure SDKs │
│ ├── Models   │ │ ├── Agent│ │ ├── ARG    │
│ ├── DbContext│ │ ├── Conv │ │ ├── Policy │
│ └── Config   │ │ └── RBAC │ │ └── MDfC   │
└──────────────┘ └──────────┘ └────────────┘
```

## Configuration

Copy `.env.example` to `.env` and configure:

```bash
ATO_RUN_MODE=http                               # stdio | http
ATO_AZURE_AD__TENANT_ID=your-tenant-id
ATO_AZURE_AD__CLIENT_ID=your-client-id
ATO_GATEWAY__AZURE__SUBSCRIPTION_ID=your-sub-id
ATO_GATEWAY__AZURE__CLOUD_ENVIRONMENT=AzureGovernment
```

Key configuration sections in [appsettings.json](src/Ato.Copilot.Mcp/appsettings.json):

| Section | Description |
|---------|-------------|
| `AzureAd` | Azure AD / Entra ID authentication settings |
| `Gateway` | Azure connection settings (tenant, subscription, managed identity) |
| `Database` | SQLite (dev) / SQL Server (prod) provider and timeouts |
| `NistCatalog` | NIST SP 800-53 catalog source (online/offline, cache) |
| `Agents:Compliance` | Default framework, impact level, supported frameworks |
| `RateLimits` | Per-service rate limits (Resource Graph, Policy, Defender) |
| `FeatureFlags` | Enable/disable scans, evidence, documents, remediation |
| `Performance` | Concurrency, timeouts, memory budget, page sizes |
| `Serilog` | Structured logging (console + rolling file) |

## Compliance Frameworks

| Framework | Support Level |
|-----------|--------------|
| NIST 800-53 Rev 5 | Full |
| FedRAMP High | Full |
| FedRAMP Moderate | Full |
| DoD IL2 | Supported |
| DoD IL4 | Supported |
| DoD IL5 | Supported |

## Testing

```bash
# Run all tests
dotnet test Ato.Copilot.sln

# Run unit tests only
dotnet test tests/Ato.Copilot.Tests.Unit/

# Run integration tests only
dotnet test tests/Ato.Copilot.Tests.Integration/
```

- **170 unit tests** — Services, agents, tools, middleware, state management
- **20 integration tests** — End-to-end HTTP endpoint testing with TestServer

## Specify Toolkit

This project uses the [Specify](https://github.com/specify-dev/specify) spec-driven development workflow:

```bash
# Create a new feature
.specify/scripts/bash/create-new-feature.sh "Add evidence API"

# Set up implementation plan
.specify/scripts/bash/setup-plan.sh

# Check prerequisites
.specify/scripts/bash/check-prerequisites.sh

# Update agent context files
.specify/scripts/bash/update-agent-context.sh copilot
```

## License

Proprietary. All rights reserved.
