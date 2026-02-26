# ATO Copilot — M365 Extension for Teams

Express.js webhook server that integrates ATO Copilot with Microsoft Teams, delivering compliance assessment results as Adaptive Cards.

## Features

- **Teams Bot** — Chat with ATO Copilot directly in Microsoft Teams
- **Adaptive Cards** — Rich, intent-routed card responses for compliance, infrastructure, cost, deployment, and resource discovery
- **Azure Government** — "View in Azure Portal" links to `portal.azure.us`
- **Follow-Up Prompts** — Interactive quick-reply buttons for missing information
- **M365 Copilot Plugin** — Plugin manifest for M365 Copilot integration

## Setup

### Prerequisites

- Node.js 20 LTS or later
- ATO Copilot MCP Server running

### Installation

```bash
cd extensions/m365
npm install
```

### Configuration

Set required environment variables:

| Variable | Required | Default | Description |
|----------|----------|---------|-------------|
| `ATO_API_URL` | Yes | — | MCP Server base URL |
| `ATO_API_KEY` | No | `""` | API key if required |
| `PORT` | No | `3978` | Express server port |
| `BOT_ID` | Yes* | — | Azure Bot registration app ID |
| `BOT_PASSWORD` | Yes* | — | Azure Bot registration password |

\* Required for Bot Framework authentication. Logged as warning if missing.

### Running

```bash
# Development
ATO_API_URL=http://localhost:3001 npm run dev

# Production
npm run build
ATO_API_URL=http://localhost:3001 npm start
```

## Endpoints

| Method | Path | Description |
|--------|------|-------------|
| POST | `/api/messages` | Teams webhook — receives messages, returns Adaptive Cards |
| GET | `/health` | Health check |
| GET | `/openapi.json` | OpenAPI 3.0 specification |
| GET | `/ai-plugin.json` | M365 Copilot plugin descriptor |

## Adaptive Card Types

| Intent | Card | Description |
|--------|------|-------------|
| `compliance` | Compliance Assessment | Score with color thresholds, control counts |
| `infrastructure` | Infrastructure Result | Resource details, Azure Portal link |
| `cost` | Cost Estimate | Monthly cost breakdown |
| `deployment` | Deployment Result | Status, logs |
| `resource_discovery` | Resource List | Name, type, status table |
| (default) | Generic | Plain text response |
| (error) | Error | Error message with help text |
| (followUp) | Follow-Up | Missing fields with quick-reply buttons |

### Score Color Thresholds

- **≥80%** — Green (Good)
- **≥60%** — Orange (Warning)
- **<60%** — Red (Attention)

## Teams App Registration

1. Register a bot in the [Azure Bot Service](https://portal.azure.us/#create/Microsoft.AzureBot)
2. Set `BOT_ID` and `BOT_PASSWORD` environment variables
3. Deploy the manifest from `src/manifest/` to your Teams tenant

## Development

```bash
npm install
npm run build
npm test
```

### Testing

```bash
# Run all tests
npm test

# Health check
curl http://localhost:3978/health

# Send a test message
curl -X POST http://localhost:3978/api/messages \
  -H "Content-Type: application/json" \
  -d '{"text": "Run compliance scan", "conversation": {"id": "test-1"}, "from": {"id": "user-1"}}'
```

## License

MIT
