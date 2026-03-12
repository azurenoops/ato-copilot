# Contracts: 026 — MCP Tool Contracts

**Date**: 2025-03-12 | **Plan**: [plan.md](../plan.md) | **Data Model**: [data-model.md](../data-model.md)

All tools follow the existing MCP JSON-RPC 2.0 protocol (`McpRequest`/`McpResponse`) and the standard tool envelope:

```json
{
  "content": [{ "type": "text", "text": "<response>" }],
  "isError": false
}
```

Errors use `McpToolResult.Error(message)`. All tools extend `BaseTool` and are registered via `RegisterTool()`.

---

## `compliance_import_nessus`

Import an ACAS/Nessus .nessus XML scan file for a registered system. Parses `NessusClientData_v2` XML, maps vulnerabilities to NIST 800-53 controls via STIG-ID xref and plugin-family heuristics, creates compliance findings, updates control effectiveness, and generates POA&M weaknesses for Critical/High/Medium findings.

### Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | yes | RegisteredSystem ID (GUID, name, or acronym — auto-resolved by `BaseTool.TryResolveSystemIdAsync`) |
| `file_content` | string | yes | Base64-encoded .nessus file content |
| `file_name` | string | yes | Original file name (e.g., `ACAS_scan_2025-03-12.nessus`) |
| `conflict_resolution` | string | no | `"skip"` (default) \| `"overwrite"` \| `"merge"` |
| `dry_run` | bool | no | Default: false. When true, preview import without persisting. |
| `assessment_id` | string | no | Existing ComplianceAssessment ID. If omitted, creates or reuses active assessment. |

### Input Schema

```json
{
  "type": "object",
  "properties": {
    "system_id": {
      "type": "string",
      "description": "RegisteredSystem ID, name, or acronym"
    },
    "file_content": {
      "type": "string",
      "description": "Base64-encoded .nessus file content"
    },
    "file_name": {
      "type": "string",
      "description": "Original file name with .nessus extension"
    },
    "conflict_resolution": {
      "type": "string",
      "enum": ["skip", "overwrite", "merge"],
      "description": "How to handle duplicate findings. Default: skip"
    },
    "dry_run": {
      "type": "boolean",
      "description": "Preview import without persisting. Default: false"
    },
    "assessment_id": {
      "type": "string",
      "description": "Existing ComplianceAssessment ID"
    }
  },
  "required": ["system_id", "file_content", "file_name"]
}
```

### Success Response

```json
{
  "status": "success",
  "data": {
    "importRecordId": "guid",
    "status": "CompletedWithWarnings",
    "reportName": "ACAS Scan - Network Segment A",
    "totalPluginResults": 1247,
    "informationalCount": 892,
    "criticalCount": 3,
    "highCount": 12,
    "mediumCount": 45,
    "lowCount": 295,
    "hostCount": 15,
    "findingsCreated": 355,
    "findingsUpdated": 0,
    "skippedCount": 0,
    "poamWeaknessesCreated": 60,
    "effectivenessRecordsCreated": 18,
    "effectivenessRecordsUpdated": 5,
    "nistControlsAffected": 12,
    "credentialedScan": true,
    "isDryRun": false,
    "warnings": [
      "Plugin family 'Custom Audit' not found in mapping table; defaulted to RA-5"
    ]
  },
  "metadata": {
    "tool": "compliance_import_nessus",
    "timestamp": "2025-03-12T14:30:00Z",
    "duration_ms": 8500
  }
}
```

### Error Response

```json
{
  "status": "error",
  "error": "Invalid .nessus file: missing <NessusClientData_v2> root element",
  "errorCode": "INVALID_NESSUS_FORMAT",
  "metadata": {
    "tool": "compliance_import_nessus",
    "timestamp": "2025-03-12T14:30:00Z",
    "duration_ms": 45
  }
}
```

### Error Codes

| Code | Condition |
|------|-----------|
| `SYSTEM_NOT_FOUND` | `system_id` does not resolve to a registered system |
| `INVALID_NESSUS_FORMAT` | File is not valid NessusClientData_v2 XML |
| `FILE_TOO_LARGE` | File exceeds 5MB limit |
| `EMPTY_SCAN` | File contains no ReportHost elements or no ReportItem elements |
| `DUPLICATE_IMPORT` | Same file hash already imported for this system (and `conflict_resolution=skip`) |
| `RMF_STEP_VIOLATION` | System is not in an RMF step that accepts scan imports |
| `RBAC_DENIED` | User does not have ISSO, SCA, or System Admin role |

### RBAC

Required roles: `ISSO`, `SCA`, `SystemAdmin` (per spec clarification C2)

### Validation

1. File extension must be `.nessus`
2. File size ≤ 5MB
3. XML must parse with `<NessusClientData_v2>` root element
4. At least one `<ReportHost>` with at least one `<ReportItem>` (severity > 0)
5. System must exist and be in an appropriate RMF step
6. File SHA-256 hash checked against prior imports for deduplication

---

## `compliance_list_nessus_imports`

List previous ACAS/Nessus scan imports for a registered system with filtering and pagination.

### Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | yes | RegisteredSystem ID, name, or acronym |
| `page` | int | no | 1-based page number (default: 1) |
| `page_size` | int | no | Results per page, max 50 (default: 20) |
| `from_date` | string | no | ISO 8601 date filter start (inclusive) |
| `to_date` | string | no | ISO 8601 date filter end (inclusive) |
| `include_dry_runs` | bool | no | Include dry-run imports (default: false) |

### Input Schema

```json
{
  "type": "object",
  "properties": {
    "system_id": {
      "type": "string",
      "description": "RegisteredSystem ID, name, or acronym"
    },
    "page": {
      "type": "integer",
      "description": "1-based page number. Default: 1",
      "minimum": 1
    },
    "page_size": {
      "type": "integer",
      "description": "Results per page, max 50. Default: 20",
      "minimum": 1,
      "maximum": 50
    },
    "from_date": {
      "type": "string",
      "description": "Filter: imports on or after this ISO 8601 date"
    },
    "to_date": {
      "type": "string",
      "description": "Filter: imports on or before this ISO 8601 date"
    },
    "include_dry_runs": {
      "type": "boolean",
      "description": "Include dry-run imports. Default: false"
    }
  },
  "required": ["system_id"]
}
```

### Success Response

```json
{
  "status": "success",
  "data": {
    "systemId": "system-guid",
    "imports": [
      {
        "importRecordId": "guid",
        "fileName": "ACAS_scan_2025-03-12.nessus",
        "importedAt": "2025-03-12T14:30:00Z",
        "importedBy": "john.doe@agency.mil",
        "status": "Completed",
        "reportName": "ACAS Scan - Network Segment A",
        "hostCount": 15,
        "totalPluginResults": 1247,
        "criticalCount": 3,
        "highCount": 12,
        "mediumCount": 45,
        "lowCount": 295,
        "informationalCount": 892,
        "findingsCreated": 355,
        "findingsUpdated": 0,
        "poamWeaknessesCreated": 60,
        "nistControlsAffected": 12,
        "credentialedScan": true,
        "isDryRun": false
      }
    ],
    "totalCount": 5,
    "page": 1,
    "pageSize": 20,
    "totalPages": 1
  },
  "metadata": {
    "tool": "compliance_list_nessus_imports",
    "timestamp": "2025-03-12T15:00:00Z",
    "duration_ms": 120
  }
}
```

### Error Codes

| Code | Condition |
|------|-----------|
| `SYSTEM_NOT_FOUND` | `system_id` does not resolve to a registered system |
| `INVALID_PAGE` | Page number exceeds total pages |
| `RBAC_DENIED` | User does not have ISSO, SCA, System Admin, ISSM, or AO role (import tool: ISSO/SCA/SystemAdmin only; list tool: ISSO/SCA/SystemAdmin/ISSM/AO allowed)
