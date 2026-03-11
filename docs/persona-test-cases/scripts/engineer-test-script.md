# Engineer Persona Test Execution Script

**Feature**: 020 | **Persona**: Engineer (Platform Engineer)
**Role**: `Compliance.PlatformEngineer` (default for CAC-authenticated users)
**Interface**: VS Code (`@ato` chat participant)
**Test Cases**: ENG-01 through ENG-30 (30 total)

---

## Pre-Execution Setup

### T029 — Role Activation

1. **Deactivate AO role** (if active): `Deactivate my AuthorizingOfficial role`
2. **Activate Engineer role**: `Activate my Compliance.PlatformEngineer role for 4 hours — persona test suite`
   - *Note*: Engineer role is the default CAC mapping. PIM activation may not be required if already mapped via CAC certificate.
3. **Verify role**: `Show my active PIM roles` → Confirm `Compliance.PlatformEngineer` is active (or CAC-mapped)
4. **Switch to VS Code**: Open VS Code, ensure `@ato` chat participant is available in Copilot Chat panel

### Preconditions from Prior Phases

- ✓ Eagle Eye system registered and in Monitor phase (ISSM)
- ✓ Moderate baseline selected with 325 controls (ISSM-11)
- ✓ Inheritance set for common controls (ISSM-14)
- ✓ SSP narratives authored at 100% progress (ISSO)
- ✓ CKL + XCCDF + Prisma scans imported (ISSO/ISSM)
- ✓ Kanban board created with remediation tasks (ISSM-26)
- ✓ Tasks assigned to engineer (ISSO/ISSM)
- ✓ Findings exist from assessments and scans (SCA/ISSM)
- ✓ ATO issued (AO-04) — system in Monitor phase
- ✓ PTA created (ISSM-44) and PIA generated (ISSM-46)
- ✓ Interconnection registered (ISSM-48) with ISA generated (ISSM-52)

---

## Phase 3 — Implement: Build & Configure (ENG-01 to ENG-10)

### ENG-01: Learn About a Control

**Task**: Get control description and Azure-specific implementation guidance
**Type**: Positive test | **Precondition**: None

```text
@ato What does AC-2 mean for Azure?
```

**Expected Tool**: `compliance_get_control_family` / knowledge tools
**Expected Output**:
- AC-2 (Account Management) control description
- Azure-specific implementation guidance
- Related STIG rules

**Verification**: Response contains Azure-specific implementation details (e.g., Azure AD, RBAC, Conditional Access)

---

### ENG-02: View STIG Mappings

**Task**: View STIG rules for a specific benchmark
**Type**: Positive test | **Precondition**: STIG data loaded

```text
@ato What STIG rules apply to Windows Server 2022?
```

**Expected Tool**: `compliance_show_stig_mapping`
**Expected Output**:
- STIG rules for Windows Server 2022 benchmark
- Each rule: VulnId, RuleId, severity (CAT I/II/III), NIST control mapping

**Verification**: Rules returned with severity classifications and NIST control cross-references

---

### ENG-03: Scan IaC for Compliance

**Task**: Scan a Bicep infrastructure-as-code file for compliance issues
**Type**: Positive test | **Precondition**: Bicep file open in VS Code editor

```text
@ato Scan my Bicep file for compliance issues
```

**Expected Tool**: IaC diagnostics (in-editor)
**Expected Output**:
- Squiggly underlines in editor:
  - CAT I / CAT II → Error (red underline)
  - CAT III → Warning (yellow underline)
- Hover reveals: NIST control ID, STIG rule, remediation guidance

**Verification**: At least one diagnostic appears; hover tooltip contains NIST control reference

**Record**: Number of diagnostics: _____ (CAT I: ___, CAT II: ___, CAT III: ___)

---

### ENG-04: Suggest a Narrative

**Task**: Get an AI-generated narrative suggestion for a control
**Type**: Positive test | **Precondition**: System with baseline

```text
@ato Suggest a narrative for SC-7 on Eagle Eye
```

**Expected Tool**: `compliance_suggest_narrative`
**Expected Output**:
- AI-generated narrative draft text
- Confidence score (0-100%)
- Reference sources used for generation

**Verification**: Narrative is relevant to SC-7 (Boundary Protection) and references Azure networking concepts

---

### ENG-05: Write a Narrative

**Task**: Save an implementation narrative for a control
**Type**: Positive test | **Precondition**: System in Implement phase (or later)

```text
@ato Write narrative for SC-7 on Eagle Eye: Network boundary protection is implemented via Azure Firewall Premium with IDPS, NSG micro-segmentation, and Azure Front Door WAF
```

**Expected Tool**: `compliance_write_narrative`
**Expected Output**:
- Narrative saved confirmation
- Status = "Implemented"
- Control ID: SC-7
- System: Eagle Eye

**Verification**: Narrative text matches input; status shows Implemented

---

### ENG-06: Generate Remediation Plan

**Task**: Create a prioritized remediation plan for a subscription
**Type**: Positive test | **Precondition**: Findings exist from scans/assessments

```text
@ato Generate a remediation plan for subscription sub-12345-abcde
```

**Expected Tool**: `compliance_generate_plan`
**Expected Output**:
- Prioritized list of findings sorted by severity (CAT I → CAT II → CAT III)
- Each finding: description, affected resources, remediation steps
- Estimated effort per finding

**Verification**: Plan is ordered by severity; remediation steps are actionable

**Record**: Plan ID: __________ | Total findings: ____

---

### ENG-07: Remediate with Dry Run

**Task**: Preview remediation changes without applying them
**Type**: Positive test | **Precondition**: Finding exists (use finding_id from ENG-06 plan)

```text
@ato Remediate finding {finding_id} with dry run
```

> ⚠️ Replace `{finding_id}` with actual finding ID from ENG-06 output

**Expected Tool**: `compliance_remediate`
**Expected Output**:
- Dry run preview (no changes applied)
- What would change: resource modifications listed
- Affected resources: count and identifiers
- Estimated impact assessment

**Verification**: Response explicitly states "dry run" / "no changes applied"; preview details are present

**Record**: Finding ID used: __________

---

### ENG-08: Apply Remediation

**Task**: Execute the remediation to fix a finding
**Type**: Positive test | **Precondition**: ENG-07 dry run reviewed and approved

```text
@ato Apply remediation for finding {finding_id}
```

> ⚠️ Use the same `{finding_id}` from ENG-07

**Expected Tool**: `compliance_remediate`
**Expected Output**:
- Remediation executed confirmation
- Resource changes applied (list of modifications)
- Finding status updated (Open → Remediated or similar)

**Verification**: Finding status changed; resource changes reflect what dry run previewed

---

### ENG-09: Validate Remediation

**Task**: Re-scan to verify the finding is resolved
**Type**: Positive test | **Precondition**: ENG-08 remediation applied

```text
@ato Validate remediation for finding {finding_id}
```

> ⚠️ Use the same `{finding_id}` from ENG-07/ENG-08

**Expected Tool**: `compliance_validate_remediation`
**Expected Output**:
- Re-scan result: **Pass** (finding resolved) or **Fail** (finding persists with details)
- If Pass: finding status = Resolved
- If Fail: remaining issues described with guidance

**Verification**: Validation result is either Pass or Fail with clear details

**Record**: Validation result: ☐ Pass / ☐ Fail

---

### ENG-10: Check Narrative Progress

**Task**: View narrative authoring progress for a control family
**Type**: Positive test | **Precondition**: Narratives partially authored (from ISSO + ENG-05)

```text
@ato Show narrative progress for the SC family on Eagle Eye
```

**Expected Tool**: `compliance_narrative_progress`
**Expected Output**:
- SC family statistics:
  - Total controls in family
  - Completed narratives
  - Draft narratives
  - Missing narratives

**Verification**: SC-7 shows as completed (from ENG-05); overall progress reflects ISSO authoring

**Record**: SC family: Total ___ | Completed ___ | Draft ___ | Missing ___

---

## Kanban Task Workflow (ENG-11 to ENG-19)

### ENG-11: View Assigned Tasks

**Task**: See all remediation tasks assigned to the current user
**Type**: Positive test | **Precondition**: Tasks assigned by ISSO/ISSM via Kanban board

```text
@ato Show my assigned remediation tasks
```

**Expected Tool**: `kanban_task_list`
**Expected Output**:
- Task list filtered to current user
- Each task: severity, control ID, status, due date
- Task IDs in format REM-{id}

**Verification**: At least one task appears; tasks have severity and control mappings

**Record**: Task count: ____ | First task ID (REM-___): __________

---

### ENG-12: Get Task Details

**Task**: View full details of a specific remediation task
**Type**: Positive test | **Precondition**: Task exists (use REM-{id} from ENG-11)

```text
@ato Show details of task REM-{id}
```

> ⚠️ Replace `REM-{id}` with actual task ID from ENG-11

**Expected Tool**: `kanban_get_task`
**Expected Output**:
- Full task details:
  - Control ID (e.g., AC-2, SC-7)
  - Finding details (description, severity)
  - Affected resources
  - Remediation script (if available)
  - SLA / due date

**Verification**: All detail fields populated; control ID matches finding

**Record**: Task REM-___: Control = _____, Severity = _____, SLA = _____

---

### ENG-13: Move Task to In Progress

**Task**: Start working on a remediation task
**Type**: Positive test | **Precondition**: Task in ToDo status

```text
@ato Move task REM-{id} to In Progress
```

> ⚠️ Use the same task ID from ENG-12

**Expected Tool**: `kanban_move_task`
**Expected Output**:
- Status changed: ToDo → InProgress
- Auto-assigns to current user if unassigned
- Timestamp recorded for start time

**Verification**: Status is InProgress; assignment confirmed

---

### ENG-14: Fix with Kanban Dry Run

**Task**: Preview remediation scoped to the task's finding
**Type**: Positive test | **Precondition**: Task in InProgress (from ENG-13)

```text
@ato Fix task REM-{id} with dry run
```

> ⚠️ Use the same task ID

**Expected Tool**: `kanban_remediate_task`
**Expected Output**:
- Dry run remediation preview
- Scoped to task's specific finding and affected resources
- Changes that would be applied
- No actual modifications made

**Verification**: Preview is scoped to the correct finding; "dry run" explicitly stated

---

### ENG-15: Apply Kanban Remediation

**Task**: Execute the fix for a Kanban task
**Type**: Positive test | **Precondition**: ENG-14 dry run reviewed

```text
@ato Apply fix for task REM-{id}
```

> ⚠️ Use the same task ID

**Expected Tool**: `kanban_remediate_task`
**Expected Output**:
- Remediation applied to cloud resources
- Task finding status updated
- Validation queued automatically

**Verification**: Fix applied; task finding reflects remediation

---

### ENG-16: Validate Task

**Task**: Run validation scan on a remediated task
**Type**: Positive test | **Precondition**: ENG-15 remediation applied

```text
@ato Validate task REM-{id}
```

> ⚠️ Use the same task ID

**Expected Tool**: `kanban_task_validate`
**Expected Output**:
- Re-scan result:
  - **Pass** → Validation confirmed; finding resolved
  - **Fail** → Details of remaining issues

**Verification**: Clear Pass or Fail result with supporting details

**Record**: Validation result: ☐ Pass / ☐ Fail

---

### ENG-17: Collect Evidence for Task

**Task**: Gather evidence artifact for a remediated task
**Type**: Positive test | **Precondition**: ENG-16 validation passed

```text
@ato Collect evidence for task REM-{id}
```

> ⚠️ Use the same task ID

**Expected Tool**: `kanban_collect_evidence`
**Expected Output**:
- Evidence collected with SHA-256 hash for integrity
- Evidence linked to both task and finding
- Timestamp and collector identity recorded

**Verification**: SHA-256 hash present; evidence linked to correct task ID

**Record**: Evidence hash (first 8 chars): __________

---

### ENG-18: Add Comment to Task

**Task**: Document remediation status on a task
**Type**: Positive test | **Precondition**: Task exists

```text
@ato Add comment on task REM-{id}: Remediation applied, waiting for DNS propagation before final validation
```

> ⚠️ Use the same task ID

**Expected Tool**: `kanban_add_comment`
**Expected Output**:
- Comment saved with timestamp and author
- Visible to ISSO for review
- Comment text preserved accurately

**Verification**: Comment appears with correct text, auto-populated timestamp and author

---

### ENG-19: Move Task to In Review

**Task**: Move completed task for ISSO review
**Type**: Positive test | **Precondition**: Validation passed (ENG-16)

```text
@ato Move task REM-{id} to In Review
```

> ⚠️ Use the same task ID

**Expected Tool**: `kanban_move_task`
**Expected Output**:
- Status changed: InProgress → InReview
- Triggers automatic validation scan
- ISSO notified of task ready for review

**Verification**: Status is InReview; notification sent

**Record**: Task REM-___ status: ☐ InReview confirmed

---

## Prisma Remediation Workflow (ENG-20 to ENG-22)

### ENG-20: View Prisma Findings with Remediation Steps

**Task**: See open Prisma Cloud findings with actionable remediation guidance
**Type**: Positive test | **Precondition**: Prisma import completed (ISSM-19/20)

```text
@ato Show open Prisma Cloud findings for Eagle Eye with remediation steps
```

**Expected Tool**: `watch_show_alerts` / findings query
**Expected Output**:
- Prisma-sourced findings listed
- Each finding includes:
  - RemediationGuidance (descriptive steps)
  - RemediationScript (CLI/IaC code if available)
  - AutoRemediable flag (true/false)
  - PrismaAlertId, CloudResourceType

**Verification**: At least one finding has Prisma-specific fields populated (PrismaAlertId ≠ null)

**Record**: Prisma findings count: ____ | Auto-remediable: ____

---

### ENG-21: View Prisma CLI Scripts

**Task**: List findings with CLI remediation scripts from API imports
**Type**: Positive test | **Precondition**: Prisma API import completed (ISSM-20)

```text
@ato What CLI scripts are available for Eagle Eye Prisma findings?
```

**Expected Tool**: Findings query
**Expected Output**:
- Findings with `RemediationCli` populated
- CLI commands (Azure CLI, PowerShell, etc.)
- Script source: Prisma API JSON import

**Verification**: At least one finding has RemediationCli with executable CLI commands

---

### ENG-22: Prisma Trend by Resource Type

**Task**: View Prisma findings trend grouped by Azure resource type
**Type**: Positive test | **Precondition**: Multiple Prisma imports completed (ISSM-19, ISSM-20, ISSM-40)

```text
@ato Show Prisma trend for Eagle Eye grouped by resource type
```

**Expected Tool**: `compliance_prisma_trend`
**Expected Output**:
- Trend data grouped by resource type (e.g., `Microsoft.Storage/storageAccounts`, `Microsoft.Compute/virtualMachines`)
- Per resource type: open/closed/total findings over time
- Targeted remediation priority guidance

**Verification**: Multiple resource types shown; trend data spans multiple import dates

---

## CKL Export, Interconnections & SSP Authoring (ENG-27 to ENG-30)

### ENG-27: Export CKL for STIG Evidence

**Task**: Export a DISA CKL file after STIG-based remediation
**Type**: Positive test | **Precondition**: STIG data imported (ISSO-06), findings remediated (ENG-08)

```text
@ato Export a CKL file for Eagle Eye Windows Server 2022 STIG
```

**Expected Tool**: `compliance_export_ckl`
**Expected Output**:
- CKL XML file generated
- Per-rule status: Open / NotAFinding / Not_Applicable
- Benchmark and version info populated
- Download link or inline preview

**Verification**: CKL contains Windows Server 2022 STIG rules; remediated findings show as NotAFinding

**Record**: CKL filename: __________ | Rules: Open ___ / NotAFinding ___ / N/A ___

---

### ENG-28: Register an Interconnection

**Task**: Register a system interconnection from the engineering perspective
**Type**: Positive test | **Precondition**: System registered, Engineer role active

```text
@ato Add an interconnection for Eagle Eye — outbound HTTPS to Azure
DevOps (dev.azure.com) for CI/CD pipeline integration, port 443
```

**Expected Tool**: `compliance_add_interconnection`
**Expected Output**:
- Interconnection record created
- Direction: Outbound
- Protocol: HTTPS, port 443
- Remote system: Azure DevOps
- Status: Registered (pending ISA)

**Verification**: Interconnection ID returned; direction and protocol correct

**Record**: Interconnection ID: __________

---

### ENG-29: Write SSP Technical Section

**Task**: Contribute to SSP §6 (Technical Controls) as the platform engineer
**Type**: Positive test | **Precondition**: System in Implement phase or later

```text
@ato Write SSP section 6 for Eagle Eye: Technical controls are
implemented using Azure Policy, Microsoft Defender for Cloud, NSG
micro-segmentation, and Azure Key Vault for secrets management.
All configurations are enforced via Bicep IaC templates.
```

**Expected Tool**: `compliance_write_ssp_section`
**Expected Output**:
- SSP §6 content saved
- Section status: Draft (pending ISSM review)
- Author: current Engineer identity

**Verification**: Section 6 saved with correct content; status is Draft

---

### ENG-30: Check SSP Completion Status

**Task**: Verify SSP completion status to see remaining work
**Type**: Positive test | **Precondition**: SSP sections partially authored

```text
@ato Show SSP completeness for Eagle Eye
```

**Expected Tool**: `compliance_ssp_completeness`
**Expected Output**:
- Overall completion percentage
- Per-section status (complete / draft / missing)
- §6 shows as Draft (from ENG-29)

**Verification**: Completion percentage returned; §6 reflects recent authoring

**Record**: SSP completion: ____%

---

## Engineer Separation-of-Duties Verification (ENG-23 to ENG-26)

> **Purpose**: Confirm that Engineers CANNOT perform actions reserved for other personas.
> All 4 tests MUST return **403 Forbidden**. Any other response (200, 404, 500) is a **FAIL**.

### ENG-23: DENIED — Assess Control

**Task**: Attempt to record an assessment (SCA-only action)
**Type**: RBAC denial test | **Precondition**: Engineer role active

```text
@ato Assess AC-2 as Satisfied
```

**Expected Tool**: `compliance_assess_control`
**Expected Response**: **403 Forbidden** — only SCA (`Compliance.Auditor`) can record assessments

**Verification**:
- [ ] HTTP 403 returned (not 200, 404, or 500)
- [ ] Error message indicates insufficient role/permissions
- [ ] No assessment record created

---

### ENG-24: DENIED — Issue Authorization

**Task**: Attempt to issue an ATO (AO-only action)
**Type**: RBAC denial test | **Precondition**: Engineer role active

```text
@ato Issue ATO for Eagle Eye
```

**Expected Tool**: `compliance_issue_authorization`
**Expected Response**: **403 Forbidden** — only AO (`Compliance.AuthorizingOfficial`) can issue authorization

**Verification**:
- [ ] HTTP 403 returned (not 200, 404, or 500)
- [ ] Error message indicates insufficient role/permissions
- [ ] No authorization decision recorded

---

### ENG-25: DENIED — Dismiss Alert

**Task**: Attempt to dismiss a monitoring alert (ISSM-only action)
**Type**: RBAC denial test | **Precondition**: Engineer role active

```text
@ato Dismiss alert ALT-{id}
```

> ⚠️ Use an actual alert ID from the monitoring alerts

**Expected Tool**: `watch_dismiss_alert`
**Expected Response**: **403 Forbidden** — only ISSM (`Compliance.SecurityLead`) can dismiss alerts

**Verification**:
- [ ] HTTP 403 returned (not 200, 404, or 500)
- [ ] Error message indicates insufficient role/permissions
- [ ] Alert remains active/acknowledged

---

### ENG-26: DENIED — Register System

**Task**: Attempt to register a new system (ISSM-only action)
**Type**: RBAC denial test | **Precondition**: Engineer role active

```text
@ato Register a new system called Test
```

**Expected Tool**: `compliance_register_system`
**Expected Response**: **403 Forbidden** — only ISSM (`Compliance.SecurityLead`) can register systems

**Verification**:
- [ ] HTTP 403 returned (not 200, 404, or 500)
- [ ] Error message indicates insufficient role/permissions
- [ ] No system "Test" created

---

## RBAC Verification Matrix

| TC-ID | Action | Required Role | Engineer Has? | Expected Result |
|-------|--------|---------------|---------------|-----------------|
| ENG-23 | Assess control | Compliance.Auditor | ✗ | 403 Forbidden |
| ENG-24 | Issue authorization | Compliance.AuthorizingOfficial | ✗ | 403 Forbidden |
| ENG-25 | Dismiss alert | Compliance.SecurityLead | ✗ | 403 Forbidden |
| ENG-26 | Register system | Compliance.SecurityLead | ✗ | 403 Forbidden |

**RBAC Pass Criteria**: All 4 tests return 403 → **☐ PASS** / **☐ FAIL** (___/4 returned 403)

---

## Key Artifacts Tracker

| Artifact | Source Test | Value |
|----------|-----------|-------|
| SC-7 Narrative ID | ENG-05 | __________ |
| Remediation Plan ID | ENG-06 | __________ |
| Finding ID (remediation target) | ENG-07 | __________ |
| Remediation validation result | ENG-09 | ☐ Pass / ☐ Fail |
| Task ID (Kanban workflow) | ENG-11 | REM-__________ |
| Task validation result | ENG-16 | ☐ Pass / ☐ Fail |
| Evidence hash | ENG-17 | __________ |
| Prisma findings count | ENG-20 | __________ |
| Auto-remediable count | ENG-20 | __________ |
| CKL filename | ENG-27 | __________ |
| Interconnection ID | ENG-28 | __________ |
| SSP completion | ENG-30 | ____% |

---

## Results Summary

### T034 — Engineer Results

| Metric | Value |
|--------|-------|
| **Total Test Cases** | 30 |
| **Passed** | ___/30 |
| **Failed** | ___/30 |
| **Blocked** | ___/30 |
| **Skipped** | ___/30 |
| **RBAC Denials (403)** | ___/4 |
| **Avg Response Time** | ____s |

### Issues Found

| # | TC-ID | Issue Description | Severity | Status |
|---|-------|------------------|----------|--------|
| 1 | | | | |
| 2 | | | | |
| 3 | | | | |

### Phase Completion

- [ ] All 10 Implement tests executed (ENG-01 to ENG-10)
- [ ] All 9 Kanban workflow tests executed (ENG-11 to ENG-19)
- [ ] All 3 Prisma remediation tests executed (ENG-20 to ENG-22)
- [ ] All 4 CKL/Interconnection/SSP tests executed (ENG-27 to ENG-30)
- [ ] All 4 RBAC denial tests verified as 403 (ENG-23 to ENG-26)
- [ ] Key artifacts tracker completed
- [ ] Results summary filled
- [ ] Issues documented

**Engineer Section Status**: ☐ PASS / ☐ FAIL | **Tester**: __________ | **Date**: __________
