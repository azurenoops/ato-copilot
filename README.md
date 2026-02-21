# ATO Copilot

Compliance-only MCP agent server for NIST 800-53 / FedRAMP authorization support on Azure Government.

Built from the [Platform Engineering Copilot](https://github.com/your-org/platform-engineering-copilot) architecture using the BaseAgent/BaseTool pattern.

## Features

- **NIST 800-53 Compliance Assessment** — Run quick, policy, or full scans against Azure subscriptions
- **Control Family Browser** — Get detailed info on AC, AU, IA, CM, SC, and all 18 control families
- **Document Generation** — Generate SSP, POA&M, SAR, and assessment reports
- **Evidence Collection** — Collect and store compliance evidence for audit
- **Automated Remediation** — Guided or automated fixes with dry-run support
- **Continuous Monitoring** — Real-time compliance posture tracking and alerting
- **Audit Trail** — Full history of compliance assessments and remediations
- **Dual-Mode MCP** — Stdio (GitHub Copilot, Claude Desktop) + HTTP REST API

## Quick Start

### Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- Azure subscription (Azure Government preferred)
- Azure CLI (`az login`)

### Build

```bash
dotnet build Ato.Copilot.sln
```

### Run (HTTP mode)

```bash
cd src/Ato.Copilot.Mcp
dotnet run -- --http
```

Server starts at `http://localhost:3001`. Try:
- `GET /health` — Health check
- `GET /mcp/tools` — List available tools
- `POST /mcp/chat` — Send a compliance question

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

## Project Structure

```
ato-copilot/
├── Ato.Copilot.sln
├── src/
│   ├── Ato.Copilot.Core/          # Models, interfaces, configuration
│   ├── Ato.Copilot.State/         # In-memory state management
│   ├── Ato.Copilot.Agents/        # BaseAgent/BaseTool + ComplianceAgent
│   └── Ato.Copilot.Mcp/           # MCP server (stdio + HTTP)
├── tests/
│   └── Ato.Copilot.Tests.Unit/    # xUnit + FluentAssertions + Moq
├── .specify/                       # Specify toolkit (constitution, templates, scripts)
├── Dockerfile
├── docker-compose.mcp.yml
└── docs/
```

## Architecture

```
MCP Client (GitHub Copilot / Claude / HTTP)
    │
    ▼
┌─────────────────────────────────┐
│  Ato.Copilot.Mcp                │
│  ├── McpServer (stdio JSON-RPC) │
│  ├── McpHttpBridge (REST API)   │
│  └── ComplianceMcpTools         │
└─────────┬───────────────────────┘
          │
          ▼
┌─────────────────────────────────┐
│  Ato.Copilot.Agents             │
│  ├── BaseAgent / BaseTool       │
│  └── ComplianceAgent (11 tools) │
└─────────┬───────────────────────┘
          │
          ▼
┌─────────────────────────────────┐
│  Ato.Copilot.Core               │
│  ├── Models & Interfaces        │
│  ├── Configuration              │
│  └── EF Core DbContext          │
└─────────────────────────────────┘
```

## Configuration

Copy `.env.example` to `.env` and configure:

```bash
ATO_RUN_MODE=http                               # stdio | http
ATO_AZURE_AD__TENANT_ID=your-tenant-id
ATO_AZURE_AD__CLIENT_ID=your-client-id
ATO_GATEWAY__DEFAULT_SUBSCRIPTION_ID=your-sub-id
```

See [appsettings.json](src/Ato.Copilot.Mcp/appsettings.json) for all options.

## Compliance Frameworks

| Framework | Support Level |
|-----------|--------------|
| NIST 800-53 Rev 5 | Full |
| FedRAMP High | Full |
| FedRAMP Moderate | Full |
| DoD IL2 | Supported |
| DoD IL4 | Supported |
| DoD IL5 | Supported |

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
