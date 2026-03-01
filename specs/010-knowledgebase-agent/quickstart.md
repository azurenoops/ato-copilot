# Quickstart: KnowledgeBase Agent — "Compliance Library"

**Feature**: `010-knowledgebase-agent` | **Date**: 2026-02-24

## Prerequisites

- .NET 9.0 SDK installed
- Repository cloned and on branch `010-knowledgebase-agent`
- All existing tests passing: `dotnet test Ato.Copilot.sln` (2,000+ tests)

## Build & Run

```bash
# Build the solution
dotnet build Ato.Copilot.sln

# Run all tests (existing + new)
dotnet test Ato.Copilot.sln

# Run only KB-related unit tests
dotnet test tests/Ato.Copilot.Tests.Unit/Ato.Copilot.Tests.Unit.csproj --filter "FullyQualifiedName~KnowledgeBase|FullyQualifiedName~Orchestrator"

# Run integration tests
dotnet test tests/Ato.Copilot.Tests.Integration/Ato.Copilot.Tests.Integration.csproj --filter "FullyQualifiedName~KnowledgeBase|FullyQualifiedName~Orchestrator"

# Start the MCP server (stdio mode)
dotnet run --project src/Ato.Copilot.Mcp/Ato.Copilot.Mcp.csproj
```

## Configuration

Add to `appsettings.json`:

```json
{
  "AgentConfiguration": {
    "KnowledgeBaseAgent": {
      "Enabled": true,
      "MaxTokens": 4096,
      "Temperature": 0.3,
      "ModelName": "gpt-4o",
      "CacheDurationMinutes": 60,
      "KnowledgeBasePath": "KnowledgeBase/Data",
      "MinimumConfidenceThreshold": 0.3
    }
  }
}
```

## JSON Data Files

9 curated JSON data files in `src/Ato.Copilot.Agents/KnowledgeBase/Data/`:

| File | Content |
|------|---------|
| `nist-800-53-controls.json` | Supplementary NIST control data |
| `stig-controls.json` | STIG controls with check/fix text |
| `windows-server-stig-azure.json` | Windows Server STIG with Azure mappings |
| `rmf-process.json` | RMF 6-step process data |
| `rmf-process-enhanced.json` | Extended RMF with service-specific guidance |
| `impact-levels.json` | IL2-IL6 and FedRAMP baselines |
| `fedramp-templates.json` | SSP, POA&M, CRM template guidance |
| `dod-instructions.json` | DoD Instructions with control mappings |
| `navy-workflows.json` | Navy-specific authorization workflows |

Files are copied to the build output directory automatically via `<Content CopyToOutputDirectory="PreserveNewest" />` in the csproj.

## MCP Tools

7 tools with `kb_` prefix — available via MCP protocol:

| Tool ID | Description |
|---------|-------------|
| `kb_explain_nist_control` | Explain a NIST 800-53 control |
| `kb_search_nist_controls` | Search controls by keyword |
| `kb_explain_stig` | Explain a STIG control |
| `kb_search_stigs` | Search STIGs by keyword/severity |
| `kb_explain_rmf` | Explain the RMF process |
| `kb_explain_impact_level` | Explain DoD impact levels |
| `kb_get_fedramp_template_guidance` | Get FedRAMP template guidance |

## Testing a Query

With the MCP server running, send a chat message:

```json
{
  "method": "tools/call",
  "params": {
    "name": "kb_explain_nist_control",
    "arguments": {
      "control_id": "AC-2"
    }
  }
}
```

Expected: A markdown explanation of AC-2 (Account Management) with Azure RBAC/Entra ID guidance and the informational disclaimer.

## Key Architecture Decisions

1. **Orchestrator routing**: `BaseAgent.CanHandle(message)` returns confidence scores (0.0-1.0). Orchestrator picks the highest score above threshold (0.3).
2. **Offline-first**: All data from local JSON files. No external HTTP calls for knowledge data.
3. **Backward compatibility**: Existing 5 stub service interfaces preserved with additional methods added alongside.
4. **Singleton lifetime**: All services and agents registered as Singleton, consistent with existing codebase.

## File Layout

```
src/Ato.Copilot.Agents/
├── Common/BaseAgent.cs                  # MODIFIED: +CanHandle abstract method
├── KnowledgeBase/                       # NEW directory
│   ├── Agents/KnowledgeBaseAgent.cs
│   ├── Configuration/KnowledgeBaseAgentOptions.cs
│   ├── Data/*.json                      # 9 curated data files
│   ├── Prompts/KnowledgeBaseAgent.prompt.txt
│   ├── Services/ImpactLevelService.cs, FedRampTemplateService.cs
│   └── Tools/Explain*Tool.cs, Search*Tool.cs, Get*Tool.cs
├── Compliance/Services/KnowledgeBase/   # REPLACED: 5 stubs → full implementations
src/Ato.Copilot.Mcp/
├── Server/AgentOrchestrator.cs          # NEW
├── Tools/KnowledgeBaseMcpTools.cs       # NEW
```
