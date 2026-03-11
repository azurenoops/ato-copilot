# Unified RMF Test Execution Script

**Feature**: 020 — Persona Test Cases
**System Under Test**: Eagle Eye
**Total Test Cases**: 172 (156 positive + 11 RBAC denial + 8 error handling + 8 auth/PIM)
**Personas**: ISSM → ISSO → SCA → AO → Engineer (with handoffs)

> This script walks through the entire RMF lifecycle as a single flow. Tests are ordered by the phase in which they naturally occur, with persona handoffs called out at each transition.

---

## Pre-Execution Setup

### Authentication & PIM (AUTH-01 to AUTH-08)

> Run these first to verify the auth infrastructure works before starting the RMF flow.

#### AUTH-01: Check CAC Session

**Persona**: Any | **Interface**: Any

```text
Check my CAC session status
```

**Expected Tool**: `cac_status`
**Expected**: Session active; certificate info; role mapping shown
**Verify**: [ ] Session status returned [ ] Certificate info [ ] Role mapping

---

#### AUTH-02: List Eligible PIM Roles

```text
What PIM roles am I eligible for?
```

**Expected Tool**: `pim_list_eligible`
**Expected**: ≥ 5 compliance roles listed with max duration
**Verify**: [ ] SecurityLead, Analyst, Auditor, AuthorizingOfficial, PlatformEngineer all present

---

#### AUTH-03: Activate PIM Role

```text
Activate my Compliance.SecurityLead role for 4 hours — quarterly review preparation
```

**Expected Tool**: `pim_activate_role`
**Expected**: Role activated; expiration ~4 hours; justification recorded
**Verify**: [ ] Activation confirmed [ ] Expiration correct

---

#### AUTH-04: List Active Roles

```text
Show my active PIM roles
```

**Expected Tool**: `pim_list_active`
**Expected**: SecurityLead appears with activation/expiration times
**Verify**: [ ] Role listed [ ] Times present

---

#### AUTH-05: Request JIT Access

```text
Request just-in-time access to the production subscription for 2 hours — emergency remediation
```

**Expected Tool**: `jit_request_access`
**Expected**: JIT session created; 2-hour expiration
**Verify**: [ ] Session confirmed [ ] Scope correct

---

#### AUTH-06: Approve PIM Request (ISSM)

```text
Approve the PIM request from Jane Smith
```

**Expected Tool**: `pim_approve_request`
**Expected**: Approved; requester's role activated; approver identity recorded
**Verify**: [ ] Approval confirmed

---

#### AUTH-07: Deny PIM Request (ISSM)

```text
Deny the PIM request — insufficient justification
```

**Expected Tool**: `pim_deny_request`
**Expected**: Denied; reason recorded; no role activated
**Verify**: [ ] Denial confirmed

---

#### AUTH-08: Deactivate Role

```text
Deactivate my SecurityLead role
```

**Expected Tool**: `pim_deactivate_role`
**Expected**: Role deactivated; session ended
**Verify**: [ ] No longer appears in active roles

---

### Setup Checklist

1. **Activate ISSM role**: `Activate my Compliance.SecurityLead role for 8 hours — persona test suite execution`
2. **Verify role**: `Show my active PIM roles` → Confirm `Compliance.SecurityLead` is active
3. **Open Teams**: ISSM works via Microsoft Teams
4. **Confirm clean slate**: `Show system details for Eagle Eye` → Should return "System not found"

---

## Phase 0 — Prepare

**Active Persona**: ISSM | **Role**: `Compliance.SecurityLead` | **Interface**: Teams

---

### ISSM-01: Register Eagle Eye System

```text
Register a new system called 'Eagle Eye' as a Major Application with
mission-critical designation in Azure Government. The acronym is 'EE',
the description is 'Mission planning and operational intelligence
platform for joint force coordination', the Azure subscription ID is
12345678-abcd-1234-abcd-123456789012, the compliance framework is
NIST80053, and the cloud environment is Azure Government.
```

**Expected Tool**: `configuration_manage`
**Expected**: system_id (GUID), name="Eagle Eye", acronym="EE", type="MajorApplication", environment="AzureGovernment", rmf_step="Prepare"
**Record**: system_id = _______________

---

### ISSM-02: Define Authorization Boundary

**Precondition**: ISSM-01

```text
Define the authorization boundary for Eagle Eye with these resources:
- /subscriptions/12345678-abcd-1234-abcd-123456789012/resourceGroups/rg-eagleeye-prod/providers/Microsoft.Compute/virtualMachines/vm-eagleeye-web01
- /subscriptions/12345678-abcd-1234-abcd-123456789012/resourceGroups/rg-eagleeye-prod/providers/Microsoft.Compute/virtualMachines/vm-eagleeye-app01
- /subscriptions/12345678-abcd-1234-abcd-123456789012/resourceGroups/rg-eagleeye-prod/providers/Microsoft.Sql/servers/sql-eagleeye-prod/databases/db-eagleeye
- /subscriptions/12345678-abcd-1234-abcd-123456789012/resourceGroups/rg-eagleeye-prod/providers/Microsoft.KeyVault/vaults/kv-eagleeye-prod
- /subscriptions/12345678-abcd-1234-abcd-123456789012/resourceGroups/rg-eagleeye-dev/providers/Microsoft.KeyVault/vaults/kv-eagleeye-dev
```

**Expected Tool**: `compliance_define_boundary`
**Expected**: 5 resources in boundary
**Verify**: All 5 resources listed

---

### ISSM-03: Exclude Resource from Boundary

**Precondition**: ISSM-02

```text
Exclude /subscriptions/12345678-abcd-1234-abcd-123456789012/resourceGroups/rg-eagleeye-dev/providers/Microsoft.KeyVault/vaults/kv-eagleeye-dev from Eagle Eye's boundary — it's in a separate authorization
```

**Expected Tool**: `compliance_exclude_from_boundary`
**Expected**: Resource excluded; boundary count = 4

---

### ISSM-04: Assign RMF Roles

**Precondition**: ISSM-01

```text
Assign Jane Smith as ISSO and Bob Jones as SCA for Eagle Eye
```

**Expected Tool**: `compliance_assign_rmf_role` (×2)
**Expected**: ISSO → Jane Smith, SCA → Bob Jones

---

### ISSM-05: List RMF Role Assignments

**Precondition**: ISSM-04

```text
Show all RMF role assignments for Eagle Eye
```

**Expected Tool**: `compliance_list_rmf_roles`
**Expected**: ≥ 2 assignments (ISSO: Jane Smith, SCA: Bob Jones)

---

### ISSM-06: Advance to Categorize

**Precondition**: ISSM-01 through ISSM-04

```text
Advance Eagle Eye to the Categorize phase
```

**Expected Tool**: `compliance_advance_rmf_step`
**Expected**: RMF step = "Categorize"

---

### ERR-01: Advance RMF Out of Order (Error)

> Test error handling: attempt to skip phases.

```text
Advance Eagle Eye to the Assess phase
```

**Expected Tool**: `compliance_advance_rmf_step`
**Expected**: Error — cannot skip phases; must advance to next sequential phase
**Verify**: [ ] Error returned [ ] Phase unchanged

---

## Phase 1 — Categorize

**Active Persona**: ISSM | **Role**: `Compliance.SecurityLead` | **Interface**: Teams

---

### ISSM-07: Suggest Information Types

**Precondition**: ISSM-06

```text
Suggest information types for Eagle Eye — it's a mission planning system
```

**Expected Tool**: `compliance_suggest_info_types`
**Expected**: SP 800-60 info type suggestions with C/I/A impact levels

---

### ISSM-08: Categorize the System

**Precondition**: ISSM-06

```text
Categorize Eagle Eye as Moderate confidentiality, Moderate integrity, Low
availability with info types: Mission Operations (C:Mod, I:Mod, A:Low)
```

**Expected Tool**: `compliance_categorize_system`
**Expected**: Overall impact = Moderate (high-water mark); C=Mod, I=Mod, A=Low

---

### ISSM-09: View Categorization

**Precondition**: ISSM-08

```text
Show the categorization for Eagle Eye
```

**Expected Tool**: `compliance_get_categorization`
**Expected**: FIPS 199 values match ISSM-08

---

### ERR-03: Re-Categorize (Error)

> Test error handling: attempt to re-categorize after progressing past phase.

```text
Categorize Eagle Eye as High/High/High
```

**Expected Tool**: `compliance_categorize_system`
**Expected**: Upsert (if still in Categorize) or error (if past Categorize)
**Record**: Behavior: ☐ Upsert / ☐ Phase error

---

### ISSM-10: Advance to Select

**Precondition**: ISSM-08

```text
Advance Eagle Eye to the Select phase
```

**Expected Tool**: `compliance_advance_rmf_step`
**Expected**: RMF step = "Select"

---

## Phase 2 — Select

**Active Persona**: ISSM | **Role**: `Compliance.SecurityLead` | **Interface**: Teams

---

### ISSM-11: Select Baseline

**Precondition**: ISSM-10

```text
Select the Moderate baseline for Eagle Eye
```

**Expected Tool**: `compliance_select_baseline`
**Expected**: 325 controls applied; baseline = "Moderate"
**Record**: control_count = ___

---

### ISSM-12: Tailor Baseline

**Precondition**: ISSM-11

```text
Remove control PE-1 from Eagle Eye's baseline — physical security is
inherited from the data center
```

**Expected Tool**: `compliance_tailor_baseline`
**Expected**: Control count = 324; rationale captured

---

### ISSM-13: Set Inheritance

**Precondition**: ISSM-11

```text
Set AC-1 through AC-4 as inherited from Azure Government FedRAMP High
for Eagle Eye
```

**Expected Tool**: `compliance_set_inheritance`
**Expected**: 4 inheritance records created; provider = "Azure Government"

---

### ISSM-14: Generate CRM

**Precondition**: ISSM-13

```text
Generate the Customer Responsibility Matrix for Eagle Eye
```

**Expected Tool**: `compliance_generate_crm`
**Expected**: CRM with inherited/shared/customer columns; AC-1 to AC-4 = inherited

---

### ISSM-15: View Baseline

**Precondition**: ISSM-11

```text
Show the baseline details for Eagle Eye
```

**Expected Tool**: `compliance_get_baseline`
**Expected**: Baseline = Moderate, totals match tailoring + inheritance

---

### ISSM-16: Advance to Implement

**Precondition**: ISSM-11

```text
Move Eagle Eye to the Implement phase
```

**Expected Tool**: `compliance_advance_rmf_step`
**Expected**: RMF step = "Implement"

---

## Phase 3 — Implement

### ISSM Oversight

**Active Persona**: ISSM | **Role**: `Compliance.SecurityLead` | **Interface**: Teams

---

### ISSM-17: Check SSP Progress

**Precondition**: ISSM-16

```text
What's the SSP completion percentage for Eagle Eye?
```

**Expected Tool**: `compliance_narrative_progress`
**Expected**: Overall %, per-family breakdown; initially low

---

### ISSM-19: Import Prisma Cloud CSV

**Precondition**: ISSM-16

```text
Import this Prisma Cloud CSV scan for Eagle Eye
```

**Attachment**: `tests/Ato.Copilot.Tests.Unit/TestData/sample-prisma-export.csv`

**Expected Tool**: `compliance_import_prisma_csv`
**Expected**: Findings with PrismaAlertId, CloudResourceType; NIST controls mapped

---

### ISSM-20: Import Prisma Cloud API Scan

**Precondition**: ISSM-16

```text
Import Prisma Cloud API scan results for Eagle Eye with auto-resolve
subscriptions
```

**Attachment**: `tests/Ato.Copilot.Tests.Unit/TestData/sample-prisma-api.json`

**Expected Tool**: `compliance_import_prisma_api`
**Expected**: `auto_resolve_subscription: true`; RemediationCli populated

---

### ERR-02: Import Malformed Prisma CSV (Error)

```text
Import this Prisma CSV for Eagle Eye
```

**Attachment**: `tests/Ato.Copilot.Tests.Unit/TestData/sample-malformed.ckl` (or any file missing required Prisma CSV columns)

**Expected Tool**: `compliance_import_prisma_csv`
**Expected**: Error — CSV parsing failed; import status = Failed; no findings created
**Verify**: [ ] Error identifies missing columns [ ] No orphaned findings

---

### ISSM-21: List Prisma Policies

**Precondition**: ISSM-19 or ISSM-20

```text
Show all Prisma Cloud policies affecting Eagle Eye
```

**Expected Tool**: `compliance_list_prisma_policies`
**Expected**: Policy list with severity, cloud type, NIST mappings

---

### ISSM-22: View Prisma Trends

**Precondition**: ISSM-19 + ISSM-20

```text
Show Prisma Cloud compliance trend for Eagle Eye over the last 90 days
grouped by severity
```

**Expected Tool**: `compliance_prisma_trend`
**Expected**: Trend data with per-period open/resolved/new counts by severity

---

### ISSM-41: Generate SAP

**Precondition**: ISSM-11

```text
Generate a Security Assessment Plan for Eagle Eye
```

**Expected Tool**: `compliance_generate_sap`
**Expected**: ~325 control entries; methods per control; status = Draft
**Record**: sap_id = _______________

---

### ISSM-42: Update SAP

**Precondition**: ISSM-41

```text
Update Eagle Eye's SAP — set assessment start date to April 1, add Bob
Jones to the assessment team as Lead Assessor, override AC-2 method to
Interview
```

**Expected Tool**: `compliance_update_sap`
**Expected**: Schedule set; team member added; AC-2 method override recorded

---

### ISSM-43: Finalize SAP

**Precondition**: ISSM-42

```text
Finalize the Security Assessment Plan for Eagle Eye
```

**Expected Tool**: `compliance_finalize_sap`
**Expected**: Status = "Finalized"; SHA-256 hash generated; immutable
**Record**: content_hash = _______________

---

### ERR-06: Re-Finalize SAP (Error)

**Precondition**: ISSM-43

```text
Finalize the SAP for Eagle Eye
```

**Expected Tool**: `compliance_finalize_sap`
**Expected**: Error — SAP already Finalized; hash unchanged
**Verify**: [ ] Error returned [ ] Hash matches ISSM-43

---

### ERR-07: Update Finalized SAP (Error)

**Precondition**: ISSM-43

```text
Update Eagle Eye's SAP — change the start date to May 1
```

**Expected Tool**: `compliance_update_sap`
**Expected**: Error — SAP is Finalized and immutable; guidance to generate a new SAP
**Verify**: [ ] Error returned [ ] Original SAP unchanged

---

### ISSM Privacy & Interconnection Management

---

### ISSM-44: Create Privacy Threshold Analysis

```text
Create a Privacy Threshold Analysis for Eagle Eye — the system
processes PII including name, SSN, and email for personnel records
```

**Expected Tool**: `compliance_create_pta`
**Expected**: PTA record with PII categories identified
**Record**: pta_id = _______________

---

### ISSM-46: Generate Privacy Impact Assessment

**Precondition**: ISSM-44

```text
Generate a Privacy Impact Assessment for Eagle Eye based on the PTA
```

**Expected Tool**: `compliance_generate_pia`
**Expected**: PIA with risk analysis, mitigation measures, retention policies
**Record**: pia_id = _______________

---

### ISSM-47: Review PIA

**Precondition**: ISSM-46

```text
Review the PIA for Eagle Eye — approve with note: retention period
set to 7 years per DoD 5400.11
```

**Expected Tool**: `compliance_review_pia`
**Expected**: PIA status = Approved; reviewer note recorded

---

### ISSM-48: Add Interconnection

```text
Add an interconnection for Eagle Eye — outbound SMTP to DISA DEE
(smtp.dee.disa.mil) for email relay, port 587 TLS
```

**Expected Tool**: `compliance_add_interconnection`
**Expected**: Interconnection created (direction: outbound, protocol: SMTP/TLS, port: 587)
**Record**: interconnection_id = _______________

---

### ISSM-49: List Interconnections

**Precondition**: ISSM-48

```text
List all interconnections for Eagle Eye
```

**Expected Tool**: `compliance_list_interconnections`
**Expected**: ≥ 1 interconnection listed with direction, protocol, status

---

### ISSM-52: Generate ISA

**Precondition**: ISSM-48

```text
Generate an Interconnection Security Agreement for Eagle Eye
```

**Expected Tool**: `compliance_generate_isa`
**Expected**: ISA document covering DISA DEE interconnection
**Record**: isa_id = _______________

---

### ISSM-53: Register Agreement

**Precondition**: ISSM-52

```text
Register a Memorandum of Agreement for the DISA DEE email relay
interconnection — effective date today, annual review
```

**Expected Tool**: `compliance_register_agreement`
**Expected**: Agreement registered (type: MOA, status: Active)
**Record**: agreement_id = _______________

---

### ISSM-55: Validate Agreements

**Precondition**: ISSM-53

```text
Validate all interconnection agreements for Eagle Eye
```

**Expected Tool**: `compliance_validate_agreements`
**Expected**: All agreements valid; coverage report

---

### → Handoff: ISSM → ISSO

> **Action**: Deactivate SecurityLead role. Activate Compliance.Analyst. Switch to VS Code.

**ISSO Preconditions from ISSM**:
- ✓ Eagle Eye in Implement phase (ISSM-16)
- ✓ Moderate baseline with 325 controls (ISSM-11)
- ✓ AC-1 through AC-4 inherited (ISSM-13)
- ✓ Prisma scans imported (ISSM-19, ISSM-20)
- ✓ SAP finalized (ISSM-43)

---

### ISSO HW/SW Inventory (Feature 025)

**Active Persona**: ISSO | **Role**: `Compliance.Analyst` | **Interface**: VS Code `@ato`

---

### ISSO-INV-01: Auto-Seed Inventory from Boundary

**Precondition**: Authorization boundary defined

```text
@ato Auto-seed the hardware inventory for Eagle Eye from the authorization boundary
```

**Expected Tool**: `inventory_auto_seed`
**Expected**: `created_count` > 0; items mapped from boundary resources

---

### ISSO-INV-02: Add Additional Hardware

**Precondition**: ISSO-INV-01

```text
@ato Add hardware item "web-server-01" to Eagle Eye — Dell Server at 10.0.0.1
```

**Expected Tool**: `inventory_add_item`
**Expected**: Item created with status Active

---

### ISSO-INV-03: Check Completeness Before Export

**Precondition**: ISSO-INV-02

```text
@ato Check inventory completeness for Eagle Eye
```

**Expected Tool**: `inventory_completeness`
**Expected**: `completeness_score`, `is_complete`, issue breakdown

---

### ISSO-INV-04: Export to eMASS Excel

**Precondition**: ISSO-INV-03

```text
@ato Export the HW/SW inventory for Eagle Eye
```

**Expected Tool**: `inventory_export`
**Expected**: Base64-encoded Excel workbook with Hardware and Software worksheets

---

### ISSO Narrative Governance (Feature 024)

**Active Persona**: ISSO | **Role**: `Compliance.Analyst` | **Interface**: VS Code `@ato`

---

### ISSO-NGV-01: View Narrative Version History

**Precondition**: ISSO-04 (narrative written for a control)

```text
@ato Show the version history for the AC-2 narrative of Eagle Eye
```

**Expected Tool**: `compliance_narrative_history`
**Expected**: List of versions (newest first) with `total_versions`, `version_number`, `authored_by`, `authored_at`

### ISSO-NGV-02: Diff Narrative Versions

**Precondition**: At least 2 versions exist for a control

```text
@ato Show the diff between version 1 and version 2 of the AC-2 narrative for Eagle Eye
```

**Expected Tool**: `compliance_narrative_diff`
**Expected**: Unified diff text with `lines_added` and `lines_removed`

### ISSO-NGV-03: Submit Narrative for ISSM Review

**Precondition**: ISSO-NGV-01

```text
@ato Submit the AC-2 narrative for Eagle Eye for ISSM review
```

**Expected Tool**: `compliance_submit_narrative`
**Expected**: `previous_status: Draft`, `new_status: InReview`

### ISSO-NGV-04: Batch Submit AC Family Narratives

**Precondition**: Multiple Draft narratives exist in AC family

```text
@ato Submit all AC family narratives for Eagle Eye for ISSM review
```

**Expected Tool**: `compliance_batch_submit_narratives` with `family_filter` = "AC"
**Expected**: `submitted_count`, `skipped_count`, `submitted_controls`

---

### ISSM Narrative Review (Feature 024)

**Active Persona**: ISSM | **Role**: `Compliance.SecurityLead` | **Interface**: Microsoft Teams

---

### ISSM-NGV-01: View Narrative Approval Progress

**Precondition**: ISSO-NGV-03 (narratives submitted for review)

```text
@ato Show the narrative approval progress for Eagle Eye
```

**Expected Tool**: `compliance_narrative_approval_progress`
**Expected**: Overall counts, `approval_percentage`, per-family breakdown, `review_queue` containing submitted controls

### ISSM-NGV-02: Approve Narrative

**Precondition**: ISSO-NGV-03

```text
@ato Approve the AC-2 narrative for Eagle Eye
```

**Expected Tool**: `compliance_review_narrative` with `decision` = "approve"
**Expected**: `new_status: Approved`, `reviewed_by`, `reviewed_at`

### ISSM-NGV-03: Request Revision with Comments

**Precondition**: Another narrative in InReview status

```text
@ato Request revision of the AC-3 narrative for Eagle Eye — comments: "Please add Azure AD configuration details"
```

**Expected Tool**: `compliance_review_narrative` with `decision` = "request_revision"
**Expected**: `new_status: NeedsRevision`, comments stored

### ISSM-NGV-04: Batch Approve Narratives

**Precondition**: Multiple InReview narratives in a family

```text
@ato Batch approve all AC family narratives for Eagle Eye
```

**Expected Tool**: `compliance_batch_review_narratives` with `decision` = "approve"
**Expected**: `reviewed_count`, `skipped_count`

---

### ISSO SSP Authoring

**Active Persona**: ISSO | **Role**: `Compliance.Analyst` | **Interface**: VS Code `@ato`

---

### ISSO-01: Auto-Populate Inherited Narratives

**Precondition**: System in Implement phase with inheritance set

```text
@ato Auto-populate the inherited control narratives for Eagle Eye
```

**Expected Tool**: `compliance_batch_populate_narratives`
**Expected**: Narratives filled for all inherited controls; idempotent on re-run
**Record**: populated = ___, skipped = ___

---

### ISSO-02: Check Narrative Progress

**Precondition**: ISSO-01

```text
@ato Show narrative progress for Eagle Eye
```

**Expected Tool**: `compliance_narrative_progress`
**Expected**: Overall %, per-family breakdown
**Record**: overall_pct = ___%

---

### ISSO-03: Get AI Narrative Suggestion

```text
@ato Suggest a narrative for AC-2 on Eagle Eye
```

**Expected Tool**: `compliance_suggest_narrative`
**Expected**: Suggested text; confidence score (~0.55 for customer control)
**Record**: confidence = ___

---

### ISSO-04: Write a Control Narrative

```text
@ato Write narrative for AC-2 on Eagle Eye: Account management is
implemented using Azure Active Directory with automated provisioning
via SCIM, quarterly access reviews, and 15-minute session timeouts
```

**Expected Tool**: `compliance_write_narrative`
**Expected**: Narrative saved; status = "Implemented"

---

### ISSO-05: Update Narrative to Partial

```text
@ato Update AC-3 narrative on Eagle Eye to PartiallyImplemented: Access
enforcement is configured via Azure RBAC, ABAC policies pending
deployment
```

**Expected Tool**: `compliance_write_narrative`
**Expected**: Status = "PartiallyImplemented"

---

### ISSO-06: Filter Progress by Family

**Precondition**: ISSO-01

```text
@ato Show narrative progress for the AC family on Eagle Eye
```

**Expected Tool**: `compliance_narrative_progress`
**Expected**: AC family only: total, completed, draft, missing

---

### ISSO-07: Generate Full SSP

**Precondition**: Narratives substantially complete

```text
@ato Generate the SSP for Eagle Eye
```

**Expected Tool**: `compliance_generate_ssp`
**Expected**: Markdown SSP with System Info, Categorization, Baseline, Control Implementations; warnings for gaps

---

### ISSO-08: Generate SSP Section Only

```text
@ato Generate just the system information section of Eagle Eye's SSP
```

**Expected Tool**: `compliance_generate_ssp`
**Expected**: Only System Information section rendered

---

### ISSO-09: Import CKL Checklist

```text
@ato Import this CKL file for Eagle Eye
```

**Attachment**: `tests/Ato.Copilot.Tests.Unit/TestData/sample-valid.ckl`

**Expected Tool**: `compliance_import_ckl`
**Expected**: Findings mapped to NIST controls; status counts (Created/Updated/Skipped/Unmatched)
**Record**: created = ___, updated = ___, skipped = ___, unmatched = ___

---

### ISSO-10: Import XCCDF Results

```text
@ato Import SCAP scan results for Eagle Eye
```

**Attachment**: `tests/Ato.Copilot.Tests.Unit/TestData/sample-valid.xccdf`

**Expected Tool**: `compliance_import_xccdf`
**Expected**: XCCDF benchmark scores; rules mapped to NIST controls
**Record**: benchmark = ___, score = ___

---

### ISSO-11: View Import History

**Precondition**: ISSO-09 or ISSO-10

```text
@ato Show import history for Eagle Eye
```

**Expected Tool**: `compliance_list_imports`
**Expected**: Paginated list with type, date, benchmark, finding counts
**Record**: import_count = ___

---

### ISSO-12: View Import Details

**Precondition**: ISSO-09 or ISSO-10

```text
@ato Show details of import {import_id}
```

**Expected Tool**: `compliance_get_import_summary`
**Expected**: Per-finding breakdown with actions, NIST mappings, conflict resolutions

---

### ISSO SSP Section Authoring & CKL Export

---

### ISSO-25: Export CKL

**Precondition**: ISSO-09 (CKL imported)

```text
@ato Export a CKL file for Eagle Eye Windows Server 2022 STIG
```

**Expected Tool**: `compliance_export_ckl`
**Expected**: CKL XML with per-rule status (Open/NotAFinding/Not_Applicable)

---

### ISSO-26: Create PTA (ISSO Contribution)

```text
@ato Create a Privacy Threshold Analysis for Eagle Eye — the system
processes PII including name, SSN, and email for personnel records
```

**Expected Tool**: `compliance_create_pta`
**Expected**: PTA record created (if not already created by ISSM)

---

### ISSO-31: Write SSP Section §5

```text
@ato Write SSP section 5 for Eagle Eye: System architecture consists
of Azure App Service frontend, Azure SQL backend, Azure Key Vault
for secrets, and Azure Front Door for global load balancing. All
components deployed in Azure Government (USGov Virginia).
```

**Expected Tool**: `compliance_write_ssp_section`
**Expected**: SSP §5 saved; status = Draft (pending ISSM review)

---

### ISSO-32: Write SSP Section §6

```text
@ato Write SSP section 6 for Eagle Eye: Technical controls include
Azure AD Conditional Access for MFA enforcement, Microsoft Defender
for Cloud for workload protection, NSG micro-segmentation, and Azure
Policy for compliance guardrails.
```

**Expected Tool**: `compliance_write_ssp_section`
**Expected**: SSP §6 saved; status = Draft (pending ISSM review)

---

### ISSO-33: Submit SSP for Review

```text
@ato Submit SSP sections 5 and 6 for Eagle Eye to the ISSM for review
```

**Expected Tool**: `compliance_write_ssp_section` (status update)
**Expected**: Sections submitted; ISSM notified

---

### ISSO-34: Check SSP Completeness

```text
@ato Show SSP completeness for Eagle Eye
```

**Expected Tool**: `compliance_ssp_completeness`
**Expected**: Overall completion %; §5 and §6 show as Draft

---

### ISSO-29: Add Interconnection (ISSO Contribution)

```text
@ato Add an interconnection for Eagle Eye — outbound SMTP to DISA
DEE (smtp.dee.disa.mil) for email relay, port 587 TLS
```

**Expected Tool**: `compliance_add_interconnection`
**Expected**: Interconnection registered (direction: outbound, port: 587)

---

### ISSO-19: Collect Evidence

```text
@ato Collect evidence for AC-2 on Eagle Eye
```

**Expected Tool**: `compliance_collect_evidence`
**Expected**: Evidence record with SHA-256 hash; Azure resource data captured
**Record**: evidence_hash = _______________

---

### → Handoff: ISSO → Engineer (Implement / Build)

> **Action**: Deactivate Analyst role. Activate Compliance.PlatformEngineer. Stay in VS Code.

---

### Engineer Implementation

**Active Persona**: Engineer | **Role**: `Compliance.PlatformEngineer` | **Interface**: VS Code `@ato`

---

### ENG-01: Learn About a Control

```text
@ato What does AC-2 mean for Azure?
```

**Expected Tool**: `compliance_get_control_family` / knowledge tools
**Expected**: AC-2 description; Azure-specific guidance; related STIG rules

---

### ENG-02: View STIG Mappings

```text
@ato What STIG rules apply to Windows Server 2022?
```

**Expected Tool**: `compliance_show_stig_mapping`
**Expected**: STIG rules with VulnId, RuleId, severity, NIST control mapping

---

### ENG-03: Scan IaC for Compliance

**Precondition**: Bicep file open in editor

```text
@ato Scan my Bicep file for compliance issues
```

**Expected Tool**: IaC diagnostics (in-editor)
**Expected**: CAT I/II = red underline; CAT III = yellow; hover shows NIST control + STIG rule
**Record**: Diagnostics: CAT I: ___, CAT II: ___, CAT III: ___

---

### ENG-04: Suggest a Narrative

```text
@ato Suggest a narrative for SC-7 on Eagle Eye
```

**Expected Tool**: `compliance_suggest_narrative`
**Expected**: AI draft for SC-7 (Boundary Protection); references Azure networking

---

### ENG-05: Write a Narrative

```text
@ato Write narrative for SC-7 on Eagle Eye: Network boundary protection is implemented via Azure Firewall Premium with IDPS, NSG micro-segmentation, and Azure Front Door WAF
```

**Expected Tool**: `compliance_write_narrative`
**Expected**: Narrative saved; status = "Implemented"

---

### ENG-06: Generate Remediation Plan

**Precondition**: Findings exist

```text
@ato Generate a remediation plan for subscription sub-12345-abcde
```

**Expected Tool**: `compliance_generate_plan`
**Expected**: Prioritized list sorted by severity (CAT I → II → III); remediation steps per finding
**Record**: Plan ID: __________ | Total findings: ____

---

### ENG-07: Remediate with Dry Run

```text
@ato Remediate finding {finding_id} with dry run
```

**Expected Tool**: `compliance_remediate`
**Expected**: Preview only — no changes applied; what-would-change details

---

### ENG-08: Apply Remediation

**Precondition**: ENG-07 reviewed

```text
@ato Apply remediation for finding {finding_id}
```

**Expected Tool**: `compliance_remediate`
**Expected**: Remediation executed; resource changes applied; finding status updated

---

### ENG-09: Validate Remediation

**Precondition**: ENG-08

```text
@ato Validate remediation for finding {finding_id}
```

**Expected Tool**: `compliance_validate_remediation`
**Expected**: Pass (resolved) or Fail (persists with details)
**Record**: Validation: ☐ Pass / ☐ Fail

---

### ENG-10: Check Narrative Progress

```text
@ato Show narrative progress for the SC family on Eagle Eye
```

**Expected Tool**: `compliance_narrative_progress`
**Expected**: SC family stats; SC-7 completed (ENG-05)
**Record**: SC family: Total ___ | Completed ___ | Draft ___ | Missing ___

---

### ENG-27: Export CKL Evidence

**Precondition**: STIG data imported (ISSO-09)

```text
@ato Export a CKL file for Eagle Eye Windows Server 2022 STIG
```

**Expected Tool**: `compliance_export_ckl`
**Expected**: CKL XML; remediated findings show as NotAFinding

---

### ENG-28: Register Interconnection

```text
@ato Add an interconnection for Eagle Eye — outbound HTTPS to Azure
DevOps (dev.azure.com) for CI/CD pipeline integration, port 443
```

**Expected Tool**: `compliance_add_interconnection`
**Expected**: Interconnection created (direction: outbound, protocol: HTTPS, port: 443)

---

### ENG-29: Write SSP Technical Section

```text
@ato Write SSP section 6 for Eagle Eye: Technical controls are
implemented using Azure Policy, Microsoft Defender for Cloud, NSG
micro-segmentation, and Azure Key Vault for secrets management.
All configurations are enforced via Bicep IaC templates.
```

**Expected Tool**: `compliance_write_ssp_section`
**Expected**: SSP §6 saved; status = Draft

---

### ENG-30: Check SSP Completion

```text
@ato Show SSP completeness for Eagle Eye
```

**Expected Tool**: `compliance_ssp_completeness`
**Expected**: Overall %; §6 reflected

---

### ERR-08: Remediate Non-Existent Finding (Error)

```text
@ato Remediate finding 00000000-0000-0000-0000-000000000000
```

**Expected Tool**: `compliance_remediate`
**Expected**: Error — Finding not found; no remediation attempted
**Verify**: [ ] Error returned [ ] No state changes

---

### → Handoff: Engineer → ISSO (Monitoring Setup)

> **Action**: Deactivate PlatformEngineer. Activate Compliance.Analyst. Stay in VS Code.

---

### ISSO Monitoring & Day-to-Day

**Active Persona**: ISSO | **Role**: `Compliance.Analyst` | **Interface**: VS Code `@ato`

---

### ISSO-13: Enable Monitoring

```text
@ato Enable daily monitoring for subscription sub-12345-abcde
```

**Expected Tool**: `watch_enable_monitoring`
**Expected**: Monitoring config created; frequency = Daily; next scan scheduled
**Record**: next_scan = _______________

---

### ISSO-14: View Monitoring Status

**Precondition**: ISSO-13

```text
@ato Show monitoring status for Eagle Eye
```

**Expected Tool**: `watch_monitoring_status`
**Expected**: Status: Enabled; frequency; last/next scan; alert count

---

### ISSO-15: Show All Alerts

**Precondition**: Monitoring active + drift detected

```text
@ato Show all unacknowledged alerts for Eagle Eye
```

**Expected Tool**: `watch_show_alerts`
**Expected**: Alert list with severity, control, resource, timestamp (unacknowledged only)
**Record**: alert_count = ___

---

### ISSO-16: Get Alert Details

**Precondition**: ISSO-15

```text
@ato Show details of alert ALT-{id}
```

**Expected Tool**: `watch_get_alert`
**Expected**: Full alert: severity, control ID, resource, current vs expected state, remediation suggestion

---

### ISSO-17: Acknowledge Alert

**Precondition**: ISSO-15

```text
@ato Acknowledge alert ALT-{id} — scheduled for next maintenance window
```

**Expected Tool**: `watch_acknowledge_alert`
**Expected**: Alert status → Acknowledged; comment saved

---

### ISSO-18: Fix an Alert

**Precondition**: ISSO-15

```text
@ato Fix alert ALT-{id}
```

**Expected Tool**: `watch_fix_alert`
**Expected**: Remediation executed; finding updated; validation returned

---

### ISSO-22: Assign Remediation Task

**Precondition**: Kanban board exists

```text
@ato Assign task REM-{id} to SSgt Rodriguez
```

**Expected Tool**: `kanban_assign_task`
**Expected**: Task assigned; engineer notified

---

### ISSO-23: View Alert History

```text
@ato Show alert trends for Eagle Eye over the last 30 days
```

**Expected Tool**: `watch_alert_history`
**Expected**: Timeline view of alert data

---

### ISSO-24: View Compliance Trend

```text
@ato Show compliance score trend for Eagle Eye
```

**Expected Tool**: `watch_compliance_trend`
**Expected**: Score progression over time with data points per scan

---

### → Handoff: ISSO → ISSM (SSP Generation & Assess Prep)

> **Action**: Deactivate Analyst. Activate Compliance.SecurityLead. Switch to Teams.

---

### ISSM SSP & Assess Preparation

**Active Persona**: ISSM | **Role**: `Compliance.SecurityLead` | **Interface**: Teams

---

### ISSM-18: Generate SSP

**Precondition**: ISSM-16 + ISSO-04/ENG-05 (narratives authored)

```text
Generate the SSP for Eagle Eye
```

**Expected Tool**: `compliance_generate_ssp`
**Expected**: Markdown SSP with 4 major sections; warnings for missing narratives

---

### → Handoff: ISSM → SCA (Independent Assessment)

> **Action**: Deactivate SecurityLead. Activate Compliance.Auditor. Stay on Teams.

**SCA Preconditions**:
- ✓ SSP narratives at high completion % (ISSO-01 through ISSO-07)
- ✓ CKL and XCCDF scans imported (ISSO-09, ISSO-10)
- ✓ Evidence collected (ISSO-19)
- ✓ Prisma imports completed (ISSM-19, ISSM-20)
- ✓ SAP finalized (ISSM-43)

---

## Phase 4 — Assess

**Active Persona**: SCA | **Role**: `Compliance.Auditor` | **Interface**: Teams

---

### SCA-01: Take Pre-Assessment Snapshot

```text
Take an assessment snapshot for Eagle Eye before I begin the assessment
```

**Expected Tool**: `compliance_take_snapshot`
**Expected**: Immutable snapshot with timestamp; captures current control states
**Record**: pre_snapshot_id = _______________

---

### SCA-02: View System Baseline

```text
Show the baseline for Eagle Eye
```

**Expected Tool**: `compliance_get_baseline`
**Expected**: Baseline = Moderate; controls ≈ 324

---

### SCA-03: View System Categorization

```text
Show Eagle Eye's categorization
```

**Expected Tool**: `compliance_get_categorization`
**Expected**: C=Moderate, I=Moderate, A=Low

---

### SCA-04: Check Evidence Completeness

```text
Check evidence completeness for the AC family on Eagle Eye
```

**Expected Tool**: `compliance_check_evidence_completeness`
**Expected**: Per-control evidence status; coverage % for AC family
**Record**: ac_coverage = ___%

---

### SCA-05: Verify Evidence Integrity

```text
Verify evidence {evidence_id}
```

**Expected Tool**: `compliance_verify_evidence`
**Expected**: SHA-256 hash validation; integrity = Pass
**Verify**: Integrity = Pass

---

### SCA-06: Assess Control — Satisfied (Examine)

```text
Assess AC-2 as Satisfied using the Examine method — policy document
reviewed, automated provisioning verified, quarterly reviews confirmed
```

**Expected Tool**: `compliance_assess_control`
**Expected**: determination=Satisfied, method=Examine

---

### SCA-07: Assess Control — OtherThanSatisfied (CAT II)

```text
Assess SI-4 as Other Than Satisfied, CAT II — monitoring is deployed
but intrusion detection signatures are 90 days out of date
```

**Expected Tool**: `compliance_assess_control`
**Expected**: determination=OtherThanSatisfied, catSeverity=CATII

---

### SCA-08: Assess Using Interview Method

```text
Assess CP-2 as Satisfied using the Interview method — ISSO confirmed
annual contingency plan testing and updated contact rosters
```

**Expected Tool**: `compliance_assess_control`
**Expected**: method=Interview

---

### SCA-09: Assess Using Test Method

```text
Assess AC-7 as Satisfied using the Test method — verified 3-attempt
lockout on all endpoints
```

**Expected Tool**: `compliance_assess_control`
**Expected**: method=Test

---

### SCA-10: View Prisma Policies for Assessment

```text
Show Prisma Cloud policies with NIST mappings for Eagle Eye
```

**Expected Tool**: `compliance_list_prisma_policies`
**Expected**: Policy list with NIST mappings; severity; open/resolved counts

---

### SCA-11: Review Prisma Trend Data

```text
Show Prisma compliance trend for Eagle Eye to validate remediation
progress
```

**Expected Tool**: `compliance_prisma_trend`
**Expected**: Trend data with open/resolved/new counts

---

### SCA-12: Compare Snapshots

**Precondition**: SCA-01 + assessments recorded

```text
Compare the pre-assessment snapshot with current state for Eagle Eye
```

**Expected Tool**: `compliance_compare_snapshots`
**Expected**: Delta report: controls changed, new/resolved findings, effectiveness changes
**Record**: controls_changed = ___

---

### SCA-13: Take Post-Assessment Snapshot

```text
Take a final assessment snapshot for Eagle Eye
```

**Expected Tool**: `compliance_take_snapshot`
**Expected**: Second immutable snapshot with all determinations captured
**Record**: post_snapshot_id = _______________

---

### SCA-14: Get SAP

**Precondition**: ISSM-43

```text
Show the Security Assessment Plan for Eagle Eye
```

**Expected Tool**: `compliance_get_sap`
**Expected**: Finalized SAP with control entries, methods, team, schedule

---

### SCA-15: List SAPs

```text
List all SAPs for Eagle Eye
```

**Expected Tool**: `compliance_list_saps`
**Expected**: SAP history with status, dates, scope summaries
**Record**: sap_count = ___

---

### SCA-16: Check SAP-SAR Alignment

**Precondition**: SAP finalized + assessments recorded

```text
Check SAP-to-SAR alignment for Eagle Eye
```

**Expected**: Alignment report: planned-but-unassessed controls, coverage %
**Record**: alignment_coverage = ___%

---

### SCA-17: Generate SAR

```text
Generate the Security Assessment Report for Eagle Eye
```

**Expected Tool**: `compliance_generate_sar`
**Expected**: SAR with executive summary, per-control effectiveness, CAT findings, evidence refs, Prisma data

---

### SCA-18: Generate RAR

```text
Generate the Risk Assessment Report for Eagle Eye
```

**Expected Tool**: `compliance_generate_rar`
**Expected**: RAR with risk characterization, aggregate assessment, recommended mitigations

---

### SCA-19: View Import Summary

```text
Show Prisma Cloud import details for Eagle Eye's latest import
```

**Expected Tool**: `compliance_get_import_summary`
**Expected**: Per-finding breakdown with PrismaAlertId, CloudResourceType, NIST mappings

---

### SCA-20: Run Compliance Assessment

```text
Run a NIST 800-53 assessment for Eagle Eye
```

**Expected Tool**: `compliance_assess`
**Expected**: Per-control pass/fail; compliance score; evidence gaps
**Record**: compliance_score = ___

---

### ERR-04: Generate SAR with Zero Assessments (Error)

> If Eagle Eye already has assessments, note behavior conceptually or use a fresh system.

```text
Generate SAR for Eagle Eye
```

**Expected Tool**: `compliance_generate_sar`
**Expected**: Warning/error — no assessment data; no empty SAR generated

---

### SCA RBAC Denial Tests

> All 4 must return **403 Forbidden**.

### SCA-21: DENIED — Write Narrative

```text
Write narrative for AC-2 on Eagle Eye: test text
```

**Expected**: **403 Forbidden** — SCA cannot modify SSP

---

### SCA-22: DENIED — Remediate Finding

```text
Fix finding {finding_id} on Eagle Eye
```

**Expected**: **403 Forbidden** — SCA cannot remediate

---

### SCA-23: DENIED — Issue Authorization

```text
Issue ATO for Eagle Eye
```

**Expected**: **403 Forbidden** — only AO can authorize

---

### SCA-24: DENIED — Dismiss Alert

```text
Dismiss alert ALT-{id}
```

**Expected**: **403 Forbidden** — only ISSM can dismiss

---

### SCA SSP & OSCAL Validation

---

### SCA-25: Check SSP Completeness

```text
Show SSP completeness for Eagle Eye
```

**Expected Tool**: `compliance_ssp_completeness`
**Expected**: Overall completion %; per-section status

---

### SCA-26: Validate Interconnection Agreements

```text
Validate all interconnection agreements for Eagle Eye
```

**Expected Tool**: `compliance_validate_agreements`
**Expected**: Agreement coverage report; all interconnections have valid agreements

---

### SCA-27: Export OSCAL SSP

```text
Export OSCAL SSP for Eagle Eye
```

**Expected Tool**: `compliance_export_oscal_ssp`
**Expected**: OSCAL SSP JSON/XML with system metadata, control implementations

---

### SCA-28: Validate OSCAL SSP

```text
Validate the OSCAL SSP for Eagle Eye against NIST SP 800-53 Rev 5
```

**Expected Tool**: `compliance_validate_oscal_ssp`
**Expected**: Validation results; schema compliance; content issues flagged

---

### SCA-29: Check Privacy Compliance

```text
Check privacy compliance status for Eagle Eye
```

**Expected Tool**: `compliance_check_privacy_compliance`
**Expected**: Privacy compliance summary: PTA complete, PIA approved

---

### → Handoff: SCA → ISSM (Package Preparation)

> **Action**: Deactivate Auditor. Activate Compliance.SecurityLead. Stay on Teams.

---

### ISSM Package Assembly

**Active Persona**: ISSM | **Role**: `Compliance.SecurityLead` | **Interface**: Teams

---

### ISSM-23a: Create POA&M (from Assessment)

**Precondition**: SCA-20 (cross-persona)

```text
Create a POA&M item for finding {finding_id} — scheduled completion in
90 days
```

> Replace `{finding_id}` with a finding ID from `compliance_assess` run (SCA-20).

**Expected Tool**: `compliance_create_poam`
**Expected**: POA&M record; finding linked (source = assessment); status = "Ongoing"; completion = today + 90 days

---

### ISSM-23b: Create POA&M (from STIG/Scan Import)

**Precondition**: ISSO-09 or ISSO-10 (cross-persona)

```text
Create a POA&M item for finding {finding_id} — scheduled completion in
90 days
```

> Replace `{finding_id}` with a finding ID from CKL/XCCDF import.

**Expected Tool**: `compliance_create_poam`
**Expected**: POA&M record; finding linked (source = import); status = "Ongoing"

---

### ISSM-23c: Create POA&M (from Prisma Cloud)

**Precondition**: ISSM-19 or ISSM-20

```text
Create a POA&M item for finding {finding_id} — scheduled completion in
90 days
```

> Replace `{finding_id}` with a Prisma finding ID.

**Expected Tool**: `compliance_create_poam`
**Expected**: POA&M record; Prisma fields preserved (PrismaAlertId, CloudResourceType); status = "Ongoing"

---

### ISSM-24: List POA&M Items

**Precondition**: ISSM-23a/b/c

```text
Show all POA&M items for Eagle Eye
```

**Expected Tool**: `compliance_list_poam`
**Expected**: List with status, severity, scheduled dates, finding references

---

### ISSM-25: Generate RAR

**Precondition**: SCA-17 (cross-persona)

```text
Generate the Risk Assessment Report for Eagle Eye
```

**Expected Tool**: `compliance_generate_rar`
**Expected**: RAR with risk characterization, finding summary, residual risk assessment

---

### ISSM-26: Create Remediation Board

**Precondition**: SCA-06 to SCA-09 (cross-persona)

```text
Create a Kanban remediation board from Eagle Eye's assessment
```

**Expected Tool**: `kanban_create_board`
**Expected**: Board with tasks per open finding; status counts (ToDo/InProgress/Done)
**Record**: board_id = _______________

---

### ISSM-27: Bulk Assign Tasks

**Precondition**: ISSM-26

```text
Assign all CAT I tasks on Eagle Eye's board to SSgt Rodriguez
```

**Expected Tool**: `kanban_bulk_update`
**Expected**: Multiple tasks assigned; updated count confirmed

---

### ISSM-28: Export Kanban to POA&M

**Precondition**: ISSM-26

```text
Export Eagle Eye's remediation board as POA&M
```

**Expected Tool**: `kanban_export`
**Expected**: POA&M-formatted export with all open tasks, milestones, responsible parties

---

### ERR-05: Bundle Incomplete Package (Error)

> Test before all artifacts exist (or conceptual).

```text
Bundle authorization package for Eagle Eye
```

**Expected Tool**: `compliance_bundle_authorization_package`
**Expected**: Package created with warnings array listing missing artifacts (soft failure, not hard)
**Verify**: [ ] Package returned [ ] Warnings present

---

## Phase 5 — Authorize

### ISSM Package Submission

**Active Persona**: ISSM | **Role**: `Compliance.SecurityLead` | **Interface**: Teams

---

### ISSM-29: Bundle Authorization Package

**Precondition**: ISSM-18 (SSP) + SCA-17 (SAR) + ISSM-25 (RAR) + ISSM-23a/b/c (POA&M)

```text
Bundle the authorization package for Eagle Eye
```

**Expected Tool**: `compliance_bundle_authorization_package`
**Expected**: Package with SSP + SAR + RAR + POA&M + CRM; completeness check

---

### ISSM-30: Advance to Authorize

**Precondition**: ISSM-29

```text
Move Eagle Eye to the Authorize phase
```

**Expected Tool**: `compliance_advance_rmf_step`
**Expected**: RMF step = "Authorize"

---

### ISSM-31: View Risk Register

**Precondition**: SCA-06 to SCA-09 (cross-persona)

```text
Show the risk register for Eagle Eye
```

**Expected Tool**: `compliance_show_risk_register`
**Expected**: Risk entries with severity, status, mitigation, residual risk

---

### → Handoff: ISSM → AO (Authorization Decision)

> **Action**: Deactivate SecurityLead. Activate Compliance.AuthorizingOfficial. Stay on Teams.

**AO Preconditions**:
- ✓ SAR generated (SCA-17)
- ✓ RAR generated (SCA-18 / ISSM-25)
- ✓ Authorization package bundled (ISSM-29)
- ✓ System in Authorize phase (ISSM-30)

---

### AO Authorization Decisions

**Active Persona**: AO | **Role**: `Compliance.AuthorizingOfficial` | **Interface**: Teams (Adaptive Cards)

---

### AO-01: View Portfolio Dashboard

```text
Show the multi-system compliance dashboard
```

**Expected Tool**: `compliance_multi_system_dashboard`
**Expected**: All systems with RMF step, auth status, score, open findings, expiration

---

### AO-02: Review Authorization Package

```text
Show the authorization package summary for Eagle Eye
```

**Expected Tool**: `compliance_bundle_authorization_package`
**Expected**: SSP status, SAR summary, RAR summary, POA&M count, CRM status

---

### AO-03: View Risk Register

```text
Show the risk register for Eagle Eye
```

**Expected Tool**: `compliance_show_risk_register`
**Expected**: Risk entries with severity, control, status, mitigation, residual risk

---

### AO-04: Issue ATO

```text
Issue an ATO for Eagle Eye expiring January 15, 2028 with Low residual
risk — all CAT I findings remediated, 2 CAT III findings accepted
```

**Expected Tool**: `compliance_issue_authorization`
**Expected**: type=ATO, expiration=2028-01-15, residual risk=Low

---

### AO-05: Issue ATO with Conditions

```text
Issue an ATO with Conditions for Eagle Eye — condition: CAT II finding
on SI-4 must be remediated within 60 days
```

**Expected Tool**: `compliance_issue_authorization`
**Expected**: type=ATOwC, conditions with SI-4 deadline

---

### AO-06: Issue IATT

```text
Issue an Interim Authorization to Test for Eagle Eye for 90 days —
limited to development environment only
```

**Expected Tool**: `compliance_issue_authorization`
**Expected**: type=IATT, 90-day duration, scope limited to dev

---

### AO-07: Deny Authorization (DATO)

```text
Deny authorization for Eagle Eye — 3 unmitigated CAT I findings present
unacceptable risk to the mission
```

**Expected Tool**: `compliance_issue_authorization`
**Expected**: type=DATO, denial rationale recorded

---

### AO-08: Accept Risk

```text
Accept the risk on finding {finding_id} — the compensating control in
AC-3 adequately mitigates the risk
```

**Expected Tool**: `compliance_accept_risk`
**Expected**: Risk status = Accepted; rationale + compensating control saved

---

### AO-09: Check ATO Expirations

```text
What ATOs expire in the next 90 days?
```

**Expected Tool**: `compliance_track_ato_expiration`
**Expected**: Systems with ATOs expiring in 90 days; alert levels per system

---

### AO-10: View Compliance Trend

```text
Show compliance score trend for Eagle Eye
```

**Expected Tool**: `watch_compliance_trend`
**Expected**: Score progression over time

---

### AO-11: View Critical Alerts

```text
Show all critical alerts across my authorized systems
```

**Expected Tool**: `watch_show_alerts`
**Expected**: Critical alerts from all AO-authorized systems

---

### AO-15: Review SSP Completeness

```text
Show SSP completeness for Eagle Eye — I need to verify all sections are
complete before issuing my authorization decision
```

**Expected Tool**: `compliance_ssp_completeness`
**Expected**: Overall completion %; per-section status

---

### AO-16: Export OSCAL SSP for Package Review

```text
Export the OSCAL SSP for Eagle Eye — I need the machine-readable
version for the authorization package
```

**Expected Tool**: `compliance_export_oscal_ssp`
**Expected**: OSCAL SSP artifact with system metadata, control implementations

---

### AO RBAC Denial Tests

> All 3 must return **403 Forbidden**.

### AO-12: DENIED — Modify SSP

```text
Write narrative for AC-2 on Eagle Eye
```

**Expected**: **403 Forbidden** — AO cannot modify SSP

---

### AO-13: DENIED — Fix Findings

```text
Remediate finding {finding_id}
```

**Expected**: **403 Forbidden** — AO cannot remediate

---

### AO-14: DENIED — Assess Controls

```text
Assess AC-2 as Satisfied
```

**Expected**: **403 Forbidden** — only SCA can assess

---

### → Handoff: AO → ISSM (Monitor Setup)

> **Action**: Deactivate AuthorizingOfficial. Activate Compliance.SecurityLead. Stay on Teams.

---

## Phase 6 — Monitor

### ISSM ConMon & Oversight

**Active Persona**: ISSM | **Role**: `Compliance.SecurityLead` | **Interface**: Teams

---

### ISSM-32: Create ConMon Plan

**Precondition**: AO-04 (cross-persona: AO issues ATO)

```text
Create a continuous monitoring plan for Eagle Eye with monthly
assessments and quarterly reviews
```

**Expected Tool**: `compliance_create_conmon_plan`
**Expected**: ConMon plan with frequency, review dates, stakeholder list

---

### ISSM-33: Generate ConMon Report

**Precondition**: ISSM-32

```text
Generate the monthly ConMon report for Eagle Eye
```

**Expected Tool**: `compliance_generate_conmon_report`
**Expected**: Compliance score, baseline delta, finding trends, POA&M status
**Record**: compliance_score = ___

---

### ISSM-34: Track ATO Expiration

**Precondition**: AO-04 (cross-persona)

```text
When does Eagle Eye's ATO expire?
```

**Expected Tool**: `compliance_track_ato_expiration`
**Expected**: Alert level, days remaining, recommended action

---

### ISSM-35: Report Significant Change

**Precondition**: AO-04 (cross-persona)

```text
Report a significant change for Eagle Eye — new interconnection with
DISA SIPR gateway
```

**Expected Tool**: `compliance_report_significant_change`
**Expected**: Change recorded; `requires_reauthorization = true`

---

### ISSM-36: Check Reauthorization Triggers

**Precondition**: AO-04 + ISSM-35

```text
Check if Eagle Eye needs reauthorization
```

**Expected Tool**: `compliance_reauthorization_workflow`
**Expected**: Triggers: expiration status, significant changes, compliance drift

---

### ISSM-37: Multi-System Dashboard

```text
Show the multi-system compliance dashboard
```

**Expected Tool**: `compliance_multi_system_dashboard`
**Expected**: Portfolio view with all systems

---

### ISSM-38: Export to eMASS

**Precondition**: ISSM-11 + SCA assessments (cross-persona)

```text
Export Eagle Eye to eMASS format
```

**Expected Tool**: `compliance_export_emass`
**Expected**: eMASS-compatible Excel with system, controls, findings, POA&M sheets

---

### ISSM-39: View Audit Log

```text
Show the audit log for Eagle Eye
```

**Expected Tool**: `compliance_audit_log`
**Expected**: Chronological trail: user, action, timestamp, entity

---

### → Handoff: ISSM → Engineer (Remediation)

> **Action**: Deactivate SecurityLead. Activate Compliance.PlatformEngineer. Switch to VS Code.

---

### Engineer Kanban Remediation Workflow

**Active Persona**: Engineer | **Role**: `Compliance.PlatformEngineer` | **Interface**: VS Code `@ato`

---

### ENG-11: View Assigned Tasks

```text
@ato Show my assigned remediation tasks
```

**Expected Tool**: `kanban_task_list`
**Expected**: Task list with severity, control ID, status, due date
**Record**: Task count: ____ | First task: REM-___

---

### ENG-12: Get Task Details

```text
@ato Show details of task REM-{id}
```

**Expected Tool**: `kanban_get_task`
**Expected**: Control ID, finding details, affected resources, remediation script, SLA

---

### ENG-13: Move Task to In Progress

```text
@ato Move task REM-{id} to In Progress
```

**Expected Tool**: `kanban_move_task`
**Expected**: Status: ToDo → InProgress; auto-assigns to current user

---

### ENG-14: Fix with Kanban Dry Run

```text
@ato Fix task REM-{id} with dry run
```

**Expected Tool**: `kanban_remediate_task`
**Expected**: Preview only; scoped to task's finding; no changes applied

---

### ENG-15: Apply Kanban Remediation

**Precondition**: ENG-14 reviewed

```text
@ato Apply fix for task REM-{id}
```

**Expected Tool**: `kanban_remediate_task`
**Expected**: Remediation applied; task finding updated; validation queued

---

### ENG-16: Validate Task

**Precondition**: ENG-15

```text
@ato Validate task REM-{id}
```

**Expected Tool**: `kanban_task_validate`
**Expected**: Pass (resolved) or Fail (remaining issues)
**Record**: Validation: ☐ Pass / ☐ Fail

---

### ENG-17: Collect Evidence for Task

**Precondition**: ENG-16 passed

```text
@ato Collect evidence for task REM-{id}
```

**Expected Tool**: `kanban_collect_evidence`
**Expected**: Evidence with SHA-256; linked to task and finding
**Record**: Evidence hash (first 8 chars): __________

---

### ENG-18: Add Comment to Task

```text
@ato Add comment on task REM-{id}: Remediation applied, waiting for DNS propagation before final validation
```

**Expected Tool**: `kanban_add_comment`
**Expected**: Comment saved with timestamp and author

---

### ENG-19: Move Task to In Review

**Precondition**: ENG-16 passed

```text
@ato Move task REM-{id} to In Review
```

**Expected Tool**: `kanban_move_task`
**Expected**: InProgress → InReview; ISSO notified

---

### ENG-20: View Prisma Findings with Remediation

**Precondition**: ISSM-19/20

```text
@ato Show open Prisma Cloud findings for Eagle Eye with remediation steps
```

**Expected Tool**: `watch_show_alerts` / findings query
**Expected**: Prisma findings with RemediationGuidance, RemediationScript, AutoRemediable flag
**Record**: Prisma findings: ____ | Auto-remediable: ____

---

### ENG-21: View Prisma CLI Scripts

**Precondition**: ISSM-20

```text
@ato What CLI scripts are available for Eagle Eye Prisma findings?
```

**Expected**: Findings with RemediationCli populated (Azure CLI/PowerShell)

---

### ENG-22: Prisma Trend by Resource Type

**Precondition**: ISSM-19 + ISSM-20 + ISSM-40

```text
@ato Show Prisma trend for Eagle Eye grouped by resource type
```

**Expected Tool**: `compliance_prisma_trend`
**Expected**: Trend grouped by resource type with open/closed/total

---

### Engineer RBAC Denial Tests

> All 4 must return **403 Forbidden**.

### ENG-23: DENIED — Assess Control

```text
@ato Assess AC-2 as Satisfied
```

**Expected**: **403 Forbidden** — only SCA can assess

---

### ENG-24: DENIED — Issue Authorization

```text
@ato Issue ATO for Eagle Eye
```

**Expected**: **403 Forbidden** — only AO can authorize

---

### ENG-25: DENIED — Dismiss Alert

```text
@ato Dismiss alert ALT-{id}
```

**Expected**: **403 Forbidden** — only ISSM can dismiss

---

### ENG-26: DENIED — Register System

```text
@ato Register a new system called Test
```

**Expected**: **403 Forbidden** — only ISSM can register systems

---

### → Handoff: Engineer → ISSM (Post-Remediation Verification)

> **Action**: Deactivate PlatformEngineer. Activate Compliance.SecurityLead. Switch to Teams.

---

### ISSM Post-Remediation & ISSO Monitoring

**Active Persona**: ISSM | **Role**: `Compliance.SecurityLead` | **Interface**: Teams

---

### ISSM-40: Re-Import Prisma After Remediation

**Precondition**: ISSM-19 + ENG-08 (cross-persona)

```text
Import the latest Prisma Cloud scan for Eagle Eye to verify remediation
```

**Expected Tool**: `compliance_import_prisma_csv`
**Expected**: New import; previously open findings resolved; trend shows improvement

---

### → Handoff: ISSM → ISSO (Ongoing Monitoring)

> **Action**: Deactivate SecurityLead. Activate Compliance.Analyst. Switch to VS Code.

**Active Persona**: ISSO | **Role**: `Compliance.Analyst` | **Interface**: VS Code `@ato`

---

### ISSO-20: Generate ConMon Report

**Precondition**: ISSM-32

```text
@ato Generate the February 2026 ConMon report for Eagle Eye
```

**Expected Tool**: `compliance_generate_conmon_report`
**Expected**: Monthly report with compliance score, delta, trends, POA&M status

---

### ISSO-21: Report Significant Change

**Precondition**: ATO granted

```text
@ato Report that Eagle Eye added a new API Management gateway
```

**Expected Tool**: `compliance_report_significant_change`
**Expected**: Change recorded; `requires_reauthorization` flag set

---

---

## RBAC Verification Summary

| TC-ID | Persona | Action Attempted | Required Role | Expected |
|-------|---------|-----------------|---------------|----------|
| SCA-21 | SCA | Write narrative | ISSO/Engineer | 403 |
| SCA-22 | SCA | Remediate | Engineer | 403 |
| SCA-23 | SCA | Issue authorization | AO | 403 |
| SCA-24 | SCA | Dismiss alert | ISSM | 403 |
| AO-12 | AO | Modify SSP | ISSO/Engineer | 403 |
| AO-13 | AO | Fix findings | Engineer | 403 |
| AO-14 | AO | Assess controls | SCA | 403 |
| ENG-23 | Engineer | Assess control | SCA | 403 |
| ENG-24 | Engineer | Issue authorization | AO | 403 |
| ENG-25 | Engineer | Dismiss alert | ISSM | 403 |
| ENG-26 | Engineer | Register system | ISSM | 403 |

**RBAC Pass Criteria**: All 11 return 403 → ___/11

---

## Error Handling Summary

| TC-ID | Scenario | Expected |
|-------|----------|----------|
| ERR-01 | Skip RMF phases | Error — phase ordering enforced |
| ERR-02 | Malformed Prisma CSV | Error — parsing failed, no findings created |
| ERR-03 | Re-categorize | Upsert or phase error |
| ERR-04 | SAR with zero assessments | Warning — no data |
| ERR-05 | Incomplete package | Soft warnings, package still created |
| ERR-06 | Re-finalize SAP | Error — already finalized |
| ERR-07 | Update finalized SAP | Error — immutable |
| ERR-08 | Remediate non-existent finding | Error — not found |

**Error Handling Pass Criteria**: All 8 return appropriate errors → ___/8

---

## Persona Handoff Summary

| # | From | To | Trigger Point | Interface Change |
|---|------|----|--------------|-----------------|
| 1 | ISSM | ISSO | System in Implement (ISSM-16) | Teams → VS Code |
| 2 | ISSO | Engineer | SSP authored, scans imported | Stay in VS Code |
| 3 | Engineer | ISSO | Implementation complete | Stay in VS Code |
| 4 | ISSO | ISSM | SSP ready for generation | VS Code → Teams |
| 5 | ISSM | SCA | SAP finalized, SSP complete | Stay on Teams |
| 6 | SCA | ISSM | SAR delivered, assessment complete | Stay on Teams |
| 7 | ISSM | AO | Auth package bundled | Stay on Teams |
| 8 | AO | ISSM | ATO issued | Stay on Teams |
| 9 | ISSM | Engineer | Kanban board created, tasks assigned | Teams → VS Code |
| 10 | Engineer | ISSM | Remediation complete | VS Code → Teams |
| 11 | ISSM | ISSO | ConMon plan created | Teams → VS Code |

---

## Overall Results

| Section | Test Cases | Passed | Failed | Blocked |
|---------|-----------|--------|--------|---------|
| Auth/PIM (AUTH-01–08) | 8 | ___/8 | | |
| Prepare (ISSM-01–06) | 6 | ___/6 | | |
| Categorize (ISSM-07–10) | 4 | ___/4 | | |
| Select (ISSM-11–16) | 6 | ___/6 | | |
| Implement — ISSM oversight (ISSM-17–22, 41–55) | 18 | ___/18 | | |
| Implement — ISSO authoring (ISSO-01–12, 19, 25–34) | 21 | ___/21 | | |
| Implement — Engineer build (ENG-01–10, 27–30) | 14 | ___/14 | | |
| Monitor — ISSO day-to-day (ISSO-13–18, 20–24) | 12 | ___/12 | | |
| Assess — SCA (SCA-01–29) | 25 | ___/25 | | |
| Assess prep — ISSM (ISSM-23a/b/c–28) | 8 | ___/8 | | |
| Authorize — ISSM submit (ISSM-29–31) | 3 | ___/3 | | |
| Authorize — AO decide (AO-01–16) | 13 | ___/13 | | |
| Monitor — ISSM ConMon (ISSM-32–40) | 9 | ___/9 | | |
| Monitor — Engineer Kanban (ENG-11–22) | 12 | ___/12 | | |
| RBAC Denial tests | 11 | ___/11 | | |
| Error handling (ERR-01–08) | 8 | ___/8 | | |
| **Total** | **172** | **___/172** | | |

### Key Artifacts Tracker

| Artifact | Value | Source |
|----------|-------|--------|
| System ID | _______________ | ISSM-01 |
| SAP ID | _______________ | ISSM-41 |
| SAP Hash | _______________ | ISSM-43 |
| Pre-Assessment Snapshot | _______________ | SCA-01 |
| Post-Assessment Snapshot | _______________ | SCA-13 |
| Board ID | _______________ | ISSM-26 |
| Evidence Hash | _______________ | ISSO-19 |
| Compliance Score | _______________ | SCA-20 |
| SAP-SAR Alignment | ___% | SCA-16 |
| PTA ID | _______________ | ISSM-44 |
| PIA ID | _______________ | ISSM-46 |
| Interconnection ID | _______________ | ISSM-48 |
| ISA ID | _______________ | ISSM-52 |
| Agreement ID | _______________ | ISSM-53 |
| SSP Completion | ___% | SCA-25 |
| OSCAL SSP Controls | ___ | SCA-27 |

### Issues Found

| # | TC-ID | Severity | Description | Root Cause |
|---|-------|----------|-------------|------------|
| | | | | |

**Overall Status**: ☐ PASS / ☐ FAIL | **Tester**: __________ | **Date**: __________
