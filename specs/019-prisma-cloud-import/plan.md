# Implementation Plan: 019 — Prisma Cloud Scan Import

**Branch**: `019-prisma-cloud-import` | **Date**: 2026-03-05 | **Spec**: [spec.md](spec.md)  
**Input**: Feature specification from `/specs/019-prisma-cloud-import/spec.md`

## Summary

Enable ATO Copilot to ingest Prisma Cloud CSPM compliance scan results (CSV exports and API JSON responses), parse alerts, map Prisma policies to NIST 800-53 controls via embedded `complianceMetadata`, create `ComplianceFinding` + `ControlEffectiveness` + `ComplianceEvidence` records, auto-resolve Azure subscriptions to registered systems, and provide trend analysis across scan cycles. Extends the existing `IScanImportService` with 2 new import methods, adds 2 parsers (CSV, JSON), 4 MCP tools, and Phase 5 documentation updates across 8 persona/reference guides.

## Technical Context

**Language/Version**: C# 13 / .NET 9.0  
**Primary Dependencies**: `System.Text.Json` (JSON parsing), `string.Split` + quote-aware CSV logic (CSV parsing), existing `IScanImportService`, `ISystemSubscriptionResolver`, `IAssessmentArtifactService`, `IBaselineService`  
**Storage**: EF Core 9.0 InMemory (dev/test). Extends existing `ScanImportRecords` and `ScanImportFindings` DbSets — NO new DbSets, NO new migrations. `EnsureCreatedAsync()`.  
**Testing**: xUnit 2.9.3 + FluentAssertions 7.0.0 + Moq 4.20.72. Target: 170+ new unit tests for parsers, severity mapper, import service, and tools.  
**Target Platform**: Docker containers (Linux), Azure Government.  
**Project Type**: MCP server (library + service extension — no new projects)  
**Performance Goals**: CSV import (500 alerts) < 15s, API JSON import (500 alerts) < 10s.  
**Constraints**: File size limit 25MB after base64 decode. No streaming file upload — base64 in JSON-RPC. Memory budget < 512MB steady state.  
**Scale/Scope**: Typical imports: 1–5 Prisma exports per system per ConMon cycle, 50–1000 alerts per export. Multi-subscription CSV auto-split.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| **I. Documentation as Source of Truth** | PASS | Spec (13 parts, 707 lines), data-model, contracts, quickstart, research all produced. Phase 5 adds 8 doc updates to persona guides and reference pages. |
| **II. BaseAgent/BaseTool Architecture** | PASS | All 4 tools extend `BaseTool`, registered via `RegisterTool()`. No new agents. Parsers are plain service classes injected via DI. |
| **III. Testing Standards** | PASS | TDD workflow: red/green for parsers, mapper, service, tools. Target 170+ tests covering positive, negative, boundary, edge cases. Existing 3,520 tests must continue passing. |
| **IV. Azure Government & Compliance First** | PASS | Prisma imports are file-based — no external API calls from ATO Copilot. Subscription auto-resolution uses existing `ISystemSubscriptionResolver` which supports GovCloud. Findings integrate with system-scoped assessments. |
| **V. Observability & Structured Logging** | PASS | Import operations logged with Serilog: system_id, import_type, alert_count, nist_controls_affected, subscription_resolved, duration_ms. Follows Feature 017/018 `Stopwatch` pattern. |
| **VI. Code Quality & Maintainability** | PASS | Parsers are stateless and testable. Import service uses DI. All enums, DTOs, and mappings are explicit — no magic values. Single Responsibility preserved (parser ≠ resolver ≠ finder ≠ upserter). |
| **VII. User Experience Consistency** | PASS | Standard MCP envelope (`{ status, data, metadata }`). Import results follow same `ImportResult` format as CKL/XCCDF. Actionable error messages with `compliance_register_system` guidance for unresolved subscriptions. |
| **VIII. Performance Requirements** | PASS | CSV <15s, JSON <10s for 500 alerts. CancellationToken on all async methods. No unbounded queries — subscription grouping is bounded by unique Account IDs in file. |

**Gate result**: PASS — proceed to Phase 0.

## Project Structure

### Documentation (this feature)

```text
specs/019-prisma-cloud-import/
├── plan.md              # This file
├── research.md          # Phase 0 output — Prisma CSV/JSON format research
├── data-model.md        # Phase 1 output — entity extensions and DTOs
├── quickstart.md        # Phase 1 output — quick validation guide
├── contracts/
│   └── mcp-tools.md     # Phase 1 output — MCP tool contracts
├── spec.md              # Feature specification (complete — 707 lines, 13 parts)
└── tasks.md             # Task breakdown (complete — 62 tasks, T001–T062)
```

### Source Code (repository root)

```text
src/
├── Ato.Copilot.Core/
│   ├── Models/Compliance/
│   │   └── ScanImportModels.cs          # MODIFIED — add PrismaCsv/PrismaApi to ScanImportType,
│   │                                    #   add Prisma-specific fields to ScanImportFinding,
│   │                                    #   add ParsedPrismaAlert and ParsedPrismaFile DTOs
│   └── Interfaces/Compliance/
│       ├── IScanImportService.cs        # MODIFIED — add ImportPrismaCsvAsync, ImportPrismaApiAsync,
│       │                                #   ListPrismaPoliciesAsync, GetPrismaTrendAsync
│       └── IPrismaParser.cs            # NEW — parser interface with Parse(byte[]) → ParsedPrismaFile
│
├── Ato.Copilot.Agents/
│   └── Compliance/
│       ├── Tools/
│       │   └── PrismaImportTools.cs     # NEW — 4 MCP tools (ImportPrismaTool, ImportPrismaApiTool,
│       │                                #   ListPrismaPoliciesTool, PrismaTrendTool)
│       └── Services/
│           └── ScanImport/
│               ├── ScanImportService.cs # MODIFIED — add ImportPrismaCsvAsync, ImportPrismaApiAsync
│               ├── PrismaCsvParser.cs   # NEW — CSV parsing with quote-aware splitting, Alert ID grouping
│               ├── PrismaApiJsonParser.cs # NEW — JSON deserialization with complianceMetadata extraction
│               └── PrismaSeverityMapper.cs # NEW — static severity mapping (Prisma → CAT/FindingSeverity)
│
├── Ato.Copilot.Mcp/
│   └── Tools/
│       └── ComplianceMcpTools.cs        # MODIFIED — register 4 new Prisma tools
│
└── Ato.Copilot.State/                   # No changes

tests/
└── Ato.Copilot.Tests.Unit/
    ├── Parsers/
    │   ├── PrismaCsvParserTests.cs      # NEW — CSV parsing tests
    │   ├── PrismaApiJsonParserTests.cs  # NEW — API JSON parsing tests
    │   └── PrismaSeverityMapperTests.cs # NEW — severity mapping tests
    ├── Services/
    │   └── PrismaImportServiceTests.cs  # NEW — Prisma import orchestration tests
    ├── Tools/
    │   └── PrismaImportToolTests.cs     # NEW — MCP tool tests
    └── TestData/
        ├── sample-prisma-export.csv     # NEW — test CSV file
        └── sample-prisma-api.json       # NEW — test API JSON file

docs/                                    # Phase 5 — Documentation updates
├── guides/
│   ├── issm-guide.md                    # MODIFIED — ISSO import workflow + ISSM Cloud Posture Oversight
│   ├── sca-guide.md                     # MODIFIED — Prisma assessment data section
│   └── engineer-guide.md               # MODIFIED — Prisma remediation workflow
├── architecture/
│   └── agent-tool-catalog.md            # MODIFIED — 4 new tool entries
├── reference/
│   └── tool-inventory.md               # MODIFIED — 4 new tool entries
└── rmf-phases/
    ├── assess.md                        # MODIFIED — Prisma as assessment input
    └── monitor.md                       # MODIFIED — Prisma as ConMon data source
```

**Structure Decision**: Single project extension — all Prisma code lives in existing `Ato.Copilot.Agents` and `Ato.Copilot.Core` projects. No new projects needed. Follows the same pattern established in Feature 017 (SCAP/STIG Import).

## Implementation Phases

### Phase 1: Prisma CSV Import (Core) — T001–T024

**Goal**: Parse Prisma Cloud CSV exports, extract NIST control mappings, auto-resolve subscriptions, create ComplianceFinding + ControlEffectiveness + ComplianceEvidence records, expose via MCP tool.

**New files**:
- `src/Ato.Copilot.Agents/Compliance/Services/ScanImport/PrismaCsvParser.cs` — CSV parser
- `src/Ato.Copilot.Agents/Compliance/Tools/PrismaImportTools.cs` — MCP tool
- `tests/Ato.Copilot.Tests.Unit/Parsers/PrismaCsvParserTests.cs` — parser tests
- `tests/Ato.Copilot.Tests.Unit/Services/PrismaImportServiceTests.cs` — service tests
- `tests/Ato.Copilot.Tests.Unit/Tools/PrismaImportToolTests.cs` — tool tests
- `tests/Ato.Copilot.Tests.Unit/TestData/sample-prisma-export.csv` — test data

**Modified files**:
- `ScanImportModels.cs` — add `PrismaCsv`/`PrismaApi` to `ScanImportType`, add Prisma fields to `ScanImportFinding`, add `ParsedPrismaAlert`/`ParsedPrismaFile` DTOs
- `IScanImportService.cs` — add `ImportPrismaCsvAsync` signature
- `ScanImportService.cs` — implement Prisma CSV import pipeline
- `ComplianceMcpTools.cs` — register `compliance_import_prisma`
- `ServiceCollectionExtensions.cs` — register `PrismaCsvParser`

**Checkpoint**: Prisma CSV files can be imported, subscription auto-resolved, findings created, effectiveness records upserted, evidence attached.

### Phase 2: Prisma API JSON Import — T025–T035

**Goal**: Parse Prisma Cloud API JSON responses with enhanced remediation context, alert history, and policy metadata.

**New files**:
- `src/Ato.Copilot.Agents/Compliance/Services/ScanImport/PrismaApiJsonParser.cs` — JSON parser
- `tests/Ato.Copilot.Tests.Unit/Parsers/PrismaApiJsonParserTests.cs` — parser tests
- `tests/Ato.Copilot.Tests.Unit/TestData/sample-prisma-api.json` — test data

**Modified files**:
- `IScanImportService.cs` — add `ImportPrismaApiAsync` signature
- `ScanImportService.cs` — implement Prisma API import pipeline (reuses Phase 1 downstream)
- `PrismaImportTools.cs` — add `ImportPrismaApiTool`
- `ComplianceMcpTools.cs` — register `compliance_import_prisma_api`
- `ServiceCollectionExtensions.cs` — register `PrismaApiJsonParser`

**Checkpoint**: API JSON files can be imported with remediation guidance, CLI scripts, and alert history preserved as evidence.

### Phase 3: Policy Catalog & Trend Analysis — T036–T047

**Goal**: Query Prisma policy registry and analyze findings across scan cycles.

**Modified files**:
- `IScanImportService.cs` — add `ListPrismaPoliciesAsync`, `GetPrismaTrendAsync` signatures
- `ScanImportService.cs` — implement policy listing and trend analysis
- `ScanImportModels.cs` — add `PrismaTrendResult` DTO
- `PrismaImportTools.cs` — add `ListPrismaPoliciesTool`, `PrismaTrendTool`
- `ComplianceMcpTools.cs` — register `compliance_list_prisma_policies`, `compliance_prisma_trend`

**Checkpoint**: Prisma policies queryable with NIST mappings and status counts. Trend analysis shows new/resolved/persistent findings between imports.

### Phase 4: Integration & Observability — T048–T053

**Goal**: Verify end-to-end integration with downstream artifact tools, add structured logging, performance testing.

**Modified files**:
- `ScanImportModels.cs` — update enum count assertions
- Integration test files
- Structured logging additions to import methods

**Checkpoint**: Prisma findings flow correctly into SAR, POA&M, SSP, SAP generation. Import operations fully instrumented with Serilog. Performance targets verified.

### Phase 5: Documentation & Validation — T054–T062

**Goal**: Update 8 documentation pages with Prisma Cloud scan import workflows. Final build validation and end-to-end quickstart scenario verification.

**Modified files**:
- `docs/guides/issm-guide.md` — ISSO import workflow + ISSM Cloud Posture Oversight
- `docs/guides/sca-guide.md` — Prisma assessment data section
- `docs/guides/engineer-guide.md` — Prisma remediation workflow
- `docs/architecture/agent-tool-catalog.md` — 4 new tool entries
- `docs/reference/tool-inventory.md` — 4 new tool entries
- `docs/rmf-phases/assess.md` — Prisma as assessment input
- `docs/rmf-phases/monitor.md` — Prisma as ConMon data source

**Checkpoint**: All persona guides document Prisma workflows. Tool catalogs include all 4 new tools. RMF phase pages reference Prisma import at Steps 4 and 6.

## Dependencies

### Internal Dependencies

| Dependency | Required For | Risk |
|------------|--------------|------|
| `IScanImportService` | Extend with Prisma methods | None — stable interface, same extension pattern as Feature 017 |
| `ISystemSubscriptionResolver` | Auto-resolve Azure subscriptions | None — existing Feature 015 service with 5-min cache |
| `IAssessmentArtifactService` | ControlEffectiveness upsert | None — stable interface |
| `IBaselineService` | Validate NIST controls in baseline | None — stable interface |
| `IRmfLifecycleService` | System validation | None — stable interface |
| `ScanImportRecord` / `ScanImportFinding` entities | Track imports | None — adding fields, not changing existing schema |
| Feature 017 (SCAP/STIG Import) | Base import framework | None — Feature 017 fully implemented (shipped as v1.17.0) |

### External Dependencies

None — all parsing uses built-in .NET libraries (`System.Text.Json`, `string.Split`). No third-party CSV libraries required. No Prisma Cloud API calls made from ATO Copilot.

## Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| Prisma CSV column order varies across versions | Medium | Medium | Parse by column name (header row), not position. Test with multiple export formats. |
| Prisma policies with no NIST 800-53 mapping | High | Low | Create unmapped finding for audit trail, add warning. Do not fail import. |
| Multi-subscription CSVs with >10 unique subscriptions | Low | Medium | Processing is per-subscription (bounded). Resolver cache prevents N+1 DB queries. |
| Large enterprise exports exceeding 25MB | Low | Low | 25MB limit with clear error message. Recommend subscription-scoped export. |
| CSV fields containing newlines in quoted values | Medium | Medium | Quote-aware parser handles RFC 4180 compliant CSV. Test with embedded newlines. |
| Prisma API JSON schema changes | Low | Medium | Defensive deserialization with nullable fields. Unknown properties ignored via `System.Text.Json` defaults. |
| `ISystemSubscriptionResolver` cache stale for new systems | Low | Low | Resolver has 5-minute cache TTL. User can re-import after registration. |
| Non-Azure alerts in mixed-cloud Prisma exports | High | Low | Skip with warning when auto-resolving. Accept all when `system_id` explicit. Actionable message guides user. |

## Complexity Tracking

> No constitution violations. No complexity justifications needed.
