# ISSO Persona Test Execution Script

**Feature**: 020 | **Persona**: ISSO (Information System Security Officer)
**Role**: `Compliance.Analyst` | **Interface**: VS Code `@ato`
**Test Cases**: ISSO-01 through ISSO-35 (35 total)

---

## Pre-Execution Setup

### T017 — Role Activation & Interface Switch

1. **Deactivate ISSM role** (if active): `@ato Deactivate my SecurityLead role`
2. **Activate ISSO role**: `@ato Activate my Compliance.Analyst role for 4 hours — persona test suite`
3. **Verify role**: `@ato Show my active PIM roles` → Confirm `Compliance.Analyst` is active
4. **Switch to VS Code**: Open VS Code with the `@ato` chat participant
5. **Verify connection**: `@ato Show system details for Eagle Eye` → Should return the system created by ISSM

### Preconditions from ISSM Phase

- ✓ Eagle Eye system exists (ISSM-01)
- ✓ System is in Implement phase (ISSM-16)
- ✓ Moderate baseline selected with 325 controls (ISSM-11)
- ✓ AC-1 through AC-4 set as inherited (ISSM-13)
- ✓ Prisma scans imported (ISSM-19, ISSM-20)
- ✓ Nessus/ACAS test data available (`test-data/acas-scan-results.nessus`)
- ✓ PTA created and PIA approved (ISSM-44, ISSM-46)
- ✓ Interconnections registered with ISA (ISSM-48, ISSM-52)

---

## Phase 3 — Implement / SSP Authoring (ISSO-01 to ISSO-12)

### ISSO-01: Auto-Populate Inherited Narratives

**Task**: Batch-fill narratives for inherited controls
**Type**: Positive test | **Precondition**: System in Implement phase with inheritance set

```text
@ato Auto-populate the inherited control narratives for Eagle Eye
```

**Expected Tool**: `compliance_batch_populate_narratives`
**Expected Output**:
- Narratives auto-filled for all inherited controls
- Count of populated vs. skipped
- Idempotent on re-run (re-running produces same result)

**Verification**: Populated count matches inherited control count
**Record**: populated = ___, skipped = ___

---

### ISSO-02: Check Narrative Progress

**Task**: View overall SSP completion
**Type**: Positive test | **Precondition**: ISSO-01

```text
@ato Show narrative progress for Eagle Eye
```

**Expected Tool**: `compliance_narrative_progress`
**Expected Output**:
- Overall completion %
- Per-family breakdown (total, completed, draft, missing)

**Verification**: % increased after ISSO-01 auto-populate
**Record**: overall_pct = ___%

---

### ISSO-03: Get AI Narrative Suggestion

**Task**: Get AI-generated narrative suggestion
**Type**: Positive test | **Precondition**: System with baseline

```text
@ato Suggest a narrative for AC-2 on Eagle Eye
```

**Expected Tool**: `compliance_suggest_narrative`
**Expected Output**:
- Suggested text for AC-2
- Confidence score (expect ~0.55 for customer-responsible control)
- Reference sources

**Verification**: Suggestion text is relevant to AC-2 (Account Management)
**Record**: confidence = ___

---

### ISSO-04: Write a Control Narrative

**Task**: Author a control narrative
**Type**: Positive test | **Precondition**: System in Implement phase

```text
@ato Write narrative for AC-2 on Eagle Eye: Account management is
implemented using Azure Active Directory with automated provisioning
via SCIM, quarterly access reviews, and 15-minute session timeouts
```

**Expected Tool**: `compliance_write_narrative`
**Expected Output**:
- Narrative saved
- Status = "Implemented"
- Upsert behavior on re-call (updates existing)

**Verification**: Status = "Implemented"

---

### ISSO-05: Update Narrative to Partial

**Task**: Update a narrative to partially implemented
**Type**: Positive test | **Precondition**: ISSO-04 pattern

```text
@ato Update AC-3 narrative on Eagle Eye to PartiallyImplemented: Access
enforcement is configured via Azure RBAC, ABAC policies pending
deployment
```

**Expected Tool**: `compliance_write_narrative`
**Expected Output**:
- Status updated to "PartiallyImplemented"
- Narrative text updated

**Verification**: Status = "PartiallyImplemented"

---

### ISSO-06: Filter Progress by Family

**Task**: View progress for specific control family
**Type**: Positive test | **Precondition**: ISSO-01

```text
@ato Show narrative progress for the AC family on Eagle Eye
```

**Expected Tool**: `compliance_narrative_progress`
**Expected Output**:
- AC family stats only
- Total, completed (Implemented + N/A), draft (Partial + Planned), missing

**Verification**: Response is filtered to AC family only

---

### ISSO-07: Generate Full SSP

**Task**: Generate complete System Security Plan
**Type**: Positive test | **Precondition**: Narratives substantially complete

```text
@ato Generate the SSP for Eagle Eye
```

**Expected Tool**: `compliance_generate_ssp`
**Expected Output**:
- Markdown SSP document with 4 sections:
  1. System Information
  2. Categorization
  3. Baseline
  4. Control Implementations
- Warnings array for missing narratives

**Verification**: 4 sections present, warnings list any gaps

---

### ISSO-08: Generate SSP Section Only

**Task**: Generate just one SSP section
**Type**: Positive test | **Precondition**: System registered

```text
@ato Generate just the system information section of Eagle Eye's SSP
```

**Expected Tool**: `compliance_generate_ssp`
**Expected Output**:
- Only the System Information section rendered
- Contains system name, type, environment, boundary info

**Verification**: Only one section returned

---

### ISSO-09: Import CKL Checklist

**Task**: Import DISA STIG Viewer checklist
**Type**: Positive test | **Precondition**: System with baseline

```text
@ato Import this CKL file for Eagle Eye
```

**Attachment**: `test-data/windows-2022-stig.ckl`

**Expected Tool**: `compliance_import_ckl`
**Expected Output**:
- Import record created
- Findings mapped to NIST controls
- Status counts: Created, Updated, Skipped, Unmatched

**Verification**: Created + Updated > 0
**Record**: created = ___, updated = ___, skipped = ___, unmatched = ___

---

### ISSO-10: Import XCCDF Results

**Task**: Import SCAP scan results
**Type**: Positive test | **Precondition**: System with baseline

```text
@ato Import SCAP scan results for Eagle Eye
```

**Attachment**: `test-data/scap-scan-results.xml`

**Expected Tool**: `compliance_import_xccdf`
**Expected Output**:
- Import record created
- XCCDF benchmark scores
- Rule results mapped to NIST controls

**Verification**: Import record created with benchmark reference
**Record**: benchmark = ___, score = ___

---

### ISSO-11: View Import History

**Task**: List all imports for the system
**Type**: Positive test | **Precondition**: ISSO-09 or ISSO-10

```text
@ato Show import history for Eagle Eye
```

**Expected Tool**: `compliance_list_imports`
**Expected Output**:
- Paginated list
- Per import: type, date, benchmark, finding counts, status

**Verification**: At least 2 imports visible (CKL + XCCDF, plus Prisma from ISSM)
**Record**: import_count = ___

---

### ISSO-12: View Import Details

**Task**: View specific import details
**Type**: Positive test | **Precondition**: ISSO-09 or ISSO-10

```text
@ato Show details of import {import_id}
```

**Note**: Replace `{import_id}` with an actual import ID from ISSO-11.

**Expected Tool**: `compliance_get_import_summary`
**Expected Output**:
- Per-finding breakdown
- Actions taken (Created/Updated/Skipped)
- NIST control mappings
- Conflict resolutions

**Verification**: Findings listed with control mappings

---

### ISSO-12a: Import ACAS/Nessus Scan

**Task**: Import Tenable Nessus/ACAS vulnerability scan
**Type**: Positive test | **Precondition**: System with baseline

```text
@ato Import this ACAS scan for Eagle Eye
```

**Attachment**: `test-data/acas-scan-results.nessus`

**Expected Tool**: `compliance_import_nessus`
**Expected Output**:
- Import record created with type `NessusXml`
- Plugin families mapped to NIST 800-53 controls
- Severity breakdown: Critical, High, Medium, Low, Informational
- POA&M weakness entries auto-created for Cat I/II/III findings
- Heuristic mapping warnings (if any families used fallback)

**Verification**: Import record created, findings_created > 0, poam_weaknesses_created > 0
**Record**: hosts = ___, plugins = ___, created = ___, poam = ___, heuristic_warnings = ___

---

### ISSO-12b: Import ACAS/Nessus Dry Run

**Task**: Preview Nessus import without persisting
**Type**: Positive test | **Precondition**: System with baseline

```text
@ato Do a dry run import of this ACAS scan for Eagle Eye
```

**Attachment**: `test-data/acas-scan-results.nessus`

**Expected Tool**: `compliance_import_nessus` (with `dry_run: true`)
**Expected Output**:
- Preview summary with host/plugin counts and severity breakdown
- No import record persisted
- No findings or POA&M entries created in database

**Verification**: `dry_run: true` in response, no new records in import history

---

### ISSO-12c: List Nessus Import History

**Task**: View Nessus-specific import history
**Type**: Positive test | **Precondition**: ISSO-12a completed

```text
@ato Show Nessus import history for Eagle Eye
```

**Expected Tool**: `compliance_list_nessus_imports`
**Expected Output**:
- Filtered list showing only NessusXml imports
- Per import: file name, date, total findings, findings created

**Verification**: At least 1 Nessus import visible from ISSO-12a
**Record**: nessus_import_count = ___

---

## CKL Export & Evidence (ISSO-25)

### ISSO-25: Export CKL Checklist

**Task**: Export findings as CKL file for DISA STIG Viewer
**Type**: Positive test | **Precondition**: ISSO-09 (CKL imported with STIG findings)

```text
@ato Export a CKL checklist for Eagle Eye's Windows Server 2022 STIG
```

**Expected Tool**: `compliance_export_ckl`
**Expected Output**:
- CKL XML file generated
- Includes STIG evaluation results from imported data
- Compatible with DISA STIG Viewer and eMASS

**Verification**: CKL file generated, benchmark reference matches import

---

## Privacy Analysis (ISSO-26 to ISSO-28)

### ISSO-26: Create PTA for System

**Task**: Conduct a Privacy Threshold Analysis
**Type**: Positive test | **Precondition**: System registered (ISSM-01), or PTA not yet created by ISSM

```text
@ato Create a Privacy Threshold Analysis for Eagle Eye — the system
processes Name and Email of system operators. PII is collected directly.
No sharing with external parties. Retention period is 3 years.
```

**Expected Tool**: `compliance_create_pta`
**Expected Output**:
- PTA created with PII categories (Name, Email)
- Collection method = Direct
- PIA required determination

**Verification**: PTA created with 2 PII categories

---

### ISSO-27: Generate PIA

**Task**: Generate a Privacy Impact Assessment from PTA
**Type**: Positive test | **Precondition**: ISSO-26 or ISSM-44

```text
@ato Generate a Privacy Impact Assessment for Eagle Eye
```

**Expected Tool**: `compliance_generate_pia`
**Expected Output**:
- PIA with 9 sections
- Status = Draft
- Content derives from PTA data

**Verification**: Status = "Draft", 9 sections present

---

### ISSO-28: Check Privacy Compliance

**Task**: View privacy compliance dashboard
**Type**: Positive test | **Precondition**: ISSO-26

```text
@ato Check privacy compliance status for Eagle Eye
```

**Expected Tool**: `compliance_check_privacy_compliance`
**Expected Output**:
- PTA status, PIA status
- Interconnection agreement health
- Overall privacy gate status

**Verification**: Privacy status returned with gate assessment

---

## Interconnection Registration (ISSO-29 to ISSO-30)

### ISSO-29: Add Interconnection

**Task**: Register system interconnection
**Type**: Positive test | **Precondition**: System registered (ISSM-01)

```text
@ato Add an interconnection for Eagle Eye — outbound data flow to
DISA Enterprise Email (DEE) for automated alert notifications via SMTP
```

**Expected Tool**: `compliance_add_interconnection`
**Expected Output**:
- Interconnection created
- Direction = Outbound
- Status = Proposed

**Verification**: Direction = "Outbound", status = "Proposed"
**Record**: isso_interconnection_id = _______________

---

### ISSO-30: List Interconnections

**Task**: View all interconnections registered by any persona
**Type**: Positive test | **Precondition**: ISSO-29 or ISSM-48

```text
@ato Show all interconnections for Eagle Eye
```

**Expected Tool**: `compliance_list_interconnections`
**Expected Output**:
- At least 1 interconnection (ISSO-29)
- May include ISSM-registered interconnections

**Verification**: List returned with at least 1 entry

---

## SSP Section Authoring (ISSO-31 to ISSO-35)

### ISSO-31: Write SSP Section 5 — General Description

**Task**: Author SSP section with free-form content
**Type**: Positive test | **Precondition**: System registered

```text
@ato Write SSP Section 5 for Eagle Eye: Eagle Eye is a mission planning
and operational intelligence platform providing joint force coordination
capabilities. The system aggregates multi-source intelligence data and
presents unified operational dashboards to authorized commanders and staff.
```

**Expected Tool**: `compliance_write_ssp_section`
**Expected Output**:
- Section 5 created with content
- Status = Draft
- Version = 1

**Verification**: Status = "Draft", section_number = 5, version = 1
**Record**: section_5_version = ___

---

### ISSO-32: Write SSP Section 6 — System Environment

**Task**: Author hybrid SSP section
**Type**: Positive test | **Precondition**: System registered

```text
@ato Write SSP Section 6 for Eagle Eye: The system operates in Azure
Government (USGov Virginia) with a secondary disaster recovery site in
USGov Texas. The environment includes 2 web servers, 1 application server,
1 SQL database, and 1 Key Vault instance.
```

**Expected Tool**: `compliance_write_ssp_section`
**Expected Output**:
- Section 6 created (hybrid: auto-populated environment + authored narrative)
- Status = Draft

**Verification**: Status = "Draft", section_number = 6

---

### ISSO-33: Submit Section for Review

**Task**: Submit an authored section to ISSM for review
**Type**: Positive test | **Precondition**: ISSO-31

```text
@ato Submit SSP Section 5 for Eagle Eye for review
```

**Expected Tool**: `compliance_write_ssp_section` (with `submit_for_review=true`)
**Expected Output**:
- Section 5 status → UnderReview
- ISSM notified for review

**Verification**: Status = "UnderReview"

---

### ISSO-34: Check SSP Completeness

**Task**: View overall SSP section status
**Type**: Positive test | **Precondition**: At least 1 section authored

```text
@ato Check SSP completeness for Eagle Eye
```

**Expected Tool**: `compliance_ssp_completeness`
**Expected Output**:
- 13-section breakdown with status per section
- Completion percentage
- Blocking issues (if any)

**Verification**: At least sections 5, 6 show Draft or UnderReview
**Record**: isso_ssp_completion_pct = ___%

---

### ISSO-35: Update SSP Section After Revision

**Task**: Update a section that was returned for revision by ISSM
**Type**: Positive test | **Precondition**: ISSM-60 (section returned for revision)

```text
@ato Update SSP Section 12 for Eagle Eye: Personnel security is managed
via Azure AD PIM with quarterly access reviews, annual security awareness
training (DD Form 2875), and documented separation procedures including
CAC revocation within 24 hours of departure.
```

**Expected Tool**: `compliance_write_ssp_section`
**Expected Output**:
- Section 12 updated with new content
- Version incremented
- Status = Draft (ready for re-review)

**Verification**: Version > 1, status = "Draft"

→ **Handoff**: SSP sections authored and submitted. ISSM reviews (ISSM-56+). System ready for SCA assessment.

---

## Phase 6 — Monitor / Day-to-Day (ISSO-13 to ISSO-24)

### ISSO-13: Enable Monitoring

**Task**: Enable continuous monitoring for subscription
**Type**: Positive test | **Precondition**: Subscription exists

```text
@ato Enable daily monitoring for subscription sub-12345-abcde
```

**Expected Tool**: `watch_enable_monitoring`
**Expected Output**:
- Monitoring config created
- Scan frequency = Daily
- Next scan scheduled

**Verification**: Status = Enabled, next scan time set
**Record**: next_scan = _______________

---

### ISSO-14: View Monitoring Status

**Task**: Check monitoring configuration
**Type**: Positive test | **Precondition**: ISSO-13

```text
@ato Show monitoring status for Eagle Eye
```

**Expected Tool**: `watch_monitoring_status`
**Expected Output**:
- Status: Enabled
- Frequency
- Last scan time
- Next scan time
- Alert count

**Verification**: Status = Enabled

---

### ISSO-15: Show All Alerts

**Task**: List unacknowledged alerts
**Type**: Positive test | **Precondition**: Monitoring active + drift detected

```text
@ato Show all unacknowledged alerts for Eagle Eye
```

**Expected Tool**: `watch_show_alerts`
**Expected Output**:
- Alert list
- Per alert: severity, control, resource, timestamp
- Filtered to unacknowledged only

**Verification**: Results filtered to unacknowledged
**Record**: alert_count = ___

---

### ISSO-16: Get Alert Details

**Task**: View single alert details
**Type**: Positive test | **Precondition**: ISSO-15

```text
@ato Show details of alert ALT-{id}
```

**Note**: Replace `{id}` with an actual alert ID from ISSO-15.

**Expected Tool**: `watch_get_alert`
**Expected Output**:
- Full alert: severity, control ID, resource
- Current vs. expected state
- Remediation suggestion

**Verification**: Remediation suggestion present

---

### ISSO-17: Acknowledge Alert

**Task**: Acknowledge with justification
**Type**: Positive test | **Precondition**: ISSO-15

```text
@ato Acknowledge alert ALT-{id} — scheduled for next maintenance window
```

**Expected Tool**: `watch_acknowledge_alert`
**Expected Output**:
- Alert status → Acknowledged
- Comment saved
- SLA clock noted

**Verification**: Status = Acknowledged

---

### ISSO-18: Fix an Alert

**Task**: Auto-remediate an alert
**Type**: Positive test | **Precondition**: ISSO-15

```text
@ato Fix alert ALT-{id}
```

**Expected Tool**: `watch_fix_alert`
**Expected Output**:
- Remediation executed
- Finding status updated
- Validation result returned

**Verification**: Alert resolved or remediation attempted

---

### ISSO-19: Collect Evidence

**Task**: Collect compliance evidence
**Type**: Positive test | **Precondition**: System with baseline

```text
@ato Collect evidence for AC-2 on Eagle Eye
```

**Expected Tool**: `compliance_collect_evidence`
**Expected Output**:
- Evidence record created
- SHA-256 hash of evidence
- Azure resource data captured

**Verification**: SHA-256 hash present
**Record**: evidence_hash = _______________

---

### ISSO-20: Generate ConMon Report

**Task**: Generate monthly ConMon report
**Type**: Positive test | **Precondition**: ConMon plan exists (ISSM-32)

```text
@ato Generate the February 2026 ConMon report for Eagle Eye
```

**Expected Tool**: `compliance_generate_conmon_report`
**Expected Output**:
- Monthly report
- Compliance score, delta
- Finding trends
- POA&M status

**Verification**: Report contains compliance score

---

### ISSO-21: Report Significant Change

**Task**: Report infrastructure change
**Type**: Positive test | **Precondition**: ATO granted

```text
@ato Report that Eagle Eye added a new API Management gateway
```

**Expected Tool**: `compliance_report_significant_change`
**Expected Output**:
- Change recorded with type classification
- `requires_reauthorization` flag set based on type

**Verification**: Change recorded successfully

---

### ISSO-22: Assign Remediation Task

**Task**: Assign task to engineer
**Type**: Positive test | **Precondition**: Kanban board exists (ISSM-26)

```text
@ato Assign task REM-{id} to SSgt Rodriguez
```

**Note**: Replace `{id}` with an actual task ID from the Kanban board.

**Expected Tool**: `kanban_assign_task`
**Expected Output**:
- Task assigned to SSgt Rodriguez
- Engineer notified
- Task status remains in current column

**Verification**: Assignment confirmed

---

### ISSO-23: View Alert History

**Task**: View alert trends
**Type**: Positive test | **Precondition**: Monitoring active

```text
@ato Show alert trends for Eagle Eye over the last 30 days
```

**Expected Tool**: `watch_alert_history`
**Expected Output**:
- Alert query results with timeline view

**Verification**: Timeline data returned

---

### ISSO-24: View Compliance Trend

**Task**: View compliance score over time
**Type**: Positive test | **Precondition**: Monitoring active

```text
@ato Show compliance score trend for Eagle Eye
```

**Expected Tool**: `watch_compliance_trend`
**Expected Output**:
- Score progression over time
- Data points per scan

**Verification**: Trend data with multiple data points

---

## ISSO Results Summary

| Metric | Value |
|--------|-------|
| Total Test Cases | 35 |
| Passed | ___ |
| Failed | ___ |
| Blocked | ___ |
| Skipped | ___ |
| Avg Response Time | ___s |
| Max Response Time | ___s |

### Issues Found

| # | TC-ID | Severity | Description | Root Cause |
|---|-------|----------|-------------|------------|
| | | | | |

### Key Artifacts Created

| Artifact | ID / Value | Test Case |
|----------|-----------|-----------|
| Populated Narratives | ___ count | ISSO-01 |
| SSP Completion | ___% | ISSO-02 |
| Evidence Hash | _______________ | ISSO-19 |
| Import Count | ___ | ISSO-11 |
| Interconnection ID | _______________ | ISSO-29 |
| SSP Section 5 Version | ___ | ISSO-31 |
| SSP Completion | ___% | ISSO-34 |

**Checkpoint**: ⬜ ISSO (35 tests) complete. SSP sections authored, scans imported, CKL exported, privacy analyzed, interconnections registered, monitoring active. SCA testing can begin.

---

## HW/SW Inventory Management (ISSO-INV-01 to ISSO-INV-07)

### ISSO-INV-01: Auto-Seed from Boundary

**Task**: Create initial inventory from boundary resources
**Precondition**: Authorization boundary defined (ISSM phase)

```text
@ato Auto-seed the hardware inventory for Eagle Eye from the authorization boundary
```

**Expected Tool**: `inventory_auto_seed`
**Expected Output**: List of created hardware items mapped from boundary resources
**Verification**: `created_count` > 0; re-running returns `created_count` = 0 (idempotent)
**Record**: created = ___

### ISSO-INV-02: Add Hardware Item

**Task**: Register a hardware item not in the boundary

```text
@ato Add hardware item "web-server-01" to Eagle Eye — it's a Dell Server at 10.0.0.1
```

**Expected Tool**: `inventory_add_item`
**Expected Output**: Created item with `id`, `type` = "Hardware", `status` = "Active"
**Record**: item_id = ___

### ISSO-INV-03: Add Software on Hardware

**Task**: Register software installed on the hardware item

```text
@ato Add software "RHEL 9.2" by Red Hat on web-server-01 in Eagle Eye — it's an OperatingSystem
```

**Expected Tool**: `inventory_add_item` with `parent_hardware_id`
**Expected Output**: Created software item linked to parent hardware
**Record**: item_id = ___

### ISSO-INV-04: Update Hardware Location

**Task**: Update the location field on an existing hardware item

```text
@ato Update web-server-01 location to "DC-East Rack A-12"
```

**Expected Tool**: `inventory_update_item`
**Expected Output**: Updated item with new location, `modified_at` timestamp updated

### ISSO-INV-05: Check Inventory Completeness

**Task**: Verify inventory completeness before export

```text
@ato Check the inventory completeness for Eagle Eye
```

**Expected Tool**: `inventory_completeness`
**Expected Output**: `completeness_score`, `is_complete`, lists of issues (if any)
**Record**: score = ___, is_complete = ___

### ISSO-INV-06: Export to eMASS Excel

**Task**: Export inventory to Excel for eMASS upload

```text
@ato Export the HW/SW inventory for Eagle Eye to Excel
```

**Expected Tool**: `inventory_export`
**Expected Output**: Base64-encoded Excel with Hardware and Software worksheets
**Verification**: Decode and open file — verify two worksheets with correct column headers

### ISSO-INV-07: Decommission Hardware (Cascade)

**Task**: Decommission a hardware item and verify cascade to child software

```text
@ato Decommission web-server-01 — rationale: "End of life, replaced by web-server-02"
```

**Expected Tool**: `inventory_decommission_item`
**Expected Output**: Item status = Decommissioned, child software also decommissioned
**Record**: cascaded_children = ___

---

## Narrative Governance (ISSO-NGV-01 to ISSO-NGV-07)

> Feature 024: Narrative version history, diff, rollback, and approval submission workflows.

### ISSO-NGV-01: Write Narrative (Version 1)

**Task**: Write a control narrative for AC-1 to create the initial version

```text
@ato Write the AC-1 narrative for Eagle Eye: "The organization develops, documents, and disseminates an access control policy..."
```

**Expected Tool**: `compliance_write_narrative`
**Expected Output**: Narrative saved with `version_number: 1`, `approval_status: Draft`
**Record**: version_number = ___

### ISSO-NGV-02: Update Narrative (Version 2)

**Task**: Update the AC-1 narrative with a change reason to create version 2

```text
@ato Update the AC-1 narrative for Eagle Eye: "Updated: The organization maintains access control policy consistent with..." — change reason: "Updated per ISSM feedback on 2026 assessment"
```

**Expected Tool**: `compliance_write_narrative` with `change_reason` parameter
**Expected Output**: `version_number: 2`, `previous_version: 1`, `approval_status: Draft`
**Record**: version_number = ___

### ISSO-NGV-03: View Narrative Version History

**Task**: Retrieve the full version history for AC-1

```text
@ato Show the version history for the AC-1 narrative of Eagle Eye
```

**Expected Tool**: `compliance_narrative_history`
**Expected Output**: List of versions (newest first) with `total_versions: 2`, each showing `version_number`, `authored_by`, `authored_at`, `change_reason`
**Record**: total_versions = ___

### ISSO-NGV-04: Diff Narrative Versions

**Task**: Compare versions 1 and 2 of the AC-1 narrative

```text
@ato Show the diff between version 1 and version 2 of the AC-1 narrative for Eagle Eye
```

**Expected Tool**: `compliance_narrative_diff`
**Expected Output**: Unified diff text with `lines_added` and `lines_removed` counts
**Record**: lines_added = ___ | lines_removed = ___

### ISSO-NGV-05: Rollback Narrative

**Task**: Roll back AC-1 narrative to version 1 (creates version 3 as copy-forward)

```text
@ato Roll back the AC-1 narrative for Eagle Eye to version 1
```

**Expected Tool**: `compliance_rollback_narrative`
**Expected Output**: `new_version_number: 3`, `rolled_back_to: 1`, `status: Draft`
**Record**: new_version_number = ___

### ISSO-NGV-06: Submit Narrative for ISSM Review

**Task**: Submit the AC-1 narrative for ISSM review

```text
@ato Submit the AC-1 narrative for Eagle Eye for ISSM review
```

**Expected Tool**: `compliance_submit_narrative`
**Expected Output**: `previous_status: Draft`, `new_status: InReview`, `submitted_by`, `submitted_at`
**Record**: new_status = ___

### ISSO-NGV-07: Batch Submit AC Family Narratives

**Task**: Submit all Draft narratives in the AC family for ISSM review

```text
@ato Submit all AC family narratives for Eagle Eye for ISSM review
```

**Expected Tool**: `compliance_batch_submit_narratives` with `family_filter` = "AC"
**Expected Output**: `submitted_count`, `skipped_count`, `submitted_controls`, `skipped_controls`
**Record**: submitted_count = ___ | skipped_count = ___
