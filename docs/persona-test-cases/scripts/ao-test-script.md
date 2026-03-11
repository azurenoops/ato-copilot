# AO Persona Test Execution Script

**Feature**: 020 | **Persona**: AO (Authorizing Official)
**Role**: `Compliance.AuthorizingOfficial` | **Interface**: Microsoft Teams (Adaptive Cards)
**Test Cases**: AO-01 through AO-16 (16 total)

---

## Pre-Execution Setup

### T025 — Role Activation

1. **Deactivate SCA role** (if active): `Deactivate my Auditor role`
2. **Activate AO role**: `Activate my Compliance.AuthorizingOfficial role for 4 hours — persona test suite`
3. **Verify role**: `Show my active PIM roles` → Confirm `Compliance.AuthorizingOfficial` is active
4. **Remain on Teams**: AO uses Microsoft Teams with Adaptive Cards

### Preconditions from SCA Phase

- ✓ SAR generated (SCA-17)
- ✓ RAR generated (SCA-18)
- ✓ Assessment complete with effectiveness determinations
- ✓ Authorization package bundled by ISSM (ISSM-29)
- ✓ System in Authorize phase (ISSM-30)
- ✓ OSCAL SSP exported (SCA-27 or ISSM-58)
- ✓ SSP sections authored by ISSO (ISSO-31 to ISSO-35)

---

## Phase 5 — Authorize (AO-01 to AO-11)

### AO-01: View Portfolio Dashboard

**Task**: Review multi-system compliance posture
**Type**: Positive test | **Precondition**: ≥ 1 system registered

```text
Show the multi-system compliance dashboard
```

**Expected Tool**: `compliance_multi_system_dashboard`
**Expected Output**:
- All systems listed
- Per system: RMF step, auth status, compliance score, open findings, expiration dates

**Verification**: Eagle Eye appears with current RMF step and score

---

### AO-02: Review Authorization Package

**Task**: Review the bundled package
**Type**: Positive test | **Precondition**: Package bundled by ISSM

```text
Show the authorization package summary for Eagle Eye
```

**Expected Tool**: `compliance_bundle_authorization_package`
**Expected Output**:
- Package summary: SSP status, SAR summary, RAR summary, POA&M count, CRM status
- Completeness check

**Verification**: All 5 artifacts present in package

---

### AO-03: View Risk Register

**Task**: Review risk posture before decision
**Type**: Positive test | **Precondition**: Assessment complete

```text
Show the risk register for Eagle Eye
```

**Expected Tool**: `compliance_show_risk_register`
**Expected Output**:
- Risk entries with finding ID, severity, control
- Status, recommended mitigation, residual risk

**Verification**: Risk entries present from SCA assessment

---

### AO-04: Issue ATO

**Task**: Grant Authority to Operate
**Type**: Positive test | **Precondition**: Package reviewed

```text
Issue an ATO for Eagle Eye expiring January 15, 2028 with Low residual
risk — all CAT I findings remediated, 2 CAT III findings accepted
```

**Expected Tool**: `compliance_issue_authorization`
**Expected Output**:
- Authorization record created
- type = ATO
- expiration = 2028-01-15
- residual risk = Low
- Conditions noted

**Verification**: Type = ATO, expiration date correct

---

### AO-05: Issue ATO with Conditions

**Task**: Grant conditional authorization
**Type**: Positive test | **Precondition**: Package reviewed

```text
Issue an ATO with Conditions for Eagle Eye — condition: CAT II finding
on SI-4 must be remediated within 60 days
```

**Expected Tool**: `compliance_issue_authorization`
**Expected Output**:
- Authorization record
- type = ATOwC
- Conditions array with SI-4 remediation deadline (60 days)

**Verification**: Type = ATOwC, condition present

---

### AO-06: Issue IATT

**Task**: Grant interim authorization for testing
**Type**: Positive test | **Precondition**: Package reviewed

```text
Issue an Interim Authorization to Test for Eagle Eye for 90 days —
limited to development environment only
```

**Expected Tool**: `compliance_issue_authorization`
**Expected Output**:
- Authorization record
- type = IATT
- 90-day duration
- Scope limitation noted

**Verification**: Type = IATT, duration = 90 days

---

### AO-07: Deny Authorization (DATO)

**Task**: Deny authorization due to unacceptable risk
**Type**: Positive test | **Precondition**: Package reviewed

```text
Deny authorization for Eagle Eye — 3 unmitigated CAT I findings present
unacceptable risk to the mission
```

**Expected Tool**: `compliance_issue_authorization`
**Expected Output**:
- Authorization record
- type = DATO
- Denial rationale recorded

**Verification**: Type = DATO, rationale contains "CAT I"

---

### AO-08: Accept Risk

**Task**: Accept risk with compensating control
**Type**: Positive test | **Precondition**: Finding exists

```text
Accept the risk on finding {finding_id} — the compensating control in
AC-3 adequately mitigates the risk
```

**Note**: Replace `{finding_id}` with an actual finding ID.

**Expected Tool**: `compliance_accept_risk`
**Expected Output**:
- Finding risk status updated to Accepted
- AO rationale saved
- Compensating control reference (AC-3) saved

**Verification**: Risk status = Accepted

---

### AO-09: Check ATO Expirations

**Task**: View upcoming expirations across portfolio
**Type**: Positive test | **Precondition**: ATOs granted

```text
What ATOs expire in the next 90 days?
```

**Expected Tool**: `compliance_track_ato_expiration`
**Expected Output**:
- List of systems with ATOs expiring within 90 days
- Alert levels per system

**Verification**: Response includes expiration data

---

### AO-10: View Compliance Trend

**Task**: Review compliance trajectory
**Type**: Positive test | **Precondition**: Monitoring active

```text
Show compliance score trend for Eagle Eye
```

**Expected Tool**: `watch_compliance_trend`
**Expected Output**:
- Score progression over time
- Data points per scan

**Verification**: Trend data returned

---

### AO-11: View Critical Alerts

**Task**: Review critical alerts across portfolio
**Type**: Positive test | **Precondition**: Monitoring active

```text
Show all critical alerts across my authorized systems
```

**Expected Tool**: `watch_show_alerts`
**Expected Output**:
- Critical alerts from all AO-authorized systems

**Verification**: Results filtered to critical severity

---

## SSP & OSCAL Review (AO-15 to AO-16)

### AO-15: Review SSP Completeness

**Task**: Check SSP completeness before authorization decision
**Type**: Positive test | **Precondition**: ISSO authored SSP sections (ISSO-31, ISSO-32)

```text
Show SSP completeness for Eagle Eye — I need to verify all sections are
complete before issuing my authorization decision
```

**Expected Tool**: `compliance_ssp_completeness`
**Expected Output**:
- Overall completion percentage
- Per-section status (complete / incomplete / draft)
- Missing or draft sections highlighted

**Verification**: Completion percentage returned; sections §5 and §6 show as authored

---

### AO-16: Export OSCAL SSP for Package Review

**Task**: Export OSCAL SSP as part of authorization package review
**Type**: Positive test | **Precondition**: SSP authored, OSCAL validated (SCA-28)

```text
Export the OSCAL SSP for Eagle Eye — I need the machine-readable
version for the authorization package
```

**Expected Tool**: `compliance_export_oscal_ssp`
**Expected Output**:
- OSCAL SSP JSON/XML artifact generated
- Includes system metadata, control implementations, responsible parties
- File download link or inline preview

**Verification**: OSCAL SSP artifact contains expected control count; format valid

---

## AO Separation-of-Duties Verification (AO-12 to AO-14)

**Purpose**: Verify that the AO (AuthorizingOfficial) role is correctly denied SSP modification, remediation, and assessment operations. All 3 tests must return **403 Forbidden**.

### AO-12: DENIED — Modify SSP

**Task**: Attempt to write narrative (should be denied)
**Type**: RBAC denial test | **Precondition**: AO role active

```text
Write narrative for AC-2 on Eagle Eye
```

**Expected Tool**: `compliance_write_narrative`
**Expected Response**: **403 Forbidden** — AO cannot modify SSP

**Verification**: HTTP 403 returned (not 404 or 500)

---

### AO-13: DENIED — Fix Findings

**Task**: Attempt to remediate (should be denied)
**Type**: RBAC denial test | **Precondition**: AO role active

```text
Remediate finding {finding_id}
```

**Expected Tool**: `compliance_remediate`
**Expected Response**: **403 Forbidden** — AO cannot execute remediation

**Verification**: HTTP 403 returned

---

### AO-14: DENIED — Assess Controls

**Task**: Attempt to assess (should be denied)
**Type**: RBAC denial test | **Precondition**: AO role active

```text
Assess AC-2 as Satisfied
```

**Expected Tool**: `compliance_assess_control`
**Expected Response**: **403 Forbidden** — only SCA can record assessments

**Verification**: HTTP 403 returned

---

## AO Results Summary

| Metric | Value |
|--------|-------|
| Total Test Cases | 16 |
| Positive Tests | ___/13 passed |
| RBAC Denied Tests | ___/3 returned 403 |
| Failed | ___ |
| Blocked | ___ |
| Avg Response Time | ___s |

### RBAC Verification Matrix

| TC-ID | Operation | Expected | Actual | Status |
|-------|-----------|----------|--------|--------|
| AO-12 | Write narrative | 403 | ___ | ⬜ |
| AO-13 | Remediate finding | 403 | ___ | ⬜ |
| AO-14 | Assess control | 403 | ___ | ⬜ |

### Issues Found

| # | TC-ID | Severity | Description | Root Cause |
|---|-------|----------|-------------|------------|
| | | | | |

### Authorization Decisions Issued

| TC-ID | Type | Expiration | Residual Risk | Conditions |
|-------|------|-----------|---------------|------------|
| AO-04 | ATO | 2028-01-15 | Low | None |
| AO-05 | ATOwC | — | — | SI-4 in 60d |
| AO-06 | IATT | +90 days | — | Dev only |
| AO-07 | DATO | — | — | 3 CAT I |

### SSP & OSCAL Review Artifacts

| Artifact | Value | TC-ID |
|----------|-------|-------|
| SSP Completion | ___% | AO-15 |
| OSCAL SSP Format | ___ | AO-16 |

**Checkpoint**: ⬜ AO (16 tests) complete. Authorization decision issued, SSP/OSCAL reviewed, RBAC enforced. Engineer testing can begin.
