# ATO Copilot — VS Code Extension

GitHub Copilot Chat participant (`@ato`) for NIST 800-53 compliance assessment, remediation planning, and ATO documentation within VS Code.

## Features

- **`@ato` Chat Participant** — Ask compliance questions directly in GitHub Copilot Chat
- **Slash Commands** — `/compliance`, `/knowledge`, `/config` for targeted agent routing
- **File Analysis** — Analyze the current editor file for compliance issues
- **Workspace Analysis** — Scan `.bicep`, `.tf`, `.yaml`, `.yml`, `.json` files across the workspace
- **Export** — Export analysis results as Markdown, JSON, or HTML
- **Template Saving** — Save generated IaC templates organized by type

## Installation

1. Open VS Code
2. Go to Extensions (`Ctrl+Shift+X` / `Cmd+Shift+X`)
3. Search for "ATO Copilot"
4. Click **Install**

### From Source

```bash
cd extensions/vscode
npm install
npm run compile
```

Then press `F5` to launch the Extension Development Host.

## Commands

| Command | Description |
|---------|-------------|
| `ATO Copilot: Check Health` | Verify connection to the MCP Server |
| `ATO Copilot: Configure` | Open extension settings |
| `ATO Copilot: Analyze Current File` | Analyze the active editor file for compliance |
| `ATO Copilot: Analyze Workspace` | Scan all supported files in the workspace |

## Settings

| Setting | Default | Description |
|---------|---------|-------------|
| `atoCopilot.apiUrl` | `http://localhost:3001` | MCP Server URL |
| `atoCopilot.apiKey` | `""` | API key for authentication |
| `atoCopilot.timeout` | `30000` | Request timeout in milliseconds |
| `atoCopilot.enableLogging` | `false` | Enable debug logging to output channel |

## Usage

### Chat with @ato

In Copilot Chat, type:

```
@ato How do I comply with AC-2 access account management?
```

### Use Slash Commands

```
@ato /compliance What controls apply to my storage accounts?
@ato /knowledge Explain FedRAMP Moderate baseline
@ato /config Show current compliance configuration
```

### Analyze Files

1. Open a `.bicep`, `.tf`, or `.yaml` file
2. Open Command Palette (`Ctrl+Shift+P` / `Cmd+Shift+P`)
3. Run **ATO Copilot: Analyze Current File for Compliance**
4. View findings in the side panel with severity-colored badges

## Requirements

- VS Code 1.90.0 or later
- GitHub Copilot Chat extension
- ATO Copilot MCP Server running (default: `http://localhost:3001`)

## Development

```bash
npm install
npm run compile
npm test
```

## License

MIT
