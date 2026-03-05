# Feature Spec: Persona End-to-End Test Cases

**Created**: 2026-03-05
**Status**: Draft
**Purpose**: Define end-to-end test scenarios for each RMF persona, showing the natural language input, tool(s) invoked, and expected output for every major workflow.

---

## Clarifications

### Session 2026-03-05

- Q: What execution type are these test cases? → A: Manual test scripts — human tester types NL input into `@ato` or Teams and visually verifies output matches expectations.
- Q: Should test data share a single system or isolate per persona? → A: Single cumulative system — one Eagle Eye instance progresses through all 5 personas in sequence; each persona picks up where the prior left off.
- Q: Should SAP generation test cases (Feature 018) be included? → A: Yes, add 4-6 SAP workflow test cases split between SCA and ISSM persona sections.
- Q: Should error/edge-case test cases be included? → A: Yes, add 5-8 error handling test cases covering malformed input, out-of-order RMF advancement, duplicate operations, and missing prerequisite errors.

---

## Overview

These test cases are **manual test scripts** designed for a human tester or demo facilitator. The tester types each natural language input literally into the `@ato` VS Code chat participant or Microsoft Teams bot, then verifies the AI resolves the correct tool and the output matches the expected fields. Each test case specifies:

| Field | Description |
|-------|-------------|
| **TC-ID** | Unique test case identifier (`{Persona}-{##}`) |
| **Persona** | ISSM, ISSO, SCA, AO, or Engineer |
| **RBAC Role** | The `ComplianceRoles` constant required |
| **RMF Phase** | Which RMF step the task belongs to |
| **Task / Job** | What the persona is trying to accomplish |
| **Natural Language Input** | The exact query the user types |
| **Tool(s) Invoked** | MCP tool(s) the AI should call |
| **Expected Output** | Key fields / behavior in the response |
| **Preconditions** | What must exist before this test runs |

### Execution Order

Tests use a **single cumulative system** ("Eagle Eye") that accumulates state across all persona sections. The mandatory execution order is:

1. **ISSM** (Prepare → Categorize → Select → Implement → Authorize → Monitor)
2. **ISSO** (Implement SSP authoring → Monitor operations)
3. **SCA** (Assess)
4. **AO** (Authorize)
5. **Engineer** (Implement remediation → Kanban tasks)

Later tests depend on artifacts created by earlier tests. Cross-persona handoffs are marked with `→ Handoff` to show where one persona's output becomes another's input.

### Test Data Constants

| Constant | Value |
|----------|-------|
| `SYSTEM_NAME` | "Eagle Eye" |
| `SYSTEM_TYPE` | "Major Application" |
| `ENVIRONMENT` | "Azure Government" |
| `BASELINE` | "Moderate" (325 controls) |
| `SUBSCRIPTION_ID` | "sub-12345-abcde" |
| `ENGINEER_NAME` | "SSgt Rodriguez" |
| `ISSO_NAME` | "Jane Smith" |
| `SCA_NAME` | "Bob Jones" |
| `AO_NAME` | "Col. Thompson" |

---

## Persona 1: ISSM (Information System Security Manager)

**RBAC Role**: `Compliance.SecurityLead`
**Primary Interface**: Microsoft Teams
**Primary RMF Phases**: Prepare, Categorize, Select, Authorize, Monitor
**Responsibility**: Manages the security program; owns the authorization package; only role that can advance RMF phases

### Phase 0 — Prepare

| TC-ID | Task / Job | Natural Language Input | Tool(s) Invoked | Expected Output | Preconditions |
|-------|-----------|----------------------|-----------------|-----------------|---------------|
| ISSM-01 | Register a new system | "Register a new system called 'Eagle Eye' as a Major Application with mission-critical designation in Azure Government" | `compliance_register_system` | Returns `system_id` (GUID), name="Eagle Eye", type="MajorApplication", environment="AzureGovernment", RMF step="Prepare" | None |
| ISSM-02 | Define authorization boundary | "Define the authorization boundary for Eagle Eye — add the production VMs, SQL database, and Key Vault in subscription sub-12345-abcde" | `compliance_define_boundary` | Returns boundary with resource list, subscription linked, resource count ≥ 3 | ISSM-01 |
| ISSM-03 | Exclude a resource from boundary | "Exclude the dev Key Vault from Eagle Eye's boundary — it's in a separate authorization" | `compliance_exclude_from_boundary` | Confirms resource excluded, boundary resource count decremented | ISSM-02 |
| ISSM-04 | Assign RMF roles | "Assign Jane Smith as ISSO and Bob Jones as SCA for Eagle Eye" | `compliance_assign_rmf_role` (×2) | Two role assignments created with correct role types; listed in `compliance_list_rmf_roles` | ISSM-01 |
| ISSM-05 | List RMF role assignments | "Show all RMF role assignments for Eagle Eye" | `compliance_list_rmf_roles` | Returns ≥ 2 assignments: ISSO (Jane Smith), SCA (Bob Jones) | ISSM-04 |
| ISSM-06 | Advance to Categorize | "Advance Eagle Eye to the Categorize phase" | `compliance_advance_rmf_step` | RMF step changes from "Prepare" to "Categorize" | ISSM-01 through ISSM-04 |

### Phase 1 — Categorize

| TC-ID | Task / Job | Natural Language Input | Tool(s) Invoked | Expected Output | Preconditions |
|-------|-----------|----------------------|-----------------|-----------------|---------------|
| ISSM-07 | Suggest information types | "Suggest information types for Eagle Eye — it's a mission planning system" | `compliance_suggest_info_types` | Returns SP 800-60 info type suggestions with C/I/A impact levels | ISSM-06 |
| ISSM-08 | Categorize the system | "Categorize Eagle Eye as Moderate confidentiality, Moderate integrity, Low availability with info types: Mission Operations (C:Mod, I:Mod, A:Low)" | `compliance_categorize_system` | FIPS 199 categorization saved; overall impact = Moderate (high-water mark); C=Moderate, I=Moderate, A=Low | ISSM-06 |
| ISSM-09 | View categorization | "Show the categorization for Eagle Eye" | `compliance_get_categorization` | Returns FIPS 199 notation, C/I/A impacts, information types, overall impact level | ISSM-08 |
| ISSM-10 | Advance to Select | "Advance Eagle Eye to the Select phase" | `compliance_advance_rmf_step` | RMF step changes to "Select" | ISSM-08 |

### Phase 2 — Select

| TC-ID | Task / Job | Natural Language Input | Tool(s) Invoked | Expected Output | Preconditions |
|-------|-----------|----------------------|-----------------|-----------------|---------------|
| ISSM-11 | Select baseline | "Select the Moderate baseline for Eagle Eye" | `compliance_select_baseline` | Baseline applied with 325 controls; baseline level = "Moderate" | ISSM-10 |
| ISSM-12 | Tailor baseline | "Remove control PE-1 from Eagle Eye's baseline — physical security is inherited from the data center" | `compliance_tailor_baseline` | Tailoring record created with action="Remove", rationale captured, control count = 324 | ISSM-11 |
| ISSM-13 | Set inheritance | "Set AC-1 through AC-4 as inherited from Azure Government FedRAMP High for Eagle Eye" | `compliance_set_inheritance` | Inheritance records created for AC-1, AC-2, AC-3, AC-4 with provider="Azure Government" | ISSM-11 |
| ISSM-14 | Generate CRM | "Generate the Customer Responsibility Matrix for Eagle Eye" | `compliance_generate_crm` | CRM document with inherited/shared/customer columns per control; counts match inheritance settings | ISSM-13 |
| ISSM-15 | View baseline | "Show the baseline details for Eagle Eye" | `compliance_get_baseline` | Returns baseline level, total controls, tailored count, inherited count, overlay info | ISSM-11 |
| ISSM-16 | Advance to Implement | "Move Eagle Eye to the Implement phase" | `compliance_advance_rmf_step` | RMF step changes to "Implement" | ISSM-11 |

### Phase 3 — Implement (Oversight)

| TC-ID | Task / Job | Natural Language Input | Tool(s) Invoked | Expected Output | Preconditions |
|-------|-----------|----------------------|-----------------|-----------------|---------------|
| ISSM-17 | Check SSP progress | "What's the SSP completion percentage for Eagle Eye?" | `compliance_narrative_progress` | Returns overall %, per-family breakdown; initially low before ISSO authoring | ISSM-16 |
| ISSM-18 | Generate SSP | "Generate the SSP for Eagle Eye" | `compliance_generate_ssp` | Markdown SSP with System Information, Categorization, Baseline, Control Implementations; warnings for missing narratives | ISSM-16 + narratives authored |
| ISSM-19 | Import Prisma CSV scan | "Import this Prisma Cloud CSV scan for Eagle Eye" | `compliance_import_prisma_csv` | Import record created; findings created with Prisma-specific fields; NIST controls mapped; effectiveness records upserted | ISSM-16 |
| ISSM-20 | Import Prisma API scan | "Import Prisma Cloud API scan results for Eagle Eye with auto-resolve subscriptions" | `compliance_import_prisma_api` | Import record with `auto_resolve_subscription: true`; CLI remediation scripts extracted; compliance standards captured | ISSM-16 |
| ISSM-21 | List Prisma policies | "Show all Prisma Cloud policies affecting Eagle Eye" | `compliance_list_prisma_policies` | Policy list with severity, cloud type, NIST mappings, open/resolved counts | ISSM-19 or ISSM-20 |
| ISSM-22 | View Prisma trends | "Show Prisma Cloud compliance trend for Eagle Eye over the last 90 days grouped by severity" | `compliance_prisma_trend` | Trend data with per-period open/resolved/new counts grouped by severity | ISSM-19 + ISSM-20 (multiple imports) |

→ **Handoff to ISSO**: System is in Implement phase; ISSO begins SSP authoring and monitoring

### SAP Generation (Pre-Assessment)

| TC-ID | Task / Job | Natural Language Input | Tool(s) Invoked | Expected Output | Preconditions |
|-------|-----------|----------------------|-----------------|-----------------|---------------|
| ISSM-41 | Generate SAP | "Generate a Security Assessment Plan for Eagle Eye" | `compliance_generate_sap` | SAP document with ~325 control entries (Moderate), assessment objectives, methods (Examine/Interview/Test), STIG benchmark coverage, team placeholder, schedule placeholder; status = Draft | System with baseline selected |
| ISSM-42 | Update SAP | "Update Eagle Eye's SAP — set assessment start date to April 1, add Bob Jones to the assessment team as Lead Assessor, override AC-2 method to Interview" | `compliance_update_sap` | SAP updated: schedule dates set, team member added with role, AC-2 method override recorded with SCA justification | ISSM-41 |
| ISSM-43 | Finalize SAP | "Finalize the Security Assessment Plan for Eagle Eye" | `compliance_finalize_sap` | SAP status → Finalized; SHA-256 content hash generated; SAP is now immutable (subsequent update attempts rejected) | ISSM-42 |

### Phase 4 — Assess (Package Preparation)

| TC-ID | Task / Job | Natural Language Input | Tool(s) Invoked | Expected Output | Preconditions |
|-------|-----------|----------------------|-----------------|-----------------|---------------|
| ISSM-23 | Create POA&M | "Create a POA&M item for finding {finding_id} — scheduled completion in 90 days" | `compliance_create_poam` | POA&M record with finding linked, scheduled completion date, status = "Ongoing" | Findings exist from assessment |
| ISSM-24 | List POA&M items | "Show all POA&M items for Eagle Eye" | `compliance_list_poam` | Returns list with status, severity, scheduled dates, finding references | ISSM-23 |
| ISSM-25 | Generate RAR | "Generate the Risk Assessment Report for Eagle Eye" | `compliance_generate_rar` | RAR document with risk characterization, finding summary, residual risk assessment | Assessment complete |
| ISSM-26 | Create remediation board | "Create a Kanban remediation board from Eagle Eye's assessment" | `kanban_create_board` | Board created with tasks for each open finding; status counts returned | Assessment complete |
| ISSM-27 | Bulk assign tasks | "Assign all CAT I tasks on Eagle Eye's board to SSgt Rodriguez" | `kanban_bulk_update` | Multiple tasks assigned; confirmation with count of updated tasks | ISSM-26 |
| ISSM-28 | Export Kanban to POA&M | "Export Eagle Eye's remediation board as POA&M" | `kanban_export` | POA&M-formatted export with all open tasks, milestones, and responsible parties | ISSM-26 |

### Phase 5 — Authorize (Package Submission)

| TC-ID | Task / Job | Natural Language Input | Tool(s) Invoked | Expected Output | Preconditions |
|-------|-----------|----------------------|-----------------|-----------------|---------------|
| ISSM-29 | Bundle authorization package | "Bundle the authorization package for Eagle Eye" | `compliance_bundle_authorization_package` | Package with SSP + SAR + RAR + POA&M + CRM; completeness check with warnings | SSP, SAR, RAR, POA&M exist |
| ISSM-30 | Advance to Authorize | "Move Eagle Eye to the Authorize phase" | `compliance_advance_rmf_step` | RMF step changes to "Authorize" | ISSM-29 |
| ISSM-31 | View risk register | "Show the risk register for Eagle Eye" | `compliance_show_risk_register` | Risk entries with severity, status, mitigation, residual risk | Assessment complete |

→ **Handoff to AO**: Authorization package submitted for AO review and decision

### Phase 6 — Monitor

| TC-ID | Task / Job | Natural Language Input | Tool(s) Invoked | Expected Output | Preconditions |
|-------|-----------|----------------------|-----------------|-----------------|---------------|
| ISSM-32 | Create ConMon plan | "Create a continuous monitoring plan for Eagle Eye with monthly assessments and quarterly reviews" | `compliance_create_conmon_plan` | ConMon plan created with frequency, review dates, stakeholder list | ATO granted |
| ISSM-33 | Generate ConMon report | "Generate the monthly ConMon report for Eagle Eye" | `compliance_generate_conmon_report` | Report with compliance score, baseline delta, finding trends, POA&M status | ISSM-32 |
| ISSM-34 | Track ATO expiration | "When does Eagle Eye's ATO expire?" | `compliance_track_ato_expiration` | Alert level (None/Info/Warning/Urgent/Expired), days remaining, recommended action | ATO granted |
| ISSM-35 | Report significant change | "Report a significant change for Eagle Eye — new interconnection with DISA SIPR gateway" | `compliance_report_significant_change` | Change recorded; `requires_reauthorization = true` for "New Interconnection" type | ATO granted |
| ISSM-36 | Check reauthorization triggers | "Check if Eagle Eye needs reauthorization" | `compliance_reauthorization_workflow` | Returns triggers: expiration status, unreviewed significant changes, compliance drift | ATO granted |
| ISSM-37 | Multi-system dashboard | "Show the multi-system compliance dashboard" | `compliance_multi_system_dashboard` | Portfolio view with all systems: RMF step, auth status, compliance score, open findings, expiration | ≥ 1 system registered |
| ISSM-38 | Export to eMASS | "Export Eagle Eye to eMASS format" | `compliance_export_emass` | eMASS-compatible Excel workbook with system, controls, findings, POA&M sheets | System with baseline + assessment |
| ISSM-39 | View audit log | "Show the audit log for Eagle Eye" | `compliance_audit_log` | Chronological audit trail with user, action, timestamp, entity | Any actions performed |
| ISSM-40 | Re-import Prisma after remediation | "Import the latest Prisma Cloud scan for Eagle Eye to verify remediation" | `compliance_import_prisma_csv` | New import record; previously open findings now resolved; trend shows improvement | ISSM-19 + remediation completed |

---

## Persona 2: ISSO (Information System Security Officer)

**RBAC Role**: `Compliance.Analyst`
**Primary Interface**: VS Code (`@ato` chat participant)
**Primary RMF Phases**: Implement, Monitor
**Responsibility**: Day-to-day SSP authoring, control narrative management, monitoring operations, evidence collection

### Phase 3 — Implement (SSP Authoring)

| TC-ID | Task / Job | Natural Language Input | Tool(s) Invoked | Expected Output | Preconditions |
|-------|-----------|----------------------|-----------------|-----------------|---------------|
| ISSO-01 | Auto-populate inherited narratives | "@ato Auto-populate the inherited control narratives for Eagle Eye" | `compliance_batch_populate_narratives` | Narratives auto-filled for all inherited controls; count of populated vs. skipped; idempotent on re-run | System in Implement phase with inheritance set |
| ISSO-02 | Check narrative progress | "@ato Show narrative progress for Eagle Eye" | `compliance_narrative_progress` | Overall completion %, per-family breakdown (total, completed, draft, missing) | ISSO-01 |
| ISSO-03 | Get AI narrative suggestion | "@ato Suggest a narrative for AC-2 on Eagle Eye" | `compliance_suggest_narrative` | Suggested text, confidence score (0.55 for customer control), reference sources | System with baseline |
| ISSO-04 | Write a control narrative | "@ato Write narrative for AC-2 on Eagle Eye: Account management is implemented using Azure Active Directory with automated provisioning via SCIM, quarterly access reviews, and 15-minute session timeouts" | `compliance_write_narrative` | Narrative saved with status="Implemented"; upsert behavior on re-call | System in Implement phase |
| ISSO-05 | Update narrative to partial | "@ato Update AC-3 narrative on Eagle Eye to PartiallyImplemented: Access enforcement is configured via Azure RBAC, ABAC policies pending deployment" | `compliance_write_narrative` | Status updated to "PartiallyImplemented"; narrative text updated | ISSO-04 pattern |
| ISSO-06 | Filter progress by family | "@ato Show narrative progress for the AC family on Eagle Eye" | `compliance_narrative_progress` | AC family stats only: total, completed (Implemented + N/A), draft (Partial + Planned), missing | ISSO-01 |
| ISSO-07 | Generate SSP | "@ato Generate the SSP for Eagle Eye" | `compliance_generate_ssp` | Markdown SSP document with 4 sections; warnings array for missing narratives | Narratives substantially complete |
| ISSO-08 | Generate SSP section | "@ato Generate just the system information section of Eagle Eye's SSP" | `compliance_generate_ssp` | Only the System Information section rendered | System registered |
| ISSO-09 | Import CKL checklist | "@ato Import this CKL file for Eagle Eye" | `compliance_import_ckl` | Import record with findings mapped to NIST controls; status counts (Created/Updated/Skipped/Unmatched) | System with baseline |
| ISSO-10 | Import XCCDF results | "@ato Import SCAP scan results for Eagle Eye" | `compliance_import_xccdf` | Import record with XCCDF benchmark scores; rule results mapped to controls | System with baseline |
| ISSO-11 | View import history | "@ato Show import history for Eagle Eye" | `compliance_list_imports` | Paginated list with import type, date, benchmark, finding counts, status | ISSO-09 or ISSO-10 |
| ISSO-12 | View import details | "@ato Show details of import {import_id}" | `compliance_get_import_summary` | Per-finding breakdown with actions, NIST mappings, conflict resolutions | ISSO-09 or ISSO-10 |

→ **Handoff to SCA**: SSP complete; system ready for independent assessment

### Phase 6 — Monitor (Day-to-Day Operations)

| TC-ID | Task / Job | Natural Language Input | Tool(s) Invoked | Expected Output | Preconditions |
|-------|-----------|----------------------|-----------------|-----------------|---------------|
| ISSO-13 | Enable monitoring | "@ato Enable daily monitoring for subscription sub-12345-abcde" | `watch_enable_monitoring` | Monitoring config created; scan frequency = Daily; next scan scheduled | Subscription exists |
| ISSO-14 | View monitoring status | "@ato Show monitoring status for Eagle Eye" | `watch_monitoring_status` | Status: Enabled, frequency, last scan time, next scan time, alert count | ISSO-13 |
| ISSO-15 | Show all alerts | "@ato Show all unacknowledged alerts for Eagle Eye" | `watch_show_alerts` | Alert list with severity, control, resource, timestamp; filtered to unacknowledged | Monitoring active + drift detected |
| ISSO-16 | Get alert details | "@ato Show details of alert ALT-{id}" | `watch_get_alert` | Full alert: severity, control ID, resource, current vs. expected state, remediation suggestion | ISSO-15 |
| ISSO-17 | Acknowledge an alert | "@ato Acknowledge alert ALT-{id} — scheduled for next maintenance window" | `watch_acknowledge_alert` | Alert status → Acknowledged; comment saved; SLA clock noted | ISSO-15 |
| ISSO-18 | Fix an alert | "@ato Fix alert ALT-{id}" | `watch_fix_alert` | Remediation executed; finding status updated; validation result returned | ISSO-15 |
| ISSO-19 | Collect evidence | "@ato Collect evidence for AC-2 on Eagle Eye" | `compliance_collect_evidence` | Evidence record created with SHA-256 hash; Azure resource data captured | System with baseline |
| ISSO-20 | Generate ConMon report | "@ato Generate the February 2026 ConMon report for Eagle Eye" | `compliance_generate_conmon_report` | Monthly report with compliance score, delta, finding trends, POA&M status | ConMon plan exists |
| ISSO-21 | Report significant change | "@ato Report that Eagle Eye added a new API Management gateway" | `compliance_report_significant_change` | Change recorded with type classification; `requires_reauthorization` flag set based on type | ATO granted |
| ISSO-22 | Assign remediation task | "@ato Assign task REM-{id} to SSgt Rodriguez" | `kanban_assign_task` | Task assigned; engineer notified; task status remains current column | Kanban board exists |
| ISSO-23 | View alert history | "@ato Show alert trends for Eagle Eye over the last 30 days" | `watch_alert_history` | NL alert query results with timeline view | Monitoring active |
| ISSO-24 | View compliance trend | "@ato Show compliance score trend for Eagle Eye" | `watch_compliance_trend` | Score progression over time with data points per scan | Monitoring active |

---

## Persona 3: SCA (Security Control Assessor)

**RBAC Role**: `Compliance.Auditor`
**Primary Interface**: Microsoft Teams
**Primary RMF Phases**: Assess (Lead)
**Responsibility**: Independent assessment of control effectiveness; **read-only** — cannot modify SSP, fix findings, or authorize

### Phase 4 — Assess

| TC-ID | Task / Job | Natural Language Input | Tool(s) Invoked | Expected Output | Preconditions |
|-------|-----------|----------------------|-----------------|-----------------|---------------|
| SCA-01 | Take pre-assessment snapshot | "Take an assessment snapshot for Eagle Eye before I begin the assessment" | `compliance_take_snapshot` | Immutable snapshot created with timestamp; snapshot_id returned; captures current control states | System in Assess phase |
| SCA-02 | View system baseline | "Show the baseline for Eagle Eye" | `compliance_get_baseline` | Baseline level, total controls, tailored controls, inheritance summary | System with baseline |
| SCA-03 | View system categorization | "Show Eagle Eye's categorization" | `compliance_get_categorization` | FIPS 199 C/I/A impacts, overall level, information types | System categorized |
| SCA-04 | Check evidence completeness | "Check evidence completeness for the AC family on Eagle Eye" | `compliance_check_evidence_completeness` | Per-control evidence status: controls with evidence, gaps, coverage percentage for AC family | Evidence collected |
| SCA-05 | Verify evidence integrity | "Verify evidence {evidence_id}" | `compliance_verify_evidence` | SHA-256 hash validation result; evidence metadata; collection timestamp; integrity = Pass/Fail | Evidence exists |
| SCA-06 | Assess control — Satisfied | "Assess AC-2 as Satisfied using the Examine method — policy document reviewed, automated provisioning verified, quarterly reviews confirmed" | `compliance_assess_control` | ControlEffectiveness record: determination=Satisfied, method=Examine, notes saved | Assessment exists |
| SCA-07 | Assess control — OtherThanSatisfied | "Assess SI-4 as Other Than Satisfied, CAT II — monitoring is deployed but intrusion detection signatures are 90 days out of date" | `compliance_assess_control` | ControlEffectiveness: determination=OtherThanSatisfied, catSeverity=CATII, notes with gap description | Assessment exists |
| SCA-08 | Assess using Interview method | "Assess CP-2 as Satisfied using the Interview method — ISSO confirmed annual contingency plan testing and updated contact rosters" | `compliance_assess_control` | ControlEffectiveness: method=Interview, notes capture interview summary | Assessment exists |
| SCA-09 | Assess using Test method | "Assess AC-7 as Satisfied using the Test method — verified 3-attempt lockout on all endpoints" | `compliance_assess_control` | ControlEffectiveness: method=Test, notes describe test procedure and result | Assessment exists |
| SCA-10 | View Prisma policies for assessment | "Show Prisma Cloud policies with NIST mappings for Eagle Eye" | `compliance_list_prisma_policies` | Policy list with NIST control mappings; helps SCA validate cloud posture controls | Prisma import completed |
| SCA-11 | Review Prisma trend data | "Show Prisma compliance trend for Eagle Eye to validate remediation progress" | `compliance_prisma_trend` | Trend data showing open/resolved/new counts; validates remediation between imports | Multiple Prisma imports |
| SCA-12 | Compare snapshots | "Compare the pre-assessment snapshot with current state for Eagle Eye" | `compliance_compare_snapshots` | Delta report: controls changed, new findings, resolved findings, effectiveness changes | SCA-01 + assessments |
| SCA-13 | Take post-assessment snapshot | "Take a final assessment snapshot for Eagle Eye" | `compliance_take_snapshot` | Second immutable snapshot with all assessment determinations captured | Assessment substantially complete |
| SCA-14 | Get SAP for assessment | "Show the Security Assessment Plan for Eagle Eye" | `compliance_get_sap` | Returns the finalized SAP with control entries, methods, team, schedule; status = Finalized | SAP finalized by ISSM |
| SCA-15 | List SAPs | "List all SAPs for Eagle Eye" | `compliance_list_saps` | SAP history with status (Draft/Finalized), dates, scope summaries | ≥ 1 SAP exists |
| SCA-16 | Check SAP-SAR alignment | "Check SAP-to-SAR alignment for Eagle Eye" | SAP-SAR alignment query | Alignment report: planned-but-unassessed controls, assessed-but-unplanned controls, coverage percentage | SAP finalized + assessments recorded |
| SCA-17 | Generate SAR | "Generate the Security Assessment Report for Eagle Eye" | `compliance_generate_sar` | SAR document with executive summary, per-control effectiveness, CAT findings, evidence references, Prisma cloud posture data | Assessments recorded |
| SCA-18 | Generate RAR | "Generate the Risk Assessment Report for Eagle Eye" | `compliance_generate_rar` | RAR with risk characterization per finding, aggregate risk assessment, recommended mitigations | Assessment complete |
| SCA-19 | View import summary | "Show Prisma Cloud import details for Eagle Eye's latest import" | `compliance_get_import_summary` | Per-finding import breakdown with PrismaAlertId, CloudResourceType, NIST mappings | Prisma import exists |
| SCA-20 | Run compliance assessment | "Run a NIST 800-53 assessment for Eagle Eye" | `compliance_assess` | Assessment results with per-control pass/fail, compliance score, evidence gaps | System with baseline + evidence |

→ **Handoff to ISSM**: SAR delivered; ISSM creates POA&M for findings and bundles authorization package

### SCA Separation-of-Duties Verification

| TC-ID | Task / Job | Natural Language Input | Tool(s) Invoked | Expected Output | Preconditions |
|-------|-----------|----------------------|-----------------|-----------------|---------------|
| SCA-21 | DENIED — Write narrative | "Write narrative for AC-2 on Eagle Eye: test text" | `compliance_write_narrative` | **403 Forbidden** — SCA (Auditor) cannot modify SSP narratives | SCA role active |
| SCA-22 | DENIED — Remediate finding | "Fix finding {finding_id} on Eagle Eye" | `compliance_remediate` | **403 Forbidden** — SCA cannot execute remediation | SCA role active |
| SCA-23 | DENIED — Issue authorization | "Issue ATO for Eagle Eye" | `compliance_issue_authorization` | **403 Forbidden** — only AO can issue authorization decisions | SCA role active |
| SCA-24 | DENIED — Dismiss alert | "Dismiss alert ALT-{id}" | `watch_dismiss_alert` | **403 Forbidden** — only ISSM (SecurityLead) can dismiss | SCA role active |

---

## Persona 4: Authorizing Official (AO)

**RBAC Role**: `Compliance.AuthorizingOfficial`
**Primary Interface**: Microsoft Teams (Adaptive Cards)
**Primary RMF Phases**: Authorize (Lead)
**Responsibility**: Accepts organizational risk; issues ATO/ATOwC/IATT/DATO decisions; reviews risk posture

### Phase 5 — Authorize

| TC-ID | Task / Job | Natural Language Input | Tool(s) Invoked | Expected Output | Preconditions |
|-------|-----------|----------------------|-----------------|-----------------|---------------|
| AO-01 | View portfolio dashboard | "Show the multi-system compliance dashboard" | `compliance_multi_system_dashboard` | All systems with RMF step, auth status, score, open findings, expiration dates | ≥ 1 system registered |
| AO-02 | Review authorization package | "Show the authorization package summary for Eagle Eye" | `compliance_bundle_authorization_package` | Package summary: SSP status, SAR summary, RAR summary, POA&M count, CRM status, completeness | Package bundled by ISSM |
| AO-03 | View risk register | "Show the risk register for Eagle Eye" | `compliance_show_risk_register` | Risk entries with finding ID, severity, control, status, recommended mitigation, residual risk | Assessment complete |
| AO-04 | Issue ATO | "Issue an ATO for Eagle Eye expiring January 15, 2028 with Low residual risk — all CAT I findings remediated, 2 CAT III findings accepted" | `compliance_issue_authorization` | Authorization record: type=ATO, expiration=2028-01-15, residual risk=Low, conditions noted | Package reviewed |
| AO-05 | Issue ATO with Conditions | "Issue an ATO with Conditions for Eagle Eye — condition: CAT II finding on SI-4 must be remediated within 60 days" | `compliance_issue_authorization` | Authorization: type=ATOwC, conditions array with SI-4 remediation deadline | Package reviewed |
| AO-06 | Issue IATT | "Issue an Interim Authorization to Test for Eagle Eye for 90 days — limited to development environment only" | `compliance_issue_authorization` | Authorization: type=IATT, 90-day duration, scope limitation noted | Package reviewed |
| AO-07 | Deny authorization | "Deny authorization for Eagle Eye — 3 unmitigated CAT I findings present unacceptable risk to the mission" | `compliance_issue_authorization` | Authorization: type=DATO, denial rationale recorded | Package reviewed |
| AO-08 | Accept risk on a finding | "Accept the risk on finding {finding_id} — the compensating control in AC-3 adequately mitigates the risk" | `compliance_accept_risk` | Finding risk status updated to Accepted; AO rationale and compensating control reference saved | Finding exists |
| AO-09 | Check ATO expirations | "What ATOs expire in the next 90 days?" | `compliance_track_ato_expiration` | List of systems with ATOs expiring within 90 days; alert levels per system | ATOs granted |
| AO-10 | View compliance trend | "Show compliance score trend for Eagle Eye" | `watch_compliance_trend` | Score progression over time with data points | Monitoring active |
| AO-11 | View all alerts | "Show all critical alerts across my authorized systems" | `watch_show_alerts` | Critical alerts from all systems the AO has authorized | Monitoring active |

### AO Separation-of-Duties Verification

| TC-ID | Task / Job | Natural Language Input | Tool(s) Invoked | Expected Output | Preconditions |
|-------|-----------|----------------------|-----------------|-----------------|---------------|
| AO-12 | DENIED — Modify SSP | "Write narrative for AC-2 on Eagle Eye" | `compliance_write_narrative` | **403 Forbidden** — AO cannot modify SSP | AO role active |
| AO-13 | DENIED — Fix findings | "Remediate finding {finding_id}" | `compliance_remediate` | **403 Forbidden** — AO cannot execute remediation | AO role active |
| AO-14 | DENIED — Assess controls | "Assess AC-2 as Satisfied" | `compliance_assess_control` | **403 Forbidden** — only SCA can record assessments | AO role active |

---

## Persona 5: Platform Engineer

**RBAC Role**: `Compliance.PlatformEngineer` (default for CAC-authenticated users)
**Primary Interface**: VS Code (`@ato` chat participant)
**Primary RMF Phases**: Implement (controls), Monitor (remediation)
**Responsibility**: Builds and operates the system; implements controls; fixes findings; writes narratives

### Phase 3 — Implement (Build & Configure)

| TC-ID | Task / Job | Natural Language Input | Tool(s) Invoked | Expected Output | Preconditions |
|-------|-----------|----------------------|-----------------|-----------------|---------------|
| ENG-01 | Learn about a control | "@ato What does AC-2 mean for Azure?" | `compliance_get_control_family` / knowledge tools | Control description, Azure-specific implementation guidance, related STIG rules | None |
| ENG-02 | View STIG mappings | "@ato What STIG rules apply to Windows Server 2022?" | `compliance_show_stig_mapping` | STIG rules for benchmark with VulnId, RuleId, severity, NIST control mapping | STIG data loaded |
| ENG-03 | Scan IaC for compliance | "@ato Scan my Bicep file for compliance issues" | IaC diagnostics (in-editor) | Squiggly underlines: CAT I/II = Error (red), CAT III = Warning (yellow); hover info with NIST control + STIG rule | Bicep file open in editor |
| ENG-04 | Suggest a narrative | "@ato Suggest a narrative for SC-7 on Eagle Eye" | `compliance_suggest_narrative` | AI-generated narrative draft with confidence score; reference sources | System with baseline |
| ENG-05 | Write a narrative | "@ato Write narrative for SC-7 on Eagle Eye: Network boundary protection is implemented via Azure Firewall Premium with IDPS, NSG micro-segmentation, and Azure Front Door WAF" | `compliance_write_narrative` | Narrative saved with status="Implemented" | System in Implement phase |
| ENG-06 | Generate remediation plan | "@ato Generate a remediation plan for subscription sub-12345-abcde" | `compliance_generate_plan` | Prioritized plan with findings sorted by severity; remediation steps per finding | Findings exist |
| ENG-07 | Remediate with dry run | "@ato Remediate finding {finding_id} with dry run" | `compliance_remediate` | Dry run preview: what would change, affected resources, estimated impact; no changes applied | Finding exists |
| ENG-08 | Apply remediation | "@ato Apply remediation for finding {finding_id}" | `compliance_remediate` | Remediation executed; resource changes applied; finding status updated | ENG-07 (dry run reviewed) |
| ENG-09 | Validate remediation | "@ato Validate remediation for finding {finding_id}" | `compliance_validate_remediation` | Re-scan result: Pass (finding resolved) or Fail (finding persists with details) | ENG-08 |
| ENG-10 | Check narrative progress | "@ato Show narrative progress for the SC family on Eagle Eye" | `compliance_narrative_progress` | SC family stats: total, completed, draft, missing | Narratives partially authored |

### Kanban Task Workflow

| TC-ID | Task / Job | Natural Language Input | Tool(s) Invoked | Expected Output | Preconditions |
|-------|-----------|----------------------|-----------------|-----------------|---------------|
| ENG-11 | View assigned tasks | "@ato Show my assigned remediation tasks" | `kanban_task_list` | Task list filtered to current user; shows severity, control, status, due date | Tasks assigned by ISSO/ISSM |
| ENG-12 | Get task details | "@ato Show details of task REM-{id}" | `kanban_get_task` | Full task: control ID, finding details, affected resources, remediation script, SLA | Task exists |
| ENG-13 | Move task to In Progress | "@ato Move task REM-{id} to In Progress" | `kanban_move_task` | Status → InProgress; auto-assigns if unassigned; timestamp recorded | Task in ToDo |
| ENG-14 | Fix with Kanban dry run | "@ato Fix task REM-{id} with dry run" | `kanban_remediate_task` | Dry run remediation preview scoped to task's finding and resources | Task in InProgress |
| ENG-15 | Apply Kanban remediation | "@ato Apply fix for task REM-{id}" | `kanban_remediate_task` | Remediation applied; task finding updated; validation queued | ENG-14 reviewed |
| ENG-16 | Validate task | "@ato Validate task REM-{id}" | `kanban_task_validate` | Re-scan: Pass → validation confirmed; Fail → details of remaining issues | ENG-15 |
| ENG-17 | Collect evidence for task | "@ato Collect evidence for task REM-{id}" | `kanban_collect_evidence` | Evidence collected with SHA-256 hash; linked to task and finding | ENG-16 (validation passed) |
| ENG-18 | Add comment to task | "@ato Add comment on task REM-{id}: Remediation applied, waiting for DNS propagation before final validation" | `kanban_add_comment` | Comment saved with timestamp and author; visible to ISSO for review | Task exists |
| ENG-19 | Move to In Review | "@ato Move task REM-{id} to In Review" | `kanban_move_task` | Status → InReview; triggers automatic validation scan; ISSO notified | Validation passed |

### Prisma Remediation Workflow

| TC-ID | Task / Job | Natural Language Input | Tool(s) Invoked | Expected Output | Preconditions |
|-------|-----------|----------------------|-----------------|-----------------|---------------|
| ENG-20 | View Prisma findings | "@ato Show open Prisma Cloud findings for Eagle Eye with remediation steps" | `watch_show_alerts` / findings query | Prisma-sourced findings with RemediationGuidance, RemediationScript, AutoRemediable flag | Prisma import completed |
| ENG-21 | View Prisma CLI scripts | "@ato What CLI scripts are available for Eagle Eye Prisma findings?" | Findings query | List of findings with `RemediationCli` populated (from API JSON imports) | Prisma API import completed |
| ENG-22 | Prisma trend by resource type | "@ato Show Prisma trend for Eagle Eye grouped by resource type" | `compliance_prisma_trend` | Trend grouped by resource type (e.g., Microsoft.Storage/storageAccounts) for targeted remediation | Multiple Prisma imports |

### Engineer Separation-of-Duties Verification

| TC-ID | Task / Job | Natural Language Input | Tool(s) Invoked | Expected Output | Preconditions |
|-------|-----------|----------------------|-----------------|-----------------|---------------|
| ENG-23 | DENIED — Assess control | "@ato Assess AC-2 as Satisfied" | `compliance_assess_control` | **403 Forbidden** — only SCA can record assessments | Engineer role active |
| ENG-24 | DENIED — Issue authorization | "@ato Issue ATO for Eagle Eye" | `compliance_issue_authorization` | **403 Forbidden** — only AO can issue authorization | Engineer role active |
| ENG-25 | DENIED — Dismiss alert | "@ato Dismiss alert ALT-{id}" | `watch_dismiss_alert` | **403 Forbidden** — only ISSM can dismiss alerts | Engineer role active |
| ENG-26 | DENIED — Register system | "@ato Register a new system called Test" | `compliance_register_system` | **403 Forbidden** — only ISSM can register systems | Engineer role active |

---

## Cross-Persona Handoff Scenarios

These scenarios validate the end-to-end RMF lifecycle across persona boundaries.

### Scenario 1: Full RMF Lifecycle — Prepare Through ATO

| Step | Persona | Action | NL Input | Tool | Output |
|------|---------|--------|----------|------|--------|
| 1 | ISSM | Register system | "Register Eagle Eye as Major Application in Azure Gov" | `compliance_register_system` | system_id |
| 2 | ISSM | Define boundary + roles | "Define boundary; assign ISSO and SCA" | `compliance_define_boundary`, `compliance_assign_rmf_role` | boundary_id, role assignments |
| 3 | ISSM | Categorize | "Categorize as Moderate/Moderate/Low" | `compliance_categorize_system` | FIPS 199 record |
| 4 | ISSM | Select + tailor baseline | "Select Moderate baseline; set inheritance" | `compliance_select_baseline`, `compliance_set_inheritance` | 325 controls, inheritance records |
| 5 | ISSM | Advance to Implement | "Move to Implement" | `compliance_advance_rmf_step` | RMF step = Implement |
| 6 | ISSO | Author SSP | "Auto-populate inherited; write customer narratives" | `compliance_batch_populate_narratives`, `compliance_write_narrative` | Narratives at 100% |
| 7 | ISSO | Import scans | "Import CKL + Prisma CSV" | `compliance_import_ckl`, `compliance_import_prisma_csv` | Import records, findings |
| 8 | Engineer | Fix findings | "Fix task REM-{id}; validate" | `kanban_remediate_task`, `kanban_task_validate` | Findings resolved |
| 9 | ISSM | Advance to Assess | "Move to Assess" | `compliance_advance_rmf_step` | RMF step = Assess |
| 10 | SCA | Assess controls | "Assess AC-2 as Satisfied; assess SI-4 as OtherThanSatisfied CAT II" | `compliance_assess_control` | Effectiveness records |
| 11 | SCA | Generate SAR | "Generate SAR" | `compliance_generate_sar` | SAR document |
| 12 | ISSM | Create POA&M + RAR | "Create POA&M for CAT II finding; generate RAR" | `compliance_create_poam`, `compliance_generate_rar` | POA&M, RAR |
| 13 | ISSM | Bundle package | "Bundle authorization package" | `compliance_bundle_authorization_package` | Complete package |
| 14 | ISSM | Advance to Authorize | "Move to Authorize" | `compliance_advance_rmf_step` | RMF step = Authorize |
| 15 | AO | Review + decide | "Issue ATO expiring Jan 2028 with Low residual risk" | `compliance_issue_authorization` | ATO granted |
| 16 | ISSM | Set up ConMon | "Create ConMon plan with monthly assessments" | `compliance_create_conmon_plan` | ConMon active |
| 17 | ISSO | Enable monitoring | "Enable daily monitoring" | `watch_enable_monitoring` | Monitoring active |

### Scenario 2: Prisma Cloud Import → Assessment → Remediation

| Step | Persona | Action | NL Input | Tool | Output |
|------|---------|--------|----------|------|--------|
| 1 | ISSM | Import Prisma CSV | "Import Prisma Cloud CSV scan for Eagle Eye" | `compliance_import_prisma_csv` | Findings with NIST mappings |
| 2 | ISSM | Import Prisma API JSON | "Import Prisma API scan with auto-resolve subscriptions" | `compliance_import_prisma_api` | Enhanced findings with CLI scripts |
| 3 | ISSM | Review policies | "Show all Prisma policies affecting Eagle Eye" | `compliance_list_prisma_policies` | Policy catalog with severity + NIST |
| 4 | SCA | Review cloud posture | "Show Prisma trend for Eagle Eye grouped by severity" | `compliance_prisma_trend` | Trend data for assessment context |
| 5 | SCA | Assess cloud controls | "Assess SC-7 as OtherThanSatisfied CAT II based on Prisma network findings" | `compliance_assess_control` | Effectiveness record linked to Prisma data |
| 6 | SCA | Generate SAR | "Generate SAR for Eagle Eye" | `compliance_generate_sar` | SAR includes Prisma cloud posture data |
| 7 | ISSM | Create Kanban board | "Create remediation board from Eagle Eye's assessment" | `kanban_create_board` | Board with Prisma-sourced tasks |
| 8 | ISSO | Assign to engineer | "Assign SC-7 tasks to SSgt Rodriguez" | `kanban_assign_task` | Task assigned |
| 9 | Engineer | View CLI scripts | "Show CLI scripts for my assigned Prisma tasks" | `kanban_get_task` | Task with RemediationCli |
| 10 | Engineer | Apply fix | "Apply fix for task REM-{id}" | `kanban_remediate_task` | Cloud resource remediated |
| 11 | Engineer | Validate | "Validate task REM-{id}" | `kanban_task_validate` | Fix confirmed |
| 12 | ISSM | Re-import Prisma | "Import latest Prisma scan to verify remediation" | `compliance_import_prisma_csv` | Resolved findings; trend improvement |
| 13 | ISSM | Review trend | "Show Prisma trend for Eagle Eye" | `compliance_prisma_trend` | Downward trend in open alerts |

### Scenario 3: Continuous Monitoring Drift → Reauthorization

| Step | Persona | Action | NL Input | Tool | Output |
|------|---------|--------|----------|------|--------|
| 1 | ISSO | Alert fires | "Show all unacknowledged alerts" | `watch_show_alerts` | New CAT I drift finding |
| 2 | ISSO | Acknowledge | "Acknowledge alert ALT-{id}" | `watch_acknowledge_alert` | Alert acknowledged |
| 3 | ISSO | Escalate to ISSM | "This is a CAT I — needs ISSM review" | `kanban_add_comment` / notification | ISSM notified |
| 4 | ISSM | Report change | "Report significant change — security architecture modified" | `compliance_report_significant_change` | `requires_reauthorization = true` |
| 5 | ISSM | Check reauth | "Check reauthorization triggers for Eagle Eye" | `compliance_reauthorization_workflow` | Triggers: significant change, drift > 10% |
| 6 | ISSM | Initiate reauth | "Initiate reauthorization for Eagle Eye" | `compliance_reauthorization_workflow` | RMF regressed to Assess; changes marked as triggered |
| 7 | SCA | Re-assess | "Run assessment on Eagle Eye" | `compliance_assess` | New assessment with updated findings |
| 8 | SCA | New SAR | "Generate SAR for Eagle Eye" | `compliance_generate_sar` | SAR reflecting current state |
| 9 | ISSM | Re-bundle | "Bundle authorization package" | `compliance_bundle_authorization_package` | Updated package |
| 10 | AO | Re-authorize | "Issue ATO with Conditions — CAT I must be fixed in 30 days" | `compliance_issue_authorization` | ATOwC with condition |

---

## Error Handling & Edge Cases

These test cases verify that the system returns clear, actionable error messages for common misuse patterns.

| TC-ID | Persona | Task / Job | Natural Language Input | Tool(s) Invoked | Expected Output | Preconditions |
|-------|---------|-----------|----------------------|-----------------|-----------------|---------------|
| ERR-01 | ISSM | Advance RMF out of order | "Advance Eagle Eye to the Assess phase" | `compliance_advance_rmf_step` | **Error**: Cannot skip phases — system is in Prepare, must advance to Categorize first | System in Prepare phase |
| ERR-02 | ISSM | Import malformed Prisma CSV | "Import this Prisma CSV for Eagle Eye" (with garbled/empty file) | `compliance_import_prisma_csv` | **Error**: CSV parsing failed — missing required columns (Alert ID, Severity, Policy Name); import record created with status=Failed | System in Implement+ |
| ERR-03 | ISSM | Categorize already-categorized system | "Categorize Eagle Eye as High/High/High" | `compliance_categorize_system` | Upsert behavior — previous categorization replaced (not error); OR error if system is past Categorize phase | System already categorized |
| ERR-04 | SCA | Generate SAR with zero assessments | "Generate SAR for Eagle Eye" | `compliance_generate_sar` | **Warning/Error**: No control effectiveness records found — SAR cannot be generated without assessments | No assessments recorded |
| ERR-05 | ISSM | Bundle incomplete package | "Bundle authorization package for Eagle Eye" | `compliance_bundle_authorization_package` | Returns package with `warnings` array listing missing artifacts (e.g., "SAR not found", "POA&M empty"); package still generated with gaps flagged | Missing SAR or POA&M |
| ERR-06 | ISSM | Finalize already-finalized SAP | "Finalize the SAP for Eagle Eye" | `compliance_finalize_sap` | **Error**: SAP is already Finalized — cannot re-finalize; SHA-256 hash preserved | SAP already finalized |
| ERR-07 | ISSM | Update finalized SAP | "Update Eagle Eye's SAP — change the start date to May 1" | `compliance_update_sap` | **Error**: SAP is Finalized and immutable — cannot modify; must generate a new SAP | SAP finalized |
| ERR-08 | Engineer | Remediate non-existent finding | "@ato Remediate finding 00000000-0000-0000-0000-000000000000" | `compliance_remediate` | **Error**: Finding not found — verify the finding ID is correct | Invalid finding ID |

---

## PIM / Authentication Test Cases (All Personas)

These test cases verify that PIM role activation and CAC authentication work across all personas.

| TC-ID | Persona | Task / Job | Natural Language Input | Tool(s) Invoked | Expected Output |
|-------|---------|-----------|----------------------|-----------------|-----------------|
| AUTH-01 | Any | Check CAC session | "Check my CAC session status" | `cac_status` | Session active/expired, certificate info, role mapping |
| AUTH-02 | Any | List eligible PIM roles | "What PIM roles am I eligible for?" | `pim_list_eligible` | List of eligible roles with max duration |
| AUTH-03 | Any | Activate PIM role | "Activate my Compliance.SecurityLead role for 4 hours — quarterly review preparation" | `pim_activate_role` | Role activated; expiration time; justification recorded |
| AUTH-04 | Any | List active roles | "Show my active PIM roles" | `pim_list_active` | Active roles with activation time, expiration, justification |
| AUTH-05 | Any | Request JIT access | "Request just-in-time access to the production subscription for 2 hours — emergency remediation" | `jit_request_access` | JIT session created; access granted; expiration set |
| AUTH-06 | ISSM | Approve PIM request | "Approve the PIM request from Jane Smith" | `pim_approve_request` | Request approved; role activated for requester |
| AUTH-07 | ISSM | Deny PIM request | "Deny the PIM request — insufficient justification" | `pim_deny_request` | Request denied; reason recorded |
| AUTH-08 | Any | Deactivate role | "Deactivate my SecurityLead role" | `pim_deactivate_role` | Role deactivated early; session ended |

---

## Test Case Summary

| Persona | Positive Tests | Negative (RBAC Denied) | Total |
|---------|---------------|----------------------|-------|
| ISSM | 43 | 0 | 43 |
| ISSO | 24 | 0 | 24 |
| SCA | 20 | 4 | 24 |
| AO | 11 | 3 | 14 |
| Engineer | 22 | 4 | 26 |
| Cross-Persona Scenarios | 3 scenarios (40 steps) | — | 3 |
| Error Handling | 0 | 8 | 8 |
| Auth/PIM | 8 | 0 | 8 |
| **Total** | **128 + 40 steps** | **19** | **147 + 3 scenarios** |

---

## Acceptance Criteria

1. Every positive test case produces the expected output within 10 seconds
2. Every RBAC-denied test case returns 403 Forbidden (not 404 or 500)
3. Cross-persona handoff scenarios complete end-to-end with correct data flowing between personas
4. PIM role activation correctly gates tool access — tools are denied before activation and allowed after
5. All natural language inputs are resolved by the AI to the correct tool with correct parameters (no manual tool specification required)
6. Prisma import test cases verify findings include Prisma-specific fields (PrismaAlertId, CloudResourceType, RemediationCli)
7. Idempotent operations (batch_populate_narratives, ConMon plan) produce consistent results on re-run
