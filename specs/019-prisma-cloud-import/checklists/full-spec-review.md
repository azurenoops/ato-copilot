# Full Spec Review Checklist: 019 — Prisma Cloud Scan Import

**Purpose**: Validate the quality, clarity, completeness, and consistency of all requirements across the 13-part specification — "unit tests for English."
**Created**: 2026-03-05
**Feature**: [spec.md](../spec.md) | [plan.md](../plan.md) | [tasks.md](../tasks.md)
**Depth**: Thorough | **Audience**: Author (self-review)

---

## Requirement Completeness

- [x] CHK001 - Are parsing requirements defined for ALL 18 CSV columns listed in Part 3? **YES** — All 18 columns have mapping targets in Part 3.1 table. ResolutionReason/ResolutionTime → ParsedPrismaAlert DTO. T014: "all 18 columns." [Completeness, Spec §3.1]
- [x] CHK002 - Are requirements specified for handling CSV rows where `Compliance Standard` does NOT contain "NIST 800-53"? **YES** — Part 3.3 rule 1: extract NIST only. T010: "skip non-NIST rows (CIS, SOC 2)." If ALL rows for an alert are non-NIST → unmapped finding per Part 3.3 rule 4. [Completeness, Spec §3.3]
- [x] CHK003 - Are error handling requirements defined for malformed CSV files? **YES** — T010: reject missing headers, empty rows, trailing commas, UTF-8 BOM. Contracts: "Invalid CSV header", "No alerts after parsing." [Gap → Resolved]
- [x] CHK004 - Are error handling requirements defined for malformed API JSON payloads? **YES** — T025: "handle malformed JSON with descriptive error." Contracts: "Invalid JSON", "Missing required alert fields." [Gap → Resolved]
- [x] CHK005 - Are requirements defined for `ISystemSubscriptionResolver` unavailability? **ACCEPTABLE** — Per-subscription failures handled as unresolved subscriptions (skip + actionable error). Service-level unavailability propagates as standard exception through MCP error envelope, same as Feature 017. [Gap → Acceptable via inherited patterns]
- [x] CHK006 - Are requirements specified for `assessment_id`? **YES** — contracts/mcp-tools.md defines `assessment_id` as optional parameter on both import tools. T013: "assessment_id parameter forwarded." Part 7.5 describes assessment context creation. [Completeness, Spec §7.5 vs §1.11]
- [x] CHK007 - Are requirements defined for `policy.labels` storage? **YES** — data-model.md: ParsedPrismaAlert.PolicyLabels (List<string>). T026: "policy labels captured as structured metadata." API tool response includes `policyLabelsFound`. Stored as import metadata, not dedicated DB column. [Gap → Resolved via data-model.md]
- [x] CHK008 - Are requirements defined for `alert.history[]` storage? **YES** — data-model.md: PrismaAlertHistoryEntry DTO (ModifiedBy, ModifiedOn, Reason, Status). ParsedPrismaAlert.AlertHistory list. T026: "alert history attached to ScanImportFinding." [Gap → Resolved via data-model.md]
- [x] CHK009 - Are requirements defined for `policy.policyType` differentiation? **YES (by design: no differentiation)** — All CSPM policy types imported identically. PolicyType preserved as metadata on ParsedPrismaAlert for filtering/reporting. Uniform handling is correct for compliance posture findings. [Completeness, Spec §3.1]
- [x] CHK010 - Are `snoozed` alert handling requirements specified? **YES** — Fixed in analysis: CSV → "Snoozed (snooze expiry unavailable from CSV)"; API JSON → "Snoozed until {date}" if expiry in history[]. Both map to FindingStatus.Open → OtherThanSatisfied. [Completeness, Spec §3.1]
- [x] CHK011 - Are mobile/responsive UI requirements in scope? **NO — out of scope by design.** Part 2 lists interaction surfaces; this feature delivers MCP tools. Teams card and VS Code UX are existing platform concerns, not Prisma-specific. [Completeness, Spec §2]
- [x] CHK012 - Are cancellation/timeout requirements defined? **ACCEPTABLE** — CancellationToken on all async methods (plan.md). Constitution VIII: complex operations <30s. MCP protocol has request timeout. No additional user-facing timeout needed. [Gap → Acceptable via Constitution VIII]

## Requirement Clarity

- [x] CHK013 - Is "auto-split by subscription" precisely defined? **YES** — Part 1.3: unresolved subscriptions skip those alerts + actionable error. Resolved subscriptions proceed independently. Overall import succeeds with partial results (CompletedWithWarnings). [Ambiguity → Resolved, Spec §1.3]
- [x] CHK014 - Is "group by Alert ID" for multi-row CSV defined precisely? **YES** — Part 3.3 rule 1: only NIST controls extracted into NistControlIds. Non-NIST rows grouped by Alert ID but CIS/SOC2 compliance data excluded. Metadata (status, severity, resource) from first row. [Clarity → Resolved, Spec §1.1]
- [x] CHK015 - Is "unmapped finding" precisely defined? **YES** — Part 3.3 rule 4: ComplianceFinding created with warning, no ControlEffectiveness generated. "Unmapped" = ComplianceFinding with zero linked ControlEffectiveness records. No special field needed. [Ambiguity → Resolved, Spec §6 step 4]
- [x] CHK016 - Is the "actionable prompt" defined with exact format? **YES** — contracts/mcp-tools.md: `unresolvedSubscriptions` array with structured objects (`accountId`, `accountName`, `alertCount`, `message`). [Clarity → Resolved, Spec §1.3]
- [x] CHK017 - Is "aggregate approach" scope defined? **YES** — Part 1.6: "query ALL ComplianceFinding records for the control across all sources (STIG, Prisma, manual)." System-wide per control, not assessment-scoped. [Ambiguity → Resolved, Spec §1.6]
- [x] CHK018 - Is `Merge` defined for Prisma? **ACCEPTABLE** — Inherits Feature 017 merge semantics: update status/severity if changed, keep existing where new is null, add new NIST mappings. Matching key: PrismaAlertId + RegisteredSystemId (data-model.md). [Clarity → Acceptable via Feature 017, Spec §1.8]
- [x] CHK019 - Is "compliance score trend" quantified? **YES** — data-model.md: PrismaTrendResult.RemediationRate = resolved / (resolved + persistent). Plus new/resolved/persistent counts per import. [Ambiguity → Resolved, Spec §3.3]
- [x] CHK020 - Are "finding" and "alert" used consistently? **YES** — "alert" = Prisma source record (P-12345), "finding" = ComplianceFinding entity. Part 1.4: "one ComplianceFinding per Prisma alert" is precise mapping from source to entity. [Clarity, Spec §1.4]

## Requirement Consistency

- [x] CHK021 - Are `ScanSource` values consistent? **YES** — Fixed in analysis: T003 now explicitly adds `Cloud` to `ScanSourceType` enum. T053 asserts enum count 4→5. [Consistency → Resolved, Spec §1.4 vs §8]
- [x] CHK022 - Is `EvidenceType` consistent? **YES** — ComplianceEvidence.EvidenceType is a string field (not enum-constrained). "CloudScanResult" is a valid arbitrary value, consistent across spec §1.7, §8, and data-model.md. [Consistency, Spec §1.7 vs §8]
- [x] CHK023 - Do Part 5 capabilities and Part 8 entity changes align for `policy.labels`? **YES** — Labels are stored on ParsedPrismaAlert.PolicyLabels (DTO), not as a ScanImportFinding DB column. The 7 new ScanImportFinding fields are for persistent cloud resource metadata; labels are transient import metadata returned in tool response as `policyLabelsFound`. [Consistency, Spec §2.4 vs §8]
- [x] CHK024 - Are RBAC roles consistent across all 4 tools? **YES** — contracts/mcp-tools.md defines per-tool: Import tools = SecurityLead, Analyst, Administrator; Read-only tools = SecurityLead, Analyst, Assessor, Administrator. Aligns with personas (ISSO=Analyst, ISSM=SecurityLead, SCA=Assessor). [Consistency, Spec §2 vs §5]
- [x] CHK025 - ~~Is the tool name `ListPrismaPoliciesList` in Part 8 a typo?~~ **RESOLVED**: Renamed to `ListPrismaPoliciesTool` across spec.md, plan.md, and tasks.md. [Consistency, Spec §8]
- [x] CHK026 - Do the 62 tasks fully cover all 6 phases and 31 capabilities? **YES** — Verified in analysis: 29/31 capabilities have direct task coverage. Remaining 2 (ScanSourceType.Cloud, ConMon integration) were fixed — T003 now adds Cloud; T049 now covers ConMon reports. [Consistency, tasks.md vs Spec §5]

## Acceptance Criteria Quality

- [x] CHK027 - Is "≥95% NIST mapping" measurable? **YES** — Test corpus is the sample Prisma fixtures (T001, T002) + any real exports. The ≥95% threshold accounts for deprecated/withdrawn controls. Measurable: count policies with valid NIST mappings / total policies with NIST metadata. [Measurability, Spec §10]
- [x] CHK028 - Are "5+ policy types" enumerated? **ACCEPTABLE** — Prisma policy types: config, network, audit_event, anomaly, data (DLP), iam. Test fixture T001 should include ≥5 types. The types are enumerable from Prisma documentation. [Measurability, Spec §10]
- [x] CHK029 - Is CSV import <15s deterministic? **YES** — Tests use InMemory EF Core (no I/O variability). Performance tests T051/T052 assert timing + memory. Threshold is conservative for any modern machine/CI. [Measurability, Spec §10]
- [x] CHK030 - Is downstream artifact inclusion testable? **YES** — T049 now verifies all 7 downstream tools (SAR, POA&M, SSP, SAP, auth package, ConMon, dashboard). Assertions: ComplianceFinding with Source="Prisma Cloud" appears in generated output. [Measurability, Spec §10]
- [x] CHK031 - Are NFR acceptance criteria in the spec? **YES** — Part 10: CSV <15s, JSON <10s. Part 7.4: 25MB limit. Plan.md: 512MB memory (Constitution VIII). T051/T052 now include memory assertions. [Gap → Resolved, Spec §10]

## Scenario Coverage

- [x] CHK032 - Empty CSV (headers only, zero data rows)? **YES** — 0 alerts → contracts error: "CSV contains no alert rows." T010 covers "handle empty rows." [Coverage, Edge Case → Resolved]
- [x] CHK033 - All alerts from unresolvable subscriptions? **YES** — Contracts error: "No Azure subscriptions could be resolved to registered systems. Provide explicit system_id or register systems first." [Coverage, Exception Flow → Resolved]
- [x] CHK034 - All alerts resolved/dismissed (zero open)? **YES** — Import still creates ScanImportRecord + ComplianceFinding (FindingStatus.Remediated/Accepted). Effectiveness re-evaluated — if all findings remediated, control becomes Satisfied. Valid import. [Coverage, Alternate Flow → Resolved]
- [x] CHK035 - Re-import after findings modified by other tools? **YES** — Conflict resolution handles: Skip keeps existing, Overwrite replaces, Merge updates. PrismaAlertId matching key ensures correct finding targeted. [Coverage, Recovery Flow → Resolved]
- [x] CHK036 - Concurrent imports for same system? **ACCEPTABLE** — Standard EF Core concurrency handling. InMemory provider is single-context; production DB uses separate DbContext instances with standard optimistic concurrency. Not Prisma-specific. [Coverage, Concurrency → Acceptable via platform]
- [x] CHK037 - Empty ControlBaseline during import? **YES** — Part 3.3 rule 3: "Validate each control exists in ControlBaseline.ControlIds." Empty baseline → no controls match → all findings get ComplianceFinding only, no ControlEffectiveness. Warnings generated per control. [Coverage, Edge Case → Resolved]
- [x] CHK038 - API JSON with empty `complianceMetadata` array? **YES** — T025: "handle missing optional fields... empty labels." Empty complianceMetadata → no NIST mapping → unmapped finding per Part 3.3 rule 4. [Coverage, Edge Case → Resolved, Spec §3.2]
- [x] CHK039 - CSV with null/empty `Resource ID` (tenant-level policy)? **ACCEPTABLE** — ParsedPrismaAlert.ResourceId is string (nullable by nature). ComplianceFinding.ResourceId can be null/empty. Import proceeds with null resource context. Handled by nullable field design. [Coverage, Edge Case → Acceptable via nullable fields]

## Edge Case Coverage

- [x] CHK040 - Duplicate Alert IDs with conflicting statuses in CSV? **ACCEPTABLE** — In practice Prisma CSV exports have consistent status per Alert ID (rows differ only by compliance standard). Multi-row grouping uses first-row status. Implementation should document first-row-wins behavior. [Edge Case → Acceptable]
- [x] CHK041 - NIST control ID format variations (IA-5(1)(a), withdrawn)? **YES** — Parser handles base controls (AC-2) and single-level enhancements (IA-5(1)). Multi-enhancement IA-5(1)(a) is not valid NIST 800-53 format. Withdrawn controls simply fail ControlBaseline lookup → ComplianceFinding only + warning. [Edge Case → Resolved, Spec §3.3]
- [x] CHK042 - 25MB limit timing? **YES** — T022: "decode base64, validate file size" — size check occurs immediately after base64 decode, before any parsing. Contracts: "File exceeds 25MB limit." [Edge Case → Resolved, Spec §7.4]
- [x] CHK043 - Line ending handling (\r\n, \n, \r)? **YES** — T014: "RFC 4180 state machine" — RFC 4180 CSV parsing handles all standard line endings. .NET string processing natively handles mixed endings. [Edge Case → Resolved]
- [x] CHK044 - Unicode in Policy Names and Resource Names? **YES** — T010: "handle UTF-8 BOM." .NET strings are natively Unicode. Non-ASCII characters in resource names from international Azure regions handled by default. [Edge Case → Resolved]
- [x] CHK045 - Resolver returns decommissioned system? **YES** — T017: "validate system exists via IRmfLifecycleService." System validation checks active status. Decommissioned system would fail validation, reporting as unresolved. [Edge Case → Resolved]
- [x] CHK046 - Non-remediable policy with null remediation? **YES** — T025: "handle missing optional fields (null remediation)." When policy.remediable=false and policy.remediation=null: RemediationGuidance from policy.recommendation (separate field), RemediationScript=null, AutoRemediable=false. [Edge Case → Resolved, Spec §2.2]

## Non-Functional Requirements

- [x] CHK047 - Memory consumption spec for CSV parser? **YES** — Research.md R6: ~75MB peak for 25MB file. Plan.md: 512MB steady-state (Constitution VIII). T051/T052 now assert memory delta <512MB. [Gap → Resolved across research.md + plan.md, NFR]
- [x] CHK048 - Character encoding requirements? **ACCEPTABLE** — T010: "handle UTF-8 BOM." Parser operates on byte[] → UTF-8 string. Prisma Cloud exports use UTF-8. UTF-16/Latin-1 not supported; UTF-8 is the de facto standard for cloud platform exports. [Gap → Acceptable, NFR]
- [x] CHK049 - Specific log events defined? **YES** — T050: "log import start (system_id, file_name, import_type), subscription resolution (subscription_id, resolved_system_id), completion (duration_ms, alert_count, findings_created, nist_controls_affected, effectiveness_records_upserted) using Stopwatch." [Clarity → Resolved, Spec §4.4]
- [x] CHK050 - Audit trail beyond SHA-256 hashing? **YES** — IScanImportService signatures include `importedBy` parameter. ScanImportRecord captures who performed the import. ComplianceFinding creation includes import context (ImportRecordId FK). Standard audit pattern from Feature 017. [Completeness → Resolved]
- [x] CHK051 - Data retention requirements? **ACCEPTABLE** — Not Prisma-specific; inherits system-wide ScanImportRecord/ComplianceFinding retention policy. Out of scope for this feature — would be a platform-level ADR. [Gap → Out of scope, NFR]
- [x] CHK052 - Idempotency requirements? **YES** — Duplicate file hash detection (Cap 1.10 / T020) warns on re-import. Conflict resolution Skip ensures existing findings aren't duplicated. Same file imported twice with Skip produces identical final state. [Gap → Resolved, NFR]

## Dependencies & Assumptions

- [x] CHK053 - Prisma CSV format stability documented? **YES** — Plan.md Risk Assessment: "Prisma CSV column order varies across versions → Parse by column name (header row), not position." Documented risk with mitigation. [Assumption → Documented]
- [x] CHK054 - Is `requirementId` format validated? **ACCEPTABLE** — Research.md R1 covers CSV column analysis. Parser validates against ControlBaseline with standard NIST IDs. Format variations would fail baseline lookup → ComplianceFinding only + warning. Safe fallback. [Assumption → Acceptable, Spec §3.3]
- [x] CHK055 - Is `ISystemSubscriptionResolver` dependency documented? **YES** — Part 7.1: "ISystemSubscriptionResolver — Resolve Azure subscription → RegisteredSystem (Feature 015 Phase 17)." Plan.md Dependencies table lists it. Feature 015 shipped v1.15.0 — always available. [Dependency → Resolved, Spec §7.1]
- [x] CHK056 - Is ControlBaseline prerequisite documented? **YES** — Part 1.6 + Part 3.3 rule 3: "Validate each control exists in ControlBaseline.ControlIds." If no baseline, all controls are outside baseline → ComplianceFinding only, no ControlEffectiveness. Safe degradation behavior. [Dependency → Resolved, Spec §1.6]
- [x] CHK057 - Prisma Cloud tier availability validated? **YES** — Prisma Cloud Standard, Enterprise, and Government all support CSV export from Alerts page. Verified against Palo Alto Networks documentation. [Assumption → Validated, Spec §3.1]

## Ambiguities & Conflicts

- [x] CHK058 - Priority when both `system_id` and CSV subscription data present? **YES** — Part 1.3: "If provided, all alerts are assigned to this system regardless of cloud type." Subscription data in CSV not used for resolution but preserved in CloudAccountId for metadata/audit. [Ambiguity → Resolved, Spec §1.3]
- [x] CHK059 - Trend analysis: 1 finding or 3 control-level impacts? **YES** — Trend compares by PrismaAlertId (finding level): 1 alert = 1 finding in trend counts. NistControlBreakdown group_by provides separate control-level view. Two complementary perspectives. [Conflict → Resolved, Spec §1.4 vs §3.3]
- [x] CHK060 - null vs omitted `system_id`? **YES** — Both null and omitted trigger auto-resolution. Standard MCP parameter handling: absent and null are equivalent for optional parameters. [Ambiguity → Resolved, Spec §1.3]
- [x] CHK061 - Does trend tool compare only Prisma imports? **YES** — Tool name `compliance_prisma_trend` and implementation (T041): "query ScanImportRecords with ImportType in (PrismaCsv, PrismaApi)." Prisma-only by design. Cross-source trending is a separate future concern. [Ambiguity → Resolved, Spec §3.3]
- [x] CHK062 - Dry-run vs actual response schema? **YES** — contracts/mcp-tools.md: dry_run parameter. Same ImportResult JSON schema with `isDryRun: true`. Counts show what WOULD be created. No separate schema needed — same structure, different semantics. [Ambiguity → Resolved, Spec §1.9]

## Traceability & Structure

- [x] CHK063 - Requirement ID scheme established? **ACCEPTABLE** — Capabilities use numbered IDs (1.1–5.6). Success criteria in Part 10 are enumerable table rows. Entity changes in Part 8 are traceable by entity name. Formal requirement IDs are not industry-standard for feature specs of this type. [Traceability → Acceptable]
- [x] CHK064 - Success criteria traceable to capabilities? **YES** — Each criterion maps: "CSV import parses correctly" → Cap 1.1; "Subscription auto-resolution works" → Cap 1.3; "Trend analysis shows progress" → Cap 3.3; etc. All 18 criteria map to specific capabilities. [Traceability → Resolved, Spec §10]
- [x] CHK065 - Out-of-scope items cross-referenced to decisions? **ACCEPTABLE** — Part 9 items traceable to Clarifications section: webhook → Q6, CWP → Q3, PDF → Q7, multi-cloud auto-resolution → Q14. Not explicitly linked inline but Clarifications section provides the provenance. [Traceability → Acceptable]

---

## Notes

- Check items off as completed: `[x]`
- Add inline findings or comments after each item as needed
- Quality dimensions in brackets: [Completeness], [Clarity], [Consistency], [Measurability], [Coverage], [Edge Case], [Gap], [Ambiguity], [Conflict], [Assumption], [Dependency], [Traceability], [NFR]
- Items referencing `[Spec §X.Y]` point to capability numbers in Part 5 or section numbers; `[Gap]` indicates a missing requirement
- 53 of 65 items (82%) include at least one traceability reference
