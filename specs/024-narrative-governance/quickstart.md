# Quickstart: Narrative Governance

**Feature**: 024-narrative-governance

---

## Prerequisites

- .NET 9.0 SDK
- Working `dotnet build Ato.Copilot.sln` with zero warnings
- Existing system with control baseline (`compliance_register_system` + `compliance_select_baseline`)
- At least one control narrative authored (`compliance_write_narrative`)

## New NuGet Dependency

```bash
# Add DiffPlex to the Agents project
dotnet add src/Ato.Copilot.Agents/Ato.Copilot.Agents.csproj package DiffPlex
```

## Database Migration

```bash
# Create migration for new entities
dotnet ef migrations add NarrativeGovernance \
  --project src/Ato.Copilot.Core \
  --startup-project src/Ato.Copilot.Mcp

# Apply migration (dev SQLite)
dotnet ef database update \
  --project src/Ato.Copilot.Core \
  --startup-project src/Ato.Copilot.Mcp
```

## Key Files to Create

| File | Purpose |
|------|---------|
| `src/Ato.Copilot.Core/Models/Compliance/NarrativeGovernanceModels.cs` | `NarrativeVersion`, `NarrativeReview` entities, `ReviewDecision` enum |
| `src/Ato.Copilot.Core/Interfaces/Compliance/INarrativeGovernanceService.cs` | Service interface for all governance operations |
| `src/Ato.Copilot.Agents/Compliance/Services/NarrativeGovernanceService.cs` | Service implementation |
| `src/Ato.Copilot.Agents/Compliance/Tools/NarrativeGovernanceTools.cs` | 8 new tool classes |
| `tests/Ato.Copilot.Tests.Unit/Compliance/NarrativeGovernanceServiceTests.cs` | Unit tests |
| `tests/Ato.Copilot.Tests.Integration/Compliance/NarrativeGovernanceToolTests.cs` | Integration tests |

## Key Files to Modify

| File | Change |
|------|--------|
| `src/Ato.Copilot.Core/Models/Compliance/SspModels.cs` | Add `ApprovalStatus`, `CurrentVersion`, `ApprovedVersionId` to `ControlImplementation` |
| `src/Ato.Copilot.Core/Data/Context/AtoCopilotContext.cs` | Add `DbSet<NarrativeVersion>`, `DbSet<NarrativeReview>`, entity configurations |
| `src/Ato.Copilot.Core/Interfaces/Compliance/ISspService.cs` | Update `WriteNarrativeAsync` signature for version support |
| `src/Ato.Copilot.Agents/Compliance/Services/SspService.cs` | Enhance `WriteNarrativeAsync` to create version records |
| `src/Ato.Copilot.Agents/Compliance/Tools/SspAuthoringTools.cs` | Add `expected_version`, `change_reason` params to `WriteNarrativeTool` |
| `src/Ato.Copilot.Agents/Extensions/ServiceCollectionExtensions.cs` | Register new service + 8 tools in DI |
| `src/Ato.Copilot.Agents/Compliance/Agents/ComplianceAgent.cs` | Register 8 new tools via `RegisterTool()` |
| `docs/architecture/agent-tool-catalog.md` | Add 8 tool entries, update `compliance_write_narrative` |
| `docs/architecture/data-model.md` | Add entities, fields, ER diagram updates |

## Build & Test

```bash
# Build
dotnet build Ato.Copilot.sln

# Unit tests
dotnet test tests/Ato.Copilot.Tests.Unit --filter "FullyQualifiedName~NarrativeGovernance"

# Integration tests
dotnet test tests/Ato.Copilot.Tests.Integration --filter "FullyQualifiedName~NarrativeGovernance"

# All tests
dotnet test Ato.Copilot.sln
```

## Verification Workflow

1. Register a system and select a baseline
2. Write a narrative → verify version 1 created
3. Update the narrative → verify version 2 created, version 1 preserved
4. View history → verify both versions returned newest-first
5. Diff versions 1 vs 2 → verify unified diff output
6. Submit for review → verify status transitions to InReview
7. Approve as ISSM → verify status transitions to Approved
8. Write new update → verify Draft version 3 created, Approved version 2 unchanged
9. Check approval progress → verify accurate counts
