# ISSO Persona Test Execution Script

**Feature**: 020 | **Persona**: ISSO (Information System Security Officer)
**Role**: `Compliance.Analyst` | **Interface**: VS Code `@ato`
**Test Cases**: ISSO-01 through ISSO-24 (24 total)

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

→ **Handoff**: SSP complete. System ready for SCA independent assessment.

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
| Total Test Cases | 24 |
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

**Checkpoint**: ⬜ ISSO (24 tests) complete. SSP authored, scans imported, monitoring active. SCA testing can begin.
