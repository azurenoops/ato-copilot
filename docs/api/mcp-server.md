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
