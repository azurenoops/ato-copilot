# Contracts: 015 — MCP Tool Contracts

**Date**: 2026-02-27 | **Plan**: [plan.md](plan.md) | **Data Model**: [data-model.md](data-model.md)

All tools follow the existing MCP JSON-RPC 2.0 protocol (`McpRequest`/`McpResponse`) and the standard tool envelope:

```json
{
  "content": [{ "type": "text", "text": "<response>" }],
  "isError": false
}
```

Errors use `McpToolResult.Error(message)`. Rich UI uses the existing `EnrichedToolResult` with `uiHint`, `metadata`, and adaptive card payloads (per Feature 014).

---

## Phase 1 — RMF Foundation Tools

### System Registration

#### `compliance_register_system`

Register a new information system for RMF processing.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `name` | string | yes | System name |
| `acronym` | string | no | System acronym |
| `system_type` | string | yes | "MajorApplication" \| "Enclave" \| "PlatformIt" |
| `mission_criticality` | string | yes | "MissionCritical" \| "MissionEssential" \| "MissionSupport" |
| `hosting_environment` | string | yes | "AzureGovernment" \| "AzureCommercial" \| "OnPremises" \| "Hybrid" |
| `description` | string | no | System description |
| `cloud_environment` | string | no | Azure cloud type for environment profile |
| `subscription_ids` | string[] | no | Azure subscription IDs in boundary |

**Returns**: RegisteredSystem JSON with generated ID, `currentRmfStep: "Prepare"`.

**RBAC**: `Compliance.Administrator` or `Compliance.PlatformEngineer`

---

#### `compliance_list_systems`

List all registered systems visible to the current user.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `active_only` | bool | no | Default: true |
| `page` | int | no | 1-based page number |
| `page_size` | int | no | Default: 20, max 100 |

**Returns**: Paginated list of `RegisteredSystem` summaries.

---

#### `compliance_get_system`

Get full details of a registered system including categorization, baseline, current RMF step.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | yes | RegisteredSystem ID |

**Returns**: Full `RegisteredSystem` with nested `SecurityCategorization`, `ControlBaseline`, `AuthorizationDecision` (if active), role assignments.

---

#### `compliance_advance_rmf_step`

Advance a system to the next RMF step (with gate checks).

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | yes | RegisteredSystem ID |
| `target_step` | string | yes | Target RMF step |
| `force` | bool | no | Override gate failures (logged) |

**Returns**: Updated step, list of gate check results (pass/fail/warning).

**Gates (forward)**:
- Prepare → Categorize: RegisteredSystem must have ≥1 RmfRoleAssignment and ≥1 AuthorizationBoundary resource
- Categorize → Select: SecurityCategorization must exist with ≥1 InformationType
- Select → Implement: ControlBaseline must exist
- Implement → Assess: ≥80% controls have narratives
- Assess → Authorize: Assessment complete, SAR generated
- Authorize → Monitor: AuthorizationDecision exists and IsActive

**Step Regression Rules**:
- Backward movement is allowed with `force: true` (audit-logged as "RMF step regression")
- Regressing from Assess/Authorize/Monitor to Select invalidates the active ControlBaseline (requires re-selection)
- Regressing from Authorize/Monitor to Assess invalidates the active AuthorizationDecision (system marked "reauthorization required")
- Regressing from Monitor to any earlier step sets `AuthorizationDecision.IsActive = false`
- Without `force: true`, backward movement returns an error with a list of downstream entities that will be invalidated

**RBAC**: `Compliance.Administrator` or `Compliance.SecurityLead`

---

### Security Categorization (FIPS 199 / SP 800-60)

#### `compliance_categorize_system`

Perform or update FIPS 199 / SP 800-60 security categorization.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | yes | RegisteredSystem ID |
| `information_types` | InformationTypeInput[] | yes | Array of info types with impacts |
| `justification` | string | no | Overall rationale |

`InformationTypeInput`:
```json
{
  "sp800_60_id": "D.1.1",
  "name": "Strategic Planning",
  "confidentiality_impact": "Moderate",
  "integrity_impact": "Moderate",
  "availability_impact": "Low",
  "uses_provisional": true,
  "adjustment_justification": null
}
```

**Returns**: `SecurityCategorization` with computed high-water mark, FIPS 199 notation, recommended NIST baseline, derived DoD IL.

---

#### `compliance_get_categorization`

Retrieve the security categorization for a system.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | yes | RegisteredSystem ID |

**Returns**: `SecurityCategorization` with InformationTypes, computed fields, formal notation.

---

#### `compliance_suggest_info_types`

AI-assisted suggestion of SP 800-60 information types based on system description.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | yes | RegisteredSystem ID |
| `description` | string | no | Additional context for AI suggestion |

**Returns**: Ranked list of suggested InformationTypes with confidence scores.

---

### Control Selection & Baseline

#### `compliance_select_baseline`

Generate the control baseline for a system based on categorization.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | yes | RegisteredSystem ID |
| `apply_overlay` | bool | no | Default: true — apply CNSSI 1253 overlay based on DoD IL |
| `overlay_name` | string | no | Override overlay (e.g., "CNSSI 1253 IL5") |

**Returns**: `ControlBaseline` with counts (total, customer, inherited, shared), overlay information, full control ID list.

**Prerequisites**: SecurityCategorization must exist.

---

#### `compliance_tailor_baseline`

Add or remove controls from the baseline with documented rationale.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | yes | RegisteredSystem ID |
| `tailoring_actions` | TailoringInput[] | yes | Array of add/remove actions |

`TailoringInput`:
```json
{ "control_id": "AC-2(12)", "action": "Added", "rationale": "Required by FedRAMP High" }
```

**Returns**: Updated baseline counts, list of accepted/rejected actions.

---

#### `compliance_set_inheritance`

Set inheritance type for controls (Inherited / Shared / Customer).

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | yes | RegisteredSystem ID |
| `inheritance_mappings` | InheritanceInput[] | yes | Array of control inheritance settings |

`InheritanceInput`:
```json
{
  "control_id": "PE-1",
  "inheritance_type": "Inherited",
  "provider": "Azure Government (FedRAMP High)",
  "customer_responsibility": null
}
```

**Returns**: Updated inheritance counts, auto-populated narratives for fully inherited controls.

---

#### `compliance_get_baseline`

Get the full control baseline for a system.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | yes | RegisteredSystem ID |
| `include_details` | bool | no | Include tailoring and inheritance details |
| `family_filter` | string | no | Filter by family prefix (e.g., "AC") |

**Returns**: ControlBaseline with optional nested tailoring and inheritance records.

---

### Authorization Boundary

#### `compliance_define_boundary`

Define or update the authorization boundary from Azure resource inventory.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | yes | RegisteredSystem ID |
| `auto_discover` | bool | no | Auto-discover from Azure subscriptions |
| `resource_ids` | string[] | no | Explicit resource IDs to add |

**Returns**: List of boundary resources, resource types, in/out-of-scope status.

---

#### `compliance_exclude_from_boundary`

Remove a resource from the authorization boundary.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | yes | RegisteredSystem ID |
| `resource_id` | string | yes | Azure resource ID |
| `rationale` | string | yes | Exclusion justification |

**Returns**: Updated boundary summary.

---

### Role Assignment

#### `compliance_assign_rmf_role`

Assign an RMF role to a user for a specific system.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | yes | RegisteredSystem ID |
| `role` | string | yes | "AO" \| "ISSM" \| "ISSO" \| "SCA" \| "SystemOwner" |
| `user_id` | string | yes | User identity |
| `user_display_name` | string | no | Display name |

**Returns**: RmfRoleAssignment.

**RBAC**: `Compliance.Administrator` or `Compliance.SecurityLead`

---

#### `compliance_list_rmf_roles`

List all RMF role assignments for a system.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | yes | RegisteredSystem ID |
| `active_only` | bool | no | Default: true |

**Returns**: List of RmfRoleAssignment records.

---

## Phase 2 — SSP Authoring Tools

#### `compliance_write_narrative`

Write or update the implementation narrative for a control.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | yes | RegisteredSystem ID |
| `control_id` | string | yes | NIST control ID |
| `narrative` | string | yes | Implementation description |
| `status` | string | no | "Implemented" \| "PartiallyImplemented" \| "Planned" \| "NotApplicable" |

**Returns**: ControlImplementation record.

**RBAC**: `Compliance.PlatformEngineer` or `Compliance.SecurityLead`

---

#### `compliance_suggest_narrative`

AI-generated draft narrative based on system context, control requirements, and Azure configuration.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | yes | RegisteredSystem ID |
| `control_id` | string | yes | NIST control ID |

**Returns**: Suggested narrative text, referenced evidence, confidence score.

---

#### `compliance_batch_populate_narratives`

Auto-populate narratives for inherited controls using provider templates.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | yes | RegisteredSystem ID |
| `inheritance_type` | string | no | "Inherited" \| "Shared" (default: both) |

**Returns**: Count of populated narratives, list of control IDs.

---

#### `compliance_narrative_progress`

Get SSP narrative completion status.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | yes | RegisteredSystem ID |
| `family_filter` | string | no | Filter by family prefix |

**Returns**: Per-family progress (total, completed, draft, missing), overall percentage.

---

#### `compliance_generate_ssp`

Generate the System Security Plan document.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | yes | RegisteredSystem ID |
| `format` | string | no | "markdown" (default) \| "docx" |
| `template` | string | no | Custom DOCX template name |
| `sections` | string[] | no | Specific sections to include (default: all) |

**Returns**: Generated document content or download path.

---

## Phase 3 — Assessment & Authorization Tools

#### `compliance_assess_control`

Record assessment determination for a control.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `assessment_id` | string | yes | ComplianceAssessment ID |
| `control_id` | string | yes | NIST control ID |
| `determination` | string | yes | "Satisfied" \| "OtherThanSatisfied" |
| `method` | string | no | "Test" \| "Interview" \| "Examine" |
| `evidence_ids` | string[] | no | Linked evidence records |
| `notes` | string | no | Assessor notes |
| `cat_severity` | string | no | "CatI" \| "CatII" \| "CatIII" (required if OtherThanSatisfied) |

**Returns**: ControlEffectiveness record.

**RBAC**: `Compliance.Auditor` (SCA)

---

#### `compliance_generate_sar`

Generate Security Assessment Report.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | yes | RegisteredSystem ID |
| `assessment_id` | string | yes | ComplianceAssessment ID |
| `format` | string | no | "markdown" \| "docx" |

**Returns**: SAR document with executive summary, control-by-control results, risk summary, CAT breakdown.

---

#### `compliance_issue_authorization`

Issue an authorization decision (AO only).

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | yes | RegisteredSystem ID |
| `decision_type` | string | yes | "ATO" \| "ATOwC" \| "IATT" \| "DATO" |
| `expiration_date` | string | no | ISO 8601 date (required for ATO/ATOwC/IATT) |
| `terms_and_conditions` | string | no | Conditions text |
| `risk_acceptances` | RiskAcceptanceInput[] | no | Findings to accept risk on |
| `residual_risk_level` | string | yes | "Low" \| "Medium" \| "High" \| "Critical" |
| `residual_risk_justification` | string | no | |

**Returns**: AuthorizationDecision record, system RMF step advances to Monitor.

**RBAC**: `Compliance.AuthorizingOfficial` **only**

---

#### `compliance_create_poam`

Create a POA&M item linked to a finding.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | yes | RegisteredSystem ID |
| `finding_id` | string | no | Link to ComplianceFinding |
| `weakness` | string | yes | Weakness description |
| `control_id` | string | yes | NIST control ID |
| `cat_severity` | string | yes | "CatI" \| "CatII" \| "CatIII" |
| `poc` | string | yes | Point of contact |
| `scheduled_completion` | string | yes | ISO 8601 date |
| `resources_required` | string | no | Needed resources |
| `milestones` | MilestoneInput[] | no | Planned milestones |

**Returns**: PoamItem with any linked RemediationTask.

---

#### `compliance_list_poam`

List POA&M items for a system.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | yes | RegisteredSystem ID |
| `status_filter` | string | no | "Ongoing" \| "Completed" \| "Delayed" \| "RiskAccepted" |
| `severity_filter` | string | no | "CatI" \| "CatII" \| "CatIII" |
| `overdue_only` | bool | no | Only show overdue items |

**Returns**: List of PoamItem with milestones and status.

---

## Phase 4 — Continuous Monitoring Tools

#### `compliance_create_conmon_plan`

Create or update the continuous monitoring plan.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | yes | RegisteredSystem ID |
| `assessment_frequency` | string | yes | "Monthly" \| "Quarterly" \| "Annually" |
| `annual_review_date` | string | yes | ISO 8601 date |
| `report_distribution` | string[] | no | User IDs or role names |
| `significant_change_triggers` | string[] | no | Trigger descriptions |

**Returns**: ConMonPlan record.

---

#### `compliance_generate_conmon_report`

Generate a continuous monitoring report.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | yes | RegisteredSystem ID |
| `report_type` | string | yes | "Monthly" \| "Quarterly" \| "Annual" |
| `period` | string | yes | Report period (e.g., "2026-02", "2026-Q1") |

**Returns**: ConMonReport with compliance score, delta from authorization, findings opened/closed, POA&M status, risk trending.

---

#### `compliance_report_significant_change`

Report or detect a significant change requiring review.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | yes | RegisteredSystem ID |
| `change_type` | string | yes | Change category |
| `description` | string | yes | Change description |

**Returns**: SignificantChange record with reauthorization recommendation.

---

## Phase 5 — Interoperability Tools

#### `compliance_export_emass`

Export system data in eMASS-compatible Excel format.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | yes | RegisteredSystem ID |
| `export_type` | string | yes | "controls" \| "poam" \| "full" |
| `format` | string | no | "xlsx" (default) |

**Returns**: File path to generated Excel workbook.

---

#### `compliance_import_emass`

Import system data from eMASS Excel export.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | yes | RegisteredSystem ID |
| `file_path` | string | yes | Path to Excel file |
| `conflict_strategy` | string | no | "skip" \| "overwrite" \| "merge" (default: "skip") |
| `dry_run` | bool | no | Preview changes without applying |

**Conflict Resolution Strategies**:
- **skip**: If a matching record exists (by control ID or POA&M weakness), skip the imported row
- **overwrite**: Replace the existing record entirely with the imported data
- **merge**: Per-field merge — narrative/text fields: append imported text after a separator (`\n---\nImported from eMASS:\n`); enum/status fields: prefer imported value; date fields: prefer imported value if more recent; computed fields (scores, counts): recalculate after merge

**Returns**: Import summary (imported, skipped, conflicts, errors).

---

#### `compliance_export_oscal`

Export system data in OSCAL JSON format.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | yes | RegisteredSystem ID |
| `model` | string | yes | "ssp" \| "assessment-results" \| "poam" |

**OSCAL Version**: Targets OSCAL 1.0.6 (latest stable). Output validated against the corresponding OSCAL JSON schema.

**Returns**: OSCAL JSON document.

---

## Phase 3 — Assessment Artifact Tools (Additional Contracts)

These tools implement spec capabilities §3.3–3.7, §3.12–3.13 and are defined in `AssessmentArtifactTools.cs` and `AuthorizationTools.cs`.

#### `compliance_take_snapshot`

Create an immutable, integrity-hashed snapshot of assessment state.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | yes | RegisteredSystem ID |
| `assessment_id` | string | yes | ComplianceAssessment ID |

**Returns**: Snapshot ID, timestamp, control count, evidence count, compliance score, SHA-256 integrity hash. Snapshot is immutable — no UPDATE/DELETE allowed.

**Hash Scope**: The SHA-256 hash is computed over a canonical JSON document containing: all `ControlEffectiveness` determinations, `ComplianceFinding` summaries, `ComplianceEvidence` hashes, and the computed compliance score. The canonical form is sorted by control ID to ensure deterministic hashing.

**RBAC**: `Compliance.Auditor` (SCA)

---

#### `compliance_compare_snapshots`

Compare two assessment snapshots side-by-side.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `snapshot_id_a` | string | yes | First snapshot ID |
| `snapshot_id_b` | string | yes | Second snapshot ID |

**Returns**: Controls changed (newly Satisfied, newly OtherThanSatisfied, unchanged), score delta, new findings, resolved findings, evidence changes.

---

#### `compliance_verify_evidence`

Recompute evidence hash and verify integrity.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `evidence_id` | string | yes | ComplianceEvidence ID |

**Returns**: Original hash, recomputed hash, match status (verified/tampered), collector identity, collection method, last verified timestamp.

**RBAC**: `Compliance.Auditor` (SCA)

---

#### `compliance_check_evidence_completeness`

Report which controls have verified evidence vs. missing evidence.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | yes | RegisteredSystem ID |
| `assessment_id` | string | no | Filter to specific assessment |
| `family_filter` | string | no | Filter by family prefix (e.g., "AC") |

**Returns**: Per-control evidence status (verified, unverified, missing), overall completeness percentage, list of controls lacking evidence.

---

#### `compliance_generate_rar`

Generate Risk Assessment Report.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | yes | RegisteredSystem ID |
| `assessment_id` | string | yes | ComplianceAssessment ID |
| `format` | string | no | "markdown" \| "docx" |

**Returns**: RAR document with residual risk by control family, aggregate risk level, threat/vulnerability analysis, CAT breakdown, risk trending.

**RBAC**: `Compliance.Auditor` or `Compliance.SecurityLead`

---

#### `compliance_accept_risk`

Accept risk for a specific finding with justification and expiration.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | yes | RegisteredSystem ID |
| `finding_id` | string | yes | ComplianceFinding ID |
| `control_id` | string | yes | NIST control ID |
| `cat_severity` | string | yes | "CatI" \| "CatII" \| "CatIII" |
| `justification` | string | yes | Risk acceptance rationale |
| `compensating_control` | string | no | Compensating measure description |
| `expiration_date` | string | yes | ISO 8601 date for auto-expire |

**Returns**: RiskAcceptance record with acceptance ID, expiration, status.

**RBAC**: `Compliance.AuthorizingOfficial` **only**

---

#### `compliance_show_risk_register`

View all risk acceptances (active, expired, revoked).

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | yes | RegisteredSystem ID |
| `status_filter` | string | no | "active" \| "expired" \| "revoked" \| "all" (default: "active") |

**Returns**: List of RiskAcceptance records with finding details, CAT severity, expiration status, compensating controls.

---

#### `compliance_bundle_authorization_package`

Generate complete authorization package as ZIP archive.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | yes | RegisteredSystem ID |
| `format` | string | no | "markdown" \| "docx" \| "pdf" (default: "markdown") |
| `template` | string | no | Custom DOCX template name |
| `include_evidence` | bool | no | Include evidence attachments (default: false) |

**Returns**: ZIP file path containing SSP + SAR + RAR + POA&M + CRM + ATO Letter. Each document rendered in the specified format.

**RBAC**: `Compliance.SecurityLead` or `Compliance.Administrator`

---

## RBAC Summary

| Role (constant in code) | Allowed Tools |
|------|---------------|
| `Compliance.Viewer` | All `get_*`, `list_*`, `narrative_progress` |
| `Compliance.PlatformEngineer` | Above + `write_narrative`, `register_system`, `define_boundary`, `categorize_system` |
| `Compliance.SecurityLead` (ISSM) | Above + `select_baseline`, `tailor_baseline`, `advance_rmf_step`, `assign_rmf_role`, `create_conmon_plan`, `report_significant_change`, `create_poam`, `generate_sar`, `bundle_authorization_package` |
| `Compliance.Auditor` (SCA) | Viewer + `assess_control`, `generate_sar`, `take_snapshot`, `compare_snapshots`, `verify_evidence`, `check_evidence_completeness` |
| `Compliance.AuthorizingOfficial` (AO) | Viewer + `issue_authorization` (exclusive), `accept_risk` (exclusive), `show_risk_register` (write) |
| `Compliance.Administrator` | All tools |
