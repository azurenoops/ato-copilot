# Feature 018 — Quickstart

**Branch**: `018-sap-generation` | **Date**: 2026-03-04

## Prerequisites

- .NET 9.0 SDK installed
- Repository cloned and on branch `018-sap-generation`
- Previous features (015, 017) entities available (build passes)

## Build & Test

```bash
# Build
dotnet build Ato.Copilot.sln

# Run all unit tests
dotnet test tests/Ato.Copilot.Tests.Unit/Ato.Copilot.Tests.Unit.csproj

# Run SAP-specific tests only
dotnet test tests/Ato.Copilot.Tests.Unit/Ato.Copilot.Tests.Unit.csproj --filter "FullyQualifiedName~Sap"
```

## Key Files

| File | Purpose |
|------|---------|
| `src/Ato.Copilot.Core/Models/Compliance/SapModels.cs` | Entities, enum, DTOs |
| `src/Ato.Copilot.Core/Interfaces/Compliance/ISapService.cs` | Service interface |
| `src/Ato.Copilot.Agents/Compliance/Services/SapService.cs` | Service implementation |
| `src/Ato.Copilot.Agents/Compliance/Tools/SapTools.cs` | 5 MCP tools |
| `tests/Ato.Copilot.Tests.Unit/Models/SapModelTests.cs` | Entity tests |
| `tests/Ato.Copilot.Tests.Unit/Services/SapServiceTests.cs` | Service tests |
| `tests/Ato.Copilot.Tests.Unit/Tools/SapToolTests.cs` | Tool tests |

## Implementation Order

```
Phase 1: Entities + DbContext              → T001–T007
Phase 2: SapService core logic             → T008–T021
Phase 3: GenerateSapTool + DI              → T022–T025
Phase 4: Update/Finalize/Validate          → T026–T037
Phase 5: Get/List/Export                   → T038–T049
Phase 6: Integration/SAR alignment/Polish  → T050–T061
```

## MCP Tools Delivered

| Tool | Verb | RBAC |
|------|------|------|
| `compliance_generate_sap` | Generate SAP from baseline | Analyst, SecurityLead, Administrator |
| `compliance_update_sap` | Update Draft SAP | Analyst, SecurityLead, Administrator |
| `compliance_finalize_sap` | Lock SAP as Finalized | Analyst, SecurityLead, Administrator |
| `compliance_get_sap` | Retrieve SAP by ID or latest | All except Viewer |
| `compliance_list_saps` | List SAPs for a system | All except Viewer |

## Smoke Test (Manual)

1. Register a system and select a Moderate baseline
2. Generate SAP: `compliance_generate_sap { system_id: "<id>" }`
3. Verify: 325 controls, all three methods per control, inheritance annotations
4. Update SAP: `compliance_update_sap { sap_id: "<id>", schedule_start: "2026-04-01" }`
5. Finalize: `compliance_finalize_sap { sap_id: "<id>" }`
6. Verify immutability: `compliance_update_sap { sap_id: "<id>" }` → error `SAP_FINALIZED`
7. List SAPs: `compliance_list_saps { system_id: "<id>" }` → shows Finalized SAP

## Dependencies

| Service | Used For |
|---------|----------|
| `INistControlsService` | Assessment objectives from OSCAL catalog |
| `IBaselineService` | Control baseline with inheritance + tailoring |
| `IRmfLifecycleService` | System context and role assignments |
| `IStigKnowledgeService` | STIG → NIST reverse mapping via CCI chain |
| `IDocumentTemplateService` | DOCX/PDF export with merge-field schema |
| `IAssessmentArtifactService` | Evidence completeness for gap summary |
