# ISSM Guide — RMF System Registration Workflow

> Feature 015: Persona-Driven RMF Workflows

This guide walks an Information System Security Manager (ISSM) through the system registration and RMF lifecycle workflow using the ATO Copilot MCP tools.

---

## Prerequisites

- Access to the ATO Copilot MCP server
- Appropriate compliance role credentials
- Knowledge of the system's Azure resource inventory

---

## Workflow Overview

```
1. Register System
2. Define Authorization Boundary
3. Assign RMF Roles
4. Advance to Categorize Phase
5. (Continue through RMF lifecycle...)
```

---

## Step 1: Register the System

Use `compliance_register_system` to register a new information system:

```
Tool: compliance_register_system
Parameters:
  name: "My Application System"
  system_type: "MajorApplication"
  mission_criticality: "MissionCritical"
  hosting_environment: "Government"
  acronym: "MAS"
  description: "Primary mission application hosted in Azure Government"
```

The system starts in the **Prepare** phase. Note the returned `id` — you'll need it for all subsequent operations.

**System Types:**
- `MajorApplication` — Standalone application performing a specific function
- `GeneralSupportSystem` — Interconnected set of IT resources under same control
- `Enclave` — Collection of computing environments within a defined boundary
- `PlatformIt` — Shared IT infrastructure service
- `CloudServiceOffering` — Cloud-based service offering

---

## Step 2: Define the Authorization Boundary

Add Azure resources to the system's authorization boundary:

```
Tool: compliance_define_boundary
Parameters:
  system_id: "<system-guid>"
  resources:
    - resource_id: "/subscriptions/xxx/resourceGroups/rg-prod/providers/Microsoft.Compute/virtualMachines/vm-app"
      resource_type: "Microsoft.Compute/virtualMachines"
      resource_name: "Production App Server"
    - resource_id: "/subscriptions/xxx/resourceGroups/rg-prod/providers/Microsoft.Sql/servers/sql-prod"
      resource_type: "Microsoft.Sql/servers"
      resource_name: "Production Database"
```

If a resource should be excluded (e.g., managed by another team):

```
Tool: compliance_exclude_from_boundary
Parameters:
  system_id: "<system-guid>"
  resource_id: "/subscriptions/xxx/resourceGroups/rg-shared/providers/..."
  rationale: "Managed under separate ATO by shared services team"
```

---

## Step 3: Assign RMF Roles

Assign required personnel roles:

```
Tool: compliance_assign_rmf_role
Parameters:
  system_id: "<system-guid>"
  role: "Issm"
  user_id: "jane.smith@agency.gov"
  user_display_name: "Jane Smith"
```

**Required Roles:**
| Role | Description |
|------|-------------|
| `AuthorizingOfficial` | Senior official who accepts risk and grants ATO |
| `Issm` | Manages the security program for assigned systems |
| `Isso` | Implements and monitors security controls day-to-day |
| `Sca` | Assesses security controls for effectiveness |
| `SystemOwner` | Program manager responsible for the system |

List current role assignments with:

```
Tool: compliance_list_rmf_roles
Parameters:
  system_id: "<system-guid>"
```

---

## Step 4: Advance to Categorize

Once the system has at least one role assigned and at least one boundary resource, you can advance to the Categorize phase:

```
Tool: compliance_advance_rmf_step
Parameters:
  system_id: "<system-guid>"
  target_step: "Categorize"
```

### Gate Requirements

Each transition has specific gate conditions that must be met:

| From → To | Requirements |
|-----------|-------------|
| Prepare → Categorize | ≥1 RMF role assigned + ≥1 boundary resource |
| Categorize → Select | Security categorization defined + ≥1 information type |
| Select → Implement | Control baseline exists |
| Implement → Assess | Advisory check (no hard block) |
| Assess → Authorize | Advisory check (no hard block) |
| Authorize → Monitor | Advisory check (no hard block) |

If gate conditions aren't met, the tool returns detailed `gate_results` explaining what's missing.

### Force Override

In exceptional cases, an authorized official can force-advance past failed gates:

```
Tool: compliance_advance_rmf_step
Parameters:
  system_id: "<system-guid>"
  target_step: "Categorize"
  force: true
```

Force overrides are logged for audit purposes. Use sparingly.

### Regression (Moving Backward)

Moving to an earlier phase always requires `force: true` and is logged as a regression event:

```
Tool: compliance_advance_rmf_step
Parameters:
  system_id: "<system-guid>"
  target_step: "Prepare"
  force: true
```

---

## Step 5: View System Status

At any time, retrieve the full system status:

```
Tool: compliance_get_system
Parameters:
  system_id: "<system-guid>"
```

This returns the current RMF phase, security categorization, boundary resource count, role assignments, and control baseline (if defined).

To see all registered systems:

```
Tool: compliance_list_systems
Parameters:
  page: 1
  page_size: 20
  active_only: true
```

---

## Security Categorization Workflow (User Story 2)

After the system is registered and advanced to the **Categorize** phase, the ISSM performs FIPS 199 categorization.

### Step 1: Get Suggested Information Types

Use the AI-assisted suggestion tool to identify relevant SP 800-60 information types:

```
Tool: compliance_suggest_info_types
Parameters:
  system_id: "<system-guid>"
  description: "Financial management and audit logging system"
```

The tool returns a ranked list of suggested information types with confidence scores, SP 800-60 identifiers, and default C/I/A impact levels.

### Step 2: Categorize the System

Apply the selected information types with their impact levels:

```
Tool: compliance_categorize_system
Parameters:
  system_id: "<system-guid>"
  information_types:
    - sp800_60_id: "C.3.1.4"
      name: "Financial Management"
      confidentiality_impact: "Moderate"
      integrity_impact: "Moderate"
      availability_impact: "Low"
    - sp800_60_id: "C.3.5.8"
      name: "Information Security"
      confidentiality_impact: "Moderate"
      integrity_impact: "High"
      availability_impact: "Moderate"
  justification: "Categorized per mission requirements and SP 800-60 Vol. 2"
```

The tool computes:
- **High-water mark**: Maximum C/I/A across all information types → Overall categorization
- **DoD Impact Level**: Low→IL2, Moderate→IL4, High→IL5
- **NIST Baseline**: Derived from overall categorization
- **FIPS 199 Notation**: Formal `SC System = {(confidentiality, X), ...}` string

### Step 3: Verify Categorization

Retrieve and review the stored categorization:

```
Tool: compliance_get_categorization
Parameters:
  system_id: "<system-guid>"
```

### Adjusting Impact Levels

If a provisional impact level needs adjustment, set `uses_provisional: false` and provide `adjustment_justification`:

```json
{
  "sp800_60_id": "C.3.1.4",
  "name": "Financial Management",
  "confidentiality_impact": "High",
  "integrity_impact": "Moderate",
  "availability_impact": "Low",
  "uses_provisional": false,
  "adjustment_justification": "Elevated confidentiality due to PII and financial data"
}
```

### Re-categorization

Calling `compliance_categorize_system` again fully **replaces** the previous categorization. The old information types are removed and replaced with the new set.

---

## Next Steps

After completing security categorization, the ISSM continues with control baseline selection and tailoring.

---

## Step 5: Select Control Baseline

Use `compliance_select_baseline` to select the NIST SP 800-53 control baseline matching the system's FIPS 199 categorization. The system must already be categorized.

```
Tool: compliance_select_baseline
Parameters:
  system_id: "<system-guid>"
  apply_overlay: true
```

**Baseline Levels:**
- **Low** — 152 controls (IL2 systems)
- **Moderate** — 329 controls (IL4 systems)
- **High** — 400 controls (IL5/IL6 systems)

**CNSSI 1253 Overlay:** When `apply_overlay` is true (default), the tool automatically applies the CNSSI 1253 overlay matching the DoD Impact Level derived from categorization. This adds enhancement controls specific to the IL.

### Re-selecting a Baseline

Calling `compliance_select_baseline` again fully **replaces** the previous baseline, including all tailoring and inheritance records. Use this after re-categorization.

---

## Step 6: Tailor the Baseline

Use `compliance_tailor_baseline` to add organization-specific controls or remove non-applicable controls:

```
Tool: compliance_tailor_baseline
Parameters:
  system_id: "<system-guid>"
  tailoring_actions:
    - control_id: "ZZ-99"
      action: "Added"
      rationale: "Organization-specific security monitoring control"
    - control_id: "PE-5"
      action: "Removed"
      rationale: "Not applicable — system is 100% cloud-hosted with no physical media"
```

**Tailoring Best Practices:**
- Always provide a meaningful rationale — this is captured in the audit trail
- Overlay-required controls can be removed but will generate a WARNING
- Added controls appear in the baseline and CRM alongside NIST controls
- Review tailoring decisions with the AO before proceeding

---

## Step 7: Set Control Inheritance

Use `compliance_set_inheritance` to map controls to their inheritance provider (e.g., a FedRAMP-authorized CSP):

```
Tool: compliance_set_inheritance
Parameters:
  system_id: "<system-guid>"
  inheritance_mappings:
    - control_id: "AC-1"
      inheritance_type: "Inherited"
      provider: "Azure Government (FedRAMP High)"
    - control_id: "AC-2"
      inheritance_type: "Shared"
      provider: "Azure Government"
      customer_responsibility: "Customer configures access policies and reviews accounts quarterly"
    - control_id: "AU-2"
      inheritance_type: "Customer"
```

**Inheritance Types:**
- **Inherited** — Fully satisfied by the CSP (e.g., physical security controls for cloud-hosted systems)
- **Shared** — Partially satisfied by the CSP; customer has documented responsibility
- **Customer** — Entirely the customer's responsibility to implement

---

## Step 8: Generate Customer Responsibility Matrix

Use `compliance_generate_crm` to generate a CRM grouped by NIST 800-53 control family:

```
Tool: compliance_generate_crm
Parameters:
  system_id: "<system-guid>"
```

The CRM shows inheritance coverage by family and highlights undesignated controls that still need inheritance mapping. Use this to:
- Track SSP completion progress
- Identify gaps in inheritance documentation
- Report to the AO on control responsibility distribution

---

## Further Steps

After completing baseline selection and tailoring, the ISSM workflow continues with:

- **User Story 4**: Control Implementation — Document how each control is implemented
- **User Story 5**: Assessment — Evaluate control effectiveness with the SCA role
- **User Story 6/7**: Assessment Artifacts — Snapshots, evidence verification, SAR generation
- **User Story 8**: Authorization — POA&M, RAR, authorization package (see below)

---

## Authorization Workflow (US8)

After assessment is complete, the ISSM prepares the authorization package for the Authorizing Official (AO).

### Create POA&M Items

For each open finding, create a formal Plan of Action & Milestones entry:

```json
{
  "system_id": "<system-guid>",
  "finding_id": "<finding-guid>",
  "weakness": "Missing MFA enforcement for privileged accounts",
  "control_id": "IA-2(1)",
  "cat_severity": "CatI",
  "poc": "John Smith",
  "scheduled_completion": "2025-06-30",
  "resources_required": "Azure AD P2 license and 20 hours engineering",
  "milestones": "[{\"description\":\"Configure Conditional Access policies\",\"target_date\":\"2025-03-31\"},{\"description\":\"Enable MFA for all admins\",\"target_date\":\"2025-05-31\"},{\"description\":\"Validate and close finding\",\"target_date\":\"2025-06-30\"}]"
}
```

Tool: `compliance_create_poam`

The POA&M links to the specific finding and NIST control, with a CAT severity and milestone schedule.

### List & Track POA&M Items

Monitor POA&M progress with filtering:

```json
{
  "system_id": "<system-guid>",
  "status_filter": "Ongoing",
  "overdue_only": "true"
}
```

Tool: `compliance_list_poam`

Filter options:
- **status_filter**: `Ongoing`, `Completed`, `Delayed`, `RiskAccepted`
- **severity_filter**: `CatI`, `CatII`, `CatIII`
- **overdue_only**: `true` to show only items past their scheduled completion

### Generate Risk Assessment Report (RAR)

Generate the RAR for AO review:

```json
{
  "system_id": "<system-guid>",
  "assessment_id": "<assessment-guid>"
}
```

Tool: `compliance_generate_rar`

The RAR includes:
- Executive summary with aggregate risk level
- Per-family risk breakdown (AC, AU, IA, etc.)
- CAT severity analysis (CAT I/II/III counts)
- Markdown content ready for inclusion in the authorization package

### Bundle Authorization Package

Compile all documents into a single authorization package:

```json
{
  "system_id": "<system-guid>",
  "include_evidence": "true"
}
```

Tool: `compliance_bundle_authorization_package`

The package includes:

| Document | Source |
|----------|--------|
| System Security Plan (SSP) | From `ComplianceDocuments` |
| Security Assessment Report (SAR) | From `ComplianceDocuments` |
| Risk Assessment Report (RAR) | Generated dynamically |
| Plan of Action & Milestones (POA&M) | Generated from POA&M items |
| Customer Responsibility Matrix (CRM) | From `ComplianceDocuments` |
| ATO Letter | From `ComplianceDocuments` |

Documents not yet created will show as `not_found` in the bundle status.

### View Risk Register

After AO issues a decision, review accepted risks:

```json
{
  "system_id": "<system-guid>",
  "status_filter": "active"
}
```

Tool: `compliance_show_risk_register`

The risk register shows all risk acceptances with their expiration dates, compensating controls, and finding details. Past-due acceptances are automatically expired on query.

---

## Complete ISSM Workflow

```
1. Register System                    → compliance_register_system
2. Define Authorization Boundary      → compliance_define_boundary
3. Assign RMF Roles                   → compliance_assign_rmf_role
4. Categorize System (FIPS 199)       → compliance_categorize_system
5. Select Control Baseline            → compliance_select_baseline
6. Tailor & Set Inheritance           → compliance_tailor_baseline, compliance_set_inheritance
7. Write Control Narratives           → compliance_write_narrative
8. Generate SSP                       → compliance_generate_ssp
9. Assess Controls (with SCA)         → compliance_assess_control
10. Generate SAR                      → compliance_generate_sar
11. Create POA&M Items                → compliance_create_poam
12. Generate RAR                      → compliance_generate_rar
13. Bundle Authorization Package      → compliance_bundle_authorization_package
14. AO Issues Decision                → compliance_issue_authorization (AO only)
15. Monitor Risk Register             → compliance_show_risk_register
```

## Continuous Monitoring Workflow (US9)

### Creating a ConMon Plan

After authorization, establish a continuous monitoring plan to track ongoing compliance:

```text
Create ConMon plan for system {system_id} with monthly assessments
```

The `compliance_create_conmon_plan` tool creates (or updates) a plan with:
- **Assessment frequency**: Monthly, Quarterly, or Annually
- **Annual review date**: When the full plan review occurs
- **Report distribution**: Who receives ConMon reports (role names or user IDs)
- **Significant change triggers**: Custom triggers beyond the 10 built-in types

### Generating Periodic Reports

Generate compliance reports that track score drift from the authorization baseline:

```text
Generate a monthly ConMon report for system {system_id}, period 2026-02
```

The `compliance_generate_conmon_report` tool produces:
- Current compliance score vs. authorized baseline
- New and resolved findings since last report
- Open and overdue POA&M items
- Markdown report content suitable for distribution
- **Watch data enrichment** (Phase 17): Monitoring enabled status, active drift alert count, auto-remediation rule count, and last monitoring check timestamp — automatically populated from ComplianceWatchService data when monitoring is configured for the system's subscriptions

### Tracking ATO Expiration

Monitor authorization expiration with graduated alerts:

```text
Check ATO expiration for system {system_id}
```

The `compliance_track_ato_expiration` tool provides alerts at:
- **90 days** (Info): Begin reauthorization planning
- **60 days** (Warning): Submit reauthorization package
- **30 days** (Urgent): Escalate to AO immediately
- **Expired**: System operating without authorization

**Phase 17 Enhancement**: Each alert level above "None" automatically creates a `ComplianceAlert` through the alert pipeline, triggering notifications via `AlertNotificationService`. Graduated severity: Low@90d, Medium@60d, High@30d, Critical@expired.

### Reporting Significant Changes

Report changes that may trigger reauthorization:

```text
Report a significant change for system {system_id}: New Interconnection — "Added VPN tunnel to partner org"
```

The `compliance_report_significant_change` tool automatically classifies whether the change requires reauthorization based on 10 built-in trigger types (New Interconnection, Major Upgrade, Data Type Change, etc.).

**Phase 17 Enhancement**: When a significant change requires reauthorization, a `ComplianceAlert` (type: Violation, severity: High) is automatically created and routed through the notification pipeline. Additionally, when ComplianceWatchService detects drift exceeding the configured threshold (default: 5 resources), it automatically reports a significant change of type `configuration_drift`.

### Reauthorization Workflow

Check for reauthorization triggers or initiate the workflow:

```text
Check reauthorization triggers for system {system_id}
Initiate reauthorization for system {system_id}
```

The `compliance_reauthorization_workflow` tool detects three trigger types:
1. ATO expiration (< 30 days remaining)
2. Unreviewed significant changes requiring reauthorization
3. Compliance score drift (> 10% below authorization baseline)

When initiated, the system's RMF step regresses to **Assess** and significant changes are marked as triggered.

### Multi-System Dashboard

View portfolio-wide compliance status:

```text
Show the multi-system compliance dashboard
```

The `compliance_multi_system_dashboard` tool provides an at-a-glance view of all systems with impact level, RMF step, authorization status, compliance score, open findings, POA&M items, and alert counts.

### Complete ISSM Workflow (Extended)

```
1.  Register System                    → compliance_register_system
2.  Define Authorization Boundary      → compliance_define_boundary
3.  Assign RMF Roles                   → compliance_assign_rmf_role
4.  Categorize System (FIPS 199)       → compliance_categorize_system
5.  Select Control Baseline            → compliance_select_baseline
6.  Tailor & Set Inheritance           → compliance_tailor_baseline, compliance_set_inheritance
7.  Write Control Narratives           → compliance_write_narrative
8.  Generate SSP                       → compliance_generate_ssp
9.  Assess Controls (with SCA)         → compliance_assess_control
10. Generate SAR                       → compliance_generate_sar
11. Create POA&M Items                 → compliance_create_poam
12. Generate RAR                       → compliance_generate_rar
13. Bundle Authorization Package       → compliance_bundle_authorization_package
14. AO Issues Decision                 → compliance_issue_authorization (AO only)
15. Monitor Risk Register              → compliance_show_risk_register
16. Create ConMon Plan                 → compliance_create_conmon_plan
17. Generate Periodic Reports          → compliance_generate_conmon_report
18. Track ATO Expiration               → compliance_track_ato_expiration
19. Report Significant Changes         → compliance_report_significant_change
20. Check Reauthorization Triggers     → compliance_reauthorization_workflow
21. View Portfolio Dashboard           → compliance_multi_system_dashboard
22. Send Notifications                 → compliance_send_notification
23. Export to eMASS (Excel)            → compliance_export_emass
24. Import from eMASS (Excel)          → compliance_import_emass
25. Export OSCAL JSON                  → compliance_export_oscal
26. Upload Document Template           → compliance_upload_template
27. List Document Templates            → compliance_list_templates
28. Update Document Template           → compliance_update_template
29. Delete Document Template           → compliance_delete_template
```

---

## eMASS & OSCAL Interoperability

ATO Copilot supports bidirectional data exchange with eMASS and OSCAL-compliant
systems, enabling ISSMs to work seamlessly across tools.

### Exporting to eMASS

Use `compliance_export_emass` to generate eMASS-compatible Excel spreadsheets:

- **Controls export**: Produces a `.xlsx` with standard eMASS column headers
  (System Name, Control Identifier, Implementation Status, Narrative, etc.)
- **POA&M export**: Produces a `.xlsx` matching the eMASS POA&M import template
  (POA&M ID, Weakness, Security Control Number, Milestones, etc.)
- **Full export**: Generates both worksheets in separate files

The exported files can be uploaded directly to eMASS without modification.

### Importing from eMASS

Use `compliance_import_emass` to ingest eMASS Excel exports:

1. **Dry-run first**: Always start with `dry_run: true` to preview changes
2. **Review conflicts**: Check the `conflict_details` array for mismatches
3. **Choose strategy**:
   - `skip` — Keep existing data, ignore conflicting imported values
   - `overwrite` — Replace existing data with imported values
   - `merge` — Combine narratives with separator, use latest status
4. **Apply changes**: Re-run with `dry_run: false` when satisfied

### OSCAL JSON Export

Use `compliance_export_oscal` for machine-readable compliance data:

- **SSP model**: Complete System Security Plan with control implementations
- **Assessment Results**: Assessment findings and effectiveness determinations
- **POA&M**: Plan of Action and Milestones with weakness details

All exports conform to OSCAL v1.0.6 specification and can be validated with
OSCAL validation tools.

---

## Document Templates & PDF Export

US11 adds the ability to upload custom DOCX templates and export compliance
documents in PDF and DOCX formats.

### Uploading Custom Templates

Use `compliance_upload_template` to upload a DOCX template for a specific
document type (SSP, SAR, POA&M, or RAR). Templates must be valid DOCX files
encoded as base64. The service validates:

- **File format** — Must be a valid ZIP archive with `word/document.xml`
- **Merge fields** — Scans for `{{field_name}}` placeholders and reports which
  are recognized, missing, or unknown for the document type

Each document type defines a schema of supported merge fields (e.g.,
`{{system_name}}`, `{{categorization}}`, `{{control_implementations}}` for SSP).
Templates with missing required fields still upload but produce warnings.

### Generating PDF Documents

Enhanced `compliance_generate_document` now accepts a `format` parameter:

| Format     | Output                              |
|------------|-------------------------------------|
| `markdown` | Markdown text (default, unchanged)  |
| `pdf`      | Base64-encoded PDF via QuestPDF     |
| `docx`     | Base64-encoded DOCX document        |

For PDF and DOCX formats, pass `system_id` to populate data from the database.
Optionally pass `template` with a template ID to use a previously uploaded
custom template for DOCX generation.

### Managing Templates

- `compliance_list_templates` — List all uploaded templates, optionally filtered
  by `document_type`
- `compliance_update_template` — Rename a template or replace its file content
- `compliance_delete_template` — Remove a template by ID

### Example Workflow

```text
1. Upload your organization's SSP template:
   compliance_upload_template(name="DISA SSP Template",
     document_type="ssp", file_base64="UEsDB...")

2. Generate a PDF from live data:
   compliance_generate_document(document_type="ssp",
     format="pdf", system_id="sys-001")

3. Generate DOCX using your custom template:
   compliance_generate_document(document_type="ssp",
     format="docx", system_id="sys-001", template="<template-id>")
```
