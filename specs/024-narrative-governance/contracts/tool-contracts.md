# Tool Contracts: Narrative Governance

**Feature**: 024-narrative-governance
**Date**: 2026-03-11

All tools follow the standard response envelope: `{ status, data, metadata }`.

---

## `compliance_narrative_history`

Retrieve the full version history of a control narrative, ordered newest-first.

**RMF Step**: Implement (Phase 3)
**RBAC**: All compliance roles (Analyst, PlatformEngineer, SecurityLead, AO, SCA — read-only)

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | Yes | RegisteredSystem ID (GUID) |
| `control_id` | string | Yes | NIST 800-53 control ID (e.g., "AC-1") |
| `page` | int | No | Page number (default: 1) |
| `page_size` | int | No | Items per page (default: 50) |

**Response (success):**

```json
{
  "status": "success",
  "data": {
    "system_id": "guid",
    "control_id": "AC-1",
    "current_version": 3,
    "approval_status": "Draft",
    "versions": [
      {
        "version_number": 3,
        "content": "Updated narrative text...",
        "status": "Draft",
        "authored_by": "isso-user",
        "authored_at": "2026-03-11T14:00:00Z",
        "change_reason": "Addressed ISSM feedback"
      },
      {
        "version_number": 2,
        "content": "Previous narrative text...",
        "status": "Approved",
        "authored_by": "isso-user",
        "authored_at": "2026-03-10T10:00:00Z",
        "change_reason": null
      }
    ],
    "total_versions": 3,
    "page": 1,
    "page_size": 50
  },
  "metadata": { "tool": "compliance_narrative_history", "timestamp": "..." }
}
```

**Error Codes:**
- `SYSTEM_NOT_FOUND` — system_id does not exist
- `CONTROL_NOT_FOUND` — no narrative exists for this control

---

## `compliance_narrative_diff`

Compare two versions of a control narrative, returning a line-level unified diff.

**RMF Step**: Implement (Phase 3)
**RBAC**: All compliance roles (read-only)

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | Yes | RegisteredSystem ID (GUID) |
| `control_id` | string | Yes | NIST 800-53 control ID |
| `from_version` | int | Yes | Base version number |
| `to_version` | int | Yes | Target version number |

**Response (success):**

```json
{
  "status": "success",
  "data": {
    "system_id": "guid",
    "control_id": "AC-1",
    "from_version": 1,
    "to_version": 3,
    "diff": "--- Version 1\n+++ Version 3\n@@ -1,3 +1,4 @@\n Access control policies...\n-Old implementation detail\n+Updated implementation detail\n+Additional configuration step\n Monitoring enabled.",
    "from_authored_by": "isso-user",
    "from_authored_at": "2026-03-01T10:00:00Z",
    "to_authored_by": "isso-user",
    "to_authored_at": "2026-03-11T14:00:00Z"
  },
  "metadata": { "tool": "compliance_narrative_diff", "timestamp": "..." }
}
```

**Error Codes:**
- `SYSTEM_NOT_FOUND` — system_id does not exist
- `CONTROL_NOT_FOUND` — no narrative exists for this control
- `VERSION_NOT_FOUND` — from_version or to_version does not exist

---

## `compliance_rollback_narrative`

Create a new version with the content of a specified prior version (copy-forward rollback). Does not delete any versions.

**RMF Step**: Implement (Phase 3)
**RBAC**: Compliance.Analyst (ISSO), Compliance.PlatformEngineer (Engineer)

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | Yes | RegisteredSystem ID (GUID) |
| `control_id` | string | Yes | NIST 800-53 control ID |
| `target_version` | int | Yes | Version number to roll back to |
| `change_reason` | string | No | Reason for rollback |

**Response (success):**

```json
{
  "status": "success",
  "data": {
    "system_id": "guid",
    "control_id": "AC-1",
    "rolled_back_from": 3,
    "rolled_back_to": 1,
    "new_version_number": 4,
    "status": "Draft",
    "authored_by": "isso-user",
    "authored_at": "2026-03-11T15:00:00Z",
    "change_reason": "Rolled back to version 1: Reverted incorrect update"
  },
  "metadata": { "tool": "compliance_rollback_narrative", "timestamp": "..." }
}
```

**Error Codes:**
- `SYSTEM_NOT_FOUND` — system_id does not exist
- `CONTROL_NOT_FOUND` — no narrative exists for this control
- `VERSION_NOT_FOUND` — target_version does not exist
- `UNDER_REVIEW` — narrative is currently in UnderReview status; cannot modify until review completes

---

## `compliance_submit_narrative`

Submit a Draft narrative for ISSM review. Transitions status from Draft to InReview.

**RMF Step**: Implement (Phase 3)
**RBAC**: Compliance.Analyst (ISSO), Compliance.PlatformEngineer (Engineer)

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | Yes | RegisteredSystem ID (GUID) |
| `control_id` | string | Yes | NIST 800-53 control ID |

**Response (success):**

```json
{
  "status": "success",
  "data": {
    "system_id": "guid",
    "control_id": "AC-1",
    "version_number": 3,
    "previous_status": "Draft",
    "new_status": "InReview",
    "submitted_by": "isso-user",
    "submitted_at": "2026-03-11T15:30:00Z"
  },
  "metadata": { "tool": "compliance_submit_narrative", "timestamp": "..." }
}
```

**Error Codes:**
- `SYSTEM_NOT_FOUND` — system_id does not exist
- `CONTROL_NOT_FOUND` — no narrative exists for this control
- `INVALID_STATUS` — narrative is not in Draft status (only Draft can be submitted)

---

## `compliance_review_narrative`

Approve or request revision of a narrative in InReview status. ISSM-only.

**RMF Step**: Implement (Phase 3)
**RBAC**: Compliance.SecurityLead (ISSM)

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | Yes | RegisteredSystem ID (GUID) |
| `control_id` | string | Yes | NIST 800-53 control ID |
| `decision` | string | Yes | `approve` or `request_revision` |
| `comments` | string | No | Reviewer comments (required when decision is `request_revision`) |

**Response (success):**

```json
{
  "status": "success",
  "data": {
    "system_id": "guid",
    "control_id": "AC-1",
    "version_number": 3,
    "decision": "approve",
    "previous_status": "InReview",
    "new_status": "Approved",
    "reviewed_by": "issm-user",
    "reviewed_at": "2026-03-11T16:00:00Z",
    "comments": null
  },
  "metadata": { "tool": "compliance_review_narrative", "timestamp": "..." }
}
```

**Error Codes:**
- `SYSTEM_NOT_FOUND` — system_id does not exist
- `CONTROL_NOT_FOUND` — no narrative exists for this control
- `INVALID_STATUS` — narrative is not in InReview status
- `COMMENTS_REQUIRED` — decision is `request_revision` but no comments provided

---

## `compliance_batch_submit_narratives`

Submit all Draft narratives for a control family (or all families) for ISSM review in a single operation.

**RMF Step**: Implement (Phase 3)
**RBAC**: Compliance.Analyst (ISSO), Compliance.PlatformEngineer (Engineer)

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | Yes | RegisteredSystem ID (GUID) |
| `family_filter` | string | No | Control family prefix (e.g., "AC", "SI"). If omitted, submits all Draft narratives |

**Response (success):**

```json
{
  "status": "success",
  "data": {
    "system_id": "guid",
    "family_filter": "AC",
    "submitted_count": 8,
    "skipped_count": 2,
    "skipped_reason": "Already in InReview or Approved status",
    "submitted_controls": ["AC-1", "AC-2", "AC-3", "AC-4", "AC-5", "AC-6", "AC-7", "AC-8"],
    "skipped_controls": ["AC-9", "AC-10"],
    "submitted_by": "isso-user",
    "submitted_at": "2026-03-11T15:45:00Z"
  },
  "metadata": { "tool": "compliance_batch_submit_narratives", "timestamp": "..." }
}
```

**Error Codes:**
- `SYSTEM_NOT_FOUND` — system_id does not exist
- `NO_DRAFT_NARRATIVES` — no Draft narratives found matching the filter

---

## `compliance_batch_review_narratives`

Batch approve or request revision of narratives for a control family or set of control IDs. ISSM-only.

**RMF Step**: Implement (Phase 3)
**RBAC**: Compliance.SecurityLead (ISSM)

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | Yes | RegisteredSystem ID (GUID) |
| `decision` | string | Yes | `approve` or `request_revision` |
| `comments` | string | No | Reviewer comments (required when decision is `request_revision`) |
| `family_filter` | string | No | Control family prefix (e.g., "AC"). Mutually exclusive with `control_ids` |
| `control_ids` | string[] | No | Specific control IDs to review. Mutually exclusive with `family_filter` |

**Response (success):**

```json
{
  "status": "success",
  "data": {
    "system_id": "guid",
    "decision": "approve",
    "reviewed_count": 8,
    "skipped_count": 1,
    "skipped_reason": "Not in InReview status",
    "reviewed_controls": ["AC-1", "AC-2", "AC-3", "AC-4", "AC-5", "AC-6", "AC-7", "AC-8"],
    "skipped_controls": ["AC-9"],
    "reviewed_by": "issm-user",
    "reviewed_at": "2026-03-11T16:30:00Z",
    "comments": null
  },
  "metadata": { "tool": "compliance_batch_review_narratives", "timestamp": "..." }
}
```

**Error Codes:**
- `SYSTEM_NOT_FOUND` — system_id does not exist
- `NO_REVIEWABLE_NARRATIVES` — no InReview narratives found matching the filter
- `COMMENTS_REQUIRED` — decision is `request_revision` but no comments provided
- `MUTUALLY_EXCLUSIVE_FILTERS` — both `family_filter` and `control_ids` provided

---

## `compliance_narrative_approval_progress`

Return aggregate approval status counts, overall approval percentage, and per-family breakdown for a system.

**RMF Step**: Implement (Phase 3)
**RBAC**: All compliance roles (read-only)

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | Yes | RegisteredSystem ID (GUID) |
| `family_filter` | string | No | Control family prefix to filter results |

**Response (success):**

```json
{
  "status": "success",
  "data": {
    "system_id": "guid",
    "overall": {
      "total_controls": 325,
      "approved": 200,
      "draft": 80,
      "in_review": 30,
      "needs_revision": 10,
      "missing": 5,
      "approval_percentage": 61.5
    },
    "families": [
      {
        "family": "AC",
        "total": 25,
        "approved": 20,
        "draft": 3,
        "in_review": 2,
        "needs_revision": 0,
        "missing": 0
      }
    ],
    "review_queue": ["AC-3", "AC-4", "SI-2", "SI-4"],
    "staleness_warnings": [
      {
        "control_id": "AC-1",
        "message": "Unapproved Draft version exists under Approved SSP §10"
      }
    ]
  },
  "metadata": { "tool": "compliance_narrative_approval_progress", "timestamp": "..." }
}
```

**Error Codes:**
- `SYSTEM_NOT_FOUND` — system_id does not exist

---

## Enhanced Existing Tool: `compliance_write_narrative`

The existing tool gains two new optional parameters and version-creating behavior.

**New Parameters (additive):**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `expected_version` | int | No | Optimistic concurrency check — rejected if current version differs |
| `change_reason` | string | No | Reason for the edit (stored on the NarrativeVersion record) |

**Enhanced Response (adds to existing fields):**

```json
{
  "status": "success",
  "data": {
    "...existing fields...",
    "version_number": 2,
    "approval_status": "Draft",
    "previous_version": 1
  },
  "metadata": { "tool": "compliance_write_narrative", "timestamp": "..." }
}
```

**New Error Code:**
- `CONCURRENCY_CONFLICT` — `expected_version` does not match current version. Response includes `current_version`, `last_modified_by`, `last_modified_at`.
- `UNDER_REVIEW` — narrative is currently in InReview status; cannot modify until review completes.
