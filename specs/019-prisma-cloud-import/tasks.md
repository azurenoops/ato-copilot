# Tasks: 019 — Prisma Cloud Scan Import

**Input**: Design documents from `/specs/019-prisma-cloud-import/`
**Prerequisites**: plan.md (required), spec.md (required), research.md, data-model.md, contracts/mcp-tools.md, quickstart.md
**Tests**: Included per TDD workflow (plan.md Constitution Check III). Target: 170+ new unit tests.
**Total Tasks**: 62 (T001–T062)

**Organization**: Tasks organized by user story to enable independent implementation and testing.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[US1]**: Prisma CSV Import — ISSO imports CSV, findings + effectiveness created
- **[US2]**: Prisma API JSON Import — ISSO imports JSON with remediation/history
- **[US3]**: Policy Catalog & Trend — SCA/ISSM views policies and tracks remediation
- Commit after each task or logical group

## Path Conventions

- Core models/interfaces: `src/Ato.Copilot.Core/`
- Service implementations: `src/Ato.Copilot.Agents/Compliance/`
- MCP registration: `src/Ato.Copilot.Mcp/`
- Unit tests: `tests/Ato.Copilot.Tests.Unit/`
- Documentation: `docs/`

---

## Phase 1: Setup

**Purpose**: Test data fixtures, enum extensions, DTOs — shared infrastructure needed before any user story

- [x] T001 [P] Create sample Prisma CSV test fixture with multi-row alerts (same Alert ID, multiple NIST controls), multiple subscriptions, open/resolved/dismissed/snoozed statuses, mixed cloud types (azure/aws), and quoted fields with commas in tests/Ato.Copilot.Tests.Unit/TestData/sample-prisma-export.csv
- [x] T002 [P] Create sample Prisma API JSON test fixture with complianceMetadata arrays (NIST + CIS), policy.remediation.cliScriptTemplate, policy.labels, policy.remediable flag, resource metadata, and history[] array in tests/Ato.Copilot.Tests.Unit/TestData/sample-prisma-api.json
- [x] T003 Add PrismaCsv and PrismaApi values to ScanImportType enum in src/Ato.Copilot.Core/Models/Compliance/ScanImportModels.cs AND add Cloud value to ScanSourceType enum in src/Ato.Copilot.Core/Models/Compliance/ComplianceModels.cs (required for Prisma findings with ScanSource=Cloud)
- [x] T004 [P] Add 7 nullable Prisma fields to ScanImportFinding entity (PrismaAlertId, PrismaPolicyId, PrismaPolicyName, CloudResourceId, CloudResourceType, CloudRegion, CloudAccountId — all string? with MaxLength constraints per data-model.md) in src/Ato.Copilot.Core/Models/Compliance/ScanImportModels.cs
- [x] T005 [P] Create ParsedPrismaAlert, ParsedPrismaFile, and PrismaAlertHistoryEntry record DTOs with all fields per data-model.md in src/Ato.Copilot.Core/Models/Compliance/ScanImportModels.cs

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Interface signatures, DTOs for query results, severity mapper, parser interface — MUST complete before user story implementation

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [x] T006 Add ImportPrismaCsvAsync, ImportPrismaApiAsync, ListPrismaPoliciesAsync, and GetPrismaTrendAsync method signatures to IScanImportService interface and add stub implementations (throw NotImplementedException) in ScanImportService so the project compiles — src/Ato.Copilot.Core/Interfaces/Compliance/IScanImportService.cs and src/Ato.Copilot.Agents/Compliance/Services/ScanImport/ScanImportService.cs
- [x] T007 [P] Create PrismaTrendResult and PrismaTrendImport DTOs (SystemId, Imports list, NewFindings, ResolvedFindings, PersistentFindings, RemediationRate, ResourceTypeBreakdown, NistControlBreakdown — per data-model.md) in src/Ato.Copilot.Core/Models/Compliance/ScanImportModels.cs
- [x] T008 [P] TDD: Write PrismaSeverityMapper unit tests (red) in tests/Ato.Copilot.Tests.Unit/Parsers/PrismaSeverityMapperTests.cs — map all 5 Prisma severities to CatSeverity and FindingSeverity, verify critical/high both → CatI, informational → no CAT equivalent, unknown/null → default Medium, case-insensitive — then implement PrismaSeverityMapper static helper class with MapToCatSeverity and MapToFindingSeverity methods (critical/high→CatI, medium→CatII, low→CatIII, informational→Informational, unknown→Medium default) in src/Ato.Copilot.Agents/Compliance/Services/ScanImport/PrismaSeverityMapper.cs
- [x] T009 [P] Create IPrismaParser interface with Parse(byte[] content) returning ParsedPrismaFile in src/Ato.Copilot.Core/Interfaces/Compliance/IPrismaParser.cs

**Checkpoint**: Foundation ready — all interfaces defined, DTOs created, severity mapper implemented. User story implementation can begin.

---

## Phase 3: User Story 1 — Prisma CSV Import (Priority: P1) 🎯 MVP

**Goal**: ISSO can import a Prisma Cloud compliance CSV export, auto-resolve Azure subscriptions to registered systems, create ComplianceFinding + ControlEffectiveness + ComplianceEvidence records, and view import results via `compliance_import_prisma` MCP tool.

**Independent Test**: Import a multi-subscription Prisma CSV via compliance_import_prisma → verify findings created (one per alert), NIST controls mapped from Compliance Standard/Requirement columns, effectiveness records upserted per control, evidence attached with SHA-256 hash, unresolved subscriptions reported with compliance_register_system guidance, non-Azure alerts skipped with warning in auto-resolve mode.

### Tests for US1

> Write tests FIRST, ensure they FAIL before implementation

- [X] T010 [P] [US1] Write PrismaCsvParser unit tests (red) in tests/Ato.Copilot.Tests.Unit/Parsers/PrismaCsvParserTests.cs — parse valid CSV with all 18 columns, handle quoted fields with embedded commas, group multi-row alerts by Alert ID into single ParsedPrismaAlert with merged NistControlIds, extract NIST controls from Compliance Standard/Requirement where standard contains "NIST 800-53", skip non-NIST rows (CIS, SOC 2), handle empty compliance columns (unmapped policy), handle UTF-8 BOM, reject CSV with missing required header columns (Alert ID, Status, Policy Name, Severity), handle empty rows, handle trailing commas
- [X] T011 [P] [US1] Verify PrismaSeverityMapper tests pass (green) — core tests written in T008 TDD workflow; add any additional US1-specific severity edge cases (mixed-case strings from real Prisma exports, empty string severity, whitespace-padded values) in tests/Ato.Copilot.Tests.Unit/Parsers/PrismaSeverityMapperTests.cs
- [X] T012 [P] [US1] Write ImportPrismaCsvAsync service unit tests (red) in tests/Ato.Copilot.Tests.Unit/Services/PrismaImportServiceTests.cs — successful import with finding/evidence creation, subscription auto-resolution via mock ISystemSubscriptionResolver, multi-subscription CSV auto-split produces multiple ScanImportRecords, explicit system_id overrides auto-resolution and accepts all cloud types, non-Azure alerts skipped with warning in auto-resolve mode, unresolved subscription returns actionable error with compliance_register_system guidance, dry-run returns accurate counts without persisting, conflict resolution Skip/Overwrite/Merge on PrismaAlertId+RegisteredSystemId matching key, duplicate file hash detection warning, unmapped policies (no NIST mapping) create ComplianceFinding with warning, controls outside baseline get ComplianceFinding only (no ControlEffectiveness), one ComplianceFinding per alert with multiple ControlEffectiveness records for multi-control policies, resolved alerts set FindingStatus.Remediated (don't independently assert Satisfied), aggregate effectiveness evaluation (ANY open→OtherThanSatisfied across all sources), snoozed alerts mapped to Open with note, file size >25MB rejected, evidence created with SHA-256 hash and EvidenceType="CloudScanResult"
- [X] T013 [P] [US1] Write ImportPrismaTool MCP tool unit tests (red) in tests/Ato.Copilot.Tests.Unit/Tools/PrismaImportToolTests.cs — valid import returns MCP envelope with imports array and counts, missing file_content returns error, invalid base64 returns error, file exceeds 25MB returns error with guidance, dry_run flag passed through correctly, conflict_resolution parameter mapped to enum, system_id optional when subscription auto-resolves, assessment_id parameter forwarded

### Implementation for US1

- [X] T014 [US1] Implement PrismaCsvParser : IPrismaParser with quote-aware CSV splitting (RFC 4180 state machine), header-based column lookup by name (not position), Alert ID grouping for multi-row alerts, NIST control extraction from Compliance Standard/Requirement columns, UTF-8 BOM stripping in src/Ato.Copilot.Agents/Compliance/Services/ScanImport/PrismaCsvParser.cs
- [X] T015 [US1] Register PrismaCsvParser in DI container in src/Ato.Copilot.Agents/Extensions/ServiceCollectionExtensions.cs
- [X] T016 [US1] Verify PrismaCsvParser and PrismaSeverityMapper tests pass (green)
- [X] T017 [US1] Implement ImportPrismaCsvAsync in src/Ato.Copilot.Agents/Compliance/Services/ScanImport/ScanImportService.cs — replace NotImplementedException stub: parse CSV via PrismaCsvParser, validate file size ≤25MB, resolve subscriptions via ISystemSubscriptionResolver (multi-subscription auto-split), handle non-Azure alerts (skip in auto-resolve, accept with explicit system_id), validate system exists via IRmfLifecycleService
- [X] T018 [US1] Implement ImportPrismaCsvAsync finding creation — one ComplianceFinding per alert with StigFinding=false, ScanSource=Cloud, Source="Prisma Cloud", Title from PolicyName, status mapping (open→Open, resolved→Remediated, dismissed→Accepted, snoozed→Open with note), severity mapping via PrismaSeverityMapper, ResourceId from ARM resource ID in src/Ato.Copilot.Agents/Compliance/Services/ScanImport/ScanImportService.cs
- [X] T019 [US1] Implement ImportPrismaCsvAsync ControlEffectiveness aggregate upsert — for each NIST control in system's ControlBaseline, query ALL ComplianceFinding records across all sources (Prisma + STIG + manual), ANY open→OtherThanSatisfied, ALL remediated/accepted→Satisfied, set AssessmentMethod="Test", controls outside baseline get ComplianceFinding only in src/Ato.Copilot.Agents/Compliance/Services/ScanImport/ScanImportService.cs
- [X] T020 [US1] Implement ImportPrismaCsvAsync evidence creation (SHA-256 hash, EvidenceType="CloudScanResult", CollectionMethod="Automated"), conflict resolution (Skip/Overwrite/Merge on PrismaAlertId+RegisteredSystemId), duplicate file hash detection, dry-run mode, ScanImportRecord creation with ImportType=PrismaCsv in src/Ato.Copilot.Agents/Compliance/Services/ScanImport/ScanImportService.cs
- [X] T021 [US1] Verify ImportPrismaCsvAsync service tests pass (green)
- [X] T022 [US1] Implement ImportPrismaTool extending BaseTool — accept file_content (base64), file_name, system_id (optional), conflict_resolution (default "skip"), dry_run (default false), assessment_id (optional); decode base64, validate file size, call ImportPrismaCsvAsync, return MCP envelope with imports array, unresolvedSubscriptions, skippedNonAzure, duration_ms per contracts/mcp-tools.md in src/Ato.Copilot.Agents/Compliance/Tools/PrismaImportTools.cs
- [X] T023 [US1] Register compliance_import_prisma tool with RBAC roles SecurityLead, Analyst, Administrator in src/Ato.Copilot.Mcp/Tools/ComplianceMcpTools.cs
- [X] T024 [US1] Verify ImportPrismaTool tests pass (green)

**Checkpoint**: Prisma CSV import fully functional. ISSO can import CSV → findings created → effectiveness upserted → evidence attached → import tracked via compliance_list_imports.

---

## Phase 4: User Story 2 — Prisma API JSON Import (Priority: P2)

**Goal**: ISSO can import Prisma Cloud API JSON responses (RQL alert data) with enhanced remediation guidance, CLI scripts, alert history, and policy metadata — reusing the same downstream finding/effectiveness pipeline as CSV.

**Independent Test**: Import a Prisma API JSON via compliance_import_prisma_api → verify remediation guidance stored on ComplianceFinding.RemediationGuidance, CLI scripts extracted as evidence, alert history captured on ScanImportFinding metadata, policy labels preserved, same downstream pipeline produces identical effectiveness behavior as CSV.

### Tests for US2

> Write tests FIRST, ensure they FAIL before implementation

- [X] T025 [P] [US2] Write PrismaApiJsonParser unit tests (red) in tests/Ato.Copilot.Tests.Unit/Parsers/PrismaApiJsonParserTests.cs — parse single alert JSON object, parse array of alert objects, extract complianceMetadata NIST controls (filter standardName containing "NIST 800-53"), extract policy.description as Description, extract policy.recommendation as Recommendation, extract policy.remediation.cliScriptTemplate as RemediationScript, extract resource metadata (id, name, resourceType, region, cloudType, accountId), capture history[] as PrismaAlertHistoryEntry list, extract policy.labels and policy.remediable, handle missing optional fields (null remediation, empty labels, no history), handle malformed JSON with descriptive error
- [X] T026 [P] [US2] Write ImportPrismaApiAsync service unit tests (red) in tests/Ato.Copilot.Tests.Unit/Services/PrismaImportServiceTests.cs — successful import with ComplianceFinding.RemediationGuidance from policy.recommendation, ComplianceFinding.RemediationScript from CLI template, ComplianceFinding.AutoRemediable from policy.remediable flag, alert history attached to ScanImportFinding, policy labels captured as structured metadata, reuses same subscription resolution and effectiveness aggregate pipeline as CSV import, ImportType=PrismaApi on ScanImportRecord
- [X] T027 [P] [US2] Write ImportPrismaApiTool unit tests (red) in tests/Ato.Copilot.Tests.Unit/Tools/PrismaImportToolTests.cs — valid import returns MCP envelope with enhanced fields (remediableCount, cliScriptsExtracted, policyLabelsFound, alertsWithHistory), invalid JSON returns descriptive error, missing required alert fields (id, status, policy) returns error per contracts/mcp-tools.md

### Implementation for US2

- [X] T028 [US2] Implement PrismaApiJsonParser : IPrismaParser with System.Text.Json deserialization, complianceMetadata NIST 800-53 filtering, remediation extraction (recommendation + cliScriptTemplate), history[] capture (convert Unix epoch ms to DateTime UTC), policy.labels and policy.remediable extraction in src/Ato.Copilot.Agents/Compliance/Services/ScanImport/PrismaApiJsonParser.cs
- [X] T029 [US2] Register PrismaApiJsonParser in DI container in src/Ato.Copilot.Agents/Extensions/ServiceCollectionExtensions.cs
- [X] T030 [US2] Verify PrismaApiJsonParser tests pass (green)
- [X] T031 [US2] Implement ImportPrismaApiAsync in src/Ato.Copilot.Agents/Compliance/Services/ScanImport/ScanImportService.cs — replace NotImplementedException stub: parse JSON via PrismaApiJsonParser, reuse CSV downstream pipeline (subscription resolution, finding creation with remediation fields populated, effectiveness upsert, evidence creation), store CLI script content as additional evidence, mark ImportType=PrismaApi on ScanImportRecord
- [X] T032 [US2] Verify ImportPrismaApiAsync service tests pass (green)
- [X] T033 [US2] Implement ImportPrismaApiTool extending BaseTool in src/Ato.Copilot.Agents/Compliance/Tools/PrismaImportTools.cs — same parameters as CSV tool (file_content, file_name, system_id, conflict_resolution, dry_run, assessment_id), enhanced response with remediableCount, cliScriptsExtracted, policyLabelsFound, alertsWithHistory per contracts/mcp-tools.md
- [X] T034 [US2] Register compliance_import_prisma_api tool with RBAC roles SecurityLead, Analyst, Administrator in src/Ato.Copilot.Mcp/Tools/ComplianceMcpTools.cs
- [X] T035 [US2] Verify ImportPrismaApiTool tests pass (green)

**Checkpoint**: Both CSV and API JSON import paths fully functional and independently testable.

---

## Phase 5: User Story 3 — Policy Catalog & Trend Analysis (Priority: P3)

**Goal**: SCA/ISSM can browse unique Prisma policies with NIST control mappings and open/resolved counts, and track remediation progress across scan cycles with new/resolved/persistent finding counts and remediation rate.

**Independent Test**: After 2+ Prisma imports, call compliance_list_prisma_policies → verify unique policies with NIST mappings and status counts. Call compliance_prisma_trend → verify new/resolved/persistent counts, remediation rate, and optional group_by breakdowns.

### Tests for US3

> Write tests FIRST, ensure they FAIL before implementation

- [X] T036 [P] [US3] Write ListPrismaPoliciesAsync unit tests (red) in tests/Ato.Copilot.Tests.Unit/Services/PrismaImportServiceTests.cs — returns unique policies grouped by PrismaPolicyName, includes NIST control mappings per policy, includes open/resolved/dismissed counts per policy, includes affected resource types (CloudResourceType), filters by system_id, returns empty policies array (not error) when no Prisma imports exist for system
- [X] T037 [P] [US3] Write GetPrismaTrendAsync unit tests (red) in tests/Ato.Copilot.Tests.Unit/Services/PrismaImportServiceTests.cs — compare 2 imports showing new/resolved/persistent counts using PrismaAlertId matching, calculate remediationRate as resolved/(resolved+persistent), group_by "resource_type" returns ResourceTypeBreakdown dictionary, group_by "nist_control" returns NistControlBreakdown dictionary, specific import_ids comparison, default to last 2 Prisma imports when import_ids omitted, single import returns snapshot (newFindings=totalAlerts, resolvedFindings=0), no Prisma imports returns error
- [X] T038 [P] [US3] Write ListPrismaPoliciesTool and PrismaTrendTool unit tests (red) in tests/Ato.Copilot.Tests.Unit/Tools/PrismaImportToolTests.cs — valid policy list returns MCP envelope with totalPolicies and policies array, valid trend returns comparison data with imports/newFindings/resolvedFindings/persistentFindings/remediationRate, system_id not found returns error, import_ids referencing non-Prisma imports returns error per contracts/mcp-tools.md

### Implementation for US3

- [X] T039 [US3] Implement ListPrismaPoliciesAsync in src/Ato.Copilot.Agents/Compliance/Services/ScanImport/ScanImportService.cs — replace NotImplementedException stub: query ScanImportFinding records with non-null PrismaPolicyName for system, group by policy name, aggregate NIST controls from ComplianceFinding associations, count open/resolved/dismissed statuses, collect affected CloudResourceType values, include lastSeenImportId and lastSeenAt
- [X] T040 [US3] Verify ListPrismaPoliciesAsync tests pass (green)
- [X] T041 [US3] Implement GetPrismaTrendAsync in src/Ato.Copilot.Agents/Compliance/Services/ScanImport/ScanImportService.cs — replace NotImplementedException stub: query ScanImportRecords with ImportType in (PrismaCsv, PrismaApi), load ScanImportFinding sets for selected imports, compare by PrismaAlertId (present in newer only→new, present in older only→resolved, present in both→persistent), calculate RemediationRate, build optional ResourceTypeBreakdown/NistControlBreakdown from group_by parameter
- [X] T042 [US3] Verify GetPrismaTrendAsync tests pass (green)
- [X] T043 [US3] Implement ListPrismaPoliciesTool extending BaseTool — accept system_id, return policy list with totalPolicies and policies array per contracts/mcp-tools.md in src/Ato.Copilot.Agents/Compliance/Tools/PrismaImportTools.cs
- [X] T044 [US3] Implement PrismaTrendTool extending BaseTool — accept system_id, optional import_ids array, optional group_by (resource_type/nist_control), return trend comparison per contracts/mcp-tools.md in src/Ato.Copilot.Agents/Compliance/Tools/PrismaImportTools.cs
- [X] T045 [US3] Register compliance_list_prisma_policies and compliance_prisma_trend tools with RBAC roles SecurityLead, Analyst, Assessor, Administrator in src/Ato.Copilot.Mcp/Tools/ComplianceMcpTools.cs
- [X] T046 [US3] Write edge case tests in tests/Ato.Copilot.Tests.Unit/Services/PrismaImportServiceTests.cs — single import trend (snapshot with newFindings=totalAlerts, resolvedFindings=0, persistentFindings=0), no Prisma imports for system (error: "No Prisma imports found"), imports with zero overlapping Alert IDs (all new in latest + all resolved from previous), empty import (0 alerts produces valid but empty result)
- [X] T047 [US3] Verify all US3 tests pass (green)

**Checkpoint**: All 4 MCP tools functional. Policy catalog and trend analysis operational with group_by support.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Integration verification, structured logging, performance validation, documentation updates, final validation

### Integration & Observability

- [X] T048 [P] Write integration tests verifying compliance_list_imports and compliance_get_import_summary return Prisma imports with correct ImportType filtering and Prisma-specific fields (PrismaAlertId, CloudResourceType) in tests/Ato.Copilot.Tests.Unit/Services/PrismaImportServiceTests.cs
- [X] T049 [P] Write integration tests verifying Prisma-sourced ComplianceFinding records with Source="Prisma Cloud" appear in SAR generation (compliance_generate_sar), POA&M creation (compliance_create_poam), SSP control narratives (compliance_generate_ssp), SAP scan plan section (compliance_generate_sap), authorization package (compliance_bundle_authorization_package), ConMon reports (compliance_generate_conmon_report), and multi-system dashboard (compliance_multi_system_dashboard) in tests/Ato.Copilot.Tests.Unit/Services/PrismaImportServiceTests.cs
- [X] T050 Add structured Serilog logging to ImportPrismaCsvAsync and ImportPrismaApiAsync — log import start (system_id, file_name, import_type), subscription resolution (subscription_id, resolved_system_id), completion (duration_ms, alert_count, findings_created, nist_controls_affected, effectiveness_records_upserted) using Stopwatch pattern from Feature 017/018 in src/Ato.Copilot.Agents/Compliance/Services/ScanImport/ScanImportService.cs
- [X] T051 [P] Write performance test: generate 500-alert Prisma CSV programmatically, import via ImportPrismaCsvAsync, assert completion in <15 seconds and memory delta <512MB via GC.GetTotalMemory in tests/Ato.Copilot.Tests.Unit/Services/PrismaImportServiceTests.cs
- [X] T052 [P] Write performance test: generate 500-alert Prisma API JSON programmatically, import via ImportPrismaApiAsync, assert completion in <10 seconds and memory delta <512MB via GC.GetTotalMemory in tests/Ato.Copilot.Tests.Unit/Services/PrismaImportServiceTests.cs
- [X] T053 Update ScanImportType enum count assertion from 2 to 4 (Ckl, Xccdf, PrismaCsv, PrismaApi) and ScanSourceType enum count assertion from 4 to 5 (Resource, Policy, Defender, Combined, Cloud) in tests/Ato.Copilot.Tests.Unit/Agents/ComplianceAgentTests.cs

### Documentation Updates

- [X] T054 [P] Update ISSM Guide — add ISSO "Import Prisma Cloud Scan Results" workflow section: CSV export from Prisma console → compliance_import_prisma invocation → import summary interpretation → handling unmapped policies → multi-subscription resolution → re-import after remediation in docs/guides/issm-guide.md (ISSO content lives as a section within the ISSM guide)
- [X] T055 [P] Update SCA Guide — add "Assess Controls Using Prisma Cloud Data" section: how Prisma findings auto-populate ControlEffectiveness, using compliance_prisma_trend for remediation validation, combined STIG + Prisma evidence review per control in docs/guides/sca-guide.md
- [X] T056 [P] Update Engineer Guide — add "Prisma Remediation Workflow" section: viewing Prisma-sourced findings with remediation guidance, CLI remediation scripts from API JSON imports, resource-centric filtering with group_by parameter in docs/guides/engineer-guide.md
- [X] T057 [P] Update ISSM Guide — add "Cloud Posture Oversight" section: directing ISSOs to import Prisma scans, reviewing trend data across systems, Prisma findings in ConMon reports, import cadence guidance (pre-assessment, post-remediation, periodic ConMon) in docs/guides/issm-guide.md
- [X] T058 [P] Update Agent Tool Catalog — add entries for 4 new Prisma MCP tools with descriptions, parameters, RBAC roles, and example invocations per contracts/mcp-tools.md in docs/architecture/agent-tool-catalog.md
- [X] T059 [P] Update Tool Inventory — add 4 new Prisma tools with parameter details, return types, and persona access matrix in docs/reference/tool-inventory.md
- [X] T060 [P] Update RMF Assess Phase page — add Prisma Cloud scan import as assessment input alongside STIG/SCAP imports, document Step 4 as primary stage for initial Prisma import in docs/rmf-phases/assess.md
- [X] T061 [P] Update RMF Monitor Phase page — add Prisma periodic re-import as ConMon data source, trend analysis for drift detection, recommended cadence (monthly/quarterly per ConMon plan) in docs/rmf-phases/monitor.md

### Final Validation

- [X] T062 Build with 0 errors, all new tests pass (target 170+), all existing 3,520 tests still pass, verify XML documentation on all new public types and members (Constitution VI), run quickstart.md validation scenarios end-to-end: CSV dry-run → actual import → auto-resolve subscription → API JSON import → list policies → trend analysis → verify downstream SAR includes Prisma findings

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Setup (Phase 1) — enums and DTOs must exist. **BLOCKS all user stories.**
- **US1 (Phase 3)**: Depends on Foundational (Phase 2) — interfaces and severity mapper must exist
- **US2 (Phase 4)**: Depends on Foundational (Phase 2) — can start after Phase 2 independently of US1, BUT reuses CSV downstream pipeline, so practically depends on US1 T017-T020 for the shared import pipeline code
- **US3 (Phase 5)**: Depends on Foundational (Phase 2) — can start after Phase 2, BUT queries ScanImportFinding/ScanImportRecord data that only exists after imports, so practically depends on US1
- **Polish (Phase 6)**: Depends on all user stories being complete

### User Story Dependencies

- **US1 (P1)**: Can start after Phase 2 — no dependencies on other stories. This is the MVP.
- **US2 (P2)**: Can start parser (T028) after Phase 2 independently. Service implementation (T031) reuses US1's downstream pipeline for finding creation, effectiveness upsert, and evidence creation — so US1 T017-T020 should be complete first.
- **US3 (P3)**: Can start after Phase 2. Queries import data that only exists after US1/US2 imports, but tests can mock the data. Implementation should wait until US1 is complete to ensure import records exist for policy/trend queries.

### Within Each User Story

1. Tests MUST be written and FAIL before implementation (TDD red→green)
2. Parser implementation before service implementation
3. Service implementation before tool implementation
4. Tool implementation before MCP registration
5. All tests green before checkpoint

### Parallel Opportunities

**Phase 1**: T001, T002, T004, T005 can all run in parallel (different files/sections)
**Phase 2**: T007, T008, T009 can all run in parallel (different files)
**Within US1**: T010, T011, T012, T013 (all test files) can run in parallel
**Within US2**: T025, T026, T027 (all test files) can run in parallel
**Within US3**: T036, T037, T038 (all test files) can run in parallel
**Phase 6 Docs**: T054–T061 can ALL run in parallel (different doc files)

---

## Parallel Example: User Story 1

```bash
# Step 1: Launch all US1 test files in parallel (TDD red — all should FAIL):
T010: PrismaCsvParserTests.cs
T011: PrismaSeverityMapperTests.cs
T012: PrismaImportServiceTests.cs (CSV section)
T013: PrismaImportToolTests.cs (CSV section)

# Step 2: Implement parser (sequential — tests depend on it):
T014: PrismaCsvParser.cs
T015: ServiceCollectionExtensions.cs (DI registration)
T016: Verify parser + severity mapper tests pass (green)

# Step 3: Implement service (sequential — builds on parser):
T017: ImportPrismaCsvAsync — parsing + subscription resolution
T018: ImportPrismaCsvAsync — finding creation
T019: ImportPrismaCsvAsync — effectiveness upsert
T020: ImportPrismaCsvAsync — evidence + conflict + dry-run
T021: Verify service tests pass (green)

# Step 4: Implement tool (sequential — builds on service):
T022: ImportPrismaTool in PrismaImportTools.cs
T023: Register in ComplianceMcpTools.cs
T024: Verify tool tests pass (green)
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (test data, enums, DTOs)
2. Complete Phase 2: Foundational (interfaces, severity mapper)
3. Complete Phase 3: User Story 1 — CSV Import
4. **STOP and VALIDATE**: Import a sample Prisma CSV, verify findings created, effectiveness upserted
5. Deploy/demo if ready — CSV import alone delivers value for NAVAIR/FS Azure MO

### Incremental Delivery

1. Setup + Foundational → Foundation ready
2. Add US1 (CSV Import) → Test independently → **Deploy/Demo (MVP!)**
3. Add US2 (API JSON Import) → Test independently → Deploy/Demo — adds remediation guidance
4. Add US3 (Policy & Trend) → Test independently → Deploy/Demo — adds ConMon value
5. Polish (Integration + Docs + Performance) → Full feature ready
6. Each story adds value without breaking previous stories

### Parallel Team Strategy

With multiple developers after Phase 2 is complete:

- **Developer A**: US1 (CSV Import) — critical path, MVP
- **Developer B**: US2 parser only (T028) — can start in parallel since parser is independent; wait for US1 pipeline before service implementation
- **Developer C**: US3 tests (T036-T038) — can write tests with mocked data; wait for US1 before implementation
- All docs (T054-T061) — any developer, all parallel, after implementation complete

---

## Summary

| Phase | Tasks | Count | Description |
|-------|-------|-------|-------------|
| Phase 1: Setup | T001–T005 | 5 | Test data, enum extensions, DTOs |
| Phase 2: Foundational | T006–T009 | 4 | Interface signatures, severity mapper, parser interface |
| Phase 3: US1 CSV Import | T010–T024 | 15 | CSV parser, import service, MCP tool — **MVP** |
| Phase 4: US2 JSON Import | T025–T035 | 11 | JSON parser, enhanced remediation, MCP tool |
| Phase 5: US3 Policy & Trend | T036–T047 | 12 | Policy catalog, trend analysis, 2 MCP tools |
| Phase 6: Polish | T048–T062 | 15 | Integration, logging, performance, 8 doc updates, validation |
| **Total** | **T001–T062** | **62** | **4 tools, 2 parsers, 6 DTOs, 3 enum values, 8 doc updates, 170+ tests** |

## Notes

- [P] tasks = different files, no dependencies on incomplete tasks
- [US1/US2/US3] label maps task to specific user story for traceability
- Each user story is independently completable and testable at its checkpoint
- TDD red→green cycle enforced: tests written before implementation
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
- File paths are relative to repository root
