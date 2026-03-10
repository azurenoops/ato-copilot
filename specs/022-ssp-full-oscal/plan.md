# Implementation Plan: Feature 022 — SSP 800-18 Full Sections + OSCAL Output

**Branch**: `022-ssp-full-oscal` | **Date**: 2026-03-10 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/022-ssp-full-oscal/spec.md`

## Summary

Complete the System Security Plan by implementing all 13 NIST SP 800-18 Rev 1 sections (currently 5 of 13), introducing structured per-section authoring with lifecycle management (NotStarted→Draft→UnderReview→Approved), producing OSCAL 1.1.2-compliant SSP JSON with full `import-profile`, `system-characteristics`, `system-implementation`, `control-implementation`, and `back-matter` sections, and providing structural validation before export. Technical approach: extend `SspService` with 3 new methods and 8 new section generators, create dedicated `OscalSspExportService` and `OscalValidationService`, add `SspSection` and `ContingencyPlanReference` entities, and register 5 new MCP tools following the existing `BaseTool` pattern.

## Technical Context

**Language/Version**: C# 13 / .NET 9.0 (`net9.0`, nullable enabled, implicit usings)
**Primary Dependencies**: EF Core 9.0.0 (SQLite + SQL Server), System.Text.Json 9.0.5, Azure.AI.OpenAI 2.1.0, Serilog 4.2.0
**Storage**: EF Core with SQLite (dev) / SQL Server (prod); InMemory provider for tests
**Testing**: xUnit 2.9.3 + FluentAssertions 7.0.0 + Moq 4.20.72 + `dotnet test`
**Target Platform**: Azure Government (Linux containers), VS Code MCP extension
**Project Type**: Web service (MCP server with stdio + HTTP modes)
**Performance Goals**: Section write <3s, full SSP generation <15s (325 controls), OSCAL export <20s, validation <5s
**Constraints**: <512MB steady-state memory, pagination required for collections, `CancellationToken` on all async operations
**Scale/Scope**: Single-system SSP with up to 13 sections, ~325 controls per baseline, ~50 authorization boundary resources

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| # | Principle | Status | Notes |
|---|-----------|--------|-------|
| I | Documentation as Source of Truth | ✅ PASS | Spec follows `/docs/` conventions; NIST 800-18 section mapping documented |
| II | BaseAgent/BaseTool Architecture | ✅ PASS | 5 new tools extend `BaseTool`; 2 new services implement interfaces; registered via `ServiceCollectionExtensions` |
| III | Testing Standards | ✅ PASS | Target ≥80% coverage (SC-009); unit tests with mocked services; integration tests for all 5 tools; boundary tests for section numbers (1–13), version concurrency, status transitions |
| IV | Azure Government & Compliance First | ✅ PASS | OSCAL 1.1.2 aligns with FedRAMP/eMASS requirements; no new Azure service interactions; existing `DefaultAzureCredential` chain preserved |
| V | Observability & Structured Logging | ✅ PASS | Tool executions auto-logged by `BaseTool.ExecuteAsync()` wrapper; new services will use Serilog structured logging |
| VI | Code Quality & Maintainability | ✅ PASS | Methods <50 lines (section generators are individual methods); DI via constructor injection; XML documentation on public types; no magic values (section numbers as constants) |
| VII | User Experience Consistency | ✅ PASS | All tool responses follow standard envelope (`status`/`data`/`metadata`); error responses include `errorCode` + `suggestion`; OSCAL validation returns actionable warnings |
| VIII | Performance Requirements | ✅ PASS | Section write <3s (<5s limit); full SSP <15s (<30s complex limit); OSCAL export <20s (<30s limit); validation <5s (<5s simple limit) |

**Gate Result**: ✅ ALL PASS — No violations requiring justification.

## Project Structure

### Documentation (this feature)

```text
specs/022-ssp-full-oscal/
├── plan.md              # This file
├── spec.md              # Feature specification (completed)
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output — MCP tool contracts
└── tasks.md             # Phase 2 output (/speckit.tasks command)
```

### Source Code (repository root)

```text
src/
├── Ato.Copilot.Core/
│   ├── Data/Context/
│   │   └── AtoCopilotContext.cs              # Add SspSections, ContingencyPlanReferences DbSets
│   └── Models/Compliance/
│       └── RmfModels.cs                      # Add SspSection, ContingencyPlanReference, SspSectionStatus,
│                                             #   OperationalStatus enums; extend RegisteredSystem
├── Ato.Copilot.Agents/
│   ├── Compliance/Services/
│   │   ├── ISspService.cs                    # Add 3 new interface methods
│   │   ├── SspService.cs                     # Enhance: 3 new methods, 8 new section generators, renumbering
│   │   ├── IOscalSspExportService.cs         # NEW — interface
│   │   ├── OscalSspExportService.cs          # NEW — OSCAL 1.1.2 SSP JSON generation
│   │   ├── IOscalValidationService.cs        # NEW — interface
│   │   ├── OscalValidationService.cs         # NEW — structural validation
│   │   └── EmassExportService.cs             # Modify: delegate BuildOscalSsp to OscalSspExportService
│   ├── Compliance/Tools/
│   │   └── SspAuthoringTools.cs              # Add 5 new tool classes; update GenerateSspTool
│   └── Extensions/
│       └── ServiceCollectionExtensions.cs    # Register new services + tools

tests/
├── Ato.Copilot.Tests.Unit/
│   ├── Services/
│   │   ├── SspServiceSectionTests.cs         # NEW — unit tests for 8 new section generators
│   │   ├── OscalSspExportServiceTests.cs     # NEW — unit tests for OSCAL generation
│   │   └── OscalValidationServiceTests.cs    # NEW — unit tests for validation
│   └── Tools/
│       └── SspAuthoringToolTests.cs          # Extend with tests for 5 new tools
└── Ato.Copilot.Tests.Integration/
    └── Tools/
        └── SspToolsIntegrationTests.cs       # NEW — integration tests for SSP section + OSCAL tools
```

**Structure Decision**: Follows existing project layout — new services in `Compliance/Services/`, new tools in `Compliance/Tools/`, entities in `Core/Models/Compliance/`. No new projects needed.

**Migration Strategy**: EF Core Code First with auto-migration. New tables (`SspSections`, `ContingencyPlanReferences`) and new columns on `RegisteredSystems` (`DitprId`, `EmassId`, `OperationalStatus`, `OperationalDate`, `DisposalDate`) are additive-only changes — no existing data modified. Apply via `dotnet ef migrations add Feature022_SspOscal` and `dotnet ef database update`. InMemory provider used for all tests (no migration needed in test context).

## Complexity Tracking

> No constitution violations — this section is intentionally empty.
