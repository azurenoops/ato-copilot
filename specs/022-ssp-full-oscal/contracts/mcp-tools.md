# MCP Tool Contracts: Feature 022 — SSP 800-18 Full Sections + OSCAL Output

**Date**: 2026-03-10
**Pattern**: All tools extend `BaseTool`. Responses use standard envelope: `{ status, data, metadata }`.

---

## Tool 1: compliance_write_ssp_section

**Purpose**: Author or update a specific SSP section by number.
**RBAC**: `PimTier.Write`

### Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | ✅ | System GUID, name, or acronym (auto-resolved by `BaseTool.TryResolveSystemIdAsync`) |
| `section_number` | int | ✅ | NIST 800-18 section number (1–13) |
| `content` | string | For authored sections | Markdown content for authored/hybrid sections |
| `authored_by` | string | ✅ | User identity writing the section |
| `expected_version` | int | For updates | Required when updating existing section (concurrency check). Omit for first write. |
| `submit_for_review` | bool | No (default: false) | If true, transitions the section from Draft to UnderReview after writing. Only valid when section is in Draft status. |

### Behavior

- **Auto-generated sections** (§1,§2,§3,§4,§7,§9,§10,§11): Regenerates content from entity data. If `content` is provided, sets `HasManualOverride = true` and stores provided content instead.
- **Authored sections** (§5,§8,§12,§13): Stores provided `content` as markdown.
- **Hybrid section** (§6): Auto-populates hosting environment data, merges with provided `content`.
- First write creates the `SspSection` record with Version=1, Status=Draft.
- Subsequent writes increment Version, reset Status to Draft (even if previously Approved).
- If `submit_for_review=true`, transitions Draft → UnderReview after writing. Returns error if section is not in Draft status.
- Concurrent writes rejected if `expected_version` doesn't match stored Version.

### Success Response

```json
{
  "status": "success",
  "data": {
    "section_number": 6,
    "section_title": "System Environment",
    "status": "Draft",
    "is_auto_generated": false,
    "has_manual_override": false,
    "version": 2,
    "authored_by": "john.doe@agency.gov",
    "authored_at": "2026-03-10T14:30:00Z",
    "word_count": 342
  },
  "metadata": {
    "tool": "compliance_write_ssp_section",
    "duration_ms": 1250,
    "timestamp": "2026-03-10T14:30:01Z"
  }
}
```

### Error Responses

| errorCode | Condition | suggestion |
|-----------|-----------|------------|
| `SYSTEM_NOT_FOUND` | system_id does not resolve | "Provide a valid system GUID, name, or acronym" |
| `INVALID_SECTION_NUMBER` | section_number not in 1–13 | "Section number must be between 1 and 13" |
| `CONTENT_REQUIRED` | Authored section without content | "Provide content for authored section §{n}" |
| `CONCURRENCY_CONFLICT` | expected_version doesn't match | "Section was modified by another user. Current version: {v}. Reload and retry." |
| `INVALID_STATUS_FOR_SUBMIT` | submit_for_review=true but section not in Draft | "Section §{n} must be in Draft status to submit for review. Current status: '{status}'." |

---

## Tool 2: compliance_review_ssp_section

**Purpose**: Approve or request revision of an SSP section.
**RBAC**: `PimTier.Write`

### Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | ✅ | System GUID, name, or acronym |
| `section_number` | int | ✅ | NIST 800-18 section number (1–13) |
| `decision` | string | ✅ | `"approve"` or `"request_revision"` |
| `reviewer` | string | ✅ | Reviewer identity |
| `comments` | string | If rejecting | Required when decision is `request_revision` |

### Behavior

- **Approve**: Sets Status = Approved, records ReviewedBy + ReviewedAt.
- **Request Revision**: Sets Status = Draft, records ReviewedBy + ReviewedAt + ReviewerComments.
- Section must be in `UnderReview` status to be reviewed. Other statuses return error.
- Validates strict lifecycle: only `UnderReview → Approved` or `UnderReview → Draft` allowed.

### Success Response

```json
{
  "status": "success",
  "data": {
    "section_number": 12,
    "section_title": "Personnel Security",
    "status": "Approved",
    "reviewed_by": "jane.issm@agency.gov",
    "reviewed_at": "2026-03-10T15:00:00Z",
    "reviewer_comments": null
  },
  "metadata": {
    "tool": "compliance_review_ssp_section",
    "duration_ms": 800,
    "timestamp": "2026-03-10T15:00:01Z"
  }
}
```

### Error Responses

| errorCode | Condition | suggestion |
|-----------|-----------|------------|
| `SECTION_NOT_FOUND` | No SspSection exists for this system/number | "Section §{n} has not been authored yet. Use compliance_write_ssp_section first." |
| `INVALID_STATUS_FOR_REVIEW` | Section not in UnderReview status | "Section §{n} is currently '{status}'. Submit for review before approving." |
| `COMMENTS_REQUIRED` | request_revision without comments | "Provide reviewer comments explaining required changes" |
| `INVALID_DECISION` | decision not approve/request_revision | "Decision must be 'approve' or 'request_revision'" |

---

## Tool 3: compliance_ssp_completeness

**Purpose**: Get per-section completion status with overall readiness percentage.
**RBAC**: `PimTier.Read`

### Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | ✅ | System GUID, name, or acronym |

### Success Response

```json
{
  "status": "success",
  "data": {
    "system_name": "ACME Portal",
    "overall_readiness_percent": 61.5,
    "approved_count": 8,
    "total_sections": 13,
    "sections": [
      {
        "section_number": 1,
        "section_title": "System Identification",
        "status": "Approved",
        "is_auto_generated": true,
        "has_manual_override": false,
        "authored_by": "system",
        "authored_at": "2026-03-10T10:00:00Z",
        "word_count": 245,
        "version": 1
      },
      {
        "section_number": 5,
        "section_title": "General Description / Purpose",
        "status": "NotStarted",
        "is_auto_generated": false,
        "has_manual_override": false,
        "authored_by": null,
        "authored_at": null,
        "word_count": 0,
        "version": 0
      }
    ],
    "blocking_issues": [
      "§5 (General Description / Purpose): Not started — requires ISSO authoring",
      "§8 (Related Laws / Regulations / Policies): Not started — requires ISSO authoring",
      "§12 (Personnel Security): Draft — pending review",
      "§13 (Contingency Plan): Not started — requires ISSO authoring",
      "§6 (System Environment): Draft — pending review"
    ]
  },
  "metadata": {
    "tool": "compliance_ssp_completeness",
    "duration_ms": 450,
    "timestamp": "2026-03-10T15:30:00Z"
  }
}
```

---

## Tool 4: compliance_export_oscal_ssp

**Purpose**: Export OSCAL 1.1.2 SSP JSON for FedRAMP/eMASS submission.
**RBAC**: `PimTier.Read`

### Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | ✅ | System GUID, name, or acronym |
| `include_back_matter` | bool | No (default: true) | Whether to include back-matter resources |
| `pretty_print` | bool | No (default: true) | Whether to format JSON with indentation |

### Behavior

- Generates complete OSCAL SSP JSON from entity data.
- Never blocked by missing data — missing sections produce schema-valid placeholders.
- Returns warnings listing any gaps in the data.
- JSON uses kebab-case property naming per OSCAL convention.

### Success Response

```json
{
  "status": "success",
  "data": {
    "oscal_ssp_json": "{ \"system-security-plan\": { ... } }",
    "warnings": [
      "No ControlBaseline found — import-profile section omitted",
      "§5 (General Description / Purpose) not authored — description placeholder used"
    ],
    "statistics": {
      "control_count": 325,
      "component_count": 12,
      "inventory_item_count": 12,
      "user_count": 6,
      "back_matter_resource_count": 4
    }
  },
  "metadata": {
    "tool": "compliance_export_oscal_ssp",
    "duration_ms": 8500,
    "timestamp": "2026-03-10T16:00:00Z"
  }
}
```

---

## Tool 5: compliance_validate_oscal_ssp

**Purpose**: Validate OSCAL SSP structural correctness before submission.
**RBAC**: `PimTier.Read`

### Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | ✅ | System GUID, name, or acronym |

### Behavior

- Generates the OSCAL SSP JSON (via `OscalSspExportService`), then validates it (via `OscalValidationService`).
- Does not require the user to provide raw JSON.
- Returns validation result with errors, warnings, and statistics.

### Success Response (Valid)

```json
{
  "status": "success",
  "data": {
    "is_valid": true,
    "errors": [],
    "warnings": [
      "back-matter has no contingency plan reference"
    ],
    "statistics": {
      "control_count": 325,
      "component_count": 12,
      "inventory_item_count": 12,
      "user_count": 6,
      "back_matter_resource_count": 4
    }
  },
  "metadata": {
    "tool": "compliance_validate_oscal_ssp",
    "duration_ms": 3200,
    "timestamp": "2026-03-10T16:30:00Z"
  }
}
```

### Success Response (Invalid)

```json
{
  "status": "success",
  "data": {
    "is_valid": false,
    "errors": [
      "Required section 'system-implementation' is missing",
      "Component UUID 'abc-123' referenced in by-components not found in system-implementation"
    ],
    "warnings": [
      "oscal-version is '1.0.6' — expected '1.1.2'"
    ],
    "statistics": {
      "control_count": 0,
      "component_count": 0,
      "inventory_item_count": 0,
      "user_count": 0,
      "back_matter_resource_count": 0
    }
  },
  "metadata": {
    "tool": "compliance_validate_oscal_ssp",
    "duration_ms": 2800,
    "timestamp": "2026-03-10T16:35:00Z"
  }
}
```

---

## Tool 6 (Modified): compliance_generate_ssp

**Purpose**: Generate full or partial SSP document (existing tool, enhanced for 13 sections).
**RBAC**: `PimTier.Read`

### Parameter Changes

| Parameter | Change |
|-----------|--------|
| `sections` | New values: `system_identification`, `categorization`, `personnel`, `system_type`, `description`, `environment`, `interconnections`, `laws_regulations`, `minimum_controls`, `control_implementations`, `authorization_boundary`, `personnel_security`, `contingency_plan`. Old values (`system_information`, `baseline`, `controls`) mapped to new keys for backward compatibility. |

### Backward Compatibility Map

| Old Key | New Key | NIST § |
|---------|---------|--------|
| `system_information` | `system_identification` | §1 |
| `baseline` | `minimum_controls` | §9 |
| `controls` | `control_implementations` | §10 |

---

## Service Interfaces

### IOscalSspExportService

```csharp
public interface IOscalSspExportService
{
    /// <summary>Export OSCAL 1.1.2 SSP JSON for the specified system.</summary>
    Task<OscalExportResult> ExportAsync(
        string registeredSystemId,
        bool includeBackMatter = true,
        bool prettyPrint = true,
        CancellationToken cancellationToken = default);
}

/// <summary>Result of OSCAL SSP export including JSON and warnings.</summary>
public record OscalExportResult(
    string OscalJson,
    List<string> Warnings,
    OscalStatistics Statistics);

/// <summary>Summary statistics for the generated OSCAL SSP.</summary>
public record OscalStatistics(
    int ControlCount,
    int ComponentCount,
    int InventoryItemCount,
    int UserCount,
    int BackMatterResourceCount);
```

### IOscalValidationService

```csharp
public interface IOscalValidationService
{
    /// <summary>Validate OSCAL SSP JSON for structural correctness.</summary>
    Task<OscalValidationResult> ValidateSspAsync(
        string oscalJson,
        CancellationToken cancellationToken = default);
}

/// <summary>Result of OSCAL structural validation.</summary>
public record OscalValidationResult(
    bool IsValid,
    List<string> Errors,
    List<string> Warnings,
    OscalStatistics Statistics);
```

### ISspService (New Methods)

```csharp
// Add to existing ISspService interface:

/// <summary>Write or update an SSP section.</summary>
Task<SspSection> WriteSspSectionAsync(
    string registeredSystemId,
    int sectionNumber,
    string? content,
    string authoredBy,
    int? expectedVersion = null,
    bool submitForReview = false,
    CancellationToken cancellationToken = default);

/// <summary>Review (approve/reject) an SSP section.</summary>
Task<SspSection> ReviewSspSectionAsync(
    string registeredSystemId,
    int sectionNumber,
    string decision,
    string reviewer,
    string? comments = null,
    CancellationToken cancellationToken = default);

/// <summary>Get SSP section completeness status for a system.</summary>
Task<SspCompletenessReport> GetSspCompletenessAsync(
    string registeredSystemId,
    CancellationToken cancellationToken = default);
```

### SspCompletenessReport

```csharp
public record SspCompletenessReport(
    string SystemName,
    double OverallReadinessPercent,
    int ApprovedCount,
    int TotalSections,
    List<SspSectionSummary> Sections,
    List<string> BlockingIssues);

public record SspSectionSummary(
    int SectionNumber,
    string SectionTitle,
    string Status,
    bool IsAutoGenerated,
    bool HasManualOverride,
    string? AuthoredBy,
    DateTime? AuthoredAt,
    int WordCount,
    int Version);
```
