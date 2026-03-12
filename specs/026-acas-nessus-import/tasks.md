# Tasks: ACAS/Nessus Scan Import

**Input**: Design documents from `/specs/026-acas-nessus-import/`
**Prerequisites**: plan.md (required), spec.md (required), research.md, data-model.md, contracts/mcp-tools.md, quickstart.md

**Tests**: Included — spec defines 26 unit tests (UT-001 through UT-026) and 20 integration tests (IT-001 through IT-020).

**Organization**: Tasks grouped by user story to enable independent implementation and testing.

## Format: `[ID] [P?] [Story?] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: Which user story (US1–US5) — omitted for Setup, Foundational, and Polish phases
- All file paths relative to repository root

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Extend existing models, create new DTOs, and define interfaces — no implementation logic.

- [X] T001 Add `NessusXml` value to `ScanImportType` enum in src/Ato.Copilot.Core/Models/Compliance/ScanImportModels.cs
- [X] T002 [P] Create NessusModels.cs with DTOs (`ParsedNessusFile`, `NessusReportHost`, `NessusPluginResult`, `PluginFamilyMapping`, `NessusControlMappingResult`, `NessusImportResult`) and `NessusControlMappingSource` enum in src/Ato.Copilot.Core/Models/Compliance/NessusModels.cs
- [X] T003 [P] Add Nessus-specific fields to `ScanImportFinding` (`NessusPluginId`, `NessusPluginName`, `NessusPluginFamily`, `NessusHostname`, `NessusHostIp`, `NessusPort`, `NessusProtocol`, `NessusServiceName`, `NessusCvssV3BaseScore`, `NessusCvssV3Vector`, `NessusCvssV2BaseScore`, `NessusVprScore`, `NessusCves`, `NessusExploitAvailable`, `NessusControlMappingSource`) in src/Ato.Copilot.Core/Models/Compliance/ScanImportModels.cs
- [X] T004 [P] Add Nessus-specific counter fields to `ScanImportRecord` (`NessusInformationalCount`, `NessusCriticalCount`, `NessusHighCount`, `NessusMediumCount`, `NessusLowCount`, `NessusHostCount`, `NessusPoamCreatedCount`, `NessusCredentialedScan`) in src/Ato.Copilot.Core/Models/Compliance/ScanImportModels.cs
- [X] T005 Add `ImportNessusAsync` method signature to `IScanImportService` interface in src/Ato.Copilot.Core/Interfaces/Compliance/IScanImportService.cs

**Checkpoint**: All models, DTOs, and interfaces defined. No implementation yet.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core parser and test fixtures that ALL user stories depend on.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [X] T006 Create test fixture .nessus files in tests/Ato.Copilot.Tests.Unit/Resources/: `sample-single-host.nessus` (1 host, 5 plugins — one per severity 0–4, with CVEs and STIG-ID xrefs), `sample-multi-host.nessus` (3 hosts, mixed severities and plugin families), `sample-malformed.nessus` (invalid XML for error handling)
- [X] T007 Implement `INessusParser` interface and `NessusParser` class using XDocument to parse NessusClientData_v2 XML — extract ReportHost properties (hostname, IP, OS, MAC, scan times, credentialed flag) and ReportItem attributes/children (plugin ID, name, family, severity, port, protocol, CVEs, CVSS scores, xrefs, exploit flag, VPR) in src/Ato.Copilot.Agents/Compliance/Services/ScanImport/NessusParser.cs
- [X] T008 Create `NessusParserTests.cs` with parser unit tests (UT-001: parse valid file, UT-002: extract plugin fields, UT-003: extract host properties, UT-008: informational excluded, UT-018: malformed XML rejection, UT-019: empty ReportHost handling, UT-020: multi-host extraction) in tests/Ato.Copilot.Tests.Unit/Parsers/NessusParserTests.cs

**Checkpoint**: Parser tested and working. All user stories can now begin.

---

## Phase 3: User Story 1 — Import ACAS .nessus File (Priority: P1) 🎯 MVP

**Goal**: ISSO/SCA/System Admin imports a .nessus file and findings are created for each plugin-host-port combination with correct severity mapping.

**Independent Test**: Upload sample-single-host.nessus for a registered system, verify ScanImportRecord created with correct counts and ScanImportFinding records with correct plugin IDs, severities, CVEs, and host info.

### Tests for User Story 1

- [X] T009 [P] [US1] Add severity mapping unit tests (UT-004: Critical→CAT I, UT-005: High→CAT I, UT-006: Medium→CAT II, UT-007: Low→CAT III) to tests/Ato.Copilot.Tests.Unit/Services/NessusImportServiceTests.cs
- [X] T010 [P] [US1] Add import infrastructure unit tests (UT-013: SHA-256 hash computation, UT-014: finding identity dedup key — same PluginID+Hostname+Port are duplicates, different ports are distinct) to tests/Ato.Copilot.Tests.Unit/Services/NessusImportServiceTests.cs

### Implementation for User Story 1

- [X] T011 [US1] Implement `ImportNessusAsync` basic orchestration in `ScanImportService.cs` — parse file via INessusParser, validate system exists, check RMF step, compute SHA-256 file hash, create ScanImportRecord, create ScanImportFinding per non-informational plugin-host-port, map severity to CatSeverity, populate Nessus-specific fields, apply Skip conflict resolution as default in src/Ato.Copilot.Agents/Compliance/Services/ScanImport/ScanImportService.cs
- [X] T012 [US1] Create `ImportNessusTool.cs` extending `BaseTool` — Name: `compliance_import_nessus`, parameters: system_id, file_content (base64), file_name, conflict_resolution, dry_run, assessment_id; validate .nessus extension, enforce 5MB file size limit, decode base64, call ImportNessusAsync, return NessusImportResult in src/Ato.Copilot.Agents/Compliance/Tools/ImportNessusTool.cs
- [X] T013 [US1] Register `INessusParser`/`NessusParser` as singleton and `ImportNessusTool` via `RegisterTool<>()` in src/Ato.Copilot.Agents/Extensions/ServiceCollectionExtensions.cs
- [X] T014 [P] [US1] Create `NessusImportToolTests.cs` with tool parameter validation tests (missing system_id, invalid file extension, file size > 5MB, valid invocation) in tests/Ato.Copilot.Tests.Unit/Tools/NessusImportToolTests.cs
- [X] T015 [US1] Add integration tests IT-001 (end-to-end single host), IT-002 (multi-host), IT-014 (nonexistent system validation), IT-015 (file size limit), IT-019 (audit logging), IT-020 (scan timestamp preservation) to tests/Ato.Copilot.Tests.Integration/ScanImport/NessusImportIntegrationTests.cs

**Checkpoint**: Basic import works — .nessus file parsed, findings created with severities, no control mapping yet.

---

## Phase 4: User Story 2 — Automatic Control Mapping (Priority: P1)

**Goal**: Imported vulnerability findings are automatically mapped to NIST 800-53 controls via STIG-ID xref (high confidence) and plugin-family heuristic (medium confidence), with ControlEffectiveness records created for in-baseline controls.

**Independent Test**: Import sample-single-host.nessus (which contains STIG-ID xrefs and known plugin families), verify findings have resolved NIST control IDs, and ControlEffectiveness records exist for baseline controls.

### Tests for User Story 2

- [X] T016 [P] [US2] Add control mapping unit tests (UT-009: CVE→CCI→NIST chain, UT-010: plugin family heuristic mapping, UT-011: heuristic confidence flag, UT-012: unresolved CVE warning, UT-021: mapping table completeness — all 35 entries resolve to valid NIST controls) to tests/Ato.Copilot.Tests.Unit/Services/NessusImportServiceTests.cs

### Implementation for User Story 2

- [X] T017 [P] [US2] Create `plugin-family-mappings.json` with 35-entry curated mapping table (PluginFamily → PrimaryControl + SecondaryControls) as embedded resource in src/Ato.Copilot.Agents/Compliance/Resources/plugin-family-mappings.json
- [X] T018 [P] [US2] Implement `PluginFamilyMappings.cs` to load embedded JSON resource at startup and expose `GetMapping(string pluginFamily)` returning `PluginFamilyMapping` (default to RA-5 for unknown families) in src/Ato.Copilot.Agents/Compliance/Services/ScanImport/PluginFamilyMappings.cs
- [X] T019 [US2] Implement `INessusControlMapper` interface and `NessusControlMapper` class — Priority 1: extract STIG-ID from `<xref>` elements → lookup via IStigKnowledgeService CCI→NIST chain (High confidence); Priority 2: fall back to PluginFamilyMappings lookup (Medium confidence); return NessusControlMappingResult in src/Ato.Copilot.Agents/Compliance/Services/ScanImport/NessusControlMapper.cs
- [X] T020 [US2] Integrate NessusControlMapper into `ImportNessusAsync` — call mapper for each finding, populate ResolvedNistControlIds/ResolvedCciRefs/NessusControlMappingSource, create/update ControlEffectiveness records for in-baseline controls in src/Ato.Copilot.Agents/Compliance/Services/ScanImport/ScanImportService.cs
- [X] T021 [US2] Register `INessusControlMapper`/`NessusControlMapper` as singleton in src/Ato.Copilot.Agents/Extensions/ServiceCollectionExtensions.cs
- [X] T022 [US2] Add integration tests IT-008 (CVE chain mapping), IT-009 (heuristic fallback), IT-010 (baseline-scoped effectiveness) to tests/Ato.Copilot.Tests.Integration/ScanImport/NessusImportIntegrationTests.cs

**Checkpoint**: Import now produces findings with NIST control mappings and confidence indicators. Core P1 functionality complete.

---

## Phase 5: User Story 3 — Import Summary and Dry-Run Preview (Priority: P2)

**Goal**: Users preview import results via dry-run mode before committing. Import summary includes severity breakdown, host count, and control statistics.

**Independent Test**: Run dry-run import of sample-multi-host.nessus, verify preview counts match, confirm no records persisted.

### Implementation for User Story 3

- [X] T023 [US3] Implement dry-run mode in `ImportNessusAsync` — when `dryRun=true`, execute full parse + mapping + dedup pipeline but skip all persistence calls (no ScanImportRecord, no ScanImportFinding, no ControlEffectiveness, no POA&M); return NessusImportResult with accurate preview counts in src/Ato.Copilot.Agents/Compliance/Services/ScanImport/ScanImportService.cs
- [X] T024 [US3] Enrich NessusImportResult with severity breakdown counters (critical/high/medium/low/informational), host count, credentialed scan flag, and warnings for unmapped plugin families in src/Ato.Copilot.Agents/Compliance/Services/ScanImport/ScanImportService.cs
- [X] T025 [US3] Add integration test IT-003 (dry-run mode — preview counts accurate, no records persisted) to tests/Ato.Copilot.Tests.Integration/ScanImport/NessusImportIntegrationTests.cs

**Checkpoint**: Dry-run mode works. Summary reports are comprehensive.

---

## Phase 6: User Story 4 — Import History and Re-Import Management (Priority: P2)

**Goal**: Users view import history with pagination/filters and re-import scans with Overwrite or Merge conflict resolution.

**Independent Test**: Import two .nessus files for the same system, list imports via `compliance_list_nessus_imports`, then re-import with Overwrite and verify updated finding counts.

### Tests for User Story 4

- [X] T026 [P] [US4] Add conflict resolution unit tests (UT-015: Skip keeps existing, UT-016: Overwrite replaces, UT-017: Merge appends) to tests/Ato.Copilot.Tests.Unit/Services/NessusImportServiceTests.cs

### Implementation for User Story 4

- [X] T027 [US4] Implement Overwrite and Merge conflict resolution strategies in `ImportNessusAsync` dedup logic — Overwrite: replace existing finding fields; Merge: keep more-recent status, append new details/comments in src/Ato.Copilot.Agents/Compliance/Services/ScanImport/ScanImportService.cs
- [X] T028 [US4] Create `ListNessusImportsTool.cs` extending `BaseTool` — Name: `compliance_list_nessus_imports`, parameters: system_id, page, page_size (max 50), from_date, to_date, include_dry_runs; call existing `ListImportsAsync` with importType filter `NessusXml`, return paginated results in src/Ato.Copilot.Agents/Compliance/Tools/ListNessusImportsTool.cs
- [X] T029 [US4] Register `ListNessusImportsTool` via `RegisterTool<>()` in src/Ato.Copilot.Agents/Extensions/ServiceCollectionExtensions.cs
- [X] T030 [US4] Add integration tests IT-004 (duplicate file detection), IT-005 (re-import Skip), IT-006 (re-import Overwrite), IT-007 (re-import Merge), IT-013 (import history query with 3 imports) to tests/Ato.Copilot.Tests.Integration/ScanImport/NessusImportIntegrationTests.cs

**Checkpoint**: Full import lifecycle: import, list history, re-import with all conflict strategies.

---

## Phase 7: User Story 5 — POA&M and Weakness Source Integration (Priority: P3)

**Goal**: Open Critical/High/Medium ACAS findings automatically create POA&M weakness entries with `WeaknessSource = "ACAS"`. Resolved findings signal POA&M closure.

**Independent Test**: Import sample-single-host.nessus, verify POA&M weaknesses exist for severity ≥ 2 findings with source "ACAS" and correct NIST control IDs, and that severity 1 (Low) findings have no POA&M entry.

### Tests for User Story 5

- [X] T031 [P] [US5] Add POA&M unit tests (UT-022: Critical creates POA&M, UT-023: High creates POA&M, UT-024: Medium creates POA&M, UT-025: Low excluded, UT-026: Informational excluded) to tests/Ato.Copilot.Tests.Unit/Services/NessusImportServiceTests.cs

### Implementation for User Story 5

- [X] T032 [US5] Implement POA&M weakness generation in `ImportNessusAsync` — for findings with severity ≥ 2 (Medium/High/Critical), create or update POA&M weakness entries with `WeaknessSource = "ACAS"`, mapped NIST control ID, plugin name as weakness description in src/Ato.Copilot.Agents/Compliance/Services/ScanImport/ScanImportService.cs
- [X] T033 [US5] Implement POA&M closure signaling — when re-import shows a previously open finding is no longer present or resolved, flag the corresponding POA&M weakness for review/closure in src/Ato.Copilot.Agents/Compliance/Services/ScanImport/ScanImportService.cs
- [X] T034 [US5] Add integration tests IT-011 (POA&M weakness creation for Critical/High/Medium), IT-012 (POA&M closure signal on resolved finding) to tests/Ato.Copilot.Tests.Integration/ScanImport/NessusImportIntegrationTests.cs

**Checkpoint**: Full pipeline: import → map controls → create POA&M → track remediation.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: RBAC enforcement, observability, performance validation, and documentation updates.

- [X] T035 Add RBAC enforcement to `ImportNessusTool` (ISSO, SCA, SystemAdmin only) and broaden `ListNessusImportsTool` to allow ISSM and AO (read-only access) per documentation update requirements. Update error handling and role validation accordingly in src/Ato.Copilot.Agents/Compliance/Tools/ImportNessusTool.cs and src/Ato.Copilot.Agents/Compliance/Tools/ListNessusImportsTool.cs
- [X] T036 Add integration tests IT-016 (RBAC authorized  ISSO/SCA/SystemAdmin succeed), IT-017 (RBAC unauthorized  Engineer denied; ISSM/AO allowed for list tool) to tests/Ato.Copilot.Tests.Integration/ScanImport/NessusImportIntegrationTests.cs
- [X] T037 [P] Add structured Serilog logging to NessusParser (parse start/end, host count, plugin count), NessusControlMapper (mapping source per finding), and ImportNessusAsync (6-step orchestration progress) in src/Ato.Copilot.Agents/Compliance/Services/ScanImport/NessusParser.cs, NessusControlMapper.cs, and ScanImportService.cs
- [X] T038 [P] Create `sample-large.nessus` test fixture (500+ plugins across 10+ hosts for performance validation) and add IT-018 (large file ≤60s performance test) in tests/Ato.Copilot.Tests.Unit/Resources/sample-large.nessus and tests/Ato.Copilot.Tests.Integration/ScanImport/NessusImportIntegrationTests.cs
- [X] T039 [P] Update documentation per spec Documentation Updates section — agent-tool-catalog.md, data-model.md, mcp-server.md, tool-inventory.md, persona guides (ISSO, SCA, ISSM, Engineer, AO), getting-started pages, RMF phases, persona-test-cases files, and glossary in docs/
- [X] T040 Run quickstart.md validation — build project, execute unit tests, verify all Nessus-related tests pass

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Setup (Phase 1) — T006-T008 need DTOs from T002
- **User Stories (Phases 3–7)**: All depend on Foundational (Phase 2) completion
  - US1 + US2 are both P1 — implement sequentially (US1 first, US2 second)
  - US3 + US4 are both P2 — can proceed in parallel after US1/US2
  - US5 is P3 — depends on US1 (findings) and US2 (control mapping) being complete
- **Polish (Phase 8)**: Depends on all user stories being complete

### User Story Dependencies

- **US1 (Import .nessus)**: Depends on Phase 2 only — no dependency on other stories
- **US2 (Control Mapping)**: Depends on US1 — extends ImportNessusAsync with mapping logic
- **US3 (Dry-Run/Summary)**: Depends on US1 — adds dry-run branch to ImportNessusAsync
- **US4 (History/Re-Import)**: Depends on US1 — extends dedup logic with Overwrite/Merge
- **US5 (POA&M)**: Depends on US1 + US2 — needs findings with mapped controls

### Within Each User Story

- Tests written alongside implementation (same phase)
- Models/DTOs before services
- Services before tools
- DI registration after implementations
- Integration tests after all implementations for that story

### Parallel Opportunities

- Phase 1: T002, T003, T004 can all run in parallel (different sections of different files)
- Phase 2: T006 and T007 can run in parallel (fixtures vs parser implementation)
- Phase 3: T009 and T010 can run in parallel (different test files); T014 in parallel with T009/T010
- Phase 4: T016, T017, T018 can all run in parallel (tests, JSON, loader — different files)
- Phase 5/6/7 after US1+US2 complete: US3 and US4 can proceed in parallel
- Phase 8: T037, T038, T039 can all run in parallel

---

## Parallel Example: Phase 4 (User Story 2)

```
# Launch in parallel (all different files):
T016: Control mapping unit tests           → tests/.../NessusImportServiceTests.cs
T017: plugin-family-mappings.json          → src/.../Resources/plugin-family-mappings.json
T018: PluginFamilyMappings.cs loader       → src/.../ScanImport/PluginFamilyMappings.cs

# Then sequentially:
T019: NessusControlMapper.cs               → depends on T017, T018
T020: Integrate into ImportNessusAsync      → depends on T019
T021: DI registration                      → depends on T019
T022: Integration tests                    → depends on T020
```

---

## Implementation Strategy

### MVP First (User Stories 1 + 2)

1. Complete Phase 1: Setup (models, DTOs, interfaces)
2. Complete Phase 2: Foundational (parser + test fixtures)
3. Complete Phase 3: US1 — Basic Import (parse → findings)
4. Complete Phase 4: US2 — Control Mapping (STIG xref + plugin family heuristic)
5. **STOP and VALIDATE**: Import a .nessus file end-to-end with control mappings
6. Deploy/demo if ready — this delivers the core value proposition

### Incremental Delivery

1. Setup + Foundational → Parser ready
2. US1 → Basic import works → **Milestone: "Files import"**
3. US2 → Controls auto-mapped → **Milestone: "MVP — core value delivered"**
4. US3 → Dry-run + summaries → **Milestone: "Production-safe"**
5. US4 → History + re-import → **Milestone: "Full lifecycle"**
6. US5 → POA&M integration → **Milestone: "Compliance pipeline complete"**
7. Polish → Docs, RBAC, logging, perf → **Milestone: "Release-ready"**

---

## Notes

- Total tasks: 40
- [P] tasks = different files, no dependencies on incomplete tasks
- [US#] label maps task to specific user story for traceability
- Test fixtures (T006) include samples for severities 0–4, multi-host, and malformed XML
- Plugin family mapping is curated/fixed (spec clarification C4) — no CRUD UI needed
- Finding identity key: Plugin ID + Hostname + Port (spec clarification C1)
- Informational plugins (severity 0) excluded from findings, counted in summary (spec clarification C5)
