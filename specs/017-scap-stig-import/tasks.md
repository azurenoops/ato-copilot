# Tasks: 017 — SCAP/STIG Viewer Import

**Input**: Design documents from `/specs/017-scap-stig-import/`  
**Prerequisites**: plan.md ✅, spec.md ✅, data-model.md ✅, contracts/mcp-tools.md ✅, research.md ✅, quickstart.md ✅

**Tests**: Included per constitution principle III. Each parser/service/tool gets positive + negative tests.

**Organization**: Tasks grouped by implementation phase. Each phase is independently testable.

## Format: `[ID] [P?] [Cap#] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Cap#]**: Maps to spec capability numbers (1.x = Phase 1, 2.x = Phase 2, etc.)

---

## Phase 1: Setup & Foundation

**Purpose**: New enums, entities, DTOs, DbContext scaffolding.

- [X] T001 Create branch `017-scap-stig-import` from `main` and verify clean build
- [X] T002 [P] Create `src/Ato.Copilot.Core/Models/Compliance/ScanImportModels.cs` — enums (`ScanImportType`, `ScanImportStatus`, `ImportConflictResolution`, `ImportFindingAction`), `ScanImportRecord` entity, `ScanImportFinding` entity per data-model.md
- [X] T003 [P] Create parsed DTOs in `ScanImportModels.cs` — `ParsedCklEntry`, `ParsedCklFile`, `CklAssetInfo`, `CklStigInfo`, `ParsedXccdfResult`, `ParsedXccdfFile`, `ImportResult`, `UnmatchedRuleInfo`
- [X] T004 Add nullable `ImportRecordId` FK (string?) to `ComplianceFinding` in `src/Ato.Copilot.Core/Models/Compliance/ComplianceModels.cs`
- [X] T005 Add `ScanImportRecords` and `ScanImportFindings` DbSets to `src/Ato.Copilot.Core/Data/Context/AtoCopilotContext.cs` with EF Core configuration per data-model.md (relationships, JSON columns, indexes)
- [X] T006 EF Core migration not needed — project uses EnsureCreatedAsync pattern
- [X] T007 [P] Create unit tests for new enums and entity validation in `tests/Ato.Copilot.Tests.Unit/Models/ScanImportModelTests.cs` (25 tests pass)

**Checkpoint**: All entities, enums, and DTOs exist. Build passes with zero warnings.

---

## Phase 2: CKL Parser (Spec §1.1)

**Purpose**: Parse DISA STIG Viewer CKL XML files into typed DTOs.

- [X] T008 Create `src/Ato.Copilot.Agents/Compliance/Services/ScanImport/CklParser.cs` implementing `ICklParser` — parse CHECKLIST XML with XDocument, extract ASSET info, STIG_INFO SI_DATA elements, and all VULN entries. Handle malformed XML with XmlException catching and descriptive error messages.
- [X] T009 [P] Create test data file `tests/Ato.Copilot.Tests.Unit/TestData/sample-valid.ckl` — valid CKL with 5 VULNs: 2 Open (high/medium), 2 NotAFinding, 1 Not_Applicable. Include ASSET/STIG_INFO/CCI_REF data.
- [X] T010 [P] Create test data file `tests/Ato.Copilot.Tests.Unit/TestData/sample-malformed.ckl` — truncated XML for error handling tests
- [X] T011 [P] Create test data file `tests/Ato.Copilot.Tests.Unit/TestData/sample-severity-override.ckl` — CKL with SEVERITY_OVERRIDE and SEVERITY_JUSTIFICATION fields populated
- [X] T012 Create unit tests in `tests/Ato.Copilot.Tests.Unit/Parsers/CklParserTests.cs` (23 tests pass):
  - Parse valid CKL → correct entry count, statuses, severities
  - Parse ASSET section → correct hostname, IP, MAC
  - Parse STIG_INFO → correct benchmark ID, version, title
  - Parse CCI_REF → multiple CCI refs per VULN
  - Parse severity override → override value used
  - Parse malformed XML → descriptive error, not crash
  - Parse empty VULN list → empty entries, no error
  - Parse CKL with Not_Reviewed → correct status mapping
  - Parse CKL with missing optional fields → null values, no crash

**Checkpoint**: CKL parser handles valid and malformed files. All parser tests pass.

---

## Phase 3: STIG Resolution & CCI→NIST Mapping (Spec §1.2, §1.3)

**Purpose**: Map parsed CKL entries to existing StigControl records and resolve NIST controls via CCI chain.

- [X] T013 Create `src/Ato.Copilot.Core/Interfaces/Compliance/IScanImportService.cs` with methods:
  - `ImportCklAsync(string systemId, string? assessmentId, byte[] fileContent, string fileName, ImportConflictResolution resolution, bool dryRun, string importedBy, CancellationToken ct)` — service receives raw bytes (tool layer decodes base64)
  - `ImportXccdfAsync(...)` (same signature pattern)
  - `ExportCklAsync(string systemId, string benchmarkId, string? assessmentId, CancellationToken ct)`
  - `ListImportsAsync(string systemId, int page, int pageSize, string? benchmarkId, string? importType, bool includeDryRuns, DateTime? fromDate, DateTime? toDate, CancellationToken ct)`
  - `GetImportSummaryAsync(string importId, CancellationToken ct)` *(IScanImportService.cs created with 5 methods)*
- [X] T014 Create STIG resolution logic in `src/Ato.Copilot.Agents/Compliance/Services/ScanImport/ScanImportService.cs`:
  - Resolve VulnId → `IStigKnowledgeService.GetStigControlAsync(vulnId)`
  - Fallback: RuleId → `IStigKnowledgeService.GetStigControlByRuleIdAsync(ruleId)` (new method)
  - Fallback: StigVersion → search by version string
  - Track unmatched entries with VulnId/RuleId/Title for reporting *(ScanImportService.cs 880 lines, full pipeline)*
- [X] T015 Implement CCI→NIST mapping in `ScanImportService`:
  - For each matched StigControl, iterate `CciRefs`
  - Lookup each CCI in `cci-nist-mapping.json` via `IStigKnowledgeService.GetCciMappingsAsync`
  - Collect unique NIST control IDs
  - Validate against system's `ControlBaseline.ControlIds` — log controls not in baseline *(Uses StigControl.NistControls with baseline validation)*
- [X] T016 Add `GetStigControlByRuleIdAsync(string ruleId)` to `IStigKnowledgeService` interface in `src/Ato.Copilot.Core/Interfaces/Compliance/IComplianceInterfaces.cs`
- [X] T017 Implement `GetStigControlByRuleIdAsync` in `src/Ato.Copilot.Agents/Compliance/Services/KnowledgeBase/StigKnowledgeService.cs` — index StigControls by RuleId on first call, cache result *(cached Dictionary<string, StigControl> with 24h TTL)*
- [X] T018 [P] Create unit tests for STIG resolution in `tests/Ato.Copilot.Tests.Unit/Services/ScanImportServiceTests.cs`:
  - VulnId match → correct StigControl returned
  - VulnId miss, RuleId match → fallback works
  - Both miss → unmatched, no error
  - CCI resolution → correct NIST controls
  - CCI with no NIST mapping → warning logged
  - NIST control not in baseline → warning logged
  - Multiple CCI refs → all NIST controls resolved *(45 tests total, all pass)*

**Checkpoint**: STIG resolution and CCI→NIST mapping working. Unmatched rules tracked.

---

## Phase 4: Finding & Effectiveness Creation (Spec §1.4, §1.5, §1.6)

**Purpose**: Create ComplianceFinding, ControlEffectiveness, and ComplianceEvidence records from parsed CKL data.

- [X] T019 Implement finding creation in `ScanImportService`:
  - CKL `Open` → `ComplianceFinding` with `StigFinding = true`, `StigId = VulnId`, `CatSeverity` mapped from severity, `Source = "CKL Import"`, `FindingStatus = Open`
  - CKL `NotAFinding` → `ComplianceFinding` with `FindingStatus = Remediated`
  - CKL `Not_Applicable` → `ScanImportFinding` with `ImportAction = NotApplicable` (no ComplianceFinding)
  - CKL `Not_Reviewed` → `ComplianceFinding` with `FindingStatus = Open` and note "Not yet reviewed" in FindingDetails + `ScanImportFinding` with `ImportAction = NotReviewed`
  - For NIST controls **outside** the system's control baseline: create `ComplianceFinding` only (audit trail), skip `ControlEffectiveness`. Add warning to import result.
  - Set `ImportRecordId` FK on all created findings
  - Validate system is in RMF step Assess or later; if not, add warning to import result (do not block)
- [X] T020 [P] Implement ControlEffectiveness upsert in `ScanImportService`:
  - Group findings by NIST control ID (only controls **in** system's control baseline)
  - For each unique in-baseline control: re-evaluate **all** current `ComplianceFinding` records for that control (not just this import's findings) to determine aggregate effectiveness
  - ALL findings for the control are `NotAFinding`/`Remediated` → `Satisfied`
  - ANY finding for the control is `Open` → `OtherThanSatisfied`
  - Set `AssessmentMethod = "Test"`, `AssessorId = importedBy`
- [X] T021 [P] Implement evidence creation in `ScanImportService`:
  - Create `ComplianceEvidence` per import (one evidence per CKL file):
    - `EvidenceType = "StigChecklist"`
    - `EvidenceCategory = Configuration`
    - `CollectionMethod = "Manual"` (CKL) or `"Automated"` (XCCDF)
    - `Content = JSON summary of parsed results`
    - `ContentHash = SHA-256 of raw file bytes`
    - `CollectedAt = ScanTimestamp or ImportedAt`
  - Link evidence to ControlEffectiveness via `EvidenceIds`
- [X] T022 [P] Create unit tests for finding creation:
  - Open → ComplianceFinding created with correct fields
  - NotAFinding → finding with Remediated status
  - Not_Applicable → no finding, ScanImportFinding with NotApplicable action
  - Not_Reviewed → ComplianceFinding with FindingStatus.Open and "Not yet reviewed" in FindingDetails
  - Severity mapping: high→CatI, medium→CatII, low→CatIII
  - SeverityOverride applied when present
  - Multiple CCI refs → finding linked to multiple NIST controls
  - Out-of-baseline NIST control → ComplianceFinding created, no ControlEffectiveness, warning emitted
  - System not in RMF Assess step → warning emitted, import proceeds
- [X] T023 [P] Create unit tests for effectiveness upsert:
  - All rules pass for control → Satisfied
  - One rule fails for control → OtherThanSatisfied
  - Mixed results across multiple controls → correct per-control determination
  - ControlEffectiveness.AssessmentMethod = "Test"
  - Re-import with Merge: previously-Open rule now NotAFinding + all other rules pass → control flips to Satisfied (aggregate re-evaluation)
  - Re-import: effectiveness re-evaluates ALL findings for control, not just current import's findings
  - Out-of-baseline control → no ControlEffectiveness created
- [X] T024 [P] Create unit tests for evidence creation:
  - SHA-256 hash computed correctly
  - EvidenceType = "StigChecklist" for CKL
  - Content contains summary JSON
  - Evidence linked to effectiveness records

**Checkpoint**: Full import pipeline working — CKL → findings → effectiveness → evidence.

---

## Phase 5: Conflict Resolution & Dry-Run (Spec §1.7, §1.8)

**Purpose**: Handle re-imports with conflict resolution and provide preview mode.

- [X] T025 Implement conflict detection in `ScanImportService`:
  - Query existing `ComplianceFinding` by `StigId` + `AssessmentId`
  - Track conflicts per finding
- [X] T026 Implement conflict resolution strategies:
  - **Skip**: Leave existing finding unchanged, log as skipped
  - **Overwrite**: Update existing finding with imported data (status, details, severity), update `ModifiedAt`
  - **Merge**: Keep more-recent status; append finding details if different; use imported severity only if higher
- [X] T027 Implement dry-run mode:
  - Run full parse, resolution, and conflict detection
  - Build `ImportResult` with all counts and warnings
  - Do NOT persist any changes (no DB writes)
  - Set `ScanImportRecord.IsDryRun = true`
  - Return `ImportResult` with preview data
- [X] T028 [P] Create unit tests for conflict resolution:
  - Skip: existing finding unchanged, import count = 0
  - Overwrite: finding updated with imported data
  - Merge: more-recent status wins, details appended
  - Merge: severity takes higher value
  - No conflict: new finding created regardless of strategy
- [X] T029 [P] Create unit tests for dry-run:
  - Dry-run returns accurate counts
  - Dry-run creates no DB records
  - Dry-run results match non-dry-run results

**Checkpoint**: Conflict resolution and dry-run working. Re-imports handled correctly.

---

## Phase 6: CKL Import MCP Tool (Spec §1.9)

**Purpose**: Expose CKL import as an MCP tool.

- [X] T030 Create `ImportCklTool` in `src/Ato.Copilot.Agents/Compliance/Tools/ScanImportTools.cs`:
  - Extends `BaseTool`
  - Parameters: `system_id` (required), `file_content` (base64, required), `file_name` (required), `conflict_resolution` (optional, default Skip), `dry_run` (optional, default false), `assessment_id` (optional)
  - **Tool layer responsibility**: decode base64 string → `byte[]`, validate decoded size ≤ 5MB, then pass `byte[]` to `IScanImportService.ImportCklAsync` (service layer receives raw bytes, not base64)
  - Check for duplicate file: compute SHA-256 of decoded bytes, query `ScanImportRecord` by `FileHash` + `RegisteredSystemId`. If match found, add warning: "File previously imported on {date} (import ID: {id})" — proceed with import
  - Return `ImportResult` as formatted summary
- [X] T031 Register `ImportCklTool` in `src/Ato.Copilot.Mcp/Tools/ComplianceMcpTools.cs`
- [X] T032 Register `ScanImportService`, `CklParser` in `src/Ato.Copilot.Agents/Extensions/ServiceCollectionExtensions.cs`
- [X] T033 [P] Create unit tests for `ImportCklTool`:
  - Valid import → success with counts
  - Missing system_id → error
  - Invalid base64 → error
  - File too large → error with helpful message
  - Duplicate file (same SHA-256 + system) → warning present in response, import proceeds
  - Dry-run flag → no persistence
  - System not found → error
  - Invalid CKL XML → error with parse details
- [X] T034 [P] Create integration test in `tests/Ato.Copilot.Tests.Integration/Tools/ScanImportIntegrationTests.cs`:
  - Register system → import CKL → verify findings created → verify effectiveness records → verify evidence → verify import history

**Checkpoint**: CKL import available as MCP tool. End-to-end integration tested.

---

## Phase 7: XCCDF Parser & Import (Spec §2.1–§2.5)

**Purpose**: Parse XCCDF results and import with same downstream pipeline.

- [X] T035 Create `src/Ato.Copilot.Agents/Compliance/Services/ScanImport/XccdfParser.cs`:
  - Parse XCCDF TestResult XML with namespace-aware XDocument parsing
  - Extract benchmark href, target info, target-facts
  - Parse all rule-result elements: idref, result, severity, weight, message
  - Extract DISA rule ID from XCCDF idref (`xccdf_mil.disa.stig_rule_SV-254239r849090_rule` → `SV-254239r849090_rule`)
  - Parse score element
  - Handle namespace variations (XCCDF 1.1 and 1.2)
- [X] T036 [P] Create test data file `tests/Ato.Copilot.Tests.Unit/TestData/sample-valid.xccdf` — valid XCCDF with 5 rule-results: 2 fail, 2 pass, 1 notapplicable
- [X] T037 [P] Create test data file `tests/Ato.Copilot.Tests.Unit/TestData/sample-malformed.xccdf`
- [X] T038 Implement `ImportXccdfAsync` in `ScanImportService`:
  - Call `XccdfParser.Parse`
  - Resolve XCCDF rule IDs → StigControl via `GetStigControlByRuleIdAsync`
  - Reuse CCI→NIST mapping, finding creation, effectiveness upsert, evidence creation
  - Set `CollectionMethod = "Automated"` (XCCDF = machine-verified)
  - Capture XCCDF score in `ScanImportRecord.XccdfScore`
- [X] T039 Create `ImportXccdfTool` in `ScanImportTools.cs` — same pattern as `ImportCklTool`
- [X] T040 Register `ImportXccdfTool` in `ComplianceMcpTools.cs` and `XccdfParser` in DI
- [X] T041 [P] Create unit tests in `tests/Ato.Copilot.Tests.Unit/Parsers/XccdfParserTests.cs`:
  - Parse valid XCCDF → correct result count, statuses
  - Parse target info → hostname, IP, OS
  - Parse score → correct value
  - Rule ID extraction → `SV-XXXXX` from XCCDF idref
  - Namespace handling → works with XCCDF 1.1 and 1.2
  - Malformed XML → descriptive error
  - Empty results → no error
  - Error/unknown results → flagged for review
- [X] T042 [P] Create unit tests for XCCDF import:
  - `fail` → Open finding + OtherThanSatisfied
  - `pass` → Satisfied effectiveness
  - `error` → flagged, no effectiveness change
  - `notapplicable` → no finding
  - Score captured in import record
  - CollectionMethod = "Automated"

**Checkpoint**: XCCDF import fully working. Both CKL and XCCDF share downstream pipeline.

---

## Phase 8: CKL Export (Spec §3.1–§3.3)

**Purpose**: Generate CKL files from assessment data.

- [X] T043 Create `src/Ato.Copilot.Agents/Compliance/Services/ScanImport/CklGenerator.cs`:
  - Generate CHECKLIST XML using XDocument/XElement
  - ASSET section from RegisteredSystem metadata
  - STIG_INFO from benchmark metadata
  - Enumerate **all** `StigControl` records matching the benchmark (full STIG coverage)
  - For each StigControl: left-join with `ComplianceFinding` records for the system/assessment
  - VULN entries with matching finding: map `FindingStatus` → CKL STATUS (Open→Open, Remediated→NotAFinding, Accepted→Not_Applicable)
  - VULN entries with **no** matching finding: default to `Not_Reviewed` STATUS
  - Include FINDING_DETAILS and COMMENTS from finding descriptions (empty string for unassessed rules)
- [X] T044 Implement `ExportCklAsync` in `ScanImportService`:
  - Validate system exists
  - Validate benchmark exists in `StigControl` library (at least one rule with matching `BenchmarkId`)
  - List all StigControl records for the benchmark → pass to CklGenerator
  - Query `ComplianceFinding` records for system/assessment with `StigFinding = true` → pass to CklGenerator for STATUS lookup
  - Return base64-encoded XML (assessment data optional — unassessed rules default to Not_Reviewed)
- [X] T045 Create `ExportCklTool` in `ScanImportTools.cs`:
  - Parameters: `system_id` (required), `benchmark_id` (required), `assessment_id` (optional)
  - Return base64-encoded CKL content with file name suggestion
- [X] T046 Register `ExportCklTool` in `ComplianceMcpTools.cs` and `CklGenerator` in DI
- [X] T047 [P] Create unit tests for CKL generation:
  - Generated XML is well-formed
  - ASSET section contains system metadata
  - VULN entries have correct STATUS mapping
  - CCI_REF elements included
  - Exported CKL re-parseable by CklParser (round-trip)
  - No findings for benchmark → CKL with all VULNs set to Not_Reviewed (full benchmark, not empty)
  - Only StigControl entries for selected benchmark included
  - Partial assessment: some findings assessed, rest default to Not_Reviewed

**Checkpoint**: CKL round-trip verified — import → modify → export → re-import.

---

## Phase 9: Import Management Tools (Spec §4.1–§4.5)

**Purpose**: Import history tracking and summary tools.

- [X] T048 Implement `ListImportsAsync` in `ScanImportService`:
  - Filter by system_id, optional date range, optional benchmark_id
  - Paginated (default 20, max 100)
  - Order by ImportedAt descending
  - Exclude dry-run records by default (optional flag to include)
- [X] T049 Implement `GetImportSummaryAsync` in `ScanImportService`:
  - Load ScanImportRecord with all ScanImportFindings
  - Include unmatched rules list
  - Include per-NIST-control breakdown showing effectiveness outcomes
- [X] T050 Create `ListImportsTool` in `ScanImportTools.cs`:
  - Parameters: `system_id` (required), `page` (optional), `page_size` (optional), `benchmark_id` (optional), `include_dry_runs` (optional)
  - Return paginated list of import summaries
- [X] T051 Create `GetImportSummaryTool` in `ScanImportTools.cs`:
  - Parameters: `import_id` (required)
  - Return detailed import breakdown with finding-level details
- [X] T052 Register `ListImportsTool` and `GetImportSummaryTool` in `ComplianceMcpTools.cs`
- [X] T053 [P] Create unit tests for list/summary tools:
  - List by system → correct results, pagination
  - List by benchmark → filtered correctly
  - Summary includes finding counts
  - Summary includes unmatched rules
  - Summary includes NIST control breakdown
  - Import not found → error
  - System with no imports → empty list

**Checkpoint**: Full import management capability. Feature complete.

---

## Phase 10: Polish & Documentation

**Purpose**: Final integration, documentation, and validation.

- [X] T054 Update `docs/architecture/agent-tool-catalog.md` with 5 new tool entries
- [X] T055 [P] Update `docs/guides/issm-guide.md` with STIG import workflow section
- [X] T056 [P] Update `docs/guides/sca-guide.md` with SCAP import for assessment section
- [X] T057 [P] Update `docs/guides/engineer-guide.md` with CKL import/export from VS Code
- [X] T058 [P] Update `docs/reference/stig-coverage.md` with import capability
- [X] T059 Add `[1.18.0]` entry to `CHANGELOG.md` documenting SCAP/STIG Viewer import feature
- [X] T060 Final integration test: register system → categorize → select baseline → import CKL → verify findings map to baseline controls → export CKL → re-import with Skip → verify no duplicates
- [X] T061 [P] Add structured logging to `ScanImportService` using `ILogger<ScanImportService>`:
  - Log import started: file name, file hash, system ID, import type, conflict resolution
  - Log import completed: duration, finding counts, effectiveness counts, warnings count
  - Log parse errors at Warning level with file name and error details
  - Log unmatched rules at Warning level with VulnId/RuleId list
  - Log out-of-baseline controls at Information level
- [X] T062 [P] Create performance benchmark tests in `tests/Ato.Copilot.Tests.Unit/Performance/ScanImportPerformanceTests.cs`:
  - Generate sample CKL with 500 VULNs programmatically → import must complete in < 10 seconds
  - Generate sample XCCDF with 500 rule-results → import must complete in < 5 seconds
  - Measure and assert wall-clock time using `System.Diagnostics.Stopwatch`

**Checkpoint**: Feature 017 complete. All tests pass, docs updated, changelog written.

---

## Task Count Summary

| Phase | Tasks | Parallelizable | Description |
|-------|-------|---------------|-------------|
| Phase 1 | 7 | 3 | Setup & foundation entities |
| Phase 2 | 5 | 3 | CKL parser |
| Phase 3 | 6 | 1 | STIG resolution & CCI mapping |
| Phase 4 | 6 | 4 | Finding & effectiveness creation |
| Phase 5 | 5 | 2 | Conflict resolution & dry-run |
| Phase 6 | 5 | 2 | CKL import MCP tool |
| Phase 7 | 8 | 4 | XCCDF parser & import |
| Phase 8 | 5 | 1 | CKL export |
| Phase 9 | 6 | 1 | Import management tools |
| Phase 10 | 9 | 6 | Polish & documentation |
| **Total** | **62** | **27** | |
