# Feature 018 — MCP Tool Contracts

**Date**: 2026-03-04 | **Branch**: `018-sap-generation`

All tools follow the standard MCP response envelope: `{ status, data, metadata }`.

---

## 1. `compliance_generate_sap`

**Purpose**: Generate a Security Assessment Plan for a registered system.
**RBAC**: `Analyst`, `SecurityLead`, `Administrator`
**RMF Step**: Step 4 — Assess

### Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| `system_id` | `string` | Yes | — | Registered system ID |
| `assessment_id` | `string` | No | `null` | Optional assessment cycle to link SAP to |
| `schedule_start` | `string` (ISO 8601) | No | `null` | Assessment start date |
| `schedule_end` | `string` (ISO 8601) | No | `null` | Assessment end date |
| `team_members` | `JSON array` | No | `[]` | Array of `{ name, organization, role, contact_info? }` |
| `scope_notes` | `string` | No | `null` | SCA-provided assessment scope notes |
| `rules_of_engagement` | `string` | No | `null` | Assessment constraints and availability windows |
| `method_overrides` | `JSON array` | No | `[]` | Array of `{ control_id, methods[], rationale? }` |
| `format` | `string` | No | `"markdown"` | Output format: `"markdown"`, `"docx"`, `"pdf"` |

### Prerequisites

| Condition | Behavior |
|-----------|----------|
| `ControlBaseline` exists for system | **Required** — returns error `BASELINE_NOT_FOUND` if missing |
| System in `RmfPhase.Assess` | **Warning** — includes `"warnings": ["System is not in Assess phase"]` |
| SCA role assignment exists | **Warning** — includes `"warnings": ["No SCA role assignment found"]` |
| Existing Draft SAP | **Overwritten** — generates new Draft replacing the existing one |

### Response — Success

```json
{
  "status": "success",
  "data": {
    "sap_id": "guid",
    "system_id": "guid",
    "assessment_id": "guid | null",
    "title": "Security Assessment Plan — SystemName — FY26 Q2",
    "status": "Draft",
    "format": "markdown",
    "baseline_level": "Moderate",
    "total_controls": 325,
    "customer_controls": 280,
    "inherited_controls": 30,
    "shared_controls": 15,
    "stig_benchmark_count": 8,
    "controls_with_objectives": 310,
    "evidence_gaps": 45,
    "family_summaries": [
      {
        "family": "Access Control (AC)",
        "control_count": 25,
        "customer_count": 20,
        "inherited_count": 3,
        "methods": ["Examine", "Interview", "Test"]
      }
    ],
    "content": "# Security Assessment Plan\n\n## 1. Introduction...",
    "generated_at": "2026-03-04T14:30:00Z",
    "warnings": []
  },
  "metadata": {
    "tool": "compliance_generate_sap",
    "duration_ms": 4500,
    "timestamp": "2026-03-04T14:30:04Z"
  }
}
```

### Response — Error

```json
{
  "status": "error",
  "errorCode": "BASELINE_NOT_FOUND",
  "message": "No control baseline selected for system 'abc-123'. Select a baseline first using compliance_select_baseline.",
  "suggestion": "Run compliance_select_baseline with the system_id and desired baseline level (Low, Moderate, or High)."
}
```

---

## 2. `compliance_update_sap`

**Purpose**: Update a Draft SAP's schedule, scope, team, methods, or rules of engagement.
**RBAC**: `Analyst`, `SecurityLead`, `Administrator`

### Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| `sap_id` | `string` | Yes | — | SAP ID to update |
| `schedule_start` | `string` (ISO 8601) | No | `null` | Updated assessment start date |
| `schedule_end` | `string` (ISO 8601) | No | `null` | Updated assessment end date |
| `scope_notes` | `string` | No | `null` | Updated scope notes |
| `rules_of_engagement` | `string` | No | `null` | Updated ROE |
| `team_members` | `JSON array` | No | `null` | Replaces entire team member list |
| `method_overrides` | `JSON array` | No | `null` | Per-control method overrides |

### Constraints

- Only Draft SAPs can be updated — Finalized SAPs return error `SAP_FINALIZED`
- Team replacement is atomic — providing `team_members` replaces all existing members
- Method overrides are additive — only specified controls are updated

### Response — Success

```json
{
  "status": "success",
  "data": {
    "sap_id": "guid",
    "status": "Draft",
    "updated_fields": ["schedule_start", "schedule_end", "method_overrides"],
    "method_overrides_applied": 3,
    "content": "# Security Assessment Plan\n\n...(re-rendered)...",
    "updated_at": "2026-03-04T15:00:00Z"
  },
  "metadata": {
    "tool": "compliance_update_sap",
    "duration_ms": 2100,
    "timestamp": "2026-03-04T15:00:02Z"
  }
}
```

### Response — Error (Finalized)

```json
{
  "status": "error",
  "errorCode": "SAP_FINALIZED",
  "message": "SAP 'abc-123' is finalized and cannot be modified.",
  "suggestion": "Generate a new SAP using compliance_generate_sap to create a fresh Draft."
}
```

---

## 3. `compliance_finalize_sap`

**Purpose**: Lock a Draft SAP as Finalized with SHA-256 integrity hash.
**RBAC**: `Analyst`, `SecurityLead`, `Administrator`

### Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| `sap_id` | `string` | Yes | — | SAP ID to finalize |

### Constraints

- Only Draft SAPs can be finalized
- Computes SHA-256 of `Content` field
- Sets `FinalizedBy`, `FinalizedAt`, `ContentHash`
- Finalized SAPs are immutable — no updates, no re-finalization

### Response — Success

```json
{
  "status": "success",
  "data": {
    "sap_id": "guid",
    "status": "Finalized",
    "content_hash": "e3b0c44298fc1c149afbf4c8996fb924...",
    "finalized_by": "user@example.com",
    "finalized_at": "2026-03-04T16:00:00Z",
    "total_controls": 325,
    "title": "Security Assessment Plan — SystemName — FY26 Q2"
  },
  "metadata": {
    "tool": "compliance_finalize_sap",
    "duration_ms": 800,
    "timestamp": "2026-03-04T16:00:00Z"
  }
}
```

---

## 4. `compliance_get_sap`

**Purpose**: Retrieve a specific SAP by ID or the latest SAP for a system.
**RBAC**: All roles except `Viewer`

### Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| `sap_id` | `string` | No | `null` | Specific SAP ID to retrieve |
| `system_id` | `string` | No | `null` | System ID — returns latest SAP (prefers Finalized) |

At least one of `sap_id` or `system_id` must be provided. If both are provided, `sap_id` takes precedence.

### Response — Success

```json
{
  "status": "success",
  "data": {
    "sap_id": "guid",
    "system_id": "guid",
    "assessment_id": "guid | null",
    "title": "Security Assessment Plan — SystemName — FY26 Q2",
    "status": "Finalized",
    "format": "markdown",
    "baseline_level": "Moderate",
    "total_controls": 325,
    "customer_controls": 280,
    "inherited_controls": 30,
    "shared_controls": 15,
    "stig_benchmark_count": 8,
    "controls_with_objectives": 310,
    "content": "# Security Assessment Plan...",
    "content_hash": "e3b0c44298fc...",
    "generated_at": "2026-03-04T14:30:00Z",
    "finalized_at": "2026-03-04T16:00:00Z",
    "family_summaries": [...]
  },
  "metadata": {
    "tool": "compliance_get_sap",
    "duration_ms": 350,
    "timestamp": "2026-03-04T16:30:00Z"
  }
}
```

### Response — Error

```json
{
  "status": "error",
  "errorCode": "SAP_NOT_FOUND",
  "message": "No SAP found for the specified criteria.",
  "suggestion": "Generate a SAP using compliance_generate_sap, or verify the sap_id/system_id."
}
```

---

## 5. `compliance_list_saps`

**Purpose**: List all SAPs for a system with status, dates, and scope summary.
**RBAC**: All roles except `Viewer`

### Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| `system_id` | `string` | Yes | — | System ID to list SAPs for |

### Response — Success

```json
{
  "status": "success",
  "data": {
    "system_id": "guid",
    "sap_count": 3,
    "saps": [
      {
        "sap_id": "guid-1",
        "title": "Security Assessment Plan — SystemName — FY26 Q2",
        "status": "Draft",
        "baseline_level": "Moderate",
        "total_controls": 325,
        "customer_controls": 280,
        "inherited_controls": 30,
        "shared_controls": 15,
        "generated_at": "2026-03-04T14:30:00Z",
        "finalized_at": null
      },
      {
        "sap_id": "guid-2",
        "title": "Security Assessment Plan — SystemName — FY25 Q4",
        "status": "Finalized",
        "baseline_level": "Moderate",
        "total_controls": 320,
        "customer_controls": 275,
        "inherited_controls": 30,
        "shared_controls": 15,
        "generated_at": "2025-10-01T10:00:00Z",
        "finalized_at": "2025-10-15T14:00:00Z"
      }
    ]
  },
  "metadata": {
    "tool": "compliance_list_saps",
    "duration_ms": 200,
    "timestamp": "2026-03-04T17:00:00Z"
  }
}
```

---

## Common Error Codes

| Code | HTTP-equivalent | When |
|------|----------------|------|
| `SYSTEM_NOT_FOUND` | 404 | `system_id` does not match any `RegisteredSystem` |
| `SAP_NOT_FOUND` | 404 | `sap_id` does not match any `SecurityAssessmentPlan` |
| `BASELINE_NOT_FOUND` | 422 | System has no selected `ControlBaseline` |
| `SAP_FINALIZED` | 409 | Attempted to update or re-finalize a Finalized SAP |
| `INVALID_FORMAT` | 400 | `format` is not `"markdown"`, `"docx"`, or `"pdf"` |
| `INVALID_METHOD` | 400 | Method override contains value not in `["Examine", "Interview", "Test"]` |
| `MISSING_PARAMETER` | 400 | Required parameter not provided |
| `INVALID_PARAMETER` | 400 | Parameter value is malformed (e.g., invalid ISO 8601 date) |
