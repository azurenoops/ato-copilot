# Quickstart: AI-Powered Task Enrichment

**Feature**: 012-task-enrichment  
**Date**: 2026-02-25

---

## Prerequisites

- Docker Compose stack running (SQL Server, MCP Server, Chat App)
- `.env` file with `AgentAIEnabled=true` and Azure OpenAI credentials
- At least one compliance assessment completed (for findings to enrich)

## What This Feature Changes

After this feature, creating a Kanban board from an assessment will **automatically populate every task** with:

1. **Remediation Script** — AI-generated Azure CLI script (or NIST-template fallback)
2. **Script Type** — Language identifier for syntax highlighting
3. **Validation Criteria** — Step-by-step instructions to verify the fix

Previously, all tasks were created with "Not provided" for both fields.

## Quick Verification

### 1. Create a board and verify enrichment

```
User: Run a compliance assessment for my subscription
AI: [runs assessment, creates findings]

User: Create a kanban board from the latest assessment
AI: [creates board → enriches 30 tasks → reports "30 enriched, 0 skipped, 0 failed"]

User: Show me task REM-001
AI: [displays task with populated Remediation Script and Validation Criteria]
```

### 2. Generate a script on-demand

```
User: Generate a PowerShell remediation script for REM-005
AI: [calls kanban_generate_script with task_id=REM-005, script_type=PowerShell]
AI: Here's the PowerShell remediation script for AC-2.1...
```

### 3. Generate validation criteria on-demand

```
User: Generate validation criteria for REM-005
AI: [calls kanban_generate_validation with task_id=REM-005]
AI: Validation steps:
    1. Verify MFA is enabled...
    2. Run az ad user list...
    3. Re-scan for compliance...
```

### 4. Lazy enrichment on existing tasks

```
User: Show me task REM-010  (a task created before this feature)
AI: [kanban_get_task detects null script → auto-enriches → returns populated task]
```

## Key Behaviors

| Scenario | Behavior |
|----------|----------|
| AI available + AgentAIEnabled=true | AI-generated scripts via Azure OpenAI |
| AI unavailable or AgentAIEnabled=false | NIST-template deterministic scripts |
| AI call fails (timeout, 429, error) | Immediate fallback to NIST templates (no retry) |
| Informational severity task | Fixed string: "Informational finding — no remediation required" |
| Task already has script | Skip (unless `force=true`) |
| Manually created task (no FindingId) | Skip enrichment (no finding context) |

## Build & Test

```bash
# Build
dotnet build Ato.Copilot.sln

# Run all tests
dotnet test Ato.Copilot.sln

# Run only enrichment tests
dotnet test tests/Ato.Copilot.Tests.Unit --filter "FullyQualifiedName~TaskEnrichment"

# Docker rebuild
docker compose down && docker compose up --build -d
```

## New MCP Tools

| Tool | Description |
|------|-------------|
| `kanban_generate_script` | Generate/regenerate a remediation script for a task (supports AzureCli, PowerShell, Bicep, Terraform) |
| `kanban_generate_validation` | Generate/regenerate validation criteria for a task |

## Configuration

| Setting | Location | Default | Purpose |
|---------|----------|---------|---------|
| `AgentAIEnabled` | `.env` / `AzureOpenAIGateway:AgentAIEnabled` | `true` | Global AI kill switch |
| Enrichment concurrency | `TaskEnrichmentService` constant | `5` | Max parallel AI calls during board enrichment |
| Per-task timeout | `TaskEnrichmentService` constant | `30s` | Timeout for individual task enrichment |
