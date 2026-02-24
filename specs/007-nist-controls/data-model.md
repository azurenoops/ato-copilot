# Data Model: NIST Controls Knowledge Foundation

**Feature Branch**: `007-nist-controls` | **Date**: 2026-02-23

## Entity Overview

This feature introduces two categories of data models:

1. **OSCAL Deserialization Models** — C# records for `System.Text.Json` deserialization of the NIST SP 800-53 Rev 5 OSCAL catalog JSON. These are transient (in-memory cache only, not persisted to database).
2. **Service/Application Models** — Configuration, status, and enriched view types used by the service layer.

The existing **`NistControl` EF Core entity** (in `ComplianceModels.cs`) is preserved unchanged for database compatibility.

---

## OSCAL Deserialization Models

Location: `src/Ato.Copilot.Agents/Compliance/Models/OscalModels.cs`

All properties use `[JsonPropertyName]` attributes mapping to OSCAL's kebab-case JSON property names.

### NistCatalogRoot

Wrapper for the OSCAL JSON root object.

| Property | Type | JSON Name | Required | Description |
|----------|------|-----------|----------|-------------|
| Catalog | `NistCatalog` | `catalog` | Yes | The single catalog object |

### NistCatalog

Top-level catalog entity.

| Property | Type | JSON Name | Required | Description |
|----------|------|-----------|----------|-------------|
| Uuid | `string` | `uuid` | Yes | Catalog UUID |
| Metadata | `CatalogMetadata` | `metadata` | Yes | Catalog metadata |
| Groups | `List<ControlGroup>` | `groups` | Yes | 20 control family groups |
| BackMatter | `BackMatter?` | `back-matter` | No | Reference resources |

### CatalogMetadata

Catalog metadata from the `metadata` object.

| Property | Type | JSON Name | Required | Description |
|----------|------|-----------|----------|-------------|
| Title | `string` | `title` | Yes | Full catalog title |
| LastModified | `string` | `last-modified` | Yes | ISO 8601 timestamp |
| Version | `string` | `version` | Yes | Catalog version (e.g., `"5.2.0"`) |
| OscalVersion | `string` | `oscal-version` | Yes | OSCAL schema version (e.g., `"1.1.3"`) |
| Remarks | `string?` | `remarks` | No | Additional notes |

### ControlGroup

Represents one NIST control family.

| Property | Type | JSON Name | Required | Description |
|----------|------|-----------|----------|-------------|
| Id | `string` | `id` | Yes | Family ID (e.g., `"ac"`, `"sc"`) |
| Class | `string?` | `class` | No | Always `"family"` |
| Title | `string` | `title` | Yes | Family name (e.g., `"Access Control"`) |
| Props | `List<ControlProperty>?` | `props` | No | Property annotations |
| Controls | `List<OscalControl>` | `controls` | Yes | Controls in this family |

### OscalControl

Individual NIST control (deserialization model, distinct from EF `NistControl` entity).

| Property | Type | JSON Name | Required | Description |
|----------|------|-----------|----------|-------------|
| Id | `string` | `id` | Yes | Control ID (e.g., `"ac-2"`, `"sc-7"`) |
| Class | `string?` | `class` | No | Always `"SP800-53"` |
| Title | `string` | `title` | Yes | Control title |
| Params | `List<ControlParam>?` | `params` | No | Organization-defined parameters |
| Props | `List<ControlProperty>?` | `props` | No | Label, sort-id, implementation-level |
| Links | `List<ControlLink>?` | `links` | No | References to other controls |
| Parts | `List<ControlPart>?` | `parts` | No | Statement, guidance, objectives |
| Controls | `List<OscalControl>?` | `controls` | No | Nested enhancements (e.g., `ac-2.1`) |

### ControlProperty

Name-value property annotation on controls/groups.

| Property | Type | JSON Name | Required | Description |
|----------|------|-----------|----------|-------------|
| Name | `string` | `name` | Yes | Property name (e.g., `"label"`, `"sort-id"`) |
| Value | `string` | `value` | Yes | Property value |
| Class | `string?` | `class` | No | Property class (e.g., `"sp800-53a"`) |
| Ns | `string?` | `ns` | No | Namespace URI |

### ControlPart

Recursive part hierarchy carrying control text content.

| Property | Type | JSON Name | Required | Description |
|----------|------|-----------|----------|-------------|
| Id | `string?` | `id` | No | Part ID (e.g., `"ac-2_smt"`, `"ac-2_gdn"`) |
| Name | `string` | `name` | Yes | Discriminator: `"statement"`, `"guidance"`, `"assessment-objective"`, `"assessment-method"`, `"item"` |
| Props | `List<ControlProperty>?` | `props` | No | Part properties (e.g., method label) |
| Prose | `string?` | `prose` | No | Text content |
| Parts | `List<ControlPart>?` | `parts` | No | Nested sub-parts (recursive) |

### ControlParam

Organization-defined parameter for control customization.

| Property | Type | JSON Name | Required | Description |
|----------|------|-----------|----------|-------------|
| Id | `string` | `id` | Yes | Param ID (e.g., `"ac-02_odp.01"`) |
| Label | `string?` | `label` | No | Human-readable label |
| Props | `List<ControlProperty>?` | `props` | No | Alt-identifier, label props |
| Guidelines | `List<ControlGuideline>?` | `guidelines` | No | Usage guidance |

### ControlGuideline

Guidance text for a parameter.

| Property | Type | JSON Name | Required | Description |
|----------|------|-----------|----------|-------------|
| Prose | `string` | `prose` | Yes | Guideline text content |

### ControlLink

Reference link between controls.

| Property | Type | JSON Name | Required | Description |
|----------|------|-----------|----------|-------------|
| Href | `string` | `href` | Yes | Link target (UUID fragment or URL) |
| Rel | `string?` | `rel` | No | Relationship type (e.g., `"related"`) |

### BackMatter

Container for reference resources.

| Property | Type | JSON Name | Required | Description |
|----------|------|-----------|----------|-------------|
| Resources | `List<BackMatterResource>?` | `resources` | No | Referenced documents |

### BackMatterResource

A single back-matter resource.

| Property | Type | JSON Name | Required | Description |
|----------|------|-----------|----------|-------------|
| Uuid | `string` | `uuid` | Yes | Resource UUID |
| Title | `string?` | `title` | No | Resource title |

---

## Service & Application Models

### ControlEnhancement (Enriched View)

Location: `src/Ato.Copilot.Agents/Compliance/Models/OscalModels.cs`

Enriched view of a control's statement, guidance, and objectives — produced by `GetControlEnhancementAsync`.

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| Id | `string` | Yes | Control ID (e.g., `"AC-2"`) |
| Title | `string` | Yes | Control title |
| Statement | `string` | Yes | Concatenated statement prose from `name="statement"` parts |
| Guidance | `string` | Yes | Concatenated guidance prose from `name="guidance"` parts |
| Objectives | `List<string>` | Yes | List of assessment objective prose strings |
| LastUpdated | `DateTime` | Yes | Timestamp of extraction |

### NistControlsOptions (Configuration)

Location: `src/Ato.Copilot.Agents/Compliance/Configuration/ComplianceAgentOptions.cs` (refactored from dead code)

Bound from `Agents:Compliance:NistControls` config section.

| Property | Type | Default | Validation | Description |
|----------|------|---------|------------|-------------|
| BaseUrl | `string` | `""` | `[Required]` | OSCAL catalog remote URL |
| TimeoutSeconds | `int` | `60` | `[Range(10, 300)]` | HTTP request timeout |
| CacheDurationHours | `int` | `24` | `[Range(1, 168)]` | Cache TTL in hours |
| MaxRetryAttempts | `int` | `3` | `[Range(1, 5)]` | Polly retry limit |
| RetryDelaySeconds | `int` | `2` | `[Range(1, 60)]` | Polly base delay |
| EnableOfflineFallback | `bool` | `true` | — | Controls whether the embedded OSCAL resource is used as fallback when the remote fetch fails. When `false`, the service returns `null` on remote failure (useful for testing remote-only scenarios). The embedded resource is always compiled into the assembly regardless of this setting. |
| WarmupDelaySeconds | `int` | `10` | `[Range(5, 60)]` | Initial warmup delay after startup |

### CatalogStatus (Status Snapshot)

Location: `src/Ato.Copilot.Agents/Compliance/Services/NistControlsService.cs` (existing nested class, enhanced)

| Property | Type | Description |
|----------|------|-------------|
| IsLoaded | `bool` | Whether catalog is currently cached |
| CatalogSource | `string` | `"remote"`, `"embedded"`, or `"none"` |
| LastSyncedAt | `DateTime?` | UTC timestamp of last successful load |
| TotalControls | `int` | Number of controls loaded |
| FamilyCount | `int` | Number of control groups |
| Version | `string?` | Catalog version from metadata |

---

## Relationships

```
NistCatalogRoot
  └── NistCatalog (1)
        ├── CatalogMetadata (1)
        ├── ControlGroup (20)
        │     └── OscalControl (N per group)
        │           ├── ControlProperty (N)
        │           ├── ControlPart (N, recursive)
        │           │     └── ControlPart (N, recursive)
        │           ├── ControlParam (N)
        │           ├── ControlLink (N)
        │           └── OscalControl (N, enhancements)
        └── BackMatter (0..1)
              └── BackMatterResource (N)

NistControlsService
  ├── uses NistCatalogRoot (deserialization)
  ├── caches NistCatalog (IMemoryCache)
  ├── returns NistControl (EF entity, unchanged)
  └── returns ControlEnhancement (enriched view)
```

## State Transitions

### Catalog Loading States

```
┌──────────┐    startup    ┌────────────┐   fetch OK    ┌──────────┐
│  EMPTY   │──────────────►│  LOADING   │──────────────►│  CACHED  │
└──────────┘               └────────────┘               └──────────┘
                                 │                           │
                            fetch fail                  TTL expired
                                 │                       (90% of TTL)
                                 ▼                           │
                           ┌────────────┐                    │
                           │  FALLBACK  │                    ▼
                           │ (embedded) │◄─── fetch fail ┌──────────┐
                           └────────────┘                │REFRESHING│
                                 │                       └──────────┘
                            load OK                          │
                                 │                       fetch OK
                                 ▼                           │
                           ┌──────────┐                      ▼
                           │  CACHED  │◄─────────────────────┘
                           │(fallback)│
                           └──────────┘
```

### Health Check States

| State | Condition |
|-------|-----------|
| `Healthy` | Version available + 3/3 test controls valid + response < 5s |
| `Degraded` | Version available + 1-2/3 test controls valid |
| `Unhealthy` | Version unavailable OR 0/3 test controls valid OR exception |

## Validation Rules

| Entity | Rule |
|--------|------|
| Control ID | Case-insensitive match; supports `AC-2`, `ac-2`, `AC-2(1)`, `ac-2.1` formats |
| Family ID | Case-insensitive 2-letter prefix match (e.g., `ac`, `SC`, `Au`) |
| Search query | Non-empty string; matched against ID, title, and prose |
| Config: BaseUrl | `[Required]` — must be non-empty |
| Config: TimeoutSeconds | `[Range(10, 300)]` |
| Config: CacheDurationHours | `[Range(1, 168)]` (1 hour to 1 week) |
| Config: MaxRetryAttempts | `[Range(1, 5)]` |
| Config: RetryDelaySeconds | `[Range(1, 60)]` |
