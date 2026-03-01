# Agent Tool Catalog — RMF Compliance Tools

> Feature 015: Persona-Driven RMF Workflows

This catalog documents the MCP tools introduced for RMF system registration, authorization boundary management, role assignment, RMF lifecycle step advancement, FIPS 199 security categorization, NIST 800-53 control baseline selection, SSP authoring & narrative management, assessment artifacts with CAT severity mapping, and authorization decisions with risk acceptance and POA&M management.

---

## Tools Overview

| Tool Name | MCP Method | Description |
|-----------|-----------|-------------|
| `compliance_register_system` | `RegisterSystemAsync` | Register a new system for RMF compliance tracking |
| `compliance_list_systems` | `ListSystemsAsync` | List registered systems with pagination and filtering |
| `compliance_get_system` | `GetSystemAsync` | Retrieve full system details including boundary, roles, categorization |
| `compliance_advance_rmf_step` | `AdvanceRmfStepAsync` | Advance (or regress) system through RMF lifecycle phases |
| `compliance_define_boundary` | `DefineBoundaryAsync` | Define or update authorization boundary with Azure resources |
| `compliance_exclude_from_boundary` | `ExcludeFromBoundaryAsync` | Exclude a resource from the authorization boundary |
| `compliance_assign_rmf_role` | `AssignRmfRoleAsync` | Assign an RMF role to a user for a system |
| `compliance_list_rmf_roles` | `ListRmfRolesAsync` | List all active RMF role assignments for a system |
| `compliance_categorize_system` | `CategorizeSystemAsync` | Perform FIPS 199 / SP 800-60 security categorization |
| `compliance_get_categorization` | `GetCategorizationAsync` | Retrieve security categorization for a system |
| `compliance_suggest_info_types` | `SuggestInfoTypesAsync` | Suggest SP 800-60 information types for a system |
| `compliance_select_baseline` | `SelectBaselineAsync` | Select NIST 800-53 control baseline with optional CNSSI 1253 overlay |
| `compliance_tailor_baseline` | `TailorBaselineAsync` | Add or remove controls from the selected baseline |
| `compliance_set_inheritance` | `SetInheritanceAsync` | Set inheritance type (inherited/shared/customer) for controls |
| `compliance_get_baseline` | `GetBaselineAsync` | Retrieve baseline details with optional tailoring/inheritance |
| `compliance_generate_crm` | `GenerateCrmAsync` | Generate Customer Responsibility Matrix grouped by family |
| `compliance_write_narrative` | `WriteNarrativeAsync` | Write or update a control implementation narrative |
| `compliance_suggest_narrative` | `SuggestNarrativeAsync` | Generate AI-suggested draft narrative with confidence score |
| `compliance_batch_populate_narratives` | `BatchPopulateNarrativesAsync` | Auto-populate inherited/shared control narratives |
| `compliance_narrative_progress` | `NarrativeProgressAsync` | Track SSP narrative completion status per-family |
| `compliance_generate_ssp` | `GenerateSspAsync` | Generate the System Security Plan document |
| `compliance_assess_control` | `AssessControlAsync` | Record per-control SCA effectiveness determination with CAT severity |
| `compliance_take_snapshot` | `TakeSnapshotAsync` | Create immutable SHA-256-hashed assessment snapshot |
| `compliance_compare_snapshots` | `CompareSnapshotsAsync` | Compare two snapshots side-by-side with score delta |
| `compliance_verify_evidence` | `VerifyEvidenceAsync` | Recompute hash and verify evidence integrity |
| `compliance_check_evidence_completeness` | `CheckEvidenceCompletenessAsync` | Report controls with/without verified evidence |
| `compliance_generate_sar` | `GenerateSarAsync` | Generate Security Assessment Report with CAT breakdown |
| `compliance_issue_authorization` | `IssueAuthorizationAsync` | Issue ATO/ATOwC/IATT/DATO authorization decision |
| `compliance_accept_risk` | `AcceptRiskAsync` | Accept risk on a specific finding and control |
| `compliance_show_risk_register` | `ShowRiskRegisterAsync` | View risk register with active/expired/revoked acceptances |
| `compliance_create_poam` | `CreatePoamAsync` | Create POA&M item with milestones |
| `compliance_list_poam` | `ListPoamAsync` | List POA&M items with filtering |
| `compliance_generate_rar` | `GenerateRarAsync` | Generate Risk Assessment Report |
| `compliance_bundle_authorization_package` | `BundleAuthorizationPackageAsync` | Bundle complete authorization package |

---

## Tool Reference

### `compliance_register_system`

Registers a new information system for RMF compliance tracking. Sets initial RMF phase to **Prepare**.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `name` | string | Yes | System name (human-readable) |
| `system_type` | enum | Yes | `MajorApplication`, `GeneralSupportSystem`, `Enclave`, `PlatformIt`, `CloudServiceOffering` |
| `mission_criticality` | enum | Yes | `MissionCritical`, `MissionEssential`, `MissionSupport` |
| `hosting_environment` | enum | Yes | `Commercial`, `Government`, `GovernmentAirGappedIl5`, `GovernmentAirGappedIl6` |
| `acronym` | string | No | System acronym |
| `description` | string | No | System description |

**Response (success):**
```json
{
  "status": "success",
  "data": {
    "id": "guid",
    "name": "System Name",
    "acronym": "SN",
    "system_type": "MajorApplication",
    "current_rmf_step": "Prepare",
    "mission_criticality": "MissionCritical",
    "created_at": "2025-01-01T00:00:00Z"
  },
  "metadata": { "tool": "compliance_register_system", "timestamp": "..." }
}
```

---

### `compliance_list_systems`

Lists registered systems with optional pagination and active-only filtering.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `page` | int | No | Page number (default: 1) |
| `page_size` | int | No | Items per page (1–100, default: 20) |
| `active_only` | bool | No | Filter to active systems only (default: false) |

**Response:** Paginated list with `systems[]` and `pagination { total_count, page, page_size }`.

---

### `compliance_get_system`

Retrieves full system details including security categorization, control baseline, authorization boundary resources, and RMF role assignments.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | Yes | System GUID |

**Response:** Full system entity with nested `boundary_resource_count`, `role_assignment_count`, security categorization, and baseline details.

---

### `compliance_advance_rmf_step`

Advances (or regresses) a system through the RMF lifecycle: Prepare → Categorize → Select → Implement → Assess → Authorize → Monitor.

**Gate Conditions (forward movement):**

| Transition | Requirements |
|-----------|-------------|
| Prepare → Categorize | ≥1 active RMF role + ≥1 boundary resource |
| Categorize → Select | Security categorization + ≥1 information type |
| Select → Implement | Control baseline exists |
| Implement → Assess | Advisory only (full checks deferred) |
| Assess → Authorize | Advisory only |
| Authorize → Monitor | Advisory only |

Backward movement requires `force=true`. Force overrides also bypass failed forward gates (logged at Warning level).

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | Yes | System GUID |
| `target_step` | enum | Yes | `Prepare`, `Categorize`, `Select`, `Implement`, `Assess`, `Authorize`, `Monitor` |
| `force` | bool | No | Override gate failures or allow backward movement (default: false) |

**Response:** `previous_step`, `new_step`, `was_forced`, `gate_results[]` with pass/fail details.

---

### `compliance_define_boundary`

Defines or updates the authorization boundary for a system by adding Azure resource references.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | Yes | System GUID |
| `resources` | array | Yes | Array of `{ resource_id, resource_type, resource_name, inheritance_provider? }` |

Handles duplicates gracefully — re-includes previously excluded resources. Returns count of `resources_added`.

---

### `compliance_exclude_from_boundary`

Marks a resource as excluded from the authorization boundary with a rationale.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | Yes | System GUID |
| `resource_id` | string | Yes | Resource identifier to exclude |
| `rationale` | string | Yes | Justification for exclusion |

---

### `compliance_assign_rmf_role`

Assigns an RMF role to a user for a specific system. Idempotent — re-activates previously deactivated assignments.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | Yes | System GUID |
| `role` | enum | Yes | `AuthorizingOfficial`, `Issm`, `Isso`, `Sca`, `SystemOwner` |
| `user_id` | string | Yes | User identifier (e.g., email) |
| `user_display_name` | string | No | Human-readable user name |

---

### `compliance_list_rmf_roles`

Lists all active RMF role assignments for a system, ordered by role then user name.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | Yes | System GUID |

**Response:** Array of role assignments with `total_roles` count.

---

## US2: Security Categorization Tools

### `compliance_categorize_system`

Perform or update FIPS 199 / SP 800-60 security categorization for a registered system. Provide information types with C/I/A impact levels. Returns computed high-water mark, DoD Impact Level, and NIST baseline.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | Yes | RegisteredSystem ID (GUID) |
| `information_types` | array | Yes | Array of info types with `sp800_60_id`, `name`, `confidentiality_impact`, `integrity_impact`, `availability_impact` (Low\|Moderate\|High) |
| `is_national_security_system` | boolean | No | Whether the system is designated NSS (affects IL derivation) |
| `justification` | string | No | Overall categorization rationale |

**Response (success):**
```json
{
  "status": "success",
  "data": {
    "id": "guid",
    "system_id": "guid",
    "system_name": "System Name",
    "confidentiality_impact": "Moderate",
    "integrity_impact": "High",
    "availability_impact": "Moderate",
    "overall_categorization": "High",
    "fips_199_notation": "SC System = {(confidentiality, MODERATE), (integrity, HIGH), (availability, MODERATE)}",
    "dod_impact_level": "IL5",
    "nist_baseline": "High",
    "is_national_security_system": false,
    "information_type_count": 2,
    "information_types": [
      {
        "sp800_60_id": "C.3.5.8",
        "name": "Information Security",
        "confidentiality": "Moderate",
        "integrity": "High",
        "availability": "Moderate",
        "uses_provisional": true,
        "adjustment_justification": null
      }
    ]
  }
}
```

**High-Water Mark Computation:**
- The overall categorization is the **maximum** of C/I/A across all information types
- FIPS 199: SC = {(confidentiality, X), (integrity, Y), (availability, Z)}
- DoD IL derivation: Low→IL2, Moderate→IL4, High→IL5, NSS+classified→IL6

---

### `compliance_get_categorization`

Retrieve the FIPS 199 security categorization for a registered system, including all information types and computed fields.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | Yes | RegisteredSystem ID (GUID) |

**Response:** Same structure as `compliance_categorize_system`, or `data: null` with message if no categorization exists.

---

### `compliance_suggest_info_types`

Suggest SP 800-60 information types based on system description, type, and mission criticality. Returns a ranked list with confidence scores using heuristic keyword matching against a 16-entry SP 800-60 Vol. 2 catalog.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | Yes | RegisteredSystem ID (GUID) |
| `description` | string | No | Additional context for better suggestions |

**Response (success):**
```json
{
  "status": "success",
  "data": {
    "system_id": "guid",
    "system_name": "System Name",
    "suggestion_count": 5,
    "suggestions": [
      {
        "sp800_60_id": "C.3.5.8",
        "name": "Information Security",
        "category": "Management and Support",
        "confidence": 0.85,
        "rationale": "System description matches 'security'",
        "default_confidentiality_impact": "Moderate",
        "default_integrity_impact": "Moderate",
        "default_availability_impact": "Low"
      }
    ]
  }
}
```

---

### `compliance_select_baseline`

Selects the NIST SP 800-53 control baseline for a system based on its FIPS 199 categorization. Optionally applies the CNSSI 1253 overlay matching the DoD Impact Level.

**Prerequisite:** System must have a security categorization (run `compliance_categorize_system` first).

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | Yes | RegisteredSystem ID (GUID) |
| `apply_overlay` | bool | No | Whether to apply the CNSSI 1253 overlay (default: true) |
| `overlay_name` | string | No | Override overlay name (e.g., "CNSSI 1253 IL5") |

**Response (success):**
```json
{
  "status": "success",
  "data": {
    "id": "guid",
    "system_id": "guid",
    "baseline_level": "Moderate",
    "overlay_applied": "CNSSI 1253 IL4",
    "total_controls": 335,
    "customer_controls": 0,
    "inherited_controls": 0,
    "shared_controls": 0,
    "tailored_out_controls": 0,
    "tailored_in_controls": 0,
    "control_ids": ["AC-1", "AC-2", "..."],
    "created_by": "mcp-user",
    "created_at": "2025-01-01T00:00:00Z"
  }
}
```

**Baseline Levels:** Low (152 controls), Moderate (329 controls), High (400 controls). Overlay adds CNSSI 1253 enhancement controls.

---

### `compliance_tailor_baseline`

Add or remove controls from the selected baseline. Supports adding organization-specific controls and removing non-applicable controls with rationale.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | Yes | RegisteredSystem ID (GUID) |
| `tailoring_actions` | array | Yes | Array of `{ control_id, action, rationale }` |

Each tailoring action:
- `control_id` — NIST control ID (e.g., "AC-99" or "ZZ-1")
- `action` — `"Added"` or `"Removed"`
- `rationale` — Justification for the tailoring decision

**Response (success):**
```json
{
  "status": "success",
  "data": {
    "accepted_count": 3,
    "rejected_count": 0,
    "tailored_in": 2,
    "tailored_out": 1,
    "total_controls": 336,
    "accepted": [
      { "control_id": "ZZ-99", "action": "Added", "accepted": true },
      { "control_id": "AC-1", "action": "Removed", "accepted": true, "reason": "WARNING: Control is required by overlay..." }
    ]
  }
}
```

---

### `compliance_set_inheritance`

Set the inheritance type for controls in the baseline. Maps each control to an inheritance provider (e.g., FedRAMP-authorized cloud service).

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | Yes | RegisteredSystem ID (GUID) |
| `inheritance_mappings` | array | Yes | Array of `{ control_id, inheritance_type, provider, customer_responsibility }` |

Each mapping:
- `control_id` — Control ID in the baseline
- `inheritance_type` — `"Inherited"`, `"Shared"`, or `"Customer"`
- `provider` — Provider or CSP name (e.g., "Azure Government (FedRAMP High)")
- `customer_responsibility` — (optional) Customer's responsibility for shared controls

**Response (success):**
```json
{
  "status": "success",
  "data": {
    "controls_updated": 60,
    "inherited_count": 50,
    "shared_count": 10,
    "customer_count": 0,
    "skipped_controls": []
  }
}
```

---

### `compliance_get_baseline`

Retrieve the current control baseline for a system. Optionally includes tailoring and inheritance details, and can filter by control family.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | Yes | RegisteredSystem ID (GUID) |
| `include_details` | bool | No | Include tailoring and inheritance records (default: false) |
| `family_filter` | string | No | Filter by control family prefix (e.g., "AC") |

**Response (success):**
```json
{
  "status": "success",
  "data": {
    "id": "guid",
    "baseline_level": "Moderate",
    "total_controls": 336,
    "control_ids": ["AC-1", "AC-2", "..."],
    "tailorings": [
      { "control_id": "ZZ-99", "action": "Added", "rationale": "..." }
    ],
    "inheritances": [
      { "control_id": "AC-1", "inheritance_type": "Inherited", "provider": "Azure Government" }
    ]
  }
}
```

---

### `compliance_generate_crm`

Generate a Customer Responsibility Matrix (CRM) for the system. Groups controls by NIST 800-53 family and shows inheritance coverage.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | Yes | RegisteredSystem ID (GUID) |

**Response (success):**
```json
{
  "status": "success",
  "data": {
    "system_name": "My Application System",
    "baseline_level": "Moderate",
    "total_controls": 336,
    "inherited_controls": 50,
    "shared_controls": 10,
    "customer_controls": 0,
    "undesignated_controls": 276,
    "inheritance_percentage": 17.86,
    "family_groups": [
      {
        "family": "AC",
        "family_name": "Access Control",
        "total": 25,
        "inherited": 10,
        "shared": 2,
        "customer": 0,
        "controls": [
          { "control_id": "AC-1", "inheritance_type": "Inherited", "provider": "Azure Government" }
        ]
      }
    ]
  }
}
```

---

## US5: SSP Authoring & Narrative Management Tools

### `compliance_write_narrative`

Write or update the implementation narrative for a NIST 800-53 control in a system's SSP. Creates a new narrative or updates an existing one (upsert behavior).

**RBAC:** Compliance.PlatformEngineer, Compliance.SecurityLead

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | Yes | RegisteredSystem ID (GUID) |
| `control_id` | string | Yes | NIST 800-53 control ID (e.g., "AC-1") |
| `narrative` | string | Yes | Implementation narrative text |
| `status` | enum | No | `Implemented`, `PartiallyImplemented`, `Planned`, `NotApplicable` (default: `Implemented`) |

**Response (success):**
```json
{
  "status": "success",
  "data": {
    "id": "guid",
    "system_id": "guid",
    "control_id": "AC-1",
    "implementation_status": "Implemented",
    "narrative": "Access control policies are enforced...",
    "is_auto_populated": false,
    "ai_suggested": false,
    "authored_by": "mcp-user",
    "authored_at": "2025-01-01T00:00:00Z",
    "modified_at": null
  },
  "metadata": { "tool": "compliance_write_narrative", "timestamp": "..." }
}
```

**Upsert Behavior:** If a narrative already exists for the (system_id, control_id) pair, the narrative and status are updated and `modified_at` is set.

---

### `compliance_suggest_narrative`

Generate an AI-suggested implementation narrative for a NIST 800-53 control based on system context, control requirements, and inheritance data. Returns a draft narrative with confidence score and reference sources.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | Yes | RegisteredSystem ID (GUID) |
| `control_id` | string | Yes | NIST 800-53 control ID (e.g., "AC-2") |

**Response (success):**
```json
{
  "status": "success",
  "data": {
    "control_id": "AC-2",
    "suggested_narrative": "This control is fully inherited from Azure Government...",
    "confidence": 0.85,
    "references": [
      "NIST SP 800-53 Rev. 5",
      "FedRAMP High Baseline",
      "Azure Government FedRAMP High P-ATO"
    ]
  },
  "metadata": { "tool": "compliance_suggest_narrative", "timestamp": "..." }
}
```

**Confidence Levels:**
- **0.85** — Inherited controls (high confidence, mostly provider-documented)
- **0.75** — Shared controls (moderate-high, partial provider coverage)
- **0.55** — Customer controls (lower confidence, requires review)

---

### `compliance_batch_populate_narratives`

Auto-populate implementation narratives for inherited and/or shared controls using provider templates. Skips controls that already have narratives (idempotent). Significantly speeds up SSP authoring by pre-filling inherited control documentation.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | Yes | RegisteredSystem ID (GUID) |
| `inheritance_type` | enum | No | Filter: `Inherited`, `Shared`, or omit for both |

**Response (success):**
```json
{
  "status": "success",
  "data": {
    "populated_count": 45,
    "skipped_count": 5,
    "populated_control_ids": ["AC-1", "AC-2", "..."],
    "skipped_control_ids": ["AC-3", "AC-4", "..."]
  },
  "metadata": { "tool": "compliance_batch_populate_narratives", "timestamp": "..." }
}
```

**Progress Reporting:** When called programmatically with `IProgress<string>`, reports progress every 10 controls processed.

---

### `compliance_narrative_progress`

Get SSP narrative completion status for a system. Shows per-family progress (total, completed, draft, missing controls) and overall completion percentage. Useful for tracking SSP readiness before assessment.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | Yes | RegisteredSystem ID (GUID) |
| `family_filter` | string | No | Filter by control family prefix (e.g., "AC") |

**Response (success):**
```json
{
  "status": "success",
  "data": {
    "system_id": "guid",
    "total_controls": 335,
    "completed_narratives": 280,
    "draft_narratives": 30,
    "missing_narratives": 25,
    "overall_percentage": 92.54,
    "family_breakdowns": [
      {
        "family": "AC",
        "total": 25,
        "completed": 22,
        "draft": 2,
        "missing": 1
      }
    ]
  },
  "metadata": { "tool": "compliance_narrative_progress", "timestamp": "..." }
}
```

**Status Classification:**
- **Completed**: `Implemented` or `NotApplicable` status
- **Draft**: `PartiallyImplemented` or `Planned` status
- **Missing**: No narrative record exists

---

### `compliance_generate_ssp`

Generate the System Security Plan (SSP) document for a registered system. Produces a Markdown document containing system information, security categorization, control baseline, and per-control implementation narratives. Warns about controls with missing narratives.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | Yes | RegisteredSystem ID (GUID) |
| `format` | string | No | Output format: `markdown` (default) or `docx` |
| `sections` | string | No | Specific sections to include (comma-separated): `system_information`, `categorization`, `baseline`, `controls`. Default: all. |

**Response (success):**
```json
{
  "status": "success",
  "data": {
    "system_id": "guid",
    "system_name": "My Application System",
    "format": "markdown",
    "total_controls": 335,
    "controls_with_narratives": 310,
    "controls_missing_narratives": 25,
    "sections": ["system_information", "categorization", "baseline", "controls"],
    "warnings": [
      "Control AC-3 has no implementation narrative",
      "Control AU-7 has no implementation narrative"
    ],
    "content": "# System Security Plan (SSP)\n\n## 1. System Information\n...",
    "generated_at": "2025-01-15T10:30:00Z"
  },
  "metadata": { "tool": "compliance_generate_ssp", "timestamp": "..." }
}
```

**SSP Document Sections:**

| Section | Content |
|---------|---------|
| `system_information` | System name, type, mission criticality, hosting environment, RMF status |
| `categorization` | FIPS 199 notation, C/I/A impacts, DoD IL, information types |
| `baseline` | Baseline level, overlay applied, total controls, tailoring/inheritance summary |
| `controls` | Per-family grouped controls with narratives, status, inheritance type |

**Progress Reporting:** When called programmatically with `IProgress<string>`, reports progress per section and per control family.

---

## Assessment Artifact Tools (US7)

### `compliance_assess_control`

Record an SCA's per-control effectiveness determination (Satisfied/OtherThanSatisfied) with DoD CAT severity mapping.

**RBAC:** `Compliance.Auditor` (SCA)

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `assessment_id` | string | Yes | ComplianceAssessment ID |
| `control_id` | string | Yes | NIST 800-53 control ID (e.g., "AC-2") |
| `determination` | enum | Yes | `Satisfied` or `OtherThanSatisfied` |
| `method` | string | No | Assessment method: `Test`, `Interview`, `Examine` |
| `evidence_ids` | string | No | Comma-separated or JSON array of evidence record IDs |
| `notes` | string | No | Assessor notes (max 4000 chars) |
| `cat_severity` | enum | No* | `CatI`, `CatII`, `CatIII` — **required if OtherThanSatisfied** |

**Response (success):**

```json
{
  "status": "success",
  "data": {
    "id": "...",
    "control_id": "AC-3",
    "determination": "OtherThanSatisfied",
    "cat_severity": "CatII",
    "assessment_method": "Test",
    "assessor_id": "mcp-user",
    "assessed_at": "2025-01-15T10:00:00Z"
  }
}
```

---

### `compliance_take_snapshot`

Create an immutable, SHA-256-hashed snapshot of the current assessment state for audit trail.

**RBAC:** `Compliance.Auditor`

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | Yes | RegisteredSystem ID |
| `assessment_id` | string | Yes | ComplianceAssessment ID |

**Response (success):**

```json
{
  "status": "success",
  "data": {
    "snapshot_id": "...",
    "captured_at": "2025-01-15T10:30:00Z",
    "compliance_score": 85.0,
    "total_controls": 20,
    "passed_controls": 17,
    "failed_controls": 3,
    "integrity_hash": "a1b2c3d4...64-char-hex",
    "is_immutable": true
  }
}
```

**Immutability:** Once created, snapshots cannot be updated or deleted. The integrity hash covers all ControlEffectiveness determinations, ComplianceFinding summaries, and ComplianceEvidence hashes in canonical JSON form.

---

### `compliance_compare_snapshots`

Compare two assessment snapshots side-by-side showing controls changed, score delta, and findings.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `snapshot_id_a` | string | Yes | First snapshot ID |
| `snapshot_id_b` | string | Yes | Second snapshot ID |

**Response (success):**

```json
{
  "status": "success",
  "data": {
    "score_delta": 15.0,
    "newly_satisfied": ["AC-3", "AU-6"],
    "newly_other_than_satisfied": [],
    "unchanged_count": 18,
    "new_findings": 0,
    "resolved_findings": 2,
    "evidence_added": 3,
    "evidence_removed": 0
  }
}
```

---

### `compliance_verify_evidence`

Recompute the SHA-256 hash of evidence content and verify it matches the stored hash. Updates `IntegrityVerifiedAt` on success.

**RBAC:** `Compliance.Auditor`

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `evidence_id` | string | Yes | ComplianceEvidence ID |

**Response (success):**

```json
{
  "status": "success",
  "data": {
    "evidence_id": "ev-1",
    "control_id": "AC-2",
    "original_hash": "a1b2c3...",
    "recomputed_hash": "a1b2c3...",
    "verification_status": "verified",
    "collector_identity": "sca@example.com",
    "collection_method": "automated_scan",
    "integrity_verified_at": "2025-01-15T10:35:00Z"
  }
}
```

**Tamper Detection:** If `verification_status` is `"tampered"`, the original and recomputed hashes will differ — this indicates evidence content was modified after collection.

---

### `compliance_check_evidence_completeness`

Report which controls have verified evidence vs. missing evidence with overall completeness percentage.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | Yes | RegisteredSystem ID |
| `assessment_id` | string | No | Filter to specific assessment |
| `family_filter` | string | No | Filter by family prefix (e.g., "AC") |

**Response (success):**

```json
{
  "status": "success",
  "data": {
    "completeness_percentage": 75.0,
    "total_controls": 8,
    "controls_with_evidence": 6,
    "controls_without_evidence": 2,
    "controls_with_unverified_evidence": 1,
    "control_statuses": [
      { "control_id": "AC-2", "status": "verified", "evidence_count": 2, "verified_count": 2 },
      { "control_id": "AC-3", "status": "unverified", "evidence_count": 1, "verified_count": 0 },
      { "control_id": "AC-4", "status": "missing", "evidence_count": 0, "verified_count": 0 }
    ]
  }
}
```

---

### `compliance_generate_sar`

Generate a Security Assessment Report (SAR) with executive summary, control-by-control results, risk summary, and CAT severity breakdown.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | Yes | RegisteredSystem ID |
| `assessment_id` | string | Yes | ComplianceAssessment ID |
| `format` | string | No | Output format: `markdown` (default) or `docx` |

**Response (success):**

```json
{
  "status": "success",
  "data": {
    "compliance_score": 87.5,
    "controls_assessed": 20,
    "controls_satisfied": 17,
    "controls_other_than_satisfied": 3,
    "cat_breakdown": { "cat_i": 0, "cat_ii": 2, "cat_iii": 1 },
    "family_results": [
      { "family": "AC", "assessed": 10, "satisfied": 8, "other_than_satisfied": 2 }
    ],
    "content": "# Security Assessment Report\n...",
    "generated_at": "2025-01-15T11:00:00Z"
  }
}
```

**SAR Sections:**

| Section | Content |
|---------|---------|
| Executive Summary | System name, assessor, overall score, finding counts |
| CAT Severity Breakdown | CAT I/II/III finding counts and risk level |
| Control Family Results | Per-family Satisfied/OtherThanSatisfied with CAT mapping |
| Risk Summary | Assessment-to-authorization risk posture |
| Detailed Findings | Per-control determination details |

---

## Phase 10 — Authorization Decisions & Risk Acceptance (US8)

### `compliance_issue_authorization`

Issues an authorization decision (ATO, ATOwC, IATT, DATO) for a registered system. Automatically supersedes any prior active decision and advances the system's RMF step to **Monitor**.

**RBAC:** `Compliance.AuthorizingOfficial` **only**

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | Yes | RegisteredSystem GUID |
| `decision_type` | enum | Yes | `ATO`, `AtoWithConditions`, `IATT`, `DATO` |
| `expiration_date` | string | No | ISO-8601 expiration date (required for ATO/ATOwC/IATT) |
| `terms_and_conditions` | string | No | Authorization terms and conditions text |
| `residual_risk_level` | enum | Yes | `Low`, `Medium`, `High`, `Critical` |
| `residual_risk_justification` | string | No | Justification for the residual risk level |
| `risk_acceptances` | string | No | JSON array of inline risk acceptances: `[{finding_id, control_id, cat_severity, justification, compensating_control?, expiration_date}]` |

**Response (success):**
```json
{
  "status": "success",
  "data": {
    "id": "guid",
    "system_id": "guid",
    "decision_type": "Ato",
    "decision_date": "2025-01-15T00:00:00Z",
    "expiration_date": "2028-01-15T00:00:00Z",
    "residual_risk_level": "Low",
    "residual_risk_justification": "All CAT I findings remediated",
    "compliance_score": 95.5,
    "terms_and_conditions": "Annual re-assessment required",
    "is_active": true,
    "issued_by": "mcp-user",
    "issued_by_name": "MCP User",
    "risk_acceptances_count": 2
  },
  "metadata": { "tool": "compliance_issue_authorization", "timestamp": "..." }
}
```

**Behavior:**
- Validates the system exists and is registered
- Calculates the compliance score from `ControlEffectiveness` records at decision time
- If a prior active authorization exists, it is deactivated (`IsActive=false`) and linked via `SupersededById`
- Creates `RiskAcceptance` records for any inline risk acceptances
- Advances the system's `CurrentRmfStep` to `Monitor`

---

### `compliance_accept_risk`

Accepts risk on a specific finding and control. Requires an active authorization decision to exist for the system.

**RBAC:** `Compliance.AuthorizingOfficial` **only**

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | Yes | RegisteredSystem GUID |
| `finding_id` | string | Yes | ComplianceFinding ID |
| `control_id` | string | Yes | NIST control ID (e.g., `AC-2`) |
| `cat_severity` | enum | Yes | `CatI`, `CatII`, `CatIII` |
| `justification` | string | Yes | Risk acceptance rationale |
| `compensating_control` | string | No | Compensating control description |
| `expiration_date` | string | Yes | ISO-8601 expiration date for auto-expire |

**Response (success):**
```json
{
  "status": "success",
  "data": {
    "id": "guid",
    "authorization_decision_id": "guid",
    "finding_id": "guid",
    "control_id": "AC-2",
    "cat_severity": "CatII",
    "justification": "Compensating controls in place",
    "compensating_control": "Network segmentation applied",
    "expiration_date": "2025-12-31T00:00:00Z",
    "accepted_by": "mcp-user",
    "accepted_at": "2025-01-15T12:00:00Z",
    "is_active": true
  },
  "metadata": { "tool": "compliance_accept_risk", "timestamp": "..." }
}
```

---

### `compliance_show_risk_register`

Views the risk register showing all risk acceptances for a system. Automatically expires past-due acceptances on query.

**RBAC:** All compliance roles

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | Yes | RegisteredSystem GUID |
| `status_filter` | enum | No | `active`, `expired`, `revoked`, `all` (default: `active`) |

**Response (success):**
```json
{
  "status": "success",
  "data": {
    "system_id": "guid",
    "total_acceptances": 5,
    "active_count": 3,
    "expired_count": 1,
    "revoked_count": 1,
    "acceptances": [
      {
        "id": "guid",
        "control_id": "AC-2",
        "cat_severity": "CatII",
        "justification": "...",
        "compensating_control": "...",
        "expiration_date": "2025-12-31T00:00:00Z",
        "accepted_at": "2025-01-15T12:00:00Z",
        "accepted_by": "ao-user",
        "status": "active",
        "finding_title": "Missing MFA enforcement"
      }
    ]
  },
  "metadata": { "tool": "compliance_show_risk_register", "timestamp": "..." }
}
```

---

### `compliance_create_poam`

Creates a formal Plan of Action & Milestones (POA&M) item with optional milestones. Links the weakness to a NIST control and DoD CAT severity.

**RBAC:** `Compliance.SecurityLead` (ISSM) or `Compliance.Administrator`

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | Yes | RegisteredSystem GUID |
| `finding_id` | string | No | ComplianceFinding ID (optional link) |
| `weakness` | string | Yes | Weakness description (max 2000 chars) |
| `control_id` | string | Yes | NIST control ID (e.g., `AC-2`) |
| `cat_severity` | enum | Yes | `CatI`, `CatII`, `CatIII` |
| `poc` | string | Yes | Point of contact |
| `scheduled_completion` | string | Yes | ISO-8601 scheduled completion date |
| `resources_required` | string | No | Resources required description |
| `milestones` | string | No | JSON array: `[{description, target_date}]` |

**Response (success):**
```json
{
  "status": "success",
  "data": {
    "id": "guid",
    "system_id": "guid",
    "finding_id": "guid",
    "weakness": "Insufficient access control logging",
    "weakness_source": "Assessment",
    "control_id": "AU-2",
    "cat_severity": "CatII",
    "poc": "John Smith",
    "resources_required": "SIEM integration (40 hours)",
    "scheduled_completion": "2025-06-30T00:00:00Z",
    "status": "Ongoing",
    "created_at": "2025-01-15T00:00:00Z",
    "milestones": [
      {
        "id": "guid",
        "description": "Configure SIEM connectors",
        "target_date": "2025-03-31T00:00:00Z",
        "completed_date": null,
        "sequence": 1,
        "is_overdue": false
      }
    ]
  },
  "metadata": { "tool": "compliance_create_poam", "timestamp": "..." }
}
```

---

### `compliance_list_poam`

Lists POA&M items for a system with optional status, severity, and overdue-only filters.

**RBAC:** All compliance roles

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | Yes | RegisteredSystem GUID |
| `status_filter` | enum | No | `Ongoing`, `Completed`, `Delayed`, `RiskAccepted` |
| `severity_filter` | enum | No | `CatI`, `CatII`, `CatIII` |
| `overdue_only` | string | No | `true` to show only overdue items |

**Response (success):**
```json
{
  "status": "success",
  "data": {
    "system_id": "guid",
    "total_items": 4,
    "ongoing_count": 2,
    "completed_count": 1,
    "delayed_count": 1,
    "overdue_count": 1,
    "items": [
      {
        "id": "guid",
        "weakness": "...",
        "control_id": "AC-2",
        "cat_severity": "CatII",
        "poc": "Jane Doe",
        "status": "Ongoing",
        "scheduled_completion": "2025-06-30T00:00:00Z",
        "actual_completion": null,
        "milestone_count": 3,
        "is_overdue": false
      }
    ]
  },
  "metadata": { "tool": "compliance_list_poam", "timestamp": "..." }
}
```

---

### `compliance_generate_rar`

Generates a Risk Assessment Report (RAR) with per-family risk analysis, CAT severity breakdown, and aggregate residual risk level.

**RBAC:** `Compliance.Auditor` or `Compliance.SecurityLead`

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | Yes | RegisteredSystem GUID |
| `assessment_id` | string | Yes | ComplianceAssessment ID |
| `format` | string | No | Output format: `markdown` (default) |

**Response (success):**
```json
{
  "status": "success",
  "data": {
    "system_id": "guid",
    "assessment_id": "guid",
    "generated_at": "2025-01-15T12:00:00Z",
    "format": "markdown",
    "executive_summary": "Risk Assessment Report for ...",
    "aggregate_risk_level": "Medium",
    "cat_breakdown": {
      "cat_i": 0,
      "cat_ii": 3,
      "cat_iii": 5,
      "total": 8
    },
    "family_risks": [
      {
        "family": "AC",
        "family_name": "Access Control",
        "total_findings": 4,
        "open_findings": 2,
        "accepted_findings": 1,
        "risk_level": "Medium"
      }
    ],
    "content": "# Risk Assessment Report\n..."
  },
  "metadata": { "tool": "compliance_generate_rar", "timestamp": "..." }
}
```

**RAR Sections:**

| Section | Content |
|---------|---------|
| Executive Summary | System name, assessment date, overall risk determination |
| CAT Severity Breakdown | CAT I/II/III finding counts by family |
| Control Family Analysis | Per-family risk with open/accepted/total findings |
| Risk Trending | Aggregate risk determination with justification |

---

### `compliance_bundle_authorization_package`

Bundles a complete authorization package containing SSP, SAR, RAR, POA&M, CRM, and ATO Letter. Reports document availability status for any missing documents.

**RBAC:** `Compliance.SecurityLead` or `Compliance.Administrator`

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | Yes | RegisteredSystem GUID |
| `format` | string | No | Output format: `markdown` (default) |
| `include_evidence` | string | No | `true` to include evidence documents |

**Response (success):**
```json
{
  "status": "success",
  "data": {
    "system_id": "guid",
    "generated_at": "2025-01-15T12:00:00Z",
    "format": "markdown",
    "document_count": 6,
    "includes_evidence": false,
    "documents": [
      {
        "name": "System Security Plan",
        "file_name": "SSP.md",
        "document_type": "SSP",
        "status": "included",
        "content": "# System Security Plan\n..."
      },
      {
        "name": "Security Assessment Report",
        "file_name": "SAR.md",
        "document_type": "SAR",
        "status": "included",
        "content": "..."
      },
      {
        "name": "Risk Assessment Report",
        "file_name": "RAR.md",
        "document_type": "RAR",
        "status": "generated",
        "content": "# Risk Assessment Report\n..."
      },
      {
        "name": "Plan of Action & Milestones",
        "file_name": "POAM.md",
        "document_type": "POAM",
        "status": "generated",
        "content": "| # | Weakness | Control | CAT | POC | Scheduled | Status |\n..."
      },
      {
        "name": "Customer Responsibility Matrix",
        "file_name": "CRM.md",
        "document_type": "CRM",
        "status": "included",
        "content": "..."
      },
      {
        "name": "ATO Letter",
        "file_name": "ATO_Letter.md",
        "document_type": "ATO_Letter",
        "status": "included",
        "content": "..."
      }
    ]
  },
  "metadata": { "tool": "compliance_bundle_authorization_package", "timestamp": "..." }
}
```

**Document Status Values:**

| Status | Meaning |
|--------|---------|
| `included` | Document found in `ComplianceDocuments` table |
| `generated` | Document generated dynamically (RAR, POA&M table) |
| `not_found` | Document not yet created for this system |

---

## Phase 11 — Continuous Monitoring & Lifecycle (US9)

### `compliance_create_conmon_plan`

Create or update the continuous monitoring plan for a registered system (one plan per system — upsert).

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | yes | RegisteredSystem ID |
| `assessment_frequency` | string | yes | `Monthly` \| `Quarterly` \| `Annually` |
| `annual_review_date` | string | yes | ISO 8601 date (e.g., `2026-06-15`) |
| `report_distribution` | string[] | no | User IDs or role names for report distribution |
| `significant_change_triggers` | string[] | no | Custom trigger descriptions |

**Returns:** ConMonPlan record with plan ID, frequency, review date, and distribution list.

```json
{
  "status": "success",
  "data": {
    "plan_id": "...",
    "system_id": "...",
    "assessment_frequency": "Monthly",
    "annual_review_date": "2026-06-15",
    "report_distribution": ["ISSM", "AO"],
    "significant_change_triggers": ["New Interconnection"],
    "created_by": "system",
    "created_at": "2026-01-15T10:00:00Z"
  }
}
```

---

### `compliance_generate_conmon_report`

Generate a periodic continuous monitoring report with compliance score, delta from authorization baseline, findings, and POA&M status.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | yes | RegisteredSystem ID |
| `report_type` | string | yes | `Monthly` \| `Quarterly` \| `Annual` |
| `period` | string | yes | Report period (e.g., `2026-02`, `2026-Q1`, `2026`) |

**Returns:** ConMonReport with compliance metrics and markdown report content.

```json
{
  "status": "success",
  "data": {
    "report_id": "...",
    "compliance_score": 92.5,
    "authorized_baseline_score": 95.0,
    "score_delta": -2.5,
    "new_findings": 3,
    "resolved_findings": 5,
    "open_poam_items": 2,
    "overdue_poam_items": 0,
    "report_content": "# Continuous Monitoring Report ..."
  }
}
```

---

### `compliance_report_significant_change`

Report a significant change that may trigger reauthorization review. Automatically classifies whether the change type requires reauthorization.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | yes | RegisteredSystem ID |
| `change_type` | string | yes | Change category (e.g., `New Interconnection`, `Major Upgrade`) |
| `description` | string | yes | Detailed description of the change |

**Reauthorization trigger types:** New Interconnection, Major Upgrade, Data Type Change, Security Architecture Change, Operating Environment Change, New Threat, Security Incident, Boundary Change, Key Personnel Change, Compliance Framework Change.

```json
{
  "status": "success",
  "data": {
    "change_id": "...",
    "change_type": "New Interconnection",
    "requires_reauthorization": true,
    "reauthorization_triggered": false,
    "disposition": null
  }
}
```

---

### `compliance_track_ato_expiration`

Check ATO expiration status with graduated alerts at 90/60/30 days. DATO systems always return `None` alert level.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | yes | RegisteredSystem ID |

**Alert Levels:**

| Level | Days Remaining | Action |
|-------|---------------|--------|
| `None` | > 90 days or DATO | No action needed |
| `Info` | 60–90 days | Begin reauthorization planning |
| `Warning` | 30–60 days | Submit reauthorization package |
| `Urgent` | < 30 days | Escalate to AO immediately |
| `Expired` | ≤ 0 days | System operating without authorization |

```json
{
  "status": "success",
  "data": {
    "system_id": "...",
    "system_name": "ACME Portal",
    "has_active_authorization": true,
    "decision_type": "Ato",
    "expiration_date": "2026-12-31",
    "days_until_expiration": 55,
    "alert_level": "Warning",
    "alert_message": "ATO expires in 55 days. Submit reauthorization package.",
    "is_expired": false
  }
}
```

---

### `compliance_multi_system_dashboard`

View all systems with name, impact level, RMF step, authorization status, compliance score, and alerts.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `active_only` | string | no | `true` (default) or `false` to include deactivated systems |

```json
{
  "status": "success",
  "data": {
    "total_systems": 3,
    "authorized_count": 2,
    "expiring_count": 1,
    "expired_count": 0,
    "systems": [
      {
        "system_id": "...",
        "name": "ACME Portal",
        "acronym": "ACP",
        "impact_level": "Moderate",
        "current_rmf_step": "Monitor",
        "authorization_status": "Authorized",
        "decision_type": "Ato",
        "expiration_date": "2026-12-31",
        "days_until_expiration": 340,
        "compliance_score": 95.2,
        "open_findings": 3,
        "open_poam_items": 1,
        "alert_count": 0
      }
    ]
  }
}
```

---

### `compliance_reauthorization_workflow`

Detect reauthorization triggers and optionally initiate the reauthorization workflow by regressing the RMF step to Assess.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | yes | RegisteredSystem ID |
| `initiate` | string | no | `true` to initiate reauthorization (default: `false` — check only) |

**Trigger sources:** ATO expiration (< 30 days), unreviewed significant changes requiring reauthorization, compliance score drift (> 10% below baseline).

```json
{
  "status": "success",
  "data": {
    "system_id": "...",
    "is_triggered": true,
    "triggers": ["ATO expiring in 25 days", "2 unreviewed significant changes require reauthorization"],
    "was_initiated": true,
    "previous_rmf_step": "Monitor",
    "new_rmf_step": "Assess",
    "unreviewed_change_count": 2
  }
}
```

---

### `compliance_send_notification`

Send continuous monitoring notifications (expiration alerts, significant change events) to configured recipients.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | yes | RegisteredSystem ID |
| `notification_type` | string | yes | `expiration` \| `significant_change` \| `conmon_report` |

**Phase 17 Enhancement**: Notifications now route through the AlertManager → AlertNotificationService pipeline.
Expiration alerts are auto-created by `ConMonService.CheckExpirationAsync()` with graduated severity
(Low@90d, Medium@60d, High@30d, Critical@expired). Significant change alerts are auto-created when
`RequiresReauthorization = true`. The tool response includes channel `alert_pipeline` when connected.

```json
{
  "status": "success",
  "data": {
    "notification_type": "expiration",
    "system_id": "...",
    "alert_level": "Warning",
    "alert_message": "ATO expires in 55 days.",
    "delivered": true,
    "channels": ["mcp_response", "alert_pipeline"]
  }
}
```

---

## eMASS & OSCAL Interoperability Tools (US10)

### `compliance_export_emass`

Export system data in eMASS-compatible Excel (.xlsx) format with standard column
headers matching the eMASS controls and POA&M import templates.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | yes | RegisteredSystem ID |
| `export_type` | string | yes | `controls`, `poam`, or `full` (both) |

```json
{
  "status": "success",
  "data": {
    "system_id": "...",
    "export_type": "controls",
    "controls_exported": 325,
    "poam_exported": 0,
    "controls_file_size_bytes": 45321,
    "poam_file_size_bytes": 0,
    "controls_base64": "<base64-encoded .xlsx>",
    "poam_base64": null
  },
  "metadata": {
    "format": "xlsx",
    "emass_compatible": true
  }
}
```

---

### `compliance_import_emass`

Import system data from an eMASS-compatible Excel file with configurable
conflict resolution (skip, overwrite, merge) and dry-run preview.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | yes | RegisteredSystem ID |
| `file_base64` | string | yes | Base64-encoded Excel file content |
| `conflict_strategy` | string | no | `skip` (default), `overwrite`, `merge` |
| `dry_run` | string | no | `true` (default) or `false` |

```json
{
  "status": "success",
  "data": {
    "system_id": "...",
    "dry_run": true,
    "conflict_strategy": "skip",
    "total_rows": 10,
    "imported": 3,
    "skipped": 7,
    "conflicts": 7,
    "conflict_details": [
      {
        "control_id": "AC-1",
        "field": "ImplementationStatus",
        "existing_value": "Implemented",
        "imported_value": "Planned",
        "resolution": "Skipped"
      }
    ]
  },
  "metadata": {
    "applied": false
  }
}
```

---

### `compliance_export_oscal`

Export system data in OSCAL JSON format (v1.0.6). Supports SSP,
assessment-results, and POA&M OSCAL models.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | yes | RegisteredSystem ID |
| `model` | string | yes | `ssp`, `assessment-results`, or `poam` |

```json
{
  "status": "success",
  "data": {
    "system_id": "...",
    "model": "ssp",
    "oscal_version": "1.0.6",
    "oscal_document": { "system-security-plan": { ... } }
  },
  "metadata": {
    "format": "json",
    "spec_version": "OSCAL 1.0.6"
  }
}
```

---

## Document Templates & PDF Export Tools (US11)

### `compliance_upload_template`

Upload a custom DOCX template for compliance document generation. Templates
contain `{{MergeField}}` placeholders that are validated against the document
type's merge-field schema (SSP, SAR, POA&M, RAR).

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `template_name` | string | Yes | Friendly display name for the template |
| `document_type` | string | Yes | Document type: `ssp`, `sar`, `poam`, `rar` |
| `file_base64` | string | Yes | Base64-encoded DOCX file content |
| `uploaded_by` | string | Yes | User performing the upload |

- **RBAC**: ISSM, AO
- **RMF Step**: Authorize (Step 5)

**Example response:**

```json
{
  "status": "success",
  "data": {
    "template_id": "abc-123",
    "template_name": "Agency SSP Template v2",
    "document_type": "ssp",
    "is_valid": true,
    "merge_fields_found": ["SystemName", "SystemAcronym", "SecurityCategorization"],
    "merge_fields_missing": [],
    "warnings": []
  }
}
```

### `compliance_list_templates`

List available document templates, optionally filtered by document type.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `document_type` | string | No | Filter by type: `ssp`, `sar`, `poam`, `rar` |

- **RBAC**: Any authenticated user
- **RMF Step**: All steps

**Example response:**

```json
{
  "status": "success",
  "data": {
    "total": 2,
    "templates": [
      {
        "template_id": "abc-123",
        "template_name": "Agency SSP Template v2",
        "document_type": "ssp",
        "uploaded_by": "issm@agency.gov",
        "file_size_bytes": 45056,
        "is_default": false
      }
    ]
  }
}
```

### `compliance_update_template`

Update an existing template by replacing the DOCX file, renaming, or both.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `template_id` | string | Yes | ID of the template to update |
| `file_base64` | string | No | New base64-encoded DOCX file |
| `new_name` | string | No | New template name |
| `updated_by` | string | Yes | User performing the update |

- **RBAC**: ISSM, AO
- **RMF Step**: Authorize (Step 5)

### `compliance_delete_template`

Delete a document template by ID. This action cannot be undone.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `template_id` | string | Yes | ID of the template to delete |

- **RBAC**: ISSM, AO
- **RMF Step**: Authorize (Step 5)

### `compliance_generate_document` (enhanced)

The existing document generation tool now supports three output formats:

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `document_type` | string | Yes | Document type: `ssp`, `poam`, `sar`, `rar` |
| `format` | string | No | Output format: `markdown` (default), `docx`, `pdf` |
| `system_id` | string | Conditional | Required for `docx`/`pdf` output |
| `template` | string | No | Custom template ID (DOCX/PDF only) |
| `subscription_id` | string | No | Azure subscription for evidence |
| `framework` | string | No | Compliance framework |
| `system_name` | string | No | System name for document |
| `board_id` | string | No | Kanban board ID for POA&M |

- **RBAC**: ISSM, SCA, AO
- **RMF Step**: Authorize (Step 5), Monitor (Step 6)
- **PDF Engine**: QuestPDF (Community Edition, MIT license)
- **Progress**: Reports streaming progress (0.0–1.0) during PDF generation
