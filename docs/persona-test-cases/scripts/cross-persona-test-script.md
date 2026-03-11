# Cross-Persona, Error Handling & Auth/PIM Test Execution Script

**Feature**: 020 | **Scope**: Cross-persona scenarios, error/edge cases, authentication
**Test Cases**: ERR-01 to ERR-08, AUTH-01 to AUTH-08, 4 cross-persona scenarios (60 steps)

---

## Part 1: Error Handling & Edge Cases (ERR-01 to ERR-08)

> **Purpose**: Verify the system returns clear, actionable error messages for common misuse patterns.
> These tests intentionally trigger error conditions — the **expected result is an error message** (not success).

### ERR-01: Advance RMF Out of Order

**Task**: Attempt to skip RMF phases
**Persona**: ISSM | **Role**: `Compliance.SecurityLead` | **Interface**: Teams
**Type**: Error handling | **Precondition**: System in Prepare phase (need a fresh system or system in early phase)

> ⚠️ For this test, either use a second test system or note the error behavior conceptually if Eagle Eye has already progressed past Prepare.

```text
Advance Eagle Eye to the Assess phase
```

**Expected Tool**: `compliance_advance_rmf_step`
**Expected Output**:
- **Error**: Cannot skip phases — system is in {current_phase}, must advance to {next_phase} first
- Clear indication of the correct next phase
- No phase change occurs

**Verification**:
- [ ] Error message returned (not success)
- [ ] Message indicates phase ordering constraint
- [ ] System phase unchanged

---

### ERR-02: Import Malformed Prisma CSV

**Task**: Attempt to import a garbled/empty CSV file
**Persona**: ISSM | **Role**: `Compliance.SecurityLead` | **Interface**: Teams
**Type**: Error handling | **Precondition**: System in Implement or later

```text
Import this Prisma CSV for Eagle Eye
```

> ⚠️ Provide a garbled/empty file or a file missing required columns (Alert ID, Severity, Policy Name)

**Expected Tool**: `compliance_import_prisma_csv`
**Expected Output**:
- **Error**: CSV parsing failed — missing required columns (Alert ID, Severity, Policy Name)
- Import record created with status = Failed
- No findings ingested

**Verification**:
- [ ] Error message identifies missing/invalid columns
- [ ] Import record exists with Failed status
- [ ] No orphaned findings created

---

### ERR-03: Categorize Already-Categorized System

**Task**: Re-categorize a system that already has categorization
**Persona**: ISSM | **Role**: `Compliance.SecurityLead` | **Interface**: Teams
**Type**: Error handling | **Precondition**: System already categorized (Eagle Eye categorized in ISSM-08)

```text
Categorize Eagle Eye as High/High/High
```

**Expected Tool**: `compliance_categorize_system`
**Expected Output**: One of:
- **Upsert behavior**: Previous categorization replaced with High/High/High (if system still in Categorize phase)
- **Error**: System has already progressed past Categorize phase — cannot re-categorize

**Verification**:
- [ ] If upsert: new categorization reflected, old one replaced
- [ ] If error: clear message about phase constraint
- [ ] Either way, no data corruption

**Record**: Behavior observed: ☐ Upsert / ☐ Phase error

---

### ERR-04: Generate SAR with Zero Assessments

**Task**: Attempt to generate SAR without any assessment records
**Persona**: SCA | **Role**: `Compliance.Auditor` | **Interface**: Teams
**Type**: Error handling | **Precondition**: No assessments recorded (may need a second system or test before SCA phase)

> ⚠️ If Eagle Eye already has assessments, note this test case as requiring a fresh system. Record the expected behavior.

```text
Generate SAR for Eagle Eye
```

**Expected Tool**: `compliance_generate_sar`
**Expected Output**:
- **Warning/Error**: No control effectiveness records found — SAR cannot be generated without assessments
- No empty SAR document created

**Verification**:
- [ ] Error or warning message returned
- [ ] Message references missing assessments as the root cause
- [ ] No incomplete SAR generated

---

### ERR-05: Bundle Incomplete Authorization Package

**Task**: Bundle a package with missing artifacts
**Persona**: ISSM | **Role**: `Compliance.SecurityLead` | **Interface**: Teams
**Type**: Error handling | **Precondition**: Missing SAR or POA&M (test before those exist, or conceptual)

```text
Bundle authorization package for Eagle Eye
```

**Expected Tool**: `compliance_bundle_authorization_package`
**Expected Output**:
- Package generated with `warnings` array listing missing artifacts
  - e.g., "SAR not found", "POA&M empty"
- Package still created (with gaps flagged)
- Not a hard failure — soft warnings

**Verification**:
- [ ] Package returned (not rejected)
- [ ] Warnings array present in response
- [ ] Each missing artifact listed by name
- [ ] Package is usable but flagged as incomplete

---

### ERR-06: Finalize Already-Finalized SAP

**Task**: Attempt to re-finalize an already-finalized SAP
**Persona**: ISSM | **Role**: `Compliance.SecurityLead` | **Interface**: Teams
**Type**: Error handling | **Precondition**: SAP already finalized (ISSM-43)

```text
Finalize the SAP for Eagle Eye
```

**Expected Tool**: `compliance_finalize_sap`
**Expected Output**:
- **Error**: SAP is already Finalized — cannot re-finalize
- SHA-256 hash preserved (original hash unchanged)
- No state change

**Verification**:
- [ ] Error message returned
- [ ] Message indicates SAP is already finalized
- [ ] SHA-256 hash matches the original finalization hash

---

### ERR-07: Update Finalized SAP

**Task**: Attempt to modify a finalized (immutable) SAP
**Persona**: ISSM | **Role**: `Compliance.SecurityLead` | **Interface**: Teams
**Type**: Error handling | **Precondition**: SAP finalized (ISSM-43)

```text
Update Eagle Eye's SAP — change the start date to May 1
```

**Expected Tool**: `compliance_update_sap`
**Expected Output**:
- **Error**: SAP is Finalized and immutable — cannot modify
- Guidance: must generate a new SAP if changes are needed
- No modification to existing SAP

**Verification**:
- [ ] Error message returned
- [ ] Message indicates immutability of finalized SAP
- [ ] Guidance provided (generate new SAP)
- [ ] Original SAP unchanged

---

### ERR-08: Remediate Non-Existent Finding

**Task**: Attempt to remediate with an invalid finding ID
**Persona**: Engineer | **Role**: `Compliance.PlatformEngineer` | **Interface**: VS Code `@ato`
**Type**: Error handling | **Precondition**: Invalid finding ID

```text
@ato Remediate finding 00000000-0000-0000-0000-000000000000
```

**Expected Tool**: `compliance_remediate`
**Expected Output**:
- **Error**: Finding not found — verify the finding ID is correct
- No remediation attempted
- No state changes

**Verification**:
- [ ] Error message returned
- [ ] Message indicates finding not found
- [ ] No resource modifications attempted

---

### Error Handling Results

| TC-ID | Error Triggered | Correct Message | No Side Effects | Status |
|-------|----------------|-----------------|-----------------|--------|
| ERR-01 | ☐ | ☐ | ☐ | ☐ Pass / ☐ Fail |
| ERR-02 | ☐ | ☐ | ☐ | ☐ Pass / ☐ Fail |
| ERR-03 | ☐ | ☐ | ☐ | ☐ Pass / ☐ Fail |
| ERR-04 | ☐ | ☐ | ☐ | ☐ Pass / ☐ Fail |
| ERR-05 | ☐ | ☐ | ☐ | ☐ Pass / ☐ Fail |
| ERR-06 | ☐ | ☐ | ☐ | ☐ Pass / ☐ Fail |
| ERR-07 | ☐ | ☐ | ☐ | ☐ Pass / ☐ Fail |
| ERR-08 | ☐ | ☐ | ☐ | ☐ Pass / ☐ Fail |

**Error Handling Pass Criteria**: All 8 return appropriate error messages → ___/8

---

## Part 2: PIM / Authentication Tests (AUTH-01 to AUTH-08)

> **Purpose**: Verify PIM role activation, CAC authentication, and JIT access work correctly.
> These tests can be run with **any** persona — use whichever is currently active.

### AUTH-01: Check CAC Session

**Task**: Verify CAC smart card session status
**Persona**: Any | **Interface**: Any

```text
Check my CAC session status
```

**Expected Tool**: `cac_status`
**Expected Output**:
- Session status: active or expired
- Certificate information (CN, expiration)
- Role mapping from CAC to Compliance roles

**Verification**:
- [ ] Session status returned
- [ ] Certificate info present
- [ ] Role mapping shown

---

### AUTH-02: List Eligible PIM Roles

**Task**: Check which roles the current user can activate
**Persona**: Any | **Interface**: Any

```text
What PIM roles am I eligible for?
```

**Expected Tool**: `pim_list_eligible`
**Expected Output**:
- List of eligible roles
- Each role: name, max activation duration
- At least 5 compliance roles present

**Verification**:
- [ ] ≥ 5 roles listed
- [ ] Max duration shown per role
- [ ] All 5 compliance roles present (SecurityLead, Analyst, Auditor, AuthorizingOfficial, PlatformEngineer)

---

### AUTH-03: Activate PIM Role

**Task**: Activate a PIM role with justification
**Persona**: Any | **Interface**: Any

```text
Activate my Compliance.SecurityLead role for 4 hours — quarterly review preparation
```

**Expected Tool**: `pim_activate_role`
**Expected Output**:
- Role activated confirmation
- Expiration time (4 hours from now)
- Justification recorded: "quarterly review preparation"

**Verification**:
- [ ] Activation confirmed
- [ ] Expiration time correct (~4 hours)
- [ ] Justification preserved in response

---

### AUTH-04: List Active Roles

**Task**: View currently active PIM roles
**Persona**: Any | **Interface**: Any

```text
Show my active PIM roles
```

**Expected Tool**: `pim_list_active`
**Expected Output**:
- Active roles with:
  - Role name
  - Activation time
  - Expiration time
  - Justification

**Verification**:
- [ ] SecurityLead role appears (from AUTH-03)
- [ ] Activation and expiration times present
- [ ] Justification matches AUTH-03

---

### AUTH-05: Request JIT Access

**Task**: Request just-in-time access to a production subscription
**Persona**: Any | **Interface**: Any

```text
Request just-in-time access to the production subscription for 2 hours — emergency remediation
```

**Expected Tool**: `jit_request_access`
**Expected Output**:
- JIT session created
- Access granted with scope (subscription)
- Expiration set (2 hours)
- Justification recorded: "emergency remediation"

**Verification**:
- [ ] JIT session confirmed
- [ ] Scope matches requested subscription
- [ ] Expiration is ~2 hours
- [ ] Justification preserved

---

### AUTH-06: Approve PIM Request

**Task**: Approve another user's PIM activation request
**Persona**: ISSM | **Role**: `Compliance.SecurityLead` | **Interface**: Teams

```text
Approve the PIM request from Jane Smith
```

**Expected Tool**: `pim_approve_request`
**Expected Output**:
- Request approved
- Role activated for the requester (Jane Smith)
- Approval recorded with approver identity and timestamp

**Verification**:
- [ ] Approval confirmed
- [ ] Requester name matches
- [ ] Approver identity recorded

---

### AUTH-07: Deny PIM Request

**Task**: Deny a PIM request with reason
**Persona**: ISSM | **Role**: `Compliance.SecurityLead` | **Interface**: Teams

```text
Deny the PIM request — insufficient justification
```

**Expected Tool**: `pim_deny_request`
**Expected Output**:
- Request denied
- Denial reason recorded: "insufficient justification"
- Requester notified

**Verification**:
- [ ] Denial confirmed
- [ ] Reason preserved in response
- [ ] No role activated for requester

---

### AUTH-08: Deactivate Role

**Task**: Deactivate a PIM role before it expires
**Persona**: Any | **Interface**: Any

```text
Deactivate my SecurityLead role
```

**Expected Tool**: `pim_deactivate_role`
**Expected Output**:
- Role deactivated early
- Session ended
- Timestamp recorded

**Verification**:
- [ ] Deactivation confirmed
- [ ] Role no longer appears in active roles (verify with AUTH-04)

---

### Auth/PIM Results

| TC-ID | Tool Resolved | Output Correct | Status |
|-------|--------------|----------------|--------|
| AUTH-01 | ☐ | ☐ | ☐ Pass / ☐ Fail |
| AUTH-02 | ☐ | ☐ | ☐ Pass / ☐ Fail |
| AUTH-03 | ☐ | ☐ | ☐ Pass / ☐ Fail |
| AUTH-04 | ☐ | ☐ | ☐ Pass / ☐ Fail |
| AUTH-05 | ☐ | ☐ | ☐ Pass / ☐ Fail |
| AUTH-06 | ☐ | ☐ | ☐ Pass / ☐ Fail |
| AUTH-07 | ☐ | ☐ | ☐ Pass / ☐ Fail |
| AUTH-08 | ☐ | ☐ | ☐ Pass / ☐ Fail |

**Auth/PIM Pass Criteria**: All 8 tests pass → ___/8

---

## Part 3: Cross-Persona Scenario 1 — Full RMF Lifecycle (Prepare Through ATO)

> **Purpose**: Walk through the complete RMF lifecycle with persona switching at each handoff.
> This is an end-to-end integration scenario covering 17 steps across 5 personas.

| Step | Persona | Role to Activate |
|------|---------|-----------------|
| 1-5 | ISSM | Compliance.SecurityLead |
| 6-7 | ISSO | Compliance.Analyst |
| 8 | Engineer | Compliance.PlatformEngineer |
| 9-14 | ISSM | Compliance.SecurityLead |
| 15 | AO | Compliance.AuthorizingOfficial |
| 16 | ISSM | Compliance.SecurityLead |
| 17 | ISSO | Compliance.Analyst |

### Step 1: Register System (ISSM)

```text
Register Eagle Eye as Major Application in Azure Gov
```

**Expected Tool**: `compliance_register_system`
**Expected Output**: system_id returned
**Record**: system_id = __________

---

### Step 2: Define Boundary + Assign Roles (ISSM)

```text
Define boundary; assign ISSO and SCA
```

**Expected Tool**: `compliance_define_boundary`, `compliance_assign_rmf_role`
**Expected Output**: boundary_id, role assignments
**Record**: boundary_id = __________

---

### Step 3: Categorize (ISSM)

```text
Categorize as Moderate/Moderate/Low
```

**Expected Tool**: `compliance_categorize_system`
**Expected Output**: FIPS 199 record (C=Moderate, I=Moderate, A=Low)

---

### Step 4: Select + Tailor Baseline (ISSM)

```text
Select Moderate baseline; set inheritance
```

**Expected Tool**: `compliance_select_baseline`, `compliance_set_inheritance`
**Expected Output**: 325 controls selected, inheritance records created

---

### Step 5: Advance to Implement (ISSM)

```text
Move to Implement
```

**Expected Tool**: `compliance_advance_rmf_step`
**Expected Output**: RMF step = Implement

---

### → Handoff: ISSM → ISSO

> **Action**: Deactivate SecurityLead role. Activate Compliance.Analyst role. Switch to VS Code.

---

### Step 6: Author SSP (ISSO)

```text
@ato Auto-populate inherited narratives for Eagle Eye; write customer narratives
```

**Expected Tool**: `compliance_batch_populate_narratives`, `compliance_write_narrative`
**Expected Output**: Narratives at 100% progress

---

### Step 7: Import Scans (ISSO)

```text
@ato Import CKL + Prisma CSV for Eagle Eye
```

**Expected Tool**: `compliance_import_ckl`, `compliance_import_prisma_csv`
**Expected Output**: Import records, findings created

---

### → Handoff: ISSO → Engineer

> **Action**: Deactivate Analyst role. Activate Compliance.PlatformEngineer. Stay in VS Code.

---

### Step 8: Fix Findings (Engineer)

```text
@ato Fix task REM-{id}; validate
```

**Expected Tool**: `kanban_remediate_task`, `kanban_task_validate`
**Expected Output**: Findings resolved, validation passed

---

### → Handoff: Engineer → ISSM

> **Action**: Deactivate PlatformEngineer. Activate Compliance.SecurityLead. Switch to Teams.

---

### Step 9: Advance to Assess (ISSM)

```text
Move to Assess
```

**Expected Tool**: `compliance_advance_rmf_step`
**Expected Output**: RMF step = Assess

---

### → Handoff: ISSM → SCA

> **Action**: Deactivate SecurityLead. Activate Compliance.Auditor. Stay on Teams.

---

### Step 10: Assess Controls (SCA)

```text
Assess AC-2 as Satisfied; assess SI-4 as OtherThanSatisfied CAT II
```

**Expected Tool**: `compliance_assess_control`
**Expected Output**: Effectiveness records created

---

### Step 11: Generate SAR (SCA)

```text
Generate SAR
```

**Expected Tool**: `compliance_generate_sar`
**Expected Output**: SAR document generated

---

### → Handoff: SCA → ISSM

> **Action**: Deactivate Auditor. Activate Compliance.SecurityLead. Stay on Teams.

---

### Step 12: Create POA&M + RAR (ISSM)

```text
Create POA&M for CAT II finding; generate RAR
```

**Expected Tool**: `compliance_create_poam`, `compliance_generate_rar`
**Expected Output**: POA&M and RAR created

---

### Step 13: Bundle Package (ISSM)

```text
Bundle authorization package
```

**Expected Tool**: `compliance_bundle_authorization_package`
**Expected Output**: Complete authorization package

---

### Step 14: Advance to Authorize (ISSM)

```text
Move to Authorize
```

**Expected Tool**: `compliance_advance_rmf_step`
**Expected Output**: RMF step = Authorize

---

### → Handoff: ISSM → AO

> **Action**: Deactivate SecurityLead. Activate Compliance.AuthorizingOfficial. Stay on Teams.

---

### Step 15: Issue ATO (AO)

```text
Issue ATO expiring Jan 2028 with Low residual risk
```

**Expected Tool**: `compliance_issue_authorization`
**Expected Output**: ATO granted; expiration = Jan 2028; residual risk = Low

---

### → Handoff: AO → ISSM

> **Action**: Deactivate AuthorizingOfficial. Activate Compliance.SecurityLead. Stay on Teams.

---

### Step 16: Set Up ConMon (ISSM)

```text
Create ConMon plan with monthly assessments
```

**Expected Tool**: `compliance_create_conmon_plan`
**Expected Output**: ConMon plan active with monthly assessment cadence

---

### → Handoff: ISSM → ISSO

> **Action**: Deactivate SecurityLead. Activate Compliance.Analyst. Switch to VS Code.

---

### Step 17: Enable Monitoring (ISSO)

```text
@ato Enable daily monitoring
```

**Expected Tool**: `watch_enable_monitoring`
**Expected Output**: Monitoring active with daily cadence

---

### Scenario 1 Results

| Step | Persona | Tool Resolved | Data Flows | Status |
|------|---------|--------------|------------|--------|
| 1 | ISSM | ☐ | ☐ | ☐ Pass / ☐ Fail |
| 2 | ISSM | ☐ | ☐ | ☐ Pass / ☐ Fail |
| 3 | ISSM | ☐ | ☐ | ☐ Pass / ☐ Fail |
| 4 | ISSM | ☐ | ☐ | ☐ Pass / ☐ Fail |
| 5 | ISSM | ☐ | ☐ | ☐ Pass / ☐ Fail |
| 6 | ISSO | ☐ | ☐ | ☐ Pass / ☐ Fail |
| 7 | ISSO | ☐ | ☐ | ☐ Pass / ☐ Fail |
| 8 | Engineer | ☐ | ☐ | ☐ Pass / ☐ Fail |
| 9 | ISSM | ☐ | ☐ | ☐ Pass / ☐ Fail |
| 10 | SCA | ☐ | ☐ | ☐ Pass / ☐ Fail |
| 11 | SCA | ☐ | ☐ | ☐ Pass / ☐ Fail |
| 12 | ISSM | ☐ | ☐ | ☐ Pass / ☐ Fail |
| 13 | ISSM | ☐ | ☐ | ☐ Pass / ☐ Fail |
| 14 | ISSM | ☐ | ☐ | ☐ Pass / ☐ Fail |
| 15 | AO | ☐ | ☐ | ☐ Pass / ☐ Fail |
| 16 | ISSM | ☐ | ☐ | ☐ Pass / ☐ Fail |
| 17 | ISSO | ☐ | ☐ | ☐ Pass / ☐ Fail |

**Persona Transitions**: 8 handoffs total → All successful: ☐ Yes / ☐ No
**Scenario 1 Status**: ☐ PASS / ☐ FAIL | ___/17 steps passed

---

## Part 4: Cross-Persona Scenario 2 — Prisma Cloud Import → Assessment → Remediation

> **Purpose**: Exercise the Prisma Cloud import lifecycle from raw scan data through remediation and trend verification.
> 13 steps across 4 personas (ISSM, SCA, ISSO, Engineer).

### Step 1: Import Prisma CSV (ISSM)

```text
Import Prisma Cloud CSV scan for Eagle Eye
```

**Expected Tool**: `compliance_import_prisma_csv`
**Expected Output**: Findings with NIST control mappings imported

---

### Step 2: Import Prisma API JSON (ISSM)

```text
Import Prisma API scan with auto-resolve subscriptions
```

**Expected Tool**: `compliance_import_prisma_api`
**Expected Output**: Enhanced findings with CLI remediation scripts

---

### Step 3: Review Policies (ISSM)

```text
Show all Prisma policies affecting Eagle Eye
```

**Expected Tool**: `compliance_list_prisma_policies`
**Expected Output**: Policy catalog with severity + NIST mappings

---

### → Handoff: ISSM → SCA

---

### Step 4: Review Cloud Posture (SCA)

```text
Show Prisma trend for Eagle Eye grouped by severity
```

**Expected Tool**: `compliance_prisma_trend`
**Expected Output**: Trend data grouped by severity for assessment context

---

### Step 5: Assess Cloud Controls (SCA)

```text
Assess SC-7 as OtherThanSatisfied CAT II based on Prisma network findings
```

**Expected Tool**: `compliance_assess_control`
**Expected Output**: Effectiveness record linked to Prisma finding data

---

### Step 6: Generate SAR (SCA)

```text
Generate SAR for Eagle Eye
```

**Expected Tool**: `compliance_generate_sar`
**Expected Output**: SAR includes Prisma cloud posture data

---

### → Handoff: SCA → ISSM

---

### Step 7: Create Kanban Board (ISSM)

```text
Create remediation board from Eagle Eye's assessment
```

**Expected Tool**: `kanban_create_board`
**Expected Output**: Board with Prisma-sourced remediation tasks

---

### → Handoff: ISSM → ISSO

---

### Step 8: Assign to Engineer (ISSO)

```text
@ato Assign SC-7 tasks to SSgt Rodriguez
```

**Expected Tool**: `kanban_assign_task`
**Expected Output**: Task assigned to SSgt Rodriguez

---

### → Handoff: ISSO → Engineer

---

### Step 9: View CLI Scripts (Engineer)

```text
@ato Show CLI scripts for my assigned Prisma tasks
```

**Expected Tool**: `kanban_get_task`
**Expected Output**: Task with RemediationCli field populated with executable Azure CLI/PowerShell commands

---

### Step 10: Apply Fix (Engineer)

```text
@ato Apply fix for task REM-{id}
```

**Expected Tool**: `kanban_remediate_task`
**Expected Output**: Cloud resource remediated

---

### Step 11: Validate (Engineer)

```text
@ato Validate task REM-{id}
```

**Expected Tool**: `kanban_task_validate`
**Expected Output**: Fix confirmed — validation passed

---

### → Handoff: Engineer → ISSM

---

### Step 12: Re-Import Prisma (ISSM)

```text
Import latest Prisma scan to verify remediation
```

**Expected Tool**: `compliance_import_prisma_csv`
**Expected Output**: Resolved findings reflected; fewer open findings

---

### Step 13: Review Trend (ISSM)

```text
Show Prisma trend for Eagle Eye
```

**Expected Tool**: `compliance_prisma_trend`
**Expected Output**: Downward trend in open alerts — remediation validated

---

### Scenario 2 Results

| Step | Persona | Tool Resolved | Prisma Data Flows | Status |
|------|---------|--------------|-------------------|--------|
| 1 | ISSM | ☐ | ☐ | ☐ Pass / ☐ Fail |
| 2 | ISSM | ☐ | ☐ | ☐ Pass / ☐ Fail |
| 3 | ISSM | ☐ | ☐ | ☐ Pass / ☐ Fail |
| 4 | SCA | ☐ | ☐ | ☐ Pass / ☐ Fail |
| 5 | SCA | ☐ | ☐ | ☐ Pass / ☐ Fail |
| 6 | SCA | ☐ | ☐ | ☐ Pass / ☐ Fail |
| 7 | ISSM | ☐ | ☐ | ☐ Pass / ☐ Fail |
| 8 | ISSO | ☐ | ☐ | ☐ Pass / ☐ Fail |
| 9 | Engineer | ☐ | ☐ | ☐ Pass / ☐ Fail |
| 10 | Engineer | ☐ | ☐ | ☐ Pass / ☐ Fail |
| 11 | Engineer | ☐ | ☐ | ☐ Pass / ☐ Fail |
| 12 | ISSM | ☐ | ☐ | ☐ Pass / ☐ Fail |
| 13 | ISSM | ☐ | ☐ | ☐ Pass / ☐ Fail |

**Persona Transitions**: 5 handoffs → All successful: ☐ Yes / ☐ No
**Scenario 2 Status**: ☐ PASS / ☐ FAIL | ___/13 steps passed

---

## Part 5: Cross-Persona Scenario 3 — Continuous Monitoring Drift → Reauthorization

> **Purpose**: Exercise the drift detection → escalation → reauthorization workflow.
> 10 steps across 4 personas (ISSO, ISSM, SCA, AO).

### Step 1: Alert Fires (ISSO)

```text
@ato Show all unacknowledged alerts
```

**Expected Tool**: `watch_show_alerts`
**Expected Output**: New CAT I drift finding detected

---

### Step 2: Acknowledge Alert (ISSO)

```text
@ato Acknowledge alert ALT-{id}
```

**Expected Tool**: `watch_acknowledge_alert`
**Expected Output**: Alert acknowledged with timestamp

---

### Step 3: Escalate to ISSM (ISSO)

```text
@ato This is a CAT I — needs ISSM review
```

**Expected Tool**: `kanban_add_comment` / notification
**Expected Output**: ISSM notified of CAT I escalation

---

### → Handoff: ISSO → ISSM

---

### Step 4: Report Significant Change (ISSM)

```text
Report significant change — security architecture modified
```

**Expected Tool**: `compliance_report_significant_change`
**Expected Output**: `requires_reauthorization = true`

---

### Step 5: Check Reauth Triggers (ISSM)

```text
Check reauthorization triggers for Eagle Eye
```

**Expected Tool**: `compliance_reauthorization_workflow`
**Expected Output**: Triggers listed: significant change reported, drift > 10%

---

### Step 6: Initiate Reauthorization (ISSM)

```text
Initiate reauthorization for Eagle Eye
```

**Expected Tool**: `compliance_reauthorization_workflow`
**Expected Output**: RMF regressed to Assess; reauthorization triggered

---

### → Handoff: ISSM → SCA

---

### Step 7: Re-Assess (SCA)

```text
Run assessment on Eagle Eye
```

**Expected Tool**: `compliance_assess`
**Expected Output**: New assessment reflecting updated findings from drift

---

### Step 8: New SAR (SCA)

```text
Generate SAR for Eagle Eye
```

**Expected Tool**: `compliance_generate_sar`
**Expected Output**: SAR reflecting current state post-drift

---

### → Handoff: SCA → ISSM

---

### Step 9: Re-Bundle Package (ISSM)

```text
Bundle authorization package
```

**Expected Tool**: `compliance_bundle_authorization_package`
**Expected Output**: Updated authorization package with new SAR

---

### → Handoff: ISSM → AO

---

### Step 10: Re-Authorize (AO)

```text
Issue ATO with Conditions — CAT I must be fixed in 30 days
```

**Expected Tool**: `compliance_issue_authorization`
**Expected Output**: ATOwC (ATO with Conditions) issued; condition: CAT I fix in 30 days

---

### Scenario 3 Results

| Step | Persona | Tool Resolved | Data Flows | Status |
|------|---------|--------------|------------|--------|
| 1 | ISSO | ☐ | ☐ | ☐ Pass / ☐ Fail |
| 2 | ISSO | ☐ | ☐ | ☐ Pass / ☐ Fail |
| 3 | ISSO | ☐ | ☐ | ☐ Pass / ☐ Fail |
| 4 | ISSM | ☐ | ☐ | ☐ Pass / ☐ Fail |
| 5 | ISSM | ☐ | ☐ | ☐ Pass / ☐ Fail |
| 6 | ISSM | ☐ | ☐ | ☐ Pass / ☐ Fail |
| 7 | SCA | ☐ | ☐ | ☐ Pass / ☐ Fail |
| 8 | SCA | ☐ | ☐ | ☐ Pass / ☐ Fail |
| 9 | ISSM | ☐ | ☐ | ☐ Pass / ☐ Fail |
| 10 | AO | ☐ | ☐ | ☐ Pass / ☐ Fail |

**Persona Transitions**: 5 handoffs → All successful: ☐ Yes / ☐ No
**Scenario 3 Status**: ☐ PASS / ☐ FAIL | ___/10 steps passed

---

## Part 6: Cross-Persona Scenario 4 — Privacy, Interconnection & SSP/OSCAL Lifecycle

> **Purpose**: Exercise the privacy analysis, interconnection management, SSP authoring, and OSCAL export workflows with cross-persona handoffs.
> 20 steps across 5 personas (ISSO, ISSM, Engineer, SCA, AO).

| Step | Persona | Role to Activate |
|------|---------|-----------------|
| 1 | ISSO | Compliance.Analyst |
| 2-3 | ISSM | Compliance.SecurityLead |
| 4 | ISSO | Compliance.Analyst |
| 5 | Engineer | Compliance.PlatformEngineer |
| 6-8 | ISSM | Compliance.SecurityLead |
| 9-10 | ISSO | Compliance.Analyst |
| 11-12 | ISSM | Compliance.SecurityLead |
| 13 | ISSO | Compliance.Analyst |
| 14 | ISSM | Compliance.SecurityLead |
| 15-17 | SCA | Compliance.Auditor |
| 18 | AO | Compliance.AuthorizingOfficial |
| 19 | ISSM | Compliance.SecurityLead |
| 20 | Engineer | Compliance.PlatformEngineer |

### Step 1: Create PTA (ISSO)

```text
@ato Create a Privacy Threshold Analysis for Eagle Eye — the system
processes PII including name, SSN, and email for personnel records
```

**Expected Tool**: `compliance_create_pta`
**Expected Output**: PTA record created with PII categories identified
**Record**: PTA ID = __________

---

### → Handoff: ISSO → ISSM

---

### Step 2: Generate PIA from PTA (ISSM)

```text
Generate a Privacy Impact Assessment for Eagle Eye based on the PTA
```

**Expected Tool**: `compliance_generate_pia`
**Expected Output**: PIA document generated with risk analysis, mitigation measures, retention policies
**Record**: PIA ID = __________

---

### Step 3: Review PIA (ISSM)

```text
Review the PIA for Eagle Eye — approve with note: retention period
set to 7 years per DoD 5400.11
```

**Expected Tool**: `compliance_review_pia`
**Expected Output**: PIA status updated to Approved; reviewer note recorded

---

### → Handoff: ISSM → ISSO

---

### Step 4: Register Interconnection — DISA DEE (ISSO)

```text
@ato Add an interconnection for Eagle Eye — outbound SMTP to DISA
DEE (smtp.dee.disa.mil) for email relay, port 587 TLS
```

**Expected Tool**: `compliance_add_interconnection`
**Expected Output**: Interconnection created (direction: outbound, protocol: SMTP/TLS, port: 587)
**Record**: Interconnection ID (DISA DEE) = __________

---

### → Handoff: ISSO → Engineer

---

### Step 5: Register Interconnection — Azure DevOps (Engineer)

```text
@ato Add an interconnection for Eagle Eye — outbound HTTPS to Azure
DevOps (dev.azure.com) for CI/CD pipeline integration, port 443
```

**Expected Tool**: `compliance_add_interconnection`
**Expected Output**: Interconnection created (direction: outbound, protocol: HTTPS, port: 443)
**Record**: Interconnection ID (Azure DevOps) = __________

---

### → Handoff: Engineer → ISSM

---

### Step 6: Generate ISA (ISSM)

```text
Generate an Interconnection Security Agreement for Eagle Eye covering
all registered interconnections
```

**Expected Tool**: `compliance_generate_isa`
**Expected Output**: ISA document generated covering DISA DEE and Azure DevOps interconnections
**Record**: ISA ID = __________

---

### Step 7: Register MOA Agreement (ISSM)

```text
Register a Memorandum of Agreement for the DISA DEE email relay
interconnection — effective date today, annual review
```

**Expected Tool**: `compliance_register_agreement`
**Expected Output**: Agreement registered with type: MOA, status: Active
**Record**: Agreement ID = __________

---

### Step 8: Validate Agreements (ISSM)

```text
Validate all interconnection agreements for Eagle Eye
```

**Expected Tool**: `compliance_validate_agreements`
**Expected Output**: All agreements valid; coverage report showing each interconnection has an agreement

---

### → Handoff: ISSM → ISSO

---

### Step 9: Write SSP §5 — System Architecture (ISSO)

```text
@ato Write SSP section 5 for Eagle Eye: System architecture consists
of Azure App Service frontend, Azure SQL backend, Azure Key Vault
for secrets, and Azure Front Door for global load balancing. All
components deployed in Azure Government (USGov Virginia).
```

**Expected Tool**: `compliance_write_ssp_section`
**Expected Output**: SSP §5 saved; status: Draft (pending ISSM review)

---

### Step 10: Write SSP §6 — Technical Controls (ISSO)

```text
@ato Write SSP section 6 for Eagle Eye: Technical controls include
Azure AD Conditional Access for MFA enforcement, Microsoft Defender
for Cloud for workload protection, NSG micro-segmentation, and Azure
Policy for compliance guardrails.
```

**Expected Tool**: `compliance_write_ssp_section`
**Expected Output**: SSP §6 saved; status: Draft (pending ISSM review)

---

### → Handoff: ISSO → ISSM

---

### Step 11: Review SSP §5 — Approve (ISSM)

```text
Review SSP section 5 for Eagle Eye — approve as written
```

**Expected Tool**: `compliance_review_ssp_section`
**Expected Output**: §5 status changed to Approved; reviewer identity recorded

---

### Step 12: Review SSP §6 — Request Revision (ISSM)

```text
Review SSP section 6 for Eagle Eye — revise: add Azure Firewall
Premium with IDPS to the technical controls description
```

**Expected Tool**: `compliance_review_ssp_section`
**Expected Output**: §6 status changed to Revision Requested; revision note recorded

---

### → Handoff: ISSM → ISSO

---

### Step 13: Update SSP §6 After Revision (ISSO)

```text
@ato Update SSP section 6 for Eagle Eye: Technical controls include
Azure AD Conditional Access for MFA enforcement, Microsoft Defender
for Cloud for workload protection, Azure Firewall Premium with IDPS,
NSG micro-segmentation, and Azure Policy for compliance guardrails.
```

**Expected Tool**: `compliance_write_ssp_section`
**Expected Output**: §6 updated with Azure Firewall Premium; status reset to Draft

---

### → Handoff: ISSO → ISSM

---

### Step 14: Re-Review SSP §6 — Approve (ISSM)

```text
Review SSP section 6 for Eagle Eye — approve the updated version
```

**Expected Tool**: `compliance_review_ssp_section`
**Expected Output**: §6 status changed to Approved

---

### → Handoff: ISSM → SCA

---

### Step 15: Check SSP Completeness (SCA)

```text
Show SSP completeness for Eagle Eye
```

**Expected Tool**: `compliance_ssp_completeness`
**Expected Output**: Overall completion percentage; §5 and §6 show as Approved

---

### Step 16: Export OSCAL SSP (SCA)

```text
Export OSCAL SSP for Eagle Eye
```

**Expected Tool**: `compliance_export_oscal_ssp`
**Expected Output**: OSCAL SSP JSON/XML artifact with system metadata, control implementations

---

### Step 17: Validate OSCAL SSP (SCA)

```text
Validate the OSCAL SSP for Eagle Eye against NIST SP 800-53 Rev 5
```

**Expected Tool**: `compliance_validate_oscal_ssp`
**Expected Output**: Validation results with any schema or content issues flagged

---

### → Handoff: SCA → AO

---

### Step 18: Review OSCAL SSP in Authorization Package (AO)

```text
Export the OSCAL SSP for Eagle Eye — I need the machine-readable
version for my authorization decision
```

**Expected Tool**: `compliance_export_oscal_ssp`
**Expected Output**: OSCAL SSP available for AO review as part of authorization package

---

### → Handoff: AO → ISSM

---

### Step 19: Check Privacy Compliance (ISSM)

```text
Check privacy compliance status for Eagle Eye
```

**Expected Tool**: `compliance_check_privacy_compliance`
**Expected Output**: Privacy compliance summary: PTA complete, PIA approved, all PII categories covered

---

### → Handoff: ISSM → Engineer

---

### Step 20: Export CKL Evidence (Engineer)

```text
@ato Export a CKL file for Eagle Eye Windows Server 2022 STIG
```

**Expected Tool**: `compliance_export_ckl`
**Expected Output**: CKL XML file with per-rule status reflecting current remediation state

---

### Scenario 4 Results

| Step | Persona | Tool Resolved | Data Flows | Status |
|------|---------|--------------|------------|--------|
| 1 | ISSO | ☐ | ☐ | ☐ Pass / ☐ Fail |
| 2 | ISSM | ☐ | ☐ | ☐ Pass / ☐ Fail |
| 3 | ISSM | ☐ | ☐ | ☐ Pass / ☐ Fail |
| 4 | ISSO | ☐ | ☐ | ☐ Pass / ☐ Fail |
| 5 | Engineer | ☐ | ☐ | ☐ Pass / ☐ Fail |
| 6 | ISSM | ☐ | ☐ | ☐ Pass / ☐ Fail |
| 7 | ISSM | ☐ | ☐ | ☐ Pass / ☐ Fail |
| 8 | ISSM | ☐ | ☐ | ☐ Pass / ☐ Fail |
| 9 | ISSO | ☐ | ☐ | ☐ Pass / ☐ Fail |
| 10 | ISSO | ☐ | ☐ | ☐ Pass / ☐ Fail |
| 11 | ISSM | ☐ | ☐ | ☐ Pass / ☐ Fail |
| 12 | ISSM | ☐ | ☐ | ☐ Pass / ☐ Fail |
| 13 | ISSO | ☐ | ☐ | ☐ Pass / ☐ Fail |
| 14 | ISSM | ☐ | ☐ | ☐ Pass / ☐ Fail |
| 15 | SCA | ☐ | ☐ | ☐ Pass / ☐ Fail |
| 16 | SCA | ☐ | ☐ | ☐ Pass / ☐ Fail |
| 17 | SCA | ☐ | ☐ | ☐ Pass / ☐ Fail |
| 18 | AO | ☐ | ☐ | ☐ Pass / ☐ Fail |
| 19 | ISSM | ☐ | ☐ | ☐ Pass / ☐ Fail |
| 20 | Engineer | ☐ | ☐ | ☐ Pass / ☐ Fail |

**Persona Transitions**: 10 handoffs → All successful: ☐ Yes / ☐ No
**Scenario 4 Status**: ☐ PASS / ☐ FAIL | ___/20 steps passed

---

## Overall Cross-Persona & Edge Cases Summary

| Section | Total | Passed | Failed | Blocked |
|---------|-------|--------|--------|---------|
| Error Handling (ERR-01–08) | 8 | ___/8 | ___/8 | ___/8 |
| Auth/PIM (AUTH-01–08) | 8 | ___/8 | ___/8 | ___/8 |
| Scenario 1: Full RMF | 17 steps | ___/17 | ___/17 | ___/17 |
| Scenario 2: Prisma Flow | 13 steps | ___/13 | ___/13 | ___/13 |
| Scenario 3: ConMon Drift | 10 steps | ___/10 | ___/10 | ___/10 |
| Scenario 4: Privacy/SSP/OSCAL | 20 steps | ___/20 | ___/20 | ___/20 |
| **Total** | **76** | **___/76** | **___/76** | **___/76** |

### Issues Found

| # | Section | TC-ID/Step | Issue Description | Severity | Status |
|---|---------|-----------|------------------|----------|--------|
| 1 | | | | | |
| 2 | | | | | |
| 3 | | | | | |

**Cross-Persona & Edge Cases Status**: ☐ PASS / ☐ FAIL | **Tester**: __________ | **Date**: __________
