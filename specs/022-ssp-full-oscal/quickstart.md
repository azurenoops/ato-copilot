# Quickstart: Feature 022 — SSP 800-18 Full Sections + OSCAL Output

**Date**: 2026-03-10

---

## Prerequisites

- .NET 9.0 SDK
- Feature 021 (PIA + System Interconnections) merged
- `dotnet build Ato.Copilot.sln` passes with zero warnings

## Build & Test

```bash
# Build
dotnet build Ato.Copilot.sln

# Run all tests
dotnet test Ato.Copilot.sln

# Run only Feature 022 unit tests
dotnet test tests/Ato.Copilot.Tests.Unit/ --filter "FullyQualifiedName~Ssp|FullyQualifiedName~Oscal"

# Run only Feature 022 integration tests
dotnet test tests/Ato.Copilot.Tests.Integration/ --filter "FullyQualifiedName~SspTools"
```

## Key Files (New)

| File | Purpose |
|------|---------|
| `src/Ato.Copilot.Agents/Compliance/Services/OscalSspExportService.cs` | OSCAL 1.1.2 SSP JSON generation |
| `src/Ato.Copilot.Agents/Compliance/Services/IOscalSspExportService.cs` | Interface |
| `src/Ato.Copilot.Agents/Compliance/Services/OscalValidationService.cs` | Structural validation |
| `src/Ato.Copilot.Agents/Compliance/Services/IOscalValidationService.cs` | Interface |
| `tests/Ato.Copilot.Tests.Unit/Services/OscalSspExportServiceTests.cs` | OSCAL export unit tests |
| `tests/Ato.Copilot.Tests.Unit/Services/OscalValidationServiceTests.cs` | Validation unit tests |
| `tests/Ato.Copilot.Tests.Unit/Services/SspServiceSectionTests.cs` | Section generation unit tests |
| `tests/Ato.Copilot.Tests.Integration/Tools/SspToolsIntegrationTests.cs` | End-to-end tool tests |

## Key Files (Modified)

| File | Change |
|------|--------|
| `src/Ato.Copilot.Core/Models/Compliance/RmfModels.cs` | Add `SspSection`, `ContingencyPlanReference`, `SspSectionStatus`, `OperationalStatus`; extend `RegisteredSystem` |
| `src/Ato.Copilot.Core/Data/Context/AtoCopilotContext.cs` | Add `SspSections`, `ContingencyPlanReferences` DbSets + configuration |
| `src/Ato.Copilot.Core/Interfaces/Compliance/ISspService.cs` | Add 3 new methods |
| `src/Ato.Copilot.Agents/Compliance/Services/SspService.cs` | Add 3 methods + 8 section generators + renumbering |
| `src/Ato.Copilot.Agents/Compliance/Services/EmassExportService.cs` | Delegate `BuildOscalSsp()` to `OscalSspExportService` |
| `src/Ato.Copilot.Agents/Compliance/Tools/SspAuthoringTools.cs` | Add 5 new tool classes + update `GenerateSspTool` |
| `src/Ato.Copilot.Agents/Extensions/ServiceCollectionExtensions.cs` | Register new services + tools |

## Usage Examples

### Author SSP Section (MCP Tool Call)

```json
{
  "tool": "compliance_write_ssp_section",
  "arguments": {
    "system_id": "ACME Portal",
    "section_number": 6,
    "content": "## System Environment\n\nACME Portal is hosted in Azure Government...",
    "authored_by": "john.isso@agency.gov"
  }
}
```

### Check SSP Completeness

```json
{
  "tool": "compliance_ssp_completeness",
  "arguments": {
    "system_id": "ACME Portal"
  }
}
```

### Export OSCAL SSP

```json
{
  "tool": "compliance_export_oscal_ssp",
  "arguments": {
    "system_id": "ACME Portal",
    "include_back_matter": true,
    "pretty_print": true
  }
}
```

### Validate OSCAL SSP

```json
{
  "tool": "compliance_validate_oscal_ssp",
  "arguments": {
    "system_id": "ACME Portal"
  }
}
```

## NIST 800-18 Section Keys for compliance_generate_ssp

```
system_identification, categorization, personnel, system_type, description,
environment, interconnections, laws_regulations, minimum_controls,
control_implementations, authorization_boundary, personnel_security,
contingency_plan
```

Old keys still work: `system_information` → `system_identification`, `baseline` → `minimum_controls`, `controls` → `control_implementations`.
