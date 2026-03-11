# MCP Server API Reference

> All MCP tools grouped by RMF lifecycle phase with parameters and response schemas.

---

## Table of Contents

- [Transport](#transport)
- [Authentication](#authentication)
- [Phase 1: Prepare](#phase-1-prepare)
- [Phase 2: Categorize](#phase-2-categorize)
- [Phase 3: Select](#phase-3-select)
- [Phase 4: Implement](#phase-4-implement)
- [Phase 5: Assess](#phase-5-assess)
- [Phase 6: Authorize](#phase-6-authorize)
- [Phase 7: Monitor](#phase-7-monitor)
- [Interoperability](#interoperability)
- [Document Templates](#document-templates)
- [CAC Authentication](#cac-authentication)
- [PIM — Privileged Identity Management](#pim--privileged-identity-management)
- [Error Responses](#error-responses)

---

## Transport

### HTTP

| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/` | GET | Server info |
| `/health` | GET | Health check |
| `/mcp/tools` | GET | List all tools |
| `/mcp/chat` | POST | Natural language chat |
| `/mcp` | POST | MCP JSON-RPC protocol |

### stdio (JSON-RPC)

Methods: `initialize`, `tools/list`, `tools/call`, `prompts/list`, `prompts/get`, `ping`

Protocol version: `2024-11-05`

---

## Authentication

All tools are classified into PIM tiers:

| Tier | Level | Requirements |
|------|-------|-------------|
| 1 | None | No special auth |
| 2a | Read | Active PIM role |
| 2b | Write | PIM Contributor+ |

---

## Phase 1: Prepare

### `compliance_register_system`

Register a new information system for RMF tracking.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `name` | string | ✓ | System name (max 200) |
| `system_type` | string | ✓ | `MajorApplication`, `GeneralSupportSystem`, `Enclave`, `PlatformIt`, `CloudServiceOffering` |
| `mission_criticality` | string | ✓ | `MissionCritical`, `MissionEssential`, `MissionSupport` |
| `hosting_environment` | string | ✓ | `Commercial`, `Government`, `GovernmentAirGappedIl5`, `GovernmentAirGappedIl6` |
| `acronym` | string | | System acronym (max 50) |
| `description` | string | | System description (max 2000) |

**Response:** System ID, name, initial RMF phase (Prepare), creation timestamp.

---

### `compliance_list_systems`

List registered systems with pagination.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `page` | int | | Page number (default: 1) |
| `page_size` | int | | Items per page (default: 20) |
| `active_only` | bool | | Filter to active systems (default: true) |

---

### `compliance_get_system`

Retrieve full system details.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | ✓ | System GUID |

**Response:** System details, boundary resources, role assignments, categorization, baseline, and current RMF phase.

---

### `compliance_advance_rmf_step`

Advance (or regress) through RMF lifecycle phases.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | ✓ | System GUID |
| `target_step` | string | ✓ | Target phase: `Prepare`, `Categorize`, `Select`, `Implement`, `Assess`, `Authorize`, `Monitor` |
| `force` | bool | | Override gate failures (default: false) |

**Gate Conditions:** See [RMF Step Map](../architecture/rmf-step-map.md) for per-transition requirements.

---

### `compliance_define_boundary`

Add Azure resources to the authorization boundary.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | ✓ | System GUID |
| `resources` | array | ✓ | Array of `{resource_id, resource_type, resource_name}` |

---

### `compliance_exclude_from_boundary`

Exclude a resource from the boundary.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | ✓ | System GUID |
| `resource_id` | string | ✓ | Azure resource ID |
| `rationale` | string | ✓ | Exclusion justification |

---

### `compliance_assign_rmf_role`

Assign a personnel role.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | ✓ | System GUID |
| `role` | string | ✓ | `AuthorizingOfficial`, `Issm`, `Isso`, `Sca`, `SystemOwner` |
| `user_id` | string | ✓ | User principal name |
| `user_display_name` | string | ✓ | Display name |

---

### `compliance_list_rmf_roles`

List role assignments.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | ✓ | System GUID |

---

## Phase 2: Categorize

### `compliance_categorize_system`

Perform FIPS 199 security categorization.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | ✓ | System GUID |
| `information_types` | array | ✓ | Array of `{sp800_60_id, name, confidentiality_impact, integrity_impact, availability_impact}` |
| `justification` | string | | Categorization rationale |

**Computed:** High-water mark C/I/A, overall categorization, DoD IL, NIST baseline, FIPS 199 notation.

---

### `compliance_get_categorization`

Retrieve stored categorization.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | ✓ | System GUID |

---

### `compliance_suggest_info_types`

AI-suggested SP 800-60 information types.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | ✓ | System GUID |
| `description` | string | ✓ | System description for AI analysis |

---

## Phase 3: Select

### `compliance_select_baseline`

Select NIST 800-53 control baseline.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | ✓ | System GUID |
| `apply_overlay` | bool | | Apply CNSSI 1253 overlay (default: true) |

**Baselines:** Low (152), Moderate (329), High (400) controls.

---

### `compliance_tailor_baseline`

Add/remove controls from baseline.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | ✓ | System GUID |
| `tailoring_actions` | array | ✓ | Array of `{control_id, action: "Added"/"Removed", rationale}` |

---

### `compliance_set_inheritance`

Set inheritance type for controls.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | ✓ | System GUID |
| `inheritance_mappings` | array | ✓ | Array of `{control_id, inheritance_type, provider?, customer_responsibility?}` |

---

### `compliance_get_baseline`

Retrieve baseline details.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | ✓ | System GUID |
| `include_tailoring` | bool | | Include tailoring records |
| `include_inheritance` | bool | | Include inheritance records |

---

### `compliance_generate_crm`

Generate Customer Responsibility Matrix.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | ✓ | System GUID |

---

### `compliance_show_stig_mapping`

View STIG-to-NIST mappings.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | ✓ | System GUID |
| `control_ids` | string | | Comma-separated control IDs to filter |

---

## Phase 4: Implement

### `compliance_write_narrative`

Write or update implementation narrative.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | ✓ | System GUID |
| `control_id` | string | ✓ | NIST control ID |
| `narrative` | string | ✓ | Implementation narrative text |
| `status` | string | | `Implemented`, `PartiallyImplemented`, `Planned`, `NotApplicable` |

---

### `compliance_suggest_narrative`

AI-generated narrative draft.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | ✓ | System GUID |
| `control_id` | string | ✓ | NIST control ID |

---

### `compliance_batch_populate_narratives`

Auto-populate inherited control narratives.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | ✓ | System GUID |
| `inheritance_type` | string | | Filter: `Inherited`, `Shared` |

---

### `compliance_narrative_progress`

Track SSP completion.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | ✓ | System GUID |
| `family_filter` | string | | NIST family (e.g., "AC") |

---

### `compliance_generate_ssp`

Generate System Security Plan.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | ✓ | System GUID |
| `sections` | string | | Comma-separated sections to generate |
| `format` | string | | `markdown` (default), `pdf`, `docx` |

---

## Phase 5: Assess

### `compliance_assess_control`

Record per-control effectiveness determination.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `assessment_id` | string | ✓ | Assessment session ID |
| `control_id` | string | ✓ | NIST control ID |
| `determination` | string | ✓ | `Satisfied` or `OtherThanSatisfied` |
| `method` | string | ✓ | `Test`, `Interview`, `Examine` |
| `cat_severity` | string | | Required if OtherThanSatisfied: `CatI`, `CatII`, `CatIII` |
| `notes` | string | | Assessment notes |
| `evidence_ids` | string | | Comma-separated evidence IDs |

---

### `compliance_take_snapshot`

Create immutable assessment snapshot.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | ✓ | System GUID |
| `assessment_id` | string | ✓ | Assessment session ID |

**Response:** Snapshot ID, SHA-256 integrity hash, compliance score, immutable flag.

---

### `compliance_compare_snapshots`

Compare two snapshots.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `snapshot_id_a` | string | ✓ | First snapshot ID |
| `snapshot_id_b` | string | ✓ | Second snapshot ID |

---

### `compliance_verify_evidence`

Verify evidence integrity.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `evidence_id` | string | ✓ | Evidence ID |

---

### `compliance_check_evidence_completeness`

Check evidence coverage.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | ✓ | System GUID |
| `assessment_id` | string | ✓ | Assessment session ID |
| `family_filter` | string | | NIST family filter |

---

### `compliance_generate_sar`

Generate Security Assessment Report.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | ✓ | System GUID |
| `assessment_id` | string | ✓ | Assessment session ID |
| `format` | string | | `markdown`, `pdf`, `docx` |

---

## Phase 6: Authorize

### `compliance_issue_authorization`

Issue authorization decision. **AO-only.**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | ✓ | System GUID |
| `decision_type` | string | ✓ | `ATO`, `AtoWithConditions`, `IATT`, `DATO` |
| `expiration_date` | string | | ISO 8601 date (required except DATO) |
| `residual_risk_level` | string | ✓ | `Low`, `Medium`, `High`, `Critical` |
| `residual_risk_justification` | string | | Risk rationale |
| `terms_and_conditions` | string | | Conditions text (for ATOwC) |
| `risk_acceptances` | string | | JSON array of inline risk acceptances |

---

### `compliance_accept_risk`

Accept risk on specific finding. **AO-only.**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | ✓ | System GUID |
| `finding_id` | string | ✓ | Finding ID |
| `control_id` | string | ✓ | NIST control ID |
| `cat_severity` | string | ✓ | `CatI`, `CatII`, `CatIII` |
| `justification` | string | ✓ | Risk acceptance rationale |
| `compensating_control` | string | | Compensating control description |
| `expiration_date` | string | | ISO 8601 expiration date |

---

### `compliance_show_risk_register`

View risk register.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | ✓ | System GUID |
| `status_filter` | string | | `active`, `expired`, `revoked`, `all` |

---

### `compliance_create_poam`

Create POA&M item.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | ✓ | System GUID |
| `finding_id` | string | | Associated finding ID |
| `weakness` | string | ✓ | Weakness description |
| `control_id` | string | ✓ | NIST control ID |
| `cat_severity` | string | ✓ | `CatI`, `CatII`, `CatIII` |
| `poc` | string | ✓ | Point of contact |
| `scheduled_completion` | string | ✓ | ISO 8601 date |
| `resources_required` | string | | Resources needed |
| `milestones` | string | | JSON array of `{description, target_date}` |

---

### `compliance_list_poam`

List POA&M items.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | ✓ | System GUID |
| `status_filter` | string | | `Ongoing`, `Completed`, `Delayed`, `RiskAccepted` |
| `severity_filter` | string | | `CatI`, `CatII`, `CatIII` |
| `overdue_only` | bool | | Show only overdue items |

---

### `compliance_generate_rar`

Generate Risk Assessment Report.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | ✓ | System GUID |
| `assessment_id` | string | ✓ | Assessment session ID |

---

### `compliance_bundle_authorization_package`

Bundle complete authorization package.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | ✓ | System GUID |
| `include_evidence` | bool | | Include evidence artifacts |

---

## Phase 7: Monitor

### `compliance_create_conmon_plan`

Create or update continuous monitoring plan.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | ✓ | System GUID |
| `assessment_frequency` | string | ✓ | `Monthly`, `Quarterly`, `Annually` |
| `annual_review_date` | string | ✓ | ISO 8601 date |
| `report_distribution` | string | | Comma-separated recipients |
| `significant_change_triggers` | string | | JSON array of custom triggers |

---

### `compliance_generate_conmon_report`

Generate periodic compliance report.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | ✓ | System GUID |
| `report_type` | string | ✓ | `Monthly`, `Quarterly`, `Annual` |
| `period` | string | ✓ | Report period (e.g., "2026-02") |

---

### `compliance_report_significant_change`

Report a change event.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | ✓ | System GUID |
| `change_type` | string | ✓ | Change type (see RMF Process reference) |
| `description` | string | ✓ | Change description |

---

### `compliance_track_ato_expiration`

Check authorization expiration.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | ✓ | System GUID |

---

### `compliance_multi_system_dashboard`

Portfolio-wide compliance dashboard.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| *(none)* | | | Returns all active systems |

---

### `compliance_reauthorization_workflow`

Check triggers or initiate reauthorization.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | ✓ | System GUID |
| `initiate` | bool | | Initiate reauthorization (regresses to Assess) |

---

### `compliance_notification_delivery`

Send notification.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | ✓ | System GUID |
| `notification_type` | string | ✓ | `expiration`, `significant_change`, `conmon_report` |

---

## Interoperability

### `compliance_export_emass`

Export to eMASS Excel format.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | ✓ | System GUID |
| `export_type` | string | ✓ | `controls`, `poam`, `full` |

---

### `compliance_import_emass`

Import from eMASS Excel.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | ✓ | System GUID |
| `file_base64` | string | ✓ | Base64-encoded Excel file |
| `import_type` | string | ✓ | `controls` or `poam` |
| `conflict_strategy` | string | | `skip`, `overwrite`, `merge` |
| `dry_run` | bool | | Preview changes without applying |

---

### `compliance_export_oscal`

Export as OSCAL JSON.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | ✓ | System GUID |
| `model_type` | string | ✓ | `ssp`, `assessment_results`, `poam` |

---

## STIG & SCAP Import (Feature 017)

**Service**: `IScanImportService`

### `compliance_import_ckl`

Import DISA STIG Viewer CKL checklist file.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | ✓ | System GUID |
| `ckl_content` | string | ✓ | CKL XML file content |
| `conflict_resolution` | string | | `Skip` (default), `Overwrite` |

### `compliance_import_xccdf`

Import SCAP Compliance Checker XCCDF results.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | ✓ | System GUID |
| `xccdf_content` | string | ✓ | XCCDF XML file content |
| `conflict_resolution` | string | | `Skip` (default), `Overwrite` |

### `compliance_export_ckl`

Export CKL checklist for DISA STIG Viewer or eMASS.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | ✓ | System GUID |
| `benchmark_id` | string | ✓ | STIG benchmark identifier |

### `compliance_list_imports`

List import history for a system.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | ✓ | System GUID |
| `import_type` | string | | Filter by type: `CKL`, `XCCDF`, `PrismaCloudCsv`, `PrismaCloudApi` |

### `compliance_get_import_summary`

Get detailed per-finding import breakdown.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `import_id` | string | ✓ | Import record GUID |

---

## SAP Generation (Feature 018)

**Service**: `ISapService`

### `compliance_generate_sap`

Generate Security Assessment Plan from system metadata.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | ✓ | System GUID |

### `compliance_update_sap`

Update SAP scope, methodology, or schedule.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `sap_id` | string | ✓ | SAP GUID |
| `updates` | object | ✓ | Fields to update (methodology, scope, schedule) |

### `compliance_finalize_sap`

Lock SAP — generates SHA-256 hash, no further edits allowed.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `sap_id` | string | ✓ | SAP GUID |

### `compliance_get_sap`

Get SAP details by ID.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `sap_id` | string | ✓ | SAP GUID |

### `compliance_list_saps`

List all SAPs for a system.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | ✓ | System GUID |

---

## Prisma Cloud Import (Feature 019)

**Parsers**: `PrismaCsvParser`, `PrismaApiJsonParser`

### `compliance_import_prisma_csv`

Import Prisma Cloud CSV compliance export.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | ✓ | System GUID |
| `csv_content` | string | ✓ | CSV file content |
| `conflict_resolution` | string | | `Skip` (default), `Overwrite`, `Merge` |

### `compliance_import_prisma_api`

Import Prisma Cloud API JSON response.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | ✓ | System GUID |
| `api_json` | string | ✓ | Prisma RQL API JSON response |
| `conflict_resolution` | string | | `Skip` (default), `Overwrite`, `Merge` |

### `compliance_list_prisma_policies`

List Prisma policies with NIST control mappings.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | ✓ | System GUID |

### `compliance_prisma_trend`

Compare scan imports for remediation progress.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | ✓ | System GUID |
| `group_by` | string | | `nist_control`, `resource_type`, `severity` |

---

## Privacy & Interconnections (Feature 021)

**Services**: `IPrivacyService`, `IInterconnectionService`

### `compliance_create_pta`

Create Privacy Threshold Analysis.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | ✓ | System GUID |
| `collects_pii` | boolean | ✓ | Whether system collects PII |
| `pii_categories` | string | | Comma-separated PII categories |

### `compliance_generate_pia`

Generate Privacy Impact Assessment from PTA.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | ✓ | System GUID |

### `compliance_review_pia`

ISSM review of PIA — approve or reject.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `pia_id` | string | ✓ | PIA GUID |
| `decision` | string | ✓ | `Approved` or `NeedsRevision` |
| `notes` | string | | Review notes |

### `compliance_check_privacy_compliance`

Verify all privacy artifacts are complete.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | ✓ | System GUID |

### `compliance_add_interconnection`

Register a system-to-system interconnection.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | ✓ | System GUID |
| `remote_system_name` | string | ✓ | Connected system name |
| `direction` | string | ✓ | `Inbound`, `Outbound`, `Bidirectional` |
| `protocol` | string | | Network protocol |
| `data_types` | string | | Comma-separated data types |

### `compliance_list_interconnections`

List all interconnections for a system.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | ✓ | System GUID |

### `compliance_update_interconnection`

Update interconnection metadata.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `interconnection_id` | string | ✓ | Interconnection GUID |
| `updates` | object | ✓ | Fields to update |

### `compliance_generate_isa`

Generate ISA document from interconnection record.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `interconnection_id` | string | ✓ | Interconnection GUID |

### `compliance_register_agreement`

Register ISA/MOU agreement for an interconnection.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `interconnection_id` | string | ✓ | Interconnection GUID |
| `agreement_type` | string | ✓ | `ISA`, `MOU`, `MOA`, `SLA` |
| `effective_date` | string | | Agreement effective date |
| `expiration_date` | string | | Agreement expiration date |

### `compliance_update_agreement`

Update agreement details.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `agreement_id` | string | ✓ | Agreement GUID |
| `updates` | object | ✓ | Fields to update |

### `compliance_certify_no_interconnections`

Certify system has no external interconnections.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | ✓ | System GUID |
| `justification` | string | ✓ | Rationale for certification |

### `compliance_validate_agreements`

Validate all ISA/MOU agreements for a system.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | ✓ | System GUID |

---

## SSP Authoring & OSCAL (Feature 022)

**Services**: `ISspService`, `IOscalSspExportService`, `IOscalValidationService`

### `compliance_write_ssp_section`

Write or update an SSP section (NIST 800-18 structure).

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | ✓ | System GUID |
| `section_number` | integer | ✓ | Section 1–13 |
| `content` | string | ✓ | Section narrative |

### `compliance_review_ssp_section`

ISSM review of SSP section — approve or request revision.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `section_id` | string | ✓ | SSP section GUID |
| `decision` | string | ✓ | `Approved` or `NeedsRevision` |
| `notes` | string | | Review notes |

### `compliance_ssp_completeness`

Check SSP completeness percentage by section.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | ✓ | System GUID |

### `compliance_export_oscal_ssp`

Export OSCAL SSP document for authorization package.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | ✓ | System GUID |

### `compliance_validate_oscal_ssp`

Validate OSCAL SSP against NIST schema.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | ✓ | System GUID |

---

## HW/SW Inventory (Feature 025)

### `inventory_add_item`

Register a hardware or software inventory item.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | ✓ | System GUID, name, or acronym |
| `item_name` | string | ✓ | Display name of the item |
| `type` | string | ✓ | `hardware` or `software` |
| `function` | string | ✓ | HW: `Server`, `Workstation`, `NetworkDevice`, `Storage`, `Other`; SW: `OperatingSystem`, `Database`, `Middleware`, `Application`, `SecurityTool`, `Other` |
| `manufacturer` | string | | Manufacturer (required for hardware) |
| `model` | string | | Hardware model |
| `serial_number` | string | | Hardware serial number |
| `ip_address` | string | | IP address (required for Server/NetworkDevice) |
| `mac_address` | string | | MAC address |
| `location` | string | | Physical location |
| `vendor` | string | | Software vendor (required for software) |
| `version` | string | | Software version (required for software) |
| `patch_level` | string | | Current patch level |
| `license_type` | string | | License type |
| `parent_hardware_id` | string | | Parent hardware GUID (for software items) |

**Response:** Created inventory item with generated GUID.

### `inventory_update_item`

Update fields on an existing inventory item. Only provided fields are changed.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `item_id` | string | ✓ | Inventory item GUID |
| `item_name` | string | | Updated display name |
| `manufacturer` | string | | Updated manufacturer |
| `model` | string | | Updated model |
| `serial_number` | string | | Updated serial number |
| `ip_address` | string | | Updated IP address |
| `mac_address` | string | | Updated MAC address |
| `location` | string | | Updated location |
| `vendor` | string | | Updated vendor |
| `version` | string | | Updated version |
| `patch_level` | string | | Updated patch level |
| `license_type` | string | | Updated license type |

**Response:** Updated inventory item.

### `inventory_decommission_item`

Soft-delete an inventory item with a decommission rationale. Cascades to child software.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `item_id` | string | ✓ | Inventory item GUID |
| `rationale` | string | ✓ | Reason for decommissioning |

**Response:** Decommissioned item with cascade count in metadata.

### `inventory_list`

List and filter inventory items with pagination.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | ✓ | System GUID, name, or acronym |
| `type` | string | | `hardware` or `software` |
| `function` | string | | Function filter |
| `vendor` | string | | Vendor/manufacturer filter |
| `status` | string | | `active` (default) or `decommissioned` |
| `search` | string | | Free-text search on item name |
| `page_size` | integer | | Results per page (default 50) |
| `page` | integer | | Page number (default 1) |

**Response:** Paginated list of inventory items with count.

### `inventory_get`

Retrieve a single inventory item with installed software children.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `item_id` | string | ✓ | Inventory item GUID |

**Response:** Inventory item with installed software array for hardware items.

### `inventory_export`

Export inventory to an eMASS-compatible Excel workbook.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | ✓ | System GUID, name, or acronym |
| `export_type` | string | | `all` (default), `hardware`, or `software` |
| `include_decommissioned` | boolean | | Include decommissioned items (default false) |

**Response:** Base64-encoded Excel workbook with Hardware and Software worksheets.

### `inventory_import`

Import inventory from an eMASS-format Excel workbook.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | ✓ | System GUID, name, or acronym |
| `file_base64` | string | ✓ | Base64-encoded Excel file |
| `dry_run` | boolean | | Preview import without persisting (default false) |

**Response:** Import result with `hardware_created`, `software_created`, `rows_skipped`, and row-level errors.

### `inventory_completeness`

Check inventory completeness against boundary and field requirements.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | ✓ | System GUID, name, or acronym |

**Response:** Completeness report with `completeness_score`, `is_complete`, items with missing fields, unmatched boundary resources, and hardware without software.

### `inventory_auto_seed`

Auto-create hardware inventory items from authorization boundary resources.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | ✓ | System GUID, name, or acronym |

**Response:** List of created items with `created_count`. Idempotent — re-running skips existing items.

---

## Narrative Governance (Feature 024)

**Service**: `INarrativeGovernanceService`

### `compliance_narrative_history`

Retrieve paginated version history for a control narrative (newest first).

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | ✓ | System GUID, name, or acronym |
| `control_id` | string | ✓ | NIST 800-53 control ID (e.g., "AC-1") |
| `page` | integer | | Page number (default: 1) |
| `page_size` | integer | | Items per page (default: 50) |

**Response:** Array of `versions` with `version_number`, `content`, `status`, `authored_by`, `authored_at`, `change_reason`. Includes `total_versions` count. Error codes: `SYSTEM_NOT_FOUND`, `CONTROL_NOT_FOUND`.

### `compliance_narrative_diff`

Compare two versions of a control narrative with line-level unified diff.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | ✓ | System GUID, name, or acronym |
| `control_id` | string | ✓ | NIST 800-53 control ID |
| `from_version` | integer | ✓ | Base version number |
| `to_version` | integer | ✓ | Target version number |

**Response:** Unified diff text with `lines_added` and `lines_removed` counts. Error codes: `SYSTEM_NOT_FOUND`, `CONTROL_NOT_FOUND`, `VERSION_NOT_FOUND`.

### `compliance_rollback_narrative`

Roll back to a prior narrative version (copy-forward — creates new version with old content, resets to Draft).

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | ✓ | System GUID, name, or acronym |
| `control_id` | string | ✓ | NIST 800-53 control ID |
| `target_version` | integer | ✓ | Version number to roll back to |
| `change_reason` | string | | Reason for rollback |

**Response:** New version details with `new_version_number`, `rolled_back_to`, `status` (Draft). Error codes: `SYSTEM_NOT_FOUND`, `CONTROL_NOT_FOUND`, `VERSION_NOT_FOUND`, `UNDER_REVIEW`.

### `compliance_submit_narrative`

Submit a Draft narrative for ISSM review (transitions status to InReview).

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | ✓ | System GUID, name, or acronym |
| `control_id` | string | ✓ | NIST 800-53 control ID |

**Response:** Updated version with `previous_status`, `new_status` (InReview), `submitted_by`, `submitted_at`. Error codes: `SYSTEM_NOT_FOUND`, `CONTROL_NOT_FOUND`, `INVALID_STATUS`.

### `compliance_review_narrative`

Approve or request revision of a narrative under review. ISSM only.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | ✓ | System GUID, name, or acronym |
| `control_id` | string | ✓ | NIST 800-53 control ID |
| `decision` | string | ✓ | `approve` or `request_revision` |
| `comments` | string | | Reviewer comments (required for `request_revision`) |

**Response:** Review result with `decision`, `new_status` (Approved or NeedsRevision), `reviewed_by`, `reviewed_at`. Error codes: `SYSTEM_NOT_FOUND`, `CONTROL_NOT_FOUND`, `INVALID_STATUS`, `COMMENTS_REQUIRED`.

### `compliance_batch_review_narratives`

Batch approve or request revision of narratives by family or control IDs. ISSM only.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | ✓ | System GUID, name, or acronym |
| `decision` | string | ✓ | `approve` or `request_revision` |
| `comments` | string | | Reviewer comments (required for `request_revision`) |
| `family_filter` | string | | Control family prefix (e.g., "AC"). Mutually exclusive with `control_ids` |
| `control_ids` | string | | Comma-separated control IDs. Mutually exclusive with `family_filter` |

**Response:** `reviewed_count`, `skipped_count`, `reviewed_controls`, `skipped_controls`. Error codes: `SYSTEM_NOT_FOUND`, `NO_REVIEWABLE_NARRATIVES`, `COMMENTS_REQUIRED`, `MUTUALLY_EXCLUSIVE_FILTERS`.

### `compliance_narrative_approval_progress`

Return aggregate approval status, per-family breakdown, review queue, and staleness warnings.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | ✓ | System GUID, name, or acronym |
| `family_filter` | string | | Control family prefix to filter results |

**Response:** Overall counts (`total_controls`, `approved`, `draft`, `in_review`, `needs_revision`, `missing`, `approval_percentage`), `families` breakdown, `review_queue` (InReview control IDs), `staleness_warnings`. Error codes: `SYSTEM_NOT_FOUND`.

### `compliance_batch_submit_narratives`

Submit all Draft narratives for a control family (or all families) for ISSM review.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | ✓ | System GUID, name, or acronym |
| `family_filter` | string | | Control family prefix. If omitted, submits all Draft narratives |

**Response:** `submitted_count`, `skipped_count`, `submitted_controls`, `skipped_controls`, `skipped_reason`. Error codes: `SYSTEM_NOT_FOUND`, `NO_DRAFT_NARRATIVES`.

---

## Document Templates

### `compliance_upload_template`

Upload custom DOCX template.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `name` | string | ✓ | Template display name |
| `document_type` | string | ✓ | `ssp`, `sar`, `poam`, `rar` |
| `file_base64` | string | ✓ | Base64-encoded DOCX file |

---

### `compliance_list_templates`

List uploaded templates.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `document_type` | string | | Filter by document type |

---

### `compliance_update_template`

Update template name or content.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `template_id` | string | ✓ | Template GUID |
| `name` | string | | New name |
| `file_base64` | string | | New DOCX content |

---

### `compliance_delete_template`

Delete template.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `template_id` | string | ✓ | Template GUID |

---

## CAC Authentication

| Tool | Parameters | Description |
|------|-----------|-------------|
| `cac_status` | *(none)* | Check session status |
| `cac_sign_out` | *(none)* | End session |
| `cac_set_timeout` | `timeout_minutes` | Set timeout duration |
| `cac_map_certificate` | `certificate_thumbprint`, `role` | Map cert to role |

---

## PIM — Privileged Identity Management

| Tool | Key Parameters | Description |
|------|---------------|-------------|
| `pim_list_eligible` | *(none)* | List eligible roles |
| `pim_activate_role` | `role_name`, `justification`, `duration_hours?`, `ticket_number?` | Activate role |
| `pim_deactivate_role` | `role_name` | Deactivate role |
| `pim_list_active` | *(none)* | List active roles |
| `pim_extend_role` | `role_name`, `additional_hours`, `justification` | Extend session |
| `pim_approve_request` | `request_id`, `justification` | Approve request |
| `pim_deny_request` | `request_id`, `reason` | Deny request |
| `pim_history` | `filter_user_id?`, `days?` | View history |
| `jit_request_access` | `vm_name`, `resource_group`, `ports`, `duration_hours?`, `justification` | Request JIT |
| `jit_list_sessions` | *(none)* | List JIT sessions |
| `jit_revoke_access` | `session_id` | Revoke JIT |

---

## Error Responses

All tools return structured error responses via `ToolResponse<T>`:

| Error Code | Description |
|-----------|-------------|
| `NOT_FOUND` | Entity not found |
| `VALIDATION_ERROR` | Invalid parameters |
| `AUTH_REQUIRED` | Authentication required |
| `PIM_ELEVATION_REQUIRED` | PIM role activation needed |
| `FORBIDDEN` | Insufficient permissions |
| `CONCURRENCY_CONFLICT` | Optimistic concurrency violation |
| `GATE_FAILED` | RMF gate conditions not met |
| `INTERNAL_ERROR` | Unexpected server error |
