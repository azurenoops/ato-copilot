# Contracts: 017 — MCP Tool Contracts

**Date**: 2026-03-01 | **Plan**: [plan.md](plan.md) | **Data Model**: [data-model.md](../data-model.md)

All tools follow the existing MCP JSON-RPC 2.0 protocol (`McpRequest`/`McpResponse`) and the standard tool envelope:

```json
{
  "content": [{ "type": "text", "text": "<response>" }],
  "isError": false
}
```

Errors use `McpToolResult.Error(message)`. All tools extend `BaseTool` and are registered via `RegisterTool()`.

---

## Phase 1 — CKL Import

### `compliance_import_ckl`

Import a DISA STIG Viewer checklist (.ckl) file for a registered system.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | yes | RegisteredSystem ID |
| `file_content` | string | yes | Base64-encoded .ckl file content |
| `file_name` | string | yes | Original file name (e.g., `Windows_Server_2022_STIG_V1R1.ckl`) |
| `conflict_resolution` | string | no | `"skip"` (default) \| `"overwrite"` \| `"merge"` |
| `dry_run` | bool | no | Default: false. When true, preview import without persisting. |
| `assessment_id` | string | no | Existing ComplianceAssessment ID. If omitted, creates or reuses active assessment. |

**Returns**: Import summary JSON:

```json
{
  "importRecordId": "guid",
  "status": "CompletedWithWarnings",
  "benchmarkId": "Windows_Server_2022_STIG",
  "benchmarkTitle": "Microsoft Windows Server 2022 Security Technical Implementation Guide",
  "targetHostName": "web-server-01",
  "totalEntries": 284,
  "openCount": 12,
  "passCount": 245,
  "notApplicableCount": 22,
  "notReviewedCount": 5,
  "skippedCount": 0,
  "unmatchedCount": 3,
  "findingsCreated": 12,
  "findingsUpdated": 0,
  "effectivenessRecordsCreated": 8,
  "effectivenessRecordsUpdated": 0,
  "nistControlsAffected": 8,
  "warnings": [
    "3 STIG rules not found in curated library: V-999001, V-999002, V-999003",
    "2 NIST controls resolved but not in system baseline: SI-16, SC-28(1)"
  ],
  "unmatchedRules": [
    { "vulnId": "V-999001", "ruleId": null, "ruleTitle": "Custom local rule", "severity": "medium" }
  ],
  "isDryRun": false
}
```

**RBAC**: `Compliance.SecurityLead`, `Compliance.Analyst`, `Compliance.Administrator`

**Validation**:
- System must exist and be active
- File content must be valid base64
- Decoded file must be ≤ 5MB
- XML must parse as valid CKL (contains `<CHECKLIST>` root)
- System should be in RMF step Assess or later (warning if not, still allowed)
- Duplicate file detection: if `FileHash` + `RegisteredSystemId` matches an existing import, add warning "File previously imported on {date} (import ID: {id})" — import still proceeds

**Error cases**:
- `system_id` not found → `"System '{system_id}' not found"`
- Invalid base64 → `"Invalid base64 encoding in file_content"`
- File too large → `"File exceeds 5MB limit (actual: {size}MB). Consider splitting by benchmark."`
- Invalid XML → `"Failed to parse CKL file: {xml_error}"`
- No VULN entries → `"CKL file contains no VULN entries"`

---

## Phase 2 — XCCDF Import

### `compliance_import_xccdf`

Import SCAP Compliance Checker XCCDF results (.xccdf) file for a registered system.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | yes | RegisteredSystem ID |
| `file_content` | string | yes | Base64-encoded .xccdf file content |
| `file_name` | string | yes | Original file name |
| `conflict_resolution` | string | no | `"skip"` (default) \| `"overwrite"` \| `"merge"` |
| `dry_run` | bool | no | Default: false |
| `assessment_id` | string | no | Existing ComplianceAssessment ID |

**Returns**: Same `ImportResult` format as CKL import, plus:

```json
{
  "xccdfScore": 72.5,
  "scanStartTime": "2026-02-15T14:30:00Z",
  "scanEndTime": "2026-02-15T14:45:22Z"
}
```

**RBAC**: `Compliance.SecurityLead`, `Compliance.Analyst`, `Compliance.Administrator`

**Validation**: Same as CKL import, plus:
- XML must contain `<TestResult>` element (XCCDF 1.1 or 1.2 namespace)

**Error cases**: Same as CKL import, plus:
- No TestResult element → `"XCCDF file does not contain a TestResult element"`

---

## Phase 3 — CKL Export

### `compliance_export_ckl`

Generate a DISA STIG Viewer checklist (.ckl) file from assessment data.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | yes | RegisteredSystem ID |
| `benchmark_id` | string | yes | STIG Benchmark ID (e.g., `Windows_Server_2022_STIG`) |
| `assessment_id` | string | no | Specific assessment. Default: most recent. |

**Returns**: Export result JSON:

```json
{
  "fileName": "Windows_Server_2022_STIG_V1R1_ACME_Portal.ckl",
  "fileContent": "<base64-encoded-ckl-xml>",
  "fileSizeBytes": 245760,
  "totalVulns": 284,
  "openCount": 8,
  "notAFindingCount": 249,
  "notApplicableCount": 22,
  "notReviewedCount": 5,
  "generatedAt": "2026-03-01T15:00:00Z"
}
```

**RBAC**: `Compliance.SecurityLead`, `Compliance.Analyst`, `Compliance.PlatformEngineer`, `Compliance.Administrator`

**Validation**:
- System must exist
- Benchmark must exist in `StigControl` library (at least one rule with matching `BenchmarkId`)
- Assessment data is optional — rules without matching `ComplianceFinding` records are exported as `Not_Reviewed`

**Error cases**:
- Benchmark not found in StigControl library → `"No STIG benchmark '{benchmark_id}' found in curated library"`
- System not found → standard error

---

## Phase 4 — Import Management

### `compliance_list_imports`

List import history for a registered system.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | yes | RegisteredSystem ID |
| `page` | int | no | 1-based page number. Default: 1 |
| `page_size` | int | no | Default: 20, max: 100 |
| `benchmark_id` | string | no | Filter by STIG benchmark |
| `import_type` | string | no | `"ckl"` \| `"xccdf"` to filter by type |
| `from_date` | string | no | ISO 8601 date. Only imports on or after this date. |
| `to_date` | string | no | ISO 8601 date. Only imports on or before this date. |
| `include_dry_runs` | bool | no | Default: false |

**Returns**: Paginated import records:

```json
{
  "systemId": "guid",
  "systemName": "ACME Portal",
  "totalImports": 12,
  "page": 1,
  "pageSize": 20,
  "imports": [
    {
      "importId": "guid",
      "importType": "Ckl",
      "fileName": "Windows_Server_2022_STIG_V1R1.ckl",
      "benchmarkId": "Windows_Server_2022_STIG",
      "benchmarkTitle": "Microsoft Windows Server 2022 STIG",
      "targetHostName": "web-server-01",
      "status": "CompletedWithWarnings",
      "totalEntries": 284,
      "openCount": 12,
      "passCount": 245,
      "findingsCreated": 12,
      "findingsUpdated": 0,
      "unmatchedCount": 3,
      "conflictResolution": "Skip",
      "isDryRun": false,
      "importedBy": "john.doe@example.mil",
      "importedAt": "2026-03-01T14:30:00Z"
    }
  ]
}
```

**RBAC**: `Compliance.SecurityLead`, `Compliance.Analyst`, `Compliance.Auditor`, `Compliance.Administrator`

---

### `compliance_get_import_summary`

Get detailed breakdown of a specific import operation.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `import_id` | string | yes | ScanImportRecord ID |

**Returns**: Full import details with per-finding breakdown:

```json
{
  "importRecord": {
    "id": "guid",
    "importType": "Ckl",
    "fileName": "Windows_Server_2022_STIG_V1R1.ckl",
    "fileHash": "sha256:abc123...",
    "benchmarkId": "Windows_Server_2022_STIG",
    "status": "CompletedWithWarnings",
    "totalEntries": 284,
    "openCount": 12,
    "passCount": 245,
    "notApplicableCount": 22,
    "notReviewedCount": 5,
    "unmatchedCount": 3,
    "findingsCreated": 12,
    "findingsUpdated": 0,
    "effectivenessRecordsCreated": 8,
    "nistControlsAffected": 8,
    "importedBy": "john.doe@example.mil",
    "importedAt": "2026-03-01T14:30:00Z",
    "warnings": ["..."]
  },
  "nistControlBreakdown": [
    {
      "controlId": "AC-2",
      "controlTitle": "Account Management",
      "stigRulesMatched": 4,
      "openFindings": 1,
      "passedFindings": 3,
      "effectiveness": "OtherThanSatisfied"
    }
  ],
  "unmatchedRules": [
    { "vulnId": "V-999001", "ruleTitle": "Custom rule", "severity": "medium" }
  ],
  "findings": [
    {
      "vulnId": "V-254239",
      "ruleId": "SV-254239r849090_rule",
      "rawStatus": "Open",
      "severity": "high",
      "catSeverity": "CatI",
      "action": "Created",
      "nistControls": ["AU-3", "AU-3(1)"],
      "findingDetails": "Audit policy not configured..."
    }
  ]
}
```

**RBAC**: `Compliance.SecurityLead`, `Compliance.Analyst`, `Compliance.Auditor`, `Compliance.Administrator`

**Error cases**:
- Import not found → `"Import record '{import_id}' not found"`

---

## RBAC Summary

| Tool | Administrator | SecurityLead | Auditor | Analyst | PlatformEngineer | Viewer |
|------|:---:|:---:|:---:|:---:|:---:|:---:|
| `compliance_import_ckl` | ✅ | ✅ | ❌ | ✅ | ❌ | ❌ |
| `compliance_import_xccdf` | ✅ | ✅ | ❌ | ✅ | ❌ | ❌ |
| `compliance_export_ckl` | ✅ | ✅ | ❌ | ✅ | ✅ | ❌ |
| `compliance_list_imports` | ✅ | ✅ | ✅ | ✅ | ❌ | ❌ |
| `compliance_get_import_summary` | ✅ | ✅ | ✅ | ✅ | ❌ | ❌ |

**Rationale**:
- Import (write) requires SecurityLead/Analyst/Admin — assessors who own the scan data
- Export (read+generate) includes PlatformEngineer — engineers need CKL files for eMASS
- List/Summary (read) includes Auditor — SCAs reviewing assessment evidence
- Viewer role excluded from all tools — import/export history is operational, not informational
