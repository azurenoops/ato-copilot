# Comprehensive Requirements Quality Checklist: 017 — SCAP/STIG Viewer Import

**Purpose**: Validate completeness, clarity, consistency, and measurability of requirements for Feature 017.  
**Created**: 2026-03-01  
**Feature**: [spec.md](../spec.md) | [plan.md](../plan.md) | [data-model.md](../data-model.md) | [contracts/mcp-tools.md](../contracts/mcp-tools.md)  
**Depth**: Deep (30+ items)  
**Audience**: Author self-review before implementation  
**Focus**: CKL/XCCDF parsing, STIG resolution, finding creation, import management

---

## Requirement Completeness — Parsing

- [x] CHK001 — Are all CKL XML elements documented that the parser must handle? [Completeness, Spec §3.1]
  > **PASS**: Spec §3.1 documents CHECKLIST structure: ASSET (7 fields), STIGS/iSTIG/STIG_INFO (SI_DATA pairs), VULN (STIG_DATA pairs + STATUS/FINDING_DETAILS/COMMENTS/SEVERITY_OVERRIDE/SEVERITY_JUSTIFICATION). Research R1 adds edge cases (multiple iSTIG, missing CCI_REF, HTML in details).

- [x] CHK002 — Are all CKL STATUS values mapped with clear behavior? [Completeness, Spec §3.1]
  > **PASS**: Four STATUS values fully mapped: Open→FindingStatus.Open, NotAFinding→FindingStatus.Remediated, Not_Applicable→no finding created, Not_Reviewed→FindingStatus.Open with note "Not yet reviewed" (clarified 2026-03-03). Each maps to ControlEffectiveness determination.

- [x] CHK003 — Are all XCCDF result values documented with mapping? [Completeness, Spec §3.2]
  > **PASS**: Nine result values mapped: pass, fail, error, unknown, notapplicable, notchecked, notselected, informational, fixed. Error/unknown flagged for manual review.

- [x] CHK004 — Is the XCCDF namespace handling strategy defined for version compatibility? [Completeness, Research R2]
  > **PASS**: Research R2 defines: try XCCDF 1.2 namespace first, fall back to 1.1, then namespace-agnostic local-name matching. ARF wrapper detection also specified.

- [x] CHK005 — Are malformed XML handling requirements specified? [Completeness, Spec §1.1]
  > **PASS**: Spec §1.1 requires "Handle malformed/truncated XML gracefully with detailed error reporting." Research R1 specifies XmlException catching. Test data includes malformed samples (T010, T037).

## Requirement Completeness — Resolution Pipeline

- [x] CHK006 — Is the STIG resolution fallback chain fully specified? [Completeness, Spec §6]
  > **PASS**: Spec §6 defines: Primary=VulnId, Fallback 1=RuleId, Fallback 2=StigVersion. Unmatched entries logged with VulnId/RuleId/Title. T014 implements this chain.

- [x] CHK007 — Is the CCI→NIST mapping resolution documented with failure handling? [Completeness, Spec §6]
  > **PASS**: Spec §6 step 3 defines: CCI→cci-nist-mapping.json→NIST IDs. Validate against ControlBaseline.ControlIds. Log controls not in baseline. Research R3 confirms missing CCI refs logged as warnings.

- [x] CHK008 — Is the aggregation rule for ControlEffectiveness clearly defined when multiple STIG rules map to the same NIST control? [Clarity, Research R3]
  > **PASS**: Research R3 defines: "worst result" — any Open finding → OtherThanSatisfied for the NIST control. All rules pass → Satisfied. Clarified 2026-03-03: re-evaluates ALL current findings for the control after each import (aggregate state, not per-import).

- [x] CHK009 — Is the behavior defined when a CKL entry has zero CCI references? [Edge Case, Research R1]
  > **PASS**: Research R1 identifies this edge case for older STIGs. Fallback: use curated `StigControl.NistControls` field. If StigControl not found either → unmatched, logged.

## Requirement Completeness — Import Management

- [x] CHK010 — Is the assessment context behavior specified when no assessment_id is provided? [Completeness, Spec §7.4]
  > **PASS**: Spec §7.4 defines three cases: (1) assessment_id provided → validate active, (2) no active assessment → create one with source="STIG/SCAP Import", (3) active assessment exists → use it with warning.

- [x] CHK011 — Is dry-run behavior specified in enough detail? [Completeness, Spec §1.8]
  > **PASS**: Spec §1.8 requires "Preview import results without persisting changes." Data-model sets `IsDryRun=true`. T027 requires no DB writes. T029 validates dry-run accuracy matches actual.

- [x] CHK012 — Is import history sufficient for audit requirements? [Completeness, Data Model]
  > **PASS**: ScanImportRecord captures: file hash, file size, benchmark, target, all counts, conflict resolution used, dry-run flag, importer identity, timestamp, warnings. ScanImportFinding provides per-rule audit trail.

## Requirement Clarity — Ambiguous Terms

- [x] CHK013 — Is "merge" conflict resolution defined with specific field-level behavior? [Clarity, Research R5]
  > **PASS**: Research R5 defines per-field merge rules: Status takes imported (more current), Severity takes higher (conservative), FindingDetails appends with timestamp separator, Comments appends, CatSeverity uses override if present, Timestamps update DiscoveredAt.

- [x] CHK014 — Is "unmatched rule" clearly defined? [Clarity, Spec §4.5]
  > **PASS**: A STIG rule is "unmatched" when neither VulnId, RuleId, nor StigVersion matches any record in the curated `stig-controls.json` library. Tracked in `ScanImportFinding.ImportAction = Unmatched`.

- [x] CHK015 — Is the 5MB file size limit justified and clearly documented? [Clarity, Spec §7.3]
  > **PASS**: Spec §7.3 provides size analysis showing typical CKL/XCCDF files are well under 5MB. Research R4 confirms >99% coverage. Error message provides actionable guidance ("Consider splitting by benchmark").

## Requirement Consistency

- [x] CHK016 — Is `ScanSourceType` consistently used? Existing findings use `Resource|Policy|Defender|Combined` — does "CKL Import" fit any of these? [Consistency, Spec §1.4]
  > **PASS**: Spec §1.4 sets `ScanSource = ScanSourceType.Combined`. The `Source` string field (not enum) gets the descriptive value `"CKL Import"` / `"SCAP Import"`. This matches the existing pattern where `Source` is a descriptive string.

- [x] CHK017 — Are new `EvidenceType` values consistent with existing types? [Consistency, Data Model]
  > **PASS**: `EvidenceType` is a string field, not an enum. Existing values: "ConfigurationExport", "PolicySnapshot", etc. New values "StigChecklist" and "ScapResult" follow the same naming convention.

- [x] CHK018 — Is the `ComplianceFinding.ImportRecordId` FK nullable and backward-compatible? [Consistency, Data Model]
  > **PASS**: `ImportRecordId` is nullable (string?). Existing findings have null ImportRecordId. OnDelete=SetNull. No breaking changes to existing data.

- [x] CHK019 — Are RBAC roles consistent between import tools and existing assessment tools? [Consistency, Contracts RBAC Summary]
  > **PASS**: Import tools use SecurityLead/Analyst/Administrator — same roles that can write assessment data. Auditor gets read-only (list/summary) matching existing assessment tool pattern.

## Acceptance Criteria Quality

- [x] CHK020 — Are performance requirements testable? [Measurability, Spec §10]
  > **PASS**: "CKL import < 10s for 500 VULNs" and "XCCDF import < 5s for 500 rule-results" are specific, measurable, and testable with sample files.

- [x] CHK021 — Is the "≥95% CCI mapping success" criterion measurable? [Measurability, Spec §10]
  > **PASS**: Measurable from import results: (resolved CCI refs / total CCI refs) × 100. The 5% margin accounts for CCI entries not yet in cci-nist-mapping.json.

- [x] CHK022 — Is "CKL export re-importable by STIG Viewer" verifiable? [Measurability, Spec §10]
  > **PASS**: Verifiable via round-trip test: export CKL → re-parse with CklParser → compare data. Manual verification with STIG Viewer is an acceptance test, not automated.

## Scenario Coverage — Happy Path

- [x] CHK023 — Is the end-to-end import flow fully specified as a connected workflow? [Coverage, Spec §6]
  > **PASS**: Spec §6 defines pipeline: Parse → Resolve STIG → Resolve NIST → Check Conflicts → Apply → Record. Integration test T060 validates full flow: register → categorize → baseline → import CKL → verify findings → export CKL → re-import.

- [x] CHK024 — Is the CKL round-trip (import → modify → export → re-import) tested? [Coverage, Tasks T047]
  > **PASS**: T047 includes "Exported CKL re-parseable by CklParser (round-trip)" test. T060 integration test covers import → export → re-import with Skip.

## Scenario Coverage — Exception Flows

- [x] CHK025 — What happens when all CKL entries are Not_Applicable? [Edge Case]
  > **PASS**: Import completes with `openCount=0, passCount=0, notApplicableCount=N`. No ComplianceFindings created. No ControlEffectiveness records. `ImportStatus = Completed` (not an error — valid assessment outcome).

- [x] CHK026 — What happens when a CKL contains VULNs for a STIG that has no overlap with the system's control baseline? [Edge Case]
  > **PASS**: CCI→NIST resolution produces NIST controls. Controls not in baseline → logged as warnings in `ScanImportRecord.Warnings`. Findings still created (audit trail). ControlEffectiveness NOT created for out-of-baseline controls (clarified 2026-03-03).

- [x] CHK027 — What happens when the same CKL file is imported twice with Skip resolution? [Edge Case]
  > **PASS**: Second import detects all findings as conflicts, skips all, `SkippedCount = N`, `FindingsCreated = 0`. Import record still created for audit trail. T060 tests this scenario.

- [x] CHK028 — What happens when the system has no ControlBaseline selected? [Edge Case]
  > **PASS**: Spec §7.1 requires `IBaselineService.GetBaselineAsync`. If no baseline → all NIST controls are "not in baseline" → warnings logged. Findings still created but no ControlEffectiveness records. Warning: "System has no control baseline — NIST control mapping skipped."

- [x] CHK029 — What happens with a CKL from a very old STIG version not in the curated library? [Edge Case]
  > **PASS**: All VULNs will be "unmatched." Import completes with `unmatchedCount = totalEntries`. `ImportStatus = CompletedWithWarnings`. Unmatched rules listed in summary for library expansion consideration.

- [x] CHK030 — Is the behavior defined for concurrent imports of the same system? [Concurrency]
  > **PASS**: Imports operate within an assessment context (AssessmentId). Multiple concurrent imports to the same system will create separate ScanImportRecords. Conflict detection uses per-finding StigId+AssessmentId locking. EF Core optimistic concurrency handles rare row-level conflicts.

## Cross-Cutting Concerns

- [x] CHK031 — Is audit logging sufficient for compliance requirements? [Audit]
  > **PASS**: `ScanImportRecord` logs who, when, what file (hash), what system, what results. `ScanImportFinding` logs per-rule action taken. Evidence includes file content hash for tamper detection. All tools log input/duration/outcome per constitution.

- [x] CHK032 — Is the data model backward-compatible with existing data? [Migration]
  > **PASS**: Only addition: nullable `ImportRecordId` on `ComplianceFinding`. Existing findings unaffected (null). New entities are additive. EF Core migration (T006) is non-destructive.

- [x] CHK033 — Are all new tools discoverable by the AI agent tool selection? [Integration]
  > **PASS**: 5 new tools registered in ComplianceMcpTools.cs. Agent's `SelectToolsForMessage` will match on keywords: "import", "CKL", "STIG", "SCAP", "XCCDF", "checklist", "scan results", "export CKL".

## Analysis-Driven Additions (2026-03-03)

- [x] CHK034 — Is duplicate file detection behavior defined? [Completeness, Spec §1.10]
  > **PASS**: Capability 1.10 added. Check FileHash + RegisteredSystemId on import. If match found: warn but proceed. Task T030 implements detection with warning message.

- [x] CHK035 — Is the base64 decoding boundary (tool vs service layer) clarified? [Clarity, Tasks T013/T030]
  > **PASS**: Tool layer decodes base64 → byte[]. Service layer receives raw bytes. Explicitly stated in T030 and T013 interface signature.

- [x] CHK036 — Is `ScanTimestamp` handling for CKL (which has no scan timestamp) clarified? [Clarity, Data Model]
  > **PASS**: Data model field description updated: CKL → null (no scan timestamp in format), XCCDF → `start-time` attribute.

- [x] CHK037 — Is date range filtering available for import history? [Completeness, Contracts]
  > **PASS**: `from_date` and `to_date` ISO 8601 parameters added to `compliance_list_imports` tool contract and `ListImportsAsync` interface.

- [x] CHK038 — Are structured logging requirements covered by tasks? [Constitution V, Tasks T061]
  > **PASS**: Task T061 adds ILogger-based structured logging to ScanImportService: import started/completed events, parse errors, unmatched rules, out-of-baseline controls.

- [x] CHK039 — Are performance benchmarks covered by tasks? [Constitution VIII, Tasks T062]
  > **PASS**: Task T062 adds performance tests: CKL 500 VULNs < 10s, XCCDF 500 rules < 5s, measured with Stopwatch and asserted.

- [x] CHK040 — Does `FindingSeverity.Informational` exist for XCCDF `informational` result mapping? [Consistency, ComplianceModels.cs]
  > **PASS**: Verified in source code — `FindingSeverity` enum includes `Informational` value with XML doc "No risk — informational observation only."
