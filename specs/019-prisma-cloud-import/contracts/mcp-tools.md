# Contracts: 019 — MCP Tool Contracts

**Date**: 2026-03-05 | **Plan**: [plan.md](../plan.md) | **Data Model**: [data-model.md](../data-model.md)

All tools follow the existing MCP JSON-RPC 2.0 protocol (`McpRequest`/`McpResponse`) and the standard tool envelope:

```json
{
  "content": [{ "type": "text", "text": "<response>" }],
  "isError": false
}
```

Errors use `McpToolResult.Error(message)`. All tools extend `BaseTool` and are registered via `RegisterTool()`.

---

## Phase 1 — Prisma CSV Import

### `compliance_import_prisma`

Import a Prisma Cloud compliance CSV export file for a registered system. Supports multi-subscription CSV auto-splitting.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `file_content` | string | yes | Base64-encoded Prisma Cloud CSV export file |
| `file_name` | string | yes | Original file name (e.g., `prisma-alerts-2026-03-05.csv`) |
| `system_id` | string | no | RegisteredSystem ID. If omitted, auto-resolved from Azure subscription IDs in the CSV via `ISystemSubscriptionResolver`. If provided, all alerts are assigned to this system regardless of cloud type. |
| `conflict_resolution` | string | no | `"skip"` (default) \| `"overwrite"` \| `"merge"` |
| `dry_run` | bool | no | Default: false. When true, preview import without persisting. |
| `assessment_id` | string | no | Existing ComplianceAssessment ID. If omitted, creates or reuses active assessment. |

**Returns**: Import summary JSON (one per resolved system for multi-subscription CSVs):

```json
{
  "imports": [
    {
      "importRecordId": "guid",
      "systemId": "system-guid",
      "systemName": "ACME Portal",
      "status": "CompletedWithWarnings",
      "totalAlerts": 47,
      "openCount": 32,
      "resolvedCount": 12,
      "dismissedCount": 3,
      "snoozedCount": 0,
      "findingsCreated": 32,
      "findingsUpdated": 0,
      "skippedCount": 0,
      "unmappedPolicies": 5,
      "effectivenessRecordsCreated": 18,
      "effectivenessRecordsUpdated": 4,
      "nistControlsAffected": 22,
      "evidenceCreated": true,
      "fileHash": "sha256:abc123...",
      "isDryRun": false,
      "warnings": [
        "5 policies have no NIST 800-53 mapping (findings created as unmapped)",
        "2 NIST controls resolved but not in system baseline: SI-16, SC-28(1)"
      ]
    }
  ],
  "unresolvedSubscriptions": [
    {
      "accountId": "d4e5f6g7-...",
      "accountName": "FS-Azure-Dev",
      "alertCount": 15,
      "message": "Subscription d4e5f6g7-... (FS-Azure-Dev) has 15 alerts but is not registered. Use compliance_register_system to register it, then re-import."
    }
  ],
  "skippedNonAzure": {
    "count": 8,
    "cloudTypes": ["aws", "gcp"],
    "message": "8 alerts from aws, gcp skipped — provide explicit system_id to import non-Azure alerts."
  },
  "totalProcessed": 47,
  "totalSkipped": 23,
  "duration_ms": 2340
}
```

**RBAC**: `Compliance.SecurityLead`, `Compliance.Analyst`, `Compliance.Administrator`

**Validation**:
- File content must be valid base64
- Decoded file must be ≤ 25MB
- CSV must have a valid header row containing `Alert ID`, `Status`, `Policy Name`, `Severity` columns
- If `system_id` provided, system must exist and be active
- If `system_id` omitted, at least one subscription must resolve to a registered system
- Duplicate file detection: if `FileHash` + `RegisteredSystemId` matches an existing import, add warning

**Error cases**:
- `system_id` not found → `"System '{system_id}' not found"`
- Invalid base64 → `"Invalid base64 encoding in file_content"`
- File too large → `"File exceeds 25MB limit (actual: {size}MB). Consider filtering by subscription or policy type before export."`
- Invalid CSV header → `"CSV header missing required columns: {missing_columns}"`
- No alerts after parsing → `"CSV contains no alert rows"`
- No subscriptions resolved and no `system_id` → `"No Azure subscriptions could be resolved to registered systems. Provide explicit system_id or register systems first."`

---

## Phase 2 — Prisma API JSON Import

### `compliance_import_prisma_api`

Import Prisma Cloud API JSON (RQL alert response) for a registered system. Includes enhanced remediation context, alert history, and policy metadata.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `file_content` | string | yes | Base64-encoded JSON file (single alert object or array of alert objects) |
| `file_name` | string | yes | Original file name (e.g., `prisma-api-alerts.json`) |
| `system_id` | string | no | RegisteredSystem ID. Same resolution logic as CSV import. |
| `conflict_resolution` | string | no | `"skip"` (default) \| `"overwrite"` \| `"merge"` |
| `dry_run` | bool | no | Default: false |
| `assessment_id` | string | no | Existing ComplianceAssessment ID |

**Returns**: Same `ImportResult` format as CSV import, plus enhanced fields:

```json
{
  "imports": [
    {
      "importRecordId": "guid",
      "systemId": "system-guid",
      "status": "Completed",
      "totalAlerts": 47,
      "remediableCount": 28,
      "alertsWithHistory": 47,
      "cliScriptsExtracted": 22,
      "policyLabelsFound": ["CSPM", "Azure", "Storage", "IAM"],
      "...": "same fields as CSV import"
    }
  ],
  "duration_ms": 1850
}
```

**RBAC**: `Compliance.SecurityLead`, `Compliance.Analyst`, `Compliance.Administrator`

**Validation**: Same as CSV import, except:
- JSON must parse as valid JSON (single object or array)
- Each alert object must have `id`, `status`, and `policy` fields
- `policy.complianceMetadata` must be present (can be empty array)

**Error cases**: Same as CSV import, plus:
- Invalid JSON → `"Failed to parse JSON: {json_error}"`
- Missing required alert fields → `"Alert object missing required field: {field}"`

---

## Phase 3 — Policy Catalog & Trend Analysis

### `compliance_list_prisma_policies`

List unique Prisma policies observed across imports for a system, with NIST control mappings and finding counts.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | yes | RegisteredSystem ID |

**Returns**:

```json
{
  "systemId": "system-guid",
  "totalPolicies": 35,
  "policies": [
    {
      "policyName": "Azure Storage account should use customer-managed key for encryption",
      "policyType": "config",
      "severity": "high",
      "nistControlIds": ["SC-28", "SC-12"],
      "openCount": 3,
      "resolvedCount": 7,
      "dismissedCount": 1,
      "affectedResourceTypes": ["Microsoft.Storage/storageAccounts"],
      "lastSeenImportId": "import-guid",
      "lastSeenAt": "2026-03-05T10:30:00Z"
    }
  ]
}
```

**RBAC**: `Compliance.SecurityLead`, `Compliance.Analyst`, `Compliance.Assessor`, `Compliance.Administrator`

**Error cases**:
- `system_id` not found → `"System '{system_id}' not found"`
- No Prisma imports for system → returns empty `policies` array (not an error)

---

### `compliance_prisma_trend`

Compare Prisma findings across scan imports to show remediation progress, new findings, and compliance drift.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | yes | RegisteredSystem ID |
| `import_ids` | string[] | no | Specific import IDs to compare. If omitted, uses last 2 Prisma imports for the system. |
| `group_by` | string | no | Group results by `"resource_type"` or `"nist_control"`. If omitted, returns aggregate totals only. |

**Returns**:

```json
{
  "systemId": "system-guid",
  "imports": [
    {
      "importId": "older-import-guid",
      "importedAt": "2026-02-15T10:00:00Z",
      "fileName": "prisma-feb.csv",
      "totalAlerts": 55,
      "openCount": 40,
      "resolvedCount": 10,
      "dismissedCount": 5
    },
    {
      "importId": "newer-import-guid",
      "importedAt": "2026-03-05T10:00:00Z",
      "fileName": "prisma-mar.csv",
      "totalAlerts": 47,
      "openCount": 32,
      "resolvedCount": 12,
      "dismissedCount": 3
    }
  ],
  "newFindings": 8,
  "resolvedFindings": 16,
  "persistentFindings": 31,
  "remediationRate": 34.04,
  "nistControlBreakdown": {
    "SC-28": 5,
    "AC-2": 3,
    "IA-5": 2
  },
  "resourceTypeBreakdown": {
    "Microsoft.Storage/storageAccounts": 12,
    "Microsoft.Sql/servers": 8,
    "Microsoft.KeyVault/vaults": 3
  }
}
```

**RBAC**: `Compliance.SecurityLead`, `Compliance.Analyst`, `Compliance.Assessor`, `Compliance.Administrator`

**Validation**:
- System must exist
- If `import_ids` provided, they must reference valid Prisma imports for the system
- If `import_ids` omitted and system has <2 Prisma imports, returns snapshot of latest import only (no comparison)

**Error cases**:
- `system_id` not found → `"System '{system_id}' not found"`
- `import_ids` referencing non-Prisma imports → `"Import '{id}' is not a Prisma import"`
- Single import (no comparison possible) → returns snapshot with `newFindings = totalAlerts`, `resolvedFindings = 0`, `persistentFindings = 0`
- No Prisma imports for system → `"No Prisma imports found for system '{system_id}'"`
